#!/usr/bin/env python3
"""Isolated A04/A07 failure-path probe; never uses operational Mongo 27017."""
import argparse, hashlib, json, os, pathlib, subprocess, tempfile, threading, time, uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from runtime_probe import API_DLL, ROOT, request, token, SECRET

DB_NAME = 'diten_mod0185_failure_probe'
COLLECTIONS = ('loads', 'load_assignments', 'loads_receipts', 'loads_audit', 'loads_outbox')
QUERY_EXITS = []

class SlowHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        time.sleep(10)
    def log_message(self, *_): pass

def scoped_counts(mongo, tenant, legal_entity, label='unlabeled'):
    """Query the same DB the API uses; missing/query failures are never treated as zero."""
    if '127.0.0.1:27785' not in mongo:
        raise RuntimeError('isolated Mongo 27785 required')
    js = 'JSON.stringify(' + json.dumps({c: {'$count': c} for c in COLLECTIONS}) + ')'
    # Build one aggregation per collection so an absent DB/collection is a valid zero,
    # while command/parse failures remain hard failures.
    expr = '{' + ','.join("%r:db.getCollection(%r).countDocuments({TenantId:%r,LegalEntityId:%r})" % (c,c,tenant,legal_entity) for c in COLLECTIONS) + '}'
    cmd = ['mongosh', '--quiet', mongo, '--eval', 'JSON.stringify(' + expr + ')']
    completed = subprocess.run(cmd, text=True, capture_output=True, timeout=10)
    QUERY_EXITS.append({'label': label, 'exit': completed.returncode})
    if completed.returncode != 0:
        raise RuntimeError('Mongo count query failed: ' + completed.stderr.strip())
    try:
        value = json.loads(completed.stdout.strip().splitlines()[-1])
    except Exception as exc:
        raise RuntimeError('Mongo count query returned invalid JSON') from exc
    if set(value) != set(COLLECTIONS) or any(not isinstance(value[c], int) or value[c] < 0 for c in COLLECTIONS):
        raise RuntimeError('Mongo count query incomplete or invalid')
    return value

def assert_no_partial(before, after):
    if set(before) != set(COLLECTIONS) or set(after) != set(COLLECTIONS):
        raise AssertionError('missing collection measurement')
    delta = {c: after[c] - before[c] for c in COLLECTIONS}
    if any(delta[c] != 0 for c in COLLECTIONS):
        raise AssertionError('partial persistence detected: ' + json.dumps(delta, sort_keys=True))
    return delta

def negative_control_results():
    """Exercise every collection-specific mutant and record fail-closed outcomes."""
    cases = []
    baseline = {c: 0 for c in COLLECTIONS}
    for collection in COLLECTIONS:
        missing = dict(baseline)
        missing.pop(collection)
        try:
            assert_no_partial(missing, baseline)
        except (AssertionError, KeyError):
            cases.append({'caseId': f'{collection}-missing-measurement', 'collection': collection,
                          'mutant': 'missing measurement', 'rejected': True})

        invalid = dict(baseline)
        invalid[collection] = 'invalid'
        try:
            assert_no_partial(baseline, invalid)
        except (AssertionError, TypeError):
            cases.append({'caseId': f'{collection}-invalid-measurement', 'collection': collection,
                          'mutant': 'invalid measurement', 'rejected': True})

        nonzero = dict(baseline)
        nonzero[collection] = 1
        try:
            assert_no_partial(baseline, nonzero)
        except AssertionError:
            cases.append({'caseId': f'{collection}-nonzero-delta', 'collection': collection,
                          'mutant': 'nonzero after/delta', 'rejected': True})

        try:
            scoped_counts('mongodb://127.0.0.1:27785/?replicaSet=missing', 't', 'l', f'{collection}-query-failure')
        except Exception:
            cases.append({'caseId': f'{collection}-query-failure', 'collection': collection,
                          'mutant': 'aggregate query failure', 'rejected': True})
    expected = len(COLLECTIONS) * 4
    assert len(cases) == expected and all(case['rejected'] for case in cases)
    return cases

def binary_fingerprint():
    digest = hashlib.sha256(API_DLL.read_bytes()).hexdigest()
    return {'apiBinaryPath': str(API_DLL), 'apiBinarySha256': digest}

def main():
    p=argparse.ArgumentParser(); p.add_argument('--mongo',required=True); p.add_argument('--evidence',required=True); a=p.parse_args()
    if '127.0.0.1:27785' not in a.mongo: raise SystemExit('isolated Mongo 27785 required')
    slow=ThreadingHTTPServer(('127.0.0.1',0),SlowHandler); threading.Thread(target=slow.serve_forever,daemon=True).start()
    results=[]; logs=tempfile.TemporaryFile(mode='w+b'); fingerprint=binary_fingerprint()
    negative_controls=[]
    try:
        negative_controls = negative_control_results()
    except Exception:
        slow.shutdown(); slow.server_close(); logs.close(); raise
    try:
        for name,url in [('connection-refused','http://127.0.0.1:9/'),('timeout',f'http://127.0.0.1:{slow.server_port}/')]:
            tenant,le,actor,correlation=[str(uuid.uuid4()) for _ in range(4)]
            idempotency='failure-probe-' + str(uuid.uuid4())
            auth={'Authorization':'Bearer '+token(tenant,le,actor),'X-Tenant-Id':tenant,'X-Legal-Entity-Id':le,'X-Correlation-Id':correlation,'Idempotency-Key':idempotency}
            body={'carrierId':str(uuid.uuid4()),'shipmentIds':[str(uuid.uuid4())],'mode':'Road','plannedDepartAt':'2030-01-01T00:00:00Z','stops':[{'sequence':1,'locationReferenceId':'','action':'Pickup'},{'sequence':2,'locationReferenceId':'dest','action':'Delivery'}]}
            before=scoped_counts(a.mongo, tenant, le, f'{name}-before')
            env=os.environ.copy(); env.update({'ASPNETCORE_URLS':'http://127.0.0.1:5061','Mongo__ConnectionString':a.mongo,'Mongo__DatabaseName':'diten_mod0185_failure_probe','JwtSettings__Secret':SECRET.decode(),'JwtSettings__Issuer':'mod0185-runtime','JwtSettings__Audience':'mod0185-runtime','Loads__ReferenceBaseUrl':url})
            proc=subprocess.Popen(['dotnet',str(API_DLL)],cwd=ROOT,env=env,stdout=logs,stderr=subprocess.STDOUT)
            try:
                deadline=time.time()+25
                while time.time()<deadline:
                    try:
                        if request('http://127.0.0.1:5061/health')[0]==200: break
                    except Exception: time.sleep(.2)
                status,headers,raw,_=request('http://127.0.0.1:5061/api/shipment-bundle/loads','POST',body,auth)
                payload=json.loads(raw)
                after=scoped_counts(a.mongo, tenant, le, f'{name}-after')
                delta=assert_no_partial(before, after)
                body_correlation=payload.get('error',{}).get('correlationId')
                response_correlation=headers.get('X-Correlation-Id')
                assert status==503 and payload.get('error',{}).get('code')=='DEPENDENCY_UNAVAILABLE'
                assert body_correlation==response_correlation==correlation
                results.append({'scenario':name,'tenantId':tenant,'legalEntityId':le,'correlationId':correlation,'idempotencyKey':idempotency,'status':status,'errorCode':payload['error']['code'],'responseCorrelation':response_correlation,'apiProcessId':proc.pid,'apiCommand':['dotnet',str(API_DLL)],'before':before,'after':after,'delta':delta})
            finally:
                proc.terminate(); proc.wait(timeout=10)
    finally: slow.shutdown(); slow.server_close(); logs.close()
    pathlib.Path(a.evidence).write_text(json.dumps({'level':'E3-failure-paths','databaseName':DB_NAME,'collections':list(COLLECTIONS),'binary':fingerprint,'queryExits':QUERY_EXITS,'negativeControls':negative_controls,'scenarios':results},indent=2)+'\n')
    assert len(results)==2 and all(x['status']==503 and x['errorCode']=='DEPENDENCY_UNAVAILABLE' and all(v==0 for v in x['delta'].values()) for x in results)
    print(json.dumps({'result':'PASS','scenarios':len(results),'evidence':a.evidence}))
if __name__=='__main__': main()
