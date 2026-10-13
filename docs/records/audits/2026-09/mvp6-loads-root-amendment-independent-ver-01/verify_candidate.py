#!/usr/bin/env python3
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

REPO = Path(__file__).resolve().parents[5]
CAND = REPO / "docs/records/audits/2026-09/mvp6-loads-root-acquisition-amendment-candidate-01"
WORK = Path(os.environ["MVP6_LOADS_ROOT_VER_WORK"])
TOOLDEPS = Path(os.environ["MVP6_OAS31_TOOLDEPS"])
sys.path.insert(0, str(TOOLDEPS))

import yaml  # noqa: E402
import jsonschema  # noqa: E402
import openapi_spec_validator  # noqa: E402
from openapi_spec_validator.schemas import openapi_v31_schema_validator  # noqa: E402


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve_pointer(document, pointer):
    value = document
    for token in pointer[2:].split("/"):
        value = value[token.replace("~1", "/").replace("~0", "~")]
    return value


def differences(left, right, path="$", out=None):
    if out is None:
        out = []
    if type(left) is not type(right):
        out.append(path)
    elif isinstance(left, dict):
        for key in sorted(set(left) | set(right)):
            child = f"{path}/{key}"
            if key not in left or key not in right:
                out.append(child)
            else:
                differences(left[key], right[key], child, out)
    elif isinstance(left, list):
        if len(left) != len(right):
            out.append(path + "/length")
        for index, (a, b) in enumerate(zip(left, right)):
            differences(a, b, f"{path}/{index}", out)
    elif left != right:
        out.append(path)
    return out


expected = {
    "baseline_yaml": (REPO / "docs/analysis/contracts/shipment-bundle.openapi.yaml", "5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c"),
    "baseline_annex": (REPO / "docs/analysis/contracts/loads-semantics-v2.0.0.md", "a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1"),
    "candidate_yaml": (CAND / "shipment-bundle-v3.1.0-rc.1.openapi.yaml", "aeb354e8cc5d06f36bc0ee0e4e43b28a8701a2c3bec44200d47cec9095dfc744"),
    "candidate_annex": (CAND / "loads-semantics-v3.1.0-rc.1.md", "620038c8d53af260ef03238db9dddebe74fd368ccbed6e04b47d032a44a15ac1"),
    "patch": (CAND / "amendment.patch", "6a87af9fb006a2583b0f734fe7a374ebdd383a15459c4bd76e0d7a58c2ac568a"),
}

hash_results = {}
for name, (path, wanted) in expected.items():
    actual = digest(path)
    hash_results[name] = {"path": str(path.relative_to(REPO)), "expected": wanted, "actual": actual, "match": actual == wanted}
assert all(item["match"] for item in hash_results.values())

target_contract_dir = WORK / "docs/analysis/contracts"
target_contract_dir.mkdir(parents=True)
shutil.copy2(expected["baseline_yaml"][0], target_contract_dir / "shipment-bundle.openapi.yaml")
shutil.copy2(expected["baseline_annex"][0], target_contract_dir / "loads-semantics-v2.0.0.md")

apply_check = subprocess.run(
    ["git", "apply", "--check", str(expected["patch"][0])],
    cwd=WORK,
    text=True,
    capture_output=True,
)
assert apply_check.returncode == 0, apply_check.stderr
apply_run = subprocess.run(
    ["git", "apply", str(expected["patch"][0])],
    cwd=WORK,
    text=True,
    capture_output=True,
)
assert apply_run.returncode == 0, apply_run.stderr

applied_yaml = target_contract_dir / "shipment-bundle.openapi.yaml"
applied_annex = target_contract_dir / "loads-semantics-v3.1.0-rc.1.md"
byte_equality = {
    "yaml": applied_yaml.read_bytes() == expected["candidate_yaml"][0].read_bytes(),
    "annex": applied_annex.read_bytes() == expected["candidate_annex"][0].read_bytes(),
}
assert all(byte_equality.values())

baseline = yaml.safe_load(expected["baseline_yaml"][0].read_text())
candidate = yaml.safe_load(expected["candidate_yaml"][0].read_text())

meta_errors = [str(error) for error in openapi_v31_schema_validator.iter_errors(candidate)]
semantic_errors = [str(error) for error in openapi_spec_validator.OpenAPIV31SpecValidator(candidate).iter_errors()]
assert not meta_errors
assert not semantic_errors

refs = []
examples = []
resolver = jsonschema.RefResolver.from_schema(candidate)


def traverse(node, path="$"):
    if isinstance(node, dict):
        if "$ref" in node:
            reference = node["$ref"]
            assert reference.startswith("#/")
            resolve_pointer(candidate, reference)
            refs.append({"at": path, "ref": reference})
        if "schema" in node:
            values = []
            if "example" in node:
                values.append(("example", node["example"]))
            for key, value in node.get("examples", {}).items():
                if isinstance(value, dict) and "value" in value:
                    values.append((key, value["value"]))
            for key, value in values:
                jsonschema.Draft202012Validator(
                    node["schema"], resolver=resolver, format_checker=jsonschema.FormatChecker()
                ).validate(value)
                examples.append({"at": path, "name": key})
        for key, value in node.items():
            traverse(value, f"{path}/{key}")
    elif isinstance(node, list):
        for index, value in enumerate(node):
            traverse(value, f"{path}/{index}")


traverse(candidate)

summary_schema = candidate["components"]["schemas"]["LoadSummary"]
summary_validator = jsonschema.Draft202012Validator(
    summary_schema, resolver=resolver, format_checker=jsonschema.FormatChecker()
)
row = copy.deepcopy(candidate["paths"]["/loads"]["get"]["responses"]["200"]["content"]["application/json"]["example"]["items"][0])

fixture_values = {
    "missing": "__MISSING__",
    "null": None,
    "valid": "18500000-0000-0000-0000-0000000000aa",
    "nil": "00000000-0000-0000-0000-000000000000",
    "malformed": "not-a-uuid",
}
fixture_results = {}
for name, value in fixture_values.items():
    item = copy.deepcopy(row)
    if value == "__MISSING__":
        item.pop("lifecycleCorrelationId", None)
    else:
        item["lifecycleCorrelationId"] = value
    errors = [error.message for error in summary_validator.iter_errors(item)]
    expected_valid = name != "malformed"
    fixture_results[name] = {"expectedValid": expected_valid, "errors": errors, "pass": (not errors) == expected_valid}
assert all(item["pass"] for item in fixture_results.values())

diff_paths = differences(baseline, candidate)
allowed_exact = {
    "$/info/version",
    "$/info/x-status",
    "$/paths//loads/get/responses/200/content/application/json/example/items/0/lifecycleCorrelationId",
    "$/paths//loads/get/x-loads-semantics",
    "$/paths//loads/get/description",
    "$/paths//loads/post/x-loads-semantics",
    "$/paths//loads/post/description",
    "$/paths//loads/{loadId}/transition/post/x-loads-semantics",
    "$/paths//loads/{loadId}/transition/post/description",
    "$/components/schemas/LoadSummary/properties/lifecycleCorrelationId",
}
unexpected_diff_paths = sorted(set(diff_paths) - allowed_exact)
missing_expected_diff_paths = sorted(allowed_exact - set(diff_paths))
assert not unexpected_diff_paths, unexpected_diff_paths
assert not missing_expected_diff_paths, missing_expected_diff_paths

assert set(candidate["paths"]) == set(baseline["paths"])
non_load_paths = [key for key in baseline["paths"] if key not in {"/loads", "/loads/{loadId}/transition"}]
assert all(candidate["paths"][key] == baseline["paths"][key] for key in non_load_paths)
assert all(
    candidate["components"]["schemas"][key] == baseline["components"]["schemas"][key]
    for key in baseline["components"]["schemas"]
    if key != "LoadSummary"
)
baseline_summary = copy.deepcopy(baseline["components"]["schemas"]["LoadSummary"])
candidate_summary = copy.deepcopy(candidate["components"]["schemas"]["LoadSummary"])
added_root_schema = candidate_summary["properties"].pop("lifecycleCorrelationId")
assert candidate_summary == baseline_summary
assert added_root_schema["type"] == ["string", "null"]
assert added_root_schema["format"] == "uuid"
assert "lifecycleCorrelationId" not in candidate["components"]["schemas"]["LoadSummary"].get("required", [])

annex = expected["candidate_annex"][0].read_text()
annex_checks = {
    "authority": "only authoritative source is the persisted `LoadPlan.CorrelationRoot`" in annex,
    "emit_when_present": "emits the stored non-null UUID when that persisted field is present" in annex,
    "no_derivation": "It does not derive the value from the current GET trace" in annex,
    "no_backfill": "Reads do not create, replace, normalize, or backfill the field" in annex,
    "nil_preserved": "A genuinely stored nil UUID remains a present UUID value" in annex,
    "fail_closed": "missing, null, malformed, or otherwise non-authoritative value keeps transition unavailable" in annex,
    "read_permission": "`supplychain.loads.read` permission protect disclosure" in annex,
    "transition_permission": "`supplychain.loads.transition`" in annex,
    "replay_preserved": "root-before-fingerprint, replay, lifecycle, error, audit and Pending-outbox rules remain unchanged" in annex,
    "multi_shipment_deferred": "does not compare, merge, select or constrain Shipment roots" in annex,
    "no_detail_lookup": "No Loads detail/search operation, searchable lookup" in annex,
}
assert all(annex_checks.values())

stale_version_claims = []
for number, line in enumerate(annex.splitlines(), 1):
    if "info.version2.0.0 is candidate metadata only" in line:
        stale_version_claims.append({"line": number, "text": line})

mutants = {}

required_mutant = copy.deepcopy(summary_schema)
required_mutant.setdefault("required", []).append("lifecycleCorrelationId")
missing_row = copy.deepcopy(row)
missing_row.pop("lifecycleCorrelationId", None)
mutants["required_presence"] = bool(list(jsonschema.Draft202012Validator(required_mutant, resolver=resolver).iter_errors(missing_row)))

nonnullable_mutant = copy.deepcopy(summary_schema)
nonnullable_mutant["properties"]["lifecycleCorrelationId"]["type"] = "string"
null_row = copy.deepcopy(row)
null_row["lifecycleCorrelationId"] = None
mutants["non_nullable"] = bool(list(jsonschema.Draft202012Validator(nonnullable_mutant, resolver=resolver).iter_errors(null_row)))

no_format_mutant = copy.deepcopy(summary_schema)
no_format_mutant["properties"]["lifecycleCorrelationId"].pop("format")
bad_row = copy.deepcopy(row)
bad_row["lifecycleCorrelationId"] = "not-a-uuid"
mutants["missing_uuid_format"] = not bool(list(jsonschema.Draft202012Validator(no_format_mutant, resolver=resolver, format_checker=jsonschema.FormatChecker()).iter_errors(bad_row)))

component_mutant = copy.deepcopy(candidate)
component_mutant["components"]["schemas"]["LoadResponse"]["properties"]["unexpected"] = {"type": "string"}
mutants["load_response_changed"] = component_mutant["components"]["schemas"]["LoadResponse"] != baseline["components"]["schemas"]["LoadResponse"]

non_load_mutant = copy.deepcopy(candidate)
non_load_key = non_load_paths[0]
non_load_mutant["paths"][non_load_key]["x-ver-mutant"] = True
mutants["non_load_path_changed"] = non_load_mutant["paths"][non_load_key] != baseline["paths"][non_load_key]

authority_mutant = annex.replace("persisted `LoadPlan.CorrelationRoot`", "current GET trace", 1)
mutants["authority_changed"] = "only authoritative source is the persisted `LoadPlan.CorrelationRoot`" not in authority_mutant
assert all(mutants.values())

bad_version = copy.deepcopy(candidate)
bad_version["openapi"] = "bad"
missing_title = copy.deepcopy(candidate)
missing_title["info"].pop("title")
negative_controls = {
    "bad_openapi_version_meta_errors": len(list(openapi_v31_schema_validator.iter_errors(bad_version))),
    "bad_openapi_version_semantic_errors": len(list(openapi_spec_validator.OpenAPIV31SpecValidator(bad_version).iter_errors())),
    "missing_info_title_meta_errors": len(list(openapi_v31_schema_validator.iter_errors(missing_title))),
    "missing_info_title_semantic_errors": len(list(openapi_spec_validator.OpenAPIV31SpecValidator(missing_title).iter_errors())),
}
assert all(value > 0 for value in negative_controls.values())

schema_path = TOOLDEPS / "openapi_spec_validator/resources/schemas/v3.1/schema.json"
result = {
    "verdict": "REWORK" if stale_version_claims else "PASS",
    "hashes": hash_results,
    "patch": {
        "checkExit": apply_check.returncode,
        "applyExit": apply_run.returncode,
        "checkStdout": apply_check.stdout,
        "checkStderr": apply_check.stderr,
        "applyStdout": apply_run.stdout,
        "applyStderr": apply_run.stderr,
        "byteEquality": byte_equality,
        "appliedYamlSha256": digest(applied_yaml),
        "appliedAnnexSha256": digest(applied_annex),
    },
    "validator": {
        "python": sys.version,
        "openapiSpecValidator": openapi_spec_validator.__version__,
        "jsonschema": jsonschema.__version__ if hasattr(jsonschema, "__version__") else "unknown",
        "pyyaml": yaml.__version__,
        "schemaPath": str(schema_path),
        "schemaSha256": digest(schema_path),
        "metaSchemaErrors": meta_errors,
        "semanticErrors": semantic_errors,
        "localRefs": len(refs),
        "schemaBoundExamples": len(examples),
        "negativeControls": negative_controls,
    },
    "scope": {
        "diffPaths": diff_paths,
        "unexpectedDiffPaths": unexpected_diff_paths,
        "missingExpectedDiffPaths": missing_expected_diff_paths,
        "nonLoadsPathsUnchanged": len(non_load_paths),
        "allSchemasExceptLoadSummaryUnchanged": True,
        "loadSummaryOnlyAddsRoot": True,
    },
    "fixtures": fixture_results,
    "annexChecks": annex_checks,
    "mutantsKilled": mutants,
    "findings": {
        "staleVersionClaims": stale_version_claims,
        "compatibility": "Optional nullable response property is additive only for tolerant consumers; strict unknown-property parsers require inventory and exact-hash consent.",
        "runtimeBoundary": "Static schema/annex verification does not prove producer emission, persistence provenance, consumer uptake, HTTP/JWT/Mongo behavior, publication, or rollout.",
    },
}

(WORK / "raw-results.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n")
(WORK / "refs.json").write_text(json.dumps(refs, indent=2) + "\n")
(WORK / "examples.json").write_text(json.dumps(examples, indent=2) + "\n")
print(json.dumps(result, indent=2, ensure_ascii=False))
