# Authority — MVP6-PACK-APPLY-Q53-01

Copied boundaries; the decision record is controlling.

- Record: `docs/records/decisions/2026-09/mvp6-self-registration-patch05-signoff-owner-decision-01.md`, SHA-256 `626055e4429f49b8b87a1068de932cadf2b25926cbff28f6da28495d60284744`. Decided 2026-09-26T09:44+0300 by the owner (CEO Natig Yusubov), Option A — sign off. Queue: Q48 DONE (signed); apply Q53 (this lane); VER Q54 (Lane-3).
- Source text: `docs/roadmap/plans/mvp6-self-registration-patch05-rebase-01/SIGN-OFF-DECISION.md`, SHA-256 `0a5260592f0ab9aff3cd14900b87b9b32a074b87be2c06d545002f3fafc405d4` (rechecked).
- Approved exactly: apply `05-MOD-0186-self-registration-rebased.patch` (SHA-256 `7adf06ad844c85345895c95a2a0a418344b53b2dea61517f2343914ad4e9c6fd`) to `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md` only if before = `6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0` and after = `a762305ec789f0b2547d5205135a0ff70b64e83f4a63b839a985432a46d8a552`. On any mismatch it is rebased again and brought back; never applied by hand.
- Effect: adds §33 (self-registration for Reverse Logistics), pack text only.
- Not authorized: code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change; new permission key or ID; commit, push or stash by an agent. Code stays with the single integration owner after Q14/Q15, shipping the provider with the Returns UI and nav keys (D4). Recorded open gaps stay open.
- Lane limits: `GIT_OPTIONAL_LOCKS=0`; only `git apply --check` / `git apply` of this patch; no add, commit, push or stash; no other file edit.
