using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;

namespace LoomaCloud.MCP.Client;

/// <summary>
/// Connects to a remote <c>LoomaCloud.MCP.Server</c> using the same
/// <c>Deployment:McpServerEndpoint</c> / <c>Mcp:Auth</c> config keys the
/// server itself validates at startup — deliberately fails the same way the
/// server does (loud, at connection time) rather than deferring an
/// unauthenticated/misconfigured failure to the first tool call.
/// </summary>
public static class LoomaCloudMcpConnection
{
    public static async Task<McpClient> ConnectAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["Deployment:McpServerEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "Deployment:McpServerEndpoint is not set in config.json — required when Deployment:Mode is \"McpClient\".");
        }

        var authMode = configuration["Mcp:Auth:Mode"];
        if (!string.Equals(authMode, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Mcp:Auth:Mode is '{authMode}', but LoomaCloud.MCP.Client only supports \"ApiKey\" so far.");
        }

        var apiKeyEnvVar = configuration["Mcp:Auth:ApiKeyEnvVar"];
        if (string.IsNullOrWhiteSpace(apiKeyEnvVar))
        {
            throw new InvalidOperationException("Mcp:Auth:ApiKeyEnvVar is not set in config.json.");
        }

        // Environment variable wins; otherwise the user-secrets store (the
        // composition root adds it to `configuration`) — see docs/secrets.md.
        var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar) ?? configuration[apiKeyEnvVar];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Secret '{apiKeyEnvVar}' is not set (neither as an environment variable nor in user secrets). " +
                "It needs to hold the same API key the remote LoomaCloud.MCP.Server was started with. See docs/secrets.md.");
        }

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpoint),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiKey}"
            }
        });

        return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
