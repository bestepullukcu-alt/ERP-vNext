namespace Diten.Web.Models.ManagementGovernance.ProcessModeling.PerformanceImprovement;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
public sealed record ImprovementCreateInput([property: JsonRequired] Guid ProcessModelVersionId, [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(4000)] string ProblemStatement, [property: StringLength(2000)] string? ExpectedOutcome, [property: Range(1, int.MaxValue)] int? PriorityRank);
public sealed record ImprovementUpdateInput([property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(4000)] string ProblemStatement, [property: StringLength(2000)] string? ExpectedOutcome,
    [property: Range(1, int.MaxValue)] int? PriorityRank, [property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion);
public sealed record ImprovementOutcomeInput([property: Required, StringLength(4000)] string Summary, [property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion);
