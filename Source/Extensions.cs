using System.IO;
using LanguageCore;
using OmniSharpPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using OmniSharpRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using OmniSharpLocation = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;
using Position = LanguageCore.Position;
using System.Diagnostics;

namespace LanguageServer;

static class Extensions
{
    public static DiagnosticSeverity ToOmniSharp(this DiagnosticsLevel level) => level switch
    {
        DiagnosticsLevel.Error => DiagnosticSeverity.Error,
        DiagnosticsLevel.Warning => DiagnosticSeverity.Warning,
        DiagnosticsLevel.Information => DiagnosticSeverity.Information,
        DiagnosticsLevel.Hint => DiagnosticSeverity.Hint,
        DiagnosticsLevel.OptimizationNotice => DiagnosticSeverity.Information,
        DiagnosticsLevel.FailedOptimization => DiagnosticSeverity.Information,
        _ => throw new UnreachableException(),
    };

    public static string GetExtension(this DocumentUri uri)
        => Path.GetExtension(uri.ToUri().AbsolutePath).TrimStart('.').ToLowerInvariant();

    public static OmniSharpRange ToOmniSharp(this Range<SinglePosition> self) => new()
    {
        Start = self.Start.ToOmniSharp(),
        End = self.End.ToOmniSharp(),
    };

    public static OmniSharpPosition ToOmniSharp(this SinglePosition self) => new()
    {
        Line = self.Line,
        Character = self.Character,
    };

    public static OmniSharpRange ToOmniSharp(this Position self) => new()
    {
        Start = self.Range.Start.ToOmniSharp(),
        End = self.Range.End.ToOmniSharp(),
    };

    public static MutableRange<SinglePosition> ToCool(this OmniSharpRange self) => new()
    {
        Start = self.Start.ToCool(),
        End = self.End.ToCool(),
    };

    public static SinglePosition ToCool(this OmniSharpPosition self) => new()
    {
        Line = self.Line,
        Character = self.Character,
    };

    public static OmniSharpLocation ToOmniSharp(this LanguageCore.Location self) => new()
    {
        Uri = self.File,
        Range = self.Position.ToOmniSharp(),
    };

    public static string ToReadable(this OmniSharpPosition self) => self.ToCool().ToReadable();
}
