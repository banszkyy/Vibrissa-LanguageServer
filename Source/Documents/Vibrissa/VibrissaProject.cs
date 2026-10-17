using LanguageCore.Workspaces;

namespace LanguageServer;

sealed class VibrissaProject
{
    public required Configuration Configuration;
    public readonly HashSet<Uri> Files = new();
    public Uri? ConfigurationPath;

    public static Dictionary<Uri, VibrissaProject> Projects { get; } = new();
}
