#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K09: source -> binary -> process -> browser binding.

Captured while the lane is still running (before K11). One row per component:
  source   : SOURCE-TREE-MANIFEST.tsv sha256 (K01) + the component's project path
  binary   : DLL sha256 recorded at build (K06 raw/binary-sha256.txt) == DLL sha256 of the running file now
  process  : PID from K06 is STILL the listener on the lane port now; command/cwd/runtime from K06 raw/processes.tsv
  browser  : (Web only) each --served asset fetched from the lane Web port is byte-identical to its source file,
             and each --browser-url the operator/agent actually used points at the lane Web origin.
Usage:
  k09_binding.py --work W --evidence E [--served "/js/x.js=frontend/Diten.Web/wwwroot/js/x.js" ...]
                 [--browser-url "http://127.0.0.1:5901/Shipments/Details/..." ...]
Writes E/SOURCE-BINARY-PROCESS.tsv and E/raw/browser-binding.tsv. Exit 1 on any broken link.
"""
import argparse, csv, hashlib, json, pathlib, subprocess, sys, urllib.request
from urllib.parse import urlparse


def sha_bytes(b):
    return hashlib.sha256(b).hexdigest()


def sha_file(p):
    return sha_bytes(pathlib.Path(p).read_bytes())


def listener(port):
    r = subprocess.run(["lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN", "-t"], capture_output=True, text=True)
    return r.stdout.split()[0] if r.stdout.split() else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--work", required=True)
    ap.add_argument("--evidence", required=True)
    ap.add_argument("--served", nargs="*", default=[])
    ap.add_argument("--browser-url", nargs="*", default=[])
    a = ap.parse_args()
    ev = pathlib.Path(a.evidence); raw = ev / "raw"; work = pathlib.Path(a.work)
    ports = json.loads((work / "ports.json").read_text())["ports"]
    tree = raw / "SOURCE-TREE-MANIFEST.tsv"
    tree_sha = sha_file(tree)
    src_hash = {r["path"]: r["sha256"] for r in csv.DictReader(open(tree, newline=""), delimiter="\t")}
    built = {}
    for line in (raw / "binary-sha256.txt").read_text().splitlines():
        h, p = line.split(None, 1); built[pathlib.Path(p).name] = h
    procs = list(csv.DictReader(open(raw / "processes.tsv", newline=""), delimiter="\t"))

    rows, bad = [], []
    for pr in procs:
        dll = pathlib.Path(pr["dll"]); now = sha_file(dll)
        live = listener(pr["port"])
        ok_bin = built.get(dll.name) == now
        ok_proc = live == pr["pid"]
        rows.append([pr["service"], tree_sha, now, "match" if ok_bin else "MISMATCH", pr["pid"], pr["port"],
                     "listening" if ok_proc else f"listener={live}", pr["loaded_runtime"], pr["cwd"], pr["health_http"]])
        if not (ok_bin and ok_proc):
            bad.append(pr["service"])
    mongo_pid = listener(ports["mongo"])
    rows.append(["mongod", "-", "-", "-", mongo_pid or "none", ports["mongo"], "listening" if mongo_pid else "NOT LISTENING", "-", "-", "-"])

    with open(ev / "SOURCE-BINARY-PROCESS.tsv", "w") as f:
        f.write("component\tsource_tree_manifest_sha256\tbinary_sha256_now\tbinary_vs_build\tpid\tport\tprocess_state\tloaded_runtime\tcwd\thealth_http\n")
        for r in rows:
            f.write("\t".join(map(str, r)) + "\n")

    web = ports.get("web")
    b_rows = []
    for item in a.served:
        path, rel = item.split("=", 1)
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{web}{path}", timeout=10) as r:
                got = sha_bytes(r.read()); status = r.status
        except Exception as e:  # noqa: BLE001 — recorded, then fails the binding
            got, status = f"error:{type(e).__name__}", 0
        want = src_hash.get(rel, "not-in-source-manifest")
        ok = got == want
        b_rows.append(["served-asset", path, rel, status, got, want, "match" if ok else "MISMATCH"])
        if not ok:
            bad.append(f"served:{path}")
    for url in a.browser_url:
        u = urlparse(url)
        ok = u.hostname == "127.0.0.1" and u.port == web
        b_rows.append(["browser-url", url, "-", "-", f"{u.hostname}:{u.port}", f"127.0.0.1:{web}", "match" if ok else "MISMATCH"])
        if not ok:
            bad.append(f"browser:{url}")
    with open(raw / "browser-binding.tsv", "w") as f:
        f.write("kind\turl_or_path\tsource_path\thttp\tobserved\texpected\tresult\n")
        for r in b_rows:
            f.write("\t".join(map(str, r)) + "\n")
    if bad:
        print("K09 FAIL:", ", ".join(bad)); sys.exit(1)
    print(f"K09 PASS: {len(procs)} processes bound to source tree {tree_sha[:16]}…; {len(b_rows)} browser binding row(s)")


if __name__ == "__main__":
    main()
