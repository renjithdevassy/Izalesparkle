using System.Security.Cryptography;
using System.Text;
using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;

namespace IzaleSparkle.Application.Auth.Commands;

// ── PASSWORD-RESET TOKEN HELPERS ──────────────────────────────
internal static class ResetTokens
{
    /// <summary>Generates a URL-safe, high-entropy reset token (the raw value emailed to the user).</summary>
    public static string NewRawToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

    /// <summary>SHA-256 hash (hex) of a token — only the hash is persisted.</summary>
    public static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

// ── REGISTER ─────────────────────────────────────────────────
public record RegisterCommand(RegisterRequest Request) : IRequest<AuthResponse>;

public sealed class RegisterCommandHandler(IUnitOfWork uow, IAuthService auth)
    : IRequestHandler<RegisterCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        if (req.Password != req.ConfirmPassword)
            return new AuthResponse(false, null, null, null, null, null, "Passwords do not match.");

        if (req.Password.Length < 8)
            return new AuthResponse(false, null, null, null, null, null,
                "Password must be at least 8 characters.");

        if (await uow.Users.EmailExistsAsync(req.Email, ct))
            return new AuthResponse(false, null, null, null, null, null,
                "An account with this email already exists.");

        var hash = auth.HashPassword(req.Password);
        var user = AppUser.Create(req.Email, req.FirstName, req.LastName, hash, UserRole.Customer);

        await uow.Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        var token = auth.GenerateJwtToken(user);
        return new AuthResponse(true, token, user.Email, user.FirstName, user.LastName,
            user.Role.ToString(), "Account created successfully.");
    }
}

// ── LOGIN ────────────────────────────────────────────────────
public record LoginCommand(LoginRequest Request) : IRequest<AuthResponse>;

public sealed class LoginCommandHandler(IUnitOfWork uow, IAuthService auth)
    : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var req  = cmd.Request;
        var user = await uow.Users.GetByEmailAsync(req.Email, ct);

        if (user == null || !auth.VerifyPassword(req.Password, user.PasswordHash))
            return new AuthResponse(false, null, null, null, null, null,
                "Invalid email or password.");

        if (!user.IsActive)
            return new AuthResponse(false, null, null, null, null, null,
                "Your account has been deactivated. Please contact support.");

        user.LastLoginAt = DateTime.UtcNow;
        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);

        var token = auth.GenerateJwtToken(user);
        return new AuthResponse(true, token, user.Email, user.FirstName, user.LastName,
            user.Role.ToString(), $"Welcome back, {user.FirstName}!");
    }
}

// ── FORGOT PASSWORD ──────────────────────────────────────────
public record ForgotPasswordCommand(ForgotPasswordRequest Request) : IRequest<AuthMessageResponse>;

public sealed class ForgotPasswordCommandHandler(IUnitOfWork uow, IEmailService email)
    : IRequestHandler<ForgotPasswordCommand, AuthMessageResponse>
{
    // Always returns the same message so the endpoint never reveals whether an account exists.
    private static readonly AuthMessageResponse Generic = new(true,
        "If an account exists for that email, a password reset link has been sent.");

    public async Task<AuthMessageResponse> Handle(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var emailAddr = cmd.Request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(emailAddr))
            return new AuthMessageResponse(false, "Please enter your email address.");

        var user = await uow.Users.GetByEmailAsync(emailAddr, ct);
        if (user is { IsActive: true })
        {
            var rawToken = ResetTokens.NewRawToken();
            user.SetPasswordResetToken(ResetTokens.Hash(rawToken), DateTime.UtcNow.AddHours(1));
            uow.Users.Update(user);
            await uow.SaveChangesAsync(ct);

            try { await email.SendPasswordResetAsync(user.Email, user.FirstName, rawToken, ct); }
            catch { /* logged inside the email service — never leak failures to the caller */ }
        }

        return Generic;
    }
}

// ── RESET PASSWORD ───────────────────────────────────────────
public record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest<AuthMessageResponse>;

public sealed class ResetPasswordCommandHandler(IUnitOfWork uow, IAuthService auth)
    : IRequestHandler<ResetPasswordCommand, AuthMessageResponse>
{
    public async Task<AuthMessageResponse> Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.Email))
            return new AuthMessageResponse(false, "This reset link is invalid or incomplete.");

        if (req.NewPassword != req.ConfirmPassword)
            return new AuthMessageResponse(false, "Passwords do not match.");

        if (req.NewPassword is null || req.NewPassword.Length < 8)
            return new AuthMessageResponse(false, "Password must be at least 8 characters.");

        var user = await uow.Users.GetByEmailAsync(req.Email, ct);
        if (user is null || !user.IsResetTokenValid(ResetTokens.Hash(req.Token)))
            return new AuthMessageResponse(false,
                "This reset link is invalid or has expired. Please request a new one.");

        user.ResetPassword(auth.HashPassword(req.NewPassword));
        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);

        return new AuthMessageResponse(true,
            "Your password has been reset. You can now sign in with your new password.");
    }
}
