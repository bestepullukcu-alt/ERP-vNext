# Release Backlog: R1 PPM MVP

## Purpose
This document lists all modules, features, and bug fixes scope-locked for the R1 PPM MVP release.

## Scope
The complete feature and module backlog scoped specifically for the R1 release candidate.

## Not the source for
- Canonical Module ID Registry (use Module ID Registry).
- General wave roadmaps (use Master Development Plan).

## Current status

| Scope | Status | Completion boundary |
|---|---|---|
| MOD-0117 Phase 2A — Context & Referenceability Core | `review` | Scoped Phase 2A evidence only; not whole-module completion |
| MOD-0117 Phase 2B Gate L — Local Investment & Planned Benefit Core | governance reconciled; runtime pending | Portfolio → InvestmentCase → BenefitCommitment CRUD, neutral lifecycle, tenant isolation, permissions, concurrency, transactional local audit intent, backend/frontend/browser evidence |
| MOD-0117 Phase 2B Gate I — Integrated 1.3 Business Flow | `blocked/deferred dependencies` | Approved typed/versioned MOD-0007 decision, MOD-0023 workflow/approval, MOD-0136 budget, MOD-0138 scenario and MOD-0072 actual-outcome contracts plus auditable integrated flow |
| Whole MOD-0117 product | `not done` | Gate L or Gate I does not complete Blueprint-wide MOD-0117 scope |

Gate L completion wording is exactly: **“1.3 PPM-owned local slice complete; external integration Gate I
remains open.”** Only Gate I completion may use **“1.3 integrated business flow complete.”**

### Gate L product records

- `InvestmentCase`: exactly one immutable Portfolio parent; lifecycle `Draft → UnderAnalysis → Closed` and
  `Draft|UnderAnalysis → Withdrawn`; terminal states imply neither approval nor funding.
- `BenefitCommitment`: exactly one immutable InvestmentCase parent and no second PortfolioId; lifecycle
  `Draft → Planned → Active → Closed` and `Draft|Planned|Active → Cancelled`; no actual/realized outcome truth.
- Both surfaces use Golden Slim, DataTable v2 and seven-language localization. Routes are
  `/ppm/investment-cases` and `/ppm/benefit-commitments`; BenefitCommitment selectors show `Code — Title`.
- Permissions are the exact `.read/.create/.update/.change-lifecycle` families under
  `ppm.investment-cases.*` and `ppm.benefit-commitments.*`; no `.view`, `.delete`, wildcard, alias or uppercase
  key is allowed.

## Source / migration note
Target placeholder for release backlog migration.

## Owner / update rule
- Owner: Product Owner / PMO
- Update Rule: Scoped and locked at the start of release planning.
