using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 5 — the configurable values reach the surface.
///
/// <para><b>The defect this closes, measured.</b> <c>ResolveCapabilities</c> declared <c>businessContext</c>
/// whenever a task had field values, and nothing ever produced the container. The contract couples the two
/// (CAPABILITY_CONTAINER_REQUIRED), and <c>validateItems</c> DROPS what it cannot validate — so a task carrying
/// configurable values did not merely render them badly, it disappeared from the surface entirely while the API
/// kept returning its values. Half a capability is worse than none.</para>
/// </summary>
public sealed class TaskBusinessContextProjectionTests
{
    // ── The container exists, and matches the definitions ────────────────────

    [Fact]
    public async Task A_task_with_values_carries_its_business_context()
    {
        var item = await ProjectAsync(
            TaskWith(("regulatory.phase", "Faz 2")),
            TenantDefinition("regulatory.phase", "Mevzuat Aşaması", section: "Uyum"));

        Assert.NotNull(item.BusinessContext);
        var section = Assert.Single(item.BusinessContext!.Sections);
        Assert.Equal("Uyum", section.Title.Text);
        var field = Assert.Single(section.Fields);
        Assert.Equal("Faz 2", field.Value);
    }

    [Fact]
    public async Task Values_are_grouped_into_the_sections_their_definitions_declare()
    {
        var item = await ProjectAsync(
            TaskWith(("a", "1"), ("b", "2"), ("c", "3")),
            TenantDefinition("a", "Alan A", section: "Uyum", sortOrder: 1),
            TenantDefinition("b", "Alan B", section: "Finans", sortOrder: 2),
            TenantDefinition("c", "Alan C", section: "Uyum", sortOrder: 3));

        Assert.Equal(2, item.BusinessContext!.Sections.Count);
        var compliance = item.BusinessContext.Sections.Single(s => s.Title.Text == "Uyum");
        Assert.Equal(2, compliance.Fields.Count);
        // Ordered by the definition's SortOrder, not by however the values happened to be stored.
        Assert.Equal("Alan A", compliance.Fields[0].Label.Text);
        Assert.Equal("Alan C", compliance.Fields[1].Label.Text);
    }

    [Fact]
    public async Task The_value_TYPE_crosses_the_wire_in_the_CONTRACTS_spelling()
    {
        /*
         * The contract's VALUE_TYPES are lowercase and the engine's enum is PascalCase; the two vocabularies were
         * declared to match value-for-value on purpose. Shipping "DateTime" where the contract says "datetime"
         * is the enum-as-wire-format defect this module has already paid for twice.
         */
        var task = TaskWith(("when", "2026-08-01T09:00:00Z"));
        task.FieldValues[0].ValueType = TaskFieldValueType.DateTime;

        var item = await ProjectAsync(task, TenantDefinition("when", "Ne zaman", section: "Plan"));

        Assert.Equal("datetime", item.BusinessContext!.Sections[0].Fields[0].ValueType);
    }

    // ── The label source split, carried through ──────────────────────────────

    [Fact]
    public async Task A_TENANT_definition_becomes_a_DISPLAY_label()
    {
        // The administrator's own words. A resource label here would render the key itself, because no resx of
        // ours has a line for a string a tenant typed.
        var item = await ProjectAsync(
            TaskWith(("regulatory.phase", "Faz 2")),
            TenantDefinition("regulatory.phase", "Mevzuat Aşaması", section: "Uyum"));

        var label = item.BusinessContext!.Sections[0].Fields[0].Label;
        Assert.Equal(WorkItemContract.LabelDisplay, label.Kind);
        Assert.Equal("Mevzuat Aşaması", label.Text);
        Assert.Null(label.Key);
    }

    [Fact]
    public async Task A_SYSTEM_definition_becomes_a_RESOURCE_label()
    {
        var item = await ProjectAsync(
            TaskWith(("regulatory.phase", "Faz 2")),
            SystemDefinition("regulatory.phase", "Tasks_Field_RegulatoryPhase", section: "Uyum"));

        var label = item.BusinessContext!.Sections[0].Fields[0].Label;
        Assert.Equal(WorkItemContract.LabelResource, label.Kind);
        Assert.Equal("Tasks_Field_RegulatoryPhase", label.Key);
        Assert.Null(label.Text);
    }

    [Fact]
    public async Task No_field_label_is_ever_the_raw_CODE()
    {
        /*
         * The defect the two-source split exists to prevent, asserted across every path at once: tenant, system,
         * and a value whose definition cannot be read at all.
         */
        var item = await ProjectAsync(
            TaskWith(("tenant.one", "a"), ("system.one", "b"), ("orphan.one", "c")),
            TenantDefinition("tenant.one", "Kiracı alanı", section: "S1"),
            SystemDefinition("system.one", "Tasks_Field_System", section: "S1"));

        var labels = item.BusinessContext!.Sections.SelectMany(s => s.Fields).Select(f => f.Label).ToList();

        Assert.Equal(3, labels.Count);
        Assert.DoesNotContain(labels, l => l.Text == "tenant.one" || l.Key == "tenant.one");
        Assert.DoesNotContain(labels, l => l.Text == "orphan.one" || l.Key == "orphan.one");
        Assert.DoesNotContain(labels, l => l.Text == "system.one" || l.Key == "system.one");
    }

    // ── The retired / unreadable definition decision ─────────────────────────

    [Fact]
    public async Task A_RETIRED_definitions_value_still_renders_with_its_own_label()
    {
        /*
         * THE decision. Retiring is not deletion precisely so the values keep their explanation — the field
         * catalogue reads retired definitions too. Dropping the value would delete from the screen what the API
         * still returns; printing its code would put `regulatory.phase` where a heading belongs.
         */
        var retired = TenantDefinition("regulatory.phase", "Mevzuat Aşaması", section: "Uyum");
        retired.IsActive = false;
        retired.DeletedAt = DateTimeOffset.UtcNow;

        var item = await ProjectAsync(TaskWith(("regulatory.phase", "Faz 2")), retired);

        var field = Assert.Single(item.BusinessContext!.Sections.SelectMany(s => s.Fields));
        Assert.Equal("Mevzuat Aşaması", field.Label.Text);
        Assert.Equal("Faz 2", field.Value);
    }

    [Fact]
    public async Task A_value_whose_definition_is_GONE_keeps_its_value_under_a_stated_label()
    {
        // Neither exit is taken: the value survives (it exists, and the API returns it) and it is NOT labelled
        // with its own code. A stated "withdrawn field" resource key is the third way.
        var item = await ProjectAsync(TaskWith(("vanished.code", "Deger")));

        var field = Assert.Single(item.BusinessContext!.Sections.SelectMany(s => s.Fields));
        Assert.Equal("Deger", field.Value);
        Assert.Equal(WorkItemContract.LabelResource, field.Label.Kind);
        Assert.Equal("WorkAggregation_BusinessContext_UnknownField", field.Label.Key);
    }

    [Fact]
    public async Task Unfiled_values_go_LAST_under_a_stated_heading()
    {
        var item = await ProjectAsync(
            TaskWith(("known", "1"), ("vanished", "2")),
            TenantDefinition("known", "Bilinen", section: "Uyum"));

        Assert.Equal(2, item.BusinessContext!.Sections.Count);
        Assert.Equal("Uyum", item.BusinessContext.Sections[0].Title.Text);
        Assert.Equal(
            "WorkAggregation_BusinessContext_Unfiled",
            item.BusinessContext.Sections[1].Title.Key);
    }

    // ── Capability and container are ONE decision ────────────────────────────

    [Fact]
    public async Task No_values_means_NEITHER_the_capability_nor_the_container()
    {
        /*
         * "Half of it, never." The contract enforces this in both directions, and getting it wrong in either is
         * an item that vanishes: a container without its capability is CAPABILITY_REQUIRED_FOR_DATA, a
         * capability without its container is CAPABILITY_CONTAINER_REQUIRED, and validateItems drops both.
         */
        var item = await ProjectAsync(TaskWith());

        Assert.Null(item.BusinessContext);
        Assert.DoesNotContain("businessContext", item.WorkItemCapabilities);
    }

    [Fact]
    public async Task Values_mean_BOTH()
    {
        var item = await ProjectAsync(
            TaskWith(("regulatory.phase", "Faz 2")),
            TenantDefinition("regulatory.phase", "Mevzuat Aşaması", section: "Uyum"));

        Assert.NotNull(item.BusinessContext);
        Assert.Contains("businessContext", item.WorkItemCapabilities);
    }

    [Fact]
    public async Task The_two_never_disagree_whatever_the_catalogue_says()
    {
        // The pairing must not depend on the catalogue being readable — an empty catalogue with stored values is
        // exactly the state a purge leaves behind, and it must still produce both halves.
        var withCatalogue = await ProjectAsync(
            TaskWith(("a", "1")), TenantDefinition("a", "Alan", section: "S"));
        var withoutCatalogue = await ProjectAsync(TaskWith(("a", "1")));

        foreach (var item in new[] { withCatalogue, withoutCatalogue })
        {
            Assert.Equal(
                item.WorkItemCapabilities.Contains("businessContext"),
                item.BusinessContext is not null);
        }
    }

    // ── The projection defends the contract's caps ───────────────────────────

    [Fact]
    public async Task More_than_six_sections_in_OLD_data_does_not_delete_the_task()
    {
        /*
         * The catalogue enforces the six-section cap at the write, but data written before that rule exists.
         * Exceeding the cap makes validateItems drop the WHOLE item — title, actions and all — so the projection
         * takes the first six rather than shipping a task that will vanish.
         */
        var values = Enumerable.Range(1, 8).Select(i => ($"f{i}", $"v{i}")).ToArray();
        var definitions = Enumerable.Range(1, 8)
            .Select(i => TenantDefinition($"f{i}", $"Alan {i}", section: $"Bölüm {i}", sortOrder: i))
            .ToArray();

        var item = await ProjectAsync(TaskWith(values), definitions);

        Assert.Equal(TaskFieldDefinitionRules.MaxSections, item.BusinessContext!.Sections.Count);
        // Still on the surface, still carrying its capability — a trimmed context, not a deleted task.
        Assert.Contains("businessContext", item.WorkItemCapabilities);
    }

    [Fact]
    public async Task Six_sections_are_all_kept()
    {
        // Non-vacuity: an off-by-one that trimmed at five would pass the test above.
        var values = Enumerable.Range(1, 6).Select(i => ($"f{i}", $"v{i}")).ToArray();
        var definitions = Enumerable.Range(1, 6)
            .Select(i => TenantDefinition($"f{i}", $"Alan {i}", section: $"Bölüm {i}", sortOrder: i))
            .ToArray();

        var item = await ProjectAsync(TaskWith(values), definitions);

        Assert.Equal(6, item.BusinessContext!.Sections.Count);
    }

    [Fact]
    public async Task A_REDACTED_value_is_omitted_rather_than_sent()
    {
        // The contract rejects a redacted field that still carries its value, and "sent but hidden with CSS" is
        // not redaction at all.
        var task = TaskWith(("salary.band", "B3"));
        task.FieldValues[0].Redacted = true;

        var item = await ProjectAsync(task, TenantDefinition("salary.band", "Ücret bandı", section: "İK"));

        Assert.Null(item.BusinessContext!.Sections[0].Fields[0].Value);
    }

    // ── The catalogue is read ONCE for the page ──────────────────────────────

    [Fact]
    public async Task The_definition_catalogue_is_read_once_for_the_whole_page()
    {
        /*
         * A stored value carries only its code, so its section, order, label and type all come from the
         * catalogue — and a per-item lookup would be an N+1 across every row that has any configurable value.
         * The same batching rule display names, pool labels and checklist runs already follow.
         */
        var definitions = new FakeTaskFieldDefinitionRepository(
            TenantDefinition("a", "Alan", section: "S"));
        var tasks = Enumerable.Range(1, 10).Select(_ => TaskWith(("a", "1"))).ToArray();

        await ProviderFor(tasks, definitions).GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.Equal(1, definitions.ListAllCalls);
    }

    [Fact]
    public async Task A_page_with_NO_configurable_values_does_not_read_the_catalogue_at_all()
    {
        // Non-vacuity for the count above, and a real saving: most tasks carry no configurable values.
        var definitions = new FakeTaskFieldDefinitionRepository();
        var tasks = Enumerable.Range(1, 10).Select(_ => TaskWith()).ToArray();

        await ProviderFor(tasks, definitions).GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.Equal(0, definitions.ListAllCalls);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<WorkItemProjectionDto> ProjectAsync(
        TaskItem task, params TaskFieldDefinition[] definitions)
        => Assert.Single(await ProviderFor([task], new FakeTaskFieldDefinitionRepository(definitions))
            .GetWorkItemsAsync(Actor(), CancellationToken.None));

    private static TaskWorkItemProvider ProviderFor(
        TaskItem[] tasks, FakeTaskFieldDefinitionRepository definitions)
        => new(
            new FakeTaskItemRepository(tasks),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            definitions);

    private static WorkItemActor Actor()
        => new(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());

    private static TaskItem TaskWith(params (string Code, string Value)[] values) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Yapılandırılabilir alanı olan görev",
        Lifecycle = TaskLifecycle.InProgress,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Version = 1,
        FieldValues = values
            .Select(v => new TaskFieldValue
            {
                DefinitionCode = v.Code,
                ValueType = TaskFieldValueType.Text,
                Value = v.Value
            })
            .ToList()
    };

    private static TaskFieldDefinition TenantDefinition(
        string code, string labelText, string section, int sortOrder = 0) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Code = code,
        LabelText = labelText,
        ValueType = TaskFieldValueType.Text,
        Section = section,
        SortOrder = sortOrder,
        IsActive = true
    };

    private static TaskFieldDefinition SystemDefinition(
        string code, string resourceKey, string section, int sortOrder = 0) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Code = code,
        LabelResourceKey = resourceKey,
        ValueType = TaskFieldValueType.Text,
        Section = section,
        SortOrder = sortOrder,
        IsActive = true
    };
}
