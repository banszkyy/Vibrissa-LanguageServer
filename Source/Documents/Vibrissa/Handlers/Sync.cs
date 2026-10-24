namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override void OnChanged(DidChangeTextDocumentParams e)
    {
        base.OnChanged(e);
    }

    public override void OnSaved(DidSaveTextDocumentParams e)
    {
        base.OnSaved(e);
        RequestCompilation(Version);
    }

    public override void OnOpened(DidOpenTextDocumentParams e)
    {
        base.OnOpened(e);
        RequestCompilation(Version);
    }
}
