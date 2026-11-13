using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.VisitEvidenceSettings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3L · W3-WEB-e — the visit evidence "scoped settings" on the REAL controller, view, resx and scripts: (1) the scope
/// tree from GET scopes, the layers' names from the MDM legal entities / MOD-0048 business units (never the code where a
/// name is known); (2) a layer's form — every field "from above (source)" or "changed in this scope", "back to the setting
/// above" sends the field as NULL (the PUT carries every field); (3) add (an empty scope, nothing written until its first
/// save) and remove (the confirm, DELETE at the version, chord-marked); (4) the save confirm "applies to N reps"; (5) the
/// effective card's source by name and the chain note; (6) the reps' scope chip and the cells over the layers; (7) the
/// history's scope filter and column (the export too); (8) every proxy → its CRM path with its keys; (9) seven languages,
/// UAS-001, the help text. vitest does not start on this machine (Node 16): the scripts run in Node through xUnit.
/// </summary>
public sealed class VwW3LScopeWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string PolicyRead = "crm.visit-evidence-policy.read";
    private const string PolicyManage = "crm.visit-evidence-policy.manage";
    private const string Le = "aaaaaaaa-1111-2222-3333-444444444444";
    private const string Le2 = "bbbbbbbb-1111-2222-3333-444444444444";
    private const string Policy = "/api/crm/visit-evidence-policy";

    // ═══ 1 · the tree from the API, the names from the lists ════════════════════════════════════════════════

    [Fact]
    public void L1_the_tree_draws_the_layers_of_GET_scopes_by_name_with_their_meta_and_add_only_with_the_manage_key()
    {
        var o = Smoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        Assert.Equal(JsonValueKind.Null, r.GetProperty("createError").ValueKind);
        var tree = Plain(r, "tree");
        // the legal entity first, then the business unit (each a node of GET scopes), by NAME
        Assert.True(tree.IndexOf("data-scope=\"legal-entity:" + Le + "\"", StringComparison.Ordinal) < tree.IndexOf("data-scope=\"business-unit:HOSP\"", StringComparison.Ordinal));
        Assert.Contains("<bdi>Diten İlaç A.Ş.</bdi>", tree);
        Assert.Contains("<bdi>Hastane Ekibi</bdi>", tree);
        Assert.Contains("2 alan farklı · 5 temsilci", tree);
        Assert.Contains("1 alan farklı · 2 temsilci", tree);
        Assert.Contains("data-add=\"legal-entity\"", tree);
        Assert.Contains("data-add=\"business-unit\"", tree);
        Assert.DoesNotContain(">HOSP<", tree);
        Assert.DoesNotContain(">" + Le + "<", tree);

        var ro = Smoke("readonly");
        if (ro is null) { return; }
        Assert.DoesNotContain("data-add=", Plain(ro.Value, "tree"));                       // read-only: nothing to add
        Assert.DoesNotContain("data-take=", Plain(ro.Value, "leGroups"));                 // …nor to change in a scope
        Assert.DoesNotContain("data-drop=", Plain(ro.Value, "leGroups"));
        Assert.False(ro.Value.GetProperty("removeShown").GetBoolean());
        Assert.Equal(0, ro.Value.GetProperty("writes").GetInt32());
    }

    // ═══ 2 · a layer's form: inheritance labels, "back to the setting above" = null ═════════════════════════

    [Fact]
    public void L2_a_legal_entity_form_labels_every_field_and_back_to_the_setting_above_sends_null()
    {
        var o = Smoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        Assert.Equal("<bdi>Diten İlaç A.Ş.</bdi>", Plain(r, "leName"));
        Assert.Equal("Tüzel kişilik", Plain(r, "leType"));
        Assert.Equal("5 temsilci · 2 alan farklı", Plain(r, "leMeta"));
        Assert.True(r.GetProperty("leInheritShown").GetBoolean() && r.GetProperty("removeShown").GetBoolean());
        var groups = Plain(r, "leGroups") + Plain(r, "leExtra");
        // the overridden fields (photo, radius): "Changed in this scope" + the value above + "Back to the setting above"
        Assert.Equal(2, Regex.Matches(groups, "Bu kapsamda değiştirildi").Count);
        Assert.Contains("Üst ayar: 300 m", groups);
        Assert.Contains("data-drop=\"radiusMeters\"", groups);
        Assert.Contains("data-drop=\"photoMode\"", groups);
        // every other field (8): "From above (Kiracı): value" + "Change in this scope"; faded and not editable
        Assert.Equal(8, Regex.Matches(groups, "Üstten gelir \\(Kiracı\\): ").Count);
        Assert.Contains("Üstten gelir (Kiracı): Zorunlu", groups);
        Assert.Contains("data-take=\"locationMode\"", groups);
        Assert.Equal(8, Regex.Matches(groups, "ve-inherited").Count);
        Assert.Contains("data-field=\"locationMode\" data-opt=\"required\" disabled", groups);
        // the preview takes the scope's effective values (the layer's photo "required")
        Assert.Equal("· zorunlu", Plain(r, "pvPhoto"));

        // the PUT: EVERY field, the dropped radius and every inherited one as null (never ""), the taken switch its value
        Assert.Equal("/api/policy/scopes/legal-entity/" + Le, Plain(r, "putUrl"));
        var put = r.GetProperty("put");
        Assert.Equal(11, put.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.Null, put.GetProperty("radiusMeters").ValueKind);
        Assert.Equal(JsonValueKind.Null, put.GetProperty("locationMode").ValueKind);
        Assert.Equal(JsonValueKind.Null, put.GetProperty("qrMode").ValueKind);
        Assert.Equal("required", put.GetProperty("photoMode").GetString());
        Assert.False(put.GetProperty("doctorConfirmation").GetBoolean());
        Assert.Equal(3, put.GetProperty("expectedVersion").GetInt32());
    }

    [Fact]
    public void L2b_the_pure_put_body_sends_an_inherited_field_as_null_and_the_changes_carry_what_is_inherited()
    {
        var node = RunNode(Core() + """
            const ov = C.layerOverrides({ fields: [{ field: 'photoMode', value: 'required' }, { field: 'radiusMeters', value: '500' }, { field: 'qrMode', value: null }] });
            const base = { locationMode: 'required', radiusMeters: 300, maxAccuracyMeters: 150, photoMode: 'optional', doctorConfirmation: true, durationConsistency: true, shortVisitSeconds: 120, accountWithoutLocation: 'allow_flagged', qrMode: 'off', antiFraudMode: 'off' };
            const draft = Object.assign({}, ov, { radiusMeters: null, doctorConfirmation: false, antiFraudMode: 'block' });
            process.stdout.write(JSON.stringify({
              body: C.layerPutBody(draft, 3), fresh: C.layerPutBody(C.layerOverrides(null), 0),
              changes: C.layerChanges(ov, draft, base), effective: C.layerEffective(base, ov), count: C.overriddenCount(ov),
              errors: Object.keys(C.layerFieldErrors({ radiusMeters: 9 }, base)), inheritedErrors: Object.keys(C.layerFieldErrors({}, Object.assign({}, base, { maxAccuracyMeters: 5 })))
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var body = r.GetProperty("body");
        Assert.Equal(JsonValueKind.Null, body.GetProperty("radiusMeters").ValueKind);                // "back to the setting above"
        Assert.Equal(JsonValueKind.Null, body.GetProperty("antiFraudMode").ValueKind);               // "block" never leaves the page
        Assert.Equal(JsonValueKind.Null, body.GetProperty("maxAccuracyMeters").ValueKind);
        Assert.Equal(("required", false, 3), (body.GetProperty("photoMode").GetString(), body.GetProperty("doctorConfirmation").GetBoolean(), body.GetProperty("expectedVersion").GetInt32()));
        Assert.All(r.GetProperty("fresh").EnumerateObject(), p => Assert.Equal(JsonValueKind.Null, p.Value.ValueKind)); // a new scope: no version, nothing set
        Assert.Equal(11, r.GetProperty("fresh").EnumerateObject().Count());
        var changes = r.GetProperty("changes").EnumerateArray().ToList();
        Assert.Equal(new[] { "radiusMeters", "doctorConfirmation", "antiFraudMode" }, changes.Select(c => c.GetProperty("field").GetString()));
        Assert.True(changes[0].GetProperty("to").GetProperty("inherited").GetBoolean());
        Assert.Equal(300, changes[0].GetProperty("to").GetProperty("value").GetInt32());              // inherits the tenant's 300
        Assert.True(changes[1].GetProperty("from").GetProperty("inherited").GetBoolean());
        Assert.Equal("required", r.GetProperty("effective").GetProperty("photoMode").GetString());
        Assert.Equal("off", r.GetProperty("effective").GetProperty("qrMode").GetString());
        Assert.Equal(2, r.GetProperty("count").GetInt32());
        Assert.Equal(new[] { "radiusMeters" }, r.GetProperty("errors").EnumerateArray().Select(x => x.GetString()));
        Assert.Empty(r.GetProperty("inheritedErrors").EnumerateArray());                              // an inherited value is the scope above's
    }

    // ═══ 3 · add / remove ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void L3_add_opens_an_empty_scope_written_only_on_save_and_remove_confirms_then_deletes_at_the_version()
    {
        var o = Smoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        Assert.True(r.GetProperty("addShown").GetBoolean());
        Assert.Equal("Tüzel kişilik ayarı ekle", Plain(r, "addTitle"));
        var options = Plain(r, "addOptions");
        Assert.Contains("Diten Uzbekistan LLC", options);
        Assert.DoesNotContain("Diten İlaç A.Ş.", options);                                           // already a layer
        Assert.True(r.GetProperty("addConfirmEnabled").GetBoolean());
        Assert.Equal(0, r.GetProperty("putsAfterPick").GetInt32());                                  // the pick writes nothing
        Assert.Equal("<bdi>Diten Uzbekistan LLC</bdi>", Plain(r, "newName"));
        Assert.Contains("data-scope=\"legal-entity:" + Le2 + "\"", Plain(r, "newTree"));
        Assert.Contains("Henüz kaydedilmedi", Plain(r, "newTree"));
        Assert.StartsWith("Yeni kapsam boş başlar", Plain(r, "newNote"));
        Assert.False(r.GetProperty("newSaveEnabledEmpty").GetBoolean());                             // an empty layer cannot be saved
        Assert.StartsWith("Kaydetmek için bu kapsamda en az bir alanı değiştirin", Plain(r, "newEmptyHint"));
        Assert.True(r.GetProperty("newSaveEnabled").GetBoolean());
        Assert.Equal("Kapsam oluşturulur ve bu kapsama düşen temsilcilere hemen uygulanır.", Plain(r, "newSaveText"));
        Assert.Equal("/api/policy/scopes/legal-entity/" + Le2, Plain(r, "newPutUrl"));
        Assert.Equal(JsonValueKind.Null, r.GetProperty("newPut").GetProperty("expectedVersion").ValueKind);
        Assert.Equal("required", r.GetProperty("newPut").GetProperty("photoMode").GetString());

        Assert.True(r.GetProperty("rmShown").GetBoolean());
        Assert.Equal("Bu kapsamdaki 5 temsilci hemen üst ayara döner. Geçmiş kapsamı saklar.", Plain(r, "rmText"));
        Assert.Equal("/api/policy/scopes/legal-entity/" + Le + "?expectedVersion=3", Plain(r, "deleteUrl"));
        Assert.Contains("Kapsam kaldırıldı; temsilciler üst ayara döndü.", r.GetProperty("toasts").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal("<bdi>Kiracı varsayılanı</bdi>", Plain(r, "selAfterRemove"));                  // back on the tenant
    }

    // ═══ 4 · the save confirm ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void L4_the_save_confirm_names_the_scopes_reps_and_shows_from_above_on_both_sides()
    {
        var o = Smoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        Assert.True(r.GetProperty("saveEnabled").GetBoolean() && r.GetProperty("saveModalShown").GetBoolean());
        Assert.Equal("Değişiklik hemen bu kapsamdaki 5 temsilciye uygulanır.", Plain(r, "saveText"));   // the layer's 5, not the tenant's 148
        var diff = Plain(r, "saveDiff");
        Assert.Contains("<s class=\"ve-mu\">500 m</s>", diff);
        Assert.Contains("<strong>Üst ayara döner (300 m)</strong>", diff);
        Assert.Contains("<s class=\"ve-mu\">Üstten: Açık</s>", diff);
        Assert.Contains("<strong>Kapalı</strong>", diff);
        Assert.Contains("Ayarlar kaydedildi; 5 temsilciye uygulandı.", r.GetProperty("toasts").EnumerateArray().Select(t => t.GetString()!.Replace("⁨", "").Replace("⁩", "")));
    }

    // ═══ 5 · the effective card: the source by name, the chain note ═════════════════════════════════════════

    [Fact]
    public void L5_the_effective_card_names_each_source_and_says_why_a_layer_was_skipped()
    {
        var o = Smoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        var rows = Plain(r, "effRows");
        Assert.Contains("<td>500 m</td><td><span class=\"ve-pill ve-tone-info\">Tüzel kişilik: Diten İlaç A.Ş.</span></td>", rows);
        Assert.Contains("<td>Konum kontrolü</td><td>Zorunlu</td><td><span class=\"ve-pill ve-tone-secondary\">Kiracı</span></td>", rows);
        Assert.DoesNotContain(Le, rows);                                                             // never the code
        Assert.DoesNotContain("NaN", rows);
        Assert.Contains("<bdi>Diten İlaç A.Ş.</bdi>", Plain(r, "effChain"));
        Assert.Contains("Birden çok iş birimi — iş birimi katmanı atlandı", Plain(r, "effNotes"));
        Assert.True(r.GetProperty("effChainShown").GetBoolean());

        var node = RunNode(Core() + """
            const names = C.scopeNames([{ ref: 'AAAAAAAA-1111-2222-3333-444444444444', name: 'Diten İlaç A.Ş.' }], [{ ref: 'HOSP', name: 'Hastane Ekibi' }]);
            process.stdout.write(JSON.stringify({
              le: C.sourceOf('legal-entity:aaaaaaaa-1111-2222-3333-444444444444', names, {}),
              bu: C.sourceOf('business-unit:HOSP', names, {}), unknown: C.sourceOf('business-unit:KAM', names, {}),
              tenant: C.sourceOf('tenant', names, {}), exception: C.sourceOf('exception', names, {}),
              notes: [C.chainNotes({ legalEntityNote: 'no-seat', businessUnitNote: 'bu-ambiguous' }), C.chainNotes({ legalEntityNote: 'platform-unavailable', businessUnitNote: 'no-territory' }),
                      C.chainNotes({ note: 'no-business-unit' }), C.chainNotes({ legalEntityNote: 'no-layer', businessUnitNote: 'resolved', note: 'resolved' })]
            }));
            """);
        if (node is null) { return; }
        var s = JsonDocument.Parse(node).RootElement;
        Assert.Equal(("SourceLegalEntity", "Diten İlaç A.Ş.", "info"), (s.GetProperty("le").GetProperty("key").GetString(), s.GetProperty("le").GetProperty("args")[0].GetString(), s.GetProperty("le").GetProperty("tone").GetString()));
        Assert.Equal(("SourceBusinessUnit", "Hastane Ekibi", "primary"), (s.GetProperty("bu").GetProperty("key").GetString(), s.GetProperty("bu").GetProperty("args")[0].GetString(), s.GetProperty("bu").GetProperty("tone").GetString()));
        Assert.Equal("KAM", s.GetProperty("unknown").GetProperty("args")[0].GetString());              // no list knows it: its code, never a guess
        Assert.Equal("SourceTenant", s.GetProperty("tenant").GetProperty("key").GetString());
        Assert.Equal("SourceExceptionUntil", s.GetProperty("exception").GetProperty("key").GetString());
        Assert.Equal("ChainNoSeat,ChainBuAmbiguous|ChainPlatformDown,ChainNoTerritory|ChainNoBusinessUnit|",
            string.Join("|", s.GetProperty("notes").EnumerateArray().Select(a => string.Join(",", a.EnumerateArray().Select(x => x.GetString())))));
    }

    // ═══ 6 · the reps: the scope chip, the cells over the layers ═══════════════════════════════════════════

    [Fact]
    public void L6_each_rep_row_carries_the_applied_scope_chip_and_its_cells_take_the_layers()
    {
        var o = Smoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        Assert.Equal("<span class=\"ve-row ve-wrap\"><span class=\"ve-pill ve-tone-info\"><bdi>Diten İlaç A.Ş.</bdi></span> <span class=\"ve-pill ve-tone-primary\"><bdi>Hastane Ekibi</bdi></span></span>", Plain(r, "repChip"));
        var lone = Plain(r, "loneChip");
        Assert.Contains("<span class=\"ve-pill ve-tone-secondary\"><bdi>Kiracı</bdi></span>", lone);
        Assert.Contains("Pozisyon ataması yok — tüzel kişilik katmanı atlandı · Birden çok iş birimi — iş birimi katmanı atlandı", lone);
        Assert.Equal("Zorunlu", Plain(r, "repPhoto"));                                               // the legal entity's override
        Assert.Equal("Zorunlu", Plain(r, "repQr"));                                                  // the business unit's override
        Assert.Equal("İsteğe bağlı", Plain(r, "lonePhoto"));                                         // no layer: the tenant's

        var tables = Script("settings-tables.js");
        Assert.Contains("{ data: 'resourceId', name: 'scope' }", tables);
        Assert.Contains("const DATA_COLUMNS = [1, 2, 3, 4, 5, 6];", tables);
    }

    // ═══ 7 · the history: the scope filter and column ══════════════════════════════════════════════════════

    [Fact]
    public async Task L7_the_history_filters_by_scope_shows_its_column_by_name_and_the_proxy_and_export_carry_it()
    {
        var o = Smoke("manage");
        if (o is null) { return; }
        var r = o.Value;
        var options = Plain(r, "historyOptions");
        Assert.Contains("<option value=\"\">Tüm kapsamlar</option>", options);
        Assert.Contains("<option value=\"tenant\" selected>Kiracı varsayılanı</option>", options);
        Assert.Contains("<option value=\"legal-entity:" + Le + "\">Diten İlaç A.Ş.</option>", options);
        Assert.Contains("<option value=\"business-unit:HOSP\">Hastane Ekibi</option>", options);
        Assert.Equal("{\"scope\":\"legal-entity\",\"scopeRef\":\"" + Le + "\"}", r.GetProperty("historyQuerySelected").GetRawText()); // follows the selection
        Assert.Equal("{}", r.GetProperty("historyQueryAll").GetRawText());
        Assert.Equal("<span class=\"ve-pill ve-tone-primary\"><bdi>Hastane Ekibi</bdi></span>", Plain(r, "historyScopeCell"));
        Assert.EndsWith("<strong>Üstten</strong>", Plain(r, "historyChangeCleared"));
        Assert.EndsWith("<strong>Kapsam kaldırıldı</strong>", Plain(r, "historyChangeArchived"));

        // the proxy: the filter → the CRM's ?scope=&scopeRef=; a scope that is none → 400 before any call
        var (g1, c1) = Arrange([PolicyRead], _ => "{\"data\":{\"items\":[],\"total\":0}}", "?scope=legal-entity&scopeRef=" + Le.ToUpperInvariant());
        Assert.IsType<OkObjectResult>(await c1.History(0, 10, null, null, null, CancellationToken.None));
        Assert.Equal(GatewayUrl + Policy + "/history?page=1&pageSize=10&scope=legal-entity&scopeRef=" + Le.ToUpperInvariant(), Assert.Single(g1.Requests).Uri);
        var (g2, c2) = Arrange([PolicyRead], _ => "{\"data\":{\"items\":[],\"total\":0}}", "?scope=tenant");
        Assert.IsType<OkObjectResult>(await c2.History(0, 10, null, null, null, CancellationToken.None));
        Assert.EndsWith("&scope=tenant", Assert.Single(g2.Requests).Uri);
        foreach (var bad in new[] { "?scope=country", "?scope=legal-entity&scopeRef=not-a-guid", "?scope=business-unit" })
        {
            var (g, c) = Arrange([PolicyRead], _ => "{}", bad);
            Assert.Equal(400, Status(await c.History(0, 10, null, null, null, CancellationToken.None)));
            Assert.Empty(g.Requests);
        }

        // the export: the same filter, the scope column by NAME (the two lists), a layer's empty side "from above"
        string Reply(string path) => path.Contains("/legal-entities/lookup", StringComparison.Ordinal)
            ? "{\"data\":[{\"legalEntityId\":\"" + Le + "\",\"code\":\"TR01\",\"displayName\":\"Diten İlaç A.Ş.\"}]}"
            : path.Contains("consumable-sets/business-unit", StringComparison.Ordinal)
                ? "{\"data\":{\"setCode\":\"business-unit\",\"items\":[{\"code\":\"HOSP\",\"label\":\"Hastane Ekibi\",\"isActive\":true}]}}"
                : "{\"data\":{\"total\":3,\"items\":["
                  + "{\"at\":\"2026-10-15T08:00:00Z\",\"by\":\"u2\",\"scope\":\"legal-entity\",\"scopeRef\":\"" + Le + "\",\"field\":\"radiusMeters\",\"oldValue\":\"500\",\"newValue\":null,\"kind\":\"cleared\"},"
                  + "{\"at\":\"2026-10-15T08:00:00Z\",\"by\":\"u2\",\"scope\":\"business-unit\",\"scopeRef\":\"HOSP\",\"field\":null,\"oldValue\":null,\"newValue\":null,\"kind\":\"archived\"},"
                  + "{\"at\":\"2026-10-15T08:00:00Z\",\"by\":\"u2\",\"scope\":\"tenant\",\"field\":\"photoMode\",\"oldValue\":\"optional\",\"newValue\":\"required\",\"kind\":\"set\"}]}}";
        var (g3, c3) = Arrange([PolicyRead], Reply, "?scope=legal-entity&scopeRef=" + Le, "tr");
        var file = Assert.IsType<FileContentResult>(await c3.HistoryExport("csv", ["at,scope,field,newValue"], CancellationToken.None));
        Assert.Contains(GatewayUrl + Policy + "/history?page=1&pageSize=200&scope=legal-entity&scopeRef=" + Le, g3.Requests.Select(x => x.Uri));
        var csv = Encoding.UTF8.GetString(file.FileContents).TrimStart((char)0xFEFF).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var tr = Resx("tr");
        Assert.Equal(string.Join(",", tr["ColDate"], tr["ColScope"], tr["ColField"], tr["ColChange"]), csv[0]);
        Assert.Contains(",Diten İlaç A.Ş.," + tr["F_radiusMeters"] + "," + tr["UnitMeters"].Replace("{0}", "500") + " → " + tr["HistInherited"], csv[1]);
        Assert.Contains(",Hastane Ekibi,—,— → " + tr["HistArchived"], csv[2]);
        Assert.Contains("," + tr["TenantDefault"] + "," + tr["F_photoMode"] + ",", csv[3]);
        Assert.DoesNotContain(Le, string.Join("\n", csv));
        Assert.DoesNotContain(",HOSP,", string.Join("\n", csv));
    }

    // ═══ 8 · the proxies ═══════════════════════════════════════════════════════════════════════════════════

    public static TheoryData<string, string, string, string[]> ScopeProxyMap => new()
    {
        { "ListScopes", "GET", Policy + "/scopes", new[] { PolicyRead } },
        { "ListScopes", "GET", Policy + "/scopes", new[] { PolicyManage } },
        { "GetScope", "GET", Policy + "/scopes/legal-entity/" + Le, new[] { PolicyRead } },
        { "GetScopeBu", "GET", Policy + "/scopes/business-unit/HOSP%20TEAM", new[] { PolicyManage } },
        { "PutScope", "PUT", Policy + "/scopes/legal-entity/" + Le, new[] { PolicyManage } },
        { "RemoveScope", "DELETE", Policy + "/scopes/business-unit/HOSP?expectedVersion=4", new[] { PolicyManage } }
    };

    [Theory]
    [MemberData(nameof(ScopeProxyMap))]
    public async Task L8_each_scope_proxy_reaches_its_crm_path_only_with_its_key(string action, string verb, string path, string[] keys)
    {
        var (gateway, controller) = Arrange(keys, _ => "{\"data\":{}}");
        Assert.Equal(200, Status(await Invoke(controller, action)));
        var sent = Assert.Single(gateway.Requests);
        Assert.Equal((verb, GatewayUrl + path, TenantId.ToString()), (sent.Method, sent.Uri, sent.Tenant));

        var others = new[] { PolicyRead, PolicyManage, "crm.visit-evidence-exception.read", "crm.visit-evidence-exception.manage" }
            .Where(k => !keys.Contains(k) && !(action is "ListScopes" or "GetScope" or "GetScopeBu" && k is PolicyRead or PolicyManage)).ToArray();
        var (none, refused) = Arrange(others, _ => "{}");
        Assert.Equal(403, Status(await Invoke(refused, action)));
        Assert.Empty(none.Requests);
    }

    [Fact]
    public async Task L8b_a_scope_key_that_is_not_a_layer_is_refused_before_any_call_and_a_tenant_body_too()
    {
        foreach (var (type, scopeRef) in new[] { ("tenant", "x"), ("country", "tr"), ("legal-entity", "not-a-guid"), ("legal-entity", Guid.Empty.ToString()), ("business-unit", new string('x', 101)), ("business-unit", " ") })
        {
            var (g, c) = Arrange([PolicyManage], _ => "{}");
            Assert.Equal(400, Status(await c.PutScope(type, scopeRef, CancellationToken.None)));
            Assert.Equal(400, Status(await c.RemoveScope(type, scopeRef, 1, CancellationToken.None)));
            Assert.Equal(400, Status(await c.GetScope(type, scopeRef, CancellationToken.None)));
            Assert.Empty(g.Requests);
        }

        Assert.Equal(Policy + "/scopes/legal-entity/" + Le, VisitEvidenceSettingsController.ScopePath("LEGAL-ENTITY", Le.ToUpperInvariant()));
        var (g2, c2) = Arrange([PolicyManage], _ => "{}", body: "{\"tenantId\":\"x\",\"photoMode\":\"required\"}");
        Assert.Equal(400, Status(await c2.PutScope("legal-entity", Le, CancellationToken.None)));
        Assert.Empty(g2.Requests);
    }

    [Fact]
    public async Task L8c_the_scope_option_lists_name_by_name_drop_what_cannot_be_picked_and_say_when_unreadable()
    {
        var le = VisitEvidenceSettingsController.LegalEntityOptionsOf(
            "{\"data\":[{\"legalEntityId\":\"" + Le + "\",\"code\":\"TR01\",\"displayName\":\"Diten İlaç A.Ş.\",\"legalName\":\"Diten İlaç Sanayi A.Ş.\"},"
            + "{\"legalEntityId\":\"" + Le2 + "\",\"code\":\"UZ01\",\"legalName\":\"Diten Uzbekistan LLC\"},"
            + "{\"legalEntityId\":\"cccccccc-1111-2222-3333-444444444444\",\"code\":\"OLD\",\"displayName\":\"Kapalı\",\"referenceable\":false},"
            + "{\"legalEntityId\":\"x\",\"displayName\":\"bozuk\"}]}")!;
        Assert.Equal(new[] { (Le, "Diten İlaç A.Ş."), (Le2, "Diten Uzbekistan LLC") }, le.Select(x => (x.Ref, x.Name)));
        var bu = VisitEvidenceSettingsController.BusinessUnitOptionsOf(
            "{\"data\":{\"setCode\":\"business-unit\",\"items\":[{\"code\":\"HOSP\",\"label\":\"Hastane Ekibi\",\"isActive\":true},{\"code\":\"OLD\",\"label\":\"Eski\",\"isActive\":false},{\"code\":\"KAM\",\"label\":\"\"}]}}")!;
        Assert.Equal(new[] { ("HOSP", "Hastane Ekibi"), ("KAM", "KAM") }, bu.Select(x => (x.Ref, x.Name)));
        Assert.Null(VisitEvidenceSettingsController.LegalEntityOptionsOf("<html>"));

        // MDM refusing: available = false and no items (the page names a layer by its code then, never by a guess)
        var (g, c) = Arrange([PolicyRead], _ => "{}", status: HttpStatusCode.Forbidden);
        var ok = Assert.IsType<OkObjectResult>(await c.LegalEntityOptions(CancellationToken.None));
        Assert.Equal("{\"data\":{\"available\":false,\"items\":[]}}", JsonSerializer.Serialize(ok.Value));
        Assert.Equal(GatewayUrl + "/api/legal-entities/lookup", Assert.Single(g.Requests).Uri);
        var (g2, c2) = Arrange(["crm.visit-evidence-exception.read"], _ => "{}");
        Assert.Equal(403, Status(await c2.LegalEntityOptions(CancellationToken.None)));
        Assert.Equal(403, Status(await c2.BusinessUnitOptions(CancellationToken.None)));
        Assert.Empty(g2.Requests);
        var (g3, c3) = Arrange([PolicyManage], _ => "{\"data\":{\"items\":[{\"code\":\"HOSP\",\"label\":\"Hastane Ekibi\"}]}}");
        var bus = Assert.IsType<OkObjectResult>(await c3.BusinessUnitOptions(CancellationToken.None));
        Assert.Equal("{\"data\":{\"available\":true,\"items\":[{\"Ref\":\"HOSP\",\"Name\":\"Hastane Ekibi\"}]}}", JsonSerializer.Serialize(bus.Value));
        Assert.Contains("consumable-sets/business-unit", Assert.Single(g3.Requests).Uri);
    }

    // ═══ 9 · the view: marks, UAS-001, the theme, seven languages, the help ═════════════════════════════════

    [Theory]
    [InlineData("ve-rm-confirm", "chord")]   // "Remove the scope": not irreversible (archived) but at once in effect for every rep of the scope
    [InlineData("ve-add-confirm", "enter")]  // the pick only opens an empty scope on the page
    public void L9_the_remove_confirm_is_chord_marked_and_the_add_confirm_enter(string id, string mark)
    {
        var button = Regex.Match(View(), "<button\\b[^>]*\\bid=\"" + id + "\"[^>]*>");
        Assert.True(button.Success, id);
        Assert.Equal(mark, Regex.Match(button.Value, "data-dt-primary=\"([^\"]*)\"").Groups[1].Value);
    }

    [Fact]
    public void L9b_without_a_policy_read_key_no_tree_and_the_page_keeps_the_layout_colour_and_the_shared_files()
    {
        var view = View();
        var stop = view.IndexOf("return;", view.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal), StringComparison.Ordinal);
        foreach (var drawn in new[] { "id=\"ve-tree-layers\"", "id=\"ve-rm-modal\"", "id=\"ve-add-modal\"", "id=\"ve-history-scope\"" })
        {
            Assert.True(view.IndexOf(drawn, StringComparison.Ordinal) > stop, drawn + " must come after the access notice's return");
        }

        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-evidence-settings.css"));
        var w3l = css[css.IndexOf("WP-VW-W3L", StringComparison.Ordinal)..];
        Assert.DoesNotMatch("#[0-9a-fA-F]{3,6}\\b|rgba?\\(", w3l);                                 // only the page's tokens (--bs-primary)
        foreach (var physical in new[] { "margin-left", "margin-right", "padding-left", "padding-right", " left:", " right:" }) Assert.DoesNotContain(physical, css);
        var js = Script("visit-evidence-settings.js");
        Assert.DoesNotContain("localStorage", js);
        Assert.DoesNotContain("keydown", js);                                                        // the shared shortcut layer only
        Assert.Equal(1, Regex.Matches(js, "'chord'").Count);                                         // U6 stands: the marks live in the view
        Assert.DoesNotContain("VisitEvidenceSettings", File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "diten-datatable.js")));
    }

    [Fact]
    public void L9c_every_new_word_is_in_seven_languages_translated_and_the_help_names_the_screen_words()
    {
        var english = Resx("en");
        var added = new[] { "TypeLegalEntity", "TypeBusinessUnit", "Diff", "NoDiff", "RepsAtLeast", "AddBusinessUnit", "AddTitleLegalEntity", "AddTitleBusinessUnit",
            "AddDescription", "AddConfirm", "AddNone", "AddUnavailable", "NewScopeBadge", "NewScopeNote", "InheritNote", "InheritNoneNote", "InheritBuNote",
            "InheritedFrom", "ChangeHere", "OverriddenHere", "UpperValue", "BackToUpperLink", "LayerEmpty", "SaveTextNew", "ScopeCreated", "DiffInherited",
            "DiffBackToUpper", "ScopeRemove", "RemoveTitle", "RemoveText", "Removed", "SourceLegalEntity", "SourceBusinessUnit", "ChainNoSeat",
            "ChainPlatformDown", "ChainNoTerritory", "ChainNoBusinessUnit", "ChainBuAmbiguous", "HistAllScopes", "HistScopeFilter", "ColScope",
            "HistArchived", "HistScopeCreated", "HistInherited" };
        Assert.All(added, k => Assert.Contains(k, VisitEvidenceSettingsIndex.ScriptKeys.Concat(VisitEvidenceSettingsIndex.ViewKeys)));
        foreach (var culture in new[] { "", "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            foreach (var key in added)
            {
                Assert.True(resx.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v), culture + ":" + key);
                Assert.Equal(Regex.Matches(english[key], "\\{\\d\\}").Count, Regex.Matches(v!, "\\{\\d\\}").Count);
                if (culture is not ("" or "en")) Assert.NotEqual(english[key], v);
            }
            Assert.False(resx.ContainsKey("AddLegalEntityHint") || resx.ContainsKey("ScopeUnresolved"), culture);  // the stand-ins are gone
        }
        Assert.Equal("Pozisyon ataması yok — tüzel kişilik katmanı atlandı", Resx("tr")["ChainNoSeat"]);
        Assert.All(new[] { "ChangeHere", "ChainBuAmbiguous", "RemoveText", "SourceBusinessUnit" }, k => Assert.Matches("[؀-ۿ]", Resx("ar")[k]));

        // the help text speaks of the layers in every language, quoting the screen's own words (U2 measures every quote)
        foreach (var language in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var help = XDocument.Load(Path.Combine(WebRoot(), "Resources", "PageHelpResource." + language + ".resx")).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");
            var page = Resx(language);
            Assert.Contains(page["AddLegalEntity"], help["Help.VISITEVIDENCESETTINGS.Step1"]);
            Assert.Contains(page["AddBusinessUnit"], help["Help.VISITEVIDENCESETTINGS.Step1"]);
            Assert.Contains(page["ChangeHere"], help["Help.VISITEVIDENCESETTINGS.Step2"]);
            Assert.Contains(page["BackToUpperLink"], help["Help.VISITEVIDENCESETTINGS.Step2"]);
            Assert.Contains(page["ScopeRemove"], help["Help.VISITEVIDENCESETTINGS.Step3"]);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static string Plain(JsonElement o, string name) => (o.GetProperty(name).GetString() ?? string.Empty).Replace("⁨", "").Replace("⁩", "");

    private JsonElement? Smoke(string mode)
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-evidence-settings-w3l-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var tr = Resx("tr");
        var texts = Path.Combine(Path.GetTempPath(), "ve-w3l-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(texts, JsonSerializer.Serialize(VisitEvidenceSettingsIndex.ScriptKeys.ToDictionary(k => k, k => tr[k])), new UTF8Encoding(false));
        try
        {
            var node = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = '" + texts.Replace("\\", "/") + "'; process.argv[4] = '" + mode + "'; require('" + smoke + "');");
            return node is null ? null : JsonDocument.Parse(node).RootElement;
        }
        finally
        {
            File.Delete(texts);
        }
    }

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static Task<IActionResult> Invoke(VisitEvidenceSettingsController c, string action) => action switch
    {
        "ListScopes" => c.ListScopes(CancellationToken.None),
        "GetScope" => c.GetScope("legal-entity", Le.ToUpperInvariant(), CancellationToken.None),
        "GetScopeBu" => c.GetScope("business-unit", "HOSP TEAM", CancellationToken.None),
        "PutScope" => c.PutScope("legal-entity", Le, CancellationToken.None),
        "RemoveScope" => c.RemoveScope("business-unit", "HOSP", 4, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static (Gateway Gateway, VisitEvidenceSettingsController Controller) Arrange(
        string[] permissions, Func<string, string> reply, string query = "", string? language = null, string body = "{\"photoMode\":\"required\"}",
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var gateway = new Gateway(reply, status);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitEvidenceSettingsController(new HttpClient(gateway), configuration, NullLogger<VisitEvidenceSettingsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "user-1") };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = services.BuildServiceProvider() };
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        http.Request.QueryString = new QueryString(query);
        if (language is not null) http.Request.Headers.AcceptLanguage = language;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private static string Core() => "const C = require('" + Path.Combine(ScriptDir(), "evidence-core.js").Replace("\\", "/") + "');\n";

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine (the code assertions stand alone).</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "ve-w3l-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitEvidenceSettings");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View() => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "VisitEvidenceSettings", "Index.cshtml")).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitEvidenceSettingsIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitEvidenceSettings", file)).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(Func<string, string> reply, HttpStatusCode status) : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Tenant)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.OriginalString;
            Requests.Add((request.Method.Method, uri, request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null));
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(reply(uri), Encoding.UTF8, "application/json") });
        }
    }
}
