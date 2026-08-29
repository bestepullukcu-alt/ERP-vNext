using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates;

/// <summary>
/// MOD-0029-FU10 → FU11 seam. A PORT the FU10 release gate engine consults when computing Gate 5 (Training readiness,
/// SOP §19 gate 5). FU10 owns the interface; FU11 (training matrix) provides the adapter. Kept optional so the FU10
/// engine has no hard dependency on the training feature — a null port means "use the existing manual/auto Gate 5
/// behaviour" (backward compatible).
/// </summary>
public interface ITrainingReadinessPort
{
    Task<TrainingGateDecision> EvaluateGate5Async(DocumentMasterRegisterEntry entry, CancellationToken ct);
}

/// <summary>What the training feature tells the release gate engine to do for Gate 5.</summary>
public enum TrainingGateOutcome
{
    /// <summary>Training matrix is satisfied — Gate 5 passes with the supplied evidence reference.</summary>
    Pass = 0,

    /// <summary>Training matrix exists but is not ready (or is mandatory-but-missing) — Gate 5 is blocked.</summary>
    Block = 1,

    /// <summary>No training matrix governs this entry — the FU10 engine falls back to its manual/auto Gate 5 logic.</summary>
    FallBackToManual = 2
}

public sealed record TrainingGateDecision(TrainingGateOutcome Outcome, string? EvidenceReference, string? Reason);
