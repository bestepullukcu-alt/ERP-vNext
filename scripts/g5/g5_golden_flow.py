#!/usr/bin/env python3
"""
G5 — SupplyChain lojistik golden flow kosucusu.

MOD-0183'un kabul listesindeki calisma-zamani maddelerini canli bir yigina karsi olcer:
  :425 bir create = bir scoped kayit + bir lifecycle/outbox girdisi
  :426 ayni idempotency anahtari ikinci yazma uretmeden ayni sonucu doner
  :427 gecis matrisi sozlesmeye uyar; gecersizler kismi durum birakmaz
  :429 korelasyon kimligi zincir boyunca korunur
  :433 canli yukler dondurulmus OpenAPI ile eslesir
  :434 zincir: shipment -> carrier/load -> POD -> return/claim

Kullanim:
    python3 scripts/g5/g5_golden_flow.py chain      # :434 zinciri
    python3 scripts/g5/g5_golden_flow.py conform    # :433 sozlesme uygunlugu
    python3 scripts/g5/g5_golden_flow.py acceptance # :425 :426 :427
    python3 scripts/g5/g5_golden_flow.py all

Ortam degiskenleri (hepsinin varsayilani var):
    G5_GATEWAY   http://127.0.0.1:5000
    G5_TOKEN     zorunlu - Auth'tan alinmis erisim jetonu
    G5_TENANT    zorunlu - kiraci UUID'si
    G5_LE        zorunlu - MDM'de Active bir legal entity UUID'si
    G5_MONGO     mongodb baglantisi icin mongosh portu (varsayilan 33994)
    G5_DB        SupplyChain veritabani adi (varsayilan g5_sc)
    G5_CONTRACT  docs/analysis/contracts/shipment-bundle.openapi.yaml yolu

NOT: ham cikti depoya YAZILMAZ. Bu arac yalnizca stdout'a rapor eder; kaniti ozetleyip
elle kaydetmek cagiranin isidir (docs/records kurali: yalniz ozet .md).

Olculen tuzaklar, tekrar kesfedilmesin diye:
  * Servis sirasi: Platform ONCE, SupplyChain SONRA. Tersi olursa modul manifestleri
    kaydolmaz, izinler Auth'a senkronlanmaz ve her cagri 403 doner.
  * Auth'un login'i Platform'un /api/internal/tenants/{id}/login-settings ucuna baglidir.
  * Sevkiyat yasam dongusu TEK korelasyon kokune baglidir; farkli korelasyonla gonderilen
    bir gecis 400 alir.
  * Yuk yalnizca sevkiyat Draft ya da Planned iken baglanabilir.
  * Korelasyon alan adi modullere gore degisir: Shipments CorrelationId, Loads/Returns/
    Claims CorrelationRoot, outbox zarflarinda correlationId.
"""
import argparse, json, os, re, subprocess, sys, urllib.error, urllib.request, uuid

GW   = os.environ.get("G5_GATEWAY", "http://127.0.0.1:5000")
BASE = GW + "/api/shipment-bundle"
PORT = os.environ.get("G5_MONGO", "33994")
DB   = os.environ.get("G5_DB", "g5_sc")

def need(name):
    v = os.environ.get(name)
    if not v:
        sys.exit(f"{name} gerekli. --help'e bakin.")
    return v

TOK, TEN, LE = need("G5_TOKEN"), need("G5_TENANT"), need("G5_LE")
RUN = uuid.uuid4().hex[:8]

def call(method, path, body=None, corr=None, key=None, headers=None, drop=None):
    req = urllib.request.Request(BASE + path, method=method)
    hs = {"Authorization": "Bearer " + TOK, "X-Tenant-Id": TEN, "X-Legal-Entity-Id": LE,
          "X-Correlation-Id": corr or str(uuid.uuid4()),
          "Idempotency-Key": key or f"{RUN}-{uuid.uuid4().hex[:8]}"}
    for k in (drop or []): hs.pop(k, None)
    hs.update(headers or {})
    data = None
    if body is not None:
        data = json.dumps(body).encode(); hs["Content-Type"] = "application/json"
    for k, v in hs.items(): req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, data, timeout=30) as r:
            raw = r.read().decode(); return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try: return e.code, (json.loads(raw) if raw else None)
        except Exception: return e.code, None

def mongo(js):
    return subprocess.run(["mongosh", "--quiet", "--port", PORT, DB, "--eval", js],
                          capture_output=True, text=True).stdout.strip()

def shipment_body(tag):
    return {"sourceModule": "MOD-0141", "sourceType": "SALES_ORDER", "sourceDocumentId": "SO-" + tag,
            "warehouseReferenceId": "wh-01", "shipToReference": "C/A",
            "plannedShipAt": "2030-01-01T08:00:00Z", "plannedDeliverAt": "2030-01-02T16:00:00Z",
            "lines": [{"lineNumber": "1", "itemId": "b1f2c3d4-0000-0000-0000-000000000001",
                       "skuId": "c3d4e5f6-0000-0000-0000-000000000002", "quantity": "2.000",
                       "uomId": "EA", "inventoryReferenceId": "r-" + tag}]}

def err(d):
    e = d.get("error") if isinstance(d, dict) else None
    return e.get("code", "") if isinstance(e, dict) else ""

def line(label, st, d=None, extra=""):
    print(f"  {label:<30} -> {st:<4} {err(d or {}):<26} {extra}")

# ---------------------------------------------------------------- :434 zincir
def chain():
    corr = str(uuid.uuid4())
    print(f"  korelasyon: {corr}\n")
    st, s = call("POST", "/shipments", shipment_body(RUN), corr=corr); line("1 shipment create", st, s)
    sid = (s or {}).get("shipmentId")
    st, c = call("POST", "/carriers", {"carrierCode": "G" + RUN[:6].upper(),
                 "displayName": "G5 " + RUN, "supportedModes": ["Road"]}, corr=corr)
    line("2 carrier create", st, c); cid = (c or {}).get("carrierId")
    # Yuk yalnizca Draft/Planned sevkiyata baglanir, bu yuzden once Planned.
    st, d = call("POST", f"/shipments/{sid}/transition",
                 {"targetStatus": "Planned", "occurredAt": "2030-01-01T09:00:00Z"}, corr=corr)
    line("3 -> Planned", st, d)
    st, l = call("POST", "/loads", {"carrierId": cid, "shipmentIds": [sid], "mode": "Road",
                 "plannedDepartAt": "2030-01-01T10:00:00Z",
                 "stops": [{"sequence": 1, "locationReferenceId": "o", "action": "Pickup"},
                           {"sequence": 2, "locationReferenceId": "d", "action": "Delivery"}]}, corr=corr)
    line("4 load create", st, l)
    for s2 in ("Dispatched", "InTransit"):
        st, d = call("POST", f"/shipments/{sid}/transition",
                     {"targetStatus": s2, "occurredAt": "2030-01-01T11:00:00Z"}, corr=corr)
        line(f"5 -> {s2}", st, d)
    st, d = call("POST", f"/shipments/{sid}/pod", {"recipientName": "R",
                 "receivedAt": "2030-01-02T15:00:00Z", "evidenceReferenceIds": ["e"], "note": "n"}, corr=corr)
    line("6 POD capture", st, d)
    st, d = call("GET", f"/shipments/{sid}", corr=corr)
    line("6b shipment", st, d, f"status={(d or {}).get('status')!r} carrierId={(d or {}).get('carrierId')!r}")
    st, d = call("POST", "/returns", {"shipmentId": sid, "reasonCode": "DAMAGED",
                 "lines": [{"shipmentLineNumber": "1", "quantity": "1.000", "uomId": "EA"}],
                 "evidenceReferenceIds": ["e"]}, corr=corr)
    line("7 return create", st, d)
    st, d = call("POST", "/claims", {"shipmentId": sid, "carrierId": cid, "reasonCode": "DAMAGED",
                 "claimedAmount": "10.00", "currency": "TRY"}, corr=corr)
    line("8 claim (gercek carrier)", st, d)
    # :429 — tek korelasyon tum modullerde
    q = ('{$or:[{CorrelationId:"%s"},{CorrelationRoot:"%s"},{"Envelope.correlationId":"%s"}]}' % (corr, corr, corr))
    tot = mongo('let t=0;db.getCollectionNames().forEach(c=>{t+=db.getCollection(c).countDocuments(%s)});print(t)' % q)
    print(f"\n  :429 bu korelasyonu tasiyan belge: {tot}")

# -------------------------------------------------------------- :433 uygunluk
def conform():
    import yaml, jsonschema
    spec = yaml.safe_load(open(os.environ.get("G5_CONTRACT",
        "docs/analysis/contracts/shipment-bundle.openapi.yaml")))
    print(f"  {spec['info']['title']} {spec['info']['version']}\n")
    def tmpl_for(path):
        for t in spec["paths"]:
            if re.match("^" + re.sub(r"\{[^}]+\}", r"[^/]+", t) + "$", path): return t
    def schema_for(t, method, status):
        op = spec["paths"][t].get(method.lower())
        if not op: return None, "sozlesmede bu metot yok"
        r = op.get("responses", {}).get(str(status))
        if r is None: return None, f"{status} ILAN EDILMEMIS"
        n = 0
        while isinstance(r, dict) and "$ref" in r and n < 5:
            node = spec
            for part in r["$ref"].lstrip("#/").split("/"): node = node[part]
            r = node; n += 1
        c = (r.get("content") or {}).get("application/json")
        return (c.get("schema") if c else None), (None if c else "sozlesme govde ilan etmiyor")
    bad = ok = 0
    def check(name, method, path, body=None, **kw):
        nonlocal bad, ok
        st, d = call(method, path, body, **kw)
        t = tmpl_for(path)
        if t is None: note = "yol sozlesmede YOK"
        else:
            sch, why = schema_for(t, method, st)
            if why: note = why
            elif d is None: note = "govde yok"
            else:
                try:
                    jsonschema.Draft202012Validator(sch, resolver=jsonschema.RefResolver("", spec)).validate(d)
                    note = "sema UYUYOR"; ok += 1
                except jsonschema.ValidationError as e:
                    note = f"SEMA IHLALI: {e.message[:80]}"; bad += 1
        print(f"  {name:<24} {method:<5} {st:<4} {note}")
        return d
    s = check("createShipment", "POST", "/shipments", shipment_body(RUN)); sid = (s or {}).get("shipmentId")
    c = check("createCarrier", "POST", "/carriers", {"carrierCode": "K" + RUN[:6].upper(),
              "displayName": "K " + RUN, "supportedModes": ["Road"]})
    for n, m, p in (("getShipment", "GET", f"/shipments/{sid}"), ("listShipments", "GET", "/shipments"),
                    ("listCarriers", "GET", "/carriers"), ("listLoads", "GET", "/loads"),
                    ("listReturns", "GET", "/returns"), ("listClaims", "GET", "/claims")):
        check(n, m, p)
    check("getShipment404", "GET", f"/shipments/{uuid.uuid4()}")
    check("LE-header-yok", "GET", "/shipments", drop=["X-Legal-Entity-Id"])
    check("LE-bozuk", "GET", "/shipments", headers={"X-Legal-Entity-Id": "not-a-uuid"})
    check("LE-yabanci", "GET", "/shipments", headers={"X-Legal-Entity-Id": str(uuid.uuid4())})
    print(f"\n  uyan {ok} · IHLAL {bad}")
    return bad

# --------------------------------------------------- :425 :426 :427 kabul
def acceptance():
    print("  :425 bir create = her koleksiyonda bir kayit")
    tag = RUN + "a"; st, d = call("POST", "/shipments", shipment_body(tag)); sid = d["shipmentId"]
    for col in ("sce_shipments", "sce_shipment_history", "sce_shipment_outbox",
                "sce_shipment_audit", "sce_shipment_receipts"):
        js = 'print(db.{c}.countDocuments({{ShipmentId:"{i}"}})||db.{c}.countDocuments({{_id:"{i}"}}))'.format(c=col, i=sid)
        print(f"    {col:<24} {mongo(js)}")
    print("\n  :426 ayni anahtar = ikinci yazma yok")
    tag = RUN + "b"; k = "idem-" + tag; corr = str(uuid.uuid4())
    st1, d1 = call("POST", "/shipments", shipment_body(tag), corr=corr, key=k)
    st2, d2 = call("POST", "/shipments", shipment_body(tag), corr=corr, key=k)
    cnt = mongo('print(db.sce_shipments.countDocuments({{SourceDocumentId:"SO-{t}"}}))'.format(t=tag))
    same = d1["shipmentId"] == d2["shipmentId"]
    print(f"    {st1} / {st2} · ayni id: {same} · replay: {d2.get('idempotentReplay')} · kayit: {cnt}")
    print("\n  :427 gecis matrisi (yasam dongusu TEK korelasyon kokune bagli)")
    tag = RUN + "c"; root = str(uuid.uuid4())
    st, d = call("POST", "/shipments", shipment_body(tag), corr=root); sid = d["shipmentId"]
    for target, exp in (("Planned", 200), ("Delivered", 422), ("Dispatched", 200),
                        ("Planned", 422), ("InTransit", 200)):
        st, _ = call("POST", f"/shipments/{sid}/transition",
                     {"targetStatus": target, "occurredAt": "2030-01-01T09:00:00Z"}, corr=root)
        print(f"    -> {target:<12} {st:<4} (beklenen {exp}) {'OK' if st == exp else '*** FARK ***'}")
    hist = mongo('print(db.sce_shipment_history.countDocuments({{ShipmentId:"{i}"}}))'.format(i=sid))
    print(f"    lifecycle girdisi: {hist}  (4 bekleniyor)")

if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["chain", "conform", "acceptance", "all"])
    a = ap.parse_args()
    rc = 0
    if a.command in ("chain", "all"): chain(); print()
    if a.command in ("conform", "all"): rc = conform() or 0; print()
    if a.command in ("acceptance", "all"): acceptance()
    sys.exit(1 if rc else 0)
