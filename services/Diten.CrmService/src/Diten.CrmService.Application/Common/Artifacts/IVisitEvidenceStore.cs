namespace Diten.CrmService.Application.Common.Artifacts;

/// <summary>
/// WP-VW-W3 (BE-a, K-W3) — stores and reads a visit's evidence (start photo, doctor signature) in the MOD-0262-FU01
/// document repository, scope <c>CrmVisitEvidence</c>, as the SIGNED-IN user (their token is forwarded, so the Platform's
/// scoped keys decide: a rep writes / reads only their own evidence, a manager with read-all reads every one).
/// <para>⛔ <b>Fail-closed.</b> A store that cannot complete throws <see cref="ContentArtifactStoreException"/>; the
/// start / signature command then writes nothing. <see cref="CompensateAsync"/> is the undo of a stored binary whose
/// record could not be written (the Platform compensation endpoint — not a deletion feature).</para>
/// </summary>
public interface IVisitEvidenceStore
{
    Task<ContentArtifactStoreResult> StoreAsync(VisitEvidenceStoreRequest request, CancellationToken cancellationToken);

    /// <summary>The evidence bytes, or null when the repository answers 404 (absent, or not readable by this caller).</summary>
    Task<ContentArtifactReadResult?> OpenReadAsync(Guid contentId, CancellationToken cancellationToken);

    /// <summary>Removes a just-stored binary whose record write failed. Never throws: a failed compensation is logged by
    /// the implementation and leaves an orphan the repository's own sweep handles; the command's answer stays the
    /// original failure.</summary>
    Task CompensateAsync(Guid contentId, CancellationToken cancellationToken);
}

/// <summary>What to store: the planned visit and execution it belongs to (the repository's owning item / version), the
/// file name, media type and bytes. Tenant / company / scope come from the forwarded context, never from here.</summary>
public sealed record VisitEvidenceStoreRequest(
    Guid PlannedVisitId,
    Guid ExecutionId,
    string FileName,
    string MediaType,
    byte[] Content);
