# MVP6-MOD0190-HTTP-DEV-01 — independent SOP §22 verification

**Verdict: PARTIAL source/evidence verification; HTTP acceptance FAIL.** This verifier did not write or repair product source, rerun the HTTP suite, or claim gateway/E5/G5 acceptance. The writer handoff's source transfer and Program composition are byte-exact, but authenticated Create, Capture, and SignOff fail with HTTP 500 because the composed service has no `IDemandFixtureReader` registration. The earlier 19/19 core result is historical; the writer's fresh targeted run is 18/19 FAIL and does not establish a new product defect by itself.

## Authority and inputs

The current user instruction authorizes the exact 38-file transfer and composition patch in `mvp6-mod0190-http-integration-prep-01/`. That preparation package's `AUTHORITY.md` was HELD; no separate earlier A decision record was found in the inspected repository. This verification does not turn a proposed decision text into an earlier owner approval. Registered writer checkout: `/private/tmp/mvp6-mod0190-http-integration-02`, detached at `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. It was inspected read-only; patch reconstruction used a separate disposable directory.

| Input/artifact | Independently measured SHA256 / result |
|---|---|
| Normal archive / manifest | `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21` / `92879d2098e5c50fb4c2862ee52060cbe8ba1aab2e038e77f513f49680e80f80`; 341/341 members match. |
| S&OP archive / transfer manifest | `09bfb801490e3a7aefdb6925b66c0187062f4f3f52a4efb0f0b1e70aa0fd2bcd` / `b55e7b2128df259604e4a318cc9194bfeff24610b1dba1790f333ef20d74a824`; 38/38 members match. |
| Composition patch | `bd972051be35971465b008d783afe9eabf529d90b3e12ccfe6369f2c9c12a074`; separate disposable `git apply --check` and apply both exited 0. |
| Program baseline → target | `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` → `0f6bf84e1c7ddff79868d32a23099e8934a33e3ac80ccecbe8b16f5e0a6c5cc2`; disposable output equals writer Program byte-for-byte. |
| Published YAML / annex | `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` / `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. |
| Final inventory / API DLL | `final-source-check.tsv` SHA256 `5ed657404f52f1fc3d164e60d319f25ef748d6f818f114cd71ae35bd12bf917c`; API DLL SHA256 `7ca1154360b076a39a6d9a028faa606835f5d88d46bf1d81d2fcf65848e6a9ae`. |

All 379 writer-checkout files match the final inventory's recorded actual hashes. Against the immutable input manifests, 340/341 normal files match; the sole changed normal file is authorized Program.cs. All 38 S&OP files match. The sets do not overlap. `git status --porcelain -uall` on the writer checkout had 224 entries, all included in those 379 paths, none staged and none under MOD-0192 CapacityPlans. This is a scope check at verification time, not a claim about unrelated main-checkout changes.

## Source → build → process → HTTP evidence

`build.exit=0`, `build.log` reports 0 errors and 14 NU1900 warnings caused by an inaccessible NuGet vulnerability feed. The API DLL on disk matches the writer's binary hash. `LAUNCH-TRANSCRIPT.md` and `raw-process-logs.tar.gz` bind that binary to isolated Mongo port 57190 and API port 55190, first PID 40693 and restart PID 42246. The raw logs confirm Development startup fails DI validation for Create/Capture/SignOff handlers and Production request-time activation throws `Unable to resolve service for type IDemandFixtureReader`. The writer used `DOTNET_ROLL_FORWARD=Major` because this host has no ASP.NET Core 8 runtime; execution under ASP.NET Core 10 is therefore a stated runtime limitation, not native .NET 8 acceptance. The unmodified process logs and all 26 listed writer artifacts pass `sha256sum -c SHA256SUMS`.

The nine recorded HTTP probes before restart and nine after show the same statuses: parser-level no-token 401; missing grant and claim/header tenant mismatch 403; three scoped GETs 404; Create/Capture/SignOff 500 with empty bodies. The published YAML `#/components/responses/Unauthenticated` explicitly permits a parser-level challenge before application context to omit the correlation header and envelope, so the empty 401 is **not** a contract violation. The authenticated POST 500s are direct functional failure; the raw API log ties each to missing fixture DI. The six isolated S&OP collection counts are zero. They do not prove transaction rollback because no successful mutation reached the repository.

The final direct-Mongo targeted TRX is 18 passed / 1 failed of 19. The failed committed write-concern uncertainty test expected 503 but got 201. Earlier discarded attempts include an unsupported switch, missing ASP.NET 8 testhost, and disabled failCommand; all are retained. The 201 needs a narrowly controlled driver/failpoint analysis before it is classified as a product defect. The prior CORE-REVER-01 19/19 must not be reported as freshly reproduced here.

## Acceptance boundary and next action

**Verified:** exact source transfer, Program target, archive/manifest integrity, same binary across restart, tested parser-level 401 and 403 branches, observed 404/500 statuses and DI root cause. **Unproven:** authenticated success for all six operations, persisted tenant/LE isolation, exact-key replay/conflict, concurrency and rollback, unknown-commit recovery, durable restart replay, Pending-only outbox, same-host functional regression, and native ASP.NET Core 8 execution. No gateway/JWT bypass or live DEMAND/Workflow claim is made.

The next rework must first obtain exact composition authority for bounded test-only `IDemandFixtureReader` registration and startup schema/index behavior, then produce a fresh source→binary→process→HTTP/DB chain. The 503-versus-201 fault case needs independent analysis. This verifier made no source, contract, pack, Program.cs, MOD-0192, guard, gateway or Git changes; the only new path is this independent audit report.
