# MVP6-MOD0190-HTTP-INTEGRATION-PREP-01 — SOP §22

**Agent verdict: PREPARED / real integration HELD.** Exact 38-file transfer and one-file composition candidates compile together in a disposable, hash-bound baseline. No real shared composition or source transfer was applied. This is E1/E2 preparation, not HTTP/JWT acceptance or CT acceptance.

## Branch, authority and scope

Common repository: `feature/mvp6-logistics`, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, initial dirty inventory 1677 rows, staged 0; other lanes' dirty files preserved. The registered MOD-0190 core checkout is detached at the same HEAD. Promoted pack SHA-256 `6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983`, published SANDOP YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`, annex `eb1df1383e637c744179abe4cccd19738aadf41896a9311bda`.

CORE-REVER-01 independently verified rework patch `46cad9ff04bc938cda177ba5125f27394739149a29a1c7a21a4b97165e97be3b`, 38-file manifest `b55e7b2128df259604e4a318cc9194bfeff24610b1dba1790f333ef20d74a824` and source archive `09bfb801490e3a7aefdb6925b66c0187062f4f3f52a4efb0f0b1e70aa0fd2bcd`; its fresh 19/19 direct-Mongo test result is inherited, not rerun here. Current source archive and manifest again matched **38/38**. The transfer manifest is a byte copy of that final manifest. No target path overlaps the 341-entry normal integration source archive.

## Target integration baseline and composition

No registered current integration checkout was altered. The exact target *candidate* is a new disposable source snapshot from `mvp6-mod0187-normal-baseline-integration-01/normal-source.tar.gz` SHA-256 `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21`, manifest `92879d2098e5c50fb4c2862ee52060cbe8ba1aab2e038e77f513f49680e80f80`. All **341/341** baseline entries were verified before transfer. It includes accepted Returns R01 plus Claims R14/R21/malformed integration source and Shipment/Carrier/Loads composition. Its Program.cs baseline is `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1`. For comparison, the common checkout Program.cs is `7fdb5ef0…` and the older Claims worktree Program.cs is `a72a05a5…`; neither is silently treated as this target.

The candidate [composition.patch](composition.patch) SHA-256 `bd972051be35971465b008d783afe9eabf529d90b3e12ccfe6369f2c9c12a074` produces Program.cs `0f6bf84e1c7ddff79868d32a23099e8934a33e3ac80ccecbe8b16f5e0a6c5cc2`. It adds `AddSandopPersistence()` and excludes only `/api/supply-chain/sandop-plans` from Shipment context middleware. The Sandop controller already invokes its module-local context gate and permission lookup; no new middleware/DI/client registration is necessary for this bounded fixture-only core. Existing Claims, Returns, Shipment, Carrier and Loads registration lines remain byte-identical. Shared permission registry and gateway need no assumed diff for direct-service verification; Gateway remains a separate E5 surface.

## Checks and evidence

- Normal archive manifest: 341/341; final core manifest: 38/38; preservation after candidate build: **340/340** normal non-Program entries and **38/38** core entries.
- `git apply --check` and `git apply` to a separately extracted baseline Program.cs: exit 0; result SHA-256 equals target.
- Disposable `dotnet build services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj -c Debug -v quiet -m:1 -p:UseSharedCompilation=false --ignore-failed-sources`: exit 0, 0 errors, 10 NU1900 vulnerability-feed warnings due unavailable NuGet index. Raw [disposable-build.log](disposable-build.log). Resulting disposable API DLL SHA-256 `3ec00f8829fdac1d7ff7c189aefddd0b6a367e4002f4c1559002ce46e78ea82f` is a build artifact only; no HTTP process was started.
- [transfer-manifest.tsv](transfer-manifest.tsv), [composition-hashes.tsv](composition-hashes.tsv) and `SHA256SUMS` bind exact inputs and outputs. Workspace paths are recorded for reproducibility; the durable authority is the existing archives and this package.

## Acceptance and remaining gates

HTTP DEV must use an approved, registered integration checkout with baseline hash matching the candidate. Plan: DB-010 isolated replica set and unique port, valid ephemeral JWT through real middleware; six operation contract and lifecycle tests; 401/403, tenant/LE, exact-key replay/conflict, current/original correlation; transaction rollback and unknown-commit, Pending-only outbox and same-binary process restart; Claims/Returns/Shipment regressions. Execute DEV then independent VER. No real DEMAND version/checksum producer, Workflow HTTP, Event Bus publisher, gateway E5, rollout or G5 is implied. Historical SupplyChain 147/152 and architecture 15/18 remain non-PASS.

**Blocker:** The earlier 2026-09-22 core authorization explicitly excludes Program.cs/shared composition. No exact 38-path transfer or this patch application authority was found. [AUTHORITY.md](AUTHORITY.md) provides one concrete two-part owner decision. Until granted, both prompts remain HELD; no real integration checkout was written.

Changed files in this task: this new audit directory only (`SOP-22.md`, `AUTHORITY.md`, `composition.patch`, `transfer-manifest.tsv`, `composition-hashes.tsv`, `disposable-build.log`, two HELD prompts, workspace pointers and `SHA256SUMS`). No source, pack, contract, guard, registry, gateway or Git mutation. No commit/push/stash.
