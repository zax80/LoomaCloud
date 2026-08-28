using LoomaCloud.Core.Abstractions;

namespace LoomaCloud.Application.UseCases;

/// <summary>Reports how many vectors are stored in a collection — backs the CLI `count` command.</summary>
public interface ICountUseCase
{
    Task<long> CountAsync(
        VectorCollection collection,
        CancellationToken cancellationToken = default);
}
