using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Diten.Platform.Application.Tests.Tasks.AssignmentWorld;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-PSS-MOD0024-TASK-SCOPE-SECURITY-01 · BL-355 — an `OrganizationUnitId` the REQUEST names, asked of the same
/// BL-057 scope <see cref="TaskAssignmentWriteGuardTests"/> already measures for people and pools.
///
/// <para>Production <see cref="CreateTaskItemHandler"/> over the production
/// <see cref="TaskAssignmentGuard"/>/<see cref="TaskAssignmentScopeResolver"/>/<see cref="TaskAssigneeEligibility"/>
/// chain; only the stores are doubles. <see cref="AssignmentWorld"/> supplies the units: HOME (my own company),
/// FOREIGN (another company, ungranted), GRANTED (another company, explicitly granted to me), ARCHIVED (mine,
/// retired).</para>
/// </summary>
public sealed class TaskOrganizationUnitScopeTests
{
    // ── the SelfAssigned branch — isolates the unit question from the person/pool question entirely ────────

    [Theory]
    [InlineData(nameof(HomeUnit), true, null)]
    [InlineData(nameof(GrantedUnit), true, null)]
    [InlineData(nameof(ForeignUnit), false, TaskReasonCodes.OrganizationUnitOutOfScope)]
    public async Task Self_assigned_create_with_an_explicit_unit_asks_the_SAME_scope_rule(
        string unitName, bool accepted, string? reasonCode)
    {
        var unit = unitName switch
        {
            nameof(HomeUnit) => HomeUnit,
            nameof(GrantedUnit) => GrantedUnit,
            nameof(ForeignUnit) => ForeignUnit,
            _ => throw new ArgumentOutOfRangeException(nameof(unitName))
        };
        var run = new Run();

        var result = await run.CreateAsync(TaskAssignmentTarget.SelfAssigned, organizationUnitId: unit);

        if (accepted)
        {
            Assert.True(result.IsSuccessful);
            Assert.Equal(unit, Assert.Single(run.Tasks.Items).OrganizationUnitId);
        }
        else
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(403, result.StatusCode);
            Assert.Equal(reasonCode, result.ReasonCode);
            Assert.Empty(run.Tasks.Items);
        }
    }

    [Fact]
    public async Task An_ARCHIVED_unit_is_refused_with_the_code_every_caller_already_uses_for_it()
    {
        var run = new Run();

        var result = await run.CreateAsync(TaskAssignmentTarget.SelfAssigned, organizationUnitId: ArchivedUnit);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.OrganizationUnitUnresolved, result.ReasonCode);
    }

    [Fact]
    public async Task A_unit_id_that_does_not_exist_at_all_is_404_not_400()
    {
        var run = new Run();

        var result = await run.CreateAsync(TaskAssignmentTarget.SelfAssigned, organizationUnitId: Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(TaskReasonCodes.OrganizationUnitNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task Another_tenant_s_unit_answers_the_SAME_404_as_one_that_does_not_exist()
    {
        // The shared FakeOrganizationUnitRepository does not model tenant isolation (GetByIdAsync ignores
        // TenantId), so this test brings its own tenant-aware double rather than loosen shared test
        // infrastructure 50 other files depend on — the production repository (TenantRepository<T>) already
        // filters by tenant; this double exists only to PROVE that filtering is what this guard relies on.
        var foreignUnit = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Code = "OTHER-TENANT", Name = "Other tenant's unit",
            LegalEntityId = Guid.NewGuid(), Status = OrgUnitStatus.Active
        };
        var run = new Run(units: new TenantScopedUnitRepository(foreignUnit, TaskTestData.Tenant));

        var result = await run.CreateAsync(TaskAssignmentTarget.SelfAssigned, organizationUnitId: foreignUnit.Id);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(TaskReasonCodes.OrganizationUnitNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task No_unit_named_in_the_request__today_s_derivation_is_UNCHANGED()
    {
        // BL-355's own scope: the guard is asked ONLY when the request names a unit. Omitted, the existing
        // three-step fallback (position → root) runs exactly as it did, and lands on MY position's own unit.
        var run = new Run();

        var result = await run.CreateAsync(TaskAssignmentTarget.SelfAssigned, organizationUnitId: null);

        Assert.True(result.IsSuccessful);
        Assert.Equal(HomeUnit, Assert.Single(run.Tasks.Items).OrganizationUnitId);
    }

    // ── the pool branch — the request unit is checked independently of the pool position's OWN unit ────────

    [Fact]
    public async Task Pool_create_with_an_out_of_scope_explicit_unit_is_refused_even_though_the_pool_itself_is_fine()
    {
        var run = new Run();

        var result = await run.CreateAsync(TaskAssignmentTarget.PositionPool, pool: MyPosition, organizationUnitId: ForeignUnit);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(TaskReasonCodes.OrganizationUnitOutOfScope, result.ReasonCode);
    }

    [Fact]
    public async Task Pool_create_with_an_in_scope_explicit_unit_is_accepted()
    {
        var run = new Run();

        var result = await run.CreateAsync(TaskAssignmentTarget.PositionPool, pool: MyPosition, organizationUnitId: HomeUnit);

        Assert.True(result.IsSuccessful);
        Assert.Equal(HomeUnit, Assert.Single(run.Tasks.Items).OrganizationUnitId);
    }

    [Fact]
    public async Task Pool_create_with_NO_explicit_unit_still_inherits_the_position_s_own_unit()
    {
        var run = new Run();

        var result = await run.CreateAsync(TaskAssignmentTarget.PositionPool, pool: MyPosition, organizationUnitId: null);

        Assert.True(result.IsSuccessful);
        Assert.Equal(HomeUnit, Assert.Single(run.Tasks.Items).OrganizationUnitId);
    }

    // ── the recurrence sweep never asks (no caller) — same exemption CheckTargetAsync already gets ─────────

    [Fact]
    public async Task The_recurrence_sweep_is_NEVER_asked_even_with_an_out_of_scope_unit()
    {
        var run = new Run();

        var result = await run.CreateAsync(
            TaskAssignmentTarget.SelfAssigned, organizationUnitId: ForeignUnit, sweep: true);

        Assert.True(result.IsSuccessful, "the sweep must not be scope-checked; it has no caller");
    }

    // ── sabotage: removing the guard call turns every refusal above green ────────────────────────────────────

    [Fact]
    public async Task SABOTAGE_probe__the_guard_is_actually_consulted()
    {
        // Not a sabotage of production code (this WP does not sabotage-and-revert source, per Durma), but proof
        // the RECORDING double sees the call the refusal tests above rely on — the same shape
        // TaskAssignmentWriteGuardTests uses for the person/pool guard.
        var recording = TaskAssignmentGuards.Recording(
            AssignmentWorld.Guard(TaskActors.Holding(TaskPermissions.Create, TaskPermissions.Assign)));
        var run = new Run(guard: recording);

        await run.CreateAsync(TaskAssignmentTarget.SelfAssigned, organizationUnitId: HomeUnit);

        Assert.True(recording.WasConsulted);
    }

    // ── harness ──────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Run
    {
        private readonly CreateTaskItemHandler _handler;

        public Run(IOrganizationUnitRepository? units = null, ITaskAssignmentGuard? guard = null, Guid? actor = null)
        {
            var who = actor ?? Me;
            var seats = SeatRepository();
            var positions = PositionRepository();
            var unitRepo = units ?? UnitRepository();
            var scopes = new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(MyScopes()), positions, unitRepo,
                new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(who));

            _handler = new CreateTaskItemHandler(
                Tasks,
                new FakeTaskAssignmentRepository(),
                new FakeTaskWatcherRepository(),
                positions,
                unitRepo,
                seats,
                new TaskFieldDefinitionService(
                    new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                new TaskLifecycleService(),
                new FakeTaskApprovalService(),
                new FakeChecklistTemplateRepository(),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new FakeTaskNotificationService(),
                new FakeCurrentUserContext(who),
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<CreateTaskItemHandler>.Instance,
                TaskDocumentFreezerDoubles.OverAnEmptyRegister(),
                guard ?? new TaskAssignmentGuard(
                    seats, positions, unitRepo, scopes,
                    TaskActors.Holding(TaskPermissions.Create, TaskPermissions.Assign),
                    new FakeCurrentUserContext(who)),
                new TaskAssignmentDirection(scopes, seats, new FakeCurrentUserContext(who)),
                Upward);
        }

        public FakeTaskItemRepository Tasks { get; } = new();
        public RecordingUpwardRequests Upward { get; } = new();

        public Task<Response<Guid>> CreateAsync(
            TaskAssignmentTarget target, Guid? person = null, Guid? pool = null, Guid? organizationUnitId = null,
            bool sweep = false)
            => _handler.Handle(
                new CreateTaskItemCommand(
                    new CreateTaskItemRequest(
                        Title: "Kapsam denemesi (birim)",
                        Description: null,
                        Priority: TaskPriority.Medium,
                        AssignmentTarget: target,
                        AssigneeUserId: person,
                        PoolPositionId: pool,
                        OrganizationUnitId: organizationUnitId,
                        DueAt: null,
                        StartAt: null,
                        PlannedDate: null,
                        EstimateHours: null,
                        Tags: null,
                        ReviewRequired: false,
                        ApprovalRequired: false,
                        ApprovalManagerUserId: null,
                        EmailNotificationsEnabled: false,
                        DelegationAllowed: true,
                        FieldValues: null,
                        Watchers: null),
                    "corr",
                    IsScheduledGeneration: sweep),
                CancellationToken.None);
    }

    /// <summary>A single-unit repository that DOES filter by tenant — what the shared
    /// <c>FakeOrganizationUnitRepository</c> does not model. Exists only to prove the guard's 404 for a foreign
    /// unit rests on tenant scoping actually happening, the way the production repository guarantees it.</summary>
    private sealed class TenantScopedUnitRepository(OrganizationUnit unit, Guid tenantId) : IOrganizationUnitRepository
    {
        public Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(unit.Id == id && unit.TenantId == tenantId ? unit : null);

        public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OrganizationUnit>>(unit.TenantId == tenantId ? [unit] : []);

        public Task<OrganizationUnit> CreateAsync(OrganizationUnit u, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(OrganizationUnit u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
