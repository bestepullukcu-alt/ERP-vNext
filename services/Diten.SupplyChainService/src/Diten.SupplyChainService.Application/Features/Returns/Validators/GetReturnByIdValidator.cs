using FluentValidation;
using Diten.SupplyChainService.Application.Features.Returns.Queries;
namespace Diten.SupplyChainService.Application.Features.Returns.Validators;
public sealed class GetReturnByIdValidator:AbstractValidator<GetReturnByIdQuery>
{ public GetReturnByIdValidator(){RuleFor(x=>x.ReturnId).NotEqual(Guid.Empty);} }
