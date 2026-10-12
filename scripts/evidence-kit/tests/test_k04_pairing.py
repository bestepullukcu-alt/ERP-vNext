#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.2 unit test for the K04 key-pairing check (finding F1).

Run: python3 tests/test_k04_pairing.py   (no service, no Mongo, no network; writes only to a temp dir; no bytecode)
What it proves:
  1. the v1.2 service-map + key-pairing.tsv PASS;
  2. the v1.1 WRONG table (MDM PlatformRegistration:InternalApiKey rendered in group platform-mdm-active, MDM
     ModuleRegistrationCredentialSecret left to the committed Development value) FAILS for auth-platform AND
     platform-mdm-active — the exact defect of the Q63 review F1, which the v1.1 check reported as PASS;
  3. the v1.1 pairing table itself (REQUIRED_PAIRS, no source citation) fails closed: every row NOT FOUND;
  4. citation verification: token on the cited line = cited; elsewhere = moved; absent = NOT FOUND.
Effective values are simulated exactly as K04 `render` would produce them (rotated group -> @@LANE:<g>@@, forced keys,
otherwise the committed appsettings). The committed Development appsettings give the four Platform/MDM keys one shared
dev value (Q63 review §2); here that value is the fake string below, never a real one.
"""
import csv, io, pathlib, sys, tempfile, unittest

sys.dont_write_bytecode = True
KIT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(KIT))
import k04_config as k04  # noqa: E402

FAKE_DEV = "FAKE-committed-development-value-not-a-secret"
COMMITTED = {  # committed Development appsettings, as fingerprinted by the Q63 review (values replaced by a fake)
    ("platform", "AuthService__InternalApiKey"): FAKE_DEV,
    ("platform", "ModuleRegistrationCredentials__Mdm__ActiveSecret"): FAKE_DEV,
    ("platform", "ModuleRegistrationCredentials__Mdm__Identifier"): "ditenmdmservice",
    ("mdm", "PlatformRegistration__InternalApiKey"): FAKE_DEV,
    ("mdm", "PlatformRegistration__ModuleRegistrationCredentialSecret"): FAKE_DEV,
    ("mdm", "PlatformRegistration__ModuleRegistrationCredentialIdentifier"): "ditenmdmservice",
}
# The v1.1 (proposal-02) MDM row secret_keys, verbatim — the wrong table.
V11_MDM_SECRET_KEYS = ("jwt:JwtSettings__Secret;jwt-iss:JwtSettings__Issuer;jwt-aud:JwtSettings__Audience;"
                       "platform-mdm-active:PlatformRegistration__InternalApiKey;svcid:PlatformServiceIdentity__Secret")
# The v1.1 REQUIRED_PAIRS, verbatim, as a pairing table without citations.
V11_REQUIRED_PAIRS = {
    "auth-platform": [("platform", "AuthService__InternalApiKey"), ("auth", "PlatformService__InternalApiKey"),
                      ("auth", "InternalEventAuth__ApiKey"), ("supplychain", "PlatformRegistration__InternalApiKey"),
                      ("web", "Platform__InternalApiKey")],
    "platform-mdm-active": [("platform", "ModuleRegistrationCredentials__Mdm__ActiveSecret"),
                            ("mdm", "PlatformRegistration__InternalApiKey")],
    "svcid": [("platform", "MdmServiceIdentity__Secret"), ("mdm", "PlatformServiceIdentity__Secret")],
}
ROTATE = set(k04.DEFAULT_ROTATE.split())
LANE = {"auth", "platform", "mdm", "supplychain", "gateway", "web"}


def load_map(v11=False):
    rows = {r["service"]: r for r in csv.DictReader(open(KIT / "service-map.tsv", newline=""), delimiter="\t")}
    if v11:
        rows["mdm"] = dict(rows["mdm"], secret_keys=V11_MDM_SECRET_KEYS)
    return rows


def simulated_effective(rows):
    env = {}
    for svc, r in rows.items():
        for k, v in k04.pairs(r["forced_keys"]):
            env[(svc, k)] = v.replace("@@SUFFIX@@", "UnitTest01")
        for g, k in (x.split(":", 1) for x in k04.pairs_raw(r["secret_keys"])):
            if g in ROTATE:
                env[(svc, k)] = k04.PLACEHOLDER.format(g)
    return lambda svc, key: env.get((svc, key), COMMITTED.get((svc, key)))


def pairing_results(rows):
    table = k04.load_pairing(KIT / "key-pairing.tsv")
    res, fails = k04.check_pairing(table, LANE, simulated_effective(rows))
    return {g: r for g, _, _, r in res}, fails


class PairingTests(unittest.TestCase):
    def test_v12_table_passes(self):
        res, fails = pairing_results(load_map())
        self.assertEqual(fails, [], fails)
        for g in ("auth-platform", "platform-mdm-active", "svcid", "jwt", "mdm-registration-id", "svcid-kid"):
            self.assertTrue(res[g].startswith("PASS"), (g, res[g]))
        self.assertIn("6 keys", res["auth-platform"])

    def test_v11_wrong_table_fails(self):
        res, fails = pairing_results(load_map(v11=True))
        self.assertEqual(res["auth-platform"], "FAIL")          # MDM audit key not in the Auth-Platform group -> 401
        self.assertEqual(res["platform-mdm-active"], "FAIL")    # MDM registration secret left at the dev value -> rejected
        self.assertTrue(any("platform-mdm-active" in f for f in fails))

    def test_v11_required_pairs_uncited_fail_closed(self):
        table = [{"group": g, "kind": "secret", "service": s, "key": k, "cites": ""}
                 for g, ms in V11_REQUIRED_PAIRS.items() for s, k in ms]
        with tempfile.TemporaryDirectory() as w:
            rows, fails = k04.verify_citations(w, table, LANE)
        self.assertEqual(len(fails), len(table))
        self.assertTrue(all(r[4].startswith("NOT FOUND") for r in rows))

    def test_citation_states(self):
        with tempfile.TemporaryDirectory() as w:
            p = pathlib.Path(w, "source", "svc", "Opt.cs"); p.parent.mkdir(parents=True)
            p.write_text("line1\npublic string Secret { get; set; }\nline3\n")
            table = [{"group": "g", "kind": "secret", "service": "auth", "key": "K", "cites": "svc/Opt.cs:2=Secret"},
                     {"group": "g", "kind": "secret", "service": "auth", "key": "K2", "cites": "svc/Opt.cs:3=Secret"},
                     {"group": "g", "kind": "secret", "service": "auth", "key": "K3", "cites": "svc/Opt.cs:2=ActiveSecret"},
                     {"group": "g", "kind": "secret", "service": "auth", "key": "K4", "cites": "svc/Missing.cs:1=X"}]
            rows, fails = k04.verify_citations(w, table, {"auth"})
        states = [r[4] for r in rows]
        self.assertEqual(states[0], "cited")
        self.assertTrue(states[1].startswith("moved"))
        self.assertEqual(states[2], "NOT FOUND (token)")
        self.assertEqual(states[3], "NOT FOUND (file)")
        self.assertEqual(len(fails), 2)


if __name__ == "__main__":
    buf = io.StringIO()
    r = unittest.TextTestRunner(stream=buf, verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(PairingTests))
    print(buf.getvalue().strip())
    sys.exit(0 if r.wasSuccessful() else 1)
