#!/usr/bin/env python3
"""Generate or verify durable Gate I-A mutation evidence without retaining mutants."""
from __future__ import annotations
import datetime as dt
import base64
import hashlib
import json
import pathlib
import subprocess
import sys
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[6]
SOURCE_REL = "services/Diten.PpmService/src/Diten.PpmService.Application/Features/InvestmentCases/GateI/DecisionTrace/DecisionTraceValidation.cs"
SOURCE = ROOT / SOURCE_REL
PROJECT = "services/Diten.PpmService/tests/Diten.PpmService.Tests/Diten.PpmService.Tests.csproj"
MANIFEST = pathlib.Path(__file__).with_name("decision-trace-mutation-evidence.json")

MUTANTS = [
    ("request-binding-fixed-time-removed", "if (!DecisionTraceRequestBinding.FixedTimeMatches(context.RequestHash, expectedHash))", "if (false && !DecisionTraceRequestBinding.FixedTimeMatches(context.RequestHash, expectedHash))", "Request_binding_mismatch_is_401_before_provider"),
    ("closed-mode-guard-removed", "request.Mode is not (DecisionTraceValidationMode.HistoricalResolve or DecisionTraceValidationMode.NewReferenceEligibility)", "false", "Closed_mode_guard_returns_400_before_provider"),
    ("mod-0007-owner-guard-removed", "!Exact(value.OwnerModule, DecisionTraceProducerProfile.OwnerModule) || ", "", "Exact_identity_and_authority_guards_fail_before_provider"),
    ("entitlement-explicit-grant-guard-removed", " || value.EntitlementState != TrustedAuthorityState.Current || value.ExplicitTenantGrantState != TrustedAuthorityState.Current", "", "Entitlement_and_explicit_grant_are_both_required"),
    ("reference-equality-no-copy-guard-removed", " || result.Reference != request.Reference.DecisionRevisionReference", "", "Exact_resolved_reference_is_contract_valid_but_non_runtime_fence_returns_503"),
    ("non-runtime-503-changed-to-200", "if (mapped.IsSuccess && DecisionTraceReadOnlyContract.NonRuntimeContractOnly) return new(503,", "if (mapped.IsSuccess && DecisionTraceReadOnlyContract.NonRuntimeContractOnly) return new(200,", "Exact_resolved_reference_is_contract_valid_but_non_runtime_fence_returns_503"),
]

def sha(data: bytes) -> str: return hashlib.sha256(data).hexdigest()
def run(args: list[str]) -> subprocess.CompletedProcess[bytes]: return subprocess.run(args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

def generate() -> int:
    original = SOURCE.read_bytes(); original_hash = sha(original); text = original.decode("utf-8"); rows = []
    try:
        for identity, before, after, test in MUTANTS:
            if text.count(before) != 1: raise RuntimeError(f"{identity}: exact mutation anchor count was {text.count(before)}")
            run_id = str(uuid.uuid4()); SOURCE.write_text(text.replace(before, after, 1), encoding="utf-8")
            compile_run = run(["dotnet", "build", PROJECT, "--no-restore", "-m:1", "--nologo"])
            target_run = run(["dotnet", "test", PROJECT, "--no-build", "--no-restore", "-m:1", "--filter", f"FullyQualifiedName~{test}"])
            output = compile_run.stdout + b"\n--- TARGETED TEST ---\n" + target_run.stdout
            binding = f"Diten.PpmService.Tests.GateI.DecisionTrace.DecisionTraceContractTests.{test}"
            rows.append({"sourcePath": SOURCE_REL, "mutantIdentity": identity, "uniqueRunId": run_id, "compileExit": compile_run.returncode, "targetedExit": target_run.returncode, "targetedTestIdentity": test, "expectedFailureIdentity": binding, "expectedFailureText": test, "rawMutationOutputBase64": base64.b64encode(output).decode("ascii"), "rawOutputSha256": sha(output), "restoredSourceSha256": original_hash, "compiledTestBinding": binding})
            SOURCE.write_bytes(original)
            if compile_run.returncode != 0 or target_run.returncode == 0: raise RuntimeError(f"{identity}: compile={compile_run.returncode}, targeted={target_run.returncode}")
    finally:
        SOURCE.write_bytes(original)
    evidence = {"schemaVersion": 1, "generatedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(), "sourcePath": SOURCE_REL, "restoredSourceSha256": original_hash, "compiledTestAssembly": "Diten.PpmService.Tests", "mutants": rows}
    MANIFEST.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    return verify()

def verify() -> int:
    evidence = json.loads(MANIFEST.read_text(encoding="utf-8")); actual = sha(SOURCE.read_bytes())
    assert evidence["restoredSourceSha256"] == actual
    assert len(evidence["mutants"]) == 6
    listed = run(["dotnet", "test", PROJECT, "--no-build", "--no-restore", "-m:1", "--list-tests"])
    assert listed.returncode == 0, listed.stdout.decode("utf-8", errors="replace")
    listed_text = listed.stdout.decode("utf-8", errors="replace")
    run_ids: set[uuid.UUID] = set()
    for row in evidence["mutants"]:
        assert row["sourcePath"] == SOURCE_REL and row["compileExit"] == 0 and row["targetedExit"] != 0
        parsed_id = uuid.UUID(row["uniqueRunId"]); assert parsed_id.version == 4 and parsed_id not in run_ids; run_ids.add(parsed_id)
        raw = base64.b64decode(row["rawMutationOutputBase64"], validate=True); raw_text = raw.decode("utf-8", errors="replace")
        assert sha(raw) == row["rawOutputSha256"]
        assert row["expectedFailureIdentity"] in raw_text and row["expectedFailureText"] in raw_text
        assert row["restoredSourceSha256"] == actual
        assert row["compiledTestBinding"] in listed_text
    print(f"PASS: 6 killed mutants; restored source sha256={actual}")
    return 0

if __name__ == "__main__":
    raise SystemExit(generate() if "--generate" in sys.argv else verify())
