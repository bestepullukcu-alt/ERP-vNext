const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

describe('MOD-0355 process improvement local-test surface', () => {
  const controller = read('Controllers/ProcessModelingFrontendController.cs');
  const models = read('Models/ManagementGovernance/ProcessModeling/PerformanceImprovement/PerformanceImprovementFrontendModels.cs');
  const index = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/Index.cshtml');
  const details = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/Details.cshtml');
  const filter = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/_Filter.cshtml');
  const table = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/_DataTable.cshtml');
  const l10n = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/_IndexL10n.cshtml');
  const script = read('wwwroot/assets/js/pages/management-governance/process-modeling/performance-improvement/index.js');
  const l10nScript = read('wwwroot/assets/js/pages/management-governance/process-modeling/performance-improvement/index.l10n.js');

  const messages = {
    Loading: 'loading', Empty: 'empty', Error401: '401', Error403: '403', Error404: 'not-found',
    Error409: '409', Error503: '503', ErrorOffline: 'offline', NotAvailable: 'not-available'
  };

  function installListHarness() {
    const jquery = target => {
      const elements = typeof target === 'string'
        ? Array.from(document.querySelectorAll(target))
        : (target ? [target] : []);
      const api = {
        length: elements.length,
        select2(value) { return value === 'data' ? [] : api; },
        off() { return api; },
        on() { return api; },
        next() { return jquery(null); },
        find() { return jquery(null); },
        data(name) { return elements[0]?.dataset?.[name] || ''; },
        val(value) {
          if (value === undefined) return elements[0]?._testValue || [];
          elements.forEach(element => { element._testValue = value; });
          return api;
        },
        trigger() { return api; }
      };
      return api;
    };
    jquery.fn = {};
    global.$ = window.$ = jquery;

    class TestDataTable {
      static last;

      constructor(_element, options) {
        this.options = options;
        this.data = [];
        this.searchValue = '';
        this.orderValue = [[4, 'asc'], [2, 'asc']];
        this.rows = { add: rows => { this.data = rows.slice(); return this; } };
        this.colReorder = { order: value => value || [0, 1, 2, 3, 4, 5, 6, 7] };
        TestDataTable.last = this;
        options.initComplete?.call({ api: () => this });
      }

      clear() { this.data = []; return this; }
      draw() { this.options.drawCallback?.call({ api: () => this }); return this; }
      search(value) { if (value === undefined) return this.searchValue; this.searchValue = value; return this; }
      columns() { return { indexes: () => ({ toArray: () => [0, 1, 2, 3, 4, 5, 6, 7] }) }; }
      column() { return { visible: () => true }; }
      order(value) { if (value === undefined) return this.orderValue; this.orderValue = value; return this; }
      on() { return this; }
    }

    global.DataTable = window.DataTable = TestDataTable;
    window.DtDefaults = {
      create: options => options,
      exportButtons: () => [],
      updateVisualState: vi.fn(),
      refreshButtonGroupRadii: vi.fn()
    };
    window.requestAnimationFrame = vi.fn();
    global.bootstrap = window.bootstrap = {
      Collapse: { getOrCreateInstance: () => ({ hide: vi.fn(), toggle: vi.fn() }) }
    };
    return TestDataTable;
  }

  function bootList(fetchResult) {
    document.body.innerHTML = '<div id="performanceImprovementStatus" class="alert d-none" role="status" aria-live="polite" data-improvement-state="idle"></div>'
      + '<div id="inlineFilterHost"></div><div id="inlineFilterCollapse"></div>'
      + '<select id="filterLifecycle" data-placeholder="lifecycle"></select>'
      + '<button id="btnFilterApply"></button><button id="btnFilterReset"></button>'
      + '<div id="skeleton-loader"></div><table id="dt-performance-improvements"></table>'
      + '<div id="performanceImprovementContext" data-mode="index" data-ready="true" data-read="true"></div>';
    const TestDataTable = installListHarness();
    window.PerformanceImprovementL10n = messages;
    window.personalizationClient = undefined;
    window.fetch = vi.fn(fetchResult);
    window.eval(script);
    return { status: document.getElementById('performanceImprovementStatus'), TestDataTable };
  }

  const settle = () => new Promise(resolve => setTimeout(resolve, 0));

  async function executeDetails(fetchResult) {
    document.body.innerHTML = '<div id="performanceImprovementStatus" class="alert d-none" data-improvement-state="idle"></div>'
      + '<div id="performanceImprovementDetail"></div>'
      + '<div id="performanceImprovementContext" data-mode="details" data-ready="true" data-id="11111111-1111-4111-8111-111111111111"></div>';
    window.PerformanceImprovementL10n = messages;
    window.fetch = vi.fn(fetchResult);
    window.eval(script);
    await new Promise(resolve => setTimeout(resolve, 0));
    return document.getElementById('performanceImprovementStatus');
  }

  it('owns exact same-origin read routes and remains default-off', () => {
    expect(controller).toContain('[HttpGet("improvements")]');
    expect(controller).toContain('[HttpGet("improvements/{id:guid}")]');
    expect(controller).toContain('[HttpGet("api/improvements")]');
    expect(controller).toContain('[HttpGet("api/improvements/{id:guid}")]');
    expect(index).toContain('data-improvement-state="default-off"');
    expect(index).toContain('Layout = "_LayoutTenantShell"');
    expect(details).toContain('Layout = "_LayoutTenantShell"');
  });

  it('uses exact six owner permissions without wildcard alias or normalization', () => {
    const permissions = [...models.matchAll(/public const string \w+ = "([^"]+)";/g)].map(x => x[1]);
    expect(permissions).toHaveLength(6);
    expect(new Set(permissions).size).toBe(6);
    expect(permissions.every(x => x.startsWith('management-governance.process-modeling.improvements.') && x === x.trim() && !x.includes('*'))).toBe(true);
  });

  it('uses the v2 table, same-origin fetch and no direct service or token handling', () => {
    expect(index).toContain('_Filter.cshtml');
    expect(index).toContain('_DataTable.cshtml');
    expect(index).toContain('_IndexL10n.cshtml');
    expect(index).toContain('index.l10n.js');
    expect(filter).toContain('id="inlineFilterCollapse"');
    expect(table).toContain('id="skeleton-loader"');
    expect(table).toContain('data-dt-standard="v2"');
    expect(table).toContain('dt-checkboxes-select-all');
    expect(script).toContain("'/management-governance/process-modeling/api/improvements'");
    expect(script).toContain('window.DtDefaults.create');
    expect(script).toContain('new DataTable(tableElement, options)');
    expect(script).toContain('loadDefaultView');
    expect(script).toContain('saveView');
    expect(script).toContain('colReorder');
    expect(script).toContain("columns: ':gt(1):not(:last-child)'");
    expect(script).toContain('syncMultiSelectSummary');
    expect(script).toContain('factoryView');
    expect(l10n).toContain('SharedLocalizer["Loading"]');
    expect(l10nScript).toContain('PerformanceImprovementL10n');
    expect([index, details, filter, table, l10n, script, l10nScript].join('\n'))
      .not.toMatch(/localhost|:5017|Authorization|X-Tenant-Id|document\.cookie|localStorage|sessionStorage/i);
    expect(controller).not.toMatch(/localhost|:5017|X-Tenant-Id|document\.cookie|localStorage|sessionStorage/i);
  });

  it('keeps details zero-read and removes the permanent loading state while default-off', () => {
    expect(details).toContain('@if (Model.GatewayReady)');
    expect(details).toContain('data-improvement-state="default-off"');
    expect(script).toContain("if (!host || !gatewayReady) return;");
  });

  it('executes exact zero browser reads and writes while default-off', async () => {
    document.body.innerHTML = '<div id="performanceImprovementStatus" class="alert d-none" data-improvement-state="idle"></div>'
      + '<div id="performanceImprovementContext" data-mode="index" data-ready="false" data-read="true"></div>';
    window.PerformanceImprovementL10n = messages;
    window.fetch = vi.fn();
    window.eval(script);
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(window.fetch).toHaveBeenCalledTimes(0);
    expect(script).not.toMatch(/method\s*:\s*['"](?:POST|PUT|PATCH|DELETE)['"]/i);
  });

  it.each([
    [403, 'error-403'],
    [404, 'error-404'],
    [409, 'error-409'],
    [503, 'error-503']
  ])('renders HTTP %s as its own non-disclosing state', async (status, expectedState) => {
    const host = await executeDetails(() => Promise.resolve({ ok: false, status }));
    expect(host.dataset.improvementState).toBe(expectedState);
    expect(host.textContent).toBe(messages[`Error${status}`]);
    expect(host.textContent).not.toMatch(/tenant|foreign|başka kiracı/i);
  });

  it('renders transport failure as offline rather than authoritative 503', async () => {
    const host = await executeDetails(() => Promise.reject(new TypeError('network')));
    expect(host.dataset.improvementState).toBe('offline');
    expect(host.textContent).toBe(messages.ErrorOffline);
  });

  it('announces loading before the list read completes', async () => {
    const pending = new Promise(() => {});
    const { status } = bootList(() => pending);
    await Promise.resolve();
    await Promise.resolve();
    expect(status.dataset.improvementState).toBe('loading');
    expect(status.textContent).toBe(messages.Loading);
    expect(status.getAttribute('aria-live')).toBe('polite');
    expect(window.fetch).toHaveBeenCalledTimes(1);
  });

  it('renders an authoritative empty list as empty', async () => {
    const { status, TestDataTable } = bootList(() => Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ data: [] })
    }));
    await settle();
    await settle();
    expect(status.dataset.improvementState).toBe('empty');
    expect(status.textContent).toBe(messages.Empty);
    expect(TestDataTable.last.data).toEqual([]);
    expect(TestDataTable.last.options.language.emptyTable).toBe(messages.Empty);
  });

  it('renders populated authoritative rows without an empty or error state', async () => {
    const row = { id: '11111111-1111-4111-8111-111111111111', title: 'Measured improvement' };
    const { status, TestDataTable } = bootList(() => Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ data: [row] })
    }));
    await settle();
    await settle();
    expect(status.dataset.improvementState).toBe('ready');
    expect(status.textContent).toBe('');
    expect(status.classList.contains('d-none')).toBe(true);
    expect(TestDataTable.last.data).toEqual([row]);
  });

  it.each([
    [403, 'error-403', messages.Error403],
    [404, 'error-404', messages.Error404],
    [409, 'error-409', messages.Error409],
    [503, 'error-503', messages.Error503]
  ])('keeps list HTTP %s distinct from an authoritative empty result', async (httpStatus, state, message) => {
    const { status, TestDataTable } = bootList(() => Promise.resolve({ ok: false, status: httpStatus }));
    await settle();
    await settle();
    expect(status.dataset.improvementState).toBe(state);
    expect(status.textContent).toBe(message);
    expect(TestDataTable.last.options.language.emptyTable).toBe(message);
    expect(TestDataTable.last.options.language.emptyTable).not.toBe(messages.Empty);
  });

  it('uses localized no-value text and accessible status/filter controls without English fallback', () => {
    expect(index).toContain('role="status"');
    expect(index).toContain('aria-live="polite"');
    expect(details).toContain('aria-live="polite"');
    expect(filter).toContain('type="button"');
    expect(filter).toContain('type="reset"');
    expect(l10n).toContain('SharedLocalizer["NotAvailable"]');
    expect(script).toContain('escape(L.NotAvailable)');
    expect(script).not.toContain("L.SaveView || 'Default'");
    expect(script).not.toContain("|| '-'");
  });

  it('has exact locale key parity across seven cultures', () => {
    const base = 'Resources/Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/PerformanceImprovementIndex';
    const keys = culture => [...read(`${base}.${culture}.resx`).matchAll(/<data name="([^"]+)"/g)].map(x => x[1]).sort();
    const expected = keys('en');
    expect(expected.length).toBeGreaterThan(20);
    expect(expected).toContain('ErrorOffline');
    expect(expected).toEqual(expect.arrayContaining(['StateOpen', 'StateQualified', 'StateClosed']));
    for (const culture of ['fr', 'es', 'zh', 'ar', 'ru', 'tr']) expect(keys(culture)).toEqual(expected);
  });
});
