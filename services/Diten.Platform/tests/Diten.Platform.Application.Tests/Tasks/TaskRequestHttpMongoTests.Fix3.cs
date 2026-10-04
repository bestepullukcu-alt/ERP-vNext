using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 (CT-TREQ-FIX3) — on the wire, over the real store, the PRODUCTION write gate and the
/// PRODUCTION read rule:
/// <list type="bullet">
/// <item>blocker 1 — a write decided on an older read never replaces a newer document (the edit against the accept, the
/// second claim against the first);</item>
/// <item>blocker 2 — the person a request waits on answers it and writes nothing else, delete and every transition
/// included;</item>
/// <item>item 8 — a reader by watching, or by being the person a waiting task asks, reads, comments and answers, and
/// changes or administers nothing;</item>
/// <item>item 9 — the legitimate flows through the real gate (answering, claiming, a manager's edit, completing, the
/// sender's comment), so a gate that refused ordinary work could not hide behind <c>TaskWriteGateDoubles.Open</c>;</item>
/// <item>item 5 — the rules that had no test of their own (the predecessor's read gate, the decline's version check, the
/// kinds the delete, comment-edit and attachment handlers ask for).</item>
/// </list>
/// </summary>
public sealed partial class TaskRequestHttpMongoTests
{
    // ── blocker 1 — the write is about the document the decision was made on ────────────────────────────────────────

    /// <summary>MUTATION TARGET (TaskItemRepository.UpdateAsync, the read version). The sender's edit is decided on the
    /// pending request (version N) but names N+1; right before it writes, the person asked accepts and the document
    /// becomes N+1. The edit must not replace the accepted request with the one it read — holder, addressee and unit
    /// would all go back to "nobody accepted".</summary>
    [Fact]
    public async Task An_edit_decided_on_an_older_read_cannot_overwrite_a_newer_accept()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        var version = await VersionAsync(id);

        _host.Hooks.When = task => task.Id == id && task.Title == "Deniz'in yeni başlığı";
        _host.Hooks.BeforeUpdate = async _ => Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = version })).Status);

        var edit = await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBody(version + 1, "Deniz'in yeni başlığı"));
        var acceptLandedInBetween = _host.Hooks.BeforeUpdate is null;
        _host.Hooks.BeforeUpdate = null;
        _host.Hooks.When = null;

        Assert.True(acceptLandedInBetween, "non-vacuity: the accept must land between the edit's read and its write");
        Assert.Equal((HttpStatusCode.Conflict, TaskReasonCodes.ConcurrencyConflict), (edit.Status, ReasonCode(edit.Body)));
        var stored = await StoredAsync(id);
        Assert.Equal(((Guid?)Ewa, (Guid?)null, _unitPl), (stored.AssigneeUserId, stored.OfferedToUserId, stored.OrganizationUnitId));
        Assert.NotEqual("Deniz'in yeni başlığı", stored.Title);
    }

    /// <summary>MUTATION TARGET (TaskItemRepository.UpdateAsync, the read version) — a second caller of it. Piotr's claim
    /// reads the unclaimed pool task (N) and names N+1; right before it writes, Ewa claims (N+1). The second claim is a
    /// controlled 409 and the first holder stays.</summary>
    [Fact]
    public async Task A_claim_decided_on_an_older_read_cannot_steal_a_newer_claim()
    {
        var (id, version) = await PoolTaskAsync();
        var ewa = Token(Ewa, UserKeys);
        var piotr = Token(Piotr, UserKeys);

        _host.Hooks.When = task => task.Id == id && task.AssigneeUserId == Piotr;
        _host.Hooks.BeforeUpdate = async _ => Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/claim", ewa, new { expectedVersion = version })).Status);

        var second = await _host.PostAsync($"/api/v1/tasks/{id}/claim", piotr, new { expectedVersion = version + 1 });
        var firstClaimLandedInBetween = _host.Hooks.BeforeUpdate is null;
        _host.Hooks.BeforeUpdate = null;
        _host.Hooks.When = null;

        Assert.True(firstClaimLandedInBetween, "non-vacuity: Ewa's claim must land between Piotr's read and his write");
        Assert.Equal((HttpStatusCode.Conflict, TaskReasonCodes.AlreadyClaimed), (second.Status, ReasonCode(second.Body)));
        Assert.Equal((Guid?)Ewa, (await StoredAsync(id)).AssigneeUserId);
        Assert.Equal(Ewa, Assert.Single(await HistoryAsync(id), row => row.Kind == TaskTransitionKind.Claimed).ActorUserId);
    }

    // ── blocker 2 — the person asked answers, and does nothing else ─────────────────────────────────────────────────

    /// <summary>MUTATION TARGET (the gate's "answer first" for every kind but Personal). Holding the delete key, the
    /// person asked could make the request vanish — no decline, no reason. The single delete, the delete-key cancel
    /// and the bulk delete all leave it standing.</summary>
    [Fact]
    public async Task The_person_asked_holding_the_delete_key_cannot_make_the_request_vanish()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        var ewa = Token(Ewa, OutsiderKeys);
        var version = await VersionAsync(id);

        var deleted = await _host.DeleteAsync($"/api/v1/tasks/{id}", ewa);
        var cancelled = await _host.PostAsync($"/api/v1/tasks/{id}/cancel", ewa, new { expectedVersion = version });
        var bulk = await _host.PostAsync("/api/v1/tasks/bulk-delete", ewa, new { ids = new[] { id } });

        Assert.Equal((HttpStatusCode.Conflict, (string?)TaskReasonCodes.RequestNotAccepted), Pair(deleted));
        Assert.Equal((HttpStatusCode.Conflict, (string?)TaskReasonCodes.RequestNotAccepted), Pair(cancelled));
        Assert.Equal(HttpStatusCode.NoContent, bulk.Status);           // the list's answer names no task; this one was skipped
        var stored = await TasksIn(_harness.TenantId).GetByIdAsync(id);
        Assert.NotNull(stored);
        Assert.Equal((version, TaskLifecycle.Open, (Guid?)Ewa), (stored!.Version, stored.Lifecycle, stored.OfferedToUserId));
        Assert.DoesNotContain(await HistoryAsync(id), row => row.Kind is TaskTransitionKind.Cancelled or TaskTransitionKind.Declined);

        // Non-vacuity: the same bulk delete, by the sender, does delete.
        Assert.Equal(HttpStatusCode.NoContent, (await _host.PostAsync("/api/v1/tasks/bulk-delete",
            Token(Deniz, OutsiderKeys), new { ids = new[] { id } })).Status);
        Assert.Null(await TasksIn(_harness.TenantId).GetByIdAsync(id));
    }

    /// <summary>MUTATION TARGET (the same rule, every write). Every write endpoint of the request — the transitions the
    /// gate asks as Administer among them, which used to answer 403 — answers the person asked with one 409; only the
    /// two answers and their own layer of the task (pin, snooze, a private note) stay open.</summary>
    [Fact]
    public async Task The_person_asked_reaches_no_write_but_the_answer_and_their_own_view()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        var other = await OwnTaskAsync();
        var ewa = Token(Ewa, OutsiderKeys);
        var version = await VersionAsync(id);
        var open = new HashSet<string> { "accept", "decline", "comment-withdraw", "personal-note", "snooze", "pin" };

        var asked = 0;
        foreach (var (name, call) in EveryWrite(id, ewa, version, other).Where(write => !open.Contains(write.Name)))
        {
            var answer = await call();
            Assert.True(answer.Status == HttpStatusCode.Conflict && ReasonCode(answer.Body) == TaskReasonCodes.RequestNotAccepted,
                $"{name}: {answer.Status} {answer.Body}");
            asked++;
        }

        Assert.True(asked >= 25, $"non-vacuity: {asked} writes asked");
        Assert.Equal(version, await VersionAsync(id));
        Assert.True((await _host.PutAsync($"/api/v1/tasks/{id}/personal/pin", ewa, new { pinned = true })).Status
            is HttpStatusCode.OK or HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = version })).Status);
    }

    // ── item 8 — a reader by watching or by being asked reads, comments and answers ─────────────────────────────────

    /// <summary>MUTATION TARGET (the gate's observer rule). Deniz watches Ewa's task and holds every write key: he reads
    /// it and comments on it, and changes, cancels, deletes and extends nothing.</summary>
    [Fact]
    public async Task A_watcher_holding_every_write_key_reads_and_comments_but_changes_and_administers_nothing()
    {
        var id = await SelfAssignedTaskAsync(Ewa);
        await _host.Watchers.CreateAsync(new TaskWatcher { TenantId = _harness.TenantId, TaskItemId = id, UserId = Deniz });
        var deniz = Token(Deniz, OutsiderKeys);
        var before = await StoredAsync(id);
        var version = before.Version;
        var predecessor = await OwnTaskAsync();
        Assert.Equal(HttpStatusCode.OK, (await _host.GetAsync($"/api/v1/tasks/{id}", deniz)).Status);   // he reads it

        var refused = new (string Name, (HttpStatusCode Status, string Body) Answer)[]
        {
            ("edit", await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBody(version, "İzleyicinin başlığı"))),
            ("delete", await _host.DeleteAsync($"/api/v1/tasks/{id}", deniz)),
            ("cancel", await _host.PostAsync($"/api/v1/tasks/{id}/cancel", deniz, new { expectedVersion = version })),
            ("checklist-add", await _host.PostAsync($"/api/v1/tasks/{id}/checklist/items", deniz,
                new { text = "İzleyicinin adımı", requirement = "Optional", expectedVersion = 0 })),
            ("dependency-add", await _host.PostAsync($"/api/v1/tasks/{id}/dependencies", deniz,
                new { dependsOnTaskItemId = predecessor, dependencyType = "FinishToStart" })),
            ("subtask", await _host.PostAsync("/api/v1/tasks", deniz,
                CreateBodyWith(null, parent: id, target: TaskAssignmentTarget.SelfAssigned)))
        };
        foreach (var (name, answer) in refused)
        {
            Assert.True(answer.Status == HttpStatusCode.Forbidden
                        && ReasonCode(answer.Body) == DocumentManagementReasonCodes.PermissionDenied,
                $"{name}: {answer.Status} {answer.Body}");
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync("/api/v1/tasks/bulk-delete", deniz, new { ids = new[] { id } })).Status);
        Assert.Equal(HttpStatusCode.Created,
            (await _host.PostAsync($"/api/v1/tasks/{id}/comments", deniz, new { text = "Takipteyim." })).Status);
        Assert.True((await _host.PutAsync($"/api/v1/tasks/{id}/personal/pin", deniz, new { pinned = true })).Status
            is HttpStatusCode.OK or HttpStatusCode.NoContent);

        var stored = await TasksIn(_harness.TenantId).GetByIdAsync(id);
        Assert.NotNull(stored);
        Assert.Equal((before.Version, before.Lifecycle, before.Title), (stored!.Version, stored.Lifecycle, stored.Title));
        Assert.Empty(_host.Dependencies.Edges);
        Assert.Equal(2, (await AllTasksAsync(_harness.TenantId)).Count);      // no subtask, nothing deleted
    }

    /// <summary>MUTATION TARGET (the observer rule, and the answer as a Comment-kind write). Ewa asks Deniz — who reads her
    /// task only because it is asking him — a question: he answers it and comments on it (item 9: the person asked
    /// answering through the REAL gate), and edits or cancels nothing.</summary>
    [Fact]
    public async Task The_person_a_waiting_task_asks_answers_and_comments_but_changes_nothing()
    {
        var ewa = Token(Ewa, UserKeys);
        var id = await SelfAssignedTaskAsync(Ewa);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.PostAsync($"/api/v1/tasks/{id}/inquire", ewa,
            new { expectedVersion = await VersionAsync(id), reason = "Deniz'in görüşü lazım.", waitingOnUserId = Deniz })).Status);
        var deniz = Token(Deniz, OutsiderKeys);
        var version = await VersionAsync(id);

        var edit = await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBody(version, "Sorulanın başlığı"));
        var cancel = await _host.PostAsync($"/api/v1/tasks/{id}/cancel", deniz, new { expectedVersion = version });
        Assert.Equal((HttpStatusCode.Forbidden, (string?)DocumentManagementReasonCodes.PermissionDenied), Pair(edit));
        Assert.Equal((HttpStatusCode.Forbidden, (string?)DocumentManagementReasonCodes.PermissionDenied), Pair(cancel));

        Assert.Equal(HttpStatusCode.Created,
            (await _host.PostAsync($"/api/v1/tasks/{id}/comments", deniz, new { text = "Bakıyorum." })).Status);
        var answer = await _host.PostAsync($"/api/v1/tasks/{id}/answer", deniz,
            new { expectedVersion = version, answer = "Uygun görüyorum." });
        Assert.True(answer.Status == HttpStatusCode.NoContent, answer.Body);

        var stored = await StoredAsync(id);
        Assert.NotEqual(TaskLifecycle.Waiting, stored.Lifecycle);
        Assert.NotEqual("Sorulanın başlığı", stored.Title);
        Assert.Equal(Deniz, Assert.Single(await HistoryAsync(id), row => row.Kind == TaskTransitionKind.InquiryAnswered).ActorUserId);
    }

    // ── item 9 — the legitimate flows, through the real gate ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pool_member_claims_pool_work_through_the_real_gate()
    {
        var (id, version) = await PoolTaskAsync();

        var claimed = await _host.PostAsync($"/api/v1/tasks/{id}/claim", Token(Piotr, UserKeys), new { expectedVersion = version });

        Assert.Equal(HttpStatusCode.NoContent, claimed.Status);
        Assert.Equal((Guid?)Piotr, (await StoredAsync(id)).AssigneeUserId);
    }

    /// <summary>Marek reads the task only because his subordinate holds it (the team leg — it is filed in TR, outside his
    /// scope), and that is a FULL footing: he edits it.</summary>
    [Fact]
    public async Task A_manager_edits_a_task_his_subordinate_holds_outside_his_scope_through_the_real_gate()
    {
        var task = new TaskItem
        {
            TenantId = _harness.TenantId, Title = "Ewa'nın TR işi", AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = Ewa, CreatedByUserId = Deniz, OrganizationUnitId = _unitTr, Lifecycle = TaskLifecycle.InProgress,
            CreatedBy = "fix3"
        };
        await TasksIn(_harness.TenantId).CreateAsync(task);
        // Non-vacuity: not through the unit — Piotr sits beside Marek, has no team, and does not read it.
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/tasks/{task.Id}", Token(Piotr, UserKeys))).Status);

        var edit = await _host.PutAsync($"/api/v1/tasks/{task.Id}", Token(Marek, UserKeys),
            EditBody(await VersionAsync(task.Id), "Marek düzeltti"));

        Assert.Equal(HttpStatusCode.NoContent, edit.Status);
        Assert.Equal("Marek düzeltti", (await StoredAsync(task.Id)).Title);
    }

    [Fact]
    public async Task The_assignee_completes_their_task_through_the_real_gate()
    {
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) })).Status);

        var completed = await _host.PostAsync($"/api/v1/tasks/{id}/complete", ewa, new { expectedVersion = await VersionAsync(id) });

        Assert.Equal(HttpStatusCode.NoContent, completed.Status);
        Assert.Equal(TaskLifecycle.Done, (await StoredAsync(id)).Lifecycle);
    }

    /// <summary>MUTATION TARGET (G5 — the gate refuses the sender of an accepted request a CHANGE only). They watch and
    /// comment: a comment, and an edit of their own comment, both land; the same person's edit of the task does not.</summary>
    [Fact]
    public async Task The_sender_of_an_accepted_request_comments_on_it_and_edits_their_own_comment()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", Token(Ewa, UserKeys), new { expectedVersion = await VersionAsync(id) });

        var commentId = await CommentAsync(id, deniz, "Nasıl gidiyor?");
        var reworded = await _host.PutAsync($"/api/v1/tasks/{id}/comments/{commentId}", deniz, new { text = "Nasıl gidiyor, yardım lazım mı?" });

        Assert.Equal(HttpStatusCode.NoContent, reworded.Status);
        Assert.Equal("Nasıl gidiyor, yardım lazım mı?", Assert.Single(_host.Comments.Comments).Text);
        var edit = await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBody(await VersionAsync(id), "Gönderenin başlığı"));
        Assert.Equal((HttpStatusCode.Forbidden, (string?)TaskReasonCodes.RequestSenderCannotDirect), Pair(edit));
    }

    // ── item 5 — the kinds the handlers ask for, each pinned ────────────────────────────────────────────────────────

    /// <summary>MUTATION TARGET (TaskItemWriteHandlers — delete and bulk delete ask ADMINISTER, not Change). The delete key
    /// is a manager's verb: the sender of an accepted request who holds it may still delete it, one by one or in bulk
    /// (the watcher's refusal, the other half of "Administer", is pinned above).</summary>
    [Fact]
    public async Task The_sender_holding_the_delete_key_may_delete_an_accepted_request_one_by_one_or_in_bulk()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var one = await SendRequestAsync(deniz, Ewa);
        var two = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{one}/accept", ewa, new { expectedVersion = await VersionAsync(one) });
        await _host.PostAsync($"/api/v1/tasks/{two}/accept", ewa, new { expectedVersion = await VersionAsync(two) });
        var admin = Token(Deniz, [.. SenderKeys, TaskPermissions.Delete, TaskPermissions.BulkDelete]);

        Assert.Equal(HttpStatusCode.NoContent, (await _host.DeleteAsync($"/api/v1/tasks/{one}", admin)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.PostAsync("/api/v1/tasks/bulk-delete", admin, new { ids = new[] { two } })).Status);

        Assert.Null(await TasksIn(_harness.TenantId).GetByIdAsync(one));
        Assert.Null(await TasksIn(_harness.TenantId).GetByIdAsync(two));
    }

    /// <summary>MUTATION TARGET (TaskCommentHandlers — editing a comment asks COMMENT, withdrawing it PERSONAL). Burak
    /// commented on the request while it was waiting on somebody else; then it is put to HIM. Before he answers he may
    /// not reword what he said (it would put his name in the feed again) — and may take it back.</summary>
    [Fact]
    public async Task The_person_asked_cannot_reword_a_comment_from_before_they_were_asked_but_may_withdraw_it()
    {
        var deniz = Token(Deniz, SenderKeys);
        var burak = Token(Burak, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        var commentId = await CommentAsync(id, burak, "Ben de bakabilirim.");       // he reads it through his unit
        await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys), new { expectedVersion = await VersionAsync(id), reason = "Olmaz." });
        Assert.Equal(HttpStatusCode.NoContent, (await _host.PostAsync($"/api/v1/tasks/{id}/offer", deniz,
            new { expectedVersion = await VersionAsync(id), offeredToUserId = Burak })).Status);

        var reworded = await _host.PutAsync($"/api/v1/tasks/{id}/comments/{commentId}", burak, new { text = "Aslında bakamam." });
        var withdrawn = await _host.DeleteAsync($"/api/v1/tasks/{id}/comments/{commentId}", burak);

        Assert.Equal((HttpStatusCode.Conflict, (string?)TaskReasonCodes.RequestNotAccepted), Pair(reworded));
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.Status);
        Assert.NotNull(Assert.Single(_host.Comments.Comments).WithdrawnAt);
    }

    /// <summary>MUTATION TARGET (TaskAttachmentHandlers.cs — a file write asks the gate for a CHANGE, and the gate is the
    /// only place the sender rule lives now). The person asked: 409; the sender of the accepted request: 403 by name;
    /// the holder passes both and reaches the attachment itself (there is none: 404).</summary>
    [Fact]
    public async Task A_file_write_is_a_change_of_the_task()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        var path = $"/api/v1/tasks/{id}/attachments/{Guid.NewGuid()}";

        var byTheAsked = await _host.DeleteAsync(path, ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) });
        var byTheSender = await _host.DeleteAsync(path, deniz);
        var byTheHolder = await _host.DeleteAsync(path, ewa);

        Assert.Equal((HttpStatusCode.Conflict, (string?)TaskReasonCodes.RequestNotAccepted), Pair(byTheAsked));
        Assert.Equal((HttpStatusCode.Forbidden, (string?)TaskReasonCodes.RequestSenderCannotDirect), Pair(byTheSender));
        Assert.Equal((HttpStatusCode.NotFound, (string?)TaskReasonCodes.AttachmentNotFound), Pair(byTheHolder));
    }

    /// <summary>MUTATION TARGET (G1 — TaskDependencyHandlers, the predecessor's read gate) over the REAL gate: a predecessor
    /// that exists but the caller cannot read answers byte for byte like one that does not exist, and nothing is
    /// stored; a readable one is linked.</summary>
    [Fact]
    public async Task A_predecessor_the_caller_cannot_read_answers_like_one_that_does_not_exist()
    {
        var ewa = Token(Ewa, UserKeys);
        var mine = await SelfAssignedTaskAsync(Ewa);
        var myOther = await SelfAssignedTaskAsync(Ewa);
        var hidden = await OwnTaskAsync();                                     // Deniz's, in TR: Ewa does not read it

        var toHidden = await _host.PostAsync($"/api/v1/tasks/{mine}/dependencies", ewa,
            new { dependsOnTaskItemId = hidden, dependencyType = "FinishToStart" });
        var toNothing = await _host.PostAsync($"/api/v1/tasks/{mine}/dependencies", ewa,
            new { dependsOnTaskItemId = Guid.NewGuid(), dependencyType = "FinishToStart" });

        Assert.Equal((HttpStatusCode.NotFound, (string?)TaskReasonCodes.DependencyTaskNotFound), Pair(toHidden));
        Assert.Equal(toNothing, toHidden);
        Assert.Empty(_host.Dependencies.Edges);

        var toMine = await _host.PostAsync($"/api/v1/tasks/{mine}/dependencies", ewa,
            new { dependsOnTaskItemId = myOther, dependencyType = "FinishToStart" });
        Assert.Equal(HttpStatusCode.Created, toMine.Status);
    }

    /// <summary>MUTATION TARGET (G2 — TaskRequestHandlers, the decline's version check). The decline is decided on the
    /// read version: a page one version stale is told "the task changed" BEFORE its reason is judged (read → addressee →
    /// pending → version → the reason).</summary>
    [Fact]
    public async Task A_decline_from_a_stale_page_is_told_the_task_changed_before_its_reason_is_judged()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        var version = await VersionAsync(id);

        var stale = await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys),
            new { expectedVersion = version + 1, reason = "   " });

        Assert.Equal((HttpStatusCode.Conflict, (string?)TaskReasonCodes.ConcurrencyConflict), Pair(stale));
        Assert.Equal((Guid?)Ewa, (await StoredAsync(id)).OfferedToUserId);
    }

    /// <summary>G2 — the decline against a withdraw that lands between its read and its write: whichever check catches
    /// it (the decline's own, or the store's), the decline does not land on the withdrawn request.</summary>
    [Fact]
    public async Task A_decline_decided_on_an_older_read_cannot_overwrite_a_newer_withdraw()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        var version = await VersionAsync(id);

        _host.Hooks.When = task => task.Id == id && task.OfferDeclineReason == "Geç kaldım.";
        _host.Hooks.BeforeUpdate = async _ => Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", deniz, new { expectedVersion = version })).Status);

        var decline = await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys),
            new { expectedVersion = version + 1, reason = "Geç kaldım." });
        _host.Hooks.BeforeUpdate = null;
        _host.Hooks.When = null;

        Assert.Equal((HttpStatusCode.Conflict, (string?)TaskReasonCodes.ConcurrencyConflict), Pair(decline));
        Assert.Null((await StoredAsync(id)).OfferDeclineReason);
        Assert.DoesNotContain(await HistoryAsync(id), row => row.Kind == TaskTransitionKind.Declined);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A task <paramref name="who"/> creates for themselves, filed under their own unit.</summary>
    private async Task<Guid> SelfAssignedTaskAsync(Guid who)
    {
        var created = await _host.PostAsync("/api/v1/tasks", Token(who, UserKeys),
            CreateBodyWith(null, target: TaskAssignmentTarget.SelfAssigned));
        Assert.True(created.Status == HttpStatusCode.Created, created.Body);
        using var document = JsonDocument.Parse(created.Body);
        return document.RootElement.GetProperty("data").GetGuid();
    }

    /// <summary>An unclaimed task pooled to a PL position Ewa and Piotr both hold (Marek's).</summary>
    private async Task<(Guid Id, int Version)> PoolTaskAsync()
    {
        var pool = await PositionAsync(_harness.TenantId, "PL-POOL", _unitPl);
        await SeatAsync(_harness.TenantId, Ewa, pool);
        await SeatAsync(_harness.TenantId, Piotr, pool);
        var task = await TasksIn(_harness.TenantId).CreateAsync(new TaskItem
        {
            TenantId = _harness.TenantId, Title = "Havuz işi", AssignmentTarget = TaskAssignmentTarget.PositionPool,
            PoolPositionId = pool, OrganizationUnitId = _unitPl, CreatedByUserId = Marek, Lifecycle = TaskLifecycle.Open,
            CreatedBy = "fix3"
        });
        return (task.Id, (await StoredAsync(task.Id)).Version);
    }

    private async Task<Guid> CommentAsync(Guid id, string bearer, string text)
    {
        var posted = await _host.PostAsync($"/api/v1/tasks/{id}/comments", bearer, new { text });
        Assert.True(posted.Status == HttpStatusCode.Created, posted.Body);
        using var document = JsonDocument.Parse(posted.Body);
        return document.RootElement.GetProperty("data").GetGuid();
    }
}
