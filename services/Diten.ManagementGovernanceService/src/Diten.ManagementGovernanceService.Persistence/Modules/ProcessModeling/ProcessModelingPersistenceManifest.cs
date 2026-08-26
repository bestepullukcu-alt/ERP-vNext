namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public sealed record ProcessModelingIndex(string Name, string Collection, IReadOnlyList<string> Keys, bool Unique = false, string? PartialFilterJson = null, bool Ttl = false);

public static class ProcessModelingPersistenceManifest
{
    public static IReadOnlyList<string> Collections { get; } =
    [
        "mg_process_architectures", "mg_process_domains", "mg_process_families", "mg_process_definitions",
        "mg_process_models", "mg_process_model_versions", "mg_process_activities", "mg_process_control_points",
        "mg_process_relationships", "mg_process_modeling_idempotency_receipts", "mg_process_modeling_audit_intents",
        "mg_process_modeling_outbox_messages"
    ];

    public static IReadOnlyList<ProcessModelingIndex> Indexes { get; } =
    [
        I("ux_pm_architecture_code",0,["TenantId","ArchitectureCode"],true,Technical),
        I("ux_pm_domain_code",1,["TenantId","ProcessArchitectureId","DomainCode"],true,Technical),
        I("ux_pm_family_code",2,["TenantId","ProcessDomainId","FamilyCode"],true,Technical),
        I("ux_pm_definition_code",3,["TenantId","ProcessCode"],true,Technical),
        I("ux_pm_model_code",4,["TenantId","ProcessDefinitionId","ModelCode"],true,Technical),
        I("ux_pm_revision",5,["TenantId","ProcessModelId","RevisionNumber"],true),
        I("ux_pm_open_version",5,["TenantId","ProcessModelId"],true,"{ LifecycleState: { $in: [ 'Draft', 'Review' ] }, IsDeleted: false }"),
        I("ux_pm_activity_code",6,["TenantId","ProcessModelVersionId","ActivityCode"],true),
        I("ux_pm_activity_logical",6,["TenantId","ProcessModelVersionId","LogicalActivityId"],true),
        I("ux_pm_control_code",7,["TenantId","ProcessModelVersionId","ControlCode"],true),
        I("ux_pm_control_logical",7,["TenantId","ProcessModelVersionId","LogicalControlPointId"],true),
        I("ux_pm_relationship",8,["TenantId","ProcessModelVersionId","FromActivityId","ToActivityId","ConditionLabel","SortOrder"],true),
        I("ux_pm_receipt_key",9,["TenantId","CommandFamily","IdempotencyKey"],true),
        I("ix_pm_receipt_subject",9,["TenantId","SubjectId","CreatedAtUtc"]),
        I("ux_pm_audit_intent",10,["TenantId","AuditIntentId"],true),
        I("ux_pm_outbox_event",11,["TenantId","EventId"],true)
    ];

    private const string Technical = "{ IsDeleted: false }";
    private static ProcessModelingIndex I(string name, int collection, string[] keys, bool unique=false, string? partial=null) => new(name, Collections[collection], keys, unique, partial);
}
