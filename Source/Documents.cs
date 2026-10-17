using LanguageCore;
using LanguageServer.DocumentManagers;

namespace LanguageServer;

sealed class Documents : ISourceProviderSync, ISourceQueryProvider, IVersionProvider
{
    readonly List<DocumentBase> _documents;
    readonly List<NotebookBase> _notebooks;

    public IReadOnlyCollection<DocumentBase> OpenedDocuments => _documents;
    public IReadOnlyCollection<NotebookBase> OpenedNotebooks => _notebooks;

    public Documents()
    {
        _documents = new List<DocumentBase>();
        _notebooks = new List<NotebookBase>();
    }

    public static DocumentBase GenerateDocument(DocumentUri uri, string? content, string languageId, Documents documentInterface) => languageId switch
    {
        LanguageConstants.LanguageId => new DocumentVibrissa(uri, content, languageId, documentInterface),
        _ => throw new ServiceException($"Unknown language \"{languageId}\"")
    };

    public static NotebookBase GenerateNotebook(DocumentUri uri, string languageId, Documents documentInterface) => languageId switch
    {
        LanguageConstants.LanguageId => new NotebookVibrissa(uri, languageId, documentInterface),
        _ => throw new ServiceException($"Unknown language \"{languageId}\"")
    };

    public bool TryGet(DocumentUri uri, [NotNullWhen(true)] out DocumentBase? document)
    {
        for (int i = 0; i < _documents.Count; i++)
        {
            if (_documents[i].Uri == uri)
            {
                document = _documents[i];
                return true;
            }
        }
        document = null;
        return false;
    }

    public bool TryGetNotebook(DocumentUri uri, [NotNullWhen(true)] out NotebookBase? notebook)
    {
        for (int i = 0; i < _notebooks.Count; i++)
        {
            if (_notebooks[i].Uri == uri)
            {
                notebook = _notebooks[i];
                return true;
            }
        }
        notebook = null;
        return false;
    }

    public void Remove(DocumentUri documentId)
    {
        Logger.Debug($"[Docs] Unregister ({documentId})");

        for (int i = _documents.Count - 1; i >= 0; i--)
        {
            if (_documents[i].Uri == documentId)
            {
                _documents.RemoveAt(i);
            }
        }

        for (int i = _notebooks.Count - 1; i >= 0; i--)
        {
            if (_notebooks[i].Uri == documentId)
            {
                _notebooks.RemoveAt(i);
            }
        }
    }

    public void RemoveDuplicates()
    {
        for (int i = _documents.Count - 1; i >= 0; i--)
        {
            for (int j = _documents.Count - 1; j >= i + 1; j--)
            {
                if (_documents[i].Uri == _documents[j].Uri)
                {
                    _documents.RemoveAt(i);
                }
            }
        }
    }

    public DocumentBase GetOrCreate(DocumentUri documentId, string? content = null)
    {
        RemoveDuplicates();

        if (TryGet(documentId, out DocumentBase? document))
        { return document; }

        Logger.Debug($"[Docs] Register ({documentId})");

        if (documentId.Scheme == "file")
        {
            if (content is null)
            {
                string path = System.Net.WebUtility.UrlDecode(documentId.ToUri().AbsolutePath);
                if (!System.IO.File.Exists(path))
                { throw new ServiceException($"File not found: \"{path}\""); }
                content = System.IO.File.ReadAllText(path);
            }
        }

        document = GenerateDocument(documentId, content, documentId.GetExtension(), this);
        _documents.Add(document);

        return document;
    }

    public NotebookBase GetOrCreateNotebook(DocumentUri documentId)
    {
        RemoveDuplicates();

        if (TryGetNotebook(documentId, out var document))
        { return document; }

        Logger.Debug($"[Docs] Register notebook ({documentId})");

        document = GenerateNotebook(documentId, documentId.GetExtension(), this);
        _notebooks.Add(document);

        return document;
    }

    public IEnumerable<Uri> GetQuery(string requestedFile, Uri? currentFile)
    {
        if (!requestedFile.EndsWith(LanguageConstants.LanguageExtension, StringComparison.Ordinal))
        {
            requestedFile += LanguageConstants.LanguageExtension;
        }

        if (Uri.TryCreate(currentFile, requestedFile, out Uri? uri))
        {
            yield return uri;
        }
    }

    public SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile)
    {
        Uri? lastUri = null;

        foreach (Uri query in GetQuery(requestedFile, currentFile))
        {
            lastUri = query;

            foreach (DocumentBase document in _documents)
            {
                if (document.Uri != query) continue;
                if (document.Content is null)
                {
                    Logger.Debug($"[Compiler] Document provided by client (no content) ({document.DocumentUri})");
                    return SourceProviderResultSync.Error(query, "Document not loaded");
                }
                else
                {
                    Logger.Debug($"[Compiler] Document provided by client (size: {document.Content.Length} bytes) ({document.DocumentUri})");
                    return SourceProviderResultSync.Success(query, document.Content);
                }
            }
        }

        if (lastUri is not null)
        {
            return SourceProviderResultSync.NotFound(lastUri!);
        }
        else
        {
            return SourceProviderResultSync.NextHandler();
        }
    }

    public bool TryGetVersion(Uri uri, out ulong version)
    {
        version = default;

        foreach (DocumentBase document in _documents)
        {
            if (document.Uri != uri) continue;
            if (document.Content is null || !document.Version.HasValue) return false;
            version = document.Version.Value.ToULong();
        }

        return false;
    }
}
