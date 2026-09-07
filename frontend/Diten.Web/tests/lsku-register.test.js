const fs = require('fs');
const path = require('path');

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
    expect(source).toContain('|| L.SaveView).trim()');
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
