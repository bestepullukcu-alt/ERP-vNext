using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

public sealed record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 20
) : IRequest<PaginatedResult<UserDto>>;
