namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

public sealed class ProcessKpiBindingDependencyGate
{
    public int ProviderInvocationCount { get; private set; }
    public int BusinessStoreInvocationCount { get; private set; }
    public int StoreInvocationCount { get; private set; }

    public ProcessPerformanceUnavailableContract.Result Evaluate(string operation)
    {
        if (operation is not PerformanceImprovementPermissions.KpiRead and
            not PerformanceImprovementPermissions.KpiCreate and
            not PerformanceImprovementPermissions.KpiArchive)
            throw new ArgumentOutOfRangeException(nameof(operation), "kpi_operation_forbidden");

        return ProcessPerformanceUnavailableContract.Result.DependencyUnavailable;
    }

    public PerformanceImprovementContract.MutationResult Evaluate(string operation, Guid requestId)
    {
        if (requestId == Guid.Empty)
            return PerformanceImprovementContract.MutationResult.Invalid(requestId);

        var result = Evaluate(operation);
        return PerformanceImprovementContract.MutationResult.Unavailable(requestId, result.Code);
    }
}
