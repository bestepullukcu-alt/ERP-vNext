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
}
