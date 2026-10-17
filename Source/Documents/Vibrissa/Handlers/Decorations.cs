using LanguageCore.Compiler;
using LanguageCore.Parser;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<IEnumerable<AllocationItem>?> Decorations(AllocationsParams request, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, cancellationToken).ConfigureAwait(false);

        List<AllocationItem> result = new();

        CompilerResult.EnumerateStatements(v =>
        {
            if (v is not IHaveAllocator haveAllocator) return true;
            Logger.Error(haveAllocator.Allocator);
            if (haveAllocator.Allocator is null) return true;
            if (haveAllocator.Allocator.Location.File != Uri) return true;
            result.Add(new()
            {
                Kind = AllocationKind.Box,
                Range = haveAllocator.Allocator.Location.Position.Range.ToOmniSharp(),
            });
            return true;
        });

        return result;
    }
}
