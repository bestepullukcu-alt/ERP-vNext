using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 (blocker 1, "the same pattern in another task store: the same fix") — every versioned
/// task store replaces the WHOLE document, so each one must refuse an entity that was not read at the version its caller
/// names. Measured on a real MongoDB for all seven: two readers read the same document; the second writes; the first —
/// naming the version the document NOW carries, as a client one version ahead would — writes nothing.
/// <para>The task's comment, attachment, dependency and personal-overlay stores carry no version and are not part of
/// this (a comment and an overlay are replaced by id / by owner, an attachment is soft-deleted by a field update, an edge
/// is never updated).</para>
/// </summary>
[Trait("Category", "Mongo")]
public sealed class TaskStoreReadVersionMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private FakeTenantContext _tenant = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("task_store_read_version", SchemaProfile.WorkflowWorkCenter);
        _tenant = new FakeTenantContext(_harness.TenantId);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_task_store_writes_nothing_from_an_entity_read_before_the_newer_document()
    {
        var store = new TaskItemRepository(_harness.DbContext, _tenant, new TaskTransitionRepository(_harness.DbContext, _tenant));
        var created = await store.CreateAsync(new TaskItem
        {
            TenantId = _harness.TenantId, Title = "Önce", AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me, CreatedByUserId = TaskTestData.Me, OrganizationUnitId = Guid.NewGuid(), CreatedBy = "fix3"
        });

        await StaleReadWritesNothingAsync(
            () => store.GetByIdAsync(created.Id)!, (task, version) => store.UpdateAsync(task, version),
            (task, mark) => task.Title = mark, task => task.Title);
    }

    [Fact]
    public async Task The_checklist_store_writes_nothing_from_an_entity_read_before_the_newer_document()
    {
        var store = new ChecklistRunRepository(_harness.DbContext, _tenant);
        var created = await store.CreateAsync(new ChecklistRun { TenantId = _harness.TenantId, TaskItemId = Guid.NewGuid(), CreatedBy = "fix3" });

        await StaleReadWritesNothingAsync(
            () => store.GetByIdAsync(created.Id)!, (run, version) => store.UpdateAsync(run, version),
            (run, mark) => run.UpdatedBy = mark, run => run.UpdatedBy);
    }

    [Fact]
    public async Task The_type_store_writes_nothing_from_an_entity_read_before_the_newer_document()
    {
        var store = new TaskTypeRepository(_harness.DbContext, _tenant);
        var created = await store.CreateAsync(new TaskType { TenantId = _harness.TenantId, Code = "T-FIX3", Name = "Önce", CreatedBy = "fix3" });

        await StaleReadWritesNothingAsync(
            () => store.GetByIdAsync(created.Id)!, (type, version) => store.UpdateAsync(type, version),
            (type, mark) => type.Name = mark, type => type.Name);
    }

    [Fact]
    public async Task The_field_definition_store_writes_nothing_from_an_entity_read_before_the_newer_document()
    {
        var store = new TaskFieldDefinitionRepository(_harness.DbContext, _tenant);
        var created = await store.CreateAsync(new TaskFieldDefinition
        {
            TenantId = _harness.TenantId, Code = "F-FIX3", LabelText = "Önce", ValueType = TaskFieldValueType.Text, Section = "s",
            CreatedBy = "fix3"
        });

        await StaleReadWritesNothingAsync(
            () => store.GetByIdAsync(created.Id)!, (definition, version) => store.UpdateAsync(definition, version),
            (definition, mark) => definition.LabelText = mark, definition => definition.LabelText);
    }

    [Fact]
    public async Task The_checklist_template_store_writes_nothing_from_an_entity_read_before_the_newer_document()
    {
        var store = new ChecklistTemplateRepository(_harness.DbContext, _tenant);
        var created = await store.CreateAsync(new ChecklistTemplate { TenantId = _harness.TenantId, Code = "C-FIX3", Name = "Önce", CreatedBy = "fix3" });

        await StaleReadWritesNothingAsync(
            () => store.GetByIdAsync(created.Id)!, (template, version) => store.UpdateAsync(template, version),
            (template, mark) => template.Name = mark, template => template.Name);
    }

    [Fact]
    public async Task The_task_template_store_writes_nothing_from_an_entity_read_before_the_newer_document()
    {
        var store = new TaskTemplateRepository(_harness.DbContext, _tenant);
        var created = await store.CreateAsync(new TaskTemplate { TenantId = _harness.TenantId, Code = "TT-FIX3", Name = "Önce", CreatedBy = "fix3" });

        await StaleReadWritesNothingAsync(
            () => store.GetByIdAsync(created.Id)!, (template, version) => store.UpdateAsync(template, version),
            (template, mark) => template.Name = mark, template => template.Name);
    }

    [Fact]
    public async Task The_recurrence_rule_store_writes_nothing_from_an_entity_read_before_the_newer_document()
    {
        var store = new TaskRecurrenceRuleRepository(_harness.DbContext, _tenant);
        var created = await store.CreateAsync(new TaskRecurrenceRule { TenantId = _harness.TenantId, Name = "Önce", CreatedBy = "fix3" });

        await StaleReadWritesNothingAsync(
            () => store.GetByIdAsync(created.Id)!, (rule, version) => store.UpdateAsync(rule, version),
            (rule, mark) => rule.Name = mark, rule => rule.Name);
    }

    /// <summary>Two readers of one document; the second writes; the first, naming the version the document now carries,
    /// writes nothing — and the newer document is what stays.</summary>
    private static async Task StaleReadWritesNothingAsync<T>(
        Func<Task<T>> read, Func<T, int, Task<bool>> update, Action<T, string> mark, Func<T, string?> marker)
        where T : BaseEntity
    {
        var stale = await read();
        var newer = await read();

        mark(newer, "newer");
        Assert.True(await update(newer, newer.Version), "non-vacuity: the newer write lands");

        mark(stale, "stale");
        Assert.False(await update(stale, stale.Version + 1));
        Assert.Equal("newer", marker(await read()));
    }
}
