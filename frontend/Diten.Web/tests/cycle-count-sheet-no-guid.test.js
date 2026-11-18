// MOD-0181 DL-4 FIX-01 (CT live measurement, owner Beste 2026-11-12) — the count sheet shows WORDS and NAMES, never an id:
// countsheet.js reads the l10n payload under the names the page writes (camelCase `words`), shows each lot by its NUMBER (the lines proxy reads it
// from the lot register), and when a lot number, bin or product name could not be read it says so in the page's words — a GUID never reaches the
// reader. Measured on the PRODUCTION script (wwwroot/assets/js/Warehouse/CycleCounts/countsheet.js), mounted in jsdom with the page's real ids.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const script = fs.readFileSync(path.join(root, 'wwwroot/assets/js/Warehouse/CycleCounts/countsheet.js'), 'utf8');
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));
const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;

const COUNT_ID = 'b1111111-0000-0000-0000-000000000001';
const WORDS = {
    CountSheetTitle: 'Sayım Formu', FieldLot: 'Lot no.', LotUnknown: 'Lot numarası okunamadı', NameUnknown: 'Ad okunamadı',
    'CycleCount.Status.Counting': 'Sayılıyor', 'CycleCount.LineStatus.Open': 'Sayılmadı', BinProgress: '{0} / {1} sayıldı',
    UncountedLines: '{0} satır sayılmadı', 'CycleCount.Action.Confirm': 'Onayla ve Kaydet', Increase: 'Artır', Decrease: 'Azalt', FieldCounted: 'Sayılan miktar'
};

function line(id, extra) {
    return Object.assign({
        lineId: id, countId: COUNT_ID, locationId: 'a3333333-0000-0000-0000-000000000003', item: { itemId: 'e1', skuId: 'f1111111-0000-0000-0000-000000000001', skuLevel: 'Lsku' },
        status: 'Open', origin: 'Snapshot', etag: 'W/"1"', version: 1
    }, extra);
}

async function mount(lines) {
    document.body.innerHTML = `
        <div id="cc-sheet" data-count-id="${COUNT_ID}"><h5 id="cc-sheet-title"></h5><p id="cc-sheet-subtitle"></p></div>
        <div id="cc-sheet-error" class="d-none"></div><div id="cc-sheet-readonly" class="d-none"></div><div id="cc-sheet-blind" class="d-none"></div>
        <div id="cc-sheet-loading"></div><div id="cc-sheet-bins"></div>
        <div id="cc-sheet-bar"><small id="cc-uncounted"></small></div>
        <script id="countsheet-l10n" type="application/json">${JSON.stringify({ words: WORDS, cancel: 'Vazgeç', save: 'Kaydet' })}</script>`;
    window.Permissions = { has: () => true };
    const fetchMock = vi.fn()
        .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({
            count: { countId: COUNT_ID, number: 'GMG-CNT-000001', status: 'Counting', blind: false, warehouse: 'D01 · İstanbul Merkez Depo', scopeBinIds: [] },
            etag: 'W/"3"' }) })
        .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ items: lines, total: lines.length }) });
    vi.stubGlobal('fetch', fetchMock);
    // run the script's DOMContentLoaded start directly — a document listener would outlive this test and fire again in the next one
    const starts = [];
    const add = document.addEventListener;
    document.addEventListener = (type, fn, options) => (type === 'DOMContentLoaded' ? starts.push(fn) : add.call(document, type, fn, options));
    try { new Function(script)(); } finally { document.addEventListener = add; }
    starts.forEach((start) => start());
    for (let i = 0; i < 6; i++) await flush();
    return fetchMock;
}

describe('MOD-0181 count sheet — words and names, never an id (DL-4 FIX-01)', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        delete window.Permissions;
        document.body.innerHTML = '';
    });

    it('reads the camelCase payload: the title and the status come from the page words', async () => {
        await mount([line('b2222222-0000-0000-0000-000000000002', { location: 'D01-A-01', sku: 'AMX 500 · SKU-1', lotId: 'da7bfb2a-66c4-4c2b-b5ce-9660ab28f56d', lotNumber: 'AMX-2604' })]);
        expect(document.getElementById('cc-sheet-title').textContent).toBe('Sayım Formu · GMG-CNT-000001');
        expect(document.getElementById('cc-sheet-subtitle').textContent).toBe('D01 · İstanbul Merkez Depo · Sayılıyor');
    });

    it('shows the lot NUMBER, the bin and product names — and no GUID anywhere a reader sees', async () => {
        await mount([line('b2222222-0000-0000-0000-000000000002', { location: 'D01-A-01', sku: 'AMX 500 · SKU-1', lotId: 'da7bfb2a-66c4-4c2b-b5ce-9660ab28f56d', lotNumber: 'AMX-2604' })]);
        const text = document.getElementById('cc-sheet-bins').textContent;
        expect(text).toContain('Lot no.: AMX-2604');
        expect(text).toContain('D01-A-01');
        expect(text).toContain('AMX 500 · SKU-1');
        expect(text).not.toMatch(GUID);
        expect(document.body.textContent).not.toMatch(GUID);
    });

    it('says in words what it could not read — never the lot id, bin id or SKU id instead', async () => {
        await mount([line('b2222222-0000-0000-0000-000000000002', { lotId: 'da7bfb2a-66c4-4c2b-b5ce-9660ab28f56d' })]);
        const text = document.getElementById('cc-sheet-bins').textContent;
        expect(text).toContain('Lot no.: Lot numarası okunamadı');
        expect(text).toContain('Ad okunamadı');
        expect(text).not.toMatch(GUID);
        expect(document.body.textContent).not.toMatch(GUID);
    });
});
