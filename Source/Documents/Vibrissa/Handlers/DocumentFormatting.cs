using System.Diagnostics;
using System.Runtime.Serialization;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;
using Position = LanguageCore.Position;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    abstract class FormatIRNode
    {
        public static implicit operator FormatIRNode(Token token) => new FormatIRNodeText() { Text = token.ToOriginalString() };
        public static implicit operator FormatIRNode(string text) => new FormatIRNodeText() { Text = text };

        public abstract override string ToString();
    }

    class FormatIRNodeText : FormatIRNode
    {
        public required string Text { get; init; }

        public override string ToString() => $"`{Text}`";
    }

    class FormatIRHardLine : FormatIRNode
    {
        public static readonly FormatIRHardLine Instance = new();

        public override string ToString() => $"HARDLINE";
    }

    class FormatIRSpace : FormatIRNode
    {
        public static readonly FormatIRSpace Instance = new();

        public override string ToString() => $"SPACE";
    }

    class FormatIRWhitespace : FormatIRNode
    {
        public static readonly FormatIRWhitespace Instance = new();

        public override string ToString() => $"WHITESPACE";
    }

    class FormatIRSpaceOrIndentedNewline : FormatIRNode
    {
        public required int Size { get; init; }

        public override string ToString() => $"SPACE_OR_NEWLINE";
    }

    class FormatIRManyWhitespace : FormatIRNode
    {
        public static readonly FormatIRManyWhitespace Instance = new();

        public override string ToString() => $"WHITESPACE+";
    }

    class FormatIRIndent : FormatIRNode
    {
        public required int Size { get; init; }

        public override string ToString() => $"INDENT+{Size}";
    }

    static void BuildFormatIR(AttributeUsage attributeUsage, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        ir.Add(attributeUsage.Brackets.Start);
        ir.Add(attributeUsage.Identifier);
        if (attributeUsage.Parameters.Length > 0)
        {
            ir.Add("(");
            for (int i = 0; i < attributeUsage.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    ir.Add(",");
                    ir.Add(FormatIRSpace.Instance);
                }
                ir.Add(attributeUsage.Parameters[i].ValueToken);
            }
            ir.Add(")");
        }
        ir.Add(attributeUsage.Brackets.End);
    }

    static void BuildFormatIR(TypeInstance? type, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        switch (type)
        {
            case null:
                break;
            case MissingTypeInstance v:
                break;
            case TypeInstanceFunction v:
                BuildFormatIR(v.FunctionReturnType, ir, indent, options);
                ir.Add(v.Brackets.Start);
                for (int i = 0; i < v.FunctionParameterTypes.Length; i++)
                {
                    if (i > 0)
                    {
                        ir.Add(",");
                        ir.Add(FormatIRSpace.Instance);
                    }
                    BuildFormatIR(v.FunctionParameterTypes[i], ir, indent, options);
                }
                ir.Add(v.Brackets.End);
                break;
            case TypeInstancePointer v:
                BuildFormatIR(v.To, ir, indent, options);
                ir.Add(v.Operator);
                break;
            case TypeInstanceReference v:
                BuildFormatIR(v.To, ir, indent, options);
                ir.Add(v.Operator);
                break;
            case TypeInstanceSimple v:
                ir.Add(v.Identifier);
                if (v.TemplateArguments.HasValue)
                {
                    ir.Add("<");
                    for (int i = 0; i < v.TemplateArguments.Value.Length; i++)
                    {
                        if (i > 0)
                        {
                            ir.Add(",");
                            ir.Add(FormatIRSpace.Instance);
                        }
                        BuildFormatIR(v.TemplateArguments.Value[i], ir, indent, options);
                    }
                    ir.Add(">");
                }
                break;
            case TypeInstanceStackArray v:
                BuildFormatIR(v.StackArrayOf, ir, indent, options);
                ir.Add(v.SquareBrackets.Start);
                BuildFormatIR(v.StackArraySize, ir, indent, options);
                ir.Add(v.SquareBrackets.End);
                break;
            default:
                throw new UnreachableException(type.GetType().Name);
        }
    }

    static void BuildFormatIR(Expression? statement, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        if (statement is null) return;
        if (statement.SurroundingBrackets.HasValue)
        {
            ir.Add(statement.SurroundingBrackets.Value.Start);
        }

        switch (statement)
        {
            case IMissingNode:
                break;
            case LiteralExpression v:
                ir.Add(v.ValueToken);
                break;
            case AnyCallExpression v:
                {
                    BuildFormatIR(v.Expression, ir, indent, options);
                    BuildFormatIR(v.Arguments, ir, indent, options);
                    break;
                }
            case ArgumentExpression v:
                {
                    if (v.Modifier is not null)
                    {
                        ir.Add(v.Modifier);
                        ir.Add(FormatIRSpace.Instance);
                    }
                    BuildFormatIR(v.Value, ir, indent, options);
                    break;
                }
            case BinaryOperatorCallExpression v:
                {
                    BuildFormatIR(v.Left, ir, indent, options);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add(v.Operator);
                    ir.Add(FormatIRSpace.Instance);
                    BuildFormatIR(v.Right, ir, indent, options);
                    break;
                }
            case ConstructorCallExpression v:
                {
                    ir.Add(v.Keyword);
                    ir.Add(FormatIRSpace.Instance);
                    BuildFormatIR(v.Type, ir, indent, options);
                    BuildFormatIR(v.Arguments, ir, indent, options);
                    break;
                }
            case DereferenceExpression v:
                {
                    ir.Add(v.Operator);
                    BuildFormatIR(v.Expression, ir, indent, options);
                    break;
                }
            case FieldExpression v:
                {
                    BuildFormatIR(v.Object, ir, indent, options);
                    ir.Add(".");
                    ir.Add(v.Identifier);
                    break;
                }
            case FunctionCallExpression v: throw new UnreachableException();
            case GetReferenceExpression v:
                {
                    ir.Add(v.Operator);
                    BuildFormatIR(v.Expression, ir, indent, options);
                    break;
                }
            case IdentifierExpression v:
                {
                    ir.Add(v.Identifier);
                    break;
                }
            case IndexCallExpression v:
                {
                    BuildFormatIR(v.Object, ir, indent, options);
                    ir.Add(v.Brackets.Start);
                    BuildFormatIR(v.Index, ir, indent, options);
                    ir.Add(v.Brackets.End);
                    break;
                }
            case LambdaExpression v:
                {
                    BuildFormatIR(v.Parameters, ir, indent, options);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add(v.Arrow);
                    ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    BuildFormatIR(v.Body, ir, indent, options);
                    break;
                }
            case ListExpression v:
                {
                    ir.Add(v.Brackets.Start);
                    if (v.Values.IsEmpty)
                    {
                        ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    }
                    else
                    {
                        for (int i = 0; i < v.Values.Length; i++)
                        {
                            if (i > 0)
                            {
                                ir.Add(",");
                                ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                            }
                            BuildFormatIR(v.Values[i], ir, indent, options);
                        }
                    }
                    ir.Add(v.Brackets.End);
                    break;
                }
            case ManagedTypeCastExpression v:
                {
                    ir.Add(v.Brackets.Start);
                    BuildFormatIR(v.Type, ir, indent, options);
                    ir.Add(v.Brackets.End);
                    BuildFormatIR(v.Expression, ir, indent, options);
                    break;
                }
            case NewInstanceExpression v:
                {
                    ir.Add(v.Keyword);
                    ir.Add(FormatIRSpace.Instance);
                    BuildFormatIR(v.Type, ir, indent, options);
                    break;
                }
            case PackExpression v:
                {
                    ir.Add(v.Keyword);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add(v.Brackets.Start);
                    if (v.Values.IsEmpty)
                    {
                        ir.Add(FormatIRSpace.Instance);
                    }
                    else
                    {
                        ir.Add(FormatIRHardLine.Instance);
                        for (int i = 0; i < v.Values.Length; i++)
                        {
                            ir.Add(new FormatIRIndent() { Size = indent + 1 });
                            BuildFormatIR(v.Values[i], ir, indent + 1, options);
                            ir.Add(",");
                            ir.Add(FormatIRHardLine.Instance);
                        }
                        ir.Add(new FormatIRIndent() { Size = indent });
                    }
                    ir.Add(v.Brackets.End);
                    break;
                }
            case ReinterpretExpression v:
                {
                    BuildFormatIR(v.PrevStatement, ir, indent, options);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add(v.Keyword);
                    ir.Add(FormatIRSpace.Instance);
                    BuildFormatIR(v.Type, ir, indent, options);
                    break;
                }
            case TemplateArgumentExpressionType v:
                {
                    BuildFormatIR(v.Value, ir, indent, options);
                    break;
                }
            case TemplateArgumentExpressionValue v:
                {
                    BuildFormatIR(v.Value, ir, indent, options);
                    break;
                }
            case UnaryOperatorCallExpression v:
                {
                    ir.Add(v.Operator);
                    BuildFormatIR(v.Expression, ir, indent, options);
                    break;
                }
            default:
                throw new UnreachableException(statement.GetType().Name);
        }

        if (statement.SurroundingBrackets.HasValue)
        {
            ir.Add(statement.SurroundingBrackets.Value.End);
        }
    }

    static void BuildFormatIR(Statement? statement, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        switch (statement)
        {
            case null:
            case IMissingNode:
                break;
            case Expression v:
                BuildFormatIR(v, ir, indent, options);
                break;
            case ArgumentListExpression v:
                ir.Add(v.Brackets.Start);
                for (int i = 0; i < v.Arguments.Length; i++)
                {
                    if (i > 0)
                    {
                        ir.Add(",");
                        ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    }
                    BuildFormatIR(v.Arguments[i], ir, indent, options);
                }
                ir.Add(v.Brackets.End);
                break;
            case Block v:
                {
                    ir.Add(v.Brackets.Start);
                    if (v.Statements.IsEmpty)
                    {
                        ir.Add(FormatIRSpace.Instance);
                    }
                    else
                    {
                        bool isSingleLine = v.Position.Range.Start.Line == v.Position.Range.End.Line;

                        if (!isSingleLine) ir.Add(FormatIRHardLine.Instance);
                        for (int i = 0; i < v.Statements.Length; i++)
                        {
                            if (!isSingleLine) ir.Add(new FormatIRIndent() { Size = indent + 1 });
                            else ir.Add(FormatIRSpace.Instance);
                            BuildFormatIR(v.Statements[i], ir, indent + 1, options);
                            if (Parser.NeedSemicolon(v.Statements[i]))
                            {
                                ir.Add(";");
                            }
                            if (!isSingleLine) ir.Add(FormatIRHardLine.Instance);
                        }
                        if (!isSingleLine) ir.Add(new FormatIRIndent() { Size = indent });
                        else ir.Add(FormatIRSpace.Instance);
                    }
                    ir.Add(v.Brackets.End);
                    break;
                }
            case CompoundAssignmentStatement v:
                {
                    BuildFormatIR(v.Target, ir, indent, options);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add(v.Operator);
                    ir.Add(FormatIRSpace.Instance);
                    BuildFormatIR(v.Value, ir, indent, options);
                    break;
                }
            case ElseBranchStatement v:
                {
                    ir.Add(v.Keyword);
                    ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    BuildFormatIR(v.Body, ir, indent, options);
                    if (Parser.NeedSemicolon(v.Body))
                    {
                        ir.Add(";");
                    }
                    break;
                }
            case EmptyStatement: break;
            case ForLoopStatement v:
                {
                    ir.Add(v.KeywordToken);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add("(");

                    BuildFormatIR(v.Initialization, ir, indent, options);
                    ir.Add(";");
                    ir.Add(FormatIRSpace.Instance);

                    BuildFormatIR(v.Condition, ir, indent, options);
                    ir.Add(";");
                    ir.Add(FormatIRSpace.Instance);

                    BuildFormatIR(v.Step, ir, indent, options);

                    ir.Add(")");
                    ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    BuildFormatIR(v.Body, ir, indent, options);
                    if (Parser.NeedSemicolon(v.Body))
                    {
                        ir.Add(";");
                    }
                    break;
                }
            case IfBranchStatement v:
                {
                    ir.Add(v.Keyword);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add("(");
                    BuildFormatIR(v.Condition, ir, indent, options);
                    ir.Add(")");
                    ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    BuildFormatIR(v.Body, ir, indent, options);
                    if (Parser.NeedSemicolon(v.Body))
                    {
                        ir.Add(";");
                    }
                    if (v.Else is not null)
                    {
                        ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                        BuildFormatIR(v.Else, ir, indent, options);
                    }
                    break;
                }
            case InstructionLabelDeclaration v:
                {
                    ir.Add(v.Identifier);
                    ir.Add(v.Colon);
                    break;
                }
            case ShortOperatorCall v:
                {
                    BuildFormatIR(v.Target, ir, indent, options);
                    ir.Add(v.Operator);
                    break;
                }
            case SimpleAssignmentStatement v:
                {
                    BuildFormatIR(v.Target, ir, indent, options);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add(v.Operator);
                    ir.Add(FormatIRSpace.Instance);
                    BuildFormatIR(v.Value, ir, indent, options);
                    break;
                }
            case VariableDefinition v:
                {
                    foreach (AttributeUsage a in v.Attributes)
                    {
                        BuildFormatIR(a, ir, indent, options);
                        ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    }

                    for (int i = 0; i < v.Modifiers.Length; i++)
                    {
                        if (i > 0)
                        {
                            ir.Add(",");
                            ir.Add(FormatIRSpace.Instance);
                        }
                        ir.Add(v.Modifiers[i]);
                    }

                    BuildFormatIR(v.Type, ir, indent, options);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add(v.Identifier);
                    if (v.InitialValue is not null)
                    {
                        ir.Add(FormatIRSpace.Instance);
                        ir.Add("=");
                        ir.Add(FormatIRSpace.Instance);
                        BuildFormatIR(v.InitialValue, ir, indent, options);
                    }
                    break;
                }
            case WhileLoopStatement v:
                {
                    ir.Add(v.Keyword);
                    ir.Add(FormatIRSpace.Instance);
                    ir.Add("(");
                    BuildFormatIR(v.Condition, ir, indent, options);
                    ir.Add(")");
                    ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
                    BuildFormatIR(v.Body, ir, indent, options);
                    if (Parser.NeedSemicolon(v.Body))
                    {
                        ir.Add(";");
                    }
                    break;
                }
            case KeywordCallStatement v:
                {
                    ir.Add(v.Keyword);
                    for (int i = 0; i < v.Arguments.Length; i++)
                    {
                        ir.Add(FormatIRSpace.Instance);
                        BuildFormatIR(v.Arguments[i], ir, indent, options);
                    }
                    break;
                }
            default:
                throw new UnreachableException(statement.GetType().Name);
        }
    }

    static void BuildFormatIR(TemplateInfo templateInfo, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        ir.Add(templateInfo.Brackets.Start);
        for (int i = 0; i < templateInfo.Parameters.Length; i++)
        {
            if (i > 0)
            {
                ir.Add(",");
                ir.Add(FormatIRSpace.Instance);
            }
            ir.Add(templateInfo.Parameters[i]);
        }
        ir.Add(templateInfo.Brackets.End);
    }

    static void BuildFormatIR(ParameterDefinition parameterDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        for (int i = 0; i < parameterDefinition.Modifiers.Length; i++)
        {
            ir.Add(parameterDefinition.Modifiers[i]);
            ir.Add(FormatIRSpace.Instance);
        }

        BuildFormatIR(parameterDefinition.Type, ir, indent, options);
        ir.Add(FormatIRSpace.Instance);
        ir.Add(parameterDefinition.Identifier);

        if (parameterDefinition.SpreadOperator is not null)
        {
            ir.Add(parameterDefinition.SpreadOperator);
        }

        if (parameterDefinition.DefaultValue is not null)
        {
            ir.Add(FormatIRSpace.Instance);
            ir.Add("=");
            ir.Add(FormatIRSpace.Instance);
            BuildFormatIR(parameterDefinition.DefaultValue, ir, indent, options);
        }
    }

    static void BuildFormatIR(ParameterDefinitionCollection parameterDefinitions, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        ir.Add(parameterDefinitions.Brackets.Start);
        for (int i = 0; i < parameterDefinitions.Parameters.Length; i++)
        {
            if (i > 0)
            {
                ir.Add(",");
                ir.Add(FormatIRSpace.Instance);
            }
            BuildFormatIR(parameterDefinitions.Parameters[i], ir, indent, options);
        }
        ir.Add(parameterDefinitions.Brackets.End);
    }

    static void BuildFormatIR(FunctionDefinition functionDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        foreach (AttributeUsage a in functionDefinition.Attributes)
        {
            BuildFormatIR(a, ir, indent, options);
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
        }

        for (int i = 0; i < functionDefinition.Modifiers.Length; i++)
        {
            ir.Add(functionDefinition.Modifiers[i]);
            ir.Add(FormatIRSpace.Instance);
        }

        BuildFormatIR(functionDefinition.Type, ir, indent, options);
        ir.Add(FormatIRSpace.Instance);
        ir.Add(functionDefinition.Identifier);

        if (functionDefinition.Template is not null)
        {
            BuildFormatIR(functionDefinition.Template, ir, indent, options);
        }

        BuildFormatIR(functionDefinition.Parameters, ir, indent, options);

        if (functionDefinition.Block is null)
        {
            ir.Add(";");
        }
        else
        {
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
            BuildFormatIR(functionDefinition.Block, ir, indent, options);
        }
    }

    static void BuildFormatIR(GeneralFunctionDefinition functionDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        foreach (AttributeUsage a in functionDefinition.Attributes)
        {
            BuildFormatIR(a, ir, indent, options);
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
        }

        for (int i = 0; i < functionDefinition.Modifiers.Length; i++)
        {
            ir.Add(functionDefinition.Modifiers[i]);
            ir.Add(FormatIRSpace.Instance);
        }

        ir.Add(functionDefinition.Identifier);

        if (functionDefinition.Template is not null)
        {
            BuildFormatIR(functionDefinition.Template, ir, indent, options);
        }

        BuildFormatIR(functionDefinition.Parameters, ir, indent, options);

        if (functionDefinition.Block is null)
        {
            ir.Add(";");
        }
        else
        {
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
            BuildFormatIR(functionDefinition.Block, ir, indent, options);
        }
    }

    static void BuildFormatIR(FieldDefinition fieldDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        foreach (AttributeUsage a in fieldDefinition.Attributes)
        {
            BuildFormatIR(a, ir, indent, options);
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
        }

        for (int i = 0; i < fieldDefinition.Modifiers.Length; i++)
        {
            ir.Add(fieldDefinition.Modifiers[i]);
            ir.Add(FormatIRSpace.Instance);
        }

        BuildFormatIR(fieldDefinition.Type, ir, indent, options);
        ir.Add(FormatIRSpace.Instance);
        ir.Add(fieldDefinition.Identifier);
    }

    static void BuildFormatIR(EnumMemberDefinition enumMemberDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        ir.Add(enumMemberDefinition.Identifier);
        if (enumMemberDefinition.Value is not null)
        {
            ir.Add(FormatIRSpace.Instance);
            ir.Add("=");
            ir.Add(FormatIRSpace.Instance);
            BuildFormatIR(enumMemberDefinition.Value, ir, indent, options);
        }
    }

    static void BuildFormatIR(EnumDefinition enumDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        foreach (AttributeUsage a in enumDefinition.Attributes)
        {
            BuildFormatIR(a, ir, indent, options);
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
        }

        for (int i = 0; i < enumDefinition.Modifiers.Length; i++)
        {
            ir.Add(enumDefinition.Modifiers[i]);
            ir.Add(FormatIRSpace.Instance);
        }

        ir.Add(enumDefinition.Keyword);
        ir.Add(FormatIRSpace.Instance);
        ir.Add(enumDefinition.Identifier);
        if (enumDefinition.Type is not null)
        {
            ir.Add(FormatIRSpace.Instance);
            ir.Add(":");
            ir.Add(FormatIRSpace.Instance);
            BuildFormatIR(enumDefinition.Type, ir, indent, options);
        }
        ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
        ir.Add(enumDefinition.Brackets.Start);
        if (enumDefinition.Members.IsEmpty)
        {
            ir.Add(FormatIRSpace.Instance);
        }
        else
        {
            ir.Add(FormatIRHardLine.Instance);
            for (int i = 0; i < enumDefinition.Members.Length; i++)
            {
                ir.Add(new FormatIRIndent() { Size = indent + 1 });
                BuildFormatIR(enumDefinition.Members[i], ir, indent + 1, options);
                ir.Add(",");
                ir.Add(FormatIRHardLine.Instance);
            }
            ir.Add(new FormatIRIndent() { Size = indent });
        }
        ir.Add(enumDefinition.Brackets.End);
    }

    static void BuildFormatIR(AliasDefinition aliasDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        foreach (AttributeUsage a in aliasDefinition.Attributes)
        {
            BuildFormatIR(a, ir, indent, options);
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
        }

        for (int i = 0; i < aliasDefinition.Modifiers.Length; i++)
        {
            ir.Add(aliasDefinition.Modifiers[i]);
            ir.Add(FormatIRSpace.Instance);
        }

        ir.Add(aliasDefinition.Keyword);
        ir.Add(FormatIRSpace.Instance);
        ir.Add(aliasDefinition.Identifier);
        ir.Add(FormatIRSpace.Instance);
        BuildFormatIR(aliasDefinition.Value, ir, indent, options);
    }

    static void BuildFormatIR(StructDefinition structDefinition, List<FormatIRNode> ir, int indent, FormattingOptions options)
    {
        foreach (AttributeUsage a in structDefinition.Attributes)
        {
            BuildFormatIR(a, ir, indent, options);
            ir.Add(new FormatIRSpaceOrIndentedNewline() { Size = indent });
        }

        for (int i = 0; i < structDefinition.Modifiers.Length; i++)
        {
            ir.Add(structDefinition.Modifiers[i]);
            ir.Add(FormatIRSpace.Instance);
        }

        ir.Add(DeclarationKeywords.Struct);
        ir.Add(FormatIRSpace.Instance);
        ir.Add(structDefinition.Identifier);

        if (structDefinition.Template is not null)
        {
            BuildFormatIR(structDefinition.Template, ir, indent, options);
        }

        ir.Add(structDefinition.BracketStart);
        bool hadAnyNodes = false;
        foreach (var node in StatementWalker.EnumerateNodes(structDefinition, StatementWalkerFilter.AllTopLevel).OrderBy(v => v.Position))
        {
            if (!hadAnyNodes) ir.Add(FormatIRHardLine.Instance);
            switch (node)
            {
                case FieldDefinition v:
                    ir.Add(new FormatIRIndent() { Size = indent + 1 });
                    BuildFormatIR(v, ir, indent + 1, options);
                    ir.Add(";");
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                case FunctionDefinition v:
                    ir.Add(new FormatIRIndent() { Size = indent + 1 });
                    BuildFormatIR(v, ir, indent + 1, options);
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                case GeneralFunctionDefinition v:
                    ir.Add(new FormatIRIndent() { Size = indent + 1 });
                    BuildFormatIR(v, ir, indent + 1, options);
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                default:
                    throw new UnreachableException(node.GetType().Name);
            }
            hadAnyNodes = true;
        }
        if (!hadAnyNodes)
        {
            ir.Add(FormatIRWhitespace.Instance);
        }
        else
        {
            ir.Add(new FormatIRIndent() { Size = indent });
        }
        ir.Add(structDefinition.BracketEnd);
    }

    class StringConsumer
    {
        public readonly string Text;
        readonly FormattingOptions Options;
        public int Index;
        public SinglePosition Position;

        public StringConsumer(string text, FormattingOptions options)
        {
            Text = text;
            Index = 0;
            Position = new SinglePosition(0, 0);
            Options = options;
        }

        public void UpdatePosition(char c)
        {
            if (c == '\n')
            {
                Position.Character = 0;
                Position.Line++;
            }
            else
            {
                Position.Character++;
            }
        }

        public bool ConsumeLengthAt(int start, FormatIRNode v, out int length)
        {
            length = 0;
            if (start >= Text.Length) return false;

            switch (v)
            {
                case FormatIRNodeText w:
                    if (Text[start..].StartsWith(w.Text))
                    {
                        length = w.Text.Length;
                        return true;
                    }
                    return false;
                case FormatIRHardLine:
                    while (start < Text.Length && Text[start] == '\n')
                    {
                        start++;
                        length++;
                    }
                    return length > 0;
                case FormatIRManyWhitespace:
                    while (start < Text.Length && Text[start] is '\n' or '\r' or '\t' or ' ')
                    {
                        start++;
                        length++;
                    }
                    return length > 0;
                case FormatIRWhitespace:
                    if (start < Text.Length && Text[start] is '\n' or '\r' or '\t' or ' ')
                    {
                        start++;
                        length++;
                    }
                    return length == 1;
                case FormatIRSpace:
                    if (start < Text.Length && Text[start] is '\n' or '\r' or '\t' or ' ')
                    {
                        if (Text[start] != ' ') return false;
                        start++;
                        length++;
                    }
                    return length == 1;
                case FormatIRIndent w:
                    if (w.Size == 0) return true;
                    int indentSize = 0;
                    int size = w.Size * (Options.TryGetValue("tabSize", out BooleanNumberString tabSizeValue) ? tabSizeValue.IsInteger ? tabSizeValue.Integer : 4 : 4);
                    while (start < Text.Length && Text[start] == ' ')
                    {
                        start++;
                        indentSize++;
                        if (indentSize >= size)
                        {
                            length = indentSize;
                            return true;
                        }
                    }
                    return false;
                case FormatIRSpaceOrIndentedNewline w:
                    if (ConsumeLengthAt(start, FormatIRSpace.Instance, out length)) return true;
                    if (ConsumeLengthAt(start, FormatIRHardLine.Instance, out length)
                        && ConsumeLengthAt(start + length, new FormatIRIndent() { Size = w.Size }, out int length2))
                    {
                        length += length2;
                        return true;
                    }
                    length = 0;
                    return false;
                default:
                    throw new UnreachableException(v.GetType().Name);
            }
        }

        public bool Consume(FormatIRNode v)
        {
            if (ConsumeLengthAt(Index, v, out int consumed))
            {
                for (int i = 0; i < consumed; i++)
                {
                    UpdatePosition(Text[Index++]);
                }
                return true;
            }
            return false;
        }

        public void ConsumeUntil(SinglePosition position)
        {
            while (Index < Text.Length && Position < position)
            {
                UpdatePosition(Text[Index++]);
            }
        }
    }

    public override async Task<IEnumerable<TextEdit>?> DocumentFormatting(DocumentFormattingParams request, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, RecompileLevel.Light, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(Content)) return [];

        List<FormatIRNode> ir = [];

        foreach (INode item in StatementWalker.EnumerateNodes(AST, StatementWalkerFilter.AllTopLevel).OrderBy(v => v.Position))
        {
            switch (item)
            {
                case UsingDefinition v:
                    ir.Add(v.Keyword);
                    ir.Add(FormatIRSpace.Instance);
                    for (int i = 0; i < v.Path.Length; i++)
                    {
                        if (i > 0) ir.Add(".");
                        ir.Add(v.Path[i]);
                    }
                    ir.Add(";");
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                case Statement v:
                    BuildFormatIR(v, ir, 0, request.Options);
                    if (Parser.NeedSemicolon(v))
                    {
                        ir.Add(";");
                    }
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                case FunctionDefinition v:
                    BuildFormatIR(v, ir, 0, request.Options);
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                case StructDefinition v:
                    BuildFormatIR(v, ir, 0, request.Options);
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                case EnumDefinition v:
                    BuildFormatIR(v, ir, 0, request.Options);
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                case AliasDefinition v:
                    BuildFormatIR(v, ir, 0, request.Options);
                    ir.Add(FormatIRHardLine.Instance);
                    break;
                default:
                    throw new UnreachableException(item.GetType().Name);
            }
        }

        List<TextEdit> res = [];
        StringConsumer consumer = new(Content, request.Options);

        int tokenIndex = 0;

        for (int j = 0; j < ir.Count; j++)
        {
            FormatIRNode item = ir[j];

            bool consumedTokens = false;
            for (int i = tokenIndex; i < Tokens.Length; i++)
            {
                Position effectivePosition = Tokens[i].Position;
                if (Tokens[i].TokenType == TokenType.PreprocessSkipped)
                {
                    effectivePosition = new Position(
                        new Range<SinglePosition>(effectivePosition.Range.Start, new SinglePosition(effectivePosition.Range.End.Line + 1, 0)),
                        new Range<int>(effectivePosition.AbsoluteRange.Start, effectivePosition.AbsoluteRange.End + 1)
                    );
                }

                if (effectivePosition.Range.End <= consumer.Position)
                {
                    continue;
                }

                if (effectivePosition.Range.Start < consumer.Position)
                {
                    Logger.Debug($"[FORMATTING] Token `{Tokens[i].ToOriginalString().Escape()}` is consumed partially (token start {effectivePosition.Range.Start} < current position {consumer.Position})");
                    break;
                }

                if (Tokens[i].TokenType is not TokenType.Comment and not TokenType.CommentMultiline and not TokenType.PreprocessSkipped)
                {
                    if (consumedTokens && Tokens[i].TokenType is TokenType.Whitespace or TokenType.LineBreak)
                    {
                    }
                    else
                    {
                        if (consumedTokens) Logger.Debug($"[FORMATTING] Stop consuming tokens before `{Tokens[i].ToOriginalString().Escape()}` ({Tokens[i].TokenType})");
                        break;
                    }
                }

                Logger.Debug($"[FORMATTING] Consumed token `{Tokens[i].ToOriginalString().Escape()}` ({Tokens[i].TokenType}) --> skipped to {effectivePosition.Range.End}");
                consumer.ConsumeUntil(effectivePosition.Range.End);
                tokenIndex = i + 1;
                consumedTokens = true;
            }

            SinglePosition start = consumer.Position;
            int startIndex = consumer.Index;
            if (!consumer.Consume(item))
            {
                goto mismatch;
            }

            Logger.Debug($"[FORMATTING] Node {item} matched at {start.ToReadable()}");
            continue;
        mismatch:
            Logger.Debug($"[FORMATTING] Node {item} mismatched at {start.ToReadable()}");
            if (consumedTokens)
            {
                Logger.Warn($"[FORMATTING] ... and won't search for it because of a dummy token");
                continue;
            }
            if (item is FormatIRNodeText nodeText)
            {
                int found = -1;
                for (int i = consumer.Index; i < consumer.Text.Length; i++)
                {
                    if (consumer.ConsumeLengthAt(i, item, out int length))
                    {
                        found = i;
                        break;
                    }
                    else if (consumer.Text[i] is not ' ' and not '\t' and not '\n' and not '\t')
                    {
                        Logger.Warn($"[FORMATTING] ... and not found later because of an unexpected character `{consumer.Text[i].Escape()}` --> ABORT");
                        return res;
                    }
                }

                if (found == -1)
                {
                    Range<SinglePosition> range = new(start, consumer.Position);
                    res.Add(new TextEdit()
                    {
                        Range = range.ToOmniSharp(),
                        NewText = nodeText.Text,
                    });
                    continue;
                }
                else
                {
                    for (int i = consumer.Index; i < found; i++)
                    {
                        consumer.UpdatePosition(consumer.Text[i]);
                    }
                    Range<SinglePosition> range = new(start, consumer.Position);
                    Logger.Debug($"[FORMATTING] ... but found later at {consumer.Position} --> deleting range {range.Start.ToReadable()}-{range.End.ToReadable()}");
                    res.Add(new TextEdit()
                    {
                        Range = range.ToOmniSharp(),
                        NewText = new string('\n', range.End.Line - range.Start.Line),
                    });
                    consumer.Index = found;
                    if (!consumer.Consume(item)) throw new UnreachableException();
                }
            }
            else if (item is FormatIRHardLine)
            {
                int found = -1;
                for (int i = consumer.Index; i < consumer.Text.Length; i++)
                {
                    if (consumer.ConsumeLengthAt(i, item, out int length))
                    {
                        found = i;
                        break;
                    }
                    else if (consumer.Text[i] is not ' ' and not '\t')
                    {
                        Logger.Debug($"[FORMATTING] ... and not found later because of an unexpected character `{consumer.Text[i].Escape()}`");
                        break;
                    }
                }

                if (found == -1)
                {
                    Range<SinglePosition> range = new(start, consumer.Position);
                    res.Add(new TextEdit()
                    {
                        Range = range.ToOmniSharp(),
                        NewText = "\n",
                    });
                    continue;
                }
                else
                {
                    for (int i = consumer.Index; i < found; i++)
                    {
                        consumer.UpdatePosition(consumer.Text[i]);
                    }
                    Range<SinglePosition> range = new(start, consumer.Position);
                    Logger.Debug($"[FORMATTING] ... but found later at {consumer.Position} --> deleting range {range.Start.ToReadable()}{range.End.ToReadable()}");
                    res.Add(new TextEdit()
                    {
                        Range = range.ToOmniSharp(),
                        NewText = new string('\n', range.End.Line - range.Start.Line),
                    });
                    consumer.Index = found;
                    if (!consumer.Consume(item)) throw new UnreachableException();
                }
            }
            else if (item is FormatIRSpaceOrIndentedNewline)
            {
                int found = -1;
                for (int i = consumer.Index; i < consumer.Text.Length; i++)
                {
                    if (consumer.ConsumeLengthAt(i, item, out int length))
                    {
                        found = i;
                        break;
                    }
                    else if (consumer.Text[i] is not ' ' and not '\t')
                    {
                        Logger.Debug($"[FORMATTING] ... and not found later because of an unexpected character `{consumer.Text[i].Escape()}`");
                        break;
                    }
                }

                if (found == -1)
                {
                    Range<SinglePosition> range = new(start, consumer.Position);
                    res.Add(new TextEdit()
                    {
                        Range = range.ToOmniSharp(),
                        NewText = " ",
                    });
                    continue;
                }
                else
                {
                    for (int i = consumer.Index; i < found; i++)
                    {
                        consumer.UpdatePosition(consumer.Text[i]);
                    }
                    Range<SinglePosition> range = new(start, consumer.Position);
                    Logger.Debug($"[FORMATTING] ... but found later at {consumer.Position} --> deleting range {range.Start.ToReadable()}-{range.End.ToReadable()}");
                    res.Add(new TextEdit()
                    {
                        Range = range.ToOmniSharp(),
                        NewText = new string('\n', range.End.Line - range.Start.Line),
                    });
                    consumer.Index = found;
                    if (!consumer.Consume(item)) throw new UnreachableException();
                }
            }
            else
            {
                Range<SinglePosition> range = new(start, consumer.Position);
                res.Add(new TextEdit()
                {
                    Range = range.ToOmniSharp(),
                    NewText = item switch
                    {
                        FormatIRNodeText w => throw new UnreachableException(),
                        FormatIRHardLine w => throw new UnreachableException(),
                        FormatIRWhitespace w => consumer.Text[startIndex..consumer.Index].Contains('\n') ? "\n" : " ",
                        FormatIRSpaceOrIndentedNewline w => consumer.Text[startIndex..consumer.Index].Contains('\n') ? "\n" + new string(' ', w.Size * (request.Options.TryGetValue("tabSize", out BooleanNumberString tabSizeValue) ? tabSizeValue.IsInteger ? tabSizeValue.Integer : 4 : 4)) : " ",
                        FormatIRManyWhitespace w => consumer.Text[startIndex..consumer.Index].Contains('\n') ? "\n" : " ",
                        FormatIRSpace w => " ",
                        FormatIRIndent w => new string(' ', w.Size * (request.Options.TryGetValue("tabSize", out BooleanNumberString tabSizeValue) ? tabSizeValue.IsInteger ? tabSizeValue.Integer : 4 : 4)),
                        _ => throw new UnreachableException(item.GetType().Name),
                    },
                });
            }
        }

        for (int i = tokenIndex; i < Tokens.Length; i++)
        {
            if (Tokens[i].Position.Range.Start < consumer.Position) break;
            if (Tokens[i].TokenType is not TokenType.Comment and not TokenType.CommentMultiline and not TokenType.PreprocessSkipped) break;
            consumer.ConsumeUntil(Tokens[i].Position.Range.End);
            tokenIndex = i + 1;
        }

        SinglePosition endStart = consumer.Position;
        while (consumer.Index < consumer.Text.Length)
        {
            consumer.UpdatePosition(consumer.Text[consumer.Index]);
            consumer.Index++;
        }
        Range<SinglePosition> endRange = new(endStart, consumer.Position);
        res.Add(new TextEdit()
        {
            Range = endRange.ToOmniSharp(),
            NewText = "",
        });

        for (int i = 0; i < res.Count; i++)
        {
            TextEdit v = res[i];
            if (v.Range.Start == v.Range.End && v.NewText == "")
            {
                res.RemoveAt(i--);
            }
        }

        return res;
    }
}
