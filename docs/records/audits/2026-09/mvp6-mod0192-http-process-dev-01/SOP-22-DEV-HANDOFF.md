# MVP6-MOD0192-HTTP-PROCESS-DEV-01 — SOP §22

**Writer verdict: PARTIAL PASS; writer-complete and independent VER ready.** Exact shared composition, real HTTP/JWT, normal restart, two-host executor competition, and all five X07 later-read failures were exercised. The rapid literal fixture completed before a controlled host kill could preserve an in-flight lease, so hosted renewal-loss/stale-worker/lease-expiry timing remains PARTIAL; existing repository tests cover those mechanics but are not relabeled as hosted evidence.

## Authority and immutable inputs

The user approved on 2026-09-23 the exact 43-source manifest `36114191e1576b8b5654b9910f3347432d5d3bf36aac87339a4280cc85a852f8`, transfer archive `753843251ea261473e308e5fb100de834b1e5ab88fa7576ff50ad22bf332274e`, `Program.cs` baseline `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5`, patch `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e`, and target `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0`. The decision also authorized evidence/test changes within the existing Capacity test paths and independent VER, while excluding further persistence, MOD-0190 behavior, duplicate-name policy, gateway/permission, canonical/guard, live producer, publisher, rollout, E5/G5, commit and push.

A registered detached worktree `/private/tmp/mvp6-mod0192-http-integration-01` was created at HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. The immutable MOD-0190 379-source archive/manifest was transferred first, followed by the exact 43-source archive. Their path intersection was zero. The Program patch applied only after its exact baseline matched.

## Exact source and build chain

| Artifact | SHA-256 / result |
|---|---|
| Final 422-path source manifest | `59d21906b77c82431df3746ad7725e305ed7286029266378fe7288c6a8a10151` |
| Final 422-path source archive | `500c395438ee34875b872671f41ce36420512d0682bd1757310c7505e3ef9970` |
| Program composition target | `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` |
| Evidence-only test target | `CapacityAtomicityTests.cs` `3953b09ac16d20978dda4ab62ca571df52611cb4e5acdecc2e2fe7702015c4aa` |
| Evidence test patch | `3f0dbbf7875837873d433f414f20468bd8a7fcf38573b411b58d3094705016fc` |
| API binary | `068255490253ac31038f630be4ac0a3a6302eb66dffc7ba4027ceeb0204fa804` |
| Persistence binary | `f05302bed6ab214348b579ac1b5b334d0f7235a688c2d48c7d111ed7b5b84a29` |
| Test binary | `baa22ca8c72bef962fa60801a8b6c7db0d7bd2c7289aa9399411f2d136499b95` |
| Fresh API build | exit 0; 0 warnings; 0 errors |

Relative to the authorized inputs, only two source/test files changed: the exact approved `Program.cs` target and the Capacity-owned evidence test. No production persistence file changed.

## Acceptance evidence

| Requirement | Fresh result | Boundary |
|---|---|---|
| Five X07 later reconciliation reads | **5/5 PASS.** `failCommand aggregate` with skips 0–4 independently targeted broad event, exact event, broad audit, exact audit and active-slot counts. Each produced one actual injected failure, reconciliation threw/refused success, state was unchanged, and reconciliation succeeded after failpoint removal. | Test-only Mongo failpoint; no production seam or endpoint. |
| Full Capacity test group | **36/36 PASS** in `capacity-dev-final.trx`. This is the prior 31-case group plus five new later-read cases. Historical 20/20 remains contained in the historical 31/31 and is never added to it. | Repository/process harness; not automatically composed HTTP. |
| Six HTTP operations | **PASS.** Plan create/get, scenario create/get and evaluation create/get returned published 201/200/201/200/202/200 shapes. | Real composed API on localhost 56192. |
| JWT/RBAC/header/isolation | **PASS.** 401 unauthenticated, 403 authenticated without permission, malformed correlation 400, tenant query override 400 and foreign-tenant GET 404 were observed with response correlation. | Local signed JWT; no gateway claim. |
| Replay/conflict | **PASS.** Plan/scenario/evaluation same-key replay returned original body/status with current response correlation; changed plan payload returned 409 `IDEMPOTENCY_KEY_REUSED`. | Normal HTTP path. |
| Hosted terminal effect | **PASS.** Evaluation moved Accepted→Completed with literal `40.000` shortfall; DB showed one terminal event, one terminal audit, zero active slots and terminal `OccurredAt` equality. | Exact fixture only; no optimizer claim. |
| API kill/restart persistence | **PASS.** After graceful process stop/start, plan and Completed evaluation remained readable and the original Accepted 202 receipt replayed without a second evaluation. | Normal process restart, not an in-flight kill. |
| Two hosted executors | **PASS for single terminal effect.** Two API hosts on 56192/56193 shared one DB. The evaluation finished with `attempt=1`, `fence=1`, one terminal audit, one Pending event and zero active slots. | The fast fixture did not establish renewal-loss or stale-worker timing. |
| No publisher | **PASS.** Host logged that transport was unregistered; Capacity event remained `Pending`. No Capacity publisher was introduced. | Shipment worker is not treated as a Capacity publisher. |
| Hosted lease kill/renewal loss/stale worker | **PARTIAL.** Repository tests inside 36/36 cover fencing, lease expiry and restart mechanics, but no controlled hosted in-flight lease survived a process kill in this run. | Requires a separately authorized bounded delay/control seam if full hosted timing proof is mandatory. No such seam was invented. |
| Hosted terminal response loss / unknown commit | **PARTIAL.** Real Mongo failpoints prove repository committed/uncommitted outcomes; normal HTTP restart proves durable replay. A transport-level response-loss injection at the hosted boundary was not available without a new seam. | Existing repository closure is preserved; hosted observation remains distinct. |

The physical Mongo `_id` of event/audit documents was not asserted. Accepted identity remains the scoped business effect tuple, exact content and count.

## Discarded attempts

- `capacity-dev.trx`: VSTest local socket denied by sandbox; 0 executed.
- `capacity-dev-escalated.trx`: wrong global .NET host lacked ASP.NET Core 8; 0 executed.
- `capacity-dev-dotnet8.trx`: 32 PASS / 4 FAIL. All five later-read faults fired, but four harness assertions incorrectly expected skipped aggregate matches to increment the failpoint failure count. Mongo reports actual injected failures only. The evidence assertion was corrected from `skip+1` to exactly one; product source was unchanged.

## Cleanup and handoff

Verifier-relevant commands are in [COMMANDS.md](COMMANDS.md); raw TRX, HTTP JSONL, DB snapshots, listener inventory and cleanup are under `raw/`. API listeners 56192/56193 and Mongo 57192 were stopped; `raw/process-cleanup.txt` is empty. The main checkout was not used as a runtime source and received only this evidence directory. No commit, push or stash occurred.

**Writer-complete:** the immutable source archive, manifests, exact two-file delta and raw evidence are ready for a separate independent verifier. The verifier must not share this mutable worktree or Mongo data directory.
