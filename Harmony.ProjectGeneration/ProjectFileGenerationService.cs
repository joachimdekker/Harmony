using System.IO.Compression;
using System.Xml.Linq;
using Harmony.Domain;

namespace Harmony.ProjectGeneration;

public class ProjectFileGenerationService
{
    public string MirrorBaseUrl { get; set; } =
        "https://github.com/needle-mirror/{PackageName}/archive/refs/tags/{Version}.zip";

    public string ProjectLocation { get; set; }
    public string EditorLocation { get; set; }

    string schema = "{http://schemas.microsoft.com/developer/msbuild/2003}";

    public string PackageCacheLocation => ProjectLocation + "/PackageCache";

    private HashSet<string> _generatedProjects = new();

    public bool usingNupkg { get; set; } = false;

    public ProjectFileGenerationService(string projectLocation, string editorLocation)
    {
        ProjectLocation = projectLocation;
        EditorLocation = editorLocation;
        Directory.CreateDirectory(ProjectLocation);
    }

    public async Task GenerateProjectFile(AssemblyDefinition assemblyDefinition)
    {
        // Copy the default csproj file to the project location
        string directory = Path.GetDirectoryName(assemblyDefinition.PathLocation);
        string projectFileLocation = Path.Combine(directory, $"{assemblyDefinition.Name}.csproj");

        // Parse the default project file as XML
        XElement projectFile = XElement.Load("DefaultProject.csproj.default");

        // Replace all hintPaths with the correct paths
        var hintPaths = projectFile.Descendants("HintPath");
        foreach (XElement hintPath in hintPaths)
        {
            string relativePath = hintPath.Value;
            string expandedPath = relativePath
                .Replace("{UnityEditorInstallationPath}", EditorLocation)
                .Replace("{ProjectRoot}", ProjectLocation);
            hintPath.Value = expandedPath;
        }

        // If the assembly is not user-made, don't display warnings
        if (!assemblyDefinition.IsUserAssembly)
        {
            XElement warningLevelElement = projectFile.Descendants("WarningLevel").First();
            warningLevelElement.Value = "0";
        }

        // Replace whether unsafe blocks are allowed
        XElement unsafeBlocksElement = projectFile.Descendants("AllowUnsafeBlocks").First();
        unsafeBlocksElement.Value = assemblyDefinition.AllowUnsafeCode.ToString().ToLower();

        // Replace the package destination folder
        var copy = projectFile.Descendants("Copy");
        foreach (XElement copyElement in copy)
        {
            string destination = copyElement.Attribute("DestinationFolder")!.Value;
            string expandedPath = destination
                .Replace("{PackageDestinationFolder}", PackageCacheLocation);
            copyElement.Attribute("DestinationFolder")!.Value = expandedPath;
        }

        // Add all references as NuGet references to the project file
        XElement referencesItemGroup = new("ItemGroup", new XAttribute("Label", "References"));
        foreach (AssemblyDefinition reference in assemblyDefinition.References.Where(x => x is not BuiltInAssemblyDefinition))
        {
            XElement referenceElement = usingNupkg ? this.ReferenceProjectUsingNupkg(reference) : ReferenceProjectUsingReference(reference);

            referencesItemGroup.Add(referenceElement);
        }

        // Add the references to the project file
        projectFile.Add(referencesItemGroup);

        // Add the dlls to the project file
        if (assemblyDefinition.PrecompiledReferences.Count > 0)
        {
            XElement compileItemGroup = new(schema + "ItemGroup", new XAttribute("Label", "Compile"));
            foreach (string precompiledReference in assemblyDefinition.PrecompiledReferences)
            {
                // Generate a reference element with the name of the reference.
                string referenceName = Path.GetFileNameWithoutExtension(precompiledReference);
                XElement referenceElement = new XElement(schema + "Reference",
                    new XAttribute("Include", referenceName)
                );

                // Add the hint path
                referenceElement.Add(new XElement(schema + "HintPath", precompiledReference));

                compileItemGroup.Add(referenceElement);
            }

            projectFile.Add(compileItemGroup);
        }

        // Remove Engine Packages if specified
        XElement enginePackagesItemGroup = projectFile.Descendants("ItemGroup").First(x => x.Attribute("Label")?.Value == "Engine Packages");
        if (assemblyDefinition.NoEngineReferences)
        {
            enginePackagesItemGroup.Remove();
        }
        else
        {
            // Add every .dll in the PrecompiledAssemblies folder
            string[] dlls = Directory.GetFiles("PrecompiledAssemblies", "*.dll");

            foreach (string dll in dlls)
            {
                XElement referenceElement = new XElement("Reference",
                    new XAttribute("Include", Path.GetFileNameWithoutExtension(dll))
                );
                string fullPath = Path.GetFullPath(dll);
                referenceElement.Add(new XElement("HintPath", fullPath));
                enginePackagesItemGroup.Add(referenceElement);
            }
        }


        // Specify which folders to exclude
        if (assemblyDefinition.ExcludedFolders.Count > 0)
        {
            XElement excludeItemGroup = new("ItemGroup", new XAttribute("Label", "FilesCompile"));
            string excludedFiles = string.Join(';', assemblyDefinition.ExcludedFolders.Select(x => x + "/**/*.cs"));
            //excludeItemGroup.Add(new XElement("Compile", new XAttribute("Include", "**/*.cs"), new XAttribute("Exclude", excludedFiles)));

            excludeItemGroup.Add(new XElement("Compile", new XAttribute("Remove", excludedFiles)));

            projectFile.Add(excludeItemGroup);
        }

        // Save the project file
        projectFile.Save(projectFileLocation);
    }

    private XElement ReferenceProjectUsingNupkg(AssemblyDefinition reference)
    {
        // Add the reference to the project file
        XElement referenceElement = new XElement("PackageReference",
            new XAttribute("Include", reference.Name)
        );

        // Add the source for the reference
        string nupkgPath = Path.Combine(this.PackageCacheLocation, reference.Name) + ".nupkg";
        referenceElement.Add(new XElement("Source", nupkgPath));
        return referenceElement;
    }

    private XElement ReferenceProjectUsingReference(AssemblyDefinition reference)
    {
        // Add a project reference to the project file
        string csprojPath = Path.Combine(Path.GetDirectoryName(reference.PathLocation), reference.Name + ".csproj");
        XElement referenceElement = new XElement("ProjectReference",
            new XAttribute("Include", csprojPath)
        );

        return referenceElement;
    }

    public async Task DownloadSourceCode(PackageDependency dependency)
    {
        if (dependency.Source != "registry")
        {
            Console.WriteLine($"Skipping {dependency.Name} because it's not from the registry");
            return;
        }
        string folderName = $"{dependency.Name}-{dependency.Version}";

        // Check if the source code has already been downloaded
        if (Directory.Exists(Path.Combine(ProjectLocation, folderName)))
        {
            Console.WriteLine($"Skipping {dependency.Name} because it's already been downloaded");

            // Add the project to the generated projects
            _generatedProjects.Add(folderName);

            return;
        }

        // Construct the URL for the mirror
        string url = MirrorBaseUrl
            .Replace("{PackageName}", dependency.Name)
            .Replace("{Version}", dependency.Version);

        // Download the source code zip
        using HttpClient client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync(url);

        // If the response was not succesful, log a warning and continue
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to download source code for {dependency.Name}");
            return;
        }

        // Save the zip file
        string zipPath = Path.Combine(ProjectLocation, $"{dependency.Name}-{dependency.Version}.zip");
        using (FileStream fileStream = File.Create(zipPath))
        {
            await response.Content.CopyToAsync(fileStream);

            // Unzip the file
            string extractPath = Path.Combine(ProjectLocation, $"{dependency.Name}-{dependency.Version}");
            Directory.CreateDirectory(extractPath);
            using ZipArchive archive = new(fileStream);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.Combine(extractPath, entry.FullName);

                // Ensure the directory exists
                string directoryPath = Path.GetDirectoryName(destinationPath)!;
                Directory.CreateDirectory(directoryPath);

                // Extract the file if it's not a directory
                if (!entry.FullName.EndsWith('/'))
                {
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        // Add the project to the generated projects
        _generatedProjects.Add(folderName);

        // Clean up
        File.Delete(zipPath);
    }
}
