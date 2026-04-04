using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using IzaleSparkle.Client;
using IzaleSparkle.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient — same origin, no CORS needed (Scoped)
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Core in-memory services (Singleton — no scoped deps)
builder.Services.AddSingleton<CartService>();
builder.Services.AddSingleton<ProductService>();

// AuthService — Singleton is correct for auth state, but it must NOT
// depend on Scoped services (HttpClient, IJSRuntime) in its constructor.
// IJSRuntime is passed per-call instead (SetTokenAsync, LogoutAsync, InitialiseAsync).
builder.Services.AddSingleton<AuthService>();

// API client — Scoped (depends on Scoped HttpClient)
builder.Services.AddScoped<IApiClient, ApiClient>();

var host = builder.Build();

// Restore auth session from localStorage before first render.
// We resolve IJSRuntime from the root scope — safe at startup.
var auth = host.Services.GetRequiredService<AuthService>();
var js   = host.Services.GetRequiredService<Microsoft.JSInterop.IJSRuntime>();
await auth.InitialiseAsync(js);

await host.RunAsync();
