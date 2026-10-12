# Authority — MVP6 self-registration patches (CT queue Q44)

| Item | Value |
|---|---|
| Decision record | `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` |
| sha256 (verified 2026-09-26) | `910ae6fb4bdf5f89e0075ea5ffc989544b0ac6fd58ae78d8534cc2482feead93` |
| Decided | 2026-09-26T09:13+0300 (Istanbul), approvedBy `current-role-user-message-2026-09-26` |
| Source text | `docs/roadmap/plans/mvp6-self-registration-prep-01/OWNER-DECISION-TEXT.md` sha256 `b0057e1f6cdc498aaea4b46422d83e7fa4a29b9f631cb2aa5e9e6b811d009c62` |
| Prep package | `docs/roadmap/plans/mvp6-self-registration-prep-01/SHA256SUMS` sha256 `c2fa03f6359624a68eea80a8e5d10fa9f078913880941d0790c96c755d61e356` — re-verified 8/8 OK at the start of this lane |
| Choices | D1 A · D2 A · D3 A · D4 A · D5 A |
| Queue | Q43 DONE → this lane is Q44 |

## Boundaries (copied verbatim from the decision record)

> Authorizes preparing the DCP-009 follow-up section and the five pack sections **as patches for later owner sign-off**.
> Does **not** authorize code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway changes, a new permission key or ID,
> or commit / push / stash. Code remains with the single CT-appointed integration owner after an integrated target (Q14/Q15) exists.

## Lane limits (from the Q44 brief)

- Git read-only, always with `GIT_OPTIONAL_LOCKS=0`; no commit, push, stash or add.
- Forbidden: editing any pack, DCP, code, `.antigravity`, gateway or `SharedResource`; a new permission key or ID; overwriting records.
- Output only in `docs/roadmap/plans/mvp6-self-registration-patches-01/`.

## How this lane stayed inside them

- The six patches are files in this folder. None was applied; `git apply --check` only reads.
- `Program.cs`, `.csproj` and appsettings appear only as **specification text** inside the DCP-009 patch; no such file is patched.
- Every permission key in the patches already exists (constants in `ShipmentPermissions`, `CarrierPermissions`, `LoadPermissions`, `ReturnPermissions`, `ClaimPermissions`, or values of `ReturnPermissions.ForTarget`). No MOD, DCP or permission ID is created. ModuleCodes are the D3 strings.
- Navigation keys are listed by name and language only; no resx value is written or approved.
