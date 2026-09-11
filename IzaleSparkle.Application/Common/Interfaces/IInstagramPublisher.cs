using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Application.Common.Interfaces;

/// <summary>
/// Publishes a single photo post to the linked Instagram Business/Creator account
/// via the Meta Graph API. Content publishing is free — only messaging is billed.
/// Implemented by the Graph API client in Infrastructure.
/// </summary>
public interface IInstagramPublisher
{
    /// <summary>True when an IG user id and access token are configured.</summary>
    bool IsConfigured { get; }

    /// <summary>The connected account's @username, or null when it can't be read.</summary>
    Task<string?> GetAccountNameAsync(CancellationToken ct = default);

    /// <summary>
    /// Posts one photo with a caption. <paramref name="publicImageUrl"/> must be an
    /// absolute URL Meta's servers can fetch — localhost and private hosts will fail.
    /// </summary>
    Task<InstagramPublishResult> PublishPhotoAsync(
        string publicImageUrl, string caption, CancellationToken ct = default);
}
