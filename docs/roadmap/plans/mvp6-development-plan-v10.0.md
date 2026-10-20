# MVP6 development plan v10.0 — 2026-10-20

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
| A1 | Run Q358 in production: `Q358Retraction__Mode=DryRun` → read the log (`unknown` 0, `failed` 0) → `Apply` → remove the setting. Runbook on main | **Ali** (deploys) | **open — next** |
| A2 | G7 clean-up on non-prod hosts with `DemandFixtures` enabled (same runbook, `G7Retraction`) | Ali decides who/when | open, optional |
| A3 | Mark `mvp6-0190-0192-g7-role-grants-owner-decision-01.md` superseded (still says "IN EFFECT") | Ali | open |
| A4 | Fix the account mapping in `mvp6-g7-grant-removal-01/REPORT.md` ("Ali (`bestepullukcu-alt`)" → Ali = `GMG-ERP`) — small docs PR | CT prepares, Beste merges | open |

**B. Main reds (other owners; MVP-6 does not touch them, XMC-001)**

| # | step | owner | state |
|---|---|---|---|
| B1 | `MrpLiveCalculationTests` ×2 — fixture fixes the first demand week at 2026-10-19 and stock `AsOf` at `UtcNow`; red every day since 2026-10-20 | MOD-0189 (`ElikPenco`) | issue **#174** |
| B2 | `PpmAuditRetentionPolicySeedMongoTests` — CI `platform-app` row has no test-owned mongod | PSS / Platform | issue **#175** |
| B3 | MOD-0022 `LoginOnlyEndpointGuardTests` ×2 | MVP-1 lane | owner-ruled known red (v9.9 1a) |
| B4 | BL-615 Arch ×3 (`DocsPathGuard`, `AuditTrailStandard` ×2) | joint with CRM | registered known failures |
| B5 | `OperationalSingleInstanceLockTests` (main auth-app, once) | Auth operational lock owner | measure before routing |

**C. Supplier work (MOD-0147 / MOD-0148) — decisions before any code**

| # | step | owner | state |
|---|---|---|---|
| C1 | MOD-0140 system of record: this repository (Procurement 5065, frozen `supplier.openapi.yaml`) or `ERP-vNext` (Platform 5057)? DC-02 D1 depends on it | **Ali** | open (`mod-0140-dual-implementation-01`, local record) |
| C2 | Private notice to the MOD-0140 owner: Procurement takes `X-Legal-Entity-Id` without validating it against the tenant (`TenantResolutionMiddleware.cs:77-100`). Already visible in closed #171 | Ali / user, private channel | open |
| C3 | **DC-02 decisions** (draft `docs/sce/dc02-supplier-eligibility-draft-01`): D1, D3, D4, D5 — MOD-0147/0148 owner (SCE); **D2 joint with the MOD-0140 Procurement owner**; D6 — management | named owners | open |
| C4 | **DC-03 decisions** (draft `docs/sce/dc03-supplier-actor-binding-draft-01`): D1–D8 — Auth/Platform security owner with the MOD-0148 owner; D9 — management. Precondition found (scan on closed #172's branch): the gateway 403s every non-`tenant_user` actor on tenant endpoints, so a portal-only rule is needed; a token without `actor_type` passes | named owners | open |
| C5 | DC-04 / DC-05 (Metric Registry, Risk Register — no service exists; MOD-0004 is DOCS ONLY): open via a Master change request, or bound MOD-0147's first version without scoring/risk depth | Ali | open |
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
| D5 | `DeletedAt` on Auth entities (soft delete records `UpdatedAt`/`UpdatedBy` today) | Ali | open |

**Critical path:** A1 (only production work left from MVP-6) · C1 → C3 → C6 → C8 for the next real code.
B1 should close first among the reds: it makes every PR's `planning-tests` red.

**Stacked PRs (lesson of #167):** the repository dismisses approvals when the merge-base changes. Land a stacked
PR as: merge the bottom PR → update the top branch once → approve → merge at once; never push to a PR under review.

## 4. Hardening after MVP-6 (unchanged from v9.9)

H1 dev JWT secret fail-fast in every service and out of 23 appsettings · H2 test hosts mint AuthService-shaped tokens
+ a legal-entity failing path per gate · H3 BL-615 audit ledger (joint with CRM) · H4 one adapter base.

## 5. Post-MVP-6 (named, not scheduled)

DemandPlanning production authorities (S1, AC-D01…D29); DEMAND v2 (G3) to replace the S&OP/Capacity fixture seed;
SUPPLY-CONSTRAINTS producer MOD-0193 (G2); BL-652 Option 2; the parked process audit.

**Dates:** the R8 transition ends about **2026-11-18** (v9.9 inference; owner confirms). BL-652's expiry 2026-11-03
is the only open expiry; Q358 and G7 are already removed.
