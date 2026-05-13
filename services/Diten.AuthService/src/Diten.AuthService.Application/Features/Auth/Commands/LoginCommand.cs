using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record LoginCommand(
    string Email,
    string Password,
    string RequestIp,
    string? UserAgent,
    bool RememberMe = false
) : IRequest<Response<AuthResponse>>;
