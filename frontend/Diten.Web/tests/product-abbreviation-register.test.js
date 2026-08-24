const fs = require('fs');
const path = require('path');

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
        expect(index).toContain('<partial name="_CreateEditOffcanvas" />');
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
        expect(source).toContain('request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"))');
    });

    it('ships marker-matched resources for exactly seven tenant locales', () => {
        const resourceDirectory = path.join(__dirname, '..', 'Resources', 'Views', 'MDM', 'ProductAbbreviationRegister');
        const files = fs.readdirSync(resourceDirectory)
            .filter((file) => file.startsWith('ProductAbbreviationRegisterIndex.') && file.endsWith('.resx'))
            .sort();

        expect(files).toEqual(['ar', 'en', 'es', 'fr', 'ru', 'tr', 'zh'].map((locale) => `ProductAbbreviationRegisterIndex.${locale}.resx`).sort());
    });
});
