using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Commands;

public sealed record ResendMfaCommand(
    string ChallengeId,
    string RequestIp,
    string? UserAgent
) : IRequest<Response<MfaChallengeCreated>>;
