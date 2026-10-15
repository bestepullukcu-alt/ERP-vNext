using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.VisitQrCodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-WEB-a — the institution QR screens on the REAL controller, views, resx and scripts: (1) the pure rules
/// (qr-core.js in Node): the card states a–d, the dialogs' bodies, the history lines, the list query / rows / selection,
/// the label grid; (2) the card script in a fake DOM: a read-only user never gets Print / Regenerate / Revoke, the decoy
/// preview reads no payload; (3) the proxies: read vs manage keys, the print endpoints no-store + an SVG (never the payload,
/// never logged), the list path clamped and escaped; (4) UAS-001, seven languages without echo, the view / script keys,
/// RTL logical properties, the local jsQR (Apache-2.0) and QRCoder (MIT) pinned — no CDN.
/// </summary>
public sealed partial class VisitQrCodesWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string Account = "11111111-1111-1111-1111-111111111111";
    private const string Payload = "DQR1.Cbiv2jobS0C3xfwPjZ0UfQ.3.k1.SECRETSIGNATURExxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    // ═══ 1 · the pure rules (Node) ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A1_the_card_states_follow_the_crm_and_the_buttons_follow_the_manage_key()
    {
        var node = RunNode(Core() + """
            const m = (st, manage) => C.cardModel(st, { read: true, manage });
            const active = { status: 'active', version: 3, issuedBy: '0a0a0a0a-0000-0000-0000-00000000000a', issuedByName: 'Elif Şahin', issuedAt: '2026-10-12T12:18:00Z', lastScannedAt: '2026-10-14T07:42:00Z', recentScanCount: 48, inheritedFrom: { accountId: 'p', accountName: 'Acıbadem', version: 2 } };
            process.stdout.write(JSON.stringify({
              none: m({ status: 'none' }, true), sub: m({ status: 'none', inheritedFrom: { accountId: 'p', accountName: 'Acıbadem Altunizade Hastanesi', version: 2 } }, true),
              active: m(active, true), readonly: m(active, false), revoked: m({ status: 'revoked', version: 3, revokedBy: '0b0b0b0b-0000-0000-0000-00000000000b', revokedByName: 'Murat Demir', revokedAt: '2026-10-13T08:00:00Z', revokeReason: 'kat değişti' }, true),
              unknown: m({ status: 'weird' }, true), empty: m(null, false)
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(("none", "Status_none", false, true), (S(r, "none", "status"), S(r, "none", "chipKey"), B(r, "none", "showHistory"), B(r, "none", "canEdit")));
        Assert.Equal("Acıbadem Altunizade Hastanesi", r.GetProperty("sub").GetProperty("inherited").GetProperty("accountName").GetString()); // Q1a sub-unit
        Assert.Equal(JsonValueKind.Null, r.GetProperty("active").GetProperty("inherited").ValueKind);                         // an active code needs no parent line
        Assert.Equal(("active", true, true, false, 3, 4, 48), (S(r, "active", "status"), B(r, "active", "showHistory"), B(r, "active", "canEdit"), B(r, "active", "readonly"),
            r.GetProperty("active").GetProperty("version").GetInt32(), r.GetProperty("active").GetProperty("nextVersion").GetInt32(), r.GetProperty("active").GetProperty("recent").GetInt32()));
        Assert.Equal((false, true), (B(r, "readonly", "canEdit"), B(r, "readonly", "readonly")));                             // Q1d
        Assert.Equal(("revoked", "Murat Demir", "kat değişti"), (S(r, "revoked", "status"), S(r, "revoked", "revokedBy"), S(r, "revoked", "reason")));
        Assert.Equal("none", S(r, "unknown", "status"));
        Assert.Equal(("none", false), (S(r, "empty", "status"), B(r, "empty", "canEdit")));
    }

    [Fact]
    public void A2_regenerate_and_revoke_send_the_version_seen_and_the_reason_is_required_up_to_500()
    {
        var node = RunNode(Core() + """
            const active = { status: 'active', version: 3 };
            process.stdout.write(JSON.stringify({
              issueNone: C.issueBody({ status: 'none' }, '  '), issueRevoked: C.issueBody({ status: 'revoked', version: 3 }, null),
              regen: C.issueBody(active, '  yıprandı  '), regenLong: C.issueBody(active, 'x'.repeat(600)).reason.length,
              revokeEmpty: C.revokeBody(active, '   '), revokeLong: C.revokeBody(active, 'x'.repeat(501)), revokeOk: C.revokeBody(active, ' taşındı '),
              revokeNotActive: C.revokeBody({ status: 'revoked', version: 3 }, 'neden'),
              rs0: C.reasonState(''), rs500: C.reasonState('y'.repeat(500)), rs501: C.reasonState('y'.repeat(501)),
              toastNew: C.toastKey({ status: 'none' }), toastRegen: C.toastKey(active),
              err409: C.errorKey({ status: 409, body: { errors: ['changed', 'visit_execution_concurrency_conflict'] } }),
              err503: C.errorKey({ status: 503, body: { errors: ['x', 'visit_qr_signing_unavailable'] } }),
              err403: C.errorKey({ status: 403, body: null }), errRaw: C.errorKey({ status: 500, body: { errors: ['boom', 'some_raw_code'] } })
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(JsonValueKind.Null, r.GetProperty("issueNone").GetProperty("expectedVersion").ValueKind);   // generate: no version
        Assert.Equal(JsonValueKind.Null, r.GetProperty("issueNone").GetProperty("reason").ValueKind);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("issueRevoked").GetProperty("expectedVersion").ValueKind);
        Assert.Equal((3, "yıprandı"), (r.GetProperty("regen").GetProperty("expectedVersion").GetInt32(), S(r, "regen", "reason"))); // regenerate: the version seen
        Assert.Equal(500, r.GetProperty("regenLong").GetInt32());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("revokeEmpty").ValueKind);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("revokeLong").ValueKind);
        Assert.Equal(JsonValueKind.Null, r.GetProperty("revokeNotActive").ValueKind);
        Assert.Equal((3, "taşındı"), (r.GetProperty("revokeOk").GetProperty("expectedVersion").GetInt32(), S(r, "revokeOk", "reason")));
        Assert.Equal((false, true, false), (B(r, "rs0", "valid"), B(r, "rs500", "valid"), B(r, "rs501", "valid")));
        Assert.Equal(("ToastIssued", "ToastRegenerated"), (r.GetProperty("toastNew").GetString(), r.GetProperty("toastRegen").GetString()));
        Assert.Equal("Err_visit_execution_concurrency_conflict", r.GetProperty("err409").GetString()); // the 409 version message
        Assert.Equal("Err_visit_qr_signing_unavailable", r.GetProperty("err503").GetString());
        Assert.Equal("Err_permission", r.GetProperty("err403").GetString());
        Assert.Equal("Err_generic", r.GetProperty("errRaw").GetString());                                       // never a raw code
    }

    [Fact]
    public void A3_the_history_lines_carry_the_paper_copies_and_reason()
    {
        var node = RunNode(Core() + """
            process.stdout.write(JSON.stringify([
              C.eventLine({ kind: 'printed', version: 3, paper: 'a4', copies: 2, by: '0a0a0a0a-0000-0000-0000-00000000000a', byName: 'Elif', at: '2026-10-12T12:20:00Z' }),
              C.eventLine({ kind: 'printed', version: 2 }),
              C.eventLine({ kind: 'revoked', version: 2, reason: 'Kabul bankosu taşındı' }),
              C.eventLine({ kind: 'regenerated', version: 2, reason: 'yıprandı' }),
              C.eventLine({ kind: 'issued', version: 1 }), C.eventLine({ kind: 'bulk_issued', version: 1 }),
              C.eventLine({ kind: 'printed', version: 1, paper: 'labels8', copies: 1 }), C.eventLine({ kind: 'printed', version: 1, paper: 'letter' }),
              C.whenKey('2026-10-14T07:00:00Z', Date.parse('2026-10-14T10:00:00Z'), 180), C.whenKey('2026-10-13T07:00:00Z', Date.parse('2026-10-14T10:00:00Z'), 180),
              C.whenKey('2026-10-10T07:00:00Z', Date.parse('2026-10-14T10:00:00Z'), 180)
            ]));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal(("Ev_printed", "Paper_a4", 2, "secondary"), (r[0].GetProperty("key").GetString(), r[0].GetProperty("paperKey").GetString(), r[0].GetProperty("copies").GetInt32(), r[0].GetProperty("tone").GetString()));
        Assert.Equal(JsonValueKind.Null, r[1].GetProperty("paperKey").ValueKind);                                     // an older print: no paper
        Assert.Equal(("Ev_revoked", "Kabul bankosu taşındı", "warning"), (r[2].GetProperty("key").GetString(), r[2].GetProperty("reason").GetString(), r[2].GetProperty("tone").GetString()));
        Assert.Equal(("Ev_regenerated", "primary"), (r[3].GetProperty("key").GetString(), r[3].GetProperty("tone").GetString()));
        Assert.Equal(("Ev_issued", "ok", "Ev_bulk_issued"), (r[4].GetProperty("key").GetString(), r[4].GetProperty("tone").GetString(), r[5].GetProperty("key").GetString()));
        Assert.Equal("Paper_labels8", r[6].GetProperty("paperKey").GetString());
        Assert.Equal(JsonValueKind.Null, r[7].GetProperty("paperKey").ValueKind);                                     // an unknown paper is never shown raw
        Assert.Equal(("Today", "Yesterday"), (r[8].GetString(), r[9].GetString()));
        Assert.Equal(JsonValueKind.Null, r[10].ValueKind);
    }

    [Fact]
    public void A4_the_list_rows_the_label_grid_and_the_print_urls()
    {
        // W3E-FIX (U-1): paging, filters and the selection are the shared list factory's now; the row model stays here
        var node = RunNode(Core() + """
            const lookups = { types: { hospital: 'Hastane', polyclinic: 'Poliklinik' }, cities: { '34': 'İstanbul' } };
            process.stdout.write(JSON.stringify({
              row: C.listRow({ accountId: 'a', accountName: 'Göğüs Hastalıkları Polikliniği', accountType: 'polyclinic', cityRef: '34', status: 'none', parentAccountId: 'p', parentAccountName: 'Acıbadem Altunizade Hastanesi', inheritedFrom: { accountId: 'p', accountName: 'Acıbadem', version: 2 }, recentScanCount: 0 }, lookups),
              rowActive: C.listRow({ accountId: 'b', accountName: 'Acıbadem', accountType: 'hospital', cityRef: '06', status: 'active', version: 2, lastScannedAt: '2026-10-14T06:15:00Z', recentScanCount: 112 }, lookups),
              rowRevoked: C.listRow({ accountId: 'c', accountName: 'Ege', accountType: 'x', status: 'revoked', version: 2, recentScanCount: 0 }, lookups),
              grid4: C.labelGrid('labels4'), grid8: C.labelGrid('labels8'), bulk: C.bulkResultArgs({ issued: 120, skippedActive: 8 }),
              print: C.printUrl('/CRM/VisitQrCodes', 'a b'), labels: C.labelsUrl('/CRM/VisitQrCodes', ['a', 'b']), copies: [C.copiesOf('0'), C.copiesOf('70'), C.copiesOf('x'), C.copiesOf('3')]
            }));
            """);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var row = r.GetProperty("row");
        Assert.Equal(("Acıbadem Altunizade Hastanesi", "Poliklinik", "İstanbul", "Chip_none", true, false), (S(r, "row", "parent"), S(r, "row", "typeLabel"), S(r, "row", "cityLabel"), S(r, "row", "chipKey"), B(r, "row", "inherits"), B(r, "row", "printable")));
        Assert.Equal(JsonValueKind.Null, row.GetProperty("recent").ValueKind);                              // "—" for a code-less row
        Assert.Equal(("06", 112, true, false), (S(r, "rowActive", "cityLabel"), r.GetProperty("rowActive").GetProperty("recent").GetInt32(), B(r, "rowActive", "printable"), B(r, "rowActive", "inherits")));
        Assert.Equal(("Chip_revoked", false), (S(r, "rowRevoked", "chipKey"), B(r, "rowRevoked", "printable")));
        Assert.Equal((2, 4), (r.GetProperty("grid4").GetProperty("rows").GetInt32(), r.GetProperty("grid8").GetProperty("rows").GetInt32()));
        Assert.Equal("[120,8]", r.GetProperty("bulk").GetRawText());
        Assert.Equal("/CRM/VisitQrCodes/Print?accountId=a%20b", r.GetProperty("print").GetString());
        Assert.Equal("/CRM/VisitQrCodes/Print?mode=labels&ids=a%2Cb", r.GetProperty("labels").GetString());
        Assert.Equal("[1,50,1,3]", r.GetProperty("copies").GetRawText());
    }

    // ═══ 2 · the card script in a fake DOM ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A5_a_read_only_user_never_gets_print_regenerate_or_revoke_and_the_preview_reads_no_payload(bool manage)
    {
        var node = RunNode(CardHarness(manage, "{ status: 'active', version: 3, issuedBy: '0a0a0a0a-0000-0000-0000-00000000000a', issuedByName: 'Elif Şahin', issuedAt: '2026-10-12T12:18:00Z', lastScannedAt: null, recentScanCount: 48 }"));
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        var html = r.GetProperty("state").GetString()!;
        Assert.Equal(new[] { "/CRM/VisitQrCodes/api/accounts/" + Account }, r.GetProperty("calls").EnumerateArray().Select(x => x.GetString()).ToArray()); // the state only — never /print
        Assert.Contains("qr-decoy.svg", html);                                                            // D-8: the decoration
        Assert.Contains("Sürüm 3 · Üreten <bdi>Elif Şahin</bdi>", html);
        Assert.Equal(manage, html.Contains("id=\"vq-print\"", StringComparison.Ordinal));                // Print
        Assert.Equal(manage, html.Contains("data-vq=\"regen\"", StringComparison.Ordinal));               // Regenerate
        Assert.Equal(manage, html.Contains("data-vq=\"revoke\"", StringComparison.Ordinal));              // Revoke
        Assert.Equal(!manage, html.Contains("id=\"vq-readonly\"", StringComparison.Ordinal));             // Q1d: "Read only"
        Assert.Equal("vq-chip vq-chip-active", r.GetProperty("chipClass").GetString());
        Assert.True(r.GetProperty("historyShown").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A5_the_none_and_revoked_cards_draw_their_generate_buttons_for_manage_only(bool manage)
    {
        var none = RunNode(CardHarness(manage, "{ status: 'none', inheritedFrom: { accountId: 'p', accountName: 'Acıbadem Altunizade Hastanesi', version: 2 } }"));
        if (none is null) { return; }
        var n = JsonDocument.Parse(none).RootElement;
        Assert.Equal(manage, n.GetProperty("state").GetString()!.Contains("data-vq=\"generate\"", StringComparison.Ordinal));
        Assert.Contains("Üst kurumun (<bdi>Acıbadem Altunizade Hastanesi</bdi>) kodu bu birimde de geçerli.", n.GetProperty("state").GetString());
        Assert.False(n.GetProperty("historyShown").GetBoolean());

        var revoked = JsonDocument.Parse(RunNode(CardHarness(manage, "{ status: 'revoked', version: 3, revokedBy: '0b0b0b0b-0000-0000-0000-00000000000b', revokedByName: 'Murat Demir', revokedAt: '2026-10-13T06:40:00Z', revokeReason: 'Poliklinik kat değiştirdi' }"))!).RootElement;
        var html = revoked.GetProperty("state").GetString()!;
        Assert.Equal(manage, html.Contains("data-vq=\"generate\"", StringComparison.Ordinal));
        Assert.Contains("Gerekçe: <bdi>Poliklinik kat değiştirdi</bdi>", html);
        Assert.Contains("Son sürüm 3 · kurum şu an kodsuz.", html);
    }

    // ═══ 3 · the proxies ═════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A6_reads_need_read_writes_need_manage_and_nothing_else_reaches_the_crm()
    {
        var (gw, read) = Arrange([VisitQrCodesController.Read], answer: "{\"data\":{\"items\":[],\"total\":0}}");
        Assert.Equal(200, Status(await read.Get(Guid.Parse(Account), CancellationToken.None)));
        Assert.Equal(200, Status(await read.History(Guid.Parse(Account), CancellationToken.None)));
        // W3E-FIX (U-1): the list contract (start / length / search / filters) → the CRM's page / pageSize; a filtered list
        // also reads the tenant's whole count (one row)
        Assert.Equal(200, Status(await read.List(100, 100, "Acı & x", null, null, "none", "hospital", "34", null, CancellationToken.None)));
        Assert.Equal(new[] { "/api/crm/visit-qr/accounts/" + Account, "/api/crm/visit-qr/accounts/" + Account + "/history",
            "/api/crm/visit-qr/accounts?status=none&accountType=hospital&city=34&search=Ac%C4%B1%20%26%20x&page=2&pageSize=100",
            "/api/crm/visit-qr/accounts?page=1&pageSize=1" }, gw.Paths());

        // a read-only user: every write / payload is 403 and nothing reaches the gateway
        var (gw2, ro) = Arrange([VisitQrCodesController.Read], "{\"expectedVersion\":3}");
        Assert.Equal(403, Status(await ro.Issue(Guid.Parse(Account), CancellationToken.None)));
        Assert.Equal(403, Status(await ro.Revoke(Guid.Parse(Account), CancellationToken.None)));
        Assert.Equal(403, Status(await ro.BulkIssue(CancellationToken.None)));
        Assert.Equal(403, Status(await ro.PrintOne(Guid.Parse(Account), "a4", 1, CancellationToken.None)));
        Assert.Equal(403, Status(await ro.PrintMany(CancellationToken.None)));
        Assert.Empty(gw2.Requests);

        // no key at all: even the reads
        var (gw3, none) = Arrange([]);
        Assert.Equal(403, Status(await none.Get(Guid.Parse(Account), CancellationToken.None)));
        Assert.Equal(403, Status(await none.Lookups(CancellationToken.None)));
        Assert.Empty(gw3.Requests);

        // manage: the writes go to the CRM paths, the body as it came; a tenantId in the body is refused
        var (gw4, manager) = Arrange([VisitQrCodesController.Manage], "{\"expectedVersion\":3,\"reason\":\"yıprandı\"}");
        Assert.Equal(200, Status(await manager.Issue(Guid.Parse(Account), CancellationToken.None)));
        Assert.Equal(("POST", "/api/crm/visit-qr/accounts/" + Account + "/issue", "{\"expectedVersion\":3,\"reason\":\"yıprandı\"}"), (gw4.Requests[0].Method, gw4.Paths()[0], gw4.Requests[0].Body));
        var (gw5, tenant) = Arrange([VisitQrCodesController.Manage], "{\"accountIds\":[\"" + Account + "\"],\"tenantId\":\"x\"}");
        Assert.Equal(400, Status(await tenant.BulkIssue(CancellationToken.None)));
        Assert.Empty(gw5.Requests);
    }

    [Fact]
    public async Task A7_the_print_endpoints_are_no_store_answer_an_svg_and_never_the_payload_nor_log_it()
    {
        var logs = new ListLogger();
        var single = "{\"data\":{\"accountId\":\"" + Account + "\",\"accountName\":\"Kardiyoloji Polikliniği\",\"accountCode\":\"K1\",\"version\":3,\"payload\":\"" + Payload + "\",\"issuedAt\":\"2026-10-12T12:18:00Z\"}}";
        var (gw, c) = Arrange([VisitQrCodesController.Manage], answer: single, logger: logs);
        var res = await c.PrintOne(Guid.Parse(Account), "A4", 2, CancellationToken.None);
        Assert.Equal(200, Status(res));
        Assert.Equal("no-store", c.Response.Headers.CacheControl.ToString());
        Assert.Equal("/api/crm/visit-qr/accounts/" + Account + "/payload?paper=a4&copies=2", gw.Paths()[0]); // the printed event's paper + copies
        var json = JsonSerializer.Serialize(((OkObjectResult)res).Value);
        Assert.DoesNotContain(Payload, json);                                                             // only the drawing leaves
        Assert.DoesNotContain("SECRETSIGNATURE", json);
        using var answer = JsonDocument.Parse(json);
        Assert.Contains("<svg", answer.RootElement.GetProperty("data").GetProperty("svg").GetString());
        Assert.Equal("Kardiyoloji Polikliniği", answer.RootElement.GetProperty("data").GetProperty("accountName").GetString());

        // bad paper / copies: 400, nothing asked
        var (gw2, bad) = Arrange([VisitQrCodesController.Manage]);
        Assert.Equal(400, Status(await bad.PrintOne(Guid.Parse(Account), "letter", 1, CancellationToken.None)));
        Assert.Equal(400, Status(await bad.PrintOne(Guid.Parse(Account), "a5", 51, CancellationToken.None)));
        Assert.Empty(gw2.Requests);

        // the label sheet: ≤ 200 ids, labels4 | labels8, the CRM gets exactly that body
        var many = "{\"data\":[{\"accountId\":\"" + Account + "\",\"accountName\":\"A\",\"version\":1,\"payload\":\"" + Payload + "\"}]}";
        var (gw3, lab) = Arrange([VisitQrCodesController.Manage], "{\"accountIds\":[\"" + Account + "\"],\"paper\":\"LABELS8\",\"copies\":1}", many, logs);
        var labels = await lab.PrintMany(CancellationToken.None);
        Assert.Equal(200, Status(labels));
        Assert.Equal("no-store", lab.Response.Headers.CacheControl.ToString());
        Assert.Equal("{\"accountIds\":[\"" + Account + "\"],\"paper\":\"labels8\",\"copies\":1}", gw3.Requests[0].Body);
        Assert.DoesNotContain(Payload, JsonSerializer.Serialize(((OkObjectResult)labels).Value));
        var tooMany = "{\"accountIds\":[" + string.Join(",", Enumerable.Range(0, 201).Select(_ => "\"" + Guid.NewGuid() + "\"")) + "],\"paper\":\"labels4\",\"copies\":1}";
        var (gw4, big) = Arrange([VisitQrCodesController.Manage], tooMany);
        Assert.Equal(400, Status(await big.PrintMany(CancellationToken.None)));
        Assert.Empty(gw4.Requests);

        // a failing gateway is logged by method + path only
        var (_, down) = Arrange([VisitQrCodesController.Manage], throwing: true, logger: logs);
        Assert.Equal(502, Status(await down.PrintOne(Guid.Parse(Account), "a5", 1, CancellationToken.None)));
        Assert.NotEmpty(logs.Lines);
        Assert.All(logs.Lines, l => Assert.DoesNotContain("DQR1", l));
    }

    [Fact]
    public void A8_the_svg_is_drawn_here_from_the_payload_and_never_writes_it_as_text()
    {
        var svg = VisitQrSvg.Render(Payload);
        Assert.StartsWith("<svg", svg.TrimStart().Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", string.Empty).TrimStart());
        Assert.Contains("viewBox", svg);
        Assert.DoesNotContain("DQR1", svg);
        Assert.NotEqual(svg, VisitQrSvg.Render(Payload.Replace(".3.", ".4.")));                         // another code, another drawing
        Assert.Equal(svg, VisitQrSvg.Render(Payload));                                                  // a reprint is the same drawing
        var csproj = File.ReadAllText(Path.Combine(WebRoot(), "Diten.Web.csproj"));
        Assert.Contains("<PackageReference Include=\"QRCoder\" Version=\"1.6.0\" />", csproj);           // pinned, MIT
    }

    [Fact]
    public void A9_list_path_ids_and_address_helpers()
    {
        Assert.Equal("/api/crm/visit-qr/accounts?page=1&pageSize=25", VisitQrCodesController.ListPath(null, " ", null, null, null, -3, null));
        Assert.Equal("/api/crm/visit-qr/accounts?territoryId=n%2F1&page=4&pageSize=1", VisitQrCodesController.ListPath(null, null, null, "n/1", null, 4, 0));
        var ids = VisitQrCodesController.ParseIds(string.Join(",", Enumerable.Range(0, 250).Select(_ => Guid.NewGuid())) + ",x,,");
        Assert.Equal(200, ids.Count);
        Assert.Single(VisitQrCodesController.ParseIds(Account + "," + Account.ToUpperInvariant()));
        Assert.Equal("Kısıklı Cad. No:1, Üsküdar / İstanbul", VisitQrCodesController.AddressOf(" Kısıklı Cad. No:1 ", "Üsküdar", "İstanbul"));
        Assert.Equal("İstanbul", VisitQrCodesController.AddressOf(null, " ", "İstanbul"));
        Assert.Null(VisitQrCodesController.AddressOf(null, null, null));
    }

    // ═══ 4 · views, keys, languages ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void A10_uas001_the_pages_and_the_card_draw_nothing_without_their_key()
    {
        var index = View("VisitQrCodes", "Index.cshtml");
        Assert.True(index.IndexOf("@if (!Model.CanRead)", StringComparison.Ordinal) < index.IndexOf("id=\"vq-list-root\"", StringComparison.Ordinal));
        Assert.Contains("<partial name=\"_AccessDenied\"", index);
        Assert.True(index.IndexOf("@if (Model.CanManage)", StringComparison.Ordinal) < index.IndexOf("_BulkActionBar.cshtml", StringComparison.Ordinal)); // no bulk bar without manage
        Assert.Contains("visible: canManage", Script("index.js"));                                         // …and no selection column
        var print = View("VisitQrCodes", "Print.cshtml");
        Assert.True(print.IndexOf("@if (!Model.CanPrint)", StringComparison.Ordinal) < print.IndexOf("id=\"vq-print-root\"", StringComparison.Ordinal));
        var card = View("Accounts", "_VisitQrCard.cshtml");
        Assert.StartsWith("@if (Model.VisitQrRead", card[card.IndexOf("@if", StringComparison.Ordinal)..]);
        var details = View("Accounts", "Details.cshtml");
        Assert.Contains("<partial name=\"~/Views/CRM/Accounts/_VisitQrCard.cshtml\" model=\"Model\" />", details);
        Assert.Contains("@if (Model.VisitQrRead)", details);
        Assert.Equal(("crm.visit-qr.read", "crm.visit-qr.manage"), (VisitQrCodesController.Read, VisitQrCodesController.Manage));
    }

    [Fact]
    public void A11_seven_languages_every_key_filled_no_echo_and_only_declared_keys_used()
    {
        var english = Resx("en");
        foreach (var culture in new[] { "", "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            foreach (var key in VisitQrIndex.ScriptKeys)
            {
                Assert.True(resx.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{culture}:{key}");
                Assert.NotEqual(key, value);
            }
        }

        foreach (var culture in new[] { "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resx = Resx(culture);
            var same = VisitQrIndex.ScriptKeys.Count(k => resx[k] == english[k] && !resx[k].Contains('{') && resx[k].Length > 2);
            Assert.True(same <= 6, $"{culture}: {same} values identical to English");
        }

        Assert.Equal("Kurum QR kodu", Resx("tr")["CardTitle"]);
        Assert.Equal("Seçilenlere kod üret (kodu olmayanlar)", Resx("tr")["BulkGenerate"]);

        // the QR words (Print: Localizer; card / list: QrLocalizer) are VisitQrIndex's; the list page's own (U-1) VisitQrCodesIndex's
        foreach (var (file, marker) in new[] { (View("VisitQrCodes", "Print.cshtml"), "Localizer"), (View("Accounts", "_VisitQrCard.cshtml"), "QrLocalizer"),
                     (View("VisitQrCodes", "Index.cshtml"), "QrLocalizer"), (View("VisitQrCodes", "_IndexL10n.cshtml"), "QrLocalizer") })
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(file, "(?<![A-Za-z])" + marker + "\\[\"([A-Za-z0-9_]+)\"\\]"))
            {
                Assert.Contains(m.Groups[1].Value, VisitQrIndex.ScriptKeys);
            }
        }

        foreach (var js in new[] { "qr-card.js", "qr-print.js" })
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(Script(js), "\\bL\\.([A-Za-z0-9_]+)"))
            {
                Assert.Contains(m.Groups[1].Value, VisitQrIndex.ScriptKeys);
            }
        }

        // the menu page (BE-a manifest CRM_VISIT_QR_CODES) is named in every language
        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var shared = XDocument.Load(Path.Combine(WebRoot(), "Resources", "SharedResource." + culture + ".resx")).Root!.Elements("data")
                .FirstOrDefault(d => (string)d.Attribute("name")! == "Nav.Page.CRMVISITQRCODES");
            Assert.True(shared is not null && !string.IsNullOrWhiteSpace(shared.Element("value")?.Value), culture);
        }
    }

    [Fact]
    public void A12_rtl_logical_sides_local_libraries_only_and_no_cdn()
    {
        var css = File.ReadAllText(Path.Combine(ScriptDir(), "visit-qr.css"));
        foreach (var physical in new[] { "margin-left", "margin-right", "padding-left", "padding-right", " left:", " right:", "text-align: left", "text-align: right" })
        {
            Assert.DoesNotContain(physical, css);
        }

        Assert.Contains("[dir=\"rtl\"] .vq-flip", css);
        var jsqr = Path.Combine(WebRoot(), "wwwroot", "assets", "vendor", "libs", "jsqr");
        Assert.True(File.Exists(Path.Combine(jsqr, "jsQR.js")));
        Assert.Contains("Apache License", File.ReadAllText(Path.Combine(jsqr, "LICENSE")));
        Assert.Contains("jsQR 1.4.0", File.ReadAllText(Path.Combine(jsqr, "VERSION.txt")));
        foreach (var source in new[] { View("VisitQrCodes", "Index.cshtml"), View("VisitQrCodes", "Print.cshtml"), View("Accounts", "_VisitQrCard.cshtml"),
                     Script("qr-card.js"), Script("index.js"), Script("index.l10n.js"), Script("qr-print.js"), Script("qr-core.js") })
        {
            Assert.DoesNotContain("https://", source);
            Assert.DoesNotContain("cdn", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("localStorage", source);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────

    private static string S(JsonElement r, string a, string b) => r.GetProperty(a).GetProperty(b).GetString()!;
    private static bool B(JsonElement r, string a, string b) => r.GetProperty(a).GetProperty(b).GetBoolean();

    private static int? Status(IActionResult result) => result switch
    {
        ContentResult c => c.StatusCode,
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static (Gateway Gateway, VisitQrCodesController Controller) Arrange(string[] permissions, string? body = null, string? answer = null,
        ILogger<VisitQrCodesController>? logger = null, bool throwing = false)
    {
        var gateway = new Gateway(answer ?? "{\"data\":{}}", throwing);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var controller = new VisitQrCodesController(new HttpClient(gateway), configuration, logger ?? new ListLogger());
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
        http.Request.Body = new MemoryStream(bytes);
        http.Request.ContentLength = bytes.Length;
        if (body is not null) http.Request.ContentType = "application/json";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (gateway, controller);
    }

    private sealed class Gateway(string answer, bool throwing) : HttpMessageHandler
    {
        public List<(string Method, string Uri, string? Body)> Requests { get; } = [];

        public List<string> Paths() => Requests.Select(r => r.Uri[GatewayUrl.Length..]).ToList();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (throwing) throw new HttpRequestException("down");
            Requests.Add((request.Method.Method, request.RequestUri!.OriginalString, request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ListLogger : ILogger<VisitQrCodesController>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception) + " " + exception?.Message);
    }

    /// <summary>The REAL qr-card.js in a minimal DOM: the card root, its ids, a fetch that answers the state and records the paths.</summary>
    private static string CardHarness(bool manage, string stateJs)
    {
        var l10n = JsonSerializer.Serialize(Resx("tr").Where(kv => VisitQrIndex.ScriptKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
        return "const C = require('" + Path.Combine(ScriptDir(), "qr-core.js").Replace("\\", "/") + "');\n" + """
            const els = {};
            const mk = id => ({ id, attrs: {}, innerHTML: '', textContent: '', value: '', disabled: false, _cls: new Set(['d-none']),
              classList: { toggle(c, on) { const o = this._o; if (on === undefined ? !o._cls.has(c) : on) o._cls.add(c); else o._cls.delete(c); }, add(c) { this._o._cls.add(c); }, remove(c) { this._o._cls.delete(c); }, contains(c) { return this._o._cls.has(c); } },
              getAttribute(n) { return this.attrs[n] == null ? null : this.attrs[n]; }, setAttribute(n, v) { this.attrs[n] = String(v); },
              addEventListener() {}, set className(v) { this._className = v; }, get className() { return this._className; } });
            const get = id => { if (!els[id]) { els[id] = mk(id); els[id].classList._o = els[id]; } return els[id]; };
            const root = get('vq-card');
            Object.assign(root.attrs, { 'data-api-base': '/CRM/VisitQrCodes/api', 'data-print-base': '/CRM/VisitQrCodes', 'data-account-id': '__ACCOUNT__',
              'data-account-name': 'Kardiyoloji Polikliniği', 'data-can-manage': '__MANAGE__', 'data-decoy': '/assets/js/CRM/VisitQrCodes/qr-decoy.svg' });
            const calls = [];
            global.window = { VisitQrCore: C, VisitQrL10n: __L10N__, VisitPlanningFormat: null,
              fetch: (url) => { calls.push(url); return Promise.resolve({ ok: true, status: 200, text: () => Promise.resolve(JSON.stringify({ data: __STATE__ })) }); } };
            global.document = { getElementById: get, documentElement: { lang: 'tr' } };
            require('__CARD__');
            setTimeout(() => process.stdout.write(JSON.stringify({
              state: get('vq-state').innerHTML, calls, chipClass: get('vq-chip').className, historyShown: !get('vq-history-link').classList.contains('d-none')
            })), 50);
            """.Replace("__ACCOUNT__", Account).Replace("__MANAGE__", manage ? "true" : "false").Replace("__L10N__", l10n)
            .Replace("__STATE__", stateJs).Replace("__CARD__", Path.Combine(ScriptDir(), "qr-card.js").Replace("\\", "/"));
    }

    private static string Core() => "const C = require('" + Path.Combine(ScriptDir(), "qr-core.js").Replace("\\", "/") + "');\n";

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vq-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitQrCodes");

    private static string Script(string file) => File.ReadAllText(Path.Combine(ScriptDir(), file)).Replace("\r\n", "\n");

    private static string View(string folder, string file) => File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", folder, file)).Replace("\r\n", "\n");

    private static Dictionary<string, string> Resx(string culture)
    {
        var file = "VisitQrIndex" + (culture.Length == 0 ? "" : "." + culture) + ".resx";
        return XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitQrCodes", file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
