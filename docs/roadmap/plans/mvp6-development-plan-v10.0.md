# MVP6 development plan v10.0 — 2026-10-20 (updated with Ali's answers, same day; Natig's answers of 2026-10-21)

> **2026-10-21 answers — answered by Natig (management).** Recorded in rows A1, B, C1–C5, D6 and §3a below. The D9
> question (who builds DC-03) goes to **Ali**; that row stays open for Ali.

Supersedes v9.9 (2026-10-19). Measured on `origin/main` `9d44e8a1f` (after #173). Every state names the PR, commit
or record that moved it. Accounts: **Ali = `GMG-ERP`** (product owner), **Beste = `Bestepul` / `bestepullukcu-alt`**
(senior developer, reviewer/merger), **`gmg-10`** = the SCE / Control Tower author account.

## 1. What changed since v9.9

**The MVP-6 finish line is reached.** PR #158 merged to main (`c07aa7746`, 2026-10-19): shipment bundle +
S&OP/Capacity + DemandPlanning (UI, fixture-proven). Everything after it is follow-up.

| PR | What | Merge |
|---|---|---|
| #158 | MVP-6 (finish line) | `c07aa7746` |
| #161 | v9.9 step 0 — `sce-mvp6` task-source row confirmed | `35d2a7501` |
| #162 | Q358 retracted — `shipment-tracking-pod` out of `AdminModules` (C-13) | `ef834ddf2` |
| #163 | MOD-0147 / MOD-0148 pack readiness (packs stay draft) | `1ff11f922` |
| #164 | Q358 clean-up runner (existing tenants' System Shipment grants; Off by default) + soft-delete reactivation | `365f260a9` |
| #167 | G7 removed — `sop-workflow-signoffs`, `capacity-planning` out of `AdminModules`; runner made definition-driven, G7 clean-up | `d6c7b9e05` |
| #168 | CT-APR-01 first approval rows MOD-0147 / MOD-0148 (HIGH) — `pack_approval.py check` exit 0, "onaylı — HIGH" | `7c5848aa4` |
| #173 | Q358 / G7 retraction runbook (`docs/guides/operations/auth-baseline-grant-retraction-runbook.md`); DCP-009 SupplyChain port 5061 → 5066 | `9d44e8a1f` |

Closed without merge: **#171 (DC-02)** and **#172 (DC-03)** — they recorded the decisions as made before their named
owners decided. Both are kept as **drafts only** on `docs/sce/dc02-supplier-eligibility-draft-01` (`9baf67061`) and
`docs/sce/dc03-supplier-actor-binding-draft-01` (`eaa7bdd06`), each with `DURUM: TASLAK — karar verilmedi`.

Also found: the gateway signing-key fail-fast (v9.9 step 8) was already on main — `20f4e2b49` (2026-10-18):
outside Development a missing or short (< 32) `Observability:Correlation:SigningKey` stops the gateway; only the
per-environment configuration remains. CT-APR-02 (independent review before approval) is now on main
(`.antigravity/rules/module-pack-standard.md`, #170).

## 2. Measured state (`origin/main`)

| | value | source |
|---|---|---|
| Tenant Admin baseline | `AdminModules` = access-governance, legal-entity, crm-account, crm-contact, esign — **no temporary widening left** | `DefaultRolePermissionTemplate.cs:66` |
| Q358 existing-tenant grants | runner on main, **not yet run in production** | #164, runbook #173 |
| G7 existing-tenant grants | production has none (S&OP/Capacity never composed in Production); non-prod `DemandFixtures` hosts may | #167 evidence |
| MOD-0147 / MOD-0148 | packs `draft`; CT-APR-01 HIGH approved (draft version) | #168 |
| CI reds on main (`673995c15` run) | phase1 Arch 141/3 (BL-615, registered); platform-app 3 (MOD-0022 `LoginOnlyEndpointGuard` ×2, PPM `PpmAuditRetentionPolicySeedMongoTests` — issue #175); planning-tests 2 (`MrpLiveCalculationTests`, date bomb — issue #174); auth-app 1 (`OperationalSingleInstanceLockTests`, once; green on PR runs — not verified as flaky); auth-acceptance-host cancelled | job logs |
| Q358 / G7 on mongod 7.0.28 | CI auth-app Linux 7.0.28 2923/0; local macOS 7.0.28 84/84 | `mvp6-g7-grant-removal-01/REPORT.md` |

## 3. Sequence (one numbering, in order)

**A. Production clean-up — no dependency left**

| # | step | owner | state |
|---|---|---|---|
| A1 | Run Q358 in production: `Q358Retraction__Mode=DryRun` → read the log (`unknown` 0, `failed` 0) → `Apply` → remove the setting. Runbook on main | **Ali** (deploys) | **waiting — Ali 2026-10-20:** run it when production authorizations are updated; production is not in active use yet. 2026-10-21: Natig will notify when production authorizations are updated |
| A2 | G7 clean-up on non-prod hosts with `DemandFixtures` enabled (same runbook, `G7Retraction`) | — | **closed — Ali 2026-10-20:** not needed on other servers |
| A3 | Mark `mvp6-0190-0192-g7-role-grants-owner-decision-01.md` superseded (still says "IN EFFECT") | Ali | open |
| A4 | Fix the account mapping in `mvp6-g7-grant-removal-01/REPORT.md` ("Ali (`bestepullukcu-alt`)" → Ali = `GMG-ERP`) — small docs PR | CT prepares, Beste merges | open |

**B. Main reds (other owners; MVP-6 does not touch them, XMC-001)**

| # | step | owner | state |
|---|---|---|---|
| B1 | `MrpLiveCalculationTests` ×2 — fixture fixes the first demand week at 2026-10-19 and stock `AsOf` at `UtcNow`; red every day since 2026-10-20 | MOD-0189 (`ElikPenco`) | issue **#174** |
| B2 | `PpmAuditRetentionPolicySeedMongoTests` — CI `platform-app` row has no test-owned mongod | PSS / Platform | issue **#175** |
| B3 | MOD-0022 `LoginOnlyEndpointGuardTests` ×2 | MVP-1 lane | owner-ruled known red (v9.9 1a) |
| B4 | BL-615 Arch ×3 (`DocsPathGuard`, `AuditTrailStandard` ×2) | joint with CRM | registered known failures |
| B5 | main auth-app red in 5 of the last 6 runs, a **different test each run** (`ProductIdentityEntitlementReconciliationMongoTests`, `AnonymousDoorLimitTests`, `OperationalSingleInstanceLockTests`), already before #164; lock test 30/30 locally (macOS) — flaky on Linux CI, not Q358/G7 | Auth owner | measured 2026-10-20; issue to file |

2026-10-21 (B1, B2, B5): fixes are still in progress; if they are not resolved they are handed to their owners as work items.

**C. Supplier work (MOD-0147 / MOD-0148) — decisions before any code**

| # | step | owner | state |
|---|---|---|---|
| C1 | MOD-0140 system of record | **Ali** | **answered 2026-10-20: Procurement, tenant-based** (each tenant its own suppliers). Open: today Procurement scopes suppliers per Tenant **+ LE** (`SupplierRepository.cs:141-148`) — confirm whether a supplier stays per LE or is shared across a tenant's LEs; D1 is written for per-LE. **Answered 2026-10-21:** one supplier master per tenant, shared by all its LEs; approval status, payment terms and the purchasing relationship are kept **per LE** (no duplicate firm records; supplier approval stays per company/site). Consequence: today's Procurement key (Tenant + LE, `SupplierRepository.cs:141-148`) does not match — a Procurement change (other owner, XMC-001) and a DC-02 D1 rewrite are needed |
| C2 | Procurement takes `X-Legal-Entity-Id` without validating it against the tenant (`TenantResolutionMiddleware.cs:77-100`). Fix recommended 2026-10-20: when the LE comes only from the header, check with MDM that it is the tenant's and active (as SupplyChain's `MdmLegalEntityScopeValidator`) → 404 / MDM down 503, zero writes; claim/header conflict stays 400; user↔LE rights are MOD-0018-FU21 (unmerged) | Ali forwards to the Procurement developer | recommendation sent; **2026-10-21: Natig forwards it to the Procurement developer** |
| C3 | **DC-02 decisions** (draft `docs/sce/dc02-supplier-eligibility-draft-01`): D1, D3, D4, D5 — MOD-0147/0148 owner (SCE); **D2 joint with the MOD-0140 Procurement owner**; D6 — management | named owners | **D1 answered 2026-10-21** (see C1: tenant-wide master, per-LE approval/terms/relationship) — the draft's per-LE D1 must be rewritten; D2–D6 open |
| C4 | **DC-03 decisions** (draft `docs/sce/dc03-supplier-actor-binding-draft-01`). Ali's first proposal 2026-10-20: **one identity, separate door** (same Auth, separate portal login; `supplier_user` cannot enter the company app and vice versa; no separate user DB). Research done (main `9d44e8a1f`); proposals to decide with Ali: separate portal subdomain (host-only cookies separate the sessions), MFA required for supplier accounts (needs per-account-kind enforcement; email code only today), no same-email company+supplier account in one tenant (unique `{Email, TenantId}`), gateway allow-list `supplier_user` → `/api/supplier-performance/portal/*` + own session endpoints only, before going public: `TrustedProxies`, gateway rate limit (MOD-0032), shared limiter store; D9 owner/timing | Ali + Auth/Platform owner | **Approved 2026-10-21:** separate portal subdomain; mandatory two-step verification for suppliers; no two accounts with the same email in one tenant; gateway allow-list for `supplier_user`; the portal is **not exposed to the internet** until `TrustedProxies`, gateway rate limit and shared limiter store are all in place. **D9 open — for Ali:** who builds it (Ali or Beste). Proposed timing: record the decision now, build when MOD-0148 starts (MVP-6) |
| C5 | DC-04 = **MOD-0004**, DC-05 = **MOD-0213** (Blueprint W-2; no MCR needed). Ali 2026-10-20: MOD-0147 v1 = performance record only; scoring and risk when MOD-0004 / MOD-0213 exist (or minimal versions first). Our additions: v1 needs an owner-approved fixed metric set (pack amendment); supplier risk overlaps **MOD-0216 TPRM** (SoR of third-party assessments); MOD-0004 may already be under way elsewhere (status report "Treasury Core" sheet) — locate before building. Open: which plan is binding (status report: MVP-6 Supporting vs Supply Chain-Quality Wave 3, MOD-0216 Wave 2) | Ali | **Answered 2026-10-21:** (1) plan — MOD-0147 and MOD-0148 stay in the MVP-6 delivery order; MOD-0147 starts after TPRM (MOD-0216) is finished. (2) MOD-0147 v1 — fixed metric set approved; the final list goes back to Natig for review (§3a); the risk part does not hold risk itself, it reads and shows TPRM's (MOD-0216) result; metrics move to the Metric Registry when it exists. (3) MOD-0004 — no three copies: first establish whether Cihan's work and the "Treasury Core" work are the same, one owner, a small shared version to main, used by PV and SCE (research §3a) |
| C6 | After C3: decision record → PR → MOD-0147/0148 pack revision ("Supplier eligibility seam") → **CT-APR-02 independent review** → CT-APR-01 row (one person adds, another merges with a merge commit) | CT, reviewer, Beste | waits on C3 |
| C7 | After C4: same path for MOD-0148 + a separate Auth/Platform pack for the binding, `supplier_user` token and gateway rule | CT, Auth/Platform, integration owner | waits on C4 |
| C8 | Only then: the narrow `ISupplierEligibilityReader` slice (MOD-0147 first) | CT | waits on C6 |

**D. Open owner items carried from v9.9**

| # | step | owner | state |
|---|---|---|---|
| D1 | **BL-652** (Admin baseline for Carrier/Load/Return/Claim): Option 1 ruled 2026-10-18, **never applied**, expiry 2026-11-03. After Q358/G7 every widening left clean-up debt; recommendation: do not apply — go straight to Option 2 (SupplyChain → MDM service identity) or the entitlement path | Ali | open |
| D2 | Where contracts live (v9.9 4c): `docs/analysis/contracts/` vs the five docs folders — move, or allow the path in `docs-organization` (rule change, approval) | Ali | open |
| D3 | Runbook text says MongoDB 7.0, dev machine runs 8.0.18 (v9.9 4b; ruled not blocking) | Ali | open item |
| D4 | Gateway `Observability:Correlation:SigningKey` set in every non-Development environment (code fail-fast is on main) | ops / Ali | open |
| D5 | `DeletedAt` on Auth entities (soft delete records `UpdatedAt`/`UpdatedBy` today) | CT (Auth, XMC-001) | **Ali 2026-10-20: may be added** — work item |
| D6 | Old open PRs **#49** and **#169** — keep, revive or close | **Beste** | **2026-10-21:** Beste decides |

**Critical path:** C1 is answered (2026-10-21) → rewrite DC-02 D1 for the tenant-wide master → C3 (D2–D6) → C6 → C8 for the next real code; MOD-0147 itself starts only after MOD-0216 TPRM; A1 waits for the production authorization update.
B1 should close first among the reds: it makes every PR's `planning-tests` red.

**Stacked PRs (lesson of #167):** the repository dismisses approvals when the merge-base changes. Land a stacked
PR as: merge the bottom PR → update the top branch once → approve → merge at once; never push to a PR under review.

## 3a. Proposals for owner review (2026-10-21) — not decisions

**MOD-0147 v1 fixed metric set — draft for Natig's review.** The pack's score policy (`MOD-0147-supplier-performance-risk.md`
§0.1) requires every metric to be unit `PERCENT`, measured `0..100`, direction `HIGHER_IS_BETTER`. The two
"rate of bad events" metrics are therefore stated as their positive counterpart (same data, same meaning).
Scope: per Tenant + LE + supplier + period (C1: approval and relationship are per LE).

| code (proposed) | metric | formula (period) | data source |
|---|---|---|---|
| `OTD` | On-time delivery rate | receipt lines received by the PO line's promised date (owner sets the tolerance in days) ÷ receipt lines × 100 | MOD-0141 PO line dates, MOD-0142 GRN receipt date |
| `QTY_ACC` | Quantity accuracy | receipt lines whose received qty is within the owner's tolerance of the ordered qty ÷ receipt lines × 100 | MOD-0141 ordered qty, MOD-0142 received qty |
| `QUAL_ACC` | Quality acceptance rate (= 100 − quality reject rate) | accepted qty ÷ inspected qty × 100 | MOD-0142 receiving + QC inspection result — **QC source module not yet identified; to confirm** |
| `INV_MATCH` | Invoice match rate (= 100 − invoice mismatch rate) | invoices that pass 3-way match without exception ÷ invoices × 100 | MOD-0143 Invoice Capture & 3-Way Match |

Still for Natig to decide: weights (proposal: 25 each, total = the policy weight), the period (proposal: calendar month),
the two tolerances, and the rule when a metric has no data in a period (the pack today requires every metric).
All four sources are Procurement modules (other owner) — reading them needs XMC-001 consent and a read contract.

**MOD-0004 — where it exists (measured 2026-10-21, read-only).**
- `origin/hr-future` (Cihan, 2026-09-08): `7a7fd47ef` bootstraps `Diten.DataKnowledgeService` (`:5062`) with MOD-0004
  Metric & Semantic Registry, plus six more DKI modules (MOD-0059…0064) — **7 commits** on `services/Diten.DataKnowledgeService`,
  each with an audit-metadata query. Not on main; the branch is 68 ahead / 3443 behind `origin/main` and touches
  `gateway/Diten.ApiGateway/ocelot.json` and `_LayoutTenantShell.cshtml`.
- `origin/main`: MOD-0004 is only a registry row (`planned / missing`, no pack, no runtime) and a hard gate cited by PVG
  (MOD-0234, DCP-004). Port 5062 is not assigned in `.antigravity/rules/ports.md` on main.
- No other branch in this repository and no commit in the source repository `ERP-vNext` mentions MOD-0004.
- **Inference (to confirm with Cihan / the status-report owner):** the "Treasury Core" sheet's 7 commits are most likely
  these same 7 DKI commits — i.e. one piece of work, not two.

## 4. Hardening after MVP-6 (unchanged from v9.9)

H1 dev JWT secret fail-fast in every service and out of 23 appsettings · H2 test hosts mint AuthService-shaped tokens
+ a legal-entity failing path per gate · H3 BL-615 audit ledger (joint with CRM) · H4 one adapter base.

## 5. Post-MVP-6 (named, not scheduled)

DemandPlanning production authorities (S1, AC-D01…D29); DEMAND v2 (G3) to replace the S&OP/Capacity fixture seed;
SUPPLY-CONSTRAINTS producer MOD-0193 (G2); BL-652 Option 2; the parked process audit.

**Dates:** the R8 transition ends about **2026-11-18** (v9.9 inference; owner confirms). BL-652's expiry 2026-11-03
is the only open expiry; Q358 and G7 are already removed.
