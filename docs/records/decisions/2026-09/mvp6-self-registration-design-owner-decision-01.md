# Owner decision — Supply Chain module self-registration design (MVP6-SELF-REGISTRATION-DESIGN-OWNER-DECISION-01)

- Decided: 2026-09-26T09:13+0300 (Istanbul), by the owner (CEO Natig Yusubov) in the CT conversation, one question at a time.
- approvedBy: current-role-user-message-2026-09-26
- Source text: `docs/roadmap/plans/mvp6-self-registration-prep-01/OWNER-DECISION-TEXT.md` sha256 `b0057e1f6cdc498aaea4b46422d83e7fa4a29b9f631cb2aa5e9e6b811d009c62`
- Prep package: `docs/roadmap/plans/mvp6-self-registration-prep-01/SHA256SUMS` sha256 `c2fa03f6359624a68eea80a8e5d10fa9f078913880941d0790c96c755d61e356` (8/8 verified by CT)
- Queue: Q43 → DONE; follow-up Q44.

## Choices

| ID | Choice |
|---|---|
| D1 | **A** — DCP-009 follow-up section for the shared foundation + a "Self-registration" section in each module pack (0183–0187) |
| D2 | **A** — existing `X-Internal-Api-Key` registration path; no Platform change |
| D3 | **A** — Domain `SupplyChainExecution`, Service `DitenSupplyChainService`, ModuleCodes `shipment-tracking-pod`, `carrier-management`, `routing-load-planning`, `reverse-logistics`, `claims-management` |
| D4 | **A** — a module's provider ships with that module's UI in the integrated target, with its nav keys; never ahead of its UI |
| D5 | **A** — the completeness test reflects `ReturnPermissions.ForTarget`; no Returns backend change |

## Decision text (as recorded)

> I approve the Supply Chain module self-registration **design** in `docs/roadmap/plans/mvp6-self-registration-prep-01/` for pack and DCP preparation only, with these choices: **D1 A**, **D2 A**, **D3 A**, **D4 A**, **D5 A**.
> (Remaining bullets exactly as in the source text above, sha256 `b0057e1f6cdc498aaea4b46422d83e7fa4a29b9f631cb2aa5e9e6b811d009c62`.)

## Boundaries

Authorizes preparing the DCP-009 follow-up section and the five pack sections **as patches for later owner sign-off**.
Does **not** authorize code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway changes, a new permission key or ID,
or commit / push / stash. Code remains with the single CT-appointed integration owner after an integrated target (Q14/Q15) exists.

Effort reference: 38 / 70 / 127 person-hours (D2=A, D5=A).
