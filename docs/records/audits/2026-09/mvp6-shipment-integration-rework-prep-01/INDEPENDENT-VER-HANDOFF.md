# MVP6-SHIPMENT-INTEGRATION-REWORK-VER-01 — HELD

Role: verifier who did not prepare or apply the successor.

Start only after an exact owner decision authorizes the hashes in this package and the integration writer reports complete.

1. Verify `ARTIFACTS.sha256`, the accepted BC archive provenance, and the writer's final source manifest.
2. Starting from the recorded Shipment target, independently apply the 258-file closure and the exact two-file successor patch. Stop on any non-ABSENT transfer path or patch preimage mismatch.
3. Prove 161 accepted BC paths remain byte-identical, the three successor paths are preserved, and all 28 Shipment UI files match `UI-PRESERVATION.tsv`.
4. Fresh-build SupplyChain, Gateway and CRM with native .NET 8. Bind source, binary and command outputs.
5. Run the focused gateway route suite and independently parse every `/api/crm/**` and `/api/shipment-bundle/**` route. Require CRM=5065 and SupplyChain=5061. Reject global port replacement.
6. Confirm the original 23 CS0234 errors are closed by transferred accepted sources, not by removing registrations or changing Program.cs.
7. Report PASS/FAIL/PARTIAL under SOP §22. A technical PASS is not runtime/browser acceptance, rollout, E5/G5 or full-module acceptance.

Protected: Auth, canonical/guard, UI product source beyond verification, Git state and rollout.
