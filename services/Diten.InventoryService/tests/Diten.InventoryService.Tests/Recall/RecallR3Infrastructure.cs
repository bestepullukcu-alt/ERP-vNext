using System.Text.Json;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R3 helpers over <see cref="RecallScenario"/> / <see cref="RecallTraceWorld"/>: the approval of a case (open Forward → submit by Qa1 →
/// prepare + sign + approve by Qa2, real handlers, contract-validated), its closure, ledger postings through the REAL HTTP endpoint, and the
/// recall records the tests read back.
/// </summary>
public static class RecallR3
{
    public static readonly string[] QaKeys = [.. RecallScenario.QaKeys, "recall.trace.run", "recall.quarantine-requests.create"];

    public static readonly string[] DepoKeys = [.. RecallScenario.DepoKeys, "recall.returns.link"];

    /// <summary>Open (Forward) on <paramref name="rootLot"/> as Qa1 of <paramref name="company"/>, submit, approve as Qa2 — the case is Active.</summary>
    /// <para>WP-MVP1-FU-CLEAN2: <paramref name="approverName"/> = the approver's token <c>name</c> claim (none = an unnamed token).</para>
    public static async Task<(string Id, JsonElement Case)> ApproveAsync(RecallScenario s, string rootLot, Guid? company = null, string? approverName = null)
    {
        var qa1 = s.Mint(s.Qa1, QaKeys, company);
        var qa2 = approverName is null
            ? s.Mint(s.Qa2, QaKeys, company)
            : TestTokens.Mint(s.Tenant, company ?? s.Ledger.LegalEntity, s.Qa2, QaKeys, [new System.Security.Claims.Claim("name", approverName)]);
        var body = RecallScenario.Open(rootLot);
        body["direction"] = "Forward";
        var (so, opened, _) = await s.PostOpenAsync(qa1, body);
        Assert.True(so == 201, opened.ToString());
        var id = RecallScenario.Id(opened);
        var (ss, submitted, _) = await s.SubmitAsync(id, RecallScenario.ETag(opened), qa1);
        Assert.True(ss == 200, submitted.ToString());
        var (sp, prepared, _) = await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), qa2);
        Assert.True(sp == 200, prepared.ToString());
        var (sa, approved, _) = await s.ApproveAsync(id, RecallScenario.ETag(prepared), qa2, s.Factory.Signature.Issue(s.Qa2, id, prepared.GetProperty("actionCode").GetString()!));
        Assert.True(sa == 200, approved.ToString());
        Assert.Equal("Active", approved.GetProperty("status").GetString());
        return (id, approved);
    }

    /// <summary>Effectiveness start + complete (Qa1), close (Qa2, with an acceptance) — the case is Closed and its blocks released.</summary>
    public static async Task<JsonElement> CloseAsync(RecallScenario s, string id, Guid? company = null)
    {
        var qa1 = s.Mint(s.Qa1, QaKeys, company);
        var qa2 = s.Mint(s.Qa2, QaKeys, company);
        var current = await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}", qa1, null, 200);
        var (st, started, _) = await s.CommandAsync(id, "effectiveness/start", qa1, new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("st") }, RecallScenario.ETag(current));
        Assert.True(st == 200, started.ToString());
        const string note = "Etkinlik kontrolu yapildi, acik miktar degerlendirildi";
        var (cp, prep, _) = await s.PrepareAsync(id, "CompleteEffectiveness", RecallScenario.ETag(started), qa1, note: note);
        Assert.True(cp == 200, prep.ToString());
        var (cc, completed, _) = await s.CommandAsync(id, "effectiveness/complete", qa1, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cmp"), ["note"] = note, ["signatureId"] = s.Factory.Signature.Issue(s.Qa1, id, "recall.effectiveness.complete")
        }, RecallScenario.ETag(prep));
        Assert.True(cc == 200, completed.ToString());
        const string summary = "Geri cagirma tamamlandi, kalan miktar degerlendirildi";
        const string acceptance = "Acik miktar musteri beyani ile kabul edildi";
        var (pc, closePrep, _) = await s.PrepareAsync(id, "Close", RecallScenario.ETag(completed), qa2, summary: summary, acceptance: acceptance);
        Assert.True(pc == 200, closePrep.ToString());
        var (cl, closed, _) = await s.CommandAsync(id, "close", qa2, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cl"), ["summary"] = summary, ["openQuantityAcceptance"] = acceptance,
            ["signatureId"] = s.Factory.Signature.Issue(s.Qa2, id, "recall.case.close")
        }, RecallScenario.ETag(closePrep));
        Assert.True(cl == 200, closed.ToString());
        return closed;
    }

    /// <summary>POST /api/inventory/movements (the REAL endpoint) and its answer.</summary>
    public static async Task<(int Status, JsonElement Body)> PostAsync(LotLedgerScenario lots, Dictionary<string, object?> body, string? token = null)
    {
        using var response = await lots.Ledger.PostAsync(body, token: token ?? lots.Token);
        return ((int)response.StatusCode, await lots.Ledger.ReadAsync(response));
    }

    public static Dictionary<string, object?> WithLot(Dictionary<string, object?> body, string lotId, string? sourceModule = null)
    {
        body["lotId"] = lotId;
        if (sourceModule is not null) body["sourceModule"] = sourceModule;
        return body;
    }

    public static string Code(JsonElement error) => error.GetProperty("error").GetProperty("code").GetString()!;

    public static List<BsonDocument> Events(RecallScenario s, string caseId, string kind)
        => s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.CaseEvents)
            .Find(new BsonDocument { ["CaseId"] = CaseIdValue(s, caseId), ["Kind"] = kind }).ToList();

    public static List<BsonDocument> Requests(RecallScenario s, string caseId)
        => s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.QuarantineRequests).Find(new BsonDocument("CaseId", CaseIdValue(s, caseId))).ToList();

    public static List<BsonDocument> Blocks(RecallScenario s, string lotId)
        => s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.LotBlocks).Find(new BsonDocument { ["TenantId"] = s.Tenant.ToString(), ["LotId"] = lotId }).ToList();

    /// <summary>Guid members are stored as strings in this service (the repo's GuidRepresentation convention), measured on the case row.</summary>
    private static BsonValue CaseIdValue(RecallScenario s, string caseId)
    {
        var any = s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.Cases).Find(new BsonDocument("_id", caseId)).FirstOrDefault();
        return any is not null ? caseId : new BsonBinaryData(Guid.Parse(caseId), GuidRepresentation.Standard);
    }

    /// <summary>An ACTIVE block of a fake case, written straight into the collection (the race seam: "the approval committed meanwhile").</summary>
    public static void InsertBlock(RecallScenario s, string lotId, string caseNumber)
        => s.Factory.Database.GetCollection<RecallLotBlock>(RecallCollections.LotBlocks).InsertOne(new RecallLotBlock
        {
            Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, TenantId = s.Tenant, LegalEntityId = s.Ledger.LegalEntity, LotId = lotId, CaseId = Guid.NewGuid(),
            CaseNumber = caseNumber, Active = true, ActivatedAtUtc = DateTimeOffset.UtcNow
        });
}
