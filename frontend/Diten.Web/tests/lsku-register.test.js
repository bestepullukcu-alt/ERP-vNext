const fs = require('fs');
const path = require('path');

describe('MOD-0290 LSKU Exposure D', () => {
  const root = path.join(__dirname, '..');
  const read = relativePath => fs.readFileSync(path.join(root, relativePath), 'utf8');
  const script = () => read('wwwroot/assets/js/MasterDataManagement/Lskus/index.js');

  it('uses only same-origin MVC proxy routes in the browser', () => {
    const source = script();
    expect(source).toContain("const endpoint = '/MasterDataManagement/Lskus/api';");
    expect(source).toContain("const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });");
    expect(source).not.toMatch(/window\.API|localhost:5000|:5059|Authorization|Bearer|X-Tenant-Id/i);
  });

  it('maps the four exposure operations plus submit/retire and no mutation extras', () => {
    const controller = read('Controllers/LskusController.cs');
    expect(controller).toContain('[Route("MasterDataManagement/Lskus")]');
    ['/api/lskus', '/api/lskus/{id:D}', '/api/lskus/create-options', '/api/lskus/drafts']
      .forEach(route => expect(controller).toContain(route));
    ['[HttpPost("api/{id:guid}/submit")]', '[HttpPost("api/{id:guid}/retire")]']
      .forEach(route => expect(controller).toContain(route));
    expect(controller).not.toMatch(/HttpPut|HttpPatch|HttpDelete|reservation|selector|approve|reject/i);
  });

  it('posts exactly two business fields plus protected form metadata', () => {
    const source = script();
    expect(source).toContain("body.set('GskuId', gskuId)");
    expect(source).toContain("body.set('MarketCode', marketCode)");
    expect(source).toContain("body.set('FormAttemptToken'");
    expect(source).toContain("body.set('__RequestVerificationToken'");
    expect(source).toContain('headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken }');
    expect(source).not.toMatch(/body\.set\(['"](?:TenantId|IdempotencyKey|CanonicalCode|ReservationId|Credential)/);
  });

  it('preserves the 202 token and rotates it only after 201', () => {
    const source = script();
    const accepted = source.indexOf('response.status === 202');
    const created = source.indexOf('response.status !== 201');
    const rotation = source.indexOf("document.getElementById('formAttemptToken').value = nextToken");
    expect(accepted).toBeGreaterThan(-1);
    expect(created).toBeGreaterThan(accepted);
    expect(rotation).toBeGreaterThan(created);
    expect(source.slice(accepted, created)).not.toContain("formAttemptToken').value");
  });

  it('renders real quick detail into the single approved offcanvas', () => {
    const source = script();
    const index = read('Views/MasterDataManagement/Lskus/Index.cshtml');
    const details = read('Views/MasterDataManagement/Lskus/_DetailsQuickView.cshtml');
    expect(source).toContain("const quickView = event.target.closest('.js-quick-view')");
    expect(source).toContain("const lifecycleAction = event.target.closest('.js-submit-identity, .js-retire-identity')");
    expect(source).toContain("action.closest('.datatables-lskus')");
    expect(source).toContain('fetch(`${endpoint}/${encodeURIComponent(id)}`');
    expect(details).toContain('id="offcanvasDetailsPreview"');
    expect(details.match(/id="offcanvasDetailsPreview"/g)).toHaveLength(1);
    expect(index).not.toMatch(/<div[^>]+id="offcanvasDetailsPreview"/);
    expect(index).toContain('is rendered by the real details partial');
  });

  it('tracks Save View search, ordering, visibility and ColReorder state', () => {
    const source = script();
    expect(source).toContain('personalizationClient.saveView(payload)');
    expect(source).toContain('record?.viewDefinition ?? record?.ViewDefinition');
    expect(source).toContain('viewDefinition: normalized');
    expect(source).not.toMatch(/record\?\.configuration|configuration: JSON\.stringify/);
    expect(source).toMatch(/viewName: \([^\n]+\|\| L\.SaveView \|\| 'Default'\)/);
    expect(source).toContain('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt');
    expect(source).toContain('setSaveFilterVisible(isDirtyComparedToDefault(dt))');
  });

  it('factory reset restores search, visibility, column order and base order', () => {
    const source = script();
    expect(source).toMatch(/const getResetBaselineState = \(\) => normalizeView\([\s\S]*?filters: emptyFilters\(\)[\s\S]*?search: ''[\s\S]*?colVis: defaultColVis\(\)[\s\S]*?columnOrder:[\s\S]*?order: baseOrder/);
    expect(source).toMatch(/btnFilterReset[\s\S]*?event\.preventDefault\(\)[\s\S]*?applySavedTableState\(dt, getResetBaselineState\(\)\)[\s\S]*?dt\.draw\(\)[\s\S]*?setSaveFilterVisible\(isDirtyComparedToDefault\(dt\)\)/);
  });

  it('keeps create permission DOM-conditional and uses the tenant shell', () => {
    const index = read('Views/MasterDataManagement/Lskus/Index.cshtml');
    expect(index).toContain('Layout = "_LayoutTenantShell"');
    expect(index).toContain('@if (canCreate)');
    expect(index).toContain('data-can-submit="@canSubmit.ToString().ToLowerInvariant()"');
    expect(index).toContain('data-can-retire="@canRetire.ToString().ToLowerInvariant()"');
    expect(index).toContain('id="lskuLifecycleToken"');
  });

  it('guards lifecycle mutations with strict server-owned identity and envelope rules', () => {
    const controller = read('Controllers/LskusController.cs');
    expect(controller).toContain('private const string SubmitPermission = "mdm.lskus.submit"');
    expect(controller).toContain('private const string RetirePermission = "mdm.lskus.retire"');
    expect(controller).toContain('HasOnlyFormFieldsAsync(isRetire ? ["ExpectedVersion", "ReasonCode"] : ["ExpectedVersion"])');
    expect(controller).toContain('TryResolveLifecycleIdentity(out var tenantId, out var actor)');
    expect(controller).toContain('tenantId, actor, "lsku", action, id, expectedVersion.Value');
    expect(controller).toContain('AppendLengthPrefixed(hash, tenantId.ToString("D"))');
    expect(controller).toContain('request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"))');
    expect(controller).toContain('envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus');
    expect(controller).toContain('new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted }');
    expect(controller).toContain('tenantId, actor, "lsku", action, id, expectedVersion.Value');
    expect(controller).toContain('TryResolveCanonicalTenant(User, out tenantId)');
    expect(controller).toContain('Guid.TryParseExact(value, "D", out var parsed)');
    expect(controller).toContain('canonical.HasValue && canonical.Value != parsed');
  });

  it('uses fresh detail fencing, bounded Premium reason input and exact state actions', () => {
    const source = script();
    expect(source).toContain('const detail = await fetchDetail(id)');
    expect(source).toContain('const verified = validateDetail(detail, id)');
    expect(source).toContain("const expectedState = action === 'submit' ? 1 : 3");
    expect(source).toContain('const expectedVersion = verified.version');
    expect(source).toContain("body.set('ExpectedVersion', String(expectedVersion))");
    expect(source).toContain('const refreshedDetail = await fetchDetail(id)');
    expect(source).toContain("refreshedState !== (action === 'submit' ? 2 : 4)");
    expect(source).toContain("showInput: true");
    expect(source).toContain("inputAttributes: { maxlength: '128', rows: '3' }");
    expect(source).toContain('reason.length > 128');
    expect(source).toContain('lifecycleRequests.has(requestKey)');
    expect(source).toContain('state === 1 && canSubmit');
    expect(source).toContain('state === 3 && canRetire');
    expect(source).toContain('if (response.status === 401) handleUnauthorized()');
    expect(source).toContain('window.DtDefaults?.handleUnauthorized?.()');
    expect(source).toContain('if (!exception?.authHandled)');
    expect(source).toContain('if (exception?.authHandled) return;');
    expect(source).not.toMatch(/js-approve|js-reject|\/approve|\/reject|crypto\.randomUUID|localhost:50(?:57|59)/i);
  });

  it('executes the retirement reason normalizer used by the retire callback', () => {
    const source = script();
    const expression = source.match(/const normalizeRetirementReason = ([^;]+);/)?.[1];
    expect(expression).toBeTruthy();
    const normalizeRetirementReason = Function(`return (${expression});`)();
    expect(normalizeRetirementReason('  lifecycle retirement  ')).toBe('lifecycle retirement');
    expect(normalizeRetirementReason('   ')).toBe('');
    expect(normalizeRetirementReason(null)).toBe('');
    expect(source).toContain('const reason = normalizeRetirementReason(input)');
  });

  it('has seven locale files with exact key parity and no forbidden inert keys', () => {
    const locales = ['en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr'];
    const keys = locales.map(locale => [...read(`Resources/Views/MasterDataManagement/Lskus/LskusIndex.${locale}.resx`)
      .matchAll(/<data name="([^"]+)"/g)].map(match => match[1]).sort());
    keys.slice(1).forEach(localeKeys => expect(localeKeys).toEqual(keys[0]));
    expect(keys[0]).toHaveLength(52);
    ['Unknown', 'Actions', 'ViewDetails', 'QuickView', 'Cancel', 'Search', 'Export', 'Filter',
      'Apply', 'Reset', 'SaveView', 'ColumnVisibility', 'Status', 'PageDescription']
      .forEach(key => expect(keys[0]).toContain(key));
    ['Active', 'Passive', 'Edit', 'BulkDelete', 'BulkDeleteConfirm', 'AreYouSure', 'Import', 'ShowAll']
      .forEach(key => expect(keys[0]).not.toContain(key));
  });

  it('does not expose edit, delete, bulk, checkbox, credentials or browser UUID generation', () => {
    const browserSurface = [
      script(),
      read('Views/MasterDataManagement/Lskus/Index.cshtml'),
      read('Views/MasterDataManagement/Lskus/_DataTable.cshtml'),
      read('Views/MasterDataManagement/Lskus/_CreateEditOffcanvas.cshtml')
    ].join('\n');
    expect(browserSurface).not.toMatch(/bulk|js-edit|btnDelete|dt-checkboxes|crypto\.randomUUID|Idempotency-Key|TenantId|reservation/i);
  });
});
