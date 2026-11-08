using LanguageCore.Compiler;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<IEnumerable<CodeLens>?> CodeLens(CodeLensParams e, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, RecompileLevel.None, cancellationToken).ConfigureAwait(false);

        List<CodeLens> result = new();

        foreach (CompiledFunctionDefinition function in CompilerResult.FunctionDefinitions)
        {
            if (function.File != Uri) continue;

            result.Add(new CodeLens()
            {
                Range = function.Definition.Identifier.Position.Range.ToOmniSharp(),
                Command = new Command()
                {
                    Title = $"{function.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                },
            });
        }

        foreach (CompiledGeneralFunctionDefinition function in CompilerResult.GeneralFunctionDefinitions)
        {
            if (function.File != Uri) continue;

            result.Add(new CodeLens()
            {
                Range = function.Definition.Identifier.Position.Range.ToOmniSharp(),
                Command = new Command()
                {
                    Title = $"{function.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                },
            });
        }

        foreach (CompiledOperatorDefinition function in CompilerResult.OperatorDefinitions)
        {
            if (function.File != Uri) continue;

            result.Add(new CodeLens()
            {
                Range = function.Definition.Identifier.Position.Range.ToOmniSharp(),
                Command = new Command()
                {
                    Title = $"{function.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                },
            });
        }

        foreach (CompiledStruct definition in CompilerResult.Structs)
        {
            if (definition.Definition.File != Uri) continue;

            result.Add(new CodeLens()
            {
                Range = definition.Definition.Identifier.Position.Range.ToOmniSharp(),
                Command = new Command()
                {
                    Title = $"{definition.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                },
            });

            foreach (CompiledField member in definition.Fields)
            {
                result.Add(new CodeLens()
                {
                    Range = member.Definition.Identifier.Position.Range.ToOmniSharp(),
                    Command = new Command()
                    {
                        Title = $"{member.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                    },
                });
            }
        }

        foreach (CompiledAlias definition in CompilerResult.Aliases)
        {
            if (definition.Definition.File != Uri) continue;

            result.Add(new CodeLens()
            {
                Range = definition.Definition.Identifier.Position.Range.ToOmniSharp(),
                Command = new Command()
                {
                    Title = $"{definition.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                },
            });
        }

        foreach (CompiledEnum definition in CompilerResult.Enums)
        {
            if (definition.Definition.File != Uri) continue;

            result.Add(new CodeLens()
            {
                Range = definition.Definition.Identifier.Position.Range.ToOmniSharp(),
                Command = new Command()
                {
                    Title = $"{definition.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                },
            });

            foreach (CompiledEnumMember member in definition.Members)
            {
                result.Add(new CodeLens()
                {
                    Range = member.Definition.Identifier.Position.Range.ToOmniSharp(),
                    Command = new Command()
                    {
                        Title = $"{member.References.DistinctBy(v => v.Source).Count(v => !v.SourceLocation.IsDefault)} reference",
                    },
                });
            }
        }

        return result;
    }
}
