#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K08: redaction and secret scan.

  redact <in> <out>            copy a raw capture with credentials replaced by [REDACTED:<kind>]
  scan   <evidence_dir> --work W   scan every file (and every member of .tar/.tar.gz archives) for
                                   (1) credential PATTERNS — the union of the prior lanes' SECRET-SCAN rules — and
                                   (2) the EXACT lane secrets in W/secrets (JWT/service secrets, actor passwords,
                                       plus their base64 and URL-encoded forms).
Scan output (E/SECRET-SCAN.txt, or --out NAME) names file, line and rule only; a matched value is never printed.
Exit 1 if anything matches. Run it last, after every other file is final and before SHA256SUMS/ARTIFACTS.
"""
import argparse, base64, io, json, pathlib, re, sys, tarfile, urllib.parse

PATTERNS = {
    "jwt": re.compile(r"eyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}"),
    "bearer-header": re.compile(r"(?i)authorization\s*[:=]\s*\"?bearer\s+[A-Za-z0-9._~+/=-]{8,}"),
    "cookie-header": re.compile(r"(?im)^\s*(set-cookie|cookie)\s*:\s*[^=\s]+=(?!\[REDACTED)[^;\s]{8,}"),
    "token-field": re.compile(r"(?i)\"(access_?token|refresh_?token|id_?token)\"\s*:\s*\"(?!\[REDACTED)[^\"]{8,}\""),
    "password-field": re.compile(r"(?i)\"?(password|passwd|pwd)\"?\s*[:=]\s*\"[^\"\[]{4,}\""),
    "secret-assignment": re.compile(r"(?i)(jwtsettings__secret|internalapikey|apikey|hashsecret|activesecret|previoussecret)\s*[=:]\s*['\"]?[^\s'\"\[]{8,}"),
    "mongo-credentials": re.compile(r"mongodb(\+srv)?://(?!\[REDACTED)[^/\s:@]+:[^/\s@]+@"),
    "bcrypt-hash": re.compile(r"\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}"),
}
REDACT = [
    (PATTERNS["jwt"], "[REDACTED:jwt]"),
    (re.compile(r"(?i)(authorization\s*[:=]\s*\"?bearer\s+)[A-Za-z0-9._~+/=-]+"), r"\1[REDACTED:bearer]"),
    (re.compile(r"(?im)^(\s*(?:set-cookie|cookie)\s*:\s*[^=;\s]+=)[^;\r\n]*"), r"\1[REDACTED:cookie]"),
    (re.compile(r"(?i)(\"(?:access_?token|refresh_?token|id_?token|password|secret|apiKey|internalApiKey)\"\s*:\s*\")[^\"]*\""), r'\1[REDACTED]"'),
    (re.compile(r"(mongodb(?:\+srv)?://)[^/\s:@]+:[^/\s@]+@"), r"\1[REDACTED:cred]@"),
    (PATTERNS["bcrypt-hash"], "[REDACTED:bcrypt]"),
]


def redact(inp, out):
    text = pathlib.Path(inp).read_text(errors="replace")
    n = 0
    for rx, rep in REDACT:
        text, k = rx.subn(rep, text); n += k
    pathlib.Path(out).write_text(text)
    print(f"K08 redact: {n} replacement(s) -> {out}")


def lane_secrets(work):
    vals = []
    sec = pathlib.Path(work, "secrets")
    if (sec / "lane-secrets.json").exists():
        vals += list(json.loads((sec / "lane-secrets.json").read_text()).values())
    if (sec / "actors.json").exists():
        vals += [v["password"] for v in json.loads((sec / "actors.json").read_text()).values()]
    forms = set()
    for v in vals:
        forms |= {v, base64.b64encode(v.encode()).decode().rstrip("="), urllib.parse.quote(v, safe="")}
    return [f for f in forms if len(f) >= 8]


def units(root):
    for p in sorted(pathlib.Path(root).rglob("*")):
        if not p.is_file():
            continue
        name = p.name
        if name.endswith((".tar.gz", ".tgz", ".tar")):
            with tarfile.open(p, "r:*") as t:
                for m in t.getmembers():
                    if m.isfile():
                        yield f"{p}::{m.name}", t.extractfile(m).read()
        else:
            yield str(p), p.read_bytes()


def scan(root, work, allow_bcrypt_files, out_name="SECRET-SCAN.txt"):
    exact = lane_secrets(work) if work else []
    hits = []
    files = 0
    for name, data in units(root):
        files += 1
        text = data.decode("utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            for rule, rx in PATTERNS.items():
                if rule == "bcrypt-hash" and any(name.endswith(x) for x in allow_bcrypt_files):
                    continue
                if rx.search(line):
                    hits.append((name, i, rule))
            if exact and any(s in line for s in exact):
                hits.append((name, i, "exact-lane-secret"))
    report = [f"scanned_units\t{files}", f"exact_lane_secret_forms\t{len(exact)}", f"pattern_rules\t{','.join(PATTERNS)}"]
    report += [f"HIT\t{n}\t{i}\t{r}" for n, i, r in hits]
    report.append(f"result\t{'FAIL' if hits else 'PASS'}")
    pathlib.Path(root, out_name).write_text("\n".join(report) + "\n")
    print(f"K08 scan {'FAIL' if hits else 'PASS'}: {files} units, {len(hits)} hit(s)")
    sys.exit(1 if hits else 0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["redact", "scan"])
    ap.add_argument("paths", nargs="+")
    ap.add_argument("--work")
    ap.add_argument("--out", default="SECRET-SCAN.txt", help="report file name inside the evidence dir")
    ap.add_argument("--allow-bcrypt-in", default="", help="comma-separated file suffixes that may contain a bcrypt hash literally (e.g. copied source)")
    a = ap.parse_args()
    if a.mode == "redact":
        redact(*a.paths[:2])
    else:
        scan(a.paths[0], a.work, [x for x in a.allow_bcrypt_in.split(",") if x], a.out)


if __name__ == "__main__":
    main()
