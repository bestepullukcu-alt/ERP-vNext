# SOP §22 — MOD-0187 Claims Management UI-REVISION-01 (CT queue Q35)

**Verdict: PATCH PREPARED AND PROVEN — NOT APPROVED, NOT APPLIED.** The shared pack is unchanged (`a342054c…1c1f`, `draft`).
Documents only; no commit, push or stash.

## Authority and gates

| Gate | Result |
|---|---|
| Repo / branch / HEAD | `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` — match |
| Owner decision | `docs/records/decisions/2026-09/mvp6-returns-claims-ui-scope-owner-decision-01.md` SHA-256 `309319173e77bcc5da1d4587082742dfa4feb31b0d5e6337b04309646acfaee9`, `status: approved`, scope approved as drafted; approves **preparing** patches only |
| Bound draft package | `mvp6-ui-pack-drafts-01/SHA256SUMS` = `b568c94f…abb57f` (bound) and 13/13 OK; `claims/APPROVAL-DECISION.md` = `ea11f670…4813` (bound) |
| Alignment package | `mvp6-pack-alignment-02-claims/SHA256SUMS` 5/5 OK; patch `00dee2b1fd202f1adc25ca4d3c2aeae532e1bf8d944aca0c68a7fe0417ca5c59`; declared preimage `a342054c…`, target `f0e4d3bd…`; still **NOT APPROVED** (PROMOTION-DECISION.md) |
| DCP-002 | `verify_module_id.py . --check-id MOD-0187 --name "Claims Management"` → `OK`, exit 0 (2026-09-26) |
| Rules read in full this lane | AGENTS.md §10; `module-pack-standard`, `frontend-datatable-template`, `frontend-form-template`, `frontend-details-template`, `localization-standard`, `views-organization`, `unauthorized-surface-standard`, `permission-key-standard`, `routes`, `premium-modal-standard`; process v1.0 §§3–4. Hashes equal those pinned in the draft README |

## Hash chain

| Step | SHA-256 |
|---|---|
| Current shared pack (preimage of alignment) | `a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f` |
| Alignment patch `mvp6-pack-alignment-02-claims/proposed-pack.patch` | `00dee2b1fd202f1adc25ca4d3c2aeae532e1bf8d944aca0c68a7fe0417ca5c59` |
| Aligned pack = **UI revision preimage** | `f0e4d3bd6af5c16a0d0904721019bfb33f10818a6f9baffd92d3f50740bb1baf` (confirmed; equals the declared target) |
| UI revision patch `claims/ui-revision.patch` | `5644479a77db172adf9d24f5a7031eed82c4eb82067d326403a37058270dae92` |
| **Final target** (alignment + UI revision) | `762ab5337af85b965216d41a212217db3681af3a31bbd1514544b58dbfc7786b` |

## Proof (scratch copies outside the repo)

| Check | Result |
|---|---|
| Alignment on shared pack: `git apply --check`, `git apply`, `patch --fuzz=0 -p1` | OK; both give `f0e4d3bd…`, byte-equal |
| UI patch on aligned preimage: `git apply --check`, `git apply`, `patch --fuzz=0 -p1` | OK; both give `762ab533…`, byte-equal |
| Combined order: shared → alignment → UI, with `git apply` and with `patch --fuzz=0` | OK; both give `762ab533…`, byte-equal to the single-step result |
| Negative: UI patch directly on the shared pack | Fails (`git apply --check` exit 1; `patch` hunks 1 and 3 FAILED) — the order is enforced |
| Scope of the UI patch | Only frontmatter `shell` none→tenant, `golden_reference` none→slim, `form_field_count` 0→6, plus a new §32 appended; preimage lines 17–539 identical; `status: ready-for-dev` unchanged; `status_note` unchanged |

## What §32 contains

Identity (in-pack revision, no new ID) · layout/shell contract · bound operations (queryClaims, createClaim, transitionClaim, getShipment
read-only) · screens/routes/permissions incl. `supplychain.shipments.read` · bounded DataTables v2 list profile · create offcanvas with 6
fields and per-field rules · transition map with keys · error/replay/concurrency table (18 annex codes) · 7 languages, UAS-001,
safe-not-found, accessibility · 21 owned UI paths, protected paths, single integration-owner handoff with the gateway routes **listed** ·
the single acceptance matrix (CU-VS1 first, CU-01…31, OUT rows CU-SCR-01…06) · UI test expectations · remaining gaps · O/M/P and the authorization boundary.
No business rule, contract statement, backend section, backend owned-path list or acceptance row of §§1–31 is changed.

## Remaining gaps (honest list)

1. **Approved-amount list gap:** `ClaimSummary` lacks `approvedAmount`; after reload it cannot be listed. It is recorded, not derived; no contract change is authorized.
2. **G-MODAL:** `premium-modal-standard` §4 requires `window.showConfirm` and forbids manual `Swal.fire` on layout pages. The approved design puts the transition inputs (`occurredAt`, `approvedAmount`, `resolutionCode`, `note`) in that dialog; whether `showConfirm` can host inputs was not verified. If it cannot, one more owned partial is needed, which is a scope amendment (owned paths 21 → 22).
3. **G-ICONMAP:** `frontend-form-template` requires `.diten-field` with an icon for every field, with the glyph registered in shared `frontend/Diten.Web/tests/diten-field-icons.test.js` `ICON_MAP`. That file is not UI-owned; the integration owner must add six entries.
4. **G-DATETIME:** `occurredAt` needs date-time with an explicit offset; the shared `diten-datefield.js`/flatpickr example is date-only. The component must be chosen at Phase 1.5.
5. **Verifier tension:** `module-pack-standard` §15 expects "DataTable modülünde verifier PASS", but the approved OUT rows (no checkbox/bulk/edit/export) make the generic verifier record-only (CU-SCR-06) until the scope-aware profile (DN-02 / PRES-183-03) is approved.
6. **Alignment not yet approved:** the UI revision depends on `mvp6-pack-alignment-02-claims` being approved and applied first. If that patch changes, this one must be rebased and re-hashed.
7. **Integration target:** the 47 accepted Claims backend paths are absent from the common checkout; UI VER needs an integrated target (Q14/Q15). Gateway routes, permission registration, navigation, shared L10n, personalization codes and producer root emission are integration-owner work.
8. **Inherited open items:** DN-01 retry policy, PNG capability (PRES-183-04), Loads 3.1.0 forward drift of the `5dfe7c1b…` pin, and the `module-implementation-status.md` update (a separate path).
9. **Record observation, not edited:** the owner-decision record states `decided_at_local: 2026-09-26T01:30+03:00`, but its file time is 01:24 and this lane read it at 01:25. That is a clerical timestamp inconsistency; CT may note it.
10. **Returns** has no UI revision in this package (see `../returns/SOP-22-PACK-REVISION.md`).

## Changed files

Only the new directory `docs/roadmap/plans/mvp6-ui-pack-revisions-01/`. Packs, contracts, product code, `.antigravity`, gateway, existing
records and the drafts were not changed. Git use was limited to `rev-parse` with `GIT_OPTIONAL_LOCKS=0`; all patch tests ran on scratch copies outside the repo.
