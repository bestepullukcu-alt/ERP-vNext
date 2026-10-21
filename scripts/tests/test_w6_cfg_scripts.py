"""WP-VW-W6 W6-CFG — the role / permission matrix, the grant scripts and the workflow / prefix console scripts.

  * matrix ↔ manifest: every commercial.* key of the Platform CrmManifestProvider is granted to a role or listed as not
    granted; a matrix key the manifest does not declare (a typo) and a '.*' row that matches nothing are errors;
  * the live console script carries the same matrix as scripts/rbac/w6_cfg_role_matrix.json;
  * dry-run writes nothing (local Mongo script and the three console scripts), --apply writes, a second run adds nothing;
  * COM-PREORDER-APPROVAL has no candidate of its own (else Platform ignores the runtime candidates = the line managers).

The console scripts run under Node with a fake window (skipped when node is not on PATH). Run:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'
"""

from __future__ import annotations

import io
import json
import os
import shutil
import subprocess
import sys
import unittest
import uuid
from contextlib import redirect_stdout

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(SCRIPTS, "rbac"))
import grant_w6_cfg_rbac_97c5 as grant  # noqa: E402

GOLIVE = os.path.join(SCRIPTS, "golive")
LIVE_GRANTS = os.path.join(GOLIVE, "w6-cfg-grant-roles-live.js")
TEMPLATES = os.path.join(GOLIVE, "w6-cfg-workflow-templates.js")
PREFIX = os.path.join(GOLIVE, "w6-cfg-preorder-prefix.js")
NODE = shutil.which("node")


class FakeStore:
    """The MongoStore surface, in memory."""

    def __init__(self, role_names, keys, held=()):
        self.role_list = [{"_id": uuid.uuid4(), "Name": n} for n in role_names]
        self.perms = {k: {"_id": uuid.uuid4(), "Key": k} for k in keys}
        by_name = {r["Name"]: r["_id"] for r in self.role_list}
        self.grants = {(by_name[r], self.perms[k]["_id"]) for r, k in held}
        self.inserted = []

    def roles(self):
        return self.role_list

    def permissions(self, keys):
        return {k: self.perms[k] for k in keys if k in self.perms}

    def held_permission_ids(self, role_id):
        return {p for r, p in self.grants if r == role_id}

    def native_template(self):
        return {"CreatedAt": [1, 0], "AssignedAt": "2026-10-01"}

    def new_grant(self, role_id, permission_id, template, marker):
        return {"RoleId": role_id, "PermissionId": permission_id, "CreatedBy": marker}

    def insert(self, doc):
        self.inserted.append(doc)
        self.grants.add((doc["RoleId"], doc["PermissionId"]))

    def non_subtype4(self, marker):
        return []


def run_quiet(*args, **kwargs):
    buf = io.StringIO()
    with redirect_stdout(buf):
        code = grant.run(*args, **kwargs)
    return code, buf.getvalue()


def workflow_keys(matrix):
    return sorted({k for keys in matrix["workflow_needs"].values() for k in keys})


def all_keys(universe, matrix):
    plan, _ = grant.resolve(matrix, universe)
    return sorted({k for _, keys in plan for k in keys} | set(workflow_keys(matrix)))


class MatrixAgainstManifest(unittest.TestCase):
    def setUp(self):
        self.matrix = grant.load_matrix()
        self.universe = grant.manifest_commercial_keys()

    def test_the_manifest_is_read_not_a_hand_list(self):
        # the 27 keys of W6-BE-a / BE-b / WEB-a (2026-10-21); more is fine (W6-WEB-b), fewer means the parser broke
        self.assertGreaterEqual(len(self.universe), 27)
        self.assertIn("commercial.pre-orders.approve", self.universe)
        self.assertIn("commercial.fx-rates.import", self.universe)
        self.assertFalse([k for k in self.universe if k.startswith("commercial.preorder.")], "event codes are not keys")

    def test_every_manifest_key_is_granted_or_listed(self):
        plan, errors = grant.resolve(self.matrix, self.universe)
        self.assertEqual(errors, [])
        granted = {k for _, keys in plan for k in keys}
        for key in self.universe:
            self.assertTrue(key in granted or key in grant.not_granted(plan, self.universe), key)
        self.assertEqual(grant.not_granted(plan, self.universe), [], "Admin gets commercial.*")
        self.assertEqual(grant.admin_only(plan, self.universe), ["commercial.pre-orders.export"])

    def test_k_w6_roles(self):
        plan = dict(grant.resolve(self.matrix, self.universe)[0])
        self.assertEqual(set(plan), {"CrmUser", "Team Leader", "Department Manager", "Admin"})
        self.assertNotIn("commercial.pre-orders.approve", plan["CrmUser"])
        self.assertNotIn("commercial.pre-orders.read-all", plan["CrmUser"])
        for key in ("crm.account.read", "crm.contact.read", "crm.account-contact.read", "crm.visit-plan.read",
                    "mdm.product-master.read"):
            self.assertIn(key, plan["CrmUser"])
        self.assertEqual(sorted(plan["Team Leader"]),
                         ["commercial.pre-orders.approve", "commercial.pre-orders.cancel", "commercial.pre-orders.read"])
        for key in ("commercial.price-lists.publish", "commercial.pricing-rules.publish", "commercial.pre-order-settings.update",
                    "commercial.fx-rates.create", "commercial.pre-orders.record-depot-response"):
            self.assertIn(key, plan["Department Manager"])
        self.assertEqual(sorted(plan["Admin"]), self.universe)

    def test_a_typo_and_an_empty_wildcard_are_errors(self):
        bad = {"marker": "x", "roles": [{"role": "R", "keys": ["commercial.pre-orders.aprove", "commercial.nothing.*"]}]}
        _, errors = grant.resolve(bad, self.universe)
        self.assertEqual(len(errors), 2)

    def test_a_new_manifest_key_no_row_covers_is_reported(self):
        no_admin = {"marker": "x", "roles": [r for r in self.matrix["roles"] if r["role"] != "Admin"]}
        universe = self.universe + ["commercial.new-thing.read"]
        plan, _ = grant.resolve(no_admin, universe)
        self.assertIn("commercial.new-thing.read", grant.not_granted(plan, universe))
        code, out = run_quiet(FakeStore([], []), no_admin, universe, {}, apply=False)
        self.assertIn("commercial.new-thing.read", out)


class LocalGrantScript(unittest.TestCase):
    ROLES = ["Admin", "CrmUser", "Team Leader", "Department Manager"]

    def setUp(self):
        self.matrix = grant.load_matrix()
        self.universe = grant.manifest_commercial_keys()
        self.keys = all_keys(self.universe, self.matrix)

    def test_dry_run_writes_nothing(self):
        store = FakeStore(self.ROLES, self.keys)
        code, out = run_quiet(store, self.matrix, self.universe, {}, apply=False)
        self.assertEqual(code, 0)
        self.assertEqual(store.inserted, [], "no --apply → no write")
        self.assertIn("DRY-RUN", out)

    def test_apply_then_rerun_adds_nothing(self):
        store = FakeStore(self.ROLES, self.keys)
        self.assertEqual(run_quiet(store, self.matrix, self.universe, {}, apply=True)[0], 0)
        first = len(store.inserted)
        self.assertEqual(first, 9 + 3 + 24 + len(self.universe))
        self.assertTrue(all(d["CreatedBy"] == "manual-grant-w6-cfg" for d in store.inserted))
        self.assertEqual(run_quiet(store, self.matrix, self.universe, {}, apply=True)[0], 0)
        self.assertEqual(len(store.inserted), first, "second run inserts nothing")

    def test_workflow_keys_are_reported_never_granted(self):
        store = FakeStore(self.ROLES, self.keys)
        code, out = run_quiet(store, self.matrix, self.universe, {}, apply=True)
        self.assertEqual(code, 0)
        wf = {store.perms[k]["_id"] for k in workflow_keys(self.matrix)}
        self.assertEqual([d for d in store.inserted if d["PermissionId"] in wf], [], "MOD-0023 keys are K13, not granted")
        self.assertIn("'CrmUser' lacks ['platform.workflow.instances.start', 'platform.workflow.instances.view']", out)
        self.assertEqual(out.count("INFO (not granted here"), 4)

    def test_a_key_already_held_is_not_inserted(self):
        store = FakeStore(self.ROLES, self.keys, held=[("Admin", "commercial.pre-orders.read")])
        run_quiet(store, self.matrix, self.universe, {}, apply=True)
        admin = store.role_list[0]["_id"]
        read = store.perms["commercial.pre-orders.read"]["_id"]
        self.assertEqual(len([d for d in store.inserted if d["RoleId"] == admin and d["PermissionId"] == read]), 0)

    def test_missing_local_role_is_reported_and_skipped_or_aborts_when_strict(self):
        store = FakeStore(["Admin", "Viewer"], self.keys)
        code, out = run_quiet(store, self.matrix, self.universe, {}, apply=True)
        self.assertEqual(code, 0)
        self.assertIn("'CrmUser' (matrix row CrmUser) is not in tenant 97c5", out)
        self.assertEqual(len(store.inserted), len(self.universe))
        store = FakeStore(["Admin"], self.keys)
        self.assertEqual(run_quiet(store, self.matrix, self.universe, {}, apply=True, strict_roles=True)[0], 2)
        self.assertEqual(store.inserted, [])

    def test_role_map_grants_a_row_to_an_existing_role_once(self):
        store = FakeStore(["Admin", "Viewer"], self.keys)
        run_quiet(store, self.matrix, self.universe, {"CrmUser": "Viewer", "Team Leader": "Viewer"}, apply=True)
        viewer = store.role_list[1]["_id"]
        rows = [d["PermissionId"] for d in store.inserted if d["RoleId"] == viewer]
        self.assertEqual(len(rows), len(set(rows)))
        self.assertEqual(len(rows), 9 + 1)  # CrmUser's nine + Team Leader's approve (read / cancel already planned)

    def test_a_key_missing_from_the_catalog_aborts_before_any_write(self):
        store = FakeStore(self.ROLES, [k for k in self.keys if k != "crm.account-contact.read"])
        code, out = run_quiet(store, self.matrix, self.universe, {}, apply=True)
        self.assertEqual(code, 3)
        self.assertIn("crm.account-contact.read", out)
        self.assertEqual(store.inserted, [])


def node(script: str, *args: str) -> dict:
    proc = subprocess.run([NODE, "-e", script, "--", *args], capture_output=True, text=True, timeout=60)
    if proc.returncode != 0:
        raise AssertionError(proc.stderr)
    return json.loads(proc.stdout.strip().splitlines()[-1])


# A fake browser window: fetch answers from a route table and records every call.
FAKE_WINDOW = r"""
const path = process.argv[process.argv.length - 1 - EXTRA];
const api = require(path);
const calls = [];
const quiet = { log() {}, table() {}, error(m) { calls.push({ error: m }); } };
const win = (routes) => ({
  console: quiet,
  API: { auth: 'https://auth' },
  DitenDataTable: { getAuthHeaders: () => ({ Authorization: 'Bearer x' }) },
  document: { querySelector: () => ({ value: 'af' }) },
  fetch: async (url, init = {}) => {
    const method = init.method || 'GET';
    calls.push({ method, url, body: init.body ? String(init.body) : null });
    const hit = routes(method, url, init);
    const body = JSON.stringify(hit === undefined ? { data: null } : hit);
    return { ok: true, status: 200, json: async () => JSON.parse(body), text: async () => body };
  },
});
const writes = () => calls.filter(c => c.method && c.method !== 'GET');
"""


@unittest.skipUnless(NODE, "node not on PATH")
class ConsoleScripts(unittest.TestCase):
    def test_live_grant_matrix_equals_the_json_matrix(self):
        out = node("console.log(JSON.stringify(require(process.argv[process.argv.length-1]).MATRIX))", LIVE_GRANTS)
        self.assertEqual(out, [{"role": r["role"], "keys": r["keys"]} for r in grant.load_matrix()["roles"]])
        needs = node("console.log(JSON.stringify(require(process.argv[process.argv.length-1]).WORKFLOW_NEEDS))", LIVE_GRANTS)
        self.assertEqual(needs, grant.load_matrix()["workflow_needs"])

    def test_live_grant_dry_run_writes_nothing_apply_writes_rerun_adds_nothing(self):
        universe = grant.manifest_commercial_keys()
        script = FAKE_WINDOW.replace("EXTRA", "1") + r"""
const universe = JSON.parse(process.argv[process.argv.length - 1]);
const wf = ['platform.workflow.instances.start','platform.workflow.instances.view','platform.workflow.tasks.approve','platform.workflow.tasks.reject'];
const catalog = universe.concat(['crm.account.read','crm.contact.read','crm.account-contact.read','crm.visit-plan.read','mdm.product-master.read'], wf)
  .map((k, i) => ({ id: 'p' + i, key: k }));
const roles = ['CrmUser','Team Leader','Department Manager','Admin'].map((n, i) => ({ id: 'r' + i, name: n }));
const held = {};
const routes = (method, url, init) => {
  if (method === 'POST') { const b = new URLSearchParams(init.body); (held[b.get('roleId')] = held[b.get('roleId')] || [])
    .push(catalog.find(c => c.id === b.get('permissionId'))); return { success: true }; }
  if (url.endsWith('/api/roles')) return { data: roles };
  if (url.endsWith('/api/permissions/tenant-assignable')) return { data: catalog };
  const m = url.match(/roles\/(r\d)\/permissions/); if (m) return { data: held[m[1]] || [] };
};
(async () => {
  const dry = await api.run(win(routes), false); const dryWrites = writes().length;
  await api.run(win(routes), true); const applied = writes().length;
  const again = await api.run(win(routes), true);
  console.log(JSON.stringify({ dryWrites, applied, total: writes().length,
    planned: dry.filter(r => r.durum === 'EKLENECEK').length,
    again: again.filter(r => r.durum === 'zaten var').length, unassigned: dry.filter(r => r.durum.startsWith('ATANMADI')).length,
    wfPosted: writes().filter(c => wf.includes(catalog.find(x => x.id === new URLSearchParams(c.body).get('permissionId')).key)).length,
    info: again.filter(r => r.durum.startsWith('BILGI')).length }));
})();
"""
        out = node(script, LIVE_GRANTS, json.dumps(universe))
        expected = 9 + 3 + 24 + len(universe)
        self.assertEqual(out["dryWrites"], 0, "APPLY=false → no POST")
        self.assertEqual(out["planned"], expected)
        self.assertEqual(out["applied"], expected)
        self.assertEqual(out["total"], expected, "second run posts nothing")
        self.assertEqual(out["again"], expected)
        self.assertEqual(out["unassigned"], 0)
        self.assertEqual(out["wfPosted"], 0, "MOD-0023 keys are reported, never granted")
        self.assertEqual(out["info"], 2 + 3 + 4 + 4)

    def test_preorder_template_has_no_candidate_of_its_own(self):
        out = node("""
const api = require(process.argv[process.argv.length-1]);
const d = t => JSON.parse(api.definitionJson(api.TEMPLATES.find(x => x.code === t), ['position:a', 'position:b']));
console.log(JSON.stringify({ pre: d('COM-PREORDER-APPROVAL'), pricing: d('COM-PRICING-PUBLISH'), positions: api.POSITIONS }));
""", TEMPLATES)
        pre = out["pre"]["stages"][0]["steps"]
        self.assertEqual(len(pre), 1)
        self.assertEqual(pre[0]["assignment"]["candidatePrincipalIds"], [], "runtime candidates = the line managers")
        self.assertNotIn("sla", pre[0])
        self.assertEqual(out["pre"]["requestedObjectType"], "commercial.pre-order")
        self.assertTrue(out["pre"]["options"]["rejectCommentRequired"])
        step = out["pricing"]["stages"][0]["steps"][0]
        self.assertEqual(step["assignment"]["candidatePrincipalIds"], ["position:a", "position:b"])
        self.assertEqual(step["sla"]["escalationPrincipalIds"], ["position:a", "position:b"])
        self.assertEqual(out["positions"]["local"], ["POS-SALES-OPS"])
        self.assertEqual(len(out["positions"]["live"]), 8)
        self.assertTrue(all(p.endswith("-SALES-OPS") for p in out["positions"]["live"]))

    def test_templates_dry_run_writes_nothing_and_published_is_left_alone(self):
        script = FAKE_WINDOW.replace("EXTRA", "0") + r"""
let defs = [];
const routes = (method, url, init) => {
  if (url.endsWith('/api/lookups/positions')) return { data: [{ id: 'pos1', code: 'POS-SALES-OPS', isArchived: false }] };
  if (url.endsWith('/api/definitions') && method === 'GET') return { data: defs };
  if (url.endsWith('/api/definitions') && method === 'POST') { const b = JSON.parse(init.body);
    defs.push({ id: 'id-' + b.templateCode, templateCode: b.templateCode, status: 'Draft' }); return { data: { id: 'id-' + b.templateCode } }; }
  const m = url.match(/definitions\/id-([A-Z-]+)\/publish/); if (m) { defs.find(d => d.templateCode === m[1]).status = 'Published';
    return { data: { versionNumber: 1, status: 'Published' } }; }
};
(async () => {
  const dry = await api.run(win(routes), false, 'local'); const dryWrites = writes().length;
  await api.run(win(routes), true, 'local'); const applied = writes().length;
  const again = await api.run(win(routes), true, 'local');
  const live = await api.run(win(routes), false, 'live');
  const pub = calls.filter(c => c.method === 'POST' && c.url.includes('COM-PREORDER-APPROVAL/publish'))[0];
  console.log(JSON.stringify({ dryWrites, applied, total: writes().length, dry: dry.map(r => r.islem),
    again: again.map(r => r.islem), preCandidates: JSON.parse(JSON.parse(pub.body).definitionJson).stages[0].steps[0].assignment.candidatePrincipalIds }));
})();
"""
        out = node(script, TEMPLATES)
        self.assertEqual(out["dryWrites"], 0, "APPLY=false → no POST")
        self.assertEqual(out["dry"], ["oluştur + yayımla", "oluştur + yayımla"])
        self.assertEqual(out["applied"], 4)  # 2 × (create + publish)
        self.assertEqual(out["total"], 4, "a published template is not touched again")
        self.assertEqual(out["again"], ["atla (zaten yayında)", "atla (zaten yayında)"])
        self.assertEqual(out["preCandidates"], [])

    def test_prefix_dry_run_writes_nothing_apply_merges_rerun_adds_nothing(self):
        script = FAKE_WINDOW.replace("EXTRA", "0") + r"""
const LE = '1754b820-1111-2222-3333-444444444444';
let settings = { isDefault: false, version: 3, companyPrefixes: [{ legalEntityId: 'aaaaaaaa-0000-0000-0000-000000000000', prefix: 'AZ' }] };
const routes = (method, url, init) => {
  if (url.endsWith('/api/settings') && method === 'GET') return { data: settings };
  if (url.endsWith('/api/settings') && method === 'PUT') { const b = JSON.parse(init.body);
    settings = Object.assign({}, settings, { companyPrefixes: b.companyPrefixes, version: settings.version + 1, sent: b }); return { data: settings }; }
  if (url.endsWith('/api/legal-entities')) return { data: { available: true, items: [
    { id: LE, name: 'GMG', countryCode: 'TR' }, { id: 'aaaaaaaa-0000-0000-0000-000000000000', name: 'AZ LLC', countryCode: 'AZ' }] } };
};
(async () => {
  const dry = await api.run(win(routes), false, 'local'); const dryWrites = writes().length;
  await api.run(win(routes), true, 'local'); const applied = writes().length; const sent = settings.sent;
  const again = await api.run(win(routes), true, 'local');
  const clash = api.plan({ companyPrefixes: [{ legalEntityId: LE, prefix: 'GM' }] }, [{ id: LE, name: 'GMG' }], [{ legalEntityId: '1754b820', prefix: 'MG' }]);
  const taken = api.plan({ companyPrefixes: [{ legalEntityId: 'x', prefix: 'MG' }] }, [{ id: LE, name: 'GMG' }], [{ legalEntityId: '1754b820', prefix: 'MG' }]);
  console.log(JSON.stringify({ dryWrites, applied, total: writes().length, dry: dry.rows.map(r => r.durum), sent,
    again: again.rows.map(r => r.durum), clash: [clash.merged, clash.rows[0].durum.slice(0, 3)], taken: [taken.merged, taken.rows[0].durum.slice(0, 3)],
    live: api.WANTED.live }));
})();
"""
        out = node(script, PREFIX)
        self.assertEqual(out["dryWrites"], 0, "APPLY=false → no PUT")
        self.assertEqual(out["dry"], ["EKLENECEK"])
        self.assertEqual(out["applied"], 1)
        self.assertEqual(out["sent"]["expectedVersion"], 3)
        self.assertEqual(out["sent"]["companyPrefixes"], [
            {"legalEntityId": "aaaaaaaa-0000-0000-0000-000000000000", "prefix": "AZ"},
            {"legalEntityId": "1754b820-1111-2222-3333-444444444444", "prefix": "MG"}], "the existing prefix is kept")
        self.assertNotIn("approvalMode", out["sent"], "other settings are not sent")
        self.assertEqual(out["total"], 1, "second run writes nothing")
        self.assertEqual(out["again"], ["zaten var"])
        self.assertEqual(out["clash"], [None, "DUR"])
        self.assertEqual(out["taken"], [None, "DUR"])
        self.assertEqual(out["live"], [], "the live prefix is the user's choice")


if __name__ == "__main__":
    unittest.main()
