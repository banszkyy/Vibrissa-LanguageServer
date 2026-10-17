namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<bool> Recompile(RecompileParams request, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, cancellationToken).ConfigureAwait(false);

        bool ok = RequestCompilation(Version, CompilationReason.Requested, true);

        return ok;
    }
}
