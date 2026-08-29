const fs = require("fs");
const path = require("path");

describe("MOD-0290 Global Product Register", () => {
    const read = (relativePath) => fs.readFileSync(path.join(__dirname, "..", relativePath), "utf8");
    const indexScript = () => read("wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.js");
    const l10nScript = () => read("wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.l10n.js");
    const controller = () => read("Controllers/GlobalProductsController.cs");

    it("uses the server-verified create permission to render the canonical toolbar action", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/Index.cshtml");
        const source = indexScript();

        expect(view).toContain('@inject Diten.Web.Services.IPermissionSnapshot Permissions');
        expect(view).toContain('Permissions.Has("mdm.global-products.create")');
        expect(view).toContain('data-can-create="@canCreate.ToString().ToLowerInvariant()"');
        expect(source).toContain("const permissionHost = document.querySelector('[data-can-create]')");
        expect(source).toContain("const canCreate = permissionHost?.getAttribute('data-can-create') === 'true'");
        expect(source).toContain('exportButtons(canCreate ? L.AddNew : null');
        expect(l10nScript()).toContain('normalized[toPascalCase(key)] = raw[key]');
    });

    it("uses only the same-origin MVC proxy from browser code", () => {
        const source = indexScript();

        expect(source).toContain("const endpoint = '/MasterDataManagement/GlobalProducts/api'");
        expect(source).not.toMatch(/localhost:5000|:5000\/api|localhost:5059|:5059\/api/);
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['\"`]Bearer/);
    });

    it("keeps the browser create payload to the single user field", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml");
        const source = indexScript();

        expect(view.match(/name="GlobalProductName"/g)).toHaveLength(1);
        expect(view).not.toMatch(/name="(?:TenantId|CanonicalCode|ReservationId|IdempotencyKey)"/);
        expect(source).not.toMatch(/body\.(?:set|append)\(['\"](?:TenantId|CanonicalCode|ReservationId|IdempotencyKey)/);
    });

    it("orchestrates reservation and draft creation on the MVC server", () => {
        const source = controller();

        expect(source).toContain("ReserveCodeAsync(model.GlobalProductName");
        expect(source).toContain("CreateDraftAsync(");
        expect(source).toContain("/api/global-products/code-reservations");
        expect(source).toContain("/api/global-products/drafts");
        expect(source).toContain("Guid.NewGuid().ToString(\"N\")");
    });

    it("keeps list, detail, selector, and create under the canonical proxy route", () => {
        const source = controller();

        expect(source).toContain('[Route("MasterDataManagement/GlobalProducts")]');
        expect(source).toContain('[HttpGet("api")]');
        expect(source).toContain('[HttpGet("api/{id:guid}")]');
        expect(source).toContain('[HttpGet("api/selector")]');
        expect(source).toContain('[HttpPost("api")]');
    });

    it("offers only the read-only details row action", () => {
        const source = indexScript();

        expect(source).toContain("className: 'js-quick-view'");
        expect(source).toContain("className: 'js-submit-identity'");
        expect(source).toContain("className: 'js-retire-identity'");
        expect(source).not.toMatch(/approve-identity|reject-identity/);
        expect(source).not.toMatch(/delete-record|js-edit-item|\/bulk/);
    });

    it("gates lifecycle actions by exact permission and fresh state", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/Index.cshtml");
        const source = indexScript();

        expect(view).toContain('Permissions.Has("mdm.global-products.submit")');
        expect(view).toContain('Permissions.Has("mdm.global-products.retire")');
        expect(view).toContain('data-can-submit="@canSubmit.ToString().ToLowerInvariant()"');
        expect(view).toContain('data-can-retire="@canRetire.ToString().ToLowerInvariant()"');
        expect(source).toContain('const detail = await fetchDetail(id)');
        expect(source).toContain("const expectedState = action === 'submit' ? 1 : 3");
        expect(source).toContain("if (state === 1 && canSubmit)");
        expect(source).toContain("if (state === 3 && canRetire)");
    });

    it("keeps lifecycle mutation strict, antiforgery protected, and server-owned", () => {
        const source = controller();
        const browser = indexScript();

        expect(source).toContain('[HttpPost("api/{id:guid}/submit")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/retire")]');
        expect(source.match(/\[ValidateAntiForgeryToken\]/g).length).toBeGreaterThanOrEqual(3);
        expect(source).toContain('private const string SubmitPermission = "mdm.global-products.submit"');
        expect(source).toContain('private const string RetirePermission = "mdm.global-products.retire"');
        expect(source.match(/\[FromForm\] int\? expectedVersion/g)?.length).toBe(2);
        expect(source).toContain('expectedVersion is null or < 0');
        expect(source).toContain('expectedVersion.Value');
        expect(source).toContain('AppendLengthPrefixed(hash, tenantId.ToString("D"))');
        expect(source).toContain('AppendLengthPrefixed(hash, actor)');
        expect(source).toContain('request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"))');
        expect(source).toContain('form.Count == allowed.Count');
        expect(source).toContain('antiforgery.Count == 1');
        expect(source).toContain('required.All(field => form.TryGetValue(field, out var values) && values.Count == 1)');
        expect(browser).toContain("body.set('ExpectedVersion'");
        expect(browser).toContain("body.set('ReasonCode'");
        expect(browser).not.toMatch(/body\.set\(['"](?:TenantId|Actor|OperationId|IdempotencyKey)/);
    });

    it("accepts only exact lifecycle success envelopes and never relays arbitrary 2xx content", () => {
        const source = controller();
        const lifecycleProxy = source.match(/private async Task<IActionResult> ProxyLifecycleAsync[\s\S]*?private IActionResult LifecycleFailure/)?.[0] || '';

        expect(source).toContain('new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted }');
        expect(source).toContain('new HashSet<int> { StatusCodes.Status200OK }');
        expect(lifecycleProxy).toContain('allowedSuccessStatusCodes.Contains(responseStatus)');
        expect(lifecycleProxy).toContain('IsJsonMediaType(mediaType)');
        expect(source).toContain('mediaType?.EndsWith("+json"');
        expect(lifecycleProxy).toContain('JsonSerializer.Deserialize<LifecycleGatewayEnvelope>');
        expect(lifecycleProxy).toContain('envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus');
        expect(lifecycleProxy).toContain('ContentType = "application/json"');
        expect(lifecycleProxy).not.toContain('ContentType = response.Content.Headers.ContentType?.ToString()');
    });

    it("fails closed unless lifecycle actor claims resolve to one canonical human subject", () => {
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
    });

    it("uses premium retirement confirmation and honest reconciliation wording", () => {
        const source = indexScript();

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
        expect(source).not.toMatch(/SubmitApproved|approved successfully/i);
    });

    it("provides the future ABB selector through the same proxy surface", () => {
        const source = indexScript();

        expect(source).toContain("window.DitenSelectors.globalProducts");
        expect(source).toContain("`${endpoint}/selector?");
    });

    it("ships an identical lifecycle localization key set for all seven locales", () => {
        const locales = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
        const keys = locales.map((locale) => {
            const xml = read(`Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.${locale}.resx`);
            return [...xml.matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]).sort();
        });
        keys.slice(1).forEach((keySet) => expect(keySet).toEqual(keys[0]));
        ['SubmitIdentity', 'RetireIdentity', 'RetirementReasonRequired', 'SubmitPendingSuccess', 'ErrorTimeout']
            .forEach((key) => expect(keys[0]).toContain(key));
    });
});
