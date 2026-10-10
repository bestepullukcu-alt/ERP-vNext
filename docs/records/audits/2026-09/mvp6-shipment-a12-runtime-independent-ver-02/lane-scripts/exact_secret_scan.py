#!/usr/bin/env python3
"""A12 runtime VER-02 exact-value secret scan. Run by lane_supervisor.py `run`, the only process holding the real values
(LANE_SECRET_* and ACTOR_PW_* in env). Replaces the evidence-kit K08 exact scan, which reads W/secrets — here W/secrets holds
placeholders only. Every file under the evidence directory is scanned as bytes (PNG included) for each value in raw,
base64 (standard/url-safe, with and without padding) and URL-encoded form. Output names file + value label + form only.
The fixture-admin password never left fixture_prepare.py's memory and cannot be scanned exactly; the K08 pattern scan covers it.
"""
import base64, os, pathlib, sys, urllib.parse

E = pathlib.Path(sys.argv[1]); out = E / sys.argv[2]
vals = {k: v for k, v in os.environ.items() if (k.startswith("LANE_SECRET_") or k.startswith("ACTOR_PW_")) and v}
forms = {}
for k, v in vals.items():
    b = v.encode()
    cand = {"raw": b, "b64": base64.b64encode(b), "b64url": base64.urlsafe_b64encode(b), "urlenc": urllib.parse.quote(v, safe="").encode()}
    for n in ("b64", "b64url"):
        cand[n + "-nopad"] = cand[n].rstrip(b"=")
    forms[k] = {f: x for f, x in cand.items() if len(x) >= 12}
hits, files = [], 0
for p in sorted(E.rglob("*")):
    if not p.is_file() or p == out:
        continue
    files += 1; data = p.read_bytes()
    for k, fs in forms.items():
        for f, x in fs.items():
            if x in data:
                hits.append(f"{p.relative_to(E)}\t{k}\t{f}")
lines = [f"# exact-value secret scan: {len(vals)} lane values (names: {', '.join(sorted(vals))}); {files} files scanned",
         f"# result: {'FAIL' if hits else 'PASS'} ({len(hits)} hit(s))"] + hits
out.write_text("\n".join(lines) + "\n")
print(lines[1]); sys.exit(1 if hits else 0)
