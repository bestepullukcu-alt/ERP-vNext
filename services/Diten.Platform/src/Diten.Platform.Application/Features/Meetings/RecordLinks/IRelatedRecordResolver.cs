namespace Diten.Platform.Application.Features.Meetings.RecordLinks;

/// <summary>
/// Turns the far end of a <c>RecordLink</c> into the title/link a card can show, for ONE module code. A
/// `RecordLink` never carries a title itself (it would go stale the moment the far record was renamed) — the
/// module that owns the record is asked, batched, every time.
/// </summary>
public interface IRelatedRecordResolver
{
    /// <summary>The <c>RecordLink.SourceModuleCode</c>/<c>TargetModuleCode</c> this resolver answers for.</summary>
    string ModuleCode { get; }

    /// <summary>
    /// Resolves every id in one call — never one call per related record, which is exactly the N+1 the
    /// `relatedRecords` projection would otherwise reintroduce. An id this resolver cannot find (deleted,
    /// cross-tenant, retired) is simply ABSENT from the result; the caller drops that link rather than
    /// rendering "a task" with no name behind it.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, RelatedRecordSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

/// <summary>
/// The registry every consumer of `relatedRecords` asks — MOD-0024's projection today, MOD-0357's own "linked
/// records" list later. A module code with NO registered resolver is not an error: the link is skipped and a
/// single warning is logged (pack §, "çözücüsü olmayan modül kodu... uydurma başlık yok") — never a card
/// titled with the raw module code and id.
/// </summary>
public interface IRelatedRecordResolverRegistry
{
    bool TryGet(string moduleCode, out IRelatedRecordResolver resolver);
}

/// <inheritdoc cref="IRelatedRecordResolverRegistry"/>
public sealed class RelatedRecordResolverRegistry : IRelatedRecordResolverRegistry
{
    private readonly IReadOnlyDictionary<string, IRelatedRecordResolver> _byModuleCode;

    public RelatedRecordResolverRegistry(IEnumerable<IRelatedRecordResolver> resolvers)
    {
        // Last registration wins on a code collision — deterministic, not a startup failure over a config
        // mistake a module pack review should catch instead. GroupBy+Last rather than a plain ToDictionary,
        // which would throw on the very collision this is meant to tolerate.
        _byModuleCode = resolvers
            .GroupBy(r => r.ModuleCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
    }

    public bool TryGet(string moduleCode, out IRelatedRecordResolver resolver)
        => _byModuleCode.TryGetValue(moduleCode, out resolver!);
}
