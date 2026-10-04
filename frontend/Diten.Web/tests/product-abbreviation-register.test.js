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
    function harness(dataset = {}) {
        const selects = [];
        const list = { reload: vi.fn(), appliedFilters: {} };
        const createList = vi.fn(async () => list);
        const root = { dataset, querySelector: () => ({ value: 'synthetic-csrf-fixture' }) };
        const addNew = { classList: { toggle: vi.fn() } };
        const fields = {
            filterGlobalProductId: { value: '' },
            filterGlobalProductText: { value: '' },
            btnFilterApply: { click: vi.fn() }
        };
        const document = {
            getElementById: (id) => id === 'product-abbreviation-register' ? root
                : id === 'dt-product-abbreviation-register' ? {} : fields[id] ?? null,
            querySelector: (selector) => selector === '.add-new' ? addNew : null, addEventListener: vi.fn(), body: {}
        };
        const win = {
            location: { search: '', assign: vi.fn() }, L10n: { AddNew: 'Request', ErrorForbidden: 'denied', ErrorConflict: 'conflict',
                ErrorGateway: 'unavailable', ErrorReconciliation: 'pending', ErrorValidation: 'invalid', ErrorNotFound: 'missing' },
            DitenDataTable: { renderActions: (actions) => actions, createList },
            DtDefaults: { handleUnauthorized: vi.fn() },
            showToast: vi.fn(), showConfirm: vi.fn()
        };
        const fetch = vi.fn();
        const context = { window: win, document, fetch, URLSearchParams, FormData, Option: function(){}, console, setTimeout,
            bootstrap: { Offcanvas: { getOrCreateInstance: () => ({ show: vi.fn(), hide: vi.fn() }) } },
            $: () => ({ length: 1, hasClass: () => false, prop: vi.fn(), select2: x => selects.push(x), on: vi.fn(),
                val: () => ({ trigger: vi.fn() }), data: () => '', find: () => ({ text: () => '' }) })
        };
        const anchor = '        buildListOptions\n    };';
        expect(source.split(anchor)).toHaveLength(2);
        const instrumented = source.replace(anchor,
            '        buildListOptions, canRequest, initSelector, renderActions, requestRetirement, requestCorrection, initDataTable\n    };')
            .replace("document.addEventListener('DOMContentLoaded', () => ProductAbbreviationRegisterList.init());", '');
        vm.createContext(context);
        vm.runInContext(instrumented + '\nglobalThis.testSubject = ProductAbbreviationRegisterList;', context);
        return { sut: context.testSubject, win, fetch, list, createList, fields, selects, addNew };
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
        expect(h.fields.filterGlobalProductId.value).toBe(id);
        expect(h.fields.btnFilterApply.click).toHaveBeenCalledTimes(1);
        await options.filters.loadOptions();
        expect(h.selects).toHaveLength(0);
        expect(options.toolbar.addNewText).toBe('');
        expect(h.fetch).not.toHaveBeenCalled();
    });
    it('ignores a deeplink that is not a product id', async () => {
        const h=harness({canAudit:'true'});
        h.win.location.search='?globalProductId=not-a-guid';
        await h.sut.initDataTable();
        expect(h.fields.filterGlobalProductId.value).toBe('');
        expect(h.fields.btnFilterApply.click).not.toHaveBeenCalled();
    });
    it('hides the request button for a product that already has an active or pending code', () => {
        const h=harness({canRequest:'true',canSelectProduct:'true'});
        const options=h.sut.buildListOptions();
        options.onResponse({data:{items:[{id:'x',lifecycleStatus:'REJECTED'}]}});
        options.config.drawCallback();
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', false);
        options.onResponse({data:{items:[{id:'x',lifecycleStatus:'REQUESTED'}]}});
        options.config.drawCallback();
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', true);
        options.onResponse({data:{items:[{id:'x',lifecycleStatus:1}]}});
        options.config.drawCallback();
        expect(h.addNew.classList.toggle).toHaveBeenLastCalledWith('d-none', true);
    });
    it('preserves saved filter/search/column/order state under the old view keys', () => {
        const options=harness().sut.buildListOptions();
        expect(options.savedView).toMatchObject({ moduleKey:'MasterDataManagement', pageKey:'ProductAbbreviationRegister',
            saveViewColumnIndexes:[1,2,3,4,5], baseOrder:[[2,'asc']] });
        expect(options.filters.fields.map(f=>f.key)).toEqual(['globalProductText','globalProductId']);
        expect(options.filters.resetBtn).toBe('btnFilterReset');
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
        expect(controller()).toContain('/by-global-product/{globalProductId.Value:D}/entries');
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

        expect(source).toContain("const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'ProductAbbreviationRegister' }");
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
