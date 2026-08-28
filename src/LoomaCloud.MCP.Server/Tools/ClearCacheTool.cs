using System.ComponentModel;
using LoomaCloud.Core.Abstractions;
using LoomaCloud.Core.Exceptions;
using ModelContextProtocol.Server;

namespace LoomaCloud.MCP.Server.Tools;

/// <summary>Wraps <see cref="IAnswerCache.ClearAsync"/> — same operation as the CLI's <c>clear-cache</c> command.</summary>
[McpServerToolType]
public static class ClearCacheTool
{
    [McpServerTool(Name = "looma_clear_cache", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Wipes LoomaCloud's answer cache (both the exact-match and semantic layers). Use after a prompt, model, or config change that a re-index wouldn't otherwise invalidate.")]
    public static async Task<string> ClearCache(
        IAnswerCache answerCache,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await answerCache.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (VectorStoreUnavailableException ex)
        {
            throw ToolErrorTranslation.Translate(ex);
        }

        return "Answer cache cleared.";
    }
}
