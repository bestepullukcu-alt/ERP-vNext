// MOD-0175-FU01 DL-2 (pack §11, AC-25 … AC-28) — the source-reversal states on the Quarantine pages, run by Node against the SHIPPED files
// (not copies): quarantine-ui.js (holdOpen / holdBadge / conflictBadge) under queue.js, details.js, blocked.js and decide.js, with the words
// of the shipped Quarantine.{tr,en,ar}.resx. The DOM is a small recording fake: what is measured is what the page draws (badges, row
// actions, banners, buttons). QuarantineSourceReversalWebTests.cs runs this file from `dotnet test`.
//   node --test frontend/Diten.Web.Tests/Quarantine/quarantine-source-reversal.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const read = (p) => readFileSync(path.join(repo, p), 'utf8');
const js = (name) => read(`frontend/Diten.Web/wwwroot/assets/js/${name}`);
const decodeXml = (s) => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');
const words = (lang) => Object.fromEntries([...read(`frontend/Diten.Web/Resources/Views/Quarantine/Quarantine.${lang}.resx`)
    .matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)].map((m) => [m[1], decodeXml(m[2])]));
const visible = (html) => decodeXml(String(html).replace(/<[^>]*>/g, ' ')).replace(/\s+/g, ' ').trim();
const CAUSES = ['Decided', 'LinesPosted', 'QuantityChanged', 'OwnMovementReversed', 'LotChanged'];
// the pack §14 action codes the pages ask QuarantineUi.can for (the view writes them as {code: true|false})
const CODES = ['DECIDE', 'VIEWHOLD', 'STARTREVIEW', 'REFRESHWORKFLOW', 'WITHDRAWDECISION', 'REAPPLYDECISION', 'SUBMITDECISION', 'DISPOSE', 'UNBLOCK', 'COMPLETEREVIEW', 'BLOCKSTOCK', 'QUARANTINELOT'];
const USER = '0d9c8b7a-6f5e-4d3c-8b1a-0f9e8d7c6b5a';
const mark = (cause, extra = {}) => ({ atUtc: '2026-11-04T09:30:00Z', reversalTransactionId: '11111111-2222-4333-8444-555555555555', reversalTransactionNumber: 'INV-REV-000042', reversedBy: { userId: USER, name: 'Zehra Yıldız' }, cause, ...extra });
const voided = (extra = {}) => ({ atUtc: '2026-11-04T09:30:00Z', reason: 'SourceReversed', reversalTransactionId: '11111111-2222-4333-8444-555555555555', reversalTransactionNumber: 'INV-REV-000042', reversedBy: { userId: USER, name: 'Zehra Yıldız' }, ...extra });

// ── a recording DOM ───────────────────────────────────────────────────────────────────────────────────────────────
const classList = (initial = []) => ({
    set: new Set(initial), add(c) { this.set.add(c); }, remove(c) { this.set.delete(c); }, contains(c) { return this.set.has(c); },
    toggle(c, on) { ((on === undefined ? !this.set.has(c) : on) ? this.set.add(c) : this.set.delete(c)); }
});
const element = (id, hidden = false) => {
    const subs = {};
    return {
        id, value: '', textContent: '', innerHTML: '', disabled: false, checked: false, href: '', dataset: {}, style: {}, listeners: {},
        classList: classList(hidden ? ['d-none'] : []),
        addEventListener(type, fn) { (this.listeners[type] ||= []).push(fn); },
        querySelector(selector) { return (subs[selector] ||= element(selector)); },
        querySelectorAll() { return []; },
        add() {}, focus() {}, scrollIntoView() {}
    };
};
const shown = (el) => !el.classList.contains('d-none');

const page = async (script, { lang = 'tr', can = () => true, json = null, ids = {}, rootSelector = null } = {}) => {
    const els = {};
    const byId = (id) => (els[id] ||= element(id, ids[id] === 'hidden'));
    Object.keys(ids).forEach(byId);
    const seen = { list: null, actions: null, gets: [] };
    const listeners = {};
    const L = words(lang);
    const window = {
        QuarantineL10n: L,
        StockMovementPeople: {
            text: (row, unknown) => row.createdByName || unknown,
            html: (row, unknown) => (row.createdByName ? String(row.createdByName).replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`) : `<span title="${row.createdById}">${unknown}</span>`)
        },
        DitenDataTable: {
            createList: async (config) => { seen.list = config; return { dt: { ajax: { reload() {} }, search: () => ({ draw() {} }) } }; },
            renderActions: (items) => { seen.actions = items; return items.filter((a) => a.visible).map((a) => `<a data-action="${a.key}"></a>`).join(''); }
        },
        bootstrap: { Modal: { getOrCreateInstance: () => ({ show() {}, hide() {} }) } },
        location: { search: '', href: '' },
        scrollTo() {}
    };
    const document = {
        documentElement: { lang },
        getElementById: (id) => (id === 'quarantine-actions' ? { textContent: JSON.stringify(Object.fromEntries(CODES.map((c) => [c, can(c)]))) } : byId(id)),
        querySelector: (selector) => (selector === rootSelector ? {} : null),
        querySelectorAll: () => [],
        addEventListener: (type, fn) => { listeners[type] = fn; }
    };
    const fetch = async (url) => { seen.gets.push(url); return { ok: !!json, status: json ? 200 : 503, json: async () => json, headers: { get: () => null } }; };
    const context = vm.createContext({ window, document, fetch, console, Intl, URLSearchParams, setTimeout, Option: class { constructor(text, value) { this.text = text; this.value = value; } } });
    vm.runInContext(js('Quarantine/quarantine-ui.js'), context);
    vm.runInContext(js(`Quarantine/${script}`), context);
    await listeners.DOMContentLoaded?.();
    await new Promise((resolve) => setImmediate(resolve));
    return { els, byId, seen, L, ui: window.QuarantineUi };
};

// ── AC-25 — the queue ───────────────────────────────────────────────────────────────────────────────────────────────

const queueRow = (extra) => ({ holdId: 'h1', holdNumber: 'GMG-QH-000001', status: 'Open', reviewState: 'Waiting', activeDecision: null, late: true, waitDays: 12, targetDays: 5, ...extra });
const queueAction = (p, row) => { p.seen.list.config.columnDefs.find((d) => d.targets === -1).render(null, 'display', row); return p.seen.actions; };
const statusCell = (p, row) => p.seen.list.config.columnDefs.find((d) => d.targets === 10).render(null, 'display', row);
const createdRow = (p, row) => { const tr = element('tr'); p.seen.list.config.createdRow(tr, row); return tr; };

for (const lang of ['tr', 'en', 'ar']) {
    test(`AC-25 (${lang}) a Voided row shows "${words(lang)['Quarantine.HoldState.Voided']}", no Karar Ver, no late colour`, async () => {
        const p = await page('queue.js', { lang, rootSelector: '.datatables-quarantine-holds' });
        const row = queueRow({ status: 'Voided', voided: voided() });
        const cell = statusCell(p, row);
        assert.ok(cell.includes('data-qr-hold-state="Voided"'), cell);
        assert.equal(visible(cell), p.L['Quarantine.HoldState.Voided']);
        const actions = queueAction(p, row);
        assert.equal(actions.find((a) => a.key === 'DECIDE').visible, false, 'Karar Ver is drawn on a Voided row');
        assert.equal(actions.find((a) => a.key === 'VIEWHOLD').visible, true);
        assert.equal(createdRow(p, row).style.backgroundColor, undefined, 'a Voided row is painted late');
    });
}

test('AC-25 control: an open late row keeps Karar Ver and the late colour; a Decided row has neither', async () => {
    const p = await page('queue.js', { rootSelector: '.datatables-quarantine-holds' });
    assert.equal(queueAction(p, queueRow()).find((a) => a.key === 'DECIDE').visible, true);
    assert.equal(createdRow(p, queueRow()).style.backgroundColor, '#fff9ee');
    assert.equal(queueAction(p, queueRow({ status: 'Decided' })).find((a) => a.key === 'DECIDE').visible, false);
    assert.equal(visible(statusCell(p, queueRow({ status: 'Decided' }))), p.L['Quarantine.HoldState.Decided']);
});

// ── AC-26 — the detail ──────────────────────────────────────────────────────────────────────────────────────────────

const DETAIL_IDS = { 'qr-details': 'shown', 'qr-d-decide': 'hidden', 'qr-d-review': 'hidden', 'qr-d-note-edit': 'hidden', 'qr-d-note-locked': 'hidden', 'qr-d-late': 'hidden',
    'qr-d-voided': 'hidden', 'qr-d-conflict': 'hidden', 'qr-d-lot': 'hidden', 'qr-wf-refresh': 'hidden', 'qr-wf-submit': 'hidden', 'qr-wf-withdraw': 'hidden', 'qr-wf-reapply': 'hidden' };
const detailHold = (extra) => ({ holdId: 'h1', holdNumber: 'GMG-QH-000001', status: 'Open', reviewState: 'Waiting', activeDecision: null, late: false, waitDays: 1, targetDays: 5,
    quantity: 4, baseUom: { code: 'EA' }, source: 'Receipt', history: [], movements: [], decisions: [], ...extra });
const detail = async (hold, decision = null, lang = 'tr') => {
    const p = await page('details.js', { lang, json: { hold, decision, me: USER, etag: 'W/"3"' }, ids: DETAIL_IDS });
    return p;
};
const BUTTONS = ['qr-d-decide', 'qr-d-review', 'qr-wf-withdraw', 'qr-wf-reapply', 'qr-wf-refresh', 'qr-wf-submit', 'qr-d-note-edit'];

test('AC-26 control: an open Waiting hold offers Karar Ver and İncelemeye al; its decision PendingApproval offers withdraw', async () => {
    const p = await detail(detailHold());
    assert.ok(shown(p.byId('qr-d-decide')) && shown(p.byId('qr-d-review')));
    const pending = await detail(detailHold({ status: 'DecisionPending', activeDecision: { decisionId: 'd1', state: 'PendingApproval' } }),
        { decisionId: 'd1', decisionNumber: 'GMG-QD-000001', kind: 'HoldDecision', state: 'PendingApproval', decider: { userId: USER } });
    assert.ok(shown(pending.byId('qr-wf-withdraw')), 'the control decision offers withdraw');
});

for (const lang of ['tr', 'en', 'ar']) {
    test(`AC-26 (${lang}) a Voided hold: the banner names the REVERSAL, its date and its person; no Karar Ver / İncelemeye al / withdraw / re-apply`, async () => {
        const decision = { decisionId: 'd1', decisionNumber: 'GMG-QD-000001', kind: 'HoldDecision', state: 'Voided', decider: { userId: USER } };
        const p = await detail(detailHold({ status: 'Voided', voided: voided({ decisionId: 'd1' }) }), decision, lang);
        for (const id of BUTTONS) assert.equal(shown(p.byId(id)), false, `${id} is drawn on a Voided hold`);
        assert.ok(shown(p.byId('qr-d-voided')), 'no "Geçersiz" banner');
        const banner = visible(p.byId('qr-d-voided').querySelector('span').innerHTML);
        const date = new Date('2026-11-04T09:30:00Z').toLocaleDateString(lang, { year: 'numeric', month: '2-digit', day: '2-digit' });
        assert.equal(banner, p.L['Quarantine.Details.VoidedBanner'].replace('{0}', 'INV-REV-000042').replace('{1}', date).replace('{2}', 'Zehra Yıldız'));
        assert.equal(visible(p.byId('qr-d-state').innerHTML), p.L['Quarantine.HoldState.Voided']);
        assert.equal(visible(p.byId('qr-wf-state').innerHTML), p.L['Quarantine.Workflow.Voided']);
        assert.equal(shown(p.byId('qr-d-conflict')), false);
    });
}

test('AC-26 a Voided hold that still carries a Waiting review and no decision draws no Karar Ver / İncelemeye al (the open predicate, not the decision)', async () => {
    const p = await detail(detailHold({ status: 'Voided', reviewState: 'Waiting', activeDecision: null, voided: voided() }));
    assert.equal(shown(p.byId('qr-d-decide')), false);
    assert.equal(shown(p.byId('qr-d-review')), false);
    assert.equal(shown(p.byId('qr-d-note-edit')), false);
});

test('AC-26 a Voided hold whose last decision reads ApplyFailed / PendingApproval / Draft offers no workflow button', async () => {
    for (const state of ['ApplyFailed', 'PendingApproval', 'Draft', 'Submitted']) {
        const p = await detail(detailHold({ status: 'Voided', voided: voided() }), { decisionId: 'd1', decisionNumber: 'GMG-QD-000001', kind: 'HoldDecision', state, decider: { userId: USER } });
        for (const id of ['qr-wf-withdraw', 'qr-wf-reapply', 'qr-wf-refresh', 'qr-wf-submit']) assert.equal(shown(p.byId(id)), false, `${state}: ${id}`);
    }
});

test('AC-26 the history shows the two new labels and the REVERSAL row is drawn in the movements list', async () => {
    const p = await detail(detailHold({
        status: 'Voided', voided: voided(),
        history: [{ action: 'Quarantined', atUtc: '2026-11-01T08:00:00Z', by: { userId: USER } },
            { action: 'SourceReversed', atUtc: '2026-11-04T09:30:00Z', by: { userId: USER }, reversalTransactionId: '11111111-2222-4333-8444-555555555555' },
            { action: 'SourceReversalConflict', atUtc: '2026-11-04T09:31:00Z', by: { userId: USER } }],
        movements: [{ transactionNumber: 'INV-GR-000001', movementType: 'GOODS_RECEIPT_PO', atUtc: '2026-11-01T08:00:00Z', quantity: 4, toStockStatus: 'QUARANTINE' },
            { transactionNumber: 'INV-REV-000042', movementType: 'REVERSAL', atUtc: '2026-11-04T09:30:00Z', quantity: -4, fromStockStatus: 'QUARANTINE' }]
    }));
    const history = visible(p.byId('qr-d-history').innerHTML);
    assert.ok(history.includes(p.L['Quarantine.History.SourceReversed']), history);
    assert.ok(history.includes(p.L['Quarantine.History.SourceReversalConflict']), history);
    assert.ok(!history.includes('SourceReversed'), 'a raw action name is shown');
    const moves = p.byId('qr-details').querySelector('#qr-d-moves tbody').innerHTML;
    assert.ok(moves.includes('INV-REV-000042') && visible(moves).includes(p.L['Quarantine.MovementType.REVERSAL']), moves);
});

test('AC-26 the banner is text: a hostile REVERSAL number or name is escaped', async () => {
    const p = await detail(detailHold({ status: 'Voided', voided: voided({ reversalTransactionNumber: '<img src=x onerror=alert(1)>', reversedBy: { userId: USER, name: '<b>x</b>' } }) }));
    const html = p.byId('qr-d-voided').querySelector('span').innerHTML;
    assert.equal(/<img|<b>/i.test(html), false, html);
});

// ── AC-27 — Bloke Stok ──────────────────────────────────────────────────────────────────────────────────────────────

const blockedRow = (extra) => ({ blockedId: 'b1', blockedNumber: 'GMG-BLK-000001', status: 'Open', reviewed: true, activeDecision: null, allowedActions: ['Dispose:Destroy', 'Unblock'], origin: 'OtherInbound', ...extra });
const blockedPage = () => page('blocked.js', { rootSelector: '.datatables-quarantine-blocked', ids: { 'qr-block-modal': 'hidden' } });
const blockedActions = (p, row) => { p.seen.list.config.columnDefs.find((d) => d.targets === -1).render(null, 'display', row); return p.seen.actions.filter((a) => a.visible).map((a) => a.key).join(','); };
const pending = (p, row) => p.seen.list.config.columnDefs.find((d) => d.targets === 7).render(null, 'display', row);

// The server answers a Voided record with allowedActions [] (QuarantineMapping.AllowedActions: only an Open record without a decision has
// actions) — so Disposition, which ALSO follows allowedActions, cannot be the measure; Blokeyi Kaldır and İncelemeyi tamamla follow only the
// page's own `open` rule (blocked.js :275, :280, :281). CT decision 2026-11-03 (son bakış 02): the expected red of the :275 sabotage is those two.
test('AC-27 a Voided blocked row shows its status badge and offers no action (Disposition / Blokeyi Kaldır / İncelemeyi tamamla)', async () => {
    const p = await blockedPage();
    const drawn = [];
    for (const reviewed of [true, false]) {
        const row = blockedRow({ status: 'Voided', reviewed, allowedActions: [], voided: voided() });
        const cell = pending(p, row);
        assert.ok(cell.includes('data-qr-blocked-status="Voided"'), cell);
        assert.equal(visible(cell), p.L['Quarantine.Blocked.Status.Voided']);
        drawn.push(`reviewed=${reviewed}: [${blockedActions(p, row)}]`);
    }
    assert.equal(drawn.join(' · '), 'reviewed=true: [] · reviewed=false: []', 'actions drawn on a Voided row');
});

test('AC-27 control: an Open reviewed row offers Disposition and Blokeyi Kaldır; an Open unreviewed row offers Blokeyi Kaldır (disabled) and İncelemeyi tamamla', async () => {
    const p = await blockedPage();
    assert.equal(blockedActions(p, blockedRow()), 'DISPOSE,UNBLOCK');
    assert.equal(blockedActions(p, blockedRow({ reviewed: false, allowedActions: [] })), 'UNBLOCK,COMPLETEREVIEW');
    assert.equal(pending(p, blockedRow()).includes('data-qr-blocked-status'), false);
});

// ── AC-28 — the conflict badge, whatever the status ─────────────────────────────────────────────────────────────────

test('AC-28 the badge + cause text on every status: Decided hold, Open / DecisionPending hold, Voided hold (LinesPosted, LotChanged)', async () => {
    const p = await page('queue.js', { rootSelector: '.datatables-quarantine-holds' });
    const rows = [
        queueRow({ status: 'Decided', sourceReversalConflict: mark('Decided') }),
        queueRow({ status: 'Open', sourceReversalConflict: mark('OwnMovementReversed') }),
        queueRow({ status: 'DecisionPending', activeDecision: { state: 'ApplyFailed' }, sourceReversalConflict: mark('LinesPosted') }),
        queueRow({ status: 'Voided', voided: voided(), sourceReversalConflict: mark('LinesPosted') }),
        queueRow({ status: 'Voided', voided: voided(), sourceReversalConflict: mark('LotChanged') })
    ];
    for (const row of rows) {
        const cell = statusCell(p, row);
        assert.ok(cell.includes(`data-qr-conflict="${row.sourceReversalConflict.cause}"`), `${row.status}: no conflict badge — ${cell}`);
        assert.ok(visible(cell).includes(p.L['Quarantine.Common.ConflictBadge']), cell);
        assert.ok(visible(cell).includes(p.L[`Quarantine.Conflict.Cause.${row.sourceReversalConflict.cause}`]), cell);
    }
    assert.equal(statusCell(p, queueRow()).includes('data-qr-conflict'), false, 'an unmarked row draws a badge');
});

test('AC-28 the badge on Bloke Stok: Closed (Decided), Open (QuantityChanged, OwnMovementReversed), Voided (LinesPosted)', async () => {
    const p = await blockedPage();
    for (const [status, cause] of [['Closed', 'Decided'], ['SampleRetained', 'Decided'], ['Open', 'QuantityChanged'], ['Open', 'OwnMovementReversed'], ['Voided', 'LinesPosted']]) {
        const cell = pending(p, blockedRow({ status, sourceReversalConflict: mark(cause) }));
        assert.ok(cell.includes(`data-qr-conflict="${cause}"`), `${status}/${cause}: ${cell}`);
        assert.ok(visible(cell).includes(p.L[`Quarantine.Conflict.Cause.${cause}`]), cell);
    }
});

test('AC-28 the detail of a Voided hold with a mark shows BOTH the "Geçersiz" banner and the conflict badge + cause; five causes have five texts', async () => {
    const p = await detail(detailHold({ status: 'Voided', voided: voided(), sourceReversalConflict: mark('LotChanged') }));
    assert.ok(shown(p.byId('qr-d-voided')) && shown(p.byId('qr-d-conflict')));
    const badge = p.byId('qr-d-conflict').querySelector('[data-qr-conflict-badge]').innerHTML;
    assert.ok(badge.includes('data-qr-conflict="LotChanged"'), badge);
    assert.ok(visible(badge).includes(p.L['Quarantine.Conflict.Cause.LotChanged']), badge);
    assert.ok(visible(p.byId('qr-d-conflict').querySelector('[data-qr-conflict-text]').innerHTML).includes('INV-REV-000042'));

    const open = await detail(detailHold({ status: 'Open', sourceReversalConflict: mark('OwnMovementReversed') }));
    assert.ok(shown(open.byId('qr-d-conflict')) && !shown(open.byId('qr-d-voided')));
    assert.ok(shown(open.byId('qr-d-decide')), 'a marked OPEN hold stays open on the page (no close action, §8.3)');

    const texts = CAUSES.map((c) => p.L[`Quarantine.Conflict.Cause.${c}`]);
    assert.equal(new Set(texts).size, 5);
    texts.forEach((x, i) => assert.ok(x && !x.includes('Quarantine.'), CAUSES[i]));
});

// ── §11 Karar Ver: the "closed" message names the Voided state ─────────────────────────────────────────────────────

test('§11 Karar Ver: a Voided hold says it is closed, with the state name, and disables the form', async () => {
    const DECIDE_IDS = { 'qr-decide': 'shown', 'qr-decide-form': 'shown', 'qr-page-error': 'hidden', 'qr-foureyes': 'hidden', 'qr-partial': 'shown', 'qr-reason': 'shown' };
    const p = await page('decide.js', { json: { hold: detailHold({ status: 'Voided', voided: voided() }), settings: {} }, ids: DECIDE_IDS });
    const box = p.byId('qr-page-error');
    assert.ok(shown(box), 'no closed message');
    assert.equal(box.textContent, p.L['Quarantine.Decide.Closed'].replace('{0}', p.L['Quarantine.HoldState.Voided']));
});
