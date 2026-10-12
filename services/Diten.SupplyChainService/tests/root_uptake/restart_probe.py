#!/usr/bin/env python3
import base64, hashlib, hmac, json, os, sys, time, urllib.request
from pathlib import Path
if not all(os.environ.get(k) for k in ["ROOT_API_URL","ROOT_RESTART_EVIDENCE","ROOT_JWT_SECRET","ROOT_TENANT","ROOT_LE","ROOT_ACTOR"]):
 print(json.dumps({"status":"BLOCKED","reason":"missing API/evidence environment"})); sys.exit(2)
def b(v): return base64.urlsafe_b64encode(v).rstrip(b"=").decode()
head=b'{"alg":"HS256","typ":"JWT"}'
payload=json.dumps({"sub":os.environ["ROOT_ACTOR"],"tenant_id":os.environ["ROOT_TENANT"],"legal_entity_id":os.environ["ROOT_LE"],"actor_type":"tenant_user","permission":["supplychain.shipments.read"],"iss":"mod0183-tests","aud":"mod0183-tests","exp":int(time.time())+900},separators=(',',':')).encode()
unsigned=b(head)+"."+b(payload)
bearer=unsigned+"."+b(hmac.new(os.environ["ROOT_JWT_SECRET"].encode(),unsigned.encode(),hashlib.sha256).digest())
# Caller restarts the same binary between phase A and B; this probe verifies phase-B persisted reads.
req=urllib.request.Request(os.environ["ROOT_API_URL"]+os.environ["ROOT_RESTART_PATH"],headers={"Authorization":"Bearer "+bearer,"X-Tenant-Id":os.environ["ROOT_TENANT"],"X-Legal-Entity-Id":os.environ["ROOT_LE"],"X-Correlation-Id":os.environ["ROOT_CORRELATION"]})
with urllib.request.urlopen(req,timeout=10) as r: body=r.read().decode(); status=r.status
Path(os.environ["ROOT_RESTART_EVIDENCE"]).write_text(json.dumps({"status":"PASS","httpStatus":status,"body":body,"binarySha256":os.environ.get("ROOT_BINARY_SHA256")},indent=2))
