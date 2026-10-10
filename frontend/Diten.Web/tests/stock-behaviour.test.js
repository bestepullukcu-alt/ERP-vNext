const fs = require('fs');
const path = require('path');
const vm = require('node:vm');

/*
 * MOD-0290-FU04 S5 (O-12) — the shared "Stock behaviour" module (StockBehaviour/stock-behaviour.js) executed against
 * the production offcanvas markup (the level's _StockBehaviourOffcanvas.cshtml with its Razor words replaced by their
 * keys) and scripted HTTP: O-8 locks drawn read-only with their reason, add-only lists, only the changed fields posted
 * with the version and the form attempt, the detail read back after a save. S5b (BL-600): units and storage conditions
 * are selects filled from the options read (scripted here), labelled by code; O-2; without the lists they stay passive
 * with the reason — never a code input. S5-FIX1: a Finished Good whose GTIN request fails after its stock save is read back.
 */
describe('Stock behaviour section and offcanvas (O-12, O-8)', () => {
    const id = '7b9a4d8e-0f4c-4a52-9f61-2f4b1c7d9e01';
    const views = path.join(__dirname, '../Views/MasterDataManagement');
    const words = ['FillIn', 'Yes', 'No', 'NotSet', 'SourceGsku', 'LockedReason', 'AddOnlyReason', 'NotEditable', 'BaseUom',
        'ShelfLife', 'StorageConditions', 'LotControlled', 'SerialControlled', 'Gtin', 'StockableFlag', 'MissingCaseBox',
        'MissingGskuBaseUom', 'MissingGskuStockable', 'DerivedPackConversion', 'RemoveConversion', 'ConversionFrom',
        'ConversionTo', 'ConversionNumerator', 'ConversionDenominator', 'Save', 'SaveConfirmation', 'Saved', 'NoChanges',
        'SaveFailed', 'StateChanged', 'InvalidCode', 'InvalidDays', 'InvalidConversion', 'InvalidGtin', 'ListCannotBeEmpty',
        'Uom_C62', 'Uom_XBX', 'Uom_XCS', 'Storage_COOL_2_8', 'Storage_AMBIENT_15_25', 'Storage_PROTECT_FROM_LIGHT',
        'OptionsUnavailable', 'TemperatureOnlyOne', 'SelectUnit'];
    const l10n = `<script id="stock-behaviour-l10n" type="application/json">${JSON.stringify(Object.fromEntries(words.map(w => [w, w])))}</script>`;
    const optionsUrl = '/MasterDataManagement/Items/api/options';
    const option = (code, attributes = {}) => ({ code, sortOrder: 0, attributes });
    const optionsAnswer = {
        status: 200, ok: true, json: async () => ({
            success: true, data: {
                materialTypes: [option('EXCIPIENT')],
                uoms: [option('C62'), option('XBX'), option('XCS'), option('XPL')],
                storageConditions: [option('AMBIENT_15_25', { ConditionKind: 'TEMPERATURE' }),
                    option('COOL_2_8', { ConditionKind: 'TEMPERATURE' }), option('PROTECT_FROM_LIGHT', { ConditionKind: 'PROTECTION' })]
            }
        })
    };
    // The production markup, its Razor words as their keys.
    const offcanvas = (folder) => fs.readFileSync(path.join(views, folder, '_StockBehaviourOffcanvas.cshtml'), 'utf8')
        .replace(/@\*[\s\S]*?\*@/g, '')
        .replace(/^@(using|inject|model)\b.*$/gm, '')
        .replace(/@Html\.AntiForgeryToken\(\)/g, '<input name="__RequestVerificationToken" value="test-csrf">')
        .replace(/<partial [^>]*\/>/g, l10n)
        .replace(/@Stock\["(\w+)"\]/g, '$1');
    const section = '<section id="sb-section"><button id="sb-edit" class="d-none"></button>' +
        ['baseUomCode', 'uomConversions', 'shelfLifeDays', 'storageConditionCodes', 'lotControlled', 'stockable']
            .map(f => `<span data-sb-source="${f}" class="d-none"></span><i data-sb-lock="${f}" class="d-none"></i><div data-sb-value="${f}"></div>`).join('') +
        '<div data-sb-missing-row class="d-none"><div data-sb-missing></div></div></section>';

    const stockBehaviour = (overrides = {}) => ({
        baseUomCode: 'C62', baseUomSource: 'Own',
        uomConversions: [{ fromUom: 'XCS', toUom: 'XBX', numerator: '12', denominator: '1' }],
        derivedPackConversion: { fromUom: 'XBX', toUom: 'C62', numerator: '10', denominator: '1' },
        shelfLifeDays: 730, shelfLifeDaysSource: 'Own', retestDays: null, minRemainingShelfLifeDays: null, daysSource: 'Own',
        storageConditionCodes: ['COOL_2_8'], lotControlled: true, lotControlledSource: 'Own', serialControlled: null,
        gtin: null, stockableFlag: null, stockable: false, missing: ['stockable', 'somethingUnknown'],
        lockedFields: [], addOnlyFields: [], editable: true, ...overrides
    });
    const detail = (sb, version = 3) => ({ id, canonicalCode: 'GSKU-1', gskuVersion: version, version, stockBehaviour: sb });
    const ok = (body, status = 200) => ({ status, ok: status >= 200 && status < 300, json: async () => body });
    const saved = (version) => ok({ data: { recordId: id, version, changed: true, isReplay: false }, isSuccessful: true, statusCode: 200 });

    function harness(level, folder, { withOffcanvas = true, details = [], options = optionsAnswer } = {}) {
        document.body.innerHTML = section + (withOffcanvas ? offcanvas(folder) : '');
        const queue = [...details];
        const fetchDetail = vi.fn(async () => {
            const next = queue.shift();
            if (!next) throw new Error('Unexpected detail read');
            return next;
        });
        // The options read is answered by URL; the writes take the answers each test queues (mockResolvedValueOnce).
        const fetch = vi.fn(async (url) => {
            if (url === optionsUrl) return options;
            throw new Error(`Unexpected HTTP call ${url}`);
        });
        const toast = vi.fn();
        const shown = vi.fn();
        const hidden = vi.fn();
        const onSaved = vi.fn();
        const window = { showToast: toast, showConfirm: (_, run) => run() };
        const source = fs.readFileSync(path.join(__dirname,
            '../wwwroot/assets/js/MasterDataManagement/StockBehaviour/stock-behaviour.js'), 'utf8');
        vm.runInNewContext(source, {
            window, document, fetch, FormData, crypto: globalThis.crypto, console,
            bootstrap: { Offcanvas: { getOrCreateInstance: () => ({ show: shown, hide: hidden }), getInstance: () => null } }
        });
        const module = window.DitenStockBehaviour.create({
            level, endpoint: '/MasterDataManagement/Gskus/api', versionField: level === 'gsku' ? 'gskuVersion' : 'version',
            fetchDetail, onSaved
        });
        const writes = () => fetch.mock.calls.filter((call) => call[0] !== optionsUrl);
        return { module, fetch, fetchDetail, toast, shown, hidden, onSaved, writes };
    }
    const $ = (selector) => document.querySelector(selector);
    const posted = (call) => Object.fromEntries([...call[1].body.entries()]);
    const choose = (select, codes) => {
        [...select.options].forEach((x) => { x.selected = codes.includes(x.value); });
        select.dispatchEvent(new Event('change'));
    };

    it('is not created without its offcanvas — a user without the stock-edit key gets nothing', () => {
        expect(harness('gsku', 'Gskus', { withOffcanvas: false }).module).toBeNull();
    });

    it('the section says the values, the source, the locks and — when not stockable — what is missing, by name', () => {
        const h = harness('gsku', 'Gskus');
        h.module.render(detail(stockBehaviour({ lockedFields: ['shelfLifeDays'], baseUomSource: 'Gsku' })));
        expect($('[data-sb-value="shelfLifeDays"]').textContent).toBe('730');
        expect($('[data-sb-value="lotControlled"]').textContent).toBe('Yes');
        expect($('[data-sb-value="stockable"]').textContent).toBe('No');
        expect([...$('[data-sb-value="uomConversions"]').children].map(x => x.textContent))
            .toEqual(['1 Uom_XCS (XCS) = 12 Uom_XBX (XBX)', '1 Uom_XBX (XBX) = 10 Uom_C62 (C62) — DerivedPackConversion']);
        expect($('[data-sb-lock="shelfLifeDays"]').classList.contains('d-none')).toBe(false);
        expect($('[data-sb-lock="lotControlled"]').classList.contains('d-none')).toBe(true);
        expect($('[data-sb-source="baseUomCode"]').classList.contains('d-none')).toBe(false);
        expect($('[data-sb-missing-row]').classList.contains('d-none')).toBe(false);
        // A missing name the screen does not know is never shown raw.
        expect([...$('[data-sb-missing]').children].map(x => x.textContent)).toEqual(['StockableFlag']);
        expect($('#sb-edit').classList.contains('d-none')).toBe(false);
    });

    // S5c: the conversion lines, the base unit and the storage conditions by name with the code beside it; a unit the
    // vocabulary does not name yet (a newer catalog value) by its code. The sabotage target "the label lookup removed".
    it('S5c: the quick view names the units and storage conditions, the code beside them; an unnamed unit by its code', () => {
        const h = harness('gsku', 'Gskus');
        h.module.render(detail(stockBehaviour({
            uomConversions: [{ fromUom: 'XCS', toUom: 'XPL', numerator: '4', denominator: '1' }], derivedPackConversion: null
        })));

        expect([...$('[data-sb-value="uomConversions"]').children].map(x => x.textContent)).toEqual(['1 Uom_XCS (XCS) = 4 XPL']);
        expect($('[data-sb-value="baseUomCode"]').textContent).toBe('Uom_C62 (C62)');
        expect($('[data-sb-value="storageConditionCodes"]').textContent).toBe('Storage_COOL_2_8 (COOL_2_8)');
    });

    it('O-8: on an approved record a locked field is drawn read-only and says why; the others stay free', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour({ lockedFields: ['shelfLifeDays', 'lotControlled', 'baseUomCode'] }))] });
        await h.module.open(id);
        for (const field of ['shelfLifeDays', 'lotControlled', 'baseUomCode']) {
            const input = $(`[data-sb-input="${field}"]`);
            expect(input.disabled && input.readOnly).toBe(true);
            expect($(`[data-sb-lock-reason="${field}"]`).classList.contains('d-none')).toBe(false);
            expect($(`[data-sb-lock-reason="${field}"]`).textContent).toContain('LockedReason');
        }
        expect($('[data-sb-input="retestDays"]').disabled).toBe(false);
        expect($('[data-sb-lock-reason="retestDays"]').classList.contains('d-none')).toBe(true);
        expect(h.shown).toHaveBeenCalled();
    });

    it('O-8: an add-only list keeps its stored rows read-only, gains new ones and is sent whole', async () => {
        const approved = stockBehaviour({ addOnlyFields: ['uomConversions', 'storageConditionCodes'], lockedFields: ['shelfLifeDays'] });
        const h = harness('gsku', 'Gskus', { details: [detail(approved), detail(approved, 4)] });
        await h.module.open(id);
        const stored = $('[data-sb-conversion-row="stored"]');
        expect([...stored.querySelectorAll('input')].every(x => x.readOnly && x.disabled)).toBe(true);
        expect([...stored.querySelectorAll('select')].every(x => x.disabled)).toBe(true);
        expect([...stored.querySelectorAll('select')].map(x => x.value)).toEqual(['XCS', 'XBX']);
        expect(stored.querySelector('[data-sb-remove]')).toBeNull();
        expect($('[data-sb-addonly-reason="uomConversions"]').classList.contains('d-none')).toBe(false);
        // The stored storage conditions stand as labelled badges; the select offers only what is not stored yet.
        expect([...$('[data-sb-existing="storageConditionCodes"]').children].map(x => x.textContent)).toEqual(['Storage_COOL_2_8']);
        expect([...$('[data-sb-input="storageConditionCodes"]').options].map(x => x.value)).toEqual(['AMBIENT_15_25', 'PROTECT_FROM_LIGHT']);
        expect($('[data-sb-input="storageConditionCodes"]').value).toBe('');

        $('#sb-add-conversion').click();
        const added = $('[data-sb-conversion-row="open"]');
        const set = (part, value) => { added.querySelector(`[data-sb-part="${part}"]`).value = value; };
        set('fromUom', 'XPL'); set('toUom', 'XCS'); set('numerator', '40'); set('denominator', '1');
        choose($('[data-sb-input="storageConditionCodes"]'), ['PROTECT_FROM_LIGHT']);
        h.fetch.mockResolvedValueOnce(saved(4));
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('Saved', 'success'));

        const body = posted(h.writes()[0]);
        expect(JSON.parse(body.UomConversions)).toEqual([
            { fromUom: 'XCS', toUom: 'XBX', numerator: '12', denominator: '1' },
            { fromUom: 'XPL', toUom: 'XCS', numerator: '40', denominator: '1' }]);
        expect(body.StorageConditionCodes).toBe('COOL_2_8,PROTECT_FROM_LIGHT');
    });

    it('posts only the changed fields, with the version, the form attempt and the anti-forgery token; reads the detail back', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour()), detail(stockBehaviour({ retestDays: 30 }), 4)] });
        await h.module.open(id);
        $('[data-sb-input="retestDays"]').value = '30';
        $('[data-sb-input="lotControlled"]').value = 'true'; // unchanged — not sent
        h.fetch.mockResolvedValueOnce(saved(4));
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('Saved', 'success'));

        const [url, options] = h.writes()[0];
        expect(url).toBe(`/MasterDataManagement/Gskus/api/${id}/stock-behaviour`);
        expect(options.method).toBe('PUT');
        expect(options.headers.RequestVerificationToken).toBe('test-csrf');
        const body = posted(h.writes()[0]);
        expect(Object.keys(body).sort()).toEqual(['ExpectedVersion', 'FormAttemptId', 'RetestDays', '__RequestVerificationToken']);
        expect(body.ExpectedVersion).toBe('3');
        expect(body.RetestDays).toBe('30');
        expect(body.FormAttemptId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
        expect(h.fetchDetail).toHaveBeenCalledTimes(2);
        expect(h.hidden).toHaveBeenCalled();
        expect(h.onSaved).toHaveBeenCalledWith(id);
    });

    it('a repeat of the same attempt sends the same form attempt; a new opening draws a new one', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour()), detail(stockBehaviour(), 4), detail(stockBehaviour(), 4)] });
        await h.module.open(id);
        $('[data-sb-input="retestDays"]').value = '30';
        h.fetch.mockResolvedValueOnce(ok({ success: false, errors: ['A sentence from the proxy'] }, 422))
            .mockResolvedValueOnce(saved(4));
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('A sentence from the proxy', 'error'));
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('Saved', 'success'));
        const [first, second] = h.writes().map(posted);
        expect(second.FormAttemptId).toBe(first.FormAttemptId);

        await h.module.open(id);
        $('[data-sb-input="retestDays"]').value = '31';
        h.fetch.mockResolvedValueOnce(saved(5));
        // The reopened form is at the version read back (4).
        h.module.save();
        await vi.waitFor(() => expect(h.writes()).toHaveLength(3));
        expect(posted(h.writes()[2]).FormAttemptId).not.toBe(first.FormAttemptId);
    });

    it('nothing changed → nothing sent; a conversion between one unit and itself is refused in words before any request', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour())] });
        await h.module.open(id);
        h.module.save();
        expect(h.toast).toHaveBeenCalledWith('NoChanges', 'info');
        $('#sb-add-conversion').click();
        const added = $('[data-sb-conversion-row="open"]');
        added.querySelector('[data-sb-part="fromUom"]').value = 'XBX';
        added.querySelector('[data-sb-part="toUom"]').value = 'XBX';
        added.querySelector('[data-sb-part="numerator"]').value = '2';
        added.querySelector('[data-sb-part="denominator"]').value = '1';
        h.module.save();
        expect(h.toast).toHaveBeenCalledWith('InvalidConversion', 'error');
        expect(h.writes()).toHaveLength(0);
    });

    it('a record whose status allows no edit opens read-only, says so, and cannot be saved', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour({ editable: false }))] });
        await h.module.open(id);
        expect($('#sb-form-not-editable').classList.contains('d-none')).toBe(false);
        expect($('#btnSaveStockBehaviour').disabled).toBe(true);
        expect([...document.querySelectorAll('[data-sb-input]')].filter(x => x.tagName !== 'DIV').every(x => x.disabled)).toBe(true);
    });

    it('the Finished Good sends its stock fields, then its GTIN as its own request at the new version', async () => {
        const fg = stockBehaviour({ baseUomSource: 'Gsku', shelfLifeDaysSource: 'Gsku', serialControlled: null });
        const h = harness('fg', 'FinishedGoods', { details: [detail(fg), detail(fg, 5)] });
        await h.module.open(id);
        // D-2: an inherited shelf life is the GSKU's — not the Finished Good's own value, shown only as a hint.
        expect($('[data-sb-input="shelfLifeDays"]').value).toBe('');
        expect($('[data-sb-input="shelfLifeDays"]').placeholder).toBe('730');
        $('[data-sb-input="serialControlled"]').value = 'true';
        $('[data-sb-input="gtin"]').value = '04012345678901';
        h.fetch.mockResolvedValueOnce(saved(4)).mockResolvedValueOnce(saved(5));
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('Saved', 'success'));
        expect(h.writes().map(c => c[0])).toEqual([
            `/MasterDataManagement/Gskus/api/${id}/stock-behaviour`, `/MasterDataManagement/Gskus/api/${id}/gtin`]);
        const [stock, gtin] = h.writes().map(posted);
        expect(stock.SerialControlled).toBe('true');
        expect(stock.Gtin).toBeUndefined();
        expect(gtin.Gtin).toBe('04012345678901');
        expect(gtin.ExpectedVersion).toBe('4');
    });

    it('the LSKU form is its serial flag only', async () => {
        const h = harness('lsku', 'Lskus', { details: [detail(stockBehaviour({ lockedFields: [] }))] });
        await h.module.open(id);
        expect([...document.querySelectorAll('[data-sb-input]')].map(x => x.dataset.sbInput)).toEqual(['serialControlled']);
        $('[data-sb-input="serialControlled"]').value = 'false';
        h.fetch.mockResolvedValueOnce(saved(4));
        h.module.save();
        await vi.waitFor(() => expect(h.fetch).toHaveBeenCalledTimes(1));
        // The LSKU offers no list: the options are never read.
        expect(h.fetch.mock.calls[0][0]).not.toBe(optionsUrl);
        const body = posted(h.fetch.mock.calls[0]);
        expect(body.SerialControlled).toBe('false');
        expect(Object.keys(body).sort()).toEqual(['ExpectedVersion', 'FormAttemptId', 'SerialControlled', '__RequestVerificationToken']);
    });
    it('S5b: the base unit, the conversion units and the storage conditions are selects from the options, labelled by code', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour())] });
        await h.module.open(id);

        expect(h.fetch.mock.calls.filter(c => c[0] === optionsUrl)).toHaveLength(1);
        const base = $('[data-sb-input="baseUomCode"]');
        const storage = $('[data-sb-input="storageConditionCodes"]');
        expect([base.tagName, storage.tagName, storage.multiple]).toEqual(['SELECT', 'SELECT', true]);
        // In the list's order; a code without a label (XPL) is shown as itself.
        expect([...base.options].map(x => x.textContent)).toEqual(['SelectUnit', 'Uom_C62', 'Uom_XBX', 'Uom_XCS', 'XPL']);
        expect(base.value).toBe('C62');
        expect([...storage.selectedOptions].map(x => x.value)).toEqual(['COOL_2_8']);
        expect([base.disabled, storage.disabled]).toEqual([false, false]);
        expect([...document.querySelectorAll('[data-sb-part="fromUom"], [data-sb-part="toUom"]')].every(x => x.tagName === 'SELECT')).toBe(true);
        expect([...document.querySelectorAll('[data-sb-options-reason]')].every(x => x.classList.contains('d-none'))).toBe(true);

        // A second opening reads no list again.
        h.fetchDetail.mockResolvedValueOnce(detail(stockBehaviour()));
        await h.module.open(id);
        expect(h.fetch.mock.calls.filter(c => c[0] === optionsUrl)).toHaveLength(1);
    });

    it('S5b O-2: a second temperature condition is refused in words and taken back — a stored add-only one counts', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour({ storageConditionCodes: [] })),
            detail(stockBehaviour({ addOnlyFields: ['storageConditionCodes'] }))] });
        await h.module.open(id);
        const storage = $('[data-sb-input="storageConditionCodes"]');
        choose(storage, ['AMBIENT_15_25']);
        choose(storage, ['AMBIENT_15_25', 'COOL_2_8']);
        expect([...storage.selectedOptions].map(x => x.value)).toEqual(['AMBIENT_15_25']);
        expect($('#sb-storage-temperature-note').classList.contains('d-none')).toBe(false);
        expect($('#sb-storage-temperature-note').textContent).toBe('TemperatureOnlyOne');

        // Approved: COOL_2_8 is stored (add-only), so no other temperature may be added.
        await h.module.open(id);
        const again = $('[data-sb-input="storageConditionCodes"]');
        expect(again.querySelector('option[value="AMBIENT_15_25"]').disabled).toBe(true);
        choose(again, ['AMBIENT_15_25', 'PROTECT_FROM_LIGHT']);
        expect([...again.selectedOptions].map(x => x.value)).toEqual(['PROTECT_FROM_LIGHT']);
        expect($('#sb-storage-temperature-note').textContent).toBe('TemperatureOnlyOne');
    });

    // Sabotage target "the form falls back to a code input when the list is missing".
    it('S5b: without the lists the list fields stay passive selects with the reason — never a code input; the rest saves', async () => {
        const refused = { status: 503, ok: false, json: async () => ({ success: false, errors: ['The lists cannot be loaded.'] }) };
        const h = harness('gsku', 'Gskus', { options: refused, details: [detail(stockBehaviour()), detail(stockBehaviour({ retestDays: 30 }), 4)] });
        await h.module.open(id);

        for (const field of ['baseUomCode', 'storageConditionCodes']) {
            expect($(`[data-sb-input="${field}"]`).tagName).toBe('SELECT');
            expect($(`[data-sb-input="${field}"]`).disabled).toBe(true);
        }
        expect(document.querySelector('input[data-sb-input="baseUomCode"], input[data-sb-input="storageConditionCodes"]')).toBeNull();
        expect(document.querySelector('input[data-sb-part="fromUom"], input[data-sb-part="toUom"]')).toBeNull();
        // The stored values stay visible in the passive selects.
        expect($('[data-sb-input="baseUomCode"]').value).toBe('C62');
        expect($('#sb-add-conversion').disabled).toBe(true);
        const reasons = [...document.querySelectorAll('[data-sb-options-reason]')];
        expect(reasons.length).toBeGreaterThanOrEqual(3);
        expect(reasons.every(x => !x.classList.contains('d-none') && x.textContent === 'The lists cannot be loaded.')).toBe(true);

        expect($('[data-sb-input="retestDays"]').disabled).toBe(false);
        $('[data-sb-input="retestDays"]').value = '30';
        h.fetch.mockResolvedValueOnce(saved(4));
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('Saved', 'success'));
        const body = posted(h.writes()[0]);
        expect(Object.keys(body).sort()).toEqual(['ExpectedVersion', 'FormAttemptId', 'RetestDays', '__RequestVerificationToken']);
    });

    it('S5-FIX1: the stock part saved and the GTIN refused — the detail is read back, the editor moves to it, the GTIN sentence is said', async () => {
        const fg = stockBehaviour({ baseUomSource: 'Gsku', shelfLifeDaysSource: 'Gsku', serialControlled: null });
        const after = stockBehaviour({ baseUomSource: 'Gsku', shelfLifeDaysSource: 'Gsku', serialControlled: true });
        const h = harness('fg', 'FinishedGoods', { details: [detail(fg), detail(after, 4)] });
        await h.module.open(id);
        $('[data-sb-input="serialControlled"]').value = 'true';
        $('[data-sb-input="gtin"]').value = '04012345678901';
        h.fetch.mockResolvedValueOnce(saved(4))
            .mockResolvedValueOnce({ status: 409, ok: false, json: async () => ({ success: false, errors: ['This GTIN is already used.'] }) });
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('This GTIN is already used.', 'error'));

        expect(h.fetchDetail).toHaveBeenCalledTimes(2);
        expect(h.onSaved).toHaveBeenCalledWith(id);
        expect(h.hidden).not.toHaveBeenCalled();
        // The editor stands at the version read back: the serial flag is no longer a change, the typed GTIN stays.
        expect($('[data-sb-input="gtin"]').value).toBe('04012345678901');
        h.fetch.mockResolvedValueOnce(saved(5));
        h.fetchDetail.mockResolvedValueOnce(detail(after, 5));
        h.module.save();
        await vi.waitFor(() => expect(h.writes()).toHaveLength(3));
        expect(h.writes()[2][0]).toBe(`/MasterDataManagement/Gskus/api/${id}/gtin`);
        expect(posted(h.writes()[2]).ExpectedVersion).toBe('4');
    });
});
