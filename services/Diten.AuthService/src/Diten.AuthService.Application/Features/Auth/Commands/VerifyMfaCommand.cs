using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record VerifyMfaCommand(
    string ChallengeId,
    string Code,
    string RequestIp,
    string? UserAgent) : IRequest<Response<AuthResponse>>;
