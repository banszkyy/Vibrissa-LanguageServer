using System.Collections.Immutable;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageServer;

static class StatementExtensions
{
    static bool GetParameterDefinitionAt<TFunction>(
        TFunction function,
        SinglePosition position,
        [NotNullWhen(true)] out ParameterDefinition? parameter,
        [NotNullWhen(true)] out GeneralType? parameterType)
        where TFunction : ICompiledFunctionDefinition
    {
        for (int i = 0; i < function.Parameters.Length; i++)
        {
            parameter = function.Parameters[i].Definition;
            parameterType = function.Parameters[i].Type;

            if (parameter.Position.Range.Contains(position))
            { return true; }
        }

        parameter = null;
        parameterType = null;
        return false;
    }

    static bool GetParameterDefinitionAt<TFunction>(
        IEnumerable<TFunction> functions,
        Uri file,
        SinglePosition position,
        [NotNullWhen(true)] out ParameterDefinition? parameter,
        [NotNullWhen(true)] out GeneralType? parameterType)
        where TFunction : ICompiledFunctionDefinition, IInFile
    {
        foreach (TFunction function in functions)
        {
            if (function.File != file) continue;

            if (GetParameterDefinitionAt(function, position, out parameter, out parameterType))
            { return true; }
        }

        parameter = null;
        parameterType = null;
        return false;
    }

    public static bool GetParameterDefinitionAt(
        this CompilerResult compilerResult,
        Uri file,
        SinglePosition position,
        [NotNullWhen(true)] out ParameterDefinition? parameter,
        [NotNullWhen(true)] out GeneralType? parameterType)
    {
        if (GetParameterDefinitionAt(compilerResult.FunctionDefinitions, file, position, out parameter, out parameterType))
        { return true; }

        if (GetParameterDefinitionAt(compilerResult.OperatorDefinitions, file, position, out parameter, out parameterType))
        { return true; }

        if (GetParameterDefinitionAt(compilerResult.GeneralFunctionDefinitions, file, position, out parameter, out parameterType))
        { return true; }

        return false;
    }

    public static bool GetTypeInstanceAt(
        this ParserResult ast,
        SinglePosition position,
        [NotNullWhen(true)] out TypeInstance? typeInstance,
        [NotNullWhen(true)] out GeneralType? generalType)
    {
        foreach (var item in StatementWalker.EnumerateNodes(ast, StatementWalkerFilter.At(position)).OfType<TypeInstance>())
        {
            GeneralType? compiled = item switch
            {
                TypeInstanceFunction v => v.CompiledType,
                TypeInstancePointer v => v.CompiledType,
                TypeInstanceReference v => v.CompiledType,
                TypeInstanceSimple v => v.CompiledType,
                TypeInstanceStackArray v => v.CompiledType,
                _ => null,
            };
            if (compiled is null) continue;

            typeInstance = item;
            generalType = compiled;
            return true;
        }

        typeInstance = null;
        generalType = null;
        return false;
    }

    public static IEnumerable<Token> GetVisibleModifiers(IEnumerable<Token> modifiers)
    {
        return modifiers.Where(v => v.Content != "export");
    }

    public static LanguageCore.Position GetInteractivePosition(ISmallNode statement) => statement switch
    {
        AnyCallExpression v => v.Expression switch
        {
            FieldExpression v2 => v2.Identifier.Position,
            _ => v.Expression.Position,
        },
        BinaryOperatorCallExpression v => v.Operator.Position,
        UnaryOperatorCallExpression v => v.Operator.Position,
        VariableDefinition v => v.Identifier.Position,
        FieldExpression v => v.Identifier.Position,
        ConstructorCallExpression v => v.Type.Position,
        NewInstanceExpression v => v.Type.Position,
        ManagedTypeCastExpression v => v.Type.Position,
        TypeInstanceSimple v => v.Identifier.Position,
        _ => statement.Position,
    };

    public static bool GetStatementAt(this ParserResult parserResult, SinglePosition position, [NotNullWhen(true)] out Statement? statement)
        => (statement = StatementWalker.EnumerateNodes(parserResult, StatementWalkerFilter.At(position)).OfType<Statement>().LastOrDefault()) is not null;

    public static bool GetFieldAt(this ParserResult parserResult, SinglePosition position, [NotNullWhen(true)] out FieldDefinition? result)
    {
        foreach (FieldDefinition field in (parserResult.Structs.IsDefault ? ImmutableArray<StructDefinition>.Empty : parserResult.Structs).SelectMany(v => v.Fields))
        {
            if (!field.Identifier.Position.Range.Contains(position)) continue;

            result = field;
            return true;
        }

        result = null;
        return false;
    }

    public static bool GetStructAt(this ParserResult parserResult, SinglePosition position, [NotNullWhen(true)] out StructDefinition? result)
    {
        foreach (StructDefinition @struct in parserResult.Structs.IsDefault ? ImmutableArray<StructDefinition>.Empty : parserResult.Structs)
        {
            if (!@struct.Identifier.Position.Range.Contains(position)) continue;

            result = @struct;
            return true;
        }

        result = null;
        return false;
    }

    public static bool GetEnumAt(this ParserResult parserResult, SinglePosition position, [NotNullWhen(true)] out EnumDefinition? result)
    {
        foreach (EnumDefinition @enum in parserResult.EnumDefinitions.IsDefault ? ImmutableArray<EnumDefinition>.Empty : parserResult.EnumDefinitions)
        {
            if (!@enum.Identifier.Position.Range.Contains(position)) continue;

            result = @enum;
            return true;
        }

        result = null;
        return false;
    }

    public static bool GetEnumMemberAt(this ParserResult parserResult, SinglePosition position, [NotNullWhen(true)] out EnumMemberDefinition? result)
    {
        foreach (EnumDefinition @enum in parserResult.EnumDefinitions.IsDefault ? ImmutableArray<EnumDefinition>.Empty : parserResult.EnumDefinitions)
        {
            foreach (EnumMemberDefinition member in @enum.Members)
            {
                if (!member.Identifier.Position.Range.Contains(position)) continue;

                result = member;
                return true;
            }
        }

        result = null;
        return false;
    }

    public static bool GetFunctionAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledFunctionDefinition? result)
    {
        foreach (CompiledFunctionDefinition thing in compilerResult.FunctionDefinitions)
        {
            if (thing.File != file) continue;
            if (!thing.Definition.Identifier.Position.Range.Contains(position)) continue;

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public static bool GetGeneralFunctionAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledGeneralFunctionDefinition? result)
    {
        foreach (CompiledGeneralFunctionDefinition thing in compilerResult.GeneralFunctionDefinitions)
        {
            if (thing.File != file) continue;
            if (!thing.Definition.Identifier.Position.Range.Contains(position)) continue;

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public static bool GetOperatorAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledOperatorDefinition? result)
    {
        foreach (CompiledOperatorDefinition thing in compilerResult.OperatorDefinitions)
        {
            if (thing.File != file) continue;
            if (!thing.Definition.Identifier.Position.Range.Contains(position)) continue;

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public static bool GetStructAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledStruct? result)
    {
        foreach (var thing in compilerResult.Structs)
        {
            if (thing.File != file) continue;
            if (!thing.Definition.Identifier.Position.Range.Contains(position)) continue;

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public static bool GetAliasAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledAlias? result)
    {
        foreach (var thing in compilerResult.Aliases)
        {
            if (thing.File != file) continue;
            if (!thing.Definition.Identifier.Position.Range.Contains(position)) continue;

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public static bool GetEnumAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledEnum? result)
    {
        foreach (var thing in compilerResult.Enums)
        {
            if (thing.File != file) continue;
            if (!thing.Definition.Identifier.Position.Range.Contains(position)) continue;

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public static bool GetEnumMemberAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledEnumMember? result)
    {
        foreach (var thing in compilerResult.Enums)
        {
            if (thing.File != file) continue;

            foreach (var member in thing.Members)
            {
                if (!member.Definition.Identifier.Position.Range.Contains(position)) continue;

                result = member;
                return true;
            }
        }

        result = default;
        return false;
    }

    public static bool GetFieldAt(this CompilerResult compilerResult, Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledField? result)
    {
        foreach (CompiledStruct @struct in compilerResult.Structs)
        {
            if (@struct.Definition.File != file) continue;

            foreach (CompiledField field in @struct.Fields)
            {
                if (field.Definition.Identifier.Position.Range.Contains(position))
                {
                    result = field;
                    return true;
                }
            }
        }

        result = null;
        return false;
    }
}
