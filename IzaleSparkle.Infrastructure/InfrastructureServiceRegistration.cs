using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Infrastructure.Persistence;
using IzaleSparkle.Infrastructure.Repositories;
using IzaleSparkle.Infrastructure.Notifications;

namespace IzaleSparkle.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("Default") ?? "Data Source=izalesparkle.db";
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connStr));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        // Email — uses SMTP when configured, falls back to stub logging if not
        services.AddScoped<IEmailService, SmtpEmailService>();

        services.AddMemoryCache();
        services.AddScoped<ICacheService, MemoryCacheService>();

        services.AddScoped<IAuthService, JwtAuthService>();

        return services;
    }
}

// ── IN-MEMORY CACHE SERVICE ────────────────────────────────────
public class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
    {
        var opts = new MemoryCacheEntryOptions();
        if (expiry.HasValue) opts.AbsoluteExpirationRelativeToNow = expiry;
        else opts.SlidingExpiration = TimeSpan.FromMinutes(10);
        cache.Set(key, value, opts);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }
}

// ── JWT AUTH SERVICE ───────────────────────────────────────────
// AppUser is in IzaleSparkle.Domain.Entities (using added above).
// JWT types are referenced by full namespace to avoid ambiguity.
public class JwtAuthService(IConfiguration config) : IAuthService
{
    private readonly string _secret        = config["Jwt:Secret"]        ?? "IzaleSparkle-Super-Secret-Key-Must-Be-32!!";
    private readonly string _issuer        = config["Jwt:Issuer"]        ?? "IzaleSparkle";
    private readonly string _audience      = config["Jwt:Audience"]      ?? "IzaleSparkleUsers";
    private readonly int    _expiryMinutes = int.TryParse(config["Jwt:ExpiryMinutes"], out var m) ? m : 1440;

    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);

    public string GenerateJwtToken(AppUser user)
    {
        var key   = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(_secret));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                        key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email,          user.Email),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName,      user.FirstName),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Surname,        user.LastName),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role,           user.Role.ToString()),
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
