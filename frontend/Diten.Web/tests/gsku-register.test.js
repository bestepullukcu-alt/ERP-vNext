const fs = require('fs');
const path = require('path');

describe('MOD-0290 GSKU Register exposure', () => {
    const root = path.join(__dirname, '..');
    const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8');
    const script = () => read('wwwroot/assets/js/MasterDataManagement/Gskus/index.js');
    const controller = () => read('Controllers/GskusController.cs');

    it('renders the tenant Golden Slim surface and creator-only offcanvas', () => {
        const index = read('Views/MasterDataManagement/Gskus/Index.cshtml');
        const partials = ['_Filter', '_DataTable', '_DetailsQuickView', '_IndexL10n'];
        partials.forEach((partial) => {
            expect(index).toContain(`~/Views/MasterDataManagement/Gskus/${partial}.cshtml`);
        });
        expect(index).toContain('Layout = "_LayoutTenantShell"');
        expect(index).toContain('ViewData["CanCreateGsku"] as bool? == true');
        expect(controller()).toContain('ViewData["CanCreateGsku"] = canCreate');
        expect(index).toMatch(/@if \(canCreate\)[\s\S]*_CreateEditOffcanvas\.cshtml/);
        expect(index).toContain('data-can-create="@canCreate.ToString().ToLowerInvariant()"');
    });

    it('exposes exactly the four approved same-origin MVC routes', () => {
        const source = controller();
        expect(source).toContain('[Route("MasterDataManagement/Gskus")]');
        expect(source).toContain('[HttpGet("api")]');
        expect(source).toContain('[HttpGet("api/{id:guid}")]');
        expect(source).toContain('[HttpGet("api/create-options")]');
        expect(source).toContain('[HttpPost("api")]');
        expect(source).toContain('/api/gskus/drafts');
        expect(source).not.toMatch(/HttpPut|HttpPatch|HttpDelete|code-reservations|\/bulk|lifecycle/i);
    });

    it('keeps browser traffic on the same-origin proxy without credentials or generated identity', () => {
        const source = script();
        expect(source).toContain("const endpoint = '/MasterDataManagement/Gskus/api'");
        expect(source).toContain('`${endpoint}/create-options`');
        expect(source).not.toMatch(/localhost:5000|:5000\/api|localhost:5059|:5059\/api/);
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['"`]Bearer|X-Tenant-Id/);
        expect(source).not.toMatch(/crypto\.randomUUID|uuidv?4|Guid\.NewGuid|IdempotencyKey|ReservationId|Credential/);
    });

    it('posts only three business fields plus anti-forgery and opaque attempt metadata', () => {
        const source = script();
        const view = read('Views/MasterDataManagement/Gskus/_CreateEditOffcanvas.cshtml');
        ['GlobalProductId', 'PackQuantity', 'PackUomCode'].forEach((field) => {
            expect(view).toContain(`name="${field}"`);
            expect(source).toContain(`body.set('${field}'`);
        });
        expect(view).toContain('name="FormAttemptToken"');
        expect(source).toContain("body.set('FormAttemptToken'");
        expect(source).toContain("body.set('__RequestVerificationToken'");
        expect(source).not.toMatch(/body\.(?:set|append)\(['"](?:TenantId|CanonicalCode|RevisionIdentifier|GskuReservationId|ExpectedReservationVersion|CreationCommandId|CatalogVersionId)/);
    });

    it('uses server-side Data Protection and stable transport idempotency', () => {
        const source = controller();
        expect(source).toContain('IDataProtectionProvider dataProtectionProvider');
        expect(source).toContain('.ToTimeLimitedDataProtector()');
        expect(source).toContain('RandomNumberGenerator.GetBytes(32)');
        expect(source).toContain('TryReadFormAttempt(formAttemptToken, out var operationKey)');
        expect(source).toContain('request.Headers.TryAddWithoutValidation("Idempotency-Key", operationKey)');
        expect(source).toContain('formAttemptToken = CreateFormAttemptToken()');
        expect(source).toContain('formAttemptToken\n                    });');
        expect(source).toContain('catch (CryptographicException)');
        expect(source).not.toContain('DataProtectionProvider.Create(');
    });

    it('enforces read/create visibility and denies creator endpoints server-side', () => {
        const source = controller();
        expect(source).toContain('private const string ReadPermission = "mdm.gskus.read"');
        expect(source).toContain('private const string CreatePermission = "mdm.gskus.create"');
        expect(source.match(/!HasPermission\(CreatePermission\)/g).length).toBeGreaterThanOrEqual(2);
        expect(script()).toContain('exportButtons(canCreate ? L.AddNew : null');
    });

    it('uses only the frozen create-options fields and provider precision', () => {
        const models = read('Models/Gskus/GskuViewModels.cs');
        const source = script();
        ['GlobalProducts', 'Uoms', 'CanonicalCode', 'GlobalProductName', 'DisplayText', 'SortOrder', 'MaximumDecimalPrecision']
            .forEach((field) => expect(models).toContain(field));
        expect(models.match(/public int LifecycleStatus \{ get; set; \}/g)?.length).toBe(2);
        expect(source).toContain("valueOf(item, 'maximumDecimalPrecision', 'MaximumDecimalPrecision')");
        expect(source).toContain('validateQuantity(quantity');
        expect(source).not.toMatch(/SCALAR_QUANTITY_APPLIES|\bC62\b|\bGRM\b|\bKGM\b|\bMLT\b|\bLTR\b/);
    });

    it('treats only 201 as success and keeps 202 open for replay', () => {
        const source = script();
        expect(source).toContain('response.status !== 201 && response.status !== 202');
        expect(source).toContain('response.status === 202 || payload?.success === false');
        expect(source).toContain("L.CreateReconciliationPending, 'warning'");
        expect(source).toContain("L.CreateSuccessWithIdentifiers || ''");
        expect(source).toContain(".replace('{0}', code)");
        expect(source).toContain(".replace('{1}', revision)");
        const pendingBlock = source.match(/if \(response\.status === 202 \|\| payload\?\.success === false\)[\s\S]*?\n\s*}/)?.[0] || '';
        expect(pendingBlock).not.toMatch(/hide\(\)|form\.reset|ajax\.reload|tokenInput\.value/);
    });

    it('maps bounded safe errors including provider failures', () => {
        const source = controller();
        expect(source).toContain('HttpStatusCode.NotFound => StatusCodes.Status404NotFound');
        expect(source).toContain('HttpStatusCode.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable');
        expect(source).toContain('HttpStatusCode.GatewayTimeout => StatusCodes.Status504GatewayTimeout');
        expect(source).toContain('StatusCodes.Status503ServiceUnavailable => _localizer["ErrorProviderUnavailable"]');
        expect(source).toContain('StatusCodes.Status504GatewayTimeout => _localizer["ErrorProviderTimeout"]');
        expect(source).not.toMatch(/payload\?\.Errors|envelope\?\.Errors/);
    });

    it('keeps server-side DataTable, skeleton, Save View and factory reset without selection surfaces', () => {
        const table = read('Views/MasterDataManagement/Gskus/_DataTable.cshtml');
        const source = script();
        expect(table).toContain('id="skeleton-loader"');
        expect(table).toContain('data-dt-standard="v2"');
        expect(source).toContain('serverSide: true');
        expect(source).toContain('window.personalizationClient');
        expect(source).toContain('dt-save-filter-btn');
        expect(source).toContain('applySavedTableState(dt, getResetBaselineState())');
        expect(source).toContain("className: 'js-quick-view'");
        expect(source).not.toMatch(/localStorage|sessionStorage|delete-record|js-edit-item|bulk-delete|bulkAction|row-checkbox/);
        expect(table).not.toMatch(/type="checkbox"|select-all|dt-checkboxes/);
    });

    it('ships an identical localization key set for all seven locales', () => {
        const locales = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
        const keys = locales.map((locale) => {
            const xml = read(`Resources/Views/MasterDataManagement/Gskus/GskusIndex.${locale}.resx`);
            return [...xml.matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]).sort();
        });
        keys.slice(1).forEach((keySet) => expect(keySet).toEqual(keys[0]));
        ['CreateReconciliationPending', 'ErrorInvalidFormAttempt', 'ErrorProviderUnavailable', 'ErrorProviderTimeout']
            .forEach((key) => expect(keys[0]).toContain(key));
    });
});
