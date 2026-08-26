using System.Reflection;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// "WHO SITS IN WHICH SEAT" — one surface, and the tests that keep it one.
///
/// <para><b>Measured before this change.</b> Nine files under Features/Tasks injected
/// <see cref="IPositionAssignmentRepository"/>, and every one of them called <c>GetAllAsync</c> — nobody asked a
/// narrow question, everybody pulled the whole table and filtered it in memory. The active-window rule
/// (<c>EffectiveFrom &lt;= now &amp;&amp; (EffectiveTo is null || EffectiveTo &gt; now)</c>, not cancelled) was
/// hand-written TEN times, while its canonical form already existed in
/// <c>TenantOrganizationMapper.IsActiveNow</c> with zero readers on the Tasks side.</para>
///
/// <para><b>Why it matters now.</b> BL-071 moves seat ownership to HCM, where the same fact is
/// <c>EmploymentRecord.StartDate/EndDate</c> as <c>DateOnly</c> rather than <c>DateTimeOffset</c>. Ten sites
/// would each have to change, and a missed one does not crash — it quietly keeps answering from stale seat data,
/// so somebody who has left keeps receiving work. The type difference is not innocent either: a half-open
/// interval over <c>DateOnly</c> and over <c>DateTimeOffset</c> disagree at the day boundary.</para>
///
/// <para><b>These tests are the deliverable.</b> The refactor itself is invisible; without the three structural
/// assertions below the next round adds the eleventh copy and nothing says so.</para>
/// </summary>
public sealed class TaskSeatDirectoryTests
{
    private static readonly Guid Position = Guid.Parse("31111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherPosition = Guid.Parse("32222222-2222-2222-2222-222222222222");

    private static string TasksRoot() => Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(typeof(TaskSeatDirectoryTests).Assembly.Location)!,
        "..", "..", "..", "..", "..", "src", "Diten.Platform.Application", "Features", "Tasks"));

    private static IReadOnlyList<string> TasksSources() =>
        Directory.GetFiles(TasksRoot(), "*.cs", SearchOption.AllDirectories);

    // ── the three structural claims ──────────────────────────────────────────

    [Fact]
    public void Exactly_ONE_file_under_Tasks_touches_the_assignment_repository()
    {
        /*
         * The whole point. Nine files reached for the repository directly; a tenth would have arrived with no
         * friction at all. This is the test that gives the next round friction.
         */
        var touching = TasksSources()
            .Where(file => File.ReadAllText(file).Contains("IPositionAssignmentRepository"))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        Assert.True(touching.Count == 1,
            $"expected exactly one seam onto the assignment repository, found {touching.Count}: "
            + string.Join(", ", touching));
        Assert.Equal("TaskSeatDirectory.cs", touching[0]);
    }

    [Fact]
    public void The_active_window_rule_is_written_in_exactly_ONE_place()
    {
        /*
         * `EffectiveFrom` is the tell: any file that mentions it is deciding for itself what "currently holds
         * this seat" means. Ten files did. When HCM takes ownership the column is gone and the semantics change
         * at the day boundary, so every mention is a site that has to be found and re-reasoned.
         */
        var mentioning = TasksSources()
            .Where(file => File.ReadAllText(file).Contains("EffectiveFrom"))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        Assert.True(mentioning.Count == 1,
            $"the active-window rule is written in {mentioning.Count} places: " + string.Join(", ", mentioning));
        Assert.Equal("TaskSeatDirectory.cs", mentioning[0]);
    }

    [Fact]
    public void Every_former_caller_now_depends_on_the_DIRECTORY_and_not_on_the_repository()
    {
        /*
         * The grep tests prove nothing reaches PAST the seam; this proves everything reaches THROUGH it. A
         * consumer that quietly stopped asking about seats altogether would pass the greps and fail here.
         */
        Type[] consumers =
        [
            typeof(TaskWorkItemProvider),
            typeof(CreateTaskItemHandler),
            typeof(ClaimTaskItemHandler),
            typeof(GetTaskItemListHandler),
            typeof(GetTaskAssignmentPersonLookupHandler),
            typeof(GetTaskAssignmentPositionLookupHandler),
            typeof(TaskNotificationService),
            typeof(TaskTeamResolver),
            typeof(TaskAssignmentDirection)
        ];

        foreach (var consumer in consumers)
        {
            var parameters = consumer.GetConstructors().Single().GetParameters();

            Assert.True(parameters.Any(p => p.ParameterType == typeof(ITaskSeatDirectory)),
                $"{consumer.Name} does not take the seat directory");
            Assert.False(parameters.Any(p => p.ParameterType == typeof(IPositionAssignmentRepository)),
                $"{consumer.Name} still reaches the assignment repository directly");
        }
    }

    // ── the rule the surface now owns ────────────────────────────────────────

    [Fact]
    public async Task A_cancelled_a_future_and_an_ended_assignment_are_all_inactive()
    {
        // Exactly the three exclusions the ten copies each spelled out, now asserted once.
        var now = DateTimeOffset.UtcNow;
        var cancelled = Seat(TaskTestData.Me, Position); cancelled.IsCancelled = true;
        var future = Seat(TaskTestData.Rival, Position); future.EffectiveFrom = now.AddDays(7);
        var ended = Seat(TaskTestData.Other, Position); ended.EffectiveTo = now.AddDays(-1);

        var directory = Seats(cancelled, future, ended);

        Assert.Empty(await directory.ActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_assignment_whose_end_is_exactly_now_is_already_over()
    {
        /*
         * The half-open boundary, pinned. It is the one detail that will read differently once HCM's DateOnly
         * replaces DateTimeOffset, so it is stated as behaviour rather than left implicit in ten predicates.
         */
        var now = DateTimeOffset.UtcNow;
        var ending = Seat(TaskTestData.Me, Position);
        ending.EffectiveTo = now;

        var directory = Seats(ending);

        Assert.Empty(await directory.ActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task It_answers_the_questions_the_call_sites_actually_ask()
    {
        /*
         * The interface was DERIVED from the nine call sites rather than invented: four distinct questions came
         * out of the measurement, and each is asserted here against the same fixture.
         */
        var mine = Seat(TaskTestData.Me, Position);
        var theirs = Seat(TaskTestData.Rival, OtherPosition);
        var directory = Seats(mine, theirs);
        var ct = CancellationToken.None;

        // (A) which seats does this user hold?
        Assert.Equal([Position], await directory.PositionIdsForUserAsync(TaskTestData.Me, ct));

        // (B) does this user hold any of these seats?
        Assert.True(await directory.HoldsAnyAsync(TaskTestData.Me, new HashSet<Guid> { Position }, ct));
        Assert.False(await directory.HoldsAnyAsync(TaskTestData.Me, new HashSet<Guid> { OtherPosition }, ct));

        // (C) who holds these seats?
        Assert.Equal([TaskTestData.Rival],
            await directory.HoldersOfAsync(new HashSet<Guid> { OtherPosition }, ct));

        // (D) every live seat, for the pickers and the eligibility rule.
        Assert.Equal(2, (await directory.ActiveAsync(ct)).Count);
    }

    [Fact]
    public async Task Everyone_who_has_EVER_held_a_seat_is_answerable_separately()
    {
        /*
         * BL-072's exclusion count needs the candidates the ACTIVE list drops — otherwise "nobody holds a
         * position" and "their assignment ended" are the same silent empty answer. It is a different question
         * from the other four and gets its own member rather than an inactive-rows leak.
         */
        var ended = Seat(TaskTestData.Rival, Position);
        ended.EffectiveTo = DateTimeOffset.UtcNow.AddDays(-1);

        var directory = Seats(Seat(TaskTestData.Me, Position), ended);

        var everyone = await directory.EverAssignedUserIdsAsync(CancellationToken.None);
        Assert.Contains(TaskTestData.Me, everyone);
        Assert.Contains(TaskTestData.Rival, everyone);
    }

    [Fact]
    public void The_surface_uses_MOD_0288s_canonical_rule_rather_than_a_copy_of_it()
    {
        /*
         * DECISION: consume `TenantOrganizationMapper.IsActiveNow`, do not absorb it.
         *
         * It is MOD-0288's own contract for MOD-0288's own entity, already read by that module and by
         * PositionAssignmentDto's derived status. Copying it into Tasks would produce the second copy this
         * round exists to delete — relocated rather than removed. Its semantics were compared line by line with
         * the ten hand-written predicates and are identical (cancelled ⇒ Ended, EffectiveFrom > now ⇒ Planned,
         * EffectiveTo <= now ⇒ Ended).
         */
        var source = File.ReadAllText(Path.Combine(TasksRoot(), "Services", "TaskSeatDirectory.cs"));

        Assert.Contains("IsActiveNow", source);
        Assert.DoesNotContain("AssignmentDerivedStatus.Active ==", source);   // no re-derivation
    }

    // ── the seam is LIVE, not merely present ─────────────────────────────────

    [Fact]
    public async Task Swapping_the_directory_changes_what_a_consumer_sees()
    {
        /*
         * A consumer could take ITaskSeatDirectory in its constructor and still answer from somewhere else. This
         * feeds two of them from a directory that knows about exactly one seat and checks the answer moves.
         */
        var team = new TaskTeamResolver(
            new StubScope(subordinates: [OtherPosition]),
            Seats(Seat(TaskTestData.Rival, OtherPosition)));

        var resolved = await team.ResolveTeamAsync(CancellationToken.None);
        Assert.Equal([TaskTestData.Rival], resolved.UserIds);

        // Same resolver, a directory that knows of no seat at all: the answer follows the directory.
        var empty = new TaskTeamResolver(new StubScope(subordinates: [OtherPosition]), Seats());
        Assert.Empty((await empty.ResolveTeamAsync(CancellationToken.None)).UserIds);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static ITaskSeatDirectory Seats(params PositionAssignment[] seats)
        => new TaskSeatDirectory(new FakePositionAssignmentRepository(seats));

    private static PositionAssignment Seat(Guid userId, Guid positionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
        EffectiveTo = null
    };

    /// <summary>
    /// Hands back a fixed subordinate set so the team resolver's OTHER input is not under test here. Built
    /// through the REAL scope resolver rather than by constructing TaskAssignmentScope directly — its
    /// constructor is internal on purpose, and reaching around that would be a second way to make a scope.
    /// </summary>
    private sealed class StubScope(Guid[] subordinates) : ITaskAssignmentScopeResolver
    {
        public async Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct)
        {
            // A position I hold, plus one that reports to it: the descent then yields exactly `subordinates`.
            var mine = Guid.Parse("3f000000-0000-0000-0000-00000000000f");
            var positions = new List<Position>
            {
                new() { Id = mine, TenantId = TaskTestData.Tenant, Code = "MINE", Name = "Mine",
                        OrganizationUnitId = Guid.NewGuid(), Status = PositionStatus.Active }
            };
            positions.AddRange(subordinates.Select(id => new Position
            {
                Id = id, TenantId = TaskTestData.Tenant, Code = "SUB", Name = "Sub",
                OrganizationUnitId = Guid.NewGuid(), ReportsToPositionId = mine, Status = PositionStatus.Active
            }));

            var resolver = new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(
                    new Diten.Platform.Common.Authorization.EntitlementDataScope(
                        Diten.Platform.Common.Authorization.EntitlementDataScopeKind.Position, mine, "MINE")),
                new FakePositionRepository([.. positions]),
                new FakeOrganizationUnitRepository(),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(TaskTestData.Me));

            return await resolver.ResolveAsync(ct);
        }
    }
}
