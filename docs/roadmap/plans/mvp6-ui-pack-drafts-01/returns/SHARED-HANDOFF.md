# MOD-0186 Returns UI — shared-surface handoff (single integration owner, HELD)

CT appoints **one** integration owner for the items below. Neither the Returns UI writer nor any other lane writes
these files. The owner delivers exact paths + preimage hashes + diff against the target CT selects (G-TARGET). No
wildcard grant is implied. If the Claims UI is approved in the same wave, the same owner should do both modules in one change set (see README).

| Surface | Owner delivers | Acceptance |
|---|---|---|
| Gateway (`ocelot.json`) — **list only, not edited here** | Explicit forwarding Gateway 5000 → SupplyChain 5061 for: GET+POST `/api/shipment-bundle/returns`; POST `/api/shipment-bundle/returns/{returnId}/transition`; and confirm GET `/api/shipment-bundle/shipments/{shipmentId}` exists in the target (added by Shipment UI integration, or add it). OPTIONS as NET-001 requires. Header passthrough: Authorization, X-Correlation-Id, Idempotency-Key, X-Tenant-Id, X-Legal-Entity-Id | Only these operations reachable; `/returnsXYZ` is not matched (annex family routing); Shipment/Carrier/Loads/Claims routes byte-stable |
| Frontend `Program.cs` / DI | Only if the target needs registration for the new controller or adapter client; exact diff | No global auth/CORS/error change; JSON-adapter 401 behaviour (Shipment A03) preserved |
| Permission catalogue / page registration | Page `/SupplyChain/Returns` gated by `supplychain.returns.read`; actions bound to `.create`, `.transition`, `.authorize`, `.transit`, `.cancel`, `.receive`, `.disposition`, `.close` (annex D186-05 keys, unchanged) | Default-deny; exact-case keys; no automatic grant; direct action 403 |
| G-SHIPREAD | Decide how create/transition actors obtain `supplychain.shipments.read` (role design), or record a different published path | No grant widening by the UI; decision recorded before DEV |
| Navigation / Ctrl+K | One tenant nav entry for Returns under Supply Chain, gated by read. Module/page codes **reconciled with the registry by this owner**; no literal is proposed here | Entry absent without read; tenant route, no `/Platform` |
| Shared L10n | `Nav.Module.*` / `Nav.Page.*` and any genuinely missing shared toolbar/error keys, in all 7 languages | No module-specific text in SharedResource; no copied shared text in module resx |
| Personalization | Confirm the shared personalization client and its gateway path exist in the target | Save View / Reset / column visibility work; no localStorage substitute |
| Shipment producer root | Confirm the target's Shipment API emits `lifecycleCorrelationId` (Root R2 uptake) | RU-15 can run; null-root legacy shipments behave as 503 by design |
| Returns backend in target | The accepted isolated 46-path Returns WP and its Program.cs composition must be integrated into the chosen target (today the common checkout lacks 43 Returns paths, per the acceptance record) | Returns 78/78 regression green on the target before UI VER |

Protected throughout: `_LayoutTenantShell.cshtml`, shared partials, global JS/CSS, backend source, contracts, `.antigravity`.
Existing components are consumed as they are.
