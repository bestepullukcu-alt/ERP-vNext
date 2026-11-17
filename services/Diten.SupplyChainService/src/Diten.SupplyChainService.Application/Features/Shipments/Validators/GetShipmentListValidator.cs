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
        // 3.5.0 (MOD-0183 r9 §24.7 item 1): search maxLength 100 (code points), orderBy / orderDir enums of the contract.
        RuleFor(x => x.Search).Must(x => x is null || x.EnumerateRunes().Count() <= 100);
        RuleFor(x => x.OrderBy).Must(x => x is null || ShipmentListOrder.Columns.ContainsKey(x));
        RuleFor(x => x.OrderDir).Must(x => x is null || x is "asc" or "desc");
    }
}
