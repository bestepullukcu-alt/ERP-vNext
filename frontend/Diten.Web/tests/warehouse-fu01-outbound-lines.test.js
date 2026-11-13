// MOD-0178-FU01 DL-1 (FU01 pack §8.2, §8.3, §16 AC-1…AC-13; WP-0178-FU01-DL1) — the outbound Create / Edit lines as a TABLE and a side PANEL,
// measured on the PRODUCTION files: rules.js + form.js (+ details.js for the Details reason column) run in jsdom, the panel markup is the real
// _LineOffcanvas.cshtml with its Razor words replaced by the real tr page resx, the service answers are stubbed.
const fs = require('fs');
const path = require('path');
const { loadScript } = require('./load-script');

const root = path.resolve(__dirname, '..');
const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));
const settle = async () => { for (let i = 0; i < 8; i++) await flush(); };
const JS = (file) => `wwwroot/assets/js/Warehouse/OutboundShipments/${file}`;
const VIEW = (file) => `Views/Warehouse/OutboundShipments/${file}`;
const LANGS = ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru'];

const unxml = (text) => text.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');
const resx = (relative) => Object.fromEntries(Array.from(read(relative).matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)).map((m) => [m[1], unxml(m[2])]));
const pageWords = (lang) => resx(`Resources/Views/Warehouse/OutboundShipments/OutboundShipmentsIndex.${lang}.resx`);
const WORDS = pageWords('tr');
const fmt = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value), template);

/** A Razor partial → the HTML the browser gets (its own words from the real tr resx; the shared ones as their key). */
const razor = (source) => source
    .replace(/@\*[\s\S]*?\*@/g, '')
    .replace(/^@(using|inject|model)[^\n]*\n/gm, '')
    // `.Value` (raw template, CT live tour 2026-11-13) renders the same words as the bare form
    .replace(/@(Shared)?Localizer\["([^"]+)"\](?:\.Value)?/g, (_, shared, key) => (shared ? key : (WORDS[key] ?? key)));

const PANEL = () => razor(read(VIEW('_LineOffcanvas.cshtml')));

const ITEM = '33333333-3333-4333-8333-333333333333';
const SKU = 'Sku:44444444-4444-4444-8444-444444444444';
const LOT_A = 'aaaaaaaa-0000-4000-8000-000000000001';
const LOT_B = 'bbbbbbbb-0000-4000-8000-000000000002';
const LOTS = [{ lotId: LOT_A, lotNumber: 'AMX-2604', expiryDate: '2028-03-31', fefoRank: 1, allocatable: '408.500' },
    { lotId: LOT_B, lotNumber: 'G1-20261020', expiryDate: '2028-10-20', fefoRank: 2, allocatable: '12.000' }];

/** The page around the lines card (ids as _Form.cshtml draws them — pinned against the real view below) + the REAL panel. */
const createDom = () => `<form id="outboundForm" data-form-mode="create">
    <div id="outbound-form-alert" class="d-none"></div>
    <select id="WarehouseId"><option value="11111111-1111-4111-8111-111111111111" selected>D01</option></select><div id="WarehouseIdError"></div>
    <select id="StagingLocationId"><option value="" selected></option><option value="22222222-2222-4222-8222-222222222222">A-01-01-01</option></select><div id="StagingLocationIdError"></div>
    <input id="OrderRef"><textarea id="Note"></textarea><input id="ShipToName" value="Klinik"><input id="ShipToCountry" value="tr"><input id="ShipToCity">
    <input id="ShipToPostalCode"><input id="ShipToLine1"><input id="ShipToLine2"><div id="ShipToError"></div>
    <button type="button" id="btnAddLine"></button><button type="button" id="btnPreview"></button><small id="outbound-lines-limit" class="d-none"></small>
    <table id="outbound-lines-table" data-dt-standard="v2" data-dt-data-mode="client"><thead><tr>${'<th></th>'.repeat(8)}</tr></thead><tbody id="outbound-lines-body"></tbody></table>
    <div id="LinesError"></div><input name="__RequestVerificationToken" value="tok"></form>${PANEL()}`;

const reply = (json, status = 200) => ({ ok: status < 400, status, json: async () => json });
const posts = (fetchMock, part) => fetchMock.mock.calls.filter(([url, init]) => String(url).includes(part) && init?.method === 'POST').map(([, init]) => JSON.parse(init.body));
const byId = (id) => document.getElementById(id);
const text = (id) => byId(id).textContent;

/** One answer line of POST /preview (lineSeq = the request position). */
const answerLine = (lineSeq, { chosen, allocatable = '408.500', lots = LOTS, failureCode = null } = {}) => ({
    lineSeq, orderLineId: String(lineSeq), requested: '2.000', allocated: '2.000', allocatable, failureCode,
    allocations: [{ lotId: chosen || LOT_A, lotNumber: chosen === LOT_B ? 'G1-20261020' : 'AMX-2604', fefoRank: chosen === LOT_B ? 2 : 1, quantity: '2.000', plannedBins: [] }],
    lots,
    deviation: chosen === LOT_B ? { firstEligibleLotId: LOT_A, firstEligibleLotNumber: 'AMX-2604', firstEligibleExpiry: '2028-03-31', mode: 'RequireReason' } : null
});

/** The default service: sku-facts EA; preview answers each line, choosing the lot the line asked for. */
const service = (mode = 'RequireReason', overrides = {}) => (url, init) => {
    if (String(url).includes('/sku-facts')) return reply({ available: true, baseUom: 'EA', lotControlled: true });
    if (String(url).includes('/preview') && overrides.preview) return overrides.preview(JSON.parse(init.body));
    if (String(url).includes('/preview')) {
        const body = JSON.parse(init.body);
        return reply({ fefoOutboundMode: mode, lines: body.lines.map((l, i) => answerLine(i + 1, { chosen: l.lotOverride?.lotId })) });
    }
    if (String(url).includes('/create') && overrides.create) return overrides.create(JSON.parse(init.body));
    if (String(url).includes('/create')) return reply({ id: 'f70b63ed-58d2-4577-9211-8b34a2c132a0', message: 'ok' });
    return reply({}, 404);
};

const offcanvasStub = () => ({
    Offcanvas: { getOrCreateInstance: (el) => ({ show: () => el.classList.add('show'), hide: () => el.classList.remove('show') }) }
});

async function boot(scripts, fetchImpl, { culture = 'tr', jquery } = {}) {
    window.CurrentLanguage = culture;
    window.L10n = { Words: WORDS, Actions: 'İşlemler', Close: 'Kapat' };
    window.DitenDataTable = { getAuthHeaders: () => ({}) };
    window.Permissions = { has: () => true };
    window.bootstrap = offcanvasStub();
    window.showToast = vi.fn();
    window.showConfirm = vi.fn();
    if (jquery) window.jQuery = jquery;
    const fetchMock = vi.fn(fetchImpl);
    vi.stubGlobal('fetch', fetchMock);
    const starts = [];
    const add = document.addEventListener;
    document.addEventListener = (type, fn, o) => (type === 'DOMContentLoaded' ? starts.push(fn) : add.call(document, type, fn, o));
    try { scripts.forEach((s) => new Function(read(s))()); } finally { document.addEventListener = add; }
    starts.forEach((start) => start());
    await settle();
    return fetchMock;
}

const bootCreate = (fetchImpl = service(), options) => {
    document.body.innerHTML = createDom();
    return boot([JS('rules.js'), JS('form.js')], fetchImpl, options);
};

const choose = (id, value, label) => {
    const el = byId(id);
    el.add(new Option(label, value, true, true));
    el.dispatchEvent(new Event('change', { bubbles: true }));
};
const type = (id, value) => { const el = byId(id); el.value = value; el.dispatchEvent(new Event('input', { bubbles: true })); };

/** Fills the open panel (product → SKU → its base unit → quantity). */
const fillPanel = async ({ qty = '2', orderLine } = {}) => {
    choose('lineProduct', ITEM, 'Amoksisilin 500 mg — AMX');
    choose('lineSku', SKU, 'LSKU-TR-000045');
    await settle();
    type('lineQuantity', qty);
    if (orderLine !== undefined) type('lineOrderLine', orderLine);
};

/** "Satır ekle" → fill → "Satırı kaydet" (the panel path). */
const addLine = async (options) => {
    byId('btnAddLine').click();
    await fillPanel(options);
    byId('btnLineSave').click();
    await settle();
};

/** A line with the second lot (G1-20261020) and a reason, saved through the panel. */
const addLineWithLotB = async (reason = 'Müşteri bu lotu istedi') => {
    byId('btnAddLine').click();
    await fillPanel();
    byId('btnLinePreview').click();
    await settle();
    const lot = byId('lineLot');
    lot.value = LOT_B;
    lot.dispatchEvent(new Event('change'));
    await settle();
    type('lineReason', reason);
    byId('btnLineSave').click();
    await settle();
};

const rows = () => Array.from(document.querySelectorAll('#outbound-lines-body tr[data-line-row]'));
const cells = (row) => Array.from(row.querySelectorAll('td')).map((td) => td.textContent.trim());
const status = (row) => row.querySelector('[data-line-status]')?.dataset.lineStatus;

describe('MOD-0178-FU01 DL-1 — the outbound lines table and the line panel', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        ['L10n', 'Permissions', 'DitenDataTable', 'showToast', 'showConfirm', 'CurrentLanguage', 'OutboundRules', 'bootstrap', 'jQuery'].forEach((k) => delete window[k]);
        document.body.innerHTML = '';
    });

    // ── AC-1 ────────────────────────────────────────────────────────────────────────────────────────
    describe('AC-1: the lines are a DataTable v2 table in client mode', () => {
        it('_Form.cshtml declares #outbound-lines-table with data-dt-standard="v2" and data-dt-data-mode="client", columns in the §8.2 order', () => {
            const form = read(VIEW('_Form.cshtml'));
            const tag = /<table id="outbound-lines-table"[^>]*>/.exec(form)?.[0];
            expect(tag, 'the lines table is not on the form').toBeTruthy();
            expect(tag).toContain('data-dt-standard="v2"');
            expect(tag).toContain('data-dt-data-mode="client"');
            const head = form.slice(form.indexOf(tag), form.indexOf('</thead>', form.indexOf(tag)));
            expect(Array.from(head.matchAll(/Localizer\["([^"]+)"\]/g)).map((m) => m[1])).toEqual(
                ['LineOrderLine', 'LineProduct', 'LineSku', 'LineQuantity', 'LinesTable.Lot', 'AllocQuantity', 'LinesTable.Allocatable', 'AllocRelease', 'LinesTable.Status', 'Actions']);
        });

        it('the card list of FIX-01 is gone from the view and the script (no lineRow, no data-line inputs)', () => {
            expect(read(VIEW('_Form.cshtml'))).not.toMatch(/id="outbound-lines"|outbound-lines-readonly|outbound-preview-lines/);
            expect(read(JS('form.js'))).not.toMatch(/lineRow|data-line="|data-field="/);
        });

        it('the fixture ids are the real view\'s — the form and the panel draw every id the script reads', () => {
            const views = read(VIEW('_Form.cshtml')) + read(VIEW('_LineOffcanvas.cshtml'));
            ['outbound-lines-table', 'outbound-lines-body', 'btnAddLine', 'btnPreview', 'outbound-lines-limit', 'LinesError', 'offcanvasOutboundLine', 'lineProduct', 'lineSku',
                'lineQuantity', 'lineUom', 'lineOrderLine', 'lineLot', 'lineReasonBlock', 'lineReason', 'lineReasonHint', 'linePreview', 'linePreviewBody', 'lineAlert',
                'btnLinePreview', 'btnLineSave', 'offcanvasOutboundLineLabel'].forEach((id) => expect(views, id).toContain(`id="${id}"`));
        });

        it('a saved line is a table row: order line, product, SKU, quantity + unit, lot, allocatable, status, menu — no GUID on screen', async () => {
            await bootCreate();
            expect(byId('outbound-lines-body').textContent).toContain(WORDS['LinesTable.Empty']);
            await addLine();
            const [row] = rows();
            expect(cells(row).slice(0, 7)).toEqual(['1', 'Amoksisilin 500 mg — AMX', 'LSKU-TR-000045', '2 EA', WORDS.LineFefoFirst, '-', WORDS['LineStatus.NotPreviewed']]);
            expect(row.querySelector('[data-line-menu]')).not.toBeNull();
            expect(byId('outbound-lines-table').textContent).not.toMatch(/[0-9a-f]{8}-[0-9a-f]{4}-/i);
        });
    });

    // ── AC-2 ────────────────────────────────────────────────────────────────────────────────────────
    describe('AC-2: the panel — 6 fields with their icon, Enter is "Satırı kaydet"', () => {
        it('six .diten-field wrappers, each with an icon the icon set has; the primary is data-dt-primary="enter"; a partial sets no layout', () => {
            const source = read(VIEW('_LineOffcanvas.cshtml'));
            const icons = read('wwwroot/assets/vendor/fonts/iconify-icons.css');
            const fields = Array.from(source.matchAll(/<div class="diten-field">\s*<i class="bx (bx-[a-z-]+) diten-field-icon[^"]*" aria-hidden="true"><\/i>\s*<(?:select|input|textarea)[^>]*id="(\w+)"/g));
            expect(fields.map((m) => m[2])).toEqual(['lineProduct', 'lineSku', 'lineQuantity', 'lineOrderLine', 'lineLot', 'lineReason']);
            fields.forEach((m) => expect(icons, m[1]).toMatch(new RegExp(`\\.${m[1]}\\b`)));
            expect(source).toMatch(/<button data-dt-primary="enter" type="button" id="btnLineSave" class="btn btn-primary/);
            expect(source).not.toMatch(/\bLayout\s*=/);
        });

        it('Enter in the quantity field clicks "Satırı kaydet" (the shared BL-586 script) and the line lands in the table', async () => {
            loadScript('wwwroot/assets/js/shared/diten-enter-primary.js');
            await bootCreate();
            byId('btnAddLine').click();
            await fillPanel();
            expect(byId('offcanvasOutboundLine').classList.contains('show')).toBe(true);
            const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
            byId('lineQuantity').dispatchEvent(enter);
            await settle();
            expect(enter.defaultPrevented).toBe(true);
            expect(rows()).toHaveLength(1);
            expect(byId('offcanvasOutboundLine').classList.contains('show')).toBe(false);
        });
    });

    // ── AC-3 ────────────────────────────────────────────────────────────────────────────────────────
    it('AC-3: a new product empties the SKU and its unit and redraws select2; both lists open inside the panel (dropdownParent)', async () => {
        const calls = [];
        const $ = (el) => {
            const api = {
                el,
                on: (event, fn) => { el.addEventListener(event, fn); return api; },
                trigger: (event) => { calls.push([el.id, 'trigger', event]); if (event === 'change') el.dispatchEvent(new Event('change')); return api; },
                val: (v) => { if (v === null) el.value = ''; return api; },
                data: () => '',
                select2: (options) => { calls.push([el.id, 'select2', options]); return api; }
            };
            return api;
        };
        $.fn = { select2: () => {} };
        await bootCreate(service(), { jquery: $ });
        const panel = byId('offcanvasOutboundLine');
        ['lineProduct', 'lineSku'].forEach((id) => {
            const options = calls.find(([el, what]) => el === id && what === 'select2')[2];
            expect(options.dropdownParent?.el, `${id} opens outside the panel`).toBe(panel);
        });

        byId('btnAddLine').click();
        await fillPanel();
        expect(byId('lineSku').value).toBe(SKU);
        expect(text('lineUom')).toBe(fmt(WORDS['LinePanel.Unit'], 'EA'));
        calls.length = 0;
        choose('lineProduct', '55555555-5555-4555-8555-555555555555', 'Parasetamol — PRC');
        expect(byId('lineSku').value).toBe('');
        expect(text('lineUom')).toBe('-');
        expect(calls).toContainEqual(['lineSku', 'trigger', 'change.select2']);
    });

    // ── AC-4 ────────────────────────────────────────────────────────────────────────────────────────
    it('AC-4: at 100 lines "Satır ekle" is disabled with its explanation, and the 101st line is added by no path (button, panel Enter)', async () => {
        await bootCreate();
        for (let i = 1; i <= 99; i++) await addLine();
        expect(rows()).toHaveLength(99);
        expect(byId('btnAddLine').disabled).toBe(false);

        // the 100th, then Enter again on the closed panel — still 100
        byId('btnAddLine').click();
        await fillPanel();
        byId('btnLineSave').click();
        byId('btnLineSave').click();
        await settle();
        expect(rows()).toHaveLength(100);
        expect(byId('btnAddLine').disabled).toBe(true);
        expect(byId('outbound-lines-limit').classList.contains('d-none')).toBe(false);

        // the button forced back on: the page refuses by itself and opens no panel
        byId('btnAddLine').disabled = false;
        byId('btnAddLine').click();
        expect(byId('offcanvasOutboundLine').classList.contains('show')).toBe(false);
        expect(text('LinesError')).toBe(WORDS.LineLimitReached);
        expect(window.OutboundRules.canAddLine(99)).toBe(true);
        expect(window.OutboundRules.canAddLine(100)).toBe(false);
    }, 120000); // a hundred lines through the real panel

    // ── AC-5 ────────────────────────────────────────────────────────────────────────────────────────
    it('AC-5 (D-4): the panel previews THIS line with the chosen lot (lotOverride) — the allocation from that lot, the FEFO suggestion beside it, the mode', async () => {
        const fetchMock = await bootCreate();
        await addLine(); // another line already on the table: the panel still sends its own line only
        byId('btnAddLine').click();
        await fillPanel();
        byId('btnLinePreview').click();
        await settle();
        const first = posts(fetchMock, '/preview')[0];
        expect(first.lines).toHaveLength(1);
        expect(first.lines[0].lotOverride).toBeUndefined();
        expect(byId('lineLot').options[0].textContent).toBe(fmt(WORDS['LinePanel.LotFefo'], 'AMX-2604'));
        expect(text('linePreview')).toContain(WORDS['LinePanel.PreviewNote']);

        const lot = byId('lineLot');
        lot.value = LOT_B;
        lot.dispatchEvent(new Event('change'));
        await settle();
        const second = posts(fetchMock, '/preview')[1];
        expect(second.lines).toHaveLength(1);
        expect(second.lines[0].lotOverride).toEqual({ lotId: LOT_B, fefoBypassReason: null });
        const shown = text('linePreviewBody');
        expect(shown).toContain(fmt(WORDS.PreviewAllocation, 'G1-20261020', '2', '2'));
        expect(shown).toContain(fmt(WORDS['LinePanel.FefoSuggestion'], 'AMX-2604', '2028-03-31'));
        expect(shown).toContain(fmt(WORDS.PreviewChosenLot, 'AMX-2604', '2028-03-31'));
        expect(shown).toContain(fmt(WORDS['LinePanel.ModeLabel'], WORDS['LinePanel.Mode.RequireReason']));
        expect(byId('lineReasonBlock').classList.contains('d-none')).toBe(false);
    });

    // ── AC-6 ────────────────────────────────────────────────────────────────────────────────────────
    it('AC-6: "Tahsisi önizle" sends every line with its lotOverride in ONE request; each row fills from ITS answer line; the chosen lot shows', async () => {
        const fetchMock = await bootCreate(service('RequireReason', {
            preview: (body) => reply({ fefoOutboundMode: 'RequireReason', lines: body.lines.map((l, i) => answerLine(i + 1, { chosen: l.lotOverride?.lotId, allocatable: i === 0 ? '408.500' : '12.000' })) })
        }));
        await addLine();
        await addLineWithLotB();
        const before = posts(fetchMock, '/preview').length;
        byId('btnPreview').click();
        await settle();
        const all = posts(fetchMock, '/preview');
        expect(all.length - before).toBe(1);
        const body = all[all.length - 1];
        expect(body.lines.map((l) => l.orderLineId)).toEqual(['1', '2']);
        expect(body.lines[0].lotOverride).toBeUndefined();
        expect(body.lines[1].lotOverride).toEqual({ lotId: LOT_B, fefoBypassReason: 'Müşteri bu lotu istedi' });

        const [one, two] = rows();
        expect(cells(one)[5]).toBe('408,5');
        expect(cells(two)[5]).toBe('12');
        expect(one.querySelector('[data-line-lot]').textContent).toBe(fmt(WORDS['LinesTable.LotFefo'], 'AMX-2604'));
        expect(two.querySelector('[data-line-lot]').textContent).toBe('G1-20261020');
        expect(status(one)).toBe('Fulfillable');
        expect(status(two)).toBe('DeviationReasonGiven');
    });

    // ── AC-7 ────────────────────────────────────────────────────────────────────────────────────────
    describe('AC-7: the FEFO mode decides on the client; a FEFO refusal lands on its line', () => {
        it('RequireReason: a reason under 10 characters is refused, 10+ saves the line', async () => {
            await bootCreate(service('RequireReason'));
            await addLineWithLotB('kısa');
            expect(rows()).toHaveLength(0);
            expect(text('lineAlert')).toBe(WORDS.DeviationNeedsReason);
            type('lineReason', 'Müşteri bu lotu istedi');
            byId('btnLineSave').click();
            expect(rows()).toHaveLength(1);
        });

        it('Warn: the line with a lot change saves without a reason', async () => {
            await bootCreate(service('Warn'));
            await addLineWithLotB('');
            expect(rows()).toHaveLength(1);
            expect(status(rows()[0])).toBe('Fulfillable');
        });

        it('Block: "Satırı kaydet" is disabled with the reason, and the line is not saved', async () => {
            await bootCreate(service('Block'));
            await addLineWithLotB('Müşteri bu lotu istedi');
            expect(byId('btnLineSave').disabled).toBe(true);
            expect(text('lineReasonHint')).toBe(WORDS.DeviationBlocked);
            expect(rows()).toHaveLength(0);
        });

        it('a 422 FEFO_ORDER_VIOLATION with details.lineSeq marks THAT line; the others stay "Önizlenmedi"', async () => {
            const fetchMock = await bootCreate();
            await addLine();
            await addLine();
            fetchMock.mockImplementation((url, init) => (String(url).includes('/preview')
                ? reply({ error: { code: 'FEFO_ORDER_VIOLATION', message: 'FEFO sırası ihlali', details: { lineSeq: 2, firstEligibleLotNumber: 'AMX-2604' } } }, 422)
                : service()(url, init)));
            byId('btnPreview').click();
            await settle();
            const [one, two] = rows();
            expect(status(one)).toBe('NotPreviewed');
            expect(status(two)).toBe('Unfulfillable');
            expect(two.querySelector('[data-line-failure]').textContent).toBe('FEFO sırası ihlali');
            expect(text('outbound-form-alert')).toBe('FEFO sırası ihlali');
        });
    });

    // ── AC-8 ────────────────────────────────────────────────────────────────────────────────────────
    describe('AC-8: a preview goes out of date; the chosen lot is never reset silently', () => {
        it('a warehouse change makes every previewed line "Önizleme eski"; a stale lot change is not saved ("yeniden önizleyin")', async () => {
            const fetchMock = await bootCreate();
            await addLine();
            await addLineWithLotB();
            byId('btnPreview').click();
            await settle();
            expect(rows().map(status)).toEqual(['Fulfillable', 'DeviationReasonGiven']);

            byId('WarehouseId').dispatchEvent(new Event('change'));
            expect(rows().map(status)).toEqual(['Stale', 'Stale']);
            expect(rows()[1].querySelector('[data-line-lot]').textContent).toBe('G1-20261020'); // the chosen lot stays

            byId('outboundForm').dispatchEvent(new Event('submit', { cancelable: true }));
            await settle();
            expect(posts(fetchMock, '/create')).toEqual([]);
            expect(text('LinesError')).toBe(fmt(WORDS['LinesTable.LineError'], '2', WORDS['LinePanel.PreviewAgain']));
        });

        it('a Staging change does the same', async () => {
            await bootCreate();
            await addLine();
            byId('btnPreview').click();
            await settle();
            byId('StagingLocationId').dispatchEvent(new Event('change'));
            expect(status(rows()[0])).toBe('Stale');
        });

        it('S-WP1-b: a chosen lot the next preview no longer lists stays chosen — "Karşılanamaz" + "Seçilen lot artık uygun değil", and it is still sent', async () => {
            let only = false;
            const fetchMock = await bootCreate(service('RequireReason', {
                preview: (body) => reply({ fefoOutboundMode: 'RequireReason', lines: body.lines.map((l, i) => answerLine(i + 1, { chosen: l.lotOverride?.lotId, lots: only ? [LOTS[0]] : LOTS })) })
            }));
            await addLineWithLotB();
            only = true;
            byId('btnPreview').click();
            await settle();
            const [row] = rows();
            expect(status(row)).toBe('Unfulfillable');
            expect(row.querySelector('[data-line-failure]').textContent).toBe(WORDS.LotNoLongerEligible);
            expect(row.querySelector('[data-line-lot]').textContent).toBe('G1-20261020');
            byId('btnPreview').click();
            await settle();
            const last = posts(fetchMock, '/preview').pop();
            expect(last.lines[0].lotOverride.lotId).toBe(LOT_B);
        });
    });

    // ── AC-9 (browser half; the proxy body against the frozen schema is Diten.Web.Tests OutboundFormV2BodyTests) ─────────────
    it('AC-9: the save body carries the lines in TABLE order, lotOverride only on the line with a chosen lot', async () => {
        const fetchMock = await bootCreate();
        await addLine();
        await addLineWithLotB();
        await addLine();
        byId('btnPreview').click();
        await settle();
        byId('outboundForm').dispatchEvent(new Event('submit', { cancelable: true }));
        await settle();
        const [body] = posts(fetchMock, '/create');
        expect(body.lines.map((l) => l.orderLineId)).toEqual(['1', '2', '3']);
        expect(body.lines.map((l) => 'lotOverride' in l)).toEqual([false, true, false]);
        expect(body.lines[1].lotOverride.lotId).toBe(LOT_B);
        expect(body.shipTo).toMatchObject({ name: 'Klinik', country: 'tr' });
    });

    // ── AC-10 ───────────────────────────────────────────────────────────────────────────────────────
    it('AC-10: 422 WMS_ALLOCATION_FAILED details.lines[] lands on each row\'s badge and note; no outbound, no redirect', async () => {
        const fetchMock = await bootCreate(service('RequireReason', {
            create: () => reply({ error: { code: 'WMS_ALLOCATION_FAILED', message: 'Tahsis yapılamadı.', details: { lines: [{ lineSeq: 2, code: 'InsufficientQuantity', requested: '5.000', allocatable: '1.500' }] } } }, 422)
        }));
        await addLine();
        await addLine({ qty: '5' });
        const before = window.location.href;
        byId('outboundForm').dispatchEvent(new Event('submit', { cancelable: true }));
        await settle();
        expect(posts(fetchMock, '/create')).toHaveLength(1);
        const [one, two] = rows();
        expect(status(one)).toBe('NotPreviewed');
        expect(status(two)).toBe('Unfulfillable');
        expect(two.querySelector('[data-line-failure]').textContent).toBe(fmt(WORDS.AllocationLineFailed, 2, WORDS['Warehouse.Outbound.LineFailure.InsufficientQuantity'], '5', '1,5'));
        expect(window.location.href).toBe(before);
        expect(window.showToast).not.toHaveBeenCalled();
    });

    // ── AC-11 ───────────────────────────────────────────────────────────────────────────────────────
    describe('AC-11: Edit is the same table, read-only; the FEFO reason is not under the reservation', () => {
        const OUTBOUND = {
            id: 'f70b63ed-58d2-4577-9211-8b34a2c132a0', number: 'GMG-OUT-000001', status: 'Allocated', warehouse: 'D01', staging: 'A-01-01-02', shipTo: { name: 'Klinik', country: 'tr' },
            lines: [
                { lineSeq: 1, orderLineId: '1', sku: 'LSKU-TR-000045', quantity: '2.000', uom: 'EA', allocations: [
                    { allocSeq: 1, lotNumber: 'G1-20261020', fefoRank: 2, quantity: '2.000', fefoBypassReason: 'Müşteri bu lotu istedi', reservationReleaseState: 'None', bins: [] }] },
                { lineSeq: 2, orderLineId: '2', sku: 'LSKU-TR-000046', quantity: '1.500', uom: 'EA', allocations: [
                    { allocSeq: 2, lotNumber: 'AMX-2604', fefoRank: 1, quantity: '1.500', reservationReleaseState: 'None', bins: [] }] }
            ]
        };
        const read$ = () => reply({ outbound: OUTBOUND, etag: 'W/"3"', timeline: [], timelineAvailable: true });

        it('Edit: no toolbar, no row menu; the lot cell carries the saved reason as its sub-line; allocated and reservation from the record', async () => {
            const view = read(VIEW('_Form.cshtml'));
            expect(view.slice(view.indexOf('@if (!isEdit)'), view.indexOf('id="btnAddLine"'))).not.toContain('}'); // the toolbar is drawn only on Create
            document.body.innerHTML = `<form id="outboundForm" data-form-mode="edit" data-outbound-id="${OUTBOUND.id}"><div id="outbound-form-alert" class="d-none"></div>
                <select id="WarehouseId"></select><select id="StagingLocationId"></select><input id="OrderRef"><textarea id="Note"></textarea><input id="ShipToName"><input id="ShipToCountry">
                <input id="ShipToCity"><input id="ShipToPostalCode"><input id="ShipToLine1"><input id="ShipToLine2"><div id="ShipToError"></div>
                <table id="outbound-lines-table" data-dt-standard="v2" data-dt-data-mode="client" data-readonly="true"><thead><tr>${'<th></th>'.repeat(7)}</tr></thead><tbody id="outbound-lines-body"></tbody></table>
                <button id="btnSaveOutbound"></button><input name="__RequestVerificationToken" value="tok"></form>`;
            await boot([JS('rules.js'), JS('form.js')], read$);
            const [one, two] = rows();
            expect(document.querySelector('#outbound-lines-body [data-line-menu]')).toBeNull();
            expect(one.querySelectorAll('td')).toHaveLength(7);
            expect(one.querySelector('[data-fefo-reason]').textContent).toBe(`${WORDS['LinesTable.FefoReasonLabel']} Müşteri bu lotu istedi`);
            expect(cells(one)[5]).toBe('2');
            expect(cells(one)[6]).toBe(WORDS['Warehouse.Outbound.ReleaseState.None']);
            expect(two.querySelector('[data-fefo-reason]')).toBeNull();
            expect(cells(two)[3]).toBe('1,5 EA');
        });

        it('Details: the reason is its own AllocFefoReason column after "Rezervasyon"; "Rezervasyon" is the state only; "-" without a reason', async () => {
            document.body.innerHTML = `<div id="outbound-details" data-outbound-id="${OUTBOUND.id}">${['d-number', 'd-status', 'd-error', 'd-warehouse', 'd-staging', 'd-order-ref',
                'd-created', 'd-ready', 'd-shipto-name', 'd-shipto-country', 'd-shipto-city', 'd-shipto-postal', 'd-shipto-line1', 'd-shipto-line2', 'd-note', 'd-cancel-reason',
                'd-item-level', 'd-nothing-picked', 'd-names', 'd-pick-tasks-empty', 'd-packages-empty', 'd-short-dispatch', 'd-shipment', 'd-timeline-unavailable']
                .map((id) => `<span id="${id}"></span>`).join('')}<div id="d-shipment-card"></div><div id="d-lines"></div><div id="d-pick-tasks"></div><div id="d-packages"></div>
                <ul id="d-timeline"></ul><div id="d-actions"></div><a id="d-edit"></a></div>`;
            await boot([JS('rules.js'), JS('details.js')], read$);
            const heads = Array.from(document.querySelector('[data-line-seq="1"] thead').querySelectorAll('th')).map((th) => th.textContent);
            expect(heads.slice(-2)).toEqual([WORDS.AllocRelease, WORDS.AllocFefoReason]);
            const withReason = document.querySelector('[data-alloc-seq="1"]');
            expect(withReason.querySelector('[data-alloc-release]').textContent).toBe(WORDS['Warehouse.Outbound.ReleaseState.None']);
            expect(withReason.querySelector('[data-alloc-fefo-reason]').textContent).toBe('Müşteri bu lotu istedi');
            expect(document.querySelector('[data-alloc-seq="2"] [data-alloc-fefo-reason]').textContent).toBe('-');
        });
    });

    // ── AC-12 ───────────────────────────────────────────────────────────────────────────────────────
    it('AC-12: the table and the panel write quantities with the FIX-01 formatter (tr "1,5", "408,5") — no second formatter', async () => {
        await bootCreate();
        await addLine({ qty: '1,5' });
        expect(cells(rows()[0])[3]).toBe('1,5 EA');
        byId('btnPreview').click();
        await settle();
        expect(cells(rows()[0])[5]).toBe('408,5');

        // the panel: the line opens with its quantity in the reader's culture and its single-line preview in the same formatter
        rows()[0].querySelector('[data-line-action="edit"]').click();
        expect(byId('lineQuantity').value).toBe('1,5');
        byId('btnLinePreview').click();
        await settle();
        expect(text('linePreviewBody')).toContain(fmt(WORDS.LineAllocatable, '408,5'));
        expect(byId('lineLot').options[1].textContent).toBe(fmt(WORDS.LotOption, 'AMX-2604', 1, '2028-03-31', '408,5'));

        const source = read(JS('form.js'));
        expect(source).not.toMatch(/Intl\.NumberFormat|toLocaleString\(/);
        expect(source).toContain('rules.quantityText(');
    });

    // ── the row menu ────────────────────────────────────────────────────────────────────────────────
    it('"Sil" asks the product\'s one confirm (MOD-0013) — its way out reads "Vazgeç", not the shared "İptal" beside "Sil"; nothing is posted', async () => {
        const fetchMock = await bootCreate();
        await addLine();
        await addLine();
        rows()[0].querySelector('[data-line-action="delete"]').click();
        expect(window.showConfirm).toHaveBeenCalledTimes(1);
        const [title, confirmDelete, options] = window.showConfirm.mock.calls[0];
        expect(title).toBe(fmt(WORDS['LinesTable.DeleteConfirm'], '1'));
        expect(options).toMatchObject({ type: 'danger', confirmButtonText: WORDS['LinesTable.Delete'], cancelButtonText: WORDS['LinePanel.Discard'] });
        expect(options.cancelButtonText).not.toBe(options.confirmButtonText);
        expect(rows()).toHaveLength(2);
        confirmDelete();
        expect(rows().map((r) => cells(r)[0])).toEqual(['2']);
        expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toEqual([]);
    });

    it('"Lot değiştir" on a line not previewed opens the panel on that line and previews it first', async () => {
        const fetchMock = await bootCreate();
        await addLine();
        rows()[0].querySelector('[data-line-action="lot"]').click();
        await settle();
        expect(byId('offcanvasOutboundLine').classList.contains('show')).toBe(true);
        expect(text('offcanvasOutboundLineLabel')).toBe(WORDS['LinePanel.TitleEdit']);
        expect(posts(fetchMock, '/preview')).toHaveLength(1);
        expect(byId('lineLot').options).toHaveLength(3);
    });

    it('the order line number is unique among the lines (client check, §12)', async () => {
        await bootCreate();
        await addLine();
        byId('btnAddLine').click();
        expect(byId('lineOrderLine').value).toBe('2');
        await fillPanel({ orderLine: '1' });
        byId('btnLineSave').click();
        expect(text('lineAlert')).toBe(WORDS['LinePanel.OrderLineTaken']);
        expect(rows()).toHaveLength(1);
    });

    // ── AC-13 ───────────────────────────────────────────────────────────────────────────────────────
    describe('AC-13: seven languages, 375 px, page help', () => {
        const NEW_KEYS = ['LinesTable.', 'LineStatus.', 'LinePanel.', 'LineLimitReached', 'LotNoLongerEligible', 'AllocFefoReason'];

        it('every DL-1 key is in all seven page resx files, none left in English, the placeholders match', () => {
            const en = pageWords('en');
            const keys = Object.keys(en).filter((k) => NEW_KEYS.some((p) => k.startsWith(p)));
            expect(keys.length).toBeGreaterThanOrEqual(35);
            LANGS.forEach((lang) => {
                const words = pageWords(lang);
                keys.forEach((key) => {
                    expect(words[key], `${lang}: ${key}`).toBeTruthy();
                    if (lang !== 'en') expect(words[key], `${lang}: ${key} is the English text`).not.toBe(en[key]);
                    expect((words[key].match(/\{\d\}/g) || []).sort(), `${lang}: ${key} placeholders`).toEqual((en[key].match(/\{\d\}/g) || []).sort());
                });
            });
            // FIX-01 item 3: the Turkish words say "Çıkış sevkiyatı", never "outbound"
            keys.forEach((key) => expect(WORDS[key]).not.toMatch(/outbound/i));
        });

        it('375 px: the table scrolls inside .table-responsive and the panel is full width on a phone (wh-confirm-panel)', () => {
            const form = read(VIEW('_Form.cshtml'));
            expect(form).toMatch(/<div class="table-responsive">\s*<table id="outbound-lines-table"/);
            expect(read(VIEW('_LineOffcanvas.cshtml'))).toMatch(/class="offcanvas offcanvas-end wh-confirm-panel"/);
            const css = read('wwwroot/assets/css/Warehouse/warehouse.css');
            expect(css).toMatch(/@media \(max-width: 575\.98px\)\s*\{\s*\.wh-confirm-panel\.offcanvas-end\s*\{\s*inline-size:\s*100%;/);
        });

        it('page help (§25): step 2 is the side panel, step 3 the preview and "Lot Değiştir", step 4 promises the hand-written recipient only, Immediate says a preview reserves nothing', () => {
            const help = resx('Resources/PageHelpResource.tr.resx');
            expect(help['Help.OUTBOUNDSHIPMENTS.Step2']).toContain(WORDS.LineAdd);
            expect(help['Help.OUTBOUNDSHIPMENTS.Step2']).toContain(WORDS['LinePanel.Save']);
            expect(help['Help.OUTBOUNDSHIPMENTS.Step3']).toContain(WORDS['Warehouse.Outbound.Action.Preview']);
            expect(help['Help.OUTBOUNDSHIPMENTS.Step3']).toContain(WORDS['LinePanel.ChangeLot']);
            expect(help['Help.OUTBOUNDSHIPMENTS.Step4']).not.toMatch(/CRM/i);
            expect(help['Help.OUTBOUNDSHIPMENTS.Immediate']).toMatch(/stok ayırmaz/);
            LANGS.forEach((lang) => expect(resx(`Resources/PageHelpResource.${lang}.resx`)['Help.OUTBOUNDSHIPMENTS.Step4'], lang).not.toMatch(/CRM/i));
        });
    });

    // ── §8.6 ────────────────────────────────────────────────────────────────────────────────────────
    it('§8.6: the order reference is one .diten-field and nothing promises an order picker', () => {
        const form = read(VIEW('_Form.cshtml'));
        expect(form).toMatch(/<div class="diten-field">\s*<i class="bx bx-receipt diten-field-icon" aria-hidden="true"><\/i>\s*<input asp-for="OrderRef"/);
        expect(form).not.toMatch(/OrderPick|orderPicker|FromOrder/i);
    });
});
