using System.Collections.Generic;

namespace Harmony.ProjectGeneration;

public record PackageDependencyFile
{
    public List<PackageDependency> Dependencies { get; set; }
}
