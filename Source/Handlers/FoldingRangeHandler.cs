namespace LanguageServer.Handlers;

sealed class FoldingRangeHandler : IFoldingRangeHandler
{
    public async Task<Container<FoldingRange>?> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] CodeAction ({request.TextDocument})");

        if (OmniSharpService.Instance?.Server == null) return null;
        if (!OmniSharpService.Instance.Documents.TryGet(request.TextDocument.Uri, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.TextDocument}\" not found");
            return null;
        }

        try
        {
            return new Container<FoldingRange>(await document.FoldingRange(request, cancellationToken).ConfigureAwait(false) ?? Enumerable.Empty<FoldingRange>());
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return null;
        }
    }

    public FoldingRangeRegistrationOptions GetRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage(LanguageCore.LanguageConstants.LanguageId),
    };
}
