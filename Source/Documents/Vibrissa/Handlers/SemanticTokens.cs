using LanguageCore;
using LanguageCore.Tokenizing;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task GetSemanticTokens(SemanticTokensBuilder builder, ITextDocumentIdentifierParams e, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, RecompileLevel.Light, cancellationToken).ConfigureAwait(false);

        foreach (Token token in Tokens)
        {
            switch (token.AnalyzedType)
            {
                case TokenAnalyzedType.Attribute:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Type, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.Type:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Type, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.TypeAlias:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Type, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.Struct:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Struct, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.FunctionName:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Function, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.VariableName:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Variable, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.ConstantName:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Variable, SemanticTokenModifier.Readonly);
                    break;
                case TokenAnalyzedType.ParameterName:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Parameter, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.TemplateParameterType:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.TypeParameter, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.TemplateParameterValue:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Variable, SemanticTokenModifier.Readonly);
                    break;
                case TokenAnalyzedType.Keyword:
                    //builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Keyword, new SemanticTokenModifier("meow"));
                    break;
                case TokenAnalyzedType.FieldName:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Property, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.Statement:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Keyword, new SemanticTokenModifier("meow"));
                    break;
                case TokenAnalyzedType.BuiltinType:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Keyword, new SemanticTokenModifier("meow"));
                    break;
                case TokenAnalyzedType.MathOperator:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Operator, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.OtherOperator:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Operator, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.TypeModifier:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Operator, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.InstructionLabel:
                    break;
                case TokenAnalyzedType.Enum:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Enum, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.EnumMember:
                    builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.EnumMember, Array.Empty<SemanticTokenModifier>());
                    break;
                case TokenAnalyzedType.None:
                default:
                    switch (token.TokenType)
                    {
                        case TokenType.LiteralNumber:
                        case TokenType.LiteralHex:
                        case TokenType.LiteralBinary:
                        case TokenType.LiteralFloat:
                            builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Number, Array.Empty<SemanticTokenModifier>());
                            break;
                        case TokenType.PreprocessSkipped:
                            if (token.Position.Range.Start.Line != token.Position.Range.End.Line)
                            {
                                // FIXME: more preprocessor info
                                var lines = Content?.Split('\n') ?? [];
                                for (int i = token.Position.Range.Start.Line; i < token.Position.Range.End.Line; i++)
                                {
                                    if (i >= 0 && i < lines.Length)
                                    {
                                        builder.Push(new Range<SinglePosition>(new SinglePosition(i, 0), new SinglePosition(i, lines[i].Length)).ToOmniSharp(), SemanticTokenType.Comment, Array.Empty<SemanticTokenModifier>());
                                    }
                                }
                            }
                            else
                            {
                                builder.Push(token.Position.Range.ToOmniSharp(), SemanticTokenType.Comment, Array.Empty<SemanticTokenModifier>());
                            }
                            break;
                    }
                    break;
            }
        }

        builder.Commit();
    }
}
