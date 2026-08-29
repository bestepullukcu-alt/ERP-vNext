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

    it('exposes the approved create/read and lifecycle same-origin MVC routes', () => {
        const source = controller();
        expect(source).toContain('[Route("MasterDataManagement/Gskus")]');
        expect(source).toContain('[HttpGet("api")]');
        expect(source).toContain('[HttpGet("api/{id:guid}")]');
        expect(source).toContain('[HttpGet("api/create-options")]');
        expect(source).toContain('[HttpPost("api")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/submit")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/retire")]');
        expect(source).toContain('/api/gskus/drafts');
        expect(source).not.toMatch(/HttpPut|HttpPatch|HttpDelete|code-reservations|\/bulk/);
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

    it('gates lifecycle actions by exact permission and freshly fetched detail state', () => {
        const source = controller();
        const browser = script();
        const view = read('Views/MasterDataManagement/Gskus/Index.cshtml');

        expect(source).toContain('private const string SubmitPermission = "mdm.gskus.submit"');
        expect(source).toContain('private const string RetirePermission = "mdm.gskus.retire"');
        expect(source).toContain('ViewData["CanSubmitGsku"] = HasPermission(SubmitPermission)');
        expect(source).toContain('ViewData["CanRetireGsku"] = HasPermission(RetirePermission)');
        expect(view).toContain('data-can-submit="@canSubmit.ToString().ToLowerInvariant()"');
        expect(view).toContain('data-can-retire="@canRetire.ToString().ToLowerInvariant()"');
        expect(browser).toContain('const detail = await fetchDetail(id)');
        expect(browser).toContain("const expectedState = action === 'submit' ? 1 : 3");
        expect(browser).toContain("if (state === 1 && canSubmit)");
        expect(browser).toContain("if (state === 3 && canRetire)");
        expect(browser).not.toMatch(/approve-identity|reject-identity/);
    });

    it('derives a stable D-GUID on the server and forwards it only as a header', () => {
        const source = controller();
        const browser = script();

        expect(source).toContain('AppendLengthPrefixed(hash, tenantId.ToString("D"))');
        expect(source).toContain('AppendLengthPrefixed(hash, actor)');
        expect(source.match(/\[FromForm\] int\? expectedVersion/g)?.length).toBe(2);
        expect(source).toContain('expectedVersion is null or < 0');
        expect(source).toContain('expectedVersion.Value');
        expect(source).toContain('AppendLengthPrefixed(hash, aggregateType)');
        expect(source).toContain('AppendLengthPrefixed(hash, action)');
        expect(source).toContain('AppendLengthPrefixed(hash, reasonCode)');
        expect(source).toContain('operationId.ToString("D")');
        expect(source).toContain('form.Count == allowed.Count');
        expect(source).toContain('antiforgery.Count == 1');
        expect(source).toContain('required.All(field => form.TryGetValue(field, out var values) && values.Count == 1)');
        expect(browser).toContain("body.set('ExpectedVersion'");
        expect(browser).toContain("body.set('ReasonCode'");
        expect(browser).not.toMatch(/body\.set\(['"](?:TenantId|Actor|OperationId|IdempotencyKey)/);
    });

    it('accepts only exact lifecycle success envelopes and never relays arbitrary 2xx content', () => {
        const source = controller();

        expect(source).toContain('new HashSet<int> { StatusCodes.Status200OK }');
        expect(source).toContain('new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted }');
        expect(source).toContain('allowedSuccessStatusCodes.Contains(responseStatus)');
        expect(source).toContain('IsJsonMediaType(mediaType)');
        expect(source).toContain('mediaType?.EndsWith("+json"');
        expect(source).toContain('JsonSerializer.Deserialize<LifecycleGatewayEnvelope>');
        expect(source).toContain('envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus');
        expect(source).toContain('ContentType = "application/json"');
    });

    it('fails closed unless lifecycle actor claims resolve to one canonical human subject', () => {
        const source = controller();
        const identity = source.match(/private bool TryResolveLifecycleIdentity[\s\S]*?private static Guid CreateLifecycleOperationId/)?.[0] || '';

        expect(identity).toContain('User.Identity?.IsAuthenticated != true');
        expect(identity).toContain('SingleClaim(User, "actor_type")');
        expect(identity).toContain('"tenant_user" or "platform_admin" or "partner_admin"');
        expect(identity).toContain('subjects.Count > 1 || nameIdentifiers.Count > 1');
        expect(identity).toContain('subjects.Count == 0 && nameIdentifiers.Count == 0');
        expect(identity).toContain('subject.HasValue && nameIdentifier.HasValue && subject != nameIdentifier');
        expect(identity).toContain('string.Equals(claim.Type, type, StringComparison.Ordinal)');
        expect(identity).toContain('Guid.TryParseExact(values[0], "D"');
        expect(identity).toContain('parsed == Guid.Empty');
        expect(identity).toContain('actor = subjectId.ToString("D")');
        expect(identity).not.toContain('User.Identity?.Name');
        expect(identity).not.toContain('ResolveUserSubject()');
    });

    it('uses premium bounded retirement confirmation and reloads list plus detail', () => {
        const source = script();
        expect(source).toContain('showInput: true');
        expect(source).toContain('inputRequired: true');
        expect(source).toContain('inputAttributes: { maxlength: 128 }');
        expect(source).toContain('const renderDetail = (detail, expectedId) =>');
        expect(source).toContain('const refreshedDetail = await fetchDetail(id)');
        expect(source).toContain('renderDetail(refreshedDetail, id)');
        expect(source).toContain("refreshedState !== (action === 'submit' ? 2 : 4)");
        expect(source.indexOf('renderDetail(refreshedDetail, id)')).toBeLessThan(source.indexOf("action === 'submit' ? L.SubmitPendingSuccess : L.RetireSuccess"));
        expect(source).not.toContain('await populateDetails(id)');
        expect(source).toContain("action === 'submit' ? L.SubmitPendingSuccess : L.RetireSuccess");
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
        ['SubmitIdentity', 'RetireIdentity', 'RetirementReasonRequired', 'SubmitPendingSuccess', 'ErrorTimeout']
            .forEach((key) => expect(keys[0]).toContain(key));
    });
});
