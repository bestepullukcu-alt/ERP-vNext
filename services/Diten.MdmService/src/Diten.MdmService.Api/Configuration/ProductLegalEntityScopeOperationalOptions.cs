namespace Diten.MdmService.Api.Configuration;

public sealed class ProductLegalEntityScopeOperationalOptions
{
    public const string SectionName = "ProductLegalEntityScope:Operational";

    public bool Enabled { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? CommandId { get; set; }
    public Guid? ExpectedRolloutStateId { get; set; }
    public int? ExpectedRolloutVersion { get; set; }
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 B3 — with ActivateEnforced / SuspendFailClosed and the SAME command, actor, reason and
    /// expected rollout as the transition that took a fence: abandon that fence (recorded on the rollout), writes reopen.
    /// </summary>
    public bool Abort { get; set; }
}
