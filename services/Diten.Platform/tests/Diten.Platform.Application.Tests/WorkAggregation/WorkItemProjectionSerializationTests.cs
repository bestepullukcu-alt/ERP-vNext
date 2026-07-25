using System.Text.Json;
using Diten.Platform.Application.Features.WorkAggregation;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

// WC-1b — the browser validates every item against fixture-contract.js (validateWorkItem), which reads camelCase
// field names. ASP.NET Core's default web JSON options are camelCase; this test ASSERTS that rather than assuming
// it, so a future serializer-options change cannot silently break the executable contract.
public sealed class WorkItemProjectionSerializationTests
{
    // JsonSerializerDefaults.Web is what ASP.NET Core MVC uses for controller responses.
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static WorkItemProjectionDto Sample() => new(
        FixtureKind: WorkItemContract.FixtureKindWorkItem,
        Id: "11111111-1111-1111-1111-111111111111",
        WorkIntent: WorkItemContract.IntentApproval,
        AssignmentMode: WorkItemContract.AssignmentApproval,
        OwnershipState: WorkItemContract.NotApplicable,
        AdmissionState: WorkItemContract.NotApplicable,
        NormalizedStatus: WorkItemContract.StatusPending,
        TaskLifecycle: WorkItemContract.NotApplicable,
        ExecutionState: WorkItemContract.NotApplicable,
        TimerState: WorkItemContract.NotApplicable,
        SystemState: WorkItemContract.SystemFresh,
        ActionDepth: WorkItemContract.DepthInline,
        Title: WorkItemLabelDto.Resource("WorkAggregation_Title_Approval", new Dictionary<string, string>
        {
            ["objectType"] = "invoice",
            ["objectId"] = "INV-1"
        }),
        NativeStatus: new WorkItemNativeStatusDto("WaitingApproval",
            WorkItemLabelDto.Resource("WorkAggregation_NativeStatus_WaitingApproval")),
        Source: new WorkItemSourceDto("workflow", "1.0", "invoice", "INV-1", null),
        LifecycleOwner: WorkItemContract.LifecycleOwnerWorkflow,
        WorkItemCapabilities: [],
        Actions:
        [
            new WorkItemActionDto("approve", WorkItemLabelDto.Resource("WorkAggregation_Action_Approve"),
                "approve", true, WorkItemContract.ActionSourceProvider, null, null, true, false, false, true, "normal")
        ],
        Concurrency: new WorkItemConcurrencyDto("version", "17"),
        WaitingContext: null,
        Escalation: null,
        DueAt: null);

    [Fact]
    public void Serializes_top_level_fields_in_camelCase_matching_the_executable_contract()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(Sample(), WebOptions));
        var root = doc.RootElement;

        // Exactly the field names fixture-contract.js validateWorkItem reads.
        foreach (var field in new[]
                 {
                     "fixtureKind", "id", "workIntent", "assignmentMode", "ownershipState", "admissionState",
                     "normalizedStatus", "taskLifecycle", "executionState", "timerState", "systemState",
                     "actionDepth", "nativeStatus", "source", "workItemCapabilities", "actions", "concurrency"
                 })
        {
            Assert.True(root.TryGetProperty(field, out _), $"Missing camelCase field '{field}'.");
        }

        // PascalCase must NOT appear (would silently fail every contract check in the browser).
        Assert.False(root.TryGetProperty("WorkIntent", out _));
        Assert.False(root.TryGetProperty("NormalizedStatus", out _));
    }

    [Fact]
    public void Serializes_nested_contract_shapes_in_camelCase()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(Sample(), WebOptions));
        var root = doc.RootElement;

        var source = root.GetProperty("source");
        Assert.True(source.TryGetProperty("providerCode", out _));
        Assert.True(source.TryGetProperty("providerContractVersion", out _));
        Assert.True(source.TryGetProperty("objectType", out _));
        Assert.True(source.TryGetProperty("objectId", out _));

        var nativeStatus = root.GetProperty("nativeStatus");
        Assert.True(nativeStatus.TryGetProperty("code", out _));
        var nativeLabel = nativeStatus.GetProperty("label");
        Assert.Equal("resource", nativeLabel.GetProperty("kind").GetString());
        Assert.True(nativeLabel.TryGetProperty("key", out _));

        var action = root.GetProperty("actions")[0];
        foreach (var field in new[] { "code", "label", "enabled", "source", "semanticType" })
        {
            Assert.True(action.TryGetProperty(field, out _), $"Missing action field '{field}'.");
        }

        var concurrency = root.GetProperty("concurrency");
        Assert.True(concurrency.TryGetProperty("kind", out _));
        Assert.True(concurrency.TryGetProperty("token", out _));

        // Label args survive as a named object — the JS l10n helper substitutes {objectType}/{objectId} (DEC-3).
        var titleArgs = root.GetProperty("title").GetProperty("args");
        Assert.Equal("invoice", titleArgs.GetProperty("objectType").GetString());
        Assert.Equal("INV-1", titleArgs.GetProperty("objectId").GetString());
    }
}
