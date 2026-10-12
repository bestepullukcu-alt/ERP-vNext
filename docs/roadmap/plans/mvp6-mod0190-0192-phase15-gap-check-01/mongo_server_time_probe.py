#!/usr/bin/env python3
"""Disposable Mongo server-time syntax probe; no production database access."""
import json
import pathlib
import socket
import subprocess
import tempfile
import time

OUT = pathlib.Path(__file__).with_name('MONGO-SERVER-TIME-SMOKE.json')
JS = '''const d=db.getSiblingDB("mvp6_phase15_server_time_smoke");
d.lease.insertOne({_id:1,LeaseUntil:new Date(0),Attempt:0,Version:0});
const a=d.lease.findOneAndUpdate({_id:1,$expr:{$lte:["$LeaseUntil","$$NOW"]}},[{$set:{LeaseUntil:{$dateAdd:{startDate:"$$NOW",unit:"second",amount:30}},Attempt:{$add:["$Attempt",1]},Version:{$add:["$Version",1]}}}],{returnDocument:"after"});
const b=d.lease.findOneAndUpdate({_id:1,$expr:{$lte:["$LeaseUntil","$$NOW"]}},[{$set:{Attempt:{$add:["$Attempt",1]}}}],{returnDocument:"after"});
const c=d.lease.findOneAndUpdate({_id:1,Version:1,$expr:{$gt:["$LeaseUntil","$$NOW"]}},[{$set:{LeaseUntil:{$dateAdd:{startDate:"$$NOW",unit:"second",amount:30}}}}],{returnDocument:"after"});
print(JSON.stringify({claimAttempt:a.Attempt,claimVersion:a.Version,leaseAheadMs:a.LeaseUntil-new Date(),duplicateClaimWasNull:b===null,renewAttempt:c.Attempt,renewSucceeded:c!==null}));'''

with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]

with tempfile.TemporaryDirectory(prefix='mvp6-phase15-mongo-') as tmp:
    p = pathlib.Path(tmp)
    with (p / 'mongod.stdout').open('w') as stdout, (p / 'mongod.stderr').open('w') as stderr:
        proc = subprocess.Popen(['mongod', '--dbpath', tmp, '--bind_ip', '127.0.0.1',
                                 '--port', str(port), '--quiet'], stdout=stdout, stderr=stderr)
        try:
            ready = False
            for _ in range(80):
                with socket.socket() as sock:
                    if sock.connect_ex(('127.0.0.1', port)) == 0:
                        ready = True
                        break
                if proc.poll() is not None:
                    break
                time.sleep(0.1)
            if not ready:
                raise RuntimeError('isolated mongod did not become ready')
            cmd = ['mongosh', '--quiet', f'mongodb://127.0.0.1:{port}/admin', '--eval', JS]
            result = subprocess.run(cmd, text=True, capture_output=True, timeout=20)
            data = {
                'mongodVersion': subprocess.run(['mongod', '--version'], text=True,
                                                 capture_output=True).stdout.splitlines()[0],
                'mongoshVersion': subprocess.run(['mongosh', '--version'], text=True,
                                                  capture_output=True).stdout.strip(),
                'host': '127.0.0.1', 'port': port, 'database': 'mvp6_phase15_server_time_smoke',
                'evalJs': JS, 'exit': result.returncode, 'stdout': result.stdout,
                'stderr': result.stderr,
                'mongodStdoutTail': (p / 'mongod.stdout').read_text()[-2000:],
                'mongodStderrTail': (p / 'mongod.stderr').read_text()[-1000:],
            }
            OUT.write_text(json.dumps(data, indent=2) + '\n')
            print(result.stdout, end='')
            if result.returncode:
                raise SystemExit(result.returncode)
        finally:
            proc.terminate()
            try:
                proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait()
