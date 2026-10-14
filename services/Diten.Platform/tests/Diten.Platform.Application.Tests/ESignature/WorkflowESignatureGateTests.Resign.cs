using System.Net;
using System.Net.Http.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E5B (FU-0022-E4-01, CT-D16) — a workflow task can be RE-SIGNED after its signature was invalidated or carried
/// the wrong meaning. The per-task unique index is gone (each test below was red before: the second signature for the task
/// answered 409 at signing); "one signature binds one approval" is the E4 gate alone — context = this task, single use on
/// the transition log — and a task is approved once. So a task may hold several signatures, at most one of them BOUND.
/// </summary>
public sealed partial class WorkflowESignatureGateTests
{
    private async Task InvalidateAsync(World w, Guid signer, Guid signature)
    {
        using var client = _host.Client(w.Token(signer));
        var invalidated = await client.PostAsJsonAsync($"{Signatures}/{signature}/invalidate",
            new { ticket = _host.Tickets.Issue(w.Tenant, signer, "invalidate"), reason = "Signed the wrong decision by mistake", idempotencyKey = Key() });
        Assert.Equal(HttpStatusCode.Created, invalidated.StatusCode);
    }

    private async Task<List<WorkflowTransitionLog>> ApproveLogsAsync(Guid taskId) =>
        await _host.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs)
            .Find(l => l.ApprovalTaskId == taskId && l.Action == WorkflowTransitionAction.Approve).ToListAsync();

    private async Task<long> TaskSignatureCountAsync(World w, Guid taskId) =>
        await _host.Database.GetCollection<ESignatureRecord>(PlatformCollections.ESignatureRecords)
            .CountDocumentsAsync(Builders<ESignatureRecord>.Filter.Eq(r => r.TenantId, w.Tenant)
                                 & Builders<ESignatureRecord>.Filter.Eq("Context.workflowTaskId", taskId.ToString("D")));

    [Fact]
    public async Task E5B_after_an_invalidated_signature_the_task_is_re_signed_and_approves()
    {
        var (w, s) = await FlaggedAsync();
        var first = await SignAsync(w, w.B, s);
        await InvalidateAsync(w, w.B, first);

        var second = await SignAsync(w, w.B, s); // 201 — was 409 ESIGN_SIGNATURE_MISMATCH at signing before E5B

        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, first)); // invalidated: never binds
        Assert.Equal("WaitingApproval", await TaskStatusAsync(s.TaskId));
        Assert.Equal(200, (await ApproveAsync(w, w.B, s.TaskId, second)).Status);
        Assert.Equal("Approved", await TaskStatusAsync(s.TaskId));
        Assert.Equal(second.ToString("D"), (await ApproveLogsAsync(s.TaskId)).Single().SignatureId);
        Assert.Equal(2, await TaskSignatureCountAsync(w, s.TaskId)); // nothing deleted: both records stay
    }

    [Fact]
    public async Task E5B_after_a_wrongly_meant_signature_the_task_is_re_signed_with_the_right_meaning_and_approves()
    {
        var w = NewWorld();
        var s = await StartAsync(w, await TemplateAsync(w, Definition(meaning: "RELEASE")));
        var wrong = await SignAsync(w, w.B, s, meaning: "APPROVE");
        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, wrong));

        var right = await SignAsync(w, w.B, s, meaning: "RELEASE"); // 201 — was 409 at signing before E5B

        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, s.TaskId, wrong)); // still the wrong meaning
        Assert.Equal(200, (await ApproveAsync(w, w.B, s.TaskId, right)).Status);
        Assert.Equal(right.ToString("D"), (await ApproveLogsAsync(s.TaskId)).Single().SignatureId);
    }

    /// <summary>
    /// Two valid signatures for one task, signed IN PARALLEL: both are recorded; one approval binds one of them; the other
    /// cannot approve the task a second time. One task → one bound signature.
    /// </summary>
    [Fact]
    public async Task E5B_two_parallel_valid_signatures_for_one_task_are_recorded_and_only_one_binds()
    {
        var (w, s) = await FlaggedAsync();
        var signed = await Task.WhenAll(SignAsync(w, w.B, s), SignAsync(w, w.B, s)); // both 201 (one was 409 before E5B)
        Assert.NotEqual(signed[0], signed[1]);

        Assert.Equal(200, (await ApproveAsync(w, w.B, s.TaskId, signed[0])).Status);
        Assert.Equal(409, (await ApproveAsync(w, w.B, s.TaskId, signed[1])).Status); // the task is decided
        Assert.Equal(signed[0].ToString("D"), (await ApproveLogsAsync(s.TaskId)).Single().SignatureId);
        Assert.Equal(2, await TaskSignatureCountAsync(w, s.TaskId));
    }

    /// <summary>
    /// Single use is what keeps "one signature → one approval" once a task may hold several signatures: of two valid
    /// signatures for task T, the one already presented on another approval (an unflagged step stores any signatureId it is
    /// given) cannot approve T — the other one can. K3 (a): without the gate's transition-log check the used one approves.
    /// </summary>
    [Fact]
    public async Task E5B_of_two_valid_signatures_the_one_already_used_elsewhere_is_refused_and_its_sibling_approves()
    {
        var w = NewWorld();
        var flagged = await TemplateAsync(w);
        var plain = await TemplateAsync(w, Definition(signatureRequired: false));
        var task = await StartAsync(w, flagged);
        var elsewhere = await StartAsync(w, plain);
        var used = await SignAsync(w, w.B, task);
        var sibling = await SignAsync(w, w.B, task); // 201 — was 409 at signing before E5B

        Assert.Equal(200, (await ApproveAsync(w, w.B, elsewhere.TaskId, used)).Status);

        Assert.Equal((409, ESignCodes.SignatureMismatch), await ApproveAsync(w, w.B, task.TaskId, used));
        Assert.Equal("WaitingApproval", await TaskStatusAsync(task.TaskId));
        Assert.Equal(200, (await ApproveAsync(w, w.B, task.TaskId, sibling)).Status);
        Assert.Equal(sibling.ToString("D"), (await ApproveLogsAsync(task.TaskId)).Single().SignatureId);
    }

    /// <summary>Two approvals of one task racing with its two valid signatures: exactly one wins, one approve log.</summary>
    [Fact]
    public async Task E5B_parallel_approvals_with_two_valid_signatures_approve_the_task_once()
    {
        var (w, s) = await FlaggedAsync();
        var first = await SignAsync(w, w.B, s);
        var second = await SignAsync(w, w.B, s);

        var results = await Task.WhenAll(ApproveAsync(w, w.B, s.TaskId, first), ApproveAsync(w, w.B, s.TaskId, second));

        Assert.Equal(1, results.Count(r => r.Status == 200));
        Assert.Equal(1, results.Count(r => r.Status == 409));
        var log = (await ApproveLogsAsync(s.TaskId)).Single();
        Assert.Contains(log.SignatureId, new[] { first.ToString("D"), second.ToString("D") });
        Assert.Equal("Approved", await TaskStatusAsync(s.TaskId));
    }
}
