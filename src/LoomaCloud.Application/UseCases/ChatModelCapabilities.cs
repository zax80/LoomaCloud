using LoomaCloud.Application.Configuration;
using Microsoft.Extensions.Options;

namespace LoomaCloud.Application.UseCases;

/// <summary>
/// Local (Standalone mode, and <c>LoomaCloud.MCP.Server</c> itself) implementation
/// of <see cref="IChatModelCapabilities"/> — a trivial synchronous read of
/// already-bound config wrapped in <see cref="Task.FromResult{TResult}"/>
/// to satisfy the interface's async shape, which exists for
/// <c>LoomaCloud.MCP.Client.RemoteChatModelCapabilities</c>'s benefit, not
/// this one's.
/// </summary>
public sealed class ChatModelCapabilities : IChatModelCapabilities
{
    private readonly ChatModelOptions _options;

    public ChatModelCapabilities(IOptions<ChatModelOptions> options)
    {
        _options = options.Value;
    }

    public Task<bool> SupportsThinkingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_options.SupportsThinking);
}
