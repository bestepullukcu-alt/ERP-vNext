using FluentValidation;using Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Validators;
public sealed class CreateSandopPlanValidator:AbstractValidator<CreateSandopPlanCommand>
{ public CreateSandopPlanValidator() { RuleFor(x=>x.Context.Scope.TenantId).NotEmpty();RuleFor(x=>x.Context.Scope.LegalEntityId).NotEmpty();RuleFor(x=>x.Context.Scope.ActorId).NotEmpty();RuleFor(x=>x.Context.Key).NotEmpty(); } }
