# MVP6-MOD0190-HTTP-DEV-01-R1 — SOP §22

**Agent verdict: PARTIAL / HTTP mutation REWORK required.** The exact owner-approved transfer and composition are applied in one separately registered isolated worktree, but authenticated mutation HTTP fails with a missing fixture-reader DI registration. The DEV is not complete; independent VER is not dispatched against a false PASS. No CT acceptance is claimed.

## Authority, branch and worktree

The user's three-part decision explicitly approved A's 38-file transfer manifest `b55e7b2128df259604e4a318cc9194bfeff24610b1dba1790f333ef20d74a824`, core source archive `09bfb801490e3a7aefdb6925b66c0187062f4f3f52a4efb0f0b1e70aa0fd2bcd`, normal integration archive `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21`, composition patch `bd972051be35971465b008d783afe9eabf529d90b3e12ccfe6369f2c9c12a074`, Program baseline `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` and target `0f6bf84e1c7ddff79868d32a23099e8934a33e3ac80ccecbe8b16f5e0a6c5cc2`.

`git worktree add --detach --no-checkout /private/tmp/mvp6-mod0190-http-integration-01 4a8d4d4b339528a88e6220fb8402e5a2c771136c` created an empty registered worktree; archive entries were copied only into absent paths. A complete HEAD-only checkout was **not** mistaken for the dirty integration baseline. Normal 341/341 and core 38/38 member/path hashes matched; their path intersection is zero. The exact Program patch passed `git apply --check`, applied once, and yielded the approved target hash. The resulting 379-entry [integration-source-manifest.tsv](integration-source-manifest.tsv) and [integration-source.tar.gz](integration-source.tar.gz) bind the assembled source; archive SHA-256 `965dd87470abcadc2be8e608bf255de6658c0d0f8ca8ba00f8eb9dc1a10b2bc1`, manifest SHA-256 `28f2c0e1ee2e6c6f60ee4e2984484e1a57ba5007291835069fcceacd8faf40e6`.

## Build and runtime

Fresh .NET 8 API build in that worktree: exit 0, 0 errors, 10 `NU1900` warnings because NuGet vulnerability feed was unreachable; [build.log](build.log). Built API DLL SHA-256 `455876aadc1b9c7d27b78f7931fd75fb120138c019b84bb8150eadf87f37e7ef`. Lane-owned MongoDB 8.0.18 replica set `rs190http` ran on 127.0.0.1:57590 with PRIMARY measured and DB `DitenSupplyChain_Mod0190_Test`; operational 27017 was untouched. The exact DLL was launched from the registered worktree on 127.0.0.1:57690 with actual JWT middleware, ephemeral test issuer/audience/key and valid tenant/LE/actor/permission claims. No token or secret is in the archive. Runtime provenance is in [runtime-provenance.tsv](runtime-provenance.tsv); the terminal session ID was 49682, but an OS PID was not captured, so a complete source→PID attestation is not claimed.

| HTTP case | Measured result | Scope |
|---|---:|---|
| `/health` | 200 | API started. |
| unauthenticated Sandop GET | 401 | JWT challenge. |
| valid JWT without Sandop create permission | 403 `FORBIDDEN` | Module-local authorization gate. |
| valid JWT with create permission and valid create body | **500** | `InvalidOperationException`: `IDemandFixtureReader` cannot be resolved while activating `CreateSandopPlanHandler`; see [api-error.txt](api-error.txt) and [http-cases.json](http-cases.json). |

The six Sandop collections (`sandop_plans`, `sandop_snapshots`, `sandop_sign_offs`, `sandop_receipts`, `sandop_audit`, `sandop_outbox`) all had count **0** after the 500, measured by independent `mongosh` query in the same test DB. This is not a successful mutation/rollback claim. The API was stopped by its own terminal session and its Mongo process was shut down; the shutdown client returned 1 on connection close and `lsof` showed no listener on 57590. The isolated credentials file remains outside the repository and was not archived.

## Findings, preservation and next gate

`IDemandFixtureReader` is required by the three mutation handlers but neither `AddApplication` nor the exact approved Program patch registers it. A separate, unapplied disposition candidate and exact owner decision are in [mvp6-mod0190-http-di-gap-01](../mvp6-mod0190-http-di-gap-01/SOP-22.md). Do not silently alter the approved Program target. Because create fails before the repository call, six-operation lifecycle, replay, rollback, concurrency, Pending outbox, restart, and independent VER remain OPEN. Direct-Mongo 19/19 CORE-REVER-01 remains inherited/content-bound; this run does not replace it. Live DEMAND/Workflow/Event Bus, gateway, E5/G5 and rollout remain excluded.

Only the registered isolated worktree received 341 normal source files, 38 Sandop source files, and the exact approved one-file Program patch. The common checkout received only this new audit record and the separate candidate report; existing dirty source/pack/contract/guard and other lanes were not intentionally modified. A mistaken first candidate `dotnet build` command targeted the common checkout and produced ignored build outputs, not source edits; the subsequent candidate build was correctly run in its disposable copy. No commit/push/stash. The source writer stopped at this blocker; no source writer remains active for A.
