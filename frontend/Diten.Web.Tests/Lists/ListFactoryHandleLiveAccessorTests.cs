using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// The list factory's handle (`DitenDataTable.createList`) on the PRODUCTION diten-datatable.js, run in Node with a
/// hand-made browser stand-in (no jQuery, no jsdom: the vitest suite cannot start on Node 16 here, and the sibling
/// tests/list-factory-server-mode.test.js 4c/4d/6 hold the same claims for when it can).
///
/// The defect (measured live 2026-10-15 on /CRM/VisitEvidenceFlags): the handle was built with
/// <c>Object.assign(handle, { get appliedFilters() { … } })</c>. Object.assign copies a getter's VALUE once, so
/// <c>handle.appliedFilters</c> stayed the initial filters after every Apply / Reset while the request already carried
/// the new ones. Sabotage proof: on the pre-fix file the "after Apply" read is <c>{ priority: "", status: "" }</c> while
/// the query says <c>priority=70&amp;status=Passive</c> — this test reads both, so it fails on that file.
/// </summary>
public sealed class ListFactoryHandleLiveAccessorTests
{
    [Fact]
    public void AppliedFilters_follows_apply_and_reset_and_lastResponse_is_the_last_envelope()
    {
        var node = RunNode(Harness("[]"));
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;

        Assert.Equal("function", r.GetProperty("accessor").GetString()); // an accessor on the handle, not a copied value
        Assert.Equal("", r.GetProperty("initial").GetProperty("priority").GetString());
        Assert.Equal("start=0&length=10&draw=2&priority=70&status=Passive", r.GetProperty("query").GetString());
        Assert.Equal("70", r.GetProperty("applied").GetProperty("priority").GetString());   // the same filters the query sent
        Assert.Equal("Passive", r.GetProperty("applied").GetProperty("status").GetString());
        Assert.Equal("", r.GetProperty("reset").GetProperty("priority").GetString());
        Assert.Equal("", r.GetProperty("reset").GetProperty("status").GetString());
        Assert.Equal(2, r.GetProperty("lastTotal").GetInt32()); // dataFilter writes handle.lastResponse on every envelope
    }

    [Fact]
    public void AppliedFilters_starts_as_the_saved_view_then_follows_apply()
    {
        var node = RunNode(Harness("""[{"id":"v","isDefault":true,"viewDefinition":{"filters":{"priority":"30"}}}]"""));
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;

        Assert.Equal("30", r.GetProperty("initial").GetProperty("priority").GetString());
        Assert.Equal("70", r.GetProperty("applied").GetProperty("priority").GetString());
    }

    private static string Harness(string savedViews) => """
        const fs = require('fs'); const vm = require('vm');
        const SRC = '__SRC__';
        const el = (id) => { const h = {}; return { id, value: '', dataset: {}, classList: { add() {}, remove() {}, toggle() {}, contains: () => false },
          addEventListener: (e, f) => { (h[e] = h[e] || []).push(f); }, click() { (h.click || []).forEach(f => f({ preventDefault() {} })); },
          setAttribute() {}, getAttribute: () => null, querySelector: () => null, querySelectorAll: () => [], closest: () => null }; };
        const els = { filterPriority: el('filterPriority'), filterStatus: el('filterStatus'), btnFilterApply: el('btnFilterApply'), btnFilterReset: el('btnFilterReset'), inlineFilterCollapse: el('inlineFilterCollapse') };
        const document = { getElementById: id => els[id] || null, querySelector: () => null, querySelectorAll: () => [], addEventListener() {}, body: el('body') };
        let captured = null;
        const api = { column: () => ({ visible: () => true }), order: () => [[2, 'asc']], search: () => '', draw() {}, page() { return api; },
          columns: { adjust() {} }, table: () => ({ container: () => null }), on() { return api; }, row: () => ({ data: () => null }) };
        const window = { document, console, setTimeout, clearTimeout, localStorage: { getItem: () => null }, sessionStorage: { getItem: () => null },
          DtDefaults: { create: c => { captured = c; return c; }, exportButtons: () => [], updateVisualState() {}, refreshButtonGroupRadii() {} },
          personalizationClient: { getViews: async () => __VIEWS__ } };
        window.window = window;
        Object.assign(window, { requestAnimationFrame: f => f(), DataTable: function () { Object.assign(this, api); },
          bootstrap: { Collapse: { getOrCreateInstance: () => ({ toggle() {}, hide() {} }) }, Offcanvas: { getOrCreateInstance: () => ({}) }, Modal: { getInstance: () => null } } });
        vm.runInNewContext(fs.readFileSync(SRC, 'utf8'), window, { filename: SRC });
        (async () => {
          const list = await window.DitenDataTable.createList({ tableEl: el('dt'), dataMode: 'server', ajax: { url: '/api/x', type: 'GET' },
            filters: { fields: [{ id: 'filterPriority', key: 'priority' }, { id: 'filterStatus', key: 'status' }] },
            savedView: { moduleKey: 'M', pageKey: 'P', saveViewColumnIndexes: [2], defaultVisibleColumnIndexes: [2], baseOrder: [[2, 'asc']] },
            config: { columns: [{ data: 'id' }, { data: 'id' }, { data: 'code' }] } });
          const snap = () => JSON.parse(JSON.stringify(list.appliedFilters));
          const out = { initial: snap(), accessor: typeof Object.getOwnPropertyDescriptor(list, 'appliedFilters').get };
          captured.initComplete.call({ api: () => api }, {}, {});
          await new Promise(r => setTimeout(r, 5));
          els.filterPriority.value = '70'; els.filterStatus.value = 'Passive';
          els.btnFilterApply.click();
          out.applied = snap();
          out.query = captured.ajax.data({ draw: 2, start: 0, length: 10, search: { value: '' }, order: [] });
          els.btnFilterReset.click();
          out.reset = snap();
          const env = n => JSON.stringify({ isSuccessful: true, statusCode: 200, data: { items: [], total: n, filteredTotal: n } });
          captured.ajax.dataFilter.call({ url: '/api/x?draw=1' }, env(1));
          captured.ajax.dataFilter.call({ url: '/api/x?draw=2' }, env(2));
          out.lastTotal = list.lastResponse.data.total;
          process.stdout.write(JSON.stringify(out));
        })().catch(e => { console.error(e); process.exit(1); });
        """.Replace("__SRC__", Path.Combine(Root(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "diten-datatable.js").Replace("\\", "/"))
           .Replace("__VIEWS__", savedViews);

    /// <summary>Runs <paramref name="code"/> in Node; null when Node is not on the machine.</summary>
    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "lf-" + Guid.NewGuid().ToString("N") + ".js");
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

    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
