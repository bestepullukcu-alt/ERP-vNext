using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E2 — pack §16 E2 and the §13 failure paths that belong to E2, end to end over HTTP against a test-owned replica
/// set. Every test works in a fresh tenant; the signable-action catalog is global, so every test registers its own action
/// codes.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class ESignatureHttpMongoTests : IClassFixture<ESignatureTestHost>
{
    private const string Signatures = "api/v1/esign/signatures";
    private static readonly string[] AllKeys = ESignPermissions.All.ToArray();

    private readonly ESignatureTestHost _host;

    public ESignatureHttpMongoTests(ESignatureTestHost host) => _host = host;

    // ── sign ─────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Signing_creates_exactly_one_record_with_server_facts_hashes_and_its_audit_intent()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        var png = TestPng.Create(320, 120);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var (status, data) = await SignAsync(t, subjectId, "APPROVE", drawnPng: png);

        Assert.Equal(201, status);
        var id = data.GetProperty("id").GetGuid();
        Assert.Matches("^ESIG-\\d{4}-\\d{6}$", data.GetProperty("signatureNumber").GetString());
        Assert.Equal(t.User.ToString("D"), data.GetProperty("signer").GetProperty("userId").GetString());
        Assert.Equal("Onaylıyorum", data.GetProperty("meaningText").GetString());
        Assert.Equal("password", data.GetProperty("reauth").GetProperty("kind").GetString());
        Assert.InRange(data.GetProperty("signedAtUtc").GetDateTime().ToUniversalTime(), before, DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(ESignatureDigest.ImageHash(png), data.GetProperty("appearance").GetProperty("imageHash").GetString());

        var record = Assert.Single(await Records().Find(r => r.TenantId == t.Tenant).ToListAsync());
        Assert.Equal(id, record.Id);
        Assert.Equal(ESignatureDigest.SubjectHash(TestSubjectProvider.Module, TestSubjectProvider.SubjectType, subjectId,
            new Dictionary<string, string?> { ["status"] = "draft", ["quantity"] = "1200", ["lot"] = "PRS-26A014" }), record.SubjectHash);
        Assert.Equal(ESignatureDigest.SignatureDigest(record.DigestInput()), record.SignatureDigest);
        Assert.NotEqual(t.LastTicket, record.TicketRef);

        // The audit intent was written with the record (same transaction).
        var outbox = Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);
        var intent = Assert.Single(await outbox.Find(Builders<BsonDocument>.Filter.Eq("RequestType", ESignAuditEvents.Signed)
            & Builders<BsonDocument>.Filter.Eq("EntityId", new BsonBinaryData(id, GuidRepresentation.Standard))).ToListAsync());
        Assert.Equal("ESignature", intent["Payload"]["Category"].AsString);
        Assert.Equal("MOD-0022", intent["Payload"]["SourceModule"].AsString);
        Assert.False(intent.ToJson().Contains(t.LastTicket, StringComparison.Ordinal), "the audit intent carries the plain ticket");
    }

    [Fact]
    public async Task Sign_without_a_ticket_is_400_and_with_a_spent_or_foreign_ticket_403()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);

        var (missing, missingBody) = await SignRawAsync(t, subjectId, "APPROVE", ticket: null);
        Assert.Equal(400, missing);
        Assert.Equal(ESignCodes.TicketRequired, Reason(missingBody));

        var ticket = _host.Tickets.Issue(t.Tenant, t.User);
        Assert.Equal(201, (await SignRawAsync(t, subjectId, "APPROVE", ticket)).Status);
        var (spent, spentBody) = await SignRawAsync(t, subjectId, "APPROVE", ticket);
        Assert.Equal(403, spent);
        Assert.Equal(ESignCodes.TicketInvalid, Reason(spentBody));

        var other = NewTenant();
        var foreign = _host.Tickets.Issue(other.Tenant, other.User);
        Assert.Equal(403, (await SignRawAsync(t, subjectId, "APPROVE", foreign)).Status);
    }

    [Fact]
    public async Task AuthService_unreachable_fails_closed_with_503()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        _host.Tickets.Unavailable = true;
        try
        {
            var (status, body) = await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User));
            Assert.Equal(503, status);
            Assert.Equal(ESignCodes.ReauthUnavailable, Reason(body));
        }
        finally
        {
            _host.Tickets.Unavailable = false;
        }

        Assert.Empty(await Records().Find(r => r.TenantId == t.Tenant).ToListAsync());
    }

    [Fact]
    public async Task An_idempotent_replay_returns_the_first_record_and_does_not_consume_a_ticket()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        var key = "sign-" + Guid.NewGuid().ToString("N");
        var (first, firstBody) = await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User), idempotencyKey: key);
        Assert.Equal(201, first);
        var calls = _host.Tickets.Calls;

        var unusedTicket = _host.Tickets.Issue(t.Tenant, t.User);
        var (replay, replayBody) = await SignRawAsync(t, subjectId, "APPROVE", unusedTicket, idempotencyKey: key);

        Assert.Equal(200, replay);
        Assert.Equal(Data(firstBody).GetProperty("id").GetGuid(), Data(replayBody).GetProperty("id").GetGuid());
        Assert.Equal(calls, _host.Tickets.Calls); // the ticket was not consumed
        Assert.Equal(201, (await SignRawAsync(t, subjectId, "APPROVE", unusedTicket)).Status); // …so it still works
        Assert.Equal(2, await Records().CountDocumentsAsync(r => r.TenantId == t.Tenant));
    }

    [Fact]
    public async Task An_invisible_or_other_tenant_subject_is_404_and_spends_no_ticket()
    {
        var t = NewTenant();
        var other = NewTenant();
        var foreignSubject = _host.Subjects.Add(other.Tenant);
        var calls = _host.Tickets.Calls;

        var (status, body) = await SignRawAsync(t, foreignSubject, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User));
        Assert.Equal(404, status);
        Assert.Equal(ESignCodes.SubjectNotFound, Reason(body));
        Assert.Equal(404, (await SignRawAsync(t, "DOC-does-not-exist", "APPROVE", _host.Tickets.Issue(t.Tenant, t.User))).Status);
        Assert.Equal(calls, _host.Tickets.Calls); // the ticket is consumed only after subject, policy and appearance
    }

    [Fact]
    public async Task A_window_ticket_needs_a_policy_window_and_a_saved_sample_is_refused_until_E3()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        var window = _host.Tickets.Issue(t.Tenant, t.User, kind: ESignatureReauthKind.Window, verifiedAt: DateTime.UtcNow.AddMinutes(-5));

        // Default policy: ReauthEvery = true (KS-3) ⇒ a window ticket is not enough.
        var (refused, refusedBody) = await SignRawAsync(t, subjectId, "APPROVE", window);
        Assert.Equal(403, refused);
        Assert.Equal(ESignCodes.ReauthRequired, Reason(refusedBody));

        // Policy "once within 15 minutes" ⇒ the same kind of ticket (5 min old) signs; a 20-minute-old one does not.
        await SavePolicyAsync(t, null, rows: [], reauthEvery: false, windowMinutes: 15, expectedVersion: await TenantPolicyVersionAsync(t));
        Assert.Equal(201, (await SignRawAsync(t, subjectId, "APPROVE",
            _host.Tickets.Issue(t.Tenant, t.User, kind: ESignatureReauthKind.Window, verifiedAt: DateTime.UtcNow.AddMinutes(-5)))).Status);
        Assert.Equal(403, (await SignRawAsync(t, subjectId, "APPROVE",
            _host.Tickets.Issue(t.Tenant, t.User, kind: ESignatureReauthKind.Window, verifiedAt: DateTime.UtcNow.AddMinutes(-20)))).Status);

        // E2 has no samples: "sample" is always 409 ESIGN_SAMPLE_NOT_ACTIVE, and is checked BEFORE the ticket is spent.
        var calls = _host.Tickets.Calls;
        var (sample, sampleBody) = await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User), appearance: new { type = "sample", sampleId = Guid.NewGuid() });
        Assert.Equal(409, sample);
        Assert.Equal(ESignCodes.SampleNotActive, Reason(sampleBody));
        Assert.Equal(calls, _host.Tickets.Calls);
    }

    [Fact]
    public async Task Draw_now_policy_refuses_other_appearances_and_images_are_checked_at_byte_level()
    {
        var t = NewTenant();
        var action = await RegisterActionAsync(defaultVisualMode: "drawRequired");
        var subjectId = _host.Subjects.Add(t.Tenant);

        var (none, noneBody) = await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User), actionCode: action);
        Assert.Equal(409, none);
        Assert.Equal(ESignCodes.AppearanceNotAllowed, Reason(noneBody));
        Assert.Equal(409, (await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User), actionCode: action,
            appearance: new { type = "sample", sampleId = Guid.NewGuid() })).Status);

        var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0xFF, 0xD9, 0, 0, 0 };
        var (notPng, notPngBody) = await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User), actionCode: action,
            appearance: Drawn(jpg));
        Assert.Equal(415, notPng);
        Assert.Equal(ESignCodes.ImageNotPng, Reason(notPngBody));

        var huge = new byte[1_200_000];
        TestPng.Create(10, 10).CopyTo(huge, 0);
        var (tooLarge, tooLargeBody) = await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User), actionCode: action,
            appearance: Drawn(huge));
        Assert.Equal(413, tooLarge);
        Assert.Equal(ESignCodes.ImageTooLarge, Reason(tooLargeBody));

        var (ok, _) = await SignAsync(t, subjectId, "APPROVE", drawnPng: TestPng.Create(400, 150), actionCode: action);
        Assert.Equal(201, ok);
    }

    [Fact]
    public async Task A_platform_or_partner_admin_cannot_sign_even_with_the_key()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        foreach (var actorType in new[] { "platform_admin", "partner_admin" })
        {
            var token = ESignatureTestHost.Token(t.Tenant, t.User, AllKeys, actorType);
            using var client = _host.Client(token);
            var response = await client.PostAsJsonAsync(Signatures, SignBody(subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User)));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Empty(await Records().Find(r => r.TenantId == t.Tenant).ToListAsync());
    }

    // ── verify / invalidate ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_is_valid_then_changed_after_a_subject_change_and_tampered_after_a_flipped_png_byte()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        var (_, signed) = await SignAsync(t, subjectId, "APPROVE", drawnPng: TestPng.Create(200, 80));
        var id = signed.GetProperty("id").GetGuid();

        Assert.Equal("valid", (await VerifyAsync(t, id)).GetProperty("status").GetString());

        _host.Subjects.Change(t.Tenant, subjectId, "quantity", "1100");
        var changed = await VerifyAsync(t, id);
        Assert.Equal("changed", changed.GetProperty("status").GetString());
        Assert.NotEqual(changed.GetProperty("subjectHashAtSign").GetString(), changed.GetProperty("currentSubjectHash").GetString());
        _host.Subjects.Change(t.Tenant, subjectId, "quantity", "1200");
        Assert.Equal("valid", (await VerifyAsync(t, id)).GetProperty("status").GetString());

        var record = await Records().Find(r => r.Id == id).SingleAsync();
        var path = Path.Combine(_host.StorageRoot, record.AppearanceStoragePath!.Replace('/', Path.DirectorySeparatorChar));
        var bytes = await File.ReadAllBytesAsync(path);
        bytes[^20] ^= 0x01;
        await File.WriteAllBytesAsync(path, bytes);
        var tampered = await VerifyAsync(t, id);
        Assert.Equal("tampered", tampered.GetProperty("status").GetString());
        Assert.False(tampered.GetProperty("imageIntact").GetBoolean());

        _host.Subjects.Remove(t.Tenant, subjectId);
        bytes[^20] ^= 0x01;
        await File.WriteAllBytesAsync(path, bytes);
        Assert.Equal("subjectUnresolvable", (await VerifyAsync(t, id)).GetProperty("status").GetString());

        // Verification never wrote anything back.
        var after = await Records().Find(r => r.Id == id).SingleAsync();
        Assert.Equal(record.ToBsonDocument(), after.ToBsonDocument());
    }

    [Fact]
    public async Task Invalidation_inserts_one_row_leaves_the_record_untouched_and_a_second_one_is_409()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        var (_, signed) = await SignAsync(t, subjectId, "APPROVE");
        var id = signed.GetProperty("id").GetGuid();
        var recordBefore = (await Records().Find(r => r.Id == id).SingleAsync()).ToBsonDocument();
        using var client = _host.Client(t.Token);

        var shortReason = await client.PostAsJsonAsync($"{Signatures}/{id}/invalidate",
            new { ticket = _host.Tickets.Issue(t.Tenant, t.User, "invalidate"), reason = "too short", idempotencyKey = Key() });
        Assert.Equal(HttpStatusCode.BadRequest, shortReason.StatusCode);

        var window = await client.PostAsJsonAsync($"{Signatures}/{id}/invalidate",
            new { ticket = _host.Tickets.Issue(t.Tenant, t.User, "invalidate", ESignatureReauthKind.Window), reason = "Signed against the wrong lot number", idempotencyKey = Key() });
        Assert.Equal(HttpStatusCode.Forbidden, window.StatusCode);

        var ok = await client.PostAsJsonAsync($"{Signatures}/{id}/invalidate",
            new { ticket = _host.Tickets.Issue(t.Tenant, t.User, "invalidate"), reason = "Signed against the wrong lot number", idempotencyKey = Key() });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal("Signed against the wrong lot number", Data(await ok.Content.ReadAsStringAsync()).GetProperty("invalidation").GetProperty("reason").GetString());

        var second = await client.PostAsJsonAsync($"{Signatures}/{id}/invalidate",
            new { ticket = _host.Tickets.Issue(t.Tenant, t.User, "invalidate"), reason = "Second attempt to invalidate it", idempotencyKey = Key() });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ESignCodes.AlreadyInvalidated, Reason(await second.Content.ReadAsStringAsync()));

        Assert.Equal(1, await Database.GetCollection<ESignatureInvalidation>(PlatformCollections.ESignatureInvalidations)
            .CountDocumentsAsync(i => i.SignatureId == id));
        Assert.Equal(recordBefore, (await Records().Find(r => r.Id == id).SingleAsync()).ToBsonDocument());
        Assert.Equal("invalidated", (await VerifyAsync(t, id)).GetProperty("status").GetString());
    }

    // ── policy ───────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_company_override_beats_the_tenant_default_revert_removes_it_and_every_save_has_history()
    {
        var t = NewTenant();
        var company = Guid.NewGuid();
        _host.LegalEntities.Known[company] = "Grand Medical Ltd";
        var action = await RegisterActionAsync(defaultVisualMode: "optional");
        var subjectInCompany = _host.Subjects.Add(t.Tenant, company);
        var subjectElsewhere = _host.Subjects.Add(t.Tenant);

        var version = await TenantPolicyVersionAsync(t);
        await SavePolicyAsync(t, null, [new { actionCode = action, required = true, meaningCode = (string?)null, visualMode = "optional" }], true, 15, version);
        var created = await SavePolicyAsync(t, company,
            [new { actionCode = action, required = true, meaningCode = (string?)null, visualMode = "none" }], true, 15, null);
        Assert.Equal(200, created.Status);

        // In the company the override's "none" refuses a drawing; elsewhere the tenant default allows it.
        Assert.Equal(409, (await SignAsync(t, subjectInCompany, "APPROVE", drawnPng: TestPng.Create(100, 40), actionCode: action, raw: true)).Status);
        Assert.Equal(201, (await SignAsync(t, subjectElsewhere, "APPROVE", drawnPng: TestPng.Create(100, 40), actionCode: action)).Status);

        // Stale version ⇒ 409; reason too short ⇒ 400.
        Assert.Equal(409, (await SavePolicyAsync(t, company, [], true, 15, 99)).Status);
        Assert.Equal(400, (await SavePolicyAsync(t, company, [], true, 15, 1, reason: "short")).Status);

        using var client = _host.Client(t.Token);
        var revert = await client.PostAsJsonAsync($"api/v1/esign/policy/legal-entities/{company}/revert", new { reason = "Company follows the tenant default again" });
        Assert.Equal(HttpStatusCode.NoContent, revert.StatusCode);
        Assert.Equal(201, (await SignAsync(t, subjectInCompany, "APPROVE", drawnPng: TestPng.Create(100, 40), actionCode: action)).Status);

        var history = Data(await client.GetStringAsync("api/v1/esign/policy/history"));
        Assert.Equal(3, history.GetArrayLength());
        Assert.All(history.EnumerateArray(), h => Assert.True(h.GetProperty("reason").GetString()!.Length >= 10));
        Assert.Equal(3, await Outbox(t.Tenant, ESignAuditEvents.PolicyChanged, ESignAuditEvents.PolicyOverrideReverted));
    }

    [Fact]
    public async Task A_second_factor_cannot_be_enabled_and_unknown_actions_are_refused()
    {
        var t = NewTenant();
        var action = await RegisterActionAsync();
        var version = await TenantPolicyVersionAsync(t);
        var (secondFactor, body) = await SavePolicyAsync(t, null,
            [new { actionCode = action, required = true, meaningCode = (string?)null, visualMode = "optional", secondFactor = "emailOtp" }], true, 15, version);
        Assert.Equal(400, secondFactor);
        Assert.Equal(ESignCodes.SecondFactorNotAvailable, Reason(body));
        Assert.Equal(400, (await SavePolicyAsync(t, null,
            [new { actionCode = "esign-test.nope", required = true, meaningCode = (string?)null, visualMode = "optional" }], true, 15, version)).Status);
    }

    // ── meanings ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Meanings_need_seven_names_lock_after_use_and_cannot_be_deactivated_while_a_policy_uses_them()
    {
        var t = NewTenant();
        using var client = _host.Client(t.Token);
        var six = new Dictionary<string, string> { ["en"] = "Checked", ["tr"] = "Kontrol ettim", ["fr"] = "Vérifié", ["es"] = "Verificado", ["zh"] = "已检查", ["ru"] = "Проверено" };
        var missing = await client.PostAsJsonAsync("api/v1/esign/meanings", new { code = "CHECKED", names = six });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var missingBody = await missing.Content.ReadAsStringAsync();
        Assert.Equal(ESignCodes.MeaningLanguageMissing, Reason(missingBody));
        Assert.Contains("missing:ar", missingBody, StringComparison.Ordinal);

        six["ar"] = "تم التحقق";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/v1/esign/meanings", new { code = "CHECKED", names = six })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("api/v1/esign/meanings", new { code = "CHECKED", names = six })).StatusCode);

        var subjectId = _host.Subjects.Add(t.Tenant);
        Assert.Equal(201, (await SignAsync(t, subjectId, "CHECKED")).Status);
        var meanings = Data(await client.GetStringAsync("api/v1/esign/meanings"));
        var checkedMeaning = meanings.EnumerateArray().Single(m => m.GetProperty("code").GetString() == "CHECKED");
        Assert.True(checkedMeaning.GetProperty("isLocked").GetBoolean());
        Assert.Equal(1, checkedMeaning.GetProperty("usageCount").GetInt64());

        var action = await RegisterActionAsync();
        await SavePolicyAsync(t, null, [new { actionCode = action, required = true, meaningCode = "CHECKED", visualMode = "optional" }], true, 15,
            await TenantPolicyVersionAsync(t));
        var deactivate = await client.PostAsync("api/v1/esign/meanings/CHECKED/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal(ESignCodes.MeaningInUse, Reason(await deactivate.Content.ReadAsStringAsync()));

        // The policy row fixes the meaning: another meaning is refused for that action.
        Assert.Equal(409, (await SignRawAsync(t, subjectId, "APPROVE", _host.Tickets.Issue(t.Tenant, t.User), actionCode: action)).Status);
    }

    [Fact]
    public async Task Renaming_a_meaning_changes_new_signatures_only()
    {
        var t = NewTenant();
        using var client = _host.Client(t.Token);
        var subjectId = _host.Subjects.Add(t.Tenant);
        var (_, first) = await SignAsync(t, subjectId, "REVIEW");
        var names = new Dictionary<string, string>
        {
            ["en"] = "Reviewed", ["tr"] = "Gözden geçirdim", ["fr"] = "Revu", ["es"] = "Revisado", ["zh"] = "已审阅", ["ar"] = "تمت المراجعة", ["ru"] = "Просмотрено"
        };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("api/v1/esign/meanings/REVIEW", new { names })).StatusCode);
        var (_, second) = await SignAsync(t, subjectId, "REVIEW");

        Assert.Equal("İnceledim", first.GetProperty("meaningText").GetString());
        Assert.Equal("Gözden geçirdim", second.GetProperty("meaningText").GetString());
        var firstRecord = await Records().Find(r => r.Id == first.GetProperty("id").GetGuid()).SingleAsync();
        Assert.Equal("İnceledim", firstRecord.MeaningTextAtSign);
    }

    // ── requests ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Requests_are_created_listed_for_the_signer_signed_rejected_with_reason_and_expired_by_the_job()
    {
        var requester = NewTenant();
        var signer = requester with { User = Guid.NewGuid() };
        signer = signer with { Token = ESignatureTestHost.Token(signer.Tenant, signer.User, AllKeys) };
        _host.Users.Known[signer.User] = true;
        var subjectId = _host.Subjects.Add(requester.Tenant);
        using var asRequester = _host.Client(requester.Token);
        using var asSigner = _host.Client(signer.Token);

        async Task<JsonElement> Create(DateTime due) => Data(await (await asRequester.PostAsJsonAsync("api/v1/esign/requests", new
        {
            subject = new { module = TestSubjectProvider.Module, subjectType = TestSubjectProvider.SubjectType, subjectId },
            meaningCode = "APPROVE", signerUserId = signer.User, dueAtUtc = due, idempotencyKey = Key()
        })).Content.ReadAsStringAsync());

        var toSign = await Create(DateTime.UtcNow.AddDays(2));
        var toReject = await Create(DateTime.UtcNow.AddDays(2));
        var toExpire = await Create(DateTime.UtcNow.AddSeconds(2));
        Assert.Matches("^SR-\\d{4}-\\d{4}$", toSign.GetProperty("requestNumber").GetString());

        var unknownSigner = await asRequester.PostAsJsonAsync("api/v1/esign/requests", new
        {
            subject = new { module = TestSubjectProvider.Module, subjectType = TestSubjectProvider.SubjectType, subjectId },
            meaningCode = "APPROVE", signerUserId = Guid.NewGuid(), dueAtUtc = DateTime.UtcNow.AddDays(1), idempotencyKey = Key()
        });
        Assert.Equal(HttpStatusCode.NotFound, unknownSigner.StatusCode);
        Assert.Equal(ESignCodes.SignerNotFound, Reason(await unknownSigner.Content.ReadAsStringAsync()));

        var mine = Data(await asSigner.GetStringAsync("api/v1/esign/requests?scope=mine"));
        Assert.Equal(3, mine.GetProperty("total").GetInt64());
        Assert.Equal(3, mine.GetProperty("waitingCount").GetInt64());
        Assert.Equal(0, Data(await asRequester.GetStringAsync("api/v1/esign/requests")).GetProperty("total").GetInt64());

        // The requester cannot sign the signer's request; the signer can.
        Assert.Equal(403, (await SignRawAsync(requester, subjectId, "APPROVE", _host.Tickets.Issue(requester.Tenant, requester.User),
            requestId: toSign.GetProperty("id").GetGuid())).Status);
        Assert.Equal(201, (await SignRawAsync(signer, subjectId, "APPROVE", _host.Tickets.Issue(signer.Tenant, signer.User),
            requestId: toSign.GetProperty("id").GetGuid())).Status);
        var signedRequest = Data(await asSigner.GetStringAsync($"api/v1/esign/requests/{toSign.GetProperty("id").GetGuid()}"));
        Assert.Equal("signed", signedRequest.GetProperty("status").GetString());
        Assert.Equal(409, (await SignRawAsync(signer, subjectId, "APPROVE", _host.Tickets.Issue(signer.Tenant, signer.User),
            requestId: toSign.GetProperty("id").GetGuid())).Status);

        var rejectId = toReject.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await asSigner.PostAsJsonAsync($"api/v1/esign/requests/{rejectId}/reject", new { reason = "no" })).StatusCode);
        var rejected = await asSigner.PostAsJsonAsync($"api/v1/esign/requests/{rejectId}/reject", new { reason = "The lot is still under investigation", idempotencyKey = Key() });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("rejected", Data(await rejected.Content.ReadAsStringAsync()).GetProperty("status").GetString());

        await Task.Delay(TimeSpan.FromSeconds(3));
        using (var scope = _host.Services.CreateScope())
        {
            var tenant = scope.ServiceProvider.GetRequiredService<Diten.Platform.Common.Tenancy.ITenantContext>();
            using (Diten.Platform.Application.Contracts.TenantScope.Begin(tenant, requester.Tenant))
            {
                var expired = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ExpireSignatureRequestsCommand(100));
                Assert.Equal(1, expired.Data);
            }
        }

        var expiredRequest = Data(await asSigner.GetStringAsync($"api/v1/esign/requests/{toExpire.GetProperty("id").GetGuid()}"));
        Assert.Equal("expired", expiredRequest.GetProperty("status").GetString());
        Assert.Equal(1, await Outbox(requester.Tenant, ESignAuditEvents.RequestExpired));
    }

    // ── export ───────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_pdf_and_csv_export_writes_a_log_row_with_the_file_hash_and_an_audit_event()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        await SignAsync(t, subjectId, "APPROVE", drawnPng: TestPng.Create(300, 100));
        await SignAsync(t, subjectId, "RELEASE");
        using var client = _host.Client(t.Token);

        foreach (var format in new[] { "pdf", "csv" })
        {
            var response = await client.PostAsJsonAsync("api/v1/esign/exports", new
            {
                format,
                filter = new { fromUtc = DateTime.UtcNow.AddDays(-1), toUtc = DateTime.UtcNow.AddDays(1) },
                idempotencyKey = Key()
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var number = response.Headers.GetValues("X-Export-Number").Single();
            Assert.Matches("^EXP-\\d{4}-\\d{4}$", number);

            var row = await Database.GetCollection<ESignatureExport>(PlatformCollections.ESignatureExports)
                .Find(e => e.TenantId == t.Tenant && e.ExportNumber == number).SingleAsync();
            Assert.Equal(ESignatureDigest.ImageHash(bytes), row.FileHash);
            Assert.Equal(2, row.RecordCount);
            if (format == "pdf")
            {
                Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
            }
            else
            {
                Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
                var csv = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
                Assert.StartsWith("\"Tarih-saat\";\"İmza kimliği\"", csv, StringComparison.Ordinal);
                Assert.Contains("\"valid\"", csv, StringComparison.Ordinal);
            }
        }

        Assert.Equal(2, await Outbox(t.Tenant, ESignAuditEvents.ExportCreated));
        Assert.Equal(2, Data(await client.GetStringAsync("api/v1/esign/exports")).GetProperty("total").GetInt64());
    }

    [Fact]
    public async Task Exports_and_searches_show_only_own_signatures_without_a_data_scope()
    {
        var t = NewTenant();
        var colleague = t with { User = Guid.NewGuid() };
        colleague = colleague with { Token = ESignatureTestHost.Token(colleague.Tenant, colleague.User, AllKeys) };
        var company = Guid.NewGuid();
        var subjectId = _host.Subjects.Add(t.Tenant, company);
        await SignAsync(t, subjectId, "APPROVE");
        await SignAsync(colleague, subjectId, "APPROVE");
        using var client = _host.Client(t.Token);
        var query = $"{Signatures}?fromUtc={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}";

        Assert.Equal(1, Data(await client.GetStringAsync(query)).GetProperty("total").GetInt64());
        _host.Scopes.LegalEntitiesByUser[t.User] = [company];
        Assert.Equal(2, Data(await client.GetStringAsync(query)).GetProperty("total").GetInt64());
    }

    // ── cross-tenant reads ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenant_reads_404_everywhere()
    {
        var t = NewTenant();
        var other = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        var (_, signed) = await SignAsync(t, subjectId, "APPROVE");
        var id = signed.GetProperty("id").GetGuid();
        _host.Users.Known[t.User] = true;
        using var mine = _host.Client(t.Token);
        var request = Data(await (await mine.PostAsJsonAsync("api/v1/esign/requests", new
        {
            subject = new { module = TestSubjectProvider.Module, subjectType = TestSubjectProvider.SubjectType, subjectId },
            meaningCode = "APPROVE", signerUserId = t.User, dueAtUtc = DateTime.UtcNow.AddDays(1), idempotencyKey = Key()
        })).Content.ReadAsStringAsync()).GetProperty("id").GetGuid();

        using var theirs = _host.Client(other.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"{Signatures}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsync($"{Signatures}/{id}/verify", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync(
            $"{Signatures}/by-subject?module={TestSubjectProvider.Module}&subjectType={TestSubjectProvider.SubjectType}&subjectId={subjectId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"api/v1/esign/requests/{request}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await theirs.PostAsJsonAsync($"{Signatures}/{id}/invalidate",
            new { ticket = _host.Tickets.Issue(other.Tenant, other.User, "invalidate"), reason = "Attempt from another tenant", idempotencyKey = Key() })).StatusCode);

        // …while the owner sees it.
        Assert.Equal(HttpStatusCode.OK, (await mine.GetAsync($"{Signatures}/{id}")).StatusCode);
    }

    // ── internal catalog registration (CT-D12) ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Actions_register_only_under_their_own_module_prefix_and_only_with_the_internal_key()
    {
        using var client = _host.Client();
        var names = SevenNames("Release");
        var body = new { module = "esign-test", actions = new[] { new { actionCode = "inventory.quarantine.release", names, defaultRequired = true, defaultVisualMode = "optional" } } };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("api/internal/esign/actions/register", body)).StatusCode);

        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", ESignatureTestHost.InternalKey);
        var mismatch = await client.PostAsJsonAsync("api/internal/esign/actions/register", body);
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal(ESignCodes.ActionPrefixMismatch, Reason(await mismatch.Content.ReadAsStringAsync()));

        var code = "esign-test.release-" + Guid.NewGuid().ToString("N")[..8];
        var first = Data(await (await client.PostAsJsonAsync("api/internal/esign/actions/register",
            new { module = "esign-test", actions = new[] { new { actionCode = code, names, defaultRequired = true, defaultVisualMode = "optional" } } }))
            .Content.ReadAsStringAsync());
        Assert.Equal(1, first.GetProperty("registered").GetInt32());
        var again = Data(await (await client.PostAsJsonAsync("api/internal/esign/actions/register",
            new { module = "esign-test", actions = new[] { new { actionCode = code, names, defaultRequired = true, defaultVisualMode = "optional" } } }))
            .Content.ReadAsStringAsync());
        Assert.Equal(1, again.GetProperty("unchanged").GetInt32());
    }

    // ── no secret in any log line ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_log_line_carries_a_ticket_or_the_image_bytes()
    {
        var t = NewTenant();
        var subjectId = _host.Subjects.Add(t.Tenant);
        var png = TestPng.Create(250, 90);
        var ticket = _host.Tickets.Issue(t.Tenant, t.User);
        var spent = _host.Tickets.Issue(t.Tenant, t.User);
        await SignRawAsync(t, subjectId, "APPROVE", spent);

        Assert.Equal(201, (await SignRawAsync(t, subjectId, "APPROVE", ticket,
            appearance: new { type = "drawn", drawing = new { pngBase64 = Convert.ToBase64String(png) } })).Status);
        Assert.Equal(403, (await SignRawAsync(t, subjectId, "APPROVE", spent)).Status);

        var logs = _host.Logs.Text;
        Assert.Contains(nameof(SignSubjectCommand), logs, StringComparison.Ordinal); // the pipeline did log this request
        Assert.False(logs.Contains(ticket, StringComparison.Ordinal), "a log line contains the plain ticket");
        Assert.False(logs.Contains(spent, StringComparison.Ordinal), "a log line contains a plain ticket");
        Assert.False(logs.Contains(Convert.ToBase64String(png)[..64], StringComparison.Ordinal), "a log line contains image bytes");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record TenantCtx(Guid Tenant, Guid User, string Token)
    {
        public string LastTicket { get; set; } = "";
    }

    private IMongoDatabase Database => _host.Database;

    private IMongoCollection<ESignatureRecord> Records() => Database.GetCollection<ESignatureRecord>(PlatformCollections.ESignatureRecords);

    private static TenantCtx NewTenant()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        return new TenantCtx(tenant, user, ESignatureTestHost.Token(tenant, user, AllKeys));
    }

    private static string Key() => "k-" + Guid.NewGuid().ToString("N");

    private static object Drawn(byte[] png) => new { type = "drawn", drawing = new { pngBase64 = Convert.ToBase64String(png) } };

    private static Dictionary<string, string> SevenNames(string en) => new()
    {
        ["en"] = en, ["tr"] = en + " (tr)", ["fr"] = en + " (fr)", ["es"] = en + " (es)", ["zh"] = en + " (zh)", ["ar"] = en + " (ar)", ["ru"] = en + " (ru)"
    };

    private static object SignBody(string subjectId, string meaning, string? ticket, string? idempotencyKey = null, string? actionCode = null,
        object? appearance = null, Guid? requestId = null) => new
    {
        ticket,
        subject = new { module = TestSubjectProvider.Module, subjectType = TestSubjectProvider.SubjectType, subjectId },
        actionCode,
        meaningCode = meaning,
        appearance = appearance ?? new { type = "none" },
        requestId,
        idempotencyKey = idempotencyKey ?? Key()
    };

    private async Task<(int Status, string Body)> SignRawAsync(TenantCtx t, string subjectId, string meaning, string? ticket,
        string? idempotencyKey = null, string? actionCode = null, object? appearance = null, Guid? requestId = null)
    {
        using var client = _host.Client(t.Token);
        var response = await client.PostAsJsonAsync(Signatures, SignBody(subjectId, meaning, ticket, idempotencyKey, actionCode, appearance, requestId));
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<(int Status, JsonElement Data)> SignAsync(TenantCtx t, string subjectId, string meaning, byte[]? drawnPng = null,
        string? actionCode = null, bool raw = false)
    {
        var ticket = _host.Tickets.Issue(t.Tenant, t.User);
        t.LastTicket = ticket;
        var appearance = drawnPng is null ? null : new { type = "drawn", drawing = new { pngBase64 = Convert.ToBase64String(drawnPng) } };
        var (status, body) = await SignRawAsync(t, subjectId, meaning, ticket, actionCode: actionCode, appearance: appearance);
        if (!raw)
        {
            Assert.True(status is 200 or 201, $"sign failed: {status} {body}");
        }

        return (status, status is 200 or 201 ? Data(body) : default);
    }

    private async Task<JsonElement> VerifyAsync(TenantCtx t, Guid id)
    {
        using var client = _host.Client(t.Token);
        var response = await client.PostAsync($"{Signatures}/{id}/verify", null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"verify: {(int)response.StatusCode} {body}");
        return Data(body);
    }

    private async Task<string> RegisterActionAsync(string defaultVisualMode = "optional")
    {
        using var client = _host.Client();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", ESignatureTestHost.InternalKey);
        var code = "esign-test.act-" + Guid.NewGuid().ToString("N")[..10];
        var response = await client.PostAsJsonAsync("api/internal/esign/actions/register", new
        {
            module = "esign-test",
            actions = new[] { new { actionCode = code, names = SevenNames("Act"), defaultRequired = true, defaultVisualMode } }
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return code;
    }

    private async Task<int> TenantPolicyVersionAsync(TenantCtx t)
    {
        using var client = _host.Client(t.Token);
        return Data(await client.GetStringAsync("api/v1/esign/policy")).GetProperty("tenant").GetProperty("version").GetInt32();
    }

    private async Task<(int Status, string Body)> SavePolicyAsync(TenantCtx t, Guid? legalEntity, object[] rows, bool reauthEvery, int windowMinutes,
        int? expectedVersion, string reason = "Quality decision QD-2026-14")
    {
        using var client = _host.Client(t.Token);
        var path = legalEntity is null ? "api/v1/esign/policy/tenant" : $"api/v1/esign/policy/legal-entities/{legalEntity}";
        var response = await client.PutAsJsonAsync(path, new { rows, reauthEvery, reauthWindowMinutes = windowMinutes, reason, expectedVersion });
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<long> Outbox(Guid tenant, params string[] events) =>
        await Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox).CountDocumentsAsync(
            Builders<BsonDocument>.Filter.In("RequestType", events) & Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)));

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

/// <summary>A real, decodable PNG (white RGB pixels) of the given size — MigraDoc decodes what the export embeds.</summary>
internal static class TestPng
{
    public static byte[] Create(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 2; // RGB
        Chunk(output, "IHDR", header);

        using var raw = new MemoryStream();
        var row = new byte[1 + width * 3];
        Array.Fill(row, (byte)0xFF);
        row[0] = 0; // filter: none
        for (var y = 0; y < height; y++)
        {
            raw.Write(row);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32([.. typeBytes, .. data]));
        output.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
