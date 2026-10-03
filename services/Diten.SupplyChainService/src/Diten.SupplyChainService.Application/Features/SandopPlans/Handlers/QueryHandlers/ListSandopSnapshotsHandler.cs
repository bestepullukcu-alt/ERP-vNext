using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;using Diten.SupplyChainService.Application.Features.SandopPlans.Queries;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Handlers.QueryHandlers;
public sealed class ListSandopSnapshotsHandler(ISandopRepository repository):IRequestHandler<ListSandopSnapshotsQuery,SandopResult>
{ public Task<SandopResult> Handle(ListSandopSnapshotsQuery request,CancellationToken ct)=>repository.ReadAsync(request.Context.Scope,SandopAction.ListSnapshots,request.PlanId,ct); }
