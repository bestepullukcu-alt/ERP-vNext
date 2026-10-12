# RESUME HERE — MVP-6 Control Tower handoff (written 2026-10-03, before a session restart)

A session restart loses the conversation. This file is the resume point. Read it after `AGENTS.md`.

## Where things stand

Branch `feature/mvp6-logistics`, HEAD `4a8d4d4b3`, **0 commits today**, 665+ uncommitted entries
under standing owner decision OD-Q03a = "no commit for now".

The system of record is `CT-QUEUE.tsv` in this folder — **352 rows**, latest chain hash `ee4ad57e`.
Read its tail before doing anything. Known defect: 77 surplus rows across 60 IDs with conflicting
states; trust the LAST row for an ID.

## The one thing that was blocking everything

`.claude/settings.local.json` denied `Edit(services/**)`, `Edit(execution/**)`, `Edit(gateway/**)`.
The owner chose to install `docs/records/decisions/2026-10/permission-gate/settings.local.PROPOSED.json`,
which moves those six entries to `ask` and drops 60 inert `Write()`/`NotebookEdit()` forms.

**First action on resume — verify it actually installed:**

```
python3 -c "import json;p=json.load(open('.claude/settings.local.json'))['permissions'];print('allow',len(p['allow']),'deny',len(p['deny']),'ask',len(p.get('ask',[])))"
```

Expect `allow 123 deny 52 ask 6`. If it still says `deny 118 ask 0`, the install did not happen and
everything below is still frozen — tell the owner, do not work around it.

`Edit(frontend/**)` and `Edit(docs/analysis/**)` stay DENIED by deliberate owner choice. Do not
assume they were an oversight; see that folder's README.

## Then, in this order

**1. Q236 — make the service boot.** The service does not start in Development: MediatR registers
handlers for four feature modules (`Returns`, `Claims`, `SandopPlans`, `CapacityPlans`) whose
repositories `Program.cs` never composes, so container validation fails at `builder.Build()` and
takes Shipments, Carriers and Loads down with it.

The fix is already written and K3-proven (change present → UP on 5061 with 0 unresolved; reverted →
exit 134 in 1 s with 18 unresolved; restored → UP again; A and C produce the same `Application.dll`
hash; 416 of 417 tests pass, the one failure is a known by-design Claims replay test):

```
diff -u services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/DependencyInjection.cs docs/records/audits/2026-10/mvp6-q236-unbreak-v2-01/proposed/DependencyInjection.cs.txt
```

Apply those bytes (target sha256 starts `f05c1fcd`). Full evidence in
`docs/records/audits/2026-10/mvp6-q236-unbreak-v2-01/` — `SABOTAGE-PROOF.md`, `MECHANISM.md`,
`TEST-RESULT.tsv`. Then start the service and confirm it listens on 5061 yourself; do not take the
record's word for it (K2).

**2. Then C-01, C-02, C-03 in parallel** — three separate paths, no write collisions:

| | work | path |
|---|---|---|
| C-01 | The pack says the module "creates no Razor UI" and frontend is "Not applicable" (pack:9-11,177,191) while 10 `.cshtml` views, a web controller and 7 languages × 63 l10n keys exist. AGENTS.md §1 makes the pack authority, so the tenant UI is **out of contract today** — a standing K12 violation. Give the pack a UI contract (screen list, field count, golden reference). | `execution/**` |
| C-02 | The backend returns code `INVALID_REQUEST` for **both** 500 and 403 (`ExceptionHandlingBehavior.cs:12`, `HasPermissionAttribute.cs:16`); the UI expects `PERSISTENCE_UNAVAILABLE` / `INTERNAL_ERROR`, which the backend emits in **0 files**. The UI cannot tell a server failure from a validation error. | `services/**` |
| C-03 | `gateway/Diten.ApiGateway/ocelot.json` contains **0** occurrences of `shipment-bundle`, while every web adapter calls `{GatewayUrl}/api/...` (`SupplyChainShipmentsController.cs:63,72,82,96,108`). | `gateway/**` |

The full plan with all 11 items is `mod-0183-closure/CLOSURE-PLAN.tsv` in this folder; its README
explains why four of the seven SOP §18.0 NOT MET rows close without `frontend/**` and three do not.

## Rules that are easy to break here

- `.antigravity/` is **never auto-loaded**. Read the agent/rule files explicitly or you are working
  without the contract (AGENTS.md §6.1).
- **GIT-002:** `git add -A` and `git add .` are forbidden; explicit paths only; two-stage review
  (`git diff --cached --name-only` then `git diff --cached`); **commit only after explicit owner
  approval**, which does not exist — OD-Q03a says no commit for now.
- **No `rm`.** Reset means a new timestamped folder plus `cp`.
- **Q248:** scratch and chat are not system-of-record. The 00:30:18 restart emptied `/private/tmp`
  and destroyed the evidence of five accepted work packages. Write artefacts under `docs/records/`.
- Platform modules need 2 languages, tenant modules 7 (en, tr, fr, es, zh, ar, ru).

## Owner rulings still outstanding

Q255 (POD evidence: waive SOP §18.0 here with owner+expiry per K19, or amend the pack), Q235 (the
exposed signing secret — CT recommends rotating it; the location list grew twice, now 17 archives +
9 `refs/codex` refs + `.git-backups` + 18 worktree files), the G2 waiver expiry (K19), and
`Edit(frontend/**)` (which freezes C-07, C-08, C-09 and Q242).

## Open and unexplained

Q249: free space has read 4.7, 6.9, 5.5, 7.2, 8.0, 11.42, 11.67 and 11 GiB with nothing building.
CT tested the APFS local-snapshot hypothesis and **falsified it** (`tmutil listlocalsnapshots /`
returns zero). No replacement hypothesis; per K10 no guess is recorded. The crisis dissolved in
practice, so it is a watch item, not a blocker.

Q251: Q245's 105-row pack-conformance matrix was never persisted and exists only in a chat report.
Do not build on its numbers.
