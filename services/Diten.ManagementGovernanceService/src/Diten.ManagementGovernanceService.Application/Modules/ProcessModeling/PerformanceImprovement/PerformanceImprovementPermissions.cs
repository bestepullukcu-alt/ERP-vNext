namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

public static class PerformanceImprovementPermissions
{
    public const string KpiRead = "management-governance.process-modeling.kpi-bindings.read";
    public const string KpiCreate = "management-governance.process-modeling.kpi-bindings.create";
    public const string KpiArchive = "management-governance.process-modeling.kpi-bindings.archive";
    public const string ImprovementRead = "management-governance.process-modeling.improvements.read";
    public const string ImprovementCreate = "management-governance.process-modeling.improvements.create";
    public const string ImprovementUpdate = "management-governance.process-modeling.improvements.update";
    public const string ImprovementQualify = "management-governance.process-modeling.improvements.qualify";
    public const string ImprovementClose = "management-governance.process-modeling.improvements.close";
    public const string ImprovementRecordOutcome = "management-governance.process-modeling.improvements.record-outcome";

    public static IReadOnlyDictionary<string, string> ExactOperationMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [KpiRead] = KpiRead,
        [KpiCreate] = KpiCreate,
        [KpiArchive] = KpiArchive,
        [ImprovementRead] = ImprovementRead,
        [ImprovementCreate] = ImprovementCreate,
        [ImprovementUpdate] = ImprovementUpdate,
        [ImprovementQualify] = ImprovementQualify,
        [ImprovementClose] = ImprovementClose,
        [ImprovementRecordOutcome] = ImprovementRecordOutcome
    };

    public static string RequiredFor(PerformanceImprovementContract.Command command) => command switch
    {
        PerformanceImprovementContract.Command.CreateOpportunity => ImprovementCreate,
        PerformanceImprovementContract.Command.UpdateOpportunity => ImprovementUpdate,
        PerformanceImprovementContract.Command.QualifyOpportunity => ImprovementQualify,
        PerformanceImprovementContract.Command.CloseOpportunity => ImprovementClose,
        PerformanceImprovementContract.Command.RecordOutcome => ImprovementRecordOutcome,
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
