#!/usr/bin/env python3
"""Run an explicitly supplied request against an integration-owner composed host.
Does not create a host, bypass JWT, or claim broad acceptance. Token stays out of output.
"""
import argparse,hashlib,json,os,pathlib,urllib.request,urllib.error,urllib.parse
p=argparse.ArgumentParser();p.add_argument("--base-url",required=True);p.add_argument("--case",required=True);p.add_argument("--output",required=True);a=p.parse_args()
url=urllib.parse.urlsplit(a.base_url)
if url.hostname not in ("127.0.0.1","localhost") or url.scheme != "http": p.error("explicit local isolated HTTP host required")
c=json.loads(pathlib.Path(a.case).read_text())
if not c["path"].startswith("/api/shipment-bundle/claims"):p.error("Claims path required")
token=os.environ.get("CLAIMS_TEST_JWT")
if not token:p.error("CLAIMS_TEST_JWT required; no authentication bypass")
raw=pathlib.Path(c["bodyFile"]).read_bytes() if c.get("bodyFile") else None
headers=dict(c.get("headers",{}));headers["Authorization"]="Bearer "+token
req=urllib.request.Request(a.base_url.rstrip("/")+c["path"],data=raw,headers=headers,method=c["method"])
try: r=urllib.request.urlopen(req,timeout=30)
except urllib.error.HTTPError as e:r=e
body=r.read();result={"case":a.case,"requestBodySha256":hashlib.sha256(raw or b"").hexdigest(),"status":r.status,"responseHeaders":dict(r.headers),"responseBody":body.decode("utf-8"),"scope":"one real HTTP request; token omitted"}
pathlib.Path(a.output).write_text(json.dumps(result,indent=2))
if r.status!=c["expectedStatus"]:raise SystemExit(1)
