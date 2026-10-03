using LanguageCore.Workspaces;

namespace LanguageServer;

sealed class BBLangProject
{
    public required Configuration Configuration;
    public readonly HashSet<Uri> Files = new();
    public Uri? ConfigurationPath;

    public static Dictionary<Uri, BBLangProject> Projects { get; } = new();
}
