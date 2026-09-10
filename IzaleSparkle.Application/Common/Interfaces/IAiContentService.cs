using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Application.Common.Interfaces;

/// <summary>
/// Generates product copy, captions and hashtags from product photos.
/// Implemented by the Gemini free-tier client in Infrastructure.
/// </summary>
public interface IAiContentService
{
    /// <summary>True when an API key is configured — lets the UI show a helpful message instead of failing.</summary>
    bool IsConfigured { get; }

    Task<GeneratedProductContent> GenerateProductContentAsync(
        GenerateProductContentRequest request, CancellationToken ct = default);

    /// <summary>Models the configured key can actually call, newest-useful first.</summary>
    Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken ct = default);
}
