using FluentValidation;
namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;
public sealed class ShipmentLineValidator : AbstractValidator<ShipmentModels.Line>
{
    public ShipmentLineValidator()
    {
        RuleFor(x => x.LineNumber).NotEmpty(); RuleFor(x => x.ItemId).NotEmpty(); RuleFor(x => x.SkuId).NotEmpty(); RuleFor(x => x.UomId).NotEmpty();
        RuleFor(x => x.Quantity).NotEmpty().Matches(@"^-?[0-9]+(\.[0-9]+)?$");
        // 3.4.0: fefoBypassReason 10–1000 when present (the INVENTORY 1.3.0 bounds the contract repeats).
        RuleFor(x => x.FefoBypassReason).Length(10, 1000).When(x => x.FefoBypassReason is not null);
    }
}
