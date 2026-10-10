# MVP6-MOD0192-DUPLICATE-NAME-UPTAKE-VER-01 — HELD

Role: independent verifier who did not prepare or apply the uptake candidate.

Start only after all three conditions are evidenced: the exact v3 successor is canonical,
the conditional production-uptake authority is a real user decision, and a separate writer
has applied the exact patch in an isolated registered checkout.

Verify the exact 43-source baseline manifest
`36114191e1576b8b5654b9910f3347432d5d3bf36aac87339a4280cc85a852f8`,
patch `f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f`,
and all three target hashes in `SOURCE-MANIFEST.tsv`. Confirm that only those three
Capacity-owned files changed and that the production source delta is only the exact error
message mapping.

On a hash-bound disposable snapshot with a DB-010-compliant isolated Mongo replica set:

1. Fresh build the SupplyChain API and tests.
2. Prove HTTP 409 `CAPACITY_SCENARIO_NAME_CONFLICT`, exact message, unchanged Error body,
   current request correlation in body/header, and wire `contractVersion: v1`.
3. Independently force a real two-request exact-name race after both requests have passed
   their initial empty-name observation. Verify one 201, one defined 409, one scenario, and
   no receipt/audit/Pending-outbox write set for the loser.
4. Verify same-key/same-body replay first; changed valid payload
   `IDEMPOTENCY_KEY_REUSED`; lifecycle and fixture precedence; exact tenant/LE/plan scope;
   simple binary collation including case and whitespace controls.
5. Inject or otherwise prove that an unrelated duplicate key, a transient retry, and an
   unresolved commit do not become name conflict. Preserve receipt-first unknown-commit
   recovery.
6. Run the relevant CapacityPlans regression. Bind source → build → binary → process →
   HTTP/DB evidence and retain every failed/discarded harness attempt.

Do not repair source, change the contract/guard/Program.cs, reopen X01/X07, or claim
rollout/full-module/E5/G5 acceptance. Produce a separate SOP §22 verdict and raw archive.
