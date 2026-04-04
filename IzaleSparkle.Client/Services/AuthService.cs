using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.JSInterop;

namespace IzaleSparkle.Client.Services;

/// <summary>
/// Manages JWT auth state. Singleton — persists across the entire WASM session.
/// Does NOT hold HttpClient (which is Scoped). ApiClient reads the token directly.
/// </summary>
public class AuthService
{
    private const string TokenKey = "izale_auth_token";

    // Raised whenever login/logout state changes — layouts subscribe to re-render
    public event Action? OnAuthChanged;

    public string? Token      { get; private set; }
    public string? Email      { get; private set; }
    public string? FirstName  { get; private set; }
    public string? LastName   { get; private set; }
    public string? Role       { get; private set; }
    public bool    IsLoggedIn => !string.IsNullOrEmpty(Token) && !IsTokenExpired(Token);
    public bool    IsAdmin    => Role == "Admin";
    public string  FullName   => $"{FirstName} {LastName}".Trim();

    // ── Initialise — called once at startup from Program.cs ──────────────────
    // JS interop is used here (not injected) so the service stays singleton-safe.
    public async Task InitialiseAsync(IJSRuntime js)
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            if (!string.IsNullOrEmpty(stored) && !IsTokenExpired(stored))
                ParseAndStore(stored);
            else
                await ClearStoredToken(js);
        }
        catch { /* localStorage unavailable during SSR pre-render */ }
    }

    // ── Called after successful login / register ──────────────────────────────
    public async Task SetTokenAsync(IJSRuntime js, string token)
    {
        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
        ParseAndStore(token);
        OnAuthChanged?.Invoke();
    }

    // ── Called on logout ──────────────────────────────────────────────────────
    public async Task LogoutAsync(IJSRuntime js)
    {
        Token = Email = FirstName = LastName = Role = null;
        await ClearStoredToken(js);
        OnAuthChanged?.Invoke();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    void ParseAndStore(string token)
    {
        Token = token;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Email     = Claim(jwt, ClaimTypes.Email)      ?? Claim(jwt, "email");
            FirstName = Claim(jwt, ClaimTypes.GivenName)  ?? Claim(jwt, "given_name");
            LastName  = Claim(jwt, ClaimTypes.Surname)    ?? Claim(jwt, "family_name");
            Role      = Claim(jwt, ClaimTypes.Role)       ?? Claim(jwt, "role");
        }
        catch { Token = null; }
    }

    static string? Claim(JwtSecurityToken jwt, string type)
        => jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    static async Task ClearStoredToken(IJSRuntime js)
    {
        try { await js.InvokeVoidAsync("localStorage.removeItem", TokenKey); }
        catch { }
    }

    static bool IsTokenExpired(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow;
        }
        catch { return true; }
    }
}
