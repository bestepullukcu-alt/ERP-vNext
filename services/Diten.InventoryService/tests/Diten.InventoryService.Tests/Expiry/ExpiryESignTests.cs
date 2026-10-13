using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Infrastructure.ESign;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F3 — the pure parts: golden SHA-256 vectors of the two field lists (pack §8.6), the four registered actions (CT-F8: APPROVE),
/// the FEFO-BUNDLE 1.0.1 → 1.0.2 step (description only).
/// </summary>
public sealed class ExpiryESignShapeTests
{
    private static readonly Guid CaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Company = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Item = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Sku = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static FefoExceptionCase Case() => new()
    {
        Id = CaseId,
        LegalEntityId = Company,
        Number = "FX-2026-0001",
        Type = ExpiryExceptionType.FefoBypass,
        Status = ExpiryExceptionStatus.Open,
        ItemId = Item,
        SkuId = Sku,
        SkuLevel = SkuLevel.Lsku,
        LotId = "55555555-5555-5555-5555-555555555555",
        LotNumber = "LOT-A1",
        ExpiryDate = new DateTimeOffset(2027, 1, 31, 0, 0, 0, TimeSpan.Zero),
        WarehouseId = "wh-1",
        Quantity = 12.5m,
        BaseUomId = "EA",
        SourceMovement = new FefoSourceMovement { TransactionId = "tx-1", MovementType = "GOODS_ISSUE_SALES" },
        Facts = new Dictionary<string, string> { ["firstEligibleLotId"] = "lot-0", ["bypassReason"] = "Musteri talebi" },
        OpenedAtUtc = new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero),
        PendingResolution = new PendingResolution { Resolution = ExpiryResolution.AcceptedWithReason, Reason = "QA karari - sapma DEV-1" },
        Version = 3
    };

    private static LotDateOverride Override() => new()
    {
        Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
        LegalEntityId = Company,
        ItemId = Item,
        SkuId = Sku,
        SkuLevel = SkuLevel.Lsku,
        LotNumber = "LOT-B2",
        ManufacturingDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        SuggestedExpiry = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        EnteredExpiry = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
        SuggestedRetest = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
        EnteredRetest = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
        Reason = "Tedarikci CoA belgesi",
        ShelfLifeDays = 365,
        RetestDays = 180
    };

    /// <summary>fx-v1, written out by hand from pack §8.6 (keys sorted ordinal) — K3 sabotage (c): rename a field without a version → red.</summary>
    private const string FxV1Canonical =
        "{\"module\":\"expiry\",\"subjectType\":\"expiry.exception\",\"subjectId\":\"11111111-1111-1111-1111-111111111111\",\"fields\":{"
        + "\"baseUom\":\"EA\",\"expiryDate\":\"2027-01-31\",\"facts.bypassReason\":\"Musteri talebi\",\"facts.firstEligibleLotId\":\"lot-0\","
        + "\"itemId\":\"33333333-3333-3333-3333-333333333333\",\"legalEntityId\":\"22222222-2222-2222-2222-222222222222\","
        + "\"lotId\":\"55555555-5555-5555-5555-555555555555\",\"lotNumber\":\"LOT-A1\",\"number\":\"FX-2026-0001\",\"openedAtUtc\":\"2026-10-13T08:00:00Z\","
        + "\"quantity\":\"12.5\",\"resolution\":\"AcceptedWithReason\",\"resolutionReason\":\"QA karari - sapma DEV-1\",\"retestDate\":null,\"skuCode\":null,"
        + "\"skuId\":\"44444444-4444-4444-4444-444444444444\",\"skuLevel\":\"Lsku\",\"sourceMovementType\":\"GOODS_ISSUE_SALES\",\"sourceTransactionId\":\"tx-1\","
        + "\"status\":\"Open\",\"type\":\"FEFO_BYPASS\",\"version\":\"3\",\"warehouseId\":\"wh-1\"}}";

    private const string DoV1Canonical =
        "{\"module\":\"expiry\",\"subjectType\":\"expiry.date-override\",\"subjectId\":\"66666666-6666-6666-6666-666666666666\",\"fields\":{"
        + "\"enteredExpiry\":\"2026-12-01\",\"enteredRetest\":\"2026-06-30\",\"itemId\":\"33333333-3333-3333-3333-333333333333\","
        + "\"legalEntityId\":\"22222222-2222-2222-2222-222222222222\",\"lotNumber\":\"LOT-B2\",\"manufacturingDate\":\"2026-01-01\","
        + "\"overrideId\":\"66666666-6666-6666-6666-666666666666\",\"reason\":\"Tedarikci CoA belgesi\",\"retestDays\":\"180\",\"shelfLifeDays\":\"365\","
        + "\"skuId\":\"44444444-4444-4444-4444-444444444444\",\"skuLevel\":\"Lsku\",\"suggestedExpiry\":\"2027-01-01\",\"suggestedRetest\":\"2026-06-30\"}}";

    private const string FxV1Sha256 = "963b6cf9bbfe6358c493a2f44c5b642c663a60136aa34e87f066006c11559c98";
    private const string DoV1Sha256 = "a0e9f999ef42d268c147b958dc17dc75bdba3f9e6bbe99b98c3005fe339fb1bf";

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void Golden_vector_fx_v1()
    {
        var c = Case();
        Assert.Equal(FxV1Canonical, ExpirySignableSubjects.Canonical(ExpirySignableActions.ExceptionSubjectType, CaseId.ToString("D"), ExpirySignableSubjects.ExceptionFields(c)));
        Assert.Equal(FxV1Sha256, ExpirySignableSubjects.ExceptionHash(c));
        Assert.Equal(FxV1Sha256, Sha(FxV1Canonical));
        Assert.Equal("fx-v1", ExpirySignableActions.ExceptionFieldListVersion);
    }

    [Fact]
    public void Golden_vector_do_v1()
    {
        var o = Override();
        Assert.Equal(DoV1Canonical, ExpirySignableSubjects.Canonical(ExpirySignableActions.DateOverrideSubjectType, o.Id.ToString("D"), ExpirySignableSubjects.DateOverrideFields(o)));
        Assert.Equal(DoV1Sha256, ExpirySignableSubjects.DateOverrideHash(o));
        Assert.Equal(DoV1Sha256, Sha(DoV1Canonical));
        Assert.Equal("do-v1", ExpirySignableActions.DateOverrideFieldListVersion);
    }

    [Fact]
    public void The_hash_covers_the_prepared_decision()
    {
        var accept = Case();
        var cancel = Case();
        cancel.PendingResolution!.Resolution = ExpiryResolution.Cancelled;
        var otherReason = Case();
        otherReason.PendingResolution!.Reason = "baska bir gerekce metni";
        Assert.NotEqual(ExpirySignableSubjects.ExceptionHash(accept), ExpirySignableSubjects.ExceptionHash(cancel));
        Assert.NotEqual(ExpirySignableSubjects.ExceptionHash(accept), ExpirySignableSubjects.ExceptionHash(otherReason));
    }

    [Fact]
    public void The_four_expiry_actions_are_registered_with_APPROVE_CT_F8()
    {
        var body = JsonSerializer.SerializeToNode(ESignActionRegistrationHostedService.Body("expiry"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("expiry", body["module"]!.GetValue<string>());
        var actions = body["actions"]!.AsArray().ToDictionary(a => a!["actionCode"]!.GetValue<string>(), a => a!);
        Assert.Equal(new[] { "expiry.exception.accept", "expiry.exception.cancel", "expiry.exception.fix", "expiry.lot-date.override" }, actions.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(actions.Values, a =>
        {
            Assert.StartsWith("expiry.", a["actionCode"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.True(a["defaultRequired"]!.GetValue<bool>());
            Assert.Equal("APPROVE", a["defaultMeaningCode"]!.GetValue<string>());
            Assert.Equal("savedAccepted", a["defaultVisualMode"]!.GetValue<string>());
            Assert.Equal(new[] { "ar", "en", "es", "fr", "ru", "tr", "zh" }, a["names"]!.AsObject().Select(n => n.Key).OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(a["names"]!.AsObject(), n => Assert.False(string.IsNullOrWhiteSpace(n.Value!.GetValue<string>())));
        });
        Assert.Equal(["quarantine", "expiry"], ESignActionRegistrationHostedService.Modules.Select(m => m.Module));
    }

    [Fact]
    public async Task One_registration_call_per_module()
    {
        var calls = await ESignActionRegistrationRegressionTests.RunAsync(2);
        Assert.Equal(2, calls.Count);
        Assert.Single(calls, c => c.Body.Contains("\"module\":\"expiry\"", StringComparison.Ordinal) && !c.Body.Contains("quarantine.", StringComparison.Ordinal));
        Assert.Single(calls, c => c.Body.Contains("\"module\":\"quarantine\"", StringComparison.Ordinal) && !c.Body.Contains("expiry.", StringComparison.Ordinal));
    }

    // ── FEFO-BUNDLE 1.0.1 → 1.0.2 ─────────────────────────────────────────────────────────────────────────

    private const string FrozenFefoV101Sha256 = "4b098ab4ac592538291d1fa0cc5dbbed00aac64ce27f680bd637502925ac4ed9";

    private static string FixtureV101Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "fefo-v1.0.1.openapi.yaml");

    private static JsonObject Load(string path)
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path));
        return (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(raw))!;
    }

    [Fact]
    public void The_fefo_v1_0_1_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenFefoV101Sha256, Sha(File.ReadAllText(FixtureV101Path).Replace("\r\n", "\n")));

    [Fact]
    public void The_1_0_2_change_is_the_F3_slice_note_and_nothing_else()
    {
        var before = Load(FixtureV101Path);
        var after = Load(ContractSchemas.FefoContractPath);
        Assert.Equal("1.0.1", before["info"]!["version"]!.GetValue<string>());
        Assert.Equal("1.0.2", after["info"]!["version"]!.GetValue<string>());
        var slices = after["info"]!["x-implemented-by-slice"]!.AsObject();
        Assert.Equal(["getSignableSubject"], slices["F3"]!.AsArray().Select(v => v!.GetValue<string>()));
        Assert.DoesNotContain("getSignableSubject", slices["not-yet-implemented"]!.AsArray().Select(v => v!.GetValue<string>()));
        var changelogBefore = before["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        var changelogAfter = after["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        Assert.Equal(changelogBefore, changelogAfter.Take(changelogBefore.Count));
        Assert.Contains("F3-A", Assert.Single(changelogAfter.Skip(changelogBefore.Count)), StringComparison.Ordinal);
        foreach (var doc in new[] { before, after })
        {
            var info = doc["info"]!.AsObject();
            info.Remove("version");
            info.Remove("x-changelog");
            info.Remove("x-implemented-by-slice");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "fefo.openapi.yaml 1.0.2 changes more than the version, the changelog and x-implemented-by-slice.");
    }
}

/// <summary>
/// WP-0176-F3 — the PRODUCTION signature implementations (<see cref="ESignExpiryResolutionSignature"/>, <see cref="ESignLotDateOverrideSignature"/>,
/// <see cref="GatewayExpiryESignClient"/>) and the subject provider, through the real HTTP pipeline, against the suite's in-memory MOD-0022
/// (<see cref="FakeESignGateway"/>: ESIGN-BUNDLE getSignature / getEffectivePolicy shapes). Signatures are recorded the way Platform records
/// them: the subject hash is computed from THIS provider's HTTP answer with an independent canonical writer (ESIGN §7).
/// </summary>
public sealed class ExpiryESignTests : IClassFixture<ProductionSignatureExpiryApiFactory>
{
    private static readonly string[] ESignKeys = ["esign.signatures.sign", "esign.signatures.read"];

    private readonly ProductionSignatureExpiryApiFactory _factory;

    public ExpiryESignTests(ProductionSignatureExpiryApiFactory factory) => _factory = factory;

    private sealed class World
    {
        public required ExpiryScenario S { get; init; }
        public required string QaSigner { get; init; }
    }

    private World Make(bool required = true, string meaning = "APPROVE")
    {
        var s = new ExpiryScenario(_factory);
        foreach (var action in new[] { "expiry.exception.accept", "expiry.exception.fix", "expiry.exception.cancel" })
        {
            _factory.ESign.Policy(s.Tenant, action, required, meaning);
        }

        return new World { S = s, QaSigner = s.Mint(s.Qa, ExpiryScenario.QaKeys.Concat(ESignKeys)) };
    }

    /// <summary>ESIGN §7 canonical form, written independently of production (Platform's side of the hash).</summary>
    internal static string PlatformHash(string subjectType, string subjectId, JsonElement fields)
    {
        static string Q(string v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var sb = new StringBuilder("{\"module\":\"expiry\",\"subjectType\":").Append(Q(subjectType)).Append(",\"subjectId\":").Append(Q(subjectId)).Append(",\"fields\":{");
        sb.Append(string.Join(",", fields.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => Q(p.Name) + ":" + (p.Value.ValueKind == JsonValueKind.Null ? "null" : Q(p.Value.GetString()!)))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.Append("}}").ToString()))).ToLowerInvariant();
    }

    private static async Task<JsonElement> SubjectAsync(ExpiryScenario s, string type, string id, string token, int expected = 200)
        => await s.ExpectAsync(HttpMethod.Get, $"/api/expiry/esign-subjects/{type}/{id}", token, null, expected);

    /// <summary>Opened by the OPERATOR (origin), prepared by QA; returns the case id and the prepared answer.</summary>
    private static async Task<(string Id, JsonElement Prepared)> PreparedAsync(World w, string resolution = "AcceptedWithReason")
    {
        var c = await w.S.OpenAsync(await w.S.LotAsync());
        var id = c.GetProperty("exceptionId").GetString()!;
        var (status, prepared, _) = await w.S.PrepareAsync(id, resolution, ExpiryScenario.ETag(c), w.QaSigner);
        Assert.True(status == 200, prepared.ToString());
        return (id, prepared);
    }

    /// <summary>What MOD-0022 does at signing: read the provider, hash the fields, record the signature.</summary>
    private async Task<string> SignAsync(World w, string id, string action = "expiry.exception.accept", Guid? signer = null, string meaning = "APPROVE",
        DateTimeOffset? signedAt = null)
    {
        var subject = await SubjectAsync(w.S, "expiry.exception", id, w.QaSigner);
        var hash = PlatformHash("expiry.exception", id, subject.GetProperty("fields"));
        return _factory.ESign.Sign(w.S.Tenant, signer ?? w.S.Qa, id, meaning, hash, "expiry", "expiry.exception", action, signedAt, w.S.Ledger.LegalEntity);
    }

    private static string Code(JsonElement e) => ExpiryScenario.Code(e);

    // ── provider ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_provider_answers_fx_v1_and_refuses_with_one_404()
    {
        var w = Make();
        var (id, _) = await PreparedAsync(w);
        var subject = await SubjectAsync(w.S, "expiry.exception", id, w.QaSigner);
        var stored = Assert.Single(w.S.Persisted("expiry_exceptions"), d => d["_id"].AsString == id);
        Assert.Equal(w.S.Ledger.LegalEntity.ToString("D"), subject.GetProperty("legalEntityId").GetString());
        Assert.Equal("AcceptedWithReason", subject.GetProperty("fields").GetProperty("resolution").GetString());
        Assert.Equal(ExpiryScenario.Reason, subject.GetProperty("fields").GetProperty("resolutionReason").GetString());
        Assert.Equal(stored["Number"].AsString, subject.GetProperty("fields").GetProperty("number").GetString());

        // the provider and the verification hash the same thing (K3 sabotage (i): two hash functions → this and the signed flow are red)
        using (var scope = _factory.Services.CreateScope())
        {
            var tenant = scope.ServiceProvider.GetRequiredService<Diten.InventoryService.Application.Common.ITenantContext>();
            tenant.SetScope(w.S.Tenant, w.S.Ledger.LegalEntity, w.S.Qa, Diten.InventoryService.Application.Common.LegalEntitySources.Token);
            var c = await scope.ServiceProvider.GetRequiredService<Diten.InventoryService.Domain.Expiry.Repositories.IExpiryRepository>().FindCaseAsync(Guid.Parse(id));
            Assert.Equal(PlatformHash("expiry.exception", id, subject.GetProperty("fields")), ExpirySignableSubjects.ExceptionHash(c!));
        }

        // claim-less token (MOD-0022 sends no company): the record's company is used
        var claimless = TestTokens.Mint(w.S.Tenant, null, w.S.Qa, ExpiryScenario.QaKeys);
        Assert.Equal(subject.GetProperty("fields").ToString(), (await SubjectAsync(w.S, "expiry.exception", id, claimless)).GetProperty("fields").ToString());

        await SubjectAsync(w.S, "expiry.exception", id, w.S.Mint(w.S.Qa, ["expiry.policies.read"]), 404);                 // no expiry.exceptions.read
        await SubjectAsync(w.S, "expiry.exception", id, w.S.Mint(w.S.Qa, ExpiryScenario.QaKeys, Guid.NewGuid()), 404);     // another company named
        await SubjectAsync(w.S, "expiry.exception", Guid.NewGuid().ToString("D"), w.QaSigner, 404);                         // unknown id
        await SubjectAsync(w.S, "expiry.unknown", id, w.QaSigner, 404);                                                     // unknown type
        await SubjectAsync(w.S, "expiry.exception", id, TestTokens.Mint(Guid.NewGuid(), null, w.S.Qa, ExpiryScenario.QaKeys), 404); // another tenant
        var (unauthorized, _, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/expiry/esign-subjects/expiry.exception/{id}", "");
        Assert.Equal(401, unauthorized);
    }

    // ── resolution ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_bound_signature_resolves_and_only_its_id_is_kept()
    {
        var w = Make();
        var (id, prepared) = await PreparedAsync(w);
        Assert.True(prepared.GetProperty("signatureRequired").GetBoolean());
        Assert.Equal("APPROVE", prepared.GetProperty("meaningCode").GetString());
        var signature = await SignAsync(w, id);

        var (status, resolved, _) = await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature, w.QaSigner);
        Assert.True(status == 200, resolved.ToString());
        Assert.Equal("Accepted", resolved.GetProperty("status").GetString());
        Assert.Equal(signature, Assert.Single(w.S.Persisted("expiry_exceptions"), d => d["_id"].AsString == id)["ResolutionSignatureId"].AsString);
        Assert.Equal(signature, Assert.Single(w.S.Audit("expiry.exception.resolved"))["Detail"]["signatureId"].AsString);
        Assert.Contains(_factory.ESign.Requests, r => r.Path == $"/api/v1/esign/signatures/{signature}" && r.Authorization!.EndsWith(w.QaSigner, StringComparison.Ordinal));
        Assert.DoesNotContain(_factory.ESign.Requests, r => r.Path.EndsWith("/verify", StringComparison.Ordinal)); // CT decision F3-A
    }

    [Fact]
    public async Task A_subject_that_changed_after_signing_is_refused()
    {
        // K3 sabotage (a): skip the hash comparison → this resolve answers 200
        var w = Make();
        var (id, prepared) = await PreparedAsync(w);
        var signature = await SignAsync(w, id);
        var otherQa = w.S.Mint(Guid.NewGuid(), ExpiryScenario.QaKeys);
        var (st, taken, _) = await w.S.TakeAsync(id, ExpiryScenario.ETag(prepared), otherQa); // status + version change after signing
        Assert.Equal(200, st);

        var (status, refused, _) = await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(taken), signature, w.QaSigner);
        Assert.Equal(409, status);
        Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", Code(refused));
    }

    [Fact]
    public async Task The_signature_of_another_case_is_refused_and_a_used_signature_cannot_be_reused()
    {
        // K3 sabotage (b): verify against the case the signature names instead of the case being resolved → B resolves with A's signature
        var w = Make();
        var (a, preparedA) = await PreparedAsync(w);
        var (b, preparedB) = await PreparedAsync(w);
        var signatureA = await SignAsync(w, a);

        var (sb, refusedB, _) = await w.S.ResolveAsync(b, "AcceptedWithReason", ExpiryScenario.ETag(preparedB), signatureA, w.QaSigner);
        Assert.Equal(409, sb);
        Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", Code(refusedB));

        Assert.Equal(200, (await w.S.ResolveAsync(a, "AcceptedWithReason", ExpiryScenario.ETag(preparedA), signatureA, w.QaSigner)).Status);
        var (c, preparedC) = await PreparedAsync(w);
        Assert.Equal(409, (await w.S.ResolveAsync(c, "AcceptedWithReason", ExpiryScenario.ETag(preparedC), signatureA, w.QaSigner)).Status);
    }

    [Fact]
    public async Task The_signer_must_be_the_resolver()
    {
        // K3 sabotage (e): drop the signer = actor rule → 200
        var w = Make();
        var (id, prepared) = await PreparedAsync(w);
        var signature = await SignAsync(w, id, signer: Guid.NewGuid());
        var (status, refused, _) = await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature, w.QaSigner);
        Assert.Equal(409, status);
        Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", Code(refused));
    }

    [Fact]
    public async Task Action_and_meaning_must_be_the_resolutions()
    {
        var w = Make();
        var (id, prepared) = await PreparedAsync(w);
        var wrongAction = await SignAsync(w, id, action: "expiry.exception.fix");
        Assert.Equal(409, (await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), wrongAction, w.QaSigner)).Status);
        var wrongMeaning = await SignAsync(w, id, meaning: "REVIEW");
        Assert.Equal(409, (await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), wrongMeaning, w.QaSigner)).Status);
        Assert.Equal(200, (await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), await SignAsync(w, id), w.QaSigner)).Status);
    }

    [Fact]
    public async Task A_signature_made_before_the_preparation_is_refused()
    {
        // K3 sabotage (h): drop signedAtUtc ≥ PendingResolution.SetAtUtc → 200
        var w = Make();
        var (id, prepared) = await PreparedAsync(w);
        var signature = await SignAsync(w, id, signedAt: _factory.Clock.GetUtcNow().AddMinutes(-5));
        var (status, refused, _) = await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature, w.QaSigner);
        Assert.Equal(409, status);
        Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", Code(refused));
    }

    [Fact]
    public async Task An_invalidated_signature_is_refused()
    {
        // K3 sabotage (j): drop the invalidation rule → 200
        var w = Make();
        var (id, prepared) = await PreparedAsync(w);
        var signature = await SignAsync(w, id);
        _factory.ESign.Invalidate(signature);
        var (status, refused, _) = await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature, w.QaSigner);
        Assert.Equal(409, status);
        Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", Code(refused));
    }

    [Fact]
    public async Task Unreachable_MOD_0022_fails_closed()
    {
        // K3 sabotage (d): allow when MOD-0022 is unreachable → the unsigned resolve answers 200
        var w = Make(required: false); // even a policy that would not require a signature cannot be read
        var (id, _) = await PreparedAsync(w);
        var signature = await SignAsync(w, id);
        _factory.ESign.Down = true;
        try
        {
            var current = await w.S.GetAsync(id, w.QaSigner);
            var (sp, prepared, _) = await w.S.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(current), w.QaSigner);
            Assert.Equal(200, sp);
            Assert.True(prepared.GetProperty("signatureRequired").GetBoolean());

            var (s1, r1, _) = await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), null, w.QaSigner);
            Assert.Equal(400, s1);
            Assert.Equal("EXPIRY_SIGNATURE_REQUIRED", Code(r1));
            var (s2, r2, _) = await w.S.ResolveAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(prepared), signature, w.QaSigner);
            Assert.Equal(409, s2);
            Assert.Equal("EXPIRY_SIGNATURE_MISMATCH", Code(r2));
        }
        finally
        {
            _factory.ESign.Down = false;
        }

        Assert.Equal("Open", (await w.S.GetAsync(id, w.QaSigner)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_policy_that_does_not_require_a_signature_resolves_unsigned()
    {
        var w = Make(required: false);
        var (id, prepared) = await PreparedAsync(w, "Fixed");
        Assert.False(prepared.GetProperty("signatureRequired").GetBoolean());
        var (status, resolved, _) = await w.S.ResolveAsync(id, "Fixed", ExpiryScenario.ETag(prepared), null, w.QaSigner);
        Assert.True(status == 200, resolved.ToString());
        Assert.Equal("false", Assert.Single(w.S.Audit("expiry.exception.resolved"))["Detail"]["signatureRequired"].AsString);
    }

    [Fact]
    public async Task The_origin_user_still_cannot_resolve_CT_F10()
    {
        var w = Make();
        var opener = w.S.Mint(w.S.Operator, ExpiryScenario.OperatorKeys.Concat(ExpiryScenario.QaKeys).Concat(ESignKeys));
        var c = await w.S.OpenAsync(await w.S.LotAsync(), token: opener);
        var id = c.GetProperty("exceptionId").GetString()!;
        var (status, refused, _) = await w.S.PrepareAsync(id, "AcceptedWithReason", ExpiryScenario.ETag(c), opener);
        Assert.Equal(409, status);
        Assert.Equal("EXPIRY_SOD_VIOLATION", Code(refused));
    }

    // ── KF-D (CT-F6) ─────────────────────────────────────────────────────────────────────────────────────

    private sealed class KfdWorld
    {
        public required ExpiryScenario S { get; init; }
        public required string Creator { get; init; }
    }

    private KfdWorld MakeKfd(bool required = true)
    {
        var s = new ExpiryScenario(_factory);
        _factory.ShelfLife[s.Ledger.Sku] = (365, 180, null);
        _factory.ESign.Policy(s.Tenant, "expiry.lot-date.override", required, "APPROVE");
        var creator = s.Mint(s.Ledger.User, TestTokens.AllPermissions.Concat(TestTokens.TracePermissions).Concat(["expiry.date-overrides.create"]).Concat(ESignKeys));
        return new KfdWorld { S = s, Creator = creator };
    }

    private static Task<(int Status, JsonElement Body)> CreateLotAsync(KfdWorld w, string number, object dateOverride)
        => CreateLotAsync(w, ExpiryLotDateHookTests.LotBody(w.S, number, ExpiryLotDateHookTests.OtherExpiry, ExpiryLotDateHookTests.SuggestedRetest, dateOverride));

    private static async Task<(int Status, JsonElement Body)> CreateLotAsync(KfdWorld w, Dictionary<string, object?> body)
    {
        using var response = await w.S.Lots.Send(HttpMethod.Post, "/api/trace/lots", body, r =>
        {
            r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", w.Creator);
        });
        return ((int)response.StatusCode, await TraceScenario.Read(response));
    }

    private static async Task<string> OverrideAsync(KfdWorld w, string number)
    {
        var body = ExpiryLotDateHookTests.OverrideBody(w.S, number, ExpiryLotDateHookTests.OtherExpiry, ExpiryLotDateHookTests.SuggestedRetest);
        var created = await w.S.ExpectAsync(HttpMethod.Post, "/api/expiry/date-overrides", w.Creator, body, 201, (string)body["idempotencyKey"]!);
        return created.GetProperty("overrideId").GetString()!;
    }

    private async Task<string> SignOverrideAsync(KfdWorld w, string overrideId, Guid? signer = null, DateTimeOffset? signedAt = null)
    {
        var subject = await SubjectAsync(w.S, "expiry.date-override", overrideId, w.Creator);
        var hash = PlatformHash("expiry.date-override", overrideId, subject.GetProperty("fields"));
        return _factory.ESign.Sign(w.S.Tenant, signer ?? w.S.Ledger.User, overrideId, "APPROVE", hash, "expiry", "expiry.date-override",
            "expiry.lot-date.override", signedAt, w.S.Ledger.LegalEntity);
    }

    [Fact]
    public async Task A_signed_override_creates_the_lot_with_the_signature_reference()
    {
        var w = MakeKfd();
        var number = TraceScenario.LotNo("F3");
        var id = await OverrideAsync(w, number);
        var subject = await SubjectAsync(w.S, "expiry.date-override", id, w.Creator);
        Assert.Equal("2026-12-01", subject.GetProperty("fields").GetProperty("enteredExpiry").GetString());
        await SubjectAsync(w.S, "expiry.date-override", id, w.S.Mint(Guid.NewGuid(), ["expiry.date-overrides.create"]), 404); // not the creator's

        var signature = await SignOverrideAsync(w, id);
        var body = ExpiryLotDateHookTests.LotBody(w.S, number, ExpiryLotDateHookTests.OtherExpiry, ExpiryLotDateHookTests.SuggestedRetest,
            new { overrideId = id, signatureId = signature });
        var (status, lot) = await CreateLotAsync(w, body);
        Assert.True(status == 201, lot.ToString());
        var (replayStatus, replay) = await CreateLotAsync(w, body); // pack §13 #6: replay → the same lot, nothing twice
        Assert.Equal(200, replayStatus);
        Assert.Equal(lot.GetProperty("lotId").GetString(), replay.GetProperty("lotId").GetString());
        Assert.Equal(signature, lot.GetProperty("expiryOverride").GetProperty("signatureId").GetString());
        Assert.Equal(signature, Assert.Single(w.S.Persisted("expiry_exceptions"))["Facts"]["signatureId"].AsString);
        Assert.Equal(signature, Assert.Single(w.S.Audit("expiry.date-override.consumed"))["Detail"]["signatureId"].AsString);
    }

    [Fact]
    public async Task Under_ReasonAndSignature_the_signature_is_required_even_when_the_policy_row_says_not_CT_F6()
    {
        // K3 sabotage (f): skip the KF-D signature when the MOD-0022 row says not required → 201
        var w = MakeKfd(required: false);
        var number = TraceScenario.LotNo("F3");
        var id = await OverrideAsync(w, number);
        var (status, refused) = await CreateLotAsync(w, number, new { overrideId = id });
        Assert.Equal(400, status);
        Assert.Equal("LOT_DATE_OVERRIDE_SIGNATURE_REQUIRED", ExpiryLotDateHookTests.Code(refused));
        Assert.Empty(w.S.Persisted("trace_lots"));
    }

    [Fact]
    public async Task An_override_signature_that_does_not_bind_is_refused()
    {
        var w = MakeKfd();
        var number = TraceScenario.LotNo("F3");
        var id = await OverrideAsync(w, number);
        foreach (var signature in new[]
                 {
                     await SignOverrideAsync(w, id, signer: Guid.NewGuid()),                                  // another signer
                     await SignOverrideAsync(w, id, signedAt: _factory.Clock.GetUtcNow().AddMinutes(-5))      // before the override existed
                 })
        {
            var (status, refused) = await CreateLotAsync(w, number, new { overrideId = id, signatureId = signature });
            Assert.Equal(409, status);
            Assert.Equal("LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH", ExpiryLotDateHookTests.Code(refused));
        }

        var good = await SignOverrideAsync(w, id);
        _factory.ESign.Down = true;
        try
        {
            var (status, refused) = await CreateLotAsync(w, number, new { overrideId = id, signatureId = good });
            Assert.Equal(409, status); // fail-closed
            Assert.Equal("LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH", ExpiryLotDateHookTests.Code(refused));
        }
        finally
        {
            _factory.ESign.Down = false;
        }

        Assert.Equal("Pending", Assert.Single(w.S.Persisted("expiry_date_overrides"))["Status"].AsString);
        Assert.Equal(201, (await CreateLotAsync(w, number, new { overrideId = id, signatureId = good })).Status);
    }
}
