using FluentValidation;
namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;
public sealed class ShipmentLineValidator : AbstractValidator<ShipmentModels.Line>
{
    public ShipmentLineValidator()
    {
        RuleFor(x => x.LineNumber).NotEmpty(); RuleFor(x => x.ItemId).NotEmpty(); RuleFor(x => x.SkuId).NotEmpty(); RuleFor(x => x.UomId).NotEmpty();
        RuleFor(x => x.Quantity).NotEmpty().Matches(@"^-?[0-9]+(\.[0-9]+)?$");
    }
}
