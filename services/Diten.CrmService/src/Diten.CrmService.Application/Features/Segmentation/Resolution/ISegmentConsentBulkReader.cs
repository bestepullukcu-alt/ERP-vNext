namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 Phase-2 consent seam: ONE bulk read of the consent and preference rows for the whole candidate set
/// (two queries in total, never one per candidate). The rows are then fed to the MOD-0164 evaluation engine in memory.
/// <para>MOD-0164 is READ here and never mutated: no MOD-0164 file, entity or interface signature changes, and the
/// verdicts come from its own engine so consent semantics can never drift between the two modules.</para>
/// </summary>
public interface ISegmentConsentBulkReader
{
    Task<SegmentConsentSnapshot> LoadAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken);
}
