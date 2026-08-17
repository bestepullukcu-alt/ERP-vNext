# Talent Ecosystem Platform Domain

Governance-only bootstrap for native Talent Ecosystem Platform delivery.

This domain exists to hold module packs, ownership decisions, and execution
contracts for TEP modules after DCP-008 canonicalization, privacy/legal, and EA
identity decisions. It does not create a production runtime service, API,
frontend, gateway route, database collection, or module implementation.

## Bootstrap Decision

| Decision | Value |
|---|---|
| Domain slug | `talent-ecosystem-platform` |
| Branch short code | `tep` |
| Runtime service candidate | `Diten.TalentEcosystemService` |
| Bootstrap type | Governance-only |
| Source roadmap | `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md` |

## Scope

Native TEP ownership is intentionally separate from
`platform-shared-services` and from native HR/HCM. TEP consumes HCM foundation
contracts and PSS substrate references; it must not build candidate/talent
identity directly on raw HRIS data or on the PSS person reference projection
without HCM/consent contracts.

## Current Blockers

- R2/R4 native TEP IDs listed in DCP-008 are absent from Blueprint and require
  `CAND-CAP REQUIRED — EA DECISION`.
- Phase R2 depends on R1 HCM foundation contracts.
- Privacy/legal policy decisions are required before reference exchange,
  consent, reputation, risk, restricted integrity, benchmarking, and talent
  mobility capabilities are prepared.
- Runtime service ownership remains candidate-only; no
  `services/Diten.TalentEcosystemService/**` scaffold exists or is authorized
  by this bootstrap.

## First Module-Pack Candidates

Prepare only after EA canonicalization / CAND-CAP decisions and HCM foundation
contract readiness:

1. Talent Ecosystem Platform Shell.
2. Consent, Visibility & Access Policy.
3. Association Membership & Member Company Registry.
4. Industry Candidate Identity & Talent Profile.

The Excel IDs must not be used as runtime identities until EA resolves the
DCP-002 findings.
