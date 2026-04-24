using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName
) : IRequest<UserDto>;
