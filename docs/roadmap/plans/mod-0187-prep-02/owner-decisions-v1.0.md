# MOD-0187 PREP-03 — Owner Decision Package v1.0

**STATUS: PROPOSED / UNAPPROVED.** This package is for owner review. It does not amend a frozen contract, promote the draft pack, or authorize DEV/VER.

## Authority

Frozen Claims paths and schemas: `docs/analysis/contracts/shipment-bundle.openapi.yaml:1433-1516`; ClaimStatus and Decimal: `.../shipment-bundle.openapi.yaml:1557-1568`; command/response schemas: `.../shipment-bundle.openapi.yaml:1819-1852`; LifecycleEventEnvelope/Error: `.../shipment-bundle.openapi.yaml:1896-1932`.

The unresolved decision rows are recorded in `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md:280-295`. Pack status remains `draft` (`:9`, `:23`).

## D187-01 — Shipment, Carrier and evidence eligibility

**Frozen facts:** create carries a required Shipment UUID and optional nullable Carrier UUID; the contract provides references but no evidence lookup operation (`shipment-bundle.openapi.yaml:1819-1825`). Claims owns no Shipment/Carrier/evidence source of record (pack §24).

**Open questions:** eligible Shipment statuses; whether a null or mismatched Shipment carrier permits a claim; treatment of retired Carrier; whether evidence IDs must exist now.

**Exact proposal:** resolve Shipment through the published MOD-0183 read seam and require an eligible, non-deleted Shipment in the caller tenant/LE. If `carrierId` is null, accept a claim without Carrier validation. If supplied, resolve the Carrier through the published Carrier read seam and require tenant/LE scope and either a matching Shipment carrier or an owner-approved historical exception. Do not validate `evidenceReferenceIds` until an owner-designated evidence seam exists; store opaque strings immutably and expose their presence only.

**Rationale/alternative:** hard evidence existence validation without a published seam would invent a dependency. Requiring Carrier always would reject valid carrier-less claims and changes business scope.

**Acceptance:** same-scope eligible Shipment succeeds; missing/cross-scope Shipment returns the owner-bound not-found result; null Carrier succeeds; matching Carrier succeeds; mismatched/retired Carrier follows the signed decision; malformed/unavailable dependency leaves zero claim/receipt/audit/outbox writes; evidence IDs are not silently treated as verified.

**Contract action:** clarification if using existing references and statuses; versioned amendment required only if a new dependency operation or response field is needed.

## D187-02 — Amount and lifecycle conditions

**Frozen facts:** Decimal is a string schema; ClaimStatus is `Open, Investigating, Approved, Rejected, Settled, Closed, Withdrawn` (`shipment-bundle.openapi.yaml:1557-1568`). `approvedAmount` is nullable/optional (`:1830`, `:1848`). Lifecycle arrows are documented in pack §22.

**Exact proposal:** create starts `Open`; `claimedAmount` must be strictly greater than zero as a business rule. `Approved` requires non-null approvedAmount, `0 <= approvedAmount <= claimedAmount`; supplied approvedAmount on non-Approved targets is rejected as a command conflict. `Rejected`, `Closed`, and `Withdrawn` never create or retain a new approved amount. `Settled` is an operational claim status only; it does not post AP/AR, payment, credit, recovery or bank settlement.

**Rationale/alternative:** separating wire Decimal from business positivity avoids rewriting schema. Allowing arbitrary approvedAmount would make lifecycle meaning ambiguous; financial posting would exceed Claims ownership.

**Acceptance:** negative/zero claimed amount rejected; exact/equal approved amount accepted; null/missing Approved amount rejected; over-claimed rejected; gratuitous amount on another target rejected; Settled writes only Claim lifecycle/audit/event records and no finance collections.

**Contract action:** clarification for business validation; amendment required if the wire must expose a new amount or finance reference. No settlementReference may be invented.

## D187-03 — Decimal storage and currency

**Frozen facts:** Decimal pattern is `^-?\d+(\.\d+)?$` (`shipment-bundle.openapi.yaml:1557`); currency is three uppercase letters in the command (`MOD-0187 pack §22`). No ISO membership is frozen.

**Exact proposal:** accept only the frozen decimal string grammar; reject JSON numbers, exponents and whitespace variants. Preserve the original decimal text for replay/audit and use an arbitrary-precision decimal representation for comparisons. Do not impose a two-decimal, length or ISO-4217 restriction until owner/data approval. Currency validation is exactly `[A-Z]{3}`; membership lookup is out of scope.

**Acceptance:** `250`, `250.0`, `-0`, and large schema-valid strings are handled according to the signed lexical/arithmetic policy; no binary float conversion or silent rounding; malformed/exponent/JSON-number inputs fail deterministically.

**Contract action:** clarification for implementation storage; versioned amendment required if precision/scale, currency membership or canonical lexical normalization is added.

## D187-04 — Action to permission matrix

**Frozen facts:** no permission fields appear on the wire; pack proposes `claims.read/create/investigate/decide/settle` (§23).

**Exact proposal:** `queryClaims` → `claims.read`; create → `claims.create`; Open→Investigating and Withdrawn → `claims.investigate`; Investigating→Approved/Rejected → `claims.decide`; Approved→Settled → `claims.settle`; Rejected/Settled→Closed requires `claims.decide` unless owner assigns a separate `claims.close`. No self-approval or workflow actor is invented. Every action also requires tenant/LE scope.

**Acceptance:** each source/target/action combination is tested with granted and missing permission; missing grant fails closed; cross-tenant/LE IDs never disclose state.

**Contract action:** clarification/security decision; amendment only if permission metadata is added to the wire.

## D187-05 — Errors, headers, correlation and replay

**Frozen facts:** Claims paths declare success and selected 404/409/422 statuses (`shipment-bundle.openapi.yaml:1433-1516`); Error schema is shared (`:1924-1932`). Transition does not declare 409. `idempotentReplay` exists in ClaimResponse (`:1838`).

**Exact proposal:** require UUID `X-Correlation-Id` and POST idempotency key (1–128 chars), subject to the security owner’s duplicate-header/parser decision. Preserve the first command root in durable event/audit records. Same key + same scoped payload/root returns the original success without new mutation/audit/event. Same key + changed payload or different target returns the frozen create conflict where declared; transition conflicts remain the frozen 422 path, never an invented 409. Different-root replay is rejected with the owner-selected declared error; no Carrier replay policy is copied. Dependency failures use only statuses/codes that the frozen operation declares.

**Acceptance:** operation-by-operation status/code/header matrix covers missing/duplicate correlation, missing permission, cross-scope target, changed payload, different root, same-state/new-key, concurrent duplicate and post-commit response loss. Response/header correlation and event root are asserted separately.

**Contract action:** owner clarification is sufficient if existing statuses and schemas are used; a versioned amendment is mandatory for any new status, error field, replay header/body field or transition 409.

## D187-06 — Transaction, duplicates and numbering

**Frozen facts:** ClaimResponse has server claim ID/number and lifecycle fields (`shipment-bundle.openapi.yaml:1838-1852`); no client number or version field exists.

**Exact proposal:** one replica-set transaction owns Claim, receipt, audit and Pending outbox event. Unique ClaimNumber is server generated and tenant/LE scoped; number format and collision retry require owner selection. Different idempotency keys for the same Shipment are allowed unless the business owner explicitly chooses a duplicate-claim rule. Same key has one durable receipt. Unknown commit resolves by receipt lookup; replay emits no second event. No publisher/worker or finance write.

**Acceptance:** concurrent same-key create yields one claim/receipt/audit/event; different keys follow the signed duplicate policy; failures after each write boundary leave no partial state; restart preserves number, receipt, audit and Pending event; number collision is retried or fails with the signed persistence result.

**Contract action:** clarification for storage/transaction policy; amendment only if ClaimNumber format, duplicate semantics or event payload is exposed on the wire.

## Owner response required

Owner must mark each D187-01…06 `APPROVED`, `REJECTED` or `NEEDS-AMENDMENT`, identify the accountable authority, and attach the exact operation/schema pointer plus acceptance scenario. Until then the pack remains draft and all prompts remain HELD.
