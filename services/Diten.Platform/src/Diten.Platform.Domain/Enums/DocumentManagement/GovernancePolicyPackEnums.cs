namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU31A — governance policy pack application enums. Kept in a dedicated file so FU31A ownership never
// edits an earlier FU's enum surface.

/// <summary>
/// MOD-0029-FU31A — the outcome of applying the default governance policy pack to a tenant. A preview NEVER
/// produces one of these (preview writes no history at all).
/// </summary>
public enum DocumentGovernancePolicyPackApplicationStatus
{
    /// <summary>Every missing policy was created; no conflict and no warning.</summary>
    Applied = 0,

    /// <summary>Applied, but at least one existing policy diverged from the default and was left untouched.</summary>
    AppliedWithWarnings = 1,

    /// <summary>The apply did not complete. Whatever was already created stays; nothing is rolled back or deleted.</summary>
    Failed = 2
}
