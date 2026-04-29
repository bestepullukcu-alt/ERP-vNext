using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record RefreshTokenCommand(
    string AccessToken,
    string RefreshToken,
    string RequestIp,
    string? UserAgent
) : IRequest<AuthResponse>;
