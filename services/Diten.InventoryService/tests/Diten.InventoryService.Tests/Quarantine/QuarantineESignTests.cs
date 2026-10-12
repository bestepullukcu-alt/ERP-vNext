using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Application.Features.Quarantine;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Infrastructure.ESign;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 pack §16 Q3 (KQ-B, KQ-G, V15) — the e-signature integration on the production approval port, with MOD-0022 and
/// MOD-0023 faked at the HTTP edge of the REAL clients (<see cref="FakeESignGateway"/>, <see cref="FakeWorkflowGateway"/> with
/// E4 simulated). Written first as the security bypass list: a required signature missing, another user's, another
/// decision's, another meaning, invalidated, a changed subject, no read key; the provider without <c>quarantine.holds.read</c>
/// or for another company; a decider who is the only candidate. Then the valid paths, the golden <c>qd-v1</c> hash, the
/// approver's signature id from the Workflow history, the action registration body and the "ids only" scan.
/// </summary>
public sealed class QuarantineESignTests : IClassFixture<WorkflowQuarantineApiFactory>
{
    private readonly WorkflowQuarantineApiFactory _factory;

    public QuarantineESignTests(WorkflowQuarantineApiFactory factory)
    {
        _factory = factory;
        _factory.Workflow.EnforceSignatures = true; // MOD-0022 E4 live (simulated at the Workflow edge)
    }

    private FakeESignGateway ESign => _factory.ESign;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private async Task<QuarantineScenario> WorldAsync(bool approvers = true, bool self = false)
    {
        var s = new QuarantineScenario(_factory);
        var candidates = self
            ? new[] { "user:" + s.Ledger.User.ToString("D") }
            : new[] { "user:" + s.Approver.ToString("D") };
        if (approvers) await s.SaveSettingsAsync(new { approverCandidates = candidates, reconcileAfterSeconds = 0 });
        var key = QuarantineScenario.Key("tpl");
        await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/settings/workflow-template", new { idempotencyKey = key }, 200, key);
        return s;
    }

    private static async Task<JsonElement> DraftReleaseAsync(QuarantineScenario s, string quantity = "500")
    {
        var (_, hold) = await s.HoldAsync(quantity, await s.ReleasedLotAsync());
        return await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", decimal.Parse(quantity))]);
    }

    private async Task<JsonElement> SubjectAsync(QuarantineScenario s, string decisionId, string? token = null, int expected = 200)
        => await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/esign-subjects/quarantine.decision/{decisionId}", null, expected, token: token);

    private static Dictionary<string, string?> Fields(JsonElement subject)
        => subject.GetProperty("fields").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString());

    /// <summary>What Platform records when <paramref name="signer"/> signs: the subject hash of 0175's provider answer.</summary>
    private async Task<string> SignAsync(QuarantineScenario s, string decisionId, Guid? signer = null, string meaning = "RELEASE", string? hashOf = null)
    {
        var subjectFor = hashOf ?? decisionId;
        var hash = QuarantineSignableSubjectBuilder.Hash(subjectFor, Fields(await SubjectAsync(s, subjectFor)));
        return ESign.Sign(s.Tenant, signer ?? s.Ledger.User, decisionId, meaning, hash);
    }

    private static async Task<(int Status, JsonElement Body)> SubmitAsync(QuarantineScenario s, string decisionId, string? signatureId, string? token = null)
    {
        var key = QuarantineScenario.Key("sub");
        var (status, body, _) = await s.SendAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/submit",
            new { signatureId, idempotencyKey = key }, key, token: token);
        return (status, body);
    }

    private static string Message(JsonElement error) => error.GetProperty("error").GetProperty("message").GetString()!;

    private void RequireRelease(QuarantineScenario s) => ESign.Policy(s.Tenant, "quarantine.hold.release", required: true, meaning: "RELEASE");

    // ── decider: the bypass list ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Policy_required_and_no_signature_is_400_and_nothing_is_locked_or_started()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var draft = await DraftReleaseAsync(s);
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), null);
        Assert.Equal(400, status);
        Assert.Equal(QuarantineErrorCodes.SignatureRequired, QuarantineScenario.Code(body));
        Assert.Equal("Draft", Id(await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/decisions/{Id(draft, "decisionId")}", null, 200), "state"));
        Assert.Empty(_factory.Workflow.InstancesOf(Id(draft, "decisionId")));
        Assert.DoesNotContain(s.Events(), e => e["DecisionId"].AsString == Id(draft, "decisionId"));
    }

    /// <summary>K3 (a): without the signer = caller check this signature submits.</summary>
    [Fact]
    public async Task Another_users_signature_is_409_mismatch()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var draft = await DraftReleaseAsync(s);
        var byApprover = await SignAsync(s, Id(draft, "decisionId"), signer: s.Approver);
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), byApprover);
        Assert.Equal((409, QuarantineErrorCodes.SignatureMismatch), (status, QuarantineScenario.Code(body)));
        Assert.Contains("not made by you", Message(body));
    }

    /// <summary>
    /// K3 (b): the subject check names the reason; without it the subject HASH check (module / type / id are part of the
    /// canonical form) still refuses — measured, layered, the test pins the subject reason.
    /// </summary>
    [Fact]
    public async Task Another_decisions_signature_is_409_mismatch()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var mine = await DraftReleaseAsync(s, "300");
        var other = await DraftReleaseAsync(s, "200");
        var forOther = await SignAsync(s, Id(other, "decisionId"));
        var (status, body) = await SubmitAsync(s, Id(mine, "decisionId"), forOther);
        Assert.Equal((409, QuarantineErrorCodes.SignatureMismatch), (status, QuarantineScenario.Code(body)));
        Assert.Contains("not on this decision", Message(body));
    }

    [Fact]
    public async Task A_signature_with_another_meaning_is_409_mismatch()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var draft = await DraftReleaseAsync(s);
        var approve = await SignAsync(s, Id(draft, "decisionId"), meaning: "APPROVE");
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), approve);
        Assert.Equal((409, QuarantineErrorCodes.SignatureMismatch), (status, QuarantineScenario.Code(body)));
        Assert.Contains("means APPROVE", Message(body));
    }

    [Fact]
    public async Task An_invalidated_signature_is_409_mismatch()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var draft = await DraftReleaseAsync(s);
        var signature = await SignAsync(s, Id(draft, "decisionId"));
        ESign.Invalidate(signature);
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), signature);
        Assert.Equal((409, QuarantineErrorCodes.SignatureMismatch), (status, QuarantineScenario.Code(body)));
        Assert.Contains("invalidated", Message(body));
    }

    [Fact]
    public async Task A_signature_over_another_subject_state_is_409_mismatch()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var draft = await DraftReleaseAsync(s);
        var stale = ESign.Sign(s.Tenant, s.Ledger.User, Id(draft, "decisionId"), "RELEASE", new string('0', 64)); // the record signed was different
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), stale);
        Assert.Equal((409, QuarantineErrorCodes.SignatureMismatch), (status, QuarantineScenario.Code(body)));
        Assert.Contains("subject hash", Message(body));
    }

    [Fact]
    public async Task A_decider_who_cannot_read_signatures_is_refused_403()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var draft = await DraftReleaseAsync(s);
        var signature = await SignAsync(s, Id(draft, "decisionId"));
        var noRead = TestTokens.Mint(s.Tenant, s.Ledger.LegalEntity, s.Ledger.User,
            TestTokens.AllPermissions.Concat(QuarantineScenario.QuarantinePermissions).Append("esign.signatures.sign"));
        var (status, _) = await SubmitAsync(s, Id(draft, "decisionId"), signature, noRead);
        Assert.Equal(403, status);
        Assert.Empty(_factory.Workflow.InstancesOf(Id(draft, "decisionId")));
    }

    // ── decider: valid paths ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Policy_off_and_no_signature_submits()
    {
        var s = await WorldAsync();
        var draft = await DraftReleaseAsync(s);
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), null);
        Assert.Equal(202, status);
        Assert.False(body.GetProperty("signatureRequired").GetBoolean());
        Assert.False(body.TryGetProperty("deciderSignatureId", out _));
    }

    [Fact]
    public async Task The_deciders_own_binding_signature_submits_and_only_its_id_is_kept()
    {
        var s = await WorldAsync();
        RequireRelease(s);
        var draft = await DraftReleaseAsync(s);
        var decisionId = Id(draft, "decisionId");
        Assert.Equal("quarantine.hold.release", Id(draft, "esignActionCode"));
        var signature = await SignAsync(s, decisionId);
        var (status, body) = await SubmitAsync(s, decisionId, signature);
        Assert.True(status == 202, body.ToString());
        Assert.Equal(signature, Id(body, "deciderSignatureId"));
        Assert.True(body.GetProperty("signatureRequired").GetBoolean());
        var submitted = Assert.Single(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString == "Submitted");
        Assert.Equal(signature, submitted["DeciderSignatureId"].AsString);

        // KQ-G: 0175 keeps the id only — the signature's hash appears in no quarantine document
        var hash = QuarantineSignableSubjectBuilder.Hash(decisionId, Fields(await SubjectAsync(s, decisionId)));
        foreach (var collection in new[] { "quarantine_decisions", "quarantine_decision_events", "quarantine_history", "quarantine_holds" })
        {
            Assert.DoesNotContain(s.Ledger.Persisted(collection), d => d.ToJson().Contains(hash, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_partial_decision_uses_the_strictest_line_action()
    {
        var s = await WorldAsync();
        ESign.Policy(s.Tenant, "quarantine.hold.reject", required: true, meaning: "APPROVE");
        var (_, hold) = await s.HoldAsync("100", await s.ReleasedLotAsync());
        var draft = await s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Reject", 60m), QuarantineScenario.Line(2, "Block", 40m)], "DEVIATION");
        Assert.Equal("quarantine.hold.reject", Id(draft, "esignActionCode"));
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), null);
        Assert.Equal((400, QuarantineErrorCodes.SignatureRequired), (status, QuarantineScenario.Code(body)));
    }

    // ── O-Q2-3 ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>K3 (d): without the check MOD-0023 gets the decider as the only candidate (an un-approvable task).</summary>
    [Fact]
    public async Task The_decider_as_the_only_candidate_is_409_and_no_task_is_created()
    {
        var s = await WorldAsync(self: true);
        var draft = await DraftReleaseAsync(s);
        var (status, body) = await SubmitAsync(s, Id(draft, "decisionId"), null);
        Assert.Equal((409, QuarantineErrorCodes.ApproversNotConfigured), (status, QuarantineScenario.Code(body)));
        Assert.Empty(_factory.Workflow.InstancesOf(Id(draft, "decisionId")));
    }

    [Fact]
    public async Task The_decider_is_never_sent_as_a_candidate()
    {
        var s = await WorldAsync();
        await s.SaveSettingsAsync(new { approverCandidates = new[] { "user:" + s.Ledger.User.ToString("D"), "user:" + s.Approver.ToString("D") } });
        var draft = await DraftReleaseAsync(s);
        Assert.Equal(202, (await SubmitAsync(s, Id(draft, "decisionId"), null)).Status);
        var body = Assert.Single(_factory.Workflow.StartBodies, b => b["objectId"]!.GetValue<string>() == Id(draft, "decisionId"));
        Assert.Equal(["user:" + s.Approver.ToString("D")], body["candidatePrincipalIds"]!.AsArray().Select(c => c!.GetValue<string>()));
    }

    // ── approver ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_approvers_signature_id_comes_from_the_Workflow_history_and_unsigned_approval_is_refused()
    {
        var s = await WorldAsync();
        var draft = await DraftReleaseAsync(s, "80");
        var decisionId = Id(draft, "decisionId");
        var submitted = await SubmitAsync(s, decisionId, null);
        var instance = Id(submitted.Body.GetProperty("workflow"), "instanceId");

        Assert.Equal((409, "ESIGN_SIGNATURE_REQUIRED"), _factory.Workflow.Approve(instance, s.Approver)); // E4: the template step requires it
        var approverSignature = ESign.Sign(s.Tenant, s.Approver, decisionId, "APPROVE", "h"); // CT-D17: same decision subject
        Assert.Equal(200, _factory.Workflow.Approve(instance, s.Approver, approverSignature).Status);

        var decision = await s.ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/refresh", null, 200);
        Assert.Equal("Applied", Id(decision, "state"));
        Assert.Equal(approverSignature, Id(decision.GetProperty("workflow"), "approverSignatureId"));
        var approved = Assert.Single(s.Events(), e => e["DecisionId"].AsString == decisionId && e["Kind"].AsString == "Approved");
        Assert.Equal(approverSignature, approved["ApproverSignatureId"].AsString);
        Assert.Contains(s.Ledger.Persisted("quarantine_history"), h => h.Contains("ApproverSignatureId") && h["ApproverSignatureId"].IsString
                                                                       && h["ApproverSignatureId"].AsString == approverSignature);
    }

    [Fact]
    public void The_template_step_carries_exactly_the_keys_MOD_0022_E4_reads()
    {
        var step = JsonNode.Parse(QuarantineWorkflowTemplate.DefinitionJson)!["stages"]![0]!["steps"]![0]!;
        Assert.True(step["requirements"]!["signatureRequired"]!.GetValue<bool>());
        Assert.Equal("APPROVE", step["requirements"]!["signatureMeaning"]!.GetValue<string>());
    }

    // ── provider ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Pack §16 Q3 golden test; K3 (c): a field dropped from the list → red.</summary>
    [Fact]
    public async Task The_provider_answers_qd_v1_with_the_same_hash_every_time_and_a_changed_field_changes_it()
    {
        var s = await WorldAsync();
        var draft = await DraftReleaseAsync(s, "1200");
        var decisionId = Id(draft, "decisionId");
        var first = Fields(await SubjectAsync(s, decisionId));
        var second = Fields(await SubjectAsync(s, decisionId));
        Assert.Equal(QuarantineSignableSubjectBuilder.FieldNames.OrderBy(n => n, StringComparer.Ordinal), first.Keys.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(35, first.Count);
        Assert.Equal("qd-v1", first["subjectVersion"]);
        Assert.Equal(QuarantineSignableSubjectBuilder.Hash(decisionId, first), QuarantineSignableSubjectBuilder.Hash(decisionId, second));

        Assert.Equal(Id(draft, "decisionNumber"), first["decisionNumber"]);
        Assert.Equal("1200", first["line1.quantity"]);
        Assert.Equal("Release", first["line1.decision"]);
        Assert.Equal("AVAILABLE", first["line1.toStockStatus"]);
        Assert.Null(first["line2.decision"]);
        Assert.Equal("true", first["coaChecked"]);
        Assert.Equal(s.Ledger.User.ToString("D"), first["deciderUserId"]);
        Assert.NotNull(first["lotNumber"]);
        Assert.NotNull(first["locationCode"]);

        var changed = new Dictionary<string, string?>(first) { ["line1.quantity"] = "1199" };
        Assert.NotEqual(QuarantineSignableSubjectBuilder.Hash(decisionId, first), QuarantineSignableSubjectBuilder.Hash(decisionId, changed));
        var other = await DraftReleaseAsync(s, "700");
        Assert.NotEqual(QuarantineSignableSubjectBuilder.Hash(decisionId, first),
            QuarantineSignableSubjectBuilder.Hash(Id(other, "decisionId"), Fields(await SubjectAsync(s, Id(other, "decisionId")))));
    }

    /// <summary>ESIGN §7 vector computed by an independent implementation (node, scratchpad vector.js) — escapes, null, ordinal order.</summary>
    [Fact]
    public void The_canonical_form_matches_the_independent_vector()
    {
        var fields = new Dictionary<string, string?>
        {
            ["decisionNumber"] = "QKR-2026-0001", ["reason"] = "Analiz \"uygun\"\nsatır 2", ["line1.quantity"] = "1200.5",
            ["coaChecked"] = "true", ["skuCode"] = null, ["lotNumber"] = "PRS-26A014"
        };
        const string id = "8a6c1c2e-0000-4000-8000-000000000001";
        Assert.Equal("{\"module\":\"quarantine\",\"subjectType\":\"quarantine.decision\",\"subjectId\":\"8a6c1c2e-0000-4000-8000-000000000001\",\"fields\":{\"coaChecked\":\"true\",\"decisionNumber\":\"QKR-2026-0001\",\"line1.quantity\":\"1200.5\",\"lotNumber\":\"PRS-26A014\",\"reason\":\"Analiz \\\"uygun\\\"\\nsatır 2\",\"skuCode\":null}}",
            QuarantineSignableSubjectBuilder.Canonical(id, fields));
        Assert.Equal("5a2b8b6fcea649dfcb841361d5d914c0e04ea87924f70b0f7cc5c01eb1ba7edb", QuarantineSignableSubjectBuilder.Hash(id, fields));
    }

    [Fact]
    public async Task The_provider_is_404_without_holds_read_for_another_company_and_for_another_type()
    {
        var s = await WorldAsync();
        var draft = await DraftReleaseAsync(s);
        var decisionId = Id(draft, "decisionId");
        var noRead = TestTokens.Mint(s.Tenant, s.Ledger.LegalEntity, s.Ledger.User,
            TestTokens.AllPermissions.Concat(QuarantineScenario.QuarantinePermissions.Where(p => p != "quarantine.holds.read")));
        await SubjectAsync(s, decisionId, noRead, 404);
        var otherCompany = TestTokens.Mint(s.Tenant, Guid.NewGuid(), s.Ledger.User, TestTokens.AllPermissions.Concat(QuarantineScenario.QuarantinePermissions));
        await SubjectAsync(s, decisionId, otherCompany, 404);
        await s.ExpectAsync(HttpMethod.Get, $"/api/quarantine/esign-subjects/quarantine.hold/{decisionId}", null, 404);
        await SubjectAsync(s, Guid.NewGuid().ToString("D"), null, 404);
    }

    /// <summary>MOD-0022 calls with the signer's claim-less token and no company header: the record's company is used.</summary>
    [Fact]
    public async Task The_provider_answers_a_claim_less_token_from_the_records_company()
    {
        var s = await WorldAsync();
        var draft = await DraftReleaseAsync(s);
        var claimless = TestTokens.Mint(s.Tenant, null, s.Approver, TestTokens.AllPermissions.Concat(QuarantineScenario.QuarantinePermissions));
        var subject = await SubjectAsync(s, Id(draft, "decisionId"), claimless);
        Assert.Equal(s.Ledger.LegalEntity.ToString("D"), Id(subject, "legalEntityId"));
        Assert.Equal(Fields(await SubjectAsync(s, Id(draft, "decisionId"))), Fields(subject));
    }

    // ── registration + KQ-G scan ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_seven_signable_actions_of_pack_8_are_registered_with_their_defaults()
    {
        var body = JsonSerializer.SerializeToNode(ESignActionRegistrationHostedService.Body(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("quarantine", body["module"]!.GetValue<string>());
        var actions = body["actions"]!.AsArray().ToDictionary(a => a!["actionCode"]!.GetValue<string>(), a => a!);
        var expected = new Dictionary<string, (string Meaning, string Mode, bool Required)>
        {
            ["quarantine.hold.release"] = ("RELEASE", "savedAccepted", true), ["quarantine.hold.reject"] = ("APPROVE", "drawRequired", true),
            ["quarantine.hold.block"] = ("APPROVE", "optional", true), ["quarantine.blocked.dispose"] = ("APPROVE", "drawRequired", true),
            ["quarantine.blocked.unblock"] = ("RELEASE", "none", true), ["quarantine.hold.create"] = ("APPROVE", "none", false),
            ["quarantine.lot.release"] = ("RELEASE", "savedAccepted", true)
        };
        Assert.Equal(expected.Keys.OrderBy(k => k), actions.Keys.OrderBy(k => k));
        foreach (var (code, (meaning, mode, required)) in expected)
        {
            Assert.Equal(meaning, actions[code]["defaultMeaningCode"]!.GetValue<string>());
            Assert.Equal(mode, actions[code]["defaultVisualMode"]!.GetValue<string>());
            Assert.Equal(required, actions[code]["defaultRequired"]!.GetValue<bool>());
            Assert.Equal(7, actions[code]["names"]!.AsObject().Count);
        }
    }

    /// <summary>KQ-G source scan: no 0175 entity has a place for signature data — ids only.</summary>
    [Fact]
    public void No_quarantine_entity_can_hold_signature_data()
    {
        var entities = typeof(Diten.InventoryService.Domain.Quarantine.Entities.QuarantineDecision).Assembly.GetTypes()
            .Where(t => t.Namespace == "Diten.InventoryService.Domain.Quarantine.Entities");
        var props = entities.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => $"{t.Name}.{p.Name}")).ToList();
        Assert.Contains("QuarantineDecisionEvent.DeciderSignatureId", props);
        Assert.DoesNotContain(props, p => System.Text.RegularExpressions.Regex.IsMatch(p.Split('.')[1], "Hash|Digest|Png|Image|Vector|Ticket|Password|Meaning|Signer"));
    }
}
