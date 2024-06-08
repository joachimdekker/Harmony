using System.Text.Json;
using Harmony.Domain;

namespace Harmony.ProjectGeneration;

public class AssemblyDefinitionParser(MetaFileParser metaFileParser)
{
    private class AssemblyDefinitionFile
    {
        required public string Name { get; init; }
        public string PathLocation { get; set; }
        public Guid Id { get; set; }
        public string RootNamespace { get; init; } = String.Empty;
        public List<string> References { get; init;  } = [];
        public List<string> IncludePlatforms { get; init; } = [];
        public List<string> ExcludePlatforms { get; init; } = [];
        public bool AllowUnsafeCode { get; init; }
        public List<string> PrecompiledReferences { get; } = [];
        public List<string> DefineConstraints { get; } = [];
        public List<string> VersionDefines { get; } = [];
        public bool NoEngineReferences { get; init; }
        public bool AutoReferenced { get; init; }
    }

    public async Task<List<AssemblyDefinition>> ParseAll(IEnumerable<string> paths)
    {
        Dictionary<Guid, AssemblyDefinitionFile> assemblyDefinitionsFiles = new();

        // Parse all the files
        foreach(string path in paths)
        {
            try
            {
                AssemblyDefinitionFile assemblyDefinitionFile = await Parse(path);

                // If the file already exists, skip it
                assemblyDefinitionsFiles.TryAdd(assemblyDefinitionFile.Id, assemblyDefinitionFile);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Meta file with path {path}.meta could not be found.");
            }
        }

        List<AssemblyDefinition> assemblyDefinitions = assemblyDefinitionsFiles.Select(x => new AssemblyDefinition()
                {
                    Id = x.Value.Id,
                    PathLocation = x.Value.PathLocation,
                    Name = x.Value.Name,
                    RootNamespace = x.Value.RootNamespace,
                    IncludePlatforms = x.Value.IncludePlatforms,
                    ExcludePlatforms = x.Value.ExcludePlatforms,
                    AllowUnsafeCode = x.Value.AllowUnsafeCode,
                    PrecompiledReferences = x.Value.PrecompiledReferences,
                    DefineConstraints = x.Value.DefineConstraints,
                    VersionDefines = x.Value.VersionDefines,
                    NoEngineReferences = x.Value.NoEngineReferences,
                    AutoReferenced = x.Value.AutoReferenced,

                }
            )
            .ToList();

        // Assign the references
        foreach(AssemblyDefinition assemblyDefinition in assemblyDefinitions)
        {
            List<string> references = assemblyDefinitionsFiles[assemblyDefinition.Id].References;

            // Determine if the reference are GUIDs or file paths
            if (references.Any(x => x.StartsWith("GUID:")))
            {
                assemblyDefinition.References = references
                    .Select(x => Guid.Parse(x.Replace("GUID:", "")))
                    .Select(x => assemblyDefinitions.FirstOrDefault(y => y.Id == x, new BuiltInAssemblyDefinition(x.ToString())))
                    .ToList();
            }
            else
            {
                assemblyDefinition.References = references
                    .Select(x => assemblyDefinitions.FirstOrDefault(y => y.Name == x, new BuiltInAssemblyDefinition(x)))
                    .ToList();
            }
        }

        return assemblyDefinitions;
    }

    private async Task<AssemblyDefinitionFile> Parse(string path)
    {
        path = Path.GetFullPath(path);

        // Parse the meta file
        string metaFilePath = $"{path}.meta";
        MetaFile metaFile = await metaFileParser.Parse(metaFilePath);

        // Get the stream of the file and parse the file
        await using FileStream stream = File.OpenRead(path);

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
        };
        AssemblyDefinitionFile? assemblyDefinitionFile = await JsonSerializer.DeserializeAsync<AssemblyDefinitionFile>(stream, options);

        if (assemblyDefinitionFile is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // Assign the id from the meta file
        assemblyDefinitionFile.Id = metaFile.Id;
        assemblyDefinitionFile.PathLocation = path;

        return assemblyDefinitionFile;
    }
}
