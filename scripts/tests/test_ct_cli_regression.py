"""CT-FIX-03 R0 — the Control Tower tools' command-line regression set.

One fixture repository is built outside the checkout (a temporary directory): the tools are copied from this checkout,
the data is minimal and synthetic (one strict sop7 queue, one module, one pack, a generated Blueprint, an acceptance
record, an effort baseline), it has its own git history and an origin/main imitation. The production command lines run
there and every case asserts an exit code and, where it matters, a named message.

Every case is a recorded counter-example of an independent audit (v3, v4: V4A-F1…F8, V4B-B…) or of the fix trials; the
id in its name says which. A case is never removed or weakened to make the set pass. The Blueprint needs openpyxl
(scripts/requirements-tools.txt): without it this module fails with a named error — it never skips.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
import xml.sax.saxutils as xu
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
try:
    import openpyxl  # noqa: F401  (the Blueprint reader of verify_module_id; the fixture writes one)
except ImportError as e:  # a missing dependency is an error, never a skip
    raise RuntimeError("test_ct_cli_regression: openpyxl gerekli — python3 -m pip install -r scripts/requirements-tools.txt") from e

ENV = dict(os.environ, GIT_OPTIONAL_LOCKS="0", PYTHONDONTWRITEBYTECODE="1", GIT_CONFIG_NOSYSTEM="1",
           GIT_CONFIG_GLOBAL=os.devnull, GIT_AUTHOR_NAME="R0 Fixture", GIT_AUTHOR_EMAIL="fixture@example.invalid",
           GIT_COMMITTER_NAME="R0 Fixture", GIT_COMMITTER_EMAIL="fixture@example.invalid",
           GIT_AUTHOR_DATE="2026-10-17T12:00:00Z", GIT_COMMITTER_DATE="2026-10-17T12:00:00Z")
BRANCH = "r0-fixture"
PACK = "execution/domains/audit/module-packs/MOD-0288-audit-ledger.md"
AUTHORITY = "docs/records/decisions/authority.md"
REASSESS = "docs/records/decisions/reassessment.md"
ACCEPT = "docs/records/audits/accept.md"
LEDGER = "execution/delivery/effort/ct.md"
APPROVAL = "docs/records/decisions/effort.md"
STATES = "execution/delivery/ct-queue-states.tsv"
TRANS = "execution/delivery/ct-sop7-transitions.tsv"
REG = "execution/delivery/ct-queue-registry.tsv"
CONSUMERS = {"validate": ["validate", "--queue", "ct"], "require": ["require", "W1", "--queue", "ct"],
             "guard": ["guard", "--queue", "ct"], "summary": ["summary", "--queue", "ct"],
             "writers": ["writers", "--queue", "ct"]}


class Fixture:
    def __init__(self):
        self._tmp = tempfile.TemporaryDirectory(prefix="ct-r0-")
        self.root = Path(self._tmp.name) / "repo"
        self.root.mkdir()
        self.marks: dict[str, str] = {}

    # -- plumbing
    def git(self, *args: str) -> str:
        done = subprocess.run(["git", *args], cwd=self.root, env=ENV, capture_output=True, text=True)
        if done.returncode != 0:
            raise RuntimeError(f"git {' '.join(args)}: {done.stderr.strip()}")
        return done.stdout.strip()

    def put(self, rel: str, text: str) -> None:
        path = self.root / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def read(self, rel: str) -> str:
        return (self.root / rel).read_text(encoding="utf-8")

    def commit(self, message: str) -> str:
        # explicit paths only (never 'add -A' / 'add -u', even in a temporary repository): the files present, and the
        # tracked files deleted since the last commit
        present = {str(p.relative_to(self.root)) for p in self.root.rglob("*")
                   if p.is_file() and ".git" not in p.relative_to(self.root).parts
                   and "__pycache__" not in p.relative_to(self.root).parts}
        deleted = set(filter(None, self.git("ls-files", "--deleted", "-z").split("\0")))
        paths = sorted(present | deleted)
        if paths:
            self.git("add", "--", *paths)
        self.git("commit", "-q", "--allow-empty", "-m", message)
        return self.git("rev-parse", "HEAD")

    def reset(self, mark: str) -> None:
        self.git("reset", "-q", "--hard", self.marks[mark])
        self.git("clean", "-q", "-fdx")

    def run(self, script: str, *args: str, stdin: str | None = None) -> subprocess.CompletedProcess:
        return subprocess.run([sys.executable, "-B", str(self.root / script), *args], cwd=self.root, env=ENV,
                              capture_output=True, text=True, input=stdin, timeout=300)

    # -- the fixture
    def build(self) -> None:
        self.git("init", "-q", "-b", BRANCH)
        for d in ("scripts", ".antigravity/scripts"):
            shutil.copytree(ROOT / d, self.root / d, ignore=shutil.ignore_patterns("__pycache__", "tests"))
        vmi = load(self.root / ".antigravity/scripts/verify_module_id.py", "r0_vmi")
        cq = load(self.root / "scripts/ct_queue.py", "r0_cq")
        ar = load(self.root / "scripts/acceptance_record.py", "r0_ar")
        kf = load(self.root / "scripts/known_failures.py", "r0_kf")
        self.cq, self.ar, self.kf, self.vmi = cq, ar, kf, vmi
        self.states = "".join(l + "\n" for l in (ROOT / STATES).read_text(encoding="utf-8").splitlines()
                              if not l.startswith("sce-mvp6\t"))
        self.trans = (ROOT / TRANS).read_text(encoding="utf-8")
        self.put(STATES, self.states)
        self.put(TRANS, self.trans)
        self.reg_md = ("| " + " | ".join(vmi.REGISTRY_HEADER) + " |\n|" + "---|" * len(vmi.REGISTRY_HEADER) + "\n"
                       "| MOD-0288 | Audit Ledger | audit-ledger | module | active | - | - | audit | fixture |\n")
        self.put(vmi.REGISTRY, self.reg_md)
        self.put(PACK, "---\nid: MOD-0288\nstatus: approved\n---\nEvidence required: E2\n## Acceptance Criteria\n"
                       "- [ ] AC-1 (zorunlu) — Audit Ledger tested\n")
        self.put(AUTHORITY, "---\nstatus: approved\n---\nW1 approved audit change\n")
        self.put("source.txt", "Audit source contract\n")
        self.put("AGENTS.md", "R0 fixture only\n")
        wb = openpyxl.Workbook()
        ws = wb.active
        ws.title = "Blueprint_Data"
        ws.append(vmi.BLUEPRINT_COLUMNS)
        ws.append(["MOD-0288", "Audit Ledger", "audit", "audit", "AuditItem"])
        ws2 = wb.create_sheet("SoR_Map")
        ws2.append(vmi.SOR_COLUMNS)
        ws2.append(["AuditItem", "MOD-0288", "Audit Ledger"])
        bp = self.root / vmi.BLUEPRINT
        bp.parent.mkdir(parents=True, exist_ok=True)
        wb.save(bp)
        self.put(vmi.GATEWAY, json.dumps({"Routes": [{"UpstreamPathTemplate": "/api/audit-ledger/{id}",
                                                      "DownstreamHostAndPorts": [{"Host": "localhost", "Port": 5000}]}]}))
        self.qh = (ROOT / "execution/delivery/ct-queue-template.tsv").read_text(encoding="utf-8")
        self.qrow = "W1\tAudit Ledger\tMOD-0288\tREADY\tCT\t-\t-\t2026-10-17\t-\t-\n"
        self.put("q.tsv", self.qh + self.qrow)
        base = self.commit("fixture base")
        self.reg_head = "\t".join(cq.REGISTRY_COLUMNS) + "\n"
        self.reg_line = f"ct\tAudit CT\tq.tsv\t{base}\tconfirmed\taudit\tfixture\tyes\tsop7\n"
        self.put(REG, self.reg_head + self.reg_line)
        registered = self.commit("registered")
        self.git("update-ref", "refs/remotes/origin/main", registered)
        self.marks.update(base=base, registered=registered)
        # discovery report (committed) and the full §36.1 prompt's inputs
        p = self.run(".antigravity/scripts/verify_module_id.py", str(self.root), "--discover", "Audit Ledger")
        if p.returncode != 3:
            raise RuntimeError(f"fixture discovery: exit {p.returncode}\n{p.stdout}\n{p.stderr}")
        self.put("discovery.txt", p.stdout)
        self.discovery_commit = self.commit("discovery evidence")
        self.discovery_hash = p.stdout.strip().split("rapor-sha256: ")[-1]
        self.discovery_findings = sum(l.startswith("BULGU ") for l in p.stdout.splitlines())
        self.marks["discovery"] = self.discovery_commit
        # acceptance inputs
        self.put("wp.md", "---\nstatus: approved\nwork_package: W1\nmodule: MOD-0288\n---\nEvidence required E2\n")
        self.put("evidence.txt", "AC-1 checked: expected observed\n")
        self.evidence_commit = self.commit("acceptance input evidence")
        lh = "| " + " | ".join(ar.LIST_COLUMNS) + " |\n|" + "---|" * len(ar.LIST_COLUMNS) + "\n"
        self.list_row = (f"| AC-1 | Audit behavior | Safety | CLI test | Expected observed | hayır | evet | Expected observed | "
                         f"geçti | evidence.txt@{self.evidence_commit}:1 |\n")
        self.list_head = lh
        self.put("acceptance-list.md", lh + self.list_row)
        self.list_commit = self.commit("acceptance list")
        self.marks["acceptance-inputs"] = self.list_commit

    def acceptance_fields(self, **over) -> dict:
        e = self.evidence_commit
        f = {"format": "rep-001-v2", "module": "MOD-0288", "work_package": "W1", "queue": "ct", "evidence_level": "E2",
             "required_evidence_level": "E2", "required_evidence_source": f"wp.md@{e}:6", "decision": "accepted",
             "completion": "WP Done", "conditions": "-", "commit": e, "evidence": f"evidence.txt@{e}",
             "acceptance_list": f"acceptance-list.md@{self.list_commit}", "date": "2026-10-17", "supersedes": "-",
             "verifier": "Independent Reviewer", "implementer": "Fixture Author", "module_pack": f"{PACK}@{e}"}
        f.update(over)
        return f

    def write_acceptance(self, **over) -> str:
        f = self.acceptance_fields(**over)
        self.put(ACCEPT, "---\n" + "".join(f"{k}: {v}\n" for k, v in f.items()) + "---\n")
        return self.commit("acceptance variant")

    def prompt(self, head: str) -> str:
        r = self.root
        return (f"@[.antigravity/agents/orchestrator.md]\nWP: W1 · Prompt P-W1 v1.0\n"
                f"Repository: {r} · Branch: {BRANCH} · Expected HEAD: {head} · Worktree: {r}\n"
                "Risk: LOW · §9.1 sinyalleri: yok\n"
                f"Görev sınıfı: yönetişim · Kapsam kaydı: {AUTHORITY}\n"
                "İzinli yollar: source.txt\nKorunan yollar: scripts/**\nÖnce oku:\n1. AGENTS.md\n2. source.txt\n\n"
                "Sözleşme yüzeyleri:\n- API: N/A — Governance test has no API surface\n"
                "- İzin: N/A — Governance test has no permission change\n"
                "- Test: «Audit source contract» (source.txt:1)\n"
                "- Sabotaj: N/A — Test fixture validates supplied bad inputs\n\n"
                f"Keşif: inceleme gerekli · rapor discovery.txt@{self.discovery_commit} · sha256 {self.discovery_hash}\n"
                + "".join(f"Keşif kararı {i}: parent — Existing module retained for this audit task\n"
                          for i in range(1, self.discovery_findings + 1))
                + "\nNE: Exercise the audit fixture source contract.\nNEDEN: Validate production command behavior.\n"
                "NASIL: Read and report the source behavior.\nYAPMA: Do not modify any production repository.\n"
                "DOĞRULA: Run the independently prepared cases.\nDurma koşulları: Stop on invalid input or mismatched source.\n"
                "Rapor formatı: Findings and observed exit code.\n")


def load(path: Path, name: str):
    import importlib.util
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


FX: Fixture | None = None


def setUpModule():
    global FX
    FX = Fixture()
    FX.build()


def tearDownModule():
    if FX is not None:
        FX._tmp.cleanup()


class Base(unittest.TestCase):
    mark = "registered"

    def setUp(self):
        FX.reset(self.mark)

    def assertExit(self, p, want, contains: str | None = None):
        out = p.stdout + p.stderr
        if want == "nonzero":
            self.assertNotEqual(p.returncode, 0, out[-1500:])
        else:
            self.assertEqual(p.returncode, want, out[-1500:])
        if contains is not None:
            self.assertIn(contains, out, out[-1500:])


# ---------------------------------------------------------------------------------------------------- controls
class Controls(Base):
    def test_consumers_on_the_intact_fixture_exit_0(self):
        for name, args in CONSUMERS.items():
            with self.subTest(consumer=name):
                self.assertExit(FX.run("scripts/ct_queue.py", *args), 0)

    def test_full_prompt_cli_normal_exit_0(self):
        FX.reset("acceptance-inputs")
        path = FX.root.parent / "prompt.txt"
        path.write_text(FX.prompt(FX.git("rev-parse", "HEAD")), encoding="utf-8")
        self.assertExit(FX.run("scripts/prompt_gate.py", "check", str(path), "--queue", "ct"), 0)


# ---------------------------------------------------------------------------------------------------- V4A-F2 (06 K)
class V4A_F2_StrictProfile(Base):
    def state_names(self):
        return [l.split("\t")[1] for l in FX.states.splitlines() if l.startswith("sop7\t")]

    def test_missing_transition_table_stops_every_consumer(self):
        (FX.root / TRANS).unlink()
        for name, args in CONSUMERS.items():
            with self.subTest(consumer=name):
                self.assertExit(FX.run("scripts/ct_queue.py", *args), "nonzero")

    def test_missing_transition_table_stops_the_full_prompt_gate(self):
        FX.reset("acceptance-inputs")
        (FX.root / TRANS).unlink()
        head = FX.commit("transition table removed")
        path = FX.root.parent / "prompt.txt"
        path.write_text(FX.prompt(head), encoding="utf-8")
        self.assertExit(FX.run("scripts/prompt_gate.py", "check", str(path), "--queue", "ct"), "nonzero")

    def test_a_main_flow_step_removed_stops_every_consumer(self):
        # CT-FIX-03 R2: the state stays, only its SOP §7 step goes (READY → DISPATCHED; VERIFIED → ACCEPTED)
        for a, b in (("READY", "DISPATCHED"), ("VERIFIED", "ACCEPTED"), ("INSPECTED", "PLANNED")):
            FX.put(TRANS, "".join(l + "\n" for l in FX.trans.splitlines() if not l.startswith(f"sop7\t{a}\t{b}\t")))
            for name, args in CONSUMERS.items():
                with self.subTest(step=f"{a}->{b}", consumer=name):
                    self.assertExit(FX.run("scripts/ct_queue.py", *args), "nonzero")
        FX.put(TRANS, FX.trans)

    def test_a_state_the_sop_does_not_list_stops_every_consumer(self):
        extra = next(l for l in FX.states.splitlines() if l.startswith("sop7\tDEFERRED\t")).replace("DEFERRED", "PARKED", 1)
        FX.put(STATES, FX.states + extra + "\n")
        for name, args in CONSUMERS.items():
            with self.subTest(consumer=name):
                self.assertExit(FX.run("scripts/ct_queue.py", *args), "nonzero")
        FX.put(STATES, FX.states)

    def test_every_sop7_state_removed_with_its_edges_stops_every_consumer(self):
        # Codex's profile matrix: each of the states, with every transition that names it, removed in turn.
        for state in self.state_names():
            FX.put(STATES, "".join(l + "\n" for l in FX.states.splitlines() if not l.startswith(f"sop7\t{state}\t")))
            kept = []
            for i, l in enumerate(FX.trans.splitlines()):
                cells = l.split("\t")       # vocabulary, from_state, to_state, evidence, source, kind
                if i == 0 or cells[0] != "sop7" or state not in cells[1:3]:
                    kept.append(l + "\n")
            FX.put(TRANS, "".join(kept))
            for name, args in CONSUMERS.items():
                with self.subTest(state=state, consumer=name):
                    self.assertExit(FX.run("scripts/ct_queue.py", *args), "nonzero")
        FX.put(STATES, FX.states)
        FX.put(TRANS, FX.trans)


# ---------------------------------------------------------------------------------------------------- V4A-F3 (07, 11 K)
REASSESS_ROWS = ("W2\tAudit Ledger\tMOD-0288\tFAILED\tCT\t-\t-\t2026-10-17\t-\t-\n"
                 f"W2-R3\tneden: prompt rework\tMOD-0288\tREADY\tCT\tW2\t-\t2026-10-17\t-\t{REASSESS}\n")
KUNYE_OK = {
    "plain approved": "---\nstatus: approved\n---\nresolved\n",
    "approved (…)": "---\nstatus: approved (fixture scope)\n---\nresolved\n",
    "decided — …": "---\nstatus: decided — fixture note\n---\nresolved\n",
    "one balanced quote pair": '---\nstatus: "approved"\n---\nresolved\n',
    "approved — note (Codex matrix)": "---\nstatus: approved — owner decision\n---\nresolved\n",
    "nested metadata list beside the status": "---\nstatus: approved\nmetadata:\n  list:\n    - one\n    - two\n---\nresolved\n",
    # Codex (R1 review): a field of another key's explicit object, list item or block text is that key's data
    "a metadata object holding its own status": "---\nstatus: approved\nmetadata:\n  status: draft\n  id: x\n---\nresolved\n",
    "a column-0 list item holding its own status": "---\nstatus: approved\nreviews:\n- by: a\n  status: draft\n---\nresolved\n",
    "block text mentioning a status": "---\nstatus: approved\nnotes: |\n  status: draft\n---\nresolved\n",
    "an anchored object holding a status": "---\nstatus: approved\nbase: &b\n  status: draft\n---\nresolved\n",
    "bold prose status in the body (not read)": "---\nstatus: approved\n---\n**Status:** in force.\n",
    "status inside a fenced code block in the body (data)": "---\nstatus: approved\n---\n```\nstatus: draft\n```\n",
    "an indented comment line under status": "---\nstatus: approved\n  # not a continuation\nowner: x\n---\nresolved\n",
    "a longer closing fence closes the block": "---\nstatus: approved\n---\n```\nstatus: draft\n`````\nresolved\n",
}
KUNYE_BAD = {
    "indented second status": "---\nstatus: approved\n  status: draft\n---\nresolved\n",
    "one-space indented second status": "---\nstatus: approved\n status: draft\n---\nresolved\n",
    # a canonical status plus a second one disguised at column 0 (R1 sabotage S1 found these missing)
    "quoted second status": '---\nstatus: approved\n"status": draft\n---\nresolved\n',
    "single-quoted second status": "---\nstatus: approved\n'status': draft\n---\nresolved\n",
    "bold second status": "---\nstatus: approved\n**status**: draft\n---\nresolved\n",
    "capitalised second status": "---\nstatus: approved\nStatus: draft\n---\nresolved\n",
    "disguised first status, canonical second": '---\n"status": draft\nstatus: approved\n---\nresolved\n',
    "upper-case status key only": "---\nSTATUS: approved\n---\nresolved\n",
    "single-quoted status line outside the künye": "---\nstatus: approved\n---\n'status': draft\n",
    "mixed quote pair": "---\nstatus: \"approved'\n---\nresolved\n",
    "a status under another scalar key (owned by nobody)": "---\nstatus: approved\ntitle: audit\n  status: draft\n---\nresolved\n",
    "a tab-indented status under an object": "---\nstatus: approved\nmetadata:\n\tstatus: draft\n---\nresolved\n",
    "the only status inside an object": "---\nmetadata:\n  status: approved\n---\nresolved\n",
    "a status in a multi-line flow mapping (limited contract)": "---\nstatus: approved\nmeta: {\n  status: draft }\n---\nresolved\n",
    "plain status outside the künye": "---\nstatus: approved\n---\nstatus: draft\nresolved\n",
    "quoted status key": '---\n"status": approved\n---\nresolved\n',
    "bold status key": "---\n**status**: approved\n---\nresolved\n",
    "capitalised status key": "---\nStatus: approved\n---\nresolved\n",
    "block scalar": "---\nstatus: >\n  approved\n---\nresolved\n",
    "continuation line": "---\nstatus: approved\n  draft\n---\nresolved\n",
    "continuation after a blank line": "---\nstatus: approved\n\n  draft\n---\nresolved\n",
    "continuation after a comment line": "---\nstatus: approved\n# note\n  draft\n---\nresolved\n",
    "status hidden behind an unclosed fence": "---\nstatus: approved\n---\n```\nstatus: draft\n",
    "status hidden behind a fence closed by the other mark": "---\nstatus: approved\n---\n```\nstatus: draft\n~~~\n",
    "status hidden behind a fence closed by a shorter mark": "---\nstatus: approved\n---\n````\nstatus: draft\n```\n",
    "quoted status line outside the künye": "---\nstatus: approved\n---\n\"status\": draft\n",
    "status key twice": "---\nstatus: approved\nstatus: approved\n---\nresolved\n",
    "anchor on the value": "---\nstatus: &a approved\n---\nresolved\n",
    "alias": "---\nok: &a approved\nstatus: *a\n---\nresolved\n",
    "merge key": "---\nbase: &b {status: draft}\n<<: *b\nstatus: approved\n---\nresolved\n",
    "empty value": "---\nstatus:\n---\nresolved\n",
    "suffix": "---\nstatus: approved-but-draft\n---\nresolved\n",
    "unbalanced quote": '---\nstatus: "approved\n---\nresolved\n',
}


class V4A_F3_Kunye(Base):
    def require_r3(self, text):
        FX.put("q.tsv", FX.qh + FX.qrow + REASSESS_ROWS)
        FX.put(REASSESS, text)
        return FX.run("scripts/ct_queue.py", "require", "W2-R3", "--queue", "ct")

    def test_reassessment_valid_forms_dispatch(self):
        for label, text in KUNYE_OK.items():
            with self.subTest(form=label):
                self.assertExit(self.require_r3(text), 0, "DAĞITILABİLİR W2-R3")

    def test_reassessment_unrecognised_or_conflicting_forms_stop(self):
        for label, text in KUNYE_BAD.items():
            with self.subTest(form=label):
                self.assertExit(self.require_r3(text), 1, "DUR W2-R3")

    def gate(self, authority_text):
        FX.reset("acceptance-inputs")
        FX.put(AUTHORITY, authority_text + "W1 approved audit change\n")   # the record names its work package
        head = FX.commit("authority variant")
        path = FX.root.parent / "prompt.txt"
        path.write_text(FX.prompt(head), encoding="utf-8")
        return FX.run("scripts/prompt_gate.py", "check", str(path), "--queue", "ct")

    def test_full_prompt_gate_valid_forms_pass(self):
        for label, text in KUNYE_OK.items():
            with self.subTest(form=label):
                self.assertExit(self.gate(text), 0)

    def test_full_prompt_gate_unrecognised_or_conflicting_forms_stop(self):
        for label, text in KUNYE_BAD.items():
            with self.subTest(form=label):
                self.assertExit(self.gate(text), 1, "ENGEL")


# ---------------------------------------------------------------------------------------------------- V4A-F4 (09 K)
class V4A_F4_Discovery(Base):
    def test_task_row_with_empty_item_and_module_is_incomplete_not_no_match(self):
        FX.put("q.tsv", FX.qh + "W1\t\t\tREADY\tCT\t-\t-\t2026-10-17\t-\t-\n")
        p = FX.run(".antigravity/scripts/verify_module_id.py", str(FX.root), "--discover", "Unmatched Zebralocus")
        self.assertExit(p, 2)
        self.assertNotIn("eşleşme bulunamadı", p.stdout)


    # CT-FIX-03 R4 (Codex item 8): discovery reads every visible pack blob's künye id only to refuse a repeated or
    # disguised id — the last value is never taken; a pack without a künye is still matched by its file name
    def test_a_pack_with_a_repeated_or_disguised_id_is_incomplete(self):
        for label, fm in (("two ids", "id: MOD-0288\nid: MOD-0289\n"), ("an indented second id", "id: MOD-0288\n  id: MOD-0289\n"),
                          ("a quoted second id", 'id: MOD-0288\n"id": MOD-0289\n')):
            with self.subTest(form=label):
                FX.reset(self.mark)
                FX.put("execution/domains/audit/module-packs/MOD-0289-twin.md", "---\n" + fm + "status: approved\n---\n# x\n")
                FX.commit("a pack whose künye names its id twice")
                p = FX.run(".antigravity/scripts/verify_module_id.py", str(FX.root), "--discover", "Audit Ledger")
                self.assertExit(p, 2, "tekrarlı ya da gizlenmiş kimlik")

    def test_a_pack_without_a_kunye_is_still_matched_by_name(self):
        FX.put("execution/domains/audit/module-packs/MOD-0289-audit-ledger-notes.md", "# notes without a künye\n")
        FX.commit("a pack without a künye")
        p = FX.run(".antigravity/scripts/verify_module_id.py", str(FX.root), "--discover", "Audit Ledger")
        self.assertExit(p, 3, "MOD-0289-audit-ledger-notes.md")


# ---------------------------------------------------------------------------------------------------- V4A-F8, V4B-B6 (10)
class V4A_F8_V4B_B6_Report(Base):
    def discover(self):
        return FX.run(".antigravity/scripts/verify_module_id.py", str(FX.root), "--discover", "Audit Ledger")

    def test_report_names_the_task_registry_bytes(self):
        a = self.discover().stdout
        FX.put(REG, FX.reg_head + FX.reg_line.replace("\tfixture\tyes", "\tfixture changed\tyes"))
        b = self.discover().stdout
        self.assertNotEqual(a.split("rapor-sha256:")[-1], b.split("rapor-sha256:")[-1])

    def check_report(self, text):
        path = FX.root.parent / "report.txt"
        path.write_bytes(text.encode("utf-8"))
        return FX.run(".antigravity/scripts/verify_module_id.py", "--check-report", str(path))

    def test_check_report_reads_the_bytes_as_they_are(self):
        lf = self.discover().stdout
        self.assertExit(self.check_report(lf), 0)
        for label, text in {"CRLF": lf.replace("\n", "\r\n"), "extra final newline": lf + "\n",
                            "trailing space on the hash line": lf.rstrip("\n") + " \n"}.items():
            with self.subTest(form=label):
                self.assertExit(self.check_report(text), 2)


# ---------------------------------------------------------------------------------------------------- V4A-F5 (13 K, 14 K)
class V4A_F5_Acceptance(Base):
    mark = "acceptance-inputs"

    def check(self, head):
        return FX.run("scripts/acceptance_record.py", "check", f"{ACCEPT}@{head}", "--queue", "ct", "--wp", "W1",
                      "--module", "MOD-0288")

    def test_normal_acceptance_passes(self):
        self.assertExit(self.check(FX.write_acceptance()), 0)

    def test_nonexistent_pack_is_refused(self):
        h = FX.write_acceptance(module_pack="execution/domains/audit/module-packs/MOD-0999-no-such-pack.md@" + "f" * 40)
        self.assertExit(self.check(h), 1)

    def test_ac_not_in_the_pack_is_refused(self):
        FX.put("acceptance-list.md", FX.list_head + FX.list_row.replace("| AC-1 |", "| AC-999 |"))
        FX.list_commit = FX.commit("list with an AC the pack does not have")
        try:
            self.assertExit(self.check(FX.write_acceptance()), 1)
        finally:
            FX.list_commit = FX.marks["acceptance-inputs"]

    def test_unbalanced_quote_in_a_field_is_refused(self):
        self.assertExit(self.check(FX.write_acceptance(decision='"accepted')), 1)

    # V4A-F5's report half: the auditor's recipe (accepted queue + approved effort baseline) — a record the check refuses
    # is not counted; the report says the percentages are not valid instead of %100
    def report_with(self, **over):
        FX.reset(self.mark)
        ledger, approval = "execution/delivery/effort/ct.md", "docs/records/decisions/effort.md"
        lt = ("---\nformat: effort-ledger-v1\nqueue: ct\nbaseline_version: 1\nunit: saat\n"
              "weights: backend=0.5 frontend=0.3 test=0.2\napproval_record: " + approval + "@" + "a" * 40 + "\n---\n\n"
              "| ID | Backend | Frontend | Test |\n|---|---|---|---|\n| W1 | 8 | 4 | 2 |\n")
        digest = hashlib.sha256("".join(l for l in lt.splitlines(keepends=True)
                                        if not l.startswith("approval_record:")).encode()).hexdigest()
        FX.put(approval, f"---\nstatus: approved\nledger: {ledger}\nbaseline_version: 1\nledger_content_sha256: {digest}\n---\n")
        aph = FX.commit("effort approval")
        FX.put(ledger, lt.replace("a" * 40, aph))
        FX.commit("effort ledger")
        ah = FX.write_acceptance(**over)
        FX.put("q.tsv", FX.qh + FX.qrow.replace("\tREADY\t", "\tACCEPTED\t").replace("\t-\t-\n", f"\t{ACCEPT}@{ah}\t-\n"))
        FX.commit("accepted queue")
        p = FX.run("scripts/status_report.py", "--mode", "v2", "--queue", "ct")
        self.assertExit(p, 0)
        return p.stdout

    def test_report_counts_only_an_acceptance_the_check_allows(self):
        self.assertIn('"percentages_valid": true', self.report_with())                      # control: %100
        for label, over in (("a nonexistent pack", {"module_pack": "execution/domains/audit/module-packs/MOD-0999-x.md@" + "f" * 40}),
                            ("a nonexistent code commit", {"commit": "f" * 40})):
            with self.subTest(form=label):
                out = self.report_with(**over)
                self.assertIn('"percentages_valid": false', out)
                self.assertNotIn("%100.0", out)

    # Codex's identity / status matrix (CT-FIX-03 R1): a decision field of the level source or of the module pack
    # written a second time, indented or quoted -> the acceptance is refused, never read from the first line
    def test_disguised_second_field_in_level_source_or_pack_is_refused(self):
        base_wp, base_pack = (FX.root / "wp.md").read_text(encoding="utf-8"), (FX.root / PACK).read_text(encoding="utf-8")
        for target, key, old, new in (("wp.md", "status", "approved", "draft"), ("wp.md", "work_package", "W1", "W2"),
                                      ("wp.md", "module", "MOD-0288", "MOD-0289"), ("pack", "id", "MOD-0288", "MOD-0289"),
                                      ("pack", "status", "approved", "draft")):
            for form, extra in (("indented", f"  {key}: {new}"), ("quoted", f'"{key}": {new}')):
                with self.subTest(target=target, key=key, form=form):
                    FX.reset(self.mark)
                    change = f"{key}: {old}\n{extra}"
                    wp = base_wp.replace(f"{key}: {old}", change, 1) if target == "wp.md" else base_wp
                    pk = base_pack.replace(f"{key}: {old}", change, 1) if target == "pack" else base_pack
                    FX.put("wp.md", wp)
                    FX.put(PACK, pk)
                    h = FX.commit(f"{target} {key} {form} duplicate")
                    line = next(i for i, l in enumerate(wp.splitlines(), 1) if "Evidence required" in l)
                    head = FX.write_acceptance(required_evidence_source=f"wp.md@{h}:{line}", module_pack=f"{PACK}@{h}")
                    self.assertExit(self.check(head), 1)


# ---------------------------------------------------------------------------------------------------- V4A-F6 (16 K)
MSG = "Known violations\n- A\n- B"


def trx(results, summary="Failed", counters=None):
    """counters=None: CT-FIX-03 R5 makes Counters mandatory, so the default writes the counters the rows imply (VSTest's
    form: notExecuted="0", executed = total - NotExecuted rows); counters=False writes none; a dict is written as given."""
    body = "".join(f'<UnitTestResult testName="{n}" outcome="{o}"><Output><ErrorInfo><Message>{xu.escape(m)}</Message>'
                   f'</ErrorInfo></Output></UnitTestResult>' for n, o, m in results)
    if counters is None:
        outs = [o for _, o, _ in results]
        counters = {"total": len(outs), "executed": len(outs) - outs.count("NotExecuted"), "passed": outs.count("Passed"),
                    "failed": outs.count("Failed"), "notExecuted": 0}
    c = "" if counters is False else "<Counters " + " ".join(f'{k}="{v}"' for k, v in counters.items()) + "/>"
    return f'<TestRun><Results>{body}</Results><ResultSummary outcome="{summary}">{c}</ResultSummary></TestRun>'


class V4A_F6_KnownFailures(Base):
    def setUp(self):
        super().setUp()
        b = FX.marks["base"]
        fp = "items:2:" + hashlib.sha256(b"A\nB").hexdigest()[:16]
        row = ["Audit", "Test.Known", b, "fixture", b, "source.txt", "fixture", "open", fp, "Fixture owner", "2026-12-01"]
        FX.put("tests/known-failures.tsv", "\t".join(FX.kf.COLUMNS) + "\n" + "\t".join(row) + "\n")
        FX.put("tests/known-failures-items.json", json.dumps({"tests": {"Test.Known": ["A", "B"]}}))
        FX.commit("known failure baseline")

    def check(self, text):
        path = FX.root.parent / "run.trx"
        path.write_text(text, encoding="utf-8")
        return FX.run("scripts/known_failures.py", "check", str(path), "--suite", "Audit")

    def test_registered_failure_known(self):
        self.assertExit(self.check(trx([("Test.Known", "Failed", MSG)])), 0, "BİLİNEN=1")

    def test_counters_say_failed_but_every_row_passed_is_untrusted(self):
        t = trx([("Test.Known", "Passed", "")], "Failed", {"total": 2, "executed": 2, "passed": 1, "failed": 1})
        self.assertExit(self.check(t), 2)

    def test_counters_count_result_rows_before_names_are_merged(self):
        # a theory test: two result rows with one name (144 rows, 143 names in the real suite) — counters agree
        t = trx([("Test.Known", "Failed", MSG), ("Test.Known", "Failed", MSG)], "Failed",
                {"total": 2, "executed": 2, "passed": 0, "failed": 2})
        self.assertExit(self.check(t), 0, "BİLİNEN=1")

    def test_counters_with_fewer_rows_than_total_is_untrusted(self):
        t = trx([("Test.Known", "Failed", MSG)], "Failed", {"total": 2, "executed": 2, "passed": 1, "failed": 1})
        self.assertExit(self.check(t), 2)

    # CT-FIX-03 R5: Codex's TRX matrix through the real CLI, and VSTest's skipped-test form as measured on real CRM runs
    def test_vstest_skipped_test_form_is_trusted(self):
        rows = [("Test.Known", "Failed", MSG), ("Test.Skip", "NotExecuted", "")]
        for nx in (0, 1):
            with self.subTest(notExecuted=nx):
                self.assertExit(self.check(trx(rows, "Failed", {"total": 2, "executed": 1, "passed": 0, "failed": 1,
                                                                 "notExecuted": nx})), 0, "BİLİNEN=1")

    def test_untrustworthy_summary_or_counters_stop(self):
        known = [("Test.Known", "Failed", MSG)]
        c1 = {"total": 1, "executed": 1, "passed": 0, "failed": 1}
        for label, text in {
                "no Counters": trx(known, "Failed", False),
                "two Counters": trx(known, "Failed", c1).replace("</ResultSummary>", '<Counters total="1" executed="1" passed="0" failed="1"/></ResultSummary>'),
                "two ResultSummary": trx(known, "Failed", c1).replace("</TestRun>", '<ResultSummary outcome="Failed"/></TestRun>'),
                "summary Completed with a failed row": trx(known, "Completed", c1),
                "summary NotExecuted while it ran": trx(known, "NotExecuted", c1),
                "a negative counter": trx(known, "Failed", dict(c1, passed=-1)),
                "a text counter": trx(known, "Failed", dict(c1, total="many")),
                "executed not the rows that ran": trx(known, "Failed", dict(c1, executed=0)),
                "an arbitrary notExecuted": trx(known, "Failed", dict(c1, notExecuted=999)),
                "an unknown outcome with the registered message": trx([("Test.Known", "Bogus", MSG)], "Failed",
                                                                      {"total": 1, "executed": 1, "passed": 0, "failed": 0})}.items():
            with self.subTest(form=label):
                self.assertExit(self.check(text), 2)


# ---------------------------------------------------------------------------------------------------- U2 / U3 (CT-FIX-03)
class U2_PromptDraft(Base):
    """CT-FIX-03 U2: the draft fills only the measured Repository, Branch, Expected HEAD, Worktree and WP id; the gate
    refuses the draft as printed; exit 0 of draft is never a dispatch permission."""
    mark = "acceptance-inputs"

    def draft(self, wp="W1"):
        return FX.run("scripts/prompt_gate.py", "draft", "--queue", "ct", "--wp", wp, "--worktree", str(FX.root))

    def test_the_five_measured_values_are_the_gate_s_own_and_the_draft_is_refused(self):
        head = FX.git("rev-parse", "HEAD")
        p = self.draft()
        self.assertExit(p, 0)
        self.assertIn("dağıtım izni değil", p.stderr)
        lines = p.stdout.splitlines()
        good = FX.prompt(head).splitlines()
        repo_line = next(l for l in lines if l.startswith("Repository:"))
        real = os.path.realpath(FX.root)                     # measured: the resolved path of the fixture repository
        self.assertEqual(repo_line, f"Repository: {real} · Branch: {BRANCH} · Expected HEAD: {head} · Worktree: {real}")
        self.assertTrue(any(l.startswith("WP: W1 · Prompt <<DOLDUR") for l in lines))
        path = FX.root.parent / "draft.txt"
        path.write_text(p.stdout, encoding="utf-8")
        self.assertExit(FX.run("scripts/prompt_gate.py", "check", str(path), "--queue", "ct"), 1, "doldurulmamış")
        # the measured line taken over unchanged into the filled prompt: the gate passes it
        path.write_text("\n".join(repo_line if l.startswith("Repository:") else l for l in good) + "\n", encoding="utf-8")
        self.assertExit(FX.run("scripts/prompt_gate.py", "check", str(path), "--queue", "ct"), 0)

    def test_a_wp_not_in_the_queue_is_not_measurable(self):
        p = self.draft("W9")
        self.assertExit(p, 2, "taslak üretilemedi")
        self.assertEqual(p.stdout, "")

    def test_a_malformed_queue_row_is_never_skipped(self):
        # Codex: a short second W1 row, or a short W2 row, made the draft count one valid W1 and exit 0
        for label, extra in (("a short second W1 row", "W1\tshort\n"), ("a short W2 row", "W2\tshort\n")):
            with self.subTest(form=label):
                FX.put("q.tsv", FX.qh + FX.qrow + extra)
                p = self.draft()
                self.assertExit(p, 2, "yapısı bozuk")
                self.assertEqual(p.stdout, "")


class U3_AcceptanceDraft(Base):
    """CT-FIX-03 U3: record and list drafts as separate outputs; the list from an approved pack version, exactly."""
    mark = "acceptance-inputs"

    def draft(self, part, *extra):
        return FX.run("scripts/acceptance_record.py", "draft", "--part", part, "--queue", "ct", "--wp", "W1", *extra)

    def check(self, head):
        return FX.run("scripts/acceptance_record.py", "check", f"{ACCEPT}@{head}", "--queue", "ct", "--wp", "W1",
                      "--module", "MOD-0288")

    def test_record_draft_fills_identity_only_and_check_refuses_it(self):
        pack = f"{PACK}@{FX.evidence_commit}"
        p = self.draft("record", "--pack", pack)
        self.assertExit(p, 0, "kabul değildir")
        for k, v in (("format", "rep-001-v2"), ("queue", "ct"), ("work_package", "W1"), ("module", "MOD-0288"), ("module_pack", pack)):
            self.assertIn(f"\n{k}: {v}\n", p.stdout)
        for k in ("decision", "commit", "date", "verifier", "evidence_level"):
            self.assertIn(f"\n{k}: <<DOLDUR", p.stdout)
        FX.put(ACCEPT, p.stdout)
        self.assertExit(self.check(FX.commit("record draft as printed")), 1, "doldurulmamış")

    def test_list_draft_copies_the_pack_and_a_filled_one_passes(self):
        p = self.draft("list", "--pack", f"{PACK}@{FX.evidence_commit}")
        self.assertExit(p, 0)
        rows = [l for l in p.stdout.splitlines() if l.startswith("| AC-")]
        self.assertEqual(len(rows), 1)
        cells = [c.strip() for c in rows[0].strip().strip("|").split("|")]
        self.assertEqual(len(cells), 10)
        self.assertEqual((cells[0], cells[1], cells[6], cells[8]), ("AC-1", "Audit Ledger tested", "evet", "denenmedi"))
        head_lines = p.stdout.splitlines()[:2]
        cells[2:6] = ["Safety", "CLI test", "Expected observed", "hayır"]
        cells[7:10] = ["Expected observed", "geçti", f"evidence.txt@{FX.evidence_commit}:1"]
        FX.put("acceptance-list.md", "\n".join(head_lines + ["| " + " | ".join(cells) + " |"]) + "\n")
        FX.list_commit = FX.commit("list from the draft, filled")
        try:
            self.assertExit(self.check(FX.write_acceptance()), 0)
        finally:
            FX.list_commit = FX.marks["acceptance-inputs"]

    def test_a_malformed_queue_row_is_never_skipped(self):
        for label, extra in (("a short second W1 row", "W1\tshort\n"), ("a short W2 row", "W2\tshort\n")):
            for part, more in (("record", ()), ("list", ("--pack", f"{PACK}@{FX.evidence_commit}"))):
                with self.subTest(form=label, part=part):
                    FX.put("q.tsv", FX.qh + FX.qrow + extra)
                    p = self.draft(part, *more)
                    self.assertExit(p, 2, "yapısı bozuk")
                    self.assertEqual(p.stdout, "")

    def test_list_needs_an_approved_pack_version(self):
        self.assertExit(self.draft("list"), 2, "--pack gerekli")
        FX.put(PACK, (FX.root / PACK).read_text(encoding="utf-8").replace("status: approved", "status: draft"))
        h = FX.commit("pack back to draft")
        self.assertExit(self.draft("list", "--pack", f"{PACK}@{h}"), 2, "tek status")


# ---------------------------------------------------------------------------------------------------- V4B-B8 (19)
DCP = "execution/portfolio/delivery-capability-packs/DCP-091-routing-test.md"


class V4B_B8_Route(Base):
    """CT-FIX-03 B8 (Codex's route matrix): a registry row the owner has not confirmed is a stamped preparation
    candidate; --in-force refuses it although origin/main holds the confirmed row."""
    def route(self, confirmation, in_force):
        FX.put(DCP, "---\nid: DCP-091\nstatus: approved\nowner: audit-coordinator\n---\n\n## 8.1 Delivery slices\n\n"
                    "| Slice | Kapsam | Not |\n|---|---|---|\n| MVP1 | MOD-0288 | synthetic |\n")
        line = lambda c: FX.reg_head + FX.reg_line.replace("\tconfirmed\taudit\t", f"\t{c}\tdcp:{DCP}#MVP1\t")
        FX.put(REG, line("confirmed"))
        FX.git("update-ref", "refs/remotes/origin/main", FX.commit("routing: the confirmed row on main"))
        if confirmation != "confirmed":
            FX.put(REG, line(confirmation))
            FX.commit("routing: the branch row not confirmed")
        try:
            return FX.run("scripts/ct_route.py", "--kind", "delivery", "--module", "MOD-0288", *(["--in-force"] if in_force else []))
        finally:
            FX.git("update-ref", "refs/remotes/origin/main", FX.marks["registered"])

    def test_a_confirmed_row_is_the_single_target_without_a_stamp(self):
        for in_force in (False, True):
            with self.subTest(in_force=in_force):
                p = self.route("confirmed", in_force)
                self.assertExit(p, 0, "sonuç: tek hedef")
                self.assertNotIn("sahip teyidi bekliyor", p.stdout)

    def test_a_pending_row_is_stamped_and_refused_in_force(self):
        p = self.route("pending", False)
        self.assertExit(p, 0, "Hazırlık adayı; sahip teyidi bekliyor; yürürlükte değil")
        self.assertTrue(any(l.startswith("sonuç: tek hedef") and "sahip teyidi bekliyor" in l for l in p.stdout.splitlines()))
        self.assertNotIn("yürürlük: yürürlükte", p.stdout)        # one truth (Codex): main confirmed, the branch row pending
        p = self.route("pending", True)
        self.assertExit(p, 1, "sahip teyidi bekliyor")
        self.assertNotIn("sonuç: tek hedef —", p.stdout)
        self.assertNotIn("yürürlük: yürürlükte", p.stdout)


# ---------------------------------------------------------------------------------------------------- V4A-F7 (18)
class V4A_F7_Hook(Base):
    def hook_with(self, verifier_source: str | None):
        page = FX.root / "frontend/Diten.Web/Views/Audit/Things/Index.cshtml"
        page.parent.mkdir(parents=True, exist_ok=True)
        page.write_text("")
        (page.parent / "_DataTable.cshtml").write_text("")
        if verifier_source is not None:
            (FX.root / ".antigravity/scripts/verify_datatable_page.py").write_text(verifier_source)
        return FX.run(".antigravity/scripts/list_screen_touch_hook.py",
                      stdin=json.dumps({"tool_input": {"file_path": str(page)}}))

    def test_an_empty_verifier_is_unverified_not_clean(self):
        p = self.hook_with("")
        self.assertExit(p, 0)
        self.assertNotIn("sapma yok", p.stdout)
        self.assertIn("DOĞRULANAMADI", p.stdout)

    def test_a_verifier_success_line_is_clean_only_when_asked_and_given(self):
        # CT-FIX-03 R6: the hook passes --ok-line; a verifier answering it is "sapma yok", one staying silent is not
        answer = ("import sys\nif '--ok-line' in sys.argv and '--format' in sys.argv:\n"
                  "    print('GAPS-OK: 12 kontrol, 0 sapma')\nsys.exit(0)\n")
        p = self.hook_with(answer)
        self.assertExit(p, 0, "sapma yok")
        p = self.hook_with("import sys\nprint('[pack] execution/domains/x/module-packs/X.md declares data_mode: server')\nsys.exit(0)\n")
        self.assertExit(p, 0, "DOĞRULANAMADI")
        self.assertNotIn("sapma yok", p.stdout)


if __name__ == "__main__":
    unittest.main()
