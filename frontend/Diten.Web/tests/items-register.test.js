const fs = require('fs');
const path = require('path');
const vm = require('node:vm');

// MOD-0290-FU04 S5 — the Items screen's executed browser contracts (isolated DOM and HTTP fixtures, the gsku-register
// harness): actions are drawn only from the server's availableActions, an unknown action is never drawn, a lifecycle
// request re-reads the item, posts its version and the reason, and reads the answer back.
describe('Items executed browser contracts', () => {
    const id = '12345678-1234-1234-1234-123456789012';
    const item = (actions, status = 3, version = 4) => ({
        id, canonicalCode: 'IT-000000000003', name: 'Synthetic', materialType: 'EXCIPIENT', baseUomCode: 'KGM',
        uomConversions: [], storageConditionCodes: ['COOL_2_8'], lotControlled: true, stockable: false,
        lifecycleStatus: status, version, availableActions: actions
    });
    const response = (data, status = 200) => ({
        status, ok: status >= 200 && status < 300,
        json: async () => status < 300 ? { data, isSuccessful: true, statusCode: status } : data
    });

    function harness(responses = [], body = '') {
        document.body.innerHTML = '<form id="itemLifecycleToken"><input name="__RequestVerificationToken" value="synthetic-csrf"></form>' + body;
        const fetch = vi.fn(async () => {
            const next = responses.shift();
            if (!next) throw new Error('Unexpected HTTP call');
            return next;
        });
        const toast = vi.fn();
        const reload = vi.fn();
        let pending;
        const fakeWindow = {
            L10n: new Proxy({}, { get: (_, key) => String(key) }),
            showToast: toast,
            showConfirm: vi.fn((_, callback) => { pending = callback('  Supplier stopped  '); }),
            // The list component's own renderer is a separate file: here it lists what it was handed.
            DitenDataTable: { renderActions: (actions) => actions.map((a) => `<a data-row-action="${a.key}" data-action="${a.attrs['data-action']}" class="${a.className}"></a>`).join('') },
            location: { href: '' }
        };
        const source = fs.readFileSync(path.join(__dirname, '../wwwroot/assets/js/MasterDataManagement/Items/index.js'), 'utf8');
        // Test-only access to existing closure functions; shipped JS has no test hook.
        const exposed = source.replace('init: async () => {', `test: {
            readAvailableActions, renderRowActions, requestLifecycle, buildListOptions, getErrorMessage, renderRecord,
            setupTable: (table) => { dt = table; }
        }, init: async () => {`);
        const api = vm.runInNewContext(exposed + '\nItemsPage.test;', {
            window: fakeWindow, document, fetch, FormData, URLSearchParams, setTimeout, clearTimeout, console
        });
        api.setupTable({ ajax: { reload } });
        return { api, fetch, toast, reload, window: fakeWindow, settle: () => pending };
    }

    it.each([
        [['DETAILS']],
        [['DETAILS', 'EDIT', 'SUBMIT']],
        [['DETAILS', 'EDIT', 'DEACTIVATE']],
        [['DETAILS', 'REACTIVATE', 'RETIRE']]
    ])('reads the server action sequence %j as it is', (actions) => {
        expect(harness().api.readAvailableActions({ availableActions: actions })).toEqual(actions);
    });

    it('draws a row\'s actions only from its availableActions — an unknown or a duplicate action is never drawn', () => {
        const h = harness();
        const html = h.api.renderRowActions(item(['DETAILS', 'WITHDRAW_APPROVAL', 'DELETE', 'DEACTIVATE', 'DEACTIVATE']));
        document.body.insertAdjacentHTML('beforeend', `<div id="row">${html}</div>`);
        const drawn = [...document.querySelectorAll('#row [data-action]')].map((x) => x.dataset.action);
        expect(drawn).toEqual(['DETAILS', 'DEACTIVATE']);
        // DETAILS is the quick-view trigger the list component delegates (.js-quick-view, data-row-action="quickView").
        expect(document.querySelector('#row .js-quick-view').dataset.rowAction).toBe('quickView');
    });

    it('draws no lifecycle action the server did not return', () => {
        const h = harness();
        document.body.insertAdjacentHTML('beforeend', `<div id="row">${h.api.renderRowActions(item(['DETAILS']))}</div>`);
        expect([...document.querySelectorAll('#row [data-action]')].map((x) => x.dataset.action)).toEqual(['DETAILS']);
        expect(h.api.readAvailableActions({})).toEqual([]);
    });

    it.each([
        ['DEACTIVATE', 'deactivate', 3, 4],
        ['RETIRE', 'retire', 4, 5],
        ['REACTIVATE', 'reactivation-requests', 4, 2]
    ])('%s: confirm with a reason, re-read, post version + reason, read back, reload', async (action, suffix, before, after) => {
        const h = harness([
            response(item(['DETAILS', action], before, 4)),
            response(item(['DETAILS'], after, 5)),
            response(item(['DETAILS'], after, 5))
        ]);
        h.api.requestLifecycle(id, action, () => h.reload());
        await h.settle();

        const calls = h.fetch.mock.calls;
        expect(calls.map((c) => c[0])).toEqual([
            `/MasterDataManagement/Items/api/${id}`,
            `/MasterDataManagement/Items/api/${id}/${suffix}`,
            `/MasterDataManagement/Items/api/${id}`]);
        const post = calls[1][1];
        expect(post.method).toBe('POST');
        expect(post.headers).toEqual({ RequestVerificationToken: 'synthetic-csrf', 'X-Requested-With': 'XMLHttpRequest' });
        expect(post.body.get('ExpectedVersion')).toBe('4');
        expect(post.body.get('Reason')).toBe('Supplier stopped');
        expect(post.body.get('__RequestVerificationToken')).toBe('synthetic-csrf');
        // No credential of its own: same-origin cookies only, never a bearer or tenant header from the browser.
        expect(JSON.stringify(calls.map((c) => c[1]?.headers || {}))).not.toMatch(/Authorization|X-Tenant-Id/);
        expect(h.window.showConfirm.mock.calls[0][2]).toMatchObject({ showInput: true, inputRequired: true });
        expect(h.toast).toHaveBeenCalledWith(`${action.charAt(0)}${action.slice(1).toLowerCase()}Success`, 'success');
        expect(h.reload).toHaveBeenCalled();
    });

    it('SUBMIT posts the version without asking a reason, and says the 503 sentence the proxy sends', async () => {
        const h = harness([
            response(item(['DETAILS', 'EDIT', 'SUBMIT'], 1, 2)),
            response({ success: false, errors: ['Activation approval is not available yet.'] }, 503)
        ]);
        h.api.requestLifecycle(id, 'SUBMIT', () => h.reload());
        await h.settle();

        const post = h.fetch.mock.calls[1][1];
        expect(post.body.get('ExpectedVersion')).toBe('2');
        expect(post.body.has('Reason')).toBe(false);
        expect(h.window.showConfirm.mock.calls[0][2].showInput).toBeUndefined();
        expect(h.toast).toHaveBeenCalledWith('Activation approval is not available yet.', 'error');
        expect(h.fetch).toHaveBeenCalledTimes(2);
    });

    it('does not post when the server no longer offers the action', async () => {
        const h = harness([response(item(['DETAILS'], 4, 5))]);
        h.api.requestLifecycle(id, 'RETIRE', () => h.reload());
        await h.settle();

        expect(h.fetch).toHaveBeenCalledTimes(1);
        expect(h.toast).toHaveBeenCalledWith('LifecycleStateChanged', 'error');
    });

    it('refuses an empty reason before any request', async () => {
        const h = harness([]);
        h.window.showConfirm = vi.fn((_, callback) => { callback('   '); });
        h.api.requestLifecycle(id, 'DEACTIVATE');
        await Promise.resolve();

        expect(h.fetch).not.toHaveBeenCalled();
        expect(h.toast).toHaveBeenCalledWith('ReasonRequired', 'error');
    });

    it('an unknown action key is not a lifecycle request', () => {
        const h = harness([]);
        h.api.requestLifecycle(id, 'DELETE');
        expect(h.window.showConfirm).not.toHaveBeenCalled();
    });

    it('the list wire: server mode on the same-origin proxy, server export, filters by MDM keys, no credential headers', () => {
        const options = harness().api.buildListOptions();
        expect(options.dataMode).toBe('server');
        expect(options.ajax.url).toBe('/MasterDataManagement/Items/api');
        expect(options.ajax.headers).toEqual({ 'X-Requested-With': 'XMLHttpRequest' });
        expect(options.export).toEqual({ mode: 'server', url: '/MasterDataManagement/Items/api/export', fileName: 'items' });
        expect(options.filters.fields.map((f) => f.key)).toEqual(['materialType', 'status']);
        expect(options.filters.fields.every((f) => !('matches' in f))).toBe(true);
        expect(options.savedView).toMatchObject({ moduleKey: 'MasterDataManagement', pageKey: 'Items' });
    });

    it('the record draws its action buttons only from availableActions (DETAILS is the page itself)', () => {
        const h = harness([], '<div id="itemDetails" data-item-id="' + id + '"><div data-item-field="lifecycleStatus"></div>' +
            '<div data-item-field="name"></div><div id="itemDetailActions"></div><tbody id="itemDetailConversions"></tbody></div>');
        h.api.renderRecord(item(['DETAILS', 'EDIT', 'DEACTIVATE', 'UNKNOWN']));

        const host = document.getElementById('itemDetailActions');
        expect([...host.children].map((x) => x.dataset.detailAction || 'EDIT-LINK')).toEqual(['EDIT-LINK', 'DEACTIVATE']);
        expect(host.querySelector('a').getAttribute('href')).toBe(`/MasterDataManagement/Items/Edit/${id}`);
        expect(document.querySelector('[data-item-field="name"]').textContent).toBe('Synthetic');
        // No inline handler is written anywhere.
        expect(document.body.innerHTML).not.toMatch(/onclick=/i);
    });

    it('says the proxy\'s sentences and warns on an unmapped refusal without showing a code', async () => {
        const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
        const h = harness();
        const text = await h.api.getErrorMessage({ status: 422, json: async () => ({ success: false, errors: ['ErrorValidation'], unmapped: true }) });
        expect(text).toBe('ErrorValidation');
        expect(warn).toHaveBeenCalled();
    });
});
