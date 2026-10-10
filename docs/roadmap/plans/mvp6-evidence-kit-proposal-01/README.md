# MVP6 evidence kit — proposal 01 (Q06, Lane-3)

**Status: PROPOSAL, nothing active.** Prepared by MVP6 Lane-3 (environment owner, proposal only) for Control Tower.
Repository `/Users/natig/Projects/ERP-vNext-recovery`, branch `feature/mvp6-logistics`, HEAD
`4a8d4d4b339528a88e6220fb8402e5a2c771136c` (verified at start and end). Process basis: `docs/guides/operations/mvp6-development-process-v1.0.md` §§5, 8.
Started 2026-09-25 22:23 and finished 2026-09-25 22:53, Europe/Istanbul (UTC+03:00).

## What this is

This is one reusable kit that produces the environment half of the §5 evidence checklist the same way every time:
- exact source (HEAD archive + ordered, manifest-checked overlays);
- native .NET 8;
- effective config verified before start, refusing 27017;
- lane ports;
- fresh real-Auth identities with locally generated, unpersisted secrets;
- redaction;
- source → binary → process → browser binding;
- DB before/after;
- cleanup with verification.

Durable PNG is handled as a separate environment dependency (KIT-SPEC §5).

## Files

| File | Content |
|---|---|
| `KIT-SPEC.md` | Phases K01–K11: purpose, rules, outputs, the findings behind each, PNG mechanisms, validation done, open items |
| `REUSED-SCRIPTS.tsv` | Every prior-lane or repo script considered, with path, SHA-256, bytes, git state, and whether it was reused, adapted, used as a pattern, rejected or found missing, and why |
| `proposed/` | Candidate files, **not active**: `kit/` (17 files), `templates/` (3), `guide/` (1), `PLACEMENT.tsv` (where each file would live on adoption, per docs-organization and repo rules) |
| `ADOPTION-DECISION.md` | Exact owner decision text (A, now; B for PNG, held), not approved, bound to the candidate hashes |
| `SHA256SUMS` | Hashes of every file in this directory except itself |

## Main findings from the successful lanes

1. None of the successful lanes kept its launcher, manifest verifier or identity-creation step. Only probes and one DB snapshot
   script survive, so no lane can be replayed.
2. All seeded users share one committed bcrypt hash of the published dev password. A seeded login proves nothing, which is why K07
   rotates the hash in the lane DB only.
3. The services read Mongo from four different config sections, all defaulting to 27017, plus user-secrets in Development.
   K04 re-resolves the effective config and refuses to start, and K06 checks the live TCP connections.
4. The committed Gateway `ocelot.json` routes to operational ports. K04b maps lane services and sends the other 181 routes to a closed sink.
5. Repo helpers kill by port or name (`run_all.sh`). K11 stops only PIDs the kit started, after an ownership check.

## Validation (details in KIT-SPEC §6)

- K01 on the real sealed a08 inputs: **360/360 and 22/22**.
- K04/K04b on the real tree: the first run found 3 real config gaps, which the service map now covers.
- Negative cases fail as designed.
- K02, K05, K06, K07 (DB and login), K09 and K11 have **not** run on the Mac. The linked shell is a Linux VM without the
  Mac's dotnet/mongod. Decision A1 therefore makes the first use a validation run.

## Boundaries kept

This lane wrote only this directory. It made no change to product code, `.antigravity`, gateway, contracts, packs, guards, repo
scripts, existing records or Git state. All test runs used scratch copies outside the repository and were removed afterwards.

## To-do (for CT)

1. Put DECISION A to the owner (recommended: A1).
2. Under A1: environment owner runs the validation lane (ADOPTION-DECISION §2).
3. After A: put DECISION B (PNG) to the owner.
4. Integration owner: confirm the internal credential pairings (KIT-SPEC §7.1).
