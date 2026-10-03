# Proposed transfer order — HELD

This order is executable only after a new exact dispatch authorizes the target checkout and selected inputs.

1. Create or select one registered, clean integration worktree at base commit `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Record its absolute path, branch/detached state, initial porcelain and HEAD. Do not reuse an existing mutable evidence worktree as though it were clean.
2. Verify every input hash in `SELECTED-SOURCE.json`. Stop on any mismatch.
3. Extract the final accepted MOD-0190 379 archive. Verify all 379 manifest rows before continuing. This single archive supplies accepted effective bytes for MOD-0183–0187 and MOD-0190; do not layer older module archives over it.
4. Overlay the Capacity 43 archive. Require a zero-path intersection with the 379 manifest, then verify all 43 rows.
5. Verify `Program.cs` preimage `a2a216be…`, apply only `PROGRAM-COMPOSITION.patch` `78cc0fa6…`, and require target `50c48a2…`. Stop rather than resolving context drift.
6. Verify `CapacityAtomicityTests.cs` preimage `91999330…`, apply only `X07-LATER-READ-EVIDENCE-TEST.patch` `b6744cdd…`, and require target `2c133ac1…`.
7. Derive the sorted 422-row manifest using the algorithm in `SELECTED-SOURCE.json`; require digest `cdc6228aa3d215f34f2f2f9d5b784cf57ac20b6eb8f2c2f26bdcace9dcd4d76c`, no extra transferred path, and the accepted S&OP test target `eba2da6f…`.
8. Assert the three duplicate-name successor targets were not introduced. Also assert normal Claims uses the NoOp commit probe and that no ClaimsEvidence product target was transferred.
9. Run the minimum integration regression in the active dispatch. Archive source→build→binary→process→HTTP/DB evidence and report integrated results independently of isolated acceptance.

## Minimum integration regression

- Hash gate all selected paths, fresh restore/build and API startup; fail on DI, middleware, route-model or serializer collisions.
- Exercise one S&OP golden chain: create → snapshot → sign-off/list, same-key replay after process restart, one permission denial and one foreign tenant/LE denial.
- Exercise one Capacity golden chain: plan → scenario → evaluation → hosted terminal/read, verifying a single terminal audit, one Pending outbox event and active-slot release; include one permission denial and one foreign tenant/LE denial.
- Exercise only changed or cross-module seams: Program coexistence; Shipment root/reference into Returns and Claims; one Carrier/Shipment/Loads path; Returns R01 malformed producer mapping; Claims strict UTF-8 idempotency header and root malformed/mismatch handling; MOD-0190 committed-ack oracle; MOD-0192 X01 and five later-read X07 focused checks.
- Keep live DEMAND/constraint, Workflow, publisher/Event Bus, gateway, UI, rollout and E5/G5 outside the result. Do not rerun all closed historical suites unless a new failure or drift justifies it.
