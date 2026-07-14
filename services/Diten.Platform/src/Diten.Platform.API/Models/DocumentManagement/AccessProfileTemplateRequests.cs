using Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates;

namespace Diten.Platform.API.Models.DocumentManagement;

/// <summary>
/// MOD-0029-FU05 — request to generate Access Matrix policies from a baseline's register access profiles. No client
/// TenantId is ever accepted; the tenant comes from the server-side tenant context.
/// </summary>
public sealed class AccessProfileTemplateGenerationRequest
{
    public Guid BaselineReleaseId { get; set; }

    /// <summary>Definition (preview/dry-run only) or Instance (runtime-enforced).</summary>
    public AccessProfileTemplateScope Scope { get; set; } = AccessProfileTemplateScope.Instance;

    public List<string>? IncludeProfiles { get; set; }
    public List<string>? ExcludeProfiles { get; set; }

    /// <summary>Apply the Effective/Superseded_Retired read-only + In_Review restrictions for GQMS status folders.</summary>
    public bool ApplyReadOnlyStatusFolderRules { get; set; } = true;
}
