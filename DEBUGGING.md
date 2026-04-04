# ✦ Izale Sparkle — Debugging Guide

Breakpoints work differently in Blazor WebAssembly Hosted projects.
The .NET runtime runs *inside the browser* via WebAssembly, so the debugger
has to connect through the browser's dev-tools protocol rather than attaching
to a process directly.

---

## Visual Studio 2022 (recommended)

1. Open `IzaleSparkle.sln`
2. Set **IzaleSparkle.Server** as the startup project
3. Press **F5** (or Debug → Start Debugging)
4. Visual Studio automatically:
   - Builds both Client and Server
   - Starts the server on `https://localhost:7000`
   - Opens Chrome and attaches the WASM debugger
5. Set a breakpoint in any `.razor` or `.cs` file in either project
6. ✅ Breakpoints work in **both** Client (Blazor) and Server (API) code

> **If breakpoints show as hollow circles ("not yet bound"):**
> - Make sure Chrome opened (not Edge/Firefox) — WASM debugging requires Chrome
> - Wait a few seconds after the page loads for the debugger to attach
> - Reload the page once (Ctrl+R) if needed

---

## Visual Studio Code

### Prerequisites
Install the required extensions (prompted automatically, or install manually):
- **C# Dev Kit** — `ms-dotnettools.csdevkit`
- **Blazor WASM Companion** — `ms-dotnettools.blazorwasm-companion`

### Steps
1. Open the `IzaleSparkleHosted/` folder in VS Code (`File → Open Folder`)
2. Go to **Run & Debug** panel (`Ctrl+Shift+D`)
3. Select **"Launch & Debug (Blazor WASM Hosted)"** from the dropdown
4. Press **F5**
5. Chrome opens automatically and the WASM debugger attaches
6. Set breakpoints — they work in `.razor` and `.cs` files in any project

---

## Why breakpoints didn't work before

The original `launchSettings.json` was missing the `inspectUri` field.
This property tells Visual Studio / VS Code how to reach the browser's
debugging WebSocket proxy, which Blazor's server exposes at:

```
/_framework/debug/ws-proxy?browser={browserInspectUri}
```

Without it, the IDE starts the app but never attaches a debugger, so
breakpoints show as hollow/unbound circles and are never hit.

Also, `<PublishTrimmed>true</PublishTrimmed>` without a condition was
trimming debug symbols even during Debug builds. It is now conditioned:

```xml
<!-- Only trim in Release — never in Debug -->
<PublishTrimmed Condition="'$(Configuration)' == 'Release'">true</PublishTrimmed>
```

---

## Debugging the API (Server) separately

If you only need to debug controller/service/repository code:

**Visual Studio:** Set a breakpoint in any controller or handler, press F5.
The server-side .NET runtime debugger works exactly as for any ASP.NET Core app.

**VS Code:** Select **"Debug Server (API only)"** from the Run & Debug dropdown.

---

## Server-side vs Client-side breakpoints

| Location | Type | Works via |
|----------|------|-----------|
| `IzaleSparkle.Server/Controllers/*.cs` | Server | .NET runtime debugger |
| `IzaleSparkle.Application/**/*.cs` | Server | .NET runtime debugger |
| `IzaleSparkle.Infrastructure/**/*.cs` | Server | .NET runtime debugger |
| `IzaleSparkle.Client/Pages/*.razor` | Client (WASM) | Browser WASM debugger |
| `IzaleSparkle.Client/Services/*.cs` | Client (WASM) | Browser WASM debugger |
| `IzaleSparkle.Client/Shared/*.razor` | Client (WASM) | Browser WASM debugger |

Both types of breakpoints are active simultaneously when you press F5 from Visual Studio.
