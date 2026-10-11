using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;using Diten.SupplyChainService.Application.Features.SandopPlans.Queries;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Handlers.QueryHandlers;
public sealed class GetSandopPlanHandler(ISandopRepository repository):IRequestHandler<GetSandopPlanQuery,SandopResult>
{ public Task<SandopResult> Handle(GetSandopPlanQuery request,CancellationToken ct)=>repository.ReadAsync(request.Context.Scope,SandopAction.Get,request.PlanId,ct); }
