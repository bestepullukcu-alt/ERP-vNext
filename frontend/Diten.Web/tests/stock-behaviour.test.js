const fs = require('fs');
const path = require('path');
const vm = require('node:vm');

/*
 * MOD-0290-FU04 S5 (O-12) — the shared "Stock behaviour" module (StockBehaviour/stock-behaviour.js) executed against
 * the production offcanvas markup (the level's _StockBehaviourOffcanvas.cshtml with its Razor words replaced by their
 * keys) and scripted HTTP: O-8 locks drawn read-only with their reason, add-only lists, only the changed fields posted
 * with the version and the form attempt, the detail read back after a save.
 */
describe('Stock behaviour section and offcanvas (O-12, O-8)', () => {
    const id = '7b9a4d8e-0f4c-4a52-9f61-2f4b1c7d9e01';
    const views = path.join(__dirname, '../Views/MasterDataManagement');
    const words = ['FillIn', 'Yes', 'No', 'NotSet', 'SourceGsku', 'LockedReason', 'AddOnlyReason', 'NotEditable', 'BaseUom',
        'ShelfLife', 'StorageConditions', 'LotControlled', 'SerialControlled', 'Gtin', 'StockableFlag', 'MissingCaseBox',
        'MissingGskuBaseUom', 'MissingGskuStockable', 'DerivedPackConversion', 'RemoveConversion', 'ConversionFrom',
        'ConversionTo', 'ConversionNumerator', 'ConversionDenominator', 'Save', 'SaveConfirmation', 'Saved', 'NoChanges',
        'SaveFailed', 'StateChanged', 'InvalidCode', 'InvalidDays', 'InvalidConversion', 'InvalidGtin', 'ListCannotBeEmpty'];
    const l10n = `<script id="stock-behaviour-l10n" type="application/json">${JSON.stringify(Object.fromEntries(words.map(w => [w, w])))}</script>`;
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

    function harness(level, folder, { withOffcanvas = true, details = [] } = {}) {
        document.body.innerHTML = section + (withOffcanvas ? offcanvas(folder) : '');
        const queue = [...details];
        const fetchDetail = vi.fn(async () => {
            const next = queue.shift();
            if (!next) throw new Error('Unexpected detail read');
            return next;
        });
        const fetch = vi.fn();
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
        return { module, fetch, fetchDetail, toast, shown, hidden, onSaved };
    }
    const $ = (selector) => document.querySelector(selector);
    const posted = (call) => Object.fromEntries([...call[1].body.entries()]);

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
            .toEqual(['1 XCS = 12 XBX', '1 XBX = 10 C62 — DerivedPackConversion']);
        expect($('[data-sb-lock="shelfLifeDays"]').classList.contains('d-none')).toBe(false);
        expect($('[data-sb-lock="lotControlled"]').classList.contains('d-none')).toBe(true);
        expect($('[data-sb-source="baseUomCode"]').classList.contains('d-none')).toBe(false);
        expect($('[data-sb-missing-row]').classList.contains('d-none')).toBe(false);
        // A missing name the screen does not know is never shown raw.
        expect([...$('[data-sb-missing]').children].map(x => x.textContent)).toEqual(['StockableFlag']);
        expect($('#sb-edit').classList.contains('d-none')).toBe(false);
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
        expect(stored.querySelector('[data-sb-remove]')).toBeNull();
        expect($('[data-sb-addonly-reason="uomConversions"]').classList.contains('d-none')).toBe(false);
        expect([...$('[data-sb-existing="storageConditionCodes"]').children].map(x => x.textContent)).toEqual(['COOL_2_8']);
        expect($('[data-sb-input="storageConditionCodes"]').value).toBe('');

        $('#sb-add-conversion').click();
        const added = $('[data-sb-conversion-row="open"]');
        const set = (part, value) => { added.querySelector(`[data-sb-part="${part}"]`).value = value; };
        set('fromUom', 'xpl'); set('toUom', 'XCS'); set('numerator', '40'); set('denominator', '1');
        $('[data-sb-input="storageConditionCodes"]').value = 'protect_from_light';
        h.fetch.mockResolvedValueOnce(saved(4));
        h.module.save();
        await vi.waitFor(() => expect(h.toast).toHaveBeenCalledWith('Saved', 'success'));

        const body = posted(h.fetch.mock.calls[0]);
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

        const [url, options] = h.fetch.mock.calls[0];
        expect(url).toBe(`/MasterDataManagement/Gskus/api/${id}/stock-behaviour`);
        expect(options.method).toBe('PUT');
        expect(options.headers.RequestVerificationToken).toBe('test-csrf');
        const body = posted(h.fetch.mock.calls[0]);
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
        const [first, second] = h.fetch.mock.calls.map(posted);
        expect(second.FormAttemptId).toBe(first.FormAttemptId);

        await h.module.open(id);
        $('[data-sb-input="retestDays"]').value = '31';
        h.fetch.mockResolvedValueOnce(saved(5));
        // The reopened form is at the version read back (4).
        h.module.save();
        await vi.waitFor(() => expect(h.fetch).toHaveBeenCalledTimes(3));
        expect(posted(h.fetch.mock.calls[2]).FormAttemptId).not.toBe(first.FormAttemptId);
    });

    it('nothing changed → nothing sent; a malformed code is refused in words before any request', async () => {
        const h = harness('gsku', 'Gskus', { details: [detail(stockBehaviour())] });
        await h.module.open(id);
        h.module.save();
        expect(h.toast).toHaveBeenCalledWith('NoChanges', 'info');
        $('[data-sb-input="storageConditionCodes"]').value = 'COOL 2-8';
        h.module.save();
        expect(h.toast).toHaveBeenCalledWith('InvalidCode', 'error');
        expect(h.fetch).not.toHaveBeenCalled();
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
        expect(h.fetch.mock.calls.map(c => c[0])).toEqual([
            `/MasterDataManagement/Gskus/api/${id}/stock-behaviour`, `/MasterDataManagement/Gskus/api/${id}/gtin`]);
        const [stock, gtin] = h.fetch.mock.calls.map(posted);
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
        const body = posted(h.fetch.mock.calls[0]);
        expect(body.SerialControlled).toBe('false');
        expect(Object.keys(body).sort()).toEqual(['ExpectedVersion', 'FormAttemptId', 'SerialControlled', '__RequestVerificationToken']);
    });
});
