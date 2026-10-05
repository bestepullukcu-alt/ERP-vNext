const fs = require('fs');
const path = require('path');

/*
 * WP-MDM-ABB-LIST-01 — the executed contract, now against the list component: the page hands its options to
 * DitenDataTable.createList (server mode) and reloads through the list handle. Every rule of the old contract is kept;
 * only the instruments moved (the 'details' action is the factory's 'quickView', a reload is list.reload).
 */
describe('ABB executed browser contract', () => {
    const vm = require('vm');
    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot/assets/js/MDM/ProductAbbreviationRegister/index.js'), 'utf8');
    /*
     * `dom`: the page's real markup (jsdom) for the behaviour tests of LIST-FIX1 item 8 — the request form, the quick
     * view and its evidence are real elements; otherwise the light stand-ins below.
     */
    function harness(dataset = {}, { dom = null, values = {} } = {}) {
        const selects = [];
        const list = { reload: vi.fn(), appliedFilters: {} };
        const createList = vi.fn(async () => list);
        const addNew = { classList: { toggle: vi.fn() } };
        const fields = {
            filterGlobalProductId: { value: '' },
            filterGlobalProductText: { value: '' },
            btnFilterApply: { click: vi.fn() }
        };
        let document;
        if (dom) {
            globalThis.document.body.innerHTML = dom;
            const root = globalThis.document.getElementById('product-abbreviation-register');
            Object.entries(dataset).forEach(([key, value]) => { root.dataset[key] = value; });
            document = globalThis.document;
        } else {
            const root = { dataset, querySelector: () => ({ value: 'synthetic-csrf-fixture' }) };
            document = {
                getElementById: (id) => id === 'product-abbreviation-register' ? root
                    : id === 'dt-product-abbreviation-register' ? {} : fields[id] ?? null,
                querySelector: (selector) => selector === '.add-new' ? addNew : null, addEventListener: vi.fn(), body: {}
            };
        }
        const win = {
            location: { search: '', assign: vi.fn() }, L10n: { AddNew: 'Request', ErrorForbidden: 'denied', ErrorConflict: 'conflict',
                ErrorGateway: 'unavailable', ErrorReconciliation: 'pending', ErrorValidation: 'invalid', ErrorNotFound: 'missing',
                ErrorProductHasEntry: 'has entry', RequestSuccess: 'requested', LifecycleActive: 'Active', LifecycleRequested: 'Requested',
                Yes: 'Yes', No: 'No', Unknown: 'Unknown', ErrorAlreadyAllocated: 'taken' },
            DitenDataTable: { renderActions: (actions) => actions, createList },
            DtDefaults: { handleUnauthorized: vi.fn() },
            showToast: vi.fn(), showConfirm: vi.fn()
        };
        const fetch = vi.fn();
        const offcanvas = { show: vi.fn(), hide: vi.fn() };
        const context = { window: win, document, fetch, URLSearchParams, FormData: globalThis.FormData, Option: function(){}, console, setTimeout,
            bootstrap: { Offcanvas: { getOrCreateInstance: () => offcanvas } },
            $: (selector) => ({ length: 1, hasClass: () => false, prop: vi.fn(), select2: x => selects.push(x), on: vi.fn(),
                val: (value) => value === undefined ? (values[selector] ?? '') : ({ trigger: vi.fn() }), data: () => '',
                find: () => ({ text: () => '' }), text: () => values[selector] ?? '' })
        };
        const anchor = '        buildListOptions\n    };';
        expect(source.split(anchor)).toHaveLength(2);
        const instrumented = source.replace(anchor,
            '        buildListOptions, canRequest, initSelector, renderActions, requestRetirement, requestCorrection, cancelRequest, showDetails, initDataTable\n    };')
            .replace("document.addEventListener('DOMContentLoaded', () => ProductAbbreviationRegisterList.init());", '');
        vm.createContext(context);
        vm.runInContext(instrumented + '\nglobalThis.testSubject = ProductAbbreviationRegisterList;', context);
        return { sut: context.testSubject, win, fetch, list, createList, fields, selects, addNew, offcanvas, document };
    }
    const keys = (h, row) => h.sut.renderActions(row).map(x => x.key);

    it.each([
        [{ canRequest:'true' }, false],
        [{ canRequest:'true', canSelectProduct:'true' }, true],
        [{ canAudit:'true' }, false]
    ])('uses current permissions for requester/requester+GP-read/auditor access %j', (permissions, allowed) => {
        const h = harness(permissions);
        expect(h.sut.canRequest()).toBe(allowed);
        h.sut.initSelector('#filterGlobalProduct');
        expect(h.selects.length).toBe(allowed ? 1 : 0);
        expect(h.sut.buildListOptions().toolbar.addNewText).toBe(allowed ? 'Request' : '');
        expect(h.fetch).not.toHaveBeenCalled();
    });

    it.each(['REQUESTED','REJECTED','CANCELLED','RETIRED'])('offers no active-state mutation in %s', status => {
        const h = harness({ canCorrect:'true',canRetire:'true' });
        expect(keys(h, { id:'x', lifecycleStatus:status })).toEqual(['quickView']);
    });
    it('shows only exact granted actions and excludes a pending retirement', () => {
        const h = harness({ canCorrect:'true' });
        expect(keys(h, { id:'x', lifecycleStatus:'ACTIVE' })).toEqual(['quickView','correct']);
        expect(keys(h, { id:'x', lifecycleStatus:'ACTIVE', retirementPending:true })).toEqual(['quickView']);
    });
    // FIX1 B2/B3 — what each reader is offered on the register page.
    it('offers cancel to the requester of a pending entry and the Task Center to anyone who decides', () => {
        const requester = harness({ canCancel:'true', canDecide:'true' });
        expect(keys(requester, { id:'x', lifecycleStatus:'REQUESTED', requestedByCurrentUser:true })).toEqual(['quickView','cancel']);
        expect(keys(requester, { id:'x', lifecycleStatus:'REQUESTED', requestedByCurrentUser:false })).toEqual(['quickView','taskCenter']);
        expect(keys(requester, { id:'x', lifecycleStatus:'ACTIVE', retirementPending:true })).toEqual(['quickView','taskCenter']);
        const reader = harness({});
        expect(keys(reader, { id:'x', lifecycleStatus:'REQUESTED', requestedByCurrentUser:true })).toEqual(['quickView']);
    });
    it('offers no correction or retirement while a correction of the code waits', () => {
        const h = harness({ canCorrect:'true', canRetire:'true' });
        expect(keys(h, { id:'x', lifecycleStatus:'ACTIVE', correctionPending:true })).toEqual(['quickView']);
        expect(keys(h, { id:'x', lifecycleStatus:'ACTIVE' })).toEqual(['quickView','correct','retire']);
    });
    it('wires every offered row action to its flow and the details to the quick view', () => {
        const options = harness({}).sut.buildListOptions();
        expect(Object.keys(options.actions.onRowAction).sort()).toEqual(['cancel','correct','retire','taskCenter']);
        expect(options.quickView.actionKey).toBe('quickView');
    });
    it.each([202,403,404,409])('never reloads a retirement or reports success for failure %s', async status => {
        const h = harness({canRetire:'true'});
        await h.sut.initDataTable();
        h.fetch.mockResolvedValue({ status, ok:status===202, json:async()=>({isSuccessful:false,errors:[]}) });
        h.sut.requestRetirement({id:'entry',version:4,lifecycleStatus:'ACTIVE'});
        await h.win.showConfirm.mock.calls[0][1]('fixture reason');
        expect(h.list.reload).not.toHaveBeenCalled();
        expect(h.win.showToast).toHaveBeenCalledWith(expect.any(String),'error');
        expect(h.win.showToast).not.toHaveBeenCalledWith(expect.anything(),'success');
    });
    it('correction posts expected version with replacement and reason then reloads authoritative state', async () => {
        const h=harness({canCorrect:'true'});
        await h.sut.initDataTable();
        h.fetch.mockResolvedValue({status:200,ok:true,json:async()=>({isSuccessful:true,data:{}})});
        h.sut.requestCorrection({id:'entry',version:7,lifecycleStatus:'ACTIVE'});
        h.win.showConfirm.mock.calls[0][1]('ABC');
        await h.win.showConfirm.mock.calls[1][1]('reason');
        expect(h.fetch.mock.calls[0][0]).toBe('/MDM/ProductAbbreviationRegister/api/entry/corrections');
        expect(JSON.parse(h.fetch.mock.calls[0][1].body)).toEqual({expectedVersion:7,replacementAbbreviation:'ABC',reason:'reason'});
        expect(h.fetch.mock.calls[0][1].headers.RequestVerificationToken).toBe('synthetic-csrf-fixture');
        expect(h.list.reload).toHaveBeenCalledWith('RequestSuccess');
    });
    it('auditor deeplink loads exact product without the selector or request button', async () => {
        const h=harness({canAudit:'true'});
        const id='11111111-1111-1111-1111-111111111111';
        h.win.location.search='?globalProductId='+id;
        await h.sut.initDataTable();
        const options=h.createList.mock.calls[0][0];
        // FIX1 B2 + LIST: every entry of the product, read through the server-mode list proxy.
        expect(options.dataMode).toBe('server');
        expect(options.ajax.url).toBe('/MDM/ProductAbbreviationRegister/api/list');
        // LIST-FIX1 item 1: nothing is applied before the factory's own start (loadOptions), and then only after it.
        expect(h.fields.btnFilterApply.click).not.toHaveBeenCalled();
        await options.filters.loadOptions();
        expect(h.fields.btnFilterApply.click).not.toHaveBeenCalled();
        await new Promise(resolve => setTimeout(resolve, 0));
        expect(h.fields.filterGlobalProductId.value).toBe(id);
        expect(h.fields.btnFilterApply.click).toHaveBeenCalledTimes(1);
        expect(h.selects).toHaveLength(0);
        expect(options.toolbar.addNewText).toBe('');
        expect(h.fetch).not.toHaveBeenCalled();
    });
    it('ignores a deeplink that is not a product id', async () => {
        const h=harness({canAudit:'true'});
        h.win.location.search='?globalProductId=not-a-guid';
        await h.sut.initDataTable();
        await h.createList.mock.calls[0][0].filters.loadOptions();
        await new Promise(resolve => setTimeout(resolve, 0));
        expect(h.fields.filterGlobalProductId.value).toBe('');
        expect(h.fields.btnFilterApply.click).not.toHaveBeenCalled();
    });
    /*
     * One list answer as jQuery delivers it (LIST-FIX2 item 4): beforeSend for the request (its draw), then — for a
     * response — dataFilter (onResponse) and complete, back to back.
     */
    const send = (options, draw) => {
        const settings = { url: `/MDM/ProductAbbreviationRegister/api/list?start=0&length=10&draw=${draw}&globalProductId=p` };
        options.ajax.beforeSend({}, settings);
        return settings;
    };
    const answer = (options, settings, data, status = 'success') => {
        if (status === 'success') options.onResponse({ isSuccessful: true, data });
        options.ajax.complete.call(settings, {}, status);
    };
    it('hides the request button by the proxy\'s answer for EVERY entry, not by the rows on this page', () => {
        // LIST-FIX1 item 4 — page two shows a rejected code, yet the product's active one (page one) still counts.
        const h=harness({canRequest:'true',canSelectProduct:'true'});
        const options=h.sut.buildListOptions();
        answer(options, send(options, 1), {items:[{id:'x',lifecycleStatus:'REJECTED'}],hasOpenEntry:true});
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', true);
        answer(options, send(options, 2), {items:[{id:'x',lifecycleStatus:'ACTIVE'}],hasOpenEntry:false});
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', false);
        options.config.drawCallback();
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', false);
    });
    it('lets only the NEWEST answer write the page state; a stale one arriving late writes nothing', () => {
        // LIST-FIX2 item 4 — draw 1 (the saved view) answers AFTER draw 2 (the link): its "open entry" must not hide the button.
        const h=harness({canRequest:'true',canSelectProduct:'true'});
        const options=h.sut.buildListOptions();
        const older = send(options, 1);
        const newer = send(options, 2);
        answer(options, newer, {items:[],hasOpenEntry:false,truncated:false});
        answer(options, older, {items:[],hasOpenEntry:true,truncated:true});
        options.config.drawCallback();
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', false);
    });
    it('hides the request button while the newest read failed (the state is unknown) and while the list is cut off', () => {
        // LIST-FIX2 items 4 and 5.
        const h=harness({canRequest:'true',canSelectProduct:'true'});
        const options=h.sut.buildListOptions();
        answer(options, send(options, 1), {items:[],hasOpenEntry:false,truncated:false});
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', false);
        answer(options, send(options, 2), null, 'error');
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', true);
        answer(options, send(options, 3), {items:[],hasOpenEntry:false,truncated:true});
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', true);
        answer(options, send(options, 4), {items:[],hasOpenEntry:false,truncated:false});
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', false);
    });
    it('preserves saved filter/search/column/order state under the versioned view key', () => {
        // LIST-FIX1 item 3 — the old key's views are carried over by the page (abb-register-real-factory.test.js).
        const options=harness().sut.buildListOptions();
        expect(options.savedView).toMatchObject({ moduleKey:'MasterDataManagement', pageKey:'ProductAbbreviationRegister.v2',
            saveViewColumnIndexes:[1,2,3,4,5], baseOrder:[[2,'asc']] });
        expect(options.filters.fields.map(f=>f.key)).toEqual(['globalProductText','globalProductId']);
        expect(options.filters.resetBtn).toBe('btnFilterReset');
    });

    // ── LIST-FIX1 items 2, 5, 6 ──────────────────────────────────────────────────────────────────────────────
    it.each([
        [403, { errorCode: 'ABBREVIATION_PERMISSION_DENIED' }, 'denied'],
        [400, { errors: ['ABBREVIATION_LIST_ORDER_INVALID'] }, 'invalid'],
        [400, { errors: ['ABBREVIATION_LIST_PRODUCT_INVALID'] }, 'invalid'],
        [404, {}, 'missing'],
        [502, { errors: ['Gateway error'] }, 'unavailable'],
        [503, { errors: ['ABBREVIATION_PERSISTENCE_UNAVAILABLE'] }, 'unavailable']
    ])('a list read refused with %s says the register\'s sentence', (status, payload, sentence) => {
        const h = harness();
        h.sut.buildListOptions().ajax.error.call({}, { status, responseJSON: payload });
        expect(h.win.showToast).toHaveBeenCalledWith(sentence, 'error');
    });
    it('hands a 401 list read to the token refresh and stays quiet on an aborted one', () => {
        const h = harness();
        const error = h.sut.buildListOptions().ajax.error;
        error.call({}, { status: 401 });
        error.call({}, { status: 0, statusText: 'abort' });
        expect(h.win.DtDefaults.handleUnauthorized).toHaveBeenCalledTimes(1);
        expect(h.win.showToast).not.toHaveBeenCalled();
    });
    it('declares the server export of every entry, not the page the table holds', () => {
        const options = harness().sut.buildListOptions();
        expect(options.export).toEqual({ mode: 'server', url: '/MDM/ProductAbbreviationRegister/api/list/export', fileName: 'product-abbreviation-register' });
        // LIST-FIX2 item 2: the product column (1) is shown but not exported.
        expect(options.toolbar.exportColumns).toEqual([2, 3, 4, 5]);
    });
    it('says when MDM\'s read of the product came back full, and stops saying it once the state is unknown', () => {
        const h = harness({}, { dom: '<div id="product-abbreviation-register"></div><table id="dt-product-abbreviation-register"></table><div id="abbListTruncated" class="d-none"></div>' });
        const options = h.sut.buildListOptions();
        const notice = h.document.getElementById('abbListTruncated');
        answer(options, send(options, 1), { items: [], truncated: true });
        expect(notice.classList.contains('d-none')).toBe(false);
        answer(options, send(options, 2), null, 'error');
        expect(notice.classList.contains('d-none')).toBe(true);
        answer(options, send(options, 3), { items: [], truncated: true });
        expect(notice.classList.contains('d-none')).toBe(false);
        answer(options, send(options, 4), { items: [], truncated: false });
        expect(notice.classList.contains('d-none')).toBe(true);
    });
    // ── LIST-FIX1 item 8: the page's flows, run ────────────────────────────────────────────────────────────────
    const PRODUCT = '33333333-3333-4333-8333-333333333333';
    const requestDom = '<div id="product-abbreviation-register"><input name="__RequestVerificationToken" value="synthetic-csrf-fixture" />' +
        '<form id="formProductAbbreviationRequest"><input id="requestAbbreviation" name="Abbreviation" value=" abc " /></form>' +
        '<button id="btnRequestAbbreviation"></button><div id="offcanvasCreateEdit"></div></div>' +
        '<table id="dt-product-abbreviation-register"></table><input id="filterGlobalProductId" /><input id="filterGlobalProductText" />' +
        '<button id="btnFilterApply"></button>';
    function requestHarness() {
        const h = harness({ canRequest: 'true', canSelectProduct: 'true' }, {
            dom: requestDom,
            values: { '#requestGlobalProduct': PRODUCT, '#requestGlobalProduct option:selected': 'P-001 — Product' }
        });
        return h;
    }
    it('request: posts the chosen product and code with the CSRF token, then lists that product', async () => {
        const h = requestHarness();
        await h.sut.init();
        const applied = vi.fn();
        h.document.getElementById('btnFilterApply').addEventListener('click', applied);
        h.fetch.mockResolvedValue({ status: 200, ok: true, json: async () => ({ isSuccessful: true, data: {} }) });

        h.document.getElementById('btnRequestAbbreviation').click();
        await h.win.showConfirm.mock.calls[0][1]();

        const [url, init] = h.fetch.mock.calls[0];
        expect(url).toBe('/MDM/ProductAbbreviationRegister/api/requests');
        expect(init.method).toBe('POST');
        expect(init.headers.RequestVerificationToken).toBe('synthetic-csrf-fixture');
        expect(init.body.get('GlobalProductId')).toBe(PRODUCT);
        expect(init.body.get('Abbreviation')).toBe('abc');
        expect(h.offcanvas.hide).toHaveBeenCalled();
        expect(h.win.showToast).toHaveBeenCalledWith('requested', 'success');
        expect(h.document.getElementById('filterGlobalProductId').value).toBe(PRODUCT);
        expect(h.document.getElementById('filterGlobalProductText').value).toBe('P-001 — Product');
        expect(applied).toHaveBeenCalledTimes(1);
    });
    it('request: a refusal keeps the form open, says why and lists nothing new', async () => {
        const h = requestHarness();
        await h.sut.init();
        h.fetch.mockResolvedValue({ status: 409, ok: false, json: async () => ({ errors: ['ABBREVIATION_ALREADY_ALLOCATED'] }) });

        h.document.getElementById('btnRequestAbbreviation').click();
        await h.win.showConfirm.mock.calls[0][1]();

        expect(h.win.showToast).toHaveBeenCalledWith('taken', 'error');
        expect(h.offcanvas.hide).not.toHaveBeenCalled();
        expect(h.document.getElementById('filterGlobalProductId').value).toBe('');
    });
    it('request: a product that already has an open code on the list is refused before anything is sent', async () => {
        const h = requestHarness();
        await h.sut.init();
        h.list.appliedFilters = { globalProductId: PRODUCT };
        const options = h.sut.buildListOptions();
        answer(options, send(options, 1), { items: [], hasOpenEntry: true });

        h.document.getElementById('btnRequestAbbreviation').click();

        expect(h.win.showToast).toHaveBeenCalledWith('has entry', 'error');
        expect(h.win.showConfirm).not.toHaveBeenCalled();
        expect(h.fetch).not.toHaveBeenCalled();
    });
    it('cancel: the requester withdraws a pending entry at its version, then the list reloads', async () => {
        const h = harness({ canCancel: 'true' });
        await h.sut.initDataTable();
        h.fetch.mockResolvedValue({ status: 200, ok: true, json: async () => ({ isSuccessful: true, data: {} }) });

        h.sut.cancelRequest({ id: 'entry', version: 3, abbreviation: 'ABC', lifecycleStatus: 'REQUESTED', requestedByCurrentUser: true });
        await h.win.showConfirm.mock.calls[0][1]();

        expect(h.fetch.mock.calls[0][0]).toBe('/MDM/ProductAbbreviationRegister/api/entry/cancel');
        expect(h.fetch.mock.calls[0][1].method).toBe('PATCH');
        expect(JSON.parse(h.fetch.mock.calls[0][1].body)).toEqual({ expectedVersion: 3 });
        expect(h.list.reload).toHaveBeenCalledWith('RequestSuccess');
    });
    it.each([
        ['someone else\'s request', { canCancel: 'true' }, { requestedByCurrentUser: false, lifecycleStatus: 'REQUESTED' }],
        ['a decided entry', { canCancel: 'true' }, { requestedByCurrentUser: true, lifecycleStatus: 'ACTIVE' }],
        ['a reader without the right', {}, { requestedByCurrentUser: true, lifecycleStatus: 'REQUESTED' }]
    ])('cancel: is not offered for %s', (_name, permissions, row) => {
        const h = harness(permissions);
        h.sut.cancelRequest({ id: 'entry', version: 1, ...row });
        expect(h.win.showConfirm).not.toHaveBeenCalled();
    });

    const detailsDom = '<div id="product-abbreviation-register"></div><table id="dt-product-abbreviation-register"></table>' +
        '<span id="oc-subtitle"></span><span id="oc-global-product"></span><span id="oc-abbreviation"></span><span id="oc-version"></span>' +
        '<span id="oc-retirement-pending"></span><span id="oc-status"></span>' +
        '<div id="auditEvidenceSection" class="d-none"><div id="auditEvidenceLoading" class="d-none"></div>' +
        '<div id="auditEvidenceEmpty" class="d-none"></div><div id="auditEvidenceList"></div></div>';
    it('quick view: fills the row and, for an auditor, its evidence — as text, never as markup', async () => {
        const h = harness({ canAudit: 'true' }, { dom: detailsDom });
        await h.sut.initDataTable();
        h.list.appliedFilters = { globalProductId: PRODUCT, globalProductText: 'P-001 — Product' };
        h.fetch.mockResolvedValue({ status: 200, ok: true, json: async () => ({ data: { history: [{
            eventType: '<img src=x onerror=alert(1)>', occurredAtUtc: '2026-10-01T10:00:00Z', beforeStatus: 'REQUESTED', afterStatus: 'ACTIVE',
            canonicalHumanSubjectId: 'subject-1', correlationId: 'corr-1', idempotencyKey: 'key-1', evidenceHash: 'hash-1', reason: 'approved'
        }] } }) });

        h.sut.showDetails({ id: 'entry/1', abbreviation: 'ABC', version: 4, retirementPending: true, lifecycleStatus: 'ACTIVE' });
        await new Promise((resolve) => setTimeout(resolve, 0));

        const text = (id) => h.document.getElementById(id).textContent;
        expect([text('oc-subtitle'), text('oc-global-product'), text('oc-abbreviation'), text('oc-version'), text('oc-retirement-pending')])
            .toEqual(['ABC', 'P-001 — Product', 'ABC', '4', 'Yes']);
        expect(h.document.getElementById('oc-status').textContent).toBe('Active');
        expect(h.fetch.mock.calls[0][0]).toBe('/MDM/ProductAbbreviationRegister/api/entry%2F1/evidence');
        const list = h.document.getElementById('auditEvidenceList');
        expect(list.querySelector('img')).toBeNull();
        expect(list.textContent).toContain('<img src=x onerror=alert(1)>');
        expect(list.textContent).toContain('Requested');
        expect(list.textContent).toContain('hash-1');
        expect(h.document.getElementById('auditEvidenceSection').classList.contains('d-none')).toBe(false);
        expect(h.document.getElementById('auditEvidenceEmpty').classList.contains('d-none')).toBe(true);
    });
    it('quick view: a reader without the audit right reads no evidence', async () => {
        const h = harness({}, { dom: detailsDom });
        await h.sut.initDataTable();

        h.sut.showDetails({ id: 'entry', abbreviation: 'ABC', version: 1, retirementPending: false, lifecycleStatus: 'ACTIVE' });

        expect(h.fetch).not.toHaveBeenCalled();
        expect(h.document.getElementById('auditEvidenceSection').classList.contains('d-none')).toBe(true);
        expect(h.document.getElementById('oc-retirement-pending').textContent).toBe('No');
    });
    it('quick view: an evidence read that fails says so and leaves no spinner', async () => {
        const h = harness({ canAudit: 'true' }, { dom: detailsDom });
        await h.sut.initDataTable();
        h.fetch.mockResolvedValue({ status: 403, ok: false, json: async () => ({ errorCode: 'ABBREVIATION_PERMISSION_DENIED' }) });

        h.sut.showDetails({ id: 'entry', abbreviation: 'ABC', version: 1, lifecycleStatus: 'ACTIVE' });
        await new Promise((resolve) => setTimeout(resolve, 0));

        expect(h.win.showToast).toHaveBeenCalledWith('denied', 'error');
        expect(h.document.getElementById('auditEvidenceLoading').classList.contains('d-none')).toBe(true);
    });
});

describe('MOD-0290-FU01 Product Abbreviation Register', () => {
    const read = (relativePath) => fs.readFileSync(path.join(__dirname, '..', relativePath), 'utf8');
    const script = () => read('wwwroot/assets/js/MDM/ProductAbbreviationRegister/index.js');
    const controller = () => read('Controllers/ProductAbbreviationRegisterController.cs');

    it('uses only the same-origin MVC proxy from browser code', () => {
        const source = script();

        expect(source).toContain("const endpoint = '/MDM/ProductAbbreviationRegister/api'");
        expect(source).not.toMatch(/localhost:5000|:5000\/api|localhost:5059|:5059\/api/);
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['"`]Bearer/);
        expect(source).not.toMatch(/crypto\.randomUUID|Idempotency-Key|X-Tenant-Id/);
    });

    it('keeps the request form to the two approved user fields', () => {
        const view = read('Views/MDM/ProductAbbreviationRegister/_CreateEditOffcanvas.cshtml');

        expect(view.match(/name="GlobalProductId"/g)).toHaveLength(1);
        expect(view.match(/name="Abbreviation"/g)).toHaveLength(1);
        expect(view).not.toMatch(/name="(?:TenantId|LegalEntityId|Reason|IdempotencyKey|ReservationId|LifecycleStatus)"/);
    });

    it('consumes only the existing Global Product selector contract', () => {
        const source = script();
        const proxy = controller();

        expect(source).toContain('`${endpoint}/global-products/selector?${query}`');
        expect(proxy).toContain('/api/global-products/selector');
        expect(source).not.toMatch(/hardcodedProducts|new Option\([^,]+,\s*['"][0-9a-f-]{36}/i);
    });

    it('loads every entry of the exact product and read-only evidence', () => {
        const source = script();

        // FIX1 B2: the requester must see the pending request they just made, so the list reads every entry —
        // LIST: through the server-mode proxy, which reads MDM's every-entry call for the one product.
        expect(source).toContain('`${endpoint}/list`');
        expect(controller()).toContain('/by-global-product/{product:D}/entries');
        expect(source).toContain('`${endpoint}/${encodeURIComponent(entryId)}/evidence`');
        expect(source).toContain('item.canonicalHumanSubjectId ?? item.CanonicalHumanSubjectId');
        expect(source).toContain('item.correlationId ?? item.CorrelationId');
        expect(source).toContain('item.idempotencyKey ?? item.IdempotencyKey');
        expect(source).toContain('item.evidenceHash ?? item.EvidenceHash');
        expect(source).not.toMatch(/\/bulk|delete-record|js-edit-item|aliases|reactivat/i);
    });

    it('uses Golden Slim and tenant-shell contracts', () => {
        const index = read('Views/MDM/ProductAbbreviationRegister/Index.cshtml');
        const table = read('Views/MDM/ProductAbbreviationRegister/_DataTable.cshtml');
        const source = script();

        expect(index).toContain('Layout = "_LayoutTenantShell"');
        expect(index).toContain('<partial name="~/Views/MDM/ProductAbbreviationRegister/_CreateEditOffcanvas.cshtml" />');
        // LIST: the shared shell (v2 table + shaped skeleton) and the shared factory, not a page-local copy.
        expect(table).toContain('<partial name="~/Views/Shared/Components/DataTable/_ListShell.cshtml" model="listShell" />');
        expect(table).toContain('DataMode = "server"');
        expect(table).toContain('HasSelection = false');
        expect(source).toContain('window.DitenDataTable.createList(buildListOptions())');
        expect(source).not.toContain('DtDefaults.create(');
        expect(source).toContain("colReorder: { columns: ':gt(0):not(:last-child)' }");
    });

    it('persists, restores, and resets the complete saved-view state without leaking browser credentials', () => {
        const source = script();

        expect(source).toContain("const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'ProductAbbreviationRegister.v2' }");
        // LIST: save / restore / reset belong to the list component; the page hands it the same view keys.
        expect(source).toContain('moduleKey: personalizationContext.moduleKey');
        expect(source).toContain('pageKey: personalizationContext.pageKey');
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['"`']Bearer|X-Tenant-Id/);
    });

    it('generates correlation and idempotency only inside the MVC proxy', () => {
        const source = controller();

        expect(source).toContain('AuthTokenCookies.GetAccessToken(Request)');
        expect(source).toContain('request.Headers.Add("X-Tenant-Id"');
        expect(source).toContain('request.Headers.Add("X-Correlation-Id"');
        expect(source).toContain('request.Headers.Add("Idempotency-Key", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)))');
    });

    it('ships marker-matched resources for exactly seven tenant locales', () => {
        const resourceDirectory = path.join(__dirname, '..', 'Resources', 'Views', 'MDM', 'ProductAbbreviationRegister');
        const files = fs.readdirSync(resourceDirectory)
            .filter((file) => file.startsWith('ProductAbbreviationRegisterIndex.') && file.endsWith('.resx'))
            .sort();

        expect(files).toEqual(['ar', 'en', 'es', 'fr', 'ru', 'tr', 'zh'].map((locale) => `ProductAbbreviationRegisterIndex.${locale}.resx`).sort());
    });
});
