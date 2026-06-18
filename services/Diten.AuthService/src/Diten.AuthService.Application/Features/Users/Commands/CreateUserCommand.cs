using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Commands;

// Password is optional. When supplied the user is created active via the self-service path.
// When omitted (null/empty) the user is created as an INVITATION: inactive, must-change-password,
// with a set-password token, and a "set your password" email link is sent.
public sealed record CreateUserCommand(
    string Email,
    string? Password,
    string FirstName,
    string LastName
) : IRequest<Response<UserDto>>;
