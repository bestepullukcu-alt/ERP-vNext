using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Queries;
public sealed record ListSandopSnapshotsQuery(SandopQueryContext Context,Guid PlanId):IRequest<SandopResult>;
