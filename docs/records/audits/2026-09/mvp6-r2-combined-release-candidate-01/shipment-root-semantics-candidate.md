# Shipment root candidate R2 semantics

- ShipmentDetail.lifecycleCorrelationId is optional, nullable UUID.
- Upgraded Shipment producer explicitly emits the field on persisted ShipmentDetail reads.
- The value is authoritative persisted root only; consumers do not derive or backfill it.
- Tenant/LE scope, read permissions, wire contractVersion v1, lifecycle and non-root operations remain unchanged.
- Candidate metadata is R2 only; contract version remains 2.0.0 and status is CANDIDATE.
- Prior 91d505 artifact was not reconstructed or claimed.
