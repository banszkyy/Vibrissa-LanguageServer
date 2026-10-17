namespace LanguageServer.Handlers;

sealed class DocumentSymbolHandler : IDocumentSymbolHandler
{
    public async Task<SymbolInformationOrDocumentSymbolContainer?> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] DocumentSymbol ({request.TextDocument})");

        if (OmniSharpService.Instance?.Server == null) return null;
        if (!OmniSharpService.Instance.Documents.TryGet(request.TextDocument.Uri, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.TextDocument}\" not found");
            return null;
        }

        try
        {
            return new SymbolInformationOrDocumentSymbolContainer(await document.Symbols(request, cancellationToken).ConfigureAwait(false) ?? Enumerable.Empty<SymbolInformationOrDocumentSymbol>());
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return null;
        }
    }

    public DocumentSymbolRegistrationOptions GetRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
    {
        capability.HierarchicalDocumentSymbolSupport = true;
        return new DocumentSymbolRegistrationOptions()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage(LanguageCore.LanguageConstants.LanguageId),
        };
    }
}
