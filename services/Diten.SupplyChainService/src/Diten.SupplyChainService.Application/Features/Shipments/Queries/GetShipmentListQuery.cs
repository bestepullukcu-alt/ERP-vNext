using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Shipments.Queries;
// 3.5.0 (MOD-0183 r9 §24.7 item 1): optional search, orderBy, orderDir; absent, the 3.4.0 behaviour is kept.
public sealed record GetShipmentListQuery(string? Status, string? SourceDocumentId, int Page = 1, int PageSize = 50,
    string? Search = null, string? OrderBy = null, string? OrderDir = null) : IRequest<Response<JsonObject>>;
