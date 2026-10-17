using System.Collections.Immutable;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;
using Position = LanguageCore.Position;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    static bool GetGotoDefinition(object? reference, [NotNullWhen(true)] out LocationLink? result)
    {
        result = null;

        if (reference is null) return false;

        if (reference is StatementCompiler.FunctionQueryResult<CompiledFunctionDefinition> function)
        {
            reference = function.Function;
        }

        Uri? file = reference switch
        {
            IInFile inFile => inFile.File,
            ILocated located1 => located1.Location.File,
            _ => null,
        };

        if (file is null)
        {
            Logger.Warn($"Definition file is null");
            return false;
        }

        Position position = reference switch
        {
            IIdentifiable<Token> v => v.Identifier.Position,
            ILocated v => v.Location.Position,
            _ => Position.Zero,
        };

        if (position == Position.Zero || position == Position.UnknownPosition)
        {
            Logger.Warn($"Definition position is null");
            return false;
        }

        result = new LocationLink()
        {
            TargetRange = position.ToOmniSharp(),
            TargetSelectionRange = position.ToOmniSharp(),
            TargetUri = DocumentUri.From(file),
        };
        return true;
    }

    public override async Task<LocationOrLocationLinks?> GotoDefinition(DefinitionParams e, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, cancellationToken).ConfigureAwait(false);

        SinglePosition p = e.Position.ToCool();

        foreach (UsingDefinition @using in AST.Usings.IsDefault ? ImmutableArray<UsingDefinition>.Empty : AST.Usings)
        {
            if (@using.Path.IsEmpty) continue;
            var pathPos = new Position(@using.Path);

            if (!pathPos.Range.Contains(p)) continue;
            if (@using.CompiledUri is null) break;

            return new LocationOrLocationLinks(new LocationLink()
            {
                TargetUri = DocumentUri.From(@using.CompiledUri),
                OriginSelectionRange = pathPos.ToOmniSharp(),
                TargetRange = Position.Zero.ToOmniSharp(),
                TargetSelectionRange = Position.Zero.ToOmniSharp(),
            });
        }

        List<LocationOrLocationLink> links = new();

        Range<SinglePosition> range = default;
        object? reference = null;

        {
            if (GetTypeInstanceAt(p, false, out var type)
                && type is TypeInstanceSimple typeInstanceSimple)
            {
                range = typeInstanceSimple.Identifier.Position.Range;
                reference = typeInstanceSimple.Reference;
                goto _;
            }
        }

        if (AST.GetStatementAt(p, out Statement? statement))
        {
            foreach (Statement item in StatementWalker.Visit(statement))
            {
                if (!item.Position.Range.Contains(p)) continue;

                if (item is IReferenceableTo _ref1)
                {
                    range = Utils.GetInteractivePosition(item).Range;
                    if (!range.Contains(p)) continue;

                    reference = _ref1.Reference;
                    goto _;
                }

                if (item is IHaveType haveType
                    && GetDeepestTypeInstance(haveType.Type, p) is TypeInstanceSimple typeInstanceSimple)
                {
                    range = typeInstanceSimple.Identifier.Position.Range;
                    if (!range.Contains(p)) continue;

                    reference = typeInstanceSimple.Reference;
                    goto _;
                }
            }
        }

    _:

        if (GetGotoDefinition(reference, out LocationLink? link))
        {
            links.Add(new LocationLink()
            {
                OriginSelectionRange = range.ToOmniSharp(),
                TargetRange = link.TargetRange,
                TargetSelectionRange = link.TargetSelectionRange,
                TargetUri = link.TargetUri,
            });
        }

        return new LocationOrLocationLinks(links);
    }
}
