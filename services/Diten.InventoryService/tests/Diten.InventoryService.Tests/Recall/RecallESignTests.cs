using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Infrastructure.ESign;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.Expiry;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R4 — the e-signature of MOD-0177 (pack §8.8, KR-A, CT-R1, CT-R16; CT decision F3-A): the five actions registered under module
/// <c>recall</c>, the subject provider (<c>rc-v1</c>, <c>rs-v1</c>; <c>dr-v1</c> has its golden vector, the provider answers 404 until R5),
/// and the PRODUCTION binding (<see cref="ESignRecallSignature"/> over the real <c>GatewayRecallESignClient</c> and the suite's in-memory MOD-0022,
/// <see cref="FakeESignGateway"/>: getSignature / getEffectivePolicy shapes, tenant from the token). Signatures are recorded the way Platform
/// records them: the subject hash is computed from the PROVIDER's answer with an independent ESIGN §7 canonical form (<see cref="PlatformHash"/>).
/// Every binding rule has a refusal and, on the same case, the acceptance that follows once the rule is met.
/// </summary>
public sealed class RecallESignTests : IClassFixture<ProductionSignatureRecallApiFactory>
{
    private static readonly string[] ESignKeys = ["esign.signatures.sign", "esign.signatures.read"];

    private readonly ProductionSignatureRecallApiFactory _factory;

    public RecallESignTests(ProductionSignatureRecallApiFactory factory) => _factory = factory;

    private FakeESignGateway ESign => _factory.ESign;

    // ── golden vectors (pack §8.8, written out by hand, keys sorted ordinal) ──────────────────────────────

    private static readonly Guid CaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Company = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Sku = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>rc-v1 — K3 sabotage (c): drop <c>approvedSnapshotSha256</c> from the field list → this vector is red.</summary>
    private const string RcV1Canonical =
        "{\"module\":\"recall\",\"subjectType\":\"recall.case\",\"subjectId\":\"11111111-1111-1111-1111-111111111111\",\"fields\":{"
        + "\"approvedSnapshotSha256\":\"seal-approved-r1\",\"class\":\"2\",\"closureSummary\":null,\"direction\":\"Forward\","
        + "\"effectiveness.44444444-4444-4444-4444-444444444444.EA.consumed\":\"100\",\"effectiveness.44444444-4444-4444-4444-444444444444.EA.met\":\"false\","
        + "\"effectiveness.44444444-4444-4444-4444-444444444444.EA.open\":\"100\",\"effectiveness.44444444-4444-4444-4444-444444444444.EA.percent\":\"80\","
        + "\"effectiveness.44444444-4444-4444-4444-444444444444.EA.returned\":\"300\",\"effectiveness.44444444-4444-4444-4444-444444444444.EA.shipped\":\"500\","
        + "\"effectiveness.targetPercent\":\"95\",\"excludedCount\":\"1\",\"includedShipmentCount\":\"3\",\"includedStockCount\":\"2\","
        + "\"legalEntityId\":\"22222222-2222-2222-2222-222222222222\",\"level\":\"Pharmacy\",\"number\":\"RC-2026-0001\",\"openQuantityAcceptance\":null,"
        + "\"pendingAction\":\"ApproveScope\",\"pendingActionPayloadSha256\":\"payload-sha-1\",\"reason\":\"Tedarikci sapma bildirimi\",\"reasonReference\":\"DEV-SUP-2026-031\","
        + "\"rootLotId\":\"55555555-5555-5555-5555-555555555555\",\"rootLotNumber\":\"LOT-A1\",\"scopeRevision\":\"1\",\"skuId\":\"44444444-4444-4444-4444-444444444444\","
        + "\"status\":\"PendingScopeApproval\",\"submittedSnapshotSha256\":\"seal-submitted-r1\",\"version\":\"4\"}}";

    private const string DrV1Canonical =
        "{\"module\":\"recall\",\"subjectType\":\"recall.drill-run\",\"subjectId\":\"77777777-7777-7777-7777-777777777777\",\"fields\":{"
        + "\"durationMinutes\":\"150\",\"endedAtUtc\":\"2026-10-14T10:30:00Z\",\"gapCount\":\"0\",\"lotId\":\"55555555-5555-5555-5555-555555555555\",\"lotNumber\":\"LOT-A1\","
        + "\"number\":\"RC-2026-D001\",\"passed\":\"true\",\"planCode\":\"PLAN-Q4\",\"resultNote\":\"Tatbikat hedef surede tamamlandi\",\"snapshotSha256\":\"drill-seal-1\","
        + "\"startedAtUtc\":\"2026-10-14T08:00:00Z\",\"targetHours\":\"4\",\"version\":\"2\"}}";

    private const string RsV1Canonical =
        "{\"module\":\"recall\",\"subjectType\":\"recall.settings-change\",\"subjectId\":\"66666666-6666-6666-6666-666666666666\",\"fields\":{"
        + "\"changes.drillTargetHours\":\"4|6\",\"changes.traceMaxDepth\":\"20|25\",\"legalEntityId\":\"22222222-2222-2222-2222-222222222222\","
        + "\"reason\":\"Tatbikat hedefi 6 saate cikarildi\",\"settingsVersion\":\"3\"}}";

    private const string RcV1Sha256 = "245b5686c77cdda0e4d73cfef95055d5b4b75687ebb2650e582284e0510e8674";
    private const string DrV1Sha256 = "967bc0aa8abb4dd6fb2a15807ed99363ac9b34c89032efc39497ea41126db54b";
    private const string RsV1Sha256 = "5efb919331ae07914a1df82a5643aa2b81b34aced82a9012b6154c0f8f088cd1";

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static RecallCase GoldenCase() => new()
    {
        Id = CaseId, LegalEntityId = Company, Number = "RC-2026-0001", Status = RecallCaseStatus.PendingScopeApproval, Class = 2, Level = RecallLevel.Pharmacy,
        RootLotId = "55555555-5555-5555-5555-555555555555", RootLotNumber = "LOT-A1", SkuId = Sku, Direction = RecallTraceDirection.Forward,
        Reason = "Tedarikci sapma bildirimi", ReasonReference = "DEV-SUP-2026-031", ScopeRevision = 1,
        SubmittedScope = new RecallScopeSeal { Revision = 1, Sha256 = "seal-submitted-r1" }, ApprovedScope = new RecallScopeSeal { Revision = 1, Sha256 = "seal-approved-r1" },
        PendingAction = new RecallPendingAction { Action = RecallPendingActionKind.ApproveScope, PayloadSha256 = "payload-sha-1" }, Version = 4
    };

    private static RecallCaseSubjectFacts GoldenFacts() => new(1, 2, 3, new RecallEffectivenessResult
    {
        TargetPercent = 95,
        Totals = [new RecallEffectivenessRow { SkuId = Sku, BaseUomId = "EA", Shipped = 500, Returned = 300, Consumed = 100, Open = 100, Percent = 80, Met = false }]
    });

    [Fact]
    public void Golden_vector_rc_v1_signs_the_frozen_trace_seals()
    {
        var c = GoldenCase();
        Assert.Equal(RcV1Canonical, RecallSignableSubjects.Canonical(RecallSignableActions.CaseSubjectType, CaseId.ToString("D"), RecallSignableSubjects.CaseFields(c, GoldenFacts())));
        Assert.Equal(RcV1Sha256, RecallSignableSubjects.CaseHash(c, GoldenFacts()));
        Assert.Equal(RcV1Sha256, Sha(RcV1Canonical));
        Assert.Equal("rc-v1", RecallSignableActions.CaseFieldListVersion);
    }

    [Fact]
    public void Golden_vector_dr_v1()
    {
        var d = new RecallDrillRunSubjectFacts(Guid.Parse("77777777-7777-7777-7777-777777777777"), "RC-2026-D001", "PLAN-Q4", "55555555-5555-5555-5555-555555555555", "LOT-A1",
            new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 14, 10, 30, 0, TimeSpan.Zero), 150, 4, true, 0, "drill-seal-1",
            "Tatbikat hedef surede tamamlandi", 2);
        Assert.Equal(DrV1Canonical, RecallSignableSubjects.Canonical(RecallSignableActions.DrillRunSubjectType, d.RunId.ToString("D"), RecallSignableSubjects.DrillRunFields(d)));
        Assert.Equal(DrV1Sha256, RecallSignableSubjects.DrillRunHash(d));
        Assert.Equal(DrV1Sha256, Sha(DrV1Canonical));
        Assert.Equal("dr-v1", RecallSignableActions.DrillRunFieldListVersion);
    }

    [Fact]
    public void Golden_vector_rs_v1()
    {
        var change = new RecallSettingsChange
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), LegalEntityId = Company, SettingsVersion = 3, Reason = "Tatbikat hedefi 6 saate cikarildi",
            Changes = [new() { Field = "drillTargetHours", Old = "4", New = "6" }, new() { Field = "traceMaxDepth", Old = "20", New = "25" }]
        };
        Assert.Equal(RsV1Canonical, RecallSignableSubjects.Canonical(RecallSignableActions.SettingsChangeSubjectType, change.Id.ToString("D"), RecallSignableSubjects.SettingsChangeFields(change)));
        Assert.Equal(RsV1Sha256, RecallSignableSubjects.SettingsChangeHash(change));
        Assert.Equal(RsV1Sha256, Sha(RsV1Canonical));
        Assert.Equal("rs-v1", RecallSignableActions.SettingsChangeFieldListVersion);
    }

    [Fact]
    public void The_field_lists_and_actions_are_the_contracts_x_signable_actions()
    {
        var yaml = RecallContractYaml.Load();
        var block = yaml["x-signable-actions"]!;
        Assert.Equal("recall", block["module"]!.GetValue<string>());
        var contract = block["actions"]!.AsArray().Select(a => (a!["actionCode"]!.GetValue<string>(), a["defaultRequired"]!.ToString(), a["subject"]!.GetValue<string>())).ToList();
        Assert.Equal(contract, RecallSignableActions.All.Select(a => (a.Code, a.DefaultRequired ? "true" : "false", a.SubjectType)).ToList());
        Assert.All(RecallSignableActions.All, a => Assert.Equal((block["defaultMeaningCode"]!.GetValue<string>(), block["defaultVisualMode"]!.GetValue<string>()), (a.DefaultMeaning, a.DefaultVisualMode)));

        List<string> Fields(string type) => block["subjects"]![type]!["fields"]!.AsArray().Select(f => f!.GetValue<string>()).ToList();
        Assert.Equal("rc-v1", block["subjects"]!["recall.case"]!["fieldListVersion"]!.GetValue<string>());
        var rc = RecallSignableSubjects.CaseFields(GoldenCase(), GoldenFacts()).Keys.ToList();
        Assert.Equal(Fields("recall.case").Where(f => f != "effectiveness.*").OrderBy(f => f, StringComparer.Ordinal),
            rc.Where(f => !f.StartsWith("effectiveness.", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal));
        Assert.Contains("effectiveness.*", Fields("recall.case"));
        var drill = RecallSignableSubjects.DrillRunFields(new RecallDrillRunSubjectFacts(Guid.Empty, "", "", "", "", DateTimeOffset.UnixEpoch, null, null, 1, null, 0, null, null, 1));
        Assert.Equal(Fields("recall.drill-run").OrderBy(f => f, StringComparer.Ordinal), drill.Keys.OrderBy(f => f, StringComparer.Ordinal));
        var rs = RecallSignableSubjects.SettingsChangeFields(new RecallSettingsChange { Changes = [new() { Field = "x", Old = "1", New = "2" }] }).Keys;
        Assert.Equal(Fields("recall.settings-change").Where(f => f != "changes.*").OrderBy(f => f, StringComparer.Ordinal),
            rs.Where(f => !f.StartsWith("changes.", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal));
    }

    // ── registration (one call per module; recall = the third) ─────────────────────────────────────────────

    [Fact]
    public async Task The_five_recall_actions_are_registered_in_one_call_under_their_own_prefix()
    {
        var calls = await ESignActionRegistrationRegressionTests.RunAsync(3);
        var recall = Assert.Single(calls, c => c.Body.Contains("\"module\":\"recall\"", StringComparison.Ordinal));
        Assert.Equal("http://platform.test/api/internal/esign/actions/register", recall.Url);
        var body = JsonNode.Parse(recall.Body)!;
        var actions = body["actions"]!.AsArray();
        Assert.Equal(["recall.scope.approve", "recall.effectiveness.complete", "recall.case.close", "recall.drill.result", "recall.settings.update"],
            actions.Select(a => a!["actionCode"]!.GetValue<string>()));
        Assert.All(actions, a =>
        {
            Assert.StartsWith("recall.", a!["actionCode"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal(new[] { "ar", "en", "es", "fr", "ru", "tr", "zh" }, a["names"]!.AsObject().Select(n => n.Key).OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(a["names"]!.AsObject(), n => Assert.False(string.IsNullOrWhiteSpace(n.Value!.GetValue<string>())));
            Assert.Equal("APPROVE", a["defaultMeaningCode"]!.GetValue<string>());
        });
        Assert.Equal([true, true, true, true, false], actions.Select(a => a!["defaultRequired"]!.GetValue<bool>()));
        Assert.Equal("Geri çağırma kapsamını onayla", actions[0]!["names"]!["tr"]!.GetValue<string>());
        Assert.Equal("Geri çağırma ayarlarını değiştir", actions[4]!["names"]!["tr"]!.GetValue<string>());
        Assert.DoesNotContain(calls, c => !c.Body.Contains("\"module\":\"recall\"", StringComparison.Ordinal) && c.Body.Contains("recall.", StringComparison.Ordinal));
        Assert.Equal(RecallRegistrationSha256, Sha(recall.Body));
    }

    /// <summary>SHA-256 of the recall registration body (System.Net.Http.Json defaults) — the body the Platform host test posts (scripts §).</summary>
    internal const string RecallRegistrationSha256 = "248e19eefc7b59e0f94216759da76146f040f74bbd7fd3f34954315c3ceddd35";

    // ── world ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed record World(RecallScenario S, string Qa1, string Qa2, string Admin);

    private World Make(bool required = true)
    {
        var s = new RecallScenario(_factory);
        foreach (var action in RecallSignableActions.All) ESign.Policy(s.Tenant, action.Code, required, "APPROVE");
        return new World(s, s.Mint(s.Qa1, [.. RecallR3.QaKeys, .. ESignKeys]), s.Mint(s.Qa2, [.. RecallR3.QaKeys, .. ESignKeys]),
            s.Mint(s.Admin, [.. RecallScenario.AdminKeys, .. ESignKeys]));
    }

    /// <summary>ESIGN §7 canonical form, written independently of production (Platform's side of the hash).</summary>
    internal static string PlatformHash(string subjectType, string subjectId, JsonElement fields)
    {
        static string Q(string v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var sb = new StringBuilder("{\"module\":\"recall\",\"subjectType\":").Append(Q(subjectType)).Append(",\"subjectId\":").Append(Q(subjectId)).Append(",\"fields\":{");
        sb.Append(string.Join(",", fields.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => Q(p.Name) + ":" + (p.Value.ValueKind == JsonValueKind.Null ? "null" : Q(p.Value.GetString()!)))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.Append("}}").ToString()))).ToLowerInvariant();
    }

    private static Task<JsonElement> SubjectAsync(RecallScenario s, string type, string id, string token, int expected = 200)
        => s.ExpectAsync(HttpMethod.Get, $"/api/recall/esign-subjects/{type}/{id}", token, null, expected);

    /// <summary>Opened + submitted by Qa1, the approval prepared by Qa2 (CT-R1: the submitter cannot approve).</summary>
    private static async Task<(string Id, JsonElement Prepared)> PreparedApprovalAsync(World w)
    {
        var c = await w.S.OpenAsync(await w.S.LotAsync(), w.Qa1);
        var id = RecallScenario.Id(c);
        var (ss, submitted, _) = await w.S.SubmitAsync(id, RecallScenario.ETag(c), w.Qa1);
        Assert.True(ss == 200, submitted.ToString());
        var (sp, prepared, _) = await w.S.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), w.Qa2);
        Assert.True(sp == 200, prepared.ToString());
        return (id, prepared);
    }

    /// <summary>What MOD-0022 does at signing: read the provider with the signer's token, hash the fields, record the signature.</summary>
    private async Task<string> SignAsync(World w, string caseId, string action = RecallSignableActions.ScopeApprove, Guid? signer = null, string meaning = "APPROVE",
        DateTimeOffset? signedAt = null, Guid? tenant = null, string? token = null)
    {
        var subject = await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, caseId, token ?? w.Qa2);
        var hash = PlatformHash(RecallSignableActions.CaseSubjectType, caseId, subject.GetProperty("fields"));
        return ESign.Sign(tenant ?? w.S.Tenant, signer ?? w.S.Qa2, caseId, meaning, hash, "recall", RecallSignableActions.CaseSubjectType, action, signedAt, w.S.Ledger.LegalEntity);
    }

    private static Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> ApproveAsync(World w, string id, JsonElement prepared, string? signatureId)
        => w.S.ApproveAsync(id, RecallScenario.ETag(prepared), w.Qa2, signatureId);

    private static void Refused((int Status, JsonElement Body, HttpResponseMessage Raw) answer, int status, string code, string because)
        => Assert.True(answer.Status == status && RecallScenario.Code(answer.Body) == code, $"{because}: {answer.Status} {answer.Body}");

    private async Task AcceptedAsync(World w, string id, JsonElement prepared)
    {
        var (status, approved, _) = await ApproveAsync(w, id, prepared, await SignAsync(w, id));
        Assert.True(status == 200, $"the correct signature must pass: {approved}");
        Assert.Equal("Active", approved.GetProperty("status").GetString());
    }

    // ── provider ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_provider_answers_rc_v1_hashed_like_the_verification_and_refuses_with_one_404()
    {
        var w = Make();
        var (id, _) = await PreparedApprovalAsync(w);
        var subject = await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, id, w.Qa2);
        var fields = subject.GetProperty("fields");
        Assert.Equal(w.S.Ledger.LegalEntity.ToString("D"), subject.GetProperty("legalEntityId").GetString());
        Assert.Equal(("ApproveScope", "PendingScopeApproval"), (fields.GetProperty("pendingAction").GetString(), fields.GetProperty("status").GetString()));
        var stored = Assert.Single(w.S.Persisted(RecallCollections.Cases), d => d["_id"].AsString == id);
        Assert.Equal(stored["SubmittedScope"]["Sha256"].AsString, fields.GetProperty("submittedSnapshotSha256").GetString()); // the frozen trace is signed
        Assert.Equal(JsonValueKind.Null, fields.GetProperty("approvedSnapshotSha256").ValueKind);

        // the provider and the verification hash the same record the same way (one function — F3-A)
        using (var scope = _factory.Services.CreateScope())
        {
            var tenant = scope.ServiceProvider.GetRequiredService<Diten.InventoryService.Application.Common.ITenantContext>();
            tenant.SetScope(w.S.Tenant, w.S.Ledger.LegalEntity, w.S.Qa2, Diten.InventoryService.Application.Common.LegalEntitySources.Token);
            var c = await scope.ServiceProvider.GetRequiredService<Diten.InventoryService.Domain.Recall.Repositories.IRecallRepository>().FindCaseAsync(Guid.Parse(id));
            var facts = await scope.ServiceProvider.GetRequiredService<RecallSubjectFacts>().FactsAsync(c!, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(PlatformHash(RecallSignableActions.CaseSubjectType, id, fields), RecallSignableSubjects.CaseHash(c!, facts));
        }

        // claim-less token (MOD-0022 sends no company): the record's company is used
        var claimless = TestTokens.Mint(w.S.Tenant, null, w.S.Qa2, RecallR3.QaKeys);
        Assert.Equal(fields.ToString(), (await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, id, claimless)).GetProperty("fields").ToString());

        var notFound = await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, id, w.S.Mint(w.S.Qa2, ["recall.settings.read"]), 404); // no recall.cases.read
        Assert.Equal("RECALL_SUBJECT_NOT_FOUND", RecallScenario.Code(notFound));
        await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, id, w.S.Mint(w.S.Qa2, RecallR3.QaKeys, Guid.NewGuid()), 404);        // another company named
        await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, Guid.NewGuid().ToString("D"), w.Qa2, 404);                            // unknown id
        await SubjectAsync(w.S, "recall.unknown", id, w.Qa2, 404);                                                                           // unknown type
        await SubjectAsync(w.S, RecallSignableActions.DrillRunSubjectType, id, w.Qa2, 404);                                                  // drill runs: R5
        await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, id, TestTokens.Mint(Guid.NewGuid(), null, w.S.Qa2, RecallR3.QaKeys), 404); // another tenant
        Assert.Equal(401, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/esign-subjects/recall.case/{id}", "")).Status);
    }

    /// <summary>
    /// The provider's first read is bound to the caller's TENANT (K3 sabotage (g)): another tenant's id finds nothing. The company-scoped
    /// repository behind it would also find nothing over HTTP, so the directory itself is measured, on the record's tenant and another one.
    /// </summary>
    [Fact]
    public async Task The_subject_directory_is_tenant_bound_K3_g()
    {
        var w = Make();
        var (id, _) = await PreparedApprovalAsync(w);
        var (prepared, _) = await PrepareAndSignSettingsAsync(w, SettingsBody(6, "Tatbikat hedefi 6 saate cikarildi"));
        var changeId = Guid.Parse(prepared.GetProperty("subject").GetProperty("subjectId").GetString()!);
        using var scope = _factory.Services.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<IRecallSubjectDirectory>();
        Assert.Equal((w.S.Ledger.LegalEntity, (Guid?)null), await directory.SubjectAsync(w.S.Tenant, RecallSignableActions.CaseSubjectType, Guid.Parse(id)));
        Assert.Null(await directory.SubjectAsync(Guid.NewGuid(), RecallSignableActions.CaseSubjectType, Guid.Parse(id)));
        Assert.Equal((w.S.Ledger.LegalEntity, (Guid?)w.S.Admin), await directory.SubjectAsync(w.S.Tenant, RecallSignableActions.SettingsChangeSubjectType, changeId));
        Assert.Null(await directory.SubjectAsync(Guid.NewGuid(), RecallSignableActions.SettingsChangeSubjectType, changeId));
        Assert.Null(await directory.SubjectAsync(w.S.Tenant, RecallSignableActions.DrillRunSubjectType, Guid.Parse(id)));
    }

    // ── binding rules (each: the refusal, then the acceptance on the same case) ──────────────────────────────

    [Fact]
    public async Task Rule_same_tenant_a_signature_recorded_in_another_tenant_is_refused()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        Refused(await ApproveAsync(w, id, prepared, await SignAsync(w, id, tenant: Guid.NewGuid())), 409, "RECALL_SIGNATURE_MISMATCH", "another tenant's record (404 at MOD-0022)");
        await AcceptedAsync(w, id, prepared);
    }

    [Fact]
    public async Task Rule_signer_is_the_acting_user_K3_d()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        Refused(await ApproveAsync(w, id, prepared, await SignAsync(w, id, signer: w.S.Qa1)), 409, "RECALL_SIGNATURE_MISMATCH", "signed by Qa1, sent by Qa2");
        await AcceptedAsync(w, id, prepared);
    }

    [Fact]
    public async Task Rule_the_action_code_is_the_decisions()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        Refused(await ApproveAsync(w, id, prepared, await SignAsync(w, id, action: RecallSignableActions.CaseClose)), 409, "RECALL_SIGNATURE_MISMATCH", "a closure signature");
        await AcceptedAsync(w, id, prepared);
    }

    [Fact]
    public async Task Rule_the_meaning_is_the_effective_policys()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        Refused(await ApproveAsync(w, id, prepared, await SignAsync(w, id, meaning: "REVIEW")), 409, "RECALL_SIGNATURE_MISMATCH", "REVIEW while the policy says APPROVE");
        await AcceptedAsync(w, id, prepared);
    }

    [Fact]
    public async Task Rule_the_subject_hash_is_the_current_one_a_case_changed_after_signing_is_refused_K3_a()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        var signature = await SignAsync(w, id);
        // the case changes after the signature (no version / preparation change): the signed subject is stale
        var cases = w.S.Factory.Database.GetCollection<BsonDocument>(RecallCollections.Cases);
        var original = Assert.Single(w.S.Persisted(RecallCollections.Cases), d => d["_id"].AsString == id)["ReasonReference"];
        cases.UpdateOne(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("ReasonReference", "DEV-CHANGED-AFTER-SIGNING")));
        Refused(await ApproveAsync(w, id, prepared, signature), 409, "RECALL_SIGNATURE_MISMATCH", "stale subject");
        Assert.Equal("PendingScopeApproval", (await w.S.GetAsync(id, w.Qa2)).GetProperty("status").GetString());
        cases.UpdateOne(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("ReasonReference", original)));
        Refused(await ApproveAsync(w, id, prepared, await SignAsync(w, id, signedAt: DateTimeOffset.UtcNow.AddHours(-1))), 409, "RECALL_SIGNATURE_MISMATCH", "control: before the preparation");
        await AcceptedAsync(w, id, prepared); // the case as signed again: a fresh signature of the current subject passes
    }

    [Fact]
    public async Task Rule_an_invalidated_signature_is_refused()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        var signature = await SignAsync(w, id);
        ESign.Invalidate(signature);
        Refused(await ApproveAsync(w, id, prepared, signature), 409, "RECALL_SIGNATURE_MISMATCH", "invalidated");
        await AcceptedAsync(w, id, prepared);
    }

    [Fact]
    public async Task Rule_signed_after_the_preparation()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        var preparedAt = DateTimeOffset.Parse(prepared.GetProperty("expiresAtUtc").GetString()!, System.Globalization.CultureInfo.InvariantCulture) - TimeSpan.FromMinutes(15);
        Refused(await ApproveAsync(w, id, prepared, await SignAsync(w, id, signedAt: preparedAt.AddSeconds(-5))), 409, "RECALL_SIGNATURE_MISMATCH", "signed before the preparation");
        await AcceptedAsync(w, id, prepared);
    }

    [Fact]
    public async Task Rule_never_used_before_a_signature_already_on_a_decision_is_refused_K3_e()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        var signature = await SignAsync(w, id); // valid in every other respect
        // the id already carried by a decision of this company (an event row, the way a decision records it)
        w.S.Factory.Database.GetCollection<RecallCaseEvent>(RecallCollections.CaseEvents).InsertOne(new RecallCaseEvent
        {
            Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, TenantId = w.S.Tenant, LegalEntityId = w.S.Ledger.LegalEntity, CaseId = Guid.NewGuid(), Sequence = 1,
            Kind = RecallCaseEventKind.ScopeApproved, AtUtc = DateTimeOffset.UtcNow, SignatureId = signature
        });
        Refused(await ApproveAsync(w, id, prepared, signature), 409, "RECALL_SIGNATURE_MISMATCH", "used before");
        await AcceptedAsync(w, id, prepared);
    }

    [Fact]
    public async Task Rule_another_cases_signature_is_refused_K3_b()
    {
        var w = Make();
        var (a, _) = await PreparedApprovalAsync(w);
        var (b, preparedB) = await PreparedApprovalAsync(w);
        Refused(await ApproveAsync(w, b, preparedB, await SignAsync(w, a)), 409, "RECALL_SIGNATURE_MISMATCH", "case A's signature on case B");
        await AcceptedAsync(w, b, preparedB);
    }

    [Fact]
    public async Task Rule_required_by_the_policy_and_missing_is_refused_K3_f_not_required_passes_unsigned()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        Assert.True(prepared.GetProperty("signatureRequired").GetBoolean());
        Refused(await ApproveAsync(w, id, prepared, null), 400, "RECALL_SIGNATURE_REQUIRED", "required and missing");
        Assert.DoesNotContain(w.S.Persisted(RecallCollections.LotBlocks), b => b["CaseId"].AsString == id);
        await AcceptedAsync(w, id, prepared);

        // the tenant's policy row says not required → the decision passes without a signature (pack §13 item 7)
        var free = Make(required: false);
        var (other, otherPrepared) = await PreparedApprovalAsync(free);
        Assert.False(otherPrepared.GetProperty("signatureRequired").GetBoolean());
        var (status, approved, _) = await ApproveAsync(free, other, otherPrepared, null);
        Assert.True(status == 200, approved.ToString());
    }

    [Fact]
    public async Task MOD_0022_unreadable_fails_closed()
    {
        var w = Make();
        var (id, prepared) = await PreparedApprovalAsync(w);
        var signature = await SignAsync(w, id);
        ESign.Down = true;
        try
        {
            Refused(await ApproveAsync(w, id, prepared, signature), 409, "RECALL_SIGNATURE_MISMATCH", "MOD-0022 down");
        }
        finally
        {
            ESign.Down = false;
        }

        await AcceptedAsync(w, id, prepared);
    }

    // ── end to end: approval (+ CT-R1), revision delta (CT-R5), effectiveness, closure ─────────────────────

    [Fact]
    public async Task Prepare_sign_approve_revise_complete_close_end_to_end_and_a_used_signature_is_refused_again()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        foreach (var action in RecallSignableActions.All) ESign.Policy(w.S.Tenant, action.Code, true, "APPROVE");
        var qa1 = w.S.Mint(w.S.Qa1, [.. RecallR3.QaKeys, .. ESignKeys]);
        var qa2 = w.S.Mint(w.S.Qa2, [.. RecallR3.QaKeys, .. ESignKeys]);
        var world = new World(w.S, qa1, qa2, w.S.AdminToken);

        var body = RecallScenario.Open(w.Fg);
        body["direction"] = "Forward";
        var (so, opened, _) = await w.S.PostOpenAsync(qa1, body);
        Assert.True(so == 201, opened.ToString());
        var id = RecallScenario.Id(opened);
        var (_, submitted, _) = await w.S.SubmitAsync(id, RecallScenario.ETag(opened), qa1);
        Refused(await w.S.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), qa1), 409, "RECALL_SOD_VIOLATION", "CT-R1: the submitter"); // four-eyes unchanged
        var (_, prepared, _) = await w.S.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), qa2);
        var first = await SignAsync(world, id);
        var (sa, approved, _) = await w.S.ApproveAsync(id, RecallScenario.ETag(prepared), qa2, first);
        Assert.True(sa == 200, approved.ToString());
        Assert.Equal(first, approved.GetProperty("scopeApprovalSignatureId").GetString());
        Assert.Equal(first, Assert.Single(RecallR3.Events(w.S, id, "ScopeApproved"))["SignatureId"].AsString); // 0177 keeps only the id

        // CT-R5: a revision's delta has its own signed approval; the first approval's signature is refused for it
        var derived = await w.S.LotAsync();
        await w.PostAsync(w.Lots.Receipt("3", derived, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", "MO-R4-" + w.Order);
        await w.LinkAsync(w.Fg, derived, "MO-R4-" + w.Order);
        var current = await w.S.GetAsync(id, qa1);
        var (sr, revised, _) = await w.S.CommandAsync(id, "scope/revisions", qa1, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("rev"), ["addNodeKeys"] = new[] { $"Lot:{derived}" }, ["reason"] = "Onay sonrasi uretilen lot kapsama alinmali"
        }, RecallScenario.ETag(current));
        Assert.True(sr == 201, revised.ToString());
        var (_, deltaPrepared, _) = await w.S.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(revised), qa2);
        Refused(await w.S.ApproveAsync(id, RecallScenario.ETag(deltaPrepared), qa2, first), 409, "RECALL_SIGNATURE_MISMATCH", "the first approval's signature again");
        var (sd, delta, _) = await w.S.ApproveAsync(id, RecallScenario.ETag(deltaPrepared), qa2, await SignAsync(world, id));
        Assert.True(sd == 200, delta.ToString());
        Assert.Equal(2, delta.GetProperty("approvedScope").GetProperty("revision").GetInt32());

        // effectiveness completion (Qa1 signs) and closure (Qa2 signs — CT-R1: not the completer)
        var (st, started, _) = await w.S.CommandAsync(id, "effectiveness/start", qa1, new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("st") }, RecallScenario.ETag(delta));
        Assert.True(st == 200, started.ToString());
        const string note = "Etkinlik kontrolu yapildi, acik miktar degerlendirildi";
        var (_, cePrepared, _) = await w.S.PrepareAsync(id, "CompleteEffectiveness", RecallScenario.ETag(started), qa1, note: note);
        var ceSubject = (await SubjectAsync(w.S, RecallSignableActions.CaseSubjectType, id, qa1)).GetProperty("fields");
        Assert.Equal("CompleteEffectiveness", ceSubject.GetProperty("pendingAction").GetString());
        Assert.True(ceSubject.TryGetProperty("effectiveness.targetPercent", out _), "the completion signs the effectiveness numbers");
        var ce = await SignAsync(world, id, RecallSignableActions.EffectivenessComplete, w.S.Qa1, token: qa1);
        Refused(await w.S.CommandAsync(id, "effectiveness/complete", qa1, new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("cmp"), ["note"] = note },
            RecallScenario.ETag(cePrepared)), 400, "RECALL_SIGNATURE_REQUIRED", "completion unsigned");
        var (sc, completed, _) = await w.S.CommandAsync(id, "effectiveness/complete", qa1, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cmp"), ["note"] = note, ["signatureId"] = ce
        }, RecallScenario.ETag(cePrepared));
        Assert.True(sc == 200, completed.ToString());

        const string summary = "Geri cagirma tamamlandi, kalan miktar degerlendirildi";
        const string acceptance = "Acik miktar musteri beyani ile kabul edildi";
        var (_, closePrepared, _) = await w.S.PrepareAsync(id, "Close", RecallScenario.ETag(completed), qa2, summary: summary, acceptance: acceptance);
        var close = await SignAsync(world, id, RecallSignableActions.CaseClose);
        var (sx, closed, _) = await w.S.CommandAsync(id, "close", qa2, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cl"), ["summary"] = summary, ["openQuantityAcceptance"] = acceptance, ["signatureId"] = close
        }, RecallScenario.ETag(closePrepared));
        Assert.True(sx == 200, closed.ToString());
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.DoesNotContain(RecallR3.Blocks(w.S, w.Fg), b => b["Active"].AsBoolean);
    }

    // ── settings (CT-R16): prepareSettingsChange → rs-v1 → updateSettings ──────────────────────────────

    private static Dictionary<string, object?> SettingsBody(int drillTargetHours, string reason, string? key = null) => new()
    {
        ["idempotencyKey"] = key ?? RecallScenario.Key("set"), ["drillTargetHours"] = drillTargetHours, ["drillFrequencyWarningDays"] = 180,
        ["effectivenessTargetPercent"] = new Dictionary<string, decimal> { ["1"] = 100, ["2"] = 95, ["3"] = 90 }, ["traceMaxDepth"] = 20, ["traceMaxNodes"] = 5000,
        ["reason"] = reason
    };

    private async Task<(JsonElement Prepared, string Signature)> PrepareAndSignSettingsAsync(World w, Dictionary<string, object?> body)
    {
        var (sp, prepared, _) = await w.S.SendAsync(HttpMethod.Post, "/api/recall/settings/pending-change", w.Admin, body, (string)body["idempotencyKey"]!, "W/\"0\"");
        Assert.True(sp == 200, prepared.ToString());
        var changeId = prepared.GetProperty("subject").GetProperty("subjectId").GetString()!;
        var subject = await SubjectAsync(w.S, RecallSignableActions.SettingsChangeSubjectType, changeId, w.Admin);
        var hash = PlatformHash(RecallSignableActions.SettingsChangeSubjectType, changeId, subject.GetProperty("fields"));
        return (prepared, ESign.Sign(w.S.Tenant, w.S.Admin, changeId, "APPROVE", hash, "recall", RecallSignableActions.SettingsChangeSubjectType,
            RecallSignableActions.SettingsUpdate, null, w.S.Ledger.LegalEntity));
    }

    private static Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> SaveSettingsAsync(World w, Dictionary<string, object?> body, string? signatureId)
    {
        var put = new Dictionary<string, object?>(body) { ["idempotencyKey"] = RecallScenario.Key("put") };
        if (signatureId is not null) put["signatureId"] = signatureId;
        return w.S.SendAsync(HttpMethod.Put, "/api/recall/settings", w.Admin, put, (string)put["idempotencyKey"]!, "W/\"0\"");
    }

    [Fact]
    public async Task Settings_prepare_sign_save_end_to_end_rs_v1()
    {
        var w = Make();
        var body = SettingsBody(6, "Tatbikat hedefi 6 saate cikarildi");
        var (prepared, signature) = await PrepareAndSignSettingsAsync(w, body);
        Assert.Equal(("UpdateSettings", "recall.settings.update", true), (prepared.GetProperty("action").GetString(), prepared.GetProperty("actionCode").GetString(),
            prepared.GetProperty("signatureRequired").GetBoolean()));
        var changeId = prepared.GetProperty("subject").GetProperty("subjectId").GetString()!;
        Assert.Equal(changeId, prepared.GetProperty("caseId").GetString()); // R4-1: PendingActionResponse.caseId carries the change id
        var fields = (await SubjectAsync(w.S, RecallSignableActions.SettingsChangeSubjectType, changeId, w.Admin)).GetProperty("fields");
        Assert.Equal(("4|6", "0", "Tatbikat hedefi 6 saate cikarildi"), (fields.GetProperty("changes.drillTargetHours").GetString(), fields.GetProperty("settingsVersion").GetString(),
            fields.GetProperty("reason").GetString()));
        Assert.Single(w.S.Audit("recall.settings.change-prepared"));

        // the same key + the same body → the same preparation; another body → 400
        var (sr, replay, _) = await w.S.SendAsync(HttpMethod.Post, "/api/recall/settings/pending-change", w.Admin, body, (string)body["idempotencyKey"]!, "W/\"0\"");
        Assert.Equal((200, changeId), (sr, replay.GetProperty("subject").GetProperty("subjectId").GetString()));
        var reused = SettingsBody(7, "Tatbikat hedefi 7 saate cikarildi", (string)body["idempotencyKey"]!);
        Refused(await w.S.SendAsync(HttpMethod.Post, "/api/recall/settings/pending-change", w.Admin, reused, (string)reused["idempotencyKey"]!, "W/\"0\""), 400, "IDEMPOTENCY_KEY_REUSED", "key reused");

        // required + unsigned → 400; signed → 200 with the id in the history
        Refused(await SaveSettingsAsync(w, body, null), 400, "RECALL_SIGNATURE_REQUIRED", "settings unsigned under a required policy");
        var (ss, saved, _) = await SaveSettingsAsync(w, body, signature);
        Assert.True(ss == 200, saved.ToString());
        Assert.All(w.S.Persisted(RecallCollections.SettingsHistory), h => Assert.Equal(signature, h["SignatureId"].AsString));

        // another user's prepared change is not its subject; another tenant sees nothing
        await SubjectAsync(w.S, RecallSignableActions.SettingsChangeSubjectType, changeId, w.S.Mint(Guid.NewGuid(), [.. RecallScenario.AdminKeys]), 404);
        await SubjectAsync(w.S, RecallSignableActions.SettingsChangeSubjectType, changeId, TestTokens.Mint(Guid.NewGuid(), null, w.S.Admin, RecallScenario.AdminKeys), 404);
    }

    [Fact]
    public async Task Settings_a_signature_binds_only_to_the_prepared_proposal_K3_h()
    {
        var w = Make();
        var prepared = SettingsBody(6, "Tatbikat hedefi 6 saate cikarildi");
        var (_, signature) = await PrepareAndSignSettingsAsync(w, prepared);

        // a DIFFERENT change sent with the signature of the prepared one → refused, nothing saved
        Refused(await SaveSettingsAsync(w, SettingsBody(12, "Tatbikat hedefi 12 saate cikarildi"), signature), 409, "RECALL_SIGNATURE_MISMATCH", "another proposal");
        Refused(await SaveSettingsAsync(w, SettingsBody(6, "Gerekce degisti ama deger ayni kaldi"), signature), 409, "RECALL_SIGNATURE_MISMATCH", "another reason");
        Assert.Empty(w.S.Persisted(RecallCollections.SettingsHistory));

        var (status, saved, _) = await SaveSettingsAsync(w, prepared, signature); // the prepared proposal itself
        Assert.True(status == 200, saved.ToString());
        Assert.Equal(6, saved.GetProperty("drillTargetHours").GetInt32());
    }
}

/// <summary>The RECALL contract as JSON (the suite's contract path).</summary>
internal static class RecallContractYaml
{
    public static JsonObject Load()
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new YamlDotNet.Serialization.DeserializerBuilder().Build()
            .Deserialize<object>(File.ReadAllText(ContractSchemas.RecallContractPath))))!;
}
