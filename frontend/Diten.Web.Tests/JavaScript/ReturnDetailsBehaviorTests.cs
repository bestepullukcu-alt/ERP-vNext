using System.Diagnostics;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

/// <summary>
/// MOD-0186 r10 DL-4 (§35.5): the shipped details.js under Node (CI runs only `dotnet test`). The script runs in a VM with a
/// small fake DOM and a recording fetch: RB-24 / RB-25 customer text never shows an id, RB-5 a stored free-text reason shows as
/// written, RB-10 no holds request without quarantine.holds.read, RB-9 a 404 shows the not-found text.
/// </summary>
public sealed class ReturnDetailsBehaviorTests
{
    private const string Script = """
import vm from 'node:vm';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const source = readFileSync(process.argv[process.argv.length - 1], 'utf8');
const ID = '14900000-0000-4000-8000-000000000001';
const make = () => ({ textContent: '', value: '', hidden: true, style: {}, dataset: {}, children: [], classList: { add() {}, remove() {}, contains: () => false },
  replaceChildren(...n) { this.children = n; }, appendChild(n) { this.children.push(n); return n; }, insertRow() { const r = make(); r.cells = []; r.insertCell = () => { const c = make(); r.cells.push(c); return c; }; this.children.push(r); return r; }, focus() {} });
const run = async (canReadHolds, answers) => {
  const els = new Map();
  const host = make(); host.dataset = { returnId: '66666666-6666-4666-8666-666666666666', legalEntityId: 'le', canReadHolds: String(canReadHolds) };
  els.set('return-details', host);
  for (const id of ['returnDetailsSkeleton', 'returnDetailsError', 'returnDetailsCards', 'detailRmaNumber', 'detailStatus', 'detailShipment', 'detailCustomer', 'detailReason', 'detailCreated', 'detailLines', 'detailOtherHolds', 'detailOtherHoldsList', 'detailHoldsError']) els.set(id, make());
  const calls = [];
  const fetch = async (url) => { calls.push(url); const a = answers.shift(); return { ok: a.status < 400, status: a.status, headers: { get: () => null }, json: async () => a.body }; };
  const ctx = vm.createContext({ document: { getElementById: (id) => els.get(id) ?? null, querySelectorAll: () => [], addEventListener() {}, createElement: () => make(), createTextNode: (t) => ({ textContent: t }), documentElement: { lang: 'en' } },
    window: { L10n: new Proxy({}, { get: (_t, k) => (typeof k === 'string' ? k : undefined) }) }, fetch, Intl, crypto, URLSearchParams });
  const api = vm.runInContext(`${source}\n;ReturnDetails`, ctx);
  await api.init();
  return { api, els, calls };
};
const detail = (customer, reasonCode) => ({ status: 200, body: { returnId: 'r', rmaNumber: 'RMA-1', shipmentId: 's', shipmentNumber: 'SHP-1', status: 'Received', customer, reasonCode, createdAt: '2026-09-22T09:00:00Z',
  lines: [{ shipmentLineNumber: '1', quantity: '2.000', uomId: 'EA', inventoryPosting: 'Posted', sku: { state: 'resolved', label: 'LS-1 · Tablet' }, lot: null }] } });

// RB-24 / RB-25: the customer is a name and code, "old record", "not found" or "name unavailable"; never the id.
{
  const { api } = await run(false, [detail({ state: 'resolved', name: 'Acme', code: 'C-1' }, 'DAMAGED')]);
  assert.equal(api.customerText({ state: 'resolved', name: 'Acme', code: 'C-1' }), 'C-1 · Acme');
  assert.equal(api.customerText({ state: 'oldRecord' }), 'OldRecord');
  assert.equal(api.customerText({ state: 'notFound', reference: ID }), 'NameNotFound');
  assert.equal(api.customerText({ state: 'unavailable', reference: ID }), 'NameUnavailable');
  assert.ok(![api.customerText({ state: 'unavailable', reference: ID })].join('').includes(ID));
  // RB-5: a listed code shows its label; an old free-text reason shows as written.
  assert.equal(api.reasonText('DAMAGED'), 'Reason_DAMAGED');
  assert.equal(api.reasonText('box was wet'), 'box was wet');
}
// RB-10: without quarantine.holds.read only the details request is sent; with it, the holds request follows.
{
  const without = await run(false, [detail({ state: 'oldRecord' }, 'OTHER')]);
  assert.equal(without.calls.length, 1);
  assert.ok(!without.calls.some((u) => u.includes('/holds')));
  assert.equal(without.els.get('detailCustomer').textContent, 'OldRecord');
  const withHolds = await run(true, [detail({ state: 'oldRecord' }, 'OTHER'), { status: 200, body: { lines: [], other: [] } }]);
  assert.equal(withHolds.calls.length, 2);
  assert.ok(withHolds.calls[1].includes('/holds?legalEntityId=le'));
}
// RB-9: a 404 is the not-found text, distinct from the error text; the cards stay hidden.
{
  const missing = await run(false, [{ status: 404, body: { error: { code: 'RETURN_NOT_FOUND' } } }]);
  assert.ok(missing.els.get('returnDetailsError').textContent.startsWith('ErrReturnNotFound'));
  assert.equal(missing.els.get('returnDetailsCards').hidden, true);
  const down = await run(false, [{ status: 503, body: {} }]);
  assert.ok(down.els.get('returnDetailsError').textContent.startsWith('DetailsErrorState'));
}
console.log('ok');
""";

    [Fact]
    public async Task DetailsJs_shows_names_not_ids_gates_the_holds_request_and_separates_not_found_from_error()
    {
        var root = Root();
        var start = new ProcessStartInfo("node") { WorkingDirectory = root, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--input-type=module");
        start.ArgumentList.Add("-");
        start.ArgumentList.Add(Path.Combine(root, "frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/details.js"));
        using var node = Process.Start(start)!;
        await node.StandardInput.WriteAsync(Script);
        node.StandardInput.Close();
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0 && output.Trim() == "ok", output + await error);
    }

    // RB-24: details.js writes values with textContent only; no innerHTML, no title attribute with a value.
    [Fact]
    public void DetailsJs_writes_text_only()
    {
        var script = File.ReadAllText(Path.Combine(Root(), "frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/details.js"));
        Assert.DoesNotContain("innerHTML", script);
        Assert.DoesNotContain(".title", script);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException();
    }
}
