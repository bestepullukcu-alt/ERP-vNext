using FluentValidation;using Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Validators;
public sealed class RecordSandopSignOffValidator:AbstractValidator<RecordSandopSignOffCommand>
{ public RecordSandopSignOffValidator() { RuleFor(x=>x.Context.Scope.TenantId).NotEmpty();RuleFor(x=>x.Context.Scope.LegalEntityId).NotEmpty();RuleFor(x=>x.Context.Scope.ActorId).NotEmpty();RuleFor(x=>x.Context.Key).NotEmpty(); } }
