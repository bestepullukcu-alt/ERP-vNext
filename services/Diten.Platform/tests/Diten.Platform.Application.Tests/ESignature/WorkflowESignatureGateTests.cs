using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E4 (pack §13 #17–#21, §16 E4; ESIGN-BUNDLE x-workflow-addendum) — a workflow step whose template says
/// <c>requirements.signatureRequired</c> cannot be approved without an e-signature that binds THIS approver, THIS task and
/// THIS meaning — through the API and through the WorkCenter action endpoint (the same MOD-0023 handler). Written first as
/// the security bypass list (each was red before the gate existed): no signature (API / WorkCenter), another user's
/// signature, another task's signature, another subject, wrong meaning, wrong decision, invalidated, changed subject, a
/// signature already presented on another approval. Plus the regressions: unflagged steps and old templates as today;
/// SoD still first; reject needs no signature (ASSUMPTION-0022-E4-02); publish refuses an inactive / unknown meaning.
/// Real HTTP host, real Mongo (DB-010 test-owned replica set).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed partial class WorkflowESignatureGateTests : IClassFixture<WorkflowESignatureTestHost>
{
    private const string Signatures = "api/v1/esign/signatures";
    private const string Workflow = "api/v1/workflow";

    private static readonly string[] Keys = ESignPermissions.All
        .Concat([WorkflowPermissions.DefinitionsView, WorkflowPermissions.DefinitionsManage, WorkflowPermissions.DefinitionsPublish,
            WorkflowPermissions.InstancesStart, WorkflowPermissions.InstancesView, WorkflowPermissions.TasksApprove,
            WorkflowPermissions.TasksReject, WorkflowPermissions.TasksCancel, WorkflowPermissions.TasksDelegate, WorkflowPermissions.TasksRequestInfo])
        .ToArray();

    private readonly WorkflowESignatureTestHost _host;

    public WorkflowESignatureGateTests(WorkflowESignatureTestHost host) => _host = host;

    private sealed record World(Guid Tenant, Guid Admin, Guid A, Guid B)
    {
        public string Token(Guid user) => ESignatureTestHost.Token(Tenant, user, Keys, name: "User " + user.ToString("N")[..6]);
    }

    private sealed record Started(Guid InstanceId, Guid TaskId, string ObjectId);

    private static World NewWorld() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private static string Definition(bool signatureRequired = true, string? meaning = "APPROVE", bool withRequirements = true)
    {
        var requirements = !withRequirements
            ? ""
            : signatureRequired
                ? $", \"requirements\": {{ \"commentRequired\": false, \"evidenceRequired\": false, \"signatureRequired\": true{(meaning is null ? "" : $", \"signatureMeaning\": \"{meaning}\"")} }}"
                : ", \"requirements\": { \"commentRequired\": false, \"evidenceRequired\": false }";
        return "{ \"stages\": [ { \"code\": \"QA\", \"steps\": [ { \"code\": \"QA_APPROVAL\", \"name\": \"QA\", \"type\": \"approval\", "
               + "\"assignment\": { \"candidatePrincipalIds\": [] }" + requirements + " } ] } ] }";
    }

    // ── workflow helpers ─────────────────────────────────────────────────────────────────────────────────────

    private async Task<(int Status, string? Reason, string Code)> PublishAsync(World w, string definitionJson)
    {
        using var client = _host.Client(w.Token(w.Admin));
        var code = "E4-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var created = await client.PostAsJsonAsync($"{Workflow}/definitions", new { templateCode = code, name = "E4 test", description = "MOD-0022 E4" });
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.IsSuccessStatusCode, createdBody);
        var id = Data(createdBody).GetProperty("id").GetString();
        var published = await client.PostAsJsonAsync($"{Workflow}/definitions/{id}/publish", new
        {
            definitionJson, schemaVersion = "workflow_schema_v1", expressionVersion = "workflow_expr_v1", publishReason = "E4 test"
        });
        return ((int)published.StatusCode, Reason(await published.Content.ReadAsStringAsync()), code);
    }

    private async Task<string> TemplateAsync(World w, string? definitionJson = null)
    {
        var (status, reason, code) = await PublishAsync(w, definitionJson ?? Definition());
        Assert.True(status == 200, $"publish: {status} {reason}");
        return code;
    }

    private async Task<Started> StartAsync(World w, string templateCode, Guid? candidate = null)
    {
        using var client = _host.Client(w.Token(w.A));
        var objectId = "QKR-" + Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync($"{Workflow}/instances", new
        {
            templateCode, objectType = "e4.test-object", objectId, objectRef = objectId,
            candidatePrincipalIds = new[] { "user:" + (candidate ?? w.B).ToString("D") },
            reasonCode = "SUBMITTED", idempotencyKey = Key(), commentRequired = false, evidenceRequired = false,
            displayContext = new { title = "Karantina kararı " + objectId, subtitle = "Lot PRS-26A014 · 1200 EA", sourceModule = "MOD-0175", deepLinkUrl = "/Quarantine/Details/x", chips = new[] { "Serbest bırak" } }
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"start: {(int)response.StatusCode} {body}");
        var data = Data(body);
        return new Started(Guid.Parse(data.GetProperty("workflowInstanceId").GetString()!), Guid.Parse(data.GetProperty("approvalTaskId").GetString()!), objectId);
    }

    private async Task<Guid> SignAsync(World w, Guid signer, Started s, string meaning = "APPROVE", Guid? contextTask = null,
        Guid? subjectTask = null, string decision = "approve")
    {
        using var client = _host.Client(w.Token(signer));
        var response = await client.PostAsJsonAsync(Signatures, new
        {
            ticket = _host.Tickets.Issue(w.Tenant, signer),
            subject = new { module = "workflow", subjectType = "workflow-task", subjectId = (subjectTask ?? s.TaskId).ToString("D") },
            meaningCode = meaning,
            appearance = new { type = "none" },
            context = new Dictionary<string, string>
            {
                ["workflowInstanceId"] = s.InstanceId.ToString("D"),
                ["workflowTaskId"] = (contextTask ?? s.TaskId).ToString("D"),
                ["stepCode"] = "QA_APPROVAL",
                ["decision"] = decision
            },
            idempotencyKey = Key()
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"sign: {(int)response.StatusCode} {body}");
        return Data(body).GetProperty("id").GetGuid();
    }

    private async Task<(int Status, string? Reason)> ApproveAsync(World w, Guid actor, Guid taskId, Guid? signatureId)
    {
        using var client = _host.Client(w.Token(actor));
        var response = await client.PostAsJsonAsync($"{Workflow}/tasks/{taskId}/approve", new
        {
            reasonCode = "APPROVED", idempotencyKey = Key(), comment = (string?)null, signatureId = signatureId?.ToString("D")
        });
        return ((int)response.StatusCode, Reason(await response.Content.ReadAsStringAsync()));
    }

    private async Task<(int Status, string? Reason)> ApproveViaWorkCenterAsync(World w, Guid actor, Guid taskId, Guid? signatureId)
    {
        using var client = _host.Client(w.Token(actor));
        // WP-LANE-MAIN-SYNC-02 P3 — the lane's Task Center card always sends the task's version (the dispatcher refuses an
        // approve without it, WORK_ITEM_ACTION_PAYLOAD_INVALID): read here as the card reads it.
        var version = (await _host.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.Id == taskId).FirstAsync()).Version;
        var response = await client.PostAsJsonAsync($"api/v1/work-items/{taskId}/actions/approve", new
        {
            providerCode = "workflow",
            payload = new { expectedVersion = version, idempotencyKey = Key(), signatureId = signatureId?.ToString("D") }
        });
        return ((int)response.StatusCode, Reason(await response.Content.ReadAsStringAsync()));
    }

    private async Task<List<JsonElement>> HistoryAsync(World w, Guid instanceId)
    {
        using var client = _host.Client(w.Token(w.A));
        var response = await client.GetAsync($"{Workflow}/instances/{instanceId}/history");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return Data(body).EnumerateArray().ToList();
    }

    private async Task<string> TaskStatusAsync(Guid taskId)
        => (await _host.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.Id == taskId).FirstAsync()).Status.ToString();

    private async Task<(World W, Started S)> FlaggedAsync()
    {
        var w = NewWorld();
        var code = await TemplateAsync(w);
        return (w, await StartAsync(w, code));
    }

    // ── the bypass list (security-agent, red before the gate) ─────────────────────────────────────────────

    [Fact]
    public async Task Api_approve_of_a_signature_required_step_without_a_signature_is_409_required()
    {
        var (w, s) = await FlaggedAsync();
        Assert.Equal((409, ESignCodes.SignatureRequired), await ApproveAsync(w, w.B, s.TaskId, null));
        Assert.Equal("WaitingApproval", await TaskStatusAsync(s.TaskId));
    }

    [Fact]
    public async Task WorkCenter_approve_of_a_signature_required_step_without_a_signature_is_409_required()
    {
        var (w, s) = await FlaggedAsync();
        Assert.Equal((409, ESignCodes.SignatureRequired), await ApproveViaWorkCenterAsync(w, w.B, s.TaskId, null));
        Assert.Equal("WaitingApproval", await TaskStatusAsync(s.TaskId));
    }

    [Fact]
    public async Task Another_users_signature_is_refused_on_both_paths()
    {
        var (w, s) = await FlaggedAsync();
        var signedByAdmin = await SignAsync(w, w.Admin, s);
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, signedByAdmin));
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveViaWorkCenterAsync(w, w.B, s.TaskId, signedByAdmin));
        Assert.Equal("WaitingApproval", await TaskStatusAsync(s.TaskId));
    }

    [Fact]
    public async Task Another_tasks_signature_is_refused()
    {
        var w = NewWorld();
        var code = await TemplateAsync(w);
        var first = await StartAsync(w, code);
        var second = await StartAsync(w, code);
        var forFirst = await SignAsync(w, w.B, first);
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, second.TaskId, forFirst));
        Assert.Equal("WaitingApproval", await TaskStatusAsync(second.TaskId));
    }

    /// <summary>Context names this task, but the signed SUBJECT is another task (its hash is valid for that one).</summary>
    [Fact]
    public async Task A_signature_whose_subject_is_another_task_is_refused_even_with_this_tasks_context()
    {
        var w = NewWorld();
        var code = await TemplateAsync(w);
        var first = await StartAsync(w, code);
        var other = await StartAsync(w, code);
        var misbound = await SignAsync(w, w.B, first, subjectTask: other.TaskId);
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, first.TaskId, misbound));
    }

    [Fact]
    public async Task A_signature_with_another_meaning_is_refused()
    {
        var (w, s) = await FlaggedAsync();
        var review = await SignAsync(w, w.B, s, meaning: "REVIEW");
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, review));
    }

    [Fact]
    public async Task A_signature_for_another_decision_is_refused()
    {
        var (w, s) = await FlaggedAsync();
        var reject = await SignAsync(w, w.B, s, decision: "reject");
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, reject));
    }

    [Fact]
    public async Task An_invalidated_signature_is_refused()
    {
        var (w, s) = await FlaggedAsync();
        var signature = await SignAsync(w, w.B, s);
        using (var client = _host.Client(w.Token(w.B)))
        {
            var invalidated = await client.PostAsJsonAsync($"{Signatures}/{signature}/invalidate",
                new { ticket = _host.Tickets.Issue(w.Tenant, w.B, "invalidate"), reason = "Signed the wrong decision by mistake", idempotencyKey = Key() });
            Assert.Equal(HttpStatusCode.Created, invalidated.StatusCode);
        }

        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, signature));
    }

    [Fact]
    public async Task A_signature_whose_subject_changed_after_signing_is_refused()
    {
        var (w, s) = await FlaggedAsync();
        var signature = await SignAsync(w, w.B, s);
        // the business display the approver signed is changed afterwards (verification → changed)
        await _host.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            i => i.Id == s.InstanceId, Builders<WorkflowInstance>.Update.Set(i => i.DisplayContext!.Subtitle, "Lot PRS-26A014 · 9999 EA"));
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, signature));
    }

    /// <summary>
    /// Single use: a signature already presented on another approval (an unflagged step stores any signatureId it is given,
    /// contract) cannot bind a second approval. K3 (c): without the binding check this second approval passes.
    /// </summary>
    [Fact]
    public async Task A_signature_already_presented_on_another_approval_cannot_be_used_again()
    {
        var w = NewWorld();
        var flagged = await TemplateAsync(w);
        var plain = await TemplateAsync(w, Definition(signatureRequired: false));
        var task = await StartAsync(w, flagged);
        var elsewhere = await StartAsync(w, plain);
        var signature = await SignAsync(w, w.B, task);

        var presented = await ApproveAsync(w, w.B, elsewhere.TaskId, signature);
        Assert.True(presented.Status == 200, $"unflagged approve: {presented}");
        Assert.Equal(signature.ToString("D"), (await HistoryAsync(w, elsewhere.InstanceId)).Last().GetProperty("signatureId").GetString());

        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, task.TaskId, signature));
        Assert.Equal("WaitingApproval", await TaskStatusAsync(task.TaskId));
    }

    // ── the valid path, audit, repeat ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_approvers_own_signature_for_this_task_approves_and_is_recorded_in_the_history()
    {
        var (w, s) = await FlaggedAsync();
        var signature = await SignAsync(w, w.B, s);
        Assert.Equal(200, (await ApproveAsync(w, w.B, s.TaskId, signature)).Status);
        Assert.Equal("Approved", await TaskStatusAsync(s.TaskId));

        var approve = (await HistoryAsync(w, s.InstanceId)).Single(r => r.GetProperty("action").GetString() == "approve");
        Assert.Equal(signature.ToString("D"), approve.GetProperty("signatureId").GetString());
        Assert.Equal(w.B.ToString("D"), approve.GetProperty("actorId").GetString());
        var log = await _host.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs)
            .Find(l => l.WorkflowInstanceId == s.InstanceId && l.Action == WorkflowTransitionAction.Approve).SingleAsync();
        Assert.Equal(signature.ToString("D"), log.SignatureId);

        // the same signature again (a second click with a new key) → refused, the task is decided
        Assert.Equal(409, (await ApproveAsync(w, w.B, s.TaskId, signature)).Status);
    }

    [Fact]
    public async Task The_WorkCenter_path_approves_with_the_signature()
    {
        var (w, s) = await FlaggedAsync();
        var signature = await SignAsync(w, w.B, s);
        var (status, reason) = await ApproveViaWorkCenterAsync(w, w.B, s.TaskId, signature);
        Assert.True(status == 200, $"{status} {reason}");
        Assert.Equal("Approved", await TaskStatusAsync(s.TaskId));
        Assert.Equal(signature.ToString("D"), (await HistoryAsync(w, s.InstanceId)).Single(r => r.GetProperty("action").GetString() == "approve").GetProperty("signatureId").GetString());
    }

    [Fact]
    public async Task The_starter_is_stopped_by_SoD_before_the_signature_check()
    {
        var w = NewWorld();
        var code = await TemplateAsync(w);
        // WP-LANE-MAIN-SYNC-02 P2a (owner decision 2026-10-14): the lane's SoD stops the starter EARLIER — at start, 400
        // WORKFLOW_NO_ELIGIBLE_APPROVER — so there is no task to approve, with or without a signature. Main expected the
        // start to pass and both approvals to be 409 WORKFLOW_SOD_VIOLATION.
        using var client = _host.Client(w.Token(w.A));
        var objectId = "QKR-" + Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync($"{Workflow}/instances", new
        {
            templateCode = code, objectType = "e4.test-object", objectId, objectRef = objectId,
            candidatePrincipalIds = new[] { "user:" + w.A.ToString("D") },
            reasonCode = "SUBMITTED", idempotencyKey = Key(), commentRequired = false, evidenceRequired = false,
            displayContext = new { title = "Karantina kararı " + objectId, subtitle = "Lot PRS-26A014 · 1200 EA", sourceModule = "MOD-0175", deepLinkUrl = "/Quarantine/Details/x", chips = new[] { "Serbest bırak" } }
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(400, (int)response.StatusCode);
        Assert.Contains(WorkflowReasonCodes.WorkflowNoEligibleApprover, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reject_of_a_signature_required_step_needs_no_signature()
    {
        var (w, s) = await FlaggedAsync();
        using var client = _host.Client(w.Token(w.B));
        var response = await client.PostAsJsonAsync($"{Workflow}/tasks/{s.TaskId}/reject",
            new { reasonCode = "REJECTED", idempotencyKey = Key(), comment = "Analiz sonucu uygun değil" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal("Rejected", await TaskStatusAsync(s.TaskId));
    }

    // ── regressions: unflagged steps, old templates ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Steps_without_the_flag_and_old_templates_approve_as_today(bool oldTemplate)
    {
        var w = NewWorld();
        var code = await TemplateAsync(w, Definition(signatureRequired: false, withRequirements: !oldTemplate));
        var s = await StartAsync(w, code);
        Assert.Equal(200, (await ApproveAsync(w, w.B, s.TaskId, null)).Status);
        var approve = (await HistoryAsync(w, s.InstanceId)).Single(r => r.GetProperty("action").GetString() == "approve");
        Assert.True(!approve.TryGetProperty("signatureId", out var id) || id.ValueKind == JsonValueKind.Null);
        var task = await _host.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.Id == s.TaskId).FirstAsync();
        Assert.False(task.SignatureRequired);
        Assert.Null(task.SignatureMeaning);
    }

    [Fact]
    public async Task Unflagged_step_via_WorkCenter_approves_as_today()
    {
        var w = NewWorld();
        var s = await StartAsync(w, await TemplateAsync(w, Definition(signatureRequired: false)));
        Assert.Equal(200, (await ApproveViaWorkCenterAsync(w, w.B, s.TaskId, null)).Status);
    }

    /// <summary>A flagged SECOND step: the next task created by the first approval carries the flag (next-step path).</summary>
    [Fact]
    public async Task A_signature_required_second_step_carries_the_flag_into_the_next_task()
    {
        var w = NewWorld();
        var definition = "{ \"stages\": [ { \"code\": \"QA\", \"steps\": ["
            + "{ \"code\": \"PRE\", \"name\": \"Ön kontrol\", \"type\": \"approval\", \"assignment\": { \"candidatePrincipalIds\": [\"user:" + w.B.ToString("D") + "\"] } },"
            + "{ \"code\": \"QA_APPROVAL\", \"name\": \"QA\", \"type\": \"approval\", \"assignment\": { \"candidatePrincipalIds\": [\"user:" + w.B.ToString("D") + "\"] },"
            + " \"requirements\": { \"signatureRequired\": true, \"signatureMeaning\": \"RELEASE\" } } ] } ] }";
        var s = await StartAsync(w, await TemplateAsync(w, definition));
        Assert.Equal(200, (await ApproveAsync(w, w.B, s.TaskId, null)).Status); // step 1 is not flagged

        var next = await _host.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == s.InstanceId && t.StepCode == "QA_APPROVAL").SingleAsync();
        Assert.True(next.SignatureRequired);
        Assert.Equal("RELEASE", next.SignatureMeaning);
        Assert.Equal((409, ESignCodes.SignatureRequired), await ApproveAsync(w, w.B, next.Id, null));
        var approveSigned = await SignAsync(w, w.B, s with { TaskId = next.Id }, meaning: "APPROVE");
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, next.Id, approveSigned)); // RELEASE expected
        // E4 measured here that E2's unique per-task index refused the corrected signature AT SIGNING (409, O-E4-1). E5B
        // (FU-0022-E4-01) relaxed the index: the corrected signature is recorded and approves; the wrong one stays refused.
        using var client = _host.Client(w.Token(w.B));
        var second = await client.PostAsJsonAsync(Signatures, new
        {
            ticket = _host.Tickets.Issue(w.Tenant, w.B),
            subject = new { module = "workflow", subjectType = "workflow-task", subjectId = next.Id.ToString("D") },
            meaningCode = "RELEASE",
            appearance = new { type = "none" },
            context = new Dictionary<string, string>
            {
                ["workflowInstanceId"] = s.InstanceId.ToString("D"), ["workflowTaskId"] = next.Id.ToString("D"), ["stepCode"] = "QA_APPROVAL", ["decision"] = "approve"
            },
            idempotencyKey = Key()
        });
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.True(second.StatusCode == HttpStatusCode.Created, $"re-sign: {(int)second.StatusCode} {secondBody}");
        var corrected = Data(secondBody).GetProperty("id").GetGuid();
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, next.Id, approveSigned)); // still wrong
        Assert.Equal("WaitingApproval", await TaskStatusAsync(next.Id));
        Assert.Equal(200, (await ApproveAsync(w, w.B, next.Id, corrected)).Status);
        Assert.Equal("Approved", await TaskStatusAsync(next.Id));
    }

    [Fact]
    public void A_WorkCenter_payload_without_a_signature_serializes_as_before()
    {
        var json = JsonSerializer.Serialize(new Diten.Platform.Application.Features.WorkAggregation.Dispatch.WorkItemActionPayloadDto(IdempotencyKey: "k"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("signatureId", json, StringComparison.OrdinalIgnoreCase);
    }

    // ── template + work item ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("NOT_A_MEANING")]
    [InlineData(null)]
    public async Task Publishing_a_signature_step_without_an_active_meaning_is_400(string? meaning)
    {
        var w = NewWorld();
        var (status, reason, _) = await PublishAsync(w, Definition(meaning: meaning));
        Assert.Equal((400, ESignCodes.MeaningNotAllowed), (status, reason));
    }

    [Fact]
    public async Task The_task_carries_the_step_flag_and_the_WorkCenter_approve_action_says_signature_required()
    {
        var (w, s) = await FlaggedAsync();
        var task = await _host.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.Id == s.TaskId).FirstAsync();
        Assert.True(task.SignatureRequired);
        Assert.Equal("APPROVE", task.SignatureMeaning);

        using var client = _host.Client(w.Token(w.B));
        var response = await client.GetAsync("api/v1/work-items/mine");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var item = FindItem(Data(body), s.TaskId);
        var approve = item.GetProperty("actions").EnumerateArray().Single(a => a.GetProperty("code").GetString() == "approve");
        Assert.True(approve.GetProperty("signatureRequired").GetBoolean());
        Assert.Equal("APPROVE", approve.GetProperty("signatureMeaning").GetString());
    }

    private static JsonElement FindItem(JsonElement data, Guid taskId)
    {
        var items = data.ValueKind == JsonValueKind.Array ? data : data.GetProperty("items");
        return items.EnumerateArray().Single(i => (i.TryGetProperty("sourceId", out var sid) && sid.GetString() == taskId.ToString("D"))
                                                 || (i.TryGetProperty("id", out var id) && id.GetString() == taskId.ToString("D")));
    }

    private static JsonElement Data(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }

    private static string? Reason(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var name in new[] { "reason_code", "reasonCode" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
