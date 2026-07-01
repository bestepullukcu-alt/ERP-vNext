namespace Diten.Platform.Domain.Enums.DocumentManagement;

/// <summary>
/// MOD-0029-FU03 — metadata/read-only approval placeholder. No approval workflow, queue, or MOD-0023 integration
/// is implemented in this FU; this value must not drive any side effect beyond computed drift.
/// </summary>
public enum TemplateVariantApprovalStatus
{
    NotRequired = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Blocked = 4
}
