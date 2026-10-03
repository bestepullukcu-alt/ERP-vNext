# Q218 — SHIPMENT-BUNDLE contract pin: 3.0.0 pinned, 3.1.0 on disk · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q218 · `AL-SCM-CONTRACT-PIN` (INS) · required E1 (static) — **reached: E1**. Nothing built, no test run. |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` (mode **strict**; no Write; no fixes) → Phase B `documentation-writer` (this folder only) |
| Scope | `docs/analysis/contracts/shipment-bundle.openapi.yaml` and its annexes · the contract pins of the MOD-0186 and MOD-0187 packs · `ReturnReferenceReader.cs`, `ClaimReferenceReader.cs` |
| Read first, in order | `AGENTS.md` (full) · `read-only-auditor.md` · `documentation-writer.md` · `api-conventions.md` · MOD-0186 pack (869 lines) · MOD-0187 pack (819 lines) · Q210 `SOP-22.md` · Q211 `SOP-22.md` |
| Depends on | Q210, Q211 (both CT ACCEPTED, per the prompt) |
| Start / End (Europe/Istanbul) | 2026-10-02 23:00:20 +03 / 2026-10-02 23:07:55 +0300 |

```text
Agent Verdict:        COMPLETE — measurement and recommendation delivered. Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      git status --porcelain: 89 " M" · 576 "??" · 0 other = 665 at start. End: see ARTIFACTS.sha256 header.
                      No .git/index.lock. No git diff. No git write. No gh. No fetch.
Changed files:        this record folder only
Build / Tests:        NOT RUN (forbidden)
Fixes / pins changed: none
```

## 1. Answers to the five tasks

| # | Task | Answer | Detail |
|---|---|---|---|
| 1 | Verify both hashes; which file is 3.1.0; does 3.0.0 exist on disk | Both hashes confirmed. The canonical file is 3.1.0 `6dc1dd48…96aa2`. **3.0.0 exists as bytes**, in one file, in an untracked record folder; hash equal to the pin `5dfe7c1b…9d21c`. | `HASHES.md` |
| 2 | What changed | 7 hunks, all in Loads. One wire change: optional, nullable `LoadSummary.lifecycleCorrelationId`. The rest: version number, one example, annex pointers. | `VERSION-DELTA.md`, `delta-3.0.0-to-3.1.0.diff` |
| 3 | Field-level parity per consumed operation | 3.0.0 vs 3.1.0: **MATCHES** for all three consumed reads, on every aspect. Consumer vs contract: differences exist, identical in both versions. | `FIELD-PARITY.tsv` (15 rows) |
| 4 | Integration Gate | Re-pin both packs to 3.1.0; do not revert. Owner decision. | `RECOMMENDATION.md` |
| 5 | Other packs that pin the contract | Five packs name it. MOD-0185 effectively pins **2.0.0**. MOD-0183 and MOD-0184 carry no hash. No pack pins 3.1.0. | `PIN-INVENTORY.tsv` |

## 2. Parity summary

| Module | Operation | 3.0.0 vs 3.1.0 | Consumer vs contract |
|---|---|---|---|
| MOD-0186 Returns | `getShipment` | MATCHES (5 of 5 aspects) | path, request, response fields: MATCHES · required/nullable: MATCHES (positive line quantity is in the Returns annex `:37`) · error codes: consumer handles statuses the operation does not declare |
| MOD-0187 Claims | `getShipment` | MATCHES (5 of 5) | path, request, response fields, required/nullable: MATCHES (empty-root widening is in the Claims annex) · error codes: same undeclared statuses |
| MOD-0187 Claims | `queryCarriers` (only when the claim names a carrier) | MATCHES (5 of 5) | MATCHES, except: consumer rejects a repeated `carrierId`; error mapping vs the Claims annex prose **not performed** |

No row is CANNOT COMPARE between the two versions: both byte sets were on disk and both were parsed.

## 3. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q218-1** | 🟢 Result | The 3.0.0 bytes are recoverable. One file hashes to the pin `5dfe7c1b…9d21c`. Git holds 1.0.0 only (`f6415bbf…`, one commit on the path). | `HASHES.md`; `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/shipment-bundle.openapi.yaml:13` |
| **F-Q218-2** | 🟢 Result | 3.0.0 → 3.1.0 touches Loads only. 14 of 17 operations, including every Shipment, Carrier, Returns and Claims operation, are equal with everything they reach. One schema differs: `LoadSummary`. | `VERSION-DELTA.md`; `docs/analysis/contracts/shipment-bundle.openapi.yaml:3197` |
| **F-Q218-3** | 🟢 Result | For the reads Returns and Claims consume (`getShipment`, `queryCarriers`), path, method, request fields, response fields, required/nullable and error codes are the same in 3.0.0 and 3.1.0. F-Q210-8 and F-Q211-4 are answered on parity: no drift for these two modules. | `FIELD-PARITY.tsv` |
| **F-Q218-4** | 🟡 Medium (corrects the framing) | 3.1.0 is not an unexplained uncommitted edit. It is the owner-authorized publication of 2026-09-26 (decisions A and B), CT-accepted after an independent VER, and bound by the docs-path guard. "Uncommitted" is true of the whole chain: 3.0.0 was never committed either. | `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-b-01.md`; `CT-QUEUE.tsv:26,33`; `docs/reference/architecture/docs-path-authority.json:11` |
| **F-Q218-5** | 🟡 Medium | Both packs say "Published contracts at acceptance **and today**" for `5dfe7c1b…`. False for "today" since 2026-09-26. Both packs record the drift in their own gap lists and neither pin was updated. Decision B excluded pack changes. Literal Integration Gate: not met. | MOD-0186 `:584`, `:619`, `:780`; MOD-0187 `:533`, `:569`, `:729` |
| **F-Q218-6** | 🟡 Medium | MOD-0185's effective pin is 2.0.0 `93c696e2…`, two publications behind, and its pack mentions neither 3.0.0 nor 3.1.0, although 3.1.0 was published for Loads. The 2.0.0 and 1.1.0 bytes were not found on disk. | MOD-0185 `:20`, `:478-480`; `PIN-INVENTORY.tsv` |
| **F-Q218-7** | 🟡 Medium | Consumer and contract disagree in ways that are the same in both versions: (a) the producer returns 500 `SHIPMENT_ROOT_INVALID` and both consumers branch on it, but `getShipment` declares only 200 and 404 and the code is in no version of the YAML nor in the root annex; (b) both consumers handle 401/403/5xx on `getShipment`, undeclared; (c) both send `X-Tenant-Id` / `X-Legal-Entity-Id`, which neither consumed operation declares as a parameter; (d) Claims rejects a repeated `carrierId`, which the schema does not forbid. Returns' rejection of a non-positive line quantity is **not** a gap: the Returns annex `:37` requires it. | `GetShipmentByIdHandler.cs:15`; `ReturnReferenceReader.cs:73-83`; `ClaimReferenceReader.cs:65,91-104`; `docs/analysis/contracts/shipment-bundle.openapi.yaml:114-121` |
| **F-Q218-8** | ⚪ Low | No test file under `services/Diten.SupplyChainService/tests` or `tests/` references `shipment-bundle.openapi`. Nothing mechanical binds consumer code to either version (K6). Confirms the remark in F-Q211-4. | grep, 0 files |
| **F-Q218-9** | ⚪ Low | The only copy of the 3.0.0 bytes is in an untracked folder. Loss of that folder makes the `5dfe7c1b…` pin unverifiable. | `git status --porcelain` → `?? docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/` |
| **F-Q218-10** | ⚪ Info | MOD-0183 names the contract as frozen authority with no version and no hash. MOD-0184 names 1.1.0 with no hash in its own pack. | MOD-0183 `:129`; MOD-0184 `:391-392` |

## 4. Not performed (stated, not hidden)

- Line-by-line comparison of the consumers' error mapping with the prose of the Claims and Returns annexes. Only the lines cited in `FIELD-PARITY.tsv` were read.
- Any run-time observation. No request was sent to any service.
- Sealed `tar.gz` archives were not opened in the search for contract bytes.
- The publication patch `dc0ad05b…` was hashed, not replayed.
- Loads annex v2 vs v3.1.0 was not compared (outside scope).
- The 25 distinct hashes among the 85 candidate files were not each identified; only `5dfe7c1b…`, `6dc1dd48…` and `f6415bbf…` were.

## 5. Method notes and deviations

- `git cat-file -p HEAD:<path>` and `git log --all -- <path>` were used once each, with `GIT_OPTIONAL_LOCKS=0`. Both only read. The task names "a git object" as a place to look. No `git diff`: the comparison is plain `diff -u` on two files.
- The read-only auditor's standard no-change block uses `git diff`; it is forbidden here. The no-change proof is porcelain-based, with sha256 of the protected files (same deviation as F-Q201-11, F-Q211 §7).
- YAML parsing used the system Python (`/Library/Developer/CommandLineTools/usr/bin/python3`, PyYAML 6.0.3). Nothing was installed. Scripts stayed in the session scratch folder; `tools/` holds copies.

## 6. Refused / not done

- No contract, annex, pack or pin edited. No re-pin. No build. No test. No git write. No ledger row changed.
- No prompt written for another work package.

## 7. Files

`SOP-22.md` · `HASHES.md` · `VERSION-DELTA.md` · `delta-3.0.0-to-3.1.0.diff` · `FIELD-PARITY.tsv` · `PIN-INVENTORY.tsv` ·
`RECOMMENDATION.md` · `tools/q218_cmp.py` · `tools/q218_ops.py` · `ARTIFACTS.sha256`

Return to CT; CT decides.
