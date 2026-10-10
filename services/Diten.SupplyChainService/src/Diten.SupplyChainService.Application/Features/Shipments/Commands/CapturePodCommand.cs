using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Shipments.Commands;
public sealed record CapturePodCommand(Guid ShipmentId, ShipmentModels.Pod Body) : IRequest<Response<JsonObject>>;
