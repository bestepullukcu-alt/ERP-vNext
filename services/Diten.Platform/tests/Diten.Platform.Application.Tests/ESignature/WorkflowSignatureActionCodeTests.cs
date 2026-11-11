using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E5W2 (CT-D19, O-E5W1-3) — the WorkCenter approve action of a signature-required step carries
/// <c>signatureActionCode = workflow.task.approve</c> (additive, E4 pattern); MOD-0023 registers that action in the global
/// catalog in-process (the §4.23 command, CT-D19 defaults: required, APPROVE, optional); the tenant policy shows the row; and
/// the visual mode the tenant sets is the one the signing dialog gets for that action. Real HTTP host, real Mongo.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class WorkflowSignatureActionCodeTests : IClassFixture<WorkflowESignatureTestHost>
{
    private const string Workflow = "api/v1/workflow";

    private static readonly string[] Keys = ESignPermissions.All
        .Concat([WorkflowPermissions.DefinitionsView, WorkflowPermissions.DefinitionsManage, WorkflowPermissions.DefinitionsPublish,
            WorkflowPermissions.InstancesStart, WorkflowPermissions.InstancesView, WorkflowPermissions.TasksApprove,
            WorkflowPermissions.TasksReject, WorkflowPermissions.TasksCancel, WorkflowPermissions.TasksDelegate, WorkflowPermissions.TasksRequestInfo])
        .ToArray();

    private readonly WorkflowESignatureTestHost _host;

    public WorkflowSignatureActionCodeTests(WorkflowESignatureTestHost host) => _host = host;

    private sealed record World(Guid Tenant, Guid Admin, Guid A, Guid B)
    {
        public string Token(Guid user) => ESignatureTestHost.Token(Tenant, user, Keys, name: "User " + user.ToString("N")[..6]);
    }

    private static World NewWorld() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private static string Definition(bool signatureRequired) =>
        "{ \"stages\": [ { \"code\": \"QA\", \"steps\": [ { \"code\": \"QA_APPROVAL\", \"name\": \"QA\", \"type\": \"approval\", "
        + "\"assignment\": { \"candidatePrincipalIds\": [] }, \"requirements\": { \"commentRequired\": false, \"evidenceRequired\": false"
        + (signatureRequired ? ", \"signatureRequired\": true, \"signatureMeaning\": \"APPROVE\"" : "") + " } } ] } ] }";

    private async Task<Guid> StartAsync(World w, bool signatureRequired)
    {
        using var admin = _host.Client(w.Token(w.Admin));
        var code = "E5W2-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var created = await admin.PostAsJsonAsync($"{Workflow}/definitions", new { templateCode = code, name = "E5W2 test", description = "MOD-0022 E5W2" });
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.IsSuccessStatusCode, createdBody);
        var published = await admin.PostAsJsonAsync($"{Workflow}/definitions/{Data(createdBody).GetProperty("id").GetString()}/publish", new
        {
            definitionJson = Definition(signatureRequired), schemaVersion = "workflow_schema_v1", expressionVersion = "workflow_expr_v1", publishReason = "E5W2 test"
        });
        Assert.True(published.IsSuccessStatusCode, await published.Content.ReadAsStringAsync());

        using var starter = _host.Client(w.Token(w.A));
        var objectId = "QKR-" + Guid.NewGuid().ToString("N")[..8];
        var started = await starter.PostAsJsonAsync($"{Workflow}/instances", new
        {
            templateCode = code, objectType = "e5w2.test-object", objectId, objectRef = objectId,
            candidatePrincipalIds = new[] { "user:" + w.B.ToString("D") },
            reasonCode = "SUBMITTED", idempotencyKey = Key(), commentRequired = false, evidenceRequired = false,
            displayContext = new { title = "Karantina kararı " + objectId, subtitle = "Lot PRS-26A014", sourceModule = "MOD-0175", deepLinkUrl = "/x", chips = new[] { "Serbest bırak" } }
        });
        var body = await started.Content.ReadAsStringAsync();
        Assert.True(started.IsSuccessStatusCode, body);
        return Guid.Parse(Data(body).GetProperty("approvalTaskId").GetString()!);
    }

    private async Task<JsonElement> ApproveActionAsync(World w, Guid taskId)
    {
        using var client = _host.Client(w.Token(w.B));
        var response = await client.GetAsync("api/v1/work-items/mine"); // the WorkCenter inbox (login-only)
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var data = Data(body);
        var items = data.ValueKind == JsonValueKind.Array ? data : data.GetProperty("items");
        var item = items.EnumerateArray().Single(i => i.GetProperty("id").GetString() == taskId.ToString("D"));
        return item.GetProperty("actions").EnumerateArray().Single(a => a.GetProperty("code").GetString() == "approve");
    }

    private async Task RegisterAsync()
    {
        using var scope = _host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetPlatformContext(Guid.Empty);
        var response = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(WorkflowSignableActions.Registration());
        Assert.True(response.IsSuccessful, string.Join("; ", response.Errors ?? []));
    }

    private async Task<JsonElement> EffectiveAsync(World w, Guid taskId, string? actionCode)
    {
        using var client = _host.Client(w.Token(w.B));
        var query = $"module=workflow&subjectType=workflow-task&subjectId={taskId:D}" + (actionCode is null ? "" : "&actionCode=" + actionCode);
        var response = await client.GetAsync($"api/v1/esign/policy/effective?{query}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return Data(body);
    }

    private async Task<JsonElement> TenantPolicyAsync(World w)
    {
        using var client = _host.Client(w.Token(w.Admin));
        var body = await (await client.GetAsync("api/v1/esign/policy")).Content.ReadAsStringAsync();
        return Data(body).GetProperty("tenant");
    }

    [Fact]
    public async Task The_approve_action_of_a_signature_required_step_carries_the_workflow_action_code()
    {
        var w = NewWorld();
        var approve = await ApproveActionAsync(w, await StartAsync(w, signatureRequired: true));
        Assert.True(approve.GetProperty("signatureRequired").GetBoolean());
        Assert.Equal(WorkflowSignableActions.TaskApprove, approve.GetProperty("signatureActionCode").GetString());
    }

    [Fact]
    public async Task An_unflagged_step_serializes_as_before_without_the_action_code()
    {
        var w = NewWorld();
        var approve = await ApproveActionAsync(w, await StartAsync(w, signatureRequired: false));
        Assert.False(approve.TryGetProperty("signatureActionCode", out _));
        Assert.False(approve.TryGetProperty("signatureRequired", out _));
    }

    [Fact]
    public async Task The_registration_puts_the_action_in_the_catalog_with_the_CT_D19_defaults_and_is_idempotent()
    {
        await RegisterAsync();
        await RegisterAsync(); // a second startup changes nothing

        var w = NewWorld();
        using var client = _host.Client(w.Token(w.Admin));
        var actions = Data(await (await client.GetAsync("api/v1/esign/actions")).Content.ReadAsStringAsync()).EnumerateArray().ToList();
        var action = actions.Single(a => a.GetProperty("actionCode").GetString() == WorkflowSignableActions.TaskApprove);
        Assert.Equal("workflow", action.GetProperty("module").GetString());
        Assert.True(action.GetProperty("defaultRequired").GetBoolean());
        Assert.Equal("APPROVE", action.GetProperty("defaultMeaningCode").GetString());
        Assert.Equal("optional", action.GetProperty("defaultVisualMode").GetString());
        Assert.Equal("İş akışı adımını onayla", action.GetProperty("names").GetProperty("tr").GetString());

        // A tenant with no stored policy sees the row on the policy screen (seeded from the catalog).
        var row = (await TenantPolicyAsync(w)).GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("actionCode").GetString() == WorkflowSignableActions.TaskApprove);
        Assert.Equal("optional", row.GetProperty("visualMode").GetString());
    }

    [Theory]
    [InlineData("drawRequired")]
    [InlineData("savedAccepted")]
    [InlineData("none")]
    public async Task The_visual_mode_the_tenant_sets_for_the_action_is_what_the_dialog_gets(string visualMode)
    {
        await RegisterAsync();
        var w = NewWorld();
        var taskId = await StartAsync(w, signatureRequired: true);
        var actionCode = (await ApproveActionAsync(w, taskId)).GetProperty("signatureActionCode").GetString(); // what WorkCenter passes

        Assert.Equal("optional", (await EffectiveAsync(w, taskId, actionCode)).GetProperty("visualMode").GetString()); // catalog default

        var version = (await TenantPolicyAsync(w)).GetProperty("version").GetInt32();
        using (var admin = _host.Client(w.Token(w.Admin)))
        {
            var saved = await admin.PutAsJsonAsync("api/v1/esign/policy/tenant", new
            {
                rows = new[] { new { actionCode = WorkflowSignableActions.TaskApprove, required = true, meaningCode = "APPROVE", visualMode } },
                reauthEvery = true, reauthWindowMinutes = 15, reason = "E5W2 workflow approval visual mode", expectedVersion = version
            });
            Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        }

        var effective = await EffectiveAsync(w, taskId, actionCode);
        Assert.Equal(visualMode, effective.GetProperty("visualMode").GetString());
        Assert.True(effective.GetProperty("required").GetBoolean());
        Assert.Equal("APPROVE", effective.GetProperty("meaningCode").GetString());
        // Without the action code (the E5W1 WorkCenter) the dialog never sees the tenant's choice.
        Assert.Equal("optional", (await EffectiveAsync(w, taskId, null)).GetProperty("visualMode").GetString());
    }

    private static JsonElement Data(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
