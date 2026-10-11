# Q420 (ledger Q356) — a dependency's 403 is a denial, not an outage

- Lane: Q420, `integration-agent`, single-writer on `ReturnReferenceReader.cs` and `ClaimReferenceReader.cs`.
- Placement: Claude app → Code tab → Local, `uname -s` = Darwin. G2 placement waiver applied — Cowork withdrawn by owner.
- Preflight 2026-10-04 22:58:20 +03: `feature/mvp6-logistics` · HEAD `cea01354e` · `git status --short` 48 · 0 staged ·
  no `index.lock`. Both reader files clean (equal to HEAD: `eaa0aa73…`, `3529fa55…`).
- Step 0 read: `AGENTS.md` (`ce8c12ad…`), `git-safety.md`, `code-style.md`, `integration-agent.md` (`f760471e…`), MOD-0186 §12,
  MOD-0187 §12, and — because §12 of both packs is a historical draft that declares no error codes — the two pinned annexes
  that do: `returns-semantics-v3.0.0.md` and `claims-semantics-v3.0.0.md`.
- **Stack window (announced for R-4c):** 2026-10-04 23:01–23:19 +03, own ports only — mongod 57420, 57421, 57422; services
  58420 (consumer), 58421 (Shipment/Carrier provider). 5000/5001/5056/5061 and R-4c's 57409/57410 were not touched.
- **Nothing staged or committed. No UI, no Shipments code, no contract, no test file touched. No error code invented.**
  Agent verdict ≠ CT ACCEPTED.

## Verdict

**FIXED, proven live in both modules, both sabotage directions caught.** A 403 from the Shipment or Carrier read now
reaches the user as a 403 with the code each module's annex declares; a 5xx or a refused connection still produces 503.

## CT's measurement, checked

`ReturnReferenceReader.cs:83` and `ClaimReferenceReader.cs:103` (pre-edit) both mapped
`(int)StatusCode >= 500 || Unauthorized or Forbidden` to 503 — **TRUE**. Measured live (BEFORE): a user without
`supplychain.shipments.read` creating a Return was told `503 DEPENDENCY_UNAVAILABLE`; a user without
`supplychain.carriers.read` creating a Claim that names a carrier was told `503 CLAIM_REFERENCE_UNAVAILABLE`. The
dependency itself had answered a plain 403 (`evidence/DEPENDENCY.txt`).

## The codes, and why — from the contract, not invented

| module | code chosen | contract text | also what the module already says for its own denial |
|---|---|---|---|
| Returns | **403 `INVALID_REQUEST`** | `returns-semantics-v3.0.0.md:62` "Auth401/**context403**/schema400/media415 use INVALID_REQUEST"; `:23` puts the grant check in the context step ("Auth -> trusted context/base grant -> …") | `ReturnContextMiddleware.cs:65` `Error(403)` → `INVALID_REQUEST` |
| Claims | **403 `FORBIDDEN`** | `claims-semantics-v3.0.0.md`: createClaim declares 403; "403 `FORBIDDEN` includes scope/identity/action failures"; message "The requested operation is not permitted." | `ClaimContextMiddleware.cs:61` `Error(403, "FORBIDDEN")` |

A user denied by the dependency is denied the action they asked for: the pack makes `supplychain.shipments.read` a
prerequisite of Returns/Claims create (MOD-0186/0187 §32.4, G-SHIPREAD), and a carrier-naming claim needs the carrier
read. So the same answer the module gives for its own missing grant is the honest one.

**401 deliberately unchanged (still 503).** The consumer has already validated the same token with the same signing
key before it calls the dependency. A dependency 401 therefore means the two services disagree about trust — a
configuration or rotation fault between services, not a missing user permission. Reporting it as the user's fault
would be the mirror image of this defect. Recorded as F-Q420-3 for the contract owner.

## Change (`evidence/readers.diff`, +9 / −2)

In each reader, before the existing 503 line: `if (StatusCode == Forbidden) throw <Failure>(403, <annex code>)`;
`Forbidden` removed from the 503 condition. Order of the other branches (404, root-invalid 500 → 502, OK) unchanged.
sha256 after: Returns `e61122b6…`, Claims `ac532a69…`.

## Live proof (`evidence/BEFORE.txt`, `AFTER.txt`, `SABOTAGE-OVERMAP.txt`)

Two SupplyChain services from a scratch copy: the consumer on 58420 (mongod 57420) reads Shipments/Carriers over HTTP
from a provider on 58421 (mongod 57421, 1.5 s server-selection timeout so a dead database answers fast). Tokens minted
with a lane secret (mode 600, never printed); one tenant/LE; a Shipment seeded through the provider's API.

| request (consumer, 58420) | BEFORE (HEAD readers) | AFTER (this lane) | SABOTAGE-OVERMAP (5xx also → 403) |
|---|---|---|---|
| Return create, user **without** `shipments.read` | 503 `DEPENDENCY_UNAVAILABLE` | **403 `INVALID_REQUEST`** | 403 |
| Return create, user **with** `shipments.read` (control) | 422 `SHIPMENT_NOT_RETURNABLE` | 422 `SHIPMENT_NOT_RETURNABLE` | — |
| Claim create naming a carrier, user **without** `carriers.read` | 503 `CLAIM_REFERENCE_UNAVAILABLE` | **403 `FORBIDDEN`** | 403 |
| Claim create, user **without** `shipments.read` | 503 `CLAIM_REFERENCE_UNAVAILABLE` | **403 `FORBIDDEN`** | 403 |
| Claim create naming a carrier, **with** `carriers.read` (control) | 404 `CLAIM_NOT_FOUND` (carrier not in list) | 404 `CLAIM_NOT_FOUND` | — |
| **Real outage — provider's database down** (provider itself answers 500 `INTERNAL_ERROR`), fully granted Return | 503 `DEPENDENCY_UNAVAILABLE` | **503 `DEPENDENCY_UNAVAILABLE`** | **403 — RED** |
| same, fully granted Claim | 503 `CLAIM_REFERENCE_UNAVAILABLE` | **503 `CLAIM_REFERENCE_UNAVAILABLE`** | **403 — RED** |
| **Real outage — provider stopped** (connection refused), Return / Claim | 503 / 503 | **503 / 503** | — |

Consumer database after every run: every collection 0 documents — no denied or failed request wrote anything
(`evidence/consumer-db-counts.txt`).

**Sabotage, both directions.** (1) Fix reverted (= BEFORE build): the denials go back to 503. (2) Over-fixed — 5xx also
mapped to 403 (`evidence/sabotage-overmap.diff`): the outage rows turn into authorization errors, which the outage probe
shows as RED. The proof therefore catches a fix that is too narrow and one that is too wide.

## Suites (Q335 recipe, own mongod 57422, one module per run)

| module | BEFORE | AFTER |
|---|---|---|
| Shipments | — | 90 / 0 / 90 |
| Carriers | — | 36 / 0 / 36 |
| Loads | — | 33 / 0 / 33 |
| **Returns** | **78 / 0 / 78** | **77 / 1 / 78** |
| Claims | 128 / 1 / 129 | 128 / 1 / 129 |
| SandopPlans | — | 19 / 0 / 19 |
| CapacityPlans | — | 48 / 0 / 48 |

- Claims' one failure in both is the known `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`
  ("Explicit write/read restart mode required") — by design.
- **Returns' new failure is the test that pinned the defect:** `ReturnReferenceTests.OtherProducerFailures_RemainDependencyUnavailable`
  case `status: 403` (`ReturnReferenceTests.cs:49`) asserts that a dependency 403 becomes `503 DEPENDENCY_UNAVAILABLE`.
  The test file is outside this lane's single-writer scope and was **not** edited (F-Q420-1).
- A first BEFORE run was invalid (macOS bash 3.2 has no associative arrays, so variable and filter were empty) and was
  discarded; the table above is the corrected rerun.

## Findings

- **F-Q420-1** — `ReturnReferenceTests.cs:49` (`InlineData(403, …)` in `OtherProducerFailures_RemainDependencyUnavailable`) pins the defect: it fails now that the 403 is reported as 403. It needs a test lane to change that case to expect `403 INVALID_REQUEST` (and keep the 401 and 5xx cases). Until then the Returns suite reads 77/1/78.
- **F-Q420-2** — No suite test guards the new behaviour in either module. Claims' transport tests cover refusal, timeout, invalid JSON and 404, but no 401 or 403 (`ClaimReferenceTests.cs:76`). The live run and its two sabotages are the only proof today; a Claims 403 case (Shipment read and Carrier read) belongs in the same test lane as F-Q420-1.
- **F-Q420-3** — A dependency **401** stays 503 by design (the consumer already authenticated the token; a dependency 401 is an inter-service trust fault). The contract does not say which; if the owner wants it otherwise, that is a contract decision, not a code change.
- **F-Q420-4** — The Returns denial is generic: `INVALID_REQUEST` with the message "INVALID REQUEST" (the module turns the code into its message). A user cannot tell "you lack shipments.read" from any other context 403. The annex allows nothing more specific for Returns; a distinguishable code would have to be added to the contract first (K12).
- **F-Q420-5** — The OpenAPI file declares only 201/404/409/422 for `createReturn` (MOD-0186 §22); the 403 is declared by the annex, not the YAML operation. Same family as F-Q218-7 (statuses the code returns that the YAML does not declare).
- **F-Q420-6** — The dependency's own 403 bodies carry messages ("Required shipment permission is missing.") that the consumer does not forward. That is correct (no foreign text leaks), and it means the user is not told which permission is missing.

## Not done

- No test file changed; no UI; no Shipments or Carrier code; no contract.
- No run through the Gateway or the Web UI — the consumer was called directly; the Gateway passes these statuses through
  (Q290 measured that path for this family).

## Cleanup

Stopped by port, own processes only: services 58420, 58421; mongods 57420, 57421, 57422 (shutdown). All five ports
closed. Scratch folder `~/mvp6-env/q420-20261004-2301/` kept (no `rm`); the secret file in it is mode 600.
