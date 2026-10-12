#!/usr/bin/env python3
"""Q97 v2.1 Part 3 — compose the Q64b Claims tree in ~/mvp6-env/claims (never from the working tree, D1).

Order (Q97 prompt + CT F3 recipe; see ASSUMPTION A-01 in the Q64b report for why BC-SOURCE / normal-source are not layered):
  L0 git archive HEAD (expected 4a8d4d4b…)
  L1 A12 360 successor overlay 7b6a0d1a… (wrapper 'mvp6-shipment-a12-rework-src/' stripped), verified 360/360 vs 8ffa6c96…
  L2 Auth 22 overlay f50350b8… (whole archive extracted, as K01 and A12 VER-02 do), verified 22/22 vs FINAL-22 b9713185…
  L3 Claims draft module files from claims-ui-draft-overlay.tar.gz bc0f5819… (overlay/frontend/**, overlay/services/**, 23 files),
     each verified against the draft SOURCE-MANIFEST.tsv
  L4 Claims draft _shared-integration items applied to THIS environment copy only (draft README step 3):
     supplychain Program.cs provider line, 2 ocelot routes, 2 nav keys x 7 SharedResource files.
Every archive hash is checked before extraction; any mismatch stops the run (exit 2).
Outputs (in --out-dir): SOURCE-MANIFEST.tsv (whole tree), LAYERS.tsv, COMPOSE-LOG.txt, derived kit overlays in --inputs.
"""
import argparse, hashlib, io, json, os, pathlib, re, subprocess, sys, tarfile

REPO = "/Users/natig/Projects/ERP-vNext-recovery"
R = REPO + "/docs/records/audits/2026-09"
HEAD = "4a8d4d4b339528a88e6220fb8402e5a2c771136c"
IN = {
    "a12": (R + "/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz", "7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d"),
    "a12m": (R + "/mvp6-shipment-a12-safe404-rework-01/SUCCESSOR-360-SOURCE-MANIFEST.tsv", "8ffa6c96d29f8004940f310ed704a2c05eaa38ffa501d6114a5f9251e6e00d36"),
    "auth": (R + "/mvp6-carrier-numericdate-exec-01/final-source.tar.gz", "f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd"),
    "authm": (R + "/mvp6-carrier-numericdate-exec-01/FINAL-22-SOURCE-MANIFEST.tsv", "b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731"),
    "claims": (R + "/mvp6-claims-ui-draft-01/claims-ui-draft-overlay.tar.gz", "bc0f5819de94273ebca162ed6ede2a62555f888f314c799c27e56a1c75b82e60"),
    "claimsums": (R + "/mvp6-claims-ui-draft-01/SHA256SUMS", "80da27c94c6e8a124e4636e679f95b39ef7e67ad5ea489a097e84d801df608d3"),
}
LOG = io.StringIO()


def log(msg):
    print(msg); LOG.write(msg + "\n")


def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest()


def shab(b):
    return hashlib.sha256(b).hexdigest()


def stop(msg):
    log("STOP: " + msg); sys.exit(2)


def safe_name(n):
    while n.startswith("./"):
        n = n[2:]
    return n


def extract(tar_path, dest, strip="", only=None):
    """Extract regular files; refuse links/devices/.. paths; skip AppleDouble. Returns list of repo paths written."""
    out = []
    with tarfile.open(tar_path, "r:gz") as t:
        for m in t.getmembers():
            n = safe_name(m.name)
            if not n or os.path.basename(n.rstrip("/")).startswith("._"):
                continue
            if m.isdir():
                continue
            if not m.isfile():
                stop(f"non-regular member {m.name} in {tar_path}")
            if strip:
                if not n.startswith(strip):
                    stop(f"member {n} outside wrapper {strip}")
                n = n[len(strip):]
            if only is not None:
                hit = next((p for p in only if n.startswith(p[0])), None)
                if not hit:
                    continue
                n = hit[1] + n[len(hit[0]):]
            if n.startswith("/") or ".." in pathlib.PurePosixPath(n).parts:
                stop(f"unsafe path {n}")
            data = t.extractfile(m).read()
            p = pathlib.Path(dest, n); p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(data); out.append(n)
    return out


def manifest_rows(path):
    lines = pathlib.Path(path).read_text().splitlines()
    rows = []
    if lines[0].startswith("path\t"):
        hdr = lines[0].split("\t")
        for l in lines[1:]:
            if not l.strip():
                continue
            d = dict(zip(hdr, l.split("\t")))
            if "target_sha256" in d:
                want = d["target_sha256"].strip()
                rows.append((d["path"], None if want in ("", "-") or d.get("disposition", "").lower() in ("deleted", "removed") else want))
            else:
                rows.append((d["path"], d["sha256"]))
    else:
        rows = [(l.split("\t")[0], l.split("\t")[1]) for l in lines if l.strip()]
    return rows


def verify(root, rows):
    bad = []
    for rel, want in rows:
        p = pathlib.Path(root, rel)
        if want is None:
            if p.exists():
                bad.append((rel, "absent", "present"))
        elif not p.is_file():
            bad.append((rel, want, "missing"))
        elif sha(p) != want:
            bad.append((rel, want, sha(p)))
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dest", required=True)
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--inputs", required=True)
    a = ap.parse_args()
    dest = pathlib.Path(a.dest); out = pathlib.Path(a.out_dir); inp = pathlib.Path(a.inputs)
    if dest.exists() and any(dest.iterdir()):
        stop(f"{dest} not empty — compose always starts empty")
    dest.mkdir(parents=True, exist_ok=True); out.mkdir(parents=True, exist_ok=True); inp.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    g = lambda *x: subprocess.run(["git", "--no-optional-locks", "-C", REPO, *x], check=True, capture_output=True, text=True, env=env).stdout.strip()
    head, br = g("rev-parse", "HEAD"), g("rev-parse", "--abbrev-ref", "HEAD")
    if head != HEAD or br != "feature/mvp6-logistics":
        stop(f"repo at {br}@{head}")
    log(f"repo {br}@{head} OK")

    # hash every sealed input BEFORE any extraction
    for k, (p, want) in IN.items():
        got = sha(p)
        log(f"input {k} {p.replace(REPO + '/', '')} sha256 {got} {'OK' if got == want else 'MISMATCH'}")
        if got != want:
            stop(f"hash mismatch {p}")
    # claims draft SOURCE-MANIFEST is bound through the draft SHA256SUMS (80da27c9…)
    sums = dict((l.split("  ", 1)[1], l.split("  ", 1)[0]) for l in pathlib.Path(IN["claimsums"][0]).read_text().splitlines() if "  " in l)
    cm = R + "/mvp6-claims-ui-draft-01/SOURCE-MANIFEST.tsv"
    if sha(cm) != sums.get("SOURCE-MANIFEST.tsv"):
        stop("claims SOURCE-MANIFEST.tsv not bound by SHA256SUMS")
    log(f"input claims-manifest SOURCE-MANIFEST.tsv sha256 {sha(cm)} OK (bound by SHA256SUMS)")

    layers = {}
    # L0
    tarp = pathlib.Path(a.inputs, "head.tar")
    with open(tarp, "wb") as f:
        subprocess.run(["git", "--no-optional-locks", "-C", REPO, "archive", "--format=tar", HEAD], check=True, stdout=f, env=env)
    log(f"L0 head.tar sha256 {sha(tarp)}")
    with tarfile.open(tarp) as t:
        n0 = 0
        for m in t.getmembers():
            if m.isfile():
                p = dest / m.name; p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(t.extractfile(m).read()); n0 += 1
                layers[m.name] = "L0-HEAD"
            elif m.issym():
                p = dest / m.name; p.parent.mkdir(parents=True, exist_ok=True)
                if not p.exists():
                    os.symlink(m.linkname, p)
                layers[m.name] = "L0-HEAD(symlink)"
    tarp.unlink()
    log(f"L0 HEAD files {n0}")
    # L1
    w1 = extract(IN["a12"][0], dest, strip="mvp6-shipment-a12-rework-src/")
    for n in w1:
        layers[n] = "L1-A12-360"
    b = verify(dest, manifest_rows(IN["a12m"][0]))
    log(f"L1 A12 files {len(w1)}; manifest {len(manifest_rows(IN['a12m'][0])) - len(b)}/{len(manifest_rows(IN['a12m'][0]))}")
    if b:
        stop(f"A12 mismatch {b[:5]}")
    # L2
    w2 = extract(IN["auth"][0], dest)
    ov = set(w1) & set(w2)
    for n in w2:
        layers[n] = "L2-AUTH22-archive"
    r2 = manifest_rows(IN["authm"][0]); b = verify(dest, r2)
    log(f"L2 Auth files {len(w2)}; overlap with L1 {len(ov)}; FINAL-22 {len(r2) - len(b)}/{len(r2)}")
    if b or ov:
        stop(f"Auth mismatch {b[:5]} overlap {sorted(ov)[:5]}")
    # L3
    crow = dict(manifest_rows(cm))
    w3 = extract(IN["claims"][0], dest, only=[("overlay/frontend/", "frontend/"), ("overlay/services/", "services/")])
    bad = [n for n in w3 if sha(dest / n) != crow.get("overlay/" + n)]
    ov3 = [n for n in w3 if n in layers and layers[n] != "L0-HEAD"]
    for n in w3:
        layers[n] = "L3-CLAIMS-DRAFT-MODULE"
    log(f"L3 Claims module files {len(w3)} (expected 23); manifest match {len(w3) - len(bad)}/{len(w3)}; replaced non-HEAD paths {ov3}")
    if len(w3) != 23 or bad or ov3:
        stop(f"claims module mismatch {bad} {ov3}")
    # derived kit overlay 3 (module files) — content identical to the verified L3 files
    mk_overlay(inp / "claims-module-23", dest, w3)

    # L4 — _shared-integration applied to the environment copy only
    with tarfile.open(IN["claims"][0], "r:gz") as t:
        si = {safe_name(m.name): t.extractfile(m).read().decode() for m in t.getmembers() if m.isfile() and "_shared-integration/" in m.name and not os.path.basename(m.name).startswith("._")}
    w4 = []
    # (a) provider line
    pc = "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs"
    txt = (dest / pc).read_text()
    line = "builder.Services.AddSingleton<IModuleManifestProvider, ClaimsManagementManifestProvider>();"
    if line in txt:
        stop("provider line already present")
    anchor = "builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();"
    if txt.count(anchor) != 1 or "using Diten.SupplyChainService.Api.ModuleRegistration;" not in txt:
        stop("Program.cs anchor/using not found exactly once")
    (dest / pc).write_text(txt.replace(anchor, anchor + "\n" + line, 1)); w4.append(pc)
    # (b) ocelot routes
    oc = "gateway/Diten.ApiGateway/ocelot.json"
    frag = json.loads(si["overlay/_shared-integration/gateway-claims-routes.ocelot-fragment.json"])
    ocj = json.loads((dest / oc).read_text())
    ups = {(r.get("UpstreamPathTemplate"),) for r in ocj["Routes"]}
    for c in frag["Confirm"]:
        hit = [r for r in ocj["Routes"] if r.get("UpstreamPathTemplate") == c["UpstreamPathTemplate"] and "GET" in [x.upper() for x in r.get("UpstreamHttpMethod", [])]]
        log(f"L4 ocelot confirm {c['UpstreamPathTemplate']} GET: {'present' if hit else 'ABSENT'}")
        if not hit:
            stop("confirm route absent")
    for r in frag["Routes"]:
        if (r["UpstreamPathTemplate"],) in ups:
            stop(f"route already present {r['UpstreamPathTemplate']}")
        ocj["Routes"].append(r)
    (dest / oc).write_text(json.dumps(ocj, indent=2, ensure_ascii=False) + "\n"); w4.append(oc)
    log(f"L4 ocelot routes appended 2 -> total {len(ocj['Routes'])}")
    # (c) nav keys x7
    for lang in ("en", "tr", "fr", "es", "zh", "ar", "ru"):
        rx = f"frontend/Diten.Web/Resources/SharedResource.{lang}.resx"
        fr = si[f"overlay/_shared-integration/sharedresource-nav-keys/SharedResource.{lang}.resx.fragment.xml"]
        rows = re.findall(r"<data name=\"[^\"]+\" xml:space=\"preserve\"><value>.*?</value></data>", fr, re.S)
        if len(rows) != 2:
            stop(f"fragment {lang} rows {len(rows)}")
        t = (dest / rx).read_text(encoding="utf-8-sig") if (dest / rx).read_bytes().startswith(b"\xef\xbb\xbf") else (dest / rx).read_text(encoding="utf-8")
        bom = (dest / rx).read_bytes().startswith(b"\xef\xbb\xbf")
        for rrow in rows:
            key = re.search(r'name="([^"]+)"', rrow).group(1)
            if f'name="{key}"' in t:
                stop(f"{rx} already has {key}")
        if t.count("</root>") != 1:
            stop(f"{rx} </root> count")
        t = t.replace("</root>", "".join("  " + x + "\n" for x in rows) + "</root>")
        (dest / rx).write_bytes((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8")); w4.append(rx)
    prev = {n: layers.get(n) for n in w4}
    for n in w4:
        layers[n] = "L4-ENV-INTEGRATION(" + (prev[n] or "new") + ")"
    log(f"L4 env-integration files {len(w4)}; previous layers {prev}")
    mk_overlay(inp / "claims-env-integration", dest, w4)
    (out / "L4-PREVIOUS-LAYER.json").write_text(json.dumps(prev, indent=2) + "\n")

    # whole-tree manifest
    mf = out / "SOURCE-MANIFEST.tsv"
    with open(mf, "w") as f:
        f.write("path\tsha256\tbytes\n")
        for p in sorted(x for x in dest.rglob("*") if x.is_file() and not x.is_symlink()):
            f.write(f"{p.relative_to(dest).as_posix()}\t{sha(p)}\t{p.stat().st_size}\n")
    with open(out / "LAYERS.tsv", "w") as f:
        f.write("path\tlayer\n")
        for k in sorted(layers):
            if layers[k] != "L0-HEAD":
                f.write(f"{k}\t{layers[k]}\n")
    log(f"COMPOSE PASS SOURCE-MANIFEST.tsv sha256 {sha(mf)} files {sum(1 for _ in open(mf)) - 1}")
    (out / "COMPOSE-LOG.txt").write_text(LOG.getvalue())


def mk_overlay(base, root, paths):
    """Derived kit overlay: tar.gz of the given composed paths (no wrapper) + headered manifest."""
    tp = pathlib.Path(str(base) + ".tar.gz"); mp = pathlib.Path(str(base) + "-MANIFEST.tsv")
    with tarfile.open(tp, "w:gz") as t:
        for n in sorted(paths):
            ti = t.gettarinfo(str(pathlib.Path(root, n)), arcname=n); ti.uid = ti.gid = 0; ti.uname = ti.gname = ""; ti.mtime = 0; ti.mode = 0o644
            with open(pathlib.Path(root, n), "rb") as fh:
                t.addfile(ti, fh)
    with open(mp, "w") as f:
        f.write("path\tsha256\tbytes\n")
        for n in sorted(paths):
            p = pathlib.Path(root, n); f.write(f"{n}\t{sha(p)}\t{p.stat().st_size}\n")
    log(f"derived overlay {tp.name} sha256 {sha(tp)} manifest {mp.name} sha256 {sha(mp)} files {len(paths)}")


if __name__ == "__main__":
    main()
