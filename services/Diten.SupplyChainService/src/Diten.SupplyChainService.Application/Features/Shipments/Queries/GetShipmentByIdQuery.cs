using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Shipments.Queries;
public sealed record GetShipmentByIdQuery(Guid ShipmentId) : IRequest<Response<JsonObject>>;
