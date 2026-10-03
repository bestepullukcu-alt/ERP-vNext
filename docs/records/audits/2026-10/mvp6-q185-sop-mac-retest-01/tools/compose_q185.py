#!/usr/bin/env python3
"""Q185 compose (copied from Q84b compose_q84b.py; v3 archive + v3 FILE-PLAN check added) per BASE-STACK v2 §4 (cp only, fail-closed, never deletes) + S&OP overlay v3 + env-copy integration.
tree A = BASE a8a236de -> Q117 83e6322c -> Q121 93bf1c07 -> Q131 4a4a0860        (~/mvp6-env/q185/treeA/src)  = base-only baseline, 14,572
tree B = A + S&OP UI draft overlay v3 716e7c4c (34 module files) + L6 env integration (~/mvp6-env/q185/treeB/src)
L6 = the draft's _shared-integration items applied to THIS environment copy only (README: "For the isolated environment only (Q84b)").
Staging: ~/mvp6-env/q185/stage/<layer>/ (kept). Copies use `cp -p`; clones `cp -c -Rp`. Any mismatch -> exit 2, nothing removed.
Writes ~/mvp6-env/q185/out/COMPOSE.tsv and COMPOSE-LOG.txt."""
import csv, hashlib, io, json, os, pathlib, re, subprocess, sys, tarfile
import os as _os
H = pathlib.Path.home() / "mvp6-env"; QS = H / "q185"; Q = QS / _os.environ.get("Q84B_ROOT", "."); OUT = Q / "out"
R = pathlib.Path("/Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09")
LOG = []; ROWS = []
def log(m): LOG.append(m); print(m, flush=True)
def flush():
    OUT.mkdir(exist_ok=True)
    (OUT / "COMPOSE-LOG.txt").write_text("\n".join(LOG) + "\n")
    (OUT / "COMPOSE.tsv").write_text("step\tlayer\tpath\taction\tbefore_sha256\tafter_sha256\texpected_before\texpected_after\tresult\n" + "".join("\t".join(r) + "\n" for r in ROWS))
def stop(m): log("STOP: " + m); flush(); sys.exit(2)
def sha(p): return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def run(*a): subprocess.run(a, check=True)
def row(step, layer, path, action, b, a, eb, ea, res): ROWS.append([step, layer, path, action, b or "-", a or "-", eb or "-", ea or "-", res])

ARCH = {"q117": (R / "mvp6-q117-test-guard-fixes-01/q117-fixes-overlay.tar.gz", "83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d"),
        "q121": (R / "mvp6-q121-mongo-guard-fixes-01/q121-mongo-guard-fixes-overlay.tar.gz", "93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90"),
        "q131": (R / "mvp6-q131a-port-uri-overlay-01/overlay-q131a.tar", "4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf"),
        "sop-v1": (R / "mvp6-sop-ui-draft-01/sop-ui-draft-overlay.tar.gz", "fcf52d827914dc9c1d217b191a416821a024ba1e3314148f488ebbb2c81e5e30"),
        "sop-v2": (R / "mvp6-sop-ui-draft-02/sop-ui-draft-overlay-v2.tar.gz", "7a9666c7ee1dfab4f85c1324db38ab06ed87ed1c385de706629e60ed68e394e3"),
        "sop-v3": (R / "mvp6-sop-ui-draft-03/sop-ui-draft-overlay-v3.tar.gz", "716e7c4c6325d26e8711c9b401d12fb61594f3eb310d59c280f3307d64904332")}
P = "services/Diten.Platform/tests/Diten.Platform.Application.Tests/"
EXPECT = {
 "q117": {r["path"]: (r["base_sha256"], r["overlay_sha256"]) for r in csv.DictReader(open(R / "mvp6-q117-test-guard-fixes-01/OVERLAY-MANIFEST.tsv"), delimiter="\t")},
 "q121": {P + "Audit/PpmAuditRetentionPolicySeedMongoTests.cs": ("c22f0cdab58c2b229fa529c1cdb466d876a99a68abb1cae40952a8f1d4acbc46", "ea6fa3623810de341bb5b4fe2e3de9bf62a6cbce5557aa0dd623ba243238aa76"),
          P + "Persistence/DisposableStandaloneMongo.cs": ("0fb06a1d020ada8deaaaf0d702fe2007ac89d2104e585d1f2bca3868c9cb00ef", "4aaec42c9919961b08005eb9cd142e5dd7b82d651a5bf65c78acbce2f987fc48")},
 "q131": {r["path"]: (r["preimage_sha256"], r["postimage_sha256"]) for r in csv.DictReader(open(R / "mvp6-q131a-port-uri-overlay-01/OVERLAY-MANIFEST.tsv"), delimiter="\t")},
}
BASEMAN = {r[0]: r[1] for r in list(csv.reader(open(H / "base/BASE-MANIFEST.tsv"), delimiter="\t"))[1:]}
FILEPLAN3 = {r["path"]: (r["v2_sha256"], r["v3_sha256"]) for r in csv.DictReader(open(R / "mvp6-sop-ui-draft-03/FILE-PLAN.tsv"), delimiter="\t")}
FILEPLAN = {r["path"]: (r["v1_sha256"], r["v2_sha256"]) for r in csv.DictReader(open(R / "mvp6-sop-ui-draft-02/FILE-PLAN.tsv"), delimiter="\t")}

def extract(layer):
    arch, h = ARCH[layer]
    if sha(arch) != h: stop(f"{layer} archive sha256 mismatch")
    st = QS / "stage" / layer; st.mkdir(parents=True, exist_ok=True)
    fresh = not any(st.iterdir())
    with tarfile.open(arch) as t:
        mem = t.getmembers()
        for m in mem:
            if m.issym() or m.islnk() or m.name.startswith("/") or ".." in m.name.split("/"): stop(f"{layer} unsafe member {m.name}")
        apple = [m.name for m in mem if os.path.basename(m.name).startswith("._")]
        files = [m for m in mem if m.isfile() and not os.path.basename(m.name).startswith("._")]
        if fresh: t.extractall(st, members=files)
        # staged bytes must equal the archive members (also when the stage was extracted earlier in this lane)
        for m in files:
            if hashlib.sha256(t.extractfile(m).read()).hexdigest() != sha(st / m.name): stop(f"{layer} staged file != archive member {m.name}")
    staged = sorted(str(p.relative_to(st)) for p in st.rglob("*") if p.is_file())
    if set(staged) != {m.name for m in files}: stop(f"{layer} stage has extra/missing files")
    log(f"{layer}: archive {h[:12]} OK; {len(files)} file members; AppleDouble skipped {len(apple)}; stage {'extracted' if fresh else 're-verified (extracted earlier)'}")
    return st, staged

def apply(layer, src, expected_tree):
    st, files = extract(layer)
    exp = EXPECT[layer]
    if set(files) != set(exp): stop(f"{layer} member list != declared list: extra {sorted(set(files)-set(exp))[:3]} missing {sorted(set(exp)-set(files))[:3]}")
    added = 0
    for p in files:
        pre, post = exp[p]
        if sha(st / p) != post: stop(f"{layer} member sha256 mismatch {p}")
        tgt = src / p
        now = sha(tgt) if tgt.exists() else None
        if pre in ("-", "", None):
            if tgt.exists(): stop(f"{layer} NEW file already present {p}")
            added += 1; act = "add"
        elif now != pre: stop(f"{layer} preimage mismatch {p}: tree {(now or 'absent')[:12]} != declared {pre[:12]}")
        else: act = "replace"
        tgt.parent.mkdir(parents=True, exist_ok=True); run("cp", "-p", str(st / p), str(tgt))
        if sha(tgt) != post: stop(f"{layer} copy mismatch {p}")
        expected_tree[p] = post
        row("compose-A", layer, p, act, now, post, None if pre in ("-", "") else pre, post, "OK")
    log(f"{layer}: {len(files)} members = declared list; preimages OK; {len(files)-added} replaced, {added} added")

def verify(src, expected_tree, label, step):
    seen = 0; bad = []; extra = []
    for d, _, fs in os.walk(src):
        for f in fs:
            p = os.path.relpath(os.path.join(d, f), src); seen += 1
            if p not in expected_tree: extra.append(p)
            elif sha(os.path.join(d, f)) != expected_tree[p]: bad.append(p)
    missing = len(expected_tree) - (seen - len(extra))
    log(f"{label}: files {seen}; expected {len(expected_tree)}; mismatches {len(bad)} {bad[:3]}; extra {len(extra)} {extra[:3]}; missing {missing}")
    row(step, "verify", str(src), "tree-verify", None, None, str(len(expected_tree)), str(seen), "OK" if not (bad or extra or missing) else f"FAIL mism={len(bad)} extra={len(extra)} missing={missing}")
    if bad or extra or missing: stop(f"{label} verification failed")
    return seen

def sop(B, exp):
    s1, f1 = extract("sop-v1"); sv2, fv2 = extract("sop-v2"); s2, f2 = extract("sop-v3")
    if not (set(f1) == set(fv2) == set(f2)): stop("sop v1/v2/v3 file sets differ")
    # v2 FILE-PLAN (v1->v2), as Q84b: v1 member = v1_sha256, v2 member = v2_sha256; and the v3 member of that path = v2_sha256
    # unless the v3 FILE-PLAN changes it. v3 FILE-PLAN (v2->v3): v2 member = v2_sha256, v3 member = v3_sha256. Every other
    # v3 file must be byte-equal to v2.
    for p in f2:
        a1, a2, a3 = sha(s1 / p), sha(sv2 / p), sha(s2 / p)
        if p in FILEPLAN:
            v1, v2 = FILEPLAN[p]
            if a1 != v1 or a2 != v2: stop(f"v2 FILE-PLAN hash mismatch {p}")
            row("overlay-fileplan-v2", "sop-v2", p, "v2 fileplan-row (v1->v2)", a1, a2, v1, v2, "OK")
        elif a1 != a2: stop(f"file changed v1->v2 but not in the v2 FILE-PLAN: {p}")
        if p in FILEPLAN3:
            w2, w3 = FILEPLAN3[p]
            if a2 != w2: stop(f"v3 FILE-PLAN before-hash (v2 member) mismatch {p}")
            if a3 != w3: stop(f"v3 FILE-PLAN after-hash (v3 member) mismatch {p}")
            row("overlay-fileplan-v3", "sop-v3", p, "v3 fileplan-row (v2->v3)", a2, a3, w2, w3, "OK")
        elif a2 != a3: stop(f"file changed v2->v3 but not in the v3 FILE-PLAN: {p}")
    if not set(FILEPLAN) <= set(f2) or not set(FILEPLAN3) <= set(f2): stop("FILE-PLAN row not in archive")
    log(f"sop FILE-PLAN v2: {len(FILEPLAN)}/{len(FILEPLAN)} rows exact; FILE-PLAN v3: {len(FILEPLAN3)}/{len(FILEPLAN3)} rows v2 member = v2_sha256 and v3 member = v3_sha256; other {len(f2)-len(FILEPLAN3)} v3 files byte-equal v2")
    mod = [p for p in f2 if not p.startswith("overlay/_shared-integration/")]
    for p in mod:
        rel = p[len("overlay/"):]
        tgt = B / rel
        if tgt.exists(): stop(f"module file would overwrite a tree path {rel} (expected absent)")
        tgt.parent.mkdir(parents=True, exist_ok=True); run("cp", "-p", str(s2 / p), str(tgt))
        post = sha(s2 / p)
        if sha(tgt) != post: stop(f"copy mismatch {rel}")
        exp[rel] = post
        fp = FILEPLAN.get(p)
        row("overlay-apply", "sop-v3", rel, "add", None, post, "absent (new in tree)", fp[1] if fp else post, "OK" + (" FILE-PLAN row" if fp else ""))
    log(f"sop-v3 module files: {len(mod)} added (all new tree paths; tree before = absent); _shared-integration {len(f2)-len(mod)} files not copied as files")
    return s2

def l6(B, exp, s2):
    si = s2 / "overlay/_shared-integration"
    # (a) provider line
    pc = "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs"
    b = sha(B / pc); txt = (B / pc).read_text()
    line = "builder.Services.AddSingleton<IModuleManifestProvider, SopWorkflowSignoffsManifestProvider>();"
    anchor = "builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();"
    if line in txt: stop("provider line already present")
    if txt.count(anchor) != 1 or "using Diten.SupplyChainService.Api.ModuleRegistration;" not in txt: stop("Program.cs anchor/using not found exactly once")
    (B / pc).write_text(txt.replace(anchor, anchor + "\n" + line, 1)); exp[pc] = sha(B / pc)
    row("L6-env", "sop-v3 _shared-integration", pc, "insert provider line", b, exp[pc], BASEMAN.get(pc), None, "OK")
    # (b) ocelot routes
    oc = "gateway/Diten.ApiGateway/ocelot.json"; b = sha(B / oc)
    frag = json.loads((si / "gateway-sandop-routes.ocelot-fragment.json").read_text())
    ocj = json.loads((B / oc).read_text()); n0 = len(ocj["Routes"])
    ups = {r.get("UpstreamPathTemplate") for r in ocj["Routes"]}
    for r in frag["Routes"]:
        if r["UpstreamPathTemplate"] in ups: stop(f"route already present {r['UpstreamPathTemplate']}")
        ocj["Routes"].append(r)
    (B / oc).write_text(json.dumps(ocj, indent=2, ensure_ascii=False) + "\n"); exp[oc] = sha(B / oc)
    row("L6-env", "sop-v3 _shared-integration", oc, f"append {len(frag['Routes'])} routes ({n0}->{len(ocj['Routes'])})", b, exp[oc], BASEMAN.get(oc), None, "OK")
    # (c) nav keys x7
    for lang in ("en", "tr", "fr", "es", "zh", "ar", "ru"):
        rx = f"frontend/Diten.Web/Resources/SharedResource.{lang}.resx"; b = sha(B / rx)
        fr = (si / f"sharedresource-nav-keys/SharedResource.{lang}.resx.fragment.xml").read_text(encoding="utf-8")
        rows = re.findall(r"<data name=\"[^\"]+\" xml:space=\"preserve\"><value>.*?</value></data>", fr, re.S)
        if len(rows) != 2: stop(f"fragment {lang} rows {len(rows)}")
        raw = (B / rx).read_bytes(); bom = raw.startswith(b"\xef\xbb\xbf"); t = raw.decode("utf-8-sig" if bom else "utf-8")
        for rrow in rows:
            key = re.search(r'name="([^"]+)"', rrow).group(1)
            if f'name="{key}"' in t: stop(f"{rx} already has {key}")
        if t.count("</root>") != 1: stop(f"{rx} </root> count")
        t = t.replace("</root>", "".join("  " + x + "\n" for x in rows) + "</root>")
        (B / rx).write_bytes((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8")); exp[rx] = sha(B / rx)
        row("L6-env", "sop-v3 _shared-integration", rx, "insert 2 nav keys", b, exp[rx], BASEMAN.get(rx), None, "OK")
    log("L6 env integration (environment copy only): Program.cs provider line; 4 ocelot routes; 2 nav keys x 7 resx. Not applied: icon-map proposal, platform checklist, frontend Program.cs note (no change)")

def main():
    for d in ("treeA", "treeB"):
        if (Q / d).exists(): stop(f"{d}/ exists (use a new folder)")
    exp = dict(BASEMAN)
    A = Q / "treeA" / "src"; A.parent.mkdir(parents=True)
    run("cp", "-c", "-Rp", str(H / "base/src"), str(A)); run("chmod", "-R", "u+w", str(A))
    log("A: BASE cloned (cp -c -Rp) from ~/mvp6-env/base/src and made writable on the copy only")
    verify(A, exp, "BASE copy (vs BASE-MANIFEST a8a236de)", "compose-A")
    apply("q117", A, exp); apply("q121", A, exp); apply("q131", A, exp)
    n = verify(A, exp, "tree A = BASE-STACK v2 (BASE→Q117→Q121→Q131)", "compose-A")
    if n != 14572: stop(f"tree A file count {n} != 14572")
    run("chmod", "-R", "a-w", str(A))
    B = Q / "treeB" / "src"; B.parent.mkdir(parents=True)
    run("cp", "-c", "-Rp", str(A), str(B)); run("chmod", "-R", "u+w", str(B))
    s2 = sop(B, exp); l6(B, exp, s2)
    nb = verify(B, exp, "tree B = BASE-STACK v2 + S&OP overlay v3 + L6 env", "compose-B")
    run("chmod", "-R", "a-w", str(B))
    OUT.mkdir(exist_ok=True); (OUT / "TREE-B-MANIFEST.tsv").write_text("path\tsha256\n" + "".join(f"{p}\t{exp[p]}\n" for p in sorted(exp)))
    log(f"DONE: A {n} files, B {nb} files; both set read-only; builds run in clones")
    flush()
main()
