using System.Text.Json;using MediatR;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Application.Features.SandopPlans.Commands;
public sealed record CreateSandopPlanCommand(SandopCommandContext Context,Guid? PlanId,JsonElement Body):IRequest<SandopResult>;
