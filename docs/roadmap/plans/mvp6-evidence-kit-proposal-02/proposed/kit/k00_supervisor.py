#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1 lane supervisor — the ONLY holder of lane secrets (D4).

Origin: adapted from the A12 VER-02 run's scripts/lane_supervisor.py (docs/records/audits/2026-09/
mvp6-shipment-a12-runtime-independent-ver-02/), which replaced the v1.0 K04/K07 habit of writing secrets to files.

What it does
  * At start it generates, with secrets.token_urlsafe, one value per rotated group (--rotate) and one password per actor
    label in actors.tsv. Nothing is printed, logged or written to disk; exceptions never carry values.
  * K04 renders env files with placeholders @@LANE:<group>@@. On `launch` the supervisor reads the env file, replaces the
    placeholders in memory and starts the service with `env -i` semantics (a fixed base environment + that env file).
  * `run` starts any helper (K07 identity rotation, fixtures, harness, K08 exact scan) with LANE_SECRET_<GROUP> and
    ACTOR_PW_<LABEL> in its environment only. The helper's output goes to an attempt-suffixed log in W/logs.
  * `shutdown` clears every value and exits. The control socket lives OUTSIDE W (default $TMPDIR/ek-<lane>.sock), so K11
    can delete the workspace while the supervisor still runs the final exact-value scan (G4). After `run seal` the
    supervisor writes nothing more into E (no COMMANDS row for the seal or the shutdown): the seal report is the last write.

Control socket: one JSON request per connection (use k00_ctl.py):
  {"op":"launch","svc":S}                         start service S (K06 semantics: listener = PID, runtime, health)
  {"op":"run","label":L,"argv":[...],"cwd":D}     run a helper with the lane values in its environment
  {"op":"reveal","actor":LABEL}                   return one actor password to the CALLER'S terminal (manual browser login)
  {"op":"pids"} | {"op":"ping"} | {"op":"shutdown"}
Usage: k00_supervisor.py --work W --evidence E --map service-map.tsv --actors actors.tsv --socket S
                         [--rotate "jwt mfa auth-platform platform-mdm-active svcid"] [--dotnet D] [--expected-runtime 8.0.23]
"""
import argparse, csv, datetime, hashlib, json, os, pathlib, re, secrets, socket, subprocess, sys, time

ap = argparse.ArgumentParser()
for x in ("--work", "--evidence", "--map", "--actors", "--socket"):
    ap.add_argument(x, required=True)
ap.add_argument("--rotate", default="jwt mfa auth-platform platform-mdm-active svcid")
ap.add_argument("--dotnet", default=os.path.expanduser("~/.dotnet/dotnet"))
ap.add_argument("--expected-runtime", default="8.0.23")
A = ap.parse_args()
W = pathlib.Path(A.work); E = pathlib.Path(A.evidence); SOCK = pathlib.Path(A.socket)
if W.resolve() in SOCK.resolve().parents:
    raise SystemExit("socket must live outside the lane workspace (K11 removes W before the final seal)")

SECRETS = {g: secrets.token_urlsafe(48) for g in A.rotate.split()}
ACTOR_LABELS = [r["label"] for r in csv.DictReader(open(A.actors), delimiter="\t") if r.get("label")]
ACTORS = {lab: secrets.token_urlsafe(24) for lab in ACTOR_LABELS}
PORTS = json.loads((W / "ports.json").read_text())["ports"]
MAP = {r["service"]: r for r in csv.DictReader(open(A.map), delimiter="\t")}
PROCS = {}
BASE_ENV = {"HOME": os.environ["HOME"], "PATH": "/usr/bin:/bin:/usr/sbin:/sbin", "TMPDIR": os.environ.get("TMPDIR", "/tmp"),
            "LANG": "en_US.UTF-8", "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"}


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def one_line(s):
    return re.sub(r"[\t\r\n]+", " ", str(s))


SEALED = False  # set once the final seal scan has run: nothing may be written to E after it (G4)


def cmdrow(t0, step, desc, result):
    """G2: exactly one COMMANDS.tsv row per command; never contains a value (descriptions are fixed text).
    After the seal, no row is written: the SECRET-SCAN-FINAL report is the seal's own record (G4)."""
    if SEALED:
        return
    with open(E / "COMMANDS.tsv", "a") as f:
        f.write("\t".join([t0, now(), "SUP", one_line(step), one_line(desc), one_line(result)]) + "\n")


def logdir():
    """W/logs while the workspace exists; after K11 removed W, a folder next to the socket (outside W and E)."""
    d = W / "logs" if W.exists() else SOCK.parent / (SOCK.name + "-logs")
    d.mkdir(mode=0o700, exist_ok=True)
    return d


def unique(d, base, ext):
    n = 1
    while (d / f"{base}-a{n}.{ext}").exists():
        n += 1
    return d / f"{base}-a{n}.{ext}"


def env_name(label):
    return re.sub(r"[^A-Z0-9]", "_", label.upper())


def sha(p):
    return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()


def subst(v):
    for g, s in SECRETS.items():
        v = v.replace(f"@@LANE:{g}@@", s)
    if "@@LANE:" in v:
        raise RuntimeError("unresolved placeholder in env file (group not rotated)")
    return v


def envfile(svc):
    out = {}
    for line in (W / "env" / f"{svc}.env").read_text().splitlines():
        if line.strip() and not line.startswith("#"):
            k, v = line.split("=", 1)
            out[k] = subst(v[1:-1] if v.startswith("'") and v.endswith("'") else v)
    return out


def sh(*a):
    return subprocess.run(a, capture_output=True, text=True).stdout


def listener(port):
    return sh("lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN", "-t").split("\n")[0].strip()


def launch(svc):
    t0 = now(); row = MAP[svc]; port = PORTS[svc]
    proj = W / "source" / row["project_dir"]; out = proj / "bin/Release/net8.0"
    extra = []
    if row["content_root"] == "outdir":
        rundir, dll = out, out / row["dll"]
    elif row["content_root"] == "project":
        rundir, dll = proj, out / row["dll"]; extra = ["--contentRoot", str(proj)]
    else:
        rundir = W / "gateway-runtime"; dll = rundir / row["dll"]
    if listener(port):
        return {"ok": False, "error": f"port {port} busy"}
    env = dict(BASE_ENV); env.update(envfile(svc))
    log = open(unique(logdir(), svc, "log"), "ab")
    p = subprocess.Popen([A.dotnet, str(dll), "--urls", f"http://127.0.0.1:{port}", *extra], cwd=rundir, env=env,
                         stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    PROCS[svc] = p
    with open(W / "pids.tsv", "a") as f:
        f.write(f"{svc}\t{p.pid}\t{port}\t{W}\n")
    lp = ""
    for _ in range(240):
        lp = listener(port)
        if lp:
            break
        if p.poll() is not None:
            cmdrow(t0, f"{svc}-launch", f"supervisor launch {row['dll']} --urls http://127.0.0.1:{port}", f"exited rc={p.returncode}")
            return {"ok": False, "error": f"{svc} exited rc={p.returncode}"}
        time.sleep(0.5)
    if lp != str(p.pid):
        return {"ok": False, "error": f"listener {lp} != pid {p.pid}"}
    raw = E / "raw"
    body = unique(raw, f"health-{svc}", "body"); hdr = body.with_suffix(".headers")
    code = subprocess.run(["curl", "-s", "-o", str(body), "-D", str(hdr), "-m", "10", "-w", "%{http_code}",
                           f"http://127.0.0.1:{port}{row['health_path']}"], capture_output=True, text=True).stdout
    lsof = sh("lsof", "-nP", "-p", str(p.pid))
    m = re.search(r"Microsoft\.NETCore\.App/([0-9][0-9.]*)", lsof)
    rt = f"Microsoft.NETCore.App/{m.group(1)}" if m else ""
    cwd = sh("lsof", "-a", "-nP", "-p", str(p.pid), "-d", "cwd", "-Fn").split("\nn")[-1].strip()
    cmd = sh("ps", "-o", "command=", "-p", str(p.pid)).strip()          # argv only; never `ps e`
    lstart = " ".join(sh("ps", "-o", "lstart=", "-p", str(p.pid)).split())
    pt = raw / "processes.tsv"
    if not pt.exists():
        pt.write_text("service\tpid\tport\tstarted\tdll\tdll_sha256\tcwd\tloaded_runtime\thealth_http\tcommand\n")
    with open(pt, "a") as f:
        f.write("\t".join(map(one_line, [svc, p.pid, port, lstart, dll, sha(dll), cwd, rt, code, cmd])) + "\n")
    ok = rt == f"Microsoft.NETCore.App/{A.expected_runtime}"
    cmdrow(t0, f"{svc}-launch", f"supervisor: base env + <{svc}.env placeholders substituted in memory> dotnet {row['dll']} "
           f"--urls http://127.0.0.1:{port}{' --contentRoot <source project>' if extra else ''}",
           f"pid {p.pid}; health {code}; runtime {rt}; {'ok' if ok else 'RUNTIME MISMATCH'}")
    return {"ok": ok, "pid": p.pid, "port": port, "health": code, "runtime": rt}


def run(label, argv, cwd):
    t0 = now()
    env = dict(BASE_ENV); env["PATH"] = "/opt/homebrew/bin:/usr/local/bin:" + env["PATH"]
    env.update({f"ACTOR_PW_{env_name(k)}": v for k, v in ACTORS.items()})
    env.update({"LANE_SECRET_" + env_name(g): v for g, v in SECRETS.items()})
    for k in ("NODE_PATH", "PLAYWRIGHT_BROWSERS_PATH", "EK_SOCK"):
        if k in os.environ:
            env[k] = os.environ[k]
    logp = unique(logdir(), f"run-{label}", "log")
    with open(logp, "wb") as log:
        rc = subprocess.run(argv, cwd=cwd or str(W if W.exists() else SOCK.parent), env=env, stdout=log, stderr=subprocess.STDOUT).returncode
    # argv of kit helpers never contains a value; record it as given (one line)
    if label == "seal":
        global SEALED
        SEALED = True  # the seal report in E is the record of this command; no COMMANDS row after it (G4)
    cmdrow(t0, f"run-{label}", " ".join(map(str, argv)), f"exit {rc}; log {logp.name}")
    return {"ok": rc == 0, "rc": rc, "log": str(logp)}


def main():
    if SOCK.exists():
        SOCK.unlink()
    s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    old = os.umask(0o177); s.bind(str(SOCK)); os.umask(old)
    s.listen(4)
    cmdrow(now(), "supervisor-start", f"k00_supervisor.py groups={sorted(SECRETS)} actors={sorted(ACTORS)} (values in memory only)",
           f"pid {os.getpid()}")
    print(f"supervisor pid {os.getpid()} ready on {SOCK}", flush=True)
    while True:
        c, _ = s.accept()
        try:
            req = json.loads(c.makefile().readline())
            op = req.get("op")
            if op == "launch":
                res = launch(req["svc"])
            elif op == "run":
                res = run(req["label"], req["argv"], req.get("cwd"))
            elif op == "reveal":
                v = ACTORS.get(req.get("actor"))
                res = {"ok": v is not None, "password": v} if v else {"ok": False, "error": "unknown actor"}
            elif op == "pids":
                res = {"ok": True, "pids": {k: p.pid for k, p in PROCS.items()}, "supervisor": os.getpid()}
            elif op == "ping":
                res = {"ok": True, "supervisor": os.getpid()}
            elif op == "shutdown":
                SECRETS.clear(); ACTORS.clear()
                cmdrow(now(), "supervisor-shutdown", "secrets cleared from memory; socket removed", "exit 0")
                c.sendall(b'{"ok":true,"shutdown":true}\n'); c.close(); s.close(); SOCK.unlink(); return
            else:
                res = {"ok": False, "error": "unknown op"}
        except Exception as ex:  # never echo secret material; exception texts here carry none
            res = {"ok": False, "error": type(ex).__name__ + ": " + str(ex)[:300]}
        c.sendall((json.dumps(res) + "\n").encode()); c.close()


main()
