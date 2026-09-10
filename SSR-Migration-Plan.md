# SSR Migration Plan — Izale Sparkle (.NET 10)

## What this achieves

Currently Google receives a blank HTML shell and has to wait for WebAssembly to load before seeing any content. After this migration, Google gets full HTML on first request for your key pages (Homepage, Shop, Product pages), while the app stays interactive for users.

---

## How it works (.NET 8+ Blazor Web App model)

Your Server project becomes the Razor Components host. Pages without a `@rendermode` directive are rendered as static HTML on the server — perfect for Google. Pages that need interactivity (Cart, Checkout, Login, Admin) keep `@rendermode InteractiveWebAssembly`.

---

## Step 1 — Update Server project

### 1a. IzaleSparkle.Server.csproj

No SDK change needed. Add one package:

```xml
<!-- Already present — confirm version is 10.x -->
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="10.0.0-preview.*" />
```

### 1b. Program.cs — replace Blazor wiring

Find these lines:
```csharp
app.UseBlazorFrameworkFiles();
// ...
app.MapFallbackToFile("index.html");
```

Replace with:
```csharp
app.UseBlazorFrameworkFiles();
// ...
app.MapRazorComponents<IzaleSparkle.Server.Components.App>()
   .AddInteractiveWebAssemblyRenderMode()
   .AddAdditionalAssemblies(typeof(IzaleSparkle.Client._Imports).Assembly);
```

Also add to the services section (before `var app = builder.Build()`):
```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
```

### 1c. Add App.razor to Server

Create folder: `IzaleSparkle.Server/Components/`

Create `IzaleSparkle.Server/Components/App.razor`:
```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover, maximum-scale=1.0" />
    <base href="/" />
    <HeadOutlet />
</head>
<body>
    <Routes />
    <script src="_framework/blazor.web.js"></script>
    <script src="js/app.js"></script>
</body>
</html>
```

> **Note:** Move your static meta tags (OG, Twitter, JSON-LD, fonts, CSS links) into `<HeadOutlet />` or a shared layout component. The `SeoMeta` component already handles per-page tags — it will work automatically once the app uses SSR.

Create `IzaleSparkle.Server/Components/Routes.razor`:
```razor
<Router AppAssembly="@typeof(IzaleSparkle.Client.App).Assembly"
        AdditionalAssemblies="@(new[] { typeof(IzaleSparkle.Client._Imports).Assembly })">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(IzaleSparkle.Client.Shared.MainLayout)" />
    </Found>
    <NotFound>
        <LayoutView Layout="@typeof(IzaleSparkle.Client.Shared.MainLayout)">
            <p>Page not found.</p>
        </LayoutView>
    </NotFound>
</Router>
```

Create `IzaleSparkle.Server/Components/_Imports.razor`:
```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using IzaleSparkle.Server.Components
```

---

## Step 2 — Mark pages with render modes

This is the key step. Pages with no `@rendermode` are server-rendered (SSR). Pages that need interactivity declare it explicitly.

### SEO pages — NO rendermode (static SSR = Google sees full HTML)

**Index.razor, Shop.razor, ProductDetail.razor, Terms.razor, Contact.razor**

These pages load product data and display content. For SSR, replace `HttpClient`-based calls with direct service injection. Since your Server project already has `IzaleSparkle.Application` referenced, you can inject your application services directly.

Example — `Pages/ProductDetail.razor` top section:
```razor
@page "/product/{Id:int}"
@* No @rendermode = static SSR *@
@inject IProductService ProductService
@inject NavigationManager Nav

<PageTitle>@(Product?.Name ?? "Product") — Izale Sparkle</PageTitle>
```

Replace `@inject IApiClient Api` with `@inject IProductService ProductService` (or equivalent) on SSR pages, then call the service directly instead of via HTTP.

> **Tip:** The interactive parts of these pages (Add to Cart button, wishlist toggle) should be extracted into small child components marked `@rendermode InteractiveWebAssembly`. The product name, description, price, and images render SSR.

### Interactive pages — keep as WASM

Add `@rendermode InteractiveWebAssembly` at the top:

```razor
@* Checkout.razor *@
@page "/checkout"
@rendermode InteractiveWebAssembly

@* Login.razor *@
@page "/login"
@rendermode InteractiveWebAssembly

@* Register.razor *@
@page "/register"
@rendermode InteractiveWebAssembly

@* MyOrders.razor *@
@page "/my-orders"
@rendermode InteractiveWebAssembly

@* Wishlist.razor *@
@page "/wishlist"
@rendermode InteractiveWebAssembly

@* All Admin pages *@
@rendermode InteractiveWebAssembly
```

---

## Step 3 — Handle IJSRuntime on SSR pages

Any page using `IJSRuntime` (scroll reveal, copy promo code, etc.) will throw during SSR because JS isn't available server-side.

**Pattern:** Guard JS calls with a lifecycle check:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // Safe: this only runs in the browser, after hydration
        await JS.InvokeVoidAsync("initScrollReveal");
    }
}
```

`OnInitializedAsync` runs during SSR — never call JS there on SSR pages. `OnAfterRenderAsync` is browser-only and always safe.

---

## Step 4 — SeoMeta component update

Your existing `SeoMeta` component uses `OnInitialized` which runs during SSR — this is correct and will work. However, the `<script>` tags it injects need to go into `<head>`, not the body. Wrap the output in `<HeadContent>`:

```razor
@* SeoMeta.razor — add HeadContent wrapper *@
<HeadContent>
    @if (SiteSchema != null)
    {
        <script type="application/ld+json">@((MarkupString)SiteSchema)</script>
    }
    @foreach (var schema in ProductSchemas)
    {
        <script type="application/ld+json">@((MarkupString)schema)</script>
    }
    @if (!string.IsNullOrEmpty(Title))
    {
        <meta property="og:title" content="@Title" />
        <meta name="description" content="@Description" />
        <meta property="og:description" content="@Description" />
    }
</HeadContent>
```

---

## Step 5 — Update Client project (minor)

In `IzaleSparkle.Client/IzaleSparkle.Client.csproj`, the SDK stays as `Microsoft.NET.Sdk.BlazorWebAssembly`. No changes needed — the Client project is now the WASM runtime that gets loaded for interactive components.

Remove the old `App.razor` Router from the Client (the Server's `Routes.razor` handles routing now), or keep it — .NET 10 handles the dual-project setup cleanly.

---

## Priority order

| Step | What | Effort | SEO impact |
|------|------|--------|-----------|
| 1 | Server wiring (Program.cs + App.razor) | 1–2 hrs | Unlocks everything |
| 2a | Index.razor → SSR | 1 hr | High — homepage |
| 2b | Shop.razor → SSR | 1 hr | High — catalogue |
| 2c | ProductDetail.razor → SSR | 2 hrs | Very high — product pages |
| 2d | Mark auth/admin pages WASM | 30 min | Maintains functionality |
| 3 | Guard JS calls | 1 hr | Stability |
| 4 | SeoMeta HeadContent | 30 min | Schema in correct location |

**Total estimated effort: 1–2 days**

---

## How to verify

After each page migration:
1. Run locally with `dotnet run --project IzaleSparkle.Server`
2. View source (`Ctrl+U`) — you should see actual HTML content, not just the loading spinner
3. Use Google's **Rich Results Test**: https://search.google.com/test/rich-results
4. Use **URL Inspection** in Google Search Console after deploying

---

## If you want the quick path instead

Skip the migration for now and add **Prerender.io** middleware:

```csharp
// Program.cs — add before app.UseBlazorFrameworkFiles()
app.UsePrerender(new PrerenderOptions
{
    PrerenderServiceUrl = "https://service.prerender.io",
    Token = "YOUR_TOKEN"
});
```

This intercepts Googlebot requests and returns a cached rendered snapshot. Cost: ~$14/month. Good bridge while you do the proper SSR migration.
