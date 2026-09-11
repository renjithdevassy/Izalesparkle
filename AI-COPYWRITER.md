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

---

# Posting to Instagram

The generated captions can go straight to Instagram from the results panel —
**📷 Post this to Instagram** under each caption posts that caption plus all the
hashtags as a single photo post. Meta charges nothing for content publishing.

Only feed photo posts are automated. Reels need a video file, Story poll stickers
cannot be created through the API, and WhatsApp broadcasts are billed per
conversation — those three stay copy-and-paste.

## Setup (once, free)

1. An **Instagram Business or Creator** account, linked to a Facebook Page
   (Instagram app → Settings → Account type and tools).
2. A Meta app at [developers.facebook.com](https://developers.facebook.com) with
   the **Instagram Graph API** product added. Posting to your own account works in
   development mode — no app review needed.
3. Generate a **long-lived access token** with `instagram_basic`,
   `instagram_content_publish` and `pages_read_engagement`, and find your
   Instagram Business account id (Graph API Explorer → `me/accounts` →
   `{page-id}?fields=instagram_business_account`).
4. Store both outside source control:

   ```
   cd IzaleSparkle.Server
   dotnet user-secrets set "Instagram:IgUserId"    "17841400000000000"
   dotnet user-secrets set "Instagram:AccessToken" "EAAG..."
   ```

   On the host set them as app settings instead (`Instagram__IgUserId`,
   `Instagram__AccessToken`). `appsettings.json` ships placeholders on purpose.

## Configuration

| Key                        | Default   | Notes |
|----------------------------|-----------|-------|
| `Instagram:IgUserId`       | *(empty)* | Instagram Business account id, not the @username. |
| `Instagram:AccessToken`    | *(empty)* | Long-lived token. **Expires roughly every 60 days — refresh it or posting stops.** |
| `Instagram:GraphVersion`   | `v21.0`   | Graph API version. |
| `Site:BaseUrl`             | `https://izalesparkle.com` | Used to build the public image URL Meta fetches. |

The button only appears when both values are set; `GET /api/admin/social/status`
reports that, and the connected @username, to the admin page.

## How a post is made

Instagram never receives the image bytes — it fetches the photo over the public
internet. So posting runs:

1. The selected photo is uploaded to `/uploads/...` on this site (cached, so
   posting a second caption with the same photo does not re-upload it).
2. `POST {ig-user-id}/media` creates a container from that absolute URL + caption.
3. The container status is polled until `FINISHED` (photos take a few seconds).
4. `POST {ig-user-id}/media_publish` publishes it, and the permalink comes back
   to the admin page.

**Posting only works from the deployed site.** From `localhost` Meta cannot reach
the image, so the endpoint rejects the attempt with that explanation rather than a
vague Graph API error.

## Limits worth knowing

* Caption max 2200 characters, max 30 hashtags — both checked before the call,
  so an over-long caption fails instantly rather than after an upload.
* JPEG works most reliably; aspect ratio must be between 4:5 and 1.91:1.
* Instagram caps published posts per rolling 24 hours (dozens, not hundreds).
* Publishing is irreversible from here — the page asks for confirmation first,
  and deleting a post is done in the Instagram app.

## Files

```
IzaleSparkle.Application/Common/Interfaces/IInstagramPublisher.cs
IzaleSparkle.Application/Social/Commands/PublishInstagramCommand.cs
IzaleSparkle.Infrastructure/Social/InstagramPublisher.cs    the only Graph-aware file
IzaleSparkle.Server/Controllers/SocialController.cs
IzaleSparkle.Client/Pages/Admin/AiContent.razor             the Post buttons
```
