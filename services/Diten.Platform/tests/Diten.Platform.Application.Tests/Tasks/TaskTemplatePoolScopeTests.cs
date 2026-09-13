using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;
using static Diten.Platform.Application.Tests.Tasks.AssignmentWorld;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-PSS-MOD0024-TASK-SCOPE-SECURITY-01 · BL-353 — a task template's `DefaultPoolPositionId`, asked of the SAME
/// BL-057 pool guard <see cref="TaskAssignmentWriteGuardTests"/> already measures for
/// <see cref="CreateTaskItemHandler"/>/<see cref="ReassignTaskItemHandler"/>.
///
/// <para>Before this: the pool named on a template was checked for SHAPE only
/// (<see cref="TaskTemplateRules.ValidateAssignment"/> — target must not be Person). A template could be saved
/// with a position that is archived, in a dead unit, or simply outside the saver's scope — and then read as
/// "the template is broken" the moment somebody tried to generate a task from it, because
/// <see cref="CreateTaskItemFromTemplateHandler"/> routes through <see cref="CreateTaskItemHandler"/>, which
/// HAS asked this question since `01bc0915`.</para>
/// </summary>
public sealed class TaskTemplatePoolScopeTests
{
    private static CreateTaskTemplateRequest CreateRequest(Guid? pool) => new(
        Code: "TPL-SCOPE", Name: "Kapsam şablonu", TitleTemplate: null, DescriptionTemplate: null,
        DefaultPriority: TaskPriority.Medium,
        DefaultAssignmentTarget: pool is null ? TaskAssignmentTarget.SelfAssigned : TaskAssignmentTarget.PositionPool,
        DefaultPoolPositionId: pool, DefaultDueInDays: null, ChecklistTemplateId: null, LegalEntityId: null);

    private static UpdateTaskTemplateRequest UpdateRequest(string code, Guid? pool, int expectedVersion) => new(
        Code: code, Name: "Kapsam şablonu", TitleTemplate: null, DescriptionTemplate: null,
        DefaultPriority: TaskPriority.Medium,
        DefaultAssignmentTarget: pool is null ? TaskAssignmentTarget.SelfAssigned : TaskAssignmentTarget.PositionPool,
        DefaultPoolPositionId: pool, DefaultDueInDays: null, ChecklistTemplateId: null, LegalEntityId: null,
        IsActive: true, ExpectedVersion: expectedVersion);

    // ── create ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_with_an_IN_SCOPE_default_pool_is_accepted()
    {
        var (templates, guard) = World();

        var result = await new CreateTaskTemplateHandler(
                templates, new FakeChecklistTemplateRepository(), new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(Me), guard)
            .Handle(new CreateTaskTemplateCommand(CreateRequest(MyPosition), "corr"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(MyPosition, Assert.Single(templates.All).DefaultPoolPositionId);
    }

    [Fact]
    public async Task Create_with_an_OUT_OF_SCOPE_default_pool_is_refused()
    {
        /*
         * ⚠ THE DEFECT ITSELF: before this WP, this save succeeded. It answered "broken" only much later, from
         * CreateTaskItemFromTemplateHandler — a different message, for a caller who never touched the pool.
         */
        var (templates, guard) = World();

        var result = await new CreateTaskTemplateHandler(
                templates, new FakeChecklistTemplateRepository(), new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(Me), guard)
            .Handle(new CreateTaskTemplateCommand(CreateRequest(ForeignPeerPosition), "corr"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.PositionNotAssignable, result.ReasonCode);
        Assert.Empty(templates.All);
    }

    [Fact]
    public async Task Create_with_an_ARCHIVED_UNIT_default_pool_is_refused()
    {
        var (templates, guard) = World();

        var result = await new CreateTaskTemplateHandler(
                templates, new FakeChecklistTemplateRepository(), new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(Me), guard)
            .Handle(new CreateTaskTemplateCommand(CreateRequest(ArchivedUnitPosition), "corr"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.PositionNotAssignable, result.ReasonCode);
    }

    [Fact]
    public async Task Create_with_NO_default_pool_never_asks_the_guard()
    {
        var (templates, inner) = World();
        var recording = TaskAssignmentGuards.Recording(inner);

        var result = await new CreateTaskTemplateHandler(
                templates, new FakeChecklistTemplateRepository(), new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(Me), recording)
            .Handle(new CreateTaskTemplateCommand(CreateRequest(null), "corr"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.False(recording.WasConsulted);
    }

    // ── update ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_moving_the_default_pool_OUT_of_scope_is_refused()
    {
        var template = Template(MyPosition);
        var (templates, guard) = World(template);

        var result = await new UpdateTaskTemplateHandler(
                templates, new FakeChecklistTemplateRepository(), new FakeCurrentUserContext(Me), guard)
            .Handle(
                new UpdateTaskTemplateCommand(
                    template.Id, UpdateRequest(template.Code, ForeignPeerPosition, template.Version), "corr"),
                CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.PositionNotAssignable, result.ReasonCode);
        Assert.Equal(MyPosition, templates.All.Single().DefaultPoolPositionId); // untouched
    }

    [Fact]
    public async Task Update_moving_the_default_pool_to_an_IN_SCOPE_position_is_accepted()
    {
        var template = Template(MyPosition);
        var (templates, guard) = World(template);

        var result = await new UpdateTaskTemplateHandler(
                templates, new FakeChecklistTemplateRepository(), new FakeCurrentUserContext(Me), guard)
            .Handle(
                new UpdateTaskTemplateCommand(
                    template.Id, UpdateRequest(template.Code, ColleaguePosition, template.Version), "corr"),
                CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ColleaguePosition, templates.All.Single().DefaultPoolPositionId);
    }

    [Fact]
    public async Task Update_that_never_names_a_pool_never_asks_the_guard()
    {
        var template = Template(pool: null);
        var (templates, inner) = World(template);
        var recording = TaskAssignmentGuards.Recording(inner);

        var result = await new UpdateTaskTemplateHandler(
                templates, new FakeChecklistTemplateRepository(), new FakeCurrentUserContext(Me), recording)
            .Handle(
                new UpdateTaskTemplateCommand(
                    template.Id, UpdateRequest(template.Code, null, template.Version), "corr"),
                CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.False(recording.WasConsulted);
    }

    // ── the promise BL-353 exists for: a saved template can actually be opened ─────────────────────────────

    [Fact]
    public async Task A_template_that_SAVES_with_this_guard_also_OPENS_a_task_through_it()
    {
        // CreateTaskItemFromTemplateHandler delegates to CreateTaskItemCommand, which has asked this same
        // question since 01bc0915 — this proves the two no longer disagree for a template that passed save.
        var seats = SeatRepository();
        var positions = PositionRepository();
        var units = UnitRepository();
        var scopes = ScopeResolver(positions, units, Me);
        var guard = new TaskAssignmentGuard(
            seats, positions, units, scopes, TaskActors.PermitAll(), new FakeCurrentUserContext(Me));

        var refusal = await guard.CheckPoolAsync(MyPosition, CancellationToken.None);

        Assert.Null(refusal); // the same position BL-353's save above accepted is accepted again at open time
    }

    // ── harness ──────────────────────────────────────────────────────────────────────────────────────────────

    private static (FakeTaskTemplateRepository Templates, TaskAssignmentGuard Guard) World(params TaskTemplate[] seed)
    {
        var seats = SeatRepository();
        var positions = PositionRepository();
        var units = UnitRepository();
        var scopes = ScopeResolver(positions, units, Me);
        var guard = new TaskAssignmentGuard(
            seats, positions, units, scopes, TaskActors.PermitAll(), new FakeCurrentUserContext(Me));
        return (new FakeTaskTemplateRepository(seed), guard);
    }

    private static TaskTemplate Template(Guid? pool) => new()
    {
        TenantId = TaskTestData.Tenant,
        Code = "TPL-SEED",
        Name = "Tohum şablon",
        DefaultPriority = TaskPriority.Medium,
        DefaultAssignmentTarget = pool is null ? TaskAssignmentTarget.SelfAssigned : TaskAssignmentTarget.PositionPool,
        DefaultPoolPositionId = pool,
        IsActive = true
    };
}
