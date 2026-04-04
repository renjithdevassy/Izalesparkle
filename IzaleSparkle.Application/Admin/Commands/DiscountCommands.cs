using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Exceptions;

namespace IzaleSparkle.Application.Admin.Commands;

// ── SHARED MAPPER ─────────────────────────────────────────────
internal static class DiscountMapper
{
    internal static DiscountCodeResponse ToResponse(DiscountCode d) => new(
        d.Id, d.Code, d.Description, d.DiscountPercent,
        d.IsActive, d.MaxUses, d.TimesUsed, d.ExpiresAt, d.IsValid);
}

// ── GET ALL ───────────────────────────────────────────────────
public record GetDiscountCodesQuery : IRequest<IEnumerable<DiscountCodeResponse>>;

public sealed class GetDiscountCodesQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetDiscountCodesQuery, IEnumerable<DiscountCodeResponse>>
{
    public async Task<IEnumerable<DiscountCodeResponse>> Handle(
        GetDiscountCodesQuery req, CancellationToken ct)
    {
        var codes = await uow.DiscountCodes.GetAllAsync(ct);
        return codes.Select(DiscountMapper.ToResponse);
    }
}

// ── VALIDATE (customer-facing, no auth) ───────────────────────
public record ValidateDiscountCodeQuery(string Code, decimal Subtotal)
    : IRequest<ValidateDiscountResponse>;

public sealed class ValidateDiscountCodeQueryHandler(IUnitOfWork uow)
    : IRequestHandler<ValidateDiscountCodeQuery, ValidateDiscountResponse>
{
    public async Task<ValidateDiscountResponse> Handle(
        ValidateDiscountCodeQuery req, CancellationToken ct)
    {
        var code = await uow.DiscountCodes.GetByCodeAsync(req.Code, ct);
        if (code == null || !code.IsValid)
            return new(false, "This code is invalid or has expired.", 0, 0);

        var amount = code.Apply(req.Subtotal);
        return new(true, $"{code.DiscountPercent}% discount applied",
            amount, code.DiscountPercent);
    }
}

// ── CREATE ────────────────────────────────────────────────────
public record CreateDiscountCodeCommand(CreateDiscountCodeRequest Request)
    : IRequest<DiscountCodeResponse>;

public sealed class CreateDiscountCodeCommandHandler(IUnitOfWork uow)
    : IRequestHandler<CreateDiscountCodeCommand, DiscountCodeResponse>
{
    public async Task<DiscountCodeResponse> Handle(
        CreateDiscountCodeCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var code = new DiscountCode
        {
            Code            = req.Code.ToUpper().Trim(),
            Description     = req.Description,
            DiscountPercent = req.DiscountPercent,
            IsActive        = req.IsActive,
            MaxUses         = req.MaxUses,
            ExpiresAt       = req.ExpiresAt,
        };
        await uow.DiscountCodes.AddAsync(code, ct);
        await uow.SaveChangesAsync(ct);
        return DiscountMapper.ToResponse(code);
    }
}

// ── UPDATE ────────────────────────────────────────────────────
public record UpdateDiscountCodeCommand(UpdateDiscountCodeRequest Request)
    : IRequest<DiscountCodeResponse>;

public sealed class UpdateDiscountCodeCommandHandler(IUnitOfWork uow)
    : IRequestHandler<UpdateDiscountCodeCommand, DiscountCodeResponse>
{
    public async Task<DiscountCodeResponse> Handle(
        UpdateDiscountCodeCommand cmd, CancellationToken ct)
    {
        var req  = cmd.Request;
        var code = await uow.DiscountCodes.GetByIdAsync(req.Id, ct)
            ?? throw new NotFoundException(nameof(DiscountCode), req.Id);

        code.Code            = req.Code.ToUpper().Trim();
        code.Description     = req.Description;
        code.DiscountPercent = req.DiscountPercent;
        code.IsActive        = req.IsActive;
        code.MaxUses         = req.MaxUses;
        code.ExpiresAt       = req.ExpiresAt;
        code.UpdatedAt       = DateTime.UtcNow;

        uow.DiscountCodes.Update(code);
        await uow.SaveChangesAsync(ct);
        return DiscountMapper.ToResponse(code);
    }
}

// ── DELETE ────────────────────────────────────────────────────
public record DeleteDiscountCodeCommand(int Id) : IRequest<bool>;

public sealed class DeleteDiscountCodeCommandHandler(IUnitOfWork uow)
    : IRequestHandler<DeleteDiscountCodeCommand, bool>
{
    public async Task<bool> Handle(DeleteDiscountCodeCommand cmd, CancellationToken ct)
    {
        var code = await uow.DiscountCodes.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(DiscountCode), cmd.Id);
        uow.DiscountCodes.Delete(code);
        await uow.SaveChangesAsync(ct);
        return true;
    }
}
