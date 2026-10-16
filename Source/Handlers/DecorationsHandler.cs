using OmniSharp.Extensions.JsonRpc;

namespace LanguageServer.Handlers;

sealed class DecorationsHandler : IJsonRpcRequestHandler<AllocationsParams, AllocationsResult>
{
    public async Task<AllocationsResult> Handle(AllocationsParams request, CancellationToken cancellationToken)
    {
        Logger.Debug($"[Handler] Decorations ({request.TextDocument.Uri})");

        if (OmniSharpService.Instance?.Server == null) return new AllocationsResult();
        if (!OmniSharpService.Instance.Documents.TryGet(request.TextDocument.Uri, out DocumentBase? document))
        {
            Logger.Warn($"Document \"{request.TextDocument.Uri}\" not found");
            return new AllocationsResult();
        }

        try
        {
            return new AllocationsResult()
            {
                Items = (await document.Decorations(request, cancellationToken).ConfigureAwait(false))?.ToArray() ?? [],
                Version = document.Version?.Version ?? 0,
            };
        }
        catch (ServiceException error)
        {
            OmniSharpService.Instance.Server?.Window?.ShowWarning($"BBLang ServiceException: {error.Message}");
            return new AllocationsResult();
        }
    }
}
