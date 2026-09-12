using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// In-memory <see cref="IRecordLinkRepository"/> — the same shape every other Fake*Repository in this suite
/// takes: tenant-filtered, soft-delete-aware, and it reproduces the ONE thing production behaviour this slice
/// depends on that a naive in-memory list would not: a duplicate insert of the same six values fails the same
/// way the real unique index does, so <c>FindOrCreateAsync</c>'s race-recovery path is exercised for real
/// rather than assumed.
/// </summary>
internal sealed class FakeRecordLinkRepository : IRecordLinkRepository
{
    private readonly List<RecordLink> _items = [];

    public FakeRecordLinkRepository(params RecordLink[] seed) => _items.AddRange(seed);

    /// <summary>Adds a row directly, bypassing the service — for a test that needs the link present BEFORE
    /// exercising the read side, without going through <c>AddLinkAsync</c>'s own idempotency check.</summary>
    public void Seed(RecordLink link) => _items.Add(link);

    // Mirrors every other Fake*Repository in this suite (e.g. FakePositionAssignmentRepository): the tenant
    // execution filter is TaskTestData.Tenant, hardcoded, exactly as the real TenantRepository<T> would apply
    // for a request made as TaskTestData.Me. A cross-tenant fixture is seeded directly with a different
    // TenantId and is invisible through every one of these methods, never through a dynamic tenant switch.
    public IReadOnlyList<RecordLink> Items
        => _items.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList();

    /// <summary>Set by a test to prove <c>FindOrCreateAsync</c> recovers from a genuine insert race — see
    /// <c>RecordLinkServiceTests.AddLink_recovers_when_a_concurrent_call_wins_the_race</c>.</summary>
    public bool SimulateConcurrentInsertOnNextCreate { get; set; }

    public Task<RecordLink> CreateAsync(RecordLink link, CancellationToken ct = default)
    {
        if (Duplicate(link) is not null)
        {
            // The real repository throws MongoWriteException/DuplicateKey here; the fake raises the same
            // shape of failure the production catch clause is written for, without depending on the driver.
            throw new InvalidOperationException("DUPLICATE_KEY (fake unique index)");
        }

        _items.Add(link);
        return Task.FromResult(link);
    }

    public Task<RecordLink?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(
            x => x.Id == id && x.TenantId == TaskTestData.Tenant && !x.IsDeleted));

    public Task<RecordLink?> FindAsync(
        string sourceModuleCode, Guid sourceRecordId,
        string targetModuleCode, Guid targetRecordId,
        string linkType, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x =>
            x.TenantId == TaskTestData.Tenant && !x.IsDeleted
            && x.SourceModuleCode == sourceModuleCode && x.SourceRecordId == sourceRecordId
            && x.TargetModuleCode == targetModuleCode && x.TargetRecordId == targetRecordId
            && x.LinkType == linkType));

    public Task<RecordLink> FindOrCreateAsync(RecordLink candidate, CancellationToken ct = default)
    {
        // Duplicate(candidate) — not the tenant-hardcoded FindAsync above — because the duplicate check for a
        // CREATE has to use the candidate's OWN tenant, exactly like the real repository's ExecutionFilter
        // would (it reads TenantContext.TenantId, not a fixed test constant).
        var existing = Duplicate(candidate);
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        return CreateOrRecoverAsync(candidate, ct);
    }

    private async Task<RecordLink> CreateOrRecoverAsync(RecordLink candidate, CancellationToken ct)
    {
        if (SimulateConcurrentInsertOnNextCreate)
        {
            // A second caller "wins" the race: their row lands in storage BETWEEN our duplicate check above
            // and our own insert attempt.
            SimulateConcurrentInsertOnNextCreate = false;
            var winner = new RecordLink
            {
                Id = Guid.NewGuid(),
                TenantId = candidate.TenantId,
                SourceModuleCode = candidate.SourceModuleCode,
                SourceRecordId = candidate.SourceRecordId,
                TargetModuleCode = candidate.TargetModuleCode,
                TargetRecordId = candidate.TargetRecordId,
                LinkType = candidate.LinkType,
                CreatedByUserId = Guid.NewGuid(),
                CreatedBy = "concurrent-caller"
            };
            _items.Add(winner);

            try
            {
                return await CreateAsync(candidate, ct);
            }
            catch (InvalidOperationException)
            {
                return winner;
            }
        }

        return await CreateAsync(candidate, ct);
    }

    public Task<IReadOnlyList<RecordLink>> ListBySourceAsync(
        IReadOnlyCollection<Guid> sourceRecordIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecordLink>>(
            _items.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted
                               && sourceRecordIds.Contains(x.SourceRecordId)).ToList());

    public Task<IReadOnlyList<RecordLink>> ListByTargetAsync(
        IReadOnlyCollection<Guid> targetRecordIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecordLink>>(
            _items.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted
                               && targetRecordIds.Contains(x.TargetRecordId)).ToList());

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is not null)
        {
            item.IsDeleted = true;
            item.DeletedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    private RecordLink? Duplicate(RecordLink link)
        => _items.FirstOrDefault(x =>
            !x.IsDeleted && x.TenantId == link.TenantId
            && x.SourceModuleCode == link.SourceModuleCode && x.SourceRecordId == link.SourceRecordId
            && x.TargetModuleCode == link.TargetModuleCode && x.TargetRecordId == link.TargetRecordId
            && x.LinkType == link.LinkType);
}

/// <summary>A resolver whose answers a test dictates directly — no repository behind it.</summary>
internal sealed class FakeRelatedRecordResolver : IRelatedRecordResolver
{
    public FakeRelatedRecordResolver(string moduleCode, Dictionary<Guid, RelatedRecordSummary> known)
    {
        ModuleCode = moduleCode;
        Known = known;
    }

    public string ModuleCode { get; }

    /// <summary>Mutable — a test may seed an answer AFTER construction (e.g. once it knows the id it seeded
    /// elsewhere), not only at construction time.</summary>
    public Dictionary<Guid, RelatedRecordSummary> Known { get; }

    /// <summary>Every id this resolver was actually asked for, across every call — the N+1 assertion reads
    /// this to prove ONE call carried every id, not one call per id.</summary>
    public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

    public Task<IReadOnlyDictionary<Guid, RelatedRecordSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        Calls.Add(ids);
        var result = ids
            .Where(Known.ContainsKey)
            .ToDictionary(id => id, id => Known[id]);
        return Task.FromResult<IReadOnlyDictionary<Guid, RelatedRecordSummary>>(result);
    }
}

internal sealed class FakeRelatedRecordResolverRegistry : IRelatedRecordResolverRegistry
{
    private readonly Dictionary<string, IRelatedRecordResolver> _byModuleCode;

    public FakeRelatedRecordResolverRegistry(params IRelatedRecordResolver[] resolvers)
        => _byModuleCode = resolvers.ToDictionary(r => r.ModuleCode, StringComparer.Ordinal);

    public bool TryGet(string moduleCode, out IRelatedRecordResolver resolver)
        => _byModuleCode.TryGetValue(moduleCode, out resolver!);
}
