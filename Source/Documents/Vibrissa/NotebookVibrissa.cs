namespace LanguageServer.DocumentManagers;

sealed partial class NotebookVibrissa : NotebookBase
{
    public NotebookVibrissa(DocumentUri uri, string languageId, Documents app) : base(uri, languageId, app)
    {
    }
}
