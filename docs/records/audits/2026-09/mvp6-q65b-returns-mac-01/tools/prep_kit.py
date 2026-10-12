#!/usr/bin/env python3
"""Q65b kit inputs (copied from Q84b prep_kit.py; paths adapted): kit K01 = HEAD archive + BC-SOURCE + A12-360 + Auth-22 (= BASE a8a236de) + ONE derived overlay
D = every tree-B path whose bytes differ from BASE-MANIFEST or that BASE lacks (Q117 + Q121 + Q131 + Returns v2 module + L6 env).
declared_overlaps for rows 1-3 = D ∩ that row's manifest paths, so K01's final check allows exactly those replacements.
Writes ~/mvp6-env/q65b/lane/{overlays.tsv, inputs/q65b-derived.tar.gz, inputs/q65b-derived-MANIFEST.tsv}. Never overwrites."""
import csv, gzip, hashlib, io, pathlib, sys, tarfile
H = pathlib.Path.home() / "mvp6-env"; Q = H / "q65b"; LANE = Q / "lane"; INP = LANE / "inputs"
R = "docs/records/audits/2026-09/"; REPO = pathlib.Path("/Users/natig/Projects/ERP-vNext-recovery")
B = Q / "treeB/src"
def sha(p): return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
base = {r[0]: r[1] for r in list(csv.reader(open(H / "base/BASE-MANIFEST.tsv"), delimiter="\t"))[1:]}
treeb = {r["path"]: r["sha256"] for r in csv.DictReader(open(Q / "out/TREE-B-MANIFEST.tsv"), delimiter="\t")}
if set(base) - set(treeb): sys.exit("tree B lacks a BASE path")
D = sorted(p for p in treeb if base.get(p) != treeb[p])
for p in D:
    if sha(B / p) != treeb[p]: sys.exit(f"tree B file != manifest {p}")
INP.mkdir(parents=True, exist_ok=True)
tar = INP / "q65b-derived.tar.gz"; man = INP / "q65b-derived-MANIFEST.tsv"
if tar.exists() or man.exists() or (LANE / "overlays.tsv").exists(): sys.exit("inputs exist (no overwrite)")
buf = io.BytesIO()
with tarfile.open(fileobj=buf, mode="w", format=tarfile.PAX_FORMAT) as t:
    for p in D:
        data = (B / p).read_bytes(); ti = tarfile.TarInfo(p); ti.size = len(data); ti.mtime = 1790000000
        ti.mode = (B / p).stat().st_mode & 0o755 | 0o644; ti.uid = ti.gid = 0; ti.uname = "root"; ti.gname = "wheel"
        t.addfile(ti, io.BytesIO(data))
tar.write_bytes(gzip.compress(buf.getvalue(), compresslevel=9, mtime=0))
man.write_text("path\tsha256\tbytes\n" + "".join(f"{p}\t{treeb[p]}\t{(B / p).stat().st_size}\n" for p in D))
rows = [(1, R + "mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz", "ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064", R + "mvp6-bc-successor-exec-02/SOURCE-MANIFEST.tsv", "dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634"),
        (2, R + "mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz", "7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d", R + "mvp6-shipment-a12-safe404-rework-01/SUCCESSOR-360-SOURCE-MANIFEST.tsv", "8ffa6c96d29f8004940f310ed704a2c05eaa38ffa501d6114a5f9251e6e00d36"),
        (3, R + "mvp6-carrier-numericdate-exec-01/final-source.tar.gz", "f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd", R + "mvp6-carrier-numericdate-exec-01/FINAL-22-SOURCE-MANIFEST.tsv", "b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731")]
# Q64d declared overlaps between rows 1-3 themselves (unchanged)
q64d = {r["order"]: r["declared_overlaps"] for r in csv.DictReader(open(REPO / R / "mvp6-q64d-claims-runtime-01/lane-config/overlays.tsv"), delimiter="\t")}
Dset = set(D); out = ["order\tarchive\tarchive_sha256\tmanifest\tmanifest_sha256\tdeclared_overlaps"]
for o, a, ah, m, mh in rows:
    if sha(REPO / a) != ah or sha(REPO / m) != mh: sys.exit(f"sealed input mismatch row {o}")
    lines = [l.split("\t")[0] for l in (REPO / m).read_text().splitlines() if l and not l.startswith("path\t")]
    ov = sorted((set(x for x in q64d[str(o)].split(",") if x not in ("", "-"))) | (Dset & set(lines)))
    out.append(f"{o}\t{a}\t{ah}\t{m}\t{mh}\t{','.join(ov) or '-'}")
    print(f"row {o}: declared overlaps {len(ov)} (D∩manifest {len(Dset & set(lines))})")
out.append(f"4\t{tar}\t{sha(tar)}\t{man}\t{sha(man)}\t-")
(LANE / "overlays.tsv").write_text("\n".join(out) + "\n")
print(f"derived overlay: {len(D)} files; tar {sha(tar)}; manifest {sha(man)}")
