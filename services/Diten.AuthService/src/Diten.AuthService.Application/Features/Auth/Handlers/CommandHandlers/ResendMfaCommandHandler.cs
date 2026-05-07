using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class ResendMfaCommandHandler : IRequestHandler<ResendMfaCommand, Response<MfaChallengeCreated>>
{
    private readonly IMfaChallengeService _mfaChallengeService;

    public ResendMfaCommandHandler(IMfaChallengeService mfaChallengeService)
    {
        _mfaChallengeService = mfaChallengeService;
    }

    public async Task<Response<MfaChallengeCreated>> Handle(ResendMfaCommand request, CancellationToken ct)
    {
        var challenge = await _mfaChallengeService.ResendEmailChallengeAsync(request.ChallengeId, request.RequestIp, request.UserAgent, ct);
        return Response<MfaChallengeCreated>.Success(challenge);
    }
}
