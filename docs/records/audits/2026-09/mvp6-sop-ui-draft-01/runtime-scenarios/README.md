# runtime-scenarios — MOD-0190 S&OP UI (for Q84b on the Mac)

DRAFT. `sop-ui.spec.mjs` was written and syntax-checked (`node --check`) in the chat lane; it has **never run**.

Prerequisites on the Mac (isolated environment, not the common checkout; README "Build dependency note"):
- HEAD `4a8d4d4b` archive + BC-SOURCE `ebd5d80c…` + **A12 360 overlay `7b6a0d1a…314d`** + the accepted S&OP isolated source
  `8fa00d40…b745` (reconcile `Program.cs`/`.csproj`) + Auth 22 `f50350b8…`, then this lane's `overlay/` module files, then
  the `_shared-integration/` items the environment needs to route (gateway routes, provider line, nav keys).
- Real-Auth identities with separate storage states: full (all four S&OP keys, LE-A), read-only (`.read`, LE-A), no-read,
  plus an LE-B identity; the exact DEMAND test fixture values of the S&OP backend (id, version, checksum).
- The shared `showConfirm` accept button selector must be confirmed on the target (`.swal2-confirm` is assumed, README A9).

| Scenario | Acceptance row |
|---|---|
| entry page: no list request, malformed UUID blocked, same-origin only | SU-01, SU-02 |
| unknown plan ID → safe-not-found, workspace removed | SU-13 |
| create → capture (`supplyInputRefs: []`) → Approved sign-off via showConfirm; reload; status stays InReview; PNG | SU-VS1, SU-06…SU-09, SU-12 |
| duplicate role/snapshot → first decision kept | SU-10 |
| sign-off in Draft by direct POST → 409 `SANDOP_SIGN_OFF_STATE_CONFLICT` | SU-11 |
| unknown demand reference → 422, inputs kept | SU-15 |
| no read → `_AccessDenied` only; read-only → no action controls | SU-04, SU-05 |
| tr and ar (RTL) at 390/768/1024/1440, no horizontal overflow, PNG | SU-17 (PNG as evidence only; SU-20 stays BLOCKED) |

Not scripted here (need fault injection or a second profile racing, see NOT-VERIFIED.md): SU-03 error state per section,
SU-14 idempotency replay/`IDEMPOTENCY_KEY_REUSED` (BLOCKED by DN-01 policy), SU-16 503 same-key retry, SU-18 family routing.
