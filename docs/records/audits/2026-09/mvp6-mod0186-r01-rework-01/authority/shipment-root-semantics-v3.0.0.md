# Shipment root semantics — SHIPMENT-BUNDLE 3.0.0 / wire v1

- ShipmentDetail.lifecycleCorrelationId is optional, nullable UUID.
- Upgraded Shipment producer explicitly emits the field on persisted ShipmentDetail reads.
- The value is authoritative persisted root only; consumers do not derive or backfill it.
- Tenant/LE scope, read permissions, wire contractVersion v1, lifecycle and non-root operations remain unchanged.
- Publication metadata proposal is SHIPMENT-BUNDLE 3.0.0 / wire v1. Prepared bytes have FROZEN metadata; actual publication requires the separate owner release decision.
- Prior 91d505 artifact was not reconstructed or claimed.
