const fs = require('fs');
const path = require('path');

describe('LSKU executable UI contracts', () => {
  const id = '10000000-0000-0000-0000-000000000001';
  const detail = (state, version, actions) => ({ id, lifecycleStatus: state, version, availableActions: actions });
  const response = (status, body) => ({ status, ok: status >= 200 && status < 300, json: async () => body });
  function harness(personalizationClient) {
    document.body.innerHTML = '<table class="datatables-lskus"></table><div id="offcanvasDetailsPreview"></div>' +
      '<span id="oc-status"></span><span id="oc-version"></span><form id="lskuLifecycleToken">' +
      '<input name="__RequestVerificationToken" value="test-csrf"></form>' +
      '<select id="filterLifecycleStatus"><option value=""></option><option value="Draft">Draft</option></select>' +
      '<button id="btnFilterApply"></button><button id="btnFilterReset"></button>';
    const L = new Proxy({}, { get: (_, key) => key });
    const toast = vi.fn(), http = vi.fn(), reload = vi.fn();
    const confirm = vi.fn((_, callback) => callback('Obsolete'));
    const api = { ajax: { reload }, draw: vi.fn(), search: vi.fn(() => ''), order: vi.fn(() => [[0, 'asc']]),
      column: () => ({ visible: vi.fn() }), columns: () => ({ visible: () => ({ toArray: () => [true,true,true,true,true,true] }) }),
      colReorder: { order: vi.fn(() => [0,1,2,3,4,5]) } };
    const jquery = node => {
      const jq = { next: () => jq, find: () => jq, toggleClass: () => jq, hasClass: () => true,
        on: () => jq, select2: () => jq, trigger: () => jq,
        val: value => { if (node) node.value = value || ''; return jq; } };
      return jq;
    };
    const w = { L10n: L, personalizationClient, showToast: toast, showConfirm: confirm,
      DtDefaults: { refreshButtonGroupRadii: vi.fn(), handleUnauthorized: vi.fn() } };
    const bootstrap = { Offcanvas: { getOrCreateInstance: () => ({ show: vi.fn() }) },
      Collapse: { getOrCreateInstance: () => ({ hide: vi.fn() }) } };
    let source = fs.readFileSync(path.join(__dirname, '../wwwroot/assets/js/MasterDataManagement/Lskus/index.js'), 'utf8');
    source = source.replace("return { init: async () => { bindEvents(); bindFilter(); await initDataTable(); } };",
      'return { readAvailableActions, postLifecycle, requestRetirement, bindEvents, bindFilter, normalizeView, getCurrentView, applySavedTableState, getResetBaselineState, saveDefaultView, loadDefaultView, setTable: value => { dt = value; } };')
      .replace("document.addEventListener('DOMContentLoaded', () => LskusList.init());", '');
    const module = new Function('window','document','$','bootstrap','fetch','FormData', source + ';return LskusList;')(
      w, document, jquery, bootstrap, http, FormData);
    module.setTable(api);
    return { module, http, toast, reload, api, w };
  }

  it('saves and reloads applied lifecycle state using the existing personalization contract', async () => {
    let saved;
    const client = {
      saveView: vi.fn(async payload => (saved = { ...payload, id: 'test-view' })),
      getViews: vi.fn(async () => [saved])
    };
    const h = harness(client);
    await h.module.saveDefaultView({ filters: { lifecycleStatus: 'Draft' } });
    expect(saved.viewName).toBe('SaveView');
    expect(saved.viewDefinition.filters.lifecycleStatus).toBe('Draft');
    const reloaded = await harness(client).module.loadDefaultView();
    expect(reloaded.filters.lifecycleStatus).toBe('Draft');
  });

  it('does not silently save when the client or its response is missing', async () => {
    await expect(harness().module.saveDefaultView({})).rejects.toThrow('ErrorGateway');
    await expect(harness({ saveView: async () => null }).module.saveDefaultView({})).rejects.toThrow('ErrorGateway');
  });

  it.each([
    [1, ['DETAILS','SUBMIT']], [2, ['DETAILS','WITHDRAW_APPROVAL']],
    [3, ['DETAILS','REQUEST_RETIREMENT']], [4, ['DETAILS']], [999, ['DETAILS']]
  ])('limits the server permission projection to lifecycle state %s', (state, expected) => {
    const { module } = harness();
    expect(module.readAvailableActions(detail(state, 1,
      ['DETAILS','SUBMIT','WITHDRAW_APPROVAL','REQUEST_RETIREMENT','RETIRE','EDIT']))).toEqual(expected);
    expect(module.readAvailableActions(detail(state, 1, ['DETAILS']))).toEqual(['DETAILS']);
    expect(module.readAvailableActions(detail(state, 1, ['details','submit']))).toEqual(['DETAILS']);
  });

  it.each([['SUBMIT',1,2],['WITHDRAW_APPROVAL',2,1]])('reads back %s state and version after an actual successful response',
    async (action, before, after) => {
      const h = harness();
      h.http.mockResolvedValueOnce(response(200, { data: detail(before, 3, ['DETAILS',action]) }))
        .mockResolvedValueOnce(response(200, { isSuccessful: true, statusCode: 200 }))
        .mockResolvedValueOnce(response(200, { data: detail(after, 4, ['DETAILS']) }));
      await h.module.postLifecycle(id, action, null);
      expect(h.http).toHaveBeenCalledTimes(3);
      const options = h.http.mock.calls[1][1];
      expect(options.body.get('ExpectedVersion')).toBe('3');
      expect(options.body.get('__RequestVerificationToken')).toBe('test-csrf');
      expect(options.credentials).toBe('same-origin');
      expect(document.getElementById('oc-version').textContent).toBe('4');
      expect(h.reload).toHaveBeenCalledWith(null, false);
      expect(h.toast.mock.calls.at(-1)[1]).toBe('success');
    });

  it.each([['SUBMIT',1,2],['WITHDRAW_APPROVAL',2,1]])('rejects %s read-back with a non-advancing version',
    async (action, before, after) => {
      const h = harness();
      h.http.mockResolvedValueOnce(response(200, { data: detail(before, 3, ['DETAILS',action]) }))
        .mockResolvedValueOnce(response(200, { isSuccessful: true, statusCode: 200 }))
        .mockResolvedValueOnce(response(200, { data: detail(after, 3, ['DETAILS']) }));
      await h.module.postLifecycle(id, action, null);
      expect(h.toast.mock.calls.every(call => call[1] !== 'success')).toBe(true);
    });

  it.each([403,409,503,504])('does not show success for API failure %s', async status => {
    const h = harness();
    h.http.mockResolvedValueOnce(response(200, { data: detail(1, 3, ['DETAILS','SUBMIT']) }))
      .mockResolvedValueOnce(response(status, { errors: ['test-failure'] }));
    await h.module.postLifecycle(id, 'SUBMIT', null);
    expect(h.http).toHaveBeenCalledTimes(2);
    expect(h.toast).toHaveBeenCalledWith('test-failure', 'error');
  });

  it('denies stale action before mutation and rejects inconsistent postcondition', async () => {
    const h = harness();
    h.http.mockResolvedValueOnce(response(200, { data: detail(2, 3, ['DETAILS']) }));
    await h.module.postLifecycle(id, 'SUBMIT', null);
    expect(h.http).toHaveBeenCalledTimes(1);
    h.http.mockResolvedValueOnce(response(200, { data: detail(1, 3, ['DETAILS','SUBMIT']) }))
      .mockResolvedValueOnce(response(200, { isSuccessful: true, statusCode: 200 }))
      .mockResolvedValueOnce(response(200, { data: detail(1, 3, ['DETAILS','SUBMIT']) }));
    await h.module.postLifecycle(id, 'SUBMIT', null);
    expect(h.toast.mock.calls.every(call => call[1] !== 'success')).toBe(true);
  });

  it('wires the real retirement row click and verifies its source fence read-back', async () => {
    const h = harness();
    h.module.bindEvents();
    const button = document.createElement('button');
    button.className = 'js-request-retirement'; button.dataset.id = id;
    document.querySelector('table').appendChild(button);
    h.http.mockResolvedValueOnce(response(200, { data: detail(3, 4, ['DETAILS','REQUEST_RETIREMENT']) }))
      .mockResolvedValueOnce(response(202, { isSuccessful: true, statusCode: 202 }))
      .mockResolvedValueOnce(response(200, { data: detail(3, 5, ['DETAILS']) }));
    button.click();
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(h.http.mock.calls[1][0]).toBe('/MasterDataManagement/Lskus/api/' + id + '/retirement-requests');
    expect(h.http.mock.calls[1][1].body.get('RequestReason')).toBe('Obsolete');
    expect(h.reload).toHaveBeenCalledWith(null, false);
    expect(h.toast).toHaveBeenCalledWith('RetirementRequestPending', 'warning');
    expect(document.getElementById('oc-version').textContent).toBe('5');
  });

  it('keeps staged filter changes out of Save View until Apply and restores the reset baseline', () => {
    const h = harness(); h.module.bindFilter();
    const filter = document.getElementById('filterLifecycleStatus');
    filter.value = 'Draft';
    expect(h.module.getCurrentView(h.api).filters.lifecycleStatus).toBe('');
    document.getElementById('btnFilterApply').click();
    expect(h.module.getCurrentView(h.api).filters.lifecycleStatus).toBe('Draft');
    document.getElementById('btnFilterReset').click();
    expect(h.module.getCurrentView(h.api).filters.lifecycleStatus).toBe('');
    expect(filter.value).toBe('');
  });
});

describe('MOD-0290 LSKU lifecycle UX', () => {
  const root = path.join(__dirname, '..');
  const read = relativePath => fs.readFileSync(path.join(root, relativePath), 'utf8');
  const script = () => read('wwwroot/assets/js/MasterDataManagement/Lskus/index.js');

  it('keeps all browser traffic on strict same-origin MVC proxies', () => {
    const source = script();
    const controller = read('Controllers/LskusController.cs');
    expect(source).toContain("const endpoint = '/MasterDataManagement/Lskus/api';");
    expect(source).not.toMatch(/window\.API|localhost:5000|:5059|Authorization|Bearer|X-Tenant-Id|crypto\.randomUUID/i);
    expect(controller).toContain('[HttpPost("api/{id:guid}/identity-approval/withdraw")]');
    expect(controller).toContain('HasOnlyFormFieldsAsync("ExpectedVersion", "ReasonCode", "Comment")');
    expect(controller).toContain('request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"))');
  });

  it('uses only ordered server-owned available actions and never renders direct retire', () => {
    const source = script();
    expect(source).toContain("const actionOrder = ['DETAILS', 'SUBMIT', 'WITHDRAW_APPROVAL', 'REQUEST_RETIREMENT'];");
    expect(source).toContain('const readAvailableActions = detail =>');
    expect(source).toContain('.filter(value => supportedActions.has(value))');
    expect(source).toContain('actionOrder.filter(value => actions.includes(value))');
    expect(source).toContain("WITHDRAW_APPROVAL: ['withdraw', 'js-lifecycle-action'");
    expect(source).toContain("REQUEST_RETIREMENT: ['request-retirement', 'js-request-retirement'");
    expect(source).not.toMatch(/js-retire|data-action[^\n]+RETIRE|state === 1 && canSubmit|canRetire/);
  });

  it('requests retirement without exposing the direct retire route', () => {
    const source = script();
    const controller = read('Controllers/LskusController.cs');
    expect(source).toContain("body.set('RequestReason', requestReason)");
    expect(source).toContain("/retirement-requests`");
    expect(source).toContain("readAvailableActions(refreshed).includes('REQUEST_RETIREMENT')");
    expect(controller).toContain('[HttpPost("api/{id:guid}/retirement-requests")]');
    expect(controller).toContain('private const string RequestRetirementPermission = "mdm.lskus.request-retirement"');
    expect(source).not.toMatch(/\/retire[`'"?]/);
  });

  it('refreshes detail before mutation and verifies submit and withdrawal postconditions', () => {
    const source = script();
    expect(source).toContain('const detail = await fetchDetail(id)');
    expect(source).toContain('if (!readAvailableActions(detail).includes(action))');
    expect(source).toContain("body.set('ExpectedVersion', String(expectedVersion))");
    expect(source).toContain("body.set('ReasonCode', 'REQUESTER_WITHDRAWAL')");
    expect(source).toContain("body.set('Comment', '')");
    expect(source).toContain("const route = action === 'SUBMIT' ? 'submit' : 'identity-approval/withdraw'");
    expect(source).toContain("action === 'WITHDRAW_APPROVAL' && response.status === 200");
    expect(source).toContain('response.status === 202 ? L.WithdrawPending : L.WithdrawSuccess');
  });

  it('persists and sends the real bounded lifecycle filter to the server', () => {
    const source = script();
    const filter = read('Views/MasterDataManagement/Lskus/_Filter.cshtml');
    expect(filter).toContain('id="filterLifecycleStatus"');
    expect(filter).toContain('class="form-select form-select-sm select2"');
    expect(source).toContain("const emptyFilters = () => ({ lifecycleStatus: '' })");
    expect(source).toContain("if (appliedFilters.lifecycleStatus) query.set('lifecycleStatus', appliedFilters.lifecycleStatus)");
    expect(source).toContain("container.toggleClass('dt-inline-filter-selected', selected)");
    expect(source).toContain("toggleClass('border-primary bg-label-primary', selected)");
    expect(source).toContain('getAppliedFilterCount()');
  });

  it('includes lifecycle in Save View and reset without English fallbacks', () => {
    const source = script();
    expect(source).toContain('filters: appliedFilters');
    expect(source).toContain('appliedFilters = normalizeFilters(normalized.filters)');
    expect(source).toContain('applySavedTableState(dt, getResetBaselineState())');
    expect(source).toContain('viewDefinition: normalized');
    expect(source).toContain('|| L.SaveView)?.trim()');
    expect(source).toContain('if (!viewName) throw new Error(L.ErrorGateway)');
    expect(source).not.toMatch(/\|\| ['"](?:Default|Save View|Request failed|Unknown)['"]/);
  });

  it('keeps immutable two-field create and tenant shell', () => {
    const source = script();
    const index = read('Views/MasterDataManagement/Lskus/Index.cshtml');
    expect(source).toContain("body.set('GskuId', gskuId)");
    expect(source).toContain("body.set('MarketCode', marketCode)");
    expect(source).not.toMatch(/body\.set\(['"](?:TenantId|IdempotencyKey|CanonicalCode|ReservationId|Credential)/);
    expect(index).toContain('Layout = "_LayoutTenantShell"');
    expect(index).not.toMatch(/data-can-submit|data-can-retire/);
    expect(index).toContain('id="lskuLifecycleToken"');
  });

  it('has seven locale files with exact parity including withdrawal vocabulary', () => {
    const locales = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
    const keys = locales.map(locale => [...read(`Resources/Views/MasterDataManagement/Lskus/LskusIndex.${locale}.resx`)
      .matchAll(/<data name="([^"]+)"/g)].map(match => match[1]).sort());
    keys.slice(1).forEach(localeKeys => expect(localeKeys).toEqual(keys[0]));
    ['WithdrawApproval', 'WithdrawConfirmation', 'WithdrawPending', 'WithdrawSuccess',
      'LifecycleDraft', 'LifecyclePendingIdentityApproval', 'LifecycleIdentityApproved', 'LifecycleRetired']
      .forEach(key => expect(keys[0]).toContain(key));
  });

  it('does not expose edit, correction, delete, bulk or direct retirement UI', () => {
    const browser = [script(), read('Views/MasterDataManagement/Lskus/Index.cshtml'),
      read('Views/MasterDataManagement/Lskus/_DataTable.cshtml'),
      read('Views/MasterDataManagement/Lskus/_CreateEditOffcanvas.cshtml')].join('\n');
    expect(browser).not.toMatch(/bulk|js-edit|request-correction|js-retire|btnDelete|dt-checkboxes/i);
  });
});
