#!/usr/bin/env python3
import argparse
import json
from pathlib import Path

SENSITIVE_TOKENS = ("Secret", "ApiKey", "Password", "HashSecret", "Token", "ConnectionString")

ALLOWLIST = {
    "services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.json::MongoDbSettings:ConnectionString": {
        "owner": "platform-shared-services",
        "reason": "Credentialless local MongoDB endpoint used for developer bootstrap only.",
        "review_date": "2026-05-26",
    },
    "services/Diten.Platform/src/Diten.Platform.API/appsettings.json::MongoDbSettings:ConnectionString": {
        "owner": "platform-shared-services",
        "reason": "Credentialless local MongoDB endpoint used for developer bootstrap only.",
        "review_date": "2026-05-26",
    },
    "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/appsettings.json::Mongo:ConnectionString": {
        "owner": "platform-shared-services",
        "reason": "Credentialless local MongoDB endpoint used for developer bootstrap only.",
        "review_date": "2026-05-26",
    },
    "frontend/Diten.Web/appsettings.json::ConnectionStrings:MongoDb": {
        "owner": "platform-shared-services",
        "reason": "Credentialless local MongoDB endpoint used for developer bootstrap only.",
        "review_date": "2026-05-26",
    },
}


def flatten(node, prefix=""):
    if isinstance(node, dict):
        for key, value in node.items():
            next_prefix = f"{prefix}:{key}" if prefix else key
            yield from flatten(value, next_prefix)
    elif isinstance(node, list):
        for index, value in enumerate(node):
            yield from flatten(value, f"{prefix}:{index}")
    else:
        yield prefix, node


def is_sensitive(path):
    return any(token.lower() in path.lower() for token in SENSITIVE_TOKENS)


def is_credentialed_connection_string(value):
    lower = value.lower()
    return "://" in lower and ("@" in value or "password=" in lower or "pwd=" in lower or "user=" in lower or "username=" in lower)


def is_allowed(relative_file, key_path, value):
    allow_key = f"{relative_file}::{key_path}"
    entry = ALLOWLIST.get(allow_key)
    if not entry:
        return False

    if key_path.lower().endswith("connectionstring") and is_credentialed_connection_string(value):
        return False

    return all(entry.get(field) for field in ("owner", "reason", "review_date"))


def main():
    parser = argparse.ArgumentParser(description="Scan production appsettings.json files for committed secrets.")
    parser.add_argument("root", nargs="?", default=".", help="Repository root.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    included_prefixes = (
        "services/Diten.AuthService/",
        "services/Diten.Platform/",
        "services/Diten.DevEnablementService/",
        "gateway/Diten.ApiGateway/",
        "frontend/Diten.Web/",
    )
    files = []
    for path in root.rglob("appsettings.json"):
        relative = path.relative_to(root).as_posix()
        if any(part in path.parts for part in ("bin", "obj", "_Reference")):
            continue
        if relative.startswith(included_prefixes):
            files.append(path)

    failures = []
    for file_path in files:
        relative = file_path.relative_to(root).as_posix()
        data = json.loads(file_path.read_text(encoding="utf-8-sig"))
        for key_path, value in flatten(data):
            if not isinstance(value, str) or not is_sensitive(key_path) or value == "":
                continue
            value_text = str(value).strip()
            if not value_text:
                continue
            if is_allowed(relative, key_path, value_text):
                continue
            failures.append((relative, key_path))

    if failures:
        print("Static secret scan failed. Suspicious values were found at these paths:")
        for relative, key_path in failures:
            print(f"- {relative} :: {key_path}")
        return 1

    print(f"Static secret scan passed. Files scanned: {len(files)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
