using LanguageCore.Compiler;

namespace LanguageServer.DocumentManagers;

sealed partial class DocumentVibrissa
{
    public override async Task<IEnumerable<AllocationItem>?> Decorations(AllocationsParams request, CancellationToken cancellationToken)
    {
        await AwaitForCompilation(Version, RecompileLevel.Light, cancellationToken).ConfigureAwait(false);

        List<AllocationItem> result = new();

        foreach (CompiledStatement v in StatementWalker.EnumerateNodes(CompilerResult, StatementWalkerFilter.All))
        {
            if (v is not IHaveAllocator haveAllocator) continue;
            if (haveAllocator.Allocator is null) continue;
            if (haveAllocator.Allocator.Location.File != Uri) continue;
            result.Add(new()
            {
                Kind = AllocationKind.Box,
                Range = haveAllocator.Allocator.Location.Position.Range.ToOmniSharp(),
            });
        }

        return result;
    }
}
