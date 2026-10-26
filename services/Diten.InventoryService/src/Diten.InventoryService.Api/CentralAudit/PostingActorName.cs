using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Api.CentralAudit;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (FU-0173-MOV-ACTORNAME) — <see cref="IPostingActorName"/>: the stored name a background flow handed over, else the
/// caller's token name exactly as the central audit queue records it (<see cref="ICentralAuditScope.ActorDisplayName"/>), so a movement and
/// its audit row carry the same name. Blank → null.
/// </summary>
public sealed class PostingActorName(ICentralAuditScope scope) : IPostingActorName
{
    private string? _stored;

    public string? Current => _stored ?? Clean(scope.ActorDisplayName);

    public void Use(string? name) => _stored = Clean(name);

    private static string? Clean(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();
}
