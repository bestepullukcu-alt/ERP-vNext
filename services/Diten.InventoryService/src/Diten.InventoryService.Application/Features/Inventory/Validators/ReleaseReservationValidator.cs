using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using FluentValidation;

namespace Diten.InventoryService.Application.Features.Inventory.Validators;

/// <summary>Contract: <c>If-Match</c> is required on releaseReservation (pack ASSUMPTION-0173-17: missing → 400).</summary>
public sealed class ReleaseReservationValidator : AbstractValidator<ReleaseReservationCommand>
{
    public ReleaseReservationValidator()
    {
        RuleFor(x => x.IfMatch).NotEmpty()
            .WithMessage("The If-Match header is required.")
            .WithErrorCode(InventoryErrorCodes.InvalidRequest);
    }
}
