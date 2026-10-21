// WP-MVP1-FIX-QUI1 — the Bloke Stok page, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Quarantine/blocked.js       (the page: unblock dialog, "Stoğu bloke et" form, list hooks)
//   frontend/Diten.Web/Views/Quarantine/_UnblockModal.cshtml         (the dialog body template)
//   frontend/Diten.Web/Views/Shared/_GlobalConfirmation.cshtml       (the dialog's body rule: subtext = TEXT, subtextHtml = markup)
//   frontend/Diten.Web/Resources/Views/Quarantine/Quarantine.tr.resx (the words)
// The DOM is a small recording fake: what is measured is what the page hands to the dialog, the form fields and the list.
// QuarantineBlockedNodeTests.cs runs this file from `dotnet test`.
//   node --test frontend/Diten.Web.Tests/JavaScript/Node/quarantine-blocked.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const read = (p) => readFileSync(path.join(repo, p), 'utf8');
const web = 'frontend/Diten.Web/';

// ── the words (tr) ────────────────────────────────────────────────────────────────────────────────────────────────
const decodeXml = (s) => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');
const tr = Object.fromEntries([...read(`${web}Resources/Views/Quarantine/Quarantine.tr.resx`)
    .matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)].map((m) => [m[1], decodeXml(m[2])]));

// ── the dialog's body rule, read from the one dialog implementation ─────────────────────────────────────────────────
const confirmView = read(`${web}Views/Shared/_GlobalConfirmation.cshtml`);
const BODY_RULE = "const body = hasFormBody ? String(options.subtextHtml) : (subtext ? text(subtext) : '');";
const ditenText = (value) => String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
/** The markup the dialog builds for its description (the rule above), and what the reader SEES of it. */
const dialogBody = (options) => {
    const hasFormBody = options.subtextHtml !== undefined && options.subtextHtml !== null;
    return hasFormBody ? String(options.subtextHtml) : (options.subtext ? ditenText(options.subtext) : '');
};
const visible = (html) => decodeXml(html.replace(/<[^>]*>/g, ' ')).replace(/\s+/g, ' ').trim();

// ── the dialog body template, from the view; textContent / outerHTML as a browser serializes them ──────────────────
const template = /<template id="qr-unblock-template">([\s\S]*?)<\/template>/.exec(read(`${web}Views/Quarantine/_UnblockModal.cshtml`))[1].trim()
    .replace(/@Localizer\["([^"]+)"\]/g, (m, key) => ditenText(tr[key]));
const serializeText = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/ /g, '&nbsp;');
const unblockBody = () => {
    let subject = '';
    return {
        querySelector: (selector) => (selector === '[data-ub-subject]' ? { set textContent(value) { subject = String(value); } } : null),
        get outerHTML() { return template.replace(/(<div[^>]*data-ub-subject[^>]*>)(<\/div>)/, (m, open, close) => open + serializeText(subject) + close); }
    };
};

// ── a recording page ──────────────────────────────────────────────────────────────────────────────────────────────
const element = (id) => ({
    id, value: '', textContent: '', innerHTML: '', disabled: false, dataset: {}, options: [], listeners: {},
    classList: { set: new Set(), add(c) { this.set.add(c); }, remove(c) { this.set.delete(c); }, toggle(c, on) { ((on === undefined ? !this.set.has(c) : on) ? this.set.add(c) : this.set.delete(c)); }, contains(c) { return this.set.has(c); } },
    addEventListener(type, fn) { (this.listeners[type] ||= []).push(fn); },
    async fire(type, event) { for (const fn of this.listeners[type] || []) await fn(event); },
    add(option) { this.options.push(option); },
    reset() {},
    querySelector: () => null
});

const STOCK = [{ itemId: 'i1', skuId: 's1', skuLevel: 'Lsku', warehouseId: 'w1', locationId: 'l1', lotId: 'lot-1', warehouse: 'D01', location: 'A-01', onHand: 200, baseUomId: 'EA' }];

const page = async ({ openCount = 2 } = {}) => {
    const els = {};
    const byId = (id) => (els[id] ||= element(id));
    byId('qr-unblock-template').content = { firstElementChild: { cloneNode: () => unblockBody() } };
    const seen = { confirms: [], writes: [], gets: [], fieldErrors: {}, counts: [], actions: null };
    const ui = {
        t: (key, ...args) => String(tr[key] ?? `[[${key}]]`).replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`),
        esc: (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])),
        qty: (v) => String(v), date: (v) => String(v), uom: (row) => row.baseUomId || '',
        byId,
        get: async (url) => {
            seen.gets.push(url);
            if (url.startsWith('/Quarantine/lookups/stock')) return { ok: true, json: { items: STOCK } };
            if (url.startsWith('/Quarantine/list/blocked')) return { ok: true, json: { data: { total: openCount } } };
            return { ok: true, json: [] };
        },
        write: async (...args) => { seen.writes.push(args); return { ok: true, json: [{ blockedNumber: 'BLK-1' }] }; },
        newKey: () => 'key-1', hideError() {}, showError() {}, loadCounts() {}, counter() {}, toast() {}, errorText: () => '',
        setCount: (code, value) => seen.counts.push([code, value]),
        clearFieldErrors: () => { seen.fieldErrors = {}; },
        fieldError: (scope, name, text) => { seen.fieldErrors[name] = text; },
        can: () => true, api: (x) => x, productCell: () => '', lotLink: () => '', whereCell: () => '',
        signAndSubmit: async () => ({ ok: true }), announceSent: async () => {}
    };
    let created = null;
    const listeners = {};
    const window = {
        QuarantineUi: ui,
        showConfirm: (title, callback, options) => seen.confirms.push({ title, callback, options }),
        DitenDataTable: {
            createList: async (config) => { created = config; return { dt: { ajax: { reload() {} }, search: () => ({ draw() {} }) } }; },
            renderActions: (items) => { seen.actions = items; return ''; }
        },
        bootstrap: { Modal: { getOrCreateInstance: () => ({ show() {}, hide() {} }) } },
        DitenModal: { error() {} },
        location: { search: '' }
    };
    const document = {
        querySelector: (selector) => (selector === '.datatables-quarantine-blocked' ? {} : null),
        documentElement: { lang: 'tr' },
        addEventListener: (type, fn) => { listeners[type] = fn; }
    };
    const context = vm.createContext({ window, document, URLSearchParams, console, Option: class { constructor(text, value) { this.text = text; this.value = value; } } });
    vm.runInContext(read(`${web}wwwroot/assets/js/Quarantine/blocked.js`), context);
    await listeners.DOMContentLoaded();
    return { els, byId, seen, config: () => created };
};

const flush = () => new Promise((resolve) => setImmediate(resolve));

// ── (1) the unblock dialog shows its sentence, not its tags ─────────────────────────────────────────────────────────

test('(1) the dialog body follows the one dialog rule (subtext is text, subtextHtml is markup)', () => {
    assert.ok(confirmView.includes(BODY_RULE), 'the body rule of _GlobalConfirmation.cshtml changed — re-read it before trusting (1)');
    assert.ok(confirmView.includes("window.DitenText = function (value)"));
});

test('(1) "Bloke kaldırılsın mı?" shows the subject and the sentence — no raw <div class=…> and no &lt;', async () => {
    const p = await page();
    const row = { blockedId: 'b1', reviewed: true, activeDecision: null, itemLabel: 'Parasetamol 500 mg', lot: { lotNumber: 'L-2026-0001' }, quantity: 50, baseUomId: 'EA' };
    p.config().actions.onRowAction.UNBLOCK({ row });
    const options = p.seen.confirms.at(-1).options;
    const text = visible(dialogBody(options));

    assert.equal(text.includes('<div'), false, `the reader sees markup: ${text}`);
    assert.equal(text.includes('&lt;'), false, `the reader sees an entity: ${text}`);
    assert.equal(text.includes('class='), false, text);
    assert.ok(text.includes('Parasetamol 500 mg · L-2026-0001 · 50 EA'), text);
    assert.ok(text.includes(tr['Quarantine.Unblock.Text']), text);
    assert.equal(options.subtext, undefined);
});

test('(1) the subject stays TEXT inside the markup body (no XSS through the record\'s name)', async () => {
    const p = await page();
    const hostile = '<img src=x onerror=alert(1)> & "Co"';
    p.config().actions.onRowAction.UNBLOCK({ row: { blockedId: 'b1', reviewed: true, itemLabel: hostile, lot: { lotNumber: 'L-1' }, quantity: 1, baseUomId: 'EA' } });
    const html = dialogBody(p.seen.confirms.at(-1).options);
    assert.equal(/<img/i.test(html), false, html);
    assert.ok(visible(html).includes(hostile), visible(html));
});

test('(1) "İncelemeyi tamamla" hands its subject over as text — the reader never sees &amp; / &quot;', async () => {
    const p = await page();
    p.config().actions.onRowAction.COMPLETEREVIEW({ row: { blockedId: 'b1', version: 3, itemLabel: 'Kayıt "A" & B\'nin ürünü', lot: { lotNumber: 'L-1' }, blockedNumber: 'BLK-1' } });
    const text = visible(dialogBody(p.seen.confirms.at(-1).options));
    assert.equal(text, 'Kayıt "A" & B\'nin ürünü · L-1 · BLK-1');
});

// ── (2) the typed quantity stays ────────────────────────────────────────────────────────────────────────────────────

const withStock = async () => {
    const p = await page();
    p.byId('qr-block-lot').value = 'lot-1';
    await p.byId('qr-block-lot').fire('change');
    assert.equal(p.byId('qr-block-row').disabled, false);
    return p;
};
const chooseRow = (p, value = '0') => { p.byId('qr-block-row').value = value; return p.byId('qr-block-row').fire('change', { target: { value } }); };

test('(2) a typed 50 stays 50 after the stock row (200 EA) is chosen; the row amount is shown under the field', async () => {
    const p = await withStock();
    p.byId('qr-block-qty').value = '50';
    await chooseRow(p);
    assert.equal(p.byId('qr-block-qty').value, '50');
    assert.equal(p.byId('qr-block-available').textContent, 'Kullanılabilir: 200 EA');
    assert.equal(p.seen.fieldErrors.qty, '');
});

// WP-MVP1-FIX-LIVE4 (FU-0175-BLOCK-QTY, G1 live run) CHANGES the expectation above it: choosing the row no longer suggests the row's amount
// (the live 80 + a typed "10" became "8010"); the field stays empty, the amount is shown under it, "Tümü" writes it on purpose.
test('(2) choosing a stock row leaves an empty quantity EMPTY — the row amount is only shown under the field', async () => {
    const p = await withStock();
    p.byId('qr-block-qty').value = '';
    await chooseRow(p);
    assert.equal(p.byId('qr-block-qty').value, '');
    assert.equal(p.byId('qr-block-available').textContent, 'Kullanılabilir: 200 EA');
    assert.equal(p.byId('qr-block-all').disabled, false);
    assert.equal(p.seen.fieldErrors.qty, '');
});

test('(2) "Tümü" writes the whole row amount — and only when clicked; typing then replaces it', async () => {
    const p = await withStock();
    await chooseRow(p);
    await p.byId('qr-block-all').fire('click', {});
    assert.equal(p.byId('qr-block-qty').value, '200');
    p.byId('qr-block-qty').value = '10';                                  // the reader's own value replaces the field (no append)
    await chooseRow(p);
    assert.equal(p.byId('qr-block-qty').value, '10');
});

test('(2) a quantity above the row amount is named at once and is NOT sent', async () => {
    const p = await withStock();
    p.byId('qr-block-qty').value = '250';
    await chooseRow(p);
    assert.equal(p.byId('qr-block-qty').value, '250');
    assert.equal(p.seen.fieldErrors.qty, tr['Quarantine.BlockStock.QtyInvalid']);

    p.byId('qr-block-reason-code').value = 'DAMAGE';
    p.byId('qr-block-reason').value = 'Ambalaj hasarlı, inceleme gerekiyor';
    await p.byId('qr-block-form').fire('submit', { preventDefault() {} });
    assert.equal(p.seen.writes.length, 0);
    assert.equal(p.seen.fieldErrors.qty, tr['Quarantine.BlockStock.QtyInvalid']);

    p.byId('qr-block-qty').value = '50';
    await p.byId('qr-block-form').fire('submit', { preventDefault() {} });
    assert.equal(p.seen.writes.length, 1);
    assert.equal(p.seen.writes[0][2].buckets[0].quantity, 50);
});

// ── (3) the list: status filter, status label, open-only badge ──────────────────────────────────────────────────────

test('(3) the list has a status filter that travels as `status`', async () => {
    const p = await page();
    const fields = p.config().filters.fields;
    assert.deepEqual(JSON.parse(JSON.stringify(fields.find((f) => f.key === 'status'))), { id: 'filterStatus', key: 'status', kind: 'single' });
});

test('(3) a Closed record shows its status label and offers no action', async () => {
    const p = await page();
    const defs = p.config().config.columnDefs;
    const closed = { blockedId: 'b1', status: 'Closed', reviewed: true, allowedActions: ['Dispose:Destroy'], origin: 'ManualBlock' };
    const cell = defs.find((d) => d.targets === 7).render(null, 'display', closed);
    assert.ok(cell.includes('data-qr-blocked-status="Closed"'), cell);
    assert.ok(cell.includes(tr['Quarantine.Blocked.Status.Closed']), cell);
    defs.find((d) => d.targets === -1).render(null, 'display', closed);
    assert.equal(p.seen.actions.length, 3);
    assert.equal(p.seen.actions.filter((a) => a.visible).map((a) => a.key).join(','), '');

    const open = defs.find((d) => d.targets === 7).render(null, 'display', { ...closed, status: 'Open', reviewed: false });
    assert.equal(open.includes('data-qr-blocked-status'), false, open);
});

test('(3) the tab badge counts OPEN records only', async () => {
    const p = await page({ openCount: 2 });
    p.config().onResponse({ status: 'Open', data: { total: 3 } });
    assert.deepEqual(p.seen.counts.at(-1), ['BLOCKEDSTOCK', 3]);

    p.config().onResponse({ status: 'Closed', data: { total: 7 } });
    await flush();
    assert.ok(p.seen.gets.includes('/Quarantine/list/blocked?start=0&length=1'), p.seen.gets.join(', '));
    assert.deepEqual(p.seen.counts.at(-1), ['BLOCKEDSTOCK', 2]);
    assert.equal(p.seen.counts.some(([, v]) => v === 7), false);
});
