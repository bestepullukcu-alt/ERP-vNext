using FluentValidation;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;
public sealed class TransitionShipmentValidator : AbstractValidator<TransitionShipmentCommand>
{
    public TransitionShipmentValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty(); RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.TargetStatus).Must(x => Enum.GetNames<ShipmentStatus>().Contains(x));
            RuleFor(x => x.Body.OccurredAt).NotEmpty().Must(x => x.Offset == TimeSpan.Zero);
            RuleFor(x => x.Body.Note).Must(ShipmentNoteRules.IsWithinLength);
        });
    }
}
