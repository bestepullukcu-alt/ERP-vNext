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
LIST = "docs/records/audits/acceptance-list.md"        # CT-FIX-05 (F3): the acceptance documents under the evidence root
RUN_DIR = "docs/records/audits/run-1"                  # CT-FIX-05 (F3): the run directory the real runner writes
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
        self.put(AUTHORITY, "---\nstatus: approved\nwork_packages: W1\n---\nW1 approved audit change\n")   # CT-FIX-04 İ2
        self.put("source.txt", "Audit source contract\n")
        self.put("AGENTS.md", "R0 fixture only\n")
        self.put(".gitignore", "__pycache__/\n")       # a byte-compiled copy is never a change of the run's code state
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
        self.code_commit = self.commit("acceptance inputs: the code state the run records")
        # CT-FIX-05 (F3): the evidence is the real runner's record of a run on that code state, in a fresh directory
        p = self.run("scripts/run_record.py", "run", "--out", RUN_DIR, "--", sys.executable, "-c",
                     "print('AC-1 checked: expected observed')")
        if p.returncode != 0:
            raise RuntimeError(f"fixture run record: exit {p.returncode}\n{p.stdout}\n{p.stderr}")
        self.evidence_commit = self.commit("acceptance input evidence: the run record")
        self.artifact = f"{RUN_DIR}/stdout.txt"
        lh = "| " + " | ".join(ar.LIST_COLUMNS) + " |\n|" + "---|" * len(ar.LIST_COLUMNS) + "\n"
        self.list_row = (f"| AC-1 | Audit Ledger tested | Safety | CLI test | Expected observed | hayır | evet | Expected observed | "
                         f"geçti | {self.artifact}@{self.evidence_commit}:1 |\n")
        self.list_head = lh
        self.put(LIST, lh + self.list_row)
        self.list_commit = self.commit("acceptance list")
        self.marks["acceptance-inputs"] = self.list_commit

    def acceptance_fields(self, **over) -> dict:
        e, c = self.evidence_commit, self.code_commit
        f = {"format": "rep-001-v2", "module": "MOD-0288", "work_package": "W1", "queue": "ct", "evidence_level": "E2",
             "required_evidence_level": "E2", "required_evidence_source": f"wp.md@{e}:6", "decision": "accepted",
             "completion": "WP Done", "conditions": "-", "commit": c, "evidence": f"{RUN_DIR}/run-record-v1.json@{e}",
             "acceptance_list": f"{LIST}@{self.list_commit}", "date": "2026-10-17", "supersedes": "-",
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
        # CT-FIX-05 (F2 (b), declared): a reassessment record lists the retry in its künye's work_packages; the status
        # forms under test are unchanged
        FX.put(REASSESS, "---\nwork_packages: W2-R3\n" + text[4:] if text.startswith("---\n") else text)
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
        # the record names its work package; CT-FIX-04 (İ2): explicitly, in its künye's work_packages
        bound = "---\nwork_packages: W1\n" + authority_text[4:] if authority_text.startswith("---\n") else authority_text
        FX.put(AUTHORITY, bound + "W1 approved audit change\n")
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
        FX.put(LIST, FX.list_head + FX.list_row.replace("| AC-1 |", "| AC-999 |"))
        FX.list_commit = FX.commit("list with an AC the pack does not have")
        try:
            self.assertExit(self.check(FX.write_acceptance()), 1)
        finally:
            FX.list_commit = FX.marks["acceptance-inputs"]

    def test_unbalanced_quote_in_a_field_is_refused(self):
        self.assertExit(self.check(FX.write_acceptance(decision='"accepted')), 1)

    # V4A-F5's report half: the auditor's recipe (accepted queue + approved effort baseline) — a record the check refuses
    # is not counted; the report says the percentages are not valid instead of %100
    def report_with(self, want=0, **over):
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
        self.assertExit(p, want)       # CT-FIX-04 (İ5, A3): a report with a refused acceptance is incomplete -> exit 1
        return p.stdout

    def test_report_counts_only_an_acceptance_the_check_allows(self):
        self.assertIn('"percentages_valid": true', self.report_with())                      # control: %100
        for label, over in (("a nonexistent pack", {"module_pack": "execution/domains/audit/module-packs/MOD-0999-x.md@" + "f" * 40}),
                            ("a nonexistent code commit", {"commit": "f" * 40})):
            with self.subTest(form=label):
                out = self.report_with(1, **over)
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
        cells[7:10] = ["Expected observed", "geçti", f"{FX.artifact}@{FX.evidence_commit}:1"]
        FX.put(LIST, "\n".join(head_lines + ["| " + " | ".join(cells) + " |"]) + "\n")
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
    def route(self, confirmation, in_force, main_confirmation="confirmed"):
        FX.put(DCP, "---\nid: DCP-091\nstatus: approved\nowner: audit-coordinator\n---\n\n## 8.1 Delivery slices\n\n"
                    "| Slice | Kapsam | Not |\n|---|---|---|\n| MVP1 | MOD-0288 | synthetic |\n")
        line = lambda c: FX.reg_head + FX.reg_line.replace("\tconfirmed\taudit\t", f"\t{c}\tdcp:{DCP}#MVP1\t")
        FX.put(REG, line(main_confirmation))
        FX.git("update-ref", "refs/remotes/origin/main", FX.commit("routing: main's row"))
        if confirmation != main_confirmation:
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
        # CT-FIX-04 (İ1): the stamp is the preparation answer when main's row is pending too
        p = self.route("pending", False, "pending")
        self.assertExit(p, 0, "Hazırlık adayı; sahip teyidi bekliyor; yürürlükte değil")
        self.assertTrue(any(l.startswith("sonuç: tek hedef") and "sahip teyidi bekliyor" in l for l in p.stdout.splitlines()))
        self.assertNotIn("yürürlük: yürürlükte", p.stdout)
        p = self.route("pending", True, "pending")
        self.assertExit(p, 1, "sahip teyidi bekliyor")
        self.assertNotIn("sonuç: tek hedef —", p.stdout)
        self.assertNotIn("yürürlük: yürürlükte", p.stdout)

    def test_a_pending_row_against_main_s_confirmed_row_is_a_conflict(self):
        # CT-FIX-03 B8's case (main confirmed, the branch row pending) — CT-FIX-04 (İ1, A1): never 'yürürlükte' and,
        # since the rows differ, no single target in either mode (until CT-FIX-04 the plain mode answered 0, stamped)
        for in_force in (False, True):
            with self.subTest(in_force=in_force):
                p = self.route("pending", in_force)
                self.assertExit(p, 1, "çelişiyor")
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

# ---------------------------------------------------------------------------------------------------- CT-FIX-04
# The six relations (v5 audit A1–A3, quality review 1–4, Codex's counter-examples) — each relation's normal and broken
# inputs through every consumer's real command line. A case's expectation is the matrix's (docs/records/audits/2026-10/
# ct-fix-04/README.md); none is relaxed to pass.
def reg_variant(confirmation="confirmed", strict="yes", scope="audit"):
    return FX.reg_head + FX.reg_line.replace("\tconfirmed\taudit\tfixture\tyes\tsop7\n",
                                             f"\t{confirmation}\t{scope}\tfixture\t{strict}\tsop7\n")


class CTFIX04(Base):
    mark = "acceptance-inputs"

    def with_main(self, main_text, branch_text):
        """origin/main holds main_text; the branch commits branch_text on top. origin/main is restored afterwards."""
        self.addCleanup(FX.git, "update-ref", "refs/remotes/origin/main", FX.marks["registered"])
        FX.put(REG, main_text)
        FX.git("update-ref", "refs/remotes/origin/main", FX.commit("CT-FIX-04: main's registry"))
        if branch_text != main_text:
            FX.put(REG, branch_text)
            FX.commit("CT-FIX-04: the branch's registry")
        return FX.git("rev-parse", "HEAD")

    def gate(self, prompt=None, prepare=False):
        path = FX.root.parent / "prompt.txt"
        path.write_text(prompt or FX.prompt(FX.git("rev-parse", "HEAD")), encoding="utf-8")
        return FX.run("scripts/prompt_gate.py", "check", str(path), "--queue", "ct", *(["--prepare"] if prepare else []))

    def report(self, *extra):
        return FX.run("scripts/status_report.py", "--mode", "v2", "--queue", "ct", *extra)

    def check(self, head, module="MOD-0288"):
        return FX.run("scripts/acceptance_record.py", "check", f"{ACCEPT}@{head}", "--queue", "ct", "--wp", "W1",
                      "--module", module)

    def accept_row(self, head):
        """W1 ACCEPTED with this record in the working tree and committed (validate and the report read it)."""
        FX.put("q.tsv", FX.qh + FX.qrow.replace("\tREADY\t", "\tACCEPTED\t").replace("\t-\t-\n", f"\t{ACCEPT}@{head}\t-\n"))
        return FX.commit("CT-FIX-04: W1 accepted")


# ---------------------------------------------------------------------------------------------------- İ1 (A1, A2)
class CTFIX04_I1_InForce(CTFIX04):
    """origin/main's row pending = preparation, never in force; a branch row that differs from main's (confirmation,
    strict) = a conflict for every consumer — ct_queue.in_force_status is the one function."""
    def variants(self):
        ok, pend = reg_variant(), reg_variant("pending")
        return {"both confirmed": (ok, ok), "both pending": (pend, pend), "branch confirmed, main pending": (pend, ok),
                "branch pending, main confirmed": (ok, pend), "branch strict=no, main strict=yes": (ok, reg_variant(strict="no"))}

    WANT = {  # consumer -> {variant: (exit, text)} — exact per fixture
        "require": {"both confirmed": (0, "— kayıtlı"), "both pending": (0, "— onaysız"), "conflict": (1, "çelişiyor")},
        "require --in-force": {"both confirmed": (0, "— kayıtlı"), "both pending": (1, "--in-force"), "conflict": (1, "çelişiyor")},
        "guard": {"both confirmed": (0, None), "both pending": (0, None), "conflict": (1, "çelişiyor")},
        "summary": {"both confirmed": (0, None), "both pending": (0, None), "conflict": (1, "çelişiyor")},
        "writers": {"both confirmed": (0, None), "both pending": (0, None), "conflict": (1, "çelişiyor")},
        "validate": {"both confirmed": (0, None), "both pending": (0, None), "conflict": (1, "çelişiyor")},
        "gate --prepare": {"both confirmed": (0, "DAĞITIM İZNİ DEĞİL"), "both pending": (0, "onaysız"), "conflict": (1, "ENGEL")},
        "gate": {"both confirmed": (0, "— kayıtlı"), "both pending": (1, "yürürlükte değil"), "conflict": (1, "ENGEL")},
        "report": {"both confirmed": (0, '"complete": true'), "both pending": (0, "YÜRÜRLÜK: onaysız"), "conflict": (1, "çelişiyor")},
    }

    def run_consumer(self, name):
        if name.startswith("gate"):
            return self.gate(prepare=name == "gate --prepare")
        if name == "report":
            return self.report()
        if name == "require --in-force":
            return FX.run("scripts/ct_queue.py", "require", "W1", "--queue", "ct", "--in-force")
        return FX.run("scripts/ct_queue.py", *CONSUMERS[name])

    def test_every_consumer_on_every_registry_relation(self):
        for variant, (main_text, branch_text) in self.variants().items():
            key = variant if variant in ("both confirmed", "both pending") else "conflict"
            for name, want in self.WANT.items():
                with self.subTest(variant=variant, consumer=name):
                    FX.reset(self.mark)
                    self.with_main(main_text, branch_text)
                    code, text = want[key]
                    if name == "report" and variant == "branch strict=no, main strict=yes":
                        code, text = 2, "katı (strict=yes) kuyruk değil"   # the report serves strict queues only (exit 2)
                    self.assertExit(self.run_consumer(name), code, text)


class CTFIX04_I1_Route(CTFIX04):
    """ct_route: the same relation, plus the ref's transition table and SOP §7 rules (ct_queue.profile_problems)."""
    def route(self, main_c, branch_c, in_force, branch_strict="yes", trans=None):
        FX.put(DCP, "---\nid: DCP-091\nstatus: approved\nowner: audit-coordinator\n---\n\n## 8.1 Delivery slices\n\n"
                    "| Slice | Kapsam | Not |\n|---|---|---|\n| MVP1 | MOD-0288 | synthetic |\n")
        scope = f"dcp:{DCP}#MVP1"
        self.with_main(reg_variant(main_c, scope=scope), reg_variant(branch_c, branch_strict, scope=scope))
        if trans is not None:
            FX.put(TRANS, trans)
            FX.commit("CT-FIX-04: the ref's transition table")
        return FX.run("scripts/ct_route.py", "--kind", "delivery", "--module", "MOD-0288", *(["--in-force"] if in_force else []))

    def test_relations(self):
        no_step = "".join(l + "\n" for l in FX.trans.splitlines() if not l.startswith("sop7\tREADY\tDISPATCHED\t"))
        cases = [("both confirmed", ("confirmed", "confirmed"), {}, (0, "sonuç: tek hedef"), (0, "yürürlükte (")),
                 ("both pending", ("pending", "pending"), {}, (0, "Hazırlık adayı"), (1, "sahip teyidi bekliyor")),
                 ("branch confirmed, main pending", ("pending", "confirmed"), {}, (1, "çelişiyor"), (1, "çelişiyor")),
                 ("branch pending, main confirmed", ("confirmed", "pending"), {}, (1, "çelişiyor"), (1, "çelişiyor")),
                 ("branch strict=no, main strict=yes", ("confirmed", "confirmed"), {"branch_strict": "no"}, (1, "çelişiyor"), (1, "çelişiyor")),
                 ("the ref's READY→DISPATCHED step removed", ("confirmed", "confirmed"), {"trans": no_step}, (2, None), (2, None))]
        for label, (main_c, branch_c), extra, plain, forced in cases:
            for in_force, (code, text) in ((False, plain), (True, forced)):
                with self.subTest(case=label, in_force=in_force):
                    FX.reset(self.mark)
                    p = self.route(main_c, branch_c, in_force, **extra)
                    self.assertExit(p, code, text)
                    if code != 0 or label != "both confirmed":
                        self.assertNotIn("yürürlük: yürürlükte", p.stdout)


# ---------------------------------------------------------------------------------------------------- İ2 (quality 1, Codex)
GOV_FM = lambda extra="", body="": f"---\nstatus: approved\n{extra}---\n{body}"


class CTFIX04_I2_Binding(CTFIX04):
    """A governance record binds W1 only explicitly: the row's record names it (path, or path@commit holding the same
    bytes as at Expected HEAD), or its künye's one work_packages line lists W1. A mention is not a binding."""
    def gate_with(self, text, record=None):
        FX.put(AUTHORITY, text)
        head = FX.commit("CT-FIX-04: scope record variant")
        if record is not None:
            FX.put("q.tsv", FX.qh + FX.qrow.replace("\t-\t-\n", f"\t{record}\t-\n"))
            head = FX.commit("CT-FIX-04: the row's record")
        return self.gate(FX.prompt(head))

    def test_explicit_bindings_pass(self):
        for label, text, record in (("work_packages: W1", GOV_FM("work_packages: W1\n"), None),
                                    ("work_packages: W2, W1", GOV_FM("work_packages: W2, W1\n"), None),
                                    ("the row's record, plain path", GOV_FM(), AUTHORITY)):
            with self.subTest(form=label):
                FX.reset(self.mark)
                self.assertExit(self.gate_with(text, record), 0)

    def test_the_row_s_record_at_an_older_commit_with_the_same_bytes_passes(self):
        FX.put(AUTHORITY, GOV_FM())
        older = FX.commit("CT-FIX-04: the scope record")
        FX.put("source-note.txt", "unrelated\n")
        FX.commit("CT-FIX-04: an unrelated change")
        for ref in (older, older[:9]):
            with self.subTest(version=len(ref)):
                self.assertExit(self.gate_with(GOV_FM(), f"{AUTHORITY}@{ref}"), 0)

    def test_no_explicit_binding_stops(self):
        for label, text in {
                "only a mention in the text": GOV_FM("", "W1 approved audit change\n"),
                "work_packages: W10": GOV_FM("work_packages: W10\n", "W1\n"),
                "work_packages: W1-R1": GOV_FM("work_packages: W1-R1\n", "W1\n"),
                "'W1 değil' in the text": GOV_FM("", "Bu kayıt W1 değil.\n"),
                "work_packages twice": GOV_FM("work_packages: W1\nwork_packages: W1\n"),
                "an indented second work_packages": GOV_FM("work_packages: W2\n  work_packages: W1\n"),
                "a commented work_packages": GOV_FM("# work_packages: W1\n", "W1\n"),
                "work_packages inside an HTML comment in the künye (Codex)": GOV_FM("<!--\nwork_packages: W1\n-->\n", "W1\n"),
                "an empty element": GOV_FM("work_packages: W1,,W2\n"), "a trailing comma": GOV_FM("work_packages: W1,\n"),
                "a spaced element": GOV_FM("work_packages: W 1, W2\n")}.items():
            with self.subTest(form=label):
                FX.reset(self.mark)
                self.assertExit(self.gate_with(text), 1, "ENGEL")

    def test_the_explicit_list_is_not_overridden_by_the_row_s_record(self):
        self.assertExit(self.gate_with(GOV_FM("work_packages: W2\n"), AUTHORITY), 1, "aşmaz")

    def test_a_broken_record_version_stops_and_work_packages_does_not_mask_it(self):
        self.addCleanup(lambda: subprocess.run(["git", "branch", "-D", "abcdef1234"], cwd=FX.root, env=ENV, capture_output=True))
        for wp in ("", "work_packages: W1\n"):
            text = GOV_FM(wp, "W1 approved audit change\n")
            FX.reset(self.mark)
            FX.put(AUTHORITY, GOV_FM("", "an earlier text\n"))
            differs = FX.commit("CT-FIX-04: another content of the scope record")
            FX.put(AUTHORITY, text)
            same = FX.commit("CT-FIX-04: the scope record as the gate reads it")
            # a tree id, a blob id and a branch named like an id that all hold the gate's own bytes: only the commit-object
            # check refuses them (the content comparison cannot)
            tree, blob = FX.git("rev-parse", f"{same}^{{tree}}"), FX.git("rev-parse", f"{same}:{AUTHORITY}")
            FX.git("branch", "-f", "abcdef1234", same)
            for label, version in (("a 40-hex id that is no object", "f" * 40), ("not hex", "not-a-commit"), ("empty", ""),
                                   ("a tree id holding the same record", tree), ("a blob id", blob),
                                   ("a branch named like an id, at a commit holding the same record", "abcdef1234"),
                                   ("a commit whose record differs", differs)):
                with self.subTest(version=label, work_packages=bool(wp)):
                    self.assertExit(self.gate_with(text, f"{AUTHORITY}@{version}"), 1, "sürüm bağı bozuk")


# ---------------------------------------------------------------------------------------------------- İ3 (quality 2, Codex)
OTHER_PACK = "execution/domains/audit/module-packs/MOD-0289-audit-ledger.md"


class CTFIX04_I3_PackIdentity(CTFIX04):
    """The pack's künye id, its file name's MOD id and the row / record module are one identity, through
    ct_queue.pack_identity — for the prompt gate (ürün) and the acceptance reader (module_pack, evidence source)."""
    def product_gate(self, pack_path, text):
        FX.put(pack_path, text)
        head = FX.commit("CT-FIX-04: pack variant")
        prompt = FX.prompt(head).replace(f"Görev sınıfı: yönetişim · Kapsam kaydı: {AUTHORITY}", f"Görev sınıfı: ürün · Paket: {pack_path}")
        return self.gate(prompt)

    def test_product_gate(self):
        good = (FX.root / PACK).read_text(encoding="utf-8")
        self.assertExit(self.product_gate(PACK, good), 0)
        for label, path, text in (
                ("file MOD-0288, künye id MOD-0289", PACK, good.replace("id: MOD-0288", "id: MOD-0289")),
                ("a second id", PACK, good.replace("id: MOD-0288", "id: MOD-0288\nid: MOD-0289")),
                ("an indented second id", PACK, good.replace("id: MOD-0288", "id: MOD-0288\n  id: MOD-0289")),
                ("a quoted second id", PACK, good.replace("id: MOD-0288", 'id: MOD-0288\n"id": MOD-0289')),
                ("no id", PACK, good.replace("id: MOD-0288\n", "")),
                ("file MOD-0289, künye id MOD-0288 (the row's module)", OTHER_PACK, good),
                # Codex (candidate1-identity): the file name's id is never cut out of a longer token
                ("file MOD-02880-…, künye id MOD-0288", PACK.replace("MOD-0288-", "MOD-02880-"), good),
                ("file MOD-0288X-…, künye id MOD-0288", PACK.replace("MOD-0288-", "MOD-0288X-"), good)):
            with self.subTest(form=label):
                FX.reset(self.mark)
                self.assertExit(self.product_gate(path, text), 1, "ENGEL")

    def test_acceptance_reader_checks_the_file_name(self):
        # Codex's 'wrong-filename' variant: the record binds a pack file named MOD-0289 (or MOD-02880-…) whose künye
        # says MOD-0288 — check, module-check (its own pack read, then through the queue), validate and the report
        for path in (OTHER_PACK, PACK.replace("MOD-0288-", "MOD-02880-")):
            with self.subTest(pack=os.path.basename(path)):
                FX.reset(self.mark)
                FX.put(path, (FX.root / PACK).read_text(encoding="utf-8"))
                h = FX.commit("CT-FIX-04: a pack under another module's file name")
                module_check = lambda: FX.run("scripts/acceptance_record.py", "module-check", "--queue", "ct", "--module",
                                              "MOD-0288", "--pack", f"{path}@{h}")
                self.assertExit(module_check(), 2, "dosya adındaki kimlikle")
                rec = FX.write_acceptance(module_pack=f"{path}@{h}")
                self.assertExit(self.check(rec), 1, "dosya adındaki kimlikle")
                self.accept_row(rec)
                self.assertExit(FX.run("scripts/ct_queue.py", *CONSUMERS["validate"]), 1)
                self.assertExit(module_check(), "nonzero")
                self.assertExit(self.report(), 1)

    def test_evidence_level_source_pack_identity_with_or_without_a_work_package(self):
        # Codex: a pack named MOD-0288 whose künye says MOD-0289, as the required_evidence_source; adding
        # 'work_package: W1' to it must not skip the identity check
        for label, extra in (("without work_package", ""), ("with work_package: W1", "work_package: W1\n")):
            with self.subTest(form=label):
                FX.reset(self.mark)
                FX.put(PACK, f"---\nid: MOD-0289\n{extra}status: approved\n---\nEvidence required E2\n## Acceptance Criteria\n"
                             "- [ ] AC-1 (zorunlu) — Audit Ledger tested\n")
                h = FX.commit("CT-FIX-04: a mismatched pack as the level source")
                line = 5 + (1 if extra else 0)
                rec = FX.write_acceptance(required_evidence_source=f"{PACK}@{h}:{line}")   # module_pack: the earlier, correct version
                self.assertExit(self.check(rec), 1, "dosya adındaki kimlikle")

    def test_evidence_level_source_this_module_s_pack_passes(self):
        h = FX.evidence_commit
        line = next(i for i, l in enumerate((FX.root / PACK).read_text(encoding="utf-8").splitlines(), 1) if "E2" in l)
        self.assertExit(self.check(FX.write_acceptance(required_evidence_source=f"{PACK}@{h}:{line}")), 0)


# ---------------------------------------------------------------------------------------------------- İ4 (quality 3, Codex)
TWO_AC_PACK = ("---\nid: MOD-0288\nstatus: approved\n---\nEvidence required: E2\n## Acceptance Criteria\n"
               "- [ ] AC-1 (zorunlu) — Audit Ledger tested\n- [ ] AC-2 (zorunlu) — Audit export tested\n")


class CTFIX04_I4_BoundList(CTFIX04):
    """A list bound to a pack version is that version's criteria only — every row an AC-<n> of it with the same text
    and mandatory flag; a WP may deliver a subset (module-check measures the module); a helper row is refused.
    Consumers: check, ct_queue validate (ACCEPTED row), module-check, the report."""
    def row(self, rid, text, req="evet"):
        return (f"| {rid} | {text} | Safety | CLI test | Expected observed | hayır | {req} | Expected observed | geçti | "
                f"{FX.artifact}@{FX.evidence_commit}:1 |\n")

    def variant(self, rows, pack=None):
        if pack is not None:
            FX.put(PACK, pack)
        ph = FX.commit("CT-FIX-04: the pack version")
        FX.put(LIST, FX.list_head + "".join(rows))
        FX.list_commit = FX.commit("CT-FIX-04: the list")
        self.addCleanup(setattr, FX, "list_commit", FX.marks["acceptance-inputs"])
        rec = FX.write_acceptance(module_pack=f"{PACK}@{ph}")
        return rec, ph

    def consumers(self, rec, ph):
        out = {"check": self.check(rec)}
        self.accept_row(rec)
        out["validate"] = FX.run("scripts/ct_queue.py", *CONSUMERS["validate"])
        out["module-check"] = FX.run("scripts/acceptance_record.py", "module-check", "--queue", "ct", "--module", "MOD-0288",
                                     "--pack", f"{PACK}@{ph}")
        out["report"] = self.report()
        return out

    def test_normal_and_subset(self):
        for label, rows, pack, want in (
                ("AC-1 exact", [self.row("AC-1", "Audit Ledger tested")], None,
                 {"check": 0, "validate": 0, "module-check": 0, "report": 0}),
                ("a WP's subset: AC-1 of AC-1, AC-2", [self.row("AC-1", "Audit Ledger tested")], TWO_AC_PACK,
                 {"check": 0, "validate": 0, "module-check": 1, "report": 0})):
            FX.reset(self.mark)
            res = self.consumers(*self.variant(rows, pack))
            for name, code in want.items():
                with self.subTest(case=label, consumer=name):
                    self.assertExit(res[name], code)

    def test_broken_lists(self):
        ok = self.row("AC-1", "Audit Ledger tested")
        for label, rows, text in (("CHECK-999 alone", [self.row("CHECK-999", "Audit Ledger tested")], "yardımcı satır"),
                                  ("K1 alone", [self.row("K1", "Audit Ledger tested")], "yardımcı satır"),
                                  ("AC-1 and CHECK-999", [ok, self.row("CHECK-999", "extra check")], "yardımcı satır"),
                                  ("AC-1 with another text", [self.row("AC-1", "Audit behavior")], "ölçüt metni"),
                                  ("AC-1 optional in the list", [self.row("AC-1", "Audit Ledger tested", "hayır")], "zorunluluğu"),
                                  ("AC-9, not in the pack", [ok, self.row("AC-9", "Audit Ledger tested")], "ölçütü değil")):
            FX.reset(self.mark)
            res = self.consumers(*self.variant(rows))
            for name, code, msg in (("check", 1, text), ("validate", 1, None), ("module-check", "nonzero", None), ("report", 1, None)):
                with self.subTest(case=label, consumer=name):
                    self.assertExit(res[name], code, msg)


# ---------------------------------------------------------------------------------------------------- İ5 (A3, Codex)
class CTFIX04_I5_ReportExit(CTFIX04):
    """A printed report exits 1 when it is not complete; a baseline never offered and absent at the default path is a
    supported result (0). An offered absent baseline and an existing invalid one are incomplete (1)."""
    def baseline(self, approval_status="approved", digest_ok=True, ledger_text=None):
        lt = ("---\nformat: effort-ledger-v1\nqueue: ct\nbaseline_version: 1\nunit: saat\n"
              "weights: backend=0.5 frontend=0.3 test=0.2\napproval_record: " + APPROVAL + "@" + "a" * 40 + "\n---\n\n"
              "| ID | Backend | Frontend | Test |\n|---|---|---|---|\n| W1 | 8 | 4 | 2 |\n")
        digest = hashlib.sha256("".join(l for l in lt.splitlines(keepends=True)
                                        if not l.startswith("approval_record:")).encode()).hexdigest()
        FX.put(APPROVAL, f"---\nstatus: {approval_status}\nledger: {LEDGER}\nbaseline_version: 1\n"
                         f"ledger_content_sha256: {digest if digest_ok else 'f' * 64}\n---\n")
        aph = FX.commit("CT-FIX-04: effort approval")
        FX.put(LEDGER, ledger_text if ledger_text is not None else lt.replace("a" * 40, aph))
        FX.commit("CT-FIX-04: effort ledger")

    def test_exit_codes(self):
        for label, setup, extra, code, text in (
                ("no baseline offered, none at the default path", None, (), 0, "Taban yok — hesaplanamadı"),
                ("a valid baseline", {}, (), 0, '"complete": true'),
                ("an offered baseline that is absent", None, ("--effort", "execution/delivery/effort/missing.md"), 1, "efor tabanı"),
                ("the approval record a draft", {"approval_status": "draft"}, (), 1, "efor tabanı"),
                ("the approval's ledger hash wrong", {"digest_ok": False}, (), 1, "efor tabanı"),
                ("a broken ledger", {"ledger_text": "---\nformat: effort-ledger-v1\n---\nnot a table\n"}, (), 1, "efor tabanı")):
            with self.subTest(case=label):
                FX.reset(self.mark)
                if setup is not None:
                    self.baseline(**setup)
                p = self.report(*extra)
                self.assertExit(p, code, text)
                if code == 1:
                    self.assertIn('"complete": false', p.stdout)
                    self.assertIn("tamamlık: eksik — ", p.stdout)
                    self.assertIn("rapor eksik — çıkış 1", p.stderr)


# ---------------------------------------------------------------------------------------------------- conditional approval
class CTFIX04_ConditionalApproval(CTFIX04):
    """Joint decision with Codex (Employee pilot): a pack whose künye carries approval_status other than 'approved'
    gives no product dispatch by its status alone. Conditional: --prepare passes and says so, a dispatch stops; any
    other value or an unreadable field stops in both modes. No record overrides it; packs and running work unchanged."""
    def product(self, approval, prepare):
        good = (FX.root / PACK).read_text(encoding="utf-8")
        text = good if approval is None else good.replace("status: approved\n", f"status: approved\n{approval}", 1)
        FX.put(PACK, text)
        head = FX.commit("CT-FIX-04: pack approval variant")
        prompt = FX.prompt(head).replace(f"Görev sınıfı: yönetişim · Kapsam kaydı: {AUTHORITY}", f"Görev sınıfı: ürün · Paket: {PACK}")
        return self.gate(prompt, prepare=prepare)

    def test_matrix(self):
        cond = "approval_status: conditionally-approved-for-draft-reference-validation-slice\n"
        for label, approval, dispatch, prepare in (
                ("no second approval field", None, (0, "sonuç: geçti"), (0, "DAĞITIM İZNİ DEĞİL")),
                ("approval_status: approved", "approval_status: approved\n", (0, "sonuç: geçti"), (0, "DAĞITIM İZNİ DEĞİL")),
                ("conditional (the measured MOD-0251 value)", cond, (1, "kapsam türetilmez"), (0, "HAZIRLIK: yeni dağıtımda ENGEL olur")),
                ("rejected", "approval_status: rejected\n", (1, "onaylı değil"), (1, "onaylı değil")),
                ("an unknown value", "approval_status: conditionally approved\n", (1, "onaylı değil"), (1, "onaylı değil")),
                ("written twice", "approval_status: approved\napproval_status: rejected\n", (1, "okunamıyor"), (1, "okunamıyor")),
                ("an indented second one", "approval_status: approved\n  approval_status: rejected\n", (1, "okunamıyor"), (1, "okunamıyor"))):
            for mode, (code, text) in (("dispatch", dispatch), ("--prepare", prepare)):
                with self.subTest(case=label, mode=mode):
                    FX.reset(self.mark)
                    self.assertExit(self.product(approval, mode == "--prepare"), code, text)


# ---------------------------------------------------------------------------------------------------- CT-FIX-05 (v6 audit)
class CTFIX05_F1_MainRegistryWhole(CTFIX04):
    """F1: origin/main's registry is validated as a whole before the queue is picked — another queue on the same task
    file, or any broken row, makes the file invalid for every consumer."""
    def variants(self):
        two = FX.reg_head + FX.reg_line + FX.reg_line.replace("ct\tAudit CT\t", "ct2\tOther CT\t", 1)
        broken = FX.reg_head + FX.reg_line + FX.reg_line.replace("ct\tAudit CT\tq.tsv", "zz\t\tz.tsv", 1)
        return {"two queues on one task file": two, "an unrelated row with an empty field": broken}

    def test_every_consumer_stops(self):
        for label, main_text in self.variants().items():
            for name, args, code, text in (
                    ("validate", ("scripts/ct_queue.py", *CONSUMERS["validate"]), 1, "kayıt geçersiz"),
                    ("require", ("scripts/ct_queue.py", *CONSUMERS["require"]), 1, "kayıt geçersiz"),
                    ("require --in-force", ("scripts/ct_queue.py", "require", "W1", "--queue", "ct", "--in-force"), 1, "kayıt geçersiz"),
                    ("guard", ("scripts/ct_queue.py", *CONSUMERS["guard"]), 1, "kayıt geçersiz"),
                    ("summary", ("scripts/ct_queue.py", *CONSUMERS["summary"]), 1, "kayıt geçersiz"),
                    ("writers", ("scripts/ct_queue.py", *CONSUMERS["writers"]), 1, "kayıt geçersiz"),
                    ("report", ("scripts/status_report.py", "--mode", "v2", "--queue", "ct"), 1, "kayıt geçersiz")):
                with self.subTest(main=label, consumer=name):
                    FX.reset(self.mark)
                    self.with_main(main_text, FX.reg_head + FX.reg_line)
                    self.assertExit(FX.run(*args), code, text)
            for prepare in (False, True):
                with self.subTest(main=label, consumer="gate" + (" --prepare" if prepare else "")):
                    FX.reset(self.mark)
                    self.with_main(main_text, FX.reg_head + FX.reg_line)
                    self.assertExit(self.gate(prepare=prepare), 1, "ENGEL")

    def test_a_valid_main_registry_with_another_queue_still_registers(self):
        FX.put("q2.tsv", FX.qh + FX.qrow)
        h2 = FX.commit("CT-FIX-05: another queue's own task file")
        ct2 = FX.reg_line.replace("ct\tAudit CT\tq.tsv", "ct2\tOther CT\tq2.tsv", 1).replace(FX.marks["base"], h2)
        self.with_main(FX.reg_head + FX.reg_line + ct2, FX.reg_head + FX.reg_line)
        self.assertExit(FX.run("scripts/ct_queue.py", "require", "W1", "--queue", "ct", "--in-force"), 0, "— kayıtlı")

    def test_the_same_task_file_under_a_non_normal_path(self):
        # Codex (relationship-probes): ct2 at './q.tsv' names the same file as ct at 'q.tsv' — a path is a normal
        # repo-relative path or the registry is invalid; the uniqueness check then compares like with like
        for spelled in ("./q.tsv", "q.tsv/", "x/../q.tsv", "/q.tsv"):
            with self.subTest(path=spelled):
                FX.reset(self.mark)
                ct2 = FX.reg_line.replace("ct\tAudit CT\tq.tsv", f"ct2\tOther CT\t{spelled}", 1)
                self.with_main(FX.reg_head + FX.reg_line + ct2, FX.reg_head + FX.reg_line)
                for name in ("validate", "require", "summary"):
                    self.assertExit(FX.run("scripts/ct_queue.py", *CONSUMERS[name]), 1, "kayıt geçersiz")
                self.assertExit(FX.run("scripts/ct_queue.py", "require", "W1", "--queue", "ct", "--in-force"), 1, "kayıt geçersiz")
                self.assertExit(self.gate(), 1, "ENGEL")
                self.assertExit(self.report(), 1, "kayıt geçersiz")

    def test_the_branch_registry_refuses_a_non_normal_path(self):
        FX.put(REG, FX.reg_head + FX.reg_line.replace("\tq.tsv\t", "\t./q.tsv\t", 1))
        self.assertExit(FX.run("scripts/ct_queue.py", *CONSUMERS["validate"]), 1, "normal")


class CTFIX05_F2_Reassessment(CTFIX04):
    """F2 (b): an approved reassessment record opens a -R3 retry only when its künye's work_packages lists that retry's id
    exactly; a version after '@' is a commit holding the same record, never cut off."""
    mark = "registered"

    def require_r3(self, text, record=None):
        FX.put(REASSESS, text)
        head = FX.commit("CT-FIX-05: reassessment record")
        rows = REASSESS_ROWS if record is None else REASSESS_ROWS.replace(f"\t{REASSESS}\n", f"\t{record}\n")
        FX.put("q.tsv", FX.qh + FX.qrow + rows)
        return head, (FX.run("scripts/ct_queue.py", "require", "W2-R3", "--queue", "ct"),
                      FX.run("scripts/ct_queue.py", *CONSUMERS["validate"]))

    def test_normal(self):
        ok = "---\nstatus: approved\nwork_packages: W2-R3\n---\nresolved\n"
        for label, extra in (("the retry listed", None), ("the retry among others", "W2-R3, W9")):
            with self.subTest(form=label):
                FX.reset(self.mark)
                text = ok if extra is None else ok.replace("W2-R3", extra)
                _, (req, val) = self.require_r3(text)
                self.assertExit(req, 0, "DAĞITILABİLİR W2-R3")
                self.assertExit(val, 0)

    def test_the_row_s_version_at_a_commit_holding_the_same_record(self):
        ok = "---\nstatus: approved\nwork_packages: W2-R3\n---\nresolved\n"
        FX.put(REASSESS, ok)
        older = FX.commit("CT-FIX-05: the reassessment record")
        for ref in (older, older[:9]):
            with self.subTest(version=len(ref)):
                _, (req, _) = self.require_r3(ok, f"{REASSESS}@{ref}")
                self.assertExit(req, 0, "DAĞITILABİLİR W2-R3")

    def test_not_bound_to_the_retry(self):
        for label, wp in (("no work_packages (the row's column alone)", ""), ("the original W2 only", "work_packages: W2\n"),
                          ("another WP (v6 audit)", "work_packages: UNRELATED-WP\n"), ("W2-R4", "work_packages: W2-R4\n"),
                          ("written twice", "work_packages: W2-R3\nwork_packages: W2-R3\n"), ("an empty element", "work_packages: W2-R3,,W9\n")):
            with self.subTest(form=label):
                FX.reset(self.mark)
                _, (req, val) = self.require_r3(f"---\nstatus: approved\n{wp}---\nresolved\n")
                self.assertExit(req, 1, "§28.2")
                self.assertExit(val, 1, "§28.2")

    def test_summary_counts_what_require_accepts(self):
        # Codex (relationship-probes): require refused the retry while summary still counted it as "require'ın kabul edeceği"
        _, (req, _) = self.require_r3("---\nstatus: approved\nwork_packages: UNRELATED-WP\n---\nresolved\n")
        self.assertExit(req, 1, "§28.2")
        p = FX.run("scripts/ct_queue.py", *CONSUMERS["summary"])
        self.assertExit(p, 0, "DAĞITILABİLİR KİMLİK 1 ")
        self.assertIn("DENETİMDEN GEÇMEYEN 1 kimlik", p.stdout)
        self.assertIn("W2-R3", p.stdout)
        FX.reset(self.mark)
        self.require_r3("---\nstatus: approved\nwork_packages: W2-R3\n---\nresolved\n")
        p = FX.run("scripts/ct_queue.py", *CONSUMERS["summary"])
        self.assertExit(p, 0, "DAĞITILABİLİR KİMLİK 2 ")
        self.assertNotIn("DENETİMDEN GEÇMEYEN", p.stdout)

    def test_a_broken_version_stops(self):
        ok = "---\nstatus: approved\nwork_packages: W2-R3\n---\nresolved\n"
        FX.put(REASSESS, "---\nstatus: approved\nwork_packages: W2-R3\n---\nan earlier text\n")
        differs = FX.commit("CT-FIX-05: another content")
        FX.put(REASSESS, ok)
        same = FX.commit("CT-FIX-05: the same content")
        tree = FX.git("rev-parse", f"{same}^{{tree}}")
        for label, version in (("a 40-hex id that is no object (v6 audit)", "f" * 40), ("not hex", "not-a-commit"),
                               ("a tree id holding the same record", tree), ("a commit whose record differs", differs)):
            with self.subTest(version=label):
                _, (req, _) = self.require_r3(ok, f"{REASSESS}@{version}")
                self.assertExit(req, 1, "sürüm")


class CTFIX05_F4_Timeout(CTFIX04):
    """F4: only a completed Failed row is a known failure."""
    mark = "registered"

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

    def test_outcomes(self):
        self.assertExit(self.check(trx([("Test.Known", "Failed", MSG)])), 0, "BİLİNEN=1")
        for o in ("Timeout", "Error", "Aborted"):
            with self.subTest(outcome=o):
                key = o[0].lower() + o[1:]
                p = self.check(trx([("Test.Known", o, MSG)], "Failed", {"total": 1, "executed": 1, "passed": 0, "failed": 0, key: 1}))
                self.assertExit(p, 1, f"sonuç türü {o}")
                self.assertIn("BİLİNEN=0", p.stdout)


class CTFIX05_F6_Previous(CTFIX04):
    """F6: an explicitly given --previous that cannot be verified makes the report incomplete (exit 1)."""
    mark = "registered"

    def test_previous(self):
        p = self.report()
        self.assertExit(p, 0)
        FX.put("previous-report.txt", p.stdout)
        good = FX.commit("CT-FIX-05: a previous report")
        self.assertExit(self.report("--previous", f"previous-report.txt@{good}"), 0, '"status": "doğrulandı"')
        FX.put("previous-report.txt", p.stdout.replace("kuyruk: ct", "kuyruk: ct (değiştirildi)", 1))
        bad = FX.commit("CT-FIX-05: the visible text of the previous report changed")
        for label, spec in (("a changed visible text (v6 audit)", f"previous-report.txt@{bad}"),
                            ("a path not at the commit", f"no-such-report.txt@{good}"), ("not path@commit", "previous-report.txt")):
            with self.subTest(form=label):
                q = self.report("--previous", spec)
                self.assertExit(q, 1, "önceki rapor")
                self.assertIn('"complete": false', q.stdout)


class CTFIX05_F3_RunRecord(CTFIX04):
    """F3: the evidence is the real runner's record of a run on exactly the accepted code state, in a fresh directory;
    an old record for changed code, a non-zero raw exit, an evidence commit that also changes code, or a row citing a
    file the run did not produce gives no automatic ACCEPTED (and no percentages)."""
    def runner(self, out, *argv):
        return FX.run("scripts/run_record.py", "run", "--out", out, "--", *argv)

    def test_the_runner_refuses(self):
        cmd = (sys.executable, "-c", "print('x')")
        self.assertExit(self.runner("src/run-2", *cmd), 2, "docs/records/audits/")
        self.assertExit(self.runner("docs/records/audits/../run-2", *cmd), 2, "normal")
        self.assertExit(self.runner(RUN_DIR, *cmd), 2, "zaten var")
        FX.put("stray.txt", "untracked\n")
        self.assertExit(self.runner("docs/records/audits/run-2", *cmd), 2, "temiz değil")
        FX.reset(self.mark)
        p = self.runner("docs/records/audits/run-4", sys.executable, "-c",
                        "import os; os.mkfifo('docs/records/audits/run-4/pipe')")      # Codex: a FIFO in the output
        self.assertExit(p, 2, "normal dosya olmayan")
        FX.reset(self.mark)
        p = self.runner("docs/records/audits/run-3", sys.executable, "-c", "open('source.txt', 'a').write('changed during the run')")
        self.assertExit(p, 2, "dışında değişiklik")
        self.assertFalse((FX.root / "docs/records/audits/run-3/run-record-v1.json").exists())

    def test_normal_and_the_raw_exit_code(self):
        self.assertExit(self.check(FX.write_acceptance()), 0, "ACCEPTED'e izin verir")
        FX.reset(self.mark)
        code = FX.git("rev-parse", "HEAD")              # the run's code state: the list commit (same code as the fixture's)
        p = self.runner("docs/records/audits/run-7", sys.executable, "-c", "import sys; print('AC-1 checked'); sys.exit(7)")
        self.assertExit(p, 0, "komutun çıkışı 7")
        e7 = FX.commit("CT-FIX-05: a run that exited 7")
        FX.put(LIST, FX.list_head + FX.list_row.replace(f"{FX.artifact}@{FX.evidence_commit}:1", f"docs/records/audits/run-7/stdout.txt@{e7}:1"))
        l7 = FX.commit("CT-FIX-05: the list citing that run")
        p = self.check(FX.write_acceptance(commit=code, evidence=f"docs/records/audits/run-7/run-record-v1.json@{e7}",
                                           acceptance_list=f"{LIST}@{l7}"))
        self.assertExit(p, 1, "ham çıkışı 7")
        self.assertEqual(sum(l.startswith("ENGEL") for l in p.stdout.splitlines()), 1, p.stdout)   # only the raw exit

    def test_old_evidence_does_not_accept_changed_code(self):
        FX.put("source.txt", "Audit source contract, changed\n")
        changed = FX.commit("CT-FIX-05: the code changed after the run")
        rec = FX.write_acceptance(commit=changed)
        self.assertExit(self.check(rec), 1, "eski kanıt yeni kodu")
        self.accept_row(rec)
        self.assertExit(self.report(), 1)

    def test_the_evidence_commit_is_not_code_and_rows_cite_artifacts(self):
        FX.put("source.txt", "Audit source contract, changed with the evidence\n")
        e2 = FX.commit("CT-FIX-05: an evidence commit that also changes code")
        rec = FX.write_acceptance(evidence=f"{RUN_DIR}/run-record-v1.json@{e2}")
        self.assertExit(self.check(rec), 1, "kanıt dışında da farklı")
        FX.reset(self.mark)
        FX.put(LIST, FX.list_head + FX.list_row.replace(f"{FX.artifact}@{FX.evidence_commit}:1", f"wp.md@{FX.code_commit}:6"))
        FX.list_commit = FX.commit("CT-FIX-05: a row citing a file the run did not produce")
        self.addCleanup(setattr, FX, "list_commit", FX.marks["acceptance-inputs"])
        self.assertExit(self.check(FX.write_acceptance()), 1, "artefaktı değil")


class CTFIX05_C1_GovernanceScope(CTFIX04):
    """C1: a governance work package writes no product code; its allowed paths stay out of services/, frontend/, gateway/,
    with no symbolic link at or under their fixed prefix (every ancestor component too)."""
    def gate_allowed(self, allowed):
        head = FX.git("rev-parse", "HEAD")
        return self.gate(FX.prompt(head).replace("İzinli yollar: source.txt", f"İzinli yollar: {allowed}"))

    def test_scope(self):
        self.assertExit(self.gate_allowed("source.txt"), 0)
        self.assertExit(self.gate_allowed("docs/**"), 0)
        for allowed in ("services/Diten.HcmService/**", "frontend/x.js", "gateway", "**", "{services,docs}/**", "docs/../services/x.cs"):
            with self.subTest(allowed=allowed):
                self.assertExit(self.gate_allowed(allowed), 1, "yönetişim işinin izinli yolu")

    def test_a_symbolic_link_on_the_way(self):
        (FX.root / "services").mkdir(exist_ok=True)
        (FX.root / "services/a.cs").write_text("x\n", encoding="utf-8")
        (FX.root / "docs").mkdir(exist_ok=True)
        os.symlink("../services", FX.root / "docs/alias")
        FX.git("add", "--", "docs/alias")                 # Fixture.commit lists files; a directory link is staged by name
        FX.commit("CT-FIX-05: a symbolic link docs/alias -> ../services")
        self.assertEqual(FX.git("ls-tree", "HEAD", "--", "docs/alias").split()[0], "120000")
        for allowed in ("docs/alias/New.cs", "docs/alias/sub/New.cs", "docs/alias/**", "docs/**"):
            with self.subTest(allowed=allowed):
                self.assertExit(self.gate_allowed(allowed), 1, "sembolik bağ")


class CTFIX05_C2_E1_PackIdentityAndApproval(CTFIX04):
    """C2: the file's full pack id is the künye id, then the parent relation — for the gate and the acceptance reader
    alike. E1: a conditional pack gives no new automatic acceptance; a rejected one stops; the draft still comes."""
    CHILD = "execution/domains/audit/module-packs/MOD-0288-FU06B-audit-ledger-detail.md"

    def child_text(self, pid):
        return (FX.root / PACK).read_text(encoding="utf-8").replace("id: MOD-0288", f"id: {pid}")

    def test_a_lettered_child_pack(self):
        FX.put(self.CHILD, self.child_text("MOD-0288-FU06B"))
        h = FX.commit("CT-FIX-05: a lettered child pack")
        prompt = FX.prompt(h).replace(f"Görev sınıfı: yönetişim · Kapsam kaydı: {AUTHORITY}", f"Görev sınıfı: ürün · Paket: {self.CHILD}")
        self.assertExit(self.gate(prompt), 0)
        self.assertExit(self.check(FX.write_acceptance(module_pack=f"{self.CHILD}@{h}")), 0, "ACCEPTED'e izin verir")
        FX.reset(self.mark)
        FX.put(self.CHILD, self.child_text("MOD-0288-FU07B"))
        h = FX.commit("CT-FIX-05: a child pack whose künye names another child")
        prompt = FX.prompt(h).replace(f"Görev sınıfı: yönetişim · Kapsam kaydı: {AUTHORITY}", f"Görev sınıfı: ürün · Paket: {self.CHILD}")
        self.assertExit(self.gate(prompt), 1, "dosya adındaki kimlikle")
        self.assertExit(self.check(FX.write_acceptance(module_pack=f"{self.CHILD}@{h}")), 1, "dosya adındaki kimlikle")

    def test_a_conditional_or_rejected_pack_in_acceptance(self):
        good = (FX.root / PACK).read_text(encoding="utf-8")
        for value, code in (("conditionally-approved-for-p2-slice", 1), ("rejected", 1)):
            with self.subTest(approval=value):
                FX.reset(self.mark)
                FX.put(PACK, good.replace("status: approved\n", f"status: approved\napproval_status: {value}\n", 1))
                h = FX.commit("CT-FIX-05: the pack's second approval field")
                why = "inceleme gerekir" if value.startswith("conditionally") else "onaylı değil"
                self.assertExit(self.check(FX.write_acceptance(module_pack=f"{PACK}@{h}")), code, why)
                self.assertExit(FX.run("scripts/acceptance_record.py", "module-check", "--queue", "ct", "--module", "MOD-0288",
                                       "--pack", f"{PACK}@{h}"), 2, why)
                draft = FX.run("scripts/acceptance_record.py", "draft", "--part", "list", "--queue", "ct", "--wp", "W1", "--pack", f"{PACK}@{h}")
                if value.startswith("conditionally"):
                    self.assertExit(draft, 0, "taslak hazırlık içindir")
                else:
                    self.assertExit(draft, 2, "onaylı değil")


class CTFIX06_DiscoverySummary(CTFIX04):
    """CT-FIX-06 (v7 Employee pilot): the real --discover writes a bounded 'ÖZET' block under the header; the BULGU
    lines, the gate's decision count and --check-report stay as they were."""
    def discover(self):
        return FX.run(".antigravity/scripts/verify_module_id.py", str(FX.root), "--discover", "Audit Ledger")

    def test_the_fixture_report_carries_the_summary_and_the_gate_s_count_is_unchanged(self):
        lines = FX.read("discovery.txt").splitlines()
        self.assertEqual(lines[0], "KEŞİF RAPORU v2")
        k = next(i for i, l in enumerate(lines) if i and not l.startswith("ÖZET "))      # the block right under the header
        self.assertGreater(k, 2)
        self.assertTrue(lines[k].startswith("sorgu: "), lines[k])
        self.assertFalse([l for l in lines[k:] if l.startswith("ÖZET ")])
        self.assertTrue(lines[k - 1].startswith("ÖZET sınır: tam rapor ") and f" {len(lines)} satır" in lines[k - 1], lines[k - 1])
        self.assertEqual(sum(l.startswith("BULGU ") for l in lines), FX.discovery_findings)
        p = FX.run(".antigravity/scripts/verify_module_id.py", "--check-report", str(FX.root / "discovery.txt"))
        self.assertExit(p, 0, "RAPOR GEÇERLİ")
        self.assertExit(self.gate(), 0)

    def test_one_file_id_on_two_pack_paths_is_a_possible_collision_first(self):
        FX.put("execution/domains/other/module-packs/MOD-0288-audit-ledger-external.md",
               "---\nid: MOD-0288\nstatus: ready-for-dev\nowner: other-team\n---\n# x\n")
        FX.commit("CT-FIX-06: the same file id on a second pack path")
        p = self.discover()
        self.assertExit(p, 3)
        out = p.stdout.splitlines()
        self.assertTrue(out[1].startswith("ÖZET bulgular: "), out[1])
        self.assertTrue(out[2].startswith("ÖZET olası kimlik çakışması — inceleme gerekli (tek başına kesin mükerrer değil): MOD-0288"), out[2])
        self.assertIn("other/module-packs/MOD-0288-audit-ledger-external.md", out[2])
        self.assertTrue(any(l.startswith("ÖZET paket künyesi: execution/domains/other/") and "owner other-team" in l for l in out), out[:8])

    def test_different_kunye_values_on_another_ref_stay_unresolved(self):
        FX.put(PACK, FX.read(PACK).replace("status: approved\n", "status: approved\nowner: another-owner\n", 1))
        FX.commit("CT-FIX-06: the pack's owner on another ref")
        FX.git("branch", "-f", "ctfix06-other")
        self.addCleanup(FX.git, "branch", "-D", "ctfix06-other")
        FX.reset(self.mark)
        p = self.discover()
        self.assertExit(p, 3)
        line = next(l for l in p.stdout.splitlines() if l.startswith(f"ÖZET paket künyesi: {PACK}"))
        self.assertIn("belirsiz — 2 farklı künye", line)
        self.assertIn("owner another-owner (blob ", line)
        self.assertIn("refs/heads/ctfix06-other", line)


class CTFIX06_EvidenceLineAndRegistry(CTFIX04):
    """CT-FIX-06 (v7 audit EK1-2, EK1-6): a cited evidence line is a real, non-blank line of a run artifact; the branch
    registry's required columns are filled — one check (check_registry) for every consumer."""
    def run_with(self, n, script, cited):
        code = FX.git("rev-parse", "HEAD")
        out = f"docs/records/audits/run-ek12-{n}"
        p = FX.run("scripts/run_record.py", "run", "--out", out, "--", sys.executable, "-c", script)
        self.assertExit(p, 0, "KOŞU KAYDI")
        e = FX.commit("CT-FIX-06: a run whose output is cited")
        FX.put(LIST, FX.list_head + FX.list_row.replace(f"{FX.artifact}@{FX.evidence_commit}:1", f"{out}/stdout.txt@{e}:{cited}"))
        l = FX.commit("CT-FIX-06: the list citing it")
        return self.check(FX.write_acceptance(commit=code, evidence=f"{out}/run-record-v1.json@{e}", acceptance_list=f"{LIST}@{l}"))

    def test_a_cited_line_must_be_a_real_filled_line(self):
        for n, (label, script, cited, want, text) in enumerate((
                ("empty output, line 1", "pass", 1, 1, "kanıt satırı 1 dosyada yok"),
                ("one line ending in LF, the phantom line 2", "print('AC-1 checked')", 2, 1, "kanıt satırı 2 dosyada yok"),
                ("a line of spaces", "print('AC-1 checked'); print('   ')", 2, 1, "kanıt satırı 2 boş"),
                ("a filled line", "print('AC-1 checked: expected observed')", 1, 0, "ACCEPTED'e izin verir"))):
            with self.subTest(case=label):
                FX.reset(self.mark)
                self.assertExit(self.run_with(n, script, cited), want, text)

    def test_an_empty_required_column_in_the_branch_registry_stops(self):
        for column, index in (("scope", 5), ("evidence", 6)):
            with self.subTest(column=column):
                FX.reset(self.mark)
                cells = FX.reg_line.rstrip("\n").split("\t")
                cells[index] = ""
                FX.put(REG, FX.reg_head + "\t".join(cells) + "\n")
                for args in (CONSUMERS["validate"], CONSUMERS["require"], ["require", "W1", "--queue", "ct", "--in-force"]):
                    self.assertExit(FX.run("scripts/ct_queue.py", *args), 1, f"boş zorunlu alan {column}")
        FX.reset(self.mark)
        self.assertExit(FX.run("scripts/ct_queue.py", *CONSUMERS["validate"]), 0)   # the intact registry


if __name__ == "__main__":
    unittest.main()
