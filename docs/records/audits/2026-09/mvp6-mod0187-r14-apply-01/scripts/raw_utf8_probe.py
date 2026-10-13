#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import hashlib
import hmac
import json
import os
import socket
import time
import uuid
from pathlib import Path


def b64(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


def token(tenant: str, legal_entity: str, actor: str) -> str:
    header = b64(b'{"alg":"HS256","typ":"JWT"}')
    payload = b64(json.dumps({
        "sub": actor,
        "tenant_id": tenant,
        "legal_entity_id": legal_entity,
        "actor_type": "tenant_user",
        "permission": [
            "supplychain.claims.create",
            "supplychain.claims.read",
            "supplychain.shipments.read"
        ],
        "iss": os.environ["R14_JWT_ISSUER"],
        "aud": os.environ["R14_JWT_AUDIENCE"],
        "exp": int(time.time()) + 600
    }, separators=(",", ":")).encode("utf-8"))
    signature = b64(hmac.new(
        os.environ["R14_JWT_SECRET"].encode("utf-8"),
        (header + "." + payload).encode("ascii"),
        hashlib.sha256
    ).digest())
    return header + "." + payload + "." + signature


def decode_chunked(body: bytes) -> bytes:
    output = bytearray()
    cursor = 0
    while True:
        end = body.find(b"\r\n", cursor)
        size = int(body[cursor:end].split(b";", 1)[0], 16)
        cursor = end + 2
        if size == 0:
            return bytes(output)
        output.extend(body[cursor:cursor + size])
        cursor += size + 2


def send(port: int, key_lines: list[bytes] | None, extra_lines: list[bytes] | None = None):
    tenant = str(uuid.UUID("11111111-1111-4111-8111-111111111111"))
    legal_entity = str(uuid.UUID("22222222-2222-4222-8222-222222222222"))
    actor = str(uuid.UUID("33333333-3333-4333-8333-333333333333"))
    correlation = str(uuid.UUID("44444444-4444-4444-8444-444444444444"))
    shipment = str(uuid.UUID("55555555-5555-4555-8555-555555555555"))
    body = json.dumps({
        "shipmentId": shipment,
        "reasonCode": "DAMAGED",
        "claimedAmount": "1",
        "currency": "USD",
        "evidenceReferenceIds": []
    }, separators=(",", ":")).encode("utf-8")
    headers = [
        b"Host: 127.0.0.1",
        b"Authorization: Bearer " + token(tenant, legal_entity, actor).encode("ascii"),
        b"X-Tenant-Id: " + tenant.encode("ascii"),
        b"X-Legal-Entity-Id: " + legal_entity.encode("ascii"),
        b"X-Correlation-Id: " + correlation.encode("ascii"),
        b"Content-Type: application/json",
        b"Content-Length: " + str(len(body)).encode("ascii"),
        b"Connection: close"
    ]
    if key_lines:
        headers.extend(key_lines)
    if extra_lines:
        headers.extend(extra_lines)
    raw_request = b"POST /api/shipment-bundle/claims HTTP/1.1\r\n" + b"\r\n".join(headers) + b"\r\n\r\n" + body
    with socket.create_connection(("127.0.0.1", port), timeout=10) as sock:
        sock.sendall(raw_request)
        response = bytearray()
        while True:
            chunk = sock.recv(65536)
            if not chunk:
                break
            response.extend(chunk)
    raw_response = bytes(response)
    head, _, response_body = raw_response.partition(b"\r\n\r\n")
    lines = head.split(b"\r\n")
    status = int(lines[0].split()[1]) if lines else None
    response_headers = {}
    for line in lines[1:]:
        if b":" in line:
            name, value = line.split(b":", 1)
            response_headers.setdefault(name.decode("ascii").lower(), []).append(value.strip().decode("latin-1"))
    if "transfer-encoding" in response_headers and "chunked" in ",".join(response_headers["transfer-encoding"]).lower():
        response_body = decode_chunked(response_body)
    parsed = None
    try:
        parsed = json.loads(response_body.decode("utf-8")) if response_body else None
    except (UnicodeDecodeError, json.JSONDecodeError):
        parsed = None
    error = parsed.get("error") if isinstance(parsed, dict) else None
    return {
        "status": status,
        "responseCorrelation": (response_headers.get("x-correlation-id") or [None])[0],
        "errorCode": error.get("code") if isinstance(error, dict) else None,
        "errorCorrelation": error.get("correlationId") if isinstance(error, dict) else None,
        "contractVersion": parsed.get("contractVersion") if isinstance(parsed, dict) else None,
        "bodyUtf8": response_body.decode("utf-8", errors="replace"),
        "requestSha256": hashlib.sha256(raw_request).hexdigest(),
        "responseSha256": hashlib.sha256(raw_response).hexdigest(),
        "requestHeaderBytes": len(raw_request.partition(b"\r\n\r\n")[0]),
        "rawResponseBytes": len(raw_response)
    }


def key(value: bytes, casing: bytes = b"Idempotency-Key") -> bytes:
    return casing + b": " + value


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--phase", choices=["baseline", "target"], required=True)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    cases = {
        "ascii-1": ([key(b"a")], None),
        "ascii-128": ([key(b"a" * 128)], None),
        "ascii-129": ([key(b"a" * 129)], None),
        "utf8-eacute-128": ([key(("é" * 128).encode("utf-8"))], None),
        "utf8-eacute-129": ([key(("é" * 129).encode("utf-8"))], None),
        "utf8-emoji-128-lowercase-name": ([key(("😀" * 128).encode("utf-8"), b"idempotency-key")], None),
        "utf8-emoji-129": ([key(("😀" * 129).encode("utf-8"))], None),
        "invalid-utf8-idempotency": ([key(b"bad-\xc3\x28")], None),
        "duplicate-idempotency": ([key(b"first"), key(b"second")], None),
        "missing-idempotency": (None, None),
        "ascii-other-header": ([key(b"valid")], [b"X-R14-Control: ascii"]),
        "utf8-other-header": ([key(b"valid")], [b"X-R14-Control: " + "é".encode("utf-8")])
    }
    results = {name: send(args.port, lines, extra) for name, (lines, extra) in cases.items()}
    expectations = {}
    if args.phase == "baseline":
        expectations = {
            "ascii-128": (404, "CLAIM_NOT_FOUND"),
            "utf8-eacute-128": (400, None),
            "invalid-utf8-idempotency": (400, None),
            "utf8-other-header": (404, "CLAIM_NOT_FOUND")
        }
    else:
        expectations = {
            "ascii-1": (404, "CLAIM_NOT_FOUND"),
            "ascii-128": (404, "CLAIM_NOT_FOUND"),
            "ascii-129": (400, "INVALID_REQUEST"),
            "utf8-eacute-128": (404, "CLAIM_NOT_FOUND"),
            "utf8-eacute-129": (400, "INVALID_REQUEST"),
            "utf8-emoji-128-lowercase-name": (404, "CLAIM_NOT_FOUND"),
            "utf8-emoji-129": (400, "INVALID_REQUEST"),
            "invalid-utf8-idempotency": (400, None),
            "duplicate-idempotency": (400, "INVALID_REQUEST"),
            "missing-idempotency": (400, "INVALID_REQUEST"),
            "ascii-other-header": (404, "CLAIM_NOT_FOUND"),
            "utf8-other-header": (404, "CLAIM_NOT_FOUND")
        }
    failures = {}
    for name, (status, code) in expectations.items():
        actual = results[name]
        if actual["status"] != status or actual["errorCode"] != code:
            failures[name] = {"expected": [status, code], "actual": [actual["status"], actual["errorCode"]]}
    output = {
        "phase": args.phase,
        "wireEncoding": "raw TCP; test Unicode values encoded with Python strict UTF-8",
        "scalarVsBytes": {
            "eacute128": {"scalars": 128, "utf8Bytes": len(("é" * 128).encode("utf-8"))},
            "emoji128": {"scalars": 128, "utf8Bytes": len(("😀" * 128).encode("utf-8"))}
        },
        "results": results,
        "expectations": {k: list(v) for k, v in expectations.items()},
        "failures": failures,
        "status": "PASS" if not failures else "FAIL",
        "secretsRecorded": False,
        "tokenRecorded": False
    }
    Path(args.output).write_text(json.dumps(output, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"phase": args.phase, "status": output["status"], "failures": failures,
                      "cases": {k: [v["status"], v["errorCode"]] for k, v in results.items()}}, ensure_ascii=False))
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
