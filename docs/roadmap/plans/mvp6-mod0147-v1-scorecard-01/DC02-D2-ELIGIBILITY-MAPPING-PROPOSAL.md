# DC-02 D2 — supplier eligibility mapping for MOD-0147 / MOD-0148 v1 — proposal

DURUM: ÖNERİ — karar değil. D2 ortak karardır (MVP-6 sahibi Natig + MOD-0140 sahibi Cihan, XMC-001). Kod izni vermez.
Yazan: MVP-6 CT, 2026-10-23.

## Source (read, not merged)

`origin/feature/mvp2/supplier-tenant-01` @ `1c0fa1d42` (Cihan, 2026-10-23, CT-APR-02 / CT-APR-01 pending):
`PACK-CHANGE-PROPOSAL.md` §1.2 (relationship `Status` {Active, OnHold, Blocked, Inactive}, `OnboardingStatus`,
`PurchasingEnabled`), §1.3 (reads go through the relationship; no relationship → 404), §1.4 (consumers ask one question: "is
there an **active relationship** for (tenant, company, supplier code)?" — stated there as DC-02's "positive Tenant+LE
eligibility"), the contract table (`supplier-performance` 1.0.0: shape unchanged, eligibility = active relationship), K-7
(blocked / on-hold suppliers get no **new transaction**; they stay visible and manageable) and the 2026-10-23 additions.
Supplier identity: `SupplierId` code (`SUP-xxxxxxxx`), tenant-wide; the frozen `supplier-performance` wire already carries
`supplierId` as an opaque string, so no wire change.

## What D2 has to fix for v1

MOD-0148 §0.0 leaves "which supplier–LE statuses may submit" and the `accessStatus` mapping (`ACTIVE` / `SUSPENDED` /
`PENDING`) to D2; MOD-0147 §0.0 gates `createSupplierEvaluation` on "eligibility per DC-02 D2".

## Proposed mapping (CT proposal)

| Relationship in the caller's LE | MOD-0148 `accessStatus` | MOD-0148 create / submit | MOD-0147 create evaluation / publish |
|---|---|---|---|
| none (or soft-deleted) | — | status / list / create → 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED`; get / submit → 404 (same as an unmapped caller, §0.0) | 422 supplier not eligible (no relationship in this LE) |
| `Active`, onboarding approved, `PurchasingEnabled` | `ACTIVE` | allowed | allowed |
| `Active`, onboarding not yet approved **or** `PurchasingEnabled = false` | `PENDING` | status, own list / get allowed; create / submit → 422 (not yet eligible) | allowed (performance of a supplier in onboarding can be recorded) — **question Q2** |
| `OnHold` or `Blocked` | `SUSPENDED` | status, own list / get allowed (read only); create / submit → 403 (AC-05) | **allowed** — evaluating a blocked supplier is a record of past performance, not a new purchase (K-7 forbids new *transactions*) — **question Q1** |
| `Inactive` | `SUSPENDED` | as `OnHold` | allowed for periods in which the relationship was active — **question Q1** |

Read path: the SupplyChain service asks MOD-0140 1.3.0's eligibility question through the Gateway with the caller's token
(XMC-001 read only), never its own copy; MOD-0140 unreachable → 503 `DEPENDENCY_UNAVAILABLE`, no state change. For the portal
the supplier id comes from the DC-03 binding, never from the request.

## Questions for the owners

1. **Q1 (Natig):** may MOD-0147 evaluate and publish a scorecard for an `OnHold` / `Blocked` / `Inactive` supplier? Proposal:
   yes — K-7 blocks new purchasing, not the record of how a supplier performed.
2. **Q2 (Natig):** may MOD-0147 evaluate a supplier whose onboarding is not approved yet? Proposal: yes (rare; no harm).
3. **Q3 (Cihan):** does 1.3.0's eligibility answer return the relationship `Status`, `OnboardingStatus` and
   `PurchasingEnabled` (needed for `PENDING` vs `SUSPENDED`), or only active yes/no? If only yes/no, v1 maps `false` →
   `SUSPENDED` and has no `PENDING`.
4. **Q4 (Cihan):** permission key for the read (`procurement.suppliers.read` or a narrower `…eligibility.read`), and its
   version.

After the answers, the mapping goes into MOD-0147 §0.0 and MOD-0148 §0.0 as one revision (with the MOD-0148 round-5 low
conditions), which needs a new CT-APR-02 round.
