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
        await AwaitForCompilation(Version, RecompileLevel.Light, cancellationToken).ConfigureAwait(false);

        SinglePosition p = e.Position.ToCool();

        foreach (UsingDefinition @using in AST.Usings.IsDefault ? ImmutableArray<UsingDefinition>.Empty : AST.Usings)
        {
            if (@using.Path.IsEmpty) continue;
            var pathPos = new Position(@using.Path);

            if (!pathPos.Range.Contains(p)) continue;
            if (@using.CompiledUri is null) break;

            return new(new LocationLink()
            {
                TargetUri = DocumentUri.From(@using.CompiledUri),
                OriginSelectionRange = pathPos.ToOmniSharp(),
                TargetRange = Position.Zero.ToOmniSharp(),
                TargetSelectionRange = Position.Zero.ToOmniSharp(),
            });
        }

        if (GetTypeInstanceAt(e.Position.ToCool(), true, out TypeInstance? origin))
        {
            origin = GetDeepestTypeInstance(origin, e.Position.ToCool());

            if (origin is not null && origin is TypeInstanceSimple w && w.CompiledTypeExpression is not null)
            {
                Position position = w.Identifier.Position;

                if (w.CompiledTypeExpression is CompiledStructTypeExpression structType && GetGotoDefinition(structType.Struct, out LocationLink? link0))
                {
                    return new(new LocationLink()
                    {
                        OriginSelectionRange = position.ToOmniSharp(),
                        TargetRange = link0.TargetRange,
                        TargetSelectionRange = link0.TargetSelectionRange,
                        TargetUri = link0.TargetUri,
                    });
                }

                if (w.CompiledTypeExpression is CompiledGenericTypeExpression genericType && genericType.Definition is not null)
                {
                    return new(new LocationLink()
                    {
                        OriginSelectionRange = position.ToOmniSharp(),
                        TargetRange = genericType.Definition.Position.Range.ToOmniSharp(),
                        TargetSelectionRange = genericType.Definition.Position.Range.ToOmniSharp(),
                        TargetUri = DocumentUri,
                    });
                }

                if (w.CompiledTypeExpression is CompiledAliasTypeExpression aliasType && aliasType.Definition is not null)
                {
                    return new(new LocationLink()
                    {
                        OriginSelectionRange = position.ToOmniSharp(),
                        TargetRange = aliasType.Definition.Definition.Position.Range.ToOmniSharp(),
                        TargetSelectionRange = aliasType.Definition.Definition.Position.Range.ToOmniSharp(),
                        TargetUri = aliasType.Definition.Definition.File,
                    });
                }

                if (w.CompiledTypeExpression is CompiledEnumTypeExpression enumType && enumType.Definition is not null)
                {
                    return new(new LocationLink()
                    {
                        OriginSelectionRange = position.ToOmniSharp(),
                        TargetRange = enumType.Definition.Definition.Position.Range.ToOmniSharp(),
                        TargetSelectionRange = enumType.Definition.Definition.Position.Range.ToOmniSharp(),
                        TargetUri = enumType.Definition.Definition.File,
                    });
                }
            }
        }

        Range<SinglePosition> range = default;
        object? reference = null;

        if (AST.GetStatementAt(p, out Statement? statement))
        {
            foreach (ISmallNode item in StatementWalker.EnumerateNodes(statement, StatementWalkerFilter.At(p)))
            {
                if (item is IReferenceableTo _ref1)
                {
                    range = StatementExtensions.GetInteractivePosition(item).Range;
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
            return new(new LocationLink()
            {
                OriginSelectionRange = range.ToOmniSharp(),
                TargetRange = link.TargetRange,
                TargetSelectionRange = link.TargetSelectionRange,
                TargetUri = link.TargetUri,
            });
        }

        return new();
    }
}
