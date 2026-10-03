#!/usr/bin/env python3
"""A12 runtime VER-02: BUILD-INPUT-MANIFEST.tsv (separate from the owned 360 manifest).

Walks the ProjectReference closure of the six runtime entry projects inside the disposable source, fails on any
missing referenced project, and lists every file under each project directory (bin/obj excluded) plus any
Directory.Build.*/Directory.Packages.props/NuGet.config/global.json found on the way to the source root.
origin = a12-360-overlay | auth-22-overlay-archive | head-archive, from the composed layers.
Usage: build_inputs.py SOURCE_DIR REPO OUT_TSV
"""
import csv, hashlib, os, pathlib, re, sys, tarfile

SRC = pathlib.Path(sys.argv[1]).resolve(); REPO = sys.argv[2]; OUT = sys.argv[3]
ENTRIES = [
    "services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj",
    "services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj",
    "services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj",
    "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj",
    "gateway/Diten.ApiGateway/Diten.ApiGateway.csproj",
    "frontend/Diten.Web/Diten.Web.csproj",
]
A = REPO + "/docs/records/audits/2026-09/"


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def names(arc, strip=""):
    with tarfile.open(A + arc) as t:
        out = set()
        for m in t.getmembers():
            n = m.name[2:] if m.name.startswith("./") else m.name
            if m.isfile() and not os.path.basename(n).startswith("._"):
                out.add(n[len(strip):] if strip and n.startswith(strip) else n)
        return out


a12 = names("mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz", "mvp6-shipment-a12-rework-src/")
auth = names("mvp6-carrier-numericdate-exec-01/final-source.tar.gz")
a12_owned = {r.split("\t")[0] for r in open(A + "mvp6-shipment-a12-safe404-rework-01/SUCCESSOR-360-SOURCE-MANIFEST.tsv") if r.strip()}

seen, missing, queue = {}, [], list(ENTRIES)
while queue:
    rel = queue.pop()
    if rel in seen:
        continue
    p = SRC / rel
    if not p.is_file():
        missing.append(rel); seen[rel] = None; continue
    seen[rel] = p
    for ref in re.findall(r'<ProjectReference\s+Include="([^"]+)"', p.read_text(encoding="utf-8-sig")):
        tgt = (p.parent / ref.replace("\\", "/")).resolve()
        try:
            queue.append(tgt.relative_to(SRC).as_posix())
        except ValueError:
            missing.append(f"{rel} -> {ref} (outside source root)")

files = set()
for rel, p in seen.items():
    if p is None:
        continue
    d = p.parent
    for f in d.rglob("*"):
        parts = f.relative_to(d).parts
        if f.is_file() and not ({"bin", "obj"} & set(parts)):
            files.add(f.relative_to(SRC).as_posix())
    cur = d
    while True:
        for n in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.config", "nuget.config", "global.json"):
            if (cur / n).is_file():
                files.add((cur / n).relative_to(SRC).as_posix())
        if cur == SRC:
            break
        cur = cur.parent
files.add("gateway/Diten.ApiGateway/ocelot.json")

with open(OUT, "w") as f:
    f.write("path\tsha256\tbytes\torigin\trole\tin_owned_360_manifest\n")
    for rel in sorted(files):
        p = SRC / rel
        origin = "a12-360-overlay" if rel in a12 else ("auth-22-overlay-archive" if rel in auth else "head-archive-4a8d4d4")
        role = "project" if rel in seen else ("build-config" if os.path.basename(rel) in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.config", "nuget.config", "global.json") else "source")
        f.write(f"{rel}\t{sha(p)}\t{p.stat().st_size}\t{origin}\t{role}\t{'yes' if rel in a12_owned else 'no'}\n")
    for m in missing:
        f.write(f"#MISSING\t{m}\n")
projs = sorted(r for r, p in seen.items() if p)
print(f"entry projects {len(ENTRIES)}; project closure {len(projs)}; files {len(files)}; missing {len(missing)}")
for r in projs:
    print("  project", r)
for m in missing:
    print("  MISSING", m)
sys.exit(1 if missing else 0)
