# Version and same-route compatibility assessment — proposal, not consent

## Recommendation

Prepare **3.0.0-rc.1 / CANDIDATE / wire v1**, with proposed eventual major line 3.0.0.
Final version is deliberately unapproved under C. This is a conservative compatibility
classification for an added observable exact error code; neither the recommendation nor
successful OAS validation authorizes publication.

| Option | Assessment |
|---|---|
| 2.0.1 | Not recommended: the change adds a declared wire outcome, not just editorial clarification. Existing implementation emitting an unpublished code does not make that code baseline authority. |
| 2.1.0 | Plausible only after explicit owner classification and demonstrated consumer tolerance. An added 409 code is not automatically backward compatible for exhaustive allowlists, switches, generated client adapters, UI error dispatch, monitoring or retry classifiers. |
| 3.0.0 (candidate 3.0.0-rc.1) | Recommended to flag the unresolved compatibility-sensitive new outcome. No route negotiation or runtime migration follows from this metadata change. |
| Alias to CAPACITY_PLAN_STATE_CONFLICT | Rejected: a valid Draft plan's exact-name collision is not lifecycle ineligibility; actual C selects the explicit name-conflict candidate. |
| Remove uniqueness | Rejected: contradicts the existing exact-name uniqueness rule and C scope. |

## What is unchanged and what a strict consumer observes

Route remains `/api/supply-chain/capacity-plans/{capacityPlanId}/scenarios`;
operationId and HTTP status 409 remain the same; Error envelope and `contractVersion: v1`
remain the same. The extra value is `CAPACITY_SCENARIO_NAME_CONFLICT`, with exact message
`Capacity scenario name already exists in this plan`. Existing state-conflict and
idempotency-reused codes/examples are retained. The response component name is retained
to avoid an unrelated ref rename; only createCapacityScenario references it.

The baseline Error schema uses a string code, so an unknown code passes JSON Schema.
Our strict-consumer fixture rejects the new value against the baseline allowlist and
accepts it against the candidate allowlist. This is a technical counterexample to
“schema-valid means compatible,” not an actual consumer implementation test or consent.
A real consumer may throw on an exhaustive switch, lose a user-facing message, or retry
incorrectly. Name conflict should be handled as a business conflict, not a dependency
retry or plan-state transition signal; exact consumer behavior must be reviewed.

MOD-0190's six operations, referenced responses and shared schemas are unchanged.
MOD-0192's other five operations are unchanged. Their no-diff result does not transfer
old artifact consent to a new shared YAML/annex hash. Inventory all actual consumers;
MOD-0190 and MOD-0192 owners must separately disposition the shared artifact/repin.
No consumer source, pin, mock or permission is updated in this task.

## Same-route/wire-v1 transition — HELD requirements

1. Independent contract VER approves the exact candidate scope and tests; owner selects
   final version and produces exact final YAML/annex/patch hashes. CANDIDATE → FROZEN
   or rc → final changes bytes and therefore requires fresh exact-hash review.
2. Inventory consumer pins and strict-code behavior. Obtain explicit consent for the
   final hashes, including same-route rollout/mixed-version behavior. Preserve baseline
   codes; consumers accepting the new code must continue to accept old ones.
3. Coordinate consumer readiness before enabling the new producer branch. Old and new
   binaries behind the same route cannot select a contract by `info.version`; wire v1
   has no new negotiation header in this proposal. If any required consumer cannot
   accept the outcome, hold cutover or separately design version negotiation. No new
   route/header is smuggled into this bounded amendment.
4. Separately authorize canonical publication and duplicate-name source implementation.
   Lane B's X01/X07 authorization is not this authorization. Production deterministic
   and real unique-index race tests, same-key receipt races, rollback/unknown commit,
   HTTP status/body/header and consumer uptake then require independent runtime VER.
5. Coordinate rollback too. Reverting YAML alone does not revert a deployed emitter;
   a consumer rollback that rejects the new code is unsafe while such a producer remains.
   Record producer/consumer versions and receipt behavior; never rewrite stored receipts
   or historical consent. No migration, rollout or rollback execution occurs here.

Publication, consumer consent, production application, pack promotion, guard, runtime
acceptance and CT/E5/G5 closure remain separate gates. Current published 2.0.0 bytes
and all historical records remain intact.
