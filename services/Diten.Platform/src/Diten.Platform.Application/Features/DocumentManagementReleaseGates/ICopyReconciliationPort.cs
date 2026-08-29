using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates;

/// <summary>
/// MOD-0029-FU10 → FU17 seam. A PORT the FU10 release gate engine consults when computing Gate 6 (a method exists to
/// withdraw superseded copies from point of use, SOP §19 gate 6). FU10 owns the interface; FU17 (controlled/obsolete
/// copy reconciliation) provides the adapter. Kept optional so the FU10 engine has no hard dependency on the
/// controlled-copy feature — a null port means "use the existing manual Gate 6 evidence behaviour" (backward compatible).
/// </summary>
public interface ICopyReconciliationPort
{
    Task<CopyGateDecision> EvaluateGate6Async(DocumentMasterRegisterEntry entry, CancellationToken ct);
}

/// <summary>What the controlled-copy feature tells the release gate engine to do for Gate 6.</summary>
public enum CopyGateOutcome
{
    /// <summary>Copy withdrawal is under control (no obsolete active copies, withdrawal readiness satisfied) → Gate 6 passes.</summary>
    Pass = 0,

    /// <summary>Controlled-copy data exists but withdrawal is incomplete / an obsolete copy is in use → Gate 6 blocks.</summary>
    Block = 1,

    /// <summary>No controlled-copy data governs this entry — the FU10 engine falls back to its manual Gate 6 evidence.</summary>
    FallBackToManual = 2
}

public sealed record CopyGateDecision(CopyGateOutcome Outcome, string? EvidenceReference, string? Reason);
