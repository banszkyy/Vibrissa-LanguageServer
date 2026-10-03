using OmniSharp.Extensions.JsonRpc;

namespace LanguageServer;

public interface IRecompileHandler : IJsonRpcRequestHandler<RecompileParams, RecompileResponse>;

public record RecompileParams(string File) : MediatR.IRequest<RecompileResponse>;

public record RecompileResponse(bool Ok) : MediatR.IRequest;
