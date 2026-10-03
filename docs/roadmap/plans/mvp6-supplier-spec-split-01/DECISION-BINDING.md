# MVP6-SUPPLIER-SPEC-SPLIT-01 — policy decision binding

## Controlling decision

On 2026-09-23, in the user message dispatching `MVP6-SUPPLIER-SPEC-SPLIT-01`, the actual user directed the orchestrator to bind the “sonraki spec hazırlığının policy hedefi” selection for all A options in SS-01…SS-09 and all acceptance rows AC-01…AC-27 from:

`docs/roadmap/plans/mvp6-supplier-exact-policy-01/DECISION-PACK.md`

The controlling decision-pack SHA-256 is `ded979f7c411c7f7364eb9b31ed94960bfd1d091e5002dfc11e4bf05da64183b`. Its acceptance companion SHA-256 is `5721cec64437c6af61962e652a501dcfb2a456e1762287dc7bc14b2b7b9f8e59`.

The selection binds these rows as **specification targets only**:

| Policy | Selected target | Acceptance binding |
|---|---|---|
| SS-01 | A — proposed SCE/service placement, with durable governance reconciliation still open | AC-01 |
| SS-02 | A — existing FROZEN SUPPLIER v1 as the identity/status base | AC-02 |
| SS-03 | A — single-supplier `getSupplier`, explicit eligibility/status rules and fail-closed dependency behavior | AC-03…AC-06 |
| SS-04 | A — future single-writer versioned successor review; frozen bytes remain unchanged here | AC-07 |
| SS-05 | A — security-owned authoritative 1-active-binding model and revocation fencing | AC-08…AC-12 |
| SS-06 | A — actor-isolated receipt scope, exact fingerprint and precedence | AC-13…AC-17 |
| SS-07 | A — `supplier-score-policy/1`, decimal/rounding/revision rules | AC-18…AC-22 |
| SS-08 | A — external taxonomy authority with MOD-0147-owned risk instances and linear lifecycle | AC-23…AC-26 |
| SS-09 | A — future test-only fixture specification; simulated evidence only | AC-27 |

## Authority boundary

This decision does not constitute concurrence by MOD-0140, Platform auth/security, Metric Registry, Risk Register, strict consumers, Enterprise/domain ownership or a contract publication owner. It does not approve an endpoint, claim carrier, producer protocol, contract version, fixture implementation, pack promotion or runtime work.

MOD-0147 and MOD-0148 remain `draft`. Domain config, DCP-009, registry, canonical contracts and runtime source remain unchanged.

## Writing boundary

- MOD-0147 specification output owner: `docs/roadmap/plans/mvp6-mod0147-spec-split-01/`.
- MOD-0148 specification output owner: `docs/roadmap/plans/mvp6-mod0148-spec-split-01/`.
- Shared Supplier seam and concurrence coordinator: this directory only.

Neither module lane may create a Supplier contract amendment, choose the common contract version, name an unapproved producer endpoint/claim, or create a competing shared seam design.
