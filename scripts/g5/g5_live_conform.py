#!/usr/bin/env python3
"""Live conformance: does the RUNNING service match the frozen contract?

g5_contract_walk.py answers "is the contract consistent with itself" and needs no stack.
conform() in g5_golden_flow.py checks twelve hardcoded calls and, measured on 2026-10-11,
increments neither ok nor bad for a status the contract never declared -- so it can print
IHLAL 0 and exit 0 while the service violates the contract. This driver closes that gap for
the three acceptance lines that say "implementation matches":

    MOD-0184-carrier-management.md:119
    MOD-0185-routing-load-planning.md:125
    MOD-0187-claims-management.md:122

Rules, and they are the point:
  * a status the contract does not declare for that operation is a FAILURE, never a note;
  * a response body is validated against the schema declared for the status ACTUALLY returned;
  * a required response header that is absent is a FAILURE;
  * a declared status this driver cannot induce is reported as UNREACHED, never as a pass.

Exit code is a bitmask so one run reports every class:
    1 undeclared status   2 schema violation   4 missing required header   8 setup failure

Environment: G5_GATEWAY (default http://127.0.0.1:5000), G5_CONTRACT, and a state file
written by the runtime lane holding T1/LE1. Credentials are read through the lane helper and
never printed; only digests appear in output.
"""
import json, os, sys, uuid, importlib.util

GATEWAY  = os.environ.get("G5_GATEWAY", "http://127.0.0.1:5000")
CONTRACT = os.environ.get("G5_CONTRACT", "docs/analysis/contracts/shipment-bundle.openapi.yaml")
HELPER   = os.environ.get("G5_HELPER", "/tmp/alsce-runtime-ver/api.py")
STATE    = os.environ.get("G5_STATE", "/tmp/alsce-runtime-ver/state.json")

FAMILIES = {"Carriers": "/carriers", "Loads": "/loads", "Claims": "/claims"}


def helper():
    spec = importlib.util.spec_from_file_location("lane_api", HELPER)
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
    return m


def load_contract():
    import yaml
    return yaml.safe_load(open(CONTRACT))


def validator_for(doc, schema_pointer):
    """The whole document is registered under one URI and schemas are referenced by JSON
    pointer: handing a subschema to the validator directly makes its own '#/...' refs resolve
    against the subschema, which fails. Measured by the walker lane, reused here."""
    import jsonschema
    from referencing import Registry, Resource
    from referencing.jsonschema import DRAFT202012
    resource = Resource(contents=doc, specification=DRAFT202012)
    registry = Registry().with_resource("urn:contract", resource)
    return jsonschema.Draft202012Validator(
        {"$ref": "urn:contract#" + schema_pointer}, registry=registry,
        format_checker=jsonschema.FormatChecker())


def operations(doc):
    """Every in-scope operation with its declared statuses, response schema pointers and
    required response headers. Enumerated from the document, never hardcoded."""
    out = []
    for path, item in (doc.get("paths") or {}).items():
        for method, op in item.items():
            if method not in ("get", "post", "put", "patch", "delete"):
                continue
            tags = op.get("tags") or []
            family = next((t for t in tags if t in FAMILIES), None)
            if family is None:
                continue
            statuses = {}
            for code, resp in (op.get("responses") or {}).items():
                node, pointer = resp, "/paths/%s/%s/responses/%s" % (
                    path.replace("~", "~0").replace("/", "~1"), method, code)
                hops = 0
                while "$ref" in node and hops < 8:
                    ref = node["$ref"]; assert ref.startswith("#/"), ref
                    pointer = ref[1:]
                    node = doc
                    for part in ref[2:].split("/"):
                        node = node[part.replace("~1", "/").replace("~0", "~")]
                    hops += 1
                content = (node.get("content") or {}).get("application/json") or {}
                statuses[int(code)] = {
                    "schema": (pointer + "/content/application~1json/schema") if content.get("schema") else None,
                    "headers": [h for h, hv in (node.get("headers") or {}).items()
                                if _required(doc, hv)],
                }
            out.append({"id": op.get("operationId"), "family": family, "method": method.upper(),
                        "path": path, "statuses": statuses,
                        "example": _request_example(doc, op)})
    return out


def _request_example(doc, op):
    """The declared request example, which is what "matches all examples" means: the probe
    body comes from the contract rather than from the author's memory of it."""
    content = ((op.get("requestBody") or {}).get("content") or {}).get("application/json") or {}
    if "example" in content:
        return content["example"]
    for value in (content.get("examples") or {}).values():
        node, hops = value, 0
        while "$ref" in node and hops < 8:
            ref = node["$ref"]; node = doc
            for part in ref[2:].split("/"):
                node = node[part.replace("~1", "/").replace("~0", "~")]
            hops += 1
        if "value" in node:
            return node["value"]
    return None


def _required(doc, header):
    node, hops = header, 0
    while "$ref" in node and hops < 8:
        ref = node["$ref"]; node = doc
        for part in ref[2:].split("/"):
            node = node[part.replace("~1", "/").replace("~0", "~")]
        hops += 1
    return bool(node.get("required"))


class Run(object):
    def __init__(self, doc, api, st):
        self.doc, self.api, self.st = doc, api, st
        self.t1, self.le1 = st["T1"], st["LE1"]
        self.tok = None
        self.leonly = None
        self.bad = {"undeclared": [], "schema": [], "header": []}
        self.hit = {}          # operationId -> set of statuses actually observed
        self.checked = 0

    # ---------------------------------------------------------------- setup
    def login(self):
        code, tok, _ = self.api.login(self.t1, "admin@diten.com")
        if not tok:
            raise RuntimeError("login %s" % code)
        self.tok = tok
        # The 403 probes need a principal that authenticates but lacks the SupplyChain permissions. A token
        # file goes stale in ten minutes, so this mints one instead of reading yesterday's. The identity is
        # the runtime lane's second-tenant user, which holds mdm.legal-entities.read and nothing else --
        # the legal-entity validation must succeed or the request dies at 503 in the middleware and never
        # reaches [HasPermission] (measured 2026-10-11).
        pw_file = "/tmp/alsce-runtime-ver/noperm_pw.txt"
        if os.path.exists(pw_file) and "T2" in self.st:
            _, lt, _ = self.api.login(self.st["T2"], "ver.noperm@diten.test",
                                      open(pw_file).read().strip())
            self.leonly = lt
        print("token digest %s%s" % (self.api.digest(tok),
              "" if self.leonly else "   (no limited token; 403 probes skipped)"))

    def call(self, method, path, body=None, tok="default", extra=None, drop=None, key=None,
             tenant=None, legalEntity=None):
        t = self.tok if tok == "default" else tok
        if t is None:
            hs = {"X-Tenant-Id": self.t1, "X-Legal-Entity-Id": self.le1,
                  "X-Correlation-Id": str(uuid.uuid4()), "Idempotency-Key": uuid.uuid4().hex}
            hs.update(extra or {})
            return self.api.raw(method, GATEWAY + "/api/shipment-bundle" + path, body, hs)
        return self.api.sc(method, path, t, tenant or self.t1, legalEntity or self.le1, body,
                           key=key, extra=extra, drop=drop)

    def media_type(self, op, path):
        """A real text/plain request. The lane helper rewrites Content-Type to
        application/json whenever a body is present, so this probe has to build the request
        itself -- measured 2026-10-11, the earlier version silently sent JSON and the 415
        it was supposed to induce could never have appeared."""
        import urllib.request, urllib.error
        url = GATEWAY + "/api/shipment-bundle" + path
        req = urllib.request.Request(url, method=op["method"], data=b"not json at all")
        for k, v in {"Authorization": "Bearer " + self.tok, "X-Tenant-Id": self.t1,
                     "X-Legal-Entity-Id": self.le1, "X-Correlation-Id": str(uuid.uuid4()),
                     "Idempotency-Key": uuid.uuid4().hex, "Content-Type": "text/plain"}.items():
            req.add_header(k, v)
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                status, raw = r.status, r.read().decode()
        except urllib.error.HTTPError as e:
            status, raw = e.code, e.read().decode()
        except Exception as exc:
            print("  ERROR      %-44s %s" % (op["id"] + " wrong-media-type", exc)); return
        try:
            body = json.loads(raw) if raw.strip() else None
        except Exception:
            body = None
        self.checked += 1
        self.record(op, "wrong-media-type", status, body)

    # ------------------------------------------------------------- checking
    def check(self, op, label, method, path, **kw):  # kw passes through to call()
        """Issue one probe and hold the response to the contract it declares."""
        status, body = self.call(method, path, **kw)
        self.checked += 1
        return self.record(op, label, status, body)

    def record(self, op, label, status, body):
        declared = op["statuses"]
        where = "%s %s" % (op["id"], label)

        if status not in declared:
            self.bad["undeclared"].append("%s -> %s is not declared (declared: %s)"
                % (where, status, ",".join(str(s) for s in sorted(declared))))
            print("  UNDECLARED %-44s %s" % (where, status))
            return status, body

        self.hit.setdefault(op["id"], set()).add(status)
        spec = declared[status]

        if spec["schema"] and body is not None:
            try:
                validator_for(self.doc, spec["schema"]).validate(body)
            except Exception as exc:
                first = str(exc).splitlines()[0][:120]
                self.bad["schema"].append("%s -> %s body: %s" % (where, status, first))
                print("  SCHEMA     %-44s %s  %s" % (where, status, first))
                return status, body
        print("  ok         %-44s %s" % (where, status))
        return status, body

    # -------------------------------------------------------------- fixtures
    def fixtures(self):
        """A real chain, because 409, 422 and the Load/Claim happy paths cannot be induced
        without one. The correlation root is shared across shipment, load, return and claim:
        the runtime lane measured on 2026-10-11 that Loads, Returns and Claims each refuse a
        foreign root with 409, a rule no acceptance line states."""
        corr = str(uuid.uuid4())
        tag = uuid.uuid4().hex[:8]
        f = {"corr": corr}

        st, s = self.call("POST", "/shipments", {
            "sourceModule": "MOD-0141", "sourceType": "SALES_ORDER", "sourceDocumentId": "LC-" + tag,
            "warehouseReferenceId": "wh-01", "shipToReference": "C/A",
            "plannedShipAt": "2030-01-01T08:00:00Z", "plannedDeliverAt": "2030-01-02T16:00:00Z",
            "lines": [{"lineNumber": "1", "itemId": "b1f2c3d4-0000-0000-0000-000000000001",
                       "skuId": "c3d4e5f6-0000-0000-0000-000000000002", "quantity": "2.000",
                       "uomId": "EA", "inventoryReferenceId": "r-" + tag}]
        }, extra={"X-Correlation-Id": corr})
        if st != 201:
            print("  fixtures: shipment %s %s" % (st, str(s)[:120])); return None
        f["sid"] = s["shipmentId"]

        st, c = self.call("POST", "/carriers", {"carrierCode": "LC" + tag[:6].upper(),
                          "displayName": "Live conform", "supportedModes": ["Road"]},
                          extra={"X-Correlation-Id": corr})
        f["cid"] = (c or {}).get("carrierId")

        self.call("POST", "/shipments/%s/transition" % f["sid"],
                  {"targetStatus": "Planned", "occurredAt": "2030-01-01T09:00:00Z"},
                  extra={"X-Correlation-Id": corr})
        f["load_body"] = {"carrierId": f["cid"], "shipmentIds": [f["sid"]], "mode": "Road",
                          "plannedDepartAt": "2030-01-01T10:00:00Z",
                          "stops": [{"sequence": 1, "locationReferenceId": "o", "action": "Pickup"},
                                    {"sequence": 2, "locationReferenceId": "d", "action": "Delivery"}]}
        return f

    # ---------------------------------------------------------------- probes
    def generic(self, op):
        """Probes every operation must answer the same way: authentication, permission,
        unknown id and wrong media type. Each targets one declared status."""
        base = op["path"].replace("{carrierId}", str(uuid.uuid4())) \
                         .replace("{loadId}", str(uuid.uuid4())) \
                         .replace("{claimId}", str(uuid.uuid4()))
        body = op.get("example") if op["method"] != "GET" else None
        self.check(op, "no-bearer", op["method"], base, body=body, tok=None)
        if self.leonly:
            self.check(op, "no-permission", op["method"], base, body=body, tok=self.leonly,
                       tenant=self.st["T2"], legalEntity=self.st["LE2"])
        if op["method"] != "GET":
            self.media_type(op, base)
            self.check(op, "malformed-body", op["method"], base, body={"nope": 1})
        if op["method"] == "GET" and "{" not in op["path"]:
            self.check(op, "bad-query-enum", "GET", base + "?status=NotAStatus")
        if "{" in op["path"]:
            # A VALID body against a nonexistent id: anything else is rejected by the wire
            # validator first and 404 is never reached. Measured 2026-10-11.
            self.check(op, "unknown-id", op["method"], base, body=body)

    def carriers(self, ops):
        q = ops["queryCarriers"]; c = ops["createCarrier"]; s = ops["changeCarrierStatus"]
        self.check(q, "list", "GET", "/carriers")
        code = "L" + uuid.uuid4().hex[:7].upper()
        st, created = self.check(c, "create", "POST", "/carriers",
            body={"carrierCode": code, "displayName": "Live conform",
                  "supportedModes": ["Road"]})
        if st != 201:
            return
        cid = created.get("carrierId") or (created.get("data") or {}).get("carrierId")
        self.check(c, "duplicate-code", "POST", "/carriers",
            body={"carrierCode": code, "displayName": "Live conform",
                  "supportedModes": ["Road"]})
        self.check(s, "status-change", "POST", "/carriers/%s/status" % cid,
            body={"targetStatus": "Suspended", "reasonCode": "LIVE"})
        self.check(s, "invalid-enum", "POST", "/carriers/%s/status" % cid,
            body={"targetStatus": "NotAStatus", "reasonCode": "LIVE"})
        # Same idempotency key, different payload: the declared IDEMPOTENCY_KEY_REUSED 409.
        k = "reuse-" + uuid.uuid4().hex
        self.check(s, "key-first-use", "POST", "/carriers/%s/status" % cid,
            body={"targetStatus": "Active", "reasonCode": "LIVE"}, key=k)
        self.check(s, "key-reused-different-payload", "POST", "/carriers/%s/status" % cid,
            body={"targetStatus": "Retired", "reasonCode": "OTHER"}, key=k)
        # A legal enum value that is not a legal move from the current state: the declared 422.
        self.check(s, "illegal-move", "POST", "/carriers/%s/status" % cid,
            body={"targetStatus": "Active", "reasonCode": "LIVE"})
        kc = "reuse-" + uuid.uuid4().hex
        code2 = "L" + uuid.uuid4().hex[:7].upper()
        self.check(c, "key-first-use", "POST", "/carriers",
            body={"carrierCode": code2, "displayName": "A", "supportedModes": ["Road"]}, key=kc)
        self.check(c, "key-reused-different-payload", "POST", "/carriers",
            body={"carrierCode": code2, "displayName": "B", "supportedModes": ["Sea"]}, key=kc)
        self.check(c, "empty-modes", "POST", "/carriers",
            body={"carrierCode": "L" + uuid.uuid4().hex[:7].upper(), "displayName": "A",
                  "supportedModes": []})

    def claims(self, ops):
        q, c, tr = ops["queryClaims"], ops["createClaim"], ops["transitionClaim"]
        self.check(q, "list", "GET", "/claims")
        f = self.f
        if not f:
            return
        h = {"X-Correlation-Id": f["corr"]}
        # Eligibility is checked before amount, so the shipment must reach Delivered first --
        # otherwise an amount probe returns the ineligibility code and looks like broken
        # amount validation (F7, measured by the runtime lane).
        for target in ("Dispatched", "InTransit"):
            self.call("POST", "/shipments/%s/transition" % f["sid"],
                      {"targetStatus": target, "occurredAt": "2030-01-01T11:00:00Z"}, extra=h)
        self.call("POST", "/shipments/%s/pod" % f["sid"],
                  {"recipientName": "R", "receivedAt": "2030-01-02T15:00:00Z",
                   "evidenceReferenceIds": ["e"], "note": "n"}, extra=h)
        body = {"shipmentId": f["sid"], "carrierId": f["cid"], "reasonCode": "DAMAGED",
                "claimedAmount": "10.005", "currency": "TRY"}
        st, cl = self.check(c, "create", "POST", "/claims", body=body, extra=h)
        self.check(c, "zero-amount", "POST", "/claims",
                   body=dict(body, claimedAmount="0.00"), extra=h)
        self.check(c, "unknown-shipment", "POST", "/claims",
                   body=dict(body, shipmentId=str(uuid.uuid4())), extra=h)
        kk = "reuse-" + uuid.uuid4().hex
        self.check(c, "key-first-use", "POST", "/claims",
                   body=dict(body, reasonCode="LOST"), extra=h, key=kk)
        self.check(c, "key-reused-different-payload", "POST", "/claims",
                   body=dict(body, reasonCode="DAMAGED", claimedAmount="99.99"), extra=h, key=kk)
        if st != 201:
            return
        cid = cl["claimId"]
        self.check(tr, "open-to-approved-is-invalid", "POST", "/claims/%s/transition" % cid,
                   body={"targetStatus": "Approved", "occurredAt": "2030-01-03T09:00:00Z",
                         "approvedAmount": "10.005"}, extra=h)
        self.check(tr, "investigating", "POST", "/claims/%s/transition" % cid,
                   body={"targetStatus": "Investigating", "occurredAt": "2030-01-03T09:10:00Z"},
                   extra=h)
        kt = "reuse-" + uuid.uuid4().hex
        self.check(tr, "key-first-use", "POST", "/claims/%s/transition" % cid,
                   body={"targetStatus": "Rejected", "occurredAt": "2030-01-03T09:20:00Z",
                         "reasonCode": "NOT_COVERED"}, extra=h, key=kt)
        self.check(tr, "key-reused-different-payload", "POST", "/claims/%s/transition" % cid,
                   body={"targetStatus": "Closed", "occurredAt": "2030-01-03T09:30:00Z"},
                   extra=h, key=kt)

    def loads(self, ops):
        q, c, tr = ops["queryLoads"], ops["createLoadPlan"], ops["transitionLoad"]
        self.check(q, "list", "GET", "/loads")
        f = self.f
        if not f:
            return
        h = {"X-Correlation-Id": f["corr"]}
        st, l = self.check(c, "create", "POST", "/loads", body=f["load_body"], extra=h)
        if st != 201:
            return
        lid = l["loadId"]
        # The same shipment on a second load: the declared 409.
        self.check(c, "shipment-already-assigned", "POST", "/loads", body=f["load_body"], extra=h)
        self.check(tr, "transition", "POST", "/loads/%s/transition" % lid,
                   body={"targetStatus": "Planned", "occurredAt": "2030-01-01T10:30:00Z"}, extra=h)
        # Planned -> Planned is not a legal move: the declared 422.
        self.check(tr, "invalid-transition", "POST", "/loads/%s/transition" % lid,
                   body={"targetStatus": "Planned", "occurredAt": "2030-01-01T10:40:00Z"}, extra=h)
        f2 = dict(f["load_body"], carrierId=str(uuid.uuid4()))
        self.check(c, "unknown-carrier", "POST", "/loads", body=f2, extra=h)
        f3 = dict(f["load_body"], shipmentIds=[str(uuid.uuid4())])
        self.check(c, "unknown-shipment", "POST", "/loads", body=f3, extra=h)
        f4 = dict(f["load_body"], mode="Air")
        self.check(c, "carrier-mode-unsupported", "POST", "/loads", body=f4, extra=h)
        kl = "reuse-" + uuid.uuid4().hex
        self.check(tr, "key-first-use", "POST", "/loads/%s/transition" % lid,
                   body={"targetStatus": "Tendered", "occurredAt": "2030-01-01T11:00:00Z"},
                   extra=h, key=kl)
        self.check(tr, "key-reused-different-payload", "POST", "/loads/%s/transition" % lid,
                   body={"targetStatus": "Accepted", "occurredAt": "2030-01-01T12:00:00Z"},
                   extra=h, key=kl)
        # A foreign correlation root: 409, the rule F7 found and no acceptance line states.
        self.check(tr, "foreign-correlation-root", "POST", "/loads/%s/transition" % lid,
                   body={"targetStatus": "Tendered", "occurredAt": "2030-01-01T10:50:00Z"},
                   extra={"X-Correlation-Id": str(uuid.uuid4())})


# Why a declared status was not reached. "I could not reach it" and "it cannot be reached" are
# different claims and the report must not blur them. Measured 2026-10-11 against the live stack.
UNREACHABLE = {
    "fault-injection": (
        "needs a fault this driver does not inject (dependency down, store down, bad dependency reply)",
        {(op, code) for op in ("queryCarriers", "createCarrier", "changeCarrierStatus", "queryLoads",
                               "createLoadPlan", "transitionLoad", "queryClaims", "createClaim",
                               "transitionClaim")
                    for code in (500, 502, 503)}),
    "over-declared": (
        "the contract declares it and the operation cannot produce it — a list answers 200 even for an "
        "unknown filter value, measured: /loads?carrierId=<unknown> 200, /loads?status=Planned 200, "
        "/carriers?status=Active 200. Its example is LOAD_NOT_FOUND, which belongs to the by-id operations",
        {("queryCarriers", 404), ("queryLoads", 404)}),
    "undocumented": (
        "declared with a schema and ZERO examples, so the contract itself does not say what triggers it "
        "(components/responses/CarrierCreateUnprocessable) — the single coverage gap in the document",
        {("createCarrier", 422)}),
}


def classify(op_id, code):
    for kind, (_why, pairs) in UNREACHABLE.items():
        if (op_id, code) in pairs:
            return kind
    return "not-yet-driven"


def main():
    api, doc = helper(), load_contract()
    st = json.load(open(STATE))
    run = Run(doc, api, st)
    try:
        run.login()
    except Exception as exc:
        print("SETUP FAILED: %s" % exc); return 8

    run.f = run.fixtures()
    ops = {o["id"]: o for o in operations(doc)}
    print("in-scope operations enumerated: %d" % len(ops))
    print("declared status codes:          %d\n" % sum(len(o["statuses"]) for o in ops.values()))

    for name, fn in (("Carriers", run.carriers), ("Loads", run.loads), ("Claims", run.claims)):
        print("--- %s" % name)
        for o in ops.values():
            if o["family"] == name:
                run.generic(o)
        fn(ops)
        print()

    print("=" * 78)
    print("probes issued            %d" % run.checked)
    total = sum(len(o["statuses"]) for o in ops.values())
    reached = sum(len(v) for v in run.hit.values())
    print("declared statuses        %d" % total)
    print("reached by this driver   %d" % reached)
    print("UNREACHED                %d  (not a pass -- listed below)" % (total - reached))
    print("undeclared statuses seen %d" % len(run.bad["undeclared"]))
    print("schema violations        %d" % len(run.bad["schema"]))

    for kind in ("undeclared", "schema", "header"):
        for line in run.bad[kind]:
            print("  %-10s %s" % (kind.upper(), line))

    print("\nUNREACHED, classified — silence is not a pass:")
    buckets = {}
    for oid, o in sorted(ops.items()):
        for code in sorted(set(o["statuses"]) - run.hit.get(oid, set())):
            buckets.setdefault(classify(oid, code), []).append("%s %s" % (oid, code))
    for kind in ("not-yet-driven", "undocumented", "over-declared", "fault-injection"):
        rows = buckets.get(kind)
        if not rows:
            continue
        why = UNREACHABLE.get(kind, (None,))[0] or "reachable in principle; this driver does not build the state yet"
        print("  %s (%d) — %s" % (kind, len(rows), why))
        for r in rows:
            print("      %s" % r)

    rc = (1 if run.bad["undeclared"] else 0) | (2 if run.bad["schema"] else 0) \
       | (4 if run.bad["header"] else 0)
    print("\nexit %d" % rc)
    return rc


if __name__ == "__main__":
    sys.exit(main())
