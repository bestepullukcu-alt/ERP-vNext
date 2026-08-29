namespace Diten.Platform.API.Models.DocumentManagement;

public sealed class ProvisionCorporateCollectionInstanceRequest
{
    public Guid BaselineReleaseId { get; set; }
    public Guid CorporateOwnerId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
}
