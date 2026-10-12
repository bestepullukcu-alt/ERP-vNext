using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;using Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Handlers.CommandHandlers;
public sealed class CreateSandopPlanHandler(ISandopRepository repository,IDemandFixtureReader fixture):IRequestHandler<CreateSandopPlanCommand,SandopResult>
{ public Task<SandopResult> Handle(CreateSandopPlanCommand request,CancellationToken ct)
{ var bad=SandopWire.Validate(SandopAction.Create,request.Body);return bad is not null?Task.FromResult(bad):repository.MutateAsync(request.Context.Scope,SandopAction.Create,request.PlanId,request.Context.Key,SandopRequestFingerprint.Create(request.Body),request.Context.Correlation,request.Body,fixture,ct); } }
