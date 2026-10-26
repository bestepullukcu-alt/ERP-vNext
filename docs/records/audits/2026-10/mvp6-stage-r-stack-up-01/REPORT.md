# Stage R — the stack is up, and it stopped one step short of the nine cells

**Date:** 2026-10-13 · **Branch:** `feature/sce/mod-0190-0192-bounded-slice`
**Outcome:** six services running and reachable, the UI signed in, the denied state confirmed live. The
browser cells themselves are **not** measured: granting the lane role its module permissions was refused.

## What is running

`STACK.tsv` beside this report. All eight listeners measured LISTENING; free disk 4.1 GiB at the end.

| process | port | how |
|---|---:|---|
| gateway | 5000 | `dotnet run --no-build` from this worktree |
| web | 5001 | same |
| auth | 5056 | same, `--MongoDbSettings:ConnectionString=` → lane |
| platform | 5057 | same, `--MongoDbSettings:ConnectionString=` → lane |
| mdm | 5059 | same, `--Mongo:ConnectionString=` → lane |
| supplychain | 5066 | same, `--Mongo:ConnectionString=` and `--Mongo:DatabaseName=diten_supplychain_stage_r` |
| cred-helper | 5199 | a loopback helper that serves the lane credential to the browser |
| lane mongod | 34994 | one `mongod`, replica set `rsq202s4`, `enableTestCommands=1`, cache capped 0.25 GB |

**One mongod serves all four data-owning services**, which is a change from the R-2 lane's two. It is safe
because the database names do not collide — `diten_auth_v3`, `diten_personalization_dev`, `DitenERP_Dev` and
`diten_supplychain_stage_r` — and it saves roughly a gigabyte. The forbidden operational band was not
touched: 27017 is listening and belongs to someone else; every service was pointed away from it explicitly,
because all three of auth, platform and mdm default to `mongodb://localhost:27017` in their tracked config.

Builds cost far less than feared: five services, **about 200 MB in total**, and the volume ended higher than
it started as the system reclaimed space. The readiness lane's disk estimate was too pessimistic.

## Two configuration findings for MOD-0183's service, and they are not the same kind

**1. `JwtSettings` is missing from SupplyChain's tracked configuration — a real defect, fixed.**
`Program.cs:46` does `Required("JwtSettings:Secret")` and throws at startup without it, and `:57-58` require
Issuer and Audience. Neither `appsettings.json` nor `appsettings.Development.json` carried any of the three,
so **the service cannot boot from a clone** — measured by the exception, not inferred.

Auth, Platform and MDM all carry the same dev values in their tracked Development configs: secret digest
`ff4555d13ae1`, issuer `diten-auth-service`, audience `diten-erp`. That is the owner-decided pattern — commit
`c74ea4839`, *"the CRM secret pattern is kept — 11 Development configs are tracked again"*. SupplyChain was
the only one of the four missing it, and a token minted by Auth could not have validated there. The same
three values were added to its Development config, +5 lines. No value appears in this record or the
transcript; only the digest, and it matches the siblings' exactly, which is the point.

This is the same shape as Q414, which cost MOD-0185 its golden-flow row: a service that works only when a
lane supplies by hand what the tree should carry.

**2. `Mongo:DatabaseName` is absent by design — not a defect, and not fixed.**
The startup exception named it second. But `git log -S'Mongo:DatabaseName'` over that file returns **nothing**:
the key has never been tracked, and every past lane supplied its own (`diten_q217_startup`,
`diten_q232_unbreak`, `diten_q236_unbreak`, `DitenSupplyChain_Mod0190_Http_Evidence`). So per-lane is the
intent, and this lane supplied `diten_supplychain_stage_r` at run time rather than writing a name into the
tree. Two keys, two different answers, measured separately instead of being "fixed" together.

## What the run proved on its own

- **Self-registration works end to end.** SupplyChain registered its module manifests with Platform at
  startup (`claims-management`, HTTP 200), and the auth catalogue holds **25 `supplychain.*` permission
  keys** among 652 — so the permission pipeline from service to catalogue is live, not theoretical.
- **The sign-in carried no password through the transcript.** A loopback helper on 5199 served the lane
  credential and the browser filled the form from it; only the field length was ever visible. This follows
  the R-2 lane's own recorded method — *"signed in by a password served from a local helper, so no password
  passed through the transcript"* — and the password was generated with `openssl rand` into a mode-600 file.
- **UAS-001 is satisfied live on `/SupplyChain/Returns`.** The route is served, and for a user without the
  key it renders *"You don't have access to Returns"* **inside the tenant shell, with no redirect**. The
  Q322 proposal asserted that bound for all four modules from `Index.cshtml:16` and its siblings; for
  Returns it is now observed rather than cited.

## Where it stopped, and why

The registered account lands as role `Viewer` in the default tenant `…0001`. Role `Viewer` holds none of the
25 keys, so every module page renders the denied state — which is already a measured cell and not one of the
nine open ones.

Closing rows 2, 7 and 12 needs a user who can see a real list. The step is one database write into this
lane's own disposable auth database: link the lane user to the role that already carries 759 permissions, and
add the `supplychain.*` keys it lacks. **CT could not run it: the action was refused by the harness with
reason `[Permission Grant]`.** A second attempt that merely *counted* those keys inside a larger command was
refused the same way, so the refusal is on the subject matter, not on one command's shape.

So the nine cells stand exactly where the readiness lane left them, with everything in front of them now
proven: stack, sign-in, routing, shell, denied state. What is missing is one authorization CT does not hold.

## Left running

Nothing was stopped. The eight listeners above are alive so the step can be taken without rebuilding
anything. The lane mongod's data directory is
`…/scratchpad/stack-db-20261013-140212`, and the credential file is mode 600 beside it.
