namespace LanguageServer.Handlers;

sealed class InlayHintsHandler : IInlayHintsHandler
{
    public async Task<InlayHintContainer?> Handle(InlayHintParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] InlayHints ({request.TextDocument}:{request.Range})");

        if (OmniSharpService.Instance?.Server == null) return null;
        if (!OmniSharpService.Instance.Documents.TryGet(request.TextDocument.Uri, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.TextDocument}\" not found");
            return null;
        }

        try
        {
            IEnumerable<InlayHint>? result = await document.InlayHints(request, cancellationToken).ConfigureAwait(false);
            return result is null ? null : new(result);
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return null;
        }
    }

    public InlayHintRegistrationOptions GetRegistrationOptions(InlayHintClientCapabilities capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage(LanguageCore.LanguageConstants.LanguageId),
    };
}
