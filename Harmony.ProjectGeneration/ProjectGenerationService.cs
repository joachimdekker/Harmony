using Harmony.Domain;

namespace Harmony.ProjectGeneration
{
    public class ProjectGenerationService
    {
        private readonly ProjectFileGenerationService _projectFileGenerationService;
        private readonly AssemblyDefinitionParser _assemblyDefinitionParser;
        private readonly PackageFileParser _packageFileParser;

        public ProjectGenerationService(ProjectFileGenerationService projectFileGenerationService,
            PackageFileParser packageFileParser,
            AssemblyDefinitionParser assemblyDefinitionParser)
        {
            this._projectFileGenerationService = projectFileGenerationService;
            this._packageFileParser = packageFileParser;
            this._assemblyDefinitionParser = assemblyDefinitionParser;
        }

        public async Task GenerateProject(string projectName, string? projectPackageLockFilePath = null)
        {
            Console.WriteLine($"Generating project {projectName}");

            // Normalize the project base path
            string projectBasePath = Path.GetFullPath(this._projectFileGenerationService.ProjectLocation);

            // Get the package lock file
            projectPackageLockFilePath ??= Path.Combine(projectBasePath, Path.Join("Packages", "packages-lock.json"));
            projectPackageLockFilePath = Path.GetFullPath(projectPackageLockFilePath);

            // Parse the package lock file
            PackageDependencies packageDependencies = await _packageFileParser.Parse(projectPackageLockFilePath);

            // Get the dependencies that need to be compiled
            IEnumerable<PackageDependency> dependencies = packageDependencies.RegistryDependencies
                .Where(package => package.Source != "builtin");

            // For every dependency: download the source code, generate the project file
            this._projectFileGenerationService.ProjectLocation = Path.Combine(projectBasePath, "Packages");
            foreach (PackageDependency dependency in dependencies)
            {
                Console.WriteLine($"Downloading source code for {dependency.Name}");
                await _projectFileGenerationService.DownloadSourceCode(dependency);
            }

            // Get all the assembly definition files and the user defined ones
            string[] asmdefFiles = Directory.GetFiles(Path.GetDirectoryName(projectBasePath), "*.asmdef", SearchOption.AllDirectories);

            // Parse the assembly definition files
            List<AssemblyDefinition> assemblyDefinitions = await _assemblyDefinitionParser.ParseAll(asmdefFiles);

            // Get the assembly definitions that need to be compiled
            IEnumerable<AssemblyDefinition> userMadeAssemblyDefinitions = assemblyDefinitions
                .Where(asmdef => asmdef.PathLocation.StartsWith(Path.Combine(projectBasePath, "Assets")));

            foreach (AssemblyDefinition assemblyDefinition in userMadeAssemblyDefinitions)
            {
                assemblyDefinition.IsUserAssembly = true;
            }

            // Gather a list of all assembly definitions that need to be compiled using depth first search
            List<AssemblyDefinition> assemblyDefinitionsToCompile = GetAllDefinitions(userMadeAssemblyDefinitions);

            // Generate the project file
            foreach (AssemblyDefinition assemblyDefinition in assemblyDefinitionsToCompile)
            {
                // First expand the precompiled references
                assemblyDefinition.PrecompiledReferences = ExpandPrecompiledReferences(assemblyDefinition, projectBasePath);

                // Exclude folders that contain another asmdef file
                string[] asmdefFilesInFolder = Directory.GetFiles(Path.GetDirectoryName(assemblyDefinition.PathLocation)!, "*.asmdef", SearchOption.AllDirectories);
                List<string> foldersToExclude = asmdefFilesInFolder
                    .Where(asmdefFile => asmdefFile != assemblyDefinition.PathLocation)
                    .Select(asmdefFile => Path.GetDirectoryName(asmdefFile)!).ToList();
                assemblyDefinition.ExcludedFolders = foldersToExclude;

                // Generate the project file
                Console.WriteLine($"Generating project file for {assemblyDefinition.Name}");
                await _projectFileGenerationService.GenerateProjectFile(assemblyDefinition);
            }

            Console.WriteLine($"Project {projectName} generated");
        }

        private static List<string> ExpandPrecompiledReferences(AssemblyDefinition assemblyDefinition, string projectBasePath)
        {
            return assemblyDefinition.PrecompiledReferences.Select(reference =>
                {
                    // Search for the reference in the downloaded projects
                    string[] referenceFiles = Directory.GetFiles(Path.GetDirectoryName(projectBasePath),
                        reference,
                        SearchOption.AllDirectories
                    );

                    // Throw an exception when there is less or more than 1 file found
                    if (referenceFiles.Length < 1)
                    {
                        throw new FileNotFoundException($"Precompiled reference {reference} not found");
                    }

                    if (referenceFiles.Length > 1)
                    {
                        throw new FileNotFoundException(
                            $"Ambiguous reference: precompiled reference {reference} has multiple files"
                        );
                    }

                    // Add the reference to the assembly definition
                    return referenceFiles[0];
                }
            ).ToList();
        }

        private static List<AssemblyDefinition> GetAllDefinitions(IEnumerable<AssemblyDefinition> userMadeAssemblyDefinitions)
        {
            List<AssemblyDefinition> assemblyDefinitionsToCompile = new();
            Queue<AssemblyDefinition> queue = new(userMadeAssemblyDefinitions);
            while (queue.Count > 0)
            {
                AssemblyDefinition assemblyDefinition = queue.Dequeue();
                if (assemblyDefinitionsToCompile.Contains(assemblyDefinition))
                {
                    continue;
                }

                assemblyDefinitionsToCompile.Add(assemblyDefinition);
                foreach (AssemblyDefinition reference in assemblyDefinition.References)
                {
                    if (reference is BuiltInAssemblyDefinition)
                    {
                        continue;
                    }

                    queue.Enqueue(reference);
                }
            }

            return assemblyDefinitionsToCompile;
        }
    }
}
