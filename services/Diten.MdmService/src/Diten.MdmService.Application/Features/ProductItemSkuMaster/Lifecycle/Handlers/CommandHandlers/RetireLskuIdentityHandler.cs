using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

/*
 * Direct LSKU retirement (POST /api/lskus/{id}/retire) is CLOSED, for everyone — WP-MDM-LSKU-ACCEPT-01 S1, the shape
 * GSKU took in WP-MDM-GSKU-ACCEPT-01 FIX2 item 13 (RetireGskuIdentityPairHandler).
 *
 * Retiring an LSKU directly, without an approved request, is a system action (separation of duties): a person retires
 * an LSKU through a retirement REQUEST, which is approved and carried out by the system
 * (LskuRetirementRequestWorkflowProcessor). It used to retire for any human holding mdm.lskus.retire — a key no
 * dedicated role holds but SuperAdmin received from the full catalogue and an administrator could assign by hand. The
 * key is now non-human in Auth (DefaultRolePermissionTemplate.NonHumanPermissionKeys) and MDM has no inbound system
 * identity to recognise, so every caller is refused with the same code before anything is read or written. When a
 * system actor is defined, the endpoint is opened here, for that identity only, with its own test.
 */
public sealed class RetireLskuIdentityHandler
    : IRequestHandler<RetireLskuIdentityCommand, Response<LskuIdentityLifecycleResult>>
{
    public const string DirectRetirementIsSystemOnly = "LSKU_DIRECT_RETIREMENT_SYSTEM_ONLY";

    public Task<Response<LskuIdentityLifecycleResult>> Handle(
        RetireLskuIdentityCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Task.FromResult(Response<LskuIdentityLifecycleResult>.Fail(DirectRetirementIsSystemOnly, 403));
    }
}
