namespace Diten.CrmService.Application.Features.VisitReport.V2;

/// <summary>One competitor the report offers (MDM <c>lookup</c>: active records + the caller's own proposals) with the
/// country card of the rep's market.</summary>
public sealed record CompetitorOptionDto(
    Guid Id,
    string Name,
    string? Company,
    IReadOnlyList<string> ActiveIngredients,
    string? FormCode,
    string? Strength,
    string Status,
    CompetitorCountryDto? Country);

public sealed record CompetitorCountryDto(string? MarketCode, decimal? BoxPrice, string? CurrencyCode, string? ReimbursementStatusCode);

/// <summary><see cref="Available"/> false = MDM could not be read (the report goes on; the competitor answer is then not
/// required).</summary>
public sealed record CompetitorLookupResult(bool Available, IReadOnlyList<CompetitorOptionDto> Items)
{
    public static readonly CompetitorLookupResult Unavailable = new(false, Array.Empty<CompetitorOptionDto>());
}

/// <summary>A forwarded proposal: <see cref="Id"/> = the MDM <c>proposed</c> record; <see cref="Available"/> false = MDM
/// unreachable (kept on the report, retried by an amendment).</summary>
public sealed record CompetitorProposalResult(bool Available, Guid? Id, string? Error);

/// <summary>
/// WP-VW-W4 (W4-BE-a, §2.6) — the MDM competitor products, read through the Gateway with the CALLER's token (no service
/// identity): the report's short list (<c>GET api/mdm/competitor-products/lookup</c>), a rep's proposal
/// (<c>POST …/proposals</c>, name + company + product + report) and the survivor of a merged record (<c>GET …/{id}</c>,
/// <c>mergedIntoId</c>). Fail-open on reads (unavailable, never an exception); the report never blocks on MDM.
/// </summary>
public interface IMdmCompetitorClient
{
    Task<CompetitorLookupResult> LookupAsync(Guid productId, Guid? indicationNodeId, string? market, string? q, CancellationToken ct);

    Task<CompetitorProposalResult> ProposeAsync(string name, string company, Guid productId, Guid? reportId, CancellationToken ct);

    /// <summary>id → the record it counts as (itself, or the survivor when merged); null when MDM cannot be read.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>?> ResolveSurvivorsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>
    /// WP-VW-W4 (W4-FIX-d, G-9) — GSKU id → its global product id (MDM <c>api/gskus/{id}</c>, the rep's
    /// <c>mdm.gskus.read</c>); null when MDM cannot be read or a GSKU cannot be resolved (the report falls back, never blocks).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>?> GskuProductsAsync(IReadOnlyCollection<Guid> gskuIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, Guid>?>(null);
}

/// <summary>The default when no gateway client is wired (tests, non-HTTP hosts): MDM is "unavailable".</summary>
public sealed class UnavailableMdmCompetitorClient : IMdmCompetitorClient
{
    public Task<CompetitorLookupResult> LookupAsync(Guid productId, Guid? indicationNodeId, string? market, string? q, CancellationToken ct)
        => Task.FromResult(CompetitorLookupResult.Unavailable);

    public Task<CompetitorProposalResult> ProposeAsync(string name, string company, Guid productId, Guid? reportId, CancellationToken ct)
        => Task.FromResult(new CompetitorProposalResult(false, null, "mdm_unavailable"));

    public Task<IReadOnlyDictionary<Guid, Guid>?> ResolveSurvivorsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, Guid>?>(null);
}
