using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;

namespace IzaleSparkle.Application.Auth.Commands;

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
