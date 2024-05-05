using System.Collections.Generic;

namespace Harmony.ProjectGeneration
{
    public record PackageDependency
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
        public required string Source { get; init; }
        public required int Depth { get; init; }
        public required List<PackageDependency> Dependencies { get; init; }
        public string? Url { get; init; }

        public virtual bool Equals(PackageDependency? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Name == other.Name && Version == other.Version && Source == other.Source;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Version.GetHashCode() ^ Source.GetHashCode();
        }
    }
}
