namespace IzaleSparkle.Contracts.Responses;

// ── AI CONTENT GENERATION ────────────────────────────────────
public record AiCaption(string Style, string Text);

public record AiModelInfo(string Id, string DisplayName);

/// <summary>Launch-ready copy for one product, generated from its photos.</summary>
public record GeneratedProductContent(
    string            Observed,
    string            ProductTitle,
    string            SeoMetaDescription,
    string            ShortDescription,
    string            LongDescription,
    List<string>      BulletPoints,
    string            Material,
    string            CareInstructions,
    string            AltText,
    List<AiCaption>   InstagramCaptions,
    List<string>      Hashtags,
    string            ReelHook,
    string            ReelScript,
    string            WhatsAppBroadcast,
    string            StoryPollIdea,
    string            ModelUsed,
    int               TokensUsed,
    bool              FromCache);
