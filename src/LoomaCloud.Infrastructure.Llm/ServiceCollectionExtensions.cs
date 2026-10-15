using System.ClientModel;
using LoomaCloud.Core.Abstractions;
using LoomaCloud.Infrastructure.Llm.Audio;
using LoomaCloud.Infrastructure.Llm.Vision;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace LoomaCloud.Infrastructure.Llm;

/// <summary>
/// Wires up chat (<c>Models.BaseModel</c> — a cloud provider, or local Ollama
/// as a fallback) + embedding + vision (captioning and CLIP) + speech-to-text
/// (Whisper), the latter three always local (Ollama / ONNX Runtime /
/// whisper.cpp) — LoomaCloud only moves answer generation off-machine, per
/// docs/loomacloud-project-brief.md.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IChatClient"/> against <c>Models.BaseModel</c> —
    /// the one model role LoomaCloud moves off local inference and onto a
    /// cloud provider's API (see docs/loomacloud-project-brief.md). Dispatches
    /// on <c>Models.BaseModel.Provider</c>:
    /// <list type="bullet">
    /// <item><c>"OpenAI"</c> — a real cloud call, authenticated via
    /// <see cref="ModelEndpointOptions.ApiKeyEnvVar"/>, validated against
    /// <see cref="SecurityOptions.AllowedCloudHosts"/> instead of the
    /// local-only <see cref="SecurityOptions.AllowedInferenceHosts"/>.</item>
    /// <item><c>"Ollama"</c> — kept as a local fallback for
    /// dev/offline-testing without a cloud API key, unchanged from Looma's
    /// own behavior (validated against <see cref="SecurityOptions.AllowedInferenceHosts"/>).</item>
    /// <item>Anything else (e.g. <c>"Anthropic"</c>, <c>"Gemini"</c>) — a
    /// clear <see cref="NotSupportedException"/> at DI-registration time.
    /// "Provider-agnostic from day one" means the seam exists here, not that
    /// every provider is actually implemented yet.</item>
    /// </list>
    /// Throws <see cref="InferenceEndpointNotAllowedException"/> or
    /// <see cref="NotSupportedException"/> immediately (not lazily on first
    /// call) for a bad/unsupported configuration.
    /// </summary>
    public static IServiceCollection AddLoomaCloudChatClient(this IServiceCollection services, IConfiguration configuration)
    {
        var (llmOptions, securityOptions) = BindOptions(configuration);
        var baseModel = llmOptions.BaseModel;

        // Validated eagerly, right here — not deferred into the factory
        // lambda below, which only runs on first resolve. A bad/unsupported
        // Provider or a non-allowlisted Endpoint must fail at registration
        // time, same as every other Add* method in this file.
        OpenAIClient openAiClient = baseModel.Provider switch
        {
            "OpenAI" => CreateCloudOpenAiClient(baseModel, securityOptions, configuration),
            "Ollama" => CreateLocalOpenAiClient(baseModel, securityOptions, nameof(LlmOptions.BaseModel)),
            _ => throw new NotSupportedException(
                $"'{LlmOptions.SectionName}:{nameof(LlmOptions.BaseModel)}:Provider' is set to " +
                $"'{baseModel.Provider}', which LoomaCloud doesn't implement yet. Supported: " +
                "\"OpenAI\" (cloud), \"Ollama\" (local fallback). The seam is provider-agnostic " +
                "by design — adding a new provider means adding a case here, not a redesign.")
        };

        services.AddSingleton<IChatClient>(_ =>
        {
            IChatClient client = openAiClient.GetChatClient(baseModel.Model).AsIChatClient();
            return baseModel.SupportsThinking ? new ReasoningEffortChatClient(client) : client;
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="IEmbeddingGenerator{String, Embedding}"/> against
    /// <c>Models.EmbeddingModel</c>. Same fail-loudly-at-registration behavior
    /// as <see cref="AddLoomaCloudChatClient"/>.
    /// </summary>
    public static IServiceCollection AddLoomaCloudEmbeddingGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        var (llmOptions, securityOptions) = BindOptions(configuration);
        var openAiClient = CreateLocalOpenAiClient(llmOptions.EmbeddingModel, securityOptions, nameof(LlmOptions.EmbeddingModel));

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            openAiClient.GetEmbeddingClient(llmOptions.EmbeddingModel.Model).AsIEmbeddingGenerator());

        return services;
    }

    /// <summary>
    /// The DI key <see cref="OllamaImageCaptioner"/> resolves its
    /// <see cref="IChatClient"/> under. Keyed rather than a second
    /// unkeyed registration — <c>Models.BaseModel</c> and
    /// <c>Models.VisionModel</c> can be different models/endpoints, and an
    /// unkeyed second <c>AddSingleton&lt;IChatClient&gt;</c> would just
    /// overwrite/collide with the chat client's own registration.
    /// </summary>
    public const string VisionChatClientKey = "vision";

    /// <summary>
    /// Registers <see cref="IImageCaptioner"/> (captioning + OCR) against
    /// <c>Models.VisionModel</c>. Same fail-loudly-at-registration endpoint
    /// check as the chat/embedding registrations.
    /// </summary>
    public static IServiceCollection AddLoomaCloudImageCaptioner(this IServiceCollection services, IConfiguration configuration)
    {
        var (llmOptions, securityOptions) = BindOptions(configuration);
        var openAiClient = CreateLocalOpenAiClient(llmOptions.VisionModel, securityOptions, nameof(LlmOptions.VisionModel));

        services.AddKeyedSingleton<IChatClient>(VisionChatClientKey, (_, _) =>
            openAiClient.GetChatClient(llmOptions.VisionModel.Model).AsIChatClient());

        services.AddSingleton<IImageCaptioner>(sp =>
            new OllamaImageCaptioner(sp.GetRequiredKeyedService<IChatClient>(VisionChatClientKey)));

        return services;
    }

    /// <summary>
    /// Registers <see cref="IImageEmbeddingGenerator"/> (CLIP) against
    /// <c>Models.ImageEmbeddingModel</c>. No endpoint/locality check here —
    /// unlike Ollama, this is a local in-process ONNX Runtime session, not
    /// an HTTP call, so there's no network endpoint to validate against
    /// <c>Security.AllowedInferenceHosts</c> in the first place.
    /// </summary>
    public static IServiceCollection AddLoomaCloudImageEmbeddingGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        var (llmOptions, _) = BindOptions(configuration);
        var modelPath = llmOptions.ImageEmbeddingModel.ModelPath
            ?? throw new InvalidOperationException(
                $"'{LlmOptions.SectionName}:{nameof(LlmOptions.ImageEmbeddingModel)}:ModelPath' is not set in config.json.");

        services.AddSingleton<IImageEmbeddingGenerator>(_ => new OnnxClipImageEmbeddingGenerator(modelPath));

        return services;
    }

    /// <summary>
    /// Registers <see cref="ITextToImageEmbeddingGenerator"/> against
    /// <c>Models.ImageEmbeddingModel.TextTower</c> — a no-op (nothing
    /// registered) if that section is absent from config.json, same
    /// "entirely optional, not fatal" stance as
    /// <see cref="LocalModelFileProvisioner.EnsureTextToImageSearchModelReadyAsync"/>.
    /// Callers that need this (only <c>SearchUseCase</c> so far) take it as
    /// an optional constructor dependency and degrade gracefully — see its
    /// doc comment — rather than requiring every deployment to configure
    /// text→image search just to use anything else.
    /// </summary>
    public static IServiceCollection AddLoomaCloudTextToImageEmbeddingGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        var (llmOptions, _) = BindOptions(configuration);
        var textTower = llmOptions.ImageEmbeddingModel.TextTower;
        if (textTower is null
            || string.IsNullOrWhiteSpace(textTower.ModelPath)
            || string.IsNullOrWhiteSpace(textTower.VocabPath)
            || string.IsNullOrWhiteSpace(textTower.MergesPath))
        {
            return services;
        }

        services.AddSingleton<ITextToImageEmbeddingGenerator>(_ =>
            new OnnxClipTextEmbeddingGenerator(textTower.ModelPath, textTower.VocabPath, textTower.MergesPath));

        return services;
    }

    /// <summary>
    /// Registers <see cref="IAudioTranscriber"/> (Whisper) against
    /// <c>Models.SpeechToTextModel</c>. No endpoint/locality check here for
    /// the same reason as <see cref="AddLoomaCloudImageEmbeddingGenerator"/> —
    /// this is a local whisper.cpp process via Whisper.net, not an HTTP call.
    /// </summary>
    public static IServiceCollection AddLoomaCloudAudioTranscriber(this IServiceCollection services, IConfiguration configuration)
    {
        var (llmOptions, _) = BindOptions(configuration);
        var modelPath = llmOptions.SpeechToTextModel.ModelPath
            ?? throw new InvalidOperationException(
                $"'{LlmOptions.SectionName}:{nameof(LlmOptions.SpeechToTextModel)}:ModelPath' is not set in config.json.");

        services.AddSingleton<IAudioTranscriber>(_ => new WhisperAudioTranscriber(modelPath, llmOptions.SpeechToTextModel.Language));

        return services;
    }

    internal static (LlmOptions Llm, SecurityOptions Security) BindOptions(IConfiguration configuration)
    {
        var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        var securityOptions = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
        return (llmOptions, securityOptions);
    }

    /// <summary>
    /// Used for <c>Models.EmbeddingModel</c>/<c>Models.VisionModel</c> (always
    /// local) and for <c>Models.BaseModel</c> when its <c>Provider</c> is
    /// <c>"Ollama"</c> — kept as an explicit method (rather than a bool flag
    /// on <see cref="ValidateEndpoint"/>) so a future reader doesn't have to
    /// trace a boolean back to figure out which allowlist applies.
    /// </summary>
    internal static void ValidateEndpoint(string endpoint, SecurityOptions security, string configPath)
    {
        if (!security.BlockNonLocalEndpoints)
        {
            return;
        }

        ValidateEndpointAgainst(endpoint, security.AllowedInferenceHosts, configPath, "Security.AllowedInferenceHosts");
    }

    /// <summary>
    /// The cloud counterpart of <see cref="ValidateEndpoint"/> — checked
    /// against <see cref="SecurityOptions.AllowedCloudHosts"/> instead of
    /// <see cref="SecurityOptions.AllowedInferenceHosts"/>, and always
    /// enforced (no <c>BlockNonLocalEndpoints</c>-style opt-out): a real API
    /// key is on the line, so there's no "safe to skip this" case the way
    /// there is for a purely local endpoint.
    /// </summary>
    internal static void ValidateCloudEndpoint(string endpoint, SecurityOptions security, string configPath)
    {
        ValidateEndpointAgainst(endpoint, security.AllowedCloudHosts, configPath, "Security.AllowedCloudHosts");
    }

    private static void ValidateEndpointAgainst(string endpoint, IReadOnlyList<string> allowedHosts, string configPath, string allowlistName)
    {
        var uri = new Uri(endpoint);
        if (!InferenceHostAllowlist.IsAllowed(uri, allowedHosts))
        {
            throw new InferenceEndpointNotAllowedException(
                $"Refusing to start: '{configPath}' is configured to '{endpoint}', whose host " +
                $"'{uri.Host}' is not in {allowlistName}. This check exists to guarantee inference " +
                "traffic only ever goes where you've explicitly said it can — fix config.json " +
                "rather than working around this.");
        }
    }

    /// <summary>
    /// Builds an <see cref="OpenAIClient"/> against a local, Ollama-served
    /// endpoint — same behavior Looma itself uses. Validates
    /// <paramref name="model"/>'s endpoint against
    /// <see cref="SecurityOptions.AllowedInferenceHosts"/> first.
    /// </summary>
    private static OpenAIClient CreateLocalOpenAiClient(ModelEndpointOptions model, SecurityOptions security, string modelRoleName)
    {
        ValidateEndpoint(model.Endpoint, security, $"{LlmOptions.SectionName}:{modelRoleName}");

        var openAiCompatibleBase = new Uri($"{model.Endpoint.TrimEnd('/')}/v1");

        // Ollama's OpenAI-compatible surface ignores the API key entirely,
        // but the OpenAI SDK requires a non-empty credential to construct a
        // client — this value is never sent anywhere that checks it.
        //
        // NetworkTimeout overrides the SDK's own 100-second default (see
        // ModelEndpointOptions.TimeoutSeconds for why that's too short here)
        // — without this, a real run's vision-captioning call retried 4
        // times and still failed purely from timing out, never from an
        // actual error.
        return new OpenAIClient(
            new ApiKeyCredential("ollama-local"),
            new OpenAIClientOptions
            {
                Endpoint = openAiCompatibleBase,
                NetworkTimeout = TimeSpan.FromSeconds(model.TimeoutSeconds)
            });
    }

    /// <summary>
    /// Builds an <see cref="OpenAIClient"/> against a genuine cloud OpenAI
    /// endpoint — the one path in LoomaCloud that actually sends data
    /// off-machine. Validates against
    /// <see cref="SecurityOptions.AllowedCloudHosts"/> and requires a real
    /// API key sourced from <see cref="ModelEndpointOptions.ApiKeyEnvVar"/>
    /// (never a raw key in config.json).
    /// </summary>
    private static OpenAIClient CreateCloudOpenAiClient(
        ModelEndpointOptions model, SecurityOptions security, IConfiguration configuration)
    {
        var configPath = $"{LlmOptions.SectionName}:{nameof(LlmOptions.BaseModel)}";
        ValidateCloudEndpoint(model.Endpoint, security, configPath);

        if (string.IsNullOrWhiteSpace(model.ApiKeyEnvVar))
        {
            throw new InvalidOperationException(
                $"'{configPath}:ApiKeyEnvVar' must be set when '{configPath}:Provider' is \"OpenAI\" — " +
                "LoomaCloud never reads a raw API key from config.json.");
        }

        // Environment variable wins; otherwise the user-secrets store (the
        // composition root adds it to `configuration`) — see docs/secrets.md.
        var apiKey = Environment.GetEnvironmentVariable(model.ApiKeyEnvVar) ?? configuration[model.ApiKeyEnvVar];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Secret '{model.ApiKeyEnvVar}' (named by '{configPath}:ApiKeyEnvVar') is not set — neither as an " +
                "environment variable nor in user secrets. Set it before starting LoomaCloud; see docs/secrets.md.");
        }

        var openAiCompatibleBase = new Uri($"{model.Endpoint.TrimEnd('/')}/v1");

        return new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = openAiCompatibleBase,
                NetworkTimeout = TimeSpan.FromSeconds(model.TimeoutSeconds)
            });
    }
}
