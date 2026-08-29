namespace Diten.CrmService.Application.Features.CyclePeriod.Read;

/// <summary>
/// MOD-0165 FU07 — the NARROW, read-only window onto MOD-0151 Territory that the business-unit picker needs, and
/// nothing else.
/// <para><b>Why a seam instead of the Territory repository.</b> <c>ITerritoryModelRepository</c> carries
/// <c>InsertAsync</c> and <c>UpdateAsync</c>. Handing that to a CyclePeriod handler would put a write path into
/// another module's aggregate one keystroke away, and no code review reliably catches that twice a year. This
/// interface cannot write, so the boundary is structural rather than a promise.</para>
/// <para><b>Candidates are a NARROWING, not a gate.</b> What comes back narrows the picker; it never decides whether a
/// write is allowed. That rule is MOD-0048 <c>business-unit</c> vocabulary validation, in the handler. Otherwise a
/// period's identity would be pinned to Territory's lifecycle: superseding a plan would make an existing period
/// uneditable, and a period could not be planned before its field plan existed — which is backwards, because in real
/// life the calendar comes first.</para>
/// </summary>
public interface ITerritoryBusinessUnitCatalog
{
    /// <summary>
    /// The distinct business-unit codes covered by the tenant's ACTIVE territory plans for this country whose
    /// effective window overlaps the period being authored. An empty list is a legitimate answer — it means "no plan
    /// covers this" and the UI must say so rather than showing an empty dropdown with no explanation.
    /// </summary>
    /// <param name="country">ISO alpha-2, or null for "any country".</param>
    /// <param name="startDate">First day of the period being authored (inclusive).</param>
    /// <param name="endDate">Last day of the period being authored (inclusive).</param>
    Task<IReadOnlyList<TerritoryBusinessUnitCandidate>> GetCandidatesAsync(
        string? country,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken);
}

/// <summary>
/// One candidate business unit and the plans it came from. <see cref="SourceModelCodes"/> exists so the UI can answer
/// the author's next question — "why is this the list?" — without a second round trip.
/// </summary>
public sealed record TerritoryBusinessUnitCandidate(
    string BusinessUnitCode,
    IReadOnlyList<string> SourceModelCodes);
