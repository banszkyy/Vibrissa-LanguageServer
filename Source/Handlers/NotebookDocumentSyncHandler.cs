using MediatR;

namespace LanguageServer.Handlers;

sealed class NotebookDocumentSyncHandler : NotebookDocumentSyncHandlerBase
{
    public override NotebookDocumentAttributes GetNotebookDocumentAttributes(DocumentUri uri) => new NotebookDocumentAttributes(uri);

    public override Task<Unit> Handle(DidOpenNotebookDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[NotebookSync] Opened ({request.NotebookDocument.Uri})");

        OmniSharpService.Instance?.Documents.GetOrCreateNotebook(request.NotebookDocument.Uri, languageId: null).OnOpened(request);

        return Task.FromResult(Unit.Value);
    }

    public override Task<Unit> Handle(DidChangeNotebookDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[NotebookSync] Changed ({request.NotebookDocument.Uri})");

        OmniSharpService.Instance?.Documents.GetOrCreateNotebook(request.NotebookDocument.Uri, languageId: null).OnChanged(request);

        return Task.FromResult(Unit.Value);
    }

    public override Task<Unit> Handle(DidSaveNotebookDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[NotebookSync] Saved ({request.NotebookDocument.Uri})");

        OmniSharpService.Instance?.Documents.GetOrCreateNotebook(request.NotebookDocument.Uri, languageId: null).OnSaved(request);

        return Task.FromResult(Unit.Value);
    }

    public override Task<Unit> Handle(DidCloseNotebookDocumentParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[NotebookSync] Closed ({request.NotebookDocument.Uri})");

        OmniSharpService.Instance?.Documents.Remove(request.NotebookDocument.Uri);

        return Task.FromResult(Unit.Value);
    }

    protected override NotebookDocumentSyncOptions CreateRegistrationOptions(NotebookDocumentSyncClientCapabilities capability, ClientCapabilities clientCapabilities) => new()
    {
        NotebookSelector = new NotebookSelector()
        {
            Notebook = new NotebookDocumentFilter()
            {
                NotebookType = $"{LanguageCore.LanguageConstants.LanguageId}-notebook"
            },
        },
    };
}
