using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Tasks.Validators;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-509 (WP-WCN-TASK-REQUEST-01, slice D1) — the TASK REQUEST end to end, ON THE WIRE, over a REAL MongoDB.
///
/// <para>Real JWT bearer validation, the real tenant-resolution middleware, the real <see cref="TasksController"/>
/// and <see cref="WorkItemsController"/> with their <c>[HasPermission]</c> filters, the real validation pipeline in
/// front of the real create handler, the real request / accept / decline / withdraw / offer / reassign / edit /
/// transition / plan handlers, the real <see cref="TaskRequestService"/>, <see cref="TaskAssignmentGuard"/>,
/// <see cref="TaskReadAccessPolicy"/>, <see cref="TaskTeamResolver"/> and <see cref="TaskWorkItemProvider"/> — and
/// the tasks, their history, their ownership log AND the org chart (units, positions, seats) in Mongo, each read
/// through the tenant the token names.</para>
///
/// <para>Doubles: the data-scope engine (answered from the same seats, as MOD-0018 would), the account directory
/// (who is switched on, per tenant — AuthService's contact read), display names, and the notification sink.</para>
///
/// <para>Two companies in one tenant: Deniz and Burak work in TR, Ewa, Piotr and their manager Marek in PL. Nobody
/// in TR is above or below anybody in PL, so BL-057 refuses every TR → PL assignment — which is the case a request
/// exists for. A second tenant holds one more person.</para>
///
/// <para>Shared test database, isolation by fresh tenants per test; never the dev database, never DefaultTenant.</para>
/// </summary>
public sealed class TaskRequestHttpMongoTests : IAsyncLifetime
{
    private static readonly Guid Deniz = Guid.Parse("50900000-0000-4000-8000-0000000000a1");   // TR — the sender
    private static readonly Guid Burak = Guid.Parse("50900000-0000-4000-8000-0000000000a2");   // TR — same company
    private static readonly Guid Ewa = Guid.Parse("50900000-0000-4000-8000-0000000000b1");     // PL — asked first
    private static readonly Guid Piotr = Guid.Parse("50900000-0000-4000-8000-0000000000b2");   // PL — asked second
    private static readonly Guid Marek = Guid.Parse("50900000-0000-4000-8000-0000000000b3");   // PL — their manager
    private static readonly Guid Elif = Guid.Parse("50900000-0000-4000-8000-0000000000c1");    // no seat anywhere
    private static readonly Guid Pasif = Guid.Parse("50900000-0000-4000-8000-0000000000c2");   // PL seat, switched off
    private static readonly Guid Stranger = Guid.Parse("50900000-0000-4000-8000-0000000000d1"); // the OTHER tenant
    private static readonly Guid Nobody = Guid.Parse("50900000-0000-4000-8000-0000000000ee");   // no such user

    private static readonly Guid LegalEntityTr = Guid.Parse("50900000-0000-4000-8000-00000000e001");
    private static readonly Guid LegalEntityPl = Guid.Parse("50900000-0000-4000-8000-00000000e002");

    /// <summary>What an ordinary Task Center user holds — create and assign included, the request key NOT.</summary>
    private static readonly string[] UserKeys =
    [
        TaskPermissions.Read, TaskPermissions.Create, TaskPermissions.Update, TaskPermissions.Complete,
        TaskPermissions.Cancel, TaskPermissions.Claim, TaskPermissions.Assign
    ];

    /// <summary>The same, plus the explicitly granted sending key.</summary>
    private static readonly string[] SenderKeys = [.. UserKeys, TaskPermissions.RequestsCreate];

    private MongoIntegrationHarness _harness = null!;
    private Guid _otherTenant;
    private Host _host = null!;
    private Guid _unitTr;
    private Guid _unitPl;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Organization, SchemaProfile.WorkflowWorkCenter);
        _otherTenant = Guid.NewGuid();

        // ── this tenant: two companies ──
        _unitTr = await UnitAsync(_harness.TenantId, "TR-HQ", LegalEntityTr);
        _unitPl = await UnitAsync(_harness.TenantId, "PL-HQ", LegalEntityPl);

        await SeatAsync(_harness.TenantId, Deniz, await PositionAsync(_harness.TenantId, "TR-QA-1", _unitTr));
        await SeatAsync(_harness.TenantId, Burak, await PositionAsync(_harness.TenantId, "TR-QA-2", _unitTr));

        var manager = await PositionAsync(_harness.TenantId, "PL-QA-MGR", _unitPl);
        await SeatAsync(_harness.TenantId, Marek, manager);
        await SeatAsync(_harness.TenantId, Ewa, await PositionAsync(_harness.TenantId, "PL-QA-1", _unitPl, manager));
        await SeatAsync(_harness.TenantId, Piotr, await PositionAsync(_harness.TenantId, "PL-QA-2", _unitPl, manager));
        await SeatAsync(_harness.TenantId, Pasif, await PositionAsync(_harness.TenantId, "PL-QA-3", _unitPl));

        // ── the other tenant: one company, one person ──
        var foreignUnit = await UnitAsync(_otherTenant, "XX-HQ", Guid.NewGuid());
        await SeatAsync(_otherTenant, Stranger, await PositionAsync(_otherTenant, "XX-QA-1", foreignUnit));

        _host = new Host(_harness, new Dictionary<Guid, HashSet<Guid>>
        {
            // Pasif has a seat and a switched-off account; Elif has an account and no seat.
            [_harness.TenantId] = [Deniz, Burak, Ewa, Piotr, Marek, Elif],
            [_otherTenant] = [Stranger, Ewa]
        });
    }

    public async Task DisposeAsync()
    {
        _host.Dispose();
        var tenants = new[] { _harness.TenantId, _otherTenant };
        await DeleteAsync<TaskItem>(PlatformCollections.TaskItems, tenants);
        await DeleteAsync<TaskTransition>(PlatformCollections.TaskTransitions, tenants);
        await DeleteAsync<TaskAssignment>(PlatformCollections.TaskAssignments, tenants);
        await DeleteAsync<OrganizationUnit>(PlatformCollections.OrganizationUnits, tenants);
        await DeleteAsync<Position>(PlatformCollections.Positions, tenants);
        await DeleteAsync<PositionAssignment>(PlatformCollections.PositionAssignments, tenants);
        await _harness.DisposeAsync();
    }

    // ── the whole journey ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Asked_declined_asked_again_accepted_and_from_then_on_the_sender_only_watches()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var piotr = Token(Piotr, UserKeys);

        // The baseline this feature stands beside: a plain ASSIGNMENT across the company boundary is still refused.
        var assigned = await _host.PostAsync("/api/v1/tasks", deniz, CreateBody(TaskAssignmentTarget.Person, assignee: Ewa));
        Assert.Equal(HttpStatusCode.BadRequest, assigned.Status);
        Assert.Equal(TaskReasonCodes.AssigneeNotAssignable, ReasonCode(assigned.Body));

        // ── asked ──
        var id = await SendRequestAsync(deniz, Ewa);

        var stored = await StoredAsync(id);
        Assert.Equal(TaskAssignmentTarget.Offered, stored.AssignmentTarget);
        Assert.Null(stored.AssigneeUserId);
        Assert.Equal(Ewa, stored.OfferedToUserId);
        Assert.True(stored.IsRequest);
        Assert.Equal(_unitTr, stored.OrganizationUnitId);          // the sender's unit until it is accepted
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);

        var sent = Assert.Single(await OwnershipLogAsync(id));
        Assert.Equal(TaskAssignmentEventType.Offered, sent.EventType);
        Assert.Equal((Deniz, Ewa), (sent.ActorUserId, sent.UserId));
        Assert.Equal((LegalEntityTr, LegalEntityPl), (sent.ActorLegalEntityId, sent.TargetLegalEntityId));

        // The person asked finds it, offered — with exactly the two acts that are theirs.
        var forEwa = Assert.Single(await MineAsync(ewa), item => IdOf(item) == id);
        Assert.Equal("offered", forEwa.GetProperty("assignmentMode").GetString());
        Assert.Equal("unowned", forEwa.GetProperty("ownershipState").GetString());
        Assert.Equal("pendingOffer", forEwa.GetProperty("admissionState").GetString());
        Assert.Equal(["accept", "decline"], ActionCodes(forEwa));
        Assert.Equal(HttpStatusCode.OK, (await _host.GetAsync($"/api/v1/work-items/{id}", ewa)).Status);

        // The sender finds it in what they started, waiting for an answer.
        var forDeniz = Assert.Single(await MineAsync(deniz), item => IdOf(item) == id);
        Assert.Equal("initiator", forDeniz.GetProperty("viewerRelation").GetString());
        Assert.Equal("pending", forDeniz.GetProperty("offer").GetProperty("state").GetString());
        Assert.Equal(Ewa.ToString(), forDeniz.GetProperty("offer").GetProperty("offeredTo").GetProperty("id").GetString());
        Assert.Equal(["withdraw", "cancel"], ActionCodes(forDeniz));

        // Somebody else in the addressee's company finds nothing.
        Assert.DoesNotContain(await MineAsync(piotr), item => IdOf(item) == id);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{id}", piotr)).Status);

        // ── declined: a reason is required, and only the person asked may give it ──
        var silent = await _host.PostAsync($"/api/v1/tasks/{id}/decline", ewa,
            new { expectedVersion = await VersionAsync(id), reason = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, silent.Status);
        Assert.Equal(TaskReasonCodes.HandoverReasonRequired, ReasonCode(silent.Body));

        var notAsked = await _host.PostAsync($"/api/v1/tasks/{id}/decline", piotr,
            new { expectedVersion = await VersionAsync(id), reason = "Benim değil." });
        Assert.Equal(HttpStatusCode.Forbidden, notAsked.Status);
        Assert.Equal(TaskReasonCodes.RequestNotAddressee, ReasonCode(notAsked.Body));
        Assert.Equal(Ewa, (await StoredAsync(id)).OfferedToUserId);

        // Through the Task Center's one dispatch address: the action code IS the route segment.
        var declined = await _host.PostAsync($"/api/v1/work-items/{id}/actions/decline", ewa,
            new { providerCode = "tasks", payload = new { expectedVersion = await VersionAsync(id), reason = "Bu ay kapasitem yok." } });
        Assert.True(declined.Status == HttpStatusCode.OK, declined.Body);

        stored = await StoredAsync(id);
        Assert.Null(stored.OfferedToUserId);
        Assert.Null(stored.AssigneeUserId);
        Assert.Equal("Bu ay kapasitem yok.", stored.OfferDeclineReason);
        var said = Assert.Single(await HistoryAsync(id), e => e.Kind == TaskTransitionKind.Declined);
        Assert.Equal((Ewa, "Bu ay kapasitem yok."), (said.ActorUserId, said.Reason));

        // Her access ended with her answer.
        Assert.DoesNotContain(await MineAsync(ewa), item => IdOf(item) == id);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{id}", ewa)).Status);

        // The sender reads who said no and why, and may ask somebody else, assign it, or call it off.
        forDeniz = Assert.Single(await MineAsync(deniz), item => IdOf(item) == id);
        Assert.Equal("declined", forDeniz.GetProperty("offer").GetProperty("state").GetString());
        Assert.Equal("Bu ay kapasitem yok.",
            forDeniz.GetProperty("offer").GetProperty("declineReason").GetProperty("text").GetString());
        Assert.Equal(["offer", "reassign", "cancel"], ActionCodes(forDeniz));

        // ── asked again ──
        var again = await _host.PostAsync($"/api/v1/tasks/{id}/offer", deniz,
            new { expectedVersion = await VersionAsync(id), offeredToUserId = Piotr });
        Assert.Equal(HttpStatusCode.NoContent, again.Status);
        stored = await StoredAsync(id);
        Assert.Equal(Piotr, stored.OfferedToUserId);
        Assert.Null(stored.OfferDeclineReason);
        Assert.Equal(Deniz, Assert.Single(await HistoryAsync(id), e => e.Kind == TaskTransitionKind.Offered).ActorUserId);

        // ── accepted ──
        var accepted = await _host.PostAsync($"/api/v1/tasks/{id}/accept", piotr,
            new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.NoContent, accepted.Status);

        stored = await StoredAsync(id);
        Assert.Equal(TaskAssignmentTarget.Person, stored.AssignmentTarget);
        Assert.Equal(Piotr, stored.AssigneeUserId);
        Assert.Equal(Piotr, stored.AcceptedByUserId);
        Assert.Null(stored.OfferedToUserId);
        Assert.True(stored.IsRequest);
        Assert.Equal(_unitPl, stored.OrganizationUnitId);          // the recipient's unit from acceptance on
        Assert.Equal(TaskLifecycle.InProgress, stored.Lifecycle);
        Assert.Equal(Piotr, Assert.Single(await HistoryAsync(id), e => e.Kind == TaskTransitionKind.Accepted).ActorUserId);

        var taken = Assert.Single(await OwnershipLogAsync(id), e => e.EventType == TaskAssignmentEventType.Accepted);
        Assert.Equal((LegalEntityPl, LegalEntityTr), (taken.ActorLegalEntityId, taken.TargetLegalEntityId));

        var forPiotr = Assert.Single(await MineAsync(piotr), item => IdOf(item) == id);
        Assert.Equal("admitted", forPiotr.GetProperty("admissionState").GetString());
        Assert.False(forPiotr.TryGetProperty("offer", out _));

        // ── the sender only watches ──
        var redirect = await _host.PostAsync($"/api/v1/tasks/{id}/reassign", deniz,
            new { expectedVersion = await VersionAsync(id), assigneeUserId = Burak, reason = "Burak yapsın." });
        Assert.Equal(HttpStatusCode.Forbidden, redirect.Status);
        Assert.Equal(TaskReasonCodes.RequestSenderCannotDirect, ReasonCode(redirect.Body));

        var edit = await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBody(await VersionAsync(id), "Başka bir iş"));
        Assert.Equal(HttpStatusCode.Forbidden, edit.Status);
        Assert.Equal(TaskReasonCodes.RequestSenderCannotDirect, ReasonCode(edit.Body));

        var tooLate = await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", deniz,
            new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.Conflict, tooLate.Status);
        Assert.Equal(TaskReasonCodes.RequestNotPending, ReasonCode(tooLate.Body));

        stored = await StoredAsync(id);
        Assert.Equal(Piotr, stored.AssigneeUserId);
        Assert.NotEqual("Başka bir iş", stored.Title);

        // …and still reads it, with nothing to press that would direct the work.
        Assert.Equal(HttpStatusCode.OK, (await _host.GetAsync($"/api/v1/work-items/{id}", deniz)).Status);

        // The holder's own edit is theirs to make.
        var holderEdit = await _host.PutAsync($"/api/v1/tasks/{id}", piotr, EditBody(await VersionAsync(id), "Piotr'un başlığı"));
        Assert.Equal(HttpStatusCode.NoContent, holderEdit.Status);
    }

    // ── who may be asked: one answer for every "no" ──────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGETS (recipient eligibility · tenant filter · one answer). No seat, a switched-off account,
    /// another tenant's user and an id nobody has all read as the SAME response, byte for byte, and store nothing.
    /// </summary>
    [Fact]
    public async Task Every_reason_a_person_cannot_be_asked_reads_as_ONE_answer_and_nothing_is_stored()
    {
        var deniz = Token(Deniz, SenderKeys);

        var answers = new List<(HttpStatusCode Status, string Body)>();
        foreach (var target in new[] { Elif, Pasif, Stranger, Nobody })
        {
            answers.Add(await _host.PostAsync("/api/v1/tasks", deniz, CreateBody(TaskAssignmentTarget.Offered, offeredTo: target)));
        }

        Assert.All(answers, answer =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
            Assert.Equal(TaskReasonCodes.RequestRecipientUnavailable, ReasonCode(answer.Body));
        });
        Assert.Single(answers.Select(answer => answer.Body).Distinct());
        Assert.Empty(await AllTasksAsync(_harness.TenantId));

        // Non-vacuity: the same call, to somebody who can be asked, lands.
        await SendRequestAsync(deniz, Ewa);
    }

    [Fact]
    public async Task Addressing_a_declined_request_to_somebody_who_cannot_be_asked_is_the_same_one_answer()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys),
            new { expectedVersion = await VersionAsync(id), reason = "Olmaz." });

        foreach (var target in new[] { Elif, Pasif, Stranger, Nobody })
        {
            var answer = await _host.PostAsync($"/api/v1/tasks/{id}/offer", deniz,
                new { expectedVersion = await VersionAsync(id), offeredToUserId = target });
            Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
            Assert.Equal(TaskReasonCodes.RequestRecipientUnavailable, ReasonCode(answer.Body));
        }

        Assert.Null((await StoredAsync(id)).OfferedToUserId);
    }

    // ── the tenant boundary ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (tenant). The addressee's own user id, under ANOTHER tenant's token, finds nothing and can
    /// answer nothing: every read and write runs through the tenant the token names.
    /// </summary>
    [Fact]
    public async Task The_addressees_id_under_another_tenants_token_never_reaches_the_request()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        var foreign = _host.Token(Ewa, _otherTenant, UserKeys);

        Assert.Empty(await MineAsync(foreign));
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{id}", foreign)).Status);

        foreach (var verb in new[] { "accept", "decline" })
        {
            var answer = await _host.PostAsync($"/api/v1/tasks/{id}/{verb}", foreign,
                new { expectedVersion = await VersionAsync(id), reason = "Hayır." });
            Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        }

        var stored = await StoredAsync(id);
        Assert.Equal(Ewa, stored.OfferedToUserId);
        Assert.Null(stored.AssigneeUserId);
    }

    // ── the sending key ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>MUTATION TARGET (key). Create and Assign are not enough: without the explicitly granted key the
    /// request is refused on the wire and nothing is stored — on the create route and on the offer route alike.</summary>
    [Fact]
    public async Task Without_the_sending_key_a_request_is_403_and_nothing_is_stored()
    {
        var withoutKey = Token(Deniz, UserKeys);

        var refused = await _host.PostAsync("/api/v1/tasks", withoutKey, CreateBody(TaskAssignmentTarget.Offered, offeredTo: Ewa));
        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Empty(await AllTasksAsync(_harness.TenantId));

        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", withoutKey, new { expectedVersion = await VersionAsync(id) });
        var offer = await _host.PostAsync($"/api/v1/tasks/{id}/offer", withoutKey,
            new { expectedVersion = await VersionAsync(id), offeredToUserId = Piotr });
        Assert.Equal(HttpStatusCode.Forbidden, offer.Status);
        Assert.Null((await StoredAsync(id)).OfferedToUserId);
    }

    [Fact]
    public async Task A_request_to_myself_is_refused_by_name()
    {
        var answer = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys),
            CreateBody(TaskAssignmentTarget.Offered, offeredTo: Deniz));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(TaskReasonCodes.RequestToSelf, ReasonCode(answer.Body));
        Assert.Empty(await AllTasksAsync(_harness.TenantId));
    }

    [Fact]
    public async Task A_request_to_somebody_INSIDE_my_scope_is_allowed_and_assigning_them_still_works()
    {
        var deniz = Token(Deniz, SenderKeys);

        var id = await SendRequestAsync(deniz, Burak);
        Assert.Equal(Burak, (await StoredAsync(id)).OfferedToUserId);

        var assigned = await _host.PostAsync("/api/v1/tasks", deniz, CreateBody(TaskAssignmentTarget.Person, assignee: Burak));
        Assert.Equal(HttpStatusCode.Created, assigned.Status);
    }

    [Fact]
    public async Task A_request_cannot_carry_an_approval_or_a_review()
    {
        var answer = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys),
            CreateBody(TaskAssignmentTarget.Offered, offeredTo: Ewa, approvalManager: Burak));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(TaskReasonCodes.RequestOptionNotSupported, ReasonCode(answer.Body));
        Assert.Empty(await AllTasksAsync(_harness.TenantId));
    }

    // ── owner decision 6: a seat is needed to RECEIVE, not to SEND ───────────────────────────────────────────

    [Fact]
    public async Task Decision_6_a_person_with_no_seat_may_SEND_a_request_and_may_not_RECEIVE_one()
    {
        // Elif holds the key and no seat: she may ask. The request is filed under the tenant's root unit.
        var id = await SendRequestAsync(Token(Elif, SenderKeys), Ewa);
        Assert.Equal(Ewa, (await StoredAsync(id)).OfferedToUserId);

        // …and nobody may ask her.
        var toElif = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys),
            CreateBody(TaskAssignmentTarget.Offered, offeredTo: Elif));
        Assert.Equal(HttpStatusCode.BadRequest, toElif.Status);
        Assert.Equal(TaskReasonCodes.RequestRecipientUnavailable, ReasonCode(toElif.Body));
    }

    // ── owner decision 4: the recipient's manager sees it only once it is accepted ───────────────────────────

    [Fact]
    public async Task Decision_4_the_recipients_manager_does_not_see_the_request_until_it_is_accepted()
    {
        var marek = Token(Marek, UserKeys);
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);

        Assert.DoesNotContain(await MineAsync(marek, team: true), item => IdOf(item) == id);
        Assert.DoesNotContain(await MineAsync(marek), item => IdOf(item) == id);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{id}", marek)).Status);

        await _host.PostAsync($"/api/v1/tasks/{id}/accept", Token(Ewa, UserKeys), new { expectedVersion = await VersionAsync(id) });

        // Accepted: it is his subordinate's work, on his team list and readable — the list and the read rule agree.
        Assert.Contains(await MineAsync(marek, team: true), item => IdOf(item) == id);
        Assert.Equal(HttpStatusCode.OK, (await _host.GetAsync($"/api/v1/work-items/{id}", marek)).Status);
    }

    // ── nobody works on what nobody has accepted ─────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (no holder on an offer). The person asked, the sender and a bystander all try start,
    /// complete and plan; every attempt is refused on the wire and the task does not move.
    /// </summary>
    [Fact]
    public async Task An_unaccepted_request_cannot_be_started_completed_or_planned_by_anybody()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);

        foreach (var who in new[] { Ewa, Deniz, Piotr })
        {
            var token = Token(who, UserKeys);
            foreach (var verb in new[] { "start", "complete" })
            {
                var answer = await _host.PostAsync($"/api/v1/tasks/{id}/{verb}", token,
                    new { expectedVersion = await VersionAsync(id) });
                Assert.True(answer.Status is HttpStatusCode.Forbidden or HttpStatusCode.Conflict,
                    $"{verb} by {who}: {answer.Status} {answer.Body}");
            }

            var plan = await _host.PostAsync($"/api/v1/tasks/{id}/plan", token,
                new { expectedVersion = await VersionAsync(id), plannedDate = DateTimeOffset.UtcNow.AddDays(1) });
            Assert.True(plan.Status is HttpStatusCode.Forbidden or HttpStatusCode.Conflict, $"plan by {who}: {plan.Body}");
        }

        var stored = await StoredAsync(id);
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);
        Assert.Null(stored.AssigneeUserId);
        Assert.Null(stored.PlannedDate);
        Assert.DoesNotContain(await HistoryAsync(id), e => e.Kind is TaskTransitionKind.Started
            or TaskTransitionKind.Completed or TaskTransitionKind.Planned);
    }

    [Fact]
    public async Task The_sender_withdraws_before_the_answer_and_the_person_asked_can_no_longer_take_it()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);

        // Only the sender withdraws.
        var notMine = await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", ewa, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.Forbidden, notMine.Status);
        Assert.Equal(TaskReasonCodes.RequestNotSender, ReasonCode(notMine.Body));

        // A pending request is not assigned out from under the person asked; it is withdrawn first.
        var assignWhilePending = await _host.PostAsync($"/api/v1/tasks/{id}/reassign", deniz,
            new { expectedVersion = await VersionAsync(id), assigneeUserId = Burak, reason = "Burak alsın." });
        Assert.Equal(HttpStatusCode.Conflict, assignWhilePending.Status);
        Assert.Equal(TaskReasonCodes.RequestPending, ReasonCode(assignWhilePending.Body));

        var withdrawn = await _host.PostAsync($"/api/v1/tasks/{id}/withdraw", deniz, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.NoContent, withdrawn.Status);
        Assert.Equal(Deniz, Assert.Single(await HistoryAsync(id), e => e.Kind == TaskTransitionKind.Withdrawn).ActorUserId);

        var late = await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.Conflict, late.Status);
        Assert.Equal(TaskReasonCodes.RequestNotPending, ReasonCode(late.Body));
        Assert.DoesNotContain(await MineAsync(ewa), item => IdOf(item) == id);

        var forDeniz = Assert.Single(await MineAsync(deniz), item => IdOf(item) == id);
        Assert.Equal("withdrawn", forDeniz.GetProperty("offer").GetProperty("state").GetString());

        // The existing road is enough for "the sender gives it to somebody in their own scope": it becomes an
        // ordinary assignment and stops being a request.
        var assigned = await _host.PostAsync($"/api/v1/tasks/{id}/reassign", deniz,
            new { expectedVersion = await VersionAsync(id), assigneeUserId = Burak, reason = "Burak alsın." });
        Assert.Equal(HttpStatusCode.NoContent, assigned.Status);
        var stored = await StoredAsync(id);
        Assert.Equal((TaskAssignmentTarget.Person, (Guid?)Burak, false),
            (stored.AssignmentTarget, stored.AssigneeUserId, stored.IsRequest));
    }

    [Fact]
    public async Task The_sender_can_call_a_request_off_and_it_leaves_the_addressees_inbox()
    {
        var deniz = Token(Deniz, SenderKeys);
        var id = await SendRequestAsync(deniz, Ewa);

        var cancelled = await _host.PostAsync($"/api/v1/tasks/{id}/cancel", deniz, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.NoContent, cancelled.Status);

        var ewa = Token(Ewa, UserKeys);
        Assert.DoesNotContain(await MineAsync(ewa), item => IdOf(item) == id);
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{id}", ewa)).Status);
        var accept = await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.Conflict, accept.Status);
    }

    /// <summary>
    /// A request handed BACK is over. The task is its sender's own again, and what they do with it next is an
    /// ordinary assignment they keep directing — the request mark must not follow it there.
    /// </summary>
    [Fact]
    public async Task A_request_returned_to_its_sender_is_the_senders_own_task_again()
    {
        var deniz = Token(Deniz, SenderKeys);
        var ewa = Token(Ewa, UserKeys);
        var id = await SendRequestAsync(deniz, Ewa);
        await _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = await VersionAsync(id) });

        var returned = await _host.PostAsync($"/api/v1/tasks/{id}/return", ewa,
            new { expectedVersion = await VersionAsync(id), reason = "Yanlış kişiyim." });
        Assert.Equal(HttpStatusCode.NoContent, returned.Status);
        var stored = await StoredAsync(id);
        Assert.Equal(((Guid?)Deniz, false), (stored.AssigneeUserId, stored.IsRequest));

        // The sender assigns it inside their own scope, and still directs it afterwards.
        var assigned = await _host.PostAsync($"/api/v1/tasks/{id}/reassign", deniz,
            new { expectedVersion = await VersionAsync(id), assigneeUserId = Burak, reason = "Burak alsın." });
        Assert.Equal(HttpStatusCode.NoContent, assigned.Status);
        var edit = await _host.PutAsync($"/api/v1/tasks/{id}", deniz, EditBody(await VersionAsync(id), "Düzeltilmiş başlık"));
        Assert.Equal(HttpStatusCode.NoContent, edit.Status);
    }

    /// <summary>
    /// WHO was asked is shown to the sender and the person asked. A colleague of the sender who may read the task
    /// through the unit scope learns that it waits for an answer — not the name of somebody in another company.
    /// </summary>
    [Fact]
    public async Task Who_was_asked_is_shown_only_to_the_sender_and_the_person_asked()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);

        var forBurak = await _host.GetAsync($"/api/v1/work-items/{id}", Token(Burak, UserKeys));
        Assert.Equal(HttpStatusCode.OK, forBurak.Status);
        using var document = JsonDocument.Parse(forBurak.Body);
        var offer = document.RootElement.GetProperty("data").GetProperty("offer");
        Assert.Equal("pending", offer.GetProperty("state").GetString());
        Assert.False(offer.TryGetProperty("offeredTo", out _));
        Assert.DoesNotContain(Ewa.ToString(), offer.GetRawText());
        Assert.Empty(document.RootElement.GetProperty("data").GetProperty("actions").EnumerateArray());

        var forEwa = await _host.GetAsync($"/api/v1/work-items/{id}", Token(Ewa, UserKeys));
        using var hers = JsonDocument.Parse(forEwa.Body);
        Assert.Equal(Ewa.ToString(),
            hers.RootElement.GetProperty("data").GetProperty("offer").GetProperty("offeredTo").GetProperty("id").GetString());
    }

    [Fact]
    public async Task A_decline_reason_longer_than_a_description_is_refused()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);

        var answer = await _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys),
            new { expectedVersion = await VersionAsync(id), reason = new string('x', TaskFieldLimits.MaxDescriptionLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(TaskReasonCodes.RequestDeclineReasonTooLong, ReasonCode(answer.Body));
        Assert.Equal(Ewa, (await StoredAsync(id)).OfferedToUserId);
    }

    // ── races: exactly one wins ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (version condition). Two callers released at the same instant with the same expected
    /// version — the person asked accepting, the sender withdrawing. Exactly one lands; the other reads a coded 409;
    /// the stored task and its history agree with the winner. Repeated so both orders occur.
    /// </summary>
    [Fact]
    public async Task Accept_against_withdraw_at_the_same_instant_exactly_one_wins()
    {
        for (var round = 0; round < 12; round++)
        {
            var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
            var version = await VersionAsync(id);

            var (accept, withdraw) = await TogetherAsync(
                () => _host.PostAsync($"/api/v1/tasks/{id}/accept", Token(Ewa, UserKeys), new { expectedVersion = version }),
                () => _host.PostAsync($"/api/v1/tasks/{id}/withdraw", Token(Deniz, SenderKeys), new { expectedVersion = version }));

            AssertExactlyOneWon(accept, withdraw);

            var stored = await StoredAsync(id);
            var history = await HistoryAsync(id);
            if (accept.Status == HttpStatusCode.NoContent)
            {
                Assert.Equal((Guid?)Ewa, stored.AssigneeUserId);
                Assert.Single(history, e => e.Kind == TaskTransitionKind.Accepted);
                Assert.DoesNotContain(history, e => e.Kind == TaskTransitionKind.Withdrawn);
            }
            else
            {
                Assert.Null(stored.AssigneeUserId);
                Assert.Null(stored.OfferedToUserId);
                Assert.Single(history, e => e.Kind == TaskTransitionKind.Withdrawn);
                Assert.DoesNotContain(history, e => e.Kind == TaskTransitionKind.Accepted);
            }
        }
    }

    [Fact]
    public async Task Accept_twice_at_the_same_instant_is_accepted_once()
    {
        for (var round = 0; round < 12; round++)
        {
            var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
            var version = await VersionAsync(id);
            var ewa = Token(Ewa, UserKeys);

            var (first, second) = await TogetherAsync(
                () => _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = version }),
                () => _host.PostAsync($"/api/v1/tasks/{id}/accept", ewa, new { expectedVersion = version }));

            AssertExactlyOneWon(first, second);
            Assert.Single(await HistoryAsync(id), e => e.Kind == TaskTransitionKind.Accepted);
            Assert.Single(await OwnershipLogAsync(id), e => e.EventType == TaskAssignmentEventType.Accepted);
        }
    }

    [Fact]
    public async Task Decline_against_withdraw_at_the_same_instant_exactly_one_wins()
    {
        for (var round = 0; round < 12; round++)
        {
            var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
            var version = await VersionAsync(id);

            var (decline, withdraw) = await TogetherAsync(
                () => _host.PostAsync($"/api/v1/tasks/{id}/decline", Token(Ewa, UserKeys), new { expectedVersion = version, reason = "Olmaz." }),
                () => _host.PostAsync($"/api/v1/tasks/{id}/withdraw", Token(Deniz, SenderKeys), new { expectedVersion = version }));

            AssertExactlyOneWon(decline, withdraw);

            var stored = await StoredAsync(id);
            var history = await HistoryAsync(id);
            Assert.Null(stored.OfferedToUserId);
            Assert.Single(history, e => e.Kind is TaskTransitionKind.Declined or TaskTransitionKind.Withdrawn);
            Assert.Equal(decline.Status == HttpStatusCode.NoContent ? "Olmaz." : null, stored.OfferDeclineReason);
        }
    }

    // ── a request nobody accepted is nobody's load ───────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (no holder on an offer). A request sent over the wire and not yet accepted is counted under
    /// NOBODY in the work report and earns the person asked no due-soon reminder; once they accept, both follow.
    /// </summary>
    [Fact]
    public async Task A_request_nobody_accepted_is_in_nobodys_work_report_and_earns_no_reminder_until_it_is_accepted()
    {
        var created = await _host.PostAsync("/api/v1/tasks", Token(Deniz, SenderKeys),
            CreateBody(TaskAssignmentTarget.Offered, offeredTo: Ewa, reminderLeadDays: 3));
        Assert.True(created.Status == HttpStatusCode.Created, created.Body);
        using var document = JsonDocument.Parse(created.Body);
        var id = document.RootElement.GetProperty("data").GetGuid();

        Assert.DoesNotContain(await ReportByAssigneeAsync(), group => group.Key == Ewa.ToString());
        Assert.DoesNotContain((await SweepRemindersAsync()).Notifications, n => n.Candidates.Contains(Ewa));

        var accepted = await _host.PostAsync($"/api/v1/tasks/{id}/accept", Token(Ewa, UserKeys),
            new { expectedVersion = await VersionAsync(id) });
        Assert.Equal(HttpStatusCode.NoContent, accepted.Status);

        Assert.Equal(1, Assert.Single(await ReportByAssigneeAsync(), group => group.Key == Ewa.ToString()).Flow.Opened);
        Assert.Contains((await SweepRemindersAsync()).Notifications,
            n => n.EventCode == TaskNotificationEvents.DueSoon && n.Candidates.SequenceEqual([Ewa]));
    }

    private async Task<IReadOnlyList<WorkReportBucket>> ReportByAssigneeAsync()
    {
        var tenant = In(_harness.TenantId);
        var repository = new WorkReportRepository(
            _harness.DbContext,
            tenant,
            new OrganizationUnitRepository(_harness.DbContext, tenant),
            new FakeTaskTypeRepository(),
            new NoCompanyFilter(),
            NullLogger<WorkReportRepository>.Instance);

        var report = await repository.AggregateAsync(new WorkReportCriteria(
            DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30),
            WorkReportScope.TenantWideScope(), WorkReportGroupBy.Assignee));
        return report.Groups;
    }

    private async Task<FakeTaskNotificationService> SweepRemindersAsync()
    {
        var notifications = new FakeTaskNotificationService();
        await new SendDueSoonRemindersHandler(
                TasksIn(_harness.TenantId), notifications, NullLogger<SendDueSoonRemindersHandler>.Instance)
            .Handle(new SendDueSoonRemindersCommand(DateTimeOffset.UtcNow, 100, "corr-sweep"), CancellationToken.None);
        return notifications;
    }

    private sealed class NoCompanyFilter : ILegalEntityReferenceValidator
    {
        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default)
            => throw new NotSupportedException("No company filter is sent in these tests.");
    }

    // ── the stored shape ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The enum is stored as its NUMBER (append-only, never renumbered), and a document written before this
    /// change — none of the three new fields on it — still reads, as an ordinary task that is not a request.
    /// </summary>
    [Fact]
    public async Task The_target_is_stored_as_a_number_and_a_document_from_before_the_request_still_reads()
    {
        var id = await SendRequestAsync(Token(Deniz, SenderKeys), Ewa);
        var collection = _harness.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems);

        var document = await RawAsync(collection, id);
        Assert.Equal(3, document[nameof(TaskItem.AssignmentTarget)].AsInt32);
        Assert.True(document[nameof(TaskItem.IsRequest)].AsBoolean);

        // A task from before: an accepted Person task, with the new fields REMOVED from the stored document.
        var old = new TaskItem
        {
            TenantId = _harness.TenantId, Title = "Eski görev", AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = Burak, CreatedByUserId = Deniz, OrganizationUnitId = _unitTr, CreatedBy = "d1"
        };
        await TasksIn(_harness.TenantId).CreateAsync(old);
        var removed = await collection.UpdateOneAsync(
            Builders<TaskItem>.Filter.Eq(x => x.Id, old.Id),
            Builders<TaskItem>.Update
                .Unset(x => x.OfferedToUserId)
                .Unset(x => x.IsRequest)
                .Unset(x => x.OfferDeclineReason));
        Assert.Equal(1, removed.ModifiedCount);
        var before = await RawAsync(collection, old.Id);
        Assert.False(before.Contains(nameof(TaskItem.OfferedToUserId)));
        Assert.False(before.Contains(nameof(TaskItem.IsRequest)));
        Assert.False(before.Contains(nameof(TaskItem.OfferDeclineReason)));

        var read = await StoredAsync(old.Id);
        Assert.Equal((false, (Guid?)null, (string?)null), (read.IsRequest, read.OfferedToUserId, read.OfferDeclineReason));

        // …and behaves as it always did: its assignee accepts it, its requester may still redirect it.
        var accepted = await _host.PostAsync($"/api/v1/tasks/{old.Id}/accept", Token(Burak, UserKeys),
            new { expectedVersion = read.Version });
        Assert.Equal(HttpStatusCode.NoContent, accepted.Status);
        var forBurak = Assert.Single(await MineAsync(Token(Burak, UserKeys)), item => IdOf(item) == old.Id);
        Assert.Equal("direct", forBurak.GetProperty("assignmentMode").GetString());
        Assert.False(forBurak.TryGetProperty("offer", out _));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private string Token(Guid user, params string[] keys) => _host.Token(user, _harness.TenantId, keys);

    /// <summary>The document as it lies on disk, not as the class map reads it back.</summary>
    private static Task<BsonDocument> RawAsync(IMongoCollection<TaskItem> collection, Guid id)
        => collection.Find(Builders<TaskItem>.Filter.Eq(x => x.Id, id)).As<BsonDocument>().FirstAsync();

    private async Task<Guid> SendRequestAsync(string bearer, Guid offeredTo)
    {
        var created = await _host.PostAsync("/api/v1/tasks", bearer, CreateBody(TaskAssignmentTarget.Offered, offeredTo: offeredTo));
        Assert.True(created.Status == HttpStatusCode.Created, created.Body);
        using var document = JsonDocument.Parse(created.Body);
        return document.RootElement.GetProperty("data").GetGuid();
    }

    /// <summary>The create payload exactly as a browser sends it — the target as its NAME, not its number.</summary>
    private static object CreateBody(
        TaskAssignmentTarget target, Guid? assignee = null, Guid? offeredTo = null, Guid? approvalManager = null,
        int? reminderLeadDays = null) => new
    {
        title = "Stabilite raporunu gözden geçir",
        priority = "Medium",
        assignmentTarget = target.ToString(),
        assigneeUserId = assignee,
        offeredToUserId = offeredTo,
        // One day out: inside a three-day reminder window when a test asks for one.
        dueAt = DateTimeOffset.UtcNow.AddDays(1),
        reminderLeadDays,
        reviewRequired = false,
        approvalRequired = approvalManager is not null,
        approvalManagerUserId = approvalManager,
        emailNotificationsEnabled = true,
        delegationAllowed = true
    };

    private static object EditBody(int expectedVersion, string title) => new
    {
        title,
        priority = "Medium",
        reviewRequired = false,
        emailNotificationsEnabled = true,
        delegationAllowed = true,
        fieldValues = Array.Empty<object>(),
        expectedVersion
    };

    private static async Task<((HttpStatusCode Status, string Body), (HttpStatusCode Status, string Body))> TogetherAsync(
        Func<Task<(HttpStatusCode Status, string Body)>> first,
        Func<Task<(HttpStatusCode Status, string Body)>> second)
    {
        // Both callers are parked on the same gate and released by one signal.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = Task.Run(async () => { await gate.Task; return await first(); });
        var b = Task.Run(async () => { await gate.Task; return await second(); });
        gate.SetResult();
        await Task.WhenAll(a, b);
        return (a.Result, b.Result);
    }

    private static void AssertExactlyOneWon((HttpStatusCode Status, string Body) a, (HttpStatusCode Status, string Body) b)
    {
        var winners = new[] { a, b }.Count(r => r.Status == HttpStatusCode.NoContent);
        Assert.True(winners == 1, $"expected exactly one winner: {a.Status} {a.Body} / {b.Status} {b.Body}");

        var loser = a.Status == HttpStatusCode.NoContent ? b : a;
        Assert.Equal(HttpStatusCode.Conflict, loser.Status);
        Assert.Contains(ReasonCode(loser.Body), new[]
        {
            TaskReasonCodes.ConcurrencyConflict, TaskReasonCodes.RequestNotPending, TaskReasonCodes.AlreadyAccepted
        });
    }

    private async Task<IReadOnlyList<JsonElement>> MineAsync(string bearer, bool team = false)
    {
        var board = await _host.GetAsync("/api/v1/work-items/mine" + (team ? "?scope=Team" : string.Empty), bearer);
        Assert.True(board.Status == HttpStatusCode.OK, board.Body);
        using var document = JsonDocument.Parse(board.Body);
        return document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(item => item.Clone())
            .ToList();
    }

    private static Guid IdOf(JsonElement item) => Guid.Parse(item.GetProperty("id").GetString()!);

    private static string[] ActionCodes(JsonElement item)
        => item.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("code").GetString()!).ToArray();

    private static string? ReasonCode(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) { return null; }
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
    }

    private TenantContext In(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return context;
    }

    private TaskItemRepository TasksIn(Guid tenant)
        => new(_harness.DbContext, In(tenant), new TaskTransitionRepository(_harness.DbContext, In(tenant)));

    private async Task<TaskItem> StoredAsync(Guid id) => (await TasksIn(_harness.TenantId).GetByIdAsync(id))!;

    private async Task<int> VersionAsync(Guid id) => (await StoredAsync(id)).Version;

    private Task<IReadOnlyList<TaskItem>> AllTasksAsync(Guid tenant) => TasksIn(tenant).GetAllForTenantAsync();

    private Task<IReadOnlyList<TaskTransition>> HistoryAsync(Guid id)
        => new TaskTransitionRepository(_harness.DbContext, In(_harness.TenantId)).ListByTaskIdAsync(id);

    private Task<IReadOnlyList<TaskAssignment>> OwnershipLogAsync(Guid id)
        => new TaskAssignmentRepository(_harness.DbContext, In(_harness.TenantId)).ListByTaskIdAsync(id);

    private async Task<Guid> UnitAsync(Guid tenant, string code, Guid legalEntity)
        => (await new OrganizationUnitRepository(_harness.DbContext, In(tenant)).CreateAsync(new OrganizationUnit
        {
            TenantId = tenant, Code = code, Name = code, LegalEntityId = legalEntity,
            Status = OrgUnitStatus.Active, OrgUnitType = OrgUnitType.HQ
        })).Id;

    private async Task<Guid> PositionAsync(Guid tenant, string code, Guid unit, Guid? reportsTo = null)
        => (await new PositionRepository(_harness.DbContext, In(tenant)).CreateAsync(new Position
        {
            TenantId = tenant, Code = code, Name = code, OrganizationUnitId = unit,
            ReportsToPositionId = reportsTo, Status = PositionStatus.Active
        })).Id;

    private Task SeatAsync(Guid tenant, Guid user, Guid position)
        => new PositionAssignmentRepository(_harness.DbContext, In(tenant)).CreateAsync(new PositionAssignment
        {
            TenantId = tenant, PositionId = position, UserId = user, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
        });

    private Task DeleteAsync<T>(string collection, Guid[] tenants) where T : Diten.Platform.Common.Persistence.TenantScopedEntity
        => _harness.Database.GetCollection<T>(collection).DeleteManyAsync(Builders<T>.Filter.In(x => x.TenantId, tenants));

    // ── doubles ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>What MOD-0018's resolver emits for a person, derived from the SAME seats the test wrote: their
    /// positions, those positions' units, and the units' legal entities.</summary>
    private sealed class SeatScopes(
        ITaskSeatDirectory seats, IPositionRepository positions, IOrganizationUnitRepository units) : IDataScopeResolver
    {
        public async Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
            Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken cancellationToken)
        {
            var scopes = new List<EntitlementDataScope>();
            foreach (var seat in await seats.ActiveForUserAsync(userId, cancellationToken))
            {
                var position = await positions.GetByIdAsync(seat.PositionId, cancellationToken);
                var unit = position is null ? null : await units.GetByIdAsync(position.OrganizationUnitId, cancellationToken);
                if (position is null || unit is null) { continue; }

                scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.Position, position.Id, position.Code));
                scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, unit.Id, unit.Code));
                scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, unit.LegalEntityId, unit.Code));
            }

            return scopes;
        }
    }

    /// <summary>AuthService's contact read: tenant-first, and a switched-off account is simply absent.</summary>
    private sealed class TenantAccounts(ITenantContext tenant, IReadOnlyDictionary<Guid, HashSet<Guid>> active)
        : ITaskNotificationRecipientResolver
    {
        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(
                userIds
                    .Where(id => active.TryGetValue(tenant.TenantId, out var known) && known.Contains(id))
                    .Select(id => new TaskNotificationRecipient(id, $"{id:N}@tenant.example", null))
                    .ToList());
    }

    private sealed class Host : IDisposable
    {
        private const string Issuer = "diten-auth-bl509-d1";
        private const string Audience = "diten-platform-bl509-d1";
        private const string Secret = "BL-509 task request D1 signing key, test only, 0123456789abcdef0123";

        private readonly TestServer _server;

        public Host(MongoIntegrationHarness harness, IReadOnlyDictionary<Guid, HashSet<Guid>> activeAccounts)
        {
            var names = new FakeUserDisplayNameResolver(
                (Deniz, "Deniz Kaya"), (Burak, "Burak Aydın"), (Ewa, "Ewa Nowak"), (Piotr, "Piotr Zieliński"),
                (Marek, "Marek Kowalski"), (Elif, "Elif Şahin"));

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.MapInboundClaims = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = Issuer,
                                ValidAudience = Audience,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                                ClockSkew = TimeSpan.Zero
                            };
                        });
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();

                    // Per request, from the token — the tenant every Mongo read below is bound to.
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId("corr-bl509");
                        return correlation;
                    });
                    services.AddScoped<IActorPermissionContext>(sp =>
                        new ClaimsActorPermissionContext(sp.GetRequiredService<IHttpContextAccessor>()));

                    // The REAL stores, on the real database, in the request's tenant — tasks AND the org chart.
                    services.AddScoped<ITaskTransitionRepository>(sp =>
                        new TaskTransitionRepository(harness.DbContext, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<ITaskItemRepository>(sp => new TaskItemRepository(
                        harness.DbContext, sp.GetRequiredService<ITenantContext>(),
                        sp.GetRequiredService<ITaskTransitionRepository>()));
                    services.AddScoped<ITaskAssignmentRepository>(sp =>
                        new TaskAssignmentRepository(harness.DbContext, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<IOrganizationUnitRepository>(sp =>
                        new OrganizationUnitRepository(harness.DbContext, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<IPositionRepository>(sp =>
                        new PositionRepository(harness.DbContext, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<IPositionAssignmentRepository>(sp =>
                        new PositionAssignmentRepository(harness.DbContext, sp.GetRequiredService<ITenantContext>()));

                    // The REAL rules over them.
                    services.AddScoped<ITaskSeatDirectory, TaskSeatDirectory>();
                    services.AddScoped<IDataScopeResolver, SeatScopes>();
                    services.AddScoped<ITaskAssignmentScopeResolver, TaskAssignmentScopeResolver>();
                    services.AddScoped<ITaskTeamResolver, TaskTeamResolver>();
                    services.AddScoped<ITaskAssignmentGuard, TaskAssignmentGuard>();
                    services.AddScoped<ITaskNotificationRecipientResolver>(sp =>
                        new TenantAccounts(sp.GetRequiredService<ITenantContext>(), activeAccounts));
                    services.AddScoped<ITaskRequestService, TaskRequestService>();
                    services.AddScoped<ITaskReadAccessPolicy>(sp => new TaskReadAccessPolicy(
                        sp.GetRequiredService<ITaskItemRepository>(),
                        new FakeTaskWatcherRepository(),
                        new FakeTaskNotificationService(),
                        sp.GetRequiredService<IOrganizationUnitRepository>(),
                        sp.GetRequiredService<ITaskAssignmentScopeResolver>(),
                        sp.GetRequiredService<ITaskTeamResolver>(),
                        sp.GetRequiredService<IActorPermissionContext>(),
                        sp.GetRequiredService<ICurrentUserContext>()));

                    services.AddScoped<IEnumerable<IWorkItemProvider>>(sp => new IWorkItemProvider[]
                    {
                        new TaskWorkItemProvider(
                            sp.GetRequiredService<ITaskItemRepository>(),
                            sp.GetRequiredService<ITaskSeatDirectory>(),
                            new TaskLifecycleService(),
                            new TaskAssignmentResolver(),
                            names,
                            new FakeChecklistRunRepository(),
                            new FakeTaskApprovalService(),
                            new FakeTaskDependencyRepository(),
                            new FakeTaskCommentRepository(),
                            sp.GetRequiredService<ITaskTransitionRepository>(),
                            new FakeTaskPersonalOverlayRepository(),
                            new FakeTaskWatcherRepository(),
                            sp.GetRequiredService<IActorPermissionContext>(),
                            sp.GetRequiredService<IPositionRepository>(),
                            sp.GetRequiredService<IOrganizationUnitRepository>(),
                            SlaForTests.Real(),
                            new FakeTaskFieldDefinitionRepository(),
                            new FakeTaskTypeRepository(),
                            teamResolver: sp.GetRequiredService<ITaskTeamResolver>())
                    });
                    services.AddScoped<IMediator>(sp => new Router(sp, Notifications));
                    services.AddScoped<IEnumerable<IWorkItemActionDispatcher>>(sp =>
                        new IWorkItemActionDispatcher[] { new TaskWorkItemActionDispatcher(sp.GetRequiredService<IMediator>()) });
                    services.AddControllers().AddApplicationPart(typeof(TasksController).Assembly);
                })
                .Configure(app =>
                {
                    // Same order as Diten.Platform.API Program.cs.
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public FakeTaskNotificationService Notifications { get; } = new();

        public string Token(Guid user, Guid tenant, params string[] keys)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.ToString()),
                new(JwtRegisteredClaimNames.Email, $"{user:N}@tenant.example"),
                new("tenant_id", tenant.ToString()),
                new("actor_type", "tenant_user")
            };
            claims.AddRange(keys.Select(key => new Claim("permission", key)));

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public Task<(HttpStatusCode Status, string Body)> GetAsync(string path, string bearer)
            => SendAsync(HttpMethod.Get, path, bearer, null);

        public Task<(HttpStatusCode Status, string Body)> PostAsync(string path, string bearer, object body)
            => SendAsync(HttpMethod.Post, path, bearer, body);

        public Task<(HttpStatusCode Status, string Body)> PutAsync(string path, string bearer, object body)
            => SendAsync(HttpMethod.Put, path, bearer, body);

        private async Task<(HttpStatusCode Status, string Body)> SendAsync(
            HttpMethod method, string path, string bearer, object? body)
        {
            var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _server.CreateClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public void Dispose() => _server.Dispose();
    }

    /// <summary>Routes each request this flow sends to its REAL handler, built over the request's own services.</summary>
    private sealed class Router(IServiceProvider services, FakeTaskNotificationService notifications) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            var tasks = services.GetRequiredService<ITaskItemRepository>();
            var events = services.GetRequiredService<ITaskAssignmentRepository>();
            var currentUser = services.GetRequiredService<ICurrentUserContext>();
            var tenant = services.GetRequiredService<ITenantContext>();
            var requests = services.GetRequiredService<ITaskRequestService>();
            var guard = services.GetRequiredService<ITaskAssignmentGuard>();
            var units = services.GetRequiredService<IOrganizationUnitRepository>();
            var fields = new TaskFieldDefinitionService(
                new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll());

            object response = request switch
            {
                GetMyWorkItemsQuery query => await new GetMyWorkItemsHandler(
                    services.GetRequiredService<IEnumerable<IWorkItemProvider>>(),
                    currentUser,
                    Options.Create(new WorkAggregationResilienceOptions()),
                    NullLogger<GetMyWorkItemsHandler>.Instance).Handle(query, ct),
                GetTaskWorkItemByIdQuery query => await new GetTaskWorkItemByIdHandler(
                    tasks,
                    services.GetRequiredService<ITaskReadAccessPolicy>(),
                    currentUser,
                    services.GetRequiredService<IEnumerable<IWorkItemProvider>>()).Handle(query, ct),

                // The production validation pipeline in front of the production create handler.
                CreateTaskItemCommand command => await new ValidationBehavior<CreateTaskItemCommand, Response<Guid>>(
                        [new CreateTaskItemValidator()])
                    .Handle(command, () => new CreateTaskItemHandler(
                        tasks,
                        events,
                        new FakeTaskWatcherRepository(),
                        services.GetRequiredService<IPositionRepository>(),
                        units,
                        services.GetRequiredService<ITaskSeatDirectory>(),
                        fields,
                        new TaskLifecycleService(),
                        new FakeTaskApprovalService(),
                        new FakeChecklistTemplateRepository(),
                        new FakeChecklistRunRepository(),
                        new TaskChecklistService(),
                        notifications,
                        currentUser,
                        tenant,
                        NullLogger<CreateTaskItemHandler>.Instance,
                        TaskDocumentFreezerDoubles.OverAnEmptyRegister(),
                        guard,
                        requests: requests).Handle(command, ct), ct),

                AcceptTaskItemCommand command => await new AcceptTaskItemHandler(
                    tasks, events, currentUser, tenant, requests).Handle(command, ct),
                DeclineTaskOfferCommand command => await new DeclineTaskOfferHandler(
                    tasks, events, requests, currentUser, tenant).Handle(command, ct),
                WithdrawTaskOfferCommand command => await new WithdrawTaskOfferHandler(
                    tasks, events, requests, currentUser, tenant).Handle(command, ct),
                OfferTaskItemCommand command => await new OfferTaskItemHandler(
                    tasks, events, requests, currentUser, tenant).Handle(command, ct),
                ReturnTaskItemCommand command => await new ReturnTaskItemHandler(
                    tasks, events, currentUser, tenant).Handle(command, ct),
                ReassignTaskItemCommand command => await new ReassignTaskItemHandler(
                    tasks, events, guard, currentUser, tenant).Handle(command, ct),
                UpdateTaskItemCommand command => await new UpdateTaskItemHandler(
                    tasks, units, fields, currentUser, new FakeTaskApprovalService(), new FakeTaskReviewService(),
                    NullLogger<UpdateTaskItemHandler>.Instance,
                    TaskDocumentFreezerDoubles.OverAnEmptyRegister()).Handle(command, ct),
                TransitionTaskItemCommand command => await new TransitionTaskItemHandler(
                    tasks, new TaskLifecycleService(), currentUser,
                    new FakeChecklistRunRepository(), new TaskChecklistService(),
                    new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository(),
                    new FakeTaskTypeRepository(), notifications, fields,
                    new FakeTaskAttachmentRepository(),
                    NullLogger<TransitionTaskItemHandler>.Instance).Handle(command, ct),
                PlanTaskItemCommand command => await new PlanTaskItemHandler(
                    tasks, new TaskLifecycleService(), currentUser,
                    new FakeWorkingHoursProvider(), new FakeCalendarMeetingReader()).Handle(command, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };
            return (TResponse)response;
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
