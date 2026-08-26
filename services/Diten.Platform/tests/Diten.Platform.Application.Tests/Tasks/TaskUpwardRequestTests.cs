using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-023 PART B — work that flows UPWARD is a REQUEST, not an order.
///
/// <para>Today a subordinate can assign a task to their own manager and it lands in the manager's list looking
/// exactly like an instruction. Organisationally that is backwards: SAP and Oracle both model upward work as a
/// request/approval rather than an assignment, because the person receiving it has to be able to say no.</para>
///
/// <para><b>No new chain walk.</b> BL-057's resolver already emits <c>ManagerChain</c> — the positions ABOVE the
/// actor. That was the "wrong" direction for assignability and had to be inverted there; it is the RIGHT
/// direction here, so this reads the scope as-is. Deriving a second upward chain from the same field would put
/// two truths on one column.</para>
///
/// <para><b>MOD-0024 decides nothing (Binding A).</b> The request is handed to MOD-0023 through the same path
/// approval and review already use — a third object type beside <c>task</c> and <c>task-review</c>, which the
/// engine supports without modification (ObjectType is free text with no allow-list). Accept/reject happens
/// there.</para>
/// </summary>
public sealed class TaskUpwardRequestTests
{
    private static readonly Guid HomeUnit = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("0a000000-0000-0000-0000-00000000000a");

    private static readonly Guid MyPosition = Guid.Parse("31111111-1111-1111-1111-111111111111");
    private static readonly Guid BossPosition = Guid.Parse("32222222-2222-2222-2222-222222222222");
    private static readonly Guid PeerPosition = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ReportPosition = Guid.Parse("34444444-4444-4444-4444-444444444444");
    private static readonly Guid UnrelatedPosition = Guid.Parse("35555555-5555-5555-5555-555555555555");

    private static readonly Guid Boss = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    private static readonly Guid Peer = Guid.Parse("c0000000-0000-0000-0000-00000000000c");
    private static readonly Guid Report = Guid.Parse("d0000000-0000-0000-0000-00000000000d");
    private static readonly Guid Unrelated = Guid.Parse("e0000000-0000-0000-0000-00000000000e");

    // ── direction detection ──────────────────────────────────────────────────

    [Fact]
    public async Task Assigning_DOWNWARD_stays_a_plain_assignment()
    {
        // Today's behaviour, pinned: a manager giving work to their report is an order and must stay one.
        Assert.False(await IsUpward(Report));
    }

    [Fact]
    public async Task Assigning_SIDEWAYS_stays_a_plain_assignment()
    {
        Assert.False(await IsUpward(Peer));
    }

    [Fact]
    public async Task Assigning_UPWARD_is_a_request()
    {
        Assert.True(await IsUpward(Boss));
    }

    [Fact]
    public async Task Somebody_with_no_chain_to_me_is_NOT_upward()
    {
        /*
         * "Unrelated" is the case that separates a real rule from a lazy one: a person who is neither above nor
         * below me must not be treated as a superior just because they are not a subordinate. Absence of a
         * chain is not evidence of one.
         */
        Assert.False(await IsUpward(Unrelated));
    }

    [Fact]
    public async Task Assigning_to_MYSELF_is_never_upward()
    {
        Assert.False(await IsUpward(TaskTestData.Me));
    }

    [Fact]
    public async Task The_direction_reads_the_EXISTING_ManagerChain_scope_rather_than_walking_again()
    {
        /*
         * If the detector stopped reading ManagerChain it would have to derive the ascent itself — a second
         * truth about Position.ReportsToPositionId. Removing the scope must therefore change the answer.
         */
        var withoutChain = await IsUpward(Boss, scopes:
        [
            new EntitlementDataScope(EntitlementDataScopeKind.Position, MyPosition, "ME"),
            new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, LegalEntity, "LE")
        ]);

        Assert.False(withoutChain);
    }

    // ── the handoff ──────────────────────────────────────────────────────────

    [Fact]
    public void The_request_is_a_THIRD_workflow_object_type_beside_approval_and_review()
    {
        /*
         * Not a reuse of "task": the approval gate keys off that object type, and sharing it would make an
         * upward request read as an approval decision on the same task — the exact corruption TaskReviewService
         * documents for its own separation.
         */
        Assert.Equal("task-request", TaskUpwardRequestService.RequestObjectType);
        Assert.NotEqual(TaskApprovalService.ApprovalObjectType, TaskUpwardRequestService.RequestObjectType);
        Assert.NotEqual(TaskReviewService.ReviewObjectType, TaskUpwardRequestService.RequestObjectType);
    }

    [Fact]
    public void The_object_reference_keeps_its_instance_history_disjoint_from_the_other_two()
    {
        var taskId = Guid.NewGuid();

        Assert.Equal($"tasks|task-request|{taskId}", TaskUpwardRequestService.BuildObjectRef(taskId));
        Assert.NotEqual(TaskApprovalService.BuildObjectRef(taskId), TaskUpwardRequestService.BuildObjectRef(taskId));
        Assert.NotEqual(TaskReviewService.BuildObjectRef(taskId), TaskUpwardRequestService.BuildObjectRef(taskId));
    }

    [Fact]
    public void MOD_0024_never_decides_the_request_itself()
    {
        /*
         * Binding A, asserted against the SOURCE because it is an architectural rule rather than a behaviour: a
         * local accept/reject branch is the mistake this project keeps re-making. The service may START a
         * workflow and READ its state; it may not resolve one.
         */
        var source = System.IO.File.ReadAllText(ServiceSourcePath());

        foreach (var forbidden in new[] { "Approved", "Rejected", "ApprovalTaskStatus.Approved" })
        {
            Assert.DoesNotContain($"= {forbidden}", source);
        }

        // It starts an instance and nothing more — the decision commands belong to MOD-0023's own callers.
        Assert.Contains("StartWorkflowInstanceCommand", source);
        Assert.DoesNotContain("DecideWorkflowTaskCommand", source);
        Assert.DoesNotContain("ApproveWorkflowTaskCommand", source);
    }

    [Fact]
    public void The_request_is_idempotent_per_task_so_a_retry_cannot_open_a_second_one()
    {
        var source = System.IO.File.ReadAllText(ServiceSourcePath());
        Assert.Contains("IdempotencyKey", source);
        Assert.Contains("task-request:", source);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string ServiceSourcePath() => System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(typeof(TaskUpwardRequestTests).Assembly.Location)!,
        "..", "..", "..", "..", "..", "src", "Diten.Platform.Application",
        "Features", "Tasks", "Services", "TaskUpwardRequestService.cs");

    private static async Task<bool> IsUpward(Guid targetUserId, EntitlementDataScope[]? scopes = null)
    {
        var positions = new[]
        {
            Position(MyPosition, "Me", reportsTo: BossPosition),
            Position(BossPosition, "Boss"),
            Position(PeerPosition, "Peer"),
            Position(ReportPosition, "Report", reportsTo: MyPosition),
            Position(UnrelatedPosition, "Unrelated")
        };
        var units = new[] { Unit(HomeUnit) };
        var assignments = new[]
        {
            Holder(TaskTestData.Me, MyPosition),
            Holder(Boss, BossPosition),
            Holder(Peer, PeerPosition),
            Holder(Report, ReportPosition),
            Holder(Unrelated, UnrelatedPosition)
        };

        var detector = new TaskAssignmentDirection(
            new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(scopes ?? MyScopes()),
                new FakePositionRepository(positions),
                new FakeOrganizationUnitRepository(units),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(TaskTestData.Me)),
            new FakePositionAssignmentRepository(assignments),
            new FakeCurrentUserContext(TaskTestData.Me));

        return await detector.IsUpwardAsync(targetUserId, CancellationToken.None);
    }

    /// <summary>What MOD-0018-FU15 emits for me — including the UPWARD ManagerChain this rule reads.</summary>
    private static EntitlementDataScope[] MyScopes() =>
    [
        new(EntitlementDataScopeKind.Position, MyPosition, "ME"),
        new(EntitlementDataScopeKind.OrgUnit, HomeUnit, "FAC-A"),
        new(EntitlementDataScopeKind.LegalEntity, LegalEntity, "LE"),
        new(EntitlementDataScopeKind.ManagerChain, BossPosition, "BOSS")
    ];

    private static PositionAssignment Holder(Guid userId, Guid positionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
    };

    private static Position Position(Guid id, string name, Guid? reportsTo = null) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = name.ToUpperInvariant(),
        Name = name,
        OrganizationUnitId = HomeUnit,
        ReportsToPositionId = reportsTo,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit Unit(Guid id) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = "FAC-A",
        Name = "Facility A",
        LegalEntityId = LegalEntity,
        Status = OrgUnitStatus.Active
    };
}
