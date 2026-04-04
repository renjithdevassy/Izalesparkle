using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Application.Admin.Commands;

public record GetUsersAdminQuery : IRequest<IEnumerable<UserListResponse>>;

public sealed class GetUsersAdminQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetUsersAdminQuery, IEnumerable<UserListResponse>>
{
    public async Task<IEnumerable<UserListResponse>> Handle(
        GetUsersAdminQuery req, CancellationToken ct)
    {
        var users = await uow.Users.GetAllAsync(ct);
        return users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserListResponse(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.FullName,
                u.Role.ToString(),
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt));
    }
}
