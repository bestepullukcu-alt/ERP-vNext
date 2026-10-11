#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1 phase K08: redaction and secret scan.

  redact <in> <out>                 copy a raw capture with credentials replaced by [REDACTED:<kind>]
  scan   <evidence_dir> [--exact] [--seal] [--out NAME]
        scans every file (bytes, PNG included) and every member of .tar/.tar.gz archives for
        (1) credential PATTERNS — the union of the prior lanes' SECRET-SCAN rules — and, with --exact,
        (2) the EXACT lane values taken from this process's environment (LANE_SECRET_<GROUP>, ACTOR_PW_<LABEL>) in raw,
            base64 (standard/url-safe, with and without padding) and URL-encoded form.
v1.1: D4 exact values come only from the environment the lane supervisor gives this child (`k00_ctl.py SOCK run
      seal -- python3 k08_redact_scan.py scan E --exact --seal`); W/secrets no longer exists. --exact with no value
      in the environment FAILS (no vacuous PASS).
      G4 --seal is the FINAL write of the lane: it records the sha256 of ARTIFACTS.sha256 (written just before it) and
      fails if any evidence file changes while it scans. Nothing may be written after it except by a new, attempt-
      suffixed seal.
Output (E/SECRET-SCAN-FINAL-a<N>.txt with --seal, else --out NAME) names file, line and rule only; never a value.
Exit 1 if anything matches.
"""
import argparse, base64, hashlib, os, pathlib, re, sys, tarfile, time, urllib.parse

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


def env_values():
    """{label: [(form, bytes)]} for every lane value in this process's environment; values are never printed."""
    vals = {k: v for k, v in os.environ.items() if (k.startswith("LANE_SECRET_") or k.startswith("ACTOR_PW_")) and v}
    forms = {}
    for k, v in vals.items():
        b = v.encode()
        cand = {"raw": b, "b64": base64.b64encode(b), "b64url": base64.urlsafe_b64encode(b),
                "urlenc": urllib.parse.quote(v, safe="").encode()}
        for n in ("b64", "b64url"):
            cand[n + "-nopad"] = cand[n].rstrip(b"=")
        forms[k] = [(f, x) for f, x in cand.items() if len(x) >= 12]
    return forms


def units(root, skip):
    for p in sorted(pathlib.Path(root).rglob("*")):
        if not p.is_file() or p.resolve() in skip:
            continue
        name = p.name
        if name.endswith((".tar.gz", ".tgz", ".tar")):
            with tarfile.open(p, "r:*") as t:
                for m in t.getmembers():
                    if m.isfile():
                        yield f"{p}::{m.name}", t.extractfile(m).read()
        else:
            yield str(p), p.read_bytes()


def unique(d, base, ext):
    n = 1
    while (d / f"{base}-a{n}.{ext}").exists():
        n += 1
    return d / f"{base}-a{n}.{ext}"


def snapshot(root, skip):
    return {str(p): (p.stat().st_size, p.stat().st_mtime_ns) for p in pathlib.Path(root).rglob("*")
            if p.is_file() and p.resolve() not in skip}


def scan(root, exact, seal, allow_bcrypt_files, out_name):
    root = pathlib.Path(root)
    out = unique(root, "SECRET-SCAN-FINAL", "txt") if seal else root / out_name
    skip = {out.resolve()}
    forms = env_values() if exact else {}
    if exact and not forms:
        print("K08 scan FAIL: --exact requested but no LANE_SECRET_*/ACTOR_PW_* in the environment "
              "(run it through the lane supervisor)"); sys.exit(1)
    before = snapshot(root, skip) if seal else None
    hits, files = [], 0
    for name, data in units(root, skip):
        files += 1
        text = data.decode("utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            for rule, rx in PATTERNS.items():
                if rule == "bcrypt-hash" and any(name.endswith(x) for x in allow_bcrypt_files):
                    continue
                if rx.search(line):
                    hits.append((name, i, rule))
        for k, fs in forms.items():
            for f, x in fs:
                if x in data:
                    hits.append((name, "-", f"exact:{k}:{f}"))
    report = [f"scanned_units\t{files}",
              f"exact_values\t{len(forms)} ({', '.join(sorted(forms)) or 'none'})",
              f"exact_forms\t{sum(len(v) for v in forms.values())}",
              f"pattern_rules\t{','.join(PATTERNS)}"]
    if seal:
        art = root / "ARTIFACTS.sha256"
        report.append(f"artifacts_sha256\t{hashlib.sha256(art.read_bytes()).hexdigest() if art.exists() else 'ABSENT'}")
        changed = [k for k, v in snapshot(root, skip).items() if before.get(k) != v] + [k for k in before if not os.path.exists(k)]
        report.append(f"files_changed_during_scan\t{len(changed)}")
        if changed or not art.exists():
            hits.append(("seal", "-", "ARTIFACTS.sha256 missing or evidence changed during the final scan"))
        report.append(f"sealed_at_utc\t{time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}")
        report.append("seal_command\tk08_redact_scan.py scan <evidence> --exact --seal (run by the lane supervisor; "
                      "this report is the command's record — no COMMANDS.tsv row follows it)")
    report += [f"HIT\t{n}\t{i}\t{r}" for n, i, r in hits]
    report.append(f"result\t{'FAIL' if hits else 'PASS'}")
    out.write_text("\n".join(report) + "\n")
    print(f"K08 scan {'FAIL' if hits else 'PASS'}: {files} units, {len(forms)} exact values, {len(hits)} hit(s) -> {out.name}")
    sys.exit(1 if hits else 0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["redact", "scan"])
    ap.add_argument("paths", nargs="+")
    ap.add_argument("--exact", action="store_true", help="also scan for the lane values in this process's environment")
    ap.add_argument("--seal", action="store_true", help="final write of the lane (G4)")
    ap.add_argument("--out", default="SECRET-SCAN.txt", help="report file name inside the evidence dir (not with --seal)")
    ap.add_argument("--allow-bcrypt-in", default="", help="comma-separated file suffixes that may contain a bcrypt hash literally (e.g. copied source)")
    ap.add_argument("--work", help="ignored (v1.0 compatibility; v1.1 reads no secret file)")
    a = ap.parse_args()
    if a.mode == "redact":
        redact(*a.paths[:2])
    else:
        scan(a.paths[0], a.exact, a.seal, [x for x in a.allow_bcrypt_in.split(",") if x], a.out)


if __name__ == "__main__":
    main()
