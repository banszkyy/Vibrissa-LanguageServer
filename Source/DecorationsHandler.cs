using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.JsonRpc;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace LanguageServer;

[Parallel, Method("meow/decorations", Direction.ClientToServer)]
public sealed record AllocationsParams : IRequest<AllocationsResult>
{
    public TextDocumentIdentifier TextDocument { get; init; } = null!;
}

public sealed record AllocationsResult
{
    public int Version { get; init; }
    public IReadOnlyList<AllocationItem> Items { get; init; } = Array.Empty<AllocationItem>();
}

public sealed record AllocationItem
{
    public Range Range { get; init; } = null!;
    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public AllocationKind Kind { get; init; }
}

public enum AllocationKind
{
    Box,
    Closure,
    String
}
