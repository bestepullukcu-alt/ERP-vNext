const { loadScript } = require('./load-script');
const fs = require('fs');
const path = require('path');

describe('shared personalization client', () => {
    const expectBrowserSafeProxyRequest = (options, contentType = null) => {
        expect(options).toEqual(expect.objectContaining({ credentials: 'include' }));
        expect(options.headers).not.toHaveProperty('X-Tenant-Id');
        expect(options.headers).not.toHaveProperty('Authorization');
        expect(options.headers).not.toHaveProperty('authorization');
        expect(Object.keys(options.headers)).toEqual(contentType ? ['Content-Type'] : []);
        if (contentType) {
            expect(options.headers).toEqual({ 'Content-Type': contentType });
        }
    };

    beforeEach(() => {
        document.body.innerHTML = '';
        window.history.replaceState({}, '', '/MasterDataManagement/FinishedGoods');
        window.ApiBaseUrl = 'http://127.0.0.1:5000';
        window.CurrentUser = {
            tenantId: '74355e70-4c7d-410c-8cf6-db5fe3b9547f',
            actorType: 'tenant_user'
        };
        window.fetch = vi.fn().mockResolvedValue({
            ok: true,
            status: 200,
            headers: { get: vi.fn().mockReturnValue('application/json; charset=utf-8') },
            json: vi.fn().mockResolvedValue([])
        });
        loadScript('wwwroot/assets/js/shared/http-media-type.js');
        loadScript('wwwroot/assets/js/personalization-client.js');
    });

    it('loads views through the same-origin MVC proxy without browser tenant headers', async () => {
        await window.personalizationClient.getViews('MasterDataManagement', 'FinishedGoods');

        const [url, options] = window.fetch.mock.calls[0];
        expect(url).toBe('/api/personalization/views?moduleKey=MasterDataManagement&pageKey=FinishedGoods');
        expect(options.method).toBe('GET');
        expectBrowserSafeProxyRequest(options);
    });

    it('writes through the same-origin MVC proxy and keeps scope in the query string', async () => {
        window.fetch.mockResolvedValueOnce({
            ok: true,
            status: 201,
            headers: { get: vi.fn().mockReturnValue('application/json; charset=utf-8') },
            json: vi.fn().mockResolvedValue({ id: 'view-1' })
        });

        await window.personalizationClient.saveView({
            moduleKey: 'MasterDataManagement',
            pageKey: 'Gskus',
            viewName: 'Default',
            viewDefinition: {},
            isDefault: true,
            visibility: 'private'
        });

        const [url, options] = window.fetch.mock.calls[0];
        expect(url).toBe('/api/personalization/views?moduleKey=MasterDataManagement&pageKey=Gskus');
        expect(options.method).toBe('POST');
        expectBrowserSafeProxyRequest(options, 'application/json');
    });

    it('updates and deletes by relative catch-all URLs', async () => {
        await window.personalizationClient.updateView('view/with spaces', {
            moduleKey: 'MasterDataManagement',
            pageKey: 'Lskus',
            viewName: 'Default'
        });
        await window.personalizationClient.deleteView(
            'view-1',
            'MasterDataManagement',
            'GlobalProducts');

        expect(window.fetch.mock.calls[0][0]).toBe(
            '/api/personalization/views/view%2Fwith%20spaces?moduleKey=MasterDataManagement&pageKey=Lskus');
        expect(window.fetch.mock.calls[1][0]).toBe(
            '/api/personalization/views/view-1?moduleKey=MasterDataManagement&pageKey=GlobalProducts');
        expect(window.fetch.mock.calls[0][1].method).toBe('PUT');
        expectBrowserSafeProxyRequest(window.fetch.mock.calls[0][1], 'application/json');
        expect(window.fetch.mock.calls[1][1].method).toBe('DELETE');
        expectBrowserSafeProxyRequest(window.fetch.mock.calls[1][1]);
    });

    it('keeps bearer and tenant propagation inside the MVC proxy', () => {
        const source = fs.readFileSync(
            path.join(__dirname, '..', 'Controllers', 'PersonalizationProxyController.cs'),
            'utf8');

        expect(source).toContain('[Route("api/personalization/views")]');
        expect(source).toContain('[Authorize]');
        expect(source).toContain('AuthTokenCookies.GetAccessToken(Request)');
        expect(source).toContain('request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token)');
        expect(source).toContain('request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"))');
        expect(source).toContain('$"{_gatewayUrl}/api/personalization/views"');
        expect(source).not.toContain('localhost:5057');
        expect(source).not.toContain('localhost:5059');

        const actorGate = source.indexOf('!string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase)');
        const canonicalTenantClaim = source.indexOf('User.FindFirstValue("tenant_id")');
        const parseGate = source.indexOf('!Guid.TryParse(tenantValue, out var tenantId)');
        const tenantHeader = source.indexOf('request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"))');
        expect(actorGate).toBeGreaterThan(-1);
        expect(canonicalTenantClaim).toBeGreaterThan(actorGate);
        expect(parseGate).toBeGreaterThan(canonicalTenantClaim);
        expect(tenantHeader).toBeGreaterThan(parseGate);
        expect(source.slice(parseGate, tenantHeader)).toContain('return false;');
    });

    it('does not parse a successful proxy fallback HTML response as JSON when loading views', async () => {
        const response = {
            ok: true,
            status: 200,
            statusText: 'OK',
            redirected: false,
            url: 'http://localhost:5001/api/personalization/views?moduleKey=MDM&pageKey=ProductAbbreviationRegister',
            headers: { get: vi.fn().mockReturnValue('text/html; charset=utf-8') },
            json: vi.fn()
        };
        window.fetch.mockResolvedValueOnce(response);

        await expect(window.personalizationClient.getViews('MDM', 'ProductAbbreviationRegister')).resolves.toEqual([]);
        expect(response.json).not.toHaveBeenCalled();
    });

    it('handles a login redirect without attempting JSON parsing', async () => {
        window.DtDefaults = { handleUnauthorized: vi.fn() };
        const response = {
            ok: true,
            status: 200,
            statusText: 'OK',
            redirected: true,
            url: 'http://localhost:5001/account/login?ReturnUrl=%2FMDM%2FProductAbbreviationRegister',
            headers: { get: vi.fn().mockReturnValue('text/html; charset=utf-8') },
            json: vi.fn()
        };
        window.fetch.mockResolvedValueOnce(response);

        await expect(window.personalizationClient.getViews('MDM', 'ProductAbbreviationRegister')).rejects.toMatchObject({ authHandled: true });
        expect(window.DtDefaults.handleUnauthorized).toHaveBeenCalledTimes(1);
        expect(response.json).not.toHaveBeenCalled();
    });
});
