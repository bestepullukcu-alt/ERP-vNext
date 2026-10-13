import hashlib, http.client, http.cookiejar, json, os, re, urllib.error, urllib.request

http.client._MAXHEADERS = 1000
BASE = 'http://localhost.:5901'
TENANT = '97c59330-dbc4-4665-b29c-0c26dbb5cc93'
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))

def request(path, method='GET', body=None, headers=None):
    encoded = None if body is None else json.dumps(body, separators=(',', ':')).encode()
    req = urllib.request.Request(BASE + path, data=encoded, method=method,
        headers={'Content-Type':'application/json', **(headers or {})})
    try:
        with opener.open(req, timeout=20) as response:
            return response.status, dict(response.headers), response.read()
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers), error.read()

status, _, _ = request('/account/login', 'POST', {
    'email':'jane.smith.t97@diten.com', 'password':os.environ['FIXTURE_PASSWORD'],
    'tenantId':TENANT, 'rememberMe':False, 'returnUrl':'/SupplyChain/Shipments'})
if status != 200: raise RuntimeError(f'login failed: {status}')

def run(case, shipment, root, kind, body, key):
    status, _, html = request(f'/SupplyChain/Shipments/Details/{shipment}')
    match = re.search(rb'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    if status != 200 or not match: raise RuntimeError(f'{case}: detail/token failed')
    canonical = json.dumps(body, separators=(',', ':'), sort_keys=True).encode()
    status, headers, raw = request(f'/SupplyChain/Shipments/api/{shipment}/{kind}', 'POST', body, {
        'RequestVerificationToken':match.group(1).decode(), 'Idempotency-Key':key,
        'X-Correlation-Id':root})
    return {'case':case,'request':{'shipmentId':shipment,'root':root,'kind':kind,
        'idempotencyKey':key,'payloadSha256':hashlib.sha256(canonical).hexdigest()},
        'response':{'status':status,'correlationHeader':headers.get('X-Correlation-Id'),
        'body':json.loads(raw)}}

cases = [
  run('A08-exact-error','868e6fed-32bb-4301-9f6e-fb5223ce6b6a','770306bf-b5b5-4645-b232-3d2b8713c323','transition',
      {'targetStatus':'Planned','occurredAt':'2026-09-26T12:10:00.000Z','reasonCode':None,'note':'exact error probe'},
      'd1000000-0000-4000-8000-000000000008'),
  run('A09-409-exact-error','ba94aaf0-36e9-450c-b982-49406667eeb7','d7f1caef-9aa0-4d92-94d7-b5c135d045bb','pod',
      {'recipientName':'Exact Error Probe','receivedAt':'2026-09-26T12:15:00.000Z','evidenceReferenceIds':['A09-409-EXACT'],'note':None},
      'd2000000-0000-4000-8000-000000000009'),
  run('A09-422-exact-error','777e506c-5b08-4e8e-8201-b4604896c61b','a51831b9-44e2-4b74-9bf3-4f360cd9ca38','pod',
      {'recipientName':'Exact Error Probe','receivedAt':'2026-09-26T12:20:00.000Z','evidenceReferenceIds':['A09-422-EXACT'],'note':None},
      'd3000000-0000-4000-8000-000000000009')
]
print(json.dumps({'profile':'real-auth-t1-le-a-actor-b','cases':cases}, indent=2, sort_keys=True))
