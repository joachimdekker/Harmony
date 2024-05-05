using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Harmony.ProjectGeneration;

public class PackageFileParser
{
    internal class PackageLockFile
    {
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

    public async Task<PackageDependencyFile> Parse(string path)
    {
        using var stream = File.OpenRead(path);
        var packageLockFile = await JsonSerializer.DeserializeAsync<PackageLockFile>(stream);

        if (packageLockFile is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        // Convert the PackageLockFile to a PackageDependencyFile
        var dependencies = packageLockFile.Dependencies.Select(d => new PackageDependency
        {
            Name = d.Key,
            Version = d.Value.Version,
            Source = d.Value.Source,
            Depth = d.Value.Depth,
            Dependencies = GetDependencies(packageLockFile.Dependencies, d.Value.Dependencies),
            Url = d.Value.Url,
        }).ToList();

        return new PackageDependencyFile
        {
            Dependencies = dependencies,
        };
    }

    private static List<PackageDependency> GetDependencies(Dictionary<string, PackageLockDependency> dependecyStore, Dictionary<string, string> dependencies)
    {
        // Get the dependency keys
        var dependencyKeys = dependencies.Keys;

        // Get the dependencies from the dependency store
        return dependencyKeys.Select(key =>
            {
                var depency = dependecyStore[key];
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
