using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public async override Task<IEnumerable<FoldingRange>?> FoldingRange(FoldingRangeRequestParam request, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, cancellationToken).ConfigureAwait(false);

        List<FoldingRange> result = new();

        foreach (var v in AST.AllFunctions)
        {
            result.Add(new()
            {
                StartLine = v.Position.Range.Start.Line,
                StartCharacter = v.Position.Range.Start.Character,
                EndLine = v.Position.Range.End.Line,
                EndCharacter = v.Position.Range.End.Character,
            });
        }

        foreach (var v in AST.Structs)
        {
            result.Add(new()
            {
                StartLine = v.Position.Range.Start.Line,
                StartCharacter = v.Position.Range.Start.Character,
                EndLine = v.Position.Range.End.Line,
                EndCharacter = v.Position.Range.End.Character,
            });
        }

        foreach (var v in AST.EnumDefinitions)
        {
            result.Add(new()
            {
                StartLine = v.Position.Range.Start.Line,
                StartCharacter = v.Position.Range.Start.Character,
                EndLine = v.Position.Range.End.Line,
                EndCharacter = v.Position.Range.End.Character,
            });
        }

        foreach (var v in AST.EnumerateStatements())
        {
            switch (v)
            {
                case IfBranchStatement w:
                    result.Add(new()
                    {
                        StartLine = w.Position.Range.Start.Line,
                        StartCharacter = w.Position.Range.Start.Character,
                        EndLine = w.Position.Range.End.Line,
                        EndCharacter = w.Position.Range.End.Character,
                    });
                    break;
                case ElseBranchStatement w:
                    result.Add(new()
                    {
                        StartLine = w.Position.Range.Start.Line,
                        StartCharacter = w.Position.Range.Start.Character,
                        EndLine = w.Position.Range.End.Line,
                        EndCharacter = w.Position.Range.End.Character,
                    });
                    break;
                case WhileLoopStatement w:
                    result.Add(new()
                    {
                        StartLine = w.Position.Range.Start.Line,
                        StartCharacter = w.Position.Range.Start.Character,
                        EndLine = w.Position.Range.End.Line,
                        EndCharacter = w.Position.Range.End.Character,
                    });
                    break;
                case ForLoopStatement w:
                    result.Add(new()
                    {
                        StartLine = w.Position.Range.Start.Line,
                        StartCharacter = w.Position.Range.Start.Character,
                        EndLine = w.Position.Range.End.Line,
                        EndCharacter = w.Position.Range.End.Character,
                    });
                    break;
            }
        }

        return result;
    }
}
