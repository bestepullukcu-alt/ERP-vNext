# G2B — BOM Structure Ready: gate accepted with one conditional waiver (MVP-3, MOD-0193)

- owner: Ali (product owner) — decision relayed by the Control Tower, 2026-10-13
- status: ACCEPTED (conditional waiver W-G2B-0290, below)
- module: MOD-0193 BOM & Routings · service `Diten.ManufacturingService` (5070) · pack
  `execution/domains/supply-chain-execution/module-packs/MOD-0193-bom-routings.md` → `done`
- code: `main` @ `4aeae0671` (PR #138) + branch `feature/mvp3-bom-version-event` (BOM-version event, its own PR)
- evidence: `docs/records/audits/2026-10/mvp3-mod0193-bom-routings-01/` (REPORT.md and `evidence/`)

## Gate criteria (report §20.2 and WP-MVP3 acceptance)

| criterion | verdict | evidence |
|---|---|---|
| Versioned BOM/routing records persist at L3 | met | Mongo replica set, `BomHistoryJournal.CommitAsync` one transaction (snapshot / majority) |
| Optimistic concurrency on revisions | met | `rowVersion` / expected `Version`; tests `Only_a_draft_changes_and_a_stale_row_version_is_a_conflict`, `Two_simultaneous_releases_of_one_item_leave_exactly_one_effective_version` |
| Controlled effective-state transition | met | release Draft → Effective, previous → Superseded in the same transaction; single-Effective partial unique index; cycle guard; Effective/Superseded immutable |
| Audit / evidence on change | met (path c) | BOM version history written in the BOM transaction (K2 fail-closed). Central audit log: BL-501, open (below) |
| create → component → effective → GET current effective | met | API suite + browser flow M-5 |
| explode → component requirements | met | frozen `POST /explode`, single level (ASSUMPTION-BOM-02) |
| BOM-version event | met | contract `bom.openapi.yaml` v1.2.0; outbox in the BOM transaction; broker delivery measured (`evidence/bom-version-event.txt`) |
| PRODUCT-MASTER validation called | **waived (conditional)** | W-G2B-0290 |

## W-G2B-0290 — MOD-0290 validation, conditional waiver

Today `ProductMaster:Mode=Permissive`: no item id is checked against MOD-0290 because its `validateRefs` / `getSkuUom`
endpoints are not live. The gate is accepted without it, on this condition (open items F-0193-07 and F-0193-03):
when both endpoints are live, MOD-0193 switches to `Mode=Http`, adds tests for `HttpProductReferenceValidator` against
the then-current contract path and shape, and validates component UoM.

Measured 2026-10-13 (read only, no code change) — the HTTP client against the PRODUCT-MASTER contract on `main`:

| aspect | v1.0.0 (FROZEN) | v1.1.0-draft.11 | client today |
|---|---|---|---|
| path | `POST /validate` (server `/api/product-master`) | same | `POST {BaseUrl}validate` — compatible when `BaseUrl` ends in `/api/product-master` |
| request | `{refs:[{itemId?, skuId?, skuLevel?}]}` | same, **`maxItems: 200`** | `{refs:[{itemId}]}`, all ids in ONE request — **a BOM may reference up to 5 501 ids** (500 lines × 10 alternates + item): over 200 the v1.1 call is a 400 |
| response | `results[{itemId, skuId, skuLevel, exists}]` | + `itemKind`, `lifecycleStatus` (open enum) | reads `itemId` + `exists` — compatible; **`lifecycleStatus` ignored**, so a `Retired` / `Deactivated` item counts as known |
| error | — | 400 | every non-2xx → 503 `DEPENDENCY_UNAVAILABLE`; a 400 is reported as "unavailable", not as the caller's error |
| UoM | no `getSkuUom` use | `getSkuUom` exists (X-04: BOM component unit) | not called (F-0193-03) |
| tests | — | — | **none** for the HTTP client |

What must change when the waiver is lifted: chunk requests at 200, decide (product owner) whether a non-`Active`
item may enter a BOM, map 400 separately from unavailability, call `getSkuUom` per component, and test all of it
against the contract.

## Open items carried (none blocks this gate)

| item | what | owner |
|---|---|---|
| BL-501 | central audit log connection | central team, after the MVPs |
| F-0193-08 | company authority: the user's rights in the legal entity are not checked | Platform ("my authorised companies") |
| F-0193-07 / F-0193-03 | MOD-0290 validation and UoM (condition of W-G2B-0290) | MOD-0290 team + MOD-0193 |
| W-0193-01 / F-0193-04 | MOD-0209 Change Control absent; `changeControlRef` format-only, no e-signature | MOD-0209 owner |
| W-0193-02 | MOD-0003 Data Contract Registry absent | Platform |
| BL-510 | shared `_AccessDenied` answers 200, UAS-001 says 403 | Control Tower |
| `mdm.legal-entities.read` | BOM users also need this permission | role administrators (pack §8) |
| F-0193-09 | MOD-0040: the registry calls it a deprecated alias of MOD-0288 (Tenant Organization); the 2026-09-05 status Excel lists it as a MVP-3 supporting module (Canonical ID & Correlation). Not reconciled. | Control Tower / registry owner |
| BOM-version event delivery | broker transport is opt-in per environment (`EventTransport:Enabled`); off, events stay Pending in the outbox | environment owner |
