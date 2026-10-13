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
        try
        {
            var challenge = await _mfaChallengeService.ResendEmailChallengeAsync(request.ChallengeId, request.RequestIp, request.UserAgent, ct);
            return Response<MfaChallengeCreated>.Success(challenge);
        }
        catch (UnauthorizedAccessException)
        {
            // WP-USERS-ROLES-CLOSE-FIX1 FIX4 item 5 (found by its test) — a challenge that is unknown, used, expired or resent too
            // often is the door's coded 401 (as the verification's), not the pipeline's 500.
            return Response<MfaChallengeCreated>.Fail("Verification challenge is not available.",
                [new ResponseError(AuthRefusalCodes.InvalidVerificationCode)], 401);
        }
    }
}
