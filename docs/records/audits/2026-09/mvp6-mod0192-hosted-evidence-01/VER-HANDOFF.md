# MVP6-MOD0192-HOSTED-EVIDENCE-VER-01

Role: independent verification; read-only against the writer archive and product source.

## Exact input

Consume the sealed writer package at `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-01/`. Verify every entry in `SHA256SUMS` before using any result. Use the exact authority and input bindings in `OWNER-DECISION.md` and `INPUT-MANIFEST.tsv`.

The source candidate is `/private/tmp/mvp6-mod0192-hosted-evidence-01-20260923`, bound by `COMBINED-SOURCE-MANIFEST.tsv` and `BINARY-MANIFEST.tsv`. Treat it as read-only. If an executable rerun is needed, copy it to a new unique disposable directory and use different API/Mongo ports, replica-set name, database name, and data path.

## Required verification

1. Verify 379 MOD-0190 baseline paths, 43 Capacity paths, zero overlap, 422 combined paths, exact `Program.cs` baseline/patch/target, and the one authorized test-only delta. Confirm no second Program writer or MOD-0190 drift.
2. Verify the fresh build record and source→binary→five-process chain for API DLL `f43af77c5c7ffeb5ea438daac4801b61cdf89e0e39f6d5f9dfb40576e3e8ee8f`.
3. Independently inspect the TRX and raw counters. Confirm that each of broad event, exact event, broad audit, exact audit, and active-slot count reached a separate intended fault boundary with one entry and one injected failure. Do not add an `_id` equality requirement.
4. Verify the raw HTTP/JWT/RBAC, tenant/LE, replay/conflict, response-loss receipt recovery, kill/restart, lease-expiry reclaim, stale-worker fence rejection, lease race, terminal event/audit/slot invariants, and Pending-only no-publisher observations.
5. Keep the initial no-.NET-8 aborted launch distinct from the later 36/36 roll-forward run. Do not turn the two records into a single run.
6. Confirm X01 remained closed without product changes and that duplicate-name remains an unpublished contract/release gate.
7. Check cleanup/no-change evidence. Do not edit product source, `Program.cs`, canonical/guard, gateway, pack, this writer archive, or git state.

## Output

Write a separate permanent SOP §22 report, exact verifier manifest, and raw verification transcript under `docs/records/audits/2026-09/mvp6-mod0192-hosted-evidence-ver-01/`. Verdict only the exact hosted-evidence scope. Report any mismatch as exact RED without repairing source or broadening authority.
