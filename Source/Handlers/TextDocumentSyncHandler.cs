using MediatR;

namespace LanguageServer.Handlers;

sealed class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    static readonly TextDocumentSelector DocumentSelector = new(new TextDocumentFilter() { Pattern = $"**/*{LanguageCore.LanguageConstants.LanguageExtension}" });

    public static TextDocumentChangeRegistrationOptions GetRegistrationOptions() => new()
    {
        DocumentSelector = DocumentSelector,
        SyncKind = TextDocumentSyncKind.Full,
    };

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, uri.GetExtension());

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[DocSync] Opened ({request.TextDocument})");

        OmniSharpService.Instance?.Documents.GetOrCreate(request.TextDocument.Uri).OnOpened(request);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[DocSync] Changed ({request.TextDocument})");

        OmniSharpService.Instance?.Documents.GetOrCreate(request.TextDocument.Uri).OnChanged(request);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[DocSync] Saved ({request.TextDocument})");

        OmniSharpService.Instance?.Documents.GetOrCreate(request.TextDocument.Uri).OnSaved(request);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[DocSync] Closed ({request.TextDocument})");

        OmniSharpService.Instance?.Documents.Remove(request.TextDocument.Uri);

        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = DocumentSelector,
    };
}
