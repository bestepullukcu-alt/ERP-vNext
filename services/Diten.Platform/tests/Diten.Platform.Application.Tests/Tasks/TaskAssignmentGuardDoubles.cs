using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Tests.Tasks;

internal static class TaskAssignmentGuards
{
    /// <summary>
    /// Admits every target. The honest way for a suite about something ELSE — notifications, checklists,
    /// approval — to say out loud that who may receive work is not what it measures, exactly as
    /// <see cref="TaskActors.PermitAll"/> does for field authorization. The real guard is measured by
    /// <c>TaskAssignmentWriteGuardTests</c>.
    /// </summary>
    internal static ITaskAssignmentGuard AdmitAll() => new Admitting();

    /// <summary>
    /// Throws if asked anything. For the recurrence SWEEP, which has no caller and must never consult the guard —
    /// a sweep that started asking would refuse every rule, and this makes that visible as a failure, not a skip.
    /// </summary>
    internal static ITaskAssignmentGuard NeverAsked() => new Refusing();

    /// <summary>
    /// Wraps another guard and remembers whether ANY Check…Async call reached it. BL-352 needs this: proving the
    /// recurrence UPDATE handler skips an unchanged target means proving the guard was never called, not merely
    /// that its answer was ignored — a double that only ever admits or only ever refuses cannot tell those apart.
    /// </summary>
    internal static RecordingGuard Recording(ITaskAssignmentGuard inner) => new(inner);

    /// <summary>
    /// The REAL guard over a small org, with the listed units granted to the actor and every permission held.
    /// For suites that already test eligibility (reassign) and must keep testing it against production code.
    /// </summary>
    internal static ITaskAssignmentGuard Over(
        FakePositionAssignmentRepository seats,
        FakePositionRepository positions,
        FakeOrganizationUnitRepository units,
        Guid actor,
        params Guid[] grantedUnitIds)
        => new TaskAssignmentGuard(
            seats,
            positions,
            units,
            new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(grantedUnitIds
                    .Select(id => new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, id, "granted"))
                    .ToArray()),
                positions,
                units,
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(actor)),
            TaskActors.PermitAll(),
            new FakeCurrentUserContext(actor));

    private sealed class Refusing : ITaskAssignmentGuard
    {
        private static InvalidOperationException Asked()
            => new("The recurrence sweep asked the assignment guard; it has no caller to ask about.");

        public Task<TaskAssignmentRefusal?> CheckPersonAsync(Guid userId, CancellationToken ct) => throw Asked();

        public Task<TaskAssignmentRefusal?> CheckPoolAsync(Guid positionId, CancellationToken ct) => throw Asked();

        public Task<TaskAssignmentRefusal?> CheckTargetAsync(
            TaskAssignmentTarget target, Guid? assigneeUserId, Guid? poolPositionId, CancellationToken ct)
            => throw Asked();

        public Task<TaskAssignmentRefusal?> CheckOrganizationUnitAsync(Guid organizationUnitId, CancellationToken ct)
            => throw Asked();
    }

    private sealed class Admitting : ITaskAssignmentGuard
    {
        public Task<TaskAssignmentRefusal?> CheckPersonAsync(Guid userId, CancellationToken ct)
            => Task.FromResult<TaskAssignmentRefusal?>(null);

        public Task<TaskAssignmentRefusal?> CheckPoolAsync(Guid positionId, CancellationToken ct)
            => Task.FromResult<TaskAssignmentRefusal?>(null);

        public Task<TaskAssignmentRefusal?> CheckTargetAsync(
            TaskAssignmentTarget target, Guid? assigneeUserId, Guid? poolPositionId, CancellationToken ct)
            => Task.FromResult<TaskAssignmentRefusal?>(null);

        public Task<TaskAssignmentRefusal?> CheckOrganizationUnitAsync(Guid organizationUnitId, CancellationToken ct)
            => Task.FromResult<TaskAssignmentRefusal?>(null);
    }

    /// <summary>Delegates to <paramref name="inner"/>'s real answer, but latches <see cref="WasConsulted"/> the
    /// moment any method is entered — so a test can assert non-consultation directly instead of inferring it from
    /// a status code.</summary>
    internal sealed class RecordingGuard(ITaskAssignmentGuard inner) : ITaskAssignmentGuard
    {
        internal bool WasConsulted { get; private set; }

        public Task<TaskAssignmentRefusal?> CheckPersonAsync(Guid userId, CancellationToken ct)
        {
            WasConsulted = true;
            return inner.CheckPersonAsync(userId, ct);
        }

        public Task<TaskAssignmentRefusal?> CheckPoolAsync(Guid positionId, CancellationToken ct)
        {
            WasConsulted = true;
            return inner.CheckPoolAsync(positionId, ct);
        }

        public Task<TaskAssignmentRefusal?> CheckTargetAsync(
            TaskAssignmentTarget target, Guid? assigneeUserId, Guid? poolPositionId, CancellationToken ct)
        {
            WasConsulted = true;
            return inner.CheckTargetAsync(target, assigneeUserId, poolPositionId, ct);
        }

        public Task<TaskAssignmentRefusal?> CheckOrganizationUnitAsync(Guid organizationUnitId, CancellationToken ct)
        {
            WasConsulted = true;
            return inner.CheckOrganizationUnitAsync(organizationUnitId, ct);
        }
    }
}

/// <summary>
/// The org every BL-057-at-the-write test is measured against. Two companies; each person exists to pass or fail
/// exactly ONE leg of the rule, so a guard that got one leg wrong cannot pass by coincidence.
///
/// <para>My chain: I report to a superior in MY company, who reports to a superior in ANOTHER company. A plant
/// manager in the other company reports to me. I am granted one foreign unit and one foreign position.</para>
/// </summary>
internal static class AssignmentWorld
{
    internal static readonly Guid HomeLegalEntity = Id(1);
    internal static readonly Guid ForeignLegalEntity = Id(2);

    internal static readonly Guid HomeUnit = Id(101);
    internal static readonly Guid ForeignUnit = Id(102);
    internal static readonly Guid GrantedUnit = Id(103);
    internal static readonly Guid ArchivedUnit = Id(104);

    internal static readonly Guid MyPosition = Id(201);
    internal static readonly Guid ColleaguePosition = Id(202);
    internal static readonly Guid ForeignReportPosition = Id(203);
    internal static readonly Guid GrantedUnitPosition = Id(204);
    internal static readonly Guid GrantedPosition = Id(205);
    internal static readonly Guid ForeignPeerPosition = Id(206);
    internal static readonly Guid HomeBossPosition = Id(207);
    internal static readonly Guid ForeignBossPosition = Id(208);
    internal static readonly Guid ArchivedUnitPosition = Id(209);
    internal static readonly Guid DraftPosition = Id(210);
    internal static readonly Guid UnknownPosition = Id(299);

    internal static readonly Guid Me = TaskTestData.Me;
    internal static readonly Guid Colleague = Id(301);              // same company
    internal static readonly Guid ForeignReport = Id(302);          // other company, reports up to me
    internal static readonly Guid GrantedUnitHolder = Id(303);      // other company, unit granted to me
    internal static readonly Guid GrantedPositionHolder = Id(304);  // other company, position granted to me
    internal static readonly Guid ForeignPeer = Id(305);            // other company, no chain, no grant
    internal static readonly Guid HomeBoss = Id(306);               // above me, same company
    internal static readonly Guid ForeignBoss = Id(307);            // above me, other company
    internal static readonly Guid NoPosition = Id(308);             // holds no seat at all
    internal static readonly Guid ArchivedUnitHolder = Id(309);     // seat in an archived unit
    internal static readonly Guid DraftHolder = Id(310);            // seat in a Draft position

    internal static IReadOnlyList<Guid> EveryPerson =>
    [
        Me, Colleague, ForeignReport, GrantedUnitHolder, GrantedPositionHolder, ForeignPeer, HomeBoss,
        ForeignBoss, NoPosition, ArchivedUnitHolder, DraftHolder
    ];

    internal static IReadOnlySet<Guid> AssignablePeople => new HashSet<Guid>
    {
        Me, Colleague, ForeignReport, GrantedUnitHolder, GrantedPositionHolder, HomeBoss
    };

    internal static IReadOnlyList<Guid> EveryPosition =>
    [
        MyPosition, ColleaguePosition, ForeignReportPosition, GrantedUnitPosition, GrantedPosition,
        ForeignPeerPosition, HomeBossPosition, ForeignBossPosition, ArchivedUnitPosition, DraftPosition,
        UnknownPosition
    ];

    internal static IReadOnlySet<Guid> AssignablePools => new HashSet<Guid>
    {
        MyPosition, ColleaguePosition, ForeignReportPosition, GrantedUnitPosition, GrantedPosition, HomeBossPosition
    };

    internal static OrganizationUnit[] Units() =>
    [
        Unit(HomeUnit, "HOME", HomeLegalEntity),
        Unit(ForeignUnit, "FOREIGN", ForeignLegalEntity),
        Unit(GrantedUnit, "GRANTED", ForeignLegalEntity),
        Unit(ArchivedUnit, "ARCHIVED", HomeLegalEntity, archived: true)
    ];

    internal static Position[] Positions() =>
    [
        Position(MyPosition, HomeUnit, reportsTo: HomeBossPosition),
        Position(ColleaguePosition, HomeUnit),
        Position(ForeignReportPosition, ForeignUnit, reportsTo: MyPosition),
        Position(GrantedUnitPosition, GrantedUnit),
        Position(GrantedPosition, ForeignUnit),
        Position(ForeignPeerPosition, ForeignUnit),
        Position(HomeBossPosition, HomeUnit, reportsTo: ForeignBossPosition),
        Position(ForeignBossPosition, ForeignUnit),
        Position(ArchivedUnitPosition, ArchivedUnit),
        Position(DraftPosition, HomeUnit, status: PositionStatus.Draft)
    ];

    internal static PositionAssignment[] Seats() =>
    [
        Seat(Me, MyPosition),
        Seat(Colleague, ColleaguePosition),
        Seat(ForeignReport, ForeignReportPosition),
        Seat(GrantedUnitHolder, GrantedUnitPosition),
        Seat(GrantedPositionHolder, GrantedPosition),
        Seat(ForeignPeer, ForeignPeerPosition),
        Seat(HomeBoss, HomeBossPosition),
        Seat(ForeignBoss, ForeignBossPosition),
        Seat(ArchivedUnitHolder, ArchivedUnitPosition),
        Seat(DraftHolder, DraftPosition)
    ];

    /// <summary>What MOD-0018-FU15 emits for me, including the two grants and the upward chain.</summary>
    internal static EntitlementDataScope[] MyScopes() =>
    [
        new(EntitlementDataScopeKind.Position, MyPosition, "ME"),
        new(EntitlementDataScopeKind.OrgUnit, HomeUnit, "HOME"),
        new(EntitlementDataScopeKind.LegalEntity, HomeLegalEntity, "HOME-LE"),
        new(EntitlementDataScopeKind.OrgUnit, GrantedUnit, "GRANTED-UNIT"),
        new(EntitlementDataScopeKind.Position, GrantedPosition, "GRANTED-POSITION"),
        // Upward. Present on purpose: the rule must not read it as assignability.
        new(EntitlementDataScopeKind.ManagerChain, HomeBossPosition, "HOME-BOSS"),
        new(EntitlementDataScopeKind.ManagerChain, ForeignBossPosition, "FOREIGN-BOSS")
    ];

    internal static FakePositionAssignmentRepository SeatRepository() => new(Seats());

    internal static FakePositionRepository PositionRepository() => new(Positions());

    internal static FakeOrganizationUnitRepository UnitRepository() => new(Units());

    internal static TaskAssignmentScopeResolver ScopeResolver(
        FakePositionRepository positions, FakeOrganizationUnitRepository units, Guid actor)
        => new(
            new FakeDataScopeResolver(MyScopes()),
            positions,
            units,
            new FakeTenantContext(TaskTestData.Tenant),
            new FakeCurrentUserContext(actor));

    /// <summary>The production guard over this world, asked by <paramref name="actor"/> (me by default).</summary>
    internal static TaskAssignmentGuard Guard(IActorPermissionContext permissions, Guid? actor = null)
    {
        var positions = PositionRepository();
        var units = UnitRepository();
        var who = actor ?? Me;
        return new TaskAssignmentGuard(
            SeatRepository(), positions, units, ScopeResolver(positions, units, who), permissions,
            new FakeCurrentUserContext(who));
    }

    private static Guid Id(int n) => Guid.Parse($"0000aaaa-0000-0000-0000-{n:D12}");

    private static OrganizationUnit Unit(Guid id, string code, Guid legalEntityId, bool archived = false) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = code,
        Name = code,
        LegalEntityId = legalEntityId,
        Status = OrgUnitStatus.Active,
        IsArchived = archived
    };

    private static Position Position(
        Guid id, Guid unitId, Guid? reportsTo = null, PositionStatus status = PositionStatus.Active) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = $"P-{id.ToString()[^3..]}",
        Name = $"Position {id.ToString()[^3..]}",
        OrganizationUnitId = unitId,
        ReportsToPositionId = reportsTo,
        Status = status
    };

    private static PositionAssignment Seat(Guid userId, Guid positionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
        EffectiveTo = null
    };
}
