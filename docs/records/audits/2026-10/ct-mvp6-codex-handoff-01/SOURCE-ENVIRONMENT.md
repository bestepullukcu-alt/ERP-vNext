# SOURCE AND EXECUTION ENVIRONMENT

Documenting a command here is **not** authority to run it. Build, runtime, commit, push, stash and
merge were all out of scope for the session that wrote this package.

## 1. Repositories and checkouts

| | |
|---|---|
| primary checkout | `/Users/natig/Projects/ERP-vNext-recovery` |
| remote | `git@github.com:bestepullukcu-alt/ERP-vNext.git` (fetch and push) |
| branch | `feature/mvp6-logistics` |
| HEAD | `5e9f2b7249beaca7f3980f4cb17d23cdd83dc7de` — a merge commit, parents `83d076e83` + `6b8a62d5a`, authored `gmg-10 <gm@grandmedical.eu>` 2026-10-15 12:26:47 +0300 |
| working tree | clean apart from 6 untracked paths (listed in `SOURCES.tsv` as EXTERNAL/UNTRACKED) |
| no merge or rebase in progress | confirmed — no `.git/MERGE_HEAD`, no `rebase-merge`, no `rebase-apply` |
| local `main` ref | `01035898a` — **stale**, do not read it as main |

### Registered worktrees — 26 total

**Holds unsecured work (read this one first):**

| path | branch / HEAD | state |
|---|---|---|
| `/Users/natig/mvp6-env/bs-0190-0192-20261011-1411` | `feature/sce/mod-0190-0192-bounded-slice` @ `15f9cb9b6` (**already merged into the primary branch**) | 1 modified config, 5 untracked evidence directories, 2 untracked test files |

**Other `mvp6-env` worktrees (external absolute paths, outside the repo):**
`contracts-33-20261013-123316` (`contracts/shipment-bundle-3.3-demand-history` @ `4b4722ece`),
`fin-treasury-20261011-1915`, `g5-20261010-1634`, `ins-contract-20261011-1340`,
`m0190-0192-20261011-1251`, `q214-20261011-1452`, `ver-logistics-20261011-1340`,
`ver-runtime-20261011-1503`, `walk-20261011-1529`.

**Prunable `/private/tmp` worktrees — 9, all detached or codex branches at `4a8d4d4b3`:**
`mvp6-integration-baseline-422-01`, `mvp6-mod0186-http-integration-01`,
`mvp6-mod0186-r01-rework-01`, `mvp6-mod0187-malformed-map-rework-01`,
`mvp6-mod0190-http-integration-02`, `mvp6-mod0192-capacity-uptake-01`,
`mvp6-mod0192-core-dev-01`, `mvp6-mod0192-http-integration-01`,
`mvp6-shipment-pod-ui-exec-01`. These are on a temp volume and will not survive a reboot.

**`~/.codex/worktrees` — 6:** `claims-dev-start-01`, `mod-0190-isolated-dev-01`,
`mvp6-carrier-ui-dev`, `mvp6-mod0190-core`, `returns-phase15-close-01`,
`shipment-integration-rework-exec-01`. All at `4a8d4d4b3`.

## 2. Main synchronisation — measured 2026-10-16

| | |
|---|---|
| `origin/main` | `3912d568b18193f627539a1b27965167ac4077b6`, 2026-10-15 14:30:45 +0300, "Merge pull request #146 from bestepullukcu-alt/feature/sce/mod-0188-demand-planning" |
| merge base | `6b8a62d5a68082c41da202e0e92f765e20461b02` (PR #144, `chore/ct-round-2`) |
| divergence | **29 commits behind, 18 ahead** |
| conflict test | `git merge-tree --write-tree origin/main HEAD` → exit 0, result tree `aaa8abd49661f0cfa61435f29b048b7a705734d4`, **no conflicts reported** |
| fetch freshness | fetched 2026-10-16 during this session |

### What the 29 incoming commits contain

Almost entirely **MOD-0188 Demand Planning** — a whole new service:
`services/Diten.PlanningService/src` (168 paths), `tests` (47), `contracts` (2), a README,
`scripts/test-env/mod0188-ci-mongo.sh`, plus `frontend/Diten.Web` Resources/Views/Controllers and
`execution/domains/supply-chain-execution` (14 paths — note this is **our** domain directory).

Second: **PR #145, the İ5-A agent-loading pilot**, which touches `AGENTS.md` (4 insertions,
3 deletions), `.claude/agents/`, `.claude/rules/localization-standard.md`, `.claude/skills/resx-check`,
`.codex/agents/`, `.antigravity/workflows/add-module.md`, `.antigravity/scripts/sync_adapters.py`,
and adds `.github/workflows/agent-adapters.yml`.

Third, and the two to check by hand after merging:
`gateway/Diten.ApiGateway/ocelot.json` and `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs`
— main adds **port 5068 for Planning** to `KnownDownstreamPorts`. SupplyChain's 5066 is already in
that set on both sides, so this is additive, but `ocelot.json` is named in the owner decision's
**bucket 3: semantic merge, never ours/theirs wholesale**.

Only one of the nine handoff modules appears in the incoming diff, and it is not one of them:
`MOD-0188`, 1 path. **Zero incoming changes to `services/Diten.SupplyChainService`.**

### The merge is governed by an owner decision, not by convenience

`docs/records/decisions/2026-10/mvp6-merge-origin-main-owner-decision-01.md` (2026-10-05, ACCEPTED):
**merge, never rebase** — rewriting the 18 commits re-identifies them and costs audit traceability.
It also sets a **completion gate**, quoted verbatim:

```
merge conflicts resolved
zero known secret material in the resulting tree
Platform / Auth targeted regression passes
gateway validation passes
frontend baseline passes
five SupplyChain golden flows rerun
required MVP-6 gates rerun
```

and states: *"This merge is therefore not housekeeping. It is the MVP-6 integrated acceptance gate."*

**Two merges have already been executed under it** (`19497c934` fast-forward and `83d076e83` on
2026-10-14, `5e9f2b724` on 2026-10-15) and no post-merge gate record exists under
`docs/records/audits/2026-10/`. That is finding **F5**. The decision also marks pre-merge evidence
`VALID_PRE_MERGE / INTEGRATION_STALE` and forbids claiming an integrated PASS until
re-verification completes (K18).

## 3. Owned source vs. full build input

**Owned source** — what the MVP-6 lanes may write:

- `services/Diten.SupplyChainService/**` (src and tests)
- `execution/domains/supply-chain-execution/module-packs/MOD-01{47,48,83,84,85,86,87,90,92}*.md`
- `docs/records/audits/2026-10/**` (new lane directories only; **K4 — a written record is never corrected**)
- `docs/roadmap/plans/**` (new packages), `docs/roadmap/backlog/product-backlog.md`
- `frontend/Diten.Web` SupplyChain Views/Controllers/Resources for the approved UI scopes

**Protected / not ours**: `gateway/**` is Protected Paths in every pack. `AGENTS.md`,
`.antigravity/**`, `.claude/**` are standards surfaces — and main is about to change them.

**Full build input** is much larger than owned source: the SupplyChain test suites reference
`Diten.BuildingBlocks`, Platform, Auth and MDM contracts, so a build pulls the whole solution.
Owned-source manifest ≠ build-input manifest; do not infer one from the other.

**In Git vs. worktree-only:** everything in `SOURCES.tsv` marked `IN-GIT` is reachable from HEAD.
The 35 evidence files, 2 test files and 1 config edit under **F1/F2/F3** are **worktree-only** and
exist at exactly one absolute path outside the repository.

## 4. Toolchain, as measured on this machine

| | |
|---|---|
| `dotnet --version` | `8.0.417` |
| `mongod` | `/opt/homebrew/bin/mongod` |
| `node --version` | `v25.5.0` |
| `dotnet-counters` | `10.0.745401` at `/Users/natig/.dotnet/tools` — **not on the default PATH**; add `~/.dotnet/tools` (owner decision 2026-10-04) |
| platform | darwin 25.5.0, zsh |

## 5. Ports

```
5000 Gateway · 5001 Web · 5056 Auth · 5057 Platform · 5058 DevEnablement
5059 MDM · 5060 HCM · 5061 CRM · 5062 PPM · 5063 HumanCapital · 5064 TEP
5065 Procurement · 5066 SupplyChain · 5067 Inventory · 5068 Planning (incoming on main)
5070 Manufacturing · 5011 PVG · 5004 ESBP
```

Two traps, both of which have already cost a gate row:

- **SupplyChain is 5066, not 5061.** Owner decision OD-P1 renumbered it and gave 5061 to **CRM**.
  Q457's fix was recorded as 5061 and had already drifted once before anything caught it.
  `TrackedConfigurationGuardTests` now asserts this (Q491, and it **is** committed).
- `.claude/launch.json` defines 8 configurations — `web` 5001, `web-verify` 5011, `gateway` 5000,
  `auth` 5056, `platform` 5057, `mdm` 5059, `mockups` 5099, `mockups-workspace` 5098 — and
  **SupplyChain is not one of them.** The Stage R stack started it outside launch.json.

## 6. Database and port isolation, fixtures

- Per-module Mongo URI variables, one per module. Capacity is `MVP6_MOD0192_MONGO_URI`. The Q266
  recipe on port 57061 gives 417/418. **Platform tests hard-code 27017** — do not point them elsewhere.
- The Stage R lane ran **one** mongod for all four data-owning services because their database
  names do not collide, saving roughly a gigabyte against the R-2 lane's two.
- `Mongo:DatabaseName` has **never** been tracked (`git log -S` finds nothing); every lane supplies
  its own. `Loads:ReferenceBaseUrl` and its siblings **are** tracked. Two keys, two answers.
- MOD-0190 and MOD-0192 are **fixture-bound for the whole of MVP-6** by owner decision — test-only
  DEMAND fixtures, routes answer 404, no real demand data. Do not try to compose them.

## 7. Browser evidence and the nine open cells

Nine GATE-18-0 cells are browser-only (rows 2×4, 7×2, 12×3). Rows 7 and 12 already have their
**server** halves evidenced in the suites; only the browser halves are open.

Everything in front of them is proven: stack up (6 services + loopback credential helper + 1 mongod,
all measured LISTENING), sign-in works, routing/shell/denied-state confirmed live on all four modules
(UAS-001 observed, not cited), and row 2's Save View chain is measured end to end through
`_IndexL10n` → `index.js` → `personalization-client.js` → `PersonalizationProxyController` →
`ocelot.json` → Platform `SavedViewsController.cs:13`.

**Credential method that leaked nothing, and should be reused:** a loopback helper served an
`openssl`-generated credential from a mode-600 file, so no password passed through the transcript.

## 8. Known blockers and cleanup requirements

- **Disk.** The data volume filled twice (2026-10-12 and 2026-10-14). The second time it cost the
  session every shell command. A lane mongod dies **silently** when the volume fills and takes a whole
  suite red with it (Q490: FTDC id 9399800, `"s":"F"`, "Terminating the process"). Cap stack logs and
  measure free space every turn; do not leave six services running across a long read-only excursion.
- **797 tracked build-output files** under `.tmp-*/Debug/net8.0/`, including 24 appsettings copies
  carrying a dev `JwtSettings.Secret` (**F7**).
- **9 prunable `/private/tmp` worktrees** on a temp volume.
- **No `rm`.** Project rule: never delete; reset means a new timestamped folder plus `cp`.
