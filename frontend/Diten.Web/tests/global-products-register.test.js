const fs = require("fs");
const path = require("path");

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

        expect(view).toContain('@inject Diten.Web.Services.IPermissionSnapshot Permissions');
        expect(view).toContain('Permissions.Has("mdm.global-products.create")');
        expect(view).toContain('data-can-create="@canCreate.ToString().ToLowerInvariant()"');
        expect(source).toContain("const permissionHost = document.querySelector('[data-can-create]')");
        expect(source).toContain("const canCreate = permissionHost?.getAttribute('data-can-create') === 'true'");
        expect(source).toContain('canCreate ? L.AddNew : null');
        expect(l10nScript()).toContain('normalized[toPascalCase(key)] = raw[key]');
    });

    it("uses only the same-origin MVC proxy from browser code", () => {
        const source = indexScript();

        expect(source).toContain("const endpoint = '/MasterDataManagement/GlobalProducts/api'");
        expect(source).not.toMatch(/localhost:5000|:5000\/api|localhost:5059|:5059\/api/);
        expect(source).not.toMatch(/document\.cookie|access_token|Authorization\s*:\s*['\"`]Bearer/);
    });

    it("overlaps the first page with saved-view loading without bypassing saved-view correctness", () => {
        const source = indexScript();
        const prefetchIndex = source.indexOf('initialPagePrefetch = startInitialPagePrefetch()');
        const savedViewIndex = source.indexOf('const savedState = await loadDefaultView()');
        const dataTableIndex = source.indexOf('dt = new DataTable(tableEl, config)');

        expect(prefetchIndex).toBeGreaterThan(-1);
        expect(savedViewIndex).toBeGreaterThan(prefetchIndex);
        expect(dataTableIndex).toBeGreaterThan(savedViewIndex);
        expect(source).toContain('if (prefetched?.query === query)');
        expect(source).toContain('prefetched?.controller.abort()');
        expect(source).toContain('consumePage(buildQuery(data))');
        expect(source).toContain('pageLength: initialPageSize');
        expect(source).toContain('deferRender: true');
    });

    it("keeps the browser create payload to the single user field", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml");
        const source = indexScript();

        expect(view.match(/name="GlobalProductName"/g)).toHaveLength(1);
        expect(view).toContain('<div class="diten-field">');
        expect(view).toContain('<i class="bx bx-text diten-field-icon" aria-hidden="true"></i>');
        expect(view).not.toMatch(/name="(?:TenantId|CanonicalCode|ReservationId|IdempotencyKey)"/);
        expect(source).not.toMatch(/body\.(?:set|append)\(['\"](?:TenantId|CanonicalCode|ReservationId|IdempotencyKey)/);
    });

    it("keeps the lifecycle filter single-valued while matching the Golden clear-chip contract", () => {
        const view = read("Views/MasterDataManagement/GlobalProducts/_Filter.cshtml");
        const source = indexScript();
        const styles = backboneCss();

        expect(view).toContain('id="filterLifecycleStatus"');
        expect(view).not.toContain('id="filterLifecycleStatus" multiple');
        expect(source).toContain("containerCssClass: 'dt-inline-filter-single'");
        expect(source).toContain("selectionCssClass: 'form-select form-select-sm'");
        expect(source).toContain("placeholder: $filter.data('placeholder') || ''");
        expect(source).toContain('minimumResultsForSearch: Infinity');
        expect(source).toContain('allowClear: true');
        expect(source).toContain(".toggleClass('text-body fw-semibold', hasValue)");
        expect(source).toContain(".toggleClass('text-secondary', !hasValue)");
        expect(source).toContain("change.globalProductsFilterState', () => syncSingleFilterState($filter)");
        expect(source).toContain('requestAnimationFrame(() => syncSingleFilterState($filter))');
        expect(styles).not.toContain('.dt-inline-filter-single--is-empty .select2-selection__rendered');
        expect(styles).not.toContain('.dt-inline-filter-single--has-value .select2-selection__rendered');
        expect(source).toContain("appliedFilters = normalizeFilters({ lifecycleStatus: $('#filterLifecycleStatus').val() })");
        expect(source).toContain("query.set('lifecycleStatus', appliedFilters.lifecycleStatus)");
        expect(source).toContain('dt?.ajax.reload()');
    });

    it("omits the forbidden fake import while preserving the real shared Action exports", () => {
        const source = indexScript();
        const defaults = dataTableDefaults();

        expect(source).not.toMatch(/importBtn|bx-import|L\.ComingSoon/);
        expect(source).toContain('const buttons = window.DtDefaults.exportButtons(');
        ['print', 'csv', 'excel', 'pdf', 'copy']
            .forEach((format) => expect(defaults).toContain(`extend: '${format}'`));
        expect(source).toContain('L.CurrentPageOnly');
        expect(source).toContain('button.titleAttr = L.CurrentPageOnly');
    });

    it("orchestrates reservation and draft creation on the MVC server", () => {
        const source = controller();

        expect(source).toContain("ReserveCodeAsync(model.GlobalProductName");
        expect(source).toContain("CreateDraftAsync(");
        expect(source).toContain("/api/global-products/code-reservations");
        expect(source).toContain("/api/global-products/drafts");
        expect(source).toContain("Guid.NewGuid().ToString(\"N\")");
        expect(source).toContain('HasOnlyFormFieldsAsync("GlobalProductName")');
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
        expect(source).toContain("actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT']");
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
        ['Submit', 'Update', 'Withdraw', 'Correction', 'Retirement']
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

    it("keeps optional personalization fallback console-free", () => {
        const source = indexScript();
        expect(source).not.toMatch(/console\.(?:error|warn)\(/);
        expect(source).toContain("catch (error) {\n            return null;");
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
