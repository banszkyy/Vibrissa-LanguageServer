using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using LanguageCore;
using LanguageCore.Bytecode.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using LanguageCore.Workspaces;
using Diagnostic = LanguageCore.Diagnostic;

namespace LanguageServer.DocumentManagers;

enum RecompileLevel
{
    None,
    Light,
    Forced,
    Full,
}

partial class DocumentVibrissa
{
    static readonly ConcurrentDictionary<Uri, CacheItem> Cache = new();

    public ImmutableArray<Token> Tokens;
    public ParserResult AST;
    public CompilerResult CompilerResult;

    public CompilerSettings CompilerSettings;

    Task<bool>? CompilationTask;
    DocumentVersion CompiledVersion;
    DocumentVersion CurrentlyCompilingVersion;
    DocumentVersion DesiredCompiledVersion;
    StatementCompiler? Compiler;

    bool RequestCompilation(DocumentVersion? version, RecompileLevel level = RecompileLevel.Light) => RequestCompilation(version ?? DocumentVersion.Zero(0), level);
    bool RequestCompilation(DocumentVersion version, RecompileLevel level = RecompileLevel.Light)
    {
        if (CompilationTask is not null && !CompilationTask.IsCompleted) return false;
        if (level < RecompileLevel.Forced && (CompiledVersion == version || DesiredCompiledVersion == version)) return false;

        {
            List<string> _frames = [];
            foreach (var item in new StackTrace().ToString().Split('\n'))
            {
                if (!item.Contains(nameof(LanguageServer)))
                {
                    _frames.Add("...");
                    break;
                }

                _frames.Add(item);
            }
            Logger.Debug($"Requesting compilation for {version} ({level})\n{string.Join('\n', _frames)}");
        }

        DesiredCompiledVersion = version;

        CompilationTask = Task.Run(async () =>
        {
            await Task.Delay(500).ConfigureAwait(false);

            if (level < RecompileLevel.Forced && CompiledVersion == DesiredCompiledVersion) return false;

            CompileAsync(level);

            if (DesiredCompiledVersion != CompiledVersion)
            {
                Logger.Debug($"Desired and compiled version mismatch {DesiredCompiledVersion} != {CompiledVersion}");
                RequestCompilation(DesiredCompiledVersion, level);
            }

            return true;
        });
        return true;
    }

    Task<bool> AwaitForCompilation(DocumentVersion? version, RecompileLevel level, CancellationToken cancellationToken) => AwaitForCompilation(version ?? DocumentVersion.Zero(0), level, cancellationToken);
    Task<bool> AwaitForCompilation(DocumentVersion version, RecompileLevel level, CancellationToken cancellationToken)
    {
        if (level == RecompileLevel.None)
        {
            if (CompilationTask is null) return Task.FromResult(false);
            return CompilationTask.WaitAsync(cancellationToken);
        }

        Logger.Trace($"Validating version {version} ({level})");
        RequestCompilation(version, level);
        if (CompilationTask is null) return Task.FromResult(false);
        return CompilationTask.WaitAsync(cancellationToken);
    }

    void CompileAsync(RecompileLevel level, CancellationToken cancellationToken = default)
    {
        CurrentlyCompilingVersion = Version ?? DocumentVersion.Zero(0);

        Logger.Debug($"Compiling {CurrentlyCompilingVersion}");

        OmniSharpService.Instance?.Server?.SendNotification<CompilerStatusNotificationArgs>("meow/compiler/status", new()
        {
            Status = "working",
        });

        VibrissaProject? project = null;

        try
        {
            DiagnosticsCollection diagnostics = new();

            Configuration config = Configuration.Empty;
            Uri? projectRoot = null;

            Logger.Debug($"  Compiling configuraton");

            if (ConfigurationManager.Search(Uri, Documents, out Uri? configurationPath, out _))
            {
                projectRoot = new(configurationPath, ".");
                Logger.Trace($"    Parsing configuration `{configurationPath}`");
                config = Configuration.Parse(configurationPath, diagnostics, new Logger());
                Logger.Trace($"    Configuation parsed");

                if (VibrissaProject.Projects.TryGetValue(configurationPath, out project))
                {
                    project.Configuration = config;
                    project.ConfigurationPath = configurationPath;
                }
                else
                {
                    VibrissaProject.Projects[configurationPath] = project = new VibrissaProject()
                    {
                        Configuration = config,
                        ConfigurationPath = configurationPath,
                    };

                    if (config.IsProject)
                    {
                        if (configurationPath.IsFile)
                        {
                            foreach (string item in System.IO.Directory.EnumerateFiles(projectRoot.LocalPath, $"*{LanguageCore.LanguageConstants.LanguageExtension}", System.IO.SearchOption.AllDirectories))
                            {
                                project.Files.Add(new Uri(item, UriKind.Absolute));
                            }
                        }

                        foreach (DocumentBase item in Documents.OpenedDocuments)
                        {
                            if (projectRoot.IsBaseOf(item.Uri))
                            {
                                project.Files.Add(item.Uri);
                            }
                        }
                    }
                }
            }

            if (project is not null)
            {
                OmniSharpService.Instance?.Server?.SendNotification<ProjectStatusNotificationArgs>("meow/project/status", new()
                {
                    ProjectType = project.Configuration.IsProject ? "project" : "file",
                    ContextFile = Uri.ToString(),
                    IndexedFiles = project.Files.Count,
                    Root = projectRoot!.ToString(),
                });
            }
            else
            {
                OmniSharpService.Instance?.Server?.SendNotification<ProjectStatusNotificationArgs>("meow/project/status", new()
                {
                    ProjectType = null,
                    ContextFile = Uri.ToString(),
                });
            }

            diagnostics.Clear();

            List<ISourceProvider> sourceProviders = [Documents];
            bool isPartialSource = false;

            if (Uri.IsFile)
            {
                sourceProviders.Add(new FileSourceProvider()
                {
                    ExtraDirectories = config.ExtraDirectories,
                });
            }
            else
            {
                isPartialSource = true;
            }

            if (level == RecompileLevel.Full)
            {
                Logger.Debug($"  Clearing cache");
                Cache.Clear();
                Compiler = null;
            }

            List<IExternalFunction> externalFunctions = new();
            externalFunctions.AddRange(config.ExternalFunctions);
            externalFunctions.AddRangeIf(BytecodeProcessor.GetExternalFunctions(VoidIO.Instance), v => !externalFunctions.Any(w => v.Name != w.Name));
            List<ExternalConstant> externalConstants = new();
            externalConstants.AddRange(config.ExternalConstants);
            externalConstants.AddRangeIf([
                new ExternalConstant("heap_size", BytecodeInterpreterSettings.Default.HeapSize)
            ], v => !externalConstants.Any(w => v.Name != w.Name));

            CompilerSettings compilerSettings = CompilerSettings = new(CodeGeneratorForMain.DefaultCompilerSettings)
            {
                Optimizations = OptimizationSettings.None,
                CompileEverything = true,
                SourceProviders = [.. sourceProviders],
                AdditionalImports = config.AdditionalImports,
                ExternalFunctions = externalFunctions.ToImmutableArray(),
                ExternalConstants = externalConstants.ToImmutableArray(),
                TokenizerSettings = new TokenizerSettings(TokenizerSettings.Default)
                {
                    TokenizeComments = true,
                },
                Cache = Cache,
                OptimizationDiagnostics = true,
                SourcePartiallyAvaliable = true,
                CancellationToken = cancellationToken,
            };
            HashSet<Uri> compiledFiles;
            CompilerResult compilerResult = CompilerResult.MakeEmpty(Uri);
            try
            {
                string[] files;
                if (project is null)
                {
                    files = [.. Documents.OpenedDocuments.Select(v => v.Uri.ToString())];
                }
                else if (project.Configuration.IsProject)
                {
                    files = [.. project.Files.Select(v => v.ToString())];
                }
                else
                {
                    files = [Uri.ToString()];
                }

                Logger.Debug($"  Compiling:{string.Join(null, files.Select(v => $"\n   {v}"))}");
                Compiler ??= new(compilerSettings, diagnostics, Logger.Instance);
                Compiler.Diagnostics = diagnostics;
                Compiler.Logger = Logger.Instance;
                compilerResult = Compiler.CompileFiles(files);
                Compiler.IsCached = true;
                //compilerResult = StatementCompiler.CompileFiles(files, compilerSettings, diagnostics, Logger.Instance);
                Logger.Debug($"  Compiled");
            }
            catch (LanguageExceptionAt languageException)
            {
                diagnostics.Add(languageException.ToDiagnostic());
            }
            catch (LanguageException languageException)
            {
                diagnostics.Add(languageException.ToDiagnostic());
            }

            ParsedFile raw = compilerResult.RawTokens.FirstOrDefault(v => v.File == Uri);
            if (raw.Index == null)
            {
                Logger.Warn($"Compiled file not found");
            }
            Tokens = !raw.AST.Tokens.IsDefault ? raw.AST.Tokens : !raw.Tokens.Tokens.IsDefault ? Tokens : ImmutableArray<Token>.Empty;
            AST = raw.AST.IsNotEmpty ? raw.AST : AST;
            CompilerResult = compilerResult;

            compiledFiles = new(compilerResult.RawTokens.Select(v => v.File));
            Logger.Info($"Validated {CurrentlyCompilingVersion}");

            foreach (Diagnostic item in diagnostics.DiagnosticsWithoutContext)
            {
                Logger.Error(item.ToString());
            }

            Dictionary<Uri, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>> diagnosticsPerFile = new();

            static string GetFullMessage(Diagnostic diagnostic, int indent)
            {
                string result = $"{diagnostic.Message}";
                foreach (Diagnostic item in diagnostic.SubErrors)
                {
                    result += $"\n{new string(' ', indent)} -> {GetFullMessage(item, indent + 2)}";
                }
                return result;
            }

            void CompileDiagnostic(Diagnostic diagnostic, Dictionary<Uri, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>> diagnosticsPerFile, DiagnosticsLevel parentLevel = 0)
            {
                if (diagnostic.IgnoreOnPartialSource && isPartialSource) return;

                if (diagnostic is DiagnosticAt diagnosticWithPosition)
                {
                    if (diagnosticWithPosition.File is null)
                    {
                        Logger.Error(diagnosticWithPosition.ToString());
                        return;
                    }

                    if (!diagnosticsPerFile.TryGetValue(diagnosticWithPosition.File, out List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic>? container))
                    { container = diagnosticsPerFile[diagnosticWithPosition.File] = new(); }

                    container.Add(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic()
                    {
                        Severity = (parentLevel > diagnosticWithPosition.Level ? parentLevel : diagnosticWithPosition.Level) switch
                        {
                            DiagnosticsLevel.Error => DiagnosticSeverity.Error,
                            DiagnosticsLevel.Warning => DiagnosticSeverity.Warning,
                            DiagnosticsLevel.Information => DiagnosticSeverity.Information,
                            DiagnosticsLevel.Hint => DiagnosticSeverity.Hint,
                            DiagnosticsLevel.OptimizationNotice => DiagnosticSeverity.Information,
                            DiagnosticsLevel.FailedOptimization => DiagnosticSeverity.Information,
                            _ => throw new UnreachableException(),
                        },
                        Range = diagnosticWithPosition.Position.ToOmniSharp(),
                        Message = GetFullMessage(diagnosticWithPosition, 0),
                        Source = diagnosticWithPosition.File.ToString(),
                        RelatedInformation = Diagnostic.EnumerateAll(diagnostic)
                            .Where(v =>
                                v is not DiagnosticAt w
                                || (w.Location.File == diagnosticWithPosition.Location.File
                                    && w.Location.Position.Union(diagnosticWithPosition.Location.Position).Equals(diagnosticWithPosition.Location.Position)
                                ))
                            .SelectMany(v => v.RelatedInformation)
                            .OfType<DiagnosticRelatedInformationAt>()
                            .Select(v => new OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticRelatedInformation()
                            {
                                Location = v.Location.ToOmniSharp(),
                                Message = v.Message,
                            })
                            .ToArray(),
                        Tags = diagnosticWithPosition.Tag switch
                        {
                            LanguageCore.DiagnosticTag.None => null,
                            LanguageCore.DiagnosticTag.Unnecessary => new(OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticTag.Unnecessary),
                            _ => null,
                        },
                    });

                    foreach (Diagnostic item in diagnostic.SubErrors)
                    {
                        if (item is DiagnosticAt diagnosticWithPosition2
                            && diagnosticWithPosition2.Location.File == diagnosticWithPosition.Location.File
                            && diagnosticWithPosition2.Location.Position.Union(diagnosticWithPosition.Location.Position).Equals(diagnosticWithPosition.Location.Position))
                        { continue; }
                        CompileDiagnostic(item, diagnosticsPerFile, diagnostic.Level);
                    }
                }
                else
                {
                    foreach (Diagnostic item in diagnostic.SubErrors)
                    {
                        CompileDiagnostic(item, diagnosticsPerFile, diagnostic.Level);
                    }
                }
            }

            foreach (DiagnosticAt diagnostic in diagnostics.Diagnostics)
            {
                if (diagnostic.Level == DiagnosticsLevel.OptimizationNotice) continue;
                if (diagnostic.Level == DiagnosticsLevel.FailedOptimization) continue;

                CompileDiagnostic(diagnostic, diagnosticsPerFile);
            }

            foreach (Uri file in compiledFiles)
            {
                diagnosticsPerFile.TryAdd(file, new());
            }

            foreach ((Uri file, List<OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic> fileDiagnostics) in diagnosticsPerFile)
            {
                OmniSharpService.Instance?.Server?.PublishDiagnostics(new PublishDiagnosticsParams()
                {
                    Uri = file,
                    Diagnostics = fileDiagnostics,
                    Version = Documents.TryGet(file, out DocumentBase? document) ? document.Version?.Version : null,
                });
            }

            OmniSharpService.Instance?.Server?.SendNotification<CompilerStatusNotificationArgs>("meow/compiler/status", new()
            {
                Status = "done",
            });
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            Logger.Error(ex);
            OmniSharpService.Instance?.Server?.Window?.ShowError($"Vibrissa {ex.GetType().Name}: {ex.Message}");
            OmniSharpService.Instance?.Server?.SendNotification<CompilerStatusNotificationArgs>("meow/compiler/status", new()
            {
                Status = "failed",
                Details = ex.GetType().Name,
            });
        }
        finally
        {
            CompiledVersion = CurrentlyCompilingVersion;
        }
    }
}
