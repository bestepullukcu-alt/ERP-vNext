using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

/*
 * Direct GSKU retirement (POST /api/gskus/{id}/retire) is CLOSED, for everyone.
 *
 * Retiring a GSKU directly, without an approved request, is a system action (separation of duties, B5): a person
 * retires through a retirement REQUEST, which is approved, and whose approval the system carries out (the pair
 * retirement, FirstGskuIdentityRetirementProcessor). No system actor calls this endpoint today and MDM has no inbound
 * service identity to recognise one, so FIX1's "a non-human caller passes" branch could never be reached (it carried
 * Guid.Empty as the actor). WP-MDM-GSKU-ACCEPT-01 FIX2 item 13 removed it: every caller — human or not — is refused
 * with the same code. When a system actor is defined, the endpoint is opened here, for that identity only, with its
 * own test (runbook: "Direct GSKU retirement").
 */
public sealed class RetireGskuIdentityPairHandler
    : IRequestHandler<RetireGskuIdentityPairCommand, Response<GskuPairRetirementResult>>
{
    public const string DirectRetirementIsSystemOnly = "GSKU_DIRECT_RETIREMENT_SYSTEM_ONLY";

    public Task<Response<GskuPairRetirementResult>> Handle(
        RetireGskuIdentityPairCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(Response<GskuPairRetirementResult>.Fail(DirectRetirementIsSystemOnly, 403));
    }
}
