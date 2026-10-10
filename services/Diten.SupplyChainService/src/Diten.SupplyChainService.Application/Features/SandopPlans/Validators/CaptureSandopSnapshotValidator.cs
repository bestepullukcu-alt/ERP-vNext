using FluentValidation;using Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Validators;
public sealed class CaptureSandopSnapshotValidator:AbstractValidator<CaptureSandopSnapshotCommand>
{ public CaptureSandopSnapshotValidator() { RuleFor(x=>x.Context.Scope.TenantId).NotEmpty();RuleFor(x=>x.Context.Scope.LegalEntityId).NotEmpty();RuleFor(x=>x.Context.Scope.ActorId).NotEmpty();RuleFor(x=>x.Context.Key).NotEmpty(); } }
