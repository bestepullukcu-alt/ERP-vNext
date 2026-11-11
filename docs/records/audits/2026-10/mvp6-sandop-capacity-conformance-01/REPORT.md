# SANDOP-CAPACITY 3.0.0 conformance — MOD-0190 and MOD-0192 (measurement only)

- Measured: **2026-10-16** (preflight `Fri Oct 16 19:57:04 UTC 2026`), branch
  `feature/sce/mod-0190-0192-bounded-slice` @ `82b50e6041702ecfa5aae8dc55a776f5e74bdb74`,
  **0 dirty paths, 0 staged**.
- Contract: `docs/analysis/contracts/sandop-capacity.openapi.yaml` —
  sha256 `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`,
  `info.version: 3.0.0`, `x-contract-version: v1`, 1697 lines.
- Annex: `docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md` —
  sha256 `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64`.
- Packs, unchanged during the measurement:
  - MOD-0190 `827fdb022a20caa31939c754b8fd13e85a0db1daeb8c03eeb61d7039c38979f8`
  - MOD-0192 `8f281e019c2412e00a07f7ee96ab3db3406ca51d0b0b28382caaeacca1a7d3e2`
- Method: **static only.** No pack edited, no code changed, no service or mongod started for this
  measurement, no build, no test run. Follows the Q332 precedent
  (`docs/records/audits/2026-10/mvp6-q332-mod0183-conformance-01/`), including its two readings.
- Verdicts: MET · NOT MET · UNDETERMINED (not settled by static evidence) · REQUIRES RUNTIME ·
  BLOCKED (waiting on a named authorization) · NOT MEASURED.
- **This is a measurement. CT rules. No disposition is recorded and no acceptance box is ticked.**

## Why this lane exists

MOD-0190 has exactly one open acceptance line (`:201`) and MOD-0192 has four (`:199`, `:200`,
`:201`, `:203`) — the five closest open lines in MVP-6. MOD-0192's `:201` names the job that would
close it: *"the `g5_live_conform.py` job, which has not been run against the capacity surface."*

**`g5_live_conform.py` does not exist in the repository.** The only occurrence of that name anywhere
is the pack line itself. So the line could not be closed the way it describes, and this lane does
the static half by the method the repository actually used for MOD-0183.

## What the contract declares

Twelve operations across ten paths — **six for MOD-0190 and six for MOD-0192**, which is what both
pack lines say:

| MOD-0190 (S&OP) | MOD-0192 (Capacity) |
|---|---|
| `POST /sandop-plans` 201 | `POST /capacity-plans` 201 |
| `GET /sandop-plans/{id}` 200 | `GET /capacity-plans/{id}` 200 |
| `POST .../snapshots` 201 | `POST .../scenarios` 201 |
| `GET .../snapshots` 200 | `GET .../scenarios/{id}` 200 |
| `POST .../sign-offs` 201 | `POST .../scenarios/{id}/evaluations` **202** |
| `GET .../sign-offs` 200 | `GET .../evaluations/{id}` 200 |

Every operation declares 400, 401, 403 and 503; POSTs add 409 and 422; GETs add 404. The error-code
set is declared in two places — `x-error-codes` on nine `components/responses` entries and `code:`
examples on the rest — **22 distinct codes** in total. Both header parameters,
`Idempotency-Key` and `X-Correlation-Id`, are `required: true`.

## Result

| | |
|---|---|
| operations implemented | **12 of 12**, routes 1:1 |
| contract-declared codes implemented | **22 of 22** |
| codes reachable in code but undeclared | **1** |
| statuses reachable in code but undeclared | **1** |

### The one gap: 500 `INTERNAL_ERROR`

`SandopRepository.cs:91` returns `SandopResult.Error(500,"INTERNAL_ERROR")` from the catch that
aborts a failed transaction. **The contract declares no 500 on any of the twelve operations**, and
`INTERNAL_ERROR` appears in no `x-error-codes` list and no example.

This is **not** a local slip. The same last-resort path exists in every module of the service —
`LoadContextMiddleware.cs:99`, `ReturnContextMiddleware.cs:123`, `CarrierContextMiddleware.cs:97`,
`ClaimContextMiddleware.cs:116`, and the Load, Carrier, Return and Claim repositories — nine sites in
all (`evidence/internal-error-sites.txt`). It is a deliberate fail-safe, and it is the same family as
**Q498**, where `GET /shipments` under-declares its responses: the contract is narrower than the
behaviour. Closing it is a contract edit, which is the owner's, so it is recorded and not acted on.

Note also that MOD-0192's own `:199` records a **500 INTERNAL_ERROR with a zero correlation id** as
the measured behaviour of an authenticated Capacity request today. So a 500 the contract does not
declare is not hypothetical on this surface — it is the current observed answer.

### What conforms, and is worth stating

- **`contractVersion: "v1"` is on every envelope**: both error factories
  (`SandopContractError.cs:6`, `CapacityContractError.cs:5`), all four Sandop projections
  (`SandopProjection.cs:6-9`) and `CapacityPlanResponse` (`CapacityPlanModels.cs:29`).
- **The two context middlewares agree with each other and with the annex**, in the same order:
  echo a correlation (generating a rejection-only UUID when the header is missing, duplicated or
  malformed) → 401 `UNAUTHENTICATED` → 403 `FORBIDDEN` → 400 `INVALID_CORRELATION_ID` → 400
  `INVALID_REQUEST` for the idempotency key and content type. The annex states the same thing twice
  over: *"Auth failure happens before receipt"* and *"Missing/malformed/duplicate correlation uses
  400 `INVALID_CORRELATION_ID` … and one generated rejection-only UUID"*. The implementation does
  precisely that, including the part that looks wrong in isolation (generating a UUID) and is right
  against the annex.
- **No declared code is missing.** The first pass of this measurement reported `INVALID_REQUEST` as
  declared-but-unimplemented; that was **an error in the method, not a finding** — the code is
  emitted as a literal at 22 Sandop sites rather than through the error factory, and the comparison
  was redone over emission sites as well as factory constants. Both passes are in
  `evidence/` so the correction is checkable.

## What the five open pack lines still need

| line | still needs |
|---|---|
| MOD-0190 `:201` | the required/nullable field diff of the twelve schemas; the three event payloads vs the annex; 409/422/503 precedence at runtime. The operations, `contractVersion` and the declared-code set are MET. |
| MOD-0192 `:201` | the replay matrix (behavioural) and the same field diff. Operations, codes, headers and `contractVersion` are MET statically. |
| MOD-0192 `:199` | **unchanged and BLOCKED** — integration-owner authorization for two `Program.cs` pipeline lines, plus a re-pin of both packs' composition hashes. CT measured the fix and reverted it for lack of authority. |
| MOD-0192 `:200` | **unchanged and BLOCKED** behind `:199`: observable only over HTTP, and Capacity has no reachable HTTP path. |
| MOD-0192 `:203` | the G5 live run. Out of reach of a static measurement. |

**Neither pack was touched and no box was ticked.** Two of the five lines are blocked on an
authorization CT does not hold; the other three are now measured as far as static evidence reaches,
and what remains for each is named above rather than left implicit.

## Reproducing this

```
python3 -I evidence/extract_contract.py docs/analysis/contracts/sandop-capacity.openapi.yaml
python3 -I evidence/compare2.py
```

Both scripts are in `evidence/` and read only the contract and `services/Diten.SupplyChainService/src`.
`compare2.py` prints the three comparisons (A undeclared, B unimplemented, C undeclared statuses) and
is the file whose output section A carries the single finding above.
