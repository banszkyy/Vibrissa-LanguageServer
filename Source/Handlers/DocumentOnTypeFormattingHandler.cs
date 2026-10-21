namespace LanguageServer.Handlers;

sealed class DocumentOnTypeFormattingHandler : IDocumentOnTypeFormattingHandler
{
    public async Task<TextEditContainer?> Handle(DocumentOnTypeFormattingParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] OnTypeFormatting ({request.TextDocument})");

        if (OmniSharpService.Instance?.Server == null) return null;
        if (!OmniSharpService.Instance.Documents.TryGet(request.TextDocument.Uri, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.TextDocument}\" not found");
            return null;
        }

        try
        {
            return new TextEditContainer(await document.OnTypeFormatting(request, cancellationToken).ConfigureAwait(false) ?? Enumerable.Empty<TextEdit>());
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return null;
        }
    }

    public DocumentOnTypeFormattingRegistrationOptions GetRegistrationOptions(DocumentOnTypeFormattingCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage(LanguageCore.LanguageConstants.LanguageId),
        FirstTriggerCharacter = "{",
        MoreTriggerCharacter = new(["\n"]),
    };
}
