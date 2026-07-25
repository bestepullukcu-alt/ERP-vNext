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
            new TaskAssignmentResolver());

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
