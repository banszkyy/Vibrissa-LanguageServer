namespace LanguageServer.Handlers;

sealed class ReferencesHandler : IReferencesHandler
{
    public async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] References ({request.TextDocument}:{request.Position.ToStringMin()}) ({request.Context})");

        if (OmniSharpService.Instance?.Server == null) return null;
        if (!OmniSharpService.Instance.Documents.TryGet(request.TextDocument.Uri, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.TextDocument}\" not found");
            return null;
        }

        try
        {
            IEnumerable<Location>? result = await document.References(request, cancellationToken).ConfigureAwait(false);
            return result is null ? null : new LocationContainer(result);
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return null;
        }
    }

    public ReferenceRegistrationOptions GetRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage(LanguageCore.LanguageConstants.LanguageId),
    };
}
