using LanguageCore;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<IEnumerable<InlineValueBase>?> InlineValues(InlineValueParams request, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, cancellationToken).ConfigureAwait(false);

        Range<SinglePosition> range = request.Range.ToCool();
        Range<SinglePosition> stopped = request.Context.StoppedLocation.ToCool();
        List<InlineValueBase> result = new();

        Logger.Debug($"Inline values at {stopped}");

        foreach (var v in AST.AllFunctions)
        {
            if (v.Location.File != request.TextDocument.Uri) continue;
            if (!RangeUtils.Overlaps(stopped, v.Location.Position.Range)) continue;
            range = RangeUtils.Intersect(range, v.Location.Position.Range);
        }

        foreach (var v in AST.AllFunctions)
        {
            if (v.Location.File != request.TextDocument.Uri) continue;
            if (!RangeUtils.Overlaps(range, v.Location.Position.Range)) continue;
            foreach (var w in v.Parameters.Parameters)
            {
                result.Add(new InlineValueVariableLookup()
                {
                    Range = w.Identifier.Position.ToOmniSharp(),
                    VariableName = w.Identifier.Content,
                    CaseSensitiveLookup = true,
                });
            }
        }

        foreach (var v in AST.EnumerateStatements())
        {
            if (v.Location.File != request.TextDocument.Uri) continue;
            if (!RangeUtils.Overlaps(range, v.Location.Position.Range)) continue;

            if (v is VariableDefinition variableDefinition)
            {
                result.Add(new InlineValueVariableLookup()
                {
                    Range = variableDefinition.Identifier.Position.ToOmniSharp(),
                    VariableName = variableDefinition.Identifier.Content,
                    CaseSensitiveLookup = true,
                });
            }
            else if (v is AnyCallExpression anyCallExpression
                && RangeUtils.Overlaps(stopped, v.Location.Position.Range)
                && anyCallExpression.ToFunctionCall(out FunctionCallExpression? functionCallExpression))
            {
                result.Add(new InlineValueVariableLookup()
                {
                    Range = v.Position.ToOmniSharp(),
                    VariableName = $"{functionCallExpression.Identifier.Content}({(functionCallExpression.Arguments.Arguments.Length > 0 ? "..." : null)})",
                    CaseSensitiveLookup = true,
                });
            }
        }

        return result;
    }
}
