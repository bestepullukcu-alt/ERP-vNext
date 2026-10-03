# runtime-scenarios — MOD-0192 Capacity UI (for Q88b on the Mac)

DRAFT. `capacity-ui.spec.mjs` was written and syntax-checked (`node --check`) in the chat lane; it has **never run**.

Prerequisites on the Mac (isolated environment, not the common checkout; README "Build note for Q88b"):
- HEAD `4a8d4d4b` archive + BC-SOURCE `ebd5d80c…7064` (Capacity backend + executor) + **A12 360 overlay `7b6a0d1a…314d`**
  (reconcile `Program.cs`/`.csproj`) + Auth 22 `f50350b8…`, then this lane's `overlay/` module files, then the
  `_shared-integration/` items the environment needs to route (gateway routes, provider line, nav keys).
- Real-Auth identities with separate storage states: full (all four Capacity keys), read-only (`.read`), no-read; the exact
  DEMAND fixture values and `CAPACITY-EVAL-FIXTURE-192-01@1` values of the Capacity backend (plan, constraint, resource,
  period, UoM).

| Scenario | Acceptance row |
|---|---|
| entry page: no list request, malformed/nil UUID blocked, same-origin only | CP-01, CP-02 |
| unknown plan ID → safe-not-found, workspace removed | CP-13 |
| create plan (7 fields, 0/7 → 7/7) → scenario (`"10.5"` as a string) → Finite evaluation (202) → no request for 10 s → one GET per Refresh click until Completed; decimals exact; PNG | CP-VS1, CP-06…CP-10 |
| second evaluate while active → 409 `EVALUATION_ALREADY_ACTIVE` | CP-11 (timing-sensitive: the executor must not finish the first evaluation before the second POST) |
| invalid decimal `1,5` blocked client-side, no request | CP-09 |
| duplicate horizon/demand version → 409 `CAPACITY_PLAN_ALREADY_EXISTS`, inputs kept | CP-12 |
| malformed scenario ID blocked; unknown scenario ID → scenario panel safe-not-found, plan kept | CP-02, CP-13 |
| no read → `_AccessDenied` only; read-only → no action controls | CP-04, CP-05 |
| tr and ar (RTL) at 390/768/1024/1440, no horizontal overflow, PNG | CP-17 (PNG as evidence only; CP-20 stays BLOCKED) |

Not scripted here (need fault injection, a Failed fixture or a second profile, see NOT-VERIFIED.md): CP-03 error states per
panel, CP-14 idempotency replay/`IDEMPOTENCY_KEY_REUSED` (BLOCKED by DN-01 policy), CP-15/CP-16 422 constraint reference and
503 same-key retry, CP-18 family routing, an evaluation reaching Failed, foreign-scope and soft-deleted resources (CP-13 full).
