namespace LanguageServer.Handlers;

sealed class RecompileHandler : IRecompileHandler
{
    public async Task<RecompileResponse> Handle(RecompileParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] Recompile ({request.File})");

        if (OmniSharpService.Instance?.Server == null) return new RecompileResponse(false);
        if (!OmniSharpService.Instance.Documents.TryGet(request.File, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.File}\" not found");
            return new RecompileResponse(false);
        }

        try
        {
            return new RecompileResponse(await document.Recompile(request, cancellationToken).ConfigureAwait(false));
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"ServiceException: {error.Message}");
            return new RecompileResponse(false);
        }
    }
}
