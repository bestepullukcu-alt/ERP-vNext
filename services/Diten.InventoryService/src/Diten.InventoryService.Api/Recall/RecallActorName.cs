using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Features.Recall.Ports;

namespace Diten.InventoryService.Api.Recall;

/// <summary>
/// WP-MVP1-FIX-LIVE2 — <see cref="IRecallActorName"/>: the caller's display name exactly as the central audit queue records it
/// (<see cref="ICentralAuditScope.ActorDisplayName"/> — the token's name / given + family name claims), so a recall event and its audit row
/// carry the same name. Null outside a request.
/// </summary>
public sealed class RecallActorName(ICentralAuditScope scope) : IRecallActorName
{
    public string? Current => string.IsNullOrWhiteSpace(scope.ActorDisplayName) ? null : scope.ActorDisplayName.Trim();
}
