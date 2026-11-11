using System.Collections.Immutable;
using System.IO;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<IEnumerable<CompletionItem>?> Completion(CompletionParams e, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, RecompileLevel.Light, cancellationToken).ConfigureAwait(false);

        void AddTypeItems(List<CompletionItem> result)
        {
            foreach (CompiledStruct @struct in CompilerResult.Structs)
            {
                if (!@struct.Definition.CanUse(Uri))
                { continue; }

                result.Add(new CompletionItem()
                {
                    Kind = CompletionItemKind.Struct,
                    Label = @struct.Identifier,
                });
            }

            foreach (CompiledAlias alias in CompilerResult.Aliases)
            {
                if (!alias.Definition.CanUse(Uri)) continue;

                result.Add(new CompletionItem()
                {
                    Kind = CompletionItemKind.Class,
                    Label = alias.Identifier,
                    LabelDetails = new CompletionItemLabelDetails()
                    {
                        Detail = $" = {alias.Value}",
                    },
                });
            }

            foreach (CompiledEnum @enum in CompilerResult.Enums)
            {
                if (!@enum.Definition.CanUse(Uri)) continue;

                result.Add(new CompletionItem()
                {
                    Kind = CompletionItemKind.Enum,
                    Label = @enum.Identifier,
                });
            }

            result.AddRange(TypeKeywords.List.Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));
        }

        void AddExpressionItems(List<CompletionItem> result)
        {
            {
                Dictionary<string, List<CompiledFunctionDefinition>> functionOverloads = new();

                foreach (CompiledFunctionDefinition function in CompilerResult.FunctionDefinitions)
                {
                    if (!function.Definition.CanUse(Uri)) continue;

                    if (!functionOverloads.TryGetValue(function.Identifier, out var overloads))
                    { overloads = functionOverloads[function.Identifier] = new(); }
                    overloads.Add(function);
                }

                foreach ((string function, var overloads) in functionOverloads)
                {
                    result.Add(new CompletionItem()
                    {
                        Kind = CompletionItemKind.Function,
                        Label = function,
                        LabelDetails = new CompletionItemLabelDetails()
                        {
                            Description = overloads.Count switch
                            {
                                0 => null,
                                1 => overloads[0].Type.ToString(),
                                _ => $"{overloads.Count} overloads",
                            },
                        },
                        CommitCharacters = new(["("]),
                    });
                }
            }

            foreach ((ImmutableArray<Statement> statements, _) in CompilerResult.RawStatements)
            {
                foreach (VariableDefinition statement in statements.OfType<VariableDefinition>())
                {
                    if (!statement.CanUse(e.TextDocument.Uri.ToUri()))
                    { continue; }

                    if (statement.Modifiers.Contains(ModifierKeywords.Const))
                    {
                        result.Add(new CompletionItem()
                        {
                            Kind = CompletionItemKind.Constant,
                            Label = statement.Identifier.Content,
                            //LabelDetails = new CompletionItemLabelDetails()
                            //{
                            //    Description = statement.Type.ToString(),
                            //},
                        });
                    }
                    else
                    {
                        result.Add(new CompletionItem()
                        {
                            Kind = CompletionItemKind.Variable,
                            Label = statement.Identifier.Content,
                            //LabelDetails = new CompletionItemLabelDetails()
                            //{
                            //    Description = statement.Type.ToString(),
                            //},
                        });
                    }
                }
            }

            SinglePosition position = e.Position.ToCool();
            foreach (CompiledFunctionDefinition function in CompilerResult.FunctionDefinitions)
            {
                if (function.File != e.TextDocument.Uri.ToUri())
                { continue; }

                if (function.Definition.Block == null) continue;
                if (function.Definition.Block.Position.Range.Contains(position))
                {
                    foreach (CompiledParameter parameter in function.Parameters)
                    {
                        result.Add(new CompletionItem()
                        {
                            Kind = CompletionItemKind.Variable,
                            Label = parameter.Identifier,
                            LabelDetails = new CompletionItemLabelDetails()
                            {
                                Description = parameter.Type.ToString(),
                            },
                        });
                    }

                    break;
                }
            }
        }

        void AddStatementItems(List<CompletionItem> result)
        {
            result.AddRange(StatementKeywords.List.Where(v => v is not StatementKeywords.If and not StatementKeywords.For and not StatementKeywords.While).Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));

            {
                string intType = "i32";
                if (CompilerResult.Aliases.TryGetValue(v => v.Definition.Attributes.TryGetAttribute(AttributeConstants.InternalType, out AttributeUsage? w0) && w0.TryGetValue(out string? w1) && w1 == InternalTypes.Integer, out var w0))
                {
                    intType = w0.Identifier;
                }
                else if (CompilerResult.Structs.TryGetValue(v => v.Definition.Attributes.TryGetAttribute(AttributeConstants.InternalType, out AttributeUsage? w0) && w0.TryGetValue(out string? w1) && w1 == InternalTypes.Integer, out var w1))
                {
                    intType = w1.Identifier;
                }
                else if (CompilerResult.Enums.TryGetValue(v => v.Definition.Attributes.TryGetAttribute(AttributeConstants.InternalType, out AttributeUsage? w0) && w0.TryGetValue(out string? w1) && w1 == InternalTypes.Integer, out var w2))
                {
                    intType = w2.Identifier;
                }

                result.Add(new CompletionItem()
                {
                    Label = StatementKeywords.For,
                    Kind = CompletionItemKind.Keyword,
                    InsertTextFormat = InsertTextFormat.Snippet,
                    InsertText =
                    $"{StatementKeywords.For} ({intType} ${{1:i}}; $1 < ${{2:length}}; $1++)\n" +
                    "{\n" +
                    "    $3\n" +
                    "}\n"
                });
            }

            result.Add(new CompletionItem()
            {
                Label = StatementKeywords.While,
                Kind = CompletionItemKind.Keyword,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText =
                $"{StatementKeywords.While} ($1)\n" +
                "{\n" +
                "    $2\n" +
                "}\n",
            });

            result.Add(new CompletionItem()
            {
                Label = StatementKeywords.If,
                Kind = CompletionItemKind.Keyword,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText =
                $"{StatementKeywords.If} ($1)\n" +
                "{\n" +
                "    $2\n" +
                "}\n"
            });
        }

        void AddBigDefinitionSnippets(List<CompletionItem> result)
        {
            result.AddRange(DeclarationKeywords.List.Where(v => v is not DeclarationKeywords.Using).Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));

            result.Add(new CompletionItem()
            {
                Label = "function",
                Kind = CompletionItemKind.Snippet,
                InsertTextFormat = InsertTextFormat.Snippet,
                Detail = "Function definition",
                InsertText =
                $"${{1:{TypeKeywords.Void}}} ${{2:meow}}($3)\n" +
                "{\n" +
                "    $4\n" +
                "}\n",
            });
        }

        SinglePosition p = e.Position.ToCool();

        List<CompletionItem> result = new();

        foreach (AttributeUsage? attribute in
            AST.AliasDefinitions.Cast<IHaveAttributes>()
            .Append(AST.Structs)
            .Append(AST.AllFunctions)
            .Append(AST.EnumDefinitions)
            .Append(StatementWalker.EnumerateNodes(AST, StatementWalkerFilter.All).OfType<IHaveAttributes>())
            .SelectMany(v => v.Attributes)
        )
        {
            if (attribute.Identifier.Position.Range.Contains(p))
            {
                foreach (string item in AttributeConstants.List)
                {
                    result.Add(new CompletionItem()
                    {
                        Label = item,
                        Kind = CompletionItemKind.Class,
                    });
                }
                return result;
            }

            if (attribute.Brackets.Position.Range.Contains(p))
            {
                switch (attribute.Identifier.Content)
                {
                    case AttributeConstants.BuiltinIdentifier:
                        if (attribute.Parameters.Length >= 1
                            && attribute.Parameters[0].Position.Range.Contains(p)
                            && attribute.Parameters[0] is StringLiteralExpression)
                        {
                            result.AddRange(BuiltinFunctions.Prototypes.Keys.Select(v => new CompletionItem()
                            {
                                Label = v,
                                Kind = CompletionItemKind.Constant,
                            }));
                        }
                        break;
                    case AttributeConstants.ExternalIdentifier:
                        if (attribute.Parameters.Length >= 1
                            && attribute.Parameters[0].Position.Range.Contains(p)
                            && attribute.Parameters[0] is StringLiteralExpression)
                        {
                            result.AddRange(CompilerSettings.ExternalFunctions.Select(v => v.Name is null ? null : new CompletionItem()
                            {
                                Label = v.Name,
                                Kind = CompletionItemKind.Constant,
                            }).Where(v => v is not null)!);
                            result.AddRange(CompilerSettings.ExternalConstants.Select(v => new CompletionItem()
                            {
                                Label = v.Name,
                                Kind = CompletionItemKind.Constant,
                            }));
                        }
                        break;
                    case AttributeConstants.InternalIdentifier:
                        if (attribute.Parameters.Length >= 1
                            && attribute.Parameters[0].Position.Range.Contains(p)
                            && attribute.Parameters[0] is StringLiteralExpression)
                        {
                        }
                        break;
                    case AttributeConstants.InternalType:
                        if (attribute.Parameters.Length >= 1
                            && attribute.Parameters[0].Position.Range.Contains(p)
                            && attribute.Parameters[0] is StringLiteralExpression)
                        {
                            result.AddRange(InternalTypes.List.Select(v => new CompletionItem()
                            {
                                Label = v,
                                Kind = CompletionItemKind.Constant,
                            }));
                        }
                        break;
                    default:
                        break;
                }
                return result;
            }
        }

        foreach (UsingDefinition @using in AST.Usings)
        {
            if (!@using.Position.Range.Contains(p)) continue;
            if (p <= @using.Keyword.Position.Range.End) continue;

            HashSet<Uri> uris = new();
            foreach (ISourceProvider sourceProvider in CompilerSettings.SourceProviders)
            {
                if (sourceProvider is not ISourceQueryProvider queryProvider) continue;
                foreach (Uri query in queryProvider.GetQuery(@using.PathString, Uri))
                {
                    uris.Add(query);
                }
            }

            foreach (Uri uri in uris)
            {
                if (uri.IsFile)
                {
                    string? dir = System.IO.Path.GetDirectoryName(uri.LocalPath);
                    if (dir is null) continue;
                    if (!Directory.Exists(dir)) continue;

                    foreach (string directory in Directory.GetDirectories(dir))
                    {
                        result.Add(new CompletionItem()
                        {
                            Kind = CompletionItemKind.Folder,
                            Label = System.IO.Path.GetFileNameWithoutExtension(directory),
                        });
                    }

                    foreach (string file in Directory.GetFiles(dir))
                    {
                        if (!file.EndsWith(LanguageConstants.LanguageExtension)) continue;

                        result.Add(new CompletionItem()
                        {
                            Kind = CompletionItemKind.File,
                            Label = System.IO.Path.GetFileNameWithoutExtension(file),
                        });
                    }
                }
            }

            return result;
        }

        foreach (ISmallNode statement in StatementWalker.EnumerateNodes(AST, StatementWalkerFilter.Before(p)))
        {
            GeneralType? objectType = null;
            Expression? objectExpression = null;

            if (statement.Position.Range.Contains(p))
            {
                Logger.Debug($"???: `{statement.GetType().Name}` `{statement}`");
            }

            if (statement is FieldExpression fieldExpression
                && fieldExpression.Position.Range.Contains(p))
            {
                objectExpression = fieldExpression.Object;
                Logger.Debug($"Object: `{objectExpression}`");
            }

            if (statement is IdentifierExpression identifierExpression
                && new Range<SinglePosition>(identifierExpression.Identifier.Position.Range.Start, identifierExpression.Identifier.Position.Range.End + 1).Contains(p)
                && e.Context is not null
                && e.Context.TriggerKind == CompletionTriggerKind.TriggerCharacter
                && e.Context.TriggerCharacter == ".")
            {
                objectExpression = identifierExpression;
                Logger.Debug($"Object: `{objectExpression}`");
            }

            if (objectExpression is not null)
            {
                if (objectExpression is IdentifierExpression objectIdentifierExpression)
                {
                    foreach (CompiledEnum enumDefinition in CompilerResult.Enums)
                    {
                        if (enumDefinition.Identifier != objectIdentifierExpression.Content) continue;
                        if (!enumDefinition.Definition.CanUse(Uri)) continue;

                        Logger.Debug($"Object is an enum type");

                        foreach (CompiledEnumMember enumMember in enumDefinition.Members)
                        {
                            result.Add(new CompletionItem()
                            {
                                Kind = CompletionItemKind.EnumMember,
                                Label = enumMember.Identifier,
                                LabelDetails = new CompletionItemLabelDetails()
                                {
                                    Detail = $" = {enumMember.Value}",
                                },
                            });
                        }
                        return result;
                    }

                    foreach (EnumDefinition enumDefinition in AST.EnumDefinitions)
                    {
                        if (enumDefinition.Identifier.Content != objectIdentifierExpression.Content) continue;
                        if (!enumDefinition.CanUse(Uri)) continue;

                        Logger.Debug($"Object is an enum type");

                        foreach (EnumMemberDefinition enumMember in enumDefinition.Members)
                        {
                            result.Add(new CompletionItem()
                            {
                                Kind = CompletionItemKind.EnumMember,
                                Label = enumMember.Identifier.Content,
                            });
                        }
                        return result;
                    }
                }

                if (objectExpression.CompiledType is not null)
                {
                    objectType = objectExpression.CompiledType;
                    Logger.Debug($"Object type: `{objectType}`");
                }
                else
                {
                    Logger.Warn($"Missing type on {objectExpression.GetType().Name} {objectExpression}");
                }
            }

            if (objectType is not null)
            {
                List<GeneralType> checkTypes = new();

                {
                    GeneralType prevType = objectType;
                    checkTypes.Add(new PointerType(prevType));
                    checkTypes.Add(new ReferenceType(prevType));
                    checkTypes.Add(prevType);
                    while (true)
                    {
                        if (prevType.Is(out PointerType? pointerType2))
                        {
                            prevType = pointerType2.To;
                            checkTypes.Add(prevType);
                        }
                        else if (prevType.Is(out ReferenceType? referenceType2))
                        {
                            prevType = referenceType2.To;
                            checkTypes.Add(prevType);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                Dictionary<string, List<(CompiledFunctionDefinition Function, ImmutableDictionary<string, CompiledTemplateArgument>? TemplateArguments)>> functionOverloads = new();

                foreach (GeneralType prevType in checkTypes)
                {
                    if (prevType.Is(out StructType? structType))
                    {
                        foreach (CompiledField field in structType.Struct.Fields)
                        {
                            result.Add(new CompletionItem()
                            {
                                Kind = CompletionItemKind.Field,
                                Label = field.Identifier,
                                LabelDetails = new CompletionItemLabelDetails()
                                {
                                    Description = GeneralType.TryInsertTemplateParameters(field.Type, structType.TemplateArguments).ToString(),
                                },
                            });
                        }
                    }

                    foreach (CompiledFunctionDefinition function in CompilerResult.FunctionDefinitions)
                    {
                        if (!function.Definition.CanUse(Uri)) continue;
                        if (function.Parameters.Length <= 0) continue;
                        if (!function.Parameters[0].Definition.IsThis) continue;
                        if (!StatementCompiler.IsAssignable(prevType, function.Parameters[0].Type, out _, out _)) continue;

                        if (!functionOverloads.TryGetValue(function.Identifier, out var overloads))
                        { overloads = functionOverloads[function.Identifier] = new(); }
                        overloads.Add((function, (prevType.Is(out StructType? w) && function.Context == w.Struct) ? w.TemplateArguments : null));
                    }
                }

                foreach ((string function, var overloads) in functionOverloads)
                {
                    result.Add(new CompletionItem()
                    {
                        Kind = CompletionItemKind.Function,
                        Label = function,
                        LabelDetails = new CompletionItemLabelDetails()
                        {
                            Description = overloads.Count switch
                            {
                                0 => null,
                                1 => GeneralType.TryInsertTemplateParameters(overloads[0].Function.Type, overloads[0].TemplateArguments).ToString(),
                                _ => $"{overloads.Count} overloads",
                            },
                        },
                    });
                }

                return result;
            }

            if (statement is NewInstanceExpression newInstanceExpression
                && newInstanceExpression.Type.Position.Range.Contains(p))
            {
                AddTypeItems(result);
                return result;
            }

            if (statement is InitializationExpression initializationExpression
                && initializationExpression.Position.Range.Contains(p))
            {
                if (initializationExpression.CompiledType is not null)
                {
                    bool expectsField = true;
                    foreach (InitializationExpressionItem item in initializationExpression.Values)
                    {
                        if (item.Operator.Position.Range.End <= p && p <= (item.Comma?.Position.Range.Start ?? item.Value.Position.Range.End))
                        {
                            expectsField = false;
                        }
                    }

                    if (expectsField)
                    {
                        ImmutableHashSet<string> assignedFields = initializationExpression.Values.Select(v => v.MemberIdentifier.Content).ToImmutableHashSet();

                        foreach (CompiledField item in initializationExpression.CompiledType.Struct.Fields)
                        {
                            if (assignedFields.Contains(item.Identifier)) continue;

                            result.Add(new()
                            {
                                Label = item.Identifier,
                                Kind = CompletionItemKind.Field,
                            });
                        }
                        return result;
                    }
                }
            }

            if (statement is MissingExpression
                && statement.Position.Range.Contains(p))
            {
                AddExpressionItems(result);
                return result;
            }
        }

        foreach (var s in AST.Structs)
        {
            if (s.File != Uri) continue;
            if (!s.Position.Range.Contains(p)) continue;

            foreach (var f in
                s.Functions
                .Cast<FunctionThingDefinition>()
                .Append(s.GeneralFunctions)
                .Append(s.Operators))
            {
                if (!s.Position.Range.Contains(p)) continue;

                goto _;
            }

            foreach (var f in
                s.Fields)
            {
                if (!s.Position.Range.Contains(p)) continue;

                goto _;
            }

            AddTypeItems(result);

            result.AddRange(ProtectionKeywords.List.Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));
            result.AddRange(ModifierKeywords.List.Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));

            AddBigDefinitionSnippets(result);
            return result;
        _:;
        }

        if (AST.GetStatementAt(p, out var atP) && atP is not Block)
        {
            Logger.Trace($"Inside statement {atP.GetType().Name} {atP}");

            AddExpressionItems(result);

            AddTypeItems(result);

            result.AddRange(ModifierKeywords.List.Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));

            return result;
        }

        AddExpressionItems(result);

        AddTypeItems(result);

        result.AddRange(ProtectionKeywords.List.Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));
        result.AddRange(ModifierKeywords.List.Select(v => new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = v }));

        AddStatementItems(result);
        AddBigDefinitionSnippets(result);

        if (!StatementWalker.EnumerateNodes(AST, StatementWalkerFilter.All).Any(v => v.Position.Range.Start <= p))
        {
            result.AddRange(new CompletionItem() { Kind = CompletionItemKind.Keyword, Label = DeclarationKeywords.Using });
        }

        return result;
    }
}
