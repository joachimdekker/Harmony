using System;
using System.Collections.Generic;

namespace Harmony.Domain;

public class AssemblyDefinition
{
    required public Guid Id { get; init; }
    required public string Name { get; init; }

    required public string PathLocation { get; init; }

    public List<string> ExcludedFolders { get; set; } = [];

    public bool IsUserAssembly { get; set; } = false;

    public string RootNamespace { get; init; } = "";
    public List<AssemblyDefinition> References { get; set; } = [];
    public List<string> IncludePlatforms { get; init; } = [];
    public List<string> ExcludePlatforms { get; init; } = [];
    public bool AllowUnsafeCode { get; init; } = false;
    public List<string> PrecompiledReferences { get; set; } = [];
    public List<string> DefineConstraints { get; init; } = [];
    public List<string> VersionDefines { get; init; } = [];
    public bool NoEngineReferences { get; init; } = false;
    public bool AutoReferenced { get; init; } = false;
}
