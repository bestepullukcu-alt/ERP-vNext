import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';

const read = path => readFileSync(new URL('../../' + path, import.meta.url), 'utf8');
const script = read('wwwroot/assets/js/PPM/Portfolios/index.js');
const markup = () => '<div id="portfolio-surface" data-can-create="false">' +
    read('Views/PPM/Portfolios/_OwnerAssignmentOffcanvas.cshtml') +
    '<input id="ppmCapacityAllocationDescription"><div id="oc-owner"></div><div id="oc-owner-history"></div>' +
    '<div id="oc-capacity"></div><button id="oc-btn-edit"></button><button class="js-portfolio-owner" data-id="p"></button></div>';
function load(html = markup()) {
    const dom = new JSDOM(html, { runScripts: 'outside-only', url: 'http://localhost/PPM/Portfolios' });
    let config;
    dom.window.PpmCrud = { mount: value => { config = value; } };
    dom.window.eval(script);
    dom.window.document.dispatchEvent(new dom.window.Event('DOMContentLoaded'));
    return { dom, window: dom.window, config };
}
const flush = async () => { for (let i = 0; i < 8; i++) await Promise.resolve(); };

test('denied page mounts no table or action machinery', () => {
    const { config, dom } = load('<div role="alert">Unavailable</div>');
    assert.equal(config, undefined);
    dom.window.close();
});

test('four user inputs follow Slim limits and never include authority or lifecycle fields', () => {
    const dom = new JSDOM(read('Views/PPM/Portfolios/_CreateEditOffcanvas.cshtml'));
    const fields = [...dom.window.document.querySelectorAll('input:not([type="hidden"]), textarea, select')];
    assert.deepEqual(fields.map(x => x.name), ['Code', 'Name', 'Description', 'CapacityAllocationDescription']);
    assert.deepEqual(fields.map(x => x.required), [true, true, false, false]);
    assert.deepEqual(fields.map(x => x.maxLength), [64, 200, 2000, 2000]);
    assert.ok(fields.every(x => x.closest('.diten-field')));
    dom.window.close();
});

test('only Draft and affirmative server decisions expose separate edit and owner actions', () => {
    const { config, dom } = load();
    for (const state of ['Active', 'Archived']) {
        assert.equal(config.rowActions({ id: 'p', lifecycleState: state, actions: { canEdit: true, canAssignOwner: true } }, {}).length, 1);
        assert.equal(config.canEdit({ lifecycleState: state, actions: { canEdit: true } }), false);
    }
    assert.equal(config.rowActions({ id: 'p', lifecycleState: 'Draft' }, {}).length, 1);
    const actions = config.rowActions({ id: 'p', lifecycleState: 'Draft', actions: { canEdit: true, canAssignOwner: true } }, {});
    assert.equal(actions.length, 3);
    assert.ok(actions.every(x => !x.className.includes('lifecycle') && !x.className.includes('delete')));
    assert.ok(actions.every(x => !('data-json' in x.attrs)));
    assert.equal(config.canCreate, false);
    assert.equal(config.disableMutations, true);
    assert.equal(config.loadDetails, true);
    assert.equal(config.clearOnReadFailure, true);
    dom.window.close();
});

test('history is rendered as text, then removed on a response without history permission', () => {
    const { config, window } = load();
    window.L10n = { NoOwner: 'No owner', AccessUnavailable: 'Access unavailable' };
    config.renderDetails({ lifecycleState: 'Active', owner: { displayLabel: '<img src=x onerror=alert(1)>' },
        ownerHistory: [{ displayLabel: 'Human', reason: '<script>bad()</script>', occurredAtUtc: '2026-09-10T00:00:00Z' }] });
    assert.equal(window.document.querySelector('#oc-owner img'), null);
    assert.equal(window.document.querySelector('#oc-owner-history script'), null);
    assert.match(window.document.getElementById('oc-owner-history').textContent, /<script>/);
    config.renderDetails({ lifecycleState: 'Draft', ownerVisible: false });
    assert.equal(window.document.getElementById('oc-owner-history').textContent, '');
    assert.equal(window.document.getElementById('oc-owner').textContent, 'Access unavailable');
    assert.equal(window.document.getElementById('oc-btn-edit').hidden, true);
    window.close();
});

test('status codes retain distinct localized explanations', () => {
    const { config, dom } = load();
    const L = { InvalidInput: 'input', SessionRequired: 'session', OperationDenied: 'denied',
        RecordUnavailable: 'missing', VersionConflict: 'refresh', DependencyUnavailable: 'dependency' };
    assert.deepEqual([400, 401, 403, 404, 409, 503].map(status => config.statusMessage({ status }, L)),
        ['input', 'session', 'denied', 'missing', 'refresh', 'dependency']);
    dom.window.close();
});

test('actual owner form retries the identical request and double click has one in-flight write', async () => {
    const { config, window } = load();
    const owner = { id: 'p', version: 3, lifecycleState: 'Draft', actions: { canAssignOwner: true },
        owner: { assignmentId: 'previous' } };
    const writes = [], urls = [];
    let selectedOptions, complete;
    const jq = () => ({ data: () => null, select2: options => { selectedOptions = options; return jq(); } });
    jq.fn = { select2: true };
    window.$ = window.jQuery = jq;
    window.bootstrap = { Offcanvas: { getOrCreateInstance: () => ({ show() {}, hide() {} }) } };
    const L = { TransferOwner: 'Transfer', DependencyUnavailable: 'Unavailable', OwnerRequired: 'Select' };
    config.onReady({
        L, baseUrl: '/PPM/Portfolios/api', antiForgery: () => 'anti', unwrap: x => x,
        reload() {},
        request: async (url, options) => {
            urls.push(url);
            if (!options?.body) return owner;
            writes.push(JSON.parse(options.body));
            assert.equal(options.headers.RequestVerificationToken, 'anti');
            await new Promise(resolve => { complete = resolve; });
            throw { status: 503 };
        }
    });
    window.document.querySelector('.js-portfolio-owner').click();
    await flush();
    assert.equal(window.document.getElementById('ownerAssignmentTitle').textContent, 'Transfer');
    assert.ok(selectedOptions.ajax.transport);
    const user = window.document.getElementById('portfolioOwnerUser');
    const option = window.document.createElement('option');
    option.value = 'target'; option.textContent = 'Test candidate'; user.appendChild(option);
    window.document.getElementById('portfolioOwnerReason').value = 'Handover';
    const save = window.document.getElementById('btnSavePortfolioOwner');
    save.click(); save.click();
    await flush();
    assert.equal(writes.length, 1);
    complete(); await flush();
    save.click(); await flush();
    assert.equal(writes.length, 2);
    assert.deepEqual(writes[0], writes[1]);
    assert.equal(writes[0].operation, 'Transfer');
    assert.equal(writes[0].expectedAssignmentId, 'previous');
    assert.equal(writes[0].expectedVersion, 3);
    assert.equal(writes[0].tenantId, undefined);
    assert.equal(writes[0].actorId, undefined);
    assert.ok(writes[0].requestId);
    complete(); await flush();
    assert.equal(window.document.getElementById('owner-assignment-alert').textContent, 'Unavailable');
    assert.equal(urls[0], '/PPM/Portfolios/api/p');
    window.close();
});

test('all seven locales contain the new field, action and failure keys', () => {
    for (const lang of ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru']) {
        const xml = read('Resources/Views/PPM/Portfolios/PortfoliosIndex.' + lang + '.resx');
        const dom = new JSDOM(xml, { contentType: 'text/xml' });
        for (const key of ['CapacityAllocationDescription', 'Owner', 'AssignOwner', 'TransferOwner', 'AssignmentReason',
            'AccessUnavailable', 'DraftDeliveryScope', 'InvalidInput', 'SessionRequired', 'OperationDenied',
            'RecordUnavailable', 'VersionConflict', 'DependencyUnavailable']) {
            assert.ok(dom.window.document.querySelector('data[name="' + key + '"] value')?.textContent, lang + ':' + key);
        }
        dom.window.close();
    }
});
