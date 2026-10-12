#!/usr/bin/env python3
"""A12 runtime VER-02 lane supervisor — keeps every lane secret in process memory only.

Why: the lane instruction says "Secrets generated locally, never written to files". The evidence-kit candidate K04/K07
write secrets to W/secrets/*.json and W/env/*.env (0600). Here K04 renders the env files with PLACEHOLDERS
(@@LANE:<group>@@) and this supervisor substitutes the in-memory values only into child-process environments.

Generated at start (secrets.token_urlsafe), never printed, never written:
  groups jwt, mfa, auth-platform, platform-mdm-active, svcid; actor passwords actor-a, actor-b, actor-le-b.
Control: unix socket W/ctl.sock (0600), one JSON request per connection:
  {"op":"launch","svc":S}                       start service S exactly like kit K06 launch (+ same post-checks)
  {"op":"run","label":L,"argv":[...],"cwd":D}   run a child with ACTOR_PW_<A|B|LEB> and LANE_SECRET_<GROUP> in env;
                                                 stdout+stderr -> W/logs/run-L.log; returns exit code
  {"op":"pids"}                                 service pids
  {"op":"shutdown"}                             forget secrets and exit (services are stopped by cleanup by PID)
"""
import json, os, pathlib, secrets, socket, subprocess, sys, time, csv, hashlib, datetime

W = pathlib.Path(sys.argv[1]); E = pathlib.Path(sys.argv[2])
DOTNET = "/Users/natig/.dotnet/dotnet"; EXPECTED_RT = "8.0.23"
SECRETS = {g: secrets.token_urlsafe(48) for g in ("jwt", "mfa", "auth-platform", "platform-mdm-active", "svcid")}
ACTORS = {k: secrets.token_urlsafe(24) for k in ("A", "B", "LEB")}
PORTS = json.loads((W / "ports.json").read_text())["ports"]
MAP = {r["service"]: r for r in csv.DictReader(open(W / "kit/service-map.tsv"), delimiter="\t")}
PROCS = {}
BASE_ENV = {"HOME": os.environ["HOME"], "PATH": "/usr/bin:/bin:/usr/sbin:/sbin", "TMPDIR": os.environ.get("TMPDIR", "/tmp"),
            "LANG": "en_US.UTF-8", "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"}


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sha(p):
    return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()


def subst(v):
    for g, s in SECRETS.items():
        v = v.replace(f"@@LANE:{g}@@", s)
    if "@@LANE:" in v:
        raise RuntimeError("unresolved placeholder")
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
    row = MAP[svc]; port = PORTS[svc]
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
    (W / "logs").mkdir(exist_ok=True)
    log = open(W / "logs" / f"{svc}.log", "ab")
    p = subprocess.Popen([DOTNET, str(dll), "--urls", f"http://127.0.0.1:{port}", *extra], cwd=rundir, env=env,
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
            return {"ok": False, "error": f"{svc} exited rc={p.returncode}"}
        time.sleep(0.5)
    if lp != str(p.pid):
        return {"ok": False, "error": f"listener {lp} != pid {p.pid}"}
    code = subprocess.run(["curl", "-s", "-o", str(E / "raw" / f"health-{svc}.body"), "-D", str(E / "raw" / f"health-{svc}.headers"),
                           "-m", "10", "-w", "%{http_code}", f"http://127.0.0.1:{port}{row['health_path']}"], capture_output=True, text=True).stdout
    lsof = sh("lsof", "-nP", "-p", str(p.pid))
    rt = next((t for t in lsof.split() if "Microsoft.NETCore.App/" in t), "")
    rt = "Microsoft.NETCore.App/" + rt.split("Microsoft.NETCore.App/")[1].split("/")[0] if rt else ""
    cwd = sh("lsof", "-a", "-nP", "-p", str(p.pid), "-d", "cwd", "-Fn").split("\nn")[-1].strip()
    cmd = sh("ps", "-o", "command=", "-p", str(p.pid)).strip()
    lstart = " ".join(sh("ps", "-o", "lstart=", "-p", str(p.pid)).split())
    pt = E / "raw" / "processes.tsv"
    if not pt.exists():
        pt.write_text("service\tpid\tport\tstarted\tdll\tdll_sha256\tcwd\tloaded_runtime\thealth_http\tcommand\n")
    with open(pt, "a") as f:
        f.write(f"{svc}\t{p.pid}\t{port}\t{lstart}\t{dll}\t{sha(dll)}\t{cwd}\t{rt}\t{code}\t{cmd}\n")
    with open(E / "COMMANDS.tsv", "a") as f:
        f.write(f"{now()}\t{now()}\tK06*\t{svc}-launch\tsupervisor: env -i + <{svc}.env placeholders substituted in memory> dotnet {row['dll']} --urls http://127.0.0.1:{port}{' --contentRoot <source project>' if extra else ''}\tstarted pid {p.pid}\n")
    ok = rt == f"Microsoft.NETCore.App/{EXPECTED_RT}"
    return {"ok": ok, "pid": p.pid, "port": port, "health": code, "runtime": rt}


def run(label, argv, cwd):
    env = dict(BASE_ENV); env["PATH"] = "/opt/homebrew/bin:" + env["PATH"]
    env.update({f"ACTOR_PW_{k}": v for k, v in ACTORS.items()})
    env.update({"LANE_SECRET_" + g.upper().replace("-", "_"): v for g, v in SECRETS.items()})
    for k in ("NODE_PATH", "PLAYWRIGHT_BROWSERS_PATH"):
        if k in os.environ:
            env[k] = os.environ[k]
    (W / "logs").mkdir(exist_ok=True)
    with open(W / "logs" / f"run-{label}.log", "wb") as log:
        rc = subprocess.run(argv, cwd=cwd or str(W), env=env, stdout=log, stderr=subprocess.STDOUT).returncode
    return {"ok": rc == 0, "rc": rc}


def main():
    sp = W / "ctl.sock"
    if sp.exists():
        sp.unlink()
    s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    old = os.umask(0o177); s.bind(str(sp)); os.umask(old)
    s.listen(4)
    print(f"supervisor pid {os.getpid()} ready", flush=True)
    while True:
        c, _ = s.accept()
        try:
            req = json.loads(c.makefile().readline())
            op = req.get("op")
            if op == "launch":
                res = launch(req["svc"])
            elif op == "run":
                res = run(req["label"], req["argv"], req.get("cwd"))
            elif op == "pids":
                res = {k: p.pid for k, p in PROCS.items()}
            elif op == "shutdown":
                SECRETS.clear(); ACTORS.clear()
                c.sendall(b'{"ok":true,"shutdown":true}\n'); c.close(); s.close(); sp.unlink(); return
            else:
                res = {"ok": False, "error": "unknown op"}
        except Exception as ex:  # never echo secret material; exceptions here carry none
            res = {"ok": False, "error": type(ex).__name__ + ": " + str(ex)[:300]}
        c.sendall((json.dumps(res) + "\n").encode()); c.close()


main()
