# Owner decision Q03 — commit strategy (NOT APPROVED — prepared text only)

Put to the owner one question at a time: Q03a, then Q03b, then Q03c. Nothing is decided by this file. Record each answer as a separate owner decision record under `docs/records/decisions/2026-09/`.

## Q03a — commit sequence

> "The owner approves turning the uncommitted MVP6 working tree into LOCAL commits on `feature/mvp6-logistics`, in the order and with the exact path lists of `docs/roadmap/plans/mvp6-commit-plan-01/` (`COMMIT-SEQUENCE.md`, `pathspec/`, SHA256SUMS as recorded), from a local Mac session only, after a fresh GIT-001 backup, with the per-commit gate G1–G8. Paths added after the plan snapshot are classified by the same rules into the matching or a trailing commit before staging. No push, stash, reset, rebase, amend or `--no-verify`."

| Option | Effect |
|---|---|
| **A — Ordered sequence C01–C14 (recommended)** | Guard + contracts first, so the docs-path guard is green at every commit; product per module (Carrier, Loads, Shipment root + `Program.cs`); packs/DCP; decisions; records per module family; plans; evidence kit on its own. Traceable and checkable. |
| B — Two commits (all product, all docs) | Faster. Weak traceability; the guard state between the two commits is unknown; very large review units. |
| C — One commit per WP folder (~260 commits) | Maximum traceability; long and error-prone in a manual Mac session; the sealed inputs still force a combined guard commit. |

## Q03b — exclusions and holds

> "The owner approves the exclusions in `EXCLUSIONS.tsv`: X1 generated test output (8 files) and X2 `.claude/settings.local.json` are not committed. The four H1 files stay out until the owner confirms their values are not a secret of any shared, development or production environment; on confirmation they go in commit H01. The 45 S1 files are committed: their flagged values are lane-local test signing secrets or test constants, the same convention as the dev/test values already in HEAD. The K7 files over 1 MB are committed because CT records reference them by exact hash."

| Option | Effect |
|---|---|
| **A — Exclude X1/X2, hold H1, commit S1 and K7 (recommended)** | Removes machine-local and generated noise. Nothing that might be a real credential enters history before confirmation, and all accepted evidence keeps its hashes. |
| B — Stricter: also hold all 45 S1 files | Nothing with a secret-like literal enters history. But key identities (A12 successor source, Carrier source archives, the Loads probe in the product tree) stay out, so their commits are incomplete and records that cite them no longer verify. |
| C — Commit everything, including X1, X2 and H1 | Simplest. It puts machine-local permission settings, generated TRX and a raw bearer token into history, which can't be removed later without a history rewrite (blocked by the working mode). Not recommended. |

## Q03c — branch naming (AGENTS.md §9, audit AG-09)

> "The owner decides the branch for MVP6 commits: [option]. Any deviation from AGENTS.md §9 is recorded in the commit record."

| Option | Effect |
|---|---|
| **A — Keep `feature/mvp6-logistics` until the end of MVP6 (recommended)** | Every record, prompt and decision cites this branch and HEAD. §9 is one branch per module (`feature/{domain}/{module-id}-{slug}`), and MVP6 spans 9+ modules in one integrated effort. The §9 domain list has no supply-chain code. The deviation is recorded once; the final push/PR split is decided at the end. |
| B — Per-module §9 branches now (e.g. `feature/<code>/mod-0184-carrier-management`) | Follows §9 literally. Splitting one working tree into several branches needs checkout/cherry-pick, which the working mode blocks; it needs a new supply-chain domain code in AGENTS.md (a rule change), and it breaks every existing branch citation. |
| C — One renamed umbrella branch (e.g. `feature/sce/mvp6-logistics`) | Closer to §9's form without splitting. Still needs a new domain code in AGENTS.md and a branch switch; it breaks existing citations for little gain. |
