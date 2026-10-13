using FluentValidation;
using Diten.SupplyChainService.Application.Features.Returns.Queries;
namespace Diten.SupplyChainService.Application.Features.Returns.Validators;
public sealed class GetReturnListValidator:AbstractValidator<GetReturnListQuery>
{ public GetReturnListValidator(){RuleFor(x=>x.Status).Must(s=>s is null||Enum.IsDefined(s.Value));} }
