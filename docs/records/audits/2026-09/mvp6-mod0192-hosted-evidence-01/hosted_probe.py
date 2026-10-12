#!/usr/bin/env python3
"""Native MOD-0192 HTTP/JWT and hosted executor evidence on isolated Mongo."""
from __future__ import annotations

import base64
import hashlib
import hmac
import http.client
import json
import os
import pathlib
import socket
import subprocess
import time
import urllib.error
import urllib.request
import uuid

ROOT = pathlib.Path('/private/tmp/mvp6-mod0192-hosted-evidence-01-20260923')
OUT = pathlib.Path('/Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-01/evidence')
DLL = ROOT / 'services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll'
MONGO = 'mongodb://127.0.0.1:57392/?replicaSet=rsmod192host&serverSelectionTimeoutMS=5000'
DB = 'DitenSupplyChain_Mod0192_Hosted_Dev'
PORT_A = 56392
PORT_B = 56393
SECRET = 'MOD0192 hosted evidence isolated signing key 2026'
ISSUER = AUDIENCE = 'mod0192-hosted-evidence'
TENANT = '19200000-0000-4000-8000-000000000001'
LE = '19200000-0000-4000-8000-000000000002'
ACTOR = '19200000-0000-4000-8000-000000000003'
PERMISSIONS = [
    'supplychain.capacity-plans.read',
    'supplychain.capacity-plans.create',
    'supplychain.capacity-plans.scenario.create',
    'supplychain.capacity-plans.evaluate',
]
records: list[dict] = []
mongo_records: list[dict] = []
process_records: list[dict] = []

def b64(value: bytes) -> str:
    return base64.urlsafe_b64encode(value).decode().rstrip('=')

def token(tenant=TENANT, le=LE, permissions=PERMISSIONS) -> str:
    header = b64(b'{"alg":"HS256","typ":"JWT"}')
    payload = b64(json.dumps({
        'iss': ISSUER, 'aud': AUDIENCE, 'exp': int(time.time()) + 900,
        'sub': ACTOR, 'tenant_id': tenant, 'legal_entity_id': le,
        'permission': permissions,
    }, separators=(',', ':')).encode())
    unsigned = f'{header}.{payload}'
    return unsigned + '.' + b64(hmac.new(SECRET.encode(), unsigned.encode(), hashlib.sha256).digest())

def mongo(js: str, label: str):
    completed = subprocess.run(['mongosh', MONGO, '--quiet', '--eval', js], text=True, capture_output=True, check=True)
    value = completed.stdout.strip()
    parsed = json.loads(value) if value else None
    mongo_records.append({'label': label, 'javascript': js, 'stdout': parsed, 'stderr': completed.stderr.strip()})
    return parsed

def db_eval(expression: str, label: str):
    return mongo(f'const d=db.getSiblingDB({json.dumps(DB)});print(JSON.stringify({expression}));', label)

def headers(correlation: str, key: str | None = None, tenant=TENANT, le=LE, permissions=PERMISSIONS, authenticated=True):
    result = {'X-Correlation-Id': correlation, 'X-Tenant-Id': tenant, 'X-Legal-Entity-Id': le}
    if authenticated:
        result['Authorization'] = 'Bearer ' + token(tenant, le, permissions)
    if key is not None:
        result['Idempotency-Key'] = key
        result['Content-Type'] = 'application/json'
    return result

def request(port: int, method: str, path: str, expected: int, body=None, key=None, tenant=TENANT, le=LE,
            permissions=PERMISSIONS, authenticated=True, correlation=None):
    correlation = correlation or str(uuid.uuid4())
    hs = headers(correlation, key, tenant, le, permissions, authenticated)
    raw = None if body is None else json.dumps(body, separators=(',', ':')).encode()
    req = urllib.request.Request(f'http://127.0.0.1:{port}{path}', data=raw, headers=hs, method=method)
    try:
        response = urllib.request.urlopen(req, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        response_raw = response.read()
        status = response.status
        response_headers = dict(response.headers)
    parsed = json.loads(response_raw) if response_raw else None
    records.append({
        'method': method, 'path': path, 'status': status,
        'requestHeaders': {k: ('<redacted>' if k == 'Authorization' else v) for k, v in hs.items()},
        'requestBody': body, 'responseHeaders': response_headers, 'responseBody': parsed,
    })
    assert status == expected, (method, path, status, expected, parsed)
    if status >= 400 and isinstance(parsed, dict) and 'error' in parsed:
        assert parsed['error']['correlationId'] == response_headers.get('X-Correlation-Id')
    return parsed, response_headers

def start(name: str, port: int, app_name: str):
    log_path = OUT / f'{name}.log'
    log = log_path.open('wb')
    env = dict(os.environ)
    env.update({
        'DOTNET_ROLL_FORWARD': 'Major', 'ASPNETCORE_ENVIRONMENT': 'Testing',
        'ASPNETCORE_URLS': f'http://127.0.0.1:{port}',
        'Mongo__ConnectionString': MONGO.replace('?', f'?appName={app_name}&'),
        'Mongo__DatabaseName': DB, 'JwtSettings__Secret': SECRET,
        'JwtSettings__Issuer': ISSUER, 'JwtSettings__Audience': AUDIENCE,
    })
    process = subprocess.Popen(['/Users/natig/.dotnet/dotnet', str(DLL)], cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
    entry = {'name': name, 'appName': app_name, 'port': port, 'pid': process.pid, 'startedAt': time.time(),
             'binarySha256': hashlib.sha256(DLL.read_bytes()).hexdigest(), 'log': str(log_path)}
    process_records.append(entry)
    deadline = time.time() + 30
    while time.time() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f'{name} exited during startup: {process.returncode}')
        try:
            with urllib.request.urlopen(f'http://127.0.0.1:{port}/health', timeout=.5) as response:
                if response.status == 200:
                    entry['healthyAt'] = time.time()
                    return process, entry, log
        except OSError:
            time.sleep(.2)
    process.kill(); process.wait()
    raise RuntimeError(f'{name} health timeout')

def stop(process, entry, log, hard=False):
    if process.poll() is None:
        process.kill() if hard else process.terminate()
        process.wait(timeout=15)
    entry['exitCode'] = process.returncode
    entry['stoppedAt'] = time.time()
    log.close()

def wait_eval(evaluation_id: str, status: str, timeout=55):
    deadline = time.time() + timeout
    while time.time() < deadline:
        row = db_eval(f'd.capacity_evaluations.findOne({{_id:{json.dumps(evaluation_id)}}})', f'poll-{evaluation_id}-{status}')
        if row and row.get('Status') == status:
            return row
        time.sleep(.5)
    raise AssertionError((evaluation_id, status, row))

def bson_int(value):
    if isinstance(value, dict):
        if 'low' in value and 'high' in value:
            return int(value['low']) + (int(value['high']) << 32)
        value = value.get('$numberLong', value.get('$numberInt'))
    return int(value)

def plan_body(name: str, year=2027):
    return {'name': name, 'horizonStart': f'{year}-01-01', 'horizonEnd': f'{year}-12-31',
            'demandPlanId': 'dp-2027', 'demandPlanVersion': '3',
            'sourceCapturedAt': '2026-09-15T09:45:00Z', 'sourceChecksum': 'sha256:ee56d4f9a3c8'}

def scenario_body(name: str):
    return {'name': name, 'constraintRefs': [{'constraintId': 'line-4-hours', 'source': 'SUPPLY-CONSTRAINTS', 'sourceVersion': '8'}],
            'adjustments': [{'resourceRef': 'line-4', 'period': '2027-W03', 'availableCapacityDelta': '80.000', 'uomId': 'HOUR'}]}

def create_flow(port: int, suffix: str):
    year = {'kill': 2029, 'race': 2030}[suffix]
    plan, _ = request(port, 'POST', '/api/supply-chain/capacity-plans', 201, plan_body('Hosted ' + suffix, year), 'plan-' + suffix)
    plan_id = plan['capacityPlanId']
    scenario, _ = request(port, 'POST', f'/api/supply-chain/capacity-plans/{plan_id}/scenarios', 201, scenario_body('Scenario ' + suffix), 'scenario-' + suffix)
    evaluation, _ = request(port, 'POST', f'/api/supply-chain/capacity-plans/{plan_id}/scenarios/{scenario["scenarioId"]}/evaluations', 202,
                            {'evaluationMode': 'Finite', 'resourceRefs': ['line-4']}, 'evaluation-' + suffix)
    return plan_id, scenario['scenarioId'], evaluation['evaluationId']

def set_block(app_name: str, milliseconds: int):
    js = 'print(JSON.stringify(db.adminCommand({configureFailPoint:"failCommand",mode:{skip:1},data:{failCommands:["findAndModify"],appName:' + json.dumps(app_name) + ',blockConnection:true,blockTimeMS:' + str(milliseconds) + '}})))'
    return mongo(js, 'failpoint-on-' + app_name)

def stop_failpoint(label: str):
    return mongo('print(JSON.stringify(db.adminCommand({configureFailPoint:"failCommand",mode:"off"})))', label)

def assert_port_free(port: int):
    with socket.socket() as sock:
        assert sock.connect_ex(('127.0.0.1', port)) != 0, f'port occupied: {port}'

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    assert DLL.is_file()
    assert_port_free(PORT_A); assert_port_free(PORT_B)
    db_eval('d.dropDatabase()', 'drop-hosted-db')
    processes = []
    main_p, main_e, main_l = start('main-http', PORT_A, 'mod192-host-main'); processes.append((main_p, main_e, main_l))
    try:
        # Real authentication and permission separation.
        request(PORT_A, 'GET', '/api/supply-chain/capacity-plans/' + str(uuid.uuid4()), 401, authenticated=False)
        request(PORT_A, 'GET', '/api/supply-chain/capacity-plans/' + str(uuid.uuid4()), 403, permissions=[])

        # Six operations, replay/fingerprint conflict, tenant/LE isolation, fixture and Pending outbox.
        body = plan_body('Hosted HTTP', 2027)
        created, _ = request(PORT_A, 'POST', '/api/supply-chain/capacity-plans', 201, body, 'http-plan')
        plan_id = created['capacityPlanId']
        replay, _ = request(PORT_A, 'POST', '/api/supply-chain/capacity-plans', 201, body, 'http-plan')
        assert replay == created
        changed = dict(body); changed['name'] = 'Changed valid payload'
        conflict, _ = request(PORT_A, 'POST', '/api/supply-chain/capacity-plans', 409, changed, 'http-plan')
        assert conflict['error']['code'] == 'IDEMPOTENCY_KEY_REUSED'
        request(PORT_A, 'GET', f'/api/supply-chain/capacity-plans/{plan_id}', 200)
        request(PORT_A, 'GET', f'/api/supply-chain/capacity-plans/{plan_id}', 404, tenant=str(uuid.uuid4()))
        request(PORT_A, 'GET', f'/api/supply-chain/capacity-plans/{plan_id}', 404, le=str(uuid.uuid4()))
        scenario, _ = request(PORT_A, 'POST', f'/api/supply-chain/capacity-plans/{plan_id}/scenarios', 201, scenario_body('Hosted scenario'), 'http-scenario')
        scenario_id = scenario['scenarioId']
        request(PORT_A, 'GET', f'/api/supply-chain/capacity-plans/{plan_id}/scenarios/{scenario_id}', 200)
        evaluation, _ = request(PORT_A, 'POST', f'/api/supply-chain/capacity-plans/{plan_id}/scenarios/{scenario_id}/evaluations', 202,
                                {'evaluationMode': 'Finite', 'resourceRefs': ['line-4']}, 'http-eval')
        evaluation_id = evaluation['evaluationId']
        terminal = wait_eval(evaluation_id, 'Completed')
        got, _ = request(PORT_A, 'GET', f'/api/supply-chain/capacity-plans/{plan_id}/evaluations/{evaluation_id}', 200)
        assert got['status'] == 'Completed' and got['bottlenecks'][0]['shortfall'] == '40.000'
        pending = db_eval(f'({{event:d.capacity_outbox.findOne({{AggregateId:{json.dumps(evaluation_id)}}}),audit:d.capacity_audit.findOne({{TargetId:{json.dumps(evaluation_id)}}}),slot:d.capacity_active_slots.countDocuments({{EvaluationId:{json.dumps(evaluation_id)}}})}})', 'http-terminal-state')
        assert pending['event']['Status'] == 'Pending' and pending['slot'] == 0
        time.sleep(11)
        assert db_eval(f'd.capacity_outbox.findOne({{AggregateId:{json.dumps(evaluation_id)}}}).Status', 'pending-after-host-running') == 'Pending'

        # Soft-delete remains invisible.
        db_eval(f'd.capacity_plans.updateOne({{_id:{json.dumps(plan_id)}}},{{$set:{{IsDeleted:true}}}})', 'soft-delete-plan')
        request(PORT_A, 'GET', f'/api/supply-chain/capacity-plans/{plan_id}', 404)

        # Transport response loss: send a valid request, close before reading, then recover with the same key.
        loss_body = json.dumps(plan_body('Response loss', 2028), separators=(',', ':')).encode()
        loss_corr = str(uuid.uuid4()); loss_headers = headers(loss_corr, 'response-loss')
        request_head = [
            'POST /api/supply-chain/capacity-plans HTTP/1.1', f'Host: 127.0.0.1:{PORT_A}',
            f'Content-Length: {len(loss_body)}', 'Connection: close',
            *[f'{name}: {value}' for name, value in loss_headers.items()], '', '',
        ]
        lost_socket = socket.create_connection(('127.0.0.1', PORT_A), timeout=10)
        lost_socket.sendall('\r\n'.join(request_head).encode() + loss_body)
        # Deliberately do not read the response. Prove the durable receipt exists
        # before the recovery request, so this is committed response loss.
        receipt = None
        for _ in range(30):
            receipt = db_eval('d.capacity_receipts.findOne({Key:"response-loss",Operation:"createCapacityPlan"})', 'response-loss-receipt-before-retry')
            if receipt:
                break
            time.sleep(.1)
        lost_socket.close()
        assert receipt is not None
        recovered, _ = request(PORT_A, 'POST', '/api/supply-chain/capacity-plans', 201, json.loads(loss_body), 'response-loss')
        receipt_body = json.loads(receipt['Body'])
        assert recovered['capacityPlanId'] == receipt_body['Id']
        records.append({'case': 'response-loss', 'transportClosedBeforeRead': True, 'receiptObservedBeforeRetry': True,
                        'recoveredCapacityPlanId': recovered['capacityPlanId'], 'receiptCorrelationId': receipt['CorrelationId']})

        # Kill/restart: block terminal after claim, kill host, restart while lease is valid, then observe expiry reclaim.
        set_block('mod192-host-main', 60000)
        _, _, kill_eval = create_flow(PORT_A, 'kill')
        running = wait_eval(kill_eval, 'Running', 20)
        stop(main_p, main_e, main_l, hard=True); processes.pop()
        # The failpoint is appName-scoped to the killed worker. Restart immediately,
        # before the 30-second lease expires; disabling the blocking failpoint first
        # would itself wait for the blocked command timeout and destroy this boundary.
        restart_p, restart_e, restart_l = start('restart-host', PORT_A, 'mod192-host-restart'); processes.append((restart_p, restart_e, restart_l))
        time.sleep(3)
        still_running = db_eval(f'd.capacity_evaluations.findOne({{_id:{json.dumps(kill_eval)}}})', 'valid-lease-after-restart')
        assert still_running['Status'] == 'Running' and still_running['Attempt'] == 1
        reclaimed = wait_eval(kill_eval, 'Completed', 45)
        assert reclaimed['Attempt'] == 2 and bson_int(reclaimed['Fence']) == bson_int(running['Fence']) + 1
        stop_failpoint('kill-failpoint-off')
        kill_effect = db_eval(f'({{events:d.capacity_outbox.countDocuments({{AggregateId:{json.dumps(kill_eval)}}}),audits:d.capacity_audit.countDocuments({{TargetId:{json.dumps(kill_eval)},Operation:"terminalCapacityEvaluation"}}),slots:d.capacity_active_slots.countDocuments({{EvaluationId:{json.dumps(kill_eval)}}})}})', 'kill-restart-effect')
        assert kill_effect == {'events': 1, 'audits': 1, 'slots': 0}
        stop(restart_p, restart_e, restart_l); processes.pop()

        # Hosted stale worker and lease race: A blocks terminal; after expiry B wins; A later loses its stale fence.
        race_seed_p, race_seed_e, race_seed_l = start('race-seed', PORT_A, 'mod192-race-seed'); processes.append((race_seed_p, race_seed_e, race_seed_l))
        _, _, race_eval = create_flow(PORT_A, 'race')
        stop(race_seed_p, race_seed_e, race_seed_l); processes.pop()
        set_block('mod192-worker-a', 45000)
        worker_a, worker_a_e, worker_a_l = start('worker-a', PORT_A, 'mod192-worker-a'); processes.append((worker_a, worker_a_e, worker_a_l))
        first = wait_eval(race_eval, 'Running', 15)
        time.sleep(31)
        worker_b, worker_b_e, worker_b_l = start('worker-b', PORT_B, 'mod192-worker-b'); processes.append((worker_b, worker_b_e, worker_b_l))
        raced = wait_eval(race_eval, 'Completed', 20)
        assert raced['Attempt'] == 2 and bson_int(raced['Fence']) == bson_int(first['Fence']) + 1
        time.sleep(15)
        race_effect = db_eval(f'({{events:d.capacity_outbox.countDocuments({{AggregateId:{json.dumps(race_eval)}}}),audits:d.capacity_audit.countDocuments({{TargetId:{json.dumps(race_eval)},Operation:"terminalCapacityEvaluation"}}),slots:d.capacity_active_slots.countDocuments({{EvaluationId:{json.dumps(race_eval)}}})}})', 'stale-race-effect')
        assert race_effect == {'events': 1, 'audits': 1, 'slots': 0}
        stop_failpoint('race-failpoint-off')
        stop(worker_a, worker_a_e, worker_a_l); processes.pop()
        stop(worker_b, worker_b_e, worker_b_l); processes.pop()

        final = db_eval('({evaluations:d.capacity_evaluations.find({}).sort({_id:1}).toArray(),outbox:d.capacity_outbox.find({}).sort({_id:1}).toArray(),audit:d.capacity_audit.find({}).sort({_id:1}).toArray(),activeSlots:d.capacity_active_slots.find({}).toArray()})', 'final-db-snapshot')
        assert final['activeSlots'] == [] and all(row['Status'] == 'Pending' for row in final['outbox'])
        result = {'result': 'PASS', 'httpRecords': len(records), 'binarySha256': hashlib.sha256(DLL.read_bytes()).hexdigest(),
                  'killRestart': kill_effect, 'staleRace': race_effect, 'noPublisherPendingEvents': len(final['outbox'])}
        (OUT / 'http-records.json').write_text(json.dumps(records, indent=2) + '\n')
        (OUT / 'mongo-transcript.json').write_text(json.dumps(mongo_records, indent=2) + '\n')
        (OUT / 'process-transcript.json').write_text(json.dumps(process_records, indent=2) + '\n')
        (OUT / 'hosted-probe-result.json').write_text(json.dumps(result, indent=2) + '\n')
        print(json.dumps(result, indent=2))
    finally:
        try:
            stop_failpoint('final-failpoint-off')
        except Exception:
            pass
        for process, entry, log in reversed(processes):
            try:
                stop(process, entry, log, hard=True)
            except Exception:
                pass
        (OUT / 'process-transcript.json').write_text(json.dumps(process_records, indent=2) + '\n')
        (OUT / 'mongo-transcript.json').write_text(json.dumps(mongo_records, indent=2) + '\n')
        (OUT / 'http-records.json').write_text(json.dumps(records, indent=2) + '\n')

if __name__ == '__main__':
    main()
