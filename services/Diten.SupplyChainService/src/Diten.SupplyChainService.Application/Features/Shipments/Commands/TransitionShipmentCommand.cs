using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Shipments.Commands;
public sealed record TransitionShipmentCommand(Guid ShipmentId, ShipmentModels.Transition Body) : IRequest<Response<JsonObject>>;
