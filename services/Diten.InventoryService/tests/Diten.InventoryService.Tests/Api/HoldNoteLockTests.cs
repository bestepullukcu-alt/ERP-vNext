using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-MVP1-FUB2 — QUARANTINE 1.2.0 <c>updateHold</c>:
/// <list type="bullet">
/// <item>FU-0175-ITEM-01: a saved change writes a <c>NoteUpdated</c> history row (who, when, <c>noteChange</c> = the note lengths and
/// attachment counts before / after); the <c>inventory_audit</c> row is unchanged.</item>
/// <item>FU-0175-ITEM-03 (GxP): while the hold has a submitted decision (pending approval, or approved and not yet applied) the
/// note is locked — 409 <c>QUARANTINE_DECISION_ALREADY_PENDING</c>, nothing written; the lock lifts when the decision ends.</item>
/// <item>O-ITEM-4: the same key again answers the FIRST result (its version, note and attachments), even after a later edit.</item>
/// </list>
/// </summary>
public sealed class HoldNoteLockTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public HoldNoteLockTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private async Task<(QuarantineScenario S, string HoldId, string ETag)> OpenHoldAsync()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        var (_, hold) = await s.HoldAsync("25", lot);
        var holdId = Id(hold, "holdId");
        return (s, holdId, await ETagAsync(s, holdId));
    }

    private static async Task<string> ETagAsync(QuarantineScenario s, string holdId)
    {
        var (_, _, raw) = await s.SendAsync(HttpMethod.Get, $"/api/quarantine/holds/{holdId}");
        return raw.Headers.ETag!.ToString();
    }

    private List<BsonDocument> Audit(QuarantineScenario s, string holdId)
        => _factory.Collection("inventory_audit")
            .Find(new BsonDocument { ["TenantId"] = s.Tenant.ToString(), ["Action"] = "QUARANTINE_HOLD_UPDATED", ["HoldId"] = holdId }).ToList();

    private static List<JsonElement> NoteRows(JsonElement detail)
        => detail.GetProperty("history").EnumerateArray().Where(h => Id(h, "action") == "NoteUpdated").ToList();

    [Fact]
    public async Task A_note_change_writes_one_NoteUpdated_history_row_with_who_when_and_the_change_summary()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        var key = QuarantineScenario.Key("upd");
        var saved = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}",
            new { analysisNote = "pH 6,8", attachments = new[] { new { name = "COA.pdf", dmsRef = "DMS-77" } }, idempotencyKey = key }, 200, key, etag);

        var row = Assert.Single(NoteRows(saved));
        Assert.Equal(s.Ledger.User.ToString("D"), Id(row.GetProperty("by"), "userId"));
        Assert.True(DateTimeOffset.UtcNow - row.GetProperty("atUtc").GetDateTimeOffset() < TimeSpan.FromMinutes(5));
        var change = row.GetProperty("noteChange");
        Assert.Equal(0, change.GetProperty("previousNoteLength").GetInt32());
        Assert.Equal(6, change.GetProperty("noteLength").GetInt32());
        Assert.Equal(0, change.GetProperty("previousAttachmentCount").GetInt32());
        Assert.Equal(1, change.GetProperty("attachmentCount").GetInt32());
        Assert.Single(Audit(s, holdId)); // the audit row stays as it was

        // a second change: one more row, the first one untouched; the detail read shows both
        var k2 = QuarantineScenario.Key("upd");
        await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "", idempotencyKey = k2 }, 200, k2, await ETagAsync(s, holdId));
        var rows = NoteRows(await s.GetHoldAsync(holdId));
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.GetProperty("noteChange").GetProperty("previousNoteLength").GetInt32() == 6
                                   && r.GetProperty("noteChange").GetProperty("noteLength").GetInt32() == 0);
    }

    [Fact]
    public async Task An_unchanged_body_writes_no_history_row()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        var key = QuarantineScenario.Key("upd");
        var same = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { attachments = Array.Empty<object>(), idempotencyKey = key }, 200, key, etag);
        Assert.Empty(NoteRows(same));
        Assert.Empty(Audit(s, holdId));
    }

    [Fact]
    public async Task While_a_submitted_decision_waits_for_approval_the_note_is_locked_and_nothing_is_written()
    {
        var (s, holdId, _) = await OpenHoldAsync();
        var decision = await s.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 25m)]);
        await s.SubmitAsync(Id(decision, "decisionId"));
        var etag = await ETagAsync(s, holdId);

        var key = QuarantineScenario.Key("upd");
        var refused = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "onay beklerken", idempotencyKey = key }, 409, key, etag);
        Assert.Equal("QUARANTINE_DECISION_ALREADY_PENDING", QuarantineScenario.Code(refused));
        Assert.Empty(Audit(s, holdId));
        var hold = await s.GetHoldAsync(holdId);
        Assert.False(hold.TryGetProperty("analysisNote", out var note) && note.ValueKind == JsonValueKind.String);
        Assert.Empty(NoteRows(hold));
    }

    [Fact]
    public async Task A_draft_decision_does_not_lock_the_note()
    {
        var (s, holdId, _) = await OpenHoldAsync();
        await s.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 25m)]);
        var key = QuarantineScenario.Key("upd");
        await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "taslak varken", idempotencyKey = key }, 200, key, await ETagAsync(s, holdId));
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public async Task When_the_decision_ends_without_applying_the_note_is_editable_again(string outcome)
    {
        var (s, holdId, _) = await OpenHoldAsync();
        var decision = await s.DecideAsync(holdId, [QuarantineScenario.Line(1, "Release", 25m)]);
        await s.SubmitAsync(Id(decision, "decisionId"));
        await s.OutcomeAsync(Id(decision, "decisionId"), outcome);

        var key = QuarantineScenario.Key("upd");
        var saved = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "karar sonrası", idempotencyKey = key }, 200, key, await ETagAsync(s, holdId));
        Assert.Equal("karar sonrası", Id(saved, "analysisNote"));
        Assert.Single(NoteRows(saved));
    }

    [Fact]
    public async Task The_same_key_answers_the_first_result_even_after_a_later_edit_and_a_different_body_is_400()
    {
        var (s, holdId, etag) = await OpenHoldAsync();
        var k1 = QuarantineScenario.Key("upd");
        var body1 = new { analysisNote = "birinci", attachments = new[] { new { name = "a.pdf", dmsRef = "DMS-1" } }, idempotencyKey = k1 };
        var first = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", body1, 200, k1, etag);

        var k2 = QuarantineScenario.Key("upd");
        var second = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}",
            new { analysisNote = "ikinci", attachments = Array.Empty<object>(), idempotencyKey = k2 }, 200, k2, await ETagAsync(s, holdId));
        Assert.Equal(first.GetProperty("version").GetInt32() + 1, second.GetProperty("version").GetInt32());

        var (status, again, raw) = await s.SendAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", body1, k1, etag);
        Assert.True(status == 200, $"{status} {again}");
        Assert.Equal(first.GetProperty("version").GetInt32(), again.GetProperty("version").GetInt32());
        Assert.Equal("birinci", Id(again, "analysisNote"));
        Assert.Equal("DMS-1", Id(again.GetProperty("attachments")[0], "dmsRef"));
        Assert.Equal($"W/\"{first.GetProperty("version").GetInt32()}\"", raw.Headers.ETag!.ToString());
        Assert.Equal(2, Audit(s, holdId).Count); // the replay wrote nothing
        Assert.Equal("ikinci", Id(await s.GetHoldAsync(holdId), "analysisNote"));

        var reused = await s.ExpectAsync(HttpMethod.Patch, $"/api/quarantine/holds/{holdId}", new { analysisNote = "başka", idempotencyKey = k1 }, 400, k1, etag);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", QuarantineScenario.Code(reused));
    }
}
