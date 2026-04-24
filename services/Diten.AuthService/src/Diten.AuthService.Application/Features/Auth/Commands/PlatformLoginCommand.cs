using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record PlatformLoginCommand(
    string Email,
    string Password
) : IRequest<AuthResponse>;
