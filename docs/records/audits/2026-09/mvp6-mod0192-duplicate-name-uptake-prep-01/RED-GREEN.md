# Duplicate-name uptake RED→GREEN

## Baseline finding

The exact 43-source baseline already implements the business branch. In
`CapacityRepository.CreateScenarioAsync`, target lookup precedes receipt lookup; receipt
replay precedes lifecycle, fixture, and exact-name validation; exact-name lookup uses
`Collation.Simple`; and a deterministic duplicate returns 409
`CAPACITY_SCENARIO_NAME_CONFLICT`. The unique active-name index is already scoped by
`TenantId`, `LegalEntityId`, `CapacityPlanId`, and `Name` under simple collation.

A real deterministic race was forced after the loser's first scenario-name read returned
empty. The winner committed through a second repository instance. The losing transaction
received Mongo's transient transaction conflict, used the existing bounded retry, reread
the exact name, and returned the same 409 with no losing receipt/audit/outbox. This means
the repository needs no new duplicate-key classifier. Adding one would risk reclassifying
unrelated duplicate keys, transient retries, or uncertain commits.

## Actual RED

The existing API error mapper has no case for the successor code. With the new exact
contract assertion against the unmodified production mapper, the body message was
`Invalid request`, not `Capacity scenario name already exists in this plan`. The retained
raw run `duplicate-name-red-final.trx` contains that genuine failure together with one
discarded experimental race oracle (`scenarioFinds == 1`). That oracle was rejected after
the run proved that the existing retry-and-reread path is the correct convergence path;
it is not counted as a product defect or acceptance result.

## GREEN candidate

The production delta is one exact error-message mapping. Two test-file deltas make the
pre-existing repository behavior explicit:

- exact same key/body replays the original result before current validation;
- changed valid payload stays 409 `IDEMPOTENCY_KEY_REUSED`;
- exact active duplicate in the same tenant/LE/plan returns the successor 409;
- case, leading whitespace, plan, tenant, and LE differences do not collide;
- lifecycle and fixture errors retain precedence over name conflict;
- an unrelated `_id_` duplicate is not classified as name conflict;
- a forced unique-name race yields one scenario and no losing write set.

Fresh disposable results:

| Run | Result | SHA-256 |
|---|---:|---|
| Targeted successor checks | 3/3 PASS | `5173195f7c7fda40e24c50fe060fc8e5ae1b9226bdb4d752b1813a52c53b5eab` |
| CapacityPlans regression | 34/34 PASS | `26786caa9adb7671de407228e534d84ddf3083a2888560ab0987b1ec4721bfbb` |

The first broad regression attempt was 27/34 because the disposable Mongo process lacked
`enableTestCommands`; all seven failures were failpoint setup failures. It is discarded,
retained in the disposable workspace, and not counted. The final run used the same isolated
port/database with test commands enabled and passed 34/34.
