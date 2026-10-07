const fs = require('fs');
const path = require('path');
const vm = require('node:vm');

describe('MOD-0290 Finished Good Draft Foundation', () => {
    const root = path.join(__dirname, '..');
    const repo = path.join(root, '..', '..');
    const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8');
    const readRepo = (relativePath) => fs.readFileSync(path.join(repo, relativePath), 'utf8');
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
        // FG FIX1 item 5: the view reads the controller's decision and the same claim reader (no UX snapshot).
        expect(index).toContain('ViewData["CanCreateFinishedGood"] as bool? == true');
        expect(index).toContain('Diten.Web.Security.PermissionClaims.HasPermission(User, "mdm.finished-goods.read")');
        expect(index).not.toContain('Permissions.Has(');
        expect(index).not.toContain('IPermissionSnapshot');
        expect(index).toContain('data-can-create="@canCreate.ToString().ToLowerInvariant()"');
        expect(controller()).toContain('PermissionClaims.HasPermission(User, permission)');
        expect(controller()).toContain('ViewData["CanCreateFinishedGood"] = canCreate');
        expect(source).toContain("const canCreate = document.querySelector('[data-can-create]')");
        expect(source).toContain("addNewText: canCreate ? L.AddNew : ''");
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

    it('keeps the browser create payload to one business field plus the server-drawn form attempt', () => {
        const view = read('Views/MasterDataManagement/FinishedGoods/_CreateEditOffcanvas.cshtml');
        const source = script();
        expect(view.match(/name="GskuId"/g)).toHaveLength(1);
        expect(view).toContain('name="FormAttemptToken" value="@ViewData["FinishedGoodFormAttemptToken"]"');
        expect(view).not.toMatch(/name="(?:TenantId|CanonicalCode|CodeReservationId|IdempotencyKey|LskuId|StewardLabel)"/);
        expect(source).toContain("body.set('GskuId', String(gskuId))");
        expect(source).toContain("body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '')");
        expect(source).not.toMatch(/body\.(?:set|append)\(['"](?:TenantId|CanonicalCode|CodeReservationId|IdempotencyKey|LskuId|StewardLabel)/);
        // The browser never makes a key: no UUID, no random source.
        expect(source).not.toMatch(/crypto\.|randomUUID|getRandomValues|uuid|Guid\.NewGuid|Math\.random/);
    });

    it('takes the next form attempt only from the server answer', () => {
        const source = script();
        expect(source).toContain('const nextToken = payload?.formAttemptToken || payload?.FormAttemptToken');
        expect(source).toContain('if (nextToken && tokenInput) tokenInput.value = nextToken');
    });

    it('derives the MDM creation key from a protected, subject-bound form attempt (never a fresh key per request)', () => {
        const source = controller();
        expect(source).toContain('"FinishedGoodFormAttempt"');
        expect(source).toMatch(/CreateProtector\("Diten\.Web", "MOD-0290", "FinishedGoodFormAttempt", "v1"\)/);
        expect(source).toContain('.ToTimeLimitedDataProtector()');
        expect(source).toContain('TryReadFormAttempt(formAttemptToken, out var attempt, out var attemptExpiresAt)');
        expect(source).toContain('var operationKey = attempt.OperationKey;');
        expect(source).toContain('idempotencyKey = operationKey');
        expect(source).toContain('_localizer["ErrorInvalidFormAttempt"]');
        expect(source).not.toContain('Guid.NewGuid().ToString("N")');
        expect(source).toContain('/api/finished-goods/drafts');
        expect(source).not.toMatch(/code-reservations|ReserveCode|ReservationId/);
        expect(source).not.toContain('catch (Exception');
        expect(source).not.toContain('catch (OperationCanceledException');
    });

    it('enforces anti-forgery and maps a timeout to 504 and a transport failure to 503 (never 502 for create)', () => {
        const source = controller();
        const browser = script();
        expect(source).toContain('[ValidateAntiForgeryToken]');
        expect(browser).toContain("input[name=\"__RequestVerificationToken\"]");
        expect(browser).toContain("'RequestVerificationToken': token");
        expect(source).toContain('catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)');
        expect(source).toContain('StatusCodes.Status504GatewayTimeout');
        expect(source).toContain('_localizer["ErrorTimeout"]');
        expect(source).toContain('catch (HttpRequestException exception)');
        expect(source).toContain('StatusCodes.Status503ServiceUnavailable');
        expect(source).not.toMatch(/Status502BadGateway|StatusCode\(\s*502/);
        expect(source).not.toMatch(/catch\s*\(\s*(?:Exception|OperationCanceledException)/);
        expect(browser).toMatch(/400:\s*L\.ErrorValidation/);
        expect(browser).toMatch(/401:\s*L\.ErrorUnauthorized/);
        expect(browser).toMatch(/403:\s*L\.ErrorForbidden/);
        expect(browser).toMatch(/404:\s*L\.ErrorNotFound/);
        expect(browser).toMatch(/409:\s*L\.ErrorConflict/);
        expect(browser).toMatch(/500:\s*L\.ErrorGateway/);
        expect(browser).toMatch(/504:\s*L\.ErrorTimeout/);
    });

    it('exposes only list, detail, selector and create MVC proxy routes', () => {
        const source = controller();
        const routes = [...source.matchAll(/\[(Http(?:Get|Post|Put|Patch|Delete))(?:\("([^"]*)"\))?\]/g)]
            .map((match) => `${match[1]} ${match[2] ?? ''}`);
        expect(source).toContain('[Route("MasterDataManagement/FinishedGoods")]');
        expect(routes).toEqual(['HttpGet ', 'HttpGet api', 'HttpGet api/{id:guid}', 'HttpGet api/gsku-selector', 'HttpPost api']);
        expect(source).not.toMatch(/HttpPut|HttpPatch|HttpDelete|bulk|lifecycle/i);
    });

    it('uses only the bounded search contract and exact selector path', () => {
        const source = script();
        expect(source).toContain('pageLength: 20');
        expect(source).toContain("query.set('search', search)");
        expect(source).toContain("pageSize: '20'");
        expect(source).toContain('`${endpoint}/gsku-selector?${query}`');
        expect(source).not.toMatch(/query\.set\(['"](?:lifecycleStatus|marketCode|legalEntityId|orderBy|orderDir)/);
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

    it('stands on the list shell: server mode, no selection, localized headers, stable table id', () => {
        const index = read('Views/MasterDataManagement/FinishedGoods/Index.cshtml');
        const table = read('Views/MasterDataManagement/FinishedGoods/_DataTable.cshtml');
        expect(index).toContain('Layout = "_LayoutTenantShell"');
        expect(table).toContain('<partial name="~/Views/Shared/Components/DataTable/_ListShell.cshtml" model="listShell" />');
        expect(table).toContain('TableId = "dt-finishedgoods"');
        expect(table).toContain('DataMode = "server"');
        expect(table).toContain('Slug = "finishedgoods"');
        expect(table).toContain('HasSelection = false');
        ['CanonicalCode', 'GskuCanonicalCode', 'LifecycleStatus', 'CreatedAt'].forEach((key) =>
            expect(table).toContain(`Localizer["${key}"].Value`));
        expect(table).toContain('ActionsHeader = Localizer["Actions"].Value');
        expect(table).not.toMatch(/type="checkbox"|select-all|dt-checkboxes|skeleton-loader|<table/);
        expect(script()).toContain("document.querySelector('.datatables-finishedgoods')");
        expect(script()).not.toMatch(/localStorage|sessionStorage|new DataTable\(/);
    });

    it('offers only read-only quick view and no edit, delete, bulk or selection control', () => {
        const source = script();
        expect(source).toContain("className: 'js-quick-view'");
        expect(source).not.toMatch(/delete-record|js-edit-item|bulk-delete|bulkAction|row-checkbox/);
    });

    it('ships the same localization key set for all seven locales', () => {
        const locales = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
        const keys = locales.map((locale) => {
            const xml = read(`Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.${locale}.resx`);
            return [...xml.matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]).sort();
        });
        keys.slice(1).forEach((keySet) => expect(keySet).toEqual(keys[0]));
        ['CreatePending', 'GskuNotReferenceable', 'QuickView', 'Search', 'Export', 'ColumnVisibility', 'Status',
            'CurrentPageOnly', 'ErrorInvalidFormAttempt', 'ErrorTimeout', 'LifecycleCancelled', 'CancellationPendingAudit']
            .forEach((key) => expect(keys[0]).toContain(key));
    });

    describe('executed browser contracts (isolated DOM, no network)', () => {
        function harness(responses = [], extra = {}) {
            document.body.innerHTML =
                '<div id="offcanvasDetailsPreview"></div><h5 id="oc-title"></h5><small id="oc-subtitle"></small>' +
                '<div id="oc-id"></div><div id="oc-code"></div><div id="oc-gsku-code"></div>' +
                '<span id="oc-status" class="badge bg-label-secondary">-</span><div id="oc-version"></div>' +
                '<div id="oc-created-at"></div><div id="oc-updated-at"></div><input id="filterCanonicalCode" value="">';
            const fetch = vi.fn(async () => {
                const next = responses.shift();
                if (!next) throw new Error('Unexpected HTTP call');
                return next;
            });
            const toast = vi.fn();
            const warn = vi.fn();
            const fakeWindow = {
                L10n: new Proxy({}, { get: (_, key) => (typeof key === 'string' ? `L:${key}` : undefined) }),
                showToast: toast,
                DitenDataTable: { renderActions: (actions) => JSON.stringify(actions) },
                ...(extra.window || {})
            };
            const source = script();
            // Test-only access to existing closure functions; shipped JS has no test hook.
            const exposed = source.replace('init: async () => {', `test: {
                buildListOptions, refusalKeys, failureSentence, getErrorMessage, onListError, lifecycleMap,
                lifecyclePresentation, renderLifecycle, toMdmPageUrl, adaptPage, clearUnservableOrder,
                labelExportsCurrentPageOnly, populateDetails, submitCreate, adoptFormAttempt
            }, init: async () => {`);
            expect(exposed).not.toBe(source);
            const api = vm.runInNewContext(`${exposed}\nFinishedGoodsList.test;`, {
                window: fakeWindow, document, fetch, $: extra.$ || (() => ({})), FormData, URLSearchParams, Intl, Date,
                bootstrap: { Offcanvas: { getOrCreateInstance: () => ({ show: vi.fn(), hide: vi.fn() }) } },
                setTimeout, clearTimeout, console: { ...console, warn }
            });
            return { api, fetch, toast, warn };
        }
        const json = (body, status) => ({ status, ok: status >= 200 && status < 300, json: async () => body });

        it('hands the list component server mode, the saved-view keys and no sortable column', () => {
            const options = harness().api.buildListOptions();
            expect(options.dataMode).toBe('server');
            expect(options.ajax.url).toBe('/MasterDataManagement/FinishedGoods/api');
            expect(options.export).toBeUndefined();
            expect(options.bulk).toBeUndefined();
            expect(options.savedView).toMatchObject({
                moduleKey: 'MasterDataManagement', pageKey: 'FinishedGoods', saveViewColumnIndexes: [1, 2, 3, 4]
            });
            expect(options.toolbar.exportColumns).toEqual([1, 2, 3, 4]);
            expect(options.filters).toMatchObject({ hostId: 'inlineFilterHost', applyBtn: 'btnFilterApply', resetBtn: 'btnFilterReset' });
            expect(options.config.columns.map((column) => column.data))
                .toEqual(['id', 'canonicalCode', 'gskuCanonicalCode', 'lifecycleStatus', 'createdAt', null]);
            const columnCount = options.config.columns.length;
            const sortable = options.config.columns.filter((_, index) => !options.config.columnDefs.some((def) =>
                (def.targets === index || (def.targets === -1 && index === columnCount - 1)) && def.orderable === false));
            expect(sortable).toEqual([]);
            expect(Object.keys(options.actions.onRowAction)).toEqual(['quickView']);
            expect(typeof options.onResponse).toBe('function');
        });

        it('rewrites the factory wire to MDM paging (pageNumber/pageSize/search, draw kept, sort dropped)', () => {
            const { api } = harness();
            const url = api.toMdmPageUrl('/MasterDataManagement/FinishedGoods/api?start=40&length=20&search=%20FG-01%20&orderBy=canonicalCode&orderDir=desc&draw=3&_=99');
            const query = new URLSearchParams(url.split('?')[1]);
            expect(url.startsWith('/MasterDataManagement/FinishedGoods/api?')).toBe(true);
            expect(Object.fromEntries(query)).toEqual({ pageNumber: '3', pageSize: '20', search: 'FG-01', draw: '3', _: '99' });
            const capped = new URLSearchParams(api.toMdmPageUrl('/x?start=0&length=-1&draw=1').split('?')[1]);
            expect(capped.get('pageSize')).toBe('20');
            const big = new URLSearchParams(api.toMdmPageUrl('/x?start=99999999999&length=1000&draw=1').split('?')[1]);
            expect(big.get('pageSize')).toBe('100');
            expect(big.get('pageNumber')).toBe('1000000');
            // DtDefaults' 401 retry sends the rewritten settings again: the page is not reset to 1.
            expect(api.toMdmPageUrl(url)).toBe(url);
            // beforeSend writes it onto jQuery's settings.
            const settings = { url: '/MasterDataManagement/FinishedGoods/api?start=20&length=20&draw=2' };
            api.buildListOptions().ajax.beforeSend({}, settings);
            expect(settings.url).toBe('/MasterDataManagement/FinishedGoods/api?pageNumber=2&pageSize=20&draw=2');
        });

        it('gives MDM\'s page the component\'s counts', () => {
            const { api } = harness();
            const envelope = { data: { items: [], pageNumber: 1, pageSize: 20, totalCount: 57 } };
            api.adaptPage(envelope);
            expect(envelope.data.total).toBe(57);
            expect(envelope.data.filteredTotal).toBe(57);
            expect(() => api.adaptPage({ errors: ['X'] })).not.toThrow();
        });

        it('puts a saved sort MDM cannot honour back to the base order on load, without a request', () => {
            const { api } = harness();
            const table = (order) => {
                let current = order;
                const setter = vi.fn((value) => { current = value; });
                return { setter, order: (...args) => (args.length ? setter(...args) : current), current: () => current };
            };
            const saved = table([[3, 'desc']]);
            api.clearUnservableOrder(saved);
            expect(saved.setter).toHaveBeenCalledWith([[1, 'asc']]);
            const flat = table([2, 'asc']);
            api.clearUnservableOrder(flat);
            expect(flat.current()).toEqual([[1, 'asc']]);
            const base = table([[1, 'asc']]);
            api.clearUnservableOrder(base);
            expect(base.setter).not.toHaveBeenCalled();
            // The page's initComplete (chained after the factory applied the saved view) runs it.
            expect(api.buildListOptions().config.initComplete.toString()).toContain('clearUnservableOrder(api)');
        });

        it('labels every export entry as current page only', () => {
            const { api } = harness();
            const options = api.buildListOptions();
            const make = (className, icon) => {
                const node = document.createElement('a');
                node.className = className;
                node.innerHTML = `<span><i class="icon-base bx ${icon}"></i>Entry</span>`;
                return node;
            };
            const nodes = [make('dropdown-item buttons-csv', 'bx-file'), make('dropdown-item dt-controlled-copy', 'bx-printer'),
                make('dropdown-item buttons-copy', 'bx-copy'), make('btn btn-icon dt-filter-btn', 'bx-filter-alt')];
            const tableApi = { buttons: () => ({ nodes: () => nodes }), order: () => [[1, 'asc']], search: () => '' };
            options.config.initComplete.call({ api: () => tableApi });
            api.labelExportsCurrentPageOnly(tableApi);
            nodes.slice(0, 3).forEach((node) => {
                expect(node.querySelectorAll('.dt-current-page-only')).toHaveLength(1);
                expect(node.querySelector('.dt-current-page-only').textContent).toBe('L:CurrentPageOnly');
                expect(node.getAttribute('title')).toBe('L:CurrentPageOnly');
            });
            expect(nodes[3].querySelector('.dt-current-page-only')).toBeNull();
        });

        it('never falls back to the raw lifecycle value', () => {
            const { api } = harness();
            expect(api.renderLifecycle('Draft', {})).toContain('L:LifecycleDraft');
            expect(api.renderLifecycle(3, {})).toContain('L:LifecycleIdentityApproved');
            ['Archived', 99, null, undefined, '<b>x</b>'].forEach((value) => {
                const html = api.renderLifecycle(value, {});
                expect(html).toContain('L:Unknown');
                if (value !== null && value !== undefined) expect(html).not.toContain(String(value));
            });
            expect(script()).not.toMatch(/title:\s*value\s*\|\|/);
        });

        it.each([
            [2, 'L:LifecycleCancelled', 'bg-label-dark'], ['Cancelled', 'L:LifecycleCancelled', 'bg-label-dark'],
            [1, 'L:CancellationPendingAudit', 'bg-label-warning'], ['PendingAudit', 'L:CancellationPendingAudit', 'bg-label-warning'],
            [0, 'L:LifecycleDraft', 'bg-label-secondary'], ['None', 'L:LifecycleDraft', 'bg-label-secondary']
        ])('m9: cancellation disposition %j shows %s ahead of Draft', (disposition, title, cssClass) => {
            const { api } = harness();
            const html = api.renderLifecycle(1, { cancellationDisposition: disposition });
            expect(html).toContain(title);
            expect(html).toContain(cssClass);
        });

        it('m9: the quick view badge shows the cancellation (classes only)', async () => {
            const { api, toast } = harness([json({ data: {
                id: 'f1', canonicalCode: 'FG-1', gskuCanonicalCode: 'GS-1', lifecycleStatus: 1, version: 2,
                createdAt: '2026-10-01T10:00:00Z', cancellationDisposition: 2
            } }, 200)]);
            await api.populateDetails('f1');
            const badge = document.getElementById('oc-status');
            expect(toast).not.toHaveBeenCalled();
            expect(badge.textContent).toBe('L:LifecycleCancelled');
            expect(badge.className).toBe('badge bg-label-dark');
            expect(badge.getAttribute('style')).toBeNull();
        });

        it('maps every refusal to a sentence and never shows a code', async () => {
            const { api, warn } = harness();
            Object.entries(api.refusalKeys).forEach(([code, key]) => {
                expect(api.failureSentence({ errors: [code] }, 409)).toBe(`L:${key}`);
            });
            expect(api.failureSentence({ errors: ['SOMETHING_NEW_FROM_MDM'] }, 409)).toBe('L:ErrorConflict');
            expect(warn).toHaveBeenCalledWith('FinishedGoods: a refusal without a sentence mapping', 409);
            expect(api.failureSentence({ errors: ['SOMETHING_NEW_FROM_MDM'] }, 503)).toBe('L:ErrorGateway');
            expect(api.failureSentence({}, 504)).toBe('L:ErrorTimeout');
            expect(api.failureSentence({ errors: [] }, 418)).toBe('L:ErrorGateway');
            // A sentence the proxy already wrote is kept.
            const sentence = 'This form has expired or belongs to another session. Reload the page and try again.';
            expect(api.failureSentence({ errors: [sentence] }, 400)).toBe(sentence);
            expect(await api.getErrorMessage(json({ errors: ['GSKU_NOT_REFERENCEABLE'] }, 404))).toBe('L:GskuNotReferenceable');
            expect(await api.getErrorMessage({ status: 504, json: async () => { throw new Error('not json'); } })).toBe('L:ErrorTimeout');
        });

        it('item 5: a definite refusal adopts the new form attempt, so the retry sends a new key; 503 keeps it', async () => {
            document.body.innerHTML = '';
            let confirmed;
            const selected = { val: () => 'g-1', text: () => 'GS-1', trigger: () => selected };
            const { api, fetch, toast } = harness([
                json({ success: false, errors: ['CODE_RESERVATION_BURNED'], formAttemptToken: 'attempt-2' }, 409),
                json({ success: false, errors: ['ErrorGateway'], formAttemptToken: 'attempt-2' }, 503),
                json({ success: false, errors: ['IDEMPOTENCY_KEY_CONFLICT'], formAttemptToken: 'attempt-3' }, 409)
            ], {
                $: () => selected,
                window: { showConfirm: (message, callback) => { confirmed = callback; } }
            });
            document.body.insertAdjacentHTML('beforeend',
                '<form id="formFinishedGood"><input type="hidden" name="__RequestVerificationToken" value="csrf">' +
                '<input type="hidden" id="formAttemptToken" value="attempt-1"></form>' +
                '<button id="btnSaveFinishedGood"></button>');
            const sent = () => fetch.mock.calls.map(([, init]) => init.body.get('FormAttemptToken'));
            const submit = async () => { api.submitCreate(); await confirmed(); };

            await submit();
            expect(document.getElementById('formAttemptToken').value).toBe('attempt-2');
            expect(toast).toHaveBeenLastCalledWith('L:RefusalCodeNotIssued', 'error');
            await submit();
            expect(document.getElementById('formAttemptToken').value).toBe('attempt-2');
            expect(toast).toHaveBeenLastCalledWith('ErrorGateway', 'error');
            await submit();
            expect(toast).toHaveBeenLastCalledWith('L:RefusalIdempotencyKeyConflict', 'error');
            // The retry after the refusal carried the new attempt; the retry after 503 carried the same one again.
            expect(sent()).toEqual(['attempt-1', 'attempt-2', 'attempt-2']);
            expect(document.getElementById('formAttemptToken').value).toBe('attempt-3');
            // An answer without an attempt (the proxy's own expired-form refusal) leaves the current one in place.
            api.adoptFormAttempt({ errors: ['ErrorInvalidFormAttempt'] });
            expect(document.getElementById('formAttemptToken').value).toBe('attempt-3');
        });

        // FG FIX2 K3 + K1 — executed through submitCreate, as item 5's test.
        function createHarness(responses, startToken = 'attempt-1') {
            document.body.innerHTML = '';
            let confirmed;
            const selected = { val: () => 'g-1', text: () => 'GS-1', trigger: () => selected };
            const run = harness(responses, {
                $: () => selected,
                window: { showConfirm: (message, callback) => { confirmed = callback; } }
            });
            document.body.insertAdjacentHTML('beforeend',
                '<form id="formFinishedGood"><input type="hidden" name="__RequestVerificationToken" value="csrf">' +
                `<input type="hidden" id="formAttemptToken" value="${startToken}"></form>` +
                '<button id="btnSaveFinishedGood"></button>');
            const sent = () => run.fetch.mock.calls.map(([, init]) => init.body.get('FormAttemptToken'));
            const submit = async () => { run.api.submitCreate(); await confirmed(); };
            const current = () => document.getElementById('formAttemptToken').value;
            return { ...run, sent, submit, current };
        }

        it('K3: a refusal MDM marks resumable (409 / 404) reads RefusalRetrySameForm, keeps the attempt, and the repeat sends it again', async () => {
            const marker = 'FINISHED_GOOD_CREATION_RETRY_SAME_KEY';
            const { submit, sent, current, toast, warn } = createHarness([
                json({ success: false, errors: ['CONCURRENCY_CONFLICT', marker], formAttemptToken: 'attempt-1a' }, 409),
                json({ success: false, errors: ['GSKU_NOT_REFERENCEABLE', marker], formAttemptToken: 'attempt-1a' }, 404),
                json({ success: true, data: { canonicalCode: 'FG-000009' }, formAttemptToken: 'attempt-2' }, 201)
            ]);

            await submit();
            // 409 [CONCURRENCY_CONFLICT, marker]: never RefusalConcurrencyConflict's "reload".
            expect(toast).toHaveBeenLastCalledWith('L:RefusalRetrySameForm', 'error');
            expect(current()).toBe('attempt-1a');
            await submit();
            // 404 [GSKU_NOT_REFERENCEABLE, marker].
            expect(toast).toHaveBeenLastCalledWith('L:RefusalRetrySameForm', 'error');
            expect(current()).toBe('attempt-1a');
            await submit();
            expect(toast).toHaveBeenLastCalledWith('L:CreateSuccessWithCode', 'success');
            // The repeats carried the kept attempt; only 201 moved to a new one.
            expect(sent()).toEqual(['attempt-1', 'attempt-1a', 'attempt-1a']);
            expect(current()).toBe('attempt-2');
            toast.mock.calls.forEach(([message]) => expect(String(message)).not.toContain(marker));
            expect(warn).not.toHaveBeenCalled();
        });

        it('K3: with the marker anywhere the sentence is RefusalRetrySameForm; without it the code\'s own sentence stays', () => {
            const { api, warn } = harness();
            const marker = 'FINISHED_GOOD_CREATION_RETRY_SAME_KEY';
            expect(api.failureSentence({ errors: ['CONCURRENCY_CONFLICT', marker] }, 409)).toBe('L:RefusalRetrySameForm');
            expect(api.failureSentence({ errors: ['GSKU_NOT_REFERENCEABLE', marker] }, 404)).toBe('L:RefusalRetrySameForm');
            expect(api.failureSentence({ errors: [marker, 'CONCURRENCY_CONFLICT'] }, 409)).toBe('L:RefusalRetrySameForm');
            expect(api.failureSentence({ errors: [marker] }, 404)).toBe('L:RefusalRetrySameForm');
            // The cancel / list uses keep their sentence.
            expect(api.failureSentence({ errors: ['CONCURRENCY_CONFLICT'] }, 409)).toBe('L:RefusalConcurrencyConflict');
            expect(api.failureSentence({ errors: ['FINISHED_GOOD_DRAFT_CANCEL_CONFLICT'] }, 409)).toBe('L:RefusalConcurrencyConflict');
            expect(warn).not.toHaveBeenCalled();
            const english = read('Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.en.resx');
            const value = (key) => (english.match(new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]*)</value>`)) || [])[1];
            expect(value('RefusalRetrySameForm')).toMatch(/same GSKU \(without reloading the page\)/);
            expect(value('CreatePendingWithCode')).toMatch(/send it again \(without reloading the page\)/);
            expect(read('Views/MasterDataManagement/FinishedGoods/_IndexL10n.cshtml')).toContain('RefusalRetrySameForm = Localizer["RefusalRetrySameForm"].Value');
            expect(l10nScript()).toContain("'RefusalRetrySameForm'");
        });

        it('K1: MDM\'s 202 reconciliation (success false) is a warning that says send the same form again, and keeps the attempt', async () => {
            const { submit, sent, current, toast } = createHarness([
                json({ success: false, errors: ['FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED', 'GSKU_CHILD_ADMISSION_CONFLICT'],
                    formAttemptToken: 'attempt-1g' }, 202),
                json({ success: true, data: { canonicalCode: 'FG-000010' }, formAttemptToken: 'attempt-2' }, 201)
            ]);

            await submit();
            expect(toast).toHaveBeenCalledTimes(1);
            expect(toast).toHaveBeenLastCalledWith('L:RefusalReconciliationRequired', 'warning');
            expect(toast).not.toHaveBeenCalledWith(expect.anything(), 'error');
            expect(current()).toBe('attempt-1g');
            await submit();
            expect(sent()).toEqual(['attempt-1', 'attempt-1g']);
            expect(toast).toHaveBeenLastCalledWith('L:CreateSuccessWithCode', 'success');
        });

        it('K1: a 202 without a mapped code or a draft code still reads as the pending sentence', async () => {
            const { submit, toast } = createHarness([
                json({ success: false, errors: [], formAttemptToken: 'attempt-1' }, 202),
                json({ success: true, data: {}, formAttemptToken: 'attempt-1' }, 202)
            ]);
            await submit();
            expect(toast).toHaveBeenLastCalledWith('L:CreatePending', 'warning');
            await submit();
            expect(toast).toHaveBeenLastCalledWith('L:CreatePending', 'warning');
        });

        it('K1 / K4: the reconciliation sentences say send the same form again; the standalone burn says an administrator must repair it', () => {
            const english = read('Resources/Views/MasterDataManagement/FinishedGoods/FinishedGoodsIndex.en.resx');
            const value = (key) => (english.match(new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]*)</value>`)) || [])[1];
            ['CreatePending', 'RefusalReconciliationRequired'].forEach((key) => {
                expect(value(key)).toMatch(/same GSKU/);
                expect(value(key)).toMatch(/without reloading the page/);
            });
            expect(value('RefusalBurnTransactionUnavailable')).toMatch(/will not succeed/);
            expect(value('RefusalBurnTransactionUnavailable')).toMatch(/administrator/);
            const { api } = harness();
            expect(api.refusalKeys.CODE_RESERVATION_BURN_TRANSACTION_UNAVAILABLE).toBe('RefusalBurnTransactionUnavailable');
            expect(api.refusalKeys.PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE).toBe('RefusalStoreUnavailable');
            expect(l10nScript()).toContain("'RefusalBurnTransactionUnavailable'");
        });

        it('the list read failure shows a sentence, not a code', () => {
            const { api, toast } = harness();
            api.onListError.call({}, { status: 409, responseJSON: { errors: ['CONCURRENCY_CONFLICT'] } });
            expect(toast).toHaveBeenCalledWith('L:RefusalConcurrencyConflict', 'error');
            api.onListError.call({}, { status: 0, statusText: 'abort' });
            expect(toast).toHaveBeenCalledTimes(1);
        });

        it('covers every refusal code of MDM\'s Finished Good create / cancel / read paths', () => {
            const { api } = harness();
            const mdm = 'services/Diten.MdmService/src';
            const product = `${mdm}/Diten.MdmService.Application/Features/ProductItemSkuMaster`;
            const files = [
                `${product}/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs`,
                `${product}/Lifecycle/Handlers/CommandHandlers/CancelFinishedGoodDraftHandler.cs`,
                `${product}/Handlers/QueryHandlers/GetFinishedGoodsHandler.cs`,
                `${product}/Handlers/QueryHandlers/GetFinishedGoodByIdHandler.cs`,
                `${product}/Handlers/QueryHandlers/GetFinishedGoodGskuSelectorHandler.cs`,
                `${product}/Validators/CreateFinishedGoodDraftValidator.cs`,
                `${mdm}/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs`,
                `${mdm}/Diten.MdmService.Domain/Repositories/ProductIdentityStore.cs`,
                `${mdm}/Diten.MdmService.Api/Controllers/FinishedGoodsController.cs`,
                `${mdm}/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs`
            ];
            const literal = /"([A-Z][A-Z0-9]+(?:_[A-Z0-9]+)+)"/g;
            // A repository's own loop signal that never leaves MDM (the transactional insert retries on it and turns
            // the last one into the ambiguous 202): not a refusal, so it needs no sentence — but it must still exist.
            const internalOnly = {
                [`${mdm}/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs`]: ['FINISHED_GOOD_INSERT_CONFLICT_RETRY'],
                // FG FIX2 item 4 / K3: MDM's "the same key can still finish this" marker — read by the proxy (it keeps
                // the attempt), skipped by failureSentence; never a sentence of its own.
                [`${product}/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs`]: ['FINISHED_GOOD_CREATION_RETRY_SAME_KEY']
            };
            const internal = new Set(Object.entries(internalOnly).flatMap(([file, codes]) => {
                codes.forEach((code) => expect(readRepo(file)).toContain(`"${code}"`));
                return codes;
            }));
            const direct = new Set(files.flatMap((file) => [...readRepo(file).matchAll(literal)].map((m) => m[1]))
                .filter((code) => !internal.has(code)));
            // Answered through other stores on the create path: the GSKU child admission, the code reservation and
            // the legal-entity scope guard. Each must still be in MDM's source.
            const indirect = {
                [`${mdm}/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`]: [
                    'GSKU_CHILD_ADMISSION_LIMIT_REACHED', 'GSKU_CHILD_ADMISSION_REJECTED', 'GSKU_CHILD_ADMISSION_CONFLICT',
                    'GSKU_CHILD_ADMISSION_CONTRACT_INVALID', 'GSKU_CHILD_BINDING_NOT_DURABLE', 'PARENT_NOT_IDENTITY_APPROVED',
                    'GSKU_RETIREMENT_IN_PROGRESS', 'GSKU_LIFECYCLE_OPERATION_ACTIVE', 'GSKU_NOT_FOUND'],
                [`${mdm}/Diten.MdmService.Persistence/Repositories/CodeReservationRepository.cs`]: [
                    'CODE_RESERVATION_ALREADY_TERMINAL', 'CODE_RESERVATION_MISMATCH', 'IDENTITY_RESERVATION_CONFLICT'],
                [`${product}/Handlers/QueryHandlers/GetGlobalProductsHandler.cs`]: [
                    'PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID', 'TENANT_CONTEXT_REQUIRED'],
                // The create path's burn that could not run in a transaction (503: the attempt is kept).
                [`${mdm}/Diten.MdmService.Domain/Repositories/ICodeReservationRepository.cs`]: [
                    'CODE_RESERVATION_BURN_TRANSACTION_UNAVAILABLE']
            };
            const all = new Set(direct);
            Object.entries(indirect).forEach(([file, codes]) => {
                const text = readRepo(file);
                codes.forEach((code) => { expect(text).toContain(`"${code}"`); all.add(code); });
            });
            ['IDEMPOTENCY_KEY_CONFLICT', 'IDEMPOTENCY_KEY_INVALID', 'FINISHED_GOOD_DRAFT_CANCELLED',
                'FINISHED_GOOD_DRAFT_ALREADY_CANCELLED', 'FINISHED_GOOD_CREATION_NOT_SETTLED', 'FINISHED_GOOD_CODE_BURN_REFUSED',
                'FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED', 'CONCURRENCY_CONFLICT', 'GSKU_NOT_REFERENCEABLE',
                'PRODUCT_IDENTITY_WORKFLOW_STORE_UNAVAILABLE'].forEach((code) => expect(all.has(code)).toBe(true));
            const missing = [...all].filter((code) => !api.refusalKeys[code]);
            expect(missing).toEqual([]);
            expect(api.refusalKeys.FINISHED_GOOD_CREATION_RETRY_SAME_KEY).toBeUndefined();
            const orphans = Object.keys(api.refusalKeys).filter((code) => !all.has(code));
            expect(orphans).toEqual([]);
            // Every sentence key is in the page's L10n payload.
            const payload = read('Views/MasterDataManagement/FinishedGoods/_IndexL10n.cshtml');
            new Set(Object.values(api.refusalKeys)).forEach((key) => expect(payload).toContain(`${key} = `));
        });
    });
});
