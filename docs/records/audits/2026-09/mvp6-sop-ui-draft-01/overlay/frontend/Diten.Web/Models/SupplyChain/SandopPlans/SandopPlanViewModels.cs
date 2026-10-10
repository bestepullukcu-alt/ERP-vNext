namespace Diten.Web.Models.SupplyChain.SandopPlans;

// MOD-0190 S&OP Workflow & Sign-offs — tenant UI (pack §23). DRAFT overlay — not built, not writer-complete.
//
// These types carry NO business rule of their own. The same-origin adapter forwards the browser's body text unchanged
// (pack §23.8 "same key and identical body text"), so nothing here parses or rewrites a request body. Field validation
// stays with the backend (SandopPlanModels.cs:8-25 SandopWire.Validate in the accepted isolated source
// docs/records/audits/2026-09/mvp6-mod0190-test-oracle-rework-01/source.tar.gz 8fa00d40…b745).

/// <summary>Permission keys the S&OP UI checks. Existing keys only — SandopPermissions.cs:3 (Read, Create, Capture,
/// SignOff) in the accepted isolated source. The UI check is a display decision (UAS-001 §4); the backend
/// <c>[SandopPermission]</c> gate stays the authority.</summary>
public static class SandopUiPermissions
{
    public const string Read = "supplychain.sandop-plans.read";                // SandopPermissions.Read
    public const string Create = "supplychain.sandop-plans.create";            // SandopPermissions.Create
    public const string Capture = "supplychain.sandop-plans.snapshot.capture"; // SandopPermissions.Capture
    public const string SignOff = "supplychain.sandop-plans.sign-off.record";  // SandopPermissions.SignOff
}

/// <summary>Published vocabulary the page renders (display only; the backend decides).</summary>
public static class SandopUiVocabulary
{
    /// <summary>SandopPlan.status enum, sandop-capacity.openapi.yaml (schema SandopPlan).</summary>
    public static readonly IReadOnlyList<string> PlanStatuses = ["Draft", "InReview", "Approved", "Rejected", "Archived"];

    /// <summary>RecordSignOffRequest.role enum (SandopPlanModels.cs:22 mirror).</summary>
    public static readonly IReadOnlyList<string> Roles = ["DemandPlanning", "SupplyPlanning", "Finance", "Operations", "Executive"];

    /// <summary>RecordSignOffRequest.decision enum.</summary>
    public static readonly IReadOnlyList<string> Decisions = ["Approved", "Rejected"];

    /// <summary>Statuses in which "Capture snapshot" is shown (pack §23.7, §13).</summary>
    public static readonly IReadOnlyList<string> CaptureStatuses = ["Draft", "InReview"];

    /// <summary>Statuses in which "Record sign-off" is shown, with at least one listed snapshot (pack §23.7, §13).</summary>
    public static readonly IReadOnlyList<string> SignOffStatuses = ["InReview"];

    /// <summary>RecordSignOffRequest.comment maxLength (SandopPlanModels.cs:23).</summary>
    public const int CommentMaxLength = 2000;

    /// <summary>Create form required fields (pack §23.6, form_field_count 5).</summary>
    public const int CreateRequiredFieldCount = 5;
}

/// <summary>Values rendered into each page's permission JSON island (display decision only, UAS-001 §4).</summary>
public sealed record SandopPagePermissions(bool CanCreate, bool CanCapture, bool CanSignOff)
{
    public static SandopPagePermissions From(Func<string, bool> has) =>
        new(has(SandopUiPermissions.Create), has(SandopUiPermissions.Capture), has(SandopUiPermissions.SignOff));
}
