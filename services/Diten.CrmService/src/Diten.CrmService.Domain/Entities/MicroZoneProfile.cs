namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 value object embedded on a <see cref="TerritoryNode"/> whose level is <c>microzone</c> only
/// (pack §12 / §7.9). <see cref="AnchorAccountId"/> is a planning-center / cluster anchor — metadata only.
/// It is NOT a route start; FU01 triggers no route/distance/nearby logic from it (that is MOD-0155).
/// On any other level this profile MUST be null (validation rule §20).
/// </summary>
public sealed class MicroZoneProfile
{
    public Guid? AnchorAccountId { get; set; }
    public string? ClusterNotes { get; set; }
    public string? PlanningCenterType { get; set; }
}
