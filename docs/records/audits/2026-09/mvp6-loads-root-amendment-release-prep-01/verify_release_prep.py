#!/usr/bin/env python3
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

REPO = Path(__file__).resolve().parents[5]
PACK = Path(__file__).resolve().parent
CAND = REPO / "docs/records/audits/2026-09/mvp6-loads-root-acquisition-amendment-candidate-02"
WORK = Path(os.environ["MVP6_LOADS_RELEASE_WORK"])
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
    "candidate_annex": (CAND / "loads-semantics-v3.1.0-rc.1.md", "e2107301e3c6cfae5e6768de77c19f5b4fa5ea7ff42f1a4023650bd3fb7750cd"),
    "candidate_patch": (CAND / "amendment.patch", "27cc3a8f6c5000b9d06b80b2b0037e84750cd7bc40611da4b5f6fa33582e11cc"),
    "rever_manifest": (REPO / "docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-rever-01/ARTIFACTS.sha256", "6b0990b90b145f8140d3d4e2805b05cfc43381d7160451b84374ef8db0ed2f61"),
    "final_yaml": (PACK / "artifacts/shipment-bundle-v3.1.0-final-proposed.openapi.yaml", None),
    "final_annex": (PACK / "artifacts/loads-semantics-v3.1.0.md", None),
    "publication_patch": (PACK / "publication.patch", None),
}

hashes = {}
for name, (path, wanted) in expected.items():
    actual = digest(path)
    hashes[name] = {"path": str(path.relative_to(REPO)), "expected": wanted, "actual": actual, "match": wanted is None or actual == wanted}
assert all(item["match"] for item in hashes.values())

target = WORK / "docs/analysis/contracts"
target.mkdir(parents=True)
shutil.copy2(expected["baseline_yaml"][0], target / "shipment-bundle.openapi.yaml")
shutil.copy2(expected["baseline_annex"][0], target / "loads-semantics-v2.0.0.md")

check = subprocess.run(["git", "apply", "--check", str(expected["publication_patch"][0])], cwd=WORK, text=True, capture_output=True)
assert check.returncode == 0, check.stderr
apply = subprocess.run(["git", "apply", str(expected["publication_patch"][0])], cwd=WORK, text=True, capture_output=True)
assert apply.returncode == 0, apply.stderr

applied_yaml = target / "shipment-bundle.openapi.yaml"
applied_annex = target / "loads-semantics-v3.1.0.md"
byte_equality = {
    "yaml": applied_yaml.read_bytes() == expected["final_yaml"][0].read_bytes(),
    "annex": applied_annex.read_bytes() == expected["final_annex"][0].read_bytes(),
    "v2_annex_preserved": digest(target / "loads-semantics-v2.0.0.md") == expected["baseline_annex"][1],
}
assert all(byte_equality.values())

baseline = yaml.safe_load(expected["baseline_yaml"][0].read_text())
candidate = yaml.safe_load(expected["candidate_yaml"][0].read_text())
final = yaml.safe_load(expected["final_yaml"][0].read_text())

meta_errors = [str(error) for error in openapi_v31_schema_validator.iter_errors(final)]
semantic_errors = [str(error) for error in openapi_spec_validator.OpenAPIV31SpecValidator(final).iter_errors()]
assert not meta_errors
assert not semantic_errors

refs = []
examples = []
resolver = jsonschema.RefResolver.from_schema(final)


def traverse(node, path="$"):
    if isinstance(node, dict):
        if "$ref" in node:
            reference = node["$ref"]
            assert reference.startswith("#/")
            resolve_pointer(final, reference)
            refs.append({"at": path, "ref": reference})
        if "schema" in node:
            values = []
            if "example" in node:
                values.append(("example", node["example"]))
            for key, value in node.get("examples", {}).items():
                if isinstance(value, dict) and "value" in value:
                    values.append((key, value["value"]))
            for key, value in values:
                jsonschema.Draft202012Validator(node["schema"], resolver=resolver, format_checker=jsonschema.FormatChecker()).validate(value)
                examples.append({"at": path, "name": key})
        for key, value in node.items():
            traverse(value, f"{path}/{key}")
    elif isinstance(node, list):
        for index, value in enumerate(node):
            traverse(value, f"{path}/{index}")


traverse(final)

summary_schema = final["components"]["schemas"]["LoadSummary"]
validator = jsonschema.Draft202012Validator(summary_schema, resolver=resolver, format_checker=jsonschema.FormatChecker())
row = copy.deepcopy(final["paths"]["/loads"]["get"]["responses"]["200"]["content"]["application/json"]["example"]["items"][0])
fixtures = {}
for name, value in {"missing": "__MISSING__", "null": None, "valid": "18500000-0000-0000-0000-0000000000aa", "nil": "00000000-0000-0000-0000-000000000000", "malformed": "not-a-uuid"}.items():
    item = copy.deepcopy(row)
    if value == "__MISSING__":
        item.pop("lifecycleCorrelationId", None)
    else:
        item["lifecycleCorrelationId"] = value
    errors = [e.message for e in validator.iter_errors(item)]
    fixtures[name] = {"errors": errors, "pass": (not errors) == (name != "malformed")}
assert all(value["pass"] for value in fixtures.values())

candidate_final_diffs = differences(candidate, final)
allowed_candidate_final = {
    "$/info/version",
    "$/info/x-status",
    "$/paths//loads/get/x-loads-semantics",
    "$/paths//loads/get/description",
    "$/paths//loads/post/x-loads-semantics",
    "$/paths//loads/post/description",
    "$/paths//loads/{loadId}/transition/post/x-loads-semantics",
    "$/paths//loads/{loadId}/transition/post/description",
}
assert set(candidate_final_diffs) == allowed_candidate_final, candidate_final_diffs

candidate_business = copy.deepcopy(candidate)
final_business = copy.deepcopy(final)
candidate_business["info"]["version"] = final_business["info"]["version"]
candidate_business["info"]["x-status"] = final_business["info"]["x-status"]
for operation in [
    candidate_business["paths"]["/loads"]["get"],
    candidate_business["paths"]["/loads"]["post"],
    candidate_business["paths"]["/loads/{loadId}/transition"]["post"],
]:
    operation.pop("x-loads-semantics", None)
    operation.pop("description", None)
for operation in [
    final_business["paths"]["/loads"]["get"],
    final_business["paths"]["/loads"]["post"],
    final_business["paths"]["/loads/{loadId}/transition"]["post"],
]:
    operation.pop("x-loads-semantics", None)
    operation.pop("description", None)
assert candidate_business == final_business

annex = expected["final_annex"][0].read_text()
annex_checks = {
    "not_published": "**NOT PUBLISHED.**" in annex,
    "final_proposed": "3.1.0 final-proposed candidate" in annex,
    "normative_version": "info.version3.1.0 is proposed candidate metadata only" in annex,
    "wire_v1": "wire contractVersion v1 remains unchanged" in annex,
    "no_migration_inference": "Metadata major does not supply route negotiation, migration, cutover or rollback" in annex,
    "authority": "only authoritative source is the persisted `LoadPlan.CorrelationRoot`" in annex,
}
assert all(annex_checks.values())
assert "3.1.0-rc.1" not in expected["final_yaml"][0].read_text()
assert "3.1.0-rc.1" not in annex
assert final["info"]["version"] == "3.1.0"
assert final["info"]["x-status"] == "FROZEN"

result = {
    "verdict": "PASS_FINAL_PROPOSED_TECHNICAL",
    "hashes": hashes,
    "apply": {"checkExit": check.returncode, "applyExit": apply.returncode, "byteEquality": byte_equality, "stderr": check.stderr + apply.stderr},
    "validator": {
        "python": sys.version,
        "openapiSpecValidator": openapi_spec_validator.__version__,
        "pyyaml": yaml.__version__,
        "schemaPath": str(TOOLDEPS / "openapi_spec_validator/resources/schemas/v3.1/schema.json"),
        "schemaSha256": digest(TOOLDEPS / "openapi_spec_validator/resources/schemas/v3.1/schema.json"),
        "metaSchemaErrors": meta_errors,
        "semanticErrors": semantic_errors,
        "localRefs": len(refs),
        "schemaBoundExamples": len(examples),
    },
    "candidateToFinal": {"diffPaths": candidate_final_diffs, "businessSemanticsEqual": True},
    "fixtures": fixtures,
    "annexChecks": annex_checks,
    "boundary": "Technical validation only; final version selection, exact consumer consent, publication, and runtime uptake remain separate owner gates.",
}
(PACK / "raw/validation-results.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n")
(PACK / "raw/refs.json").write_text(json.dumps(refs, indent=2) + "\n")
(PACK / "raw/examples.json").write_text(json.dumps(examples, indent=2) + "\n")
print(json.dumps(result, indent=2, ensure_ascii=False))
