using System.Reflection;
using System.Text.Json;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Tests.CentralAudit;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using Diten.CrmService.Persistence.Repositories.Commercial;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-CRM-SYNC-S0B B0a (F8-b, CT decision D-1) — MOD-0168 §21 rows 851 / 852 / 854 / 861: the "Before/After" values live in
/// the module's OWN history (the order's event payload, the settings change history), because the central audit record
/// names the entity and never copies what changed (AUD-001 §3). On CRM's real pipeline (<see cref="PreOrderWorld"/>).
/// </summary>
public sealed class PreOrderB0aHistoryTests : PreOrderTestBase
{
    [Fact]
    public async Task B0a_1_an_update_writes_the_old_and_new_value_of_exactly_the_fields_that_changed()
    {
        var draft = await Draft();                                   // TUT q=20 mf=10+2 d=5, depot Selçuk, note "Kasım siparişi", no date
        var date = W.Today.AddDays(3);

        var updated = Ok(await W.Send(new UpdatePreOrderCommand(draft.Id,
            new PreOrderInput(PreOrderWorld.Pharmacy, PreOrderWorld.Depot, date, "Kasım siparişi", null, [Line("TUT", 30, (10, 2), 5m), Line("ALM", 10)]))));

        var payload = updated.Events.Single(e => e.Type == PreOrderEventTypes.Updated).Payload;
        Assert.Equal(new[] { "date.new", "date.old", "lines.new", "lines.old" }, payload.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("LS-TUT q=20 mf=10+2 d=5", payload["lines.old"]);
        Assert.Equal("LS-TUT q=30 mf=10+2 d=5; LS-ALM q=10", payload["lines.new"]);
        Assert.Null(payload["date.old"]);
        Assert.Equal(date.ToString("yyyy-MM-dd"), payload["date.new"]);

        // depot + note: old → new; the lines and the date are now unchanged and stay out
        var again = Ok(await W.Send(new UpdatePreOrderCommand(draft.Id,
            new PreOrderInput(PreOrderWorld.Pharmacy, PreOrderWorld.DepotNoEmail, date, "Aralık siparişi", null, [Line("TUT", 30, (10, 2), 5m), Line("ALM", 10)]))));
        var second = again.Events.Last(e => e.Type == PreOrderEventTypes.Updated).Payload;
        Assert.Equal(new[] { "depot.new", "depot.old", "note.new", "note.old" }, second.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal((PreOrderWorld.Depot.ToString("D"), PreOrderWorld.DepotNoEmail.ToString("D")), (second["depot.old"], second["depot.new"]));
        Assert.Equal(("Kasım siparişi", "Aralık siparişi"), (second["note.old"], second["note.new"]));

        // the first update's row is still there, unchanged (append only)
        Assert.Equal("LS-TUT q=20 mf=10+2 d=5", W.Stored(draft.Id).Events.First(e => e.Type == PreOrderEventTypes.Updated).Payload["lines.old"]);
    }

    [Fact]
    public async Task B0a_2_an_update_that_changes_nothing_writes_an_empty_payload()
    {
        var draft = await Draft();

        var same = Ok(await W.Send(new UpdatePreOrderCommand(draft.Id, Input())));

        Assert.Empty(same.Events.Single(e => e.Type == PreOrderEventTypes.Updated).Payload);
    }

    [Fact]
    public async Task B0a_3_a_cleared_signature_leaves_its_content_id_and_signer_name_in_the_event_never_the_image()
    {
        // (a) the rep changes the signed lines
        var draft = await Draft();
        var signed = await Sign(draft.Id);
        var changed = Ok(await W.Send(new UpdatePreOrderCommand(draft.Id, Input(lines: Line("TUT", 40)))));

        Assert.Null(changed.Signature);
        var cleared = changed.Events.Single(e => e.Type == PreOrderEventTypes.SignatureCleared).Payload;
        Assert.Equal(new[] { "binaryRef", "signerName" }, cleared.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal((signed.Signature!.BinaryRef.ToString("D"), "Ecz. Ali Veli"), (cleared["binaryRef"], cleared["signerName"]));
        Assert.DoesNotContain(Png, JsonSerializer.Serialize(W.Stored(draft.Id).Events), StringComparison.Ordinal);

        // (b) the approver asks for changes (D2)
        var pending = await Pending();
        var signatureBefore = W.Stored(pending.Id).Signature!;
        var back = Ok(await Decide(pending.Id, PreOrderDecisions.RequestChanges, "MF'yi 10+2 yapın"));
        var byDecision = back.Events.Single(e => e.Type == PreOrderEventTypes.SignatureCleared).Payload;
        Assert.Equal((signatureBefore.BinaryRef.ToString("D"), signatureBefore.SignerName), (byDecision["binaryRef"], byDecision["signerName"]));
    }

    [Fact]
    public async Task B0a_3b_the_created_event_keeps_the_first_lines_and_depot_and_no_event_carries_an_email_address()
    {
        var draft = await Draft();
        Ok(await W.Send(new UpdatePreOrderCommand(draft.Id, Input(depot: PreOrderWorld.DepotNoEmail, lines: Line("ALM", 10)))));

        var stored = W.Stored(draft.Id);
        var created = stored.Events.Single(e => e.Type == PreOrderEventTypes.Created).Payload;
        Assert.Equal(new[] { "code", "depot", "lines", "pharmacy" }, created.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal((draft.Code, PreOrderWorld.Pharmacy.ToString("D"), PreOrderWorld.Depot.ToString("D"), "LS-TUT q=20 mf=10+2 d=5"),
            (created["code"], created["pharmacy"], created["depot"], created["lines"]));
        Assert.DoesNotContain("@", JsonSerializer.Serialize(stored.Events), StringComparison.Ordinal); // §21: no e-mail address
    }

    [Fact]
    public async Task B0a_4_a_settings_update_appends_one_row_with_the_old_and_new_values_the_actor_and_the_time_in_its_tenant_only()
    {
        Assert.Empty(W.SettingsHistory.Store);
        W.Clock.Now = new DateTimeOffset(2026, 11, 13, 10, 30, 0, TimeSpan.Zero);

        var saved = Ok(await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput("auto-within-limits", 24,
            [new PreOrderCountryTierInput("tr", "depot")], null, null, 1)), PreOrderWorld.Ops));

        var row = Assert.Single(W.SettingsHistory.Store);
        Assert.Equal((PreOrderWorld.Tenant, saved.Version, PreOrderWorld.Ops.ToString("D"), W.Clock.Now), (row.TenantId, row.SettingsVersion, row.ByUserId, row.At));
        Assert.Equal(new[]
            {
                ("approvalMode", (string?)"always", (string?)"auto-within-limits"),
                ("approvalLinkTtlHours", "48", "24"),
                ("priceTierByCountry", null, "TR=depot")
            },
            row.Changes.Select(c => (c.Field, c.Old, c.New)));

        // a second save adds a second row; the first one is untouched
        Ok(await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput(null, null, null, 3, null)), PreOrderWorld.Ops));
        Assert.Equal(2, W.SettingsHistory.Store.Count);
        Assert.Equal(("transmissionRetryMax", "5", "3"), W.SettingsHistory.Store[1].Changes.Select(c => (c.Field, c.Old, c.New)).Single());
        Assert.Equal(3, W.SettingsHistory.Store[0].Changes.Count);

        // a refused save (stale version, invalid input) writes no row
        var stale = await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput(null, 30, null, null, null, 1)), PreOrderWorld.Ops);
        var invalid = await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput(null, null, null, 11, null)), PreOrderWorld.Ops);
        Assert.Equal((409, 400), (stale.StatusCode, invalid.StatusCode));
        Assert.Equal(2, W.SettingsHistory.Store.Count);

        // the other tenant reads none of it
        var history = W.Get<IPreOrderSettingsHistoryRepository>();
        Assert.Equal(2, (await history.ListAsync(PreOrderWorld.Tenant, 50, default)).Count);
        Assert.Empty(await history.ListAsync(PreOrderWorld.OtherTenant, 50, default));
    }

    [Fact]
    public void B0a_5_the_settings_history_has_no_update_and_no_delete_path()
    {
        string[] Members(Type type) => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "AppendAsync", "ListAsync" }, Members(typeof(IPreOrderSettingsHistoryRepository)));
        Assert.Equal(new[] { "AppendAsync", "ListAsync" }, Members(typeof(PreOrderSettingsHistoryRepository)));
        // the settings store itself gained no way to reach the history
        Assert.Equal(new[] { "GetAsync", "SaveAsync" }, Members(typeof(IPreOrderSettingsRepository)));
    }
}

/// <summary>
/// B0a on a TEST-OWNED mongod (<see cref="DisposableCrmMongod"/>: fresh port, temp dbpath, never 27017; no silent skip):
/// the tenant filter and the order live IN THE REPOSITORY — a fake cannot see them removed.
/// </summary>
public sealed class PreOrderSettingsHistoryMongoTests : IDisposable
{
    private readonly DisposableCrmMongod _mongod = new();

    public void Dispose() => _mongod.Dispose();

    [Fact]
    public async Task B0a_4m_the_history_is_read_per_tenant_newest_first_and_served_by_its_index()
    {
        Persistence.DependencyInjection.EnsureClassMapsForTests();
        var database = new MongoClient(_mongod.ConnectionString).GetDatabase("crm_pre_order_settings_history_tests");
        typeof(Persistence.DependencyInjection).GetMethod("TryEnsureIndexes", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [database]);
        var history = new PreOrderSettingsHistoryRepository(database);
        var (tenant, other) = (Guid.NewGuid(), Guid.NewGuid());
        var at = new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

        PreOrderSettingsChange Row(Guid owner, int version, int minutes) => new()
        {
            TenantId = owner, SettingsVersion = version, ByUserId = PreOrderWorld.Ops.ToString("D"), At = at.AddMinutes(minutes),
            Changes = [new PreOrderSettingsFieldChange { Field = "approvalLinkTtlHours", Old = "48", New = "24" }]
        };

        await history.AppendAsync(Row(tenant, 2, 0), default);
        await history.AppendAsync(Row(other, 7, 5), default);
        await history.AppendAsync(Row(tenant, 3, 10), default);

        var own = await history.ListAsync(tenant, 50, default);
        var foreign = await history.ListAsync(other, 50, default);

        Assert.Equal(new[] { 3, 2 }, own.Select(r => r.SettingsVersion));
        Assert.All(own, r => Assert.Equal(tenant, r.TenantId));
        Assert.Equal(7, Assert.Single(foreign).SettingsVersion);
        Assert.Empty(await history.ListAsync(Guid.NewGuid(), 50, default));
        var change = Assert.Single(own[0].Changes);
        Assert.Equal(("approvalLinkTtlHours", "48", "24"), (change.Field, change.Old, change.New));

        var indexes = await (await database.GetCollection<MongoDB.Bson.BsonDocument>(PreOrderSettingsHistoryRepository.CollectionName).Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, i => i["name"] == "ix_commercial_pre_order_settings_history_tenant_at");
    }
}
