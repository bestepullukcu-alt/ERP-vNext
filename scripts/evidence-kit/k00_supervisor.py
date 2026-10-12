#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.2 lane supervisor — the ONLY holder of lane secrets (D4).

Origin: adapted from the A12 VER-02 run's scripts/lane_supervisor.py (docs/records/audits/2026-09/
mvp6-shipment-a12-runtime-independent-ver-02/), which replaced the v1.0 K04/K07 habit of writing secrets to files.

What it does
  * At start it generates, with secrets.token_urlsafe, one value per rotated group (--rotate) and one password per actor
    label in actors.tsv. Nothing is printed, logged or written to disk; exceptions never carry values.
  * K04 renders env files with placeholders @@LANE:<group>@@. On `launch` the supervisor reads the env file, replaces the
    placeholders in memory and starts the service with a fixed base environment + that env file.
  * `run` starts ONE of a fixed set of kit tasks (below) and gives it only the values that task needs.
  * `shutdown` clears every value and exits; `abort` also stops the services this supervisor launched (failure path).

Trust boundary (stated, v1.2 F2): the supervisor serves only connections whose peer process runs as the same Unix user
(checked with the kernel's peer credentials, fail-closed if unavailable). A process of that user can still ask for the
allow-listed tasks; it can no longer run arbitrary code with the lane values, and it can never receive a password
over the socket: `reveal` writes the password to the requesting user's own terminal device, after that user has typed
a one-time code that the supervisor wrote to the same terminal. A background process without a terminal is refused.

v1.2 changes (proposal-03; review mvp6-evidence-kit-v1-1-independent-review-01 findings):
  F2  `run` is an allow-list: k07 (identity rotation; actor passwords only), scan and seal (K08 exact scan; all values),
      harness (a lane script inside --harness-dir, run by node or python3; actor passwords only). No free argv, no cwd.
      `reveal` needs reveal-arm + the code shown on the caller's terminal; the password goes to that terminal only.
  F4  the supervisor refuses to start if a live supervisor answers on the socket (no orphan); `abort` stops its own
      services, clears the values and removes the socket; --max-lifetime / --idle expiry does the same by itself.
  F6  the fallback log folder next to the socket (used after K11 removed W) is removed at shutdown, abort and expiry.
  F7  the socket's folder must be owned by this user and not group/world-writable; an existing socket path must be a
      socket owned by this user and not answered by a live supervisor before it is replaced.
  F8  core dumps are disabled (RLIMIT_CORE = 0) for the supervisor and, by inheritance, every child.
  F10 requests are bounded (64 KiB, 10 s); labels, services, actors and harness arguments are validated. Requests are
      served one at a time on purpose: lane operations are serialised (a `ping` waits while a task runs).
  F14 after launch, every listening address of the service PID must be 127.0.0.1; anything else fails the launch.

Control socket: one JSON request per connection (use k00_ctl.py):
  {"op":"launch","svc":S}
  {"op":"run","task":"k07"|"scan"|"seal"}      {"op":"run","task":"harness","file":REL,"args":[...]}
  {"op":"reveal-arm","actor":A}  then  {"op":"reveal","actor":A,"code":C}
  {"op":"pids"} | {"op":"ping"} | {"op":"shutdown"} | {"op":"abort"}
Usage: k00_supervisor.py --work W --evidence E --map service-map.tsv --actors actors.tsv --socket S --suffix SFX
                         [--rotate "..."] [--dotnet D] [--expected-runtime 8.0.23] [--harness-dir DIR]
                         [--max-lifetime 43200] [--idle 14400]
"""
import argparse, csv, datetime, hashlib, json, os, pathlib, re, resource, secrets, shutil, signal, socket, stat
import struct, subprocess, sys, time

ap = argparse.ArgumentParser()
for x in ("--work", "--evidence", "--map", "--actors", "--socket", "--suffix"):
    ap.add_argument(x, required=True)
ap.add_argument("--rotate", default="jwt mfa auth-platform platform-mdm-active svcid")
ap.add_argument("--dotnet", default=os.path.expanduser("~/.dotnet/dotnet"))
ap.add_argument("--expected-runtime", default="8.0.23")
ap.add_argument("--harness-dir", default="")
ap.add_argument("--max-lifetime", type=int, default=43200, help="seconds; then abort by itself (F4)")
ap.add_argument("--idle", type=int, default=14400, help="seconds without a request; then abort by itself (F4)")
A = ap.parse_args()
W = pathlib.Path(A.work); E = pathlib.Path(A.evidence); SOCK = pathlib.Path(A.socket)
KIT = pathlib.Path(__file__).resolve().parent
LABEL = re.compile(r"^[A-Za-z0-9._-]{1,64}$")
ARG = re.compile(r"^[A-Za-z0-9._:/=@,+-]{1,256}$")
MAX_REQ = 65536

resource.setrlimit(resource.RLIMIT_CORE, (0, 0))   # F8: no core file can ever hold a lane value

if W.resolve() in SOCK.resolve().parents:
    raise SystemExit("socket must live outside the lane workspace (K11 removes W before the final seal)")
if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*[0-9]{2}", A.suffix):
    raise SystemExit("--suffix must be the lane DB suffix (DB-010)")
HARNESS = pathlib.Path(A.harness_dir).resolve() if A.harness_dir else None

SECRETS = {g: secrets.token_urlsafe(48) for g in A.rotate.split()}
ACTOR_LABELS = [r["label"] for r in csv.DictReader(open(A.actors), delimiter="\t") if r.get("label")]
if any(not LABEL.match(x) for x in ACTOR_LABELS):
    raise SystemExit("actor labels must match [A-Za-z0-9._-]{1,64}")
ACTORS = {lab: secrets.token_urlsafe(24) for lab in ACTOR_LABELS}
PORTS = json.loads((W / "ports.json").read_text())["ports"]
MAP = {r["service"]: r for r in csv.DictReader(open(A.map), delimiter="\t")}
PROCS = {}
PENDING = {}   # reveal-arm: actor -> (code, tty, deadline)
BASE_ENV = {"HOME": os.environ["HOME"], "PATH": "/usr/bin:/bin:/usr/sbin:/sbin", "TMPDIR": os.environ.get("TMPDIR", "/tmp"),
            "LANG": "en_US.UTF-8", "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"}
SEALED = False  # set once the final seal scan has run: nothing may be written to E after it (G4)
FALLBACK_LOGS = SOCK.parent / (SOCK.name + "-logs")


def now():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def one_line(s):
    return re.sub(r"[\t\r\n]+", " ", str(s))


def cmdrow(t0, step, desc, result):
    """G2: exactly one COMMANDS.tsv row per command; never contains a value (descriptions are fixed text).
    After the seal, no row is written: the SECRET-SCAN-FINAL report is the seal's own record (G4)."""
    if SEALED or not E.exists():
        return
    with open(E / "COMMANDS.tsv", "a") as f:
        f.write("\t".join([t0, now(), "SUP", one_line(step), one_line(desc), one_line(result)]) + "\n")


def logdir():
    """W/logs while the workspace exists; after K11 removed W, a 0700 folder next to the socket (removed at exit, F6)."""
    d = W / "logs" if W.exists() else FALLBACK_LOGS
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


def listen_addrs(pid):
    """F14: every TCP listening address of this PID (lsof -F n lines, e.g. n127.0.0.1:5856)."""
    out = sh("lsof", "-nP", "-a", "-p", str(pid), "-iTCP", "-sTCP:LISTEN", "-Fn")
    return sorted({l[1:] for l in out.splitlines() if l.startswith("n")})


# ------------------------------------------------------------------------------------------------ peer checks (F2)
def peer(conn):
    """(uid, pid) of the connecting process from the kernel, or (None, None). Linux SO_PEERCRED; macOS LOCAL_PEERCRED
    (xucred: cr_version u32, cr_uid u32 …) and LOCAL_PEERPID. Values are never trusted from the request."""
    try:
        if hasattr(socket, "SO_PEERCRED"):
            pid, uid, _ = struct.unpack("3i", conn.getsockopt(socket.SOL_SOCKET, socket.SO_PEERCRED, struct.calcsize("3i")))
            return uid, pid
        if sys.platform == "darwin":
            SOL_LOCAL, LOCAL_PEERCRED, LOCAL_PEERPID = 0, 0x001, 0x002
            cred = conn.getsockopt(SOL_LOCAL, LOCAL_PEERCRED, 76)
            uid = struct.unpack_from("I", cred, 4)[0]
            pid = struct.unpack("i", conn.getsockopt(SOL_LOCAL, LOCAL_PEERPID, 4))[0]
            return uid, pid
    except OSError:
        pass
    return None, None


def tty_of(pid):
    t = sh("ps", "-o", "tty=", "-p", str(pid)).strip()
    if not t or t in ("?", "??", "-"):
        return None
    dev = pathlib.Path("/dev") / t
    if not dev.exists() and pathlib.Path("/dev/pts", t.split("/")[-1]).exists():
        dev = pathlib.Path("/dev/pts", t.split("/")[-1])
    try:
        st = dev.stat()
    except OSError:
        return None
    return dev if stat.S_ISCHR(st.st_mode) and st.st_uid == os.getuid() else None


def to_tty(dev, text):
    fd = os.open(dev, os.O_WRONLY | os.O_NOCTTY)
    try:
        os.write(fd, text.encode())
    finally:
        os.close(fd)


# ------------------------------------------------------------------------------------------------ operations
def launch(svc):
    if svc not in MAP or svc not in PORTS:
        return {"ok": False, "error": "unknown service"}
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
    addrs = listen_addrs(p.pid)
    loopback_only = bool(addrs) and all(a.startswith("127.0.0.1:") for a in addrs)
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
        pt.write_text("service\tpid\tport\tstarted\tdll\tdll_sha256\tcwd\tloaded_runtime\thealth_http\tlisten_addrs\tcommand\n")
    with open(pt, "a") as f:
        f.write("\t".join(map(one_line, [svc, p.pid, port, lstart, dll, sha(dll), cwd, rt, code, ",".join(addrs), cmd])) + "\n")
    ok = rt == f"Microsoft.NETCore.App/{A.expected_runtime}" and loopback_only
    cmdrow(t0, f"{svc}-launch", f"supervisor: base env + <{svc}.env placeholders substituted in memory> dotnet {row['dll']} "
           f"--urls http://127.0.0.1:{port}{' --contentRoot <source project>' if extra else ''}",
           f"pid {p.pid}; health {code}; runtime {rt}; listen {','.join(addrs) or 'none'}; "
           f"{'ok' if ok else ('NOT LOOPBACK-ONLY' if not loopback_only else 'RUNTIME MISMATCH')}")
    return {"ok": ok, "pid": p.pid, "port": port, "health": code, "runtime": rt, "listen": addrs}


def task_env(scope):
    env = dict(BASE_ENV); env["PATH"] = "/opt/homebrew/bin:/usr/local/bin:" + env["PATH"]
    if scope in ("actors", "all"):
        env.update({f"ACTOR_PW_{env_name(k)}": v for k, v in ACTORS.items()})
    if scope == "all":
        env.update({"LANE_SECRET_" + env_name(g): v for g, v in SECRETS.items()})
    for k in ("NODE_PATH", "PLAYWRIGHT_BROWSERS_PATH"):
        if k in os.environ:
            env[k] = os.environ[k]
    return env


def build_task(req):
    """F2: map a task name to a fixed argv and the narrowest secret scope. Returns (label, argv, cwd, scope)."""
    task = req.get("task")
    py = sys.executable or "python3"
    if task == "k07":
        return "k07", [py, str(KIT / "k07_identity.py"), "rotate", "--work", str(W), "--evidence", str(E),
                       "--suffix", A.suffix, "--actors", str(pathlib.Path(A.actors).resolve())], None, "actors"
    if task == "scan":
        out = unique(E, "SECRET-SCAN-EXACT", "txt").name
        return "scan", [py, str(KIT / "k08_redact_scan.py"), "scan", str(E), "--exact", "--out", out], None, "all"
    if task == "seal":
        return "seal", [py, str(KIT / "k08_redact_scan.py"), "scan", str(E), "--exact", "--seal"], None, "all"
    if task == "harness":
        if HARNESS is None:
            raise ValueError("no --harness-dir was given at supervisor start")
        rel = str(req.get("file", ""))
        f = (HARNESS / rel).resolve()
        if HARNESS not in f.parents or not f.is_file() or f.suffix not in (".js", ".mjs", ".cjs", ".py"):
            raise ValueError("harness file must be a .js/.mjs/.cjs/.py file inside --harness-dir")
        args = req.get("args") or []
        if not isinstance(args, list) or len(args) > 32 or any(not isinstance(x, str) or not ARG.match(x) for x in args):
            raise ValueError("harness args: at most 32 strings matching [A-Za-z0-9._:/=@,+-]{1,256}")
        interp = shutil.which("node", path=task_env("none")["PATH"]) if f.suffix != ".py" else py
        if not interp:
            raise ValueError("node not found")
        return f"harness-{f.stem}"[:64], [interp, str(f), *args], str(HARNESS), "actors"
    raise ValueError("unknown task (allowed: k07, scan, seal, harness)")


def run(req):
    global SEALED
    label, argv, cwd, scope = build_task(req)
    if not LABEL.match(label):
        raise ValueError("bad label")
    t0 = now()
    logp = unique(logdir(), f"run-{label}", "log")
    with open(logp, "wb") as log:
        rc = subprocess.run(argv, cwd=cwd or str(W if W.exists() else SOCK.parent), env=task_env(scope), stdout=log,
                            stderr=subprocess.STDOUT).returncode
    if label == "seal":
        SEALED = True  # the seal report in E is the record of this command; no COMMANDS row after it (G4)
    shown = [a.replace(str(E), "<E>").replace(str(W), "<W>").replace(str(KIT), "<kit>") for a in argv]
    cmdrow(t0, f"run-{label}", f"task {label} (scope {scope}): " + " ".join(shown), f"exit {rc}; log {logp.name}")
    return {"ok": rc == 0, "rc": rc, "log": logp.name}


def reveal_arm(actor, pid):
    if actor not in ACTORS:
        return {"ok": False, "error": "unknown actor"}
    dev = tty_of(pid)
    if dev is None:
        return {"ok": False, "error": "reveal needs a terminal: the requesting process has no controlling tty"}
    code = f"{secrets.randbelow(10**6):06d}"
    PENDING[actor] = (code, str(dev), time.time() + 60)
    to_tty(dev, f"\r\n[ek supervisor] reveal code for actor {actor}: {code} (valid 60 s, one use)\r\n")
    cmdrow(now(), "reveal-arm", f"reveal code for actor {actor} written to the caller's terminal", "armed")
    return {"ok": True, "armed": True}


def reveal(actor, code, pid):
    pend = PENDING.pop(actor, None)
    dev = tty_of(pid)
    if not pend or dev is None or str(dev) != pend[1] or time.time() > pend[2] \
            or not secrets.compare_digest(str(code), pend[0]):
        return {"ok": False, "error": "reveal refused (arm first; same terminal; correct code within 60 s)"}
    to_tty(dev, f"\r\n[ek supervisor] password for {actor}: {ACTORS[actor]}\r\n")
    cmdrow(now(), "reveal", f"password of actor {actor} written to the caller's terminal only", "ok")
    return {"ok": True, "written_to_terminal": True}


def stop_own_services():
    for p in PROCS.values():
        if p.poll() is None:
            try:
                os.killpg(p.pid, signal.SIGTERM)
            except OSError:
                pass
    deadline = time.time() + 20
    for p in PROCS.values():
        while p.poll() is None and time.time() < deadline:
            time.sleep(0.25)
        if p.poll() is None:
            try:
                os.killpg(p.pid, signal.SIGKILL)
            except OSError:
                pass


def finish(srv, reason, stop_services):
    if stop_services:
        stop_own_services()
    SECRETS.clear(); ACTORS.clear(); PENDING.clear()
    cmdrow(now(), f"supervisor-{reason}", "values cleared from memory; socket and fallback log folder removed"
           + ("; own services stopped" if stop_services else ""), "exit 0")
    srv.close()
    try:
        SOCK.unlink()
    except FileNotFoundError:
        pass
    shutil.rmtree(FALLBACK_LOGS, ignore_errors=True)   # F6


# ------------------------------------------------------------------------------------------------ socket setup (F4/F7)
def prepare_socket():
    parent = SOCK.parent.resolve()
    st = parent.stat()
    if st.st_uid != os.getuid() or st.st_mode & 0o022:
        raise SystemExit(f"socket folder {parent} must be owned by you and not group/world-writable "
                         "(use the per-user $TMPDIR, not /tmp) — F7")
    if os.path.lexists(SOCK):
        lst = os.lstat(SOCK)
        if not stat.S_ISSOCK(lst.st_mode) or lst.st_uid != os.getuid():
            raise SystemExit(f"{SOCK} exists and is not a socket owned by you — refusing to replace it (F7)")
        probe = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM); probe.settimeout(2)
        live = False
        try:
            probe.connect(str(SOCK)); probe.sendall(b'{"op":"ping"}\n')
            live = bool(probe.recv(64))
        except OSError:
            live = False
        finally:
            probe.close()
        if live:
            raise SystemExit(f"a live lane supervisor already owns {SOCK}: run `run-kit.sh abort` first (F4)")
        SOCK.unlink()   # stale socket of a dead supervisor, owned by this user


def read_request(c):
    c.settimeout(10)
    buf = b""
    while b"\n" not in buf:
        chunk = c.recv(4096)
        if not chunk:
            break
        buf += chunk
        if len(buf) > MAX_REQ:
            raise ValueError("request too large")
    return json.loads(buf.split(b"\n", 1)[0] or b"{}")


def main():
    prepare_socket()
    srv = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    old = os.umask(0o177); srv.bind(str(SOCK)); os.umask(old)
    srv.listen(4); srv.settimeout(5)
    started = last = time.time()
    cmdrow(now(), "supervisor-start", f"k00_supervisor.py groups={sorted(SECRETS)} actors={sorted(ACTORS)} "
           f"(values in memory only; core dumps off; lifetime {A.max_lifetime}s; idle {A.idle}s)", f"pid {os.getpid()}")
    print(f"supervisor pid {os.getpid()} ready on {SOCK}", flush=True)
    while True:
        if time.time() - started > A.max_lifetime or time.time() - last > A.idle:
            finish(srv, "expired", stop_services=True)
            sys.exit(3)
        try:
            c, _ = srv.accept()
        except socket.timeout:
            continue
        last = time.time()
        try:
            uid, pid = peer(c)
            if uid is None or uid != os.getuid():
                res = {"ok": False, "error": "peer credentials unavailable or another user (F2)"}
            else:
                req = read_request(c)
                op = req.get("op")
                if op == "launch":
                    res = launch(str(req.get("svc", "")))
                elif op == "run":
                    res = run(req)
                elif op == "reveal-arm":
                    res = reveal_arm(str(req.get("actor", "")), pid)
                elif op == "reveal":
                    res = reveal(str(req.get("actor", "")), req.get("code", ""), pid)
                elif op == "pids":
                    res = {"ok": True, "pids": {k: p.pid for k, p in PROCS.items()}, "supervisor": os.getpid()}
                elif op == "ping":
                    res = {"ok": True, "supervisor": os.getpid()}
                elif op in ("shutdown", "abort"):
                    c.sendall(json.dumps({"ok": True, op: True}).encode() + b"\n"); c.close()
                    finish(srv, op, stop_services=(op == "abort"))
                    return
                else:
                    res = {"ok": False, "error": "unknown op"}
        except Exception as ex:  # never echo secret material; exception texts here carry none
            res = {"ok": False, "error": type(ex).__name__ + ": " + str(ex)[:300]}
        try:
            c.sendall((json.dumps(res) + "\n").encode())
        except OSError:
            pass
        c.close()


main()
