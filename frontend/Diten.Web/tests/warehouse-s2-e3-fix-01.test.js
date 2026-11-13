// WP-0178-E3-FIX-01 (MOD-0178 E3 live tour 2026-11-13, G-1 / G-2 + owner) — the PRODUCTION scripts of the outbound screens run in jsdom (rules.js
// + the page script, the service answers stubbed):
//   1. "Sevke hazır" (+ its FEFO reasons) and "İptal" open the product's ONE confirm (window.showConfirm, MOD-0013) — no page-drawn Bootstrap modal;
//   2. a quantity / weight / dimension the service writes "2.000", "1.500" is shown in the reader's culture (tr "2", "1,5"; en "1.5") on Details,
//      the Create form's preview, Packing and PickTasks — and what the screens SEND stays the dot-decimal wire string;
//   4. "Tahsisi önizle" previews the CHOSEN lot (lotOverride) and says it is not the FEFO suggestion; a Staging refusal points at its field.
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
const flush = () => new Promise((resolve) => setTimeout(resolve, 0));
const settle = async () => { for (let i = 0; i < 6; i++) await flush(); };
const js = (folder, file) => `wwwroot/assets/js/Warehouse/${folder}/${file}`;

const WORDS = {
    'Warehouse.Outbound.Status.Packed': 'Paketlendi', 'Warehouse.Outbound.Status.Picked': 'Toplandı',
    'Warehouse.Outbound.Action.Release': 'Sevke Hazır', 'Warehouse.Outbound.Action.Cancel': 'İptal', 'Warehouse.Outbound.Action.Pack': 'Paketle',
    'Warehouse.Outbound.Action.Release.Confirm': 'Sevke hazır yapılan çıkış sevkiyatı değiştirilemez ve iptal edilemez.',
    'Warehouse.Outbound.Action.Cancel.Confirm': 'Çıkış sevkiyatı iptal edilir.',
    'Warehouse.Outbound.ReleaseState.None': 'Tutuluyor', 'Warehouse.Package.Status.Closed': 'Kapalı', 'Warehouse.Package.Status.Open': 'Açık',
    'Warehouse.Status.Completed': 'Tamamlandı', 'Warehouse.Status.Open': 'Açık', 'Warehouse.Action.Confirm': 'Onayla',
    PackageContentLine: 'Tahsis {0}: {1}', PackageLeftHint: 'Kalan {0}', FieldWeight: 'Ağırlık (kg)', FieldDimensions: 'Ölçü (cm)',
    FieldReason: 'Gerekçe', ReasonHint: 'En az 10 karakter', ReasonRequired: 'En az 10 karakterlik bir gerekçe yazın.',
    ReleaseReasonsIntro: 'Bu tahsislerde lot ilk FEFO lotu değil; gerekçe yazın.', ReleaseReasonLabel: 'Tahsis {0} — lot {1} (SKT {2})',
    LineFefoFirst: 'FEFO sırası (önerilen)', LineAllocatable: 'Alınabilir: {0}', LotOption: '{0} · FEFO {1} · SKT {2} · {3} alınabilir',
    PreviewAllocation: 'Lot {0}: {1} (FEFO sırası {2})', PreviewChosenLot: 'Seçtiğiniz lot kayıtta uygulanır; FEFO önerisi lot {0} (SKT {1}).',
    StagingNotAllowed: 'Seçilen lokasyon sevkiyat hazırlık lokasyonu değil.', DeviationNeedsReason: 'Gerekçe yazın.',
    RemainingHint: 'Kalan: {0} {1}', 'Warehouse.Error.Generic': 'Hata', 'Warehouse.Error.WMS_TARGET_NOT_ALLOWED': 'Hedef raf olmalı.'
};

/** Runs production scripts in order; the page start (DOMContentLoaded) is called directly. */
async function boot(scripts, fetchImpl, { culture = 'tr', createList } = {}) {
    window.CurrentLanguage = culture;
    window.L10n = { Words: WORDS, AreYouSure: 'Emin misiniz?', Close: 'Kapat', Actions: 'İşlemler', QuickView: 'Hızlı bakış' };
    window.Permissions = { has: () => true };
    window.DitenDataTable = { getAuthHeaders: () => ({}), createList: createList || (async () => ({})), renderActions: () => '' };
    window.showToast = vi.fn();
    window.showConfirm = vi.fn();
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

const reply = (json, status = 200) => ({ ok: status < 400, status, json: async () => json });
const posts = (fetchMock, part) => fetchMock.mock.calls.filter(([url, init]) => String(url).includes(part) && init?.method === 'POST').map(([, init]) => JSON.parse(init.body));
const text = (id) => document.getElementById(id).textContent;

const OUTBOUND = {
    id: 'f70b63ed-58d2-4577-9211-8b34a2c132a0', number: 'GMG-OUT-000001', status: 'Packed', itemLevel: false, warehouse: 'D01', staging: 'A-01-01-02',
    lines: [{ lineSeq: 1, orderLineId: '1', sku: 'LSKU-TR-000045', quantity: '2.000', uom: 'EA', allocations: [
        { allocSeq: 1, lotNumber: 'AMX-2604', fefoRank: 1, quantity: '2.000', picked: '2.000', packed: '1.500', bins: [{ location: 'A-01-01-01', quantity: '408.000' }], reservationReleaseState: 'None' }
    ] }],
    pickTasks: [{ id: 't1', number: 'GMG-PCK-000001', from: 'A-01-01-01', lotNumber: 'AMX-2604', confirmed: '2.000', planned: '2.000', status: 'Completed' }],
    packages: [{ id: 'p1', number: 'GMG-PKG-000001', status: 'Closed', contents: [{ allocSeq: 1, quantity: '2.000' }], weightKg: '1.500', dimensionsCm: '30.5x20x10' }]
};

const DETAILS_DOM = `<div id="outbound-details" data-outbound-id="${OUTBOUND.id}">
  ${['d-number', 'd-status', 'd-error', 'd-warehouse', 'd-staging', 'd-order-ref', 'd-created', 'd-ready', 'd-shipto-name', 'd-shipto-country', 'd-shipto-city',
        'd-shipto-postal', 'd-shipto-line1', 'd-shipto-line2', 'd-note', 'd-cancel-reason', 'd-item-level', 'd-nothing-picked', 'd-names', 'd-pick-tasks-empty',
        'd-packages-empty', 'd-short-dispatch', 'd-shipment', 'd-timeline-unavailable'].map((id) => `<span id="${id}"></span>`).join('')}
  <div id="d-shipment-card"></div><div id="d-lines"></div><div id="d-pick-tasks"></div><div id="d-packages"></div><ul id="d-timeline"></ul>
  <div id="d-actions"></div><a id="d-edit"></a></div>
  <form id="outboundActionToken"><input name="__RequestVerificationToken" value="tok"></form>`;

const bootDetails = (fetchImpl, culture) => {
    document.body.innerHTML = DETAILS_DOM;
    return boot([js('OutboundShipments', 'rules.js'), js('OutboundShipments', 'details.js')], fetchImpl, { culture });
};

const detailsRead = (url, init) => (init?.method === 'POST' ? null : reply({ outbound: OUTBOUND, etag: 'W/"7"', timeline: [], timelineAvailable: true }));

describe('WP-0178-E3-FIX-01 — the outbound screens (MOD-0178)', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
        ['L10n', 'Permissions', 'DitenDataTable', 'showToast', 'showConfirm', 'CurrentLanguage', 'OutboundRules', 'PickRules', 'PackingRules'].forEach((k) => delete window[k]);
        document.body.innerHTML = '';
    });

    it('G-1: Details shows the service\'s "2.000" / "1.500" in tr as "2" / "1,5" (quantity, picked, packed, bin, package, weight, dimensions)', async () => {
        await bootDetails(detailsRead, 'tr');
        const line = document.querySelector('[data-line-seq="1"]');
        expect(line.querySelector('span').textContent).toBe('2 EA');
        const cells = Array.from(document.querySelectorAll('[data-alloc-seq="1"] td')).map((td) => td.textContent);
        expect(cells.slice(3, 7)).toEqual(['2', '2', '1,5', 'A-01-01-01 (408)']);
        expect(document.querySelector('[data-pick-task="t1"]').textContent).toContain('2 / 2');
        const pkg = document.querySelector('[data-package="p1"]').textContent;
        expect(pkg).toContain('Tahsis 1: 2');
        expect(pkg).toContain('1,5 kg · 30,5x20x10');
        expect(document.getElementById('outbound-details').textContent).not.toMatch(/\d\.\d{3}\b/); // no "2.000", "1.500", "408.000"
    });

    it('G-1: the same page in en writes "1.5" — the culture decides, not a fixed separator', async () => {
        await bootDetails(detailsRead, 'en');
        expect(document.querySelector('[data-package="p1"]').textContent).toContain('1.5 kg · 30.5x20x10');
        expect(document.querySelector('[data-line-seq="1"] span').textContent).toBe('2 EA');
    });

    it('item 1: "Sevke hazır" is window.showConfirm with the record number; a FEFO 422 asks each reason in the same component, then ONE request with a new key', async () => {
        let releases = 0;
        const fetchMock = await bootDetails((url, init) => {
            if (init?.method !== 'POST') return detailsRead(url, init);
            releases += 1;
            return releases === 1
                ? reply({ error: { code: 'FEFO_BYPASS_REASON_REQUIRED', message: 'FEFO', details: { deviations: [{ allocSeq: 1, firstEligibleExpiry: '2028-03-31' }] } } }, 422)
                : reply({ message: 'Çıkış sevkiyatı sevke hazır.' });
        }, 'tr');
        expect(document.querySelector('.modal')).toBeNull(); // no page-drawn Bootstrap modal is left on the page

        document.querySelector('[data-d-action="Release"]').click();
        expect(window.showConfirm).toHaveBeenCalledTimes(1);
        const [title, confirmRelease, options] = window.showConfirm.mock.calls[0];
        expect(title).toBe(WORDS['Warehouse.Outbound.Action.Release.Confirm']);
        expect(options).toMatchObject({ entityName: 'GMG-OUT-000001', confirmButtonText: 'Sevke Hazır', icon: 'bx-send' });
        await confirmRelease();
        await settle();

        expect(window.showConfirm).toHaveBeenCalledTimes(2); // the FEFO reason, in the same component
        const [, confirmReason, reasonOptions] = window.showConfirm.mock.calls[1];
        expect(reasonOptions).toMatchObject({ showInput: true, inputType: 'textarea', entityName: 'GMG-OUT-000001', inputLabel: 'Tahsis 1 — lot AMX-2604 (SKT 2028-03-31)' });
        expect(reasonOptions.inputValidator('kısa')).toBe(WORDS.ReasonRequired);
        expect(reasonOptions.inputValidator('Müşteri talebiyle seçildi')).toBeNull();
        await confirmReason('  Müşteri talebiyle seçildi ');
        await settle();

        const [first, second] = posts(fetchMock, '/release/');
        expect(first.fefoBypassReasons).toBeUndefined();
        expect(second.fefoBypassReasons).toEqual([{ allocSeq: 1, reason: 'Müşteri talebiyle seçildi' }]);
        expect(second.idempotencyKey).not.toBe(first.idempotencyKey);
        expect(second.ifMatch).toBe('W/"7"');
    });

    it('item 1: "İptal" is window.showConfirm (danger) with its reason field kept (10-1000) — the way out does not also read "İptal"', async () => {
        const fetchMock = await bootDetails((url, init) => (init?.method === 'POST' ? reply({ message: 'ok' }) : detailsRead(url, init)), 'tr');
        document.querySelector('[data-d-action="Cancel"]').click();
        const [title, confirmCancel, options] = window.showConfirm.mock.calls[0];
        expect(title).toBe(WORDS['Warehouse.Outbound.Action.Cancel.Confirm']);
        expect(options).toMatchObject({ type: 'danger', entityName: 'GMG-OUT-000001', confirmButtonText: 'İptal', cancelButtonText: 'Kapat', showInput: true, inputLabel: 'Gerekçe' });
        expect(options.inputAttributes.maxlength).toBe(1000);
        expect(options.inputValidator('az')).toBe(WORDS.ReasonRequired);
        await confirmCancel(' Sipariş müşteri tarafından iptal edildi ');
        await settle();
        expect(posts(fetchMock, '/cancel/')).toEqual([expect.objectContaining({ reason: 'Sipariş müşteri tarafından iptal edildi', ifMatch: 'W/"7"' })]);
    });

    it('G-2 / item 3: no Turkish text of the MOD-0178 screens says "outbound" (page resx, the menu entry, the page help)', () => {
        const values = (relative) => Array.from(read(relative).matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)).map((m) => [m[1], m[2]]);
        const pages = ['OutboundShipments/OutboundShipmentsIndex', 'PickTasks/PickTasksIndex', 'Packing/PackingIndex', 'PutawayTasks/PutawayTasksIndex',
            'WarehouseActivity/WarehouseActivityIndex', 'WarehouseExceptions/WarehouseExceptionsIndex', 'WarehouseIntegration/WarehouseIntegrationIndex'];
        const left = pages.flatMap((p) => values(`Resources/Views/Warehouse/${p}.tr.resx`).filter(([, v]) => /outbound/i.test(v)).map(([k]) => `${p}: ${k}`))
            .concat(values('Resources/SharedResource.tr.resx').filter(([k, v]) => /^Nav\./.test(k) && /outbound/i.test(v)).map(([k]) => k))
            .concat(values('Resources/PageHelpResource.tr.resx')
                .filter(([k, v]) => /^Help\.(OUTBOUNDSHIPMENTS|PICKTASKS|PACKINGSTATION|PUTAWAYTASKS|WAREHOUSE[A-Z]+)\./.test(k) && /outbound/i.test(v)).map(([k]) => k));
        expect(left).toEqual([]);
        const title = values('Resources/Views/Warehouse/OutboundShipments/OutboundShipmentsIndex.tr.resx').find(([k]) => k === 'OutboundShipmentsTitle')[1];
        expect(title).toBe('Çıkış Sevkiyatları');
    });

    it('the Details view draws no Bootstrap modal for the state actions any more (MOD-0013) — only the anti-forgery token', () => {
        const view = read('Views/Warehouse/OutboundShipments/_ActionDialogs.cshtml');
        expect(view).not.toMatch(/class="modal\b/);
        expect(view).toContain('id="outboundActionToken"');
        expect(read(js('OutboundShipments', 'details.js'))).not.toMatch(/bootstrap\?*\.Modal/);
    });

    // MOD-0178-FU01 DL-1 (FU01 pack §8.1: "FIX-01'in testi FU01'in DOM'una göre güncellenir, silinmez"): the line is no longer a card on the form
    // but a row of #outbound-lines-table, edited in the side panel (#offcanvasOutboundLine); the item 4 assertions are kept as they were and now
    // run through the panel — its lot list, its single-line preview and the table's "Tahsisi önizle".
    describe('the Create form — "Tahsisi önizle"', () => {
        const LOT_A = 'aaaaaaaa-0000-4000-8000-000000000001';
        const LOT_B = 'bbbbbbbb-0000-4000-8000-000000000002';
        const FORM_DOM = `<form id="outboundForm" data-form-mode="create">
            <div id="outbound-form-alert" class="d-none"></div>
            <select id="WarehouseId"><option value="11111111-1111-4111-8111-111111111111" selected>D01</option></select><div id="WarehouseIdError"></div>
            <select id="StagingLocationId"><option value="22222222-2222-4222-8222-222222222222" selected>A-01-01-01</option></select><div id="StagingLocationIdError"></div>
            <input id="OrderRef"><input id="Note"><input id="ShipToName"><input id="ShipToCountry"><input id="ShipToCity"><input id="ShipToPostalCode"><input id="ShipToLine1"><input id="ShipToLine2">
            <button type="button" id="btnAddLine"></button><button type="button" id="btnPreview"></button><small id="outbound-lines-limit"></small>
            <table id="outbound-lines-table" data-dt-standard="v2" data-dt-data-mode="client"><thead><tr>${'<th></th>'.repeat(8)}</tr></thead><tbody id="outbound-lines-body"></tbody></table>
            <div id="LinesError"></div><input name="__RequestVerificationToken" value="tok"></form>
            <div class="offcanvas offcanvas-end" id="offcanvasOutboundLine"><h5 id="offcanvasOutboundLineLabel"></h5><div id="lineAlert" class="d-none"></div>
            <select id="lineProduct"><option value=""></option></select><select id="lineSku"><option value=""></option></select><input id="lineQuantity"><small id="lineUom" data-template="{0}"></small>
            <input id="lineOrderLine"><select id="lineLot"></select><div id="lineReasonBlock" class="d-none"><textarea id="lineReason"></textarea><small id="lineReasonHint"></small></div>
            <div id="linePreview" class="d-none"><div id="linePreviewBody"></div></div><button type="button" id="btnLinePreview"></button><button type="button" id="btnLineSave"></button></div>`;

        const previewAnswer = (chosen) => ({
            fefoOutboundMode: 'RequireReason',
            lines: [{
                lineSeq: 1, orderLineId: '1', requested: '2.000', allocated: '2.000', allocatable: '408.500',
                allocations: [{ lotId: chosen || LOT_A, lotNumber: chosen ? 'G1-20261020' : 'AMX-2604', fefoRank: chosen ? 2 : 1, quantity: '2.000', plannedBins: [] }],
                lots: [{ lotId: LOT_A, lotNumber: 'AMX-2604', expiryDate: '2028-03-31', fefoRank: 1, allocatable: '408.500' },
                    { lotId: LOT_B, lotNumber: 'G1-20261020', expiryDate: '2028-10-20', fefoRank: 2, allocatable: '12.000' }],
                deviation: chosen ? { firstEligibleLotId: LOT_A, firstEligibleLotNumber: 'AMX-2604', firstEligibleExpiry: '2028-03-31', mode: 'RequireReason' } : null
            }]
        });

        /** The panel path: "Satır ekle" → product → SKU (its base unit EA from sku-facts) → quantity 2. */
        const fillLine = async () => {
            document.getElementById('btnAddLine').click();
            const choose = (id, value) => {
                const select = document.getElementById(id);
                select.add(new Option(value, value, true, true));
                select.dispatchEvent(new Event('change'));
            };
            choose('lineProduct', '33333333-3333-4333-8333-333333333333');
            choose('lineSku', 'Sku:44444444-4444-4444-8444-444444444444');
            await settle();
            document.getElementById('lineQuantity').value = '2';
        };
        const skuFacts = (url) => (String(url).includes('/sku-facts') ? reply({ available: true, baseUom: 'EA' }) : null);

        it('item 4: the chosen lot goes into the preview (lotOverride) and the line says it is not the FEFO suggestion; the choice survives the redraw', async () => {
            document.body.innerHTML = FORM_DOM;
            const fetchMock = await boot([js('OutboundShipments', 'rules.js'), js('OutboundShipments', 'form.js')], (url, init) => {
                if (skuFacts(url)) return skuFacts(url);
                const body = JSON.parse(init.body);
                return reply(previewAnswer(body.lines[0].lotOverride?.lotId));
            });
            await fillLine();
            document.getElementById('btnLinePreview').click();
            await settle();
            expect(posts(fetchMock, '/preview')[0].lines[0].lotOverride).toBeUndefined();
            expect(text('linePreviewBody')).toContain('Alınabilir: 408,5');
            expect(text('linePreviewBody')).toContain('Lot AMX-2604: 2 (FEFO sırası 1)');

            const lot = document.getElementById('lineLot');
            expect(lot.options[1].textContent).toBe('AMX-2604 · FEFO 1 · SKT 2028-03-31 · 408,5 alınabilir');
            lot.value = LOT_B;
            lot.dispatchEvent(new Event('change'));
            await settle();

            const second = posts(fetchMock, '/preview')[1];
            expect(second.lines[0].lotOverride).toEqual({ lotId: LOT_B, fefoBypassReason: null });
            expect(second.lines[0].quantity).toBe('2');
            const shown = text('linePreviewBody');
            expect(shown).toContain('Lot G1-20261020: 2 (FEFO sırası 2)');
            expect(shown).toContain('Seçtiğiniz lot kayıtta uygulanır; FEFO önerisi lot AMX-2604 (SKT 2028-03-31).');
            expect(document.getElementById('lineLot').value).toBe(LOT_B);

            // the line, saved with its reason, keeps the choice through the table's "Tahsisi önizle" too
            document.getElementById('lineReason').value = 'Müşteri bu lotu istedi';
            document.getElementById('btnLineSave').click();
            document.getElementById('btnPreview').click();
            await settle();
            const third = posts(fetchMock, '/preview')[2];
            expect(third.lines[0].lotOverride).toEqual({ lotId: LOT_B, fefoBypassReason: 'Müşteri bu lotu istedi' });
            expect(document.querySelector('#outbound-lines-body [data-line-lot]').textContent).toBe('G1-20261020');
        });

        it('item 4: a WMS_TARGET_NOT_ALLOWED on the preview points at the Staging field', async () => {
            document.body.innerHTML = FORM_DOM;
            await boot([js('OutboundShipments', 'rules.js'), js('OutboundShipments', 'form.js')],
                (url) => skuFacts(url) || reply({ error: { code: 'WMS_TARGET_NOT_ALLOWED', message: 'Hedef raf olmalı.' } }, 422));
            await fillLine();
            document.getElementById('btnLineSave').click();
            document.getElementById('btnPreview').click();
            await settle();
            expect(text('StagingLocationIdError')).toBe(WORDS.StagingNotAllowed);
            expect(text('outbound-form-alert')).toBe(WORDS.StagingNotAllowed);
        });
    });

    it('G-1: Packing shows tr "2" / "1,5", its edit form takes "1,5" back, and the package body sends "1.5"', async () => {
        document.body.innerHTML = `<div id="packing-root" data-outbound-id="${OUTBOUND.id}"><div id="packing-error"></div><span id="packing-outbound"></span><span id="packing-status"></span>
            <span id="packing-state"></span><button id="btnNewPackage"></button><table id="packing-allocations"><tbody></tbody></table><div id="packing-packages"></div>
            <div id="packing-packages-empty"></div></div><form id="packingActionToken"><input name="__RequestVerificationToken" value="tok"></form>
            <div id="offcanvasPackage"><h5 id="offcanvasPackageLabel"></h5><small id="package-subtitle"></small><div id="package-alert"></div><div id="package-contents"></div>
            <div id="packageContentsError"></div><input id="packageWeight"><input id="packageDimensions"><div id="packageDimensionsError"></div><button id="btnPackageSave"></button></div>`;
        const fetchMock = await boot([js('Packing', 'rules.js'), js('Packing', 'index.js')], (url, init) => (init?.method === 'POST'
            ? reply({ message: 'ok' })
            : reply({ outbound: { number: 'GMG-OUT-000001', status: 'Picked' },
                allocations: [{ allocSeq: 1, sku: 'LSKU-TR-000045', lotNumber: 'AMX-2604', picked: '2.000', packed: '0.000' }],
                packages: [{ id: 'p1', number: 'GMG-PKG-000001', status: 'Open', etag: 'W/"1"', contents: [{ allocSeq: 1, quantity: '1.500' }], weightKg: '1.500', dimensionsCm: '30.5x20x10' }] })));

        expect(Array.from(document.querySelectorAll('#packing-allocations td')).map((td) => td.textContent).slice(3)).toEqual(['2', '0', '0,5']);
        const card = document.querySelector('#packing-packages [data-package-id="p1"]').textContent;
        expect(card).toContain('Tahsis 1: 1,5');
        expect(card).toContain('Ağırlık (kg): 1,5');
        expect(card).toContain('Ölçü (cm): 30,5x20x10');

        document.querySelector('[data-package-action="Edit"]').click();
        expect(document.getElementById('packageQty1').value).toBe('1,5');
        expect(document.getElementById('packageWeight').value).toBe('1,5');
        expect(document.getElementById('packageDimensions').value).toBe('30,5x20x10');
        document.getElementById('packageWeight').value = '2,25';
        document.getElementById('btnPackageSave').click();
        await settle();
        const [body] = posts(fetchMock, '/Warehouse/Packing/edit/');
        expect(body).toMatchObject({ contents: [{ allocSeq: 1, quantity: '1.5' }], weightKg: '2.25', dimensionsCm: '30.5x20x10' });
    });

    it('G-1: PickTasks shows tr "2 / 2 EA", the confirm panel offers "1,5" and the confirmation sends "1.5"', async () => {
        document.body.innerHTML = `<table class="datatables-picktasks"></table><form id="pickActionToken"><input name="__RequestVerificationToken" value="tok"></form>
            <div id="offcanvasDetailsPreview">${['offcanvasDetailsPreviewLabel', 'oc-subtitle', 'oc-error', 'oc-status', 'oc-flag', 'oc-outbound', 'oc-origin', 'oc-sku', 'oc-lot',
            'oc-warehouse', 'oc-from', 'oc-to', 'oc-planned', 'oc-confirmed', 'oc-pending', 'oc-remaining', 'oc-assignee', 'oc-confirmations-empty', 'oc-timeline-unavailable']
            .map((id) => `<span id="${id}"></span>`).join('')}<a id="oc-pack-link"></a><div id="oc-confirmations"></div><ul id="oc-timeline"></ul><div id="oc-actions"></div></div>
            <div id="offcanvasConfirm"><small id="confirm-subtitle"></small><div id="confirm-alert"></div><form id="formConfirm"><input id="confirmQuantity"></form>
            <small id="confirm-remaining"></small><div id="confirmQuantityError"></div><span id="confirm-from"></span><span id="confirm-to"></span>
            <button id="btnConfirmRetry"></button><button id="btnConfirmSubmit"></button></div>`;
        const task = { id: 't1', etag: 'W/"3"', number: 'GMG-PCK-000001', status: 'Open', sku: 'LSKU-TR-000045', uom: 'EA', planned: '2.000', confirmed: '0.500',
            pending: '0.000', remaining: '1.500', allowedActions: ['Confirm'], fromLocation: 'A-01-01-01', toLocation: 'A-01-01-02' };
        let options = null;
        const fetchMock = await boot([js('PickTasks', 'rules.js'), js('PickTasks', 'index.js')], (url, init) => (init?.method === 'POST'
            ? reply({ message: 'ok' })
            : reply({ task, etag: 'W/"3"', confirmations: [{ seq: 1, status: 'Posted', quantity: '0.500', toLocation: 'A-01-01-02' }], timeline: [] })),
        { createList: async (o) => { options = o; return { dt: { ajax: { reload() {} } } }; } });

        const quantityColumn = options.config.columnDefs.find((d) => options.config.columns[d.targets]?.name === 'quantity');
        expect(quantityColumn.render(task.planned, 'display', task)).toBe('0,5 / 2 EA');

        await options.quickView.populate(task);
        await settle();
        expect(text('oc-planned')).toBe('2 EA');
        expect(text('oc-remaining')).toBe('1,5');
        expect(text('oc-confirmations')).toContain('0,5 → A-01-01-02');

        document.querySelector('[data-oc-action="Confirm"]').click();
        expect(document.getElementById('confirmQuantity').value).toBe('1,5');
        expect(text('confirm-remaining')).toBe('Kalan: 1,5 EA');
        document.getElementById('btnConfirmSubmit').click();
        await settle();
        expect(posts(fetchMock, '/Warehouse/PickTasks/confirm/')).toEqual([expect.objectContaining({ quantity: '1.5', ifMatch: 'W/"3"' })]);
    });
});
