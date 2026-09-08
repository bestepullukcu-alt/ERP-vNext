const fs = require('fs');
const path = require('path');
const vm = require('node:vm');

describe('GSKU executed browser contracts (isolated DOM and HTTP fixtures)', () => {
    const id = '12345678-1234-1234-1234-123456789012';
    const detail = (actions = ['DETAILS', 'EDIT'], state = 1, version = 3) => ({
        id, gskuVersion: version, revisionVersion: 2, lifecycleStatus: state,
        availableActions: actions, globalProductId: '22345678-1234-1234-1234-123456789012',
        globalProductCanonicalCode: 'GP-TEST', globalProductName: 'Synthetic',
        packQuantity: 1, packUomCode: 'C62'
    });
    const response = (data, status = 200) => ({
        status, ok: status >= 200 && status < 300,
        json: async () => ({ data, isSuccessful: status < 300, statusCode: status })
    });
    function harness(responses = []) {
        document.body.innerHTML = '<form id="formGsku"><input name="__RequestVerificationToken" value="synthetic-csrf">' +
            '<select id="globalProductId"></select><select id="packUomCode"></select><input id="packQuantity" value="1"></form>' +
            '<button id="btnSaveGsku"></button><span id="btnSaveGskuText"></span><span id="offcanvasCreateEditLabel"></span>' +
            '<div id="offcanvasCreateEdit"></div><form id="gskuLifecycleToken"><input name="__RequestVerificationToken" value="synthetic-csrf"></form>';
        const fetch = vi.fn(async () => {
            const next = responses.shift();
            if (next instanceof Error) throw next;
            if (!next) throw new Error('Unexpected HTTP call');
            return next;
        });
        const reload = vi.fn();
        const toast = vi.fn();
        let pending;
        const fakeWindow = {
            L10n: new Proxy({}, { get: (_, key) => String(key) }),
            showToast: toast,
            showConfirm: (_, callback) => { pending = callback('Synthetic reason'); },
            personalizationClient: { saveView: vi.fn(async payload => ({ ...payload, id: 'view-test' })) }
        };
        const jquery = (selector) => {
            const elements = typeof selector === 'string'
                ? [...document.querySelectorAll(selector.replace(':selected', ':checked'))] : selector ? [selector] : [];
            const api = {
                length: elements.length, hasClass: () => false, select2: () => api,
                trigger: () => api, toggleClass: () => api, next: () => api, find: () => api,
                val: (...args) => {
                    if (!args.length) return elements[0]?.value;
                    elements.forEach(el => { el.value = args[0] ?? ''; }); return api;
                },
                text: () => elements[0]?.textContent ?? ''
            };
            return api;
        };
        const source = fs.readFileSync(path.join(__dirname, '../wwwroot/assets/js/MasterDataManagement/Gskus/index.js'), 'utf8');
        // Test-only access to existing closure functions; shipped JS has no test hook.
        const exposed = source.replace('init: async () => {', `test: {
            readAvailableActions, readVersion, normalizeFilters, getResetBaselineState, saveDefaultView,
            openExistingEditor, requestLifecycle, requestRetirement, submitEditor,
            setupEditor: (mode) => { editorId = '${id}'; editorGskuVersion = 3; editorRevisionVersion = 2; editorMode = mode; },
            setupTable: (table) => { dt = table; }
        }, init: async () => {`);
        const api = vm.runInNewContext(exposed + '\nGskusList.test;', {
            window: fakeWindow, document, fetch, $: jquery, Option, FormData, URLSearchParams,
            bootstrap: { Offcanvas: { getOrCreateInstance: () => ({ show: vi.fn(), hide: vi.fn() }) } },
            setTimeout, clearTimeout, console
        });
        api.setupTable({ ajax: { reload } });
        return { api, fetch, reload, toast, window: fakeWindow, settle: () => pending };
    }

    it.each([
        ['DETAILS'], ['DETAILS', 'EDIT', 'SUBMIT'],
        ['DETAILS', 'WITHDRAW_APPROVAL'], ['DETAILS', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT']
    ])('accepts the exact server action sequence %j', (...actions) => {
        expect(harness().api.readAvailableActions({ availableActions: actions })).toEqual(actions);
    });
    it.each([['details'], ['DETAILS', 'edit'], ['DETAILS', 'EDIT', 'EDIT'], ['EDIT', 'DETAILS'], ['DETAILS', 'DELETE']])(
        'rejects action aliases, unknown keys and malformed order %j', (...actions) => {
            expect(() => harness().api.readAvailableActions({ availableActions: actions })).toThrow();
        });
    it.each([null, undefined, '', '3', -1, 0.5])('rejects malformed version %j', value => {
        expect(Number.isNaN(harness().api.readVersion({ gskuVersion: value }, 'gskuVersion', 'GskuVersion'))).toBe(true);
    });
    it.each([['edit', 'EDIT', 1], ['correction', 'REQUEST_CORRECTION', 3]])(
        '%s loads only target-bound verified UoMs, not create parent choices', async (mode, action, state) => {
            const h = harness([response(detail(['DETAILS', action], state)),
                response({ gskuId: id, gskuVersion: 3, revisionVersion: 2,
                    uoms: [{ code: 'C62', displayText: 'Unit', sortOrder: 1, maximumDecimalPrecision: 0 }] })]);
            await h.api.openExistingEditor(id, null, mode);
            expect(h.fetch.mock.calls.map(c => c[0])).toEqual([
                '/MasterDataManagement/Gskus/api/' + id,
                '/MasterDataManagement/Gskus/api/' + id + '/' + mode + '-options']);
            expect(document.getElementById('globalProductId').disabled).toBe(true);
            expect(document.getElementById('globalProductId').options).toHaveLength(1);
            expect(h.toast).not.toHaveBeenCalled();
        });
    it('refuses drifted option versions without promoting a save', async () => {
        const h = harness([response(detail()), response({ gskuId: id, gskuVersion: 4, revisionVersion: 2,
            uoms: [{ code: 'C62', displayText: 'Unit', sortOrder: 1, maximumDecimalPrecision: 0 }] })]);
        await h.api.openExistingEditor(id, null, 'edit');
        expect(h.toast).toHaveBeenCalledWith('LifecycleStateChanged', 'error');
        expect(h.reload).not.toHaveBeenCalled();
    });
    it.each([['SUBMIT', 1, 2], ['WITHDRAW_APPROVAL', 2, 1]])(
        '%s requires source read-back before reload/success', async (action, before, after) => {
            const h = harness([response(detail(['DETAILS', action], before)), response({ gskuId: id, gskuVersion: 4, revisionVersion: 2 }),
                response(detail(['DETAILS'], after, 4))]);
            h.api.requestLifecycle(id, action, null); await h.settle();
            expect(h.fetch).toHaveBeenCalledTimes(3);
            expect(h.fetch.mock.calls[1][1].body.get(action === 'SUBMIT' ? 'ExpectedVersion' : 'ExpectedGskuVersion')).toBe('3');
            expect(h.reload).toHaveBeenCalledWith(null, false);
            expect(h.toast.mock.calls.at(-1)[1]).toBe('success');
        });
    it.each([403, 409, 503, 504])('HTTP %i is not reported as success or reloaded', async status => {
        const h = harness([response(detail(['DETAILS', 'SUBMIT'])), response({}, status)]);
        h.api.requestLifecycle(id, 'SUBMIT', null); await h.settle();
        expect(h.reload).not.toHaveBeenCalled();
        expect(h.toast.mock.calls.at(-1)[1]).toBe('error');
    });
    it('retirement accepts canonical operation GUID and validates admission read-back', async () => {
        const h = harness([response(detail(['DETAILS', 'REQUEST_RETIREMENT'], 3)),
            response({ gskuId: id, operationId: '12345678-1234-9234-1234-123456789012', gskuVersion: 4, checkpoint: 'AwaitingDecision' }, 202),
            response(detail(['DETAILS'], 3, 4))]);
        h.api.requestRetirement(id, null); await h.settle();
        expect(h.fetch.mock.calls[1][0]).toBe('/MasterDataManagement/Gskus/api/' + id + '/retirement-requests');
        expect(h.reload).toHaveBeenCalled();
        expect(h.toast).toHaveBeenCalledWith('RetirementRequestedSuccess', 'warning');
    });
    it('Save View persists normalized applied lifecycle filters and reset stays empty', async () => {
        const h = harness();
        expect(h.api.normalizeFilters({ lifecycleStatus: 'Draft' })).toEqual({ lifecycleStatus: 'Draft' });
        expect(h.api.normalizeFilters({ lifecycleStatus: 'Unknown' })).toEqual({ lifecycleStatus: '' });
        expect(h.api.getResetBaselineState().filters).toEqual({ lifecycleStatus: '' });
        await h.api.saveDefaultView({ filters: { lifecycleStatus: 'Draft' } });
        const payload = h.window.personalizationClient.saveView.mock.calls[0][0];
        expect(payload.moduleKey).toBe('MasterDataManagement');
        expect(payload.pageKey).toBe('Gskus');
        expect(JSON.stringify(payload)).toContain('Draft');
    });

    it('correction admission increments version and is reported pending only after the action fence read-back', async () => {
        const base = detail(['DETAILS', 'REQUEST_CORRECTION'], 3);
        const h = harness([response(base),
            response({ gskuId: id, gskuVersion: 3, revisionVersion: 2,
                uoms: [{ code: 'C62', displayText: 'Unit', sortOrder: 1, maximumDecimalPrecision: 0 }] }),
            response(base), response({ gskuId: id, operationId: id, gskuVersion: 4, checkpoint: 'AwaitingDecision' }, 202),
            response(detail(['DETAILS'], 3, 4))]);
        await h.api.openExistingEditor(id, null, 'correction');
        document.getElementById('packQuantity').value = '2';
        h.api.submitEditor(); await h.settle();
        expect(h.reload).toHaveBeenCalled();
        expect(h.toast).toHaveBeenCalledWith('CorrectionPending', 'warning');
    });
    it('draft update reads back quantity, state and exact version before success', async () => {
        const h = harness([response(detail()),
            response({ gskuId: id, gskuVersion: 3, revisionVersion: 2,
                uoms: [{ code: 'C62', displayText: 'Unit', sortOrder: 1, maximumDecimalPrecision: 0 }] }),
            response({ gskuId: id, version: 4 }),
            response({ ...detail(['DETAILS', 'EDIT'], 1, 4), packQuantity: 2 })]);
        await h.api.openExistingEditor(id, null, 'edit');
        document.getElementById('packQuantity').value = '2';
        h.api.submitEditor(); await h.settle();
        expect(h.fetch.mock.calls[2][1].method).toBe('PUT');
        expect(h.reload).toHaveBeenCalled();
        expect(h.toast).toHaveBeenCalledWith('UpdateSuccess', 'success');
    });
    it('wrong target in a successful response cannot produce lifecycle success', async () => {
        const h = harness([response(detail(['DETAILS', 'SUBMIT'])), response({ gskuId: 'other' })]);
        h.api.requestLifecycle(id, 'SUBMIT', null); await h.settle();
        expect(h.reload).not.toHaveBeenCalled();
        expect(h.toast.mock.calls.at(-1)[1]).toBe('error');
    });
});

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
        expect(index).toMatch(/@if \(canUseEditor\)[\s\S]*_CreateEditOffcanvas\.cshtml/);
        expect(controller()).toContain('ViewData["CanUseGskuEditor"] = canCreate');
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
        expect(source).toContain('[HttpPut("api/{id:guid}")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/identity-approval/withdraw")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/correction-requests")]');
        expect(source).toContain('[HttpPost("api/{id:guid}/retirement-requests")]');
        expect(source).not.toContain('[HttpPost("api/{id:guid}/retire")]');
        expect(source).toContain('[HttpGet("api/{id:guid}/edit-options")]');
        expect(source).toContain('[HttpGet("api/{id:guid}/correction-options")]');
        expect(source).toContain('/api/gskus/drafts');
        expect(source).toContain('/api/gskus/{id:D}/identity-approval/withdraw');
        expect(source).toContain('/api/gskus/{id:D}/correction-requests');
        expect(source).toContain('/api/gskus/{id:D}/retirement-requests');
        expect(source).not.toMatch(/HttpPatch|HttpDelete|code-reservations|\/bulk/);
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

    it('renders only exact server-owned actions and keeps unavailable mutations fail closed', () => {
        const browser = script();
        const view = read('Views/MasterDataManagement/Gskus/Index.cshtml');
        const models = read('Models/Gskus/GskuViewModels.cs');

        expect(models).toContain('IReadOnlyList<string> AvailableActions');
        expect(browser).toContain("const actionOrder = ['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT']");
        expect(browser).toContain("const supportedActions = new Set(['DETAILS', 'EDIT', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_CORRECTION', 'REQUEST_RETIREMENT'])");
        expect(browser).toContain('const readAvailableActions = (detail) =>');
        expect(browser).toContain("actions[0] !== 'DETAILS'");
        expect(browser).toContain('const actions = readAvailableActions(await fetchDetail(id))');
        expect(view).not.toMatch(/data-can-submit|data-can-retire/);
        expect(browser).toContain("EDIT: ['js-edit-draft', 'bx bx-edit'");
        expect(browser).toContain("WITHDRAW_APPROVAL: ['js-lifecycle-action', 'bx bx-undo'");
        expect(browser).toContain("REQUEST_CORRECTION: ['js-request-correction', 'bx bx-revision'");
        expect(browser).toContain("REQUEST_RETIREMENT: ['js-request-retirement', 'bx bx-archive'");
        expect(browser).toContain("if (!id || !['SUBMIT', 'WITHDRAW_APPROVAL'].includes(action)) return");
        expect(browser).not.toMatch(/canSubmit|canRetire|js-retire-identity/);
        expect(browser).not.toContain("`${endpoint}/${encodeURIComponent(id)}/retire`");
    });

    it('uses fresh pair versions for exact draft edit and maker withdrawal contracts', () => {
        const source = controller();
        const browser = script();
        const models = read('Models/Gskus/GskuViewModels.cs');

        expect(models).toContain('public int RevisionVersion { get; set; }');
        expect(models).toContain('public int GskuVersion { get; set; }');
        expect(source).toContain('private const string UpdatePermission = "mdm.gskus.update"');
        expect(source).toContain('private const string WithdrawPermission = "mdm.gskus.withdraw"');
        expect(source).toContain('HasOnlyFormFieldsAsync("ExpectedVersion", "PackQuantity", "PackUomCode")');
        expect(source).toContain('HasOnlyFormFieldsAsync("ExpectedGskuVersion", "ReasonCode", "Comment")');
        expect(source).toContain('HttpMethod.Put');
        expect(browser).toContain("readVersion(detail, 'gskuVersion', 'GskuVersion')");
        expect(browser).toContain("body.set('ExpectedVersion', String(editorGskuVersion))");
        expect(browser).toContain("body.set('ExpectedGskuVersion', String(gskuVersion))");
        expect(browser).not.toContain("valueOf(detail, 'version', 'Version')");
    });

    it('derives a stable D-GUID on the server and forwards it only as a header', () => {
        const source = controller();
        const browser = script();

        expect(source).toContain('AppendLengthPrefixed(hash, tenantId.ToString("D"))');
        expect(source).toContain('AppendLengthPrefixed(hash, actor)');
        expect(source.match(/\[FromForm\] int\? expectedVersion/g)?.length).toBe(2);
        expect(source.match(/\[FromForm\] int\? expectedGskuVersion/g)?.length).toBe(3);
        expect(source).toContain('expectedVersion is null or < 0');
        expect(source).toContain('expectedVersion.Value');
        expect(source).toContain('AppendLengthPrefixed(hash, aggregateType)');
        expect(source).toContain('AppendLengthPrefixed(hash, action)');
        expect(source).toContain('AppendLengthPrefixed(hash, reasonCode)');
        expect(source).toContain('operationId.ToString("D")');
        expect(source).toContain('form.Count == allowed.Count');
        expect(source).toContain('antiforgery.Count == 1');
        expect(source).toContain('required.All(field => form.TryGetValue(field, out var values) && values.Count == 1)');
        expect(browser).toContain("body.set('ExpectedVersion', String(editorGskuVersion))");
        expect(browser).toContain("body.set('ExpectedGskuVersion', String(gskuVersion))");
        expect(browser).toContain("body.set('ReasonCode', 'REQUESTER_WITHDRAWAL')");
        expect(browser).toContain("body.set('Comment', '')");
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

    it('uses WorkCenter-style icon-bearing create structure without exposing direct retirement', () => {
        const source = script();
        const view = read('Views/MasterDataManagement/Gskus/_CreateEditOffcanvas.cshtml');
        ['bx-package', 'bx-calculator', 'bx-ruler', 'bx-save', 'bx-x']
            .forEach((icon) => expect(view).toContain(icon));
        expect(view).toContain('id="btnSaveGskuText"');
        expect(source).toContain("document.getElementById('offcanvasCreateEditLabel').textContent = L.FormTitleCreate");
        expect(source).not.toMatch(/RetireConfirmation|RetireSuccess|js-retire-identity/);
        expect(source).toContain("const openEdit = (id, actionButton) => openExistingEditor(id, actionButton, 'edit')");
        expect(source).toContain("method: editorMode === 'edit' ? 'PUT' : 'POST'");
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
        expect(source).toContain('editorId ? L.UpdateSuccess : L.CreateSuccessWithIdentifiers');
        expect(source).toContain(".replace('{0}', code)");
        expect(source).toContain(".replace('{1}', revision)");
        const pendingBlock = source.match(/if \(response\.status === 202 \|\| payload\?\.success === false\)[\s\S]*?\n\s*}/)?.[0] || '';
        expect(pendingBlock).not.toMatch(/hide\(\)|form\.reset|ajax\.reload|tokenInput\.value/);
    });

    it('requests correction only from a fresh server-owned action and pair-version snapshot', () => {
        const source = script();
        const proxy = controller();
        expect(proxy).toContain('private const string RequestCorrectionPermission = "mdm.gskus.request-correction"');
        expect(proxy).toContain('HasOnlyFormFieldsAsync("ExpectedGskuVersion", "PackQuantity", "PackUomCode")');
        expect(proxy).toContain('"request-correction"');
        expect(source).toContain("const openCorrection = (id, actionButton) => openExistingEditor(id, actionButton, 'correction')");
        expect(source).toContain("readAvailableActions(fresh).includes('REQUEST_CORRECTION')");
        expect(source).toContain("readVersion(fresh, 'revisionVersion', 'RevisionVersion')");
        expect(source).toContain("body.set('ExpectedGskuVersion', String(editorGskuVersion))");
        expect(source).toContain("`${endpoint}/${encodeURIComponent(editorId)}/correction-requests`");
        expect(source).toContain("response.status === 202 && checkpoint !== 'AwaitingDecision'");
        expect(source).toContain("response.status === 200 && checkpoint !== 'Completed'");
        expect(source).toContain("refreshedActions.includes('REQUEST_CORRECTION')");
        expect(source).toContain("REQUEST_RETIREMENT: ['js-request-retirement'");
    });

    it('requests retirement through the exact same-origin durable contract and fresh read-back', () => {
        const source = script();
        const proxy = controller();
        expect(proxy).toContain('private const string RequestRetirementPermission = "mdm.gskus.request-retirement"');
        expect(proxy).toContain('HasOnlyFormFieldsAsync("ExpectedGskuVersion", "RequestReason")');
        expect(proxy).toContain('requestReason.Length > 128');
        expect(proxy).toContain('new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted }');
        expect(source).toContain("readAvailableActions(detail).includes('REQUEST_RETIREMENT')");
        expect(source).toContain("body.set('ExpectedGskuVersion', String(gskuVersion))");
        expect(source).toContain("body.set('RequestReason', requestReason)");
        expect(source).toContain("`${endpoint}/${encodeURIComponent(id)}/retirement-requests`");
        expect(source).toContain("response.status === 202 && checkpoint !== 'AwaitingDecision'");
        expect(source).toContain("response.status === 200 && checkpoint !== 'Completed'");
        expect(source).toContain("inputAttributes: { maxlength: 128 }");
        expect(source).toContain("refreshedActions.includes('REQUEST_RETIREMENT')");
        expect(source).not.toContain("`${endpoint}/${encodeURIComponent(id)}/retire`");
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
        expect(source).toContain('class="dropdown-item dt-action-item ${className}"');
        expect(source).toContain("filters: normalizeFilters(appliedFilters)");
        expect(source).toContain("query.set('lifecycleStatus', appliedFilters.lifecycleStatus)");
        expect(source).toContain('if (appliedFilters.lifecycleStatus)');
        expect(source).not.toContain('lifecycleFilterReady');
        expect(source).toContain("container.toggleClass('filter-selected', selected)");
        expect(source).not.toMatch(/localStorage|sessionStorage|delete-record|js-edit-item|bulk-delete|bulkAction|row-checkbox/);
        expect(table).not.toMatch(/type="checkbox"|select-all|dt-checkboxes/);
    });

    it('persists and resets the exact lifecycle filter in Save View', () => {
        const source = script();
        const filter = read('Views/MasterDataManagement/Gskus/_Filter.cshtml');
        expect(filter).toContain('id="filterLifecycleStatus"');
        ['Draft', 'PendingIdentityApproval', 'IdentityApproved', 'Retired']
            .forEach((state) => expect(filter).toContain(`value="${state}"`));
        expect(source).toContain("let appliedFilters = { lifecycleStatus: '' }");
        expect(source).toContain('appliedFilters = normalizeFilters(normalized.filters)');
        expect(source).toContain('applySavedTableState(dt, getResetBaselineState())');
        expect(source).toContain('getAppliedFilterCount()');
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
        ['SubmitIdentity', 'SubmitPendingSuccess', 'ErrorTimeout']
            .forEach((key) => expect(keys[0]).toContain(key));
        ['FormTitleEdit', 'EditDraft', 'UpdateDraft', 'UpdateConfirmation', 'UpdateSuccess',
            'WithdrawApproval', 'WithdrawConfirmation', 'WithdrawPending', 'WithdrawSuccess',
            'FormTitleCorrection', 'RequestCorrection', 'CorrectionConfirmation', 'CorrectionPending', 'CorrectionCompleted',
            'RequestRetirement', 'RetirementRequestConfirmation', 'RetirementRequestReasonLabel',
            'RetirementRequestReasonRequired', 'RetirementRequestReasonInvalid', 'RetirementRequestedSuccess']
            .forEach((key) => expect(keys[0]).toContain(key));
    });
});
