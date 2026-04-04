# ✦ Izale Sparkle — Blazor WebAssembly Hosted Solution

> *Where Every Look Sparkles*

A single `dotnet run` starts everything — the ASP.NET Core server hosts both the
REST API **and** serves the Blazor WebAssembly PWA from the same port.

---

## Solution Structure

```
IzaleSparkle.sln
│
├── IzaleSparkle.Server/          ← ASP.NET Core 10 host  (API + serves Blazor WASM)
│   ├── Controllers/              ← Products, Orders, Contact, Newsletter
│   ├── Middleware/               ← Global exception handler
│   └── Program.cs                ← Single unified entry point
│
├── IzaleSparkle.Client/          ← Blazor WebAssembly PWA  (mobile-first)
│   ├── Pages/                    ← All 7 pages
│   ├── Shared/                   ← MainLayout, ProductCard
│   ├── Services/                 ← CartService, ProductService, ApiClient
│   ├── Models/                   ← View models
│   └── wwwroot/                  ← CSS, JS, manifest, service worker
│
├── IzaleSparkle.Application/     ← CQRS (MediatR), FluentValidation, AutoMapper
├── IzaleSparkle.Domain/          ← Entities, Value Objects, Domain Events
├── IzaleSparkle.Infrastructure/  ← EF Core + SQLite, Repositories, UoW
└── IzaleSparkle.Contracts/       ← Shared request/response DTOs
```

---

## How the Hosted Model Works

```
Browser (Blazor WASM)
        │
        │  https://localhost:7000/           → serves index.html + _framework/ + wwwroot/
        │  https://localhost:7000/api/*      → API controllers (Products, Orders, etc.)
        │  https://localhost:7000/swagger    → Swagger UI (dev only)
        ▼
IzaleSparkle.Server  (single ASP.NET Core process)
        │
        ├── UseBlazorFrameworkFiles()   → serves IzaleSparkle.Client/_framework/
        ├── UseStaticFiles()            → serves IzaleSparkle.Client/wwwroot/
        ├── MapControllers()            → /api/* routes
        └── MapFallbackToFile()         → any unknown route → index.html (enables SPA routing)
```

**No CORS configuration needed** — the browser and API share the same origin.

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Run (single command)
```bash
cd IzaleSparkle.Server
dotnet run
```

- **App:**     https://localhost:7000
- **API:**     https://localhost:7000/api
- **Swagger:** https://localhost:7000/swagger  *(dev only)*

The SQLite database is created and seeded with all 12 products automatically on first run.

### Publish to production
```bash
dotnet publish IzaleSparkle.Server -c Release -o ./publish
```
The output folder contains everything needed — the server binary + the compiled WASM client files ready to deploy to any host (Azure App Service, Railway, Render, etc.).

---

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/products` | List products (filter, sort, page) |
| GET | `/api/products/{id}` | Product detail with images |
| GET | `/api/products/featured` | Featured products |
| GET | `/api/products/{id}/related` | Related products |
| GET | `/api/products/{id}/reviews` | Product reviews |
| GET | `/api/products/categories` | Category list |
| POST | `/api/orders` | Place order |
| GET | `/api/orders/{orderNumber}` | Get order |
| POST | `/api/contact` | Contact form |
| POST | `/api/newsletter/subscribe` | Newsletter |

---

## Architecture

### Clean Architecture layers
```
IzaleSparkle.Server      → entry point, HTTP, DI composition root
IzaleSparkle.Client      → Blazor WASM SPA, UI components, PWA
IzaleSparkle.Application → CQRS queries & commands, pipeline behaviours
IzaleSparkle.Domain      → pure domain: entities, value objects, events
IzaleSparkle.Infrastructure → EF Core, repositories, email, cache
IzaleSparkle.Contracts   → shared DTOs between client and server
```

### Key patterns
| Pattern | Where |
|---------|-------|
| CQRS via MediatR | `Application/Products/Queries/`, `Application/Orders/Commands/` |
| Pipeline Behaviours | Logging + FluentValidation on every request |
| Repository + UoW | `Infrastructure/Repositories/` |
| Value Objects | `Domain/ValueObjects/` — `Money`, `Address` |
| Domain Events | `Domain/Events/` — fired on order placed, product created |
| Global exception middleware | `Server/Middleware/ExceptionHandlingMiddleware.cs` |
| Typed HTTP client | `Client/Services/ApiClient.cs` — wraps all `/api/*` calls |
| Offline PWA fallback | Client `ProductService` / `CartService` work without network |

---

## Promo Codes
`SPARKLE10` or `IZALE10` — 10% discount at checkout.

---

*© 2024 Izale Sparkle. Where Every Look Sparkles. ✦*
