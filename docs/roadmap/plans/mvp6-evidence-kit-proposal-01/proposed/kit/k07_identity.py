#!/usr/bin/env python3
"""CANDIDATE — NOT ACTIVE. MVP6 evidence kit phase K07: fresh real-Auth identities, secrets generated locally.

Problem it closes: the Auth seeder gives every seeded user ONE shared, committed bcrypt hash (DataSeeder.cs), so a
seeded login proves nothing about who holds the credential, and no earlier lane archived a replayable identity step
(mvp6-shipment-integration-rework-independent-ver-01: "no exact replayable Auth fixture").
Method (lane DB only, never 27017): for each actor in actors.tsv, generate a new random password locally, bcrypt it
exactly as Auth does (BCrypt, work factor 12 — Diten.AuthService.Infrastructure/Services/PasswordHasher.cs), replace
that one user's PasswordHash in DitenAuth_<SUFFIX>.users, then log in through the lane Gateway and check the
tenant/legal-entity claims of the real Auth-issued token. Tokens stay in memory and are discarded.
The only copy of a password is W/secrets/actors.json (0600); K11 deletes it together with the lane database, so
the credential is dead after cleanup even if an operator typed it into a browser.

actors.tsv (tab-separated, header): label  email  tenant_id  expected_legal_entity_id('-' = none)  note
Modes:
  rotate   --work W --evidence E --suffix SFX --actors actors.tsv   (rotate + login check; writes E/raw/identities.json)
  reveal   --work W --actor LABEL     print one password to the terminal for manual browser entry (never to a file)
Requires: python3 `bcrypt` module, mongosh.
"""
import argparse, base64, json, os, pathlib, re, secrets, subprocess, sys, urllib.error, urllib.request

SEED_HASH = re.compile(r"\$2[aby]\$12\$[./A-Za-z0-9]{53}")
JS = r"""
const fs=require('fs'); const a=JSON.parse(fs.readFileSync(process.env.EK_ARGS,'utf8'));
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


def rotate(a):
    import bcrypt  # imported here so `reveal` works without it
    work = pathlib.Path(a.work); sec = work / "secrets"; sec.mkdir(mode=0o700, exist_ok=True)
    p = ports(a.work)
    if p["mongo"] == 27017:
        raise SystemExit("refusing 27017")
    seeder = work / "source/services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs"
    seed_hashes = sorted(set(SEED_HASH.findall(seeder.read_text())))
    uri = f"mongodb://127.0.0.1:{p['mongo']}/?replicaSet=rs{a.suffix}"
    gateway = f"http://127.0.0.1:{p['gateway']}"
    actors = [l.split("\t") for l in pathlib.Path(a.actors).read_text().splitlines()[1:] if l.strip()]
    store, results, failed = {}, [], False
    for label, email, tenant, exp_le, *_ in actors:
        pw = secrets.token_urlsafe(24)
        h = bcrypt.hashpw(pw.encode(), bcrypt.gensalt(12, b"2a")).decode()
        args = sec / f"args-{label}.json"
        fd = os.open(args, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        with os.fdopen(fd, "w") as f:
            json.dump({"db": f"DitenAuth_{a.suffix}", "email": email, "tenant": tenant, "hash": h, "seed": seed_hashes, "port": p["mongo"]}, f)
        proc = subprocess.run(["mongosh", "--quiet", uri, "--eval", JS], capture_output=True, text=True,
                              env={**os.environ, "EK_ARGS": str(args)})
        args.unlink()
        lines = [x for x in proc.stdout.splitlines() if x.startswith("{")]
        db = json.loads(lines[-1]) if lines else {"error": "no output", "exit": proc.returncode}
        rec = {"actor": label, "email": email, "tenantId": tenant, "expectedLegalEntityId": None if exp_le == "-" else exp_le,
               "db": db, "passwordGeneratedLocally": True, "passwordPersistedInEvidence": False}
        if db.get("matched") == 1 and db.get("modified") == 1:
            store[label] = {"email": email, "password": pw}
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
        rec["result"] = "PASS" if ok else "FAIL"; failed |= not ok
        results.append(rec)
    fd = os.open(sec / "actors.json", os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, "w") as f:
        json.dump(store, f)
    raw = pathlib.Path(a.evidence, "raw"); raw.mkdir(parents=True, exist_ok=True)
    (raw / "identities.json").write_text(json.dumps({"method": "lane-local bcrypt(12) rotation of seeded users in DitenAuth_" + a.suffix,
                                                     "seedHashesKnown": len(seed_hashes), "actors": results}, indent=2) + "\n")
    print(f"K07 {'FAIL' if failed else 'PASS'}: " + ", ".join(f"{r['actor']}={r['result']}" for r in results))
    sys.exit(1 if failed else 0)


def reveal(a):
    store = json.loads(pathlib.Path(a.work, "secrets", "actors.json").read_text())
    if a.actor not in store:
        raise SystemExit("unknown actor")
    print(store[a.actor]["password"])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["rotate", "reveal"])
    ap.add_argument("--work", required=True)
    ap.add_argument("--evidence"); ap.add_argument("--suffix"); ap.add_argument("--actors"); ap.add_argument("--actor")
    a = ap.parse_args()
    rotate(a) if a.mode == "rotate" else reveal(a)


if __name__ == "__main__":
    main()
