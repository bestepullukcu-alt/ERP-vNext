namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public static class PayrollIntegrationGovernanceCollectionNames
{
    public const string Runs = "payroll_integration_runs";
    public const string SourceLinks = "payroll_integration_source_links";
    public const string MappingControls = "payroll_integration_mapping_controls";
    public const string ReconciliationControls = "payroll_integration_reconciliation_controls";
    public const string Exceptions = "payroll_integration_exceptions";
    public const string RetryReplayRequests = "payroll_integration_retry_replay_requests";
    public const string EvidenceExportReferences = "payroll_integration_evidence_export_references";
    public const string HealthSnapshots = "payroll_integration_health_snapshots";
}
