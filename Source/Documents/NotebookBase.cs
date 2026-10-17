namespace LanguageServer;

abstract class NotebookBase
{
    public Uri Uri => DocumentUri.ToUri();
    public DocumentUri DocumentUri { get; private set; }
    public string LanguageId { get; private set; }
    public string? NotebookType { get; private set; }
    public int? Version { get; private set; }
    public string Path
    {
        get
        {
            string result = Uri.AbsolutePath;
            result = System.Net.WebUtility.UrlDecode(result);
            result = System.IO.Path.GetFullPath(result);
            return result;
        }
    }
    protected List<NotebookCell> Cells { get; }

    protected Documents Documents { get; }

    protected NotebookBase(DocumentUri uri, string languageId, Documents app)
    {
        DocumentUri = uri;
        LanguageId = languageId;
        Documents = app;
        Cells = new List<NotebookCell>();
    }

    public virtual void OnOpened(DidOpenNotebookDocumentParams e)
    {
        Logger.Debug($"[NotebookBuffer] Updated ({e.NotebookDocument.Uri})");

        foreach (TextDocumentItem item in e.CellTextDocuments)
        {
            Documents.GetOrCreate(item.Uri, item.Text).OnOpened(new DidOpenTextDocumentParams()
            {
                TextDocument = item,
            });
        }

        Cells.Clear();
        foreach (NotebookCell cell in e.NotebookDocument.Cells)
        {
            Cells.Add(cell);
        }

        NotebookType = e.NotebookDocument.NotebookType;
        DocumentUri = e.NotebookDocument.Uri;
        LanguageId = LanguageCore.LanguageConstants.LanguageId;
        Version = e.NotebookDocument.Version;
    }

    public virtual void OnChanged(DidChangeNotebookDocumentParams e)
    {
        Logger.Debug($"[NotebookBuffer] Updated ({e.NotebookDocument.Uri})");

        if (e.Change.Cells.Structure is not null)
        {
            Logger.Debug($"[NotebookBuffer]  - Structure change");
            Logger.Debug($"[NotebookBuffer]  - Removing {e.Change.Cells.Structure.Array.DeleteCount} cells from {e.Change.Cells.Structure.Array.Start}");

            Cells.RemoveRange((int)e.Change.Cells.Structure.Array.Start, (int)e.Change.Cells.Structure.Array.DeleteCount);

            if (e.Change.Cells.Structure.Array.Cells is not null)
            {
                Logger.Debug($"[NotebookBuffer]  - Inserting {e.Change.Cells.Structure.Array.Cells!.Count()} cells to {e.Change.Cells.Structure.Array.Start}");
                Cells.InsertRange((int)e.Change.Cells.Structure.Array.Start, e.Change.Cells.Structure.Array.Cells!);
            }

            if (e.Change.Cells.Structure.DidClose is not null)
            {
                Logger.Debug($"[NotebookBuffer]  - Closing {e.Change.Cells.Structure.DidClose.Count()} cells");

                foreach (TextDocumentIdentifier cell in e.Change.Cells.Structure.DidClose)
                {
                    Logger.Debug($"[NotebookBuffer]    - Closing cell {cell.Uri}");

                    int i = Cells.FindIndex(v => v.Document == cell.Uri);
                    if (i == -1)
                    {
                        Logger.Error($"Failed to handle notebook change event: Failed to apply cell remove: Cell {cell.Uri} not found in notebook");
                        continue;
                    }
                    Documents.Remove(cell.Uri);
                    Cells.RemoveAt(i);
                }
            }

            if (e.Change.Cells.Structure.DidOpen is not null)
            {
                Logger.Debug($"[NotebookBuffer]  - Opening {e.Change.Cells.Structure.DidOpen.Count()} cells");

                foreach (TextDocumentItem cell in e.Change.Cells.Structure.DidOpen)
                {
                    Logger.Debug($"[NotebookBuffer]    - Opening cell {cell.Uri}");

                    Documents.GetOrCreate(cell.Uri, cell.Text).OnOpened(new DidOpenTextDocumentParams() { TextDocument = cell });
                }
            }
        }

        if (e.Change.Cells.Data is not null)
        {
            Logger.Debug($"[NotebookBuffer]  - Changing {e.Change.Cells.Data.Count()} cells");

            foreach (NotebookCell cell in e.Change.Cells.Data)
            {
                Logger.Debug($"[NotebookBuffer]    - Changing cell {cell.Document}");

                int i = Cells.FindIndex(v => v.Document == cell.Document);
                if (i == -1)
                {
                    Logger.Error($"Failed to handle notebook change event: Failed to set cell data: Cell {cell.Document} not found in notebook");
                    continue;
                }

                Cells[i] = cell;
            }
        }

        Documents.GetOrCreate(e.Change.Cells.TextContent.Document.Uri).OnChanged(new DidChangeTextDocumentParams()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier()
            {
                Uri = e.Change.Cells.TextContent.Document.Uri,
                Version = e.Change.Cells.TextContent.Document.Version,
            },
            ContentChanges = e.Change.Cells.TextContent.Changes,
        });

        DocumentUri = e.NotebookDocument.Uri;
        LanguageId = e.NotebookDocument.Uri.GetExtension();
        Version = e.NotebookDocument.Version;
    }

    public virtual void OnSaved(DidSaveNotebookDocumentParams e)
    {
        Logger.Debug($"[NotebookBuffer] Updated ({e.NotebookDocument.Uri})");

        DocumentUri = e.NotebookDocument.Uri;
        LanguageId = e.NotebookDocument.Uri.GetExtension();
    }

    public override string ToString() => $"{Path}";
}
