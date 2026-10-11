# MVP6-R2-COMBINED-RELEASE-CANDIDATE-01 — SOP §22
Verdict: actual combined candidate produced; bounded author validation PASS. Independent VER and final release authorization OPEN.

Exact handoff inputs verified:3archive hashes and all named YAML/patch/annex/baseline hashes;56 internal checksum entries verified (overlap exists, not56unique policy files). Common baseline93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571.

## Actual application / conflict
Original Root→Returns→Claims and Root→Claims→Returns each applied first two patches successfully; third fails one hunk at fuzz0. Claims hunk context expects baseline Returns response tail; Returns hunk context expects baseline Claims operation head. These are TEXTUAL CONTEXT conflicts. Original rejects and output hashes retained. No semantic business conflict found: consumer deltas confined exactly to separate operation text blocks; root delta outside both.
Context-only rebase builds each next diff against preceding actual output using exact original consumer block bytes. Both rebased patch sequences applied, not dry-run, all exits0, and final YAML byte-identical:
d763b571a1c6b88cbfadc56af2c51da1c0009589e3b82bcbe122329b17213c88.
Exact final baseline patch reapplication verified. Root schema/getShipment example and four consumer path/method groups match source candidates; baseline-ast-diff.json records real changes. Shared components equal Root R2 (only authorized root delta), other paths and top-level values preserved.

## Fresh validation
68/68 combined checks,298 local refs,236 JSON Schema/format examples;49 Claims lifecycle policy pairs compared with baseline canonical edge oracle. Root optional nullable UUID, Claims investigate permission and exact lifecycle/payload/root codes verified. Returns operation policies preserved exactly, including its distinct error/fingerprint profile. No business rules normalized across modules.
No complete OpenAPI meta-schema validator installed; OpenAPI validation here is parsed structure, refs, examples and exact source-union comparison. Independent VER must retain this limitation.

## Historical evidence
Accepted successor Returns UUID/Claims stateful closure reused only via unchanged operation/annex hash and AST mapping, not counted as fresh model execution. Old flawed model results not promoted. R01–R30 static/unverified limits remain. No HTTP/JWT/Mongo/persistence/restart or producer/consumer uptake tested.

## Release boundaries
Current bytes retain2.0.0 metadata with Root R2 CANDIDATE status.3.0.0/wirev1 is written ONLY in release-proposal.md, not selected as approved release; no FROZEN declaration. Annexes retain original bytes and are locally mapped. Any later version/link/status edits yield new hashes requiring exact final disposition. Prior consents do not transfer. Loads multi-root, external consumer owner decisions, guard policy/payload and runtime rollout remain separate gates.

Only this new durable audit/candidate directory and unique disposable directory written. Canonical/runtime/guard/pack/git unchanged by this task. Source archive pins and current canonical hash rechecked. Existing dirty/concurrent work preserved; no global checkout immutability claim. Single candidate writer; no publication writer or operational DB.
