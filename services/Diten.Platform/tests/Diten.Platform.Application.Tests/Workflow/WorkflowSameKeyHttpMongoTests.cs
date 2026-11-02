using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.ESignature;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// MOD-0023 §17 DL-2 — the same-key answer on the WIRE: the real HTTP host (JWT, tenant middleware, <c>[HasPermission]</c>,
/// <c>WorkflowDefinitionsController</c>, the MediatR pipeline, the workflow repositories) over a test-owned replica set.
/// AC-W5 without a barrier, AC-W11's <c>idempotentReplay</c> field both ways, AC-W12's closed-start 409 and its readable
/// close row, AC-W14's e-signature as part of the compared body. A fake repository is not evidence here.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class WorkflowSameKeyHttpMongoTests : IClassFixture<WorkflowESignatureTestHost>
{
    private const string Signatures = "api/v1/esign/signatures";
    private const string Workflow = "api/v1/workflow";

    private static readonly string[] Keys = ESignPermissions.All
        .Concat([WorkflowPermissions.DefinitionsView, WorkflowPermissions.DefinitionsManage, WorkflowPermissions.DefinitionsPublish,
            WorkflowPermissions.InstancesStart, WorkflowPermissions.InstancesView, WorkflowPermissions.TasksApprove,
            WorkflowPermissions.TasksReject, WorkflowPermissions.TasksCancel, WorkflowPermissions.TasksDelegate, WorkflowPermissions.TasksRequestInfo])
        .ToArray();

    private readonly WorkflowESignatureTestHost _host;

    public WorkflowSameKeyHttpMongoTests(WorkflowESignatureTestHost host) => _host = host;

    private sealed record World(Guid Tenant, Guid Admin, Guid A, Guid B)
    {
        public string Token(Guid user) => ESignatureTestHost.Token(Tenant, user, Keys, name: "User " + user.ToString("N")[..6]);
    }

    private sealed record Started(Guid InstanceId, Guid TaskId, string ObjectId);

    private static World NewWorld() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private static string Definition(bool signatureRequired)
    {
        var requirements = signatureRequired
            ? ", \"requirements\": { \"commentRequired\": false, \"evidenceRequired\": false, \"signatureRequired\": true, \"signatureMeaning\": \"APPROVE\" }"
            : ", \"requirements\": { \"commentRequired\": false, \"evidenceRequired\": false }";
        return "{ \"stages\": [ { \"code\": \"QA\", \"steps\": [ { \"code\": \"QA_APPROVAL\", \"name\": \"QA\", \"type\": \"approval\", "
               + "\"assignment\": { \"candidatePrincipalIds\": [] }" + requirements + " } ] } ] }";
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<string> TemplateAsync(World w, bool signatureRequired = false)
    {
        using var client = _host.Client(w.Token(w.Admin));
        var code = "S17-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var created = await client.PostAsJsonAsync($"{Workflow}/definitions", new { templateCode = code, name = "S17 same key", description = "MOD-0023 DL-2" });
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.IsSuccessStatusCode, createdBody);
        var id = Data(createdBody).GetProperty("id").GetString();
        var published = await client.PostAsJsonAsync($"{Workflow}/definitions/{id}/publish", new
        {
            definitionJson = Definition(signatureRequired), schemaVersion = "workflow_schema_v1", expressionVersion = "workflow_expr_v1",
            publishReason = "S17 test"
        });
        Assert.True(published.IsSuccessStatusCode, await published.Content.ReadAsStringAsync());
        return code;
    }

    private async Task<(HttpStatusCode Status, string Body)> PostStartAsync(World w, string templateCode, string objectId, string key)
    {
        using var client = _host.Client(w.Token(w.A));
        var response = await client.PostAsJsonAsync($"{Workflow}/instances", new
        {
            templateCode, objectType = "s17.test-object", objectId, objectRef = objectId,
            candidatePrincipalIds = new[] { "user:" + w.B.ToString("D") },
            reasonCode = "SUBMITTED", idempotencyKey = key, commentRequired = false, evidenceRequired = false
        });
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<Started> StartAsync(World w, string templateCode)
    {
        var objectId = "S17-" + Guid.NewGuid().ToString("N")[..8];
        var (status, body) = await PostStartAsync(w, templateCode, objectId, Key());
        Assert.True(status == HttpStatusCode.OK, $"start: {(int)status} {body}");
        var data = Data(body);
        return new Started(Guid.Parse(data.GetProperty("workflowInstanceId").GetString()!), Guid.Parse(data.GetProperty("approvalTaskId").GetString()!), objectId);
    }

    private async Task<Guid> SignAsync(World w, Guid signer, Started s)
    {
        using var client = _host.Client(w.Token(signer));
        var response = await client.PostAsJsonAsync(Signatures, new
        {
            ticket = _host.Tickets.Issue(w.Tenant, signer),
            subject = new { module = "workflow", subjectType = "workflow-task", subjectId = s.TaskId.ToString("D") },
            meaningCode = "APPROVE",
            appearance = new { type = "none" },
            context = new Dictionary<string, string>
            {
                ["workflowInstanceId"] = s.InstanceId.ToString("D"),
                ["workflowTaskId"] = s.TaskId.ToString("D"),
                ["stepCode"] = "QA_APPROVAL",
                ["decision"] = "approve"
            },
            idempotencyKey = Key()
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"sign: {(int)response.StatusCode} {body}");
        return Data(body).GetProperty("id").GetGuid();
    }

    private async Task<(int Status, string? Reason, string Body)> ApproveAsync(World w, Guid actor, Guid taskId, string key, Guid? signatureId)
    {
        using var client = _host.Client(w.Token(actor));
        var response = await client.PostAsJsonAsync($"{Workflow}/tasks/{taskId}/approve", new
        {
            reasonCode = "APPROVED", idempotencyKey = key, comment = (string?)null, signatureId = signatureId?.ToString("D")
        });
        var body = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, Reason(body), body);
    }

    private IMongoCollection<T> Collection<T>(string name) => _host.Database.GetCollection<T>(name);

    private static JsonElement Data(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }

    private static string? Reason(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
    }

    // ── AC-W5 · independent requests, no barrier ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC_W5_independent_requests_without_a_barrier_end_in_one_instance()
    {
        const int callers = 8;
        var w = NewWorld();
        var template = await TemplateAsync(w);
        for (var round = 1; round <= 5; round++)
        {
            var key = Key();
            var objectId = "S17-W5-" + round + "-" + Guid.NewGuid().ToString("N")[..6];

            var first = await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => Task.Run(() => PostStartAsync(w, template, objectId, key))));

            var finals = new List<(HttpStatusCode Status, string Body)>();
            foreach (var answer in first)
            {
                var current = answer;
                // 503 is the retryable answer of a lost claim: the same key is sent again until the start answers
                for (var retry = 0; retry < 5 && current.Status == HttpStatusCode.ServiceUnavailable; retry++)
                {
                    current = await PostStartAsync(w, template, objectId, key);
                }

                finals.Add(current);
            }

            var instance = await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
                .Find(i => i.TenantId == w.Tenant && i.IdempotencyKey == key).ToListAsync();
            var only = Assert.Single(instance);
            var tasks = await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.WorkflowInstanceId == only.Id).ToListAsync();
            var task = Assert.Single(tasks);
            var logs = await Collection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs)
                .Find(l => l.WorkflowInstanceId == only.Id).ToListAsync();
            Assert.Single(logs);
            Assert.All(finals, f =>
            {
                Assert.True(f.Status == HttpStatusCode.OK, $"round {round}: {(int)f.Status} {f.Body} — a business refusal");
                var data = Data(f.Body);
                Assert.Equal(only.Id, data.GetProperty("workflowInstanceId").GetGuid());
                Assert.Equal(task.Id, data.GetProperty("approvalTaskId").GetGuid());
            });
        }
    }

    // ── AC-W11 · the flag on the wire ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC_W11_idempotent_replay_on_the_wire()
    {
        var w = NewWorld();
        var template = await TemplateAsync(w);
        var key = Key();
        var objectId = "S17-W11-" + Guid.NewGuid().ToString("N")[..8];

        var first = await PostStartAsync(w, template, objectId, key);
        var repeat = await PostStartAsync(w, template, objectId, key);
        var again = await PostStartAsync(w, template, objectId, key);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, repeat.Status);
        // the field is named exactly idempotentReplay on the wire, and present in both directions
        Assert.True(Data(first.Body).TryGetProperty("idempotentReplay", out var firstFlag), first.Body);
        Assert.Equal(JsonValueKind.False, firstFlag.ValueKind);
        Assert.True(Data(repeat.Body).TryGetProperty("idempotentReplay", out var repeatFlag), repeat.Body);
        Assert.Equal(JsonValueKind.True, repeatFlag.ValueKind);
        Assert.Equal(JsonValueKind.True, Data(again.Body).GetProperty("idempotentReplay").ValueKind);
        Assert.Equal(Data(first.Body).GetProperty("workflowInstanceId").GetGuid(), Data(repeat.Body).GetProperty("workflowInstanceId").GetGuid());
    }

    // ── AC-W12 · a never-whole start, closed, then repeated ──────────────────────────────────────────────────

    [Fact]
    public async Task AC_W12_never_whole_closed_start_replay_is_409_and_close_record_is_readable()
    {
        var w = NewWorld();
        var code = await TemplateAsync(w);
        var template = await Collection<WorkflowTemplate>(PlatformCollections.WorkflowTemplates)
            .Find(t => t.TenantId == w.Tenant && t.TemplateCode == code).FirstAsync();
        var key = Key();
        var objectId = "S17-W12-" + Guid.NewGuid().ToString("N")[..8];
        var then = DateTimeOffset.UtcNow.AddHours(-2);
        // What a first call that crashed right after its instance leaves: an Active instance with the ids of its rows and no task.
        var half = new WorkflowInstance
        {
            TenantId = w.Tenant,
            TemplateId = template.Id,
            WorkflowTemplateId = template.Id,
            TemplateVersionId = template.ActivePublishedVersionId,
            ObjectType = "s17.test-object",
            ObjectId = objectId,
            ObjectRef = objectId,
            Status = WorkflowInstanceStatus.Active,
            IdempotencyKey = key,
            StartedBy = $"user.{w.A:N}@tenant.example",
            StartedByUserId = w.A,
            StartedAt = then,
            LastTransitionAt = then,
            InitialApprovalTaskId = Guid.NewGuid(),
            InitialAssignmentSnapshotId = Guid.NewGuid(),
            StartTransitionLogId = Guid.NewGuid()
        };
        await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(half);

        using (var scope = _host.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            ((TenantContext)sp.GetRequiredService<ITenantContext>()).SetTenant(w.Tenant);
            var closer = new CloseAbandonedWorkflowStartHandler(
                sp.GetRequiredService<IWorkflowInstanceRepository>(), sp.GetRequiredService<IApprovalTaskRepository>(),
                sp.GetRequiredService<IWorkflowTransitionLogRepository>(), sp.GetRequiredService<IPlatformTransactionExecutor>(),
                sp.GetRequiredService<ITransactionalIntegrationEventWriter>(), sp.GetRequiredService<IWorkflowTemplateRepository>());
            var closed = await closer.Handle(new CloseAbandonedWorkflowStartCommand(half.Id, TimeSpan.FromMinutes(30), "corr-s17-w12"), default);
            Assert.True(closed.IsSuccessful, $"{closed.StatusCode} {closed.ReasonCode}");
            Assert.False(closed.Data!.AlreadyClosed);
        }

        var replay = await PostStartAsync(w, code, objectId, key);

        Assert.Equal(HttpStatusCode.Conflict, replay.Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowInstanceStartConflict, Reason(replay.Body));
        var stored = await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).Find(i => i.Id == half.Id).FirstAsync();
        Assert.Equal(WorkflowInstanceStatus.Cancelled, stored.Status); // not brought back
        Assert.Empty(await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.WorkflowInstanceId == half.Id).ToListAsync());

        // The close row is readable through the history endpoint, under the recovery's own key.
        using var client = _host.Client(w.Token(w.A));
        var history = await client.GetAsync($"{Workflow}/instances/{half.Id}/history");
        var historyBody = await history.Content.ReadAsStringAsync();
        Assert.True(history.IsSuccessStatusCode, historyBody);
        var close = Assert.Single(Data(historyBody).EnumerateArray().ToList(),
            row => row.TryGetProperty("idempotencyKey", out var k) && k.GetString() == $"workflow-start-recovery-close:{half.Id:N}");
        Assert.Equal(WorkflowReasonCodes.WorkflowStartAbandoned, close.GetProperty("reasonCode").GetString());
    }

    // ── AC-W14 · the e-signature is part of the compared body ────────────────────────────────────────────────

    [Fact]
    public async Task AC_W14_signature_id_is_part_of_the_body()
    {
        var w = NewWorld();
        var code = await TemplateAsync(w, signatureRequired: true);
        var s = await StartAsync(w, code);
        var signatureA = await SignAsync(w, w.B, s);
        var signatureB = await SignAsync(w, w.B, s);
        var key = Key();

        var approved = await ApproveAsync(w, w.B, s.TaskId, key, signatureA);
        Assert.Equal(200, approved.Status);

        var differentSignature = await ApproveAsync(w, w.B, s.TaskId, key, signatureB);
        var sameBody = await ApproveAsync(w, w.B, s.TaskId, key, signatureA);

        // the replay answers before the signature gate: the gate would say SIGNATURE_*, this is the distinct body conflict
        Assert.Equal((409, WorkflowReasonCodes.WorkflowTransitionIdempotencyConflict), (differentSignature.Status, differentSignature.Reason));
        Assert.Equal(200, sameBody.Status);
        Assert.True(Data(sameBody.Body).GetProperty("isIdempotent").GetBoolean(), sameBody.Body);
        var rows = await Collection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs)
            .Find(l => l.ApprovalTaskId == s.TaskId && l.Action == WorkflowTransitionAction.Approve && l.IdempotencyKey == key).ToListAsync();
        var row = Assert.Single(rows);                                // no new row
        Assert.Equal(signatureA.ToString("D"), row.SignatureId);      // the first body is the one kept
    }
}
