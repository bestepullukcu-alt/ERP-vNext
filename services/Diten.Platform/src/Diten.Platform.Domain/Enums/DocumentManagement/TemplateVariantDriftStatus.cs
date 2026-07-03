namespace Diten.Platform.Domain.Enums.DocumentManagement;

/// <summary>
/// MOD-0029-FU03 — read-time computed drift of a variant relative to its master. Never persisted: it is derived
/// from master status/version and the variant's last-rebased lineage on every read.
/// </summary>
public enum TemplateVariantDriftStatus
{
    InSync = 0,
    RebaseRequired = 1,
    Drifted = 2,
    Blocked = 3
}
