using System;
using System.Diagnostics.CodeAnalysis;

namespace Harmony.Domain;

public class BuiltInAssemblyDefinition : AssemblyDefinition
{
    [SetsRequiredMembers]
    public BuiltInAssemblyDefinition(string name)
    {
        Name = name;
        Id = Guid.Empty;
        PathLocation = "builtin";
    }
}
