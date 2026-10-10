using FluentValidation;
using Diten.SupplyChainService.Application.Features.Loads.Queries;
using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Application.Features.Loads.Validators;
public sealed class GetLoadListValidator:AbstractValidator<GetLoadListQuery>
{ public GetLoadListValidator() { RuleFor(x=>x.Status).Must(x=>x is null || Enum.GetNames<LoadStatus>().Contains(x)); RuleFor(x=>x.CarrierId).Must(x=>x is null || LoadWire.Uuid(x)); } }
