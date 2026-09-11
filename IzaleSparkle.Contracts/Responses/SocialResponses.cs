namespace IzaleSparkle.Contracts.Responses;

// ── SOCIAL PUBLISHING ────────────────────────────────────────
/// <summary>A published Instagram post.</summary>
public record InstagramPublishResult(
    string  MediaId,
    string? Permalink);

/// <summary>Whether the admin UI should offer the "Post to Instagram" action at all.</summary>
public record SocialStatus(
    bool    InstagramConfigured,
    string? AccountName);
