namespace LanguageServer.Handlers;

sealed class CompletionHandler : ICompletionHandler
{
    public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] Completion ({request.TextDocument}:{request.Position.ToStringMin()}) ({request.Context})");

        if (OmniSharpService.Instance?.Server == null) return new CompletionList();
        if (!OmniSharpService.Instance.Documents.TryGet(request.TextDocument.Uri, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.TextDocument}\" not found");
            return new CompletionList();
        }

        try
        {
            return new CompletionList(await document.Completion(request, cancellationToken).ConfigureAwait(false) ?? Enumerable.Empty<CompletionItem>());
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return new CompletionList();
        }
    }

    public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
    {
        capability.ContextSupport = false;
        return new CompletionRegistrationOptions()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage(LanguageCore.LanguageConstants.LanguageId),
            ResolveProvider = false,
            TriggerCharacters = new string[] { ".", "(" },
            AllCommitCharacters = new string[] { ".", "(" },
            CompletionItem = new()
            {
                LabelDetailsSupport = true,
            },
        };
    }
}
