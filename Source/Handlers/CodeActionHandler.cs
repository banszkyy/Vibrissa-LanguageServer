namespace LanguageServer.Handlers;

sealed class CodeActionHandler : ICodeActionHandler
{
    public async Task<CommandOrCodeActionContainer?> Handle(CodeActionParams request, CancellationToken cancellationToken)
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
            return new CommandOrCodeActionContainer(await document.CodeAction(request, cancellationToken).ConfigureAwait(false) ?? Enumerable.Empty<CommandOrCodeAction>());
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return null;
        }
    }

    public CodeActionRegistrationOptions GetRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage(LanguageCore.LanguageConstants.LanguageId),
        ResolveProvider = false,
        CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.Defaults),
    };
}
