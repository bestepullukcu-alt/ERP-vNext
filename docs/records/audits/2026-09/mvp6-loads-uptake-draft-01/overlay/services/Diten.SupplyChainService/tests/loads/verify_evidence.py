#!/usr/bin/env python3
"""Check runtime probe evidence shape without retaining credentials or operational data."""
import base64, hashlib, json, pathlib, sys, uuid

def main():
    if len(sys.argv)!=2: raise SystemExit("usage: verify_evidence.py runtime-evidence.json")
    path=pathlib.Path(sys.argv[1]); d=json.loads(path.read_text())
    if d["level"]=="E3-runtime-http":
        assert d["fingerprints"]["frozenContractsSha256"]["shipmentBundle"]=="6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2"
        assert d["fingerprints"]["frozenContractsSha256"]["loadsAnnex"]=="9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034"
        assert len(d["fingerprints"]["apiBinarySha256"])==64 and len(d["fingerprints"]["implementationSourceSetSha256"])==64
        assert [x["status"] for x in d["operations"]]==[201,200,201]
        assert len(d["operations"])==3
        for op in d["operations"]:
            assert set(("method","path","status","requestBody","requestBytes","requestHeaders","responseBody","responseBytes")) <= set(op)
            sent = b"" if op["requestBody"] is None else op["requestBody"].encode()
            assert op["requestBytes"] == len(sent)
            assert op["responseBytes"] == len(op["responseBody"].encode())
            assert op["requestHeaders"].get("correlation") == d["correlationId"]
        assert d["operations"][0]["requestBytes"] == 294
        assert d["operations"][1]["requestBody"] is None and d["operations"][1]["requestBytes"] == 0
        assert d["operations"][2]["requestBytes"] == 294
        assert d["operations"][2]["idempotentReplay"] is True
        assert len(d["mockCalls"])==2
        assert all(x["method"]=="GET" and x["requestBytes"]==0 for x in d["mockCalls"])
        assert all(x["tenant"]==d["tenant"] and x["legalEntity"]==d["legalEntity"] and x["correlation"]==d["correlationId"] and x["authorizationPresent"] for x in d["mockCalls"])
        listed=json.loads(d["operations"][1]["responseBody"]); created=json.loads(d["operations"][0]["responseBody"])
        assert listed["items"] and all("lifecycleCorrelationId" in x for x in listed["items"])
        mine=[x for x in listed["items"] if x["loadId"]==created["loadId"]]
        assert len(mine)==1 and mine[0]["lifecycleCorrelationId"] is not None and uuid.UUID(mine[0]["lifecycleCorrelationId"])==uuid.UUID(d["correlationId"])
        checks=19
    elif d["level"]=="E3-process-restart":
        assert len(d["fingerprints"]["apiBinarySha256"])==64 and len(d["fingerprints"]["implementationSourceSetSha256"])==64
        assert len(set(d["processIds"]))==2 and d["createStatus"]==d["replayStatus"]==201 and d["listStatus"]==200
        assert d["replayedHistoricalResult"] is True and d["listedAfterRestart"] is True
        assert len(d["dependencyCalls"])==2 and all(x["method"]=="GET" and x["requestBytes"]==0 for x in d["dependencyCalls"])
        assert d["outboxAfterRestart"]["count"]==1 and d["outboxAfterRestart"]["status"]=="Pending" and d["outboxAfterRestart"]["loadId"]==d["loadId"]
        checks=6
    else: raise AssertionError("unsupported evidence level")
    serialized=json.dumps(d).lower()
    assert "bearer " not in serialized and "connectionstring" not in serialized and "secret" not in serialized
    print(json.dumps({"result":"PASS","checks":checks+1,"evidence":str(path)}))
if __name__=="__main__": main()
