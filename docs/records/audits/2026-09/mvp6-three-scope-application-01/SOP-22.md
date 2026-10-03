# MVP6 three-scope application — SOP §22

## Verdict

| Scope | Result | Acceptance boundary |
|---|---|---|
| A — single historical JSON guard activation | **PASS / ACTIVATED** | Exact guard authority only; production DocsPathGuard 39/39. |
| B — isolated 422-row integration baseline | **PASS / independent VER complete** | Bounded isolated integration evidence only; no CT/full-module/E5/G5 claim. |
| C — post-publication Capacity uptake | **PASS / independent VER complete** | Exact three-file isolated uptake only; no CT/full-module/E5/G5 claim. |

## A — guard activation

The approved payload `3bd20e2608ec62cd5c2da1fc60bc4419b6aefa594e0efc45f32b35df17ad8dfb` is bound by real decision SHA-256 `877c3878d9b38446fc437a384d5e61630ccbec601dc5dd025a536ea21aa20ae2`. Final authority SHA-256 is `6d3865f80707ff0e9ca7c9cf2e2343f34d2961be2916a3eceb37168d1b0a8167`. It retains two canonical targets and 34 previous seals and appends only the authorized JSON seal. Policy, schema and reader stayed unchanged.

Disposable production-mode and actual checkout DocsPathGuard each passed 39/39. Full record: `docs/records/audits/2026-09/mvp6-capacity-guard-single-activation-01/SOP-22.md`.

## Authorized v3 publication prerequisite for C

The prior conditional publication authority became executable after A passed. Patch `064827b512586bb17813a8e3703cc186f4a94295c590329674897ba31327aa23` published:

- canonical YAML `5213b5353267ad4c25d150975d6f38422bced72f678c02271b0cccf6e768adab`;
- v3 annex `f9d9d5553d70e1c54af3e03924600f4c8f426de50355ae861a4d3d5cef21ee64`.

The v2 annex remained `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. Disposable and actual post-publication guard runs passed 39/39. Full handoff: `docs/records/audits/2026-09/mvp6-capacity-duplicate-publication-exec-01/SOP-22.md`.

## B — isolated integration baseline

Registered detached checkout: `/private/tmp/mvp6-integration-baseline-422-01`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. The exact 379+43 transfer had zero intersection and produced the required 422-row, 74,155-byte manifest SHA-256 `cdc6228aa3d215f34f2f2f9d5b784cf57ac20b6eb8f2c2f26bdcace9dcd4d76c`.

Writer native .NET 8 evidence: build 0/0; Capacity 36/36; S&OP 19/19; cross-module seams 128/128; composed Capacity/S&OP HTTP, JWT, persistence, replay and restart PASS. Duplicate-name uptake was excluded and Claims retained the normal NoOp binding.

The first independent report correctly remained PARTIAL because its Mongo port/name and foreign-scope setup did not reach controlling behavior. A separate narrow recovery changed no source: exact `rsmod192@57192` produced Capacity 36/36, and matching foreign JWT plus tenant/LE headers produced 404 `UNKNOWN_SANDOP_PLAN`. The successor verifier closed both evidence gaps. Report SHA-256 `22f84efe507a475a37e979dca5f095c364c2c8b0fbc3b8f6e78bf248e11658c7`.

## C — Capacity v3 uptake

Registered isolated checkout: `/private/tmp/mvp6-mod0192-capacity-uptake-01`, same base HEAD. Patch SHA-256 `f4fc82b44eb237d1dbc8bdd9c0d9633c948ca4e33cd2dcff91cd65f653780c8f` applied only after the v3 publication handoff and exact source preimages matched. The successor source manifest contains 43 rows: three `UPTAKE`, 40 `UNCHANGED`; SHA-256 `1fbfa066796eac0ea7cdee1656edf755248f36dcd29495e0c49d60d56567cc94`.

The production delta is limited to the `CapacityContractError.cs` message mapping plus its two authorized tests. Repository, executor, schema, `Program.cs`, X01 and X07 inputs remained unchanged. Independent native .NET 8 VER: rebuild 0/0, targeted 3/3 and CapacityPlans 34/34 PASS. Report SHA-256 `ea7d99be7d1dc6c9d30291dd8c76e0ce1711a8409e5df95ec0e7bdae00aa9072`.

## Evidence retention and cleanup

The five source evidence directories from the isolated checkouts/verifiers are archived beside this report and hash-bound in `SHA256SUMS`. Writer and verifier API/Mongo processes were stopped; checked ports were clear. The common checkout did not receive B or C source transfers. No gateway, UI, shared permission, live producer, rollout, migration, E5/G5, commit, push or stash operation occurred.
