# MOD-0187 Claims UI — shared-surface handoff (single integration owner, HELD)

The same rules as the Returns handoff: one CT-appointed owner, exact paths + preimage hashes + diffs against the
selected target, no wildcard grant, and no shared edit by the UI writer. Recommended: the **same** owner does Returns and Claims in one change set.

| Surface | Owner delivers | Acceptance |
|---|---|---|
| Gateway (`ocelot.json`) — **list only, not edited here** | Gateway 5000 → SupplyChain 5061 for GET+POST `/api/shipment-bundle/claims` and POST `/api/shipment-bundle/claims/{claimId}/transition`; confirm GET `/api/shipment-bundle/shipments/{shipmentId}` exists in the target; OPTIONS per NET-001; passthrough of Authorization, X-Correlation-Id, Idempotency-Key | Only these operations; `/claimsXYZ` not matched (annex H06); other routes byte-stable |
| Frontend `Program.cs` / DI | Only if the target requires registration; exact diff | No global auth/CORS/error change; A03 401 JSON behaviour preserved |
| Permission catalogue / page registration | Page `/SupplyChain/Claims` gated by `supplychain.claims.read`; actions bound to `.create`, `.investigate`, `.decide`, `.settle` (annex D187-04 map, unchanged) | Default-deny; exact keys; no new close/withdraw key; direct action 403 |
| G-SHIPREAD | Same decision as Returns (`supplychain.shipments.read` for create/transition actors) | Decision recorded before DEV |
| Navigation / Ctrl+K | One tenant nav entry for Claims, gated by read; codes reconciled with the registry by this owner | Absent without read; tenant route |
| Shared L10n | Nav keys and genuinely missing shared keys, 7 languages | No module text in SharedResource |
| Personalization | Confirm shared client and path in target | Save View/Reset/colvis work |
| Shipment producer root | Confirm `lifecycleCorrelationId` emitted in the target | CU-18 runnable |
| Claims backend in target | Integrate the accepted isolated Claims WP (47 owned paths + separately approved `Program.cs` lease) into the target | Claims regression green on target before UI VER |
