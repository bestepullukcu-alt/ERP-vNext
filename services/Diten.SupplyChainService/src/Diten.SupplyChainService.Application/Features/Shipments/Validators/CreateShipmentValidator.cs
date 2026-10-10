using FluentValidation;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;
public sealed class CreateShipmentValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentValidator()
    {
        RuleFor(x => x.Body).NotNull();
        When(x => x.Body is not null, () =>
        {
            RuleFor(x => x.Body.SourceModule).NotEmpty(); RuleFor(x => x.Body.SourceType).NotEmpty(); RuleFor(x => x.Body.SourceDocumentId).NotEmpty();
            RuleFor(x => x.Body.WarehouseReferenceId).NotEmpty(); RuleFor(x => x.Body.ShipToReference).NotEmpty();
            RuleFor(x => x.Body.PlannedShipAt).NotEmpty().Must(x => x.Offset == TimeSpan.Zero);
            RuleFor(x => x.Body.PlannedDeliverAt).Must((r, x) => x is null || (x.Value.Offset == TimeSpan.Zero && x >= r.Body.PlannedShipAt));
            RuleFor(x => x.Body.Lines).NotEmpty();
            When(x => x.Body.Lines is not null, () =>
            {
                RuleFor(x => x.Body.Lines).Must(xs => xs.All(x => x is not null) && xs.Select(x => x.LineNumber?.Trim()).Distinct().Count() == xs.Length);
                RuleForEach(x => x.Body.Lines).SetValidator(new ShipmentLineValidator());
            });
        });
    }
}
