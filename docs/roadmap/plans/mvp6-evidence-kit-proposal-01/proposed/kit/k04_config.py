#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K04: lane configuration, rendered then verified BEFORE start.

Why: every service's committed appsettings default to mongodb://localhost:27017, and each service reads Mongo
from a DIFFERENT section (Auth/Platform MongoDbSettings, MDM/SupplyChain Mongo, Web ConnectionStrings:MongoDb).
A historical harness set the wrong section and silently fell back to 27017 (handoff ENVIRONMENT.md). So the kit
renders every key from service-map.tsv and then re-resolves the effective configuration the way ASP.NET Core
layers it — appsettings.json < appsettings.{Env}.json < user-secrets (Development only) < environment < command
line — and refuses to continue unless every Mongo value is the lane replica set and nothing points at 27017.

Modes:
  secrets  generate lane secrets for the groups in --rotate (default: jwt mfa) into W/secrets/lane-secrets.json (0600)
  render   write W/env/<service>.env (0600) for each service
  check    resolve and verify; write redacted E/raw/effective-config-<service>.json + E/raw/effective-config-summary.tsv
Common args: --map service-map.tsv --services "..." --work W --evidence E --suffix SFX --env-name Development
Optional: --overrides overrides.tsv (service<TAB>key<TAB>value; required for every explicit_keys entry)
          --rotate "jwt mfa"  --home ~ (user-secrets root)
Secret VALUES are never printed or written outside W/secrets and W/env.
"""
import argparse, csv, hashlib, json, os, pathlib, re, secrets, sys
from urllib.parse import urlparse

SECRET_KEY = re.compile(r"(secret|password|apikey|api_key|token|hash|privatekey)", re.I)
SUFFIX_OK = re.compile(r"^[A-Za-z][A-Za-z0-9]*[0-9]{2}$")
GUIDISH = re.compile(r"[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}", re.I)
URLISH = re.compile(r"^(https?|mongodb(\+srv)?)://", re.I)


def load_map(path, services):
    with open(path, newline="", encoding="utf-8") as f:
        rows = {r["service"]: r for r in csv.DictReader(f, delimiter="\t")}
    missing = [s for s in services if s not in rows]
    if missing:
        raise SystemExit(f"services not in map: {missing}")
    return [rows[s] for s in services]


def pairs(cell):
    if cell.strip() in ("", "-"):
        return []
    return [tuple(x.split("=", 1)) if "=" in x else (x, None) for x in cell.split(";") if x.strip()]


def ports(work):
    return json.loads(pathlib.Path(work, "ports.json").read_text())["ports"]


def lane_mongo(work, suffix, web=False):
    p = ports(work)["mongo"]
    if p == 27017:
        raise SystemExit("refusing 27017")
    q = f"replicaSet=rs{suffix}&serverSelectionTimeoutMS=5000"
    if web:
        q += "&connectTimeoutMS=3000"
    return f"mongodb://127.0.0.1:{p}/?{q}"


def peer_url(work, spec):
    target, _, tail = spec.partition("+")
    p = ports(work)
    # A peer that is not part of this lane goes to the closed sink port: fail-closed, never an operational port.
    return f"http://127.0.0.1:{p.get(target, p['sink'])}{tail}"


def content_root(work, row):
    src = pathlib.Path(work, "source", row["project_dir"])
    cr = row["content_root"]
    if cr == "project":
        return src
    if cr == "gateway-runtime":
        return pathlib.Path(work, "gateway-runtime")
    return src / "bin" / "Release" / "net8.0"


# ---------------------------------------------------------------- secrets / render
def cmd_secrets(a, rows):
    out = pathlib.Path(a.work, "secrets"); out.mkdir(mode=0o700, exist_ok=True)
    path = out / "lane-secrets.json"
    if path.exists():
        print("K04 secrets: already generated (not regenerated)"); return
    groups = {g for r in rows for g, _ in (x.split(":", 1) for x in pairs_raw(r["secret_keys"]))}
    rotate = set(a.rotate.split())
    data = {g: secrets.token_urlsafe(48) for g in sorted(groups & rotate)}
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w") as f:
        json.dump(data, f)
    # Non-secret values every lane service must share: the issuer/audience Auth actually signs with.
    # Committed appsettings disagree between services (and SupplyChain has none), so the kit pins them to Auth's.
    auth = pathlib.Path(a.work, "source/services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.json")
    jwt = json.loads(auth.read_text(encoding="utf-8-sig")).get("JwtSettings", {}) if auth.exists() else {}
    shared = {"jwt-iss": jwt.get("Issuer", ""), "jwt-aud": jwt.get("Audience", "")}
    if not all(shared.values()):
        raise SystemExit("cannot read JwtSettings Issuer/Audience from the Auth appsettings in the K01 source")
    pathlib.Path(out, "lane-shared.json").write_text(json.dumps(shared) + "\n")
    print(f"K04 secrets: generated groups {sorted(data)} (values not shown); shared issuer/audience pinned to Auth's")


def pairs_raw(cell):
    return [] if cell.strip() in ("", "-") else [x for x in cell.split(";") if x.strip()]


def overrides(path):
    out = {}
    if path:
        with open(path, newline="", encoding="utf-8") as f:
            for r in csv.reader(f, delimiter="\t"):
                if r and not r[0].startswith("#") and len(r) >= 3 and r[0] != "service":
                    out.setdefault(r[0], {})[r[1]] = r[2]
    return out


def desired_env(a, row, lane_secrets, ov, lane_shared):
    env = {"ASPNETCORE_ENVIRONMENT": a.env_name, "DOTNET_ROLL_FORWARD": "LatestPatch"}
    svc = row["service"]
    if row["mongo_conn_key"] != "-":
        env[row["mongo_conn_key"]] = lane_mongo(a.work, a.suffix, web=(svc == "web"))
        env[row["mongo_db_key"]] = f"{row['db_prefix']}_{a.suffix}"
    for k, prefix in pairs(row["extra_db_keys"]):
        env[k] = f"{prefix}_{a.suffix}"
    for k, spec in pairs(row["peer_url_keys"]):
        env[k] = peer_url(a.work, spec)
    for k, v in pairs(row["forced_keys"]):
        env[k] = v
    for g, k in (x.split(":", 1) for x in pairs_raw(row["secret_keys"])):
        if g in lane_secrets:
            env[k] = lane_secrets[g]
        elif g in lane_shared:
            env[k] = lane_shared[g]
    for k, v in ov.get(svc, {}).items():
        env[k] = v
    return env


def shared_values(work):
    p = pathlib.Path(work, "secrets", "lane-shared.json")
    return json.loads(p.read_text()) if p.exists() else {}


def cmd_render(a, rows):
    lane_secrets = json.loads(pathlib.Path(a.work, "secrets", "lane-secrets.json").read_text())
    ov = overrides(a.overrides)
    envdir = pathlib.Path(a.work, "env"); envdir.mkdir(mode=0o700, exist_ok=True)
    for row in rows:
        env = desired_env(a, row, lane_secrets, ov, shared_values(a.work))
        p = envdir / f"{row['service']}.env"
        fd = os.open(p, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        with os.fdopen(fd, "w") as f:
            for k, v in env.items():
                if "'" in v:
                    raise SystemExit(f"single quote not allowed in value of {k}")
                f.write(f"{k}='{v}'\n")
    print(f"K04 render: {len(rows)} env files written (0600)")


# ---------------------------------------------------------------- effective resolution
def flatten(obj, prefix=""):
    out = {}
    if isinstance(obj, dict):
        for k, v in obj.items():
            out.update(flatten(v, f"{prefix}:{k}" if prefix else k))
    elif isinstance(obj, list):
        for i, v in enumerate(obj):
            out.update(flatten(v, f"{prefix}:{i}"))
    else:
        out[prefix] = "" if obj is None else (str(obj).lower() if isinstance(obj, bool) else str(obj))
    return out


def read_envfile(p):
    out = {}
    for line in pathlib.Path(p).read_text().splitlines():
        if line.strip() and not line.startswith("#"):
            k, v = line.split("=", 1)
            out[k] = v[1:-1] if v.startswith("'") and v.endswith("'") else v
    return out


def user_secrets_id(csproj):
    m = re.search(r"<UserSecretsId>([^<]+)</UserSecretsId>", pathlib.Path(csproj).read_text())
    return m.group(1).strip() if m else None


def resolve(a, row):
    """Returns {lower_key: (display_key, value, layer)} following ASP.NET Core precedence."""
    eff = {}

    def put(layer, flat):
        for k, v in flat.items():
            eff[k.lower()] = (k, v, layer)

    cr = content_root(a.work, row)
    for name in ("appsettings.json", f"appsettings.{a.env_name}.json"):
        p = cr / name
        if p.exists():
            put(name, flatten(json.loads(p.read_text(encoding="utf-8-sig"))))
    if a.env_name == "Development":
        usid = user_secrets_id(pathlib.Path(a.work, "source", row["project_dir"], row["csproj"]))
        if usid:
            p = pathlib.Path(os.path.expanduser(a.home), ".microsoft", "usersecrets", usid, "secrets.json")
            if p.exists():
                put("user-secrets", flatten(json.loads(p.read_text(encoding="utf-8-sig"))))
    env = read_envfile(pathlib.Path(a.work, "env", f"{row['service']}.env"))
    put("environment", {k.replace("__", ":"): v for k, v in env.items()})
    port = ports(a.work)["gateway" if row["service"] == "gateway" else row["service"]]
    put("command-line", {"urls": f"http://127.0.0.1:{port}"})
    return eff, env


def cmd_check(a, rows):
    p = ports(a.work); lane_ports = set(p.values()) - {p["mongo"]}
    lane_secrets = json.loads(pathlib.Path(a.work, "secrets", "lane-secrets.json").read_text())
    shared = shared_values(a.work)
    raw = pathlib.Path(a.evidence, "raw"); raw.mkdir(parents=True, exist_ok=True)
    fails, summary, group_vals = [], [], {}
    if not SUFFIX_OK.match(a.suffix) or GUIDISH.search(a.suffix):
        fails.append(f"DB-010: suffix {a.suffix!r} must be one fixed lane suffix (letters/digits, 2-digit end), never a GUID")
    ov = overrides(a.overrides)
    for row in rows:
        svc = row["service"]
        eff, env = resolve(a, row)
        get = lambda key: eff.get(key.replace("__", ":").lower(), (key, None, "absent"))
        # C1 Mongo connection is the lane replica set; C3 DB-010 names
        if row["mongo_conn_key"] != "-":
            _, v, layer = get(row["mongo_conn_key"])
            u = urlparse(v or "")
            if not (u.scheme == "mongodb" and u.hostname == "127.0.0.1" and u.port == p["mongo"]
                    and f"replicaSet=rs{a.suffix}" in (u.query or "")):
                fails.append(f"{svc}: {row['mongo_conn_key']} is not the lane replica set (layer={layer})")
            for key, prefix in [(row["mongo_db_key"], row["db_prefix"])] + pairs(row["extra_db_keys"]):
                _, v, layer = get(key)
                if v != f"{prefix}_{a.suffix}":
                    fails.append(f"{svc}: {key} = {v!r} (layer={layer}); DB-010 requires {prefix}_{a.suffix}")
        # C2 nothing anywhere points at 27017; C4 every local URL is lane-owned
        external = []
        for lk, (k, v, layer) in eff.items():
            if SECRET_KEY.search(k):
                continue
            if "27017" in v:
                fails.append(f"{svc}: {k} references 27017 (layer={layer})")
            if URLISH.match(v.strip()):
                u = urlparse(v.strip())
                if u.scheme.startswith("mongodb"):
                    if u.port != p["mongo"] or u.hostname != "127.0.0.1":
                        fails.append(f"{svc}: {k} is a Mongo URL outside the lane (layer={layer})")
                elif u.hostname in ("localhost", "127.0.0.1", "0.0.0.0", "::1"):
                    if (u.port or 80) not in lane_ports:
                        fails.append(f"{svc}: {k} -> local port {u.port} not lane-owned (layer={layer})")
                else:
                    external.append(k)
        # C5 explicit keys must be set by the lane, never inherited silently
        for k, _ in pairs(row["explicit_keys"]):
            v = ov.get(svc, {}).get(k)
            if v is None or v.startswith("<"):
                fails.append(f"{svc}: {k} must be set explicitly in overrides.tsv (security-relevant; inherited or placeholder value not accepted)")
        # C6 secret groups
        red = {}
        for g, k in (x.split(":", 1) for x in pairs_raw(row["secret_keys"])):
            _, v, layer = get(k)
            group_vals.setdefault(g, set()).add(hashlib.sha256((v or "").encode()).hexdigest())
            if g in lane_secrets and v != lane_secrets[g]:
                fails.append(f"{svc}: {k} (group {g}) is not the lane-generated value (layer={layer})")
            if g in shared and v != shared[g]:
                fails.append(f"{svc}: {k} = {v!r} differs from the lane-pinned Auth value (layer={layer})")
            red[k] = "lane-generated" if g in lane_secrets else f"inherited:{layer}"
        out = {}
        for lk, (k, v, layer) in sorted(eff.items()):
            shown = f"[REDACTED {red.get(k.replace(':', '__'), 'secret:' + layer)}]" if SECRET_KEY.search(k) else v
            out[k] = {"value": shown, "layer": layer}
        (raw / f"effective-config-{svc}.json").write_text(json.dumps(
            {"service": svc, "environment": a.env_name, "contentRoot": str(content_root(a.work, row)),
             "externalUrlKeysForReview": external, "keys": out}, indent=2) + "\n")
        summary.append((svc, get(row["mongo_conn_key"])[1] if row["mongo_conn_key"] != "-" else "-",
                        get(row["mongo_db_key"])[1] if row["mongo_db_key"] != "-" else "-", len(external)))
    for g, vals in group_vals.items():
        if (g in lane_secrets or g.startswith("jwt")) and len(vals) != 1:
            fails.append(f"secret/claim group {g} differs between services")
    with open(raw / "effective-config-summary.tsv", "w") as f:
        f.write("service\tmongo_connection\tdatabase\texternal_url_keys\n")
        for r in summary:
            f.write("\t".join(map(str, r)) + "\n")
        f.write(f"#groups\tlane-generated={sorted(lane_secrets)}\tunverified-pairing={sorted(set(group_vals) - set(lane_secrets) - {g for g in group_vals if g.startswith('jwt')})}\n")
        f.write(f"#result\t{'FAIL' if fails else 'PASS'}\t{len(fails)} finding(s)\n")
    (raw / "effective-config-findings.txt").write_text("\n".join(fails) + ("\n" if fails else "none\n"))
    if fails:
        print("K04 check FAIL:\n  " + "\n  ".join(fails)); sys.exit(1)
    print(f"K04 check PASS: {len(rows)} services resolved; no 27017; DB-010 suffix {a.suffix}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["secrets", "render", "check"])
    for x in ("--map", "--services", "--work", "--evidence", "--suffix"):
        ap.add_argument(x, required=True)
    ap.add_argument("--env-name", default="Development")
    ap.add_argument("--overrides")
    ap.add_argument("--rotate", default="jwt mfa")
    ap.add_argument("--home", default="~")
    a = ap.parse_args()
    rows = load_map(a.map, a.services.split())
    {"secrets": cmd_secrets, "render": cmd_render, "check": cmd_check}[a.mode](a, rows)


if __name__ == "__main__":
    main()
