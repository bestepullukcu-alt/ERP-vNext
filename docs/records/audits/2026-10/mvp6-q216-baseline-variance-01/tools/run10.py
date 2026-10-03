#!/usr/bin/env python3
"""Q216 driver: N full-suite runs back to back, no build, no code change. Writes only under its own folder.
Per-test hang cap: --blame-hang-timeout 180s. Per-run cap: 20 minutes (the whole process group is killed).
Stops if free disk drops below 2 GiB or another build/test process appears."""
import os, subprocess, sys, time, signal, shutil, json
S = os.path.dirname(os.path.abspath(__file__)); N = int(sys.argv[1]) if len(sys.argv) > 1 else 10
REPO = "/Users/natig/Projects/ERP-vNext-recovery"
P = "services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests"
env = dict(os.environ)
for l in open(S + "/env-seven.sh"):
    k, v = l.strip()[len("export "):].split("=", 1); env[k] = v.replace("\\", "")
def free(): return shutil.disk_usage("/").free / 2**30
def now(): return time.strftime("%Y-%m-%d %H:%M:%S")
log = open(S + "/runs.tsv", "a")
if log.tell() == 0: log.write("run\tstart_local\tend_local\tseconds\texit\tcapped\tfree_gib_before\tfree_gib_after\tsummary\n")
for i in range(1, N + 1):
    f0 = free()
    if f0 < 2.0: print("STOP disk", f0); break
    others = subprocess.run("pgrep -fl 'testhost|vstest' || true", shell=True, capture_output=True, text=True).stdout.strip()
    if others: print("STOP other test process:", others); break
    tag = f"run{i:02d}"; t0 = time.time(); a = now()
    cmd = ["dotnet", "test", P, "--no-build", "--blame-hang", "--blame-hang-timeout", "180s", "--logger", f"trx;LogFileName={tag}.trx", "--results-directory", f"{S}/results/{tag}"]
    out = open(f"{S}/logs/{tag}.log", "w"); capped = "no"
    p = subprocess.Popen(cmd, cwd=REPO, env=env, stdout=out, stderr=subprocess.STDOUT, start_new_session=True)
    try: rc = p.wait(timeout=1200)
    except subprocess.TimeoutExpired:
        os.killpg(p.pid, signal.SIGKILL); rc = -9; capped = "RUN-CAP-20MIN"
    out.close()
    summ = [l.strip() for l in open(f"{S}/logs/{tag}.log", errors="replace") if l.startswith(("Passed!", "Failed!"))]
    log.write("\t".join([tag, a, now(), f"{time.time()-t0:.0f}", str(rc), capped, f"{f0:.2f}", f"{free():.2f}", (summ[-1].split(" - Diten")[0] if summ else "NO SUMMARY LINE")]) + "\n"); log.flush()
    print(tag, rc, summ[-1][:90] if summ else "no summary", flush=True)
print("DONE", flush=True)
