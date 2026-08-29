namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

public sealed class ProcessKpiBindingDependencyGate
{
    public int StoreInvocationCount { get; private set; }

    public PerformanceImprovementContract.MutationResult Evaluate(string operation, Guid requestId)
    {
        if (requestId == Guid.Empty) return PerformanceImprovementContract.MutationResult.Invalid(requestId);
        if (operation is not PerformanceImprovementPermissions.KpiRead and
            not PerformanceImprovementPermissions.KpiCreate and
            not PerformanceImprovementPermissions.KpiArchive)
            return PerformanceImprovementContract.MutationResult.Invalid(requestId, "kpi_operation_forbidden");

        return PerformanceImprovementContract.MutationResult.Unavailable(requestId, "kpi_reference_contract_unavailable");
    }
}
