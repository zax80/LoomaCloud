namespace LoomaCloud.Infrastructure.Llm;

/// <summary>Binds to the <c>Security</c> section of config.json.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public IReadOnlyList<string> AllowedInferenceHosts { get; set; } = [];

    /// <summary>
    /// When true (the documented default), any configured inference endpoint
    /// whose host isn't in <see cref="AllowedInferenceHosts"/> fails DI
    /// registration outright. Setting this false is an explicit, auditable
    /// opt-out — not something a bad default should silently allow.
    /// </summary>
    public bool BlockNonLocalEndpoints { get; set; } = true;

    /// <summary>
    /// The counterpart allowlist for <c>Models.BaseModel</c> when its
    /// <c>Provider</c> is a genuine cloud API (currently <c>"OpenAI"</c>),
    /// not a local/private-network endpoint — <see cref="AllowedInferenceHosts"/>'s
    /// "localhost or private CIDR range" semantics don't apply to a real
    /// public API host like <c>api.openai.com</c>, but the same fail-loudly
    /// principle still should: a typo'd or unexpected host in
    /// <c>Models.BaseModel.Endpoint</c> should refuse to start, not silently
    /// send a request (and the API key) somewhere unintended. Only checked
    /// when <c>Models.BaseModel.Provider</c> is a recognized cloud provider;
    /// <c>Models.EmbeddingModel</c>/<c>Models.VisionModel</c>/etc. still go
    /// through <see cref="AllowedInferenceHosts"/> exactly as before — this
    /// fork moves answer generation to the cloud, nothing else.
    /// </summary>
    public IReadOnlyList<string> AllowedCloudHosts { get; set; } = ["api.openai.com"];
}
