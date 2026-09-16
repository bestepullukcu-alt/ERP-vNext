using FluentValidation;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;
public sealed class CapturePodValidator : AbstractValidator<CapturePodCommand>
{
    public CapturePodValidator()
    {
        RuleFor(x => x.ShipmentId).NotEmpty(); RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.RecipientName).NotEmpty(); RuleFor(x => x.Body.ReceivedAt).NotEmpty().Must(x => x.Offset == TimeSpan.Zero);
            RuleFor(x => x.Body.EvidenceReferenceIds).NotEmpty(); RuleForEach(x => x.Body.EvidenceReferenceIds).NotEmpty(); RuleFor(x => x.Body.Note).Must(ShipmentNoteRules.IsWithinLength);
        });
    }
}
