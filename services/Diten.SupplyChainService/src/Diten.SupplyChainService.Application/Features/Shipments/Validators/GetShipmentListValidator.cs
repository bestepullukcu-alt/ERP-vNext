using FluentValidation;
using Diten.SupplyChainService.Application.Features.Shipments.Queries;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Validators;
public sealed class GetShipmentListValidator : AbstractValidator<GetShipmentListQuery>
{
    public GetShipmentListValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).LessThanOrEqualTo(int.MaxValue / 200);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Status).Must(x => x is null || Enum.GetNames<ShipmentStatus>().Contains(x));
    }
}
