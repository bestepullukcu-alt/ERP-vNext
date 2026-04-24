using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

public sealed record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    bool IsActive
) : IRequest<UserDto>;
