namespace Diten.Web.Models.ManagementGovernance.ProcessModeling.PerformanceImprovement;

public static class PerformanceImprovementFrontendPermissions
{
    public const string Read = "management-governance.process-modeling.improvements.read";
    public const string Create = "management-governance.process-modeling.improvements.create";
    public const string Update = "management-governance.process-modeling.improvements.update";
    public const string Qualify = "management-governance.process-modeling.improvements.qualify";
    public const string Close = "management-governance.process-modeling.improvements.close";
    public const string RecordOutcome = "management-governance.process-modeling.improvements.record-outcome";
    public static IReadOnlySet<string> Exact { get; } = new HashSet<string>(
        [Read, Create, Update, Qualify, Close, RecordOutcome], StringComparer.Ordinal);
}

public sealed record PerformanceImprovementIndexViewModel(bool GatewayReady, IReadOnlySet<string> Permissions);
public sealed record PerformanceImprovementDetailViewModel(Guid OpportunityId, bool GatewayReady, IReadOnlySet<string> Permissions);
