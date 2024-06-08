using System.Collections.Generic;
using Harmony.Domain;

namespace Harmony.ProjectGeneration;

public record PackageDependencies
{
    public List<PackageDependency> RegistryDependencies { get; init; } = [];
}
