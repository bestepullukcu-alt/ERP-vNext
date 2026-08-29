const fs = require('fs');
const path = require('path');
const { loadScript } = require('./load-script');

describe('MOD-0354 DWS frontend contract', () => {
    const root = path.join(__dirname, '..');
    const read = (relative) => fs.readFileSync(path.join(root, relative), 'utf8');
    const controller = () => read('Controllers/DwsStructuresFrontendController.cs');
    const proxy = () => read('Services/ManagementGovernance/Dws/DwsFrontendGatewayProxy.cs');
    const view = () => read('Views/ManagementGovernance/Dws/Index.cshtml');
    const script = () => read('wwwroot/assets/js/pages/management-governance/dws/index.js');

    it('binds the localization partial by its module-owned absolute view path', () => {
        expect(view()).toContain('<partial name="~/Views/ManagementGovernance/Dws/_IndexL10n.cshtml" />');
    });

    it('owns only the canonical tenant route and preserves the legacy comparison URL', () => {
        expect(controller()).toContain('[Route("management-governance/delivery-execution/structures")]');
        expect(view()).toContain('href="/DecompositionTreeBuilder"');
        expect(view()).toContain('Layout = "_LayoutTenantShell"');
        expect(controller()).not.toContain('DecompositionTreeBuilderController');
    });

    it('keeps browser calls same-origin and read-only', () => {
        const source = script();
        expect(source).toContain("const endpoint = '/management-governance/delivery-execution/structures/api'");
        expect(source).toContain("method: 'GET', credentials: 'same-origin'");
        expect(source).not.toMatch(/localhost|:5017|document\.cookie|access_token|Authorization|X-Tenant-Id/);
        expect(source).not.toMatch(/method:\s*['"](?:POST|PUT|PATCH|DELETE)/);
        expect(controller()).not.toMatch(/\[Http(?:Post|Put|Patch|Delete)/);
    });

    it('uses exactly the six governed permissions and exposes only three read-first controls', () => {
        const model = read('Models/ManagementGovernance/Dws/DwsFrontendModels.cs');
        const keys = [...model.matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1]);
        expect(keys).toEqual([
            'management-governance.dws.create', 'management-governance.dws.update',
            'management-governance.dws.read', 'management-governance.dws.validate',
            'management-governance.dws.compare', 'management-governance.dws.baseline'
        ]);
        expect([...view().matchAll(/data-permission="([^"]+)"/g)].map((match) => match[1])).toEqual([
            'management-governance.dws.read', 'management-governance.dws.validate', 'management-governance.dws.compare'
        ]);
    });

    it('fails closed before any Gateway read while default-off', () => {
        const source = proxy();
        expect(source.indexOf('if (!IsEnabled)')).toBeLessThan(source.indexOf('httpClient.SendAsync'));
        expect(source).toContain('dws_frontend_gateway_not_ready');
        expect(source).toContain('StatusCodes.Status503ServiceUnavailable');
    });

    it('resolves the HttpOnly token, tenant and correlation only in the MVC proxy', () => {
        const source = proxy();
        expect(source).toContain('Auth.AuthTokenCookies.GetAccessToken(context.Request)');
        expect(source).toContain('request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token)');
        expect(source).toContain('request.Headers.TryAddWithoutValidation("X-Tenant-Id"');
        expect(source).toContain('request.Headers.TryAddWithoutValidation(CorrelationHeader');
        expect(source).toContain('Guid.TryParse(tenantId');
    });

    it('ships marker-matched key parity for exactly seven locales', () => {
        const resourceDir = path.join(root, 'Resources/Views/ManagementGovernance/Dws');
        const files = fs.readdirSync(resourceDir).filter((name) => /^DwsIndex\.[a-z]{2}\.resx$/.test(name)).sort();
        expect(files).toEqual(['ar', 'en', 'es', 'fr', 'ru', 'tr', 'zh'].map((locale) => `DwsIndex.${locale}.resx`).sort());
        const keys = (name) => [...read(`Resources/Views/ManagementGovernance/Dws/${name}`).matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]).sort();
        files.forEach((name) => expect(keys(name)).toEqual(keys('DwsIndex.en.resx')));
    });

    it('makes default-off a browser-visible zero-fetch state', () => {
        document.body.innerHTML = `
          <main id="dws-workspace" data-gateway-enabled="false"><div id="dws-state"><b id="dws-state-title"></b><span id="dws-state-detail"></span></div>
          <input id="dws-structure-id"><input id="dws-revision-number"><button id="dws-load" data-permission="read"></button>
          <div id="dws-tree"></div><span id="dws-node-code"></span><span id="dws-node-title"></span><span id="dws-node-description"></span><span id="dws-node-order"></span></main>`;
        window.DwsL10n = { Unavailable: 'not-ready', Empty: 'empty' };
        global.fetch = vi.fn();
        loadScript('wwwroot/assets/js/pages/management-governance/dws/index.js');
        document.getElementById('dws-load').click();
        expect(global.fetch).not.toHaveBeenCalled();
        expect(document.getElementById('dws-state-detail').textContent).toBe('not-ready');
        expect(document.getElementById('dws-load').disabled).toBe(true);
    });

    it('reads camel-case localization JSON without relying on the bootstrap script', () => {
        document.body.innerHTML = `
          <script id="dws-l10n" type="application/json">{"unavailable":"hazır değil","empty":"boş"}</script>
          <main id="dws-workspace" data-gateway-enabled="false"><div id="dws-state"><b id="dws-state-title"></b><span id="dws-state-detail"></span></div>
          <input id="dws-structure-id"><input id="dws-revision-number"><button id="dws-load" data-permission="read"></button>
          <div id="dws-tree"></div><span id="dws-node-code"></span><span id="dws-node-title"></span><span id="dws-node-description"></span><span id="dws-node-order"></span></main>`;
        delete window.DwsL10n;
        global.fetch = vi.fn();
        loadScript('wwwroot/assets/js/pages/management-governance/dws/index.js');
        document.getElementById('dws-load').click();
        expect(global.fetch).not.toHaveBeenCalled();
        expect(document.getElementById('dws-state-title').textContent).toBe('hazır değil');
        expect(document.getElementById('dws-state-detail').textContent).toBe('hazır değil');
    });

    it('implements keyboard tree navigation and RTL-safe logical CSS', () => {
        expect(script()).toMatch(/ArrowDown|ArrowUp|ArrowLeft|ArrowRight|Home|End/);
        const css = read('wwwroot/assets/css/pages/management-governance/dws.css');
        expect(css).toMatch(/margin-inline-start|padding-inline-start/);
        expect(css).toContain('[dir="rtl"]');
    });
});
