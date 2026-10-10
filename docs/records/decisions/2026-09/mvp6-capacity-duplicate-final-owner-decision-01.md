---
decision_id: MVP6-CAPACITY-DUPLICATE-FINAL-OWNER-DECISION-01
status: approved
decided_at_utc: 2026-09-23T05:55:28Z
decision_source: current-role-user-message
scope: SANDOP-CAPACITY final artifact selection, MOD-0190 and MOD-0192 design-consumer consent, conditional single-writer publication
---

# MVP6 Capacity duplicate final owner decision

The owner approved all four decisions in
`docs/records/audits/2026-09/mvp6-capacity-duplicate-final-release-prep-01/OWNER-DECISION-PACK.md`
with the scope and conditions stated there.

## Exact binding

| Decision target | SHA-256 |
|---|---|
| A-derived SANDOP-CAPACITY 3.0.0 / FROZEN / wire v1 YAML | `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab` |
| SANDOP-CAPACITY semantics v3.0.0 annex | `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64` |
| Two-target publication patch | `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23` |

## Approved decisions

1. The A-derived SANDOP-CAPACITY 3.0.0 / wire v1 final is selected at the exact hashes above. B and all historical candidates remain historical and unchanged.
2. MOD-0190 receives separate design-consumer consent for the exact trio. This is specification-design consent only and does not establish a running consumer, HTTP/runtime uptake, rollout, or pack promotion.
3. MOD-0192 receives separate design-consumer consent for the exact trio, including the exact `CAPACITY_SCENARIO_NAME_CONFLICT` disposition and the duplicate-name precedence/race/unknown-commit contract stated in the approved pack. This is not duplicate-name production-source application authority.
4. One contract publication writer may apply the exact patch to the two named canonical targets only after the exact preimage/target/patch checks, independent final verification, disposable byte equality, and current publication/guard gates all pass. A failed gate blocks publication. This decision does not authorize a guard change, exception, or activation.

## Publication conditions and exclusions

The canonical YAML preimage must be `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` and the v3 annex target must be absent before application. The existing v2 annex SHA-256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` must remain unchanged. Both outputs must be verified together; no partial publication is permitted.

This decision grants no duplicate-name production source change, `Program.cs`, shared permission, gateway, guard mutation/activation, runtime rollout, migration, publisher, live producer/optimizer, pack promotion, E5/G5, commit, push, or stash authority. Strict-code consumer readiness, mixed-version cutover, rollback, and any consumer discovered after the dated external-inventory declaration remain separate runtime gates.
