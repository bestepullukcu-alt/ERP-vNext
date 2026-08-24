const fs = require('fs');
const path = require('path');

describe('MOD-0290 Finished Good Draft Foundation', () => {
    const root = path.join(__dirname, '..');
    const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8');
    const script = () => read('wwwroot/assets/js/MasterDataManagement/FinishedGoods/index.js');
    const l10nScript = () => read('wwwroot/assets/js/MasterDataManagement/FinishedGoods/index.l10n.js');
    const controller = () => read('Controllers/FinishedGoodsController.cs');

    it('uses canonical partial paths and the server-verified create permission', () => {
        const index = read('Views/MasterDataManagement/FinishedGoods/Index.cshtml');
        const source = script();
        const partials = ['_Filter', '_DataTable', '_DetailsQuickView', '_CreateEditOffcanvas', '_IndexL10n'];

        partials.forEach((partial) => {
            expect(index).toContain(`~/Views/MasterDataManagement/FinishedGoods/${partial}.cshtml`);
        });
        expect(index).toContain('@inject Diten.Web.Services.IPermissionSnapshot Permissions');
        expect(index).toContain('Permissions.Has("mdm.finished-goods.create")');
        expect(index).toContain('data-can-create="@canCreate.ToString().ToLowerInvariant()"');
        expect(source).toContain("const canCreate = document.querySelector('[data-can-create]')");
        expect(source).toContain('exportButtons(canCreate ? L.AddNew : null');
        expect(l10nScript()).toContain('normalized[toPascalCase(key)] = raw[key]');
    });

    it('uses only the same-origin MVC proxy from browser code', () => {
        const source = script();
        expect(source).toContain("const endpoint = '/MasterDataManagement/FinishedGoods/api'");
        expect(source).toContain("const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' })");
        expect(source).toContain('headers: getAuthHeaders()');
        expect(source).not.toMatch(/localhost:5000|:5000\/api|localhost:5059|:5059\/api/);
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['"`]Bearer|X-Tenant-Id/);
    });

    it('keeps the browser create payload to exactly one business field', () => {
        const view = read('Views/MasterDataManagement/FinishedGoods/_CreateEditOffcanvas.cshtml');
        const source = script();
        expect(view.match(/name="GskuId"/g)).toHaveLength(1);
        expect(view).not.toMatch(/name="(?:TenantId|CanonicalCode|CodeReservationId|IdempotencyKey|LskuId|StewardLabel)"/);
        expect(source).toContain("body.set('GskuId', String(gskuId))");
        expect(source).not.toMatch(/body\.(?:set|append)\(['"](?:TenantId|CanonicalCode|CodeReservationId|IdempotencyKey|LskuId|StewardLabel)/);
        expect(source).not.toMatch(/crypto\.randomUUID|uuid|Guid\.NewGuid/);
    });

    it('creates directly through the server-side draft proxy without a public reservation flow', () => {
        const source = controller();
        expect(source).toContain('Guid.NewGuid().ToString("N")');
        expect(source).toContain('/api/finished-goods/drafts');
        expect(source).not.toMatch(/code-reservations|ReserveCode|ReservationId/);
        expect(source).not.toContain('catch (Exception');
        expect(source).not.toContain('catch (OperationCanceledException');
    });

    it('enforces anti-forgery and keeps cancellation plus bounded status mapping explicit', () => {
        const source = controller();
        const browser = script();
        expect(source).toContain('[ValidateAntiForgeryToken]');
        expect(browser).toContain("input[name=\"__RequestVerificationToken\"]");
        expect(browser).toContain("'RequestVerificationToken': token");
        expect(source).toContain('catch (HttpRequestException exception)');
        expect(source).not.toMatch(/catch\s*\(\s*(?:Exception|OperationCanceledException)/);
        expect(browser).toMatch(/400:\s*L\.ErrorValidation/);
        expect(browser).toMatch(/401:\s*L\.ErrorUnauthorized/);
        expect(browser).toMatch(/403:\s*L\.ErrorForbidden/);
        expect(browser).toMatch(/404:\s*L\.ErrorNotFound/);
        expect(browser).toMatch(/409:\s*L\.ErrorConflict/);
        expect(browser).toMatch(/500:\s*L\.ErrorGateway/);
    });

    it('exposes only list, detail, selector and create MVC proxy routes', () => {
        const source = controller();
        expect(source).toContain('[Route("MasterDataManagement/FinishedGoods")]');
        expect(source).toContain('[HttpGet("api")]');
        expect(source).toContain('[HttpGet("api/{id:guid}")]');
        expect(source).toContain('[HttpGet("api/gsku-selector")]');
        expect(source).toContain('[HttpPost("api")]');
        expect(source).not.toMatch(/HttpPut|HttpPatch|HttpDelete|bulk|lifecycle/i);
    });

    it('uses only the bounded search contract and exact selector path', () => {
        const source = script();
        expect(source).toContain('pageLength: 20');
        expect(source).toContain("query.set('search', search)");
        expect(source).toContain("pageSize: '20'");
        expect(source).toContain('`${endpoint}/gsku-selector?${query}`');
        expect(source).not.toMatch(/query\.set\(['"](?:lifecycleStatus|marketCode|legalEntityId)/);
        expect(source).toContain('id: item.id || item.Id');
        expect(source).toContain('text: item.gskuCanonicalCode || item.GskuCanonicalCode');
        expect(source).not.toMatch(/item\.(?:display|Display|name|Name|stewardLabel|StewardLabel)/);
    });

    it('distinguishes 201 success from 202 pending reconciliation and reloads both', () => {
        const source = script();
        expect(source).toContain('response.status !== 201 && response.status !== 202');
        expect(source).toContain('if (response.status === 202)');
        expect(source).toContain("'warning'");
        expect(source).toContain("'success'");
        expect(source).toContain('dt?.ajax.reload(null, false)');
        expect(source).toContain('L.CreatePending');
    });

    it('keeps Golden Slim DataTable v2, CreatedAt, ColReorder and Save View behavior', () => {
        const index = read('Views/MasterDataManagement/FinishedGoods/Index.cshtml');
        const table = read('Views/MasterDataManagement/FinishedGoods/_DataTable.cshtml');
        const source = script();
        expect(index).toContain('Layout = "_LayoutTenantShell"');
        expect(table).toContain('data-dt-standard="v2"');
        expect(table).toContain('@Localizer["CreatedAt"]');
        expect(source).toContain("colReorder: { columns: ':gt(0):not(:last-child)' }");
        expect(source).toContain('window.personalizationClient');
        expect(source).toContain('dt-save-filter-btn');
        expect(source).toContain('loadDefaultView');
        expect(source).not.toMatch(/localStorage|sessionStorage/);
    });

    it('restores the complete factory state and recalculates Save View dirtiness', () => {
        const source = script();
        expect(source).toContain('const getResetBaselineState = () => normalizeView({');
        expect(source).toContain('filters: emptyFilters()');
        expect(source).toContain("search: ''");
        expect(source).toContain('colVis: defaultColVis()');
        expect(source).toContain('columnOrder: Array.from({ length: totalColumnCount }');
        expect(source).toContain('order: baseOrder');
        expect(source).toContain('applySavedTableState(dt, getResetBaselineState())');
        expect(source).toContain('setSaveFilterVisible(isDirtyComparedToDefault(dt))');
    });

    it('offers only read-only quick view and no edit, delete, bulk or selection control', () => {
        const source = script();
        const table = read('Views/MasterDataManagement/FinishedGoods/_DataTable.cshtml');
        expect(source).toContain("className: 'js-quick-view'");
        expect(source).not.toMatch(/delete-record|js-edit-item|bulk-delete|bulkAction|row-checkbox/);
        expect(table).not.toMatch(/type="checkbox"|select-all|dt-checkboxes/);
    });

    it('ships the same localization key set for all seven locales', () => {
        const locales = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
        const keys = locales.map((locale) => {
            const xml = read(`Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.${locale}.resx`);
            return [...xml.matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]).sort();
        });
        keys.slice(1).forEach((keySet) => expect(keySet).toEqual(keys[0]));
        expect(keys[0]).toContain('CreatePending');
        expect(keys[0]).toContain('GskuNotReferenceable');
        ['QuickView', 'Search', 'Export', 'ColumnVisibility', 'Status']
            .forEach((key) => expect(keys[0]).toContain(key));
    });
});
