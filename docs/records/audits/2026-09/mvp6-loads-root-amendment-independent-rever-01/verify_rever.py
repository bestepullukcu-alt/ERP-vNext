#!/usr/bin/env python3
from __future__ import annotations

import difflib
import hashlib
import json
import shutil
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
C1 = ROOT / "docs/records/audits/2026-09/mvp6-loads-root-acquisition-amendment-candidate-01"
C2 = ROOT / "docs/records/audits/2026-09/mvp6-loads-root-acquisition-amendment-candidate-02"
PREV = ROOT / "docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-ver-01"

EXPECTED = {
    "baseline_yaml": "5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c",
    "baseline_annex": "a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1",
    "candidate_yaml": "aeb354e8cc5d06f36bc0ee0e4e43b28a8701a2c3bec44200d47cec9095dfc744",
    "candidate_annex": "e2107301e3c6cfae5e6768de77c19f5b4fa5ea7ff42f1a4023650bd3fb7750cd",
    "patch": "27cc3a8f6c5000b9d06b80b2b0037e84750cd7bc40611da4b5f6fa33582e11cc",
    "successor_delta": "79de2b2c59d085e7759fc356a454a697d35e6052a8f3a18049bf755a8c70a0b5",
    "prior_raw": "8255831b4510abdcc60a29ebc3d860df7a37e2d9f77305a1da00df180f82aa9c",
}

PATHS = {
    "baseline_yaml": ROOT / "docs/analysis/contracts/shipment-bundle.openapi.yaml",
    "baseline_annex": ROOT / "docs/analysis/contracts/loads-semantics-v2.0.0.md",
    "candidate_yaml": C2 / "shipment-bundle-v3.1.0-rc.1.openapi.yaml",
    "candidate_annex": C2 / "loads-semantics-v3.1.0-rc.1.md",
    "patch": C2 / "amendment.patch",
    "successor_delta": C2 / "successor-delta.patch",
    "prior_raw": PREV / "raw/raw-results.json",
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(args: list[str], cwd: Path) -> dict:
    p = subprocess.run(args, cwd=cwd, text=True, capture_output=True)
    return {"command": args, "exit": p.returncode, "stdout": p.stdout, "stderr": p.stderr}


def main() -> int:
    result: dict = {"hashes": {}, "apply": {}, "delta": {}, "inherited": {}}
    for key, path in PATHS.items():
        actual = sha(path)
        result["hashes"][key] = {
            "path": str(path.relative_to(ROOT)),
            "expected": EXPECTED[key],
            "actual": actual,
            "match": actual == EXPECTED[key],
        }

    with tempfile.TemporaryDirectory(prefix="mvp6-loads-root-rever-") as td:
        work = Path(td)
        contract_dir = work / "docs/analysis/contracts"
        contract_dir.mkdir(parents=True)
        shutil.copy2(PATHS["baseline_yaml"], contract_dir / "shipment-bundle.openapi.yaml")
        shutil.copy2(PATHS["baseline_annex"], contract_dir / "loads-semantics-v2.0.0.md")
        check = run(["git", "apply", "--check", str(PATHS["patch"])], work)
        apply = run(["git", "apply", str(PATHS["patch"])], work)
        applied_yaml = contract_dir / "shipment-bundle.openapi.yaml"
        applied_annex = contract_dir / "loads-semantics-v3.1.0-rc.1.md"
        result["apply"] = {
            "check": check,
            "apply": apply,
            "applied_yaml_sha256": sha(applied_yaml) if applied_yaml.exists() else None,
            "applied_annex_sha256": sha(applied_annex) if applied_annex.exists() else None,
            "yaml_byte_equal": applied_yaml.read_bytes() == PATHS["candidate_yaml"].read_bytes() if applied_yaml.exists() else False,
            "annex_byte_equal": applied_annex.read_bytes() == PATHS["candidate_annex"].read_bytes() if applied_annex.exists() else False,
            "baseline_v2_annex_preserved": sha(contract_dir / "loads-semantics-v2.0.0.md") == EXPECTED["baseline_annex"],
        }

    old_yaml = C1 / "shipment-bundle-v3.1.0-rc.1.openapi.yaml"
    old_annex = C1 / "loads-semantics-v3.1.0-rc.1.md"
    old_lines = old_annex.read_text().splitlines(keepends=True)
    new_lines = PATHS["candidate_annex"].read_text().splitlines(keepends=True)
    diff = list(difflib.unified_diff(old_lines, new_lines, fromfile="candidate-01/loads-semantics-v3.1.0-rc.1.md", tofile="candidate-02/loads-semantics-v3.1.0-rc.1.md"))
    changed = [(i + 1, a.rstrip("\n"), b.rstrip("\n")) for i, (a, b) in enumerate(zip(old_lines, new_lines)) if a != b]
    expected_old = "info.version2.0.0 is candidate metadata only; wire contractVersion v1 remains unchanged."
    expected_new = "info.version3.1.0-rc.1 is candidate metadata only; wire contractVersion v1 remains unchanged."
    normalized = PATHS["candidate_annex"].read_text().replace(expected_new, expected_old)
    result["delta"] = {
        "yaml_byte_identical_candidate01_candidate02": old_yaml.read_bytes() == PATHS["candidate_yaml"].read_bytes(),
        "annex_line_counts_equal": len(old_lines) == len(new_lines),
        "changed_lines": changed,
        "exact_single_line_change": changed == [(192, expected_old, expected_new)],
        "annex_byte_equal_after_reversing_one_token": normalized.encode() == old_annex.read_bytes(),
        "stale_normative_sentence_absent": expected_old not in PATHS["candidate_annex"].read_text(),
        "corrected_normative_sentence_count": PATHS["candidate_annex"].read_text().count(expected_new),
        "unified_diff": "".join(diff),
    }

    prior = json.loads(PATHS["prior_raw"].read_text())
    result["inherited"] = {
        "classification": "INHERITED",
        "same_yaml_sha256": prior["hashes"]["candidate_yaml"]["actual"] == EXPECTED["candidate_yaml"],
        "prior_meta_schema_errors": prior["validator"]["metaSchemaErrors"],
        "prior_semantic_errors": prior["validator"]["semanticErrors"],
        "prior_local_refs": prior["validator"]["localRefs"],
        "prior_schema_bound_examples": prior["validator"]["schemaBoundExamples"],
        "prior_fixtures": prior["fixtures"],
        "prior_mutants": prior["mutantsKilled"],
        "prior_toolchain": prior["validator"],
    }

    checks = [v["match"] for v in result["hashes"].values()]
    checks += [
        result["apply"]["check"]["exit"] == 0,
        result["apply"]["apply"]["exit"] == 0,
        result["apply"]["yaml_byte_equal"],
        result["apply"]["annex_byte_equal"],
        result["apply"]["baseline_v2_annex_preserved"],
        result["delta"]["yaml_byte_identical_candidate01_candidate02"],
        result["delta"]["exact_single_line_change"],
        result["delta"]["annex_byte_equal_after_reversing_one_token"],
        result["delta"]["stale_normative_sentence_absent"],
        result["delta"]["corrected_normative_sentence_count"] == 1,
        result["inherited"]["same_yaml_sha256"],
        not result["inherited"]["prior_meta_schema_errors"],
        not result["inherited"]["prior_semantic_errors"],
    ]
    result["verdict"] = "PASS" if all(checks) else "REWORK"
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["verdict"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
