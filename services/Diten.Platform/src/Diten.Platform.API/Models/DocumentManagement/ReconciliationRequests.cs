using Diten.Platform.Application.Features.DocumentManagementReconciliation;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.API.Models.DocumentManagement;

/// <summary>MOD-0028-FU09 read-back reconciliation request. BaselineReleaseId comes from the route, never the body.</summary>
public sealed class ReconciliationRunApiRequest
{
    public ReconciliationScope Scope { get; set; } = ReconciliationScope.DefinitionToInstance;
    public ProvisioningPlatformProvider Provider { get; set; } = ProvisioningPlatformProvider.InHouse;
}

/// <summary>MOD-0028-FU09 provisioning evidence upsert. No client TenantId is ever accepted.</summary>
public sealed class ProvisioningEvidenceUpsertApiRequest
{
    public Guid BaselineReleaseId { get; set; }
    public Guid CollectionInstanceId { get; set; }
    public Guid? CollectionDefinitionId { get; set; }
    public string? RegisterFolderId { get; set; }
    public string? RegisterParentFolderId { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public ProvisioningPlatformProvider PlatformProvider { get; set; } = ProvisioningPlatformProvider.InHouse;
    public string? PlatformFolderId { get; set; }
    public string? PlatformParentId { get; set; }
    public ProvisioningEvidenceStatus? ProvisioningStatus { get; set; }
    public DateTimeOffset? CreatedOnPlatformAt { get; set; }
    public string? CreatedOnPlatformBy { get; set; }
    public string? DeviationComment { get; set; }
}

/// <summary>MOD-0028-FU09 deviation resolve/accept request.</summary>
public sealed class DeviationResolutionApiRequest
{
    public string? Comment { get; set; }
}
