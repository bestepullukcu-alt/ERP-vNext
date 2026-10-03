# MVP6 Loads root amendment — owner decision B (canonical publication) — 2026-09-25

- **Decision:** B — canonical publication authority, limited to one exact patch.
- **Given by:** repository owner (Natig Yusubov, ny@gmgroup.ch), in the MVP6 Lane-4 release-owner review session for decision B.
- **Given:** 2026-09-25, between 23:46 and 23:48 +03:00 (20:46–20:48Z), in three answers to option questions. Recorded 2026-09-25T20:48Z (23:48 +03:00).
- **Queue item:** CT-QUEUE Q08. **Prepared text:** `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/OWNER-DECISION-PACK.md` §B (SHA-256 `51f7be10befc6deb1d76b5a80a3df0e194157fa24a9380fa16be6d4f1513018a`).
- **Precondition A:** `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-a-01.md` (SHA-256 `e12d59750c4846269bf1b84341f9c437060e5b891740bb4077e8917ebf3eea9f`). A is recorded with a complete external-consumer declaration (none).
- **Baseline:** `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (the canonical preimages are uncommitted working-tree content).

## Exact decision text (adopted)

> After A is recorded with a complete external-consumer declaration, I authorize the single publication owner to apply only publication patch SHA-256 `dc0ad05bbb47b05c3c62dadf138faab847337ddc9f387bb9f764e84e02fdbed5` to canonical preimages YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` and Loads v2 annex `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1`.
>
> The required targets are YAML `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` and new annex `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034`. The existing Loads v2 annex remains unchanged. This publication authorization does not authorize runtime, UI, gateway, pack promotion, rollout, commit or push.

**Single publication owner (owner's choice):** agent lane **AL-MVP6-LOADS-PUB01**. It is the only writer of the two canonical paths for this step and is dispatched by CT through `docs/roadmap/plans/mvp6-loads-publication-dispatch-01/DISPATCH-v1.0.md`.

## How it was given (verbatim selections)

1. B1 — "Authorize publication (Recommended)". Authority to apply only that patch, from the preimage hashes to the target hashes. The question told the owner that runtime, UI, gateway, guard binding, pack status, commit and push do not change, and that C stays HELD until publication is independently verified.
2. B2 — "New lane AL-MVP6-LOADS-PUB01 (Recommended)". Names the single publication owner. Independent verification is done by a different lane.
3. B3 — "Adopt full B text (Recommended)". Adopts the §B text above with that owner named. Guard binding is explicitly left unauthorized.

Presentation note: the dispatch for this session gave the patch hash as "dc0ad05b…fed5". The file on disk, the decision pack and record A all use `dc0ad05b…e02fdbed5`. The owner was shown the full correct hash, and the authority binds to that hash only.

## What publication changes on disk (and nothing else)

| Path | Before (preimage) | After (target) |
|---|---|---|
| `docs/analysis/contracts/shipment-bundle.openapi.yaml` | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` (info.version 3.1.0, x-status FROZEN, optional/nullable `LoadSummary.lifecycleCorrelationId`) |
| `docs/analysis/contracts/loads-semantics-v3.1.0.md` | absent | `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` (new) |
| `docs/analysis/contracts/loads-semantics-v2.0.0.md` | `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1` | unchanged |

## Verification at recording (2026-09-25T20:48Z)

| Check | Result |
|---|---|
| HEAD / branch | `4a8d4d4b…1136c` / `feature/mvp6-logistics` — matches |
| Release-prep `SHA256SUMS` (self `61ddf10962d51f52f73c155c6e5c52c8dc753344ad115e9ae7905b31d0b36de9`) | 21/21 OK |
| Canonical YAML / v2 annex | preimage hashes match — patch not applied |
| `loads-semantics-v3.1.0.md` | absent |
| `publication.patch` | `dc0ad05b…fdbed5` present; its headers touch only the YAML (modify) and `loads-semantics-v3.1.0.md` (create) |

## Effect and limits

- Decision B is **recorded**. It authorizes lane AL-MVP6-LOADS-PUB01 to apply only the patch above, under the publication dispatch. The patch was **not** applied during this recording.
- It does not authorize: runtime/producer uptake, UI, gateway, guard or docs-path binding changes (a new canonical hash may need a separate binding decision; see `mvp6-loads-docs-path-owner-binding-01.json` and R185-C05), pack promotion, rollout, commit or push.
- Q09 (C — producer uptake) stays **HELD** until publication is independently verified by a lane other than the publication owner.
- Already-granted decisions were not re-requested: A, D185-01…05, R185-C01…05, MOD-0185 §28, and the Carrier decisions.
