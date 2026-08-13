using System.Text.Json;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 — what the provider actually puts ON THE WIRE. The Task Center validates every incoming item against
/// the executable contract (fixture-contract.js) and DROPS the ones that fail, so a shape error here removes work
/// from the user's list without any error surfacing. Asserting the serialized JSON — not just the DTO — is the
/// same lesson the string-enum bug taught: object graphs and wire payloads are different things.
/// </summary>
public sealed class TaskWorkItemProviderWireContractTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static async Task<JsonElement> ProjectAndSerializeAsync(TaskItem task)
    {
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(), new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

        var items = await provider.GetWorkItemsAsync(
            new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()));

        var json = JsonSerializer.Serialize(Assert.Single(items), WebOptions);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task The_title_is_the_text_the_user_typed_not_a_resource_key()
    {
        var element = await ProjectAndSerializeAsync(SelfTask("CT probe"));

        var title = element.GetProperty("title");
        Assert.Equal("display", title.GetProperty("kind").GetString());
        Assert.Equal("CT probe", title.GetProperty("text").GetString());
    }

    [Fact]
    public async Task A_display_title_omits_key_entirely_rather_than_writing_null()
    {
        // fixture-contract.js requires `label.key === undefined` for a display label. A serialized "key": null is
        // NOT undefined, so writing null would fail validation and the item would vanish from the Task Center.
        var element = await ProjectAndSerializeAsync(SelfTask("CT probe"));
        var title = element.GetProperty("title");

        Assert.False(title.TryGetProperty("key", out _));
        Assert.True(title.TryGetProperty("locale", out var locale));
        Assert.False(string.IsNullOrWhiteSpace(locale.GetString()));
    }

    [Fact]
    public async Task A_resource_label_still_omits_text_so_existing_providers_keep_validating()
    {
        // The mirror rule: `label.text === undefined` for a resource label. Adding Text/Locale members to the
        // shared DTO must not start emitting "text": null on every resource label MOD-0023 sends.
        var element = await ProjectAndSerializeAsync(SelfTask("CT probe"));
        var nativeStatusLabel = element.GetProperty("nativeStatus").GetProperty("label");

        Assert.Equal("resource", nativeStatusLabel.GetProperty("kind").GetString());
        Assert.True(nativeStatusLabel.TryGetProperty("key", out _));
        Assert.False(nativeStatusLabel.TryGetProperty("text", out _));
    }

    [Fact]
    public async Task The_provider_code_matches_the_module_catalog_and_permission_namespace()
    {
        var element = await ProjectAndSerializeAsync(SelfTask("CT probe"));

        // "tasks" — the same string as TaskManifestProvider.ModuleCode and platform.tasks.*; a provider-only
        // alias leaves the Task Center unable to name the owning module.
        Assert.Equal("tasks", element.GetProperty("source").GetProperty("providerCode").GetString());
        Assert.Equal("tasks", element.GetProperty("lifecycleOwner").GetString());
    }

    [Fact]
    public async Task A_title_containing_markup_is_carried_verbatim_for_the_client_to_escape()
    {
        // Display text is user input; the projection must not silently alter it (the shell escapes on render).
        var element = await ProjectAndSerializeAsync(SelfTask("<b>hi</b> & bye"));

        Assert.Equal("<b>hi</b> & bye", element.GetProperty("title").GetProperty("text").GetString());
    }


    // ── WHO the work belongs to (the detail pane showed "ATANAN —" without these) ──

    [Fact]
    public async Task The_assignee_and_requester_reach_the_wire()
    {
        var task = SelfTask("CT probe");
        task.CreatedByUserId = TaskTestData.Me;

        var element = await ProjectAndSerializeAsync(task);

        var assignee = element.GetProperty("assignee");
        Assert.Equal(TaskTestData.Me.ToString(), assignee.GetProperty("id").GetString());
        var requester = element.GetProperty("requester");
        Assert.Equal(TaskTestData.Me.ToString(), requester.GetProperty("id").GetString());
    }

    [Fact]
    public async Task A_person_who_is_the_caller_is_flagged_so_the_client_can_say_Me()
    {
        var task = SelfTask("CT probe");
        task.CreatedByUserId = TaskTestData.Rival;

        var element = await ProjectAndSerializeAsync(task);

        Assert.True(element.GetProperty("assignee").GetProperty("isCurrentUser").GetBoolean());
        Assert.False(element.GetProperty("requester").GetProperty("isCurrentUser").GetBoolean());
    }

    [Fact]
    public async Task An_unresolvable_display_name_is_OMITTED_not_written_as_null()
    {
        // Platform has no user-directory seam yet. A serialized "displayName": null would reach the client as a
        // present-but-empty value; omission keeps `person.displayName || fallback` working.
        var element = await ProjectAndSerializeAsync(SelfTask("CT probe"));

        Assert.False(element.GetProperty("assignee").TryGetProperty("displayName", out _));
    }

    [Fact]
    public async Task An_unclaimed_pool_task_reports_NO_assignee_rather_than_an_empty_one()
    {
        var task = SelfTask("Pooled work");
        task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        task.AssigneeUserId = null;
        task.PoolPositionId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(new Domain.Entities.Organization.PositionAssignment
            {
                TenantId = TaskTestData.Tenant,
                PositionId = task.PoolPositionId.Value,
                UserId = TaskTestData.Me,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
            }),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(), new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

        var items = await provider.GetWorkItemsAsync(
            new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()));
        var element = JsonDocument.Parse(JsonSerializer.Serialize(Assert.Single(items), WebOptions)).RootElement;

        // Nobody holds it — the field is absent, not an object with a blank id.
        Assert.False(element.TryGetProperty("assignee", out _));
    }

    [Fact]
    public async Task A_task_with_no_recorded_creator_omits_the_requester()
    {
        var task = SelfTask("CT probe");
        task.CreatedByUserId = null;

        var element = await ProjectAndSerializeAsync(task);

        Assert.False(element.TryGetProperty("requester", out _));
    }

    private static TaskItem SelfTask(string title) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = title,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };
}
