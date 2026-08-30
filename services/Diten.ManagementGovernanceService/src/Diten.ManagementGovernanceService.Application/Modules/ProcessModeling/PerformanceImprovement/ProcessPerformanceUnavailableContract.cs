namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

public static class ProcessPerformanceUnavailableContract
{
    public const int StatusCode = 503;
    public const string Reason = "kpi_reference_contract_unavailable";
    public const string Availability = "unavailable";

    public sealed record Result(int StatusCode, string Code, string Availability)
    {
        public static Result DependencyUnavailable { get; } =
            new(ProcessPerformanceUnavailableContract.StatusCode, Reason, ProcessPerformanceUnavailableContract.Availability);
    }
}
