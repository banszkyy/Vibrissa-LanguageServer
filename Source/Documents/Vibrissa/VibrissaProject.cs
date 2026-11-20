using System.Collections.Concurrent;
using LanguageCore.Workspaces;

namespace LanguageServer;

sealed class VibrissaProject
{
    public required Configuration Configuration;
    public readonly HashSet<Uri> Files = new();
    public Uri? ConfigurationPath;

    public static ConcurrentDictionary<Uri, VibrissaProject> Projects { get; } = new();
}
