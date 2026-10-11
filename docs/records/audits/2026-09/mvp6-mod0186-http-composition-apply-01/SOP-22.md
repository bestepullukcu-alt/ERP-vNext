# MVP6-MOD0186-HTTP-COMPOSITION-APPLY-01 — SOP §22

## Verdict

**PARTIAL / BLOCKED at integrated build.** The real owner decision was found and the approved Program.cs patch was applied byte-exactly in a separate registered integration checkout. The Program.cs target hash is exact and Claims composition is preserved. Integrated build cannot pass because the separately developed 46-path Returns core has not been authorized for delivery to this integration checkout. No core file was transferred under the Program.cs approval.

## Authority and exact artifacts

- Actual user decision: session `/Users/natig/.codex/sessions/2026/09/16/rollout-2026-09-16T22-10-27-01a0ab32-c980-7dd0-8872-e597eb94e6f9.jsonl`, timestamp `2026-09-21T07:53:17.219Z`; extracted message SHA-256 `0e67123747d46b7cfc89911e1e51e99faeba14256fc10e86b57502ba237f2e52`.
- Approved baseline: `a28cb1ab5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a`.
- Approved patch: `70b80f7920f0d216ccd22795328df750a761c949e438a55ede7876c57cbf6444`.
- Approved target: `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.
- Reconstructed applied diff and approved patch are byte-identical.

The decision authorizes only this Program.cs diff and explicitly excludes unauthorized transfer of Returns core files, business changes, rollout, commit and push.

## Checkout and dirty-input preservation

- Integration checkout: registered detached worktree `/private/tmp/mvp6-mod0186-http-integration-01` at `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Claims source lane: `/Users/natig/.codex/worktrees/claims-dev-start-01/ERP-vNext-recovery`.
- 491 dirty Claims input files were inventoried in `claims-source-dirty.sha256`; 490 non-Program inputs were copied byte-for-byte. Directory timestamp-only rsync differences were ignored; file content comparison was clean.
- Program.cs was set from the archived approved baseline rather than from the live Claims lane, because a concurrent writer had already changed the live file to the target hash before this task began.
- The concurrent Claims lane was observed at Program.cs `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`; this task did not write to that lane.

## Returns core disposition

The Returns DEV lane is internally complete and its delivery bytes are intact:

- source patch: `3bb73a8cd77071c0c63721e53f54e9792d155924e8c38ea41e233df78b223a98`;
- source manifest: `8d84d9eebe6f46a97cf22716960a0c5ec10fd37148951bd4ed523d7bc046a26f`;
- handoff archive: `2cdbd60a35e6602b540f8ead8b60dbb48c2f2343ae675d3ed4c1a3469207b66f`;
- all 46 manifest entries rehashed successfully in the Returns worktree.

Existing authority permits the 46-path implementation in its isolated Returns checkout. It does not authorize transferring those paths into this integration checkout. The Program.cs decision explicitly refuses to stand in for that delivery authority. The target therefore remains without Returns core.

## Composition preservation

The applied diff changes only Program.cs and adds the approved Returns usings, persistence/client registration, invalid-model branch, middleware and Shipment exclusion. It preserves the existing Claims registrations:

- `AddClaimPersistence()`;
- `IClaimReferenceReader` / `ClaimReferenceReader` HTTP client;
- Claims invalid-model response branch;
- `ClaimContextMiddleware` registration;
- Claims exclusion from `ShipmentContextMiddleware`.

No worker, permission, serializer, canonical, guard, gateway, Claims business source or other shared source change was introduced.

## Build and acceptance

- Baseline Claims-composed checkout: build PASS, 0 warnings / 0 errors, using existing restored assets copied into the disposable checkout. This is not a fresh restore claim.
- Approved Program.cs target: exact target hash PASS; build FAIL, 0 warnings / 4 errors.
- All four errors are missing Returns namespaces caused by absent core paths. No Claims registration error was reported.
- HTTP/JWT, real Shipment producer uptake, Kestrel restart and Returns regression acceptance were not run because the integrated source graph does not build.

## Required next action

The core owner must issue an exact delivery disposition that binds the existing 46-path `source-manifest.tsv` (or an exact replacement) to `/private/tmp/mvp6-mod0186-http-integration-01`. After that delivery is applied and all 46 hashes are rechecked, the existing Returns DEV lane may continue with:

1. clean integrated build;
2. real Kestrel/JWT parser and permission matrix;
3. authoritative Shipment producer/root uptake;
4. no-write rejection checks;
5. process restart/replay evidence;
6. relevant Claims/Loads/Shipment regressions and independent VER.

Until that exact core delivery is authorized, the integration gate remains **BLOCKED**. No runtime acceptance, rollout, commit, push or stash occurred.
