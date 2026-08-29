using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// ══ BL-016 · "AHMET'E ATADIĞIM GÖREVİ NEREDE GÖRÜRÜM?" ═══════════════════════════════════════════════════
///
/// <para>THE DEFECT, MEASURED. The Task Center provider ran exactly two reads: <c>ListByAssigneeAsync</c> (what
/// the actor holds) and <c>ListUnclaimedByPositionsAsync</c> (what their pools are offering). Work the actor
/// OPENED and handed to somebody else was in neither, so it was on no surface in the product at all. On the dev
/// tenant that was 21 live tasks — 13 sitting with a named colleague and 8 in pools — invisible to the person
/// who created every one of them.</para>
///
/// <para><b>The four guards below are the feature.</b> Each was broken deliberately before it was kept, because
/// a guard that has never failed is a sentence, not a test:</para>
/// <list type="number">
///   <item>work I opened and somebody else holds APPEARS in the Outbox;</item>
///   <item>work I opened and I hold does NOT — it is my own work, and it stays in İşlerim;</item>
///   <item>work somebody else opened does NOT, however it reaches my board;</item>
///   <item>the Outbox offers NO action that requires holding the work.</item>
/// </list>
///
/// <para>⚠ EVERY ABSENCE HERE IS ASSERTED AGAINST A PRESENCE. "Not in the Outbox" is trivially true of a
/// projection that returned nothing, and this repository has already shipped two guards that stayed green while
/// seeing almost nothing. So each negative case also asserts what the row IS — its tab-deciding fields, or the
/// holder actions the same task offers when the actor really does hold it.</para>
/// </summary>
public sealed class TaskOutboxTests
{
    private static readonly Guid PositionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Every act that presupposes holding the work. None of these may be OFFERED in the Outbox.</summary>
    private static readonly string[] HolderActs =
        ["accept", "claim", "start", "complete", "submitReview", "plan", "inquire", "release", "return"];

    // ── guard (a) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Work_I_opened_and_somebody_else_holds_is_ON_the_board_and_marked_initiator()
    {
        var task = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Me);

        var item = Assert.Single(await Project(task));

        // The whole defect in one assertion: before ListByCreatorAsync existed this projection was EMPTY.
        Assert.Equal(task.Id.ToString(), item.Id);
        Assert.Equal(WorkItemContract.ViewerRelationInitiator, item.ViewerRelation);
        // …and the row can name both people, which is what makes the tab answer the question it was asked.
        Assert.NotNull(item.Requester);
        Assert.True(item.Requester!.IsCurrentUser);
        Assert.NotNull(item.Assignee);
        Assert.False(item.Assignee!.IsCurrentUser);
    }

    [Fact]
    public async Task An_unclaimed_pool_task_I_opened_into_a_pool_I_am_NOT_in_is_initiator_work_not_pool_work()
    {
        /*
         * The case that decides whether `viewerRelation` had to exist at all. The shell can see
         * `requester.isCurrentUser` and it can see `admissionState === 'pendingClaim'` — this row has BOTH, and
         * they point at two different tabs. Only the provider knows that the pool read did not produce it, so
         * only the provider can say the row is not claimable. Without the field the row lands in Havuz under a
         * `claim` button that answers 403.
         */
        var task = PoolTaskFor(creator: TaskTestData.Me);

        // No PositionAssignment for Me → the actor is in no pool, so the pool read returns nothing.
        var item = Assert.Single(await Project(task));

        Assert.Equal("pendingClaim", item.AdmissionState);
        Assert.Equal(WorkItemContract.ViewerRelationInitiator, item.ViewerRelation);
        Assert.DoesNotContain(item.Actions, a => a.Code == "claim");
    }

    // ── guard (b) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Work_I_opened_and_I_HOLD_is_my_own_work_and_is_never_marked_initiator()
    {
        var task = TaskFor(holder: TaskTestData.Me, creator: TaskTestData.Me);

        var item = Assert.Single(await Project(task));

        // The MEANINGFUL baseline: the row is here, it is mine, and it carries the holder's actions. A guard
        // that only asserted "ViewerRelation is null" would pass just as happily over an empty board.
        Assert.Null(item.ViewerRelation);
        Assert.True(item.Assignee!.IsCurrentUser);
        Assert.Contains(item.Actions, a => a.Code == "start");
    }

    [Fact]
    public async Task Work_I_opened_into_a_pool_I_AM_in_stays_pool_work_because_claiming_is_an_act_I_can_press()
    {
        // Precedence, stated: claimable outranks opened-it. The action lives in Havuz, so the row lives in Havuz.
        var task = PoolTaskFor(creator: TaskTestData.Me);
        var provider = Provider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(Holder(TaskTestData.Me)));

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor(), CancellationToken.None));

        Assert.Null(item.ViewerRelation);
        Assert.Contains(item.Actions, a => a.Code == "claim" && a.Enabled);
    }

    // ── guard (c) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Work_SOMEBODY_ELSE_opened_is_never_initiator_work_even_when_it_is_on_my_board()
    {
        // On my board because I hold it — the only way another person's request reaches me. The point is that
        // being on the board is not what marks a row; being its author is.
        var task = TaskFor(holder: TaskTestData.Me, creator: TaskTestData.Rival);

        var item = Assert.Single(await Project(task));

        Assert.Null(item.ViewerRelation);
        Assert.False(item.Requester!.IsCurrentUser);
        // The baseline again: this row really is on the board with a holder's actions on it.
        Assert.Contains(item.Actions, a => a.Code == "start");
    }

    [Fact]
    public async Task Work_between_two_OTHER_people_reaches_no_read_at_all()
    {
        // The tenant is full of other people's work. None of the three reads asks for it, and the creator read
        // must not have widened that — "show me everything anyone started" is a separate, permission-gated
        // surface (SAP SWI1 / Oracle Administrative Tasks) and is deliberately not this one.
        var mine = TaskFor(holder: TaskTestData.Me, creator: TaskTestData.Me);
        var theirs = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Other);

        var items = await Project(mine, theirs);

        var item = Assert.Single(items);
        Assert.Equal(mine.Id.ToString(), item.Id);
    }

    // ── guard (d) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_Outbox_offers_NO_action_that_requires_holding_the_work()
    {
        var task = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Me);

        var item = Assert.Single(await Project(task));

        Assert.All(HolderActs, act =>
            Assert.DoesNotContain(item.Actions, a => a.Code == act));
    }

    [Fact]
    public async Task The_SAME_task_offers_those_acts_the_moment_the_actor_actually_holds_it()
    {
        /*
         * ⚠ THIS IS WHAT MAKES THE GUARD ABOVE MEAN SOMETHING. Identical task, identical actor, ONE field
         * changed — who holds it. If the withholding came from anywhere but the relationship (an empty board, a
         * lifecycle that offers nothing, a permission the actor lacks) this test would go red with it.
         *
         * Measured BEFORE the outbox branch existed: the guard's task came back offering `accept` as its PRIMARY
         * button — the creator was being invited to accept, on the creator's behalf, work assigned to somebody
         * else. The server refuses the write; the invitation was still a lie.
         */
        var task = TaskFor(holder: TaskTestData.Me, creator: TaskTestData.Me);
        task.AssignmentTarget = TaskAssignmentTarget.Person;

        var item = Assert.Single(await Project(task));

        Assert.Equal("accept", item.PrimaryActionCode);
        Assert.Contains(item.Actions, a => a.Code == "accept");
    }

    [Fact]
    public async Task The_Outbox_offers_the_REQUESTER_acts_and_leads_with_the_one_that_is_not_destructive()
    {
        /*
         * Withholding the holder's acts must not leave a row with nothing on it — an outbox that can only be
         * read is a report, not a work surface. Two acts survive, both already the requester's on the server:
         * `cancel` (TransitionTaskItemHandler answers a non-requester with 403 CANCEL_NOT_REQUESTER) and
         * `reassign` ("this is with the wrong person").
         *
         * `reassign` LEADS, and that is a rule rather than a preference: cancel is riskLevel destructive, and a
         * destructive act must never be a row's primary button.
         */
        var task = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Me);

        var item = Assert.Single(await Project(task));

        Assert.Equal(["reassign", "cancel"], item.Actions.Select(a => a.Code).ToArray());
        Assert.Equal("reassign", item.PrimaryActionCode);
        Assert.Equal(["cancel"], item.OverflowActionCodes!.ToArray());
        Assert.DoesNotContain(item.Actions, a => a.Code == item.PrimaryActionCode && a.RiskLevel == "destructive");
    }

    [Fact]
    public async Task Recall_is_NOT_offered_here_because_no_endpoint_answers_it()
    {
        // v1.5, and stated as a test so "we could just add a button" meets a red one first. An action with no
        // endpoint behind it is the mock-era failure this provider's own remarks refuse.
        var task = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Me);

        var item = Assert.Single(await Project(task));

        Assert.DoesNotContain(item.Actions, a => a.Code is "recall" or "withdraw");
    }

    // ── the read itself ───────────────────────────────────────────────────────

    [Fact]
    public async Task Finished_work_I_opened_is_not_dragged_onto_the_board_by_the_creator_read()
    {
        /*
         * The creator read excludes terminal work, exactly as the pool read does. The Outbox answers "what did I
         * start that is still out there"; a finished task the actor never held is not their History either —
         * that tab is what was once on their OWN board — so it belongs to neither and is not fetched.
         *
         * A deliberate boundary, not an oversight: "everything I ever started, closed included" is a reporting
         * question and is recorded rather than smuggled in here.
         */
        var done = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Me);
        done.Lifecycle = TaskLifecycle.Done;
        done.CompletedAt = DateTimeOffset.UtcNow;
        var cancelled = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Me);
        cancelled.Lifecycle = TaskLifecycle.Cancelled;
        cancelled.CancelledAt = DateTimeOffset.UtcNow;
        var live = TaskFor(holder: TaskTestData.Rival, creator: TaskTestData.Me);

        var items = await Project(done, cancelled, live);

        // Exactly one row, and it is the live one — not "some rows", which two closed tasks would also satisfy.
        var item = Assert.Single(items);
        Assert.Equal(live.Id.ToString(), item.Id);
    }

    [Fact]
    public async Task A_task_I_opened_reaches_the_board_ONCE_however_many_reads_would_claim_it()
    {
        // Held AND opened by the same person: two reads return it, one row must reach the surface. The tab bar
        // is an axis, and a row in two ownership tabs would make it a set of filters instead.
        var task = TaskFor(holder: TaskTestData.Me, creator: TaskTestData.Me);

        var items = await Project(task);

        Assert.Single(items);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<WorkItemProjectionDto>> Project(params TaskItem[] tasks)
        => await Provider(new FakeTaskItemRepository(tasks)).GetWorkItemsAsync(Actor(), CancellationToken.None);

    private static TaskWorkItemProvider Provider(
        FakeTaskItemRepository tasks,
        FakePositionAssignmentRepository? positionAssignments = null)
        => new(tasks,
            positionAssignments ?? new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(), new FakeTaskApprovalService(), new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(),
            new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

    private static WorkItemActor Actor() => new(
        TaskTestData.Me,
        IsPlatformActor: true,
        new HashSet<string>());

    private static TaskItem TaskFor(Guid holder, Guid creator) => new()
    {
        // Stated, because the default is the opposite and `reassign` is one of the two acts the Outbox keeps.
        DelegationAllowed = true,
        TenantId = TaskTestData.Tenant,
        Title = "Ahmet'e verilen iş",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = holder,
        CreatedByUserId = creator,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static TaskItem PoolTaskFor(Guid creator) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Havuza bırakılan iş",
        AssignmentTarget = TaskAssignmentTarget.PositionPool,
        PoolPositionId = PositionId,
        AssigneeUserId = null,
        CreatedByUserId = creator,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
    };
}
