using FluentValidation;
using Diten.SupplyChainService.Application.Features.Carriers.Queries;
using Diten.SupplyChainService.Domain.Features.Carriers;
namespace Diten.SupplyChainService.Application.Features.Carriers.Validators;
public sealed class GetCarrierListValidator : AbstractValidator<GetCarrierListQuery>
{
    public GetCarrierListValidator() => RuleFor(x => x.Status).Must(x => x is null || Enum.GetNames<CarrierStatus>().Contains(x, StringComparer.Ordinal));
}
