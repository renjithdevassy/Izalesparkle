namespace IzaleSparkle.Contracts.Requests;

// ── AI CONTENT GENERATION ────────────────────────────────────
/// <summary>An image handed to the model, already base64-encoded by the caller.</summary>
public record AiImageInput(string MimeType, string Base64Data);

/// <summary>
/// Everything the copywriter model needs about one piece.
/// Images may be supplied inline (<see cref="Images"/>) or as a URL the
/// server fetches itself (<see cref="ImageUrl"/> — typically /uploads/...).
/// </summary>
public record GenerateProductContentRequest(
    string?             Name        = null,
    string?             Category    = null,
    decimal?            Price       = null,
    string?             Material    = null,
    string?             Occasion    = null,
    string?             Notes       = null,
    List<AiImageInput>? Images      = null,
    string?             ImageUrl    = null,
    string?             Model       = null,
    double?             Temperature = null);
