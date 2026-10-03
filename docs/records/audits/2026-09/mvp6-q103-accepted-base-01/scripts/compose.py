#!/usr/bin/env python3
"""Q103 compose: git archive HEAD -> BC-SOURCE -> A12-360 -> Auth 22, into ~/mvp6-env/base/src."""
import hashlib, os, shutil, subprocess, sys, tarfile, csv, json

REPO = "/Users/natig/Projects/ERP-vNext-recovery"
BASE = os.path.expanduser("~/mvp6-env/base")
SRC = os.path.join(BASE, "src")
WORK = os.path.join(BASE, "work")
R = "docs/records/audits/2026-09/"
INPUTS = [
  ("bc",    R+"mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz", "ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064"),
  ("bcm",   R+"mvp6-bc-successor-exec-02/SOURCE-MANIFEST.tsv", "dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634"),
  ("a12",   R+"mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz", "7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d"),
  ("a12m",  R+"mvp6-shipment-a12-safe404-rework-01/SUCCESSOR-360-SOURCE-MANIFEST.tsv", "8ffa6c96d29f8004940f310ed704a2c05eaa38ffa501d6114a5f9251e6e00d36"),
  ("auth",  R+"mvp6-carrier-numericdate-exec-01/final-source.tar.gz", "f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd"),
  ("authm", R+"mvp6-carrier-numericdate-exec-01/FINAL-22-SOURCE-MANIFEST.tsv", "b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731"),
]
HEAD = "4a8d4d4b339528a88e6220fb8402e5a2c771136c"
log = open(os.path.join(BASE, "logs", "COMPOSE-LOG.txt"), "w")
def L(*a):
    s = " ".join(str(x) for x in a); print(s); log.write(s + "\n"); log.flush()
def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for b in iter(lambda: f.read(1 << 20), b""): h.update(b)
    return h.hexdigest()
def die(m): L("STOP", m); sys.exit(2)

env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
br = subprocess.check_output(["git", "-C", REPO, "rev-parse", "--abbrev-ref", "HEAD"], env=env, text=True).strip()
hd = subprocess.check_output(["git", "-C", REPO, "rev-parse", "HEAD"], env=env, text=True).strip()
if br != "feature/mvp6-logistics" or hd != HEAD: die(f"branch/HEAD mismatch {br}@{hd}")
L("repo", f"{br}@{hd}", "OK")
paths = {}
for k, p, h in INPUTS:
    got = sha(os.path.join(REPO, p))
    if got != h: die(f"input {k} {p} sha256 {got} != {h}")
    paths[k] = os.path.join(REPO, p); L("input", k, p, "sha256", got, "OK")

if os.path.exists(SRC): die("src already exists; refusing to compose over it")
os.makedirs(SRC)
layer = {}      # path -> layer name
history = {}    # path -> list of layers

# L0 git archive HEAD
ht = os.path.join(WORK, "head.tar")
with open(ht, "wb") as f:
    subprocess.check_call(["git", "-C", REPO, "archive", "--format=tar", HEAD], stdout=f, env=env)
L("L0 head.tar sha256", sha(ht))
with tarfile.open(ht) as t:
    mem = [m for m in t.getmembers()]
    t.extractall(SRC, members=mem)
    n0 = 0
    for m in mem:
        if m.isfile() or m.issym():
            layer[m.name] = "L0-HEAD"; history[m.name] = ["L0-HEAD"]; n0 += 1
L("L0 HEAD files", n0)

def apply(name, archive, strip=None):
    tmp = os.path.join(WORK, "x-" + name)
    if os.path.exists(tmp): die(f"temp dir {tmp} exists")
    os.makedirs(tmp)
    with tarfile.open(archive) as t:
        mem = t.getmembers()
        ad = [m for m in mem if os.path.basename(m.name).startswith("._")]
        mem = [m for m in mem if m not in ad]
        L(f"  {name}: AppleDouble metadata members skipped", len(ad))
        for m in ad: L("    skipped", m.name, m.size)
        for m in mem:
            if os.path.isabs(m.name) or ".." in m.name.split("/"): die(f"{name}: unsafe member {m.name}")
            if m.issym() or m.islnk(): die(f"{name}: link member {m.name}")
        t.extractall(tmp, members=mem)
    root = os.path.join(tmp, strip) if strip else tmp
    applied = []
    for d, _, fs in os.walk(root):
        for fn in fs:
            full = os.path.join(d, fn); rel = os.path.relpath(full, root)
            dst = os.path.join(SRC, rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(full, dst)
            history.setdefault(rel, []).append(name); layer[rel] = name
            applied.append(rel)
    shutil.rmtree(tmp)
    return sorted(applied)

def read_manifest(p):
    with open(p, newline="") as f:
        return list(csv.DictReader(f, delimiter="\t"))

# L1 BC-SOURCE
bc = apply("L1-BC-SOURCE", paths["bc"])
bcm = read_manifest(paths["bcm"])
ok = sum(1 for r in bcm if os.path.exists(os.path.join(SRC, r["path"])) and sha(os.path.join(SRC, r["path"])) == r["sha256"])
L("L1 BC files", len(bc), "; manifest", f"{ok}/{len(bcm)}", "; new vs HEAD", sum(1 for p in bc if history[p][0] == "L1-BC-SOURCE"))
if ok != len(bcm) or len(bc) != len(bcm): die("BC manifest mismatch")

# L2 A12-360
a12 = apply("L2-A12-360", paths["a12"], strip="mvp6-shipment-a12-rework-src")
with open(paths["a12m"], newline="") as f:  # headerless: path, sha256, bytes
    a12m = list(csv.DictReader(f, fieldnames=["path", "sha256", "bytes"], delimiter="\t"))
ok = sum(1 for r in a12m if sha(os.path.join(SRC, r["path"])) == r["sha256"])
L("L2 A12 files", len(a12), "; manifest", f"{ok}/{len(a12m)}", "; overlap with BC", len(set(a12) & set(bc)))
if len(a12) != 360 or ok != 360: die("A12 manifest mismatch")

# L3 Auth 22
au = apply("L3-AUTH22-archive", paths["auth"])
aum = read_manifest(paths["authm"])
ok = sum(1 for r in aum if sha(os.path.join(SRC, r["path"])) == r["target_sha256"])
ov12 = sorted(set(au) & set(a12)); ovbc = sorted(set(au) & set(bc))
L("L3 Auth files", len(au), "; FINAL-22", f"{ok}/{len(aum)}", "; overlap with A12", len(ov12), "; overlap with BC", len(ovbc))
if ok != len(aum): die("Auth FINAL-22 mismatch")
# Auth overlaps with BC are allowed only if identical content
for p in ovbc:
    L("  L3 overlaps BC path", p)

# Post-compose re-check of the 360 target (Auth must not clobber A12)
ok360 = sum(1 for r in a12m if sha(os.path.join(SRC, r["path"])) == r["sha256"])
L("FINAL 360 check", f"{ok360}/360")
pc = "services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs"
L("FINAL Program.cs", sha(os.path.join(SRC, pc)))
with open(os.path.join(BASE, "logs", "LAYERS.tsv"), "w") as f:
    f.write("path\tfinal_layer\thistory\n")
    for p in sorted(layer): f.write(f"{p}\t{layer[p]}\t{'>'.join(history[p])}\n")
L("COMPOSE", "PASS" if ok360 == 360 else "FAIL", "files", len(layer))
