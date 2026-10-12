const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

describe('MOD-0290-FU03 Product Legal Entity Scope Golden Slim contract', () => {
  const script = () => read('wwwroot/assets/js/MasterDataManagement/ProductLegalEntityScopes/index.js');
  const loadModule = ({ canConfigure = true, canReplace = true, canEnd = true } = {}) => {
    document.body.innerHTML = `
      <div id="product-legal-entity-scopes" data-can-configure="${canConfigure}" data-can-replace="${canReplace}" data-can-end="${canEnd}"></div>
      <table id="dt-product-legal-entity-scopes"></table>
      <select id="legalEntityIds" multiple></select>`;
    window.L10n = {};
    const executable = script().replace(/document\.addEventListener\('DOMContentLoaded'[\s\S]*$/, '');
    return new Function(`${executable}\nreturn ProductLegalEntityScopesList;`)();
  };

  it('uses tenant shell, same-origin MVC only and no browser-owned security context', () => {
    const controller = read('Controllers/ProductLegalEntityScopesController.cs');
    const index = read('Views/MasterDataManagement/ProductLegalEntityScopes/Index.cshtml');
    const js = script();
    expect(index).toContain('Layout = "_LayoutTenantShell"');
    expect(controller).toContain('[Route("MasterDataManagement/ProductLegalEntityScopes")]');
    expect(controller).toContain('_gateway}/api/global-products');
    expect(js).toContain("const endpoint = '/MasterDataManagement/ProductLegalEntityScopes/api';");
    expect(js).toContain("const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });");
    expect(js).not.toMatch(/localhost:5057|localhost:5059|Authorization|X-Tenant-Id|crypto\.randomUUID/);
  });

  it('renders exactly three business fields with conditional bounded Legal Entities', () => {
    const form = read('Views/MasterDataManagement/ProductLegalEntityScopes/_CreateEditOffcanvas.cshtml');
    expect(form).toContain('name="GlobalProductId"');
    expect(form).toContain('name="Mode"');
    expect(form).toContain('name="LegalEntityIds"');
    expect(form).not.toMatch(/name="(?:TenantId|ActorId|EffectiveFromUtc|EffectiveToUtc|Audit|IdempotencyKey)"/);
    expect(script()).toContain('if (legalEntities.length > 200)');
    expect(script()).not.toContain('.slice(0, 200)');
    expect(script()).toContain("toggleAttribute('required', scoped)");
  });

  it('uses Data Protection attempt transport and preserves it for 202', () => {
    const controller = read('Controllers/ProductLegalEntityScopesController.cs');
    const js = script();
    expect(controller).toContain('CreateProtector("Diten.Web", "MOD-0290-FU03"');
    expect(controller).toContain('Guid.NewGuid().ToString("D")');
    expect(controller).not.toContain('Convert.ToHexString');
    expect(controller).toContain('request.Headers.TryAddWithoutValidation("Idempotency-Key", operationKey)');
    expect(controller).toContain('envelope.TryGetProperty("data", out var camelData)');
    expect(js).toContain("body.set('FormAttemptToken'");
    const accepted = js.indexOf('response.status === 202');
    const rotation = js.indexOf("formAttemptToken').value = nextToken");
    expect(accepted).toBeGreaterThan(-1);
    expect(rotation).toBeGreaterThan(accepted);
    expect(js.slice(accepted, rotation)).not.toContain("formAttemptToken').value");
  });

  it('provides management quick detail, Save View and factory reset without bulk/delete/workflow', () => {
    // WP-MDM-SCOPE-ACCEPT-01 FIX1 (BL-440): Save View and the factory reset belong to the list component now; the page
    // hands it the same view keys and the quick view, and keeps fetching the product's policy itself.
    const js = script();
    const detail = read('Views/MasterDataManagement/ProductLegalEntityScopes/_DetailsQuickView.cshtml');
    const options = loadModule().buildListOptions();
    expect(options.savedView).toMatchObject({ moduleKey: 'MasterDataManagement', pageKey: 'ProductLegalEntityScopes',
      saveViewColumnIndexes: [1, 2, 3], baseOrder: [[1, 'asc']] });
    expect(options.quickView).toMatchObject({ offcanvasId: 'offcanvasDetailsPreview', actionKey: 'quickView' });
    expect(options.filters.resetBtn).toBe('btnFilterReset');
    expect(options.bulk).toBeUndefined();
    expect(js).toContain('fetchPolicy(productId(row))');
    expect(detail.match(/id="offcanvasDetailsPreview"/g)).toHaveLength(1);
    expect([js, detail].join('\n')).not.toMatch(/checkbox|bulk|DELETE|PATCH|WorkCenter|workflow/i);
  });

  it('asks the server for the list in its own protocol and sorts only by what MDM whitelists', () => {
    // WP-MDM-SCOPE-ACCEPT-01 FIX1 (BL-440 list standard): the factory in SERVER mode (it seeds the first request from
    // the saved view); the page's column data names are MDM's own orderBy whitelist, read from MDM's query.
    const options = loadModule().buildListOptions();
    const query = read('../../services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Queries/GetGlobalProductsQuery.cs');
    const whitelist = [...query.slice(query.indexOf('SortableFields'), query.indexOf('};', query.indexOf('SortableFields')))
      .matchAll(/\["([A-Za-z]+)"\]/g)].map(match => match[1]).sort();
    expect(options.dataMode).toBe('server');
    expect(options.ajax.url).toBe('/MasterDataManagement/ProductLegalEntityScopes/api');
    const orderable = options.config.columns
      .filter((column, index) => !options.config.columnDefs.some(def => def.targets === index && def.orderable === false)
        && !(index === options.config.columns.length - 1))
      .map(column => column.data).filter(Boolean).sort();
    expect(orderable).toEqual(whitelist);
    expect(options.filters.fields).toEqual([{ id: 'filterLifecycleStatus', key: 'lifecycleStatus', kind: 'single' }]);
  });

  it('says a failed list read in the page\'s own words and shows the lifecycle by name, never its code', () => {
    const module = loadModule();
    Object.assign(window.L10n, { ErrorProviderUnavailable: 'fixture: provider down', ErrorGateway: 'fixture: gateway',
      LifecycleIdentityApproved: 'fixture: approved', LifecycleRetired: 'fixture: retired' });
    const toasts = [];
    window.showToast = (message, type) => toasts.push([message, type]);
    const options = module.buildListOptions();

    options.ajax.error.call({}, { status: 503 });
    options.ajax.error.call({}, { status: 500 });

    expect(toasts).toEqual([['fixture: provider down', 'error'], ['fixture: gateway', 'error']]);
    expect(module.testHooks.lifecycleText('IdentityApproved')).toBe('fixture: approved');
    expect(module.testHooks.lifecycleText(4)).toBe('fixture: retired');
  });

  it('says a refused write with the sentence the server sends, never a code of its own (BL-598 FIX2 item 1)', () => {
    const module = loadModule();
    Object.assign(window.L10n, { ErrorConflict: 'fixture: conflict', ErrorProviderUnavailable: 'fixture: provider down',
      ErrorGateway: 'fixture: gateway' });
    const say = module.testHooks.failureText;

    // The controller turns MDM's code into the reader's sentence (ProductLegalEntityScopesController.RefusalKeys).
    expect(say({ success: false, errors: ['The product scope store is getting ready.'] }, 503))
      .toBe('The product scope store is getting ready.');
    expect(say({ Errors: ['Another change is being saved.'] }, 409)).toBe('Another change is being saved.');
    // No sentence: the status's own sentence.
    expect(say({ errors: [] }, 409)).toBe('fixture: conflict');
    expect(say({ errors: ['  '] }, 503)).toBe('fixture: provider down');
    expect(say(null, 500)).toBe('fixture: gateway');
  });

  it('shows the companies the reader may not see as a count, never as ids (FIX2 item 10)', () => {
    const module = loadModule();
    Object.assign(window.L10n, { OtherLegalEntities: 'fixture: other companies {0}' });
    const text = module.testHooks.legalEntitiesText;

    expect(text({ legalEntityIds: ['A'], otherLegalEntityCount: 2 })).toBe('A · fixture: other companies 2');
    expect(text({ LegalEntityIds: [], OtherLegalEntityCount: 1 })).toBe('fixture: other companies 1');
    expect(text({ legalEntityIds: ['A', 'B'], otherLegalEntityCount: 0 })).toBe('A, B');
    expect(text(null)).toBe('-');
  });

  it('does not offer replace or end when a persisted policy has no current period', () => {
    const module = loadModule();
    const endedPolicy = { periods: [{ effectiveToUtc: '2026-08-27T00:00:00Z' }] };
    const currentPolicy = { periods: [{ effectiveToUtc: null }] };

    expect(module.testHooks.resolveManageMode(null)).toBe('create');
    expect(module.testHooks.resolveManageMode(endedPolicy)).toBeNull();
    expect(module.testHooks.canEndPolicy(endedPolicy)).toBe(false);
    expect(module.testHooks.resolveManageMode(currentPolicy)).toBe('replace');
    expect(module.testHooks.canEndPolicy(currentPolicy)).toBe(true);
  });

  it('synchronizes Select2 after applying a full-set scoped replacement selection', () => {
    const module = loadModule();
    const select = document.getElementById('legalEntityIds');
    select.add(new Option('LE-A', '11111111-1111-1111-1111-111111111111'));
    select.add(new Option('LE-B', '22222222-2222-2222-2222-222222222222'));
    const trigger = vi.fn();
    window.$ = global.$ = vi.fn(() => ({ trigger }));

    module.testHooks.applyLegalEntitySelection(['22222222-2222-2222-2222-222222222222']);

    expect(select.options[0].selected).toBe(false);
    expect(select.options[1].selected).toBe(true);
    expect(window.$).toHaveBeenCalledWith('#legalEntityIds');
    expect(trigger).toHaveBeenCalledWith('change');
  });

  it('does not expose rollout activation or rollback controls', () => {
    const surface = [
      script(),
      read('Views/MasterDataManagement/ProductLegalEntityScopes/Index.cshtml'),
      read('Views/MasterDataManagement/ProductLegalEntityScopes/_DataTable.cshtml'),
      read('Views/MasterDataManagement/ProductLegalEntityScopes/_CreateEditOffcanvas.cshtml')
    ].join('\n');
    expect(surface).not.toMatch(/activate[_ -]?rollout|rollback[_ -]?rollout|FailClosedSuspended/);
  });

  it('selects create versus replace from current policy state and treats end 202 as non-success', () => {
    const js = script();
    expect(js).toContain('const managePolicy = async row');
    expect(js).toContain('const mode = resolveManageMode(policy)');
    expect(js).toContain('if (mode) return openForm(row, mode)');
    expect(js).toContain('if (!canEndPolicy(policy))');
    expect(js).toContain("className: 'js-scope-manage'");
    expect(js).not.toContain("className: 'js-scope-replace'");
    const end = js.indexOf('const endPolicy = async row');
    const accepted = js.indexOf('response.status === 202', end);
    const success = js.indexOf("list?.reload('RecordSaved')", accepted);
    expect(accepted).toBeGreaterThan(end);
    expect(success).toBeGreaterThan(accepted);
    expect(js.slice(accepted, success)).toContain("L.ReconciliationPending, 'warning'");
    expect(js.slice(accepted, success)).toContain('return;');
  });

  it('keeps seven real locale files in exact parity', () => {
    const locales = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
    const keySets = locales.map(locale => [...read(`Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.${locale}.resx`)
      .matchAll(/<data name="([^"]+)"/g)].map(match => match[1]).sort());
    keySets.slice(1).forEach(keys => expect(keys).toEqual(keySets[0]));
    ['ProductLegalEntityScopesTitle', 'PageDescription', 'AddNewProductLegalEntityScope', 'Actions', 'QuickView']
      .forEach(key => expect(keySets[0]).toContain(key));
    ['Save', 'Cancel', 'Search', 'Apply', 'Reset', 'Filter', 'Export', 'SaveView']
      .forEach(sharedKey => expect(keySets[0]).not.toContain(sharedKey));
    locales.slice(1).forEach((locale, index) => {
      const english = read(`Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.en.resx`);
      const translated = read(`Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.${locale}.resx`);
      expect(translated).not.toEqual(english);
    });
  });

  it('declares a navigation-hidden manifest page with exact six keys', () => {
    const manifest = read('../../services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs');
    expect(manifest).toContain('PageCode: "PRODUCT_LEGAL_ENTITY_SCOPES"');
    expect(manifest).toContain('RoutePath: "/MasterDataManagement/ProductLegalEntityScopes"');
    expect(manifest).toMatch(/PageCode: "PRODUCT_LEGAL_ENTITY_SCOPES"[\s\S]*?IsNavigationVisible: false/);
    [
      'mdm.product-legal-entity-scopes.read', 'mdm.product-legal-entity-scopes.configure',
      'mdm.product-legal-entity-scopes.replace', 'mdm.product-legal-entity-scopes.end',
      'mdm.product-legal-entity-scope-rollout.activate', 'mdm.product-legal-entity-scope-rollout.rollback'
    ].forEach(key => expect(manifest).toContain(key));
  });
});
