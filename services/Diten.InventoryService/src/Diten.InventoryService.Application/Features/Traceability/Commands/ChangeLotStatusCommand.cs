using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Commands;

/// <summary>POST /api/trace/lots/{lotId}/status (v1.1 <c>changeLotStatus</c>, K-E). Raw values; the handler parses.</summary>
public sealed record ChangeLotStatusCommand : IRequest<Response<LotRefDto>>
{
    public string? LotId { get; init; }
    public string? IfMatch { get; init; }
    public string? HeaderIdempotencyKey { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? ToStatus { get; init; }
    public string? Reason { get; init; }
    public bool? AcknowledgeImpact { get; init; }
}
