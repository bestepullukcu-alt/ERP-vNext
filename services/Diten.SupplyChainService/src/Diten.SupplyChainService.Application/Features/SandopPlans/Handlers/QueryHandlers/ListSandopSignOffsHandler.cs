using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;using Diten.SupplyChainService.Application.Features.SandopPlans.Queries;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Handlers.QueryHandlers;
public sealed class ListSandopSignOffsHandler(ISandopRepository repository):IRequestHandler<ListSandopSignOffsQuery,SandopResult>
{ public Task<SandopResult> Handle(ListSandopSignOffsQuery request,CancellationToken ct)=>repository.ReadAsync(request.Context.Scope,SandopAction.ListSignOffs,request.PlanId,ct); }
