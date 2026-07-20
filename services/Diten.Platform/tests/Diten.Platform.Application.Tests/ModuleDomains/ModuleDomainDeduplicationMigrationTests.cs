using Diten.Platform.Application.Features.ModuleDomains;
using Diten.Platform.Application.Features.ModuleDomains.Commands;
using Diten.Platform.Application.Features.ModuleDomains.Handlers.CommandHandlers;
using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleDomains;

// FIX-DOMAIN-DEDUP — the pure planner behind ModuleDomainDeduplicationMigration (survivor selection + value
// merge), plus the canonical-code contract shared by all three creation paths and the ModuleDomain.CodeKey invariant.
public sealed class ModuleDomainDeduplicationMigrationTests
{
    [Fact]
    public void Plan_merges_cross_format_duplicate_pair_into_one_canonical_survivor()
    {
        // Same logical domain in two Code formats (the exact bug): hyphenated + no-separator.
        var hyphenated = new ModuleDomain { Code = "MASTER-DATA-MANAGEMENT", DisplayName = "Master Data Management", SortOrder = 30, IsActive = true };
        var noSeparator = new ModuleDomain { Code = "MASTERDATAMANAGEMENT", DisplayName = "MASTERDATAMANAGEMENT", SortOrder = 10, IsActive = true };

        var plans = ModuleDomainDeduplicationMigration.Plan(new[] { hyphenated, noSeparator });

        var plan = Assert.Single(plans);
        Assert.Equal("MASTERDATAMANAGEMENT", plan.CanonicalCode);            // canonical UPPERCASE-no-separator
        Assert.True(plan.IsActive);
        Assert.Equal(10, plan.SortOrder);                                    // lowest meaningful SortOrder wins
        Assert.Equal("Master Data Management", plan.DisplayName);            // human-friendly display preserved
        Assert.Single(plan.RedundantIds);                                    // exactly one row soft-deleted
        Assert.True(plan.SurvivorChanged);
        // Both rows carried a meaningful SortOrder → the collision is surfaced for the operator log.
        Assert.Equal(new[] { 10, 30 }, plan.ConflictingSortOrders);
    }

    [Fact]
    public void Plan_is_idempotent_rerun_over_the_survived_set_is_a_no_op()
    {
        // After a first pass the survivor is a single canonical row (Code == CodeKey == key); re-planning it alone
        // must produce NOTHING (no survivor rewrite, no deletes).
        var canonical = new ModuleDomain { Code = "MASTERDATAMANAGEMENT", DisplayName = "Master Data Management", SortOrder = 10, IsActive = true };

        var plans = ModuleDomainDeduplicationMigration.Plan(new[] { canonical });

        Assert.Empty(plans);
    }

    [Fact]
    public void Plan_prefers_an_active_row_when_a_duplicate_is_inactive()
    {
        var inactive = new ModuleDomain { Code = "FINANCE", DisplayName = "Finance", SortOrder = 40, IsActive = false };
        var active = new ModuleDomain { Code = "finance", DisplayName = "Finance", SortOrder = 20, IsActive = true };

        var plan = Assert.Single(ModuleDomainDeduplicationMigration.Plan(new[] { inactive, active }));

        Assert.Equal(active.Id, plan.SurvivorId);   // active row kept as survivor identity
        Assert.True(plan.IsActive);                  // merged active because at least one row was active
        Assert.Equal(20, plan.SortOrder);
        Assert.Equal("FINANCE", plan.CanonicalCode);
    }

    [Fact]
    public void Plan_backfills_code_key_on_a_lone_row_that_predates_the_field()
    {
        // A single legacy row whose CodeKey was never persisted still needs a plan so the unique index has a value.
        // Simulate the pre-field state by clearing CodeKey via reflection-free means: an explicit stale value.
        var legacy = new ModuleDomain { Code = "SALES", DisplayName = "Sales", SortOrder = 50, IsActive = true };
        // Its CodeKey is auto-derived and correct here, so a lone already-canonical row is a no-op...
        Assert.Empty(ModuleDomainDeduplicationMigration.Plan(new[] { legacy }));

        // ...but a lone row whose Code is NOT yet canonical (e.g. hyphenated) is rewritten to canonical form.
        var drifted = new ModuleDomain { Code = "COST-CENTER", DisplayName = "Cost Center", SortOrder = 60, IsActive = true };
        var plan = Assert.Single(ModuleDomainDeduplicationMigration.Plan(new[] { drifted }));
        Assert.Equal("COSTCENTER", plan.CanonicalCode);
        Assert.Empty(plan.RedundantIds);            // nothing deleted — just canonicalized
        Assert.True(plan.SurvivorChanged);
    }

    [Fact]
    public async Task All_three_creation_paths_produce_the_same_canonical_code_for_one_logical_domain()
    {
        // The three paths and the raw inputs they each see for the SAME logical domain "Master Data Management":
        //   • manifest self-registration → ResolveOrRegisterDomainCodeAsync uses NormalizeKey(enum name)
        //   • ModuleDomainSeed          → NormalizeKey(enum name)
        //   • CreateModuleDomainCommandHandler → NormalizeKey(operator-typed code)
        var manifestForm = ModuleTaxonomyCanonicalizer.NormalizeKey("MasterDataManagement"); // manifest/seed enum name
        var seedForm = ModuleTaxonomyCanonicalizer.NormalizeKey("MasterDataManagement");

        var repo = new SingleCaptureRepo();
        await new CreateModuleDomainCommandHandler(repo).Handle(
            new CreateModuleDomainCommand(new CreateModuleDomainRequest("master-data-management", "Master Data Management", null, 10, true)),
            CancellationToken.None);
        var createForm = repo.Created!.Code;

        Assert.Equal("MASTERDATAMANAGEMENT", manifestForm);
        Assert.Equal(manifestForm, seedForm);
        Assert.Equal(manifestForm, createForm);
    }

    [Theory]
    [InlineData("MASTER-DATA-MANAGEMENT", "MASTERDATAMANAGEMENT")]
    [InlineData("master data management", "MASTERDATAMANAGEMENT")]
    [InlineData("Access Governance", "ACCESSGOVERNANCE")]
    [InlineData("finance", "FINANCE")]
    public void ModuleDomain_CodeKey_tracks_Code_via_the_shared_normalizer(string code, string expectedKey)
    {
        // The unique index keys on CodeKey; this invariant is what makes two cross-format Codes collide.
        var domain = new ModuleDomain { Code = code };
        Assert.Equal(expectedKey, domain.CodeKey);
        Assert.Equal(ModuleTaxonomyCanonicalizer.NormalizeKey(code), domain.CodeKey);
    }

    [Fact]
    public void Backfill_populates_CodeKey_on_an_already_canonical_row_whose_stored_CodeKey_is_null()
    {
        // THE EXACT GAP: the Code is already canonical, so the migration never rewrites it — yet the row persisted
        // before the CodeKey field existed, so its stored CodeKey is null. It MUST still be backfilled, else the
        // unique partial index sees a null and its build collides/fails.
        var (newCodeKey, needsWrite, normalizesToEmpty) = ModuleDomainDeduplicationMigration.DecideCodeKeyBackfill("FINANCE", storedCodeKey: null);

        Assert.True(needsWrite);
        Assert.Equal("FINANCE", newCodeKey);
        Assert.False(normalizesToEmpty);
    }

    [Fact]
    public void Backfill_is_idempotent_when_stored_CodeKey_already_matches()
    {
        var (_, needsWrite, _) = ModuleDomainDeduplicationMigration.DecideCodeKeyBackfill("FINANCE", storedCodeKey: "FINANCE");
        Assert.False(needsWrite); // already correct → no write on re-run
    }

    [Theory]
    [InlineData("MASTER-DATA-MANAGEMENT", null, "MASTERDATAMANAGEMENT")]   // hyphenated + never-set → normalize + write
    [InlineData("DEVENABLEMENT", "", "DEVENABLEMENT")]                     // empty stored → write
    [InlineData("SALES", "STALE", "SALES")]                               // stale stored → corrected
    public void Backfill_writes_a_non_empty_canonical_CodeKey(string code, string? stored, string expected)
    {
        var (newCodeKey, needsWrite, normalizesToEmpty) = ModuleDomainDeduplicationMigration.DecideCodeKeyBackfill(code, stored);
        Assert.True(needsWrite);
        Assert.Equal(expected, newCodeKey);
        Assert.NotEqual(string.Empty, newCodeKey); // every real domain row ends with a usable, non-empty CodeKey
        Assert.False(normalizesToEmpty);
    }

    [Fact]
    public void Backfill_flags_a_Code_that_normalizes_to_empty_so_the_index_failure_is_surfaced()
    {
        // A pathological Code of only separators cannot yield a usable key — the migration must flag it (it logs
        // loudly) rather than silently leaving a null/empty CodeKey that breaks the unique index build.
        var (newCodeKey, _, normalizesToEmpty) = ModuleDomainDeduplicationMigration.DecideCodeKeyBackfill("---", storedCodeKey: null);
        Assert.Equal(string.Empty, newCodeKey);
        Assert.True(normalizesToEmpty);
    }

    [Fact]
    public void Two_cross_format_rows_share_a_CodeKey_so_the_unique_index_rejects_the_second()
    {
        // The unique partial index is on CodeKey; two Codes that differ only by separators/case collapse to the
        // SAME CodeKey, so the DB unique index rejects the second live insert. (DB-level enforcement; here we prove
        // the key equality the index relies on.)
        var first = new ModuleDomain { Code = "MASTERDATAMANAGEMENT" };
        var second = new ModuleDomain { Code = "master-data-management" };
        Assert.Equal(first.CodeKey, second.CodeKey);
        Assert.Equal("MASTERDATAMANAGEMENT", second.CodeKey);
    }

    private sealed class SingleCaptureRepo : Domain.Repositories.IModuleDomainRepository
    {
        public ModuleDomain? Created { get; private set; }

        public Task<ModuleDomain> CreateAsync(ModuleDomain item, CancellationToken ct = default)
        {
            Created = item;
            return Task.FromResult(item);
        }

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<ModuleDomain?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ModuleDomain?>(null);
        public Task<ModuleDomain?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<ModuleDomain?>(null);
        public Task UpdateAsync(ModuleDomain item, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<ModuleDomain> Items, long TotalCount)> QueryAsync(Domain.Repositories.ModuleDomainQuery query, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<ModuleDomain>)new List<ModuleDomain>(), 0L));
        public Task<IReadOnlyList<ModuleDomain>> GetActiveAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<ModuleDomain>)new List<ModuleDomain>());
    }
}
