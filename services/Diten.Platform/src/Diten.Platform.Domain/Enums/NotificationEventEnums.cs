namespace Diten.Platform.Domain.Enums;

// MOD-0027-FU03 — Notification Event Catalog lifecycle + classification enums.

public enum NotificationEventStatus
{
    Draft = 0,
    Active = 1,
    Deprecated = 2,
    Archived = 3
}

// MOD-0027-FU03A — Notification Event source model (Bridge). Discriminates how a NotificationEventDefinition entered
// the catalog. Manifest MUST be 0: existing Mongo documents have no SourceType field, so the driver deserializes the
// missing field to default(enum) == 0 == Manifest (backward-compatible; no migration).
public enum NotificationEventSourceType
{
    // FU03 manifest-driven event (reconciled from ModuleManifestDocument.NotificationEvents). Backward-compat default.
    Manifest = 0,
    // FU03A platform fixed/admin seed event (e.g. Platform Admin fixed pages; not a Module Catalog citizen).
    PlatformSeed = 1,
    // FU03A system-owned seed event (forward-reserved; non-tenant system seeds).
    SystemSeed = 2
}

public enum NotificationEventUsageType
{
    // Producer-emitted system event (automatic dispatch).
    SystemEvent = 0,
    // Operator/manually selected event slot (custom/business).
    ManualSelection = 1
}

public enum NotificationEventSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Critical = 3
}

public enum NotificationEventLinkPolicy
{
    None = 0,
    TargetPage = 1,
    CustomUrl = 2
}
