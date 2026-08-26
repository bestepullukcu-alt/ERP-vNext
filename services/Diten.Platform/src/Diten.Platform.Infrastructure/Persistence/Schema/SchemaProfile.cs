namespace Diten.Platform.Infrastructure.Persistence.Schema;

/// <summary>
/// A named slice of the platform schema. A test asks for the slice it actually uses; production asks for
/// every slice. Both read the SAME manifest, so a slice can never drift away from what production builds.
/// </summary>
public enum SchemaProfile
{
    /// <summary>Tenants, module catalog, subscriptions, quotas, interface registry, job logs, saved views.</summary>
    Core = 1,

    /// <summary>The audit trail: events, retention policies, tenant preferences, outbox.</summary>
    AccessGovernance = 2,

    /// <summary>MOD-0290 business reference data: sets, versions, assignments, publish operations.</summary>
    BusinessReferenceData = 3,

    /// <summary>Transport bookkeeping: outbox_events, consumed_events.</summary>
    Eventing = 4,

    /// <summary>Messaging settings, templates, dispatches.</summary>
    Notification = 5,

    /// <summary>MOD-0288 organization units, positions, position assignments, person references.</summary>
    Organization = 6,

    /// <summary>MOD-0023 workflow engine and MOD-0024 task engine.</summary>
    WorkflowWorkCenter = 7,

    /// <summary>MOD-0029 controlled documents, templates, shares, access policies.</summary>
    DocumentManagement = 8
}
