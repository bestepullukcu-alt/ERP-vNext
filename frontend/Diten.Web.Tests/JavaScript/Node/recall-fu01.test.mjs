// MOD-0177-FU01 DL-2 — the customer on the recall screens, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Recall/recall-ui.js, case-view.js, case-decision.js, case-detail.js (Vaka Detayı, drawn on a small DOM)
//   frontend/Diten.Web/wwwroot/assets/js/Recall/trace-view.js, trace.js (İzleme), drill-view.js (never draws run.customers)
//   frontend/Diten.Web/Resources/Views/Recall/Recall.{tr,ar}.resx (the words)
// No jsdom (a clean checkout has no node_modules — CT HOLD 2026-10-21): the scripts run unchanged on the DOM below, exactly the surface they
// touch. RecallFu01NodeTests.cs runs this file from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const read = (p) => readFileSync(path.join(repo, p), 'utf8');
const js = (name) => read(`frontend/Diten.Web/wwwroot/assets/js/Recall/${name}`);
const decodeXml = (s) => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');
// the words a page receives: the Recall.Keys list (Views/Recall/Recall.cs → _RecallL10n.cshtml), valued from the language's resx
const delivered = new Set([...read('frontend/Diten.Web/Views/Recall/Recall.cs').match(/IReadOnlyList<string> Keys =\s*\[([\s\S]*?)\];/)[1].matchAll(/"([^"]+)"/g)].map((m) => m[1]));
const resx = (lang) => Object.fromEntries([...read(`frontend/Diten.Web/Resources/Views/Recall/Recall.${lang}.resx`)
    .matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)].map((m) => [m[1], decodeXml(m[2])]).filter(([key]) => delivered.has(key)));
const TR = resx('tr');
const AR = resx('ar');
const fmt = (words) => (key, ...args) => String(words[key] || '').replace(/\{(\d)\}/g, (m, i) => `${args[i] ?? ''}`);

const A = '3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c';
const C = '5d1b2e3f-0a4c-4d6e-8f7a-9b0c1d2e3f4a';
const D = '6e2c3f4a-1b5d-4e7f-9a8b-0c1d2e3f4a5b';
const N = '7f3d4a5b-2c6e-4f8a-8b9c-1d2e3f4a5b6c';
const CASE = '0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f';
const ME = '11111111-1111-4111-8111-111111111111';

// ── a small DOM: what the recall scripts touch, nothing more ──────────────────────────────────────────────────────────
const strip = (html) => decodeXml(String(html).replace(/<[^>]*>/g, ' ')).replace(/\s+/g, ' ').trim();
const createDom = (words, actions, responses) => {
    const ids = new Map();
    const fetched = [];
    const docListeners = {};
    class El {
        constructor(tag, id) {
            this.tagName = tag.toUpperCase(); this.attrs = {}; this.html = ''; this.text = null; this.cls = new Set(); this.children = []; this.parentElement = null;
            this.on = {}; this.parts = {}; this.options = []; this._value = '';
            if (id) this.id = id;
        }
        get id() { return this.attrs.id || ''; }
        set id(v) { this.attrs.id = v; ids.set(v, this); }
        get className() { return [...this.cls].join(' '); }
        set className(v) { this.cls = new Set(String(v).split(/\s+/).filter(Boolean)); }
        get classList() {
            const s = this.cls;
            return { add: (...c) => c.forEach((x) => s.add(x)), remove: (...c) => c.forEach((x) => s.delete(x)), contains: (c) => s.has(c),
                toggle: (c, on) => { const want = on === undefined ? !s.has(c) : !!on; if (want) s.add(c); else s.delete(c); return want; } };
        }
        get innerHTML() { return this.html; }
        set innerHTML(v) { this.html = String(v); this.text = null; this.parts = {}; }
        get textContent() { return this.text ?? strip(this.html); }
        set textContent(v) { this.text = String(v); this.html = ''; }
        get value() { return this._value; }
        set value(v) { this._value = String(v); }
        get dataset() { return Object.fromEntries(Object.entries(this.attrs).filter(([k]) => k.startsWith('data-')).map(([k, v]) => [k.slice(5).replace(/-([a-z])/g, (m, c) => c.toUpperCase()), v])); }
        get selectedOptions() { return this.options.length ? [this.options[0]] : []; }
        get nextSibling() { return null; }
        setAttribute(k, v) { this.attrs[k] = String(v); if (k === 'id') ids.set(String(v), this); }
        getAttribute(k) { return k in this.attrs ? this.attrs[k] : null; }
        removeAttribute(k) { delete this.attrs[k]; }
        addEventListener(type, fn) { (this.on[type] ||= []).push(fn); }
        async fire(type) { for (const fn of this.on[type] || []) await fn({ type, target: this, preventDefault() {} }); }
        insertBefore(child, ref) { child.parentElement = this; this.children.push(child); return child; }
        appendChild(child) { return this.insertBefore(child, null); }
        replaceChildren(...nodes) { this.children = nodes; this.options = nodes; }
        remove() {}
        closest() { return null; }
        reset() {}
        allHtml() { return this.html + Object.values(this.parts).map((p) => p.allHtml()).join('') + this.children.map((c) => c.allHtml?.() || '').join(''); }
        querySelector(sel) { return sel === 'tbody' ? (this.parts.tbody ||= new El('tbody')) : null; }
        add(option) { this.options.push(option); }
        querySelectorAll(sel) {
            const m = /^\[data-([a-z-]+)\]$/.exec(sel);
            if (!m) return [];
            const at = sel + '|' + this.allHtml();
            if (this.cache?.[at]) return this.cache[at]; // the same markup → the same buttons (their listeners are the page's)
            const found = [...this.allHtml().matchAll(new RegExp(`<[^>]*data-${m[1]}="([^"]*)"[^>]*>`, 'g'))].map((x) => {
                const b = new El('button');
                b.attrs[`data-${m[1]}`] = decodeXml(x[1]);
                if (/disabled/.test(x[0])) b.attrs.disabled = '';
                return b;
            });
            (this.cache ||= {})[at] = found;
            return found;
        }
    }
    const byId = (id) => ids.get(id) || new El('div', id);
    byId('rc-case').setAttribute('data-case-id', CASE);
    byId('rc-case').setAttribute('data-user-id', ME);
    byId('recall-actions').textContent = JSON.stringify(actions);
    const document = {
        documentElement: { lang: 'tr' },
        getElementById: byId,
        createElement: (tag) => new El(tag),
        querySelector: () => null,
        querySelectorAll: (sel) => (sel === '#rc-case-tabs [data-rc-tab]' ? tabs : []),
        addEventListener: (type, fn) => { (docListeners[type] ||= []).push(fn); }
    };
    const tabs = ['scope', 'customers', 'returns', 'effectiveness'].map((name) => { const tab = new El('a'); tab.attrs['data-rc-tab'] = name; return tab; });
    const fetch = async (url) => {
        fetched.push(url);
        const answer = responses(url);
        return { ok: answer.status < 300, status: answer.status, json: async () => answer.body };
    };
    const modals = new Map();
    const window = {
        RecallL10n: words, location: { search: '', assign() {} }, localStorage: { getItem: () => null, setItem() {} },
        bootstrap: { Modal: { getOrCreateInstance: (el) => modals.get(el) || (modals.set(el, { shown: false, show() { this.shown = true; }, hide() { this.shown = false; } }), modals.get(el)) } }
    };
    class Option { constructor(text, value) { this.text = text; this.value = value; this.attrs = {}; this.dataset = {}; } }
    const context = vm.createContext({ window, document, fetch, Option, console, URLSearchParams, CSS: { escape: (s) => s }, Intl, setTimeout, queueMicrotask });
    return { context, byId, fetched, tabs, window, ready: async () => { for (const fn of docListeners.DOMContentLoaded || []) await fn(); } };
};

// ── fixtures (RECALL 1.10.0 bodies as the Web door wraps them: { data }) ───────────────────────────────────────────────
const lot = { lotId: 'L-1', lotNumber: 'LOT-26' };
const item = { itemId: 'I-1', skuId: 'S-1', skuLevel: 'Lsku' };
const display = (state, more = {}) => ({ source: 'Crm', state, ...more });
const RESOLVED = display('Resolved', { name: 'Example Hastanesi', code: 'ACC-2026-000042' });
const shipment = (n, number) => ({ nodeKey: `Shipment:L-1:${n}`, lot, item, sourceDocumentId: n, quantity: 3, baseUomId: 'EA', read: 'Ok', status: 'Delivered',
    shipToReference: `CUST-${n}/ADDR-1`, movements: [{ transactionId: 'T' + n, number, quantity: 3, postingDate: '2026-11-01T10:00:00Z' }] });
const row = (key, customer, n = key.slice(0, 4), extra = {}) => ({ customerRef: key, customer, shipments: [shipment(n, 'GIS-' + n)], notifications: [], consumptions: [], ...extra });
const caseDetail = { caseId: CASE, number: 'RC-2026-0001', status: 'Active', class: 2, level: 'Pharmacy', item, rootLot: lot, version: 4, approvedScope: { revision: 1 },
    scopeSubmittedBy: { userId: '22222222-2222-4222-8222-222222222222' }, owner: { userId: ME, name: 'Ayşe' } };
const ACTIONS = { RECALL_CUSTOMER_NOTIFY: true, RECALL_CONSUMPTION_DECLARE: true, RECALL_RETURN_LINK: true, RECALL_SCOPE_MARK: true };

const casePage = async (parts, words = TR, actions = ACTIONS) => {
    const dom = createDom(words, actions, (url) => {
        const part = new URL('http://x' + url).searchParams.get('part') || '';
        const page = Number(new URL('http://x' + url).searchParams.get('customerPage') || 1);
        const body = part === '' ? caseDetail : typeof parts[part] === 'function' ? parts[part](page) : parts[part];
        return body && body.httpStatus ? { status: body.httpStatus, body: body.body } : { status: 200, body: { data: body ?? {} } };
    });
    vm.runInContext(`${js('recall-ui.js')}\n${js('case-view.js')}\n${js('case-decision.js')}\n${js('case-detail.js')}`, dom.context);
    await dom.ready();
    return dom;
};
const settle = () => new Promise((resolve) => setTimeout(resolve, 10)); // the page's loaders are not awaited by its listeners
const openTab = async (dom, name) => { await dom.tabs.find((t) => t.attrs['data-rc-tab'] === name).fire('shown.bs.tab'); await settle(); };
const visible = (el) => el.textContent;
const titles = (html) => [...html.matchAll(/title="([^"]*)"/g)].map((m) => decodeXml(m[1]));
const assertNoKey = (html, what) => {
    assert.ok(!strip(html).includes(A), `${what}: the account id is visible text`);
    assert.ok(!titles(html).some((x) => x.includes(A)), `${what}: the account id is a title`);
};

const MIXED = [
    row(A, RESOLVED, 'A001'),
    row(C, display('Unresolved', { shipTo: 'CUST-200/ADDR-1', shipmentNumber: 'GIS-0042' }), 'C001'),
    row(D, display('Forbidden', { shipTo: 'CUST-201/ADDR-1', shipmentNumber: 'GIS-0043' }), 'D001'),
    row(N, display('NotFound', { shipmentNumber: 'GIS-0044' }), 'N001'),
    row('CUST-100/ADDR-2', { source: 'Unknown', state: 'Legacy', label: 'CUST-100/ADDR-2' }, 'L001', { label: 'CUST-100/ADDR-2' }),
    row('DOC:X1', { source: 'Document', state: 'NoCrm', label: 'DOC:X1' }, 'X001'),
    row('CUST-300/ADDR-1', { source: 'Manual', state: 'NoCrm', label: 'CUST-300/ADDR-1' }, 'M001')
];

test('customers tab never prints the account id', async () => {
    const dom = await casePage({ scope: { items: [], excludedCount: 0, revision: 1 }, customers: MIXED, effectiveness: { rows: [], totals: [] } });
    await openTab(dom, 'customers');
    const html = dom.byId('rc-cust-table').querySelector('tbody').innerHTML;
    const text = strip(html);
    assert.ok(text.includes('Example Hastanesi · ACC-2026-000042'));
    assert.ok(text.includes(fmt(TR)('Recall.Customer.UnresolvedWith', 'CUST-200/ADDR-1 · GIS-0042')));
    assert.ok(text.includes(fmt(TR)('Recall.Customer.ForbiddenWith', 'CUST-201/ADDR-1 · GIS-0043')));
    assert.ok(text.includes(fmt(TR)('Recall.Customer.NotFoundWith', 'GIS-0044')));
    assert.ok(!/font-monospace">[0-9a-f]{8}-/.test(html), 'no key line under the name');
    assertNoKey(html, 'customers tab');
    assert.ok(html.includes(`data-notify="${A}"`), 'the key stays as a command value');
});

test('no view prints the account id', async () => {
    const scope = { items: [
        { nodeKey: 'Lot:L-1', kind: 'Lot', isRoot: true, lot, item, included: true, effectivelyIncluded: true },
        { nodeKey: 'Shipment:L-1:A001', parentKey: 'Lot:L-1', kind: 'Shipment', lot, item, customerRef: A, customer: RESOLVED, quantity: 3, baseUomId: 'EA', included: true, effectivelyIncluded: true }
    ], excludedCount: 0, revision: 1, editable: false };
    const dom = await casePage({
        scope, customers: MIXED,
        effectiveness: { rows: [{ customerRef: A, customer: RESOLVED, skuId: 'S-1', baseUomId: 'EA', shipped: 3, returned: 0, consumed: 0, open: 3, percent: 0, met: false, notified: false }], totals: [] },
        returns: { links: [{ linkId: 'K1', source: 'ManualCustomerReturn', transactionId: 'T9', transactionNumber: 'CR-9', lot, quantity: 1, baseUomId: 'EA', customerRef: A, customer: RESOLVED, postedAtUtc: '2026-11-02T00:00:00Z' }],
            candidates: [{ transactionId: 'T10', transactionNumber: 'CR-10', lot, quantity: 1, baseUomId: 'EA', eligible: true, postedAtUtc: '2026-11-02T00:00:00Z' }] },
        diff: { added: [{ nodeKey: 'Shipment:L-1:A002', kind: 'Shipment', lot, quantity: 1, baseUomId: 'EA', customerRef: A, customer: RESOLVED }], removed: [] }
    });
    assertNoKey(dom.byId('rc-scope-table').querySelector('tbody').innerHTML, 'scope');
    assert.ok(strip(dom.byId('rc-scope-table').querySelector('tbody').innerHTML).includes('Example Hastanesi · ACC-2026-000042'));
    for (const tab of ['customers', 'returns', 'effectiveness']) await openTab(dom, tab);
    assertNoKey(dom.byId('rc-ret-links').querySelector('tbody').innerHTML, 'return links');
    assertNoKey(dom.byId('rc-eff-table').querySelector('tbody').innerHTML, 'effectiveness');
    assert.ok(strip(dom.byId('rc-eff-table').querySelector('tbody').innerHTML).includes('Example Hastanesi · ACC-2026-000042'));
    // the diff list ("Yeniden izle")
    dom.context.window.RecallUi.can = () => true;
    await vm.runInContext('(async () => {})()', dom.context);
    // the three dialogs: notify / consume read the row's display; the link list's option texts
    const cust = dom.byId('rc-cust-table');
    await cust.querySelectorAll('[data-notify]').find((b) => b.dataset.notify === A).fire('click'); await settle();
    assert.equal(visible(dom.byId('rc-notify-customer')), 'Example Hastanesi · ACC-2026-000042');
    await cust.querySelectorAll('[data-consume]').find((b) => b.dataset.consume === A).fire('click'); await settle();
    assert.equal(visible(dom.byId('rc-consume-customer')), 'Example Hastanesi · ACC-2026-000042');
    await dom.byId('rc-ret-candidates').querySelectorAll('[data-link]')[0].fire('click'); await settle();
    const options = dom.byId('rc-link-customer').options;
    assert.ok(options.length > 0);
    assert.ok(options.every((o) => !o.text.includes(A)));
    assert.ok(options.some((o) => o.value === A && o.text === 'Example Hastanesi · ACC-2026-000042'));
    // the drill screen never draws run.customers
    const drill = js('drill-view.js') + js('drills.js');
    assert.ok(!/\.customers\b/.test(drill), 'drill-view.js / drills.js draw no customers');
});

test('the diff list shows the display, not the key', async () => {
    const dom = await casePage({ scope: { items: [], excludedCount: 0, revision: 1 },
        diff: { added: [{ nodeKey: 'Shipment:L-1:A002', kind: 'Shipment', lot, quantity: 1, baseUomId: 'EA', customerRef: A, customer: RESOLVED }], removed: [] } });
    const page = js('case-detail.js');
    assert.match(page, /n\.customer \? '· ' \+ esc\(cv\.text\(n\.customer, t\)\) : ''/);
    assert.doesNotMatch(page, /n\.customerRef \? '· ' \+ esc\(n\.customerRef\)/);
    const cv = dom.context.window.RecallCustomerView;
    assert.equal(cv.text(RESOLVED, fmt(TR)), 'Example Hastanesi · ACC-2026-000042');
});

test('legacy and no-crm rows stay separate with badge', async () => {
    const warned = { ...RESOLVED, noCrmWarning: true };
    const rows = [row(A, warned, 'A001'), row('CUST-100/ADDR-2', { source: 'Unknown', state: 'Legacy', label: 'CUST-100/ADDR-2' }, 'L001'),
        row('CUST-100/ADDR-3', { source: 'Unknown', state: 'Legacy', label: 'Example Hastanesi · ACC-2026-000042' }, 'L002'),
        row('DOC:X1', { source: 'Document', state: 'NoCrm', label: 'DOC:X1' }, 'X001'), row('CUST-300/ADDR-1', { source: 'Manual', state: 'NoCrm', label: 'CUST-300/ADDR-1' }, 'M001')];
    const scope = { items: [{ nodeKey: 'Shipment:L-1:A001', kind: 'Shipment', lot, item, customerRef: A, customer: warned, included: true, effectivelyIncluded: true }],
        excludedCount: 0, revision: 1 };
    const dom = await casePage({ scope, customers: rows, effectiveness: { rows: [], totals: [] } });
    const scopeHtml = dom.byId('rc-scope-table').querySelector('tbody').innerHTML;
    assert.match(scopeHtml, /data-customer-badge="Recall\.Customer\.NoCrmBadge" title="([^"]*)"/);
    assert.equal(titles(scopeHtml)[0], TR['Recall.Customer.NoCrmHint']);
    await openTab(dom, 'customers');
    const html = dom.byId('rc-cust-table').querySelector('tbody').innerHTML;
    const badges = [...html.matchAll(/data-customer-badge="([^"]+)" title="([^"]*)"/g)].map((m) => [m[1], decodeXml(m[2])]);
    assert.equal(badges.length, 5);
    assert.equal(badges.filter(([k, h]) => k === 'Recall.Customer.LegacyBadge' && h === TR['Recall.Customer.LegacyHint']).length, 2);
    assert.equal(badges.filter(([, h]) => h === TR['Recall.Customer.ManualHint']).length, 1);
    assert.equal(badges.filter(([k, h]) => k === 'Recall.Customer.NoCrmBadge' && h === TR['Recall.Customer.NoCrmHint']).length, 2);
    assert.equal((strip(html).match(/Example Hastanesi · ACC-2026-000042/g) || []).length, 2, 'the same name on two rows: never merged');
    const unwarned = await casePage({ scope: { ...scope, items: [{ ...scope.items[0], customer: RESOLVED }] } });
    assert.doesNotMatch(unwarned.byId('rc-scope-table').querySelector('tbody').innerHTML, /data-customer-badge/);
});

test('texts come from l10n', async () => {
    const t = fmt(AR);
    const cv = (await casePage({ scope: { items: [], excludedCount: 0, revision: 1 } }, AR)).context.window.RecallCustomerView;
    assert.equal(cv.text(display('Unresolved'), t), AR['Recall.Customer.Unresolved']);
    assert.equal(cv.text(display('Forbidden'), t), AR['Recall.Customer.Forbidden']);
    assert.equal(cv.text(display('NotFound'), t), AR['Recall.Customer.NotFound']);
    assert.equal(cv.text(display('Deferred'), t), AR['Recall.Customer.Deferred']);
    assert.equal(cv.text(display('Unresolved', { shipmentNumber: 'GIS-1' }), t), t('Recall.Customer.UnresolvedWith', 'GIS-1'));
    const dom = await casePage({ scope: { items: [], excludedCount: 0, revision: 1 }, customers: [...MIXED, ...Array.from({ length: 30 }, (_, i) =>
        row(`ff${String(i).padStart(6, '0')}-0000-4000-8000-000000000000`, display('Deferred'), `Z${i}`))], effectiveness: { rows: [], totals: [] } }, AR);
    await openTab(dom, 'customers');
    const text = strip(dom.byId('rc-cust-table').querySelector('tbody').innerHTML);
    for (const key of ['Recall.Customer.LegacyBadge', 'Recall.Customer.NoCrmBadge', 'Recall.Customer.CrmUnavailable', 'Recall.Customer.CrmForbidden']) assert.ok(text.includes(AR[key]), key);
    const pager = dom.byId('rc-cust-cpager').textContent;
    assert.ok(pager.includes(t('Recall.Customer.NamesPage', 1, 25, 37)) && pager.includes(AR['Recall.Customer.PagePrev']) && pager.includes(AR['Recall.Customer.PageNext']));
    // the trace screen: Deferred and the export refusal in Arabic
    const tv = vm.runInContext(`${js('trace-view.js')}\n;RecallTraceView`, vm.createContext({ window: {}, console }));
    assert.equal(tv.customerText(display('Deferred'), t), AR['Recall.Customer.Deferred']);
    const trace = await tracePage([{ nodeKey: 'Shipment:L-1:D1', kind: 'Shipment', lot, customerRef: D, customer: display('Forbidden') }], AR);
    assert.ok(trace.byId('rc-trace-actions').innerHTML.includes(AR['Recall.Customer.ExportForbidden']));
    assert.ok(!Object.values(TR).some((v) => v && v.length > 6 && v.includes('Çözülemedi') && trace.byId('rc-trace-table').querySelector('tbody').innerHTML.includes(v)));
});

const tracePage = async (nodes, words = TR, extra = {}) => {
    const all = [{ nodeKey: 'Lot:L-1', kind: 'FinishedGoodLot', lot, item, level: 0 }, ...nodes.map((n) => ({ parentKey: 'Lot:L-1', level: 1, item, ...n }))];
    const dom = createDom(words, { RECALL_TRACE_EXPORT: true }, (url) => ({ status: 200, body: { trace: { nodes: all, gaps: [], summary: {}, ...extra }, labelsAvailable: true } }));
    dom.byId('rc-trace-lot').value = 'L-1';
    vm.runInContext(`${js('recall-ui.js')}\n${js('trace-view.js')}\n${js('trace.js')}`, dom.context);
    await dom.ready();
    await dom.byId('rc-trace-form').fire('submit');
    return dom;
};

test('unresolved or forbidden customer offers no command', async () => {
    const dom = await casePage({ scope: { items: [], excludedCount: 0, revision: 1 }, customers: MIXED, effectiveness: { rows: [], totals: [] },
        returns: { links: [], candidates: [{ transactionId: 'T10', transactionNumber: 'CR-10', lot, quantity: 1, baseUomId: 'EA', eligible: true, postedAtUtc: '2026-11-02T00:00:00Z' }] } });
    await openTab(dom, 'customers');
    const html = dom.byId('rc-cust-table').querySelector('tbody').innerHTML;
    for (const key of [C, D]) assert.ok(!html.includes(`data-notify="${key}"`) && !html.includes(`data-consume="${key}"`), `no command for ${key}`);
    for (const key of [A, N]) assert.ok(html.includes(`data-notify="${key}"`) && html.includes(`data-consume="${key}"`), `commands for ${key}`);
    assert.ok(html.includes(`data-customer-blocked="Recall.Customer.CrmUnavailable"`));
    assert.ok(html.includes(`data-customer-blocked="Recall.Customer.CrmForbidden"`));
    assert.equal((strip(html).match(new RegExp(TR['Recall.Customer.CrmUnavailable'].replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g')) || []).length, 1);
    // the link dialog: C and D are no options; the reasons are said
    await openTab(dom, 'returns');
    await dom.byId('rc-ret-candidates').querySelectorAll('[data-link]')[0].fire('click'); await settle();
    const values = dom.byId('rc-link-customer').options.map((o) => o.value);
    assert.ok(!values.includes(C) && !values.includes(D) && values.includes(A) && values.includes(N));
    const note = dom.byId('rc-link-crm-note').textContent;
    assert.ok(note.includes(TR['Recall.Customer.CrmUnavailable']) && note.includes(TR['Recall.Customer.CrmForbidden']));
    // only C in the case → no field, no bind button
    const lonely = await casePage({ scope: { items: [], excludedCount: 0, revision: 1 }, customers: [MIXED[1]], effectiveness: { rows: [], totals: [] },
        returns: { links: [], candidates: [{ transactionId: 'T10', transactionNumber: 'CR-10', lot, quantity: 1, baseUomId: 'EA', eligible: true, postedAtUtc: '2026-11-02T00:00:00Z' }] } });
    await openTab(lonely, 'returns');
    await lonely.byId('rc-ret-candidates').querySelectorAll('[data-link]')[0].fire('click'); await settle();
    for (const id of ['rc-link-fields', 'rc-link-submit']) assert.ok(lonely.byId(id).classList.contains('d-none'), id);
    // the server's 503 / 403 reach the dialog's error box as their OWN words (not the generic ones they would fall back to)
    const ui = dom.context.window.RecallUi;
    const shown503 = ui.errorText({ status: 503, json: { error: { code: 'RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE' } } });
    const shown403 = ui.errorText({ status: 403, json: { error: { code: 'RECALL_CUSTOMER_DIRECTORY_FORBIDDEN' } } });
    assert.equal(shown503, TR['Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE']);
    assert.notEqual(shown503, TR['Recall.Errors.SERVICE_UNAVAILABLE']);
    assert.equal(shown403, TR['Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN']);
    assert.notEqual(shown403, TR['Recall.Errors.FORBIDDEN']);
    // İzleme: a Forbidden node → no CSV link, the refusal text; an Unresolved node → the link as before
    const forbidden = await tracePage([{ nodeKey: 'Shipment:L-1:D1', kind: 'Shipment', lot, customerRef: D, customer: display('Forbidden') }]);
    assert.ok(!forbidden.byId('rc-trace-actions').innerHTML.includes('id="rc-trace-csv"'));
    assert.ok(forbidden.byId('rc-trace-actions').innerHTML.includes(TR['Recall.Customer.ExportForbidden']));
    const unresolved = await tracePage([{ nodeKey: 'Shipment:L-1:C1', kind: 'Shipment', lot, customerRef: C, customer: display('Unresolved') }]);
    assert.ok(unresolved.byId('rc-trace-actions').innerHTML.includes('id="rc-trace-csv"'));
    // the evidence screen keeps its own reading: ui.errorText(reply) — the same mapping
    assert.match(js('evidence.js'), /ui\.errorText\(reply\)/);
});

const V200 = Array.from({ length: 200 }, (_, i) => `${String(i).padStart(8, '0')}-0000-4000-8000-000000000000`).sort();
const v200Rows = (page) => V200.map((k, i) => row(k, Math.floor(i / 25) + 1 === page ? display('Resolved', { name: `Musteri ${i}`, code: `ACC-${i}` }) : display('Deferred'), `P${i}`));

test('customer pages', async () => {
    const dom = await casePage({ scope: { items: [], excludedCount: 0, revision: 1 }, customers: v200Rows, effectiveness: (page) => ({ totals: [],
        rows: V200.map((k, i) => ({ customerRef: k, customer: Math.floor(i / 25) + 1 === page ? display('Resolved', { name: `Musteri ${i}`, code: `ACC-${i}` }) : display('Deferred'),
            skuId: 'S-1', baseUomId: 'EA', shipped: 1, returned: 0, consumed: 0, open: 1, percent: 0, met: false, notified: false })) }),
        returns: { links: [], candidates: [{ transactionId: 'T10', transactionNumber: 'CR-10', lot, quantity: 1, baseUomId: 'EA', eligible: true, postedAtUtc: '2026-11-02T00:00:00Z' }] } });
    await openTab(dom, 'customers');
    // (a) 25 customers drawn, the summary counts 200, the selector, no Deferred text in the tab
    let html = dom.byId('rc-cust-table').querySelector('tbody').innerHTML;
    assert.equal((html.match(/data-notify="/g) || []).length, 25);
    assert.ok(dom.byId('rc-cust-summary').textContent.includes('200'));
    assert.ok(!strip(html).includes(TR['Recall.Customer.Deferred']));
    assert.ok(dom.byId('rc-cust-cpager').textContent.includes(fmt(TR)('Recall.Customer.NamesPage', 1, 25, 200)));
    const next = dom.byId('rc-cust-cpager').querySelectorAll('[data-cpage]').find((b) => b.dataset.cpage === '1');
    await next.fire('click'); await settle();
    assert.ok(dom.fetched.some((u) => u.includes('part=customers&customerPage=2')), dom.fetched.join('\n'));
    html = dom.byId('rc-cust-table').querySelector('tbody').innerHTML;
    assert.ok(strip(html).includes('Musteri 25 · ACC-25') && !strip(html).includes('Musteri 0 · ACC-0'));
    assert.ok(!strip(html).includes(TR['Recall.Customer.Deferred']));
    // (b) the link dialog lists the page's 25 customers and carries the selector
    await openTab(dom, 'returns');
    await dom.byId('rc-ret-candidates').querySelectorAll('[data-link]')[0].fire('click'); await settle();
    assert.equal(dom.byId('rc-link-customer').options.length, 25);
    assert.ok(dom.byId('rc-link-cpager').textContent.includes(TR['Recall.Customer.PageNext']));
    // (c) effectiveness: all 200 rows, the selector above, the other pages' CRM rows say "not on this page"
    await openTab(dom, 'effectiveness');
    const eff = strip(dom.byId('rc-eff-table').querySelector('tbody').innerHTML);
    assert.equal((eff.match(new RegExp(TR['Recall.Customer.Deferred'], 'g')) || []).length, 175);
    assert.ok(dom.byId('rc-eff-cpager').textContent.includes(fmt(TR)('Recall.Customer.NamesPage', 1, 25, 200)));
    assert.ok(!eff.includes(V200[0]));
    // the trace: 200 nodes, the selector, Deferred for 175
    const trace = await tracePage(V200.map((k, i) => ({ nodeKey: `Shipment:L-1:${i}`, kind: 'Shipment', lot, customerRef: k,
        customer: i < 25 ? display('Resolved', { name: `Musteri ${i}`, code: `ACC-${i}` }) : display('Deferred') })));
    const table = strip(trace.byId('rc-trace-table').querySelector('tbody').innerHTML);
    assert.equal((table.match(new RegExp(TR['Recall.Customer.Deferred'], 'g')) || []).length, 175);
    assert.ok(trace.byId('rc-trace-cpager').textContent.includes(fmt(TR)('Recall.Customer.NamesPage', 1, 25, 200)));
    assert.ok(!table.includes(V200[0]));
    // (d) 25 customers or fewer: no selector
    const few = await casePage({ scope: { items: [], excludedCount: 0, revision: 1 }, customers: MIXED, effectiveness: { rows: [], totals: [] } });
    await openTab(few, 'customers');
    assert.ok(few.byId('rc-cust-cpager').classList.contains('d-none'));
});
