using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Harmony.Domain;

namespace Harmony.ProjectGeneration;

public class PackageFileParser
{
    internal class PackageLockFile
    {
        [JsonPropertyName("dependencies")]
        public Dictionary<string, PackageLockDependency> Dependencies { get; set; }
    }

    internal class PackageLockDependency
    {
        public required string Version { get; set; }
        public required string Source { get; set; }
        public required int Depth { get; set; }
        public required Dictionary<string, string> Dependencies { get; set; }
        public string? Url { get; set; }
    }

    public async Task<PackageDependencies> Parse(string path)
    {
        using FileStream stream = File.OpenRead(path);

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
        };
        PackageLockFile? packageLockFile = await JsonSerializer.DeserializeAsync<PackageLockFile>(stream, options);

        if (packageLockFile is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // Convert the PackageLockFile to a PackageDependencyFile
        List<PackageDependency> dependencies = packageLockFile.Dependencies.Select(d => new PackageDependency
        {
            Name = d.Key,
            Version = d.Value.Version,
            Source = d.Value.Source,
            Depth = d.Value.Depth,
            Dependencies = GetDependencies(packageLockFile.Dependencies, d.Value.Dependencies),
            Url = d.Value.Url,
        }).ToList();

        return new PackageDependencies
        {
            RegistryDependencies = dependencies,
        };
    }

    private static List<PackageDependency> GetDependencies(Dictionary<string, PackageLockDependency> dependecyStore, Dictionary<string, string> dependencies)
    {
        // Get the dependency keys
        Dictionary<string, string>.KeyCollection dependencyKeys = dependencies.Keys;

        // Get the dependencies from the dependency store
        return dependencyKeys.Select(key =>
            {
                PackageLockDependency depency = dependecyStore[key];
                return new PackageDependency
                {
                    Name = key,
                    Version = depency.Version,
                    Source = depency.Source,
                    Depth = depency.Depth,
                    Dependencies = GetDependencies(dependecyStore, depency.Dependencies),
                    Url = depency.Url,
                };
            }
        ).ToList();
    }
}
