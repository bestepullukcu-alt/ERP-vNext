using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

/// <summary>
/// MOD-0288-FU02 — the second reporting line: optional, self-rejecting, deliberately duplicable, cycle-free
/// per line AND across the combined graph, depth-bounded, and safe against two writers at once.
///
/// <para>⚠ EVERY TEST HERE CALLS THE PRODUCTION HANDLER OR THE PRODUCTION GUARD. None of them re-implements a
/// rule and then measures its own copy — that is how two defects passed 1,500 green tests in this codebase.</para>
/// </summary>
public sealed class OrganizationMatrixReportingTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-0000000000f2");
    private static readonly Guid LegalEntityId = Guid.Parse("10000000-0000-0000-0000-0000000000f2");

    // ── the line is OPTIONAL, and null means undefined ────────────────────────────────────────────────────

    [Fact]
    public async Task A_unit_may_keep_only_its_functional_parent()
    {
        var (repo, root) = await SeedRootAsync();

        var response = await Create(repo).Handle(
            new CreateOrganizationUnitCommand(Request("CHILD", parent: root.Id)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var child = Assert.Single((await repo.GetAllAsync()).Where(u => u.Code == "CHILD"));

        Assert.Equal(root.Id, child.ParentOrganizationUnitId);

        // ⚠ NULL, NOT root.Id. There is no fallback to the functional line: a defaulted second line would be
        // indistinguishable from a deliberate one, which destroys the distinction the feature exists for.
        Assert.Null(child.AdministrativeParentOrganizationUnitId);
        Assert.Null(TenantOrganizationMapper.ToDto(child).AdministrativeParentOrganizationUnitId);
    }

    [Fact]
    public async Task Both_lines_round_trip_through_create_and_the_dto_names_each_one()
    {
        var (repo, root) = await SeedRootAsync();
        var second = Unit("SECOND");
        repo.Add(second);

        var response = await Create(repo).Handle(
            new CreateOrganizationUnitCommand(Request("CHILD", parent: root.Id, administrative: second.Id)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var child = Assert.Single((await repo.GetAllAsync()).Where(u => u.Code == "CHILD"));
        Assert.Equal(root.Id, child.ParentOrganizationUnitId);
        Assert.Equal(second.Id, child.AdministrativeParentOrganizationUnitId);

        var dto = TenantOrganizationMapper.ToDto(child);
        Assert.Equal(root.Id, dto.ParentOrganizationUnitId);
        Assert.Equal(second.Id, dto.AdministrativeParentOrganizationUnitId);
    }

    [Fact]
    public async Task An_administrator_may_deliberately_point_both_lines_at_the_same_unit()
    {
        /*
         * ⚠ THIS IS A SUCCESS PATH, NOT A FAILURE ONE (§12, §13). One unit genuinely holding both
         * responsibilities for another is a real structure, and refusing it would refuse valid data. What is
         * forbidden is the SYSTEM producing the pair — see the test below, which pins that no code path
         * copies one line into the other.
         */
        var (repo, root) = await SeedRootAsync();

        var response = await Create(repo).Handle(
            new CreateOrganizationUnitCommand(Request("CHILD", parent: root.Id, administrative: root.Id)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var child = Assert.Single((await repo.GetAllAsync()).Where(u => u.Code == "CHILD"));
        Assert.Equal(root.Id, child.ParentOrganizationUnitId);
        Assert.Equal(root.Id, child.AdministrativeParentOrganizationUnitId);
    }

    [Fact]
    public async Task The_system_never_manufactures_the_duplicate_when_only_one_line_is_given()
    {
        // The complement of the test above: a functional parent alone leaves the administrative slot EMPTY.
        var (repo, root) = await SeedRootAsync();

        await Create(repo).Handle(
            new CreateOrganizationUnitCommand(Request("CHILD", parent: root.Id)), CancellationToken.None);

        var child = Assert.Single((await repo.GetAllAsync()).Where(u => u.Code == "CHILD"));
        Assert.NotEqual(child.ParentOrganizationUnitId, child.AdministrativeParentOrganizationUnitId);
    }

    // ── self, per-line and cross-line cycles ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_unit_cannot_be_its_own_administrative_parent()
    {
        var repo = new InMemoryOrganizationUnitRepository(TenantId);
        var unit = Unit("SELF");
        repo.Add(unit);

        var result = await OrganizationUnitCycleGuard.EnsureNoCycleAsync(
            repo, unit.Id, functionalParentId: null, administrativeParentId: unit.Id, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task A_cycle_inside_the_administrative_line_alone_is_rejected()
    {
        var repo = new InMemoryOrganizationUnitRepository(TenantId);
        var a = Unit("A");
        var b = Unit("B");
        b.AdministrativeParentOrganizationUnitId = a.Id;   // B --admin--> A
        repo.Add(a);
        repo.Add(b);

        // …and now A --admin--> B would close it.
        var result = await OrganizationUnitCycleGuard.EnsureNoCycleAsync(
            repo, a.Id, functionalParentId: null, administrativeParentId: b.Id, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task A_cross_line_cycle_is_rejected_even_though_each_line_alone_is_acyclic()
    {
        /*
         * ⚠ THE CASE THE PER-LINE RULE CANNOT SEE, and the reason the combined pass is a separate rule.
         * B --administrative--> A already exists. Adding A --functional--> B leaves BOTH lines acyclic when
         * walked alone; only the union closes.
         *
         * Rejected for AMBIGUITY, not traversal safety: approval walks neither line, so nothing would
         * overflow — the structure would simply have no statable meaning.
         */
        var repo = new InMemoryOrganizationUnitRepository(TenantId);
        var a = Unit("A");
        var b = Unit("B");
        repo.Add(a);
        repo.Add(b);

        /*
         * First, WITHOUT the administrative edge: A --functional--> B is plainly fine, and the functional line
         * is the only line in play. This half of the test is what makes the other half mean something — it
         * pins that the rejection below comes from the SECOND line, not from anything about the first.
         */
        var functionalLineAlone = await OrganizationUnitCycleGuard.EnsureNoCycleAsync(
            repo, a.Id, b.Id, CancellationToken.None);
        Assert.True(functionalLineAlone.IsSuccessful);

        // Now B --administrative--> A exists. Each line walked ALONE is still acyclic: the functional graph
        // holds no edges at all, and the administrative graph is the single edge B->A.
        b.AdministrativeParentOrganizationUnitId = a.Id;

        // The very same call is now refused, and only the combined graph can see why.
        var combined = await OrganizationUnitCycleGuard.EnsureNoCycleAsync(
            repo, a.Id, b.Id, CancellationToken.None);

        Assert.False(combined.IsSuccessful);
        Assert.Equal(409, combined.StatusCode);
        Assert.Equal(OrganizationUnitCycleGuard.CycleMessage, Assert.Single(combined.Errors));
    }

    // ── depth boundary ───────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public async Task Depth_is_bounded_at_thirty_two_ancestors(int ancestors, bool accepted)
    {
        var repo = new InMemoryOrganizationUnitRepository(TenantId);
        var chain = new List<OrganizationUnit>();
        for (var i = 0; i < ancestors; i++)
        {
            var node = Unit($"N{i}");
            if (i > 0)
            {
                node.ParentOrganizationUnitId = chain[i - 1].Id;
            }

            chain.Add(node);
            repo.Add(node);
        }

        var subject = Unit("SUBJECT");
        repo.Add(subject);

        var result = await OrganizationUnitCycleGuard.EnsureNoCycleAsync(
            repo, subject.Id, chain[^1].Id, CancellationToken.None);

        Assert.Equal(accepted, result.IsSuccessful);
        if (!accepted)
        {
            Assert.Equal(OrganizationUnitCycleGuard.DepthMessage, Assert.Single(result.Errors));
        }
    }

    [Fact]
    public async Task The_guard_reads_one_level_per_round_trip_rather_than_one_node_per_hop()
    {
        /*
         * ⚠ ASSERTS THE BATCHING, RATHER THAN TRUSTING THE COMMENT THAT CLAIMS IT. The old guard issued one
         * GetByIdAsync per hop, so depth 32 was 32 round trips and two lines would have doubled it. A level
         * here holds two nodes (one per line) and must still cost ONE read.
         */
        var repo = new InMemoryOrganizationUnitRepository(TenantId);
        var f = Unit("F");
        var a = Unit("A");
        var subject = Unit("SUBJECT");
        repo.Add(f);
        repo.Add(a);
        repo.Add(subject);

        var before = repo.BatchedReadCalls;
        var result = await OrganizationUnitCycleGuard.EnsureNoCycleAsync(
            repo, subject.Id, functionalParentId: f.Id, administrativeParentId: a.Id, CancellationToken.None);

        Assert.True(result.IsSuccessful);

        // Two per-line passes (1 read each) plus one combined pass (1 read for the level of two) = 3.
        Assert.Equal(3, repo.BatchedReadCalls - before);
    }

    // ── permission separation ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ordinary_update_may_rename_a_unit_but_not_re_hang_it()
    {
        var (repo, root) = await SeedRootAsync();
        var other = Unit("OTHER");
        repo.Add(other);
        var child = Unit("CHILD");
        child.ParentOrganizationUnitId = root.Id;
        repo.Add(child);

        var handler = Update(repo);

        // Renaming, with the lines round-tripped unchanged: allowed.
        var rename = await handler.Handle(
            new UpdateOrganizationUnitCommand(child.Id, Request("CHILD", "Renamed", root.Id)),
            CancellationToken.None);
        Assert.True(rename.IsSuccessful);

        // Moving it, without the reporting-line grant: refused.
        var move = await handler.Handle(
            new UpdateOrganizationUnitCommand(child.Id, Request("CHILD", "Renamed", other.Id)),
            CancellationToken.None);
        Assert.False(move.IsSuccessful);
        Assert.Equal(403, move.StatusCode);

        // The same move, through the reporting-line endpoint's command: allowed.
        var authorized = await ReportingLines(repo).Handle(
            new UpdateOrganizationUnitReportingLinesCommand(
                child.Id, new OrganizationUnitReportingLinesRequest(other.Id, null)),
            CancellationToken.None);
        Assert.True(authorized.IsSuccessful);
        Assert.Equal(other.Id, (await repo.GetByIdAsync(child.Id))!.ParentOrganizationUnitId);
    }

    [Fact]
    public async Task The_reporting_line_grant_cannot_rename_a_unit_or_move_its_legal_entity()
    {
        /*
         * ⚠ THE SEPARATION HAS TO CUT BOTH WAYS, AND THIS IS THE HALF THAT IS EASY TO FORGET. It is not enough
         * that `…update` cannot re-parent. If the reporting-line endpoint took the whole record, its holder
         * could rename the unit, move it to another legal entity or retire it — one permission, every field,
         * granted by the shape of a request rather than by anybody's decision.
         *
         * The command carries two ids and nothing else, so there is no path from this request to any other
         * property: everything else is read back from what is stored.
         */
        var (repo, root) = await SeedRootAsync();
        var other = Unit("OTHER");
        repo.Add(other);
        var child = Unit("CHILD");
        child.Name = "Original name";
        child.Description = "Original description";
        child.ParentOrganizationUnitId = root.Id;
        repo.Add(child);

        var response = await ReportingLines(repo).Handle(
            new UpdateOrganizationUnitReportingLinesCommand(
                child.Id, new OrganizationUnitReportingLinesRequest(other.Id, root.Id)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);

        var stored = (await repo.GetByIdAsync(child.Id))!;
        Assert.Equal(other.Id, stored.ParentOrganizationUnitId);
        Assert.Equal(root.Id, stored.AdministrativeParentOrganizationUnitId);

        // …and everything the grant does NOT cover is untouched.
        Assert.Equal("CHILD", stored.Code);
        Assert.Equal("Original name", stored.Name);
        Assert.Equal("Original description", stored.Description);
        Assert.Equal(LegalEntityId, stored.LegalEntityId);

        // The request type itself is the enforcement: it has nowhere to put anything else.
        Assert.Equal(
            new[] { "ParentOrganizationUnitId", "AdministrativeParentOrganizationUnitId" },
            typeof(OrganizationUnitReportingLinesRequest)
                .GetProperties()
                .Where(p => p.Name != "EqualityContract")
                .Select(p => p.Name));
    }

    // ── graph concurrency, in-process contract ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_reparenting_that_lost_the_structure_token_is_refused()
    {
        var (repo, root) = await SeedRootAsync();
        var other = Unit("OTHER");
        repo.Add(other);
        var child = Unit("CHILD");
        child.ParentOrganizationUnitId = root.Id;
        repo.Add(child);

        // Somebody else's re-parenting lands between our read and our write.
        repo.RaceOnNextRead = true;
        var handler = Update(repo);

        var response = await handler.Handle(
            new UpdateOrganizationUnitCommand(child.Id, Request("CHILD", parent: other.Id), AllowReportingLineChange: true),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    // ── graph concurrency, the REAL proof ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_concurrent_reparentings_cannot_both_land()
    {
        /*
         * ⚠ TWO GENUINELY CONCURRENT WRITERS AGAINST A REAL MONGO — not two sequential calls, and not the
         * in-memory fake. The defect this guards is only visible with real concurrency: writer 1 sets A's
         * parent to B while writer 2 sets B's parent to A, each validating against a graph that does not yet
         * contain the other's edge. Both are acyclic alone; together they are a cycle.
         *
         * A `lock` would not be a fix and this test would not detect its absence — the service runs as more
         * than one process, so an in-memory guard protects one instance while the other writes the other half.
         * What is proved here is the DATABASE-level contract: both writers read the same structure token, and
         * exactly one compare-and-set lands.
         */
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Organization);

        var repository = new OrganizationUnitRepository(harness.DbContext, harness.TenantContext);
        var handler = new UpdateOrganizationUnitCommandHandler(repository, repository, new AlwaysValidLegalEntity());
        var legalEntityId = Guid.NewGuid();

        /*
         * ⚠ REPEATED, AND NOT OUT OF SUPERSTITION. Two things make one round insufficient.
         *
         * First, two tasks released from a barrier may still serialize by luck, and a round that serialized
         * proves nothing about the guard — the second writer would simply see the first writer's edge and be
         * refused by the cycle check instead. Over several rounds at least one genuinely interleaves.
         *
         * Second, the guard has TWO code paths and one round only reaches one of them. The very first parent
         * mutation for a tenant finds no token document and races on the INSERT (`_id` is the compare-and-set);
         * every round after that races on the conditional `$inc`. A single round would leave the second path
         * completely unexercised — and it is the path every mutation in production takes.
         */
        var refusals = 0;

        for (var round = 0; round < 6; round++)
        {
            var a = new OrganizationUnit
            {
                TenantId = harness.TenantId, Code = $"RACE-A{round}", Name = "A", LegalEntityId = legalEntityId
            };
            var b = new OrganizationUnit
            {
                TenantId = harness.TenantId, Code = $"RACE-B{round}", Name = "B", LegalEntityId = legalEntityId
            };
            await repository.CreateAsync(a);
            await repository.CreateAsync(b);

            var gate = new Barrier(2);

            async Task<Response<NoContent>> ReparentAsync(OrganizationUnit child, OrganizationUnit parent)
            {
                gate.SignalAndWait();
                return await handler.Handle(
                    new UpdateOrganizationUnitCommand(
                        child.Id,
                        new OrganizationUnitRequest(child.Code, child.Name, legalEntityId, parent.Id),
                        AllowReportingLineChange: true),
                    CancellationToken.None);
            }

            /*
             * ⚠ DEDICATED THREADS, NOT THREAD-POOL WORK ITEMS. A Barrier BLOCKS the thread that reaches it, and
             * under the full suite the pool is already saturated by other test classes — so two Task.Run items
             * can deadlock waiting for each other while the pool grows one thread per second. LongRunning asks
             * for a thread of its own, which is what a test about genuine concurrency actually needs. Measured:
             * the suite hung on this before the change and finishes normally after it.
             */
            var results = await Task.WhenAll(
                Task.Factory.StartNew(() => ReparentAsync(a, b),
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap(),
                Task.Factory.StartNew(() => ReparentAsync(b, a),
                    CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap());

            var landed = results.Count(r => r.IsSuccessful);
            refusals += results.Count(r => !r.IsSuccessful);

            Assert.True(landed <= 1, $"round {round}: both re-parentings landed — the graph is now cyclic");
            Assert.All(results.Where(r => !r.IsSuccessful), r => Assert.Equal(409, r.StatusCode));

            // The decisive check: the stored graph never holds both edges.
            var storedA = await repository.GetByIdAsync(a.Id);
            var storedB = await repository.GetByIdAsync(b.Id);
            Assert.False(
                storedA!.ParentOrganizationUnitId == b.Id && storedB!.ParentOrganizationUnitId == a.Id,
                $"round {round}: both edges are stored — the writers were not serialized");
        }

        /*
         * ⚠ AND THE TEST MUST NOT BE ABLE TO PASS BY DOING NOTHING. If every round had serialized cleanly, or
         * if both writers had somehow been refused for an unrelated reason, the assertions above would still
         * hold while proving nothing. At least one writer has to have been TOLD it lost.
         */
        Assert.True(refusals > 0, "no writer was ever refused — the race never actually happened");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<(InMemoryOrganizationUnitRepository Repo, OrganizationUnit Root)> SeedRootAsync()
    {
        var repo = new InMemoryOrganizationUnitRepository(TenantId);
        var root = Unit("ROOT");
        repo.Add(root);
        await Task.CompletedTask;
        return (repo, root);
    }

    private static OrganizationUnit Unit(string code) =>
        new() { TenantId = TenantId, Code = code, Name = code, LegalEntityId = LegalEntityId };

    private static OrganizationUnitRequest Request(
        string code,
        string? name = null,
        Guid? parent = null,
        Guid? administrative = null) =>
        new(code, name ?? code, LegalEntityId, parent, AdministrativeParentOrganizationUnitId: administrative);

    private static CreateOrganizationUnitCommandHandler Create(InMemoryOrganizationUnitRepository repo)
    {
        var context = new TenantContext();
        context.SetTenant(TenantId);
        return new CreateOrganizationUnitCommandHandler(repo, repo, new AlwaysValidLegalEntity(), context);
    }

    private static UpdateOrganizationUnitCommandHandler Update(InMemoryOrganizationUnitRepository repo) =>
        new(repo, repo, new AlwaysValidLegalEntity());

    private static UpdateOrganizationUnitReportingLinesCommandHandler ReportingLines(
        InMemoryOrganizationUnitRepository repo) =>
        new(repo, repo, new AlwaysValidLegalEntity());

    private sealed class AlwaysValidLegalEntity : ILegalEntityReferenceValidator
    {
        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default) =>
            Task.FromResult(Response<LegalEntityReferenceDto>.Success(
                new LegalEntityReferenceDto(legalEntityId, "Legal", "Legal", "ACTIVE", true)));
    }
}
