using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E3 — signature samples ("İmzam") end to end over HTTP against the test-owned replica set: save with a PASSWORD
/// ticket only, Pending under the tenant's approval rule, no self-approval, one Active per owner, a used sample never
/// withdrawn, and the signing port — a signature shows only the signer's own Active sample.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class ESignatureSampleHttpMongoTests : IClassFixture<ESignatureTestHost>
{
    private const string Samples = "api/v1/esign/samples";
    private static readonly string[] AllKeys = ESignPermissions.All.ToArray();

    private readonly ESignatureTestHost _host;

    public ESignatureSampleHttpMongoTests(ESignatureTestHost host) => _host = host;

    // ── save ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_needs_a_password_ticket_for_intent_sample_never_a_window_ticket()
    {
        var a = NewTenant();
        var png = TestPng.Create(300, 100);

        var (missing, missingBody) = await SaveRawAsync(a, ticket: null, png);
        Assert.Equal(400, missing);
        Assert.Equal(ESignCodes.TicketRequired, Reason(missingBody));

        var window = _host.Tickets.Issue(a.Tenant, a.User, "sample", ESignatureReauthKind.Window);
        var (windowStatus, windowBody) = await SaveRawAsync(a, window, png);
        Assert.Equal(403, windowStatus);
        Assert.Equal(ESignCodes.ReauthRequired, Reason(windowBody));

        var signIntent = _host.Tickets.Issue(a.Tenant, a.User, "sign");
        var (wrongIntent, wrongIntentBody) = await SaveRawAsync(a, signIntent, png);
        Assert.Equal(403, wrongIntent);
        Assert.Equal(ESignCodes.TicketInvalid, Reason(wrongIntentBody));

        Assert.Empty(await SampleRows(a.Tenant));
        Assert.Equal(201, (await SaveRawAsync(a, _host.Tickets.Issue(a.Tenant, a.User, "sample"), png)).Status);
    }

    [Fact]
    public async Task Saving_checks_the_image_at_byte_level_before_spending_the_ticket()
    {
        var a = NewTenant();
        var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0xFF, 0xD9, 0, 0, 0 };
        var calls = _host.Tickets.Calls;
        var (notPng, notPngBody) = await SaveRawAsync(a, _host.Tickets.Issue(a.Tenant, a.User, "sample"), jpg);
        Assert.Equal(415, notPng);
        Assert.Equal(ESignCodes.ImageNotPng, Reason(notPngBody));

        var huge = new byte[1_200_000];
        TestPng.Create(10, 10).CopyTo(huge, 0);
        Assert.Equal(413, (await SaveRawAsync(a, _host.Tickets.Issue(a.Tenant, a.User, "sample"), huge)).Status);
        Assert.Equal(calls, _host.Tickets.Calls);
    }

    // ── approval (CT-D5, CT-D15) ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task With_approval_required_a_new_sample_is_pending_and_its_owner_cannot_decide_it()
    {
        var a = NewTenant();
        var b = Colleague(a);
        var sample = await SaveAsync(a);
        Assert.Equal("pending", sample.GetProperty("status").GetString());
        var id = sample.GetProperty("id").GetGuid();

        using var asOwner = _host.Client(a.Token);
        var selfApprove = await asOwner.PostAsync($"{Samples}/{id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);
        Assert.Equal(ESignCodes.SelfApprovalForbidden, Reason(await selfApprove.Content.ReadAsStringAsync()));
        var selfReject = await asOwner.PostAsJsonAsync($"{Samples}/{id}/reject", new { reason = "Not my best signature at all" });
        Assert.Equal(HttpStatusCode.Forbidden, selfReject.StatusCode);
        Assert.Equal("pending", (await MineAsync(a)).Single().GetProperty("status").GetString());

        using var asApprover = _host.Client(b.Token);
        var approved = await asApprover.PostAsync($"{Samples}/{id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var body = Data(await approved.Content.ReadAsStringAsync());
        Assert.Equal("active", body.GetProperty("status").GetString());
        Assert.Equal(b.User.ToString("D"), body.GetProperty("decision").GetProperty("by").GetString());

        var again = await asApprover.PostAsync($"{Samples}/{id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ESignCodes.SampleNotPending, Reason(await again.Content.ReadAsStringAsync()));
        Assert.Equal(1, await Outbox(a.Tenant, ESignAuditEvents.SampleApproved));
    }

    [Fact]
    public async Task Rejection_needs_a_reason_and_lands_in_the_decision_log()
    {
        var a = NewTenant();
        var b = Colleague(a);
        var id = (await SaveAsync(a)).GetProperty("id").GetGuid();
        using var asApprover = _host.Client(b.Token);

        Assert.Equal(HttpStatusCode.BadRequest, (await asApprover.PostAsJsonAsync($"{Samples}/{id}/reject", new { reason = "blurry" })).StatusCode);
        var rejected = await asApprover.PostAsJsonAsync($"{Samples}/{id}/reject", new { reason = "The drawing is cut off at the right" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("rejected", Data(await rejected.Content.ReadAsStringAsync()).GetProperty("status").GetString());

        var log = Data(await asApprover.GetStringAsync($"{Samples}/decisions"));
        var entry = Assert.Single(log.EnumerateArray());
        Assert.Equal("rejected", entry.GetProperty("decision").GetString());
        Assert.Equal("The drawing is cut off at the right", entry.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Two_approvers_deciding_one_sample_in_parallel_have_exactly_one_winner()
    {
        var a = NewTenant();
        var id = (await SaveAsync(a)).GetProperty("id").GetGuid();
        var approvers = Enumerable.Range(0, 6).Select(_ => Colleague(a)).ToList();

        var results = await Task.WhenAll(approvers.Select(async approver =>
        {
            using var client = _host.Client(approver.Token);
            return (await client.PostAsync($"{Samples}/{id}/approve", null)).StatusCode;
        }));

        Assert.Equal(1, results.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(5, results.Count(s => s == HttpStatusCode.Conflict));
        Assert.Single(await Events(a.Tenant, id, ESignatureSampleEventKind.Approved));
    }

    // ── one Active per owner ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_new_active_sample_makes_the_previous_one_inactive()
    {
        var a = NewTenant();
        await DisableApprovalAsync(a);
        var first = await SaveAsync(a);
        Assert.Equal("active", first.GetProperty("status").GetString());
        var second = await SaveAsync(a);
        Assert.Equal("active", second.GetProperty("status").GetString());

        var mine = await MineAsync(a);
        Assert.Equal("active", mine.Single(s => s.GetProperty("id").GetGuid() == second.GetProperty("id").GetGuid()).GetProperty("status").GetString());
        Assert.Equal("inactive", mine.Single(s => s.GetProperty("id").GetGuid() == first.GetProperty("id").GetGuid()).GetProperty("status").GetString());
        var pointer = await Database.GetCollection<ESignatureSampleOwnerState>(PlatformCollections.ESignatureSampleOwnerStates)
            .Find(s => s.TenantId == a.Tenant && s.OwnerUserId == a.User.ToString("D")).SingleAsync();
        Assert.Equal(second.GetProperty("id").GetGuid(), pointer.ActiveSampleId);
    }

    [Fact]
    public async Task Approving_a_second_sample_replaces_the_active_one_and_parallel_approvals_leave_one_active()
    {
        var a = NewTenant();
        var first = (await SaveAsync(a)).GetProperty("id").GetGuid();
        var second = (await SaveAsync(a)).GetProperty("id").GetGuid();
        var third = (await SaveAsync(a)).GetProperty("id").GetGuid();
        var b = Colleague(a);
        var c = Colleague(a);
        using (var asB = _host.Client(b.Token))
        {
            Assert.Equal(HttpStatusCode.OK, (await asB.PostAsync($"{Samples}/{first}/approve", null)).StatusCode);
        }

        var parallel = await Task.WhenAll(new[] { (b, second), (c, third) }.Select(async x =>
        {
            using var client = _host.Client(x.Item1.Token);
            return (await client.PostAsync($"{Samples}/{x.Item2}/approve", null)).StatusCode;
        }));
        Assert.Contains(HttpStatusCode.OK, parallel);

        var active = (await MineAsync(a)).Where(s => s.GetProperty("status").GetString() == "active").ToList();
        Assert.Single(active);
        Assert.Equal("inactive", (await MineAsync(a)).Single(s => s.GetProperty("id").GetGuid() == first).GetProperty("status").GetString());
    }

    // ── signing with a sample (the E2 port) ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_the_signers_own_active_sample_can_be_shown_and_the_record_carries_its_id_and_hash()
    {
        var a = NewTenant();
        var b = Colleague(a);
        var subjectId = _host.Subjects.Add(a.Tenant);
        var saved = await SaveAsync(a, TestPng.Create(380, 140));
        var sampleId = saved.GetProperty("id").GetGuid();

        // Pending ⇒ 409, and no ticket is spent on it.
        var calls = _host.Tickets.Calls;
        var (pending, pendingBody) = await SignWithSampleAsync(a, subjectId, sampleId);
        Assert.Equal(409, pending);
        Assert.Equal(ESignCodes.SampleNotActive, Reason(pendingBody));
        Assert.Equal(calls, _host.Tickets.Calls);

        using (var asB = _host.Client(b.Token))
        {
            Assert.Equal(HttpStatusCode.OK, (await asB.PostAsync($"{Samples}/{sampleId}/approve", null)).StatusCode);
        }

        // Someone else's active sample ⇒ 409.
        var (foreign, foreignBody) = await SignWithSampleAsync(b, subjectId, sampleId);
        Assert.Equal(409, foreign);
        Assert.Equal(ESignCodes.SampleNotActive, Reason(foreignBody));

        // A window ticket never carries a saved sample (KS-10) — even inside a policy window.
        await SavePolicyAsync(a, reauthEvery: false, sampleApprovalRequired: true);
        var (windowed, windowedBody) = await SignWithSampleAsync(a, subjectId, sampleId, ESignatureReauthKind.Window);
        Assert.Equal(403, windowed);
        Assert.Equal(ESignCodes.ReauthRequired, Reason(windowedBody));

        var (ok, okBody) = await SignWithSampleAsync(a, subjectId, sampleId);
        Assert.Equal(201, ok);
        var record = Data(okBody);
        Assert.Equal("sample", record.GetProperty("appearance").GetProperty("type").GetString());
        Assert.Equal(sampleId, record.GetProperty("appearance").GetProperty("sampleId").GetGuid());
        Assert.Equal(saved.GetProperty("imageHash").GetString(), record.GetProperty("appearance").GetProperty("imageHash").GetString());

        using var asA = _host.Client(a.Token);
        var verify = Data(await (await asA.PostAsync($"api/v1/esign/signatures/{record.GetProperty("id").GetGuid()}/verify", null)).Content.ReadAsStringAsync());
        Assert.Equal("valid", verify.GetProperty("status").GetString());
        Assert.True(verify.GetProperty("imageIntact").GetBoolean());
        Assert.Equal(1, (await MineAsync(a)).Single().GetProperty("usedCount").GetInt64());
    }

    [Fact]
    public async Task The_signing_dialog_sees_the_callers_active_and_pending_samples()
    {
        var a = NewTenant();
        var b = Colleague(a);
        var subjectId = _host.Subjects.Add(a.Tenant);
        var active = (await SaveAsync(a)).GetProperty("id").GetGuid();
        using (var asB = _host.Client(b.Token))
        {
            await asB.PostAsync($"{Samples}/{active}/approve", null);
        }

        var pending = (await SaveAsync(a)).GetProperty("id").GetGuid();
        using var asA = _host.Client(a.Token);
        var effective = Data(await asA.GetStringAsync(
            $"api/v1/esign/policy/effective?module={TestSubjectProvider.Module}&subjectType={TestSubjectProvider.SubjectType}&subjectId={subjectId}"));
        Assert.Equal(active, effective.GetProperty("mySample").GetProperty("id").GetGuid());
        Assert.Equal(pending, effective.GetProperty("myPendingSample").GetProperty("id").GetGuid());
    }

    // ── deactivate / withdraw ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_used_sample_is_never_withdrawn_only_deactivated_and_an_unused_rejected_one_can_be()
    {
        var a = NewTenant();
        var b = Colleague(a);
        await DisableApprovalAsync(a);
        var subjectId = _host.Subjects.Add(a.Tenant);
        var used = (await SaveAsync(a)).GetProperty("id").GetGuid();
        Assert.Equal(201, (await SignWithSampleAsync(a, subjectId, used)).Status);
        using var asA = _host.Client(a.Token);

        var activeWithdraw = await asA.PostAsync($"{Samples}/{used}/withdraw", null);
        Assert.Equal(HttpStatusCode.Conflict, activeWithdraw.StatusCode);
        Assert.Equal(ESignCodes.SampleInUse, Reason(await activeWithdraw.Content.ReadAsStringAsync()));

        Assert.Equal(HttpStatusCode.OK, (await asA.PostAsync($"{Samples}/{used}/deactivate", null)).StatusCode);
        var usedWithdraw = await asA.PostAsync($"{Samples}/{used}/withdraw", null);
        Assert.Equal(HttpStatusCode.Conflict, usedWithdraw.StatusCode);
        Assert.Equal(ESignCodes.SampleInUse, Reason(await usedWithdraw.Content.ReadAsStringAsync()));
        Assert.Equal(409, (await SignWithSampleAsync(a, subjectId, used)).Status); // inactive

        // An unused, rejected sample can be withdrawn; nothing is ever deleted.
        await SavePolicyAsync(a, reauthEvery: true, sampleApprovalRequired: true);
        var unused = (await SaveAsync(a)).GetProperty("id").GetGuid();
        using (var asB = _host.Client(b.Token))
        {
            await asB.PostAsJsonAsync($"{Samples}/{unused}/reject", new { reason = "Please draw it larger next time" });
        }

        var withdrawn = await asA.PostAsync($"{Samples}/{unused}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal("withdrawn", Data(await withdrawn.Content.ReadAsStringAsync()).GetProperty("status").GetString());
        Assert.Equal(2, (await SampleRows(a.Tenant)).Count);

        // Only the owner may deactivate / withdraw: anyone else gets 404.
        using var asB2 = _host.Client(b.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await asB2.PostAsync($"{Samples}/{used}/deactivate", null)).StatusCode);
    }

    // ── image and listings ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_image_is_served_to_its_owner_and_approvers_only()
    {
        var a = NewTenant();
        var outsider = a with { User = Guid.NewGuid() };
        outsider = outsider with { Token = ESignatureTestHost.Token(outsider.Tenant, outsider.User, [ESignPermissions.MySamplesManage]) };
        var png = TestPng.Create(200, 80);
        var id = (await SaveAsync(a, png)).GetProperty("id").GetGuid();

        using var asOwner = _host.Client(a.Token);
        var mine = await asOwner.GetAsync($"{Samples}/{id}/image");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal("image/png", mine.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await mine.Content.ReadAsByteArrayAsync());

        using var asOutsider = _host.Client(outsider.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await asOutsider.GetAsync($"{Samples}/{id}/image")).StatusCode);

        var approver = Colleague(a);
        using var asApprover = _host.Client(approver.Token);
        Assert.Equal(HttpStatusCode.OK, (await asApprover.GetAsync($"{Samples}/{id}/image")).StatusCode);
        var pending = Data(await asApprover.GetStringAsync($"{Samples}/pending"));
        Assert.Contains(pending.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);

        // Another tenant sees nothing.
        var other = NewTenant();
        using var asOther = _host.Client(other.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await asOther.GetAsync($"{Samples}/{id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await asOther.PostAsync($"{Samples}/{id}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Sample_records_and_events_are_insert_only_and_carry_no_ticket_or_image_bytes()
    {
        var a = NewTenant();
        var png = TestPng.Create(260, 90);
        var ticket = _host.Tickets.Issue(a.Tenant, a.User, "sample");
        var (status, _) = await SaveRawAsync(a, ticket, png);
        Assert.Equal(201, status);

        var row = (await SampleRows(a.Tenant)).Single();
        var json = row.ToJson();
        Assert.False(json.Contains(ticket, StringComparison.Ordinal), "the sample row carries the plain ticket");
        Assert.False(json.Contains(Convert.ToBase64String(png)[..64], StringComparison.Ordinal), "the sample row carries image bytes");
        Assert.Equal(ESignatureDigest.ImageHash(png), row["ImageHash"].AsString);
        Assert.Equal(1, await Outbox(a.Tenant, ESignAuditEvents.SampleSubmitted));

        var logs = _host.Logs.Text;
        Assert.False(logs.Contains(ticket, StringComparison.Ordinal), "a log line contains the plain ticket");
        Assert.False(logs.Contains(Convert.ToBase64String(png)[..64], StringComparison.Ordinal), "a log line contains image bytes");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record TenantCtx(Guid Tenant, Guid User, string Token);

    private IMongoDatabase Database => _host.Database;

    private static TenantCtx NewTenant()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        return new TenantCtx(tenant, user, ESignatureTestHost.Token(tenant, user, AllKeys));
    }

    private static TenantCtx Colleague(TenantCtx of)
    {
        var user = Guid.NewGuid();
        return new TenantCtx(of.Tenant, user, ESignatureTestHost.Token(of.Tenant, user, AllKeys, name: "Burak Yıldız"));
    }

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private async Task<(int Status, string Body)> SaveRawAsync(TenantCtx t, string? ticket, byte[] png)
    {
        using var client = _host.Client(t.Token);
        var response = await client.PostAsJsonAsync(Samples, new
        {
            ticket, kind = "drawn", pngBase64 = Convert.ToBase64String(png), strokes = new[] { new[] { new[] { 0.1, 0.2 }, new[] { 0.3, 0.4 } } },
            idempotencyKey = Key()
        });
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> SaveAsync(TenantCtx t, byte[]? png = null)
    {
        var (status, body) = await SaveRawAsync(t, _host.Tickets.Issue(t.Tenant, t.User, "sample"), png ?? TestPng.Create(240, 80));
        Assert.True(status == 201, $"save: {status} {body}");
        return Data(body);
    }

    private async Task<List<JsonElement>> MineAsync(TenantCtx t)
    {
        using var client = _host.Client(t.Token);
        return Data(await client.GetStringAsync($"{Samples}/mine")).EnumerateArray().ToList();
    }

    private async Task<(int Status, string Body)> SignWithSampleAsync(TenantCtx t, string subjectId, Guid sampleId,
        ESignatureReauthKind kind = ESignatureReauthKind.Password)
    {
        using var client = _host.Client(t.Token);
        var response = await client.PostAsJsonAsync("api/v1/esign/signatures", new
        {
            ticket = _host.Tickets.Issue(t.Tenant, t.User, "sign", kind, DateTime.UtcNow.AddMinutes(-1)),
            subject = new { module = TestSubjectProvider.Module, subjectType = TestSubjectProvider.SubjectType, subjectId },
            meaningCode = "APPROVE",
            appearance = new { type = "sample", sampleId },
            idempotencyKey = Key()
        });
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task SavePolicyAsync(TenantCtx t, bool reauthEvery, bool sampleApprovalRequired)
    {
        using var client = _host.Client(t.Token);
        var version = Data(await client.GetStringAsync("api/v1/esign/policy")).GetProperty("tenant").GetProperty("version").GetInt32();
        var response = await client.PutAsJsonAsync("api/v1/esign/policy/tenant", new
        {
            rows = Array.Empty<object>(), reauthEvery, reauthWindowMinutes = 15, sampleApprovalRequired, reason = "Quality decision QD-2026-21", expectedVersion = version
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private Task DisableApprovalAsync(TenantCtx t) => SavePolicyAsync(t, reauthEvery: true, sampleApprovalRequired: false);

    private async Task<List<BsonDocument>> SampleRows(Guid tenant) =>
        await Database.GetCollection<BsonDocument>(PlatformCollections.ESignatureSamples)
            .Find(Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard))).ToListAsync();

    private async Task<List<ESignatureSampleEvent>> Events(Guid tenant, Guid sampleId, ESignatureSampleEventKind kind) =>
        await Database.GetCollection<ESignatureSampleEvent>(PlatformCollections.ESignatureSampleEvents)
            .Find(e => e.TenantId == tenant && e.SampleId == sampleId && e.Event == kind).ToListAsync();

    private async Task<long> Outbox(Guid tenant, string eventName) =>
        await Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox).CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("RequestType", eventName) & Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)));

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
}
