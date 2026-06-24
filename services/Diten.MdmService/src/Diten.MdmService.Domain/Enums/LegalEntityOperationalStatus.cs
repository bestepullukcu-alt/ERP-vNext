namespace Diten.MdmService.Domain.Enums;

// MOD-0220 FULL — operational lifecycle. Replaces the legacy LegalEntityLifecycleStatus
// (Draft=1/Active=2/Archived=3); existing rows are backfilled by LegalEntityOperationalStatusMigration.
// IsReferenceable is now driven by Active here. Int values are PERSISTED — do not reorder.
public enum LegalEntityOperationalStatus
{
    Draft = 1,
    InReview = 2,
    Approved = 3,
    Active = 4,
    Suspended = 5,
    Archived = 6
}
