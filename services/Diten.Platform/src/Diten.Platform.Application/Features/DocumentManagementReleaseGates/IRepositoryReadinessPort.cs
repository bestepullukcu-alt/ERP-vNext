using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates;

/// <summary>
/// MOD-0029-FU10 → FU16 seam. A PORT the FU10 release gate engine consults when computing Gate 2 (Approved repository /
/// validated DMS with an authorised release route, SOP §19 gate 2). FU10 owns the interface; FU16 (repository
/// assessment / DMS boundary) provides the adapter. Kept optional so the FU10 engine has no hard dependency on the
/// repository-assessment feature — a null port means "use the existing manual Gate 2 evidence behaviour" (backward
/// compatible).
/// </summary>
public interface IRepositoryReadinessPort
{
    Task<RepositoryGateDecision> EvaluateGate2Async(DocumentMasterRegisterEntry entry, CancellationToken ct);
}

/// <summary>What the repository-assessment feature tells the release gate engine to do for Gate 2.</summary>
public enum RepositoryGateOutcome
{
    /// <summary>An approved repository assessment supports the release gate — Gate 2 passes with the evidence reference.</summary>
    Pass = 0,

    /// <summary>An assessment exists but cannot support the gate (rejected / expired / unapproved / critical findings).</summary>
    Block = 1,

    /// <summary>No repository assessment governs this entry — the FU10 engine falls back to its manual Gate 2 evidence.</summary>
    FallBackToManual = 2
}

public sealed record RepositoryGateDecision(RepositoryGateOutcome Outcome, string? EvidenceReference, string? Reason);
