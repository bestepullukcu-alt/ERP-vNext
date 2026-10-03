# MVP6-MOD0192-HOSTED-EVIDENCE-01 — SOP §22

## Verdict

**PASS — writer-complete for the exact authorized MOD-0192 hosted evidence scope.** The result closes the X07 hosted/process evidence request on the owner-approved 43-source transfer and exact `Program.cs` composition. It preserves the prior X01 closure. It is not canonical publication, duplicate-name acceptance, E5/G5, rollout, pack promotion, or commit/push authority.

## Authority and immutable source binding

The exact owner decision is recorded in `OWNER-DECISION.md`. The integration snapshot was `/private/tmp/mvp6-mod0192-hosted-evidence-01-20260923`; it was created after the earlier mutable integration location was rejected as an evidence source. `INPUT-MANIFEST.tsv` binds the disposition package, 43-source archive and manifest, MOD-0190 baseline, published contract/annex, and promoted pack.

Source verification passed with 379 MOD-0190 baseline paths plus 43 Capacity paths, zero overlap, and 422 combined entries. The MOD-0190 baseline was therefore retained byte-for-byte. The approved `Program.cs` baseline `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` reached exact target `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` through patch `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e`.

The only post-transfer delta was the authorized Capacity test-path probe in `CapacityAtomicityTests.cs`, recorded by `X07-LATER-READ-EVIDENCE-TEST.patch`: source hash `91999330faf2a50f5ff2cda3595506ecded50d5b87ffb7bf393dd655f71e51db`, target hash `2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43`, patch hash `b6744cddfd498536abeb4b350d8b8b2bd1c30c0824875757522026ed5f01df81`. No product source was changed after the approved transfer/composition.

## Build and source-to-process chain

Fresh Debug build passed with 0 warnings and 0 errors. The immutable binary manifest contains 52 files. Every launched API process used `Diten.SupplyChainService.Api.dll` SHA-256 `f43af77c5c7ffeb5ea438daac4801b61cdf89e0e39f6d5f9dfb40576e3e8ee8f`; `process-transcript.json` binds that hash independently for all five processes.

The first test launch did not execute tests because the host lacked the .NET 8 shared framework. That aborted result is retained in `capacity-hosted-dev.trx`. A second run over the same source and binary used the supported major roll-forward setting and passed 36/36 in `capacity-hosted-dev-rollforward.trx`. These are two runs and are not reported as one combined run.

## X07 later-read fault boundaries

Five separate Mongo failpoint cases passed. Each case recorded exactly one failpoint entry and one injected failure at its intended read boundary:

| Boundary | Skip index | Entries | Injected failures | Result |
|---|---:|---:|---:|---|
| broad event | 0 | 1 | 1 | PASS |
| exact event | 1 | 1 | 1 | PASS |
| broad audit | 2 | 1 | 1 | PASS |
| exact audit | 3 | 1 | 1 | PASS |
| active-slot count | 4 | 1 | 1 | PASS |

The assertions use accepted scope, content, count, and slot invariants. No event/audit physical Mongo `_id` equality condition was introduced.

## Native hosted runtime evidence

The isolated native process probe used API ports 56392/56393 and replica-set MongoDB port 57392 with test DB `DitenSupplyChain_Mod0192_Hosted_Dev`; operational MongoDB was not used. The probe passed with 21 HTTP records: status distribution was 200×3, 201×8, 202×3, 401×1, 403×1, 404×3, and 409×1.

The records cover real HTTP/JWT, missing token 401, permission denial 403, tenant and legal-entity isolation 404, soft-delete 404, all six Capacity operations, same-key replay, changed-payload `IDEMPOTENCY_KEY_REUSED`, and the finite fixture result. Response-loss evidence proves that the client closed before reading, the durable receipt existed before retry, and the same key recovered the same CapacityPlan identifier.

For kill/restart, the first host was killed after claim. A restarted host left the still-leased attempt running, reclaimed it only after expiry, and completed with attempt 2 and a higher fence. The final scoped state contained one terminal event, one terminal audit, and zero active slots.

For stale fencing and lease race, worker A held the terminal boundary past lease expiry; worker B reclaimed and completed with the new fence; worker A then logged lease loss. The final scoped state again contained one terminal event, one terminal audit, and zero active slots. This demonstrates fenced terminal ownership and durable recovery; it does not claim exactly-once execution.

No publisher was registered. All five process logs recorded the no-transport warning, and 10 Capacity outbox records remained Pending at final inspection.

## Dispositions and limits

- X01 remains closed; no X01 product rework was performed.
- No product defect was found within this exact hosted-evidence scope.
- The duplicate-name successor remains unpublished and outside this PASS. Its contract/release gate stays open.
- The fixture oracle remains a bounded fixture oracle; this evidence does not establish a live optimizer or producer.
- Canonical/guard, gateway, MOD-0190 behavior, shared permission, production database, migration, rollout, E5/G5, pack promotion, and git state were unchanged.
- Earlier harness-only RED attempts are retained separately in `discarded-pre-final-evidence.tar.gz`; they are not product verdicts. The final probe evidence is clean and separately sealed.

## Evidence map

- `combined-source-verification.json`, `COMBINED-SOURCE-MANIFEST.tsv`, `BINARY-MANIFEST.tsv`: source and binary linkage.
- `evidence/BUILD.log`: fresh build.
- `evidence/capacity-hosted-dev-rollforward.trx`: 36/36 and five separate X07 later-read fault cases.
- `evidence/http-records.json`: raw request/response outcomes with tokens redacted.
- `evidence/mongo-transcript.json`: scoped durable state and fault observations.
- `evidence/process-transcript.json` plus five process logs: binary/process/exit linkage, kill/restart, stale fence, and no-publisher state.
- `evidence/SUMMARY.json`: machine-readable consolidation.
- `PROCESS-CLEANUP.md`: dedicated process and port cleanup.

Writer state: **COMPLETE**. Independent verification must consume `VER-HANDOFF.md` and `SHA256SUMS` without modifying this directory.
