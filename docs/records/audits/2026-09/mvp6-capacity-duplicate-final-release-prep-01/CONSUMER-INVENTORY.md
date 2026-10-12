# Consumer inventory and exact impact — 2026-09-23 bounded observation

## External declaration, dated and scoped

The original owner asserted no repository-external SANDOP-CAPACITY application, SDK
or integration at **2026-09-22 08:42:05.340 UTC**, answering “Yoktur” to the explicit
SANDOP external-consumer question. The preceding 08:41:09.797 UTC message took inventory
responsibility. Both original role:user messages were read directly, not inferred
from a prior agent report; their text, IDs, source lines and hashes are in
external-inventory-owner-evidence.json.txt.

Preparation message SHA256 `01740204191807a16ef598b0a37fba0ed01fb225816f3390dd6517f3db1ab73a`;
clarification SHA256 `83ca37a02596b5f9b5f3406707f7939470064e1e506bd1398e6726ac8037585b`.
That selection concerned preparation of the earlier 2.0.0 release. It **does not select
3.0.0**, grant new-hash consent, cover later-discovered/deployed clients, or certify
continuing absence on September 23. No redundant blanket declaration is requested.
Any newly identified consumer needs its own disposition before runtime cutover.

## Known scope and accountable action

| Surface / owner | Evidence and exact impact | Required disposition |
|---|---|---|
| MOD-0190 design consumer; supply-chain-execution / control-tower | Main pack SHA256 `637690f32c1fa03e3039a4542bd53f7bb9b12f6b18d856a7d0e1f23d740c6877` references shared frozen contract. All six S&OP operations, schemas, headers, existing examples and annex business rules remain identical to A and published 2.0.0. Shared artifact version/path/hashes change. | Separate design-consumer consent to exact final trio and controlled future pin update; unchanged operation semantics is not inherited consent. |
| MOD-0192 design consumer; same domain owner acting separately | Main pack SHA256 `edd550b84451af082b934b392cd34f7dc24dd6e462f7e1d9ee0e03c21b4469f7`. One createCapacityScenario 409 gains the explicit code compared with 2.0.0; five other operations and all request/success/event schemas unchanged. A→final changes no error behavior. | Separate exact final design-consumer consent covering code/message, proof of name-index collision, lifecycle/fixture/receipt precedence and unknown-commit boundary. |
| Actual isolated Capacity producer, Lane B | Read-only observed `/private/tmp/mvp6-mod0192-core-dev-01/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityRepository.cs`, SHA256 `6bf026d3bfaeddf37ff4e461efedfa977a07765a413d5f363fcbdc80edfe0364`, line125 already emits unpublished deterministic name 409; lines136–142 generic retries can end in 503. `CapacitySchema.cs` SHA256 `ac704148cff99a8015ba3e8337ad7c05daf319d4f39eb18e571b09a7c0fc0ed7`, line21 has active-only simple-collation scoped name index. These are observations of an evolving isolated checkout, not an immutable release pin or runtime PASS. | Duplicate production correction and real race/HTTP verification remain separately authorized. This release-prep task neither edits nor accepts that behavior. X01/X07 Lane B authority is not duplicate-name publication/cutover authority. |
| Strict-code application clients, generated adapters, UI error handlers, retry/monitoring classifiers | Bounded main `services/frontend/gateway/tests/scripts` search is in consumer-search.txt. No matching client or exhaustive dispatch was identified in the searched files. Isolated CapacityContractTests only establish request/success projections, not strict duplicate-error acceptance. | Actual strict-client readiness remains unproven; a repository search cannot prove deployment or external absence. Inventory any runtime client before rollout. |
| Script/mock/test pins | A's verify_candidate.py SHA256 `ab5af12a13effa08839894f55cdcb1c8d1f7ff64689bfb3ca188bae9adc09591` deliberately tests old/new allowlists. Published manifest/repin and historical amendment scripts bind old versions/hashes; these are historical evidence, not a live client inventory. | Preserve historical validators and pins. New verification/uptake tooling must pin final hashes; technical repinning is not owner consent. No existing script/mock/pack pin changed here. |
| Main vs isolated module state | Main packs are draft; historical isolated-dispatch evidence has independently promoted copies and runtime sources. | Do not call all copies draft or absent. This task promotes neither and grants no runtime GO. |

## Exact compatibility counterexample

Published createCapacityScenario 409 allowlist is
`[CAPACITY_PLAN_STATE_CONFLICT, IDEMPOTENCY_KEY_REUSED]`.
The final-proposed list appends `CAPACITY_SCENARIO_NAME_CONFLICT`, preserving prior
order/examples. Its exact new message is `Capacity scenario name already exists in this plan`.
Status 409, Error envelope, correlation header/body and `contractVersion: v1` stay the same.
The old strict allowlist rejects the new value while JSON Schema accepts it because
Error.code is a string. Our final-byte test proves this counterexample, **not actual
consumer compatibility**. A→final allowlists and all examples are byte/structure equal.

## Version, publication and runtime cutover

Recommend **3.0.0** as the conservative major release signal; it is still a proposal.
Do not assert 2.1 minor compatibility or infer safety from a major number. The route
and wire v1 do not negotiate info.version. Design consent can authorize a specification
publication while leaving all runtime enablement closed. Before a producer emits the
new behavior, actual consumers must tolerate old and new codes, and an explicit
mixed-version cutover/rollback plan must be approved. Rolling back a YAML or consumer
alone cannot undo a deployed producer; existing receipts are never rewritten.

The decision package requests two bounded **design-consumer** consents, not invented
external consumer signatures or runtime readiness. Earlier 2.0.0 consents and C's
candidate-prep permission do not transfer to these final hashes.
