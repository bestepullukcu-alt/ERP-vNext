const fs = require('fs');
const path = require('path');
const vm = require('node:vm');

// MOD-0290-FU04 S5 — the Items screen's executed browser contracts (isolated DOM and HTTP fixtures, the gsku-register
// harness): actions are drawn only from the server's availableActions, an unknown action is never drawn, a lifecycle
// request re-reads the item, posts its version and the reason, and reads the answer back.
describe('Items executed browser contracts', () => {
    const id = '12345678-1234-1234-1234-123456789012';
    const item = (actions, status = 3, version = 4) => ({
        id, canonicalCode: 'IT-000000000003', name: 'Synthetic', materialType: 'EXCIPIENT', baseUomCode: 'KGM',
        uomConversions: [], storageConditionCodes: ['COOL_2_8'], lotControlled: true, stockable: false,
        lifecycleStatus: status, version, availableActions: actions
    });
    const response = (data, status = 200) => ({
        status, ok: status >= 200 && status < 300,
        json: async () => status < 300 ? { data, isSuccessful: true, statusCode: status } : data
    });

    function harness(responses = [], body = '') {
        document.body.innerHTML = '<form id="itemLifecycleToken"><input name="__RequestVerificationToken" value="synthetic-csrf"></form>' + body;
        const fetch = vi.fn(async () => {
            const next = responses.shift();
            if (!next) throw new Error('Unexpected HTTP call');
            return next;
        });
        const toast = vi.fn();
        const reload = vi.fn();
        let pending;
        const fakeWindow = {
            L10n: new Proxy({}, { get: (_, key) => String(key) }),
            showToast: toast,
            showConfirm: vi.fn((_, callback) => { pending = callback('  Supplier stopped  '); }),
            // The list component's own renderer is a separate file: here it lists what it was handed.
            DitenDataTable: { renderActions: (actions) => actions.map((a) => `<a data-row-action="${a.key}" data-action="${a.attrs['data-action']}" class="${a.className}"></a>`).join('') },
            location: { href: '' }
        };
        const source = fs.readFileSync(path.join(__dirname, '../wwwroot/assets/js/MasterDataManagement/Items/index.js'), 'utf8');
        // Test-only access to existing closure functions; shipped JS has no test hook.
        const exposed = source.replace('init: async () => {', `test: {
            readAvailableActions, renderRowActions, requestLifecycle, buildListOptions, getErrorMessage, renderRecord,
            setupTable: (table) => { dt = table; }
        }, init: async () => {`);
        const api = vm.runInNewContext(exposed + '\nItemsPage.test;', {
            window: fakeWindow, document, fetch, FormData, URLSearchParams, setTimeout, clearTimeout, console
        });
        api.setupTable({ ajax: { reload } });
        return { api, fetch, toast, reload, window: fakeWindow, settle: () => pending };
    }

    it.each([
        [['DETAILS']],
        [['DETAILS', 'EDIT', 'SUBMIT']],
        [['DETAILS', 'EDIT', 'DEACTIVATE']],
        [['DETAILS', 'REACTIVATE', 'RETIRE']]
    ])('reads the server action sequence %j as it is', (actions) => {
        expect(harness().api.readAvailableActions({ availableActions: actions })).toEqual(actions);
    });

    it('draws a row\'s actions only from its availableActions — an unknown or a duplicate action is never drawn', () => {
        const h = harness();
        const html = h.api.renderRowActions(item(['DETAILS', 'APPROVE', 'DELETE', 'DEACTIVATE', 'DEACTIVATE']));
        document.body.insertAdjacentHTML('beforeend', `<div id="row">${html}</div>`);
        const drawn = [...document.querySelectorAll('#row [data-action]')].map((x) => x.dataset.action);
        expect(drawn).toEqual(['DETAILS', 'DEACTIVATE']);
        // DETAILS is the quick-view trigger the list component delegates (.js-quick-view, data-row-action="quickView").
        expect(document.querySelector('#row .js-quick-view').dataset.rowAction).toBe('quickView');
    });

    it('draws no lifecycle action the server did not return', () => {
        const h = harness();
        document.body.insertAdjacentHTML('beforeend', `<div id="row">${h.api.renderRowActions(item(['DETAILS']))}</div>`);
        expect([...document.querySelectorAll('#row [data-action]')].map((x) => x.dataset.action)).toEqual(['DETAILS']);
        expect(h.api.readAvailableActions({})).toEqual([]);
    });

    it.each([
        ['DEACTIVATE', 'deactivate', 3, 4],
        ['RETIRE', 'retire', 4, 5],
        ['REACTIVATE', 'reactivation-requests', 4, 2]
    ])('%s: confirm with a reason, re-read, post version + reason, read back, reload', async (action, suffix, before, after) => {
        const h = harness([
            response(item(['DETAILS', action], before, 4)),
            response(item(['DETAILS'], after, 5)),
            response(item(['DETAILS'], after, 5))
        ]);
        h.api.requestLifecycle(id, action, () => h.reload());
        await h.settle();

        const calls = h.fetch.mock.calls;
        expect(calls.map((c) => c[0])).toEqual([
            `/MasterDataManagement/Items/api/${id}`,
            `/MasterDataManagement/Items/api/${id}/${suffix}`,
            `/MasterDataManagement/Items/api/${id}`]);
        const post = calls[1][1];
        expect(post.method).toBe('POST');
        expect(post.headers).toEqual({ RequestVerificationToken: 'synthetic-csrf', 'X-Requested-With': 'XMLHttpRequest' });
        expect(post.body.get('ExpectedVersion')).toBe('4');
        expect(post.body.get('Reason')).toBe('Supplier stopped');
        expect(post.body.get('__RequestVerificationToken')).toBe('synthetic-csrf');
        // No credential of its own: same-origin cookies only, never a bearer or tenant header from the browser.
        expect(JSON.stringify(calls.map((c) => c[1]?.headers || {}))).not.toMatch(/Authorization|X-Tenant-Id/);
        expect(h.window.showConfirm.mock.calls[0][2]).toMatchObject({ showInput: true, inputRequired: true });
        expect(h.toast).toHaveBeenCalledWith(`${action.charAt(0)}${action.slice(1).toLowerCase()}Success`, 'success');
        expect(h.reload).toHaveBeenCalled();
    });

    it('SUBMIT posts the version without asking a reason, and says the 503 sentence the proxy sends', async () => {
        const h = harness([
            response(item(['DETAILS', 'EDIT', 'SUBMIT'], 1, 2)),
            response({ success: false, errors: ['Activation approval is not available yet.'] }, 503)
        ]);
        h.api.requestLifecycle(id, 'SUBMIT', () => h.reload());
        await h.settle();

        const post = h.fetch.mock.calls[1][1];
        expect(post.body.get('ExpectedVersion')).toBe('2');
        expect(post.body.has('Reason')).toBe(false);
        expect(h.window.showConfirm.mock.calls[0][2].showInput).toBeUndefined();
        expect(h.toast).toHaveBeenCalledWith('Activation approval is not available yet.', 'error');
        expect(h.fetch).toHaveBeenCalledTimes(2);
    });

    // S2 lane merge FIX1: the requester's WITHDRAW_APPROVAL — drawn after SUBMIT, posts the version to /withdraw without
    // asking a reason, reads the item back and says its own sentence.
    it('S2 merge FIX1: WITHDRAW_APPROVAL is drawn in MDM\'s order and posts the version to /withdraw', async () => {
        const drawn = harness();
        document.body.insertAdjacentHTML('beforeend',
            `<div id="row">${drawn.api.renderRowActions(item(['DETAILS', 'WITHDRAW_APPROVAL', 'DEACTIVATE']))}</div>`);
        expect([...document.querySelectorAll('#row [data-action]')].map((x) => x.dataset.action))
            .toEqual(['DETAILS', 'WITHDRAW_APPROVAL', 'DEACTIVATE']);
        expect(document.querySelector('#row [data-action="WITHDRAW_APPROVAL"]').dataset.rowAction).toBe('withdrawApproval');

        const h = harness([
            response(item(['DETAILS', 'WITHDRAW_APPROVAL'], 2, 2)),
            response(item(['DETAILS', 'EDIT', 'SUBMIT'], 1, 3)),
            response(item(['DETAILS', 'EDIT', 'SUBMIT'], 1, 3))
        ]);
        h.api.requestLifecycle(id, 'WITHDRAW_APPROVAL', () => h.reload());
        await h.settle();

        expect(h.fetch.mock.calls[1][0]).toBe(`/MasterDataManagement/Items/api/${id}/withdraw`);
        expect(h.fetch.mock.calls[1][1].body.get('ExpectedVersion')).toBe('2');
        expect(h.fetch.mock.calls[1][1].body.has('Reason')).toBe(false);
        expect(h.window.showConfirm.mock.calls[0][0]).toBe('WithdrawApprovalConfirmation');
        expect(h.toast).toHaveBeenCalledWith('WithdrawApprovalSuccess', 'success');
        expect(h.reload).toHaveBeenCalled();
    });

    // S2 lane merge FIX1 (CT blocker): MDM answers a submit 202 while the approval is still being started — the proxy
    // passes it on and the screen says the submit's success, never an error.
    it('S2 merge FIX1: a SUBMIT answered 202 (the approval being started) is a success', async () => {
        const h = harness([
            response(item(['DETAILS', 'EDIT', 'SUBMIT'], 1, 1)),
            response(item(['DETAILS'], 2, 2), 202),
            response(item(['DETAILS'], 2, 2))
        ]);
        h.api.requestLifecycle(id, 'SUBMIT', () => h.reload());
        await h.settle();

        expect(h.toast).toHaveBeenCalledWith('SubmitSuccess', 'success');
        expect(h.toast).not.toHaveBeenCalledWith(expect.anything(), 'error');
    });

    it('does not post when the server no longer offers the action', async () => {
        const h = harness([response(item(['DETAILS'], 4, 5))]);
        h.api.requestLifecycle(id, 'RETIRE', () => h.reload());
        await h.settle();

        expect(h.fetch).toHaveBeenCalledTimes(1);
        expect(h.toast).toHaveBeenCalledWith('LifecycleStateChanged', 'error');
    });

    it('refuses an empty reason before any request', async () => {
        const h = harness([]);
        h.window.showConfirm = vi.fn((_, callback) => { callback('   '); });
        h.api.requestLifecycle(id, 'DEACTIVATE');
        await Promise.resolve();

        expect(h.fetch).not.toHaveBeenCalled();
        expect(h.toast).toHaveBeenCalledWith('ReasonRequired', 'error');
    });

    it('an unknown action key is not a lifecycle request', () => {
        const h = harness([]);
        h.api.requestLifecycle(id, 'DELETE');
        expect(h.window.showConfirm).not.toHaveBeenCalled();
    });

    it('the list wire: server mode on the same-origin proxy, server export, filters by MDM keys, no credential headers', () => {
        const options = harness().api.buildListOptions();
        expect(options.dataMode).toBe('server');
        expect(options.ajax.url).toBe('/MasterDataManagement/Items/api');
        expect(options.ajax.headers).toEqual({ 'X-Requested-With': 'XMLHttpRequest' });
        expect(options.export).toEqual({ mode: 'server', url: '/MasterDataManagement/Items/api/export', fileName: 'items' });
        expect(options.filters.fields.map((f) => f.key)).toEqual(['materialType', 'status']);
        expect(options.filters.fields.every((f) => !('matches' in f))).toBe(true);
        expect(options.savedView).toMatchObject({ moduleKey: 'MasterDataManagement', pageKey: 'Items' });
    });

    it('the record draws its action buttons only from availableActions (DETAILS is the page itself)', () => {
        const h = harness([], '<div id="itemDetails" data-item-id="' + id + '"><div data-item-field="lifecycleStatus"></div>' +
            '<div data-item-field="name"></div><div id="itemDetailActions"></div><tbody id="itemDetailConversions"></tbody></div>');
        h.api.renderRecord(item(['DETAILS', 'EDIT', 'DEACTIVATE', 'UNKNOWN']));

        const host = document.getElementById('itemDetailActions');
        expect([...host.children].map((x) => x.dataset.detailAction || 'EDIT-LINK')).toEqual(['EDIT-LINK', 'DEACTIVATE']);
        expect(host.querySelector('a').getAttribute('href')).toBe(`/MasterDataManagement/Items/Edit/${id}`);
        expect(document.querySelector('[data-item-field="name"]').textContent).toBe('Synthetic');
        // No inline handler is written anywhere.
        expect(document.body.innerHTML).not.toMatch(/onclick=/i);
    });

    // S5c: the record shows its units and storage conditions by name with the code beside it; a value the vocabulary
    // does not name (a newer catalog value) by its code — never blank. The sabotage target "the label lookup removed".
    it('S5c: the details show units and storage conditions by name, the code beside it, an unnamed one by its code', () => {
        const labels = '<script id="reference-option-labels" type="application/json">' +
            JSON.stringify({ Uom_KGM: 'Kilogram', Uom_XBX: 'Box', Storage_COOL_2_8: 'Store at 2-8 °C' }) + '</script>';
        const h = harness([], labels + '<div id="itemDetails" data-item-id="' + id + '"><div data-item-field="lifecycleStatus"></div>' +
            '<div data-item-field="baseUomCode"></div><div data-item-field="storageConditionCodes"></div>' +
            '<div id="itemDetailActions"></div><table><tbody id="itemDetailConversions"></tbody></table></div>');
        h.api.renderRecord({ ...item(['DETAILS']), storageConditionCodes: ['COOL_2_8', 'NEW_CONDITION'],
            uomConversions: [{ fromUom: 'XBX', toUom: 'IU', numerator: '100', denominator: '1' }] });

        expect(document.querySelector('[data-item-field="baseUomCode"]').textContent).toBe('Kilogram (KGM)');
        expect(document.querySelector('[data-item-field="storageConditionCodes"]').textContent)
            .toBe('Store at 2-8 °C (COOL_2_8), NEW_CONDITION');
        expect([...document.querySelectorAll('#itemDetailConversions td')].map((x) => x.textContent))
            .toEqual(['Box (XBX)', 'IU', '100', '1']);
    });

    it('says the proxy\'s sentences and warns on an unmapped refusal without showing a code', async () => {
        const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
        const h = harness();
        const text = await h.api.getErrorMessage({ status: 422, json: async () => ({ success: false, errors: ['ErrorValidation'], unmapped: true }) });
        expect(text).toBe('ErrorValidation');
        expect(warn).toHaveBeenCalled();
    });
});

/*
 * MOD-0290-FU04 S5b (BL-600) — the item form's lists, executed against the production _Form.cshtml (its Razor words
 * replaced by their keys) and the options read scripted: material type / base unit / conversion units / storage
 * conditions are selects filled from the options and labelled by stable code; at most one temperature (O-2); a list
 * that cannot be loaded leaves them passive with the reason — never a code input.
 */
describe('Items form option lists (S5b)', () => {
    const id = '12345678-1234-1234-1234-123456789012';
    const optionsUrl = '/MasterDataManagement/Items/api/options';
    const formMarkup = (mode) => fs.readFileSync(path.join(__dirname, '../Views/MasterDataManagement/Items/_Form.cshtml'), 'utf8')
        .replace(/@\*[\s\S]*?\*@/g, '')
        .replace(/^@(using|inject|model)\b.*$/gm, '')
        .replace(/^@\{[\s\S]*?^\}/m, '')
        .replace(/@Html\.AntiForgeryToken\(\)/g, '<input type="hidden" name="__RequestVerificationToken" value="synthetic-csrf">')
        .replace(/@\(isEdit \? "edit" : "create"\)/g, mode)
        .replace(/@\(ViewData\["ItemFormAttemptToken"\][^)]*\)/g, 'synthetic-attempt')
        .replace(/@Model\?\.Id/g, mode === 'edit' ? id : '')
        .replace(/@(Shared)?Localizer\["(\w+)"\]/g, '$2')
        .replace(/@title/g, 'title')
        .replace(/<label asp-for="(\w+)"/g, '<label for="$1"')
        .replace(/<input asp-for="(LotControlled|Stockable)"/g, '<input type="checkbox" id="$1" name="$1"')
        .replace(/asp-for="(\w+)"/g, 'id="$1" name="$1"');
    const labels = '<script id="reference-option-labels" type="application/json">' + JSON.stringify({
        uom_KGM: 'Kilogram', Uom_C62: 'Piece', Storage_COOL_2_8: 'Refrigerated', Storage_AMBIENT_15_25: 'Room temperature',
        Storage_PROTECT_FROM_LIGHT: 'Protect from light', TemperatureOnlyOne: 'Only one temperature condition',
        OptionsUnavailable: 'Lists unavailable', optionsUnavailableCreate: 'The item cannot be created until the list arrives.'
    }) + '</script>';
    const option = (code, attributes = {}) => ({ code, sortOrder: 0, attributes });
    const options = {
        catalogVersions: { materialTypes: 'MT-V1', uoms: 'ITEM-UNIVERSAL-V1', storageConditions: 'SC-V1' },
        materialTypes: [option('ACTIVE_INGREDIENT'), option('EXCIPIENT')],
        uoms: [option('C62'), option('KGM'), option('IU')],
        storageConditions: [option('AMBIENT_15_25', { ConditionKind: 'TEMPERATURE' }), option('COOL_2_8', { ConditionKind: 'TEMPERATURE' }),
            option('PROTECT_FROM_LIGHT', { ConditionKind: 'PROTECTION' })]
    };
    const ok = (data, status = 200) => ({ status, ok: true, json: async () => ({ success: true, data, isSuccessful: true, statusCode: status }) });
    const refused = (status, sentence) => ({ status, ok: false, json: async () => ({ success: false, errors: [sentence] }) });

    function harness(mode, answers) {
        document.body.innerHTML = formMarkup(mode) + labels;
        const fetch = vi.fn(async (url, init) => {
            const answer = answers[`${init?.method || 'GET'} ${url}`];
            if (!answer) throw new Error(`Unexpected HTTP call ${init?.method || 'GET'} ${url}`);
            return typeof answer === 'function' ? answer(init) : answer;
        });
        const fakeWindow = {
            L10n: new Proxy({}, { get: (_, key) => String(key) }), showToast: vi.fn(), location: { href: '' }
        };
        const source = fs.readFileSync(path.join(__dirname, '../wwwroot/assets/js/MasterDataManagement/Items/index.js'), 'utf8');
        // Test-only access to existing closure functions; shipped JS has no test hook.
        const exposed = source.replace('init: async () => {', 'test: { initForm, submitForm }, init: async () => {');
        const api = vm.runInNewContext(exposed + '\nItemsPage.test;', {
            window: fakeWindow, document, fetch, FormData, URLSearchParams, setTimeout, clearTimeout, console, Event
        });
        return { api, fetch, window: fakeWindow };
    }
    const $ = (selector) => document.querySelector(selector);
    const texts = (select) => [...select.options].map((x) => x.textContent);
    const values = (select) => [...select.options].map((x) => x.value);
    const choose = (select, codes) => {
        [...select.options].forEach((x) => { x.selected = codes.includes(x.value); });
        select.dispatchEvent(new Event('change'));
    };

    it('fills every list field as a select from the options, labelled by stable code (an unknown code as itself)', async () => {
        const h = harness('create', { [`GET ${optionsUrl}`]: ok(options) });
        await h.api.initForm();

        expect(h.fetch.mock.calls[0][0]).toBe(optionsUrl);
        expect(JSON.stringify(h.fetch.mock.calls[0][1].headers)).not.toMatch(/Authorization|X-Tenant-Id/);
        const material = $('[name="MaterialType"]');
        const base = $('[name="BaseUomCode"]');
        const storage = $('[name="StorageConditionCodes"]');
        expect([material.tagName, base.tagName, storage.tagName]).toEqual(['SELECT', 'SELECT', 'SELECT']);
        expect(storage.multiple).toBe(true);
        expect(values(material)).toEqual(['', 'ACTIVE_INGREDIENT', 'EXCIPIENT']);
        expect(texts(material).slice(1)).toEqual(['MaterialType_ACTIVE_INGREDIENT', 'MaterialType_EXCIPIENT']);
        expect(texts(base)).toEqual(['BaseUomPlaceholder', 'Piece', 'Kilogram', 'IU']);
        expect(texts(storage)).toEqual(['Room temperature', 'Refrigerated', 'Protect from light']);
        expect([material, base, storage].every((x) => !x.disabled)).toBe(true);
        expect([...document.querySelectorAll('[data-item-options-reason]')].every((x) => x.classList.contains('d-none'))).toBe(true);

        $('#btnAddConversion').click();
        const row = $('#itemConversionRows .js-conversion-row');
        expect(row.querySelector('.js-conversion-from').tagName).toBe('SELECT');
        expect(values(row.querySelector('.js-conversion-to'))).toEqual(['', 'C62', 'KGM', 'IU']);
    });

    it('O-2: a second temperature condition is refused in words and taken back; the first one stays', async () => {
        const h = harness('create', { [`GET ${optionsUrl}`]: ok(options) });
        await h.api.initForm();
        const storage = $('[name="StorageConditionCodes"]');

        choose(storage, ['COOL_2_8', 'PROTECT_FROM_LIGHT']);
        expect($('#itemStorageTemperatureNote').classList.contains('d-none')).toBe(true);
        expect(storage.querySelector('option[value="AMBIENT_15_25"]').disabled).toBe(true);

        choose(storage, ['COOL_2_8', 'PROTECT_FROM_LIGHT', 'AMBIENT_15_25']);
        expect([...storage.selectedOptions].map((x) => x.value)).toEqual(['COOL_2_8', 'PROTECT_FROM_LIGHT']);
        expect($('#itemStorageTemperatureNote').classList.contains('d-none')).toBe(false);
        expect($('#itemStorageTemperatureNote').textContent).toBe('Only one temperature condition');
    });

    // Sabotage target "the form falls back to a code input when the list is missing".
    it('without the lists the list fields stay passive selects with the reason — never a code input; the rest is usable', async () => {
        const h = harness('create', { [`GET ${optionsUrl}`]: refused(503, 'The lists cannot be loaded right now.') });
        await h.api.initForm();

        for (const name of ['MaterialType', 'BaseUomCode', 'StorageConditionCodes']) {
            const element = $(`[name="${name}"]`);
            expect(element.tagName).toBe('SELECT');
            expect(element.disabled).toBe(true);
            expect(document.querySelector(`input[name="${name}"]`)).toBeNull();
        }
        expect($('#btnAddConversion').disabled).toBe(true);
        expect(document.querySelector('input.js-conversion-from, input.js-conversion-to, template input.js-conversion-from')).toBeNull();
        const reasons = [...document.querySelectorAll('[data-item-options-reason]')];
        expect(reasons.length).toBeGreaterThanOrEqual(3);
        const createReason = 'The lists cannot be loaded right now. The item cannot be created until the list arrives.';
        expect(reasons.every((x) => !x.classList.contains('d-none') && x.textContent === createReason)).toBe(true);
        expect($('[name="Name"]').disabled).toBe(false);
        expect($('[name="ShelfLifeDays"]').disabled).toBe(false);

        // A create cannot be sent without its lists: the reason is said, nothing is posted.
        $('[name="Name"]').value = 'Synthetic';
        await h.api.submitForm(new Event('submit'));
        expect($('#itemFormAlert').textContent).toBe(createReason);
        expect(h.fetch).toHaveBeenCalledTimes(1);
    });

    // S5b-FIX2: the Create form says no item can be made without the lists (OptionsUnavailableCreate) — never the
    // Edit form's "the other fields can still be saved". An empty list (no proxy sentence) says the Create sentence alone;
    // the Edit form keeps its own sentence. Sabotage target "the Create reason removed".
    it('S5b-FIX2: a create without the lists says the item cannot be created until the list arrives; an edit does not', async () => {
        const empty = { ...options, uoms: [] };
        const create = harness('create', { [`GET ${optionsUrl}`]: ok(empty) });
        await create.api.initForm();
        const createReasons = [...document.querySelectorAll('[data-item-options-reason]')].map((x) => x.textContent);
        expect(createReasons.length).toBeGreaterThanOrEqual(3);
        expect(createReasons.every((x) => x === 'The item cannot be created until the list arrives.')).toBe(true);

        const item = { id, name: 'Old', materialType: 'EXCIPIENT', baseUomCode: 'KGM', storageConditionCodes: [], uomConversions: [],
            lotControlled: false, stockable: false, lifecycleStatus: 1, version: 2, availableActions: ['DETAILS', 'EDIT'] };
        const edit = harness('edit', {
            [`GET ${optionsUrl}`]: ok(empty), [`GET /MasterDataManagement/Items/api/${id}`]: ok(item)
        });
        await edit.api.initForm();
        const editReasons = [...document.querySelectorAll('[data-item-options-reason]')].map((x) => x.textContent);
        expect(editReasons.length).toBeGreaterThanOrEqual(3);
        expect(editReasons.every((x) => x === 'Lists unavailable')).toBe(true);
    });

    it('an edit without the lists keeps the stored codes in the passive selects and still saves a rename', async () => {
        const item = {
            id, name: 'Old', materialType: 'EXCIPIENT', baseUomCode: 'KGM', storageConditionCodes: ['COOL_2_8'],
            uomConversions: [{ fromUom: 'XBX', toUom: 'KGM', numerator: '10', denominator: '1' }],
            lotControlled: true, stockable: true, lifecycleStatus: 1, version: 6, availableActions: ['DETAILS', 'EDIT']
        };
        let sent;
        const h = harness('edit', {
            [`GET ${optionsUrl}`]: refused(503, 'The lists cannot be loaded right now.'),
            [`GET /MasterDataManagement/Items/api/${id}`]: ok(item),
            [`PUT /MasterDataManagement/Items/api/${id}`]: (init) => { sent = JSON.parse(init.body); return ok(item); }
        });
        await h.api.initForm();

        expect($('[name="BaseUomCode"]').value).toBe('KGM');
        expect($('[name="BaseUomCode"]').disabled).toBe(true);
        expect($('.js-conversion-from').value).toBe('XBX');
        expect($('.js-conversion-from').disabled).toBe(true);
        $('[name="Name"]').value = 'New name';
        await h.api.submitForm(new Event('submit'));

        expect(sent).toMatchObject({
            name: 'New name', materialType: 'EXCIPIENT', baseUomCode: 'KGM', storageConditionCodes: ['COOL_2_8'], expectedVersion: 6,
            uomConversions: [{ fromUom: 'XBX', toUom: 'KGM', numerator: '10', denominator: '1' }]
        });
    });

    // S2 lane merge (R-04): a legacy draft's lot flag is null — drawn indeterminate and sent as null (MDM names it),
    // never a silent false; once the reader sets it, the set value is sent.
    it('S2 merge: a never-filled lot flag is indeterminate and sent as null until the reader sets it', async () => {
        const item = { id, name: 'Old', materialType: 'EXCIPIENT', baseUomCode: 'KGM', storageConditionCodes: ['COOL_2_8'],
            uomConversions: [], lotControlled: null, stockable: false, lifecycleStatus: 1, version: 3, availableActions: ['DETAILS', 'EDIT'] };
        const sent = [];
        const h = harness('edit', {
            [`GET ${optionsUrl}`]: ok(options), [`GET /MasterDataManagement/Items/api/${id}`]: ok(item),
            [`PUT /MasterDataManagement/Items/api/${id}`]: (init) => { sent.push(JSON.parse(init.body)); return ok(item); }
        });
        await h.api.initForm();

        expect($('[name="LotControlled"]').indeterminate).toBe(true);
        expect($('[name="LotControlled"]').checked).toBe(false);
        await h.api.submitForm(new Event('submit'));
        expect(sent[0].lotControlled).toBeNull();

        $('[name="LotControlled"]').click();
        await h.api.submitForm(new Event('submit'));
        expect(sent[1].lotControlled).toBe(true);

        // FIX1 (CT minor): "no" is a second click from the indeterminate state, and it is sent as false.
        $('[name="LotControlled"]').click();
        expect($('[name="LotControlled"]').indeterminate).toBe(false);
        await h.api.submitForm(new Event('submit'));
        expect(sent[2].lotControlled).toBe(false);
    });

    it('sends the chosen codes: the material type, the base unit, the storage conditions and the conversion units', async () => {
        let sent;
        const h = harness('create', {
            [`GET ${optionsUrl}`]: ok(options),
            'POST /MasterDataManagement/Items/api': (init) => { sent = JSON.parse(init.body); return ok({ id }, 201); }
        });
        await h.api.initForm();
        $('[name="Name"]').value = 'Synthetic';
        $('[name="MaterialType"]').value = 'EXCIPIENT';
        $('[name="BaseUomCode"]').value = 'KGM';
        choose($('[name="StorageConditionCodes"]'), ['COOL_2_8', 'PROTECT_FROM_LIGHT']);
        $('#btnAddConversion').click();
        const row = $('#itemConversionRows .js-conversion-row');
        row.querySelector('.js-conversion-from').value = 'C62';
        row.querySelector('.js-conversion-to').value = 'KGM';
        row.querySelector('.js-conversion-numerator').value = '0.5';
        row.querySelector('.js-conversion-denominator').value = '1';
        await h.api.submitForm(new Event('submit'));

        expect(sent).toMatchObject({
            materialType: 'EXCIPIENT', baseUomCode: 'KGM', storageConditionCodes: ['COOL_2_8', 'PROTECT_FROM_LIGHT'],
            uomConversions: [{ fromUom: 'C62', toUom: 'KGM', numerator: '0.5', denominator: '1' }], formAttemptToken: 'synthetic-attempt'
        });
    });
});
