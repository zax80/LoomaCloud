using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoomaCloud.MCP.Client;

/// <summary>
/// Must match <c>LoomaCloud.MCP.Server.Wire</c> exactly — both sides serialize/
/// deserialize the same <c>LoomaCloud.Core.Entities</c> records
/// (<c>IndexingProgress</c>, <c>VectorSearchResult</c>, <c>AnswerToken</c>)
/// directly as MCP progress-notification payloads, no separate DTO layer.
/// Duplicated here rather than shared via a third project — it's one line
/// of actual configuration; not worth the extra project for that.
/// </summary>
internal static class Wire
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
