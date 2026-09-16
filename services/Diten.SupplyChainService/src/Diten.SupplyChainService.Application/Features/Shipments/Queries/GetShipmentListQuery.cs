using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Shipments.Queries;
public sealed record GetShipmentListQuery(string? Status, string? SourceDocumentId, int Page = 1, int PageSize = 50) : IRequest<Response<JsonObject>>;
