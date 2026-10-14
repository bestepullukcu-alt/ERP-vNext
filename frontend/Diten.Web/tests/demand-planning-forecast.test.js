const fs = require('fs');
const path = require('path');
const read = file => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');
const tick = () => new Promise(resolve => setTimeout(resolve, 0));
const company = '11111111-1111-4111-8111-111111111111';
const cycle = '22222222-2222-4222-8222-222222222222';
const sku = '33333333-3333-4333-8333-333333333333';
const revision = '44444444-4444-4444-8444-444444444444';
const methods = ['Naive', 'MovingAverage', 'SimpleExponentialSmoothing', 'HoltTrend',
    'SeasonalNaive', 'HoltWinters', 'BiasCorrectedCroston'];
const resource = locale => new DOMParser().parseFromString(read(
    'Resources/Views/SupplyChain/DemandPlanning/DemandPlanningIndex.' + locale + '.resx'), 'application/xml');
const labels = () => Object.fromEntries([...resource('en').querySelectorAll('data')]
    .map(item => [item.getAttribute('name'), item.querySelector('value').textContent]));
const ok = data => ({ ok: true, status: 200, json: async () => ({ data, isSuccessful: true }) });
const companies = ok([{ legalEntityId: company, displayName: '<Company>' }]);
function evaluation() {
    return { planningCycleId: cycle, skuId: sku, warehouseId: 'WH-1', policyVersion: 'approved',
        candidates: methods.map((method, i) => ({ method, eligible: i < 4,
            reason: i < 4 ? 'Eligible' : 'InsufficientReliableWeeks', reliableWeeks: 26,
            realDemandEvents: 1, wapeRatio: null, mae: 0, bias: 0, mase: null,
            forecastUomId: 'CASE', unitVerificationPending: true,
            byWeeksAhead: [{ weeksAhead: 1, count: 4, wapeRatio: null, mae: 0, bias: 0, mase: null }] })) };
}
function mount(fetchMock, canCreate = true) {
    // Read actual Razor markup so identifier drift breaks behavior tests.
    let markup = read('Views/SupplyChain/DemandPlanning/_Forecast.cshtml');
    markup = markup.slice(markup.indexOf('<main'))
        .replace(/data-can-create-draft="[^"]*"/, 'data-can-create-draft="' + canCreate + '"')
        .replace(/@if \(canCreateDraft\)\s*\{/g, '').replace(/^\s*\}\s*$/gm, '')
        .replace('@Html.AntiForgeryToken()', '<input name="__RequestVerificationToken" value="test-csrf" type="hidden">')
        .replace(/@Localizer\["([^"]+)"\]/g, (_, key) => labels()[key]);
    document.body.innerHTML = markup;
    if (!canCreate) document.getElementById('forecastCommand').remove();
    window.L10n = labels(); window.showToast = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    new Function(read('wwwroot/assets/js/SupplyChain/demand-planning-forecast.js'))();
}
const id = name => document.getElementById('forecast' + name);
async function selectScope() {
    await tick();
    id('Company').value = company; id('Cycle').value = cycle;
    id('Sku').value = sku; id('Warehouse').value = 'WH-1'; id('Version').value = '0';
    id('Scope').dispatchEvent(new Event('submit', { cancelable: true })); await tick();
}
async function create() {
    id('Command').dispatchEvent(new Event('submit', { cancelable: true })); await tick();
}

describe('MOD-0188 API-backed forecast workspace', () => {
    afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ''; delete window.L10n; });
    it('shows API order, ineligibility, undefined WAPE and no default method', async () => {
        const fetchMock = vi.fn().mockResolvedValueOnce(companies).mockResolvedValueOnce(ok(evaluation()));
        mount(fetchMock); await selectScope();
        expect(fetchMock.mock.calls[1][0]).toContain('/api/forecast-runs/evaluation?');
        expect(fetchMock.mock.calls[1][0]).toContain('legalEntityId=' + company);
        expect([...id('Candidates').querySelectorAll('h3')].map(item => item.textContent))
            .toEqual(methods.map(method => labels()['ForecastMethod' + method]));
        expect(id('Candidates').textContent).toContain(labels().ForecastInsufficientWeeks);
        expect(id('Candidates').textContent).toContain(labels().ForecastUndefined);
        expect(id('Candidates').textContent).toContain(labels().HistoryUnitPending);
        expect(id('Method').value).toBe('');
        expect([...id('Method').options].map(item => item.value)).not.toContain('HoltWinters');
        expect(id('Company').options[1].textContent).toBe('<Company>');
        expect(id('Company').querySelector('Company')).toBeNull();
    });
    it('requires explicit selection and reason, sends only expected inputs and preserves retry key', async () => {
        const fetchMock = vi.fn().mockResolvedValueOnce(companies).mockResolvedValueOnce(ok(evaluation()))
            .mockResolvedValueOnce({ ok: false, status: 503 })
            .mockResolvedValue(ok({ draft: { revisionId: revision } }));
        mount(fetchMock); await selectScope();
        await create(); expect(fetchMock).toHaveBeenCalledTimes(2);
        id('Method').value = 'Naive'; await create(); expect(fetchMock).toHaveBeenCalledTimes(2);
        id('Reason').value = 'Explicit choice'; await create();
        expect(id('Created').hidden).toBe(true);
        const request = fetchMock.mock.calls[2][1];
        expect(request.headers.RequestVerificationToken).toBe('test-csrf');
        expect(JSON.parse(request.body)).toEqual({ planningCycleId: cycle, skuId: sku,
            warehouseId: 'WH-1', method: 'Naive', reason: 'Explicit choice', expectedCycleVersion: 0 });
        expect(request.credentials).toBe('same-origin');
        await create();
        expect(fetchMock.mock.calls[3][1].headers['Idempotency-Key']).toBe(request.headers['Idempotency-Key']);
        expect(id('Created').hidden).toBe(false);
        expect(window.showToast).toHaveBeenCalledWith(labels().ForecastCreated, 'success');
    });
    it('closes selection and Draft after POST permission loss with a persistent next step', async () => {
        const fetchMock = vi.fn().mockResolvedValueOnce(companies).mockResolvedValueOnce(ok(evaluation()))
            .mockResolvedValueOnce({ ok: false, status: 403 });
        mount(fetchMock); await selectScope(); id('Method').value = 'Naive'; id('Reason').value = 'Choice';
        await create();
        const root = document.getElementById('demandForecast');
        expect(root.children.length).toBe(1);
        expect(root.querySelector('form, section, h1, button')).toBeNull();
        expect(root.textContent).toBe(labels().ForecastPermissionLost);
    });
    it('removes the entire action surface when evaluation authorization is lost', async () => {
        const fetchMock = vi.fn().mockResolvedValueOnce(companies)
            .mockResolvedValueOnce({ ok: false, status: 403 });
        mount(fetchMock); await selectScope();
        const root = document.getElementById('demandForecast');
        expect(root.children.length).toBe(1);
        expect(root.querySelector('form, section, h1, button')).toBeNull();
        expect(root.textContent).toBe(labels().ForecastPermissionLost);
    });
    it('reads persisted Draft over API and reveals all 52 weeks', async () => {
        const weeks = Array.from({ length: 52 }, (_, i) => ({ weekStart: 'week-' + i,
            weekEnd: 'end-' + i, quantity: i, methodProvenance: { unitVerificationPending: true } }));
        const fetchMock = vi.fn().mockResolvedValueOnce(companies).mockResolvedValueOnce(ok(evaluation()))
            .mockResolvedValueOnce(ok({ draft: { revisionId: revision } }))
            .mockResolvedValueOnce(ok({ revisionId: revision, createdBy: 'actor', createdAt: 'UTC',
                creationReason: '<Explicit reason>', series: [{ baseUomId: 'CASE', weeks }] }));
        mount(fetchMock); await selectScope(); id('Method').value = 'Naive'; id('Reason').value = 'Choice';
        await create(); id('OpenDraft').click(); await tick();
        expect(fetchMock.mock.calls[3][0]).toContain('/api/revisions/' + revision);
        expect(id('DraftWeeks').children.length).toBe(13); id('AllWeeks').click();
        expect(id('DraftWeeks').children.length).toBe(52); expect(id('DraftUnit').hidden).toBe(false);
        expect(id('DraftInfo').textContent).toContain('<Explicit reason>');
        expect(id('DraftInfo').children.length).toBe(0);
    });
    it('discards a stale response after a scope change', async () => {
        let resolve;
        const fetchMock = vi.fn().mockResolvedValueOnce(companies)
            .mockImplementationOnce(() => new Promise(done => { resolve = done; }));
        mount(fetchMock); await selectScope();
        id('Warehouse').value = 'WH-2'; id('Warehouse').dispatchEvent(new Event('input'));
        resolve(ok(evaluation())); await tick();
        expect(id('Evaluation').hidden).toBe(true); expect(id('Candidates').children.length).toBe(0);
    });
    it('keeps scope form closed on company outage without a fallback', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 503 });
        mount(fetchMock); await tick();
        expect(id('Scope').hidden).toBe(true); expect(fetchMock).toHaveBeenCalledTimes(1);
        expect(id('Status').textContent).toBe(labels().ForecastUnavailable);
    });
    it('allows evaluation but no create form without Draft creation permission', async () => {
        const fetchMock = vi.fn().mockResolvedValueOnce(companies).mockResolvedValueOnce(ok(evaluation()));
        mount(fetchMock, false); await selectScope();
        expect(id('Candidates').children.length).toBe(7); expect(id('Command')).toBeNull();
    });
    it('gates Razor and scripts, preserves Gateway server scope and CSRF', () => {
        const index = read('Views/SupplyChain/DemandPlanning/Index.cshtml');
        const proxy = read('Controllers/DemandPlanningController.Forecast.cs');
        const script = read('wwwroot/assets/js/SupplyChain/demand-planning-forecast.js');
        expect(index).toContain('!canRead || (!preview && ViewData["CanCreateRun"] as bool? != true)');
        expect(index).toContain('<partial name="_AccessDenied"');
        expect(index).toContain('canRead && !preview && ViewData["CanCreateRun"] as bool? == true');
        expect(proxy).toContain('[ValidateAntiForgeryToken]');
        expect(proxy).toContain('PermissionClaims.HasPermission(User, "demand.drafts.create")');
        expect(proxy).toContain('_gatewayUrl + path'); expect(proxy).toContain('request.Headers.Add("X-Tenant-Id"');
        expect(script).not.toMatch(/5068|document.cookie|Authorization|fixture|\.sort\(/);
    });
    it('provides complete seven-language method and UI text families', () => {
        const script = read('wwwroot/assets/js/SupplyChain/demand-planning-forecast.js')
            .replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
        const keys = [...new Set([...script.matchAll(/'((?:Forecast|History)[A-Za-z]+|ReasonRequired|ShowAllWeeks|FirstThirteen|NoCompanyData)'/g)]
            .map(match => match[1]).filter(key => key !== 'ForecastMethod')
            .concat(methods.map(method => 'ForecastMethod' + method))
            .concat([...read('Views/SupplyChain/DemandPlanning/_Forecast.cshtml').matchAll(/@Localizer\["([^"]+)"\]/g)]
                .map(match => match[1])))];
        for (const locale of ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru']) {
            const xml = resource(locale); expect(xml.querySelector('parsererror')).toBeNull();
            keys.forEach(key => expect(xml.querySelector('data[name="' + key + '"] value')?.textContent.trim(),
                locale + ': ' + key).toBeTruthy());
        }
        expect(read('Views/SupplyChain/DemandPlanning/_ForecastL10n.cshtml')).toContain('GetAllStrings(true)');
    });
});