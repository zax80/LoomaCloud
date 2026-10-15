# Secrets (API keys)

Two secrets exist, both referenced **by name** from config.json (never stored
in it):

| Secret name | Used by | Config key naming it |
| --- | --- | --- |
| `LOOMACLOUD_OPENAI_API_KEY` | MCP.Server (OpenAI calls) | `Models:BaseModel:ApiKeyEnvVar` |
| `LOOMA_MCP_API_KEY` | MCP.Server, CLI, MAUI Desktop (must be the same value everywhere) | `Mcp:Auth:ApiKeyEnvVar` |

Lookup order: an environment variable with that name, then the .NET **user
secrets** store. CLI, MCP.Server and MAUI Desktop share one `UserSecretsId`,
so one store serves all three. It lives outside the repo, in
`%APPDATA%\Microsoft\UserSecrets\a7d2c4e1-3b5f-4c68-9e0a-1f2b3c4d5e6f\secrets.json`.

## Set them once

```powershell
cd D:\GitHub\LoomaCloud
dotnet user-secrets set LOOMACLOUD_OPENAI_API_KEY "<openai key>" --project src\LoomaCloud.MCP.Server
dotnet user-secrets set LOOMA_MCP_API_KEY "<long random string>" --project src\LoomaCloud.MCP.Server
dotnet user-secrets list --project src\LoomaCloud.MCP.Server
```

In Visual Studio: right-click `LoomaCloud.MCP.Server` → *Manage User Secrets*
and paste:

```json
{
  "LOOMACLOUD_OPENAI_API_KEY": "<openai key>",
  "LOOMA_MCP_API_KEY": "<long random string>"
}
```

After that, start the server/UI/CLI with no `$env:` lines. An environment
variable, if set, overrides the stored secret.

Note: user secrets are plaintext in your Windows profile (not in the repo, so
they can't be committed). Use a project-scoped OpenAI key with a spending limit.
