using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX2 (P-2026-10-03-TREQ-FIX2) — THE WRITE GATE, on the wire, over the real store and the
/// PRODUCTION <c>TaskWriteGate</c>: nobody reaches a write on a task they cannot read (CT decision 2026-10-03); the
/// relationship rules of a request narrow it inside; and the request fixes of this round (A2–A5, the notes).
/// </summary>
public sealed partial class TaskRequestHttpMongoTests
{
    /// <summary>Every task key short of reading everything — what a "tasks.update holder elsewhere" carries.</summary>
    private static readonly string[] OutsiderKeys =
    [
        TaskPermissions.Read, TaskPermissions.Create, TaskPermissions.Update, TaskPermissions.Delete,
        TaskPermissions.BulkDelete, TaskPermissions.Assign, TaskPermissions.Claim, TaskPermissions.Complete,
        TaskPermissions.Cancel, TaskPermissions.RequestsCreate
    ];

    // ── 1 · A1 + A4 — whoever cannot read a task reaches none of its writes ──────────────────────────────────────

    [Fact]
    public async Task Whoever_cannot_read_a_task_reaches_none_of_its_writes_and_nothing_is_written()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await OwnTaskAsync();
        var other = await OwnTaskAsync();
        // REAL things to aim at — a comment, a checklist step, a predecessor — so that a bypassed gate would reach
        // them (and answer something other than "not found").
        var commented = await _host.PostAsync($"/api/v1/tasks/{id}/comments", deniz, new { text = "Deniz'in notu." });
        using var commentDocument = JsonDocument.Parse(commented.Body);
        var commentId = commentDocument.RootElement.GetProperty("data").GetGuid();
        var stepAdded = await _host.PostAsync($"/api/v1/tasks/{id}/checklist/items", deniz,
            new { text = "Deniz'in adımı", requirement = "Optional", expectedVersion = 0 });
        Assert.True(stepAdded.Status is HttpStatusCode.NoContent or HttpStatusCode.OK, stepAdded.Body);
        var run = (await ChecklistsIn(_harness.TenantId).GetByTaskIdAsync(id))!;
        var step = Assert.Single(run.Items);
        var edgeAdded = await _host.PostAsync($"/api/v1/tasks/{id}/dependencies", deniz,
            new { dependsOnTaskItemId = other, dependencyType = "FinishToStart" });
        Assert.True(edgeAdded.Status is HttpStatusCode.Created or HttpStatusCode.OK, edgeAdded.Body);
        var edge = Assert.Single(_host.Dependencies.Edges);
        var before = await StoredAsync(id);
        var piotr = Token(Piotr, OutsiderKeys);

        foreach (var (name, call) in EveryWrite(id, piotr, before.Version, other, commentId, step.Code, run.Version, edge.Id))
        {
            var answer = await call();
            Assert.True(answer.Status == HttpStatusCode.NotFound && ReasonCode(answer.Body) == TaskReasonCodes.NotFound,
                $"{name}: {answer.Status} {answer.Body}");
        }

        // A bulk delete answers for the whole list and skips what the caller cannot read — it names no task.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync("/api/v1/tasks/bulk-delete", piotr, new { ids = new[] { id } })).Status);

        // A subtask under it reads exactly like a subtask under a parent that does not exist.
        var underIt = await _host.PostAsync("/api/v1/tasks", piotr, CreateBodyWith(null, parent: id, target: TaskAssignmentTarget.SelfAssigned));
        var underNothing = await _host.PostAsync("/api/v1/tasks", piotr, CreateBodyWith(null, parent: Guid.NewGuid(), target: TaskAssignmentTarget.SelfAssigned));
        Assert.Equal(TaskReasonCodes.ParentTaskNotFound, ReasonCode(underIt.Body));
        Assert.Equal((underNothing.Status, ReasonCode(underNothing.Body)), (underIt.Status, ReasonCode(underIt.Body)));

        var after = await StoredAsync(id);
        Assert.Equal((before.Version, before.Lifecycle, before.Title, before.AssigneeUserId),
            (after.Version, after.Lifecycle, after.Title, after.AssigneeUserId));
        Assert.Equal("Deniz'in notu.", Assert.Single(_host.Comments.Comments).Text);
        Assert.Empty(_host.Overlays.Overlays);
        Assert.Equal(edge.Id, Assert.Single(_host.Dependencies.Edges).Id);
        var stepAfter = Assert.Single((await ChecklistsIn(_harness.TenantId).GetByTaskIdAsync(id))!.Items);
        Assert.Equal(("Deniz'in adımı", false), (stepAfter.LabelText, stepAfter.Completed));
        Assert.Equal(2, (await AllTasksAsync(_harness.TenantId)).Count);   // nothing deleted, no subtask created
    }

    /// <summary>For whoever cannot read it, a pending request answers byte for byte like an ordinary task: no
    /// <c>TASK_REQUEST_UNIT_FIXED</c>, no 403 on accept — nothing tells them it is a request, or that it exists.</summary>
    [Fact]
    public async Task For_whoever_cannot_read_it_a_waiting_request_answers_exactly_like_an_ordinary_task()
    {
        var deniz = Token(Deniz, SenderKeys);
        var request = await SendRequestAsync(deniz, Burak);
        var ordinary = await OwnTaskAsync();
        var piotr = Token(Piotr, OutsiderKeys);

        async Task<(HttpStatusCode, string?)[]> AnswersAsync(Guid id) =>
        [
            Pair(await _host.PutAsync($"/api/v1/tasks/{id}", piotr, EditBodyWith(await VersionAsync(id), unit: _unitPl))),
            Pair(await _host.PostAsync($"/api/v1/tasks/{id}/accept", piotr, new { expectedVersion = await VersionAsync(id) })),
            Pair(await _host.PostAsync($"/api/v1/tasks/{id}/cancel", piotr, new { expectedVersion = await VersionAsync(id) })),
            Pair(await _host.PostAsync($"/api/v1/tasks/{id}/comments", piotr, new { text = "?" })),
            Pair(await _host.PostAsync($"/api/v1/tasks/{id}/checklist/items", piotr, new { text = "x", requirement = "Optional", expectedVersion = 0 }))
        ];

        var forRequest = await AnswersAsync(request);
        Assert.Equal(await AnswersAsync(ordinary), forRequest);
        Assert.All(forRequest, answer => Assert.Equal((HttpStatusCode.NotFound, (string?)TaskReasonCodes.NotFound), answer));
    }

    [Fact]
    public async Task The_person_who_declined_reaches_no_write_afterwards()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, OutsiderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        Assert.Equal(HttpStatusCode.NoContent, (await _host.PostAsync($"/api/v1/tasks/{id}/decline", ewa,
            new { expectedVersion = await VersionAsync(id), reason = "Kapasitem yok." })).Status);

        var comment = await _host.PostAsync($"/api/v1/tasks/{id}/comments", ewa, new { text = "Yine de bakarım." });
        var edit = await _host.PutAsync($"/api/v1/tasks/{id}", ewa, EditBody(await VersionAsync(id), "Benim başlığım"));
        var subtask = await _host.PostAsync("/api/v1/tasks", ewa, CreateBodyWith(null, parent: id, target: TaskAssignmentTarget.SelfAssigned));

        Assert.Equal((HttpStatusCode.NotFound, TaskReasonCodes.NotFound), (comment.Status, ReasonCode(comment.Body)));
        Assert.Equal((HttpStatusCode.NotFound, TaskReasonCodes.NotFound), (edit.Status, ReasonCode(edit.Body)));
        Assert.Equal(TaskReasonCodes.ParentTaskNotFound, ReasonCode(subtask.Body));
        Assert.Empty(_host.Comments.Comments);
        Assert.Single(await AllTasksAsync(_harness.TenantId));
    }

    /// <summary>The person a request waits on READS it in order to answer — every write before the answer is a coded
    /// 409 (and the comment is one of them: it would put their name in the sender's colleagues' feed).</summary>
    [Fact]
    public async Task The_person_asked_writes_nothing_before_answering()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, OutsiderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        var version = await VersionAsync(id);

        var answers = new[]
        {
            await _host.PutAsync($"/api/v1/tasks/{id}", ewa, EditBody(version, "Benim başlığım")),
            await _host.PostAsync($"/api/v1/tasks/{id}/comments", ewa, new { text = "Bir sorum var." }),
            await _host.PostAsync($"/api/v1/tasks/{id}/checklist/items", ewa, new { text = "Adım", requirement = "Optional", expectedVersion = 0 }),
            await _host.PostAsync($"/api/v1/tasks/{id}/dependencies", ewa, new { dependsOnTaskItemId = id, dependencyType = "FinishToStart" })
        };

        Assert.All(answers, answer => Assert.Equal((HttpStatusCode.Conflict, TaskReasonCodes.RequestNotAccepted),
            (answer.Status, ReasonCode(answer.Body))));
        Assert.Empty(_host.Comments.Comments);
        Assert.Equal(version, await VersionAsync(id));

        // Answering is open to them.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = version })).Status);
    }

    // ── 2 · A5 — a decision built on an older read never overwrites a newer write ───────────────────────────────

    [Fact]
    public async Task A_withdraw_decided_on_an_older_read_cannot_overwrite_a_newer_accept()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        var version = await VersionAsync(id);

        // Barrier: the moment the withdraw would write, the person asked accepts — the withdraw's own write then meets
        // the accepted document at the version its CLIENT named.
        _host.Hooks.When = task => task.Id == id && task.OfferedToUserId is null && task.AssigneeUserId is null;
        _host.Hooks.BeforeUpdate = async _ => Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = version })).Status);

        var withdraw = await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", deniz, new { expectedVersion = version + 1 });
        _host.Hooks.BeforeUpdate = null;
        _host.Hooks.When = null;

        Assert.Equal((HttpStatusCode.Conflict, TaskReasonCodes.ConcurrencyConflict), (withdraw.Status, ReasonCode(withdraw.Body)));
        var stored = await StoredAsync(id);
        Assert.True(stored.AssigneeUserId == Ewa || stored.OfferedToUserId == Ewa,
            $"the request was overwritten: holder {stored.AssigneeUserId}, asked {stored.OfferedToUserId}");
        Assert.DoesNotContain(await HistoryAsync(id), row => row.Kind == TaskTransitionKind.Withdrawn);
    }

    [Fact]
    public async Task An_accept_decided_on_an_older_read_cannot_overwrite_a_newer_withdraw()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        var version = await VersionAsync(id);

        _host.Hooks.When = task => task.Id == id && task.AssigneeUserId == Ewa;
        _host.Hooks.BeforeUpdate = async _ => Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", deniz, new { expectedVersion = version })).Status);

        var accept = await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = version + 1 });
        _host.Hooks.BeforeUpdate = null;
        _host.Hooks.When = null;

        Assert.Equal((HttpStatusCode.Conflict, TaskReasonCodes.ConcurrencyConflict), (accept.Status, ReasonCode(accept.Body)));
        Assert.Null((await StoredAsync(id)).AssigneeUserId);
    }

    // ── 3 · A3 — who was asked, who said no, who was taken back: the sender's feed only ─────────────────────────

    [Fact]
    public async Task After_a_new_offer_the_senders_colleague_sees_no_offered_declined_or_withdrawn_row()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", deniz, new { expectedVersion = await VersionAsync(id) });
        await _host.PostAsync($"/api/v1/tasks/{id}/offer", deniz, new { expectedVersion = await VersionAsync(id), offeredToUserId = Piotr });
        await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Piotr, UserKeys),
            new { expectedVersion = await VersionAsync(id), reason = "Bu ay olmaz." });

        var forBurak = await EverythingAsync(id, Token(Burak, UserKeys));
        Assert.NotEmpty(forBurak);                                     // non-vacuity: Burak reads the task
        foreach (var row in new[] { "offered", "declined", "withdrawn" })
        {
            Assert.DoesNotContain($"\"code\":\"{row}\"", forBurak);
        }

        var forDeniz = await EverythingAsync(id, deniz);
        foreach (var row in new[] { "offered", "declined", "withdrawn" })
        {
            Assert.Contains($"\"code\":\"{row}\"", forDeniz);
        }
    }

    // ── 4 · A2 — the sender keeps their request's history after it is handed back ───────────────────────────────

    [Fact]
    public async Task The_sender_keeps_the_decline_history_after_the_request_is_handed_back()
    {
        const string Reason = "Ewa'nın gerekçesi: kapasite.";
        var deniz = Token(Deniz, SenderKeys);
        var piotr = Token(Piotr, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys), new { expectedVersion = await VersionAsync(id), reason = Reason });
        await _host.PostAsync($"/api/v1/tasks/{id}/offer", deniz, new { expectedVersion = await VersionAsync(id), offeredToUserId = Piotr });
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", piotr, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.NoContent, (await _host.PostAsync($"/api/v1/tasks/{id}/return", piotr,
            new { expectedVersion = await VersionAsync(id), reason = "Bana göre değil." })).Status);
        Assert.False((await StoredAsync(id)).IsRequest);               // non-vacuity: the request mark is gone

        var forDeniz = await EverythingAsync(id, deniz);
        Assert.Contains("\"code\":\"declined\"", forDeniz);
        Assert.Contains(Reason, forDeniz);

        // The work done after the acceptance is ordinary history; the decline is still nobody else's.
        var forBurak = await EverythingAsync(id, Token(Burak, UserKeys));
        Assert.NotEmpty(forBurak);
        Assert.DoesNotContain("\"code\":\"declined\"", forBurak);
        Assert.DoesNotContain(Reason, forBurak);
        Assert.Contains("\"code\":\"returned\"", forBurak);
    }

    // ── 5 · the notes ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_request_cannot_name_the_unit_it_is_filed_under()
    {
        var created = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys), new
        {
            title = "Birimi seçilmiş talep",
            priority = "Medium",
            assignmentTarget = TaskAssignmentTarget.Offered.ToString(),
            offeredToUserId = Ewa,
            organizationUnitId = _unitTr,
            dueAt = DateTimeOffset.UtcNow.AddDays(1),
            reviewRequired = false,
            approvalRequired = false,
            emailNotificationsEnabled = true,
            delegationAllowed = true
        });

        Assert.Equal(TaskReasonCodes.RequestOptionNotSupported, ReasonCode(created.Body));
        Assert.Empty(await AllTasksAsync(_harness.TenantId));
    }

    [Fact]
    public async Task A_returned_request_whose_origin_unit_was_archived_goes_to_the_senders_current_unit()
    {
        var ewa = Token(Ewa, UserKeys);
        var lab = await UnitAsync(_harness.TenantId, "TR-LAB", LegalEntityTr);
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        // Sent from a unit that has been archived since (staged on the stored request: the lab, then archived).
        await RawTaskAsync(id, Builders<BsonDocument>.Update.Set(nameof(TaskItem.RequestOriginUnitId), new BsonBinaryData(lab, GuidRepresentation.Standard)));
        await _harness.Database.GetCollection<OrganizationUnit>(PlatformCollections.OrganizationUnits)
            .UpdateOneAsync(u => u.Id == lab, Builders<OrganizationUnit>.Update.Set(u => u.IsArchived, true));
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) });

        Assert.Equal(HttpStatusCode.NoContent, (await _host.PostAsync($"/api/v1/tasks/{id}/return", ewa,
            new { expectedVersion = await VersionAsync(id), reason = "Yanlış kişiyim." })).Status);

        Assert.Equal(_unitTr, (await StoredAsync(id)).OrganizationUnitId);
    }

    [Fact]
    public async Task The_sender_of_an_accepted_request_is_refused_by_authority_before_the_state_is_asked()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.NoContent,
            (await _host.PostAsync($"/api/v1/tasks/{id}/complete", ewa, new { expectedVersion = await VersionAsync(id) })).Status);

        // Done cannot be cancelled by anybody (409) — but the sender is not even asked that: 403 first.
        var cancel = await _host.PostAsync($"/api/v1/tasks/{id}/cancel", deniz, new { expectedVersion = await VersionAsync(id) });

        Assert.Equal((HttpStatusCode.Forbidden, TaskReasonCodes.RequestSenderCannotDirect), (cancel.Status, ReasonCode(cancel.Body)));
    }

    // ── rules that had no test (CT list) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_sender_holding_the_delete_key_may_still_cancel_an_accepted_request()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", Token(Ewa, UserKeys), new { expectedVersion = await VersionAsync(id) });

        var cancel = await _host.PostAsync($"/api/v1/tasks/{id}/cancel", Token(Deniz, [.. SenderKeys, TaskPermissions.Delete]),
            new { expectedVersion = await VersionAsync(id) });

        Assert.Equal(HttpStatusCode.NoContent, cancel.Status);
        Assert.Equal(TaskLifecycle.Cancelled, (await StoredAsync(id)).Lifecycle);
    }

    [Fact]
    public async Task An_ordinary_task_handed_back_stays_in_its_own_unit()
    {
        var lab = await UnitAsync(_harness.TenantId, "TR-LAB", LegalEntityTr);
        var created = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys),
            CreateBody(TaskAssignmentTarget.Person, assignee: Burak));
        Assert.True(created.Status == HttpStatusCode.Created, created.Body);
        using var document = JsonDocument.Parse(created.Body);
        var id = document.RootElement.GetProperty("data").GetGuid();
        // Filed in the lab (staged): not the requester's home unit.
        await RawTaskAsync(id, Builders<BsonDocument>.Update.Set(nameof(TaskItem.OrganizationUnitId), new BsonBinaryData(lab, GuidRepresentation.Standard)));

        var returned = await _host.PostAsync($"/api/v1/tasks/{id}/return", Token(Burak, UserKeys),
            new { expectedVersion = await VersionAsync(id), reason = "Bana değil." });

        Assert.Equal(HttpStatusCode.NoContent, returned.Status);
        Assert.Equal(lab, (await StoredAsync(id)).OrganizationUnitId);
    }

    [Fact]
    public async Task A_request_from_before_its_origin_was_recorded_goes_back_to_its_senders_unit()
    {
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        await RawTaskAsync(id, Builders<BsonDocument>.Update.Unset(nameof(TaskItem.RequestOriginUnitId)));
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(_unitPl, (await StoredAsync(id)).OrganizationUnitId);

        await _host.PostAsync($"/api/v1/tasks/{id}/return", ewa, new { expectedVersion = await VersionAsync(id), reason = "Yanlış kişiyim." });

        Assert.Equal(_unitTr, (await StoredAsync(id)).OrganizationUnitId);
    }

    [Fact]
    public async Task A_declined_request_still_cannot_move_unit_by_an_edit()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys), new { expectedVersion = await VersionAsync(id), reason = "Hayır." });

        var moved = await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBodyWith(await VersionAsync(id), unit: _unitPl));

        Assert.Equal((HttpStatusCode.Conflict, TaskReasonCodes.RequestUnitFixed), (moved.Status, ReasonCode(moved.Body)));
        Assert.Equal(_unitTr, (await StoredAsync(id)).OrganizationUnitId);
    }

    [Fact]
    public async Task A_declined_request_earns_no_due_soon_reminder()
    {
        var created = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys),
            CreateBody(TaskAssignmentTarget.Offered, offeredTo: Ewa, reminderLeadDays: 3));
        using var document = JsonDocument.Parse(created.Body);
        var id = document.RootElement.GetProperty("data").GetGuid();
        await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys), new { expectedVersion = await VersionAsync(id), reason = "Hayır." });

        var version = await VersionAsync(id);

        Assert.DoesNotContain((await SweepRemindersAsync()).Notifications, n => n.TaskId == id);

        // Nobody's deadline is not even claimed: the sweep writes nothing on it (a claim would bump the version the
        // sender's page holds, and their next answer would read a false 409).
        var stored = await StoredAsync(id);
        Assert.Equal(version, stored.Version);
        Assert.Null(stored.LastDueSoonReminderKey);
    }

    [Fact]
    public async Task An_addressee_who_still_reads_a_called_off_request_is_told_it_is_no_longer_pending_on_decline()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Burak);          // Burak reads it through the unit scope as well
        await _host.PostAsync($"/api/v1/tasks/{id}/cancel", deniz, new { expectedVersion = await VersionAsync(id) });

        var decline = await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Burak, UserKeys),
            new { expectedVersion = await VersionAsync(id), reason = "Hayır." });

        Assert.Equal((HttpStatusCode.Conflict, TaskReasonCodes.RequestNotPending), (decline.Status, ReasonCode(decline.Body)));
    }

    [Fact]
    public async Task Naming_the_same_unit_in_an_edit_of_an_unanswered_request_is_not_a_move()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);

        var edit = await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBodyWith(await VersionAsync(id), unit: _unitTr));

        Assert.Equal(HttpStatusCode.NoContent, edit.Status);
    }

    [Fact]
    public async Task A_request_from_a_sender_without_a_seat_records_the_unit_it_was_filed_under()
    {
        var id = await SendRequestAsync(Token(Elif, SenderKeys), Ewa);

        var stored = await StoredAsync(id);

        Assert.NotNull(stored.RequestOriginUnitId);
        Assert.Equal(stored.OrganizationUnitId, stored.RequestOriginUnitId);
    }

    [Fact]
    public async Task Once_a_request_is_called_off_the_person_asked_no_longer_reads_it()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        Assert.Equal(HttpStatusCode.OK, (await _host.GetAsync($"/api/v1/tasks/{id}", Token(Ewa, UserKeys))).Status);
        await _host.PostAsync($"/api/v1/tasks/{id}/cancel", deniz, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(Ewa, (await StoredAsync(id)).OfferedToUserId);            // non-vacuity: the name is still stored

        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/tasks/{id}", Token(Ewa, UserKeys))).Status);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Deniz's own task (self-assigned, TR): nobody in PL reads it.</summary>
    private async Task<Guid> OwnTaskAsync()
    {
        var created = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys),
            CreateBodyWith(null, target: TaskAssignmentTarget.SelfAssigned));
        Assert.True(created.Status == HttpStatusCode.Created, created.Body);
        using var document = JsonDocument.Parse(created.Body);
        return document.RootElement.GetProperty("data").GetGuid();
    }

    /// <summary>Every write endpoint of one task, each with a body its own client would send.</summary>
    private IEnumerable<(string Name, Func<Task<(HttpStatusCode Status, string Body)>> Call)> EveryWrite(
        Guid id, string bearer, int version, Guid otherTask,
        Guid? commentId = null, string? stepCode = null, int stepVersion = 0, Guid? edgeId = null)
    {
        var step = stepCode ?? "S1";
        var comment = commentId ?? Guid.NewGuid();
        var dependency = edgeId ?? Guid.NewGuid();
        var path = $"/api/v1/tasks/{id}";
        var transition = new { expectedVersion = version };
        return
        [
            ("edit", () => _host.PutAsync(path, bearer, EditBody(version, "Ele geçirilmiş"))),
            ("delete", () => _host.DeleteAsync(path, bearer)),
            ("accept", () => _host.PostAsync($"{path}/accept", bearer, transition)),
            ("claim", () => _host.PostAsync($"{path}/claim", bearer, transition)),
            ("release", () => _host.PostAsync($"{path}/release", bearer, transition)),
            ("plan", () => _host.PostAsync($"{path}/plan", bearer, new { expectedVersion = version, plannedDate = DateTimeOffset.UtcNow.AddDays(1) })),
            ("unplan", () => _host.PostAsync($"{path}/unplan", bearer, transition)),
            ("start", () => _host.PostAsync($"{path}/start", bearer, transition)),
            ("submitReview", () => _host.PostAsync($"{path}/submitReview", bearer, transition)),
            ("complete", () => _host.PostAsync($"{path}/complete", bearer, transition)),
            ("cancel", () => _host.PostAsync($"{path}/cancel", bearer, transition)),
            ("inquire", () => _host.PostAsync($"{path}/inquire", bearer, new { expectedVersion = version, reason = "Bekliyorum." })),
            ("answer", () => _host.PostAsync($"{path}/answer", bearer, new { expectedVersion = version, answer = "Cevap." })),
            ("return", () => _host.PostAsync($"{path}/return", bearer, new { expectedVersion = version, reason = "Geri." })),
            ("reassign", () => _host.PostAsync($"{path}/reassign", bearer, new { expectedVersion = version, assigneeUserId = Piotr, reason = "Bende." })),
            ("decline", () => _host.PostAsync($"{path}/decline", bearer, new { expectedVersion = version, reason = "Hayır." })),
            ("withdraw", () => _host.PostAsync($"{path}/withdraw", bearer, transition)),
            ("offer", () => _host.PostAsync($"{path}/offer", bearer, new { expectedVersion = version, offeredToUserId = Marek })),
            ("checklist-state", () => _host.PostAsync($"{path}/checklist/items/state", bearer, new { itemCode = step, completed = true, expectedVersion = stepVersion })),
            ("checklist-add", () => _host.PostAsync($"{path}/checklist/items", bearer, new { text = "Adım", requirement = "Blocking", expectedVersion = stepVersion })),
            ("checklist-edit", () => _host.PutAsync($"{path}/checklist/items/{step}", bearer, new { labelText = "Adım", requirement = "Blocking", evidenceRequired = false, expectedVersion = stepVersion })),
            ("checklist-remove", () => _host.DeleteAsync($"{path}/checklist/items/{step}", bearer, new { expectedVersion = stepVersion })),
            ("checklist-order", () => _host.PutAsync($"{path}/checklist/order", bearer, new { itemCodes = new[] { step }, expectedVersion = stepVersion })),
            ("attachment-remove", () => _host.DeleteAsync($"{path}/attachments/{Guid.NewGuid()}", bearer)),
            ("comment", () => _host.PostAsync($"{path}/comments", bearer, new { text = "Bir sorum var." })),
            ("comment-edit", () => _host.PutAsync($"{path}/comments/{comment}", bearer, new { text = "Düzeltme." })),
            ("comment-withdraw", () => _host.DeleteAsync($"{path}/comments/{comment}", bearer)),
            ("personal-note", () => _host.PostAsync($"{path}/personal/notes", bearer, new { text = "Not." })),
            ("snooze", () => _host.PutAsync($"{path}/personal/snooze", bearer, new { snoozedUntil = DateTimeOffset.UtcNow.AddDays(2) })),
            ("pin", () => _host.PutAsync($"{path}/personal/pin", bearer, new { pinned = true })),
            ("dependency-add", () => _host.PostAsync($"{path}/dependencies", bearer, new { dependsOnTaskItemId = otherTask, dependencyType = "FinishToStart" })),
            ("dependency-remove", () => _host.DeleteAsync($"{path}/dependencies/{dependency}", bearer))
        ];
    }

    private ChecklistRunRepository ChecklistsIn(Guid tenant) => new(_harness.DbContext, In(tenant));

    private static (HttpStatusCode, string?) Pair((HttpStatusCode Status, string Body) answer) => (answer.Status, ReasonCode(answer.Body));

    private Task RawTaskAsync(Guid id, UpdateDefinition<BsonDocument> update)
        => _harness.Database.GetCollection<BsonDocument>(PlatformCollections.TaskItems).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)), update);
}
