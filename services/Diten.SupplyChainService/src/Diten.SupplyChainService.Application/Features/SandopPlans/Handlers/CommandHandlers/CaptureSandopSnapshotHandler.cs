using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;using Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Handlers.CommandHandlers;
public sealed class CaptureSandopSnapshotHandler(ISandopRepository repository,IDemandFixtureReader fixture):IRequestHandler<CaptureSandopSnapshotCommand,SandopResult>
{ public Task<SandopResult> Handle(CaptureSandopSnapshotCommand request,CancellationToken ct)
{ var bad=SandopWire.Validate(SandopAction.Capture,request.Body);return bad is not null?Task.FromResult(bad):repository.MutateAsync(request.Context.Scope,SandopAction.Capture,request.PlanId,request.Context.Key,SandopRequestFingerprint.Create(request.Body),request.Context.Correlation,request.Body,fixture,ct); } }
