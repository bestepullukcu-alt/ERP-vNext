using System.Text.Json;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// MOD-0290-FU04 S2 (CT decision 2026-10-09): the tracked development allow-list carries EXACTLY
/// {Diten.MDM, TRUSTED_WORKFLOW_CONSUMER, Item, ITEM-ACTIVATION} for MDM's default client — measured on the real file
/// and the real start handler: the item start is admitted with the entry, and without it the same start is refused by
/// name (403 WORKFLOW_TRUSTED_START_FORBIDDEN) before the engine is touched. No wildcard, no other object type or code.
/// </summary>
public sealed class ItemActivationTrustedStartAuthorizationTests
{
    // The id Auth derives from the default client code 'diten-mdm-workflow' (the file's own note).
    private static readonly Guid MdmClient = Guid.Parse("69682e78-4ad0-80b2-8d9e-8d107eb8dea2");

    [Fact]
    public void The_development_list_names_the_item_flow_exactly_once_and_nothing_wider()
    {
        var entries = DevelopmentEntries();
        var item = Assert.Single(entries, entry => entry.ObjectType.Equals("Item", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(MdmClient, item.ClientId);
        Assert.Equal("Diten.MDM", item.ServiceName);
        Assert.Equal("TRUSTED_WORKFLOW_CONSUMER", item.Audience);
        Assert.Equal("Item", item.ObjectType);
        Assert.Equal("ITEM-ACTIVATION", item.TemplateCode);
        Assert.Null(item.TemplateId);
        Assert.Single(entries, entry => entry.TemplateCode == "ITEM-ACTIVATION");
        Assert.True(new TrustedWorkflowStartAuthorizationOptionsValidator()
            .Validate(null, new TrustedWorkflowStartAuthorizationOptions { Entries = entries }).Succeeded);
    }

    [Fact]
    public async Task An_item_start_is_admitted_with_the_entry()
    {
        var coordinator = new RecordingCoordinator();
        var handler = Handler(DevelopmentEntries(), coordinator);

        var response = await handler.Handle(Command("Item", "ITEM-ACTIVATION"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, coordinator.Calls);
    }

    [Fact]
    public async Task Without_the_entry_the_same_item_start_is_refused_by_name_and_the_engine_is_not_touched()
    {
        var coordinator = new RecordingCoordinator();
        var handler = Handler(DevelopmentEntries().Where(entry => entry.ObjectType != "Item").ToList(), coordinator);

        var response = await handler.Handle(Command("Item", "ITEM-ACTIVATION"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal("WORKFLOW_TRUSTED_START_FORBIDDEN", response.ReasonCode);
        Assert.Equal(0, coordinator.Calls);
    }

    [Theory]
    [InlineData("item", "ITEM-ACTIVATION")]
    [InlineData("Item", "item-activation")]
    [InlineData("Item", "GSKU-IDENTITY")]
    [InlineData("ItemCorrection", "ITEM-ACTIVATION")]
    public async Task Any_drift_from_the_exact_item_entry_is_refused(string objectType, string templateCode)
    {
        var coordinator = new RecordingCoordinator();
        var handler = Handler(DevelopmentEntries(), coordinator);

        var response = await handler.Handle(Command(objectType, templateCode), CancellationToken.None);

        Assert.Equal("WORKFLOW_TRUSTED_START_FORBIDDEN", response.ReasonCode);
        Assert.Equal(0, coordinator.Calls);
    }

    private static StartTrustedWorkflowInstanceHandler Handler(List<TrustedWorkflowStartAuthorizationEntry> entries,
        IWorkflowInstanceStartCoordinator coordinator) =>
        new(coordinator, new ConfiguredTrustedWorkflowStartAuthorizationPolicy(
            Options.Create(new TrustedWorkflowStartAuthorizationOptions { Entries = entries })));

    private static StartTrustedWorkflowInstanceCommand Command(string objectType, string templateCode) => new(
        new TrustedWorkflowStartRequest(null, templateCode, objectType, Guid.NewGuid().ToString("D"), "IT-000000000001",
            ["position:" + Guid.NewGuid().ToString("D")], "ITEM_ACTIVATION_APPROVAL", "item-activation:key", false, false, null),
        MdmClient, "Diten.MDM", "TRUSTED_WORKFLOW_CONSUMER", Guid.NewGuid(), "correlation");

    private static List<TrustedWorkflowStartAuthorizationEntry> DevelopmentEntries()
    {
        var path = Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API",
            "appsettings.Development.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
        });
        return json.RootElement.GetProperty("TrustedWorkflowStartAuthorization").GetProperty("Entries").EnumerateArray()
            .Select(entry => new TrustedWorkflowStartAuthorizationEntry
            {
                ClientId = entry.GetProperty("ClientId").GetGuid(),
                ServiceName = entry.GetProperty("ServiceName").GetString()!,
                Audience = entry.GetProperty("Audience").GetString()!,
                ObjectType = entry.GetProperty("ObjectType").GetString()!,
                TemplateId = entry.TryGetProperty("TemplateId", out var id) ? id.GetGuid() : null,
                TemplateCode = entry.TryGetProperty("TemplateCode", out var code) ? code.GetString() : null
            }).ToList();
    }

    private sealed class RecordingCoordinator : IWorkflowInstanceStartCoordinator
    {
        public int Calls { get; private set; }

        public Task<Response<TrustedWorkflowStartResult>> StartAsync(TrustedWorkflowStartRequest request, Guid serviceClientId,
            Guid delegatedMakerUserId, string correlationId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Response<TrustedWorkflowStartResult>.Success(new TrustedWorkflowStartResult(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                request.ObjectRef ?? string.Empty, "Running", "Approval", "Review", DateTimeOffset.UtcNow, null, false,
                correlationId)));
        }
    }
}
