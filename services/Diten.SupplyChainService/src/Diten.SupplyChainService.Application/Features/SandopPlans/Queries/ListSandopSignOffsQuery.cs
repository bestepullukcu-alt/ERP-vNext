using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Queries;
public sealed record ListSandopSignOffsQuery(SandopQueryContext Context,Guid PlanId):IRequest<SandopResult>;
