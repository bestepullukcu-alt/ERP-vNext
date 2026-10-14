// WP-0173-UI2 — localhost-only persona helper for the UAS-001 / view-only smoke (E3). Copy of the LOC2 helper with
// the UI-2 personas. Started and stopped by run-ui2-env.ps1.
//
//   GET http://localhost:<port>/persona/<viewer|none>?tenant=<uuid>&company=<uuid>&next=/StockMovements
//
// mints a short-lived HS256 test JWT with the Web's LOCAL signing settings (handed over through this process's
// environment, never written anywhere) carrying the claim NAMES of a real AuthService token (sub, tenant_id,
// actor_type, permission, email) plus legal_entity_id, sets it as the access_token cookie (cookies are per host, not
// per port, so the Web on :5091 receives it) and redirects to the Web. The token is never printed or logged.
//   viewer — inventory.stock.read + inventory.locations.read (view-only: no New / Movement / Correct / Reversal)
//   none   — no inventory permission at all (every FU01 route: _AccessDenied)
'use strict';

const http = require('http');
const crypto = require('crypto');

const port = Number(process.env.UI2_HELPER_PORT || 5097);
const webOrigin = process.env.UI2_WEB_ORIGIN || 'http://localhost:5091';
const secret = process.env.UI2_JWT_SECRET || '';
const issuer = process.env.UI2_JWT_ISSUER || '';
const audience = process.env.UI2_JWT_AUDIENCE || '';

const personas = {
    viewer: { sub: '1c0c2000-0000-4000-8000-0000000000c2', permissions: ['inventory.stock.read', 'inventory.locations.read'] },
    none: { sub: '1c0c2000-0000-4000-8000-0000000000c3', permissions: ['inventory.locations.read'] }
};

const b64url = (buffer) => Buffer.from(buffer).toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function mint(persona, tenant, company) {
    const now = Math.floor(Date.now() / 1000);
    const header = b64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
    const payload = b64url(JSON.stringify({
        sub: persona.sub,
        tenant_id: tenant,
        legal_entity_id: company,
        actor_type: 'tenant_user',
        email: `${persona.sub}@ui2.test`,
        permission: persona.permissions,
        iss: issuer,
        aud: audience,
        nbf: now - 30,
        iat: now - 30,
        exp: now + 1800
    }));
    const signature = b64url(crypto.createHmac('sha256', Buffer.from(secret, 'utf8')).update(`${header}.${payload}`).digest());
    return `${header}.${payload}.${signature}`;
}

const handler = (req, res) => {
    const url = new URL(req.url, `http://localhost:${port}`);
    const match = /^\/persona\/(\w+)$/.exec(url.pathname);
    const persona = match ? personas[match[1]] : null;
    const tenant = url.searchParams.get('tenant') || '';
    const company = url.searchParams.get('company') || '';
    const next = url.searchParams.get('next') || '/StockMovements';
    if (!persona || !uuid.test(tenant) || !uuid.test(company) || !next.startsWith('/') || next.startsWith('//') || !secret) {
        res.writeHead(400, { 'Content-Type': 'text/plain' });
        res.end('bad request');
        return;
    }
    const token = mint(persona, tenant, company);
    res.writeHead(302, {
        'Set-Cookie': [
            `access_token=${token}; Path=/; HttpOnly; SameSite=Lax`,
            'refresh_token=; Path=/; Max-Age=0',
            'auth_ticket=; Path=/; Max-Age=0',
            ...Array.from({ length: 10 }, (_, i) => `access_tokenC${i + 1}=; Path=/; Max-Age=0`),
            ...Array.from({ length: 10 }, (_, i) => `refresh_tokenC${i + 1}=; Path=/; Max-Age=0`)
        ],
        Location: webOrigin + next
    });
    res.end();
    console.log(`${new Date().toISOString()} persona ${match[1]} issued (token not logged)`);
};

for (const host of ['127.0.0.1', '::1']) {
    http.createServer(handler).listen(port, host, () => console.log(`persona helper on ${host}:${port}`));
}
