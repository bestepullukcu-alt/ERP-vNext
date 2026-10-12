#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.2 phase K07: fresh real-Auth identities, secrets held in memory only.

v1.1 (D4): this script no longer generates, stores or reveals passwords. It runs as a child of the lane supervisor
(v1.2: supervisor task `k00_ctl.py SOCK run k07`, which gives it the actor passwords only) and reads each actor's password from ACTOR_PW_<LABEL>,
which only the supervisor holds. The bcrypt hash reaches mongosh through the child environment (EK_ARGS_JSON), never a
file. W/secrets no longer exists. Manual browser login: `k00_ctl.py SOCK reveal <label>` (v1.2: one-time code +
password written by the supervisor to the caller's own terminal only).


Problem it closes: the Auth seeder gives every seeded user ONE shared, committed bcrypt hash (DataSeeder.cs), so a
seeded login proves nothing about who holds the credential, and no earlier lane archived a replayable identity step
(mvp6-shipment-integration-rework-independent-ver-01: "no exact replayable Auth fixture").
Method (lane DB only, never 27017): for each actor in actors.tsv, generate a new random password locally, bcrypt it
exactly as Auth does (BCrypt, work factor 12 — Diten.AuthService.Infrastructure/Services/PasswordHasher.cs), replace
that one user's PasswordHash in DitenAuth_<SUFFIX>.users, then log in through the lane Gateway and check the
tenant/legal-entity claims of the real Auth-issued token. Tokens stay in memory and are discarded.
v1.0 kept the only copy of a password in W/secrets/actors.json; v1.1 keeps it only in the supervisor's memory. Cleanup
still destroys the lane database, so the credential is dead afterwards even if an operator typed it into a browser.

actors.tsv (tab-separated, header): label  email  tenant_id  expected_legal_entity_id('-' = none)  note
Mode:
  rotate   --work W --evidence E --suffix SFX --actors actors.tsv   (rotate + login check; writes E/raw/identities-a<N>.json)
Requires: python3 `bcrypt` module, mongosh; ACTOR_PW_<LABEL> in the environment (i.e. started by the supervisor).
"""
import argparse, base64, json, os, pathlib, re, subprocess, sys, urllib.error, urllib.request

SEED_HASH = re.compile(r"\$2[aby]\$12\$[./A-Za-z0-9]{53}")
JS = r"""
const a=JSON.parse(process.env.EK_ARGS_JSON);
const d=db.getSiblingDB(a.db);
const port=db.adminCommand({getCmdLineOpts:1}).parsed.net.port;
if (port===27017 || port!==a.port) { print(JSON.stringify({error:'server port '+port+' is not the lane port'})); quit(2); }
const f={Email:a.email, TenantId:{$in:[UUID(a.tenant), a.tenant]}};
const found=d.users.find(f).toArray();
const out={matched:found.length};
if (found.length===1){
  const u=found[0];
  out.pre={IsActive:u.IsActive===true, EmailConfirmed:u.EmailConfirmed===true,
           MustChangePassword:u.MustChangePassword===true, IsDeleted:u.IsDeleted===true,
           LockedOut:!!(u.LockoutEnd && u.LockoutEnd > new Date())};
  out.previousHashWasRepoSeed=a.seed.includes(u.PasswordHash);
  const ok=out.pre.IsActive && out.pre.EmailConfirmed && !out.pre.MustChangePassword && !out.pre.IsDeleted && !out.pre.LockedOut;
  if (ok) { const r=d.users.updateOne({_id:u._id},{$set:{PasswordHash:a.hash}}); out.modified=r.modifiedCount; }
  else out.modified=0;
}
print(JSON.stringify(out));
"""


def ports(work):
    return json.loads(pathlib.Path(work, "ports.json").read_text())["ports"]


def claims(token):
    part = token.split(".")[1]
    return json.loads(base64.urlsafe_b64decode(part + "=" * (-len(part) % 4)))


def login(gateway, tenant, email, password):
    body = json.dumps({"email": email, "password": password, "rememberMe": False}).encode()
    req = urllib.request.Request(f"{gateway}/api/tenant-auth/login", data=body, method="POST",
                                 headers={"Content-Type": "application/json", "X-Tenant-Id": tenant})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            payload = json.loads(r.read() or b"{}"); status = r.status; hdrs = r.headers
    except urllib.error.HTTPError as e:
        return e.code, None, []
    cookie_names = sorted({c.split("=", 1)[0] for c in hdrs.get_all("Set-Cookie") or []})
    token = (payload.get("data") or {}).get("accessToken")
    return status, token, cookie_names


def env_name(label):
    return re.sub(r"[^A-Z0-9]", "_", label.upper())


def unique(d, base, ext):
    n = 1
    while (d / f"{base}-a{n}.{ext}").exists():
        n += 1
    return d / f"{base}-a{n}.{ext}"


def rotate(a):
    import bcrypt
    work = pathlib.Path(a.work)
    p = ports(a.work)
    if p["mongo"] == 27017:
        raise SystemExit("refusing 27017")
    seeder = work / "source/services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs"
    seed_hashes = sorted(set(SEED_HASH.findall(seeder.read_text())))
    uri = f"mongodb://127.0.0.1:{p['mongo']}/?replicaSet=rs{a.suffix}"
    gateway = f"http://127.0.0.1:{p['gateway']}"
    actors = [l.split("\t") for l in pathlib.Path(a.actors).read_text().splitlines()[1:] if l.strip()]
    missing = [row[0] for row in actors if not os.environ.get(f"ACTOR_PW_{env_name(row[0])}")]
    if missing:
        raise SystemExit(f"ACTOR_PW_* missing for {missing}: K07 must run under the lane supervisor (v1.1 D4)")
    results, failed = [], False
    for label, email, tenant, exp_le, *_ in actors:
        pw = os.environ[f"ACTOR_PW_{env_name(label)}"]
        h = bcrypt.hashpw(pw.encode(), bcrypt.gensalt(12, b"2a")).decode()
        args = json.dumps({"db": f"DitenAuth_{a.suffix}", "email": email, "tenant": tenant, "hash": h, "seed": seed_hashes, "port": p["mongo"]})
        proc = subprocess.run(["mongosh", "--quiet", uri, "--eval", JS], capture_output=True, text=True,
                              env={**os.environ, "EK_ARGS_JSON": args})
        args = h = None
        lines = [x for x in proc.stdout.splitlines() if x.startswith("{")]
        db = json.loads(lines[-1]) if lines else {"error": "no output", "exit": proc.returncode}
        rec = {"actor": label, "email": email, "tenantId": tenant, "expectedLegalEntityId": None if exp_le == "-" else exp_le,
               "db": db, "passwordSource": "lane supervisor memory", "passwordPersisted": False}
        if db.get("matched") == 1 and db.get("modified") == 1:
            status, token, cookies = login(gateway, tenant, email, pw)
            rec["login"] = {"via": "gateway", "status": status, "setCookieNames": cookies}
            if token:
                c = claims(token)
                rec["login"].update({"claimNames": sorted(c), "tenantClaimMatches": str(c.get("tenant_id")) == tenant,
                                     "legalEntityClaim": ("absent" if "legal_entity_id" not in c else
                                                          ("matches" if exp_le != "-" and c["legal_entity_id"] == exp_le else "present-other")),
                                     "tokenPersisted": False})
                token = None
            ok = status == 200 and rec["login"].get("tenantClaimMatches") and (exp_le == "-" or rec["login"]["legalEntityClaim"] == "matches")
        else:
            ok = False
        pw = None
        rec["result"] = "PASS" if ok else "FAIL"; failed |= not ok
        results.append(rec)
    raw = pathlib.Path(a.evidence, "raw"); raw.mkdir(parents=True, exist_ok=True)
    out = unique(raw, "identities", "json")  # G3: a rerun never overwrites the previous attempt
    out.write_text(json.dumps({"method": "lane-local bcrypt(12) rotation of seeded users in DitenAuth_" + a.suffix,
                               "seedHashesKnown": len(seed_hashes), "actors": results}, indent=2) + "\n")
    print(f"K07 {'FAIL' if failed else 'PASS'}: " + ", ".join(f"{r['actor']}={r['result']}" for r in results) + f" ({out.name})")
    sys.exit(1 if failed else 0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["rotate"])
    ap.add_argument("--work", required=True)
    ap.add_argument("--evidence", required=True); ap.add_argument("--suffix", required=True); ap.add_argument("--actors", required=True)
    rotate(ap.parse_args())


if __name__ == "__main__":
    main()
