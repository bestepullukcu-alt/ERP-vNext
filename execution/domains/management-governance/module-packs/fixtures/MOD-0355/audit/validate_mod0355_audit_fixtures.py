#!/usr/bin/env python3
"""Executable, dependency-free validation for the MOD-0355 NON-RUNTIME audit fixtures."""

from __future__ import annotations

import base64
import hashlib
import hmac
import json
import re
import subprocess
import sys
import uuid
from copy import deepcopy
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PREFIX = "process-modeling-audit-intent-submitted-v1"
HEX64 = re.compile(r"^[0-9a-f]{64}$")
UTC7 = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$")


def fail(message: str) -> None:
    raise AssertionError(message)


def load(name: str):
    return json.loads((ROOT / name).read_text(encoding="utf-8"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_manifest() -> None:
    manifest = load(f"{PREFIX}.fixture-manifest.json")
    assert manifest["status"] == "NON-RUNTIME"
    assert manifest["productionAuthority"] == "none"
    assert manifest["mod0021Admission"] == "deferred-until-immutable-producer-checkpoint"
    for name, expected in manifest["files"].items():
        assert HEX64.fullmatch(expected), name
        assert digest(ROOT / name) == expected, name


def validate_payload() -> tuple[dict, bytes]:
    path = ROOT / f"{PREFIX}.canonical.json"
    raw = path.read_bytes()
    assert len(raw) == 258
    assert not raw.startswith(b"\xef\xbb\xbf")
    assert not raw.endswith((b"\n", b"\r"))
    payload = json.loads(raw)
    keys = ["auditIntentId", "actorId", "entityType", "entityId", "mutation", "occurredAtUtc"]
    assert list(payload) == keys
    assert json.dumps(payload, separators=(",", ":"), ensure_ascii=False).encode() == raw
    for field in ("auditIntentId", "actorId", "entityId"):
        uuid.UUID(payload[field])
    assert UTC7.fullmatch(payload["occurredAtUtc"])
    sha = hashlib.sha256(raw).hexdigest()
    assert sha == "7ee7d2d6b19d832d71dbe89967b0964ce5932b872b8d2528fb2cf14918500f1a"
    checksum = (ROOT / f"{PREFIX}.canonical.sha256").read_bytes()
    assert checksum == (sha + "\n").encode()
    return payload, raw


def expected_pairs() -> set[tuple[str, str]]:
    catalogs = {"ProcessArchitecture", "ProcessDomain", "ProcessFamily", "ProcessDefinition"}
    pairs = {(entity, mutation) for entity in catalogs for mutation in ("Created", "Updated", "Archived")}
    pairs |= {("ProcessModel", "Created"), ("ProcessModel", "Updated")}
    pairs |= {("ProcessModelVersion", mutation) for mutation in (
        "DraftUpdated", "ReviewRequested", "ReturnedToDraft", "Published", "Retired", "RevisionCreated"
    )}
    return pairs


def apply_patch(document: dict, operations: list[dict]) -> dict:
    result = deepcopy(document)
    for operation in operations:
        parts = operation["path"].strip("/").split("/")
        target = result
        for part in parts[:-1]:
            target = target[part]
        leaf = parts[-1]
        if operation["op"] == "remove":
            target.pop(leaf, None)
        elif operation["op"] in {"add", "replace"}:
            target[leaf] = operation["value"]
        else:
            fail(f"unsupported patch operation: {operation['op']}")
    return result


def payload_stage(payload: dict) -> str:
    exact = ["auditIntentId", "actorId", "entityType", "entityId", "mutation", "occurredAtUtc"]
    if list(payload) != exact:
        return "schema"
    try:
        for field in ("auditIntentId", "actorId", "entityId"):
            uuid.UUID(payload[field])
    except (ValueError, TypeError, AttributeError):
        return "schema"
    if not isinstance(payload["occurredAtUtc"], str) or not UTC7.fullmatch(payload["occurredAtUtc"]):
        return "schema"
    if (payload["entityType"], payload["mutation"]) not in expected_pairs():
        return "allowlist"
    return "accepted"


def envelope_stage(envelope: dict, raw: bytes) -> str:
    exact = {
        "eventName": "management-governance.process-modeling-audit-intent.submitted.v1",
        "eventType": "ProcessModelingAuditIntentSubmittedV1",
        "producer": "Diten.ManagementGovernanceService",
        "moduleCode": "MOD-0355",
    }
    if type(envelope.get("eventVersion")) is not int or envelope["eventVersion"] != 1:
        return "envelope"
    if any(envelope.get(field) != value for field, value in exact.items()):
        return "envelope"
    if envelope.get("payloadSha256") != hashlib.sha256(raw).hexdigest():
        return "envelope"
    return "accepted"


def validate_schema_and_matrix(payload: dict) -> None:
    schema = load(f"{PREFIX}.schema.json")
    envelope = load(f"{PREFIX}.envelope.json")
    matrix = load(f"{PREFIX}.allowlist-matrix.json")
    expected = expected_pairs()
    entities = set(schema["properties"]["entityType"]["enum"])
    mutations = set(schema["properties"]["mutation"]["enum"])
    assert schema["$id"] == "urn:diten:management-governance:process-modeling-audit-intent-submitted:v1"
    assert schema["x-producer"] == envelope["producer"] == "Diten.ManagementGovernanceService"
    assert schema["x-module-code"] == envelope["moduleCode"] == "MOD-0355"
    assert schema["x-event-name"] == envelope["eventName"] == "management-governance.process-modeling-audit-intent.submitted.v1"
    assert schema["x-event-type"] == envelope["eventType"] == "ProcessModelingAuditIntentSubmittedV1"
    assert type(schema["x-event-version"]) is int and schema["x-event-version"] == 1
    assert type(envelope["eventVersion"]) is int and envelope["eventVersion"] == 1
    assert schema["x-audit-category"] == "AuditCategory.Integration"
    assert schema["x-signature-algorithm"] == "HmacSha256"
    one_of = schema["allOf"][0]["oneOf"]
    schema_pairs = {(item["properties"]["entityType"]["const"], item["properties"]["mutation"]["const"]) for item in one_of}
    positive = {tuple(item) for item in matrix["positive"]}
    invalid = {tuple(item) for item in matrix["invalidCrossPairs"]}
    projection = matrix["projection"]
    cartesian = {(entity, mutation) for entity in entities for mutation in mutations}
    assert len(expected) == len(schema_pairs) == len(positive) == 20
    assert expected == schema_pairs == positive
    assert len(invalid) == 34 and invalid == cartesian - expected
    assert positive.isdisjoint(invalid)
    for entity, mutation in positive:
        candidate = dict(payload, entityType=entity, mutation=mutation)
        assert payload_stage(candidate) == "accepted", (entity, mutation)
        assert projection["operationByMutation"][mutation].startswith("AuditOperation.")
    for entity, mutation in invalid:
        candidate = dict(payload, entityType=entity, mutation=mutation)
        assert payload_stage(candidate) == "allowlist", (entity, mutation)
    assert matrix["invalidDisposition"] == {"terminal": True, "retry": False, "auditEvent": False}
    assert projection["sourceService"] == "Diten.ManagementGovernanceService"
    assert projection["sourceModule"] == "MOD-0355"
    assert projection["category"] == "AuditCategory.Integration"
    expected_event = load(f"{PREFIX}.expected-audit-event.json")
    assert expected_event["entityType"] == projection["entityTypePrefix"] + payload["entityType"]
    assert expected_event["operation"] == projection["operationByMutation"][payload["mutation"]]
    assert expected_event["sourceService"] == projection["sourceService"]
    assert expected_event["sourceModule"] == projection["sourceModule"]
    assert expected_event["category"] == projection["category"]
    negatives = load(f"{PREFIX}.contract-negative-matrix.json")
    assert len(negatives["cases"]) == 21
    assert len({case["id"] for case in negatives["cases"]}) == 21
    assert negatives["disposition"] == {"terminal": True, "retry": False, "auditEvent": False}
    for case in negatives["cases"]:
        if "patch" in case:
            actual = payload_stage(apply_patch(payload, case["patch"]))
        else:
            actual = envelope_stage(apply_patch(envelope, case["envelopePatch"]), (ROOT / f"{PREFIX}.canonical.json").read_bytes())
        assert actual == case["stage"], (case["id"], actual)


def validate_signing(raw: bytes) -> None:
    vector = load(f"{PREFIX}.signing-vector.test-only.json")
    assert vector["algorithm"] == "HmacSha256"
    assert vector["scheme"] == "management-governance-event-hmac-sha256.v1"
    assert vector["fixtureOnly"] is True and vector["productionAllowed"] is False
    assert vector["payloadByteCount"] == len(raw) == 258
    fields = [
        vector["scheme"], vector["eventId"], vector["eventName"], str(vector["eventVersion"]),
        vector["tenantId"], vector["correlationId"], vector["producer"], vector["causationId"] or "-",
        vector["occurredAtUtc"], str(len(raw)),
    ]
    signing_input = ("\n".join(fields) + "\n").encode() + raw
    signature = hmac.new(base64.b64decode(vector["keyBase64"]), signing_input, hashlib.sha256).hexdigest()
    assert len(signing_input) == vector["signingInputByteCount"] == 548
    assert hashlib.sha256(signing_input).hexdigest() == vector["signingInputSha256"]
    assert signature == vector["expectedHmacSha256"]
    assert (ROOT / f"{PREFIX}.expected-signature.test-only.txt").read_text() == signature + "\n"


def validate_provenance_and_production() -> None:
    delegated = load(f"{PREFIX}.delegated-provenance.positive.json")
    delegated_neg = load(f"{PREFIX}.delegated-provenance.negative.json")
    nondelegated = load(f"{PREFIX}.non-delegated-provenance.positive.json")
    production = load(f"{PREFIX}.production-validator-rejection.json")
    expected_operation = "process-modeling.architectures.create.v1"
    expected_permission = "management-governance.process-modeling.architectures.create"
    assert delegated["trustedTransportProvenance"]["operationId"] == expected_operation
    assert delegated["trustedTransportProvenance"]["permissionKey"] == expected_permission
    assert delegated["producerPayloadContainsTrustedProvenance"] is False
    assert delegated["expected"] == {"accepted": True, "auditEvent": True}
    assert len(delegated_neg["cases"]) == 10
    assert len({case["id"] for case in delegated_neg["cases"]}) == 10
    assert delegated_neg["disposition"] == {"terminal": True, "retry": False, "auditEvent": False}
    def delegated_accepts(candidate: dict) -> bool:
        trusted = candidate.get("trustedTransportProvenance")
        if not isinstance(trusted, dict):
            return False
        required = {"validatedTokenFamily", "tenantId", "servicePrincipalId", "delegatedActorId", "operationId", "permissionKey", "requestHash", "proofValidated", "proofExpiresAtUtc"}
        return (
            required <= trusted.keys()
            and trusted["validatedTokenFamily"] == "diten-delegated-actor-proof+jwt"
            and trusted["tenantId"] == "35000355-0000-4000-8000-000000000004"
            and trusted["delegatedActorId"] == "35000355-0000-4000-8000-000000000002"
            and trusted["operationId"] == expected_operation
            and trusted["permissionKey"] == expected_permission
            and trusted["requestHash"] == "b5f27ecad3c18be1d35c41f36a595c106b1775b6a11fd1c9e66680e867979301"
            and trusted["proofValidated"] is True
            and trusted["proofExpiresAtUtc"] > "2026-08-26T12:34:56.0000000Z"
        )
    assert delegated_accepts(delegated)
    for case in delegated_neg["cases"]:
        assert not delegated_accepts(apply_patch(delegated, case["patch"])), case["id"]
    assert nondelegated["structurallyAbsent"] == ["delegatedActorId", "delegationId", "delegatedActorProof"]
    assert nondelegated["producerPayloadContainsTrustedProvenance"] is False
    assert nondelegated["delegatedProfileWithSameAbsence"]["auditEvent"] is False
    assert production["productionKeyProvisioned"] is False
    assert production["productionKeyIdProvisioned"] is False
    assert len(production["cases"]) == 7
    assert {(case["id"], case.get("slot")) for case in production["cases"]} >= {
        ("test-identity-active-slot", "active"), ("test-identity-previous-slot", "previous"),
        ("test-key-id-active-slot", "active"), ("test-key-id-previous-slot", "previous"),
    }
    assert production["disposition"] == {"terminal": True, "retry": False, "auditEvent": False}
    for case in production["cases"]:
        assert (
            case.get("identity", production["reservedProductionSigningIdentity"]) != production["reservedProductionSigningIdentity"]
            or case.get("keyId") == production["testOnlyKeyId"]
            or case.get("signatureSource") == "test-only-vector"
            or case.get("scheme") != "management-governance-event-hmac-sha256.v1"
        ), case["id"]


def validate_non_reuse() -> None:
    vector = load(f"{PREFIX}.signing-vector.test-only.json")
    unique_values = [vector["signingIdentity"], vector["keyId"], vector["keyBase64"], vector["expectedHmacSha256"]]
    commits = [
        "9968ecede48822f95a74461a4959c94b23abbc9b",
        "711962a3fdc1226d947672dc9b48d29296c960a0",
        "3df680d6e006bfce19e382253ddd1f2f873c2295",
        "b4589139e8c9db544de5b66300640b214db3acf4",
        "c954984d00ad43bc3127bbd0ada09da2b2721768",
    ]
    for commit in commits:
        for value in unique_values:
            result = subprocess.run(
                ["git", "grep", "-F", "--quiet", value, commit, "--", "execution", "services"],
                cwd=ROOT, check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            )
            assert result.returncode == 1, f"test vector reuse in {commit}: {value}"


def main() -> int:
    validate_manifest()
    payload, raw = validate_payload()
    validate_schema_and_matrix(payload)
    validate_signing(raw)
    validate_provenance_and_production()
    validate_non_reuse()
    print("PASS MOD-0355 audit fixtures: 258 bytes; 20 positive; 34 cross-pair; 21 contract-negative; 10 delegated-negative; 7 production-reject")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:
        print(f"FAIL MOD-0355 audit fixtures: {exc}", file=sys.stderr)
        raise
