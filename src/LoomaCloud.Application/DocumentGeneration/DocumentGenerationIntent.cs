using LoomaCloud.Application.UseCases;
using LoomaCloud.Core.Entities;

namespace LoomaCloud.Application.DocumentGeneration;

/// <summary>Result of <see cref="DocumentGenerationIntentDetector.Detect"/> — non-null means "offer a document export for this turn".</summary>
public sealed record DocumentGenerationIntent
{
    /// <summary>Format to default the export button to.</summary>
    public required DocumentExportFormat Format { get; init; }
}
