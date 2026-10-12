#!/usr/bin/env python3
"""Q129 compose (derived from Q122 compose_q122.py, parametrised: argv[1] = v3 or v4): Q103 accepted base (copy at ~/mvp6-env/q122/src)
 -> L4 Q117 overlay (q117-fixes-overlay.tar.gz 83e6322c..., extracted at q122/q117x)
 -> L5 Claims UI DRAFT v3 module files (claims-ui-draft-overlay-v3.tar.gz 66e72bf6..., extracted at q122/v3x)
 -> L6 the draft's _shared-integration items applied to THIS environment copy only (provider line, ocelot routes, nav keys x7).
Fail-closed. Never deletes anything. Writes derived kit overlays (tar.gz + manifest) to ~/mvp6-env/q122/inputs/."""
import hashlib, json, pathlib, re, sys, tarfile, io, gzip
VER = sys.argv[1]; assert VER in ("v3", "v4")
HOME = pathlib.Path.home() / "mvp6-env" / "q129" / VER; ROOT = HOME.parent
SRC = HOME / "src"; Q117 = ROOT / "q117x"; V3 = ROOT / f"{VER}x"; INP = HOME / "inputs"
LOG = []
def log(m): LOG.append(m); print(m)
def stop(m): log("STOP: " + m); (HOME / "COMPOSE-LOG.txt").write_text("\n".join(LOG) + "\n"); sys.exit(2)
def sha(p): return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()

def mk_overlay(name, paths):
    tar = INP / f"{name}.tar.gz"; man = INP / f"{name}-MANIFEST.tsv"
    if tar.exists() or man.exists(): stop(f"{name} already exists (no overwrite; use a new folder)")
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w", format=tarfile.PAX_FORMAT) as t:
        for p in sorted(paths):
            data = (SRC / p).read_bytes(); ti = tarfile.TarInfo(p); ti.size = len(data); ti.mtime = 1790000000
            ti.mode = 0o644; ti.uid = ti.gid = 0; ti.uname = "root"; ti.gname = "wheel"; t.addfile(ti, io.BytesIO(data))
    with open(tar, "wb") as f:
        f.write(gzip.compress(buf.getvalue(), compresslevel=9, mtime=0))
    man.write_text("path\tsha256\tbytes\n" + "".join(f"{p}\t{sha(SRC / p)}\t{(SRC / p).stat().st_size}\n" for p in sorted(paths)))
    log(f"derived overlay {name}: {len(paths)} files tar={sha(tar)} manifest={sha(man)}")

def main():
    ba = []  # (layer, path, before, after)
    # L4 Q117 overlay: replaces existing base files or adds new ones (recorded per file)
    q = sorted(str(p.relative_to(Q117)) for p in Q117.rglob("*") if p.is_file())
    if len(q) != 11: stop(f"Q117 overlay file count {len(q)} != 11")
    for p in q:
        before = sha(SRC / p) if (SRC / p).exists() else "-"
        (SRC / p).parent.mkdir(parents=True, exist_ok=True); (SRC / p).write_bytes((Q117 / p).read_bytes())
        if sha(SRC / p) != sha(Q117 / p): stop(f"copy mismatch {p}")
        ba.append(("L4-Q117", p, before, sha(SRC / p)))
    log(f"L4 Q117 files placed: {len(q)} (replaced {sum(1 for r in ba if r[2] != '-')}, new {sum(1 for r in ba if r[2] == '-')})")
    # L5 module files (overlay/frontend/**, overlay/services/**) — new paths only
    mod = sorted(str(p.relative_to(V3 / "overlay")) for p in (V3 / "overlay").rglob("*") if p.is_file() and not str(p.relative_to(V3 / "overlay")).startswith("_shared-integration/"))
    for p in mod:
        if (SRC / p).exists(): stop(f"module file would overwrite base path {p}")
    for p in mod:
        (SRC / p).parent.mkdir(parents=True, exist_ok=True); (SRC / p).write_bytes((V3 / "overlay" / p).read_bytes())
        if sha(SRC / p) != sha(V3 / "overlay" / p): stop(f"copy mismatch {p}")
        ba.append((f"L5-{VER}-module", p, "-", sha(SRC / p)))
    log(f"L5 module files placed: {len(mod)} (all new paths)")
    si = {str(p.relative_to(V3 / "overlay")): p.read_text(encoding="utf-8") for p in (V3 / "overlay" / "_shared-integration").rglob("*") if p.is_file()}
    w = []; before = {}
    # (a) provider line
    pc = "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs"
    before[pc] = sha(SRC / pc); txt = (SRC / pc).read_text()
    line = "builder.Services.AddSingleton<IModuleManifestProvider, ClaimsManagementManifestProvider>();"
    anchor = "builder.Services.AddSingleton<IModuleManifestProvider, ShipmentTrackingPodManifestProvider>();"
    if line in txt: stop("provider line already present")
    if txt.count(anchor) != 1 or "using Diten.SupplyChainService.Api.ModuleRegistration;" not in txt: stop("Program.cs anchor/using not found exactly once")
    (SRC / pc).write_text(txt.replace(anchor, anchor + "\n" + line, 1)); w.append(pc)
    log("L6(a) SupplyChain Program.cs: ClaimsManagementManifestProvider line after the Shipment provider line")
    # (b) ocelot routes
    oc = "gateway/Diten.ApiGateway/ocelot.json"; before[oc] = sha(SRC / oc)
    frag = json.loads(si["_shared-integration/gateway-claims-routes.ocelot-fragment.json"])
    ocj = json.loads((SRC / oc).read_text()); n0 = len(ocj["Routes"])
    ups = {r.get("UpstreamPathTemplate") for r in ocj["Routes"]}
    for c in frag["Confirm"]:
        hit = [r for r in ocj["Routes"] if r.get("UpstreamPathTemplate") == c["UpstreamPathTemplate"] and "GET" in [x.upper() for x in r.get("UpstreamHttpMethod", [])]]
        log(f"L6(b) ocelot confirm {c['UpstreamPathTemplate']} GET: {'present' if hit else 'ABSENT'}")
        if not hit: stop("confirm route absent")
    for r in frag["Routes"]:
        if r["UpstreamPathTemplate"] in ups: stop(f"route already present {r['UpstreamPathTemplate']}")
        ocj["Routes"].append(r); log(f"L6(b) ocelot route added {r['UpstreamPathTemplate']} {r.get('UpstreamHttpMethod')}")
    (SRC / oc).write_text(json.dumps(ocj, indent=2, ensure_ascii=False) + "\n"); w.append(oc)
    log(f"L6(b) ocelot routes {n0} -> {len(ocj['Routes'])}")
    # (c) nav keys x7
    for lang in ("en", "tr", "fr", "es", "zh", "ar", "ru"):
        rx = f"frontend/Diten.Web/Resources/SharedResource.{lang}.resx"; before[rx] = sha(SRC / rx)
        fr = si[f"_shared-integration/sharedresource-nav-keys/SharedResource.{lang}.resx.fragment.xml"]
        rows = re.findall(r"<data name=\"[^\"]+\" xml:space=\"preserve\"><value>.*?</value></data>", fr, re.S)
        if len(rows) != 2: stop(f"fragment {lang} rows {len(rows)}")
        raw = (SRC / rx).read_bytes(); bom = raw.startswith(b"\xef\xbb\xbf"); t = raw.decode("utf-8-sig" if bom else "utf-8")
        for rrow in rows:
            key = re.search(r'name="([^"]+)"', rrow).group(1)
            if f'name="{key}"' in t: stop(f"{rx} already has {key}")
        if t.count("</root>") != 1: stop(f"{rx} </root> count")
        t = t.replace("</root>", "".join("  " + x + "\n" for x in rows) + "</root>")
        (SRC / rx).write_bytes((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8")); w.append(rx)
    log("L6(c) SharedResource x7: Nav.Module.CLAIMSMANAGEMENT + Nav.Page.CLAIMS")
    log("L6 not applied: gateway-tests-ocelot-count.patch.txt (Gateway tests out of scope), icon-map.proposal.md, platform-registration.md, frontend-Program.cs.note.txt (notes/proposals)")
    for p in w: ba.append(("L6-env-integration", p, before[p], sha(SRC / p)))
    log(f"L6 env-integration files {len(w)}")
    (HOME / "LAYERS-BEFORE-AFTER.tsv").write_text("layer\tpath\tbefore_sha256\tafter_sha256\n" + "".join("\t".join(r) + "\n" for r in ba))
    INP.mkdir(exist_ok=True)
    mk_overlay("q117-files", q)
    mk_overlay(f"claims-{VER}-module", mod)
    mk_overlay(f"claims-{VER}-env-integration", w)
    n = sum(1 for p in SRC.rglob("*") if p.is_file()); log(f"src files after compose: {n}")
    (HOME / "COMPOSE-LOG.txt").write_text("\n".join(LOG) + "\n")
main()
