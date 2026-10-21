using LanguageCore.Compiler;
using LanguageCore.Parser;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<IEnumerable<TextEdit>?> OnTypeFormatting(DocumentOnTypeFormattingParams request, CancellationToken cancellationToken)
    {
        //await AwaitForCompilation(Version, cancellationToken).ConfigureAwait(false);

        List<TextEdit> res = [];

        if (request.Character == "{")
        {
            res.Add(new TextEdit()
            {
                Range = new(new(request.Position.Line, request.Position.Character), new(request.Position.Line, request.Position.Character + 1)),
                NewText = "\n{\n}",
            });
        }

        return res;
    }
}
