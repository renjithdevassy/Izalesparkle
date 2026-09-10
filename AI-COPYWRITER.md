# AI Copywriter (Gemini free tier)

Turns product photos into launch-ready copy: product title, SEO meta, short and long
descriptions, bullets, material line, care instructions, alt text, three Instagram
captions, 24 hashtags, a 15-second Reel script, a WhatsApp broadcast line and a
Story poll idea.

Admin panel → **✨ AI Copywriter** (`/admin/ai-content`).

## Setup (one minute, no card)

1. Get a free key: https://aistudio.google.com/apikey
2. Store it outside source control — the Server project already has a `UserSecretsId`:

   ```
   cd IzaleSparkle.Server
   dotnet user-secrets set "Gemini:ApiKey" "AIza..."
   ```

   On the host, set it as an app setting / environment variable instead
   (`Gemini__ApiKey`). `appsettings.json` ships with an empty `ApiKey` on purpose —
   never commit the real one.
3. Restart the server. The page shows setup instructions until a key is present.

## Configuration

| Key                 | Default              | Notes |
|---------------------|----------------------|-------|
| `Gemini:ApiKey`     | *(empty)*            | Required. Free tier, no billing. |
| `Gemini:Model`      | `gemini-flash-latest`| `GET /api/admin/ai/models` lists what this key can actually call, cheapest first. Flash-Lite models have the largest free allowance. |
| `Gemini:CacheHours` | `12`                 | Identical requests are served from cache and cost no quota. |
| `Gemini:BrandVoice` | *(built-in brief)*   | Override the Izale Sparkle voice brief without a rebuild. |

## Endpoints (all `AdminOnly`)

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/admin/ai/product-content` | Generate copy from 1–4 photos (inline base64) or an `imageUrl` the server fetches. |
| `GET`  | `/api/admin/ai/models`          | Models available to the configured key. |
| `GET`  | `/api/admin/ai/status`          | `true` when a key is configured — used to hide the UI otherwise. |

## Where the code lives

```
IzaleSparkle.Contracts/Requests/AiRequests.cs         GenerateProductContentRequest, AiImageInput
IzaleSparkle.Contracts/Responses/AiResponses.cs       GeneratedProductContent, AiCaption, AiModelInfo
IzaleSparkle.Application/Common/Interfaces/IAiContentService.cs
IzaleSparkle.Application/Ai/Commands/                 MediatR command + query
IzaleSparkle.Application/Ai/Validators/               photo count/size, temperature
IzaleSparkle.Infrastructure/Ai/GeminiContentService.cs  the only Gemini-aware file
IzaleSparkle.Server/Controllers/AiContentController.cs
IzaleSparkle.Client/Pages/Admin/AiContent.razor       the admin page
IzaleSparkle.Client/Shared/AiCopyBlock.razor          one result card + Copy button
```

Swapping providers means one new `IAiContentService` implementation — nothing else
in the solution knows Gemini exists.

## Free-tier behaviour

* Rate limits are per-minute and per-day, not per-pound. A 429 is retried with
  backoff (honouring `Retry-After`) up to 3 times, then surfaced as a readable 400.
* Results are cached for `Gemini:CacheHours`, keyed on the model, the fields and the
  photos — regenerating the same piece costs no quota (`FromCache` says so in the UI).
* Free-tier prompts and responses may be used by Google to improve their products.
  **Send product photos and marketing copy only — never customer data.** If customer
  data ever needs a model, use a paid key or a local model for that path.

## Guardrails in the prompt

The model is told to describe only what the photo shows or the brief states: no
invented gemstones, carats, hallmarks, weights, dimensions, certifications or prices,
British spelling, and plated brass described as plated brass. Copy is still a draft —
read it before it goes live.
