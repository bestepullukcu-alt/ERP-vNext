const fs = require('fs');
const path = require('path');

describe('ABB executed browser contract', () => {
    const vm = require('vm');
    const source = fs.readFileSync(path.join(__dirname, '..', 'wwwroot/assets/js/MDM/ProductAbbreviationRegister/index.js'), 'utf8');
    function harness(dataset = {}) {
        const disabled = [];
        const selects = [];
        const reload = vi.fn();
        const api = {
            ajax: { reload }, search: vi.fn(() => ''),
            table: () => ({ container: () => ({ querySelector: () => null }) }),
            column: () => ({ visible: vi.fn() }), order: vi.fn(), draw: vi.fn(),
            colReorder: { order: vi.fn() }
        };
        const root = { dataset, querySelector: () => ({ value: 'synthetic-csrf-fixture' }) };
        const document = {
            getElementById: (id) => id === 'product-abbreviation-register' ? root : id === 'dt-product-abbreviation-register' ? {} : null,
            querySelector: () => null, addEventListener: vi.fn(), body: {}
        };
        let config;
        const win = {
            location: { search: '' }, L10n: { AddNew: 'Request', ErrorForbidden: 'denied', ErrorConflict: 'conflict',
                ErrorGateway: 'unavailable', ErrorReconciliation: 'pending', ErrorValidation: 'invalid' },
            DitenDataTable: { renderActions: (actions) => actions, bindBulkSelection: vi.fn(), bindActionDispatcher: vi.fn(), reloadWithToast: (dt) => dt.ajax.reload(null,false) },
            DtDefaults: { create: (c) => { config = c; return c; }, exportButtons: vi.fn((title) => title), handleUnauthorized: vi.fn() },
            showToast: vi.fn(), showConfirm: vi.fn(), personalizationClient: { getViews: vi.fn(async () => []), saveView: vi.fn(async x => x) }
        };
        const fetch = vi.fn();
        const context = { window: win, document, fetch, URLSearchParams, FormData, Option: function(){},
            bootstrap: { Collapse: { getOrCreateInstance: () => ({ hide: vi.fn() }) } }, console, setTimeout,
            DataTable: function() { return api; },
            $: (selector) => ({ length: 1, hasClass: () => false, prop: (key, value) => disabled.push([selector,key,value]),
                select2: x => selects.push(x), on: vi.fn(), val: () => ({ trigger: vi.fn() }), data: () => '' })
        };
        const instrumented = source.replace('return { init: async () => { bindEvents(); await initDataTable(); } };',
            'return { canRequest, initSelector, renderActions, requireSuccess, requestRetirement, requestCorrection, initDataTable, normalizeView, reloadAppliedTableState, loadEvidence, saveDefaultView, getCurrentView, setDt: value => { dt = value; } };')
            .replace("document.addEventListener('DOMContentLoaded', () => ProductAbbreviationRegisterList.init());", '');
        vm.createContext(context);
        vm.runInContext(instrumented + '\nglobalThis.testSubject = ProductAbbreviationRegisterList;', context);
        context.testSubject.setDt(api);
        return { sut: context.testSubject, win, fetch, reload, disabled, selects, api, config: () => config };
    }

    it.each([
        [{ canRequest:'true' }, false],
        [{ canRequest:'true', canSelectProduct:'true' }, true],
        [{ canAudit:'true' }, false]
    ])('uses current permissions for requester/requester+GP-read/auditor access %j', (permissions, allowed) => {
        const h = harness(permissions);
        expect(h.sut.canRequest()).toBe(allowed);
        h.sut.initSelector('#filterGlobalProduct');
        expect(h.selects.length).toBe(allowed ? 1 : 0);
        expect(h.fetch).not.toHaveBeenCalled();
    });

    it.each(['REQUESTED','REJECTED','CANCELLED','RETIRED'])('offers no active-state mutation in %s', status => {
        const h = harness({ canCorrect:'true',canRetire:'true' });
        expect(h.sut.renderActions({ id:'x', lifecycleStatus:status }).map(x=>x.key)).toEqual(['details']);
    });
    it('shows only exact granted actions and excludes a pending retirement', () => {
        const h = harness({ canCorrect:'true' });
        expect(h.sut.renderActions({ id:'x', lifecycleStatus:'ACTIVE' }).map(x=>x.key)).toEqual(['details','correct']);
        expect(h.sut.renderActions({ id:'x', lifecycleStatus:'ACTIVE', retirementPending:true }).map(x=>x.key)).toEqual(['details']);
    });
    it.each([202,403,404,409])('never reloads a retirement or reports success for failure %s', async status => {
        const h = harness({canRetire:'true'});
        h.fetch.mockResolvedValue({ status, ok:status===202, json:async()=>({isSuccessful:false,errors:[]}) });
        h.sut.requestRetirement({id:'entry',version:4,lifecycleStatus:'ACTIVE'});
        await h.win.showConfirm.mock.calls[0][1]('fixture reason');
        expect(h.reload).not.toHaveBeenCalled();
        expect(h.win.showToast).toHaveBeenCalledWith(expect.any(String),'error');
    });
    it('correction posts expected version with replacement and reason then reloads authoritative state', async () => {
        const h=harness({canCorrect:'true'});
        h.fetch.mockResolvedValue({status:200,ok:true,json:async()=>({isSuccessful:true,data:{}})});
        h.sut.requestCorrection({id:'entry',version:7,lifecycleStatus:'ACTIVE'});
        h.win.showConfirm.mock.calls[0][1]('ABC');
        await h.win.showConfirm.mock.calls[1][1]('reason');
        expect(h.fetch.mock.calls[0][0]).toBe('/MDM/ProductAbbreviationRegister/api/entry/corrections');
        expect(JSON.parse(h.fetch.mock.calls[0][1].body)).toEqual({expectedVersion:7,replacementAbbreviation:'ABC',reason:'reason'});
        expect(h.fetch.mock.calls[0][1].headers.RequestVerificationToken).toBe('synthetic-csrf-fixture');
        expect(h.reload).toHaveBeenCalledWith(null,false);
    });
    it('auditor deeplink loads exact product without the selector or request button', async () => {
        const h=harness({canAudit:'true'});
        const id='11111111-1111-1111-1111-111111111111';
        h.win.location.search='?globalProductId='+id;
        h.fetch.mockResolvedValue({status:200,ok:true,json:async()=>({data:{id:'entry',version:8,lifecycleStatus:'ACTIVE'}})});
        await h.sut.initDataTable();
        const callback=vi.fn();
        h.config().ajax({},callback);
        await new Promise(resolve=>setTimeout(resolve,0));
        expect(h.fetch.mock.calls[0][0]).toBe('/MDM/ProductAbbreviationRegister/api/by-global-product/'+id);
        expect(callback.mock.calls[0][0].data[0].version).toBe(8);
        expect(h.win.DtDefaults.exportButtons.mock.calls[0][0]).toBeNull();
        expect(h.selects).toHaveLength(0);
    });
    it('preserves saved filter/search/column/order state and reloads on reset', async () => {
        const h=harness();
        const state={filters:{globalProductId:'product',globalProductText:'Product'},search:'term',colVis:[true,false,true,true,true],columnOrder:[0,1,2,3,4,5,6,7],order:[[3,'desc']]};
        await h.sut.saveDefaultView(state);
        expect(h.win.personalizationClient.saveView.mock.calls[0][0].viewDefinition).toEqual(state);
        h.sut.reloadAppliedTableState(h.api,state);
        expect(h.reload).toHaveBeenCalledWith(expect.any(Function),false);
        expect(h.api.search).toHaveBeenCalledWith('term');
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

    it('loads an exact-product zero-or-one row and read-only evidence', () => {
        const source = script();

        expect(source).toContain('`${endpoint}/by-global-product/${encodeURIComponent(selectedProductId)}`');
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
        expect(table).toContain('data-dt-standard="v2"');
        expect(table).toContain('id="skeleton-loader"');
        expect(source).toContain('window.DtDefaults.create({');
        expect(source).toContain("stateSave: false");
        expect(source).toContain("colReorder: { columns: ':gt(1):not(:last-child)' }");
    });

    it('persists, restores, and resets the complete saved-view state without leaking browser credentials', () => {
        const source = script();

        expect(source).toContain("const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'ProductAbbreviationRegister' }");
        expect(source).toContain("api.table().container().querySelector('.dt-search input')?.value ?? api.search()");
        expect(source).toContain('const syncSearch = (api, value) =>');
        expect(source).toContain("api.table().container().querySelector('.dt-search input')");
        expect(source).toContain('syncSearch(api, normalized.search);');
        expect(source).toContain('redrawAppliedTableState(this.api(), saved || getResetBaselineState());');
        expect(source).toContain('api.ajax.reload(() => syncSearch(api, normalized.search), false);');
        expect(source).toContain('if (dt) reloadAppliedTableState(dt, getResetBaselineState());');
        expect(source).toContain('setSaveFilterVisible(isDirtyComparedToDefault(dt));');
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
