namespace Diten.Web.Models.SupplyChain.CapacityPlans;

// MOD-0192 Capacity Planning — tenant UI (pack §23). DRAFT overlay — not built, not writer-complete.
//
// These types carry NO business rule of their own. The same-origin adapter forwards the browser's body text unchanged
// (pack §23.8 "same key and identical body text"), so nothing here parses or rewrites a request body. Field validation
// stays with the backend (CapacityPlanModels.cs:4-27 and Validators/*.cs in the accepted isolated source
// docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz ebd5d80c…7064).

/// <summary>Permission keys the Capacity UI checks. Existing keys only — CapacityPermissions.cs:4-7 (Read, Create,
/// ScenarioCreate, Evaluate) in the accepted isolated source. The UI check is a display decision (UAS-001 §4); the
/// backend <c>[CapacityPermission]</c> gate stays the authority.</summary>
public static class CapacityUiPermissions
{
    public const string Read = "supplychain.capacity-plans.read";                      // CapacityPermissions.Read
    public const string Create = "supplychain.capacity-plans.create";                  // CapacityPermissions.Create
    public const string ScenarioCreate = "supplychain.capacity-plans.scenario.create"; // CapacityPermissions.ScenarioCreate
    public const string Evaluate = "supplychain.capacity-plans.evaluate";              // CapacityPermissions.Evaluate
}

/// <summary>Published vocabulary the page renders (display only; the backend decides).</summary>
public static class CapacityUiVocabulary
{
    /// <summary>CapacityPlan.status enum, sandop-capacity.openapi.yaml (schema CapacityPlan).</summary>
    public static readonly IReadOnlyList<string> PlanStatuses = ["Draft", "Evaluating", "Ready", "Approved", "Archived"];

    /// <summary>CapacityScenario.status enum.</summary>
    public static readonly IReadOnlyList<string> ScenarioStatuses = ["Draft", "Evaluating", "Evaluated", "Archived"];

    /// <summary>CapacityEvaluation.status enum.</summary>
    public static readonly IReadOnlyList<string> EvaluationStatuses = ["Accepted", "Running", "Completed", "Failed"];

    /// <summary>EvaluateCapacityScenarioRequest.evaluationMode enum (EvaluateCapacityScenarioValidator.cs:10 mirror).</summary>
    public static readonly IReadOnlyList<string> EvaluationModes = ["Finite", "Infinite"];

    /// <summary>Evaluation statuses in which Refresh is shown and Evaluate is withheld (pack §23.7).</summary>
    public static readonly IReadOnlyList<string> ActiveEvaluationStatuses = ["Accepted", "Running"];

    /// <summary>Decimal string pattern mirrored for availableCapacityDelta (pack §23.6; contract Decimal schema).</summary>
    public const string DecimalPattern = @"^-?\d+(\.\d+)?$";

    /// <summary>Create-plan form required fields (pack §23.6, form_field_count 7).</summary>
    public const int CreateRequiredFieldCount = 7;
}

/// <summary>Values rendered into each page's permission JSON island (display decision only, UAS-001 §4).</summary>
public sealed record CapacityPagePermissions(bool CanCreate, bool CanCreateScenario, bool CanEvaluate)
{
    public static CapacityPagePermissions From(Func<string, bool> has) =>
        new(has(CapacityUiPermissions.Create), has(CapacityUiPermissions.ScenarioCreate), has(CapacityUiPermissions.Evaluate));
}
