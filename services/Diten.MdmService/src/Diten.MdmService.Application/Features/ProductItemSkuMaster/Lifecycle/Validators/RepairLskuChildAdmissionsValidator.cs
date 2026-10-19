using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Domain.ValueObjects;
using FluentValidation;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;

/// <summary>
/// WP-MDM-SCOPE-INT-01 FIX1 M1: the repair's request shape is refused by the validation behavior, which runs BEFORE the
/// write fence — an invalid request never takes a writer lease (before, the handler refused it after the fence, and the
/// objectless lease, with no baseline to prove "nothing changed", was kept until it expired, ~120 s). One code, as before.
/// </summary>
public sealed class RepairLskuChildAdmissionsValidator : AbstractValidator<RepairLskuChildAdmissionsCommand>
{
    public RepairLskuChildAdmissionsValidator()
    {
        RuleFor(command => command)
            .Must(command => command.Limit is >= 1 and <= RepairLskuChildAdmissionsHandler.MaximumLimit
                             && command.MinimumAgeMinutes >= RepairLskuChildAdmissionsHandler.MinimumAgeFloorMinutes
                             && command.ChildKind is GskuChildIdentityKind.Lsku or GskuChildIdentityKind.FinishedGood)
            .WithMessage(RepairLskuChildAdmissionsHandler.RequestInvalid);
    }
}
