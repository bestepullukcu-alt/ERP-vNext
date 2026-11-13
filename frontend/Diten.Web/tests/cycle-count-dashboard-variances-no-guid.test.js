// MOD-0181 DL-4 FIX-02 (owner Beste 2026-11-12) — the Cycle Count Dashboard and the Variance Trace show NAMES and lot NUMBERS, never an id: the
// PRODUCTION scripts (wwwroot/assets/js/Warehouse/CycleCounts/index.js and CountVariances/index.js) run in jsdom with the list component stubbed; their
// own column renderers, QuickViews, edit form chips and decision summary are fed rows whose names the proxy could not read (the proxy then leaves the
// member out — CycleCountProxyControllerTests.DL4_FIX02_*) and rows with names; no GUID may reach any visible text.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));
const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;
const ID = (n) => `${String(n).repeat(8)}-0000-0000-0000-00000000000${n}`;

const WORDS = {
    NameUnknown: 'Ad okunamadı', LotUnknown: 'Lot numarası okunamadı', ScopeFull: 'Tüm depo', BlindBadge: 'Kör', RecountOfYes: 'Evet',
    'CycleCount.Status.Draft': 'Taslak', 'CycleCount.Status.Submitted': 'Gönderildi', 'CycleCount.Type.Cycle': 'Dönemsel sayım',
    'CycleCount.Source.Count': 'Sayım', 'CycleCount.Source.Manual': 'Elle', 'CycleCount.LineStatus.AwaitingApproval': 'Onay bekliyor',
    'MovementType.COUNT_GAIN': 'Sayım fazlası', DecisionSummary: '{0} · {1} · {2} · fark {3}', ReadOnlyManual: 'Salt okunur',
    ColBin: 'Raf gözü', ColProduct: 'Ürün', ColLot: 'Lot no.', ColVariance: 'Fark', MovementType: 'Hareket türü', SourceDeclared: 'Kaynak',
    PostedAt: 'İşlenme', OpenLedger: 'Defter', Timeline: 'Geçmiş', Loading: 'Yükleniyor'
};

/** Runs a production page script with the list component stubbed and its DOMContentLoaded start called directly; returns what it gave createList. */
async function boot(script, fetchImpl) {
    let options = null;
    window.L10n = { Words: WORDS, Actions: 'İşlemler', QuickView: 'Hızlı bakış' };
    window.Permissions = { has: () => true };
    window.DitenDataTable = {
        createList: async (o) => { options = o; return { dt: { ajax: { reload() {} }, rows: () => ({ data: () => ({ toArray: () => [] }) }) } }; },
        renderActions: () => '', getAuthHeaders: () => ({})
    };
    vi.stubGlobal('fetch', vi.fn(fetchImpl));
    const starts = [];
    const add = document.addEventListener;
    document.addEventListener = (type, fn, o) => (type === 'DOMContentLoaded' ? starts.push(fn) : add.call(document, type, fn, o));
    try { new Function(read(script))(); } finally { document.addEventListener = add; }
    starts.forEach((start) => start());
    for (let i = 0; i < 4; i++) await flush();
    return options;
}

/** The visible text of every column a renderer draws for a row (attributes such as the checkbox value are not visible text). */
function rendered(options, row, skip = []) {
    const host = document.createElement('div');
    host.innerHTML = options.config.columnDefs.filter((d) => !skip.includes(d.targets) && d.render)
        .map((d) => `<span>${d.render(row[options.config.columns[d.targets < 0 ? options.config.columns.length - 1 : d.targets].data], 'display', row)}</span>`).join(' ');
    return host.textContent;
}

describe('MOD-0181 dashboard and variance trace — names, never an id (DL-4 FIX-02)', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        delete window.L10n; delete window.Permissions; delete window.DitenDataTable;
        document.body.innerHTML = '';
    });

    it('the dashboard list, QuickView and edit chips say "could not be read" instead of an id', async () => {
        document.body.innerHTML = `<table class="datatables-cyclecounts"></table>
            <div id="offcanvasDetailsPreview"><h5 id="offcanvasDetailsPreviewLabel"></h5><small id="oc-subtitle"></small>
              <span id="oc-warehouse"></span><span id="oc-type"></span><span id="oc-scope"></span><span id="oc-planned"></span><span id="oc-assignee"></span>
              <span id="oc-snapshot"></span><span id="oc-lines"></span><span id="oc-recountof"></span><span id="oc-note"></span><span id="oc-cancel"></span>
              <div id="oc-readonly"></div><ul id="oc-timeline"></ul><a id="oc-btn-sheet"></a><button id="oc-btn-edit"></button><button id="oc-btn-release"></button><button id="oc-btn-cancel"></button></div>`;
        const unnamed = {
            countId: ID(1), number: 'GMG-CNT-000001', status: 'Draft', warehouseId: ID(2), countType: 'Cycle', blind: false, plannedDate: '2026-11-12',
            assignedTo: ID(3), createdBy: ID(4), lineCount: 0, createdAt: '2026-11-12T08:00:00.000Z', scopeLocationIds: [ID(5)], scope: [null],
            scopeSkuIds: [ID(6)], scopeSkus: [{ id: ID(6) }], version: 1
        };
        const options = await boot('wwwroot/assets/js/Warehouse/CycleCounts/index.js', async (url) => ({
            ok: String(url).includes('/count/'), status: 200,
            json: async () => (String(url).includes('/count/') ? { count: unnamed, etag: 'W/"1"', timeline: [{ kind: 'Created', atUtc: '2026-11-12T08:00:00Z', actorUserId: ID(4) }] } : {})
        }));

        const list = rendered(options, unnamed, [1]);
        expect(list).toContain('Ad okunamadı');
        expect(list).not.toMatch(GUID);

        await options.quickView.populate(unnamed);
        for (let i = 0; i < 3; i++) await flush();
        const quick = document.getElementById('offcanvasDetailsPreview').textContent;
        expect(document.getElementById('oc-warehouse').textContent).toBe('Ad okunamadı');
        expect(document.getElementById('oc-assignee').textContent).toBe('Ad okunamadı');
        expect(document.getElementById('oc-scope').textContent).toBe('Ad okunamadı');
        expect(quick).not.toMatch(GUID);
    });

    it('the variance trace shows lot NUMBERS and names, says what it could not read, and its decision summary carries no id', async () => {
        document.body.innerHTML = `<table class="datatables-countvariances"></table>
            <div id="offcanvasDetailsPreview"><h5 id="offcanvasDetailsPreviewLabel"></h5><small id="oc-subtitle"></small><div id="oc-note"></div>
              <dl id="oc-fields"></dl><button id="oc-btn-decide"></button></div>
            <div id="offcanvasCreateEdit"><div id="formDecisionAlert"></div><div id="decisionSummary"></div><div id="decisionChoices"></div>
              <div id="decisionNone"></div><div id="decisionReasonBlock"><textarea id="decisionReason"></textarea></div><button id="btnSaveDecision"></button>
              <form id="formDecision"></form></div>`;
        const options = await boot('wwwroot/assets/js/Warehouse/CountVariances/index.js', async () => ({ ok: false, status: 500, json: async () => ({}) }));
        const line = (extra) => Object.assign({ lineId: ID(1), countId: ID(2), locationId: ID(3), item: { itemId: ID(4), skuId: ID(5), skuLevel: 'Lsku' },
            lotId: ID(6), status: 'AwaitingApproval', expectedAtCount: '10.000', countedQuantity: '12.000', varianceQuantity: '2.000', etag: 'W/"1"' }, extra);
        const named = { source: 'Count', countNumber: 'GMG-CNT-000001', line: line({ location: 'D01-A-01', sku: 'SKU-9', lotNumber: 'AMX-2604' }), countEtag: 'W/"7"', countStatus: 'Submitted' };
        const unnamed = { source: 'Count', countNumber: 'GMG-CNT-000001', line: line({}), countEtag: 'W/"7"', countStatus: 'Submitted' };
        const manual = { source: 'Manual', movement: { transactionId: ID(7), transactionNumber: 'MG-TRX-000009', movementType: 'COUNT_GAIN', locationId: ID(3),
            item: { itemId: ID(4), skuId: ID(5), skuLevel: 'Lsku' }, lotId: ID(6), quantity: '1', sourceModule: 'MOD-0173', sourceType: 'MANUAL_MOVEMENT',
            createdBy: ID(8), createdAt: '2026-11-12T08:00:00.000Z', sourceMismatch: false } };

        const withNames = rendered(options, named);
        expect(withNames).toContain('AMX-2604');
        expect(withNames).toContain('D01-A-01');
        expect(withNames).not.toMatch(GUID);
        for (const row of [unnamed, manual]) {
            const text = rendered(options, row);
            expect(text).toContain('Ad okunamadı');
            expect(text).toContain('Lot numarası okunamadı');
            expect(text).not.toMatch(GUID);
            options.quickView.populate(row);
            expect(document.getElementById('offcanvasDetailsPreview').textContent).not.toMatch(GUID);
        }

        options.actions.onRowAction.decide({ row: unnamed });
        const summary = document.getElementById('decisionSummary').textContent;
        expect(summary).toBe('GMG-CNT-000001 · Ad okunamadı · Ad okunamadı · fark 2.000');
        expect(summary).not.toMatch(GUID);
    });
});
