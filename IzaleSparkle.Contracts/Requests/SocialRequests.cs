namespace IzaleSparkle.Contracts.Requests;

// ── SOCIAL PUBLISHING ────────────────────────────────────────
/// <summary>
/// One Instagram feed post. <paramref name="ImageUrl"/> is normally the
/// "/uploads/..." path returned by the uploads endpoint — the server expands it
/// to an absolute URL because Meta fetches the image from the public internet.
/// </summary>
public record PublishInstagramRequest(
    string  ImageUrl,
    string  Caption);
