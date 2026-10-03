# Proposed LOCAL commit sequence — Q03a option A (NOT APPROVED)

Branch `feature/mvp6-logistics`, base `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Commits are made **only from a local Mac session** (working mode 2026-09-26); no push until the end of MVP6. Every pathspec file lists **exact file paths** (no folder, no wildcard); `git add -A` / `git add .` stay forbidden (GIT-002 §3).

## Step 0 — before the first commit (no commit)

1. Fresh non-invasive backup (GIT-001 §A: bundle + working-tree patch + untracked tar + SHA256SUMS). The 09:41 backup holds 3,652 untracked entries; the tree now has 4,065 untracked files.
2. `shasum -a 256 -c` this folder's `SHA256SUMS` → all OK.
3. Snapshot check: `GIT_OPTIONAL_LOCKS=0 git status --porcelain` against `INVENTORY.tsv`. Any path added or changed since this plan (other lanes keep writing) is classified by the same rules into the matching commit, or into a trailing commit C15, **before** staging; nothing is staged that is not in a pathspec file.
4. Owner decisions Q03a/Q03b/Q03c recorded (see `DECISION-TEXT.md`).

## Per-commit gate (every commit)

| # | Check | Pass condition |
|---|---|---|
| G1 | `git add --pathspec-from-file=docs/roadmap/plans/mvp6-commit-plan-01/pathspec/<Cnn>.txt` | exit 0 |
| G2 | `git diff --cached --name-only \| sort \| diff - <(sort pathspec/<Cnn>.txt)` | empty (exactly the planned files) |
| G3 | `git diff --cached --stat`; full `git diff --cached` for C01–C05 and C14; name + stat review for record commits | nothing unexpected (GIT-002 §3 two-stage check) |
| G4 | Secret scan of staged content (exact-value + pattern, as kit K08) | no hit outside `EXCLUSIONS.tsv` S1 rows; no H1 file staged |
| G5 | Size check: staged files > 1 MB | equal to the K7 rows for this commit |
| G6 | `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter DocsPathGuard --results-directory <evidence folder>` | green from C01 onward |
| G7 | Build/tests where named below | as stated |
| G8 | `git commit -m "<title>"` (no `--no-verify`, no `--amend`); record the hash | one commit, hash recorded in the commit record |

## Sequence

| Commit | Title (message) | Pathspec | Content | Extra checks (G7) |
|---|---|---|---|---|
| C01 | `MVP6: published contracts + docs-path guard authority (Loads 3.1.0, SANDOP-CAPACITY 3.0.0)` | `pathspec/C01-guard-contracts.txt` | 57 files, 3.2 MB, >1 MB: 1. 10 contract files (2 modified YAMLs, 8 annexes), `docs-path-authority.json`, `DocsPathGuardTests.cs`, the candidate rule in `.antigravity/rules/docs-organization.md`, the guard decision JSON and the 43 sealed inputs the authority pins | **Must be first:** the authority pins 46 files by SHA-256 (all match today; only the YAML is in HEAD). Committing them together keeps the guard green at this and every later commit. Full architecture test project green |
| C02 | `MVP6 MOD-0184 Carrier: accepted bounded source (baseline for isolated WPs)` | `pathspec/C02-product-carrier.txt` | 35 files, 0.1 MB, >1 MB: 0 | `dotnet build` SupplyChainService; `dotnet test --filter Carriers` |
| C03 | `MVP6 MOD-0185 Loads: accepted bounded source` | `pathspec/C03-product-loads.txt` | 46 files, 0.1 MB, >1 MB: 0 | build; `--filter Loads` |
| C04 | `MVP6 MOD-0183 Shipment root read + SupplyChain Program.cs composition (Carrier, Loads)` | `pathspec/C04-product-shipment-root-programcs.txt` | 12 files, 0.0 MB, >1 MB: 0. `Program.cs` registers Carrier and Loads, so it follows C02–C03 | build; full SupplyChain test project (known NON_PASS recorded, not waived) |
| C05 | `MVP6: module packs 0183–0187 and DCP-009 as signed and applied` | `pathspec/C05-packs-dcp.txt` | 6 files, 0.3 MB, >1 MB: 0 | pack hashes equal the last applied/verified hashes in the pack-apply records |
| C06 | `MVP6: owner decision records (2026-09)` | `pathspec/C06-decision-records.txt` | 24 files, 0.1 MB, >1 MB: 0 | — |
| C07 | `MVP6 records: Shipment / MOD-0183 work packages` | `pathspec/C07-records-shipment.txt` | 833 files, 17.3 MB, >1 MB: 3 | each WP folder's own `SHA256SUMS`/`ARTIFACTS.sha256` verifies after staging (spot-check the accepted ones) |
| C08 | `MVP6 records: Carrier / MOD-0184 work packages` | `pathspec/C08-records-carrier.txt` | 527 files, 54.4 MB, >1 MB: 13 | as C07 |
| C09 | `MVP6 records: Loads / MOD-0185 work packages` | `pathspec/C09-records-loads.txt` | 169 files, 2.2 MB, >1 MB: 0 | as C07 |
| C10 | `MVP6 records: Returns / MOD-0186 and Claims / MOD-0187 work packages` | `pathspec/C10-records-returns-claims.txt` | 804 files, 20.3 MB, >1 MB: 3 | as C07 |
| C11 | `MVP6 records: S&OP / Capacity (MOD-0190/0192) and BC successor` | `pathspec/C11-records-sandop-capacity.txt` | 651 files, 23.0 MB, >1 MB: 9 | as C07 |
| C12 | `MVP6 records: cross-module, publication/guard, integration and CT records` | `pathspec/C12-records-cross-module-ct.txt` | 197 files, 6.2 MB, >1 MB: 2 | as C07 |
| C13 | `MVP6: plans, backlog BL-372…384, process guide v1.0, this commit plan` | `pathspec/C13-plans-backlog-guides.txt` | 689 files, 4.5 MB, >1 MB: 0 (includes the 21 files of this folder) | — |
| C14 | `MVP6: evidence kit v1.2 install (Q24a) — installed, not validated` | `pathspec/C14-evidence-kit-install.txt` | 40 files, 0.2 MB, >1 MB: 0: `scripts/evidence-kit/`, kit guide, V1 decision record, kit proposal-review and install records | K08 scan must not flag kit code; mode bits 755/644 set on the Mac before staging |
| C15 | (only if needed) `MVP6: records added after the commit plan snapshot` | built at Step 0.3 | new paths since this snapshot, same rules | as C07 |
| H01 | `MVP6 records: held evidence after owner secret review` — only after Q03b confirmation | `pathspec/H01-held-secret-review.txt` | 4 files | per Q03b outcome |

The Q24b validation run commits its own evidence as a **separate later commit** (not part of this plan).

## Traceability notes

- The 93 product files equal, byte for byte, the input baseline that the later isolated WPs started from (`mod-0187-runtime-dispatch-01/checkout-input-manifest.tsv`, `mvp6-mod0190-core-dev-01/input-manifest.tsv`, `mvp6-mod0192-core-dev-01/TRANSFERRED-INPUTS.tsv`: 93/93). The only difference from the Shipment successor manifests is `Program.cs` (checkout `7fdb5ef0…` = pre-integration composition). Accepted isolated successors (Claims, Returns, S&OP, Capacity, Shipment UI, Auth overlay) stay in their sealed archives; they enter the tree only through integration (Q14/Q15).
- C01 takes the 43 sealed inputs out of 20 WP/plan folders; the rest of those folders follow in C07–C13. The commit record lists them, and a folder's own checksum file verifies only once all of its commits are in.
- Record grouping uses the WP folder name (module keywords); cross-module and `mvp6-ct-*` records go to C12.
