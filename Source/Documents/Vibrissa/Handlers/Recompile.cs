namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<bool> Recompile(RecompileParams request, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, cancellationToken, RecompileLevel.Light).ConfigureAwait(false);

        bool ok = RequestCompilation(Version, RecompileLevel.Full);

        return ok;
    }
}
