# MVP6 Loads root amendment — owner decision A (final version, consumer consent, external inventory) — 2026-09-25

- **Decision:** A — final version selection, exact-hash consumer consent, external consumer inventory.
- **Given by:** repository owner (Natig Yusubov, ny@gmgroup.ch), in the MVP6 Lane-4 release-owner review session.
- **Given:** 2026-09-25, between 22:24 and 23:35 +03:00 (19:24–20:35Z), in three answers to option questions. Recorded 2026-09-25T20:35Z (23:35 +03:00).
- **Queue item:** CT-QUEUE Q07. **Prepared text:** `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/OWNER-DECISION-PACK.md` §A (SHA-256 `51f7be10befc6deb1d76b5a80a3df0e194157fa24a9380fa16be6d4f1513018a`).
- **Baseline:** `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (working tree carries uncommitted changes, including the canonical preimages below).

## Exact decision text (adopted)

> I select SHIPMENT-BUNDLE **3.1.0 / wire v1** for the Loads root-read amendment. I reviewed and consent to the exact final-proposed bytes:
>
> - YAML SHA-256 `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`
> - Loads annex SHA-256 `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034`
>
> I acknowledge that `queryLoads` adds optional/nullable `LoadSummary.lifecycleCorrelationId` on the existing route while wire `contractVersion` remains `v1`; the version number alone does not protect strict unknown-property consumers.
>
> I am accountable for the consumer inventory. Repository-external applications, SDKs or integrations using this contract are: **none**. For every listed consumer, exact-hash consent and any required strict-parser migration are recorded before publication. Repo-local current state remains: Loads producer requires later uptake; Loads UI is design-only; no current Loads gateway route was evidenced.

The only change from the prepared text is the owner's inventory entry: `[none / exact list]` → **none**. With no listed consumers, the per-consumer consent / migration sentence has nothing to cover.

## How it was given (verbatim selections)

1. A1 — "Select 3.1.0 / v1 (Recommended)" — version selection, consent to both exact hashes, strict-consumer acknowledgement.
2. A2 — "None outside repo (Recommended if true)" — external consumer inventory = none; owner accountable.
3. A3 — "Adopt full A text (Recommended)" — adopts the full §A text above with inventory = none.

Presentation note: the A1 question gave an abbreviated YAML hash with a wrong tail ("6dc1dd48…a2df0e0"). The same question also gave the full hash `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2`, which is correct. A3 adopted the pack text that contains the full hash. Consent binds to the full hash only.

## Verification at recording (2026-09-25T20:35Z)

| Check | Result |
|---|---|
| HEAD / branch | `4a8d4d4b…1136c` / `feature/mvp6-logistics` — matches dispatch |
| Release-prep `SHA256SUMS` (self `61ddf10962d51f52f73c155c6e5c52c8dc753344ad115e9ae7905b31d0b36de9`) | 21/21 OK |
| Canonical YAML preimage `docs/analysis/contracts/shipment-bundle.openapi.yaml` | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` — match |
| Canonical Loads v2 annex `docs/analysis/contracts/loads-semantics-v2.0.0.md` | `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1` — match |
| `docs/analysis/contracts/loads-semantics-v3.1.0.md` | absent — not published |
| Independent re-VER `ARTIFACTS.sha256` (self `6b0990b9…2f61`) | 14/14 OK; RVR-01…10 PASS, RVR-11 OPEN, RVR-12 HELD |
| Publication patch (unapplied) | `dc0ad05bbb47b05c3c62dadf138faab847337ddc9f387bb9f764e84e02fdbed5` |

## Effect and limits

- Decision A is **recorded**. This closes the RVR-11 consumer-compatibility gate for the declared inventory (repo-local consumers are handled by later uptake). Its basis is the owner's declaration, not a proof by repository search.
- Q08 (B — canonical publication) meets its re-dispatch trigger ("Q07 recorded"). B is **not authorized** by this record and needs its own exact owner decision.
- Q09 (C — producer uptake) stays **HELD** until B is recorded and publication is independently verified.
- This record grants no publication, patch application, runtime uptake, UI, gateway, guard binding, pack promotion, rollout, commit or push.
- Already-granted decisions are unchanged and were not re-requested: D185-01…05 (`mvp6-mod0185-design-approval-01.md`), R185-C01…05 (`mvp6-mod0185-precision-approval-01.md`), MOD-0185 §28, and the Carrier decisions.
