const fs = require("fs");
const path = require("path");
const vm = require("node:vm");

describe("MOD-0290 Global Product Register", () => {
    const read = (relativePath) => fs.readFileSync(path.join(__dirname, "..", relativePath), "utf8");
    const indexScript = () => read("wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.js");
    const l10nScript = () => read("wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.l10n.js");
    const backboneCss = () => read("wwwroot/assets/css/backbone-custom.css");
    const dataTableDefaults = () => read("wwwroot/assets/js/dt-defaults.js");
    const controller = () => read("Controllers/GlobalProductsController.cs");
    const extractMutationEnvelopeValidator = () => {
        const match = indexScript().match(/const validMutationEnvelope = [\s\S]*?\n    };/);
        if (!match) throw new Error("validMutationEnvelope was not found");
        return Function(`"use strict"; ${match[0]} return validMutationEnvelope;`)();
    };
    const extractMutationReadbackValidators = () => {
        const match = indexScript().match(/const mutationReadbackIsCoherent = [\s\S]*?\n    };/);
        const proof = indexScript().match(/const mutationReadbackProvesCommitted = [\s\S]*?\n    };/);
        if (!match || !proof) throw new Error("mutation readback validators were not found");
        return Function("lifecycleCode", "readAvailableActions", "normalizeString",
            `"use strict"; ${match[0]} ${proof[0]} return { mutationReadbackIsCoherent, mutationReadbackProvesCommitted };`)(
            (value) => Number(value),
            (detail) => detail.availableActions,
            (value) => typeof value === "string" ? value.trim() : "");
    };

    it("uses the server-verified create permission to render the canonical toolbar action", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/Index.cshtml");
        const source = indexScript();

        // FIX6 item 5 (d): Add is read with the controller's reader (PermissionClaims), like the list gate.
        expect(view).toContain('PermissionClaims.HasPermission(User, "mdm.global-products.create")');
        expect(view).not.toContain('IPermissionSnapshot');
        expect(view).toContain('data-can-create="@canCreate.ToString().ToLowerInvariant()"');
        expect(source).toContain("const permissionHost = document.querySelector('[data-can-create]')");
        expect(source).toContain("const canCreate = permissionHost?.getAttribute('data-can-create') === 'true'");
        expect(source).toContain("addNewText: canCreate ? L.AddNew : ''");
        expect(l10nScript()).toContain('normalized[toPascalCase(key)] = raw[key]');
    });

    it("uses only the same-origin MVC proxy from browser code", () => {
        const source = indexScript();

        expect(source).toContain("const endpoint = '/MasterDataManagement/GlobalProducts/api'");
        expect(source).not.toMatch(/localhost:5000|:5000\/api|localhost:5059|:5059\/api/);
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['\"`]Bearer/);
    });

    // WP-MDM-GP-LIST-01: the prefetch is kept on the list component. It is started before the component loads the saved
    // view, and the component's first request is answered from it only when that request is the very same page.
    it("overlaps the first page with saved-view loading without bypassing saved-view correctness", () => {
        const source = indexScript();
        const prefetchIndex = source.indexOf('initialPagePrefetch = startInitialPagePrefetch()');
        const listIndex = source.indexOf('list = await window.DitenDataTable.createList(buildListOptions())');

        expect(prefetchIndex).toBeGreaterThan(-1);
        expect(listIndex).toBeGreaterThan(prefetchIndex);
        expect(source).toContain("xhr: listRequest");
        expect(source).toContain('pageLength: initialPageSize');
        expect(source).toContain('deferRender: true');
    });

    // Owner / CT correction (MOD-0290-FU05 pack): form_field_count 1 → 2 — the panel sends exactly the name and the brand.
    it("keeps the browser create payload to exactly the name and the brand id", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml");
        const source = indexScript();

        expect([...view.matchAll(/\bname="([^"]+)"/g)].map((match) => match[1])).toEqual(["GlobalProductName", "BrandId"]);
        expect(view.match(/name="GlobalProductName"/g)).toHaveLength(1);
        expect(view.match(/name="BrandId"/g)).toHaveLength(1);
        expect(view).toContain('<div class="diten-field">');
        expect(view).toContain('<i class="bx bx-text diten-field-icon" aria-hidden="true"></i>');
        expect(view).not.toMatch(/name="(?:TenantId|CanonicalCode|ReservationId|IdempotencyKey|BrandCode|BrandName)"/);
        expect(source).not.toMatch(/body\.(?:set|append)\(['\"](?:TenantId|CanonicalCode|ReservationId|IdempotencyKey|BrandCode|BrandName)/);
    });

    // WP-MDM-GP-LIST-01: the inline filter (select2 single chip, Apply / Reset, the saved view's filter) is the list
    // component's; the register declares the field and its key, which the component sends as a query parameter.
    it("keeps the lifecycle filter single-valued and hands it to the list component", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/_Filter.cshtml");
        const source = indexScript();
        const styles = backboneCss();

        expect(view).toContain('id="filterLifecycleStatus"');
        expect(view).not.toContain('id="filterLifecycleStatus" multiple');
        expect(view).toContain('id="btnFilterApply"');
        expect(view).toContain('id="btnFilterReset"');
        // MOD-0290-FU05: + the brand filter (one brand; the options are the linkable brands).
        expect(source).toContain("{ id: 'filterLifecycleStatus', key: 'lifecycleStatus', kind: 'single' }");
        expect(source).toContain("{ id: 'filterBrand', key: 'brandId', kind: 'single' }");
        expect(source).toContain("loadOptions: loadBrandFilterOptions");
        expect(source).not.toMatch(/\$filter\.select2\(|appliedFilters|mountInlineFilter/);
        expect(styles).not.toContain('.dt-inline-filter-single--is-empty .select2-selection__rendered');
        expect(styles).not.toContain('.dt-inline-filter-single--has-value .select2-selection__rendered');
        expect(source).toContain('dt?.ajax.reload(null, false)');
    });

    // EK item 11: the register's own assertions live in a GREEN test — the one below is red at baseline for a reason
    // outside this page (it pins the shared dt-defaults.js export shape) and would hide a regression here.
    it("omits the forbidden fake import and leaves the export menu to the list component", () => {
        const source = indexScript();

        expect(source).not.toMatch(/importBtn|bx-import|L\.ComingSoon/);
        // The export menu is the list component's (DtDefaults.exportButtons); server export is BL-452 (open).
        expect(source).not.toContain('window.DtDefaults.exportButtons(');
        expect(source).toContain('exportColumns: saveViewColumnIndexes');
    });

    // WP-LANE-GUARDS-02 — this pin was a baseline red since BL-452 package 2 (c3de1c02c): print and PDF are no longer
    // DataTables' `extend` buttons but the controlled copy (screen, company, filters, rows, uncontrolled mark). All five
    // shared Action exports are still pinned — csv / excel / copy as `extend`, print / pdf as the controlled copy.
    it("omits the forbidden fake import while preserving the real shared Action exports", () => {
        const defaults = dataTableDefaults();

        ['csv', 'excel', 'copy']
            .forEach((format) => expect(defaults).toContain(`extend: '${format}'`));
        ['print', 'pdf'].forEach((format) => {
            expect(defaults).toContain(`attr: { 'data-export-format': '${format}' },\n                    action: function (e, dt) { return runControlledCopy('${format}', dt, exportOptions, controlledCopy); }`);
        });
        expect((defaults.match(/className: 'dropdown-item dt-controlled-copy'/g) || []).length).toBe(2);
    });

    it("orchestrates reservation and draft creation on the MVC server", () => {
        const source = controller();

        expect(source).toContain("ReserveCodeAsync(model.GlobalProductName");
        expect(source).toContain("CreateDraftAsync(");
        expect(source).toContain("/api/global-products/code-reservations");
        expect(source).toContain("/api/global-products/drafts");
        expect(source).toContain("Guid.NewGuid().ToString(\"N\")");
        expect(source).toContain('HasOnlyFormFieldsAsync("GlobalProductName", "BrandId")');
        expect(source).toContain('model.GlobalProductName.EnumerateRunes().Count() > 200');
        expect(read("Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml"))
            .toContain('maxlength="400"');
    });

    it("keeps list, detail, selector, and create under the canonical proxy route", () => {
        const source = controller();

        expect(source).toContain('[Route("MasterDataManagement/GlobalProducts")]');
        expect(source).toContain('[HttpGet("api")]');
        expect(source).toContain('[HttpGet("api/{id:guid}")]');
        expect(source).toContain('[HttpGet("api/selector")]');
        expect(source).toContain('[HttpPost("api")]');
    });

    it("keeps every read-visible row kebab and loads only server-projected actions from fresh detail", () => {
        const source = indexScript();

        expect(source).toContain('js-global-product-actions-toggle');
        expect(source).toContain('const detail = await fetchDetail(id)');
        expect(source).toContain("const raw = detail?.availableActions ?? detail?.AvailableActions");
        expect(source).toContain("actions[0] !== 'DETAILS'");
        expect(source).toContain("new Set(actions).size !== actions.length");
        expect(source).toContain("actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT', 'CHANGE_BRAND']");
        expect(source).not.toMatch(/lifecycleCode\(row\.|data-can-submit|data-can-retire/);
        expect(source).not.toMatch(/approve-identity|reject-identity/);
        expect(source).not.toMatch(/delete-record|\/bulk|\/retire['"`]/);
    });

    it("does not reproduce server permission or ownership decisions in Razor or JavaScript", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/Index.cshtml");
        const source = indexScript();

        expect(view).not.toMatch(/mdm\.global-products\.(?:submit|retire|update|withdraw|request-correction|request-retirement)/);
        expect(source).not.toMatch(/canSubmit|canRetire|canonical submitter|requester/i);
        expect(source).toContain("if (!readAvailableActions(detail).includes(action))");
    });

    it("keeps lifecycle mutation strict, antiforgery protected, and server-owned", () => {
        const source = controller();
        const browser = indexScript();

        expect(source).toContain('[HttpPost("api/{id:guid}/submit")]');
        expect(source).toContain('[HttpPut("api/{id:guid}")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/withdraw")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/correction-requests")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/retirement-requests")]');
        expect(source).not.toContain('[HttpPost("api/{id:guid}/retire")]');
        expect(source.match(/\[ValidateAntiForgeryToken\]/g).length).toBeGreaterThanOrEqual(6);
        expect(source).toContain('private const string SubmitPermission = "mdm.global-products.submit"');
        expect(source).toContain('private const string UpdatePermission = "mdm.global-products.update"');
        expect(source).toContain('private const string WithdrawPermission = "mdm.global-products.withdraw"');
        expect(source).toContain('private const string RequestCorrectionPermission = "mdm.global-products.request-correction"');
        expect(source).toContain('private const string RequestRetirementPermission = "mdm.global-products.request-retirement"');
        expect(source).toContain('expectedVersion is null or < 0');
        expect(source).toContain('expectedVersion.Value');
        expect(source).toContain('AppendLengthPrefixed(hash, tenantId.ToString("D"))');
        expect(source).toContain('AppendLengthPrefixed(hash, actor)');
        expect(source).toContain('request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"))');
        expect(source).toContain('form.Count == allowed.Count');
        expect(source).toContain('antiforgery.Count == 1');
        expect(source).toContain('required.All(field => form.TryGetValue(field, out var values) && values.Count == 1)');
        expect(browser).toContain("body.set('ExpectedVersion'");
        expect(browser).toContain("body.set('ExpectedVersion'");
        expect(browser).toContain("{ GlobalProductName: name }");
        expect(browser).toContain("{ Reason: reason }");
        expect(browser).not.toMatch(/ReasonCode|Comment/);
        expect(browser).not.toMatch(/body\.set\(['"](?:TenantId|Actor|OperationId|IdempotencyKey)/);
    });

    it("accepts only exact lifecycle success envelopes and never relays arbitrary 2xx content", () => {
        const source = controller();
        const browser = indexScript();
        const lifecycleProxy = source.match(/private async Task<IActionResult> ProxyLifecycleAsync[\s\S]*?private IActionResult LifecycleFailure/)?.[0] || '';

        expect(source).toContain('new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted }');
        expect(source).toContain('new HashSet<int> { StatusCodes.Status200OK }');
        expect(lifecycleProxy).toContain('allowedSuccessStatusCodes.Contains(responseStatus)');
        expect(lifecycleProxy).toContain('IsJsonMediaType(mediaType)');
        expect(source).toContain('mediaType?.EndsWith("+json"');
        expect(lifecycleProxy).toContain('JsonSerializer.Deserialize<LifecycleGatewayEnvelope>');
        expect(source).toContain('IsValidLifecycleEnvelope(envelope, responseStatus, mutationKind, aggregateId)');
        ['Submit', 'Update', 'Withdraw', 'Correction', 'Retirement', 'ChangeBrand']
            .forEach((kind) => expect(source).toContain(`LifecycleMutationKind.${kind}`));
        expect(source).toContain('HasMatchingGuid(data, "globalProductId", aggregateId)');
        expect(source).toContain('HasMatchingGuid(data, "id", aggregateId)');
        expect(source).toContain('HasNonNegativeInt(data, "productVersion")');
        expect(source).toContain('HasCheckpointForStatus(data, responseStatus)');
        expect(source).toContain('StatusCodes.Status200OK => string.Equals(');
        expect(source).toContain('checkpoint, "Completed", StringComparison.Ordinal');
        expect(source).toContain('StatusCodes.Status202Accepted => string.Equals(');
        expect(source).toContain('checkpoint, "AwaitingDecision", StringComparison.Ordinal');
        expect(lifecycleProxy).toContain('for (var attempt = 0; attempt < 2; attempt++)');
        expect(lifecycleProxy).toContain('responseStatus == StatusCodes.Status502BadGateway && attempt == 0');
        expect(lifecycleProxy).toContain('catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)');
        expect(lifecycleProxy).toContain('catch (HttpRequestException exception)');
        expect(lifecycleProxy.match(/if \(attempt == 0\)\s+continue;/g)).toHaveLength(5);
        expect(lifecycleProxy).toContain('request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"))');
        expect(lifecycleProxy).toContain('ContentType = "application/json"');
        expect(lifecycleProxy).not.toContain('ContentType = response.Content.Headers.ContentType?.ToString()');
        expect(browser).toContain('const validMutationEnvelope = (payload, responseStatus, action, expectedId) =>');
        expect(browser.indexOf('const refreshedDetail = await fetchDetail(id)'))
            .toBeLessThan(browser.indexOf('if (responseError) throw responseError'));
    });

    it("validates each actual MDM lifecycle response shape without inventing common fields", () => {
        const validate = extractMutationEnvelopeValidator();
        const id = "11111111-1111-1111-1111-111111111111";
        const operationId = "22222222-2222-2222-2222-222222222222";
        const envelope = (statusCode, data) => ({ isSuccessful: true, statusCode, data });

        expect(validate(envelope(202, { operationId, globalProductId: id, checkpoint: "STARTED" }), 202, "SUBMIT", id)).toBe(true);
        expect(validate(envelope(200, { id, lifecycleStatus: 1, version: 2, isReplay: false }), 200, "EDIT", id)).toBe(true);
        expect(validate(envelope(200, { globalProductId: id, lifecycleStatus: 1, version: 3 }), 200, "WITHDRAW_APPROVAL", id)).toBe(true);
        expect(validate(envelope(202, { operationId, globalProductId: id, checkpoint: "AwaitingDecision", productVersion: 4 }), 202, "REQUEST_CORRECTION", id)).toBe(true);
        expect(validate(envelope(200, { operationId, globalProductId: id, checkpoint: "Completed", productVersion: 5 }), 200, "REQUEST_RETIREMENT", id)).toBe(true);

        expect(validate(envelope(200, { globalProductId: id, checkpoint: "STARTED" }), 200, "SUBMIT", id)).toBe(false);
        expect(validate(envelope(200, { id: operationId, lifecycleStatus: 1, version: 2 }), 200, "EDIT", id)).toBe(false);
        expect(validate(envelope(202, { operationId, globalProductId: id, checkpoint: "AwaitingDecision" }), 202, "REQUEST_CORRECTION", id)).toBe(false);
        expect(validate(envelope(202, { operationId, globalProductId: id, checkpoint: "Completed", productVersion: 4 }), 202, "REQUEST_CORRECTION", id)).toBe(false);
        expect(validate(envelope(200, { operationId, globalProductId: id, checkpoint: "AwaitingDecision", productVersion: 4 }), 200, "REQUEST_CORRECTION", id)).toBe(false);
    });

    it("reconciles only mutations proven committed by fresh server detail", () => {
        const { mutationReadbackIsCoherent, mutationReadbackProvesCommitted } =
            extractMutationReadbackValidators();
        const draft = { version: 5, lifecycleStatus: 1, globalProductName: "New name", availableActions: ["DETAILS", "EDIT", "SUBMIT"] };
        const pending = { version: 5, lifecycleStatus: 2, globalProductName: "Old", availableActions: ["DETAILS", "WITHDRAW_APPROVAL"] };
        const approvedBusy = { version: 5, lifecycleStatus: 3, globalProductName: "Old", availableActions: ["DETAILS"] };
        const approvedFree = { version: 6, lifecycleStatus: 3, globalProductName: "New name", availableActions: ["DETAILS", "REQUEST_CORRECTION", "REQUEST_RETIREMENT"] };
        const accepted = { isSuccessful: true, statusCode: 202, data: { productVersion: 5, checkpoint: "AwaitingDecision" } };
        const completed = { isSuccessful: true, statusCode: 200, data: { productVersion: 6, checkpoint: "Completed" } };

        expect(mutationReadbackProvesCommitted(4, draft, "EDIT", { GlobalProductName: "New name" })).toBe(true);
        expect(mutationReadbackProvesCommitted(4, draft, "EDIT", { GlobalProductName: "Different" })).toBe(false);
        expect(mutationReadbackProvesCommitted(4, pending, "SUBMIT", {})).toBe(true);
        expect(mutationReadbackProvesCommitted(4, draft, "WITHDRAW_APPROVAL", {})).toBe(true);
        expect(mutationReadbackProvesCommitted(4, approvedBusy, "REQUEST_CORRECTION", { GlobalProductName: "New" })).toBe(false);
        expect(mutationReadbackIsCoherent(4, approvedBusy, "REQUEST_CORRECTION", {}, 202, accepted)).toBe(true);
        expect(mutationReadbackIsCoherent(4, approvedFree, "REQUEST_CORRECTION", {}, 200, completed)).toBe(true);
        expect(mutationReadbackIsCoherent(4, approvedBusy, "REQUEST_CORRECTION", {}, 200, completed)).toBe(false);
        expect(indexScript()).toContain('[502, 503, 504].includes(response.status)');
        expect(indexScript()).toContain('if (reconciledCommit)');
        expect(indexScript()).toContain('lifecycleRequests.delete(requestKey)');
    });

    // WP-MDM-GP-LIST-01: Save View is the list component's (same moduleKey / pageKey as before); the page has no
    // personalization client of its own, and stays console-free.
    it("keeps optional personalization fallback console-free", () => {
        const source = indexScript();
        expect(source).not.toMatch(/console\.(?:error|warn)\(/);
        expect(source).not.toMatch(/personalizationClient|loadDefaultView|saveDefaultView/);
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
        expect(identity).toContain('TryResolveCanonicalTenant(User, out tenantId)');
        expect(source).toContain('values.Length == 1');
        expect(source).toContain('Guid.TryParseExact(values[0], "D", out tenantId)');
        expect(source).toContain('if (!TryResolveCanonicalTenant(User, out var tenantId))');
        expect(identity).not.toContain('User.Identity?.Name');
    });

    it("uses mode-specific Golden Slim edit and Premium correction/retirement request inputs", () => {
        const source = indexScript();
        const form = read("Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml");

        expect(form).toContain('id="offcanvasCreateEditLabel"');
        expect(source).toContain("editorMode = 'edit'");
        expect(source).toContain("method: 'PUT'");
        expect(source).toContain('showInput: true');
        expect(source).toContain('inputRequired: true');
        expect(source).toContain("inputType: 'text'");
        expect(source).toContain('inputAttributes: { maxlength: 4000 }');
        expect(source).toContain('scalarLength(name) > 200');
        expect(source).toContain("postLifecycle(id, action, { Reason: reason }");
        expect(source).toContain('const refreshedDetail = await fetchDetail(id)');
        expect(source).not.toMatch(/RetireIdentity|RetireSuccess|js-retire-identity/);
        expect(source).not.toMatch(/WorkCenterNext\/Details|source\.deepLink/);
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
        ['FormTitleEdit', 'EditDraft', 'UpdateDraft', 'WithdrawApproval', 'RequestCorrection', 'CurrentPageOnly',
            'RequestRetirement', 'RetirementRequestReasonRequired', 'SubmitPendingSuccess',
            'CorrectionRequestedSuccess', 'RetirementRequestedSuccess', 'ErrorTimeout', 'TechnicalId', 'RecordVersion']
            .forEach((key) => expect(keys[0]).toContain(key));
    });

    it("labels immutable technical metadata explicitly in the detail quick view", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/_DetailsQuickView.cshtml");

        expect(view).toContain('@Localizer["TechnicalId"]');
        expect(view).toContain('@Localizer["RecordVersion"]');
        expect(view).not.toContain('@Localizer["Id"]');
        expect(view).not.toContain('@Localizer["Version"]');
    });
});

// WP-MDM-GP-LIST-01: the register on the list component, executed in an isolated DOM with a fake HTTP layer.
describe("Global Product register on the list component (executed)", () => {
    const source = fs.readFileSync(path.join(__dirname, "../wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.js"), "utf8");
    const page = (items = []) => JSON.stringify({ success: true, data: { items, total: items.length, filteredTotal: items.length } });

    function harness({ fetchAnswer = { status: 200, text: page() } } = {}) {
        document.body.innerHTML = '<table class="datatables-globalproducts" id="dt-globalproducts"></table>' +
            '<div id="offcanvasDetailsPreview"></div>';
        const aborted = [];
        const fetch = vi.fn((url, init) => {
            init?.signal?.addEventListener("abort", () => aborted.push(url));
            return Promise.resolve({
                status: fetchAnswer.status, statusText: "OK",
                headers: { get: () => "application/json; charset=utf-8" },
                text: async () => fetchAnswer.text
            });
        });
        const realRequests = [];
        function FakeXhr() { realRequests.push(this); }
        const fakeWindow = {
            L10n: new Proxy({}, { get: (_, key) => String(key) }),
            showToast: vi.fn(),
            XMLHttpRequest: FakeXhr,
            location: { origin: "https://register.test" }
        };
        // Test-only access to existing closure functions; shipped JS has no test hook.
        const exposed = source.replace("init: async () => {", `test: {
            buildListOptions, startInitialPagePrefetch, listRequest,
            prefetch: () => { initialPagePrefetch = startInitialPagePrefetch(); }
        }, init: async () => {`);
        const api = vm.runInNewContext(exposed + "\nGlobalProductsList.test;", {
            window: fakeWindow, document, fetch, URL, URLSearchParams, AbortController, Promise,
            setTimeout, clearTimeout, console
        });
        return { api, fetch, aborted, realRequests };
    }
    const defaultFirstRequest = "/MasterDataManagement/GlobalProducts/api?start=0&length=10&orderBy=globalProductName&orderDir=asc&draw=1&_=1700000000000";

    it("hands the list component the saved-view keys, the lifecycle filter and only MDM's sortable data names", () => {
        const options = harness().api.buildListOptions();

        expect(options.dataMode).toBe("server");
        expect(options.ajax.url).toBe("/MasterDataManagement/GlobalProducts/api");
        expect(options.savedView).toMatchObject({
            moduleKey: "MasterDataManagement", pageKey: "GlobalProducts", saveViewColumnIndexes: [1, 2, 3, 4], baseOrder: [[2, "asc"]]
        });
        expect(options.filters.fields).toEqual([
            { id: "filterLifecycleStatus", key: "lifecycleStatus", kind: "single" },
            { id: "filterBrand", key: "brandId", kind: "single" }
        ]);
        expect(options.quickView.offcanvasId).toBe("offcanvasDetailsPreview");
        expect(options.export).toBeUndefined();
        const sortable = options.config.columns
            .filter((column, index) => !options.config.columnDefs.some((def) =>
                (def.targets === index || (def.targets === -1 && index === options.config.columns.length - 1)) && def.orderable === false))
            .map((column) => column.data);
        expect(sortable).toEqual(["canonicalCode", "globalProductName", "lifecycleStatus"]);
        expect(Object.keys(options.actions.onRowAction).sort()).toEqual(
            ["changeBrand", "editDraft", "quickView", "requestCorrection", "requestRetirement", "submitIdentity", "withdrawApproval"]);
        expect(options.config.pageLength).toBe(10);
    });

    // EK item 9: no silent partial export — every export entry says "current page only"; nothing else is touched,
    // a server export (BL-452, every matching row) is not labelled, and a second run adds nothing.
    it("labels every export entry of the list as current page only", () => {
        const options = harness().api.buildListOptions();
        const make = (className, icon) => {
            const node = document.createElement("a");
            node.className = className;
            node.innerHTML = icon ? `<span><i class="icon-base bx ${icon} me-2"></i>Entry</span>` : "Entry";
            return node;
        };
        const nodes = [
            make("dropdown-item dt-controlled-copy", "bx-printer"), make("dropdown-item buttons-csv", "bx-file"),
            make("dropdown-item", "bxs-file-export"), make("dropdown-item dt-controlled-copy", "bxs-file-pdf"),
            make("dropdown-item buttons-copy", "bx-copy"),
            make("dropdown-item dt-server-export", "bx-file"), make("btn btn-icon dt-filter-btn", "bx-filter-alt")
        ];
        const api = { buttons: () => ({ nodes: () => nodes }) };

        options.config.initComplete.call({ api: () => api });
        options.config.initComplete.call({ api: () => api });

        nodes.slice(0, 5).forEach((node) => {
            expect(node.querySelectorAll(".dt-current-page-only")).toHaveLength(1);
            expect(node.querySelector(".dt-current-page-only").textContent).toBe("CurrentPageOnly");
            expect(node.getAttribute("title")).toBe("CurrentPageOnly");
        });
        expect(nodes[5].querySelector(".dt-current-page-only")).toBeNull();
        expect(nodes[6].querySelector(".dt-current-page-only")).toBeNull();
    });

    it("answers the component's first request from the prefetch when it is the same page", async () => {
        const h = harness({ fetchAnswer: { status: 200, text: page([{ id: "1", canonicalCode: "GP-1" }]) } });
        h.api.prefetch();
        expect(h.fetch).toHaveBeenCalledTimes(1);
        expect(h.fetch.mock.calls[0][0]).toBe("/MasterDataManagement/GlobalProducts/api?start=0&length=10&orderBy=globalProductName&orderDir=asc");

        const request = h.api.listRequest.call({ url: defaultFirstRequest });
        const loaded = new Promise((resolve) => { request.onload = resolve; });
        request.open("GET", defaultFirstRequest, true);
        request.send(null);
        await loaded;

        expect(h.realRequests).toHaveLength(0);
        expect(h.fetch).toHaveBeenCalledTimes(1);
        expect(request.status).toBe(200);
        expect(JSON.parse(request.responseText).data.items[0].canonicalCode).toBe("GP-1");
        expect(request.getAllResponseHeaders()).toContain("content-type: application/json");
        // Used once: the next request is the component's own.
        h.api.listRequest.call({ url: defaultFirstRequest });
        expect(h.realRequests).toHaveLength(1);
    });

    it("aborts the prefetch and lets the component ask for its own page when the saved view differs", () => {
        const h = harness();
        h.api.prefetch();

        const request = h.api.listRequest.call({
            url: "/MasterDataManagement/GlobalProducts/api?start=0&length=10&orderBy=globalProductName&orderDir=asc&draw=1&lifecycleStatus=Draft"
        });

        expect(h.realRequests).toEqual([request]);
        expect(h.aborted).toHaveLength(1);
    });
});

// MOD-0290-FU05 slice 3: the brand on the register — picker, edit preselection, "No brand", CHANGE_BRAND, server order.
describe("Global Product brand (MOD-0290-FU05)", () => {
    const source = fs.readFileSync(path.join(__dirname, "../wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.js"), "utf8");
    const brandId = "33333333-3333-3333-3333-333333333333";
    const productId = "11111111-1111-1111-1111-111111111111";
    const json = (status, body) => ({
        ok: status >= 200 && status < 300, status, statusText: "OK",
        headers: { get: () => "application/json" },
        json: async () => body, text: async () => JSON.stringify(body)
    });

    function harness(answer) {
        document.body.innerHTML =
            '<div id="offcanvasCreateEdit"><h5 id="offcanvasCreateEditLabel"></h5>' +
            '<form id="formGlobalProduct"><input type="hidden" name="__RequestVerificationToken" value="t" />' +
            '<div id="globalProductNameField"><input id="globalProductName" name="GlobalProductName" required /></div>' +
            '<select id="globalProductBrand" name="BrandId"><option value="">BrandNone</option></select></form>' +
            '<button id="btnSaveGlobalProduct"></button></div>' +
            '<div class="dropdown"><button class="js-global-product-actions-toggle" data-id="' + productId + '"></button>' +
            '<div class="js-global-product-actions-menu"></div></div>' +
            '<div id="offcanvasDetailsPreview"><span id="oc-title"></span><span id="oc-subtitle"></span><span id="oc-id"></span>' +
            '<span id="oc-code"></span><span id="oc-name"></span><span id="oc-brand"></span><span id="oc-status"></span>' +
            '<span id="oc-version"></span><span id="oc-created-at"></span><span id="oc-updated-at"></span></div>';
        const calls = [];
        const fetch = vi.fn(async (url, init) => { calls.push({ url, init }); return answer(url, init); });
        const fakeWindow = {
            L10n: new Proxy({}, { get: (_, key) => String(key) }),
            showToast: vi.fn(),
            showConfirm: vi.fn((message, callback) => callback()),
            location: { origin: "https://register.test" }
        };
        const bootstrap = {
            Dropdown: { getOrCreateInstance: () => ({ show() { } }) },
            Offcanvas: { getOrCreateInstance: () => ({ show() { }, hide() { } }) }
        };
        const exposed = source.replace("init: async () => {", `test: {
            brandPickerResults, readAvailableActions, loadActionMenu, renderDetail, openEdit, openCreate, submitEditor,
            buildListOptions, loadBrandFilterOptions
        }, init: async () => {`);
        const api = vm.runInNewContext(exposed + "\nGlobalProductsList.test;", {
            window: fakeWindow, document, fetch, URL, URLSearchParams, AbortController, Promise, FormData,
            Option: document.defaultView.Option, bootstrap, setTimeout, clearTimeout, console
        });
        return { api, calls, window: fakeWindow };
    }
    const detail = (actions, extra = {}) => ({
        data: {
            id: productId, canonicalCode: "GP-1", globalProductName: "Product", lifecycleStatus: 3, version: 4,
            availableActions: actions, brandId: null, brandCode: null, brandName: null, ...extra
        }
    });

    it("draws in the brand picker only the brand-options answer, after one 'No brand' choice", () => {
        const { api } = harness(() => json(200, {}));
        const first = api.brandPickerResults(
            { data: { items: [{ id: brandId, brandCode: "BRD", brandName: "Brand One" }], totalCount: 30 } }, 1);
        const second = api.brandPickerResults({ data: { items: [], totalCount: 30 } }, 2);

        expect(JSON.parse(JSON.stringify(first))).toEqual({
            results: [{ id: "", text: "BrandNone" }, { id: brandId, text: "BRD · Brand One" }], pagination: { more: true }
        });
        expect(JSON.parse(JSON.stringify(second))).toEqual({ results: [], pagination: { more: false } });
    });

    it("preselects the product's current brand when a draft is edited", async () => {
        const { api } = harness(() => json(200, detail(["DETAILS", "EDIT"], {
            lifecycleStatus: 1, brandId, brandCode: "BRD", brandName: "Brand One"
        })));

        await api.openEdit(productId, null);

        const select = document.getElementById("globalProductBrand");
        expect(select.value).toBe(brandId);
        expect(select.selectedOptions[0].textContent).toBe("BRD · Brand One");
    });

    it("sends an empty BrandId (no brand) when 'No brand' is chosen on create", async () => {
        const { api, calls } = harness(() => json(201, { data: { canonicalCode: "GP-9" } }));
        api.openCreate();
        document.getElementById("globalProductName").value = "New product";

        api.submitEditor();
        await new Promise((resolve) => setTimeout(resolve, 0));

        const post = calls.find((call) => call.init?.method === "POST");
        expect(post.url).toBe("/MasterDataManagement/GlobalProducts/api");
        expect(post.init.body.getAll("BrandId")).toEqual([""]);
        expect(post.init.body.get("GlobalProductName")).toBe("New product");
    });

    it("draws CHANGE_BRAND only when the server lists it", async () => {
        const offered = harness(() => json(200, detail(["DETAILS", "REQUEST_CORRECTION", "REQUEST_RETIREMENT", "CHANGE_BRAND"])));
        await offered.api.loadActionMenu(document.querySelector(".js-global-product-actions-toggle"));
        expect(document.querySelector('.js-global-product-actions-menu [data-action="CHANGE_BRAND"]')).not.toBeNull();

        const withheld = harness(() => json(200, detail(["DETAILS", "REQUEST_CORRECTION", "REQUEST_RETIREMENT"])));
        await withheld.api.loadActionMenu(document.querySelector(".js-global-product-actions-toggle"));
        expect(document.querySelector('.js-global-product-actions-menu [data-action="CHANGE_BRAND"]')).toBeNull();
        expect(document.querySelectorAll(".js-global-product-actions-menu [data-action]")).toHaveLength(3);
    });

    it("reads the server's order with CHANGE_BRAND last and refuses it out of order", () => {
        const { api } = harness(() => json(200, {}));
        const order = ["DETAILS", "REQUEST_CORRECTION", "REQUEST_RETIREMENT", "CHANGE_BRAND"];

        expect(api.readAvailableActions({ availableActions: order })).toEqual(order);
        expect(() => api.readAvailableActions({ availableActions: ["DETAILS", "CHANGE_BRAND", "REQUEST_RETIREMENT"] }))
            .toThrow("ErrorGateway");
    });

    it("loads every page of brand options into the filter, never a capped first page", async () => {
        const option = (n) => ({ id: `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`, brandCode: `B${n}`, brandName: `Brand ${n}` });
        const { api, calls } = harness((url) => {
            const page = Number(new URL(url, "https://register.test").searchParams.get("pageNumber"));
            const items = page === 1 ? Array.from({ length: 100 }, (_, i) => option(i + 1)) : [option(101), option(102)];
            return json(200, { data: { items, totalCount: 102 } });
        });
        document.body.insertAdjacentHTML("beforeend", '<div class="filter-chip"><select id="filterBrand"></select></div>');
        let filled = null;

        await api.loadBrandFilterOptions({ fillSelect: (select, items) => { filled = items; } });

        expect(calls.map((call) => call.url)).toEqual([
            "/MasterDataManagement/GlobalProducts/api/brand-options?pageNumber=1&pageSize=100",
            "/MasterDataManagement/GlobalProducts/api/brand-options?pageNumber=2&pageSize=100"
        ]);
        expect(filled).toHaveLength(102);
        expect(filled[101]).toEqual({ value: option(102).id, text: "B102 · Brand 102" });
    });

    // CT fix round v1.1 (critical): the filter's page loop is bounded and fails closed — never an endless loop.
    it("hides the brand filter after one request when the count is not a number", async () => {
        const { api, calls } = harness(() => json(200, { data: { items: [{ id: "a", brandCode: "A", brandName: "A" }], totalCount: "abc" } }));
        document.body.insertAdjacentHTML("beforeend", '<div class="filter-chip"><select id="filterBrand"></select></div>');
        let filled = false;

        await api.loadBrandFilterOptions({ fillSelect: () => { filled = true; } });

        expect(calls).toHaveLength(1);
        expect(filled).toBe(false);
        expect(document.getElementById("filterBrand").closest(".filter-chip").classList.contains("d-none")).toBe(true);
    });

    it("stops a server that repeats the same full page and shows no duplicated list", async () => {
        const fullPage = Array.from({ length: 100 }, (_, i) => ({ id: `b${i}`, brandCode: `B${i}`, brandName: `Brand ${i}` }));
        const { api, calls } = harness(() => json(200, { data: { items: fullPage, totalCount: 250 } }));
        document.body.insertAdjacentHTML("beforeend", '<div class="filter-chip"><select id="filterBrand"></select></div>');
        let filled = false;

        await api.loadBrandFilterOptions({ fillSelect: () => { filled = true; } });

        expect(calls).toHaveLength(3);
        expect(filled).toBe(false);
        expect(document.getElementById("filterBrand").closest(".filter-chip").classList.contains("d-none")).toBe(true);
    });

    it("never asks for more than fifty pages and does not cut the list silently past the cap", async () => {
        const fullPage = Array.from({ length: 100 }, (_, i) => ({ id: `b${i}`, brandCode: `B${i}`, brandName: `Brand ${i}` }));
        const { api, calls } = harness(() => json(200, { data: { items: fullPage, totalCount: 1000000 } }));
        document.body.insertAdjacentHTML("beforeend", '<div class="filter-chip"><select id="filterBrand"></select></div>');
        let filled = false;

        await api.loadBrandFilterOptions({ fillSelect: () => { filled = true; } });

        expect(calls.length).toBeLessThanOrEqual(50);
        expect(filled).toBe(false);
        expect(document.getElementById("filterBrand").closest(".filter-chip").classList.contains("d-none")).toBe(true);
    });

    it("serves every key index.js reads and requires the brand keys", () => {
        const view = fs.readFileSync(path.join(__dirname, "../Views/MasterDataManagement/GlobalProducts/_IndexL10n.cshtml"), "utf8");
        const l10n = fs.readFileSync(path.join(__dirname, "../wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.l10n.js"), "utf8");
        const served = new Set([...view.matchAll(/^\s*([A-Z][A-Za-z]+)\s*=/gm)].map((match) => match[1]));
        const used = [...new Set([...source.matchAll(/\bL\.([A-Z][A-Za-z]+)/g)].map((match) => match[1]))];

        expect(used.filter((key) => !served.has(key))).toEqual([]);
        ["Brand", "BrandNone", "BrandUnchanged", "ChangeBrand", "ChangeBrandConfirmation", "ChangeBrandSuccess"]
            .forEach((key) => {
                expect(served.has(key)).toBe(true);
                expect(l10n).toContain(`'${key}'`);
            });
    });

    it("hides the brand filter when the reader may not read the brand options", async () => {
        const { api } = harness(() => json(403, { errors: ["ErrorForbidden"] }));
        document.body.insertAdjacentHTML("beforeend", '<div class="filter-chip"><select id="filterBrand"></select></div>');
        let filled = false;

        await api.loadBrandFilterOptions({ fillSelect: () => { filled = true; } });

        expect(filled).toBe(false);
        expect(document.getElementById("filterBrand").closest(".filter-chip").classList.contains("d-none")).toBe(true);
    });

    it("shows the brand as 'CODE · Name' in the quick view and in the list, and '—' without one", () => {
        const { api } = harness(() => json(200, {}));
        api.renderDetail(detail(["DETAILS"], { brandId, brandCode: "BRD", brandName: "Brand One" }).data, productId);
        expect(document.getElementById("oc-brand").textContent).toBe("BRD · Brand One");
        api.renderDetail(detail(["DETAILS"]).data, productId);
        expect(document.getElementById("oc-brand").textContent).toBe("—");

        const brandColumn = api.buildListOptions().config.columnDefs.find((def) => def.targets === 4);
        expect(brandColumn.orderable).toBe(false);
        expect(brandColumn.render(null, "display", { brandId, brandCode: "B<1>", brandName: "N" })).toBe("B&lt;1&gt; · N");
    });
});
