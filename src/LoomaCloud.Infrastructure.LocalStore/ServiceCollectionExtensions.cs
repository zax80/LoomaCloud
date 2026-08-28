using LoomaCloud.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoomaCloud.Infrastructure.LocalStore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FileChatSessionStore"/>/<see cref="FileSavedAnswerStore"/>
    /// as <see cref="IChatSessionStore"/>/<see cref="ISavedAnswerStore"/>,
    /// bound from the <c>ChatHistory</c> section of configuration.
    /// </summary>
    public static IServiceCollection AddLoomaCloudLocalChatStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ChatHistoryOptions>().Bind(configuration.GetSection(ChatHistoryOptions.SectionName));

        services.AddSingleton<IChatSessionStore, FileChatSessionStore>();
        services.AddSingleton<ISavedAnswerStore, FileSavedAnswerStore>();

        return services;
    }
}
