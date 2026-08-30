# Legacy Enterprise Strategy Parity Gate

Status: **OPEN / USER-ACCEPTANCE-PENDING**

Authority: Control Tower user decision — legacy and new surfaces must remain side-by-side until page-level acceptance.

Evidence rule: legacy code is evidence, not canonical authority.

Static audit checkpoint: `91321bc4ce87fdda6bfdcf6656d555daae11e40d` (2026-08-29). The ledger contains
14 page-level rows and 142 field-level rows (68 Initiative + 74 Project). Static code evidence closes zero
browser rows: all 14 still require an approved new browser URL, real backend evidence and user acceptance.

## Gate rules

1. Legacy `Diten.EnterpriseStrategyService` and `EnterpriseStrategyBusinessPerformance` surfaces are not
   deleted, changed, retired or declared fully replaced by this ledger.
2. Every row requires old and new browser evidence on different ports before user acceptance.
3. `Same`, `Replaced`, `Missing`, `Intentionally Removed` and `Out of Scope` are the only dispositions.
4. A placeholder or mock surface is never proof of implemented business behavior.
5. Foreign-owned fields are mapped to typed references; their truth is not copied into the new owner.
6. Retirement remains blocked while any required row is `Missing`, lacks browser evidence, or lacks user
   acceptance.

## Page-level ledger — subdomains 1.3, 1.4 and 1.6

| Area | Old URL | Old entities / fields | Commands, lifecycle, permissions, integrations | New owner / module | New URL | Field mapping | Disposition | Migration requirement | Browser evidence |
|---|---|---|---|---|---|---|---|---|---|
| 1.3 Portfolio Management | `/management-governance/modules/mod-portfolio-management` | Registry metadata only; no authoritative portfolio aggregate evidenced | Placeholder navigation; no proven command/lifecycle/permission | MOD-0117 Portfolio | TBD | Registry label is not migrated; compare only intended capability | Replaced | No placeholder-data migration | Pending |
| 1.3 Investment Governance | `/management-governance/modules/mod-investment-governance` | Registry metadata only | Placeholder navigation; approval truth must remain MOD-0023-owned | MOD-0117 InvestmentCase; MOD-0023 approval refs | TBD | Investment identity/decision references map to typed contracts | Replaced | No placeholder-data migration; decision refs require contract mapping | Pending |
| 1.3 Benefits & Value Tracking | `/management-governance/modules/mod-benefits-value` | Registry metadata only | No proven lifecycle | MOD-0117 BenefitCommitment + MOD-0072 Outcome | TBD | Commitment stays MOD-0117; measured outcome stays MOD-0072 | Missing | Define page projection; do not copy Outcome truth | Pending |
| 1.3 Capital Allocation | `/management-governance/modules/mod-capital-allocation` | `ModuleSummary` registry descriptor only; synthetic pending/KPI metadata, no allocation aggregate | Placeholder navigation only; no command, allocation lifecycle or permission enforcement evidenced | MOD-0136 owns financial allocation truth; MOD-0117 owns only `InvestmentCase` and its typed selected-budget-version relationship | None evidenced. Existing `/ppm/investment-cases` is adjacent, not an equivalent Capital Allocation page. | Placeholder metadata is not migrated. Any future allocation amount/currency/period/line/certification remains MOD-0136-owned; MOD-0117 may carry only the approved typed reference. | Out of Scope for MOD-0117 / Missing authoritative target page | No placeholder migration. Real allocation data, if any, requires a separate read-only inventory and owner-approved MOD-0136 migration plan. | Pending |
| 1.4 Initiatives list | `/management-governance/delivery-execution/initiatives` | Initiative link plus name, description, owner/sponsor, company/person/position refs, dates, wave, status, priority, complexity, benefit, KPI, budget and governance fields | Create/edit/detail, link to Objective, readiness/traceability; current frontend adapter may be mock | MOD-0117 Initiative; MOD-0352 strategy-link projection | TBD | Initiative lifecycle fields → MOD-0117; ParentObjective/Goal and contribution → MOD-0352 projection; person/org → MOD-0288 refs; budget → MOD-0136 refs | Missing | Field-by-field migration/quarantine plan required | Pending |
| 1.4 Initiative create/edit/detail | `/management-governance/delivery-execution/initiatives/new`, `/initiatives/{id}/edit`, `/initiatives/{id}` | Same rich Initiative contract | Draft/edit/readiness and strategy anchor | MOD-0117 + MOD-0352 read-only alignment | TBD | Same ownership split as list; no second lifecycle | Missing | Exact create/update payload mapping and rejected-field ledger | Pending |
| 1.4 Projects list | `/management-governance/delivery-execution/projects` | Project link plus owner/PM, sponsor, status/phase, dates, methodology, readiness/risk/health, budget/funding/cost center, approval/evidence refs | Create/detail/link; non-draft local validation exists; current frontend adapter may be mock | MOD-0117 Project; MOD-0352 link-only projection | TBD | Project lifecycle → MOD-0117; strategy lineage → MOD-0352; approval → MOD-0023; budget → MOD-0136; evidence → shared typed ref | Missing | Field-by-field migration/quarantine plan required | Pending |
| 1.4 Project create/detail | `/management-governance/delivery-execution/projects/new`, `/projects/{id}` | Same rich Project contract | Draft/non-draft lifecycle and budget governance | MOD-0117 + typed owner references | TBD | Full lifecycle stays MOD-0117; MOD-0352 cannot require foreign local fields for link-only projection | Missing | Exact payload and state-transition mapping | Pending |
| 1.4 Programs | `/management-governance/delivery-execution/programs` | Shell cards only; no authoritative legacy Program model evidenced | Navigation shell | MOD-0117 Program | TBD | No legacy data assumed | Replaced | None unless later data evidence appears | Pending |
| 1.4 Delivery Map / Dependencies | `/management-governance/delivery-execution/delivery-map` | Static shell; controller loads Goal/Objective/Connection plus Initiative/Project strategy-link collections, but the page renders no dependency entity/edge rows | Three navigation links only; no create/edit dependency command, dependency lifecycle, critical-path calculation or route-level permission evidenced | MOD-0354 owns pure structural hierarchy/order/dependency; MOD-0117 owns Initiative/Program/Project identity and lifecycle only; MOD-0024/execution owner must own task/status/due/assignee semantics | Partial structural target exists at `/management-governance/delivery-execution/structures`; existing `/ppm/initiatives`, `/ppm/programs`, `/ppm/projects` are entity workspaces, not a Delivery Map. No canonical composite-map URL exists. | Structure/node/order/untyped directed edge → MOD-0354; typed PPM identity reference → MOD-0117; task/status/due/assignee → MOD-0024/execution owner. FS/SS/FF/SF, lag, calendar and critical path remain unsupported pending a separate owner decision. | Missing full equivalent; partial structural replacement only | Quarantine ambiguous legacy rows. No blind conversion of free text, task fields or scheduling edge types into DWS/PPM. | Pending |
| 1.6 Process Architecture | `/management-governance/modules/mod-process-architecture` | Registry placeholder only | No proven backend lifecycle | MOD-0355 | TBD | No legacy entity migration evidenced | Replaced | None | Pending |
| 1.6 Process Design & Modeling | `/management-governance/modules/mod-process-design` | Registry placeholder only | No proven backend lifecycle | MOD-0355 | TBD | New model/version/activity/control-point contract is authoritative | Replaced | None | Pending |
| 1.6 Process Performance | `/management-governance/modules/mod-process-performance` | Registry placeholder only | No proven legacy backend lifecycle | MOD-0355 bounded performance contract; no browser/API surface | TBD | MOD-0355 owns the bounded KPI-binding contract, but the mandatory reference contract and user surface remain unavailable | Missing | No placeholder-data migration; browser/API delivery remains a separate bounded slice | Pending |
| 1.6 Process Improvement | `/management-governance/modules/mod-process-improvement` | Registry placeholder only | No proven legacy backend lifecycle | MOD-0355 Process Improvement Opportunity/Outcome | `/management-governance/process-modeling/improvements` | MOD-0355 owns the bounded opportunity/outcome contract, nine exact operations/permissions and two collections; the new page is read-only and Gateway-default-off | Missing full parity; route-backed bounded replacement exists | No placeholder-data migration; write UI, activated Gateway/auth and populated browser evidence remain separate gates | Pending: compare placeholder with permission-denied/default-off read states; no write-parity claim |

## Focused parity audit — Capital Allocation and Delivery Map

### Capital Allocation

- **Old URL:** `/management-governance/modules/mod-capital-allocation`.
- **Old entity/fields:** no persisted allocation entity is proven. The route resolves the registry-only
  `ModuleSummary` fields `Id`, `Name`, `Slug`, `Description`, `Status`, `Owner`, `RiskLevel`, `Route`,
  `PendingItemsCount`, `Implemented`, `LinkedBlueprintModuleIds`, `Tags`, `Dependencies` and `KpiSummary`.
  `PendingItemsCount`, Blueprint link and KPI rows are generated shell metadata, not financial truth.
- **Old actions/lifecycle/permissions/integrations:** the placeholder offers only **Open Related Queue** and
  **Find Signals** navigation. No allocation create/edit/approve command, lifecycle, persistence, permission
  guard or finance-provider integration is evidenced.
- **Authoritative target:** MOD-0136 remains the owner of budget/allocation financial truth. MOD-0117 owns
  `InvestmentCase` and only the typed selected-budget-version relationship; it cannot become a second budget
  or capital-allocation SoR.
- **Existing-page decision:** `/ppm/investment-cases` can show and mutate the PPM-owned case but cannot serve
  as the Capital Allocation equivalent. Its fields and lifecycle contain no allocation amount, currency,
  period, line, certification or approval truth. Reusing it as the replacement would silently collapse two
  owners.
- **Required slice:** if Capital Allocation remains a required product page, a separately approved MOD-0136
  backend/read projection plus frontend slice is required. A bounded MOD-0117 UI extension may later display
  only the approved typed budget-version reference. No new URL is authorized by this ledger.
- **Pack authority:** MOD-0117 is `review` and authorizes the selected-budget-version relationship/API evidence,
  not a Capital Allocation UI or financial aggregate. No MOD-0136 pack is materialized in this integration
  worktree, so an executable Capital Allocation page authority cannot be proven here.

### Delivery Map / Dependencies

- **Old URL:** `/management-governance/delivery-execution/delivery-map`.
- **Old entity/fields:** there is no delivery-map aggregate. `DeliveryExecutionManagementController` loads
  `Goal`, `Objective`, `StrategyConnection`, `InitiativeStrategyLinkView` and `ProjectStrategyLinkView` and
  computes three summary cards. The Razor page renders explanatory text and navigation only; its shared shell
  explicitly disables metric-card rendering. It renders no dependency ID, endpoints, type, lag, dates,
  critical path or edge lifecycle.
- **Old actions/lifecycle/permissions/integrations:** the only visible actions navigate to Projects, Programs
  and Strategy Lineage. No dependency mutation or lifecycle exists and no route-level permission is evidenced.
  Adapter reads are not proof of a dependency SoR or user-visible dependency mapping.
- **Authoritative target split:** MOD-0354 owns structure definition, revision, logical node, sibling order and
  pure untyped directed structural dependencies. MOD-0117 owns Portfolio/Initiative/Program/Project identity
  and lifecycle. MOD-0024 or another expressly approved execution owner must own task, assignee, due-date,
  status and execution dependency semantics.
- **Existing-page decision:** `/ppm/initiatives`, `/ppm/programs`, `/ppm/projects` and the Project workspace
  are not a Delivery Map: their DTOs have no dependency collection or critical-path contract. The default-off
  MOD-0354 page `/management-governance/delivery-execution/structures` is a valid partial replacement for
  hierarchy/order/pure structural edges only; it is not a full Delivery Map and currently lacks populated
  Gateway/browser acceptance evidence.
- **Required slice:** a composite read-only Delivery Map frontend/projection is still required if the product
  needs one screen combining DWS structure with typed PPM references. No new PPM backend aggregate is needed
  for the pure structural subset. If FS/SS/FF/SF, lag, calendar or critical-path behavior is required, a new
  owner decision and separately authorized backend contract are mandatory because MOD-0354 deliberately
  forbids those fields and MOD-0117 does not own them.
- **Pack authority:** MOD-0354 is `ready-for-dev` and records a default-off/non-production structural frontend
  amendment plus static checkpoint evidence; Gateway, populated browser evidence and activation remain open.
  MOD-0117 remains `review`, owns no dependency entity/map and its DCP-004 Gate-2 disposition projects zero
  WorkCenter items/actions. Neither pack currently authorizes a full composite Delivery Map.

## Field-level mandatory follow-up

Before browser acceptance, the Initiative and Project rows must be expanded into one row per field with:

- exact legacy source property and persistence location;
- exact new owner/module and target property or typed reference;
- null/empty rendering behavior on the new page;
- Same/Replaced/Missing/Intentionally Removed/Out of Scope disposition;
- conversion, quarantine and rollback rule;
- test evidence and user browser evidence.

In particular, owners/sponsors, person-position-company references, dates/waves, status/phase/methodology,
benefit hypotheses, risk/readiness/health/compliance, budgets/funding/cost center, approval, decision and
evidence references cannot be silently dropped.

## Field-by-field annex — Initiative and Project legacy DTO evidence

Source: `EnterpriseStrategyDtos.cs`. These DTOs are evidence, not authority. Exact target properties remain TBD
where no approved contract exists; this annex creates no URL, wire contract, persistence field or ownership exception.
`Missing` means target plus browser evidence is not closed. Empty states are fail-closed.

### InitiativeStrategyLinkViewDto — 68 properties

| Legacy field | New owner/module and target | Disposition | Empty rendering | Migration rule | Browser evidence |
|---|---|---|---|---|---|
| `LinkId` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `InitiativeId` (`string`) | MOD-0117 `InitiativeDto.Id` (`Guid`) | Missing | Explicit unavailable/blank; no inference or substitution. | Require an approved exact string-to-Guid identity conversion; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `InitiativeName` (`string`) | MOD-0117 `InitiativeDto.Name` | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Description` (`string`) | MOD-0117 `InitiativeDto.Description` | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Owner` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `DeliveryOwnerCompanyId` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `DeliveryOwnerPositionId` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `DeliveryOwnerPersonId` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `ExecutiveSponsor` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `AccountableSponsorRole` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `Status` (`string`) | MOD-0117 `InitiativeDto.LifecycleState` | Missing | Explicit unavailable/blank; no inference or substitution. | Require an approved exact lifecycle-value mapping; quarantine unknown/conflicting values; retain legacy record for rollback until acceptance. | Pending |
| `Type` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `NormalizedType` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `WaveOrPhase` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Priority` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Complexity` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Maturity` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `StartDate` (`DateTime?`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `EndDate` (`DateTime?`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ReportingFrequency` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `PrimaryKpi` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ContributionMetricName` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ContributionUnitOfMeasure` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ContributionPlanGranularity` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ContributionMethod` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ObjectiveTargetGranularity` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ContributionTiming` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `BenefitHypothesis` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `BenefitRealizationStart` (`DateTime?`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `BenefitRealizationEnd` (`DateTime?`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ContributionPlanValues` (`IReadOnlyList<InitiativeContributionPlanValueDto>`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit empty list; no synthetic rows. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `SourceSystem` (`string`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceRecordId` (`string`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `ParentObjectiveId` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentObjectiveName` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentGoalId` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentGoalName` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `StrategyLinkStatus` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ContributionType` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ContributionWeight` (`decimal`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Unavailable unless authoritative; never infer zero. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `MetricBindingsJson` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Intentionally Removed | Explicit unavailable/blank; no inference or substitution. | Do not migrate raw JSON as authority; require separately approved typed metric binding, otherwise quarantine. | Pending |
| `DecisionReference` (`string?`) | MOD-0023 typed decision/approval reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | No local approval inference; migrate only verifiable typed reference, otherwise quarantine. | Pending |
| `EvidenceReference` (`string?`) | Shared evidence authority typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy payload; preserve verified reference/provenance only, otherwise quarantine. | Pending |
| `SponsoringCompanyId` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `ParticipatingCompanyIds` (`List<string>`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit empty list; no synthetic rows. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `EntityScope` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `InitiativeClass` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `BudgetEnvelope` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `BudgetAmount` (`decimal?`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Unavailable unless authoritative; never infer zero. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `CurrencyCode` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `FundingSource` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `StrategyAlignmentNote` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `GovernanceStage` (`string`) | MOD-0023 typed decision/approval reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | No local approval inference; migrate only verifiable typed reference, otherwise quarantine. | Pending |
| `GovernanceNotes` (`string`) | MOD-0023 typed decision/approval reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | No local approval inference; migrate only verifiable typed reference, otherwise quarantine. | Pending |
| `DependencyFlag` (`bool`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Unavailable unless authoritative; never infer false. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Readiness` (`InitiativeReadinessDto?`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ReadinessStatus` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Notes` (`string`) | MOD-0117 Initiative lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Version` (`int`) | MOD-0117 `InitiativeDto.Version` | Missing | Unavailable unless authoritative; never infer zero. | Preserve only an authoritative concurrency version; quarantine missing/conflicting versions; retain legacy record for rollback until acceptance. | Pending |
| `SyncedAt` (`DateTime?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SyncFreshness` (`string`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `Warnings` (`IReadOnlyList<string>`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit empty list; no synthetic rows. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceTemplateType` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceTemplateId` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceTemplateVersion` (`int?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Unavailable unless authoritative; never infer zero. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceBlueprintPackId` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `InstantiationBatchId` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `CreatedFromLibrary` (`bool`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Unavailable unless authoritative; never infer false. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |

### ProjectStrategyLinkViewDto — 74 properties

| Legacy field | New owner/module and target | Disposition | Empty rendering | Migration rule | Browser evidence |
|---|---|---|---|---|---|
| `LinkId` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ProjectId` (`string`) | MOD-0117 `ProjectDto.Id` (`Guid`) | Missing | Explicit unavailable/blank; no inference or substitution. | Require an approved exact string-to-Guid identity conversion; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ProjectName` (`string`) | MOD-0117 `ProjectDto.Name` | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Description` (`string`) | MOD-0117 `ProjectDto.Description` | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `OwnerPm` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `Sponsor` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `BusinessOwner` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `Status` (`string`) | MOD-0117 `ProjectDto.LifecycleState` | Missing | Explicit unavailable/blank; no inference or substitution. | Require an approved exact lifecycle-value mapping; quarantine unknown/conflicting values; retain legacy record for rollback until acceptance. | Pending |
| `Phase` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `DeliveryType` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `DeliveryMethodology` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `Priority` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ComplexitySize` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `StartDate` (`DateTime?`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `EndDate` (`DateTime?`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `GoLiveDate` (`DateTime?`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ReportingCadence` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `SuccessMetric` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `MetricBaseline` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `MetricTarget` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `RiskRating` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ReadinessStatus` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `OverallHealth` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `ComplianceRegulatoryImpact` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `DependencyFlag` (`bool`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Unavailable unless authoritative; never infer false. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `EvidenceRequiredFlag` (`bool`) | Shared evidence authority typed reference consumed by MOD-0117; exact target TBD | Missing | Unavailable unless authoritative; never infer false. | Do not copy payload; preserve verified reference/provenance only, otherwise quarantine. | Pending |
| `SourceSystem` (`string`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceRecordId` (`string`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `ParentInitiativeId` (`string`) | MOD-0117 `ProjectDto.ParentId` (`Guid`) only when `ProjectDto.ParentType = Initiative`; MOD-0352 consumes a read-only link | Missing | Explicit unavailable/blank; no inference or substitution. | Require exact parent-type and string-to-Guid identity conversion; quarantine unresolved/mismatched parent identity. | Pending |
| `ParentInitiativeName` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentObjectiveId` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentObjectiveName` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentGoalId` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentGoalName` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ParentType` (`string`) | MOD-0117 `ProjectDto.ParentType` | Missing | Explicit unavailable/blank; no inference or substitution. | Require an approved exact enum-value mapping; quarantine unknown values; MOD-0352 consumes rather than owns this lifecycle truth. | Pending |
| `EntityScope` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `CreationMode` (`string`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `TemplateApplicationMode` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `StrategyLinkStatus` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `ContributionNote` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy foreign lifecycle truth; rebuild/validate from authoritative typed references; quarantine unresolved identity. | Pending |
| `MetricBindingsJson` (`string`) | MOD-0352 read-only strategy-link projection; exact target property TBD | Intentionally Removed | Explicit unavailable/blank; no inference or substitution. | Do not migrate raw JSON as authority; require separately approved typed metric binding, otherwise quarantine. | Pending |
| `DecisionReference` (`string?`) | MOD-0023 typed decision/approval reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | No local approval inference; migrate only verifiable typed reference, otherwise quarantine. | Pending |
| `EvidenceReference` (`string?`) | Shared evidence authority typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy payload; preserve verified reference/provenance only, otherwise quarantine. | Pending |
| `DeliveryCompanyId` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `FundingCompanyId` (`string?`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `OwningFunctionDepartment` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `DeliveryPartnerVendor` (`string`) | MOD-0288 typed reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Resolve exact person/position/company reference; quarantine missing/ambiguous identity; never default. | Pending |
| `ScopeSummary` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `OutOfScopeNote` (`string`) | MOD-0117 Project lifecycle; exact target property TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Map by exact tenant-scoped canonical identity; quarantine conversion/conflict; retain legacy record for rollback until acceptance. | Pending |
| `BudgetRequired` (`bool?`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Unavailable unless authoritative; never infer false. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `BudgetAmount` (`decimal?`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Unavailable unless authoritative; never infer zero. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `CurrencyCode` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `BudgetType` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `BudgetBasis` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `FundingSource` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `CostCenter` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `BudgetOwner` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `ApprovalRoute` (`string`) | MOD-0023 typed decision/approval reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | No local approval inference; migrate only verifiable typed reference, otherwise quarantine. | Pending |
| `FinancialNotes` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `NoBudgetReason` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `BudgetSummary` (`string`) | MOD-0136 typed financial reference consumed by MOD-0117; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Do not copy financial SoR truth; carry only approved typed reference/value; quarantine mismatch. | Pending |
| `Version` (`int`) | MOD-0117 `ProjectDto.Version` | Missing | Unavailable unless authoritative; never infer zero. | Preserve only an authoritative concurrency version; quarantine missing/conflicting versions; retain legacy record for rollback until acceptance. | Pending |
| `SyncedAt` (`DateTime?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `CreatedAt` (`DateTime`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `UpdatedAt` (`DateTime`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SyncFreshness` (`string`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `Warnings` (`IReadOnlyList<string>`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit empty list; no synthetic rows. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceTemplateType` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceTemplateId` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceTemplateName` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceTemplateVersion` (`int?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Unavailable unless authoritative; never infer zero. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `SourceBlueprintPackId` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `InstantiationBatchId` (`string?`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Explicit unavailable/blank; no inference or substitution. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |
| `CreatedFromLibrary` (`bool`) | MOD-0117 projection provenance/technical metadata; exact target TBD | Missing | Unavailable unless authoritative; never infer false. | Preserve source identity/version exactly; no synthesized defaults; quarantine stale/conflicting provenance. | Pending |

## Static target contract evidence

These are executable backend or default-off local-test contracts from the static baseline
`91321bc4ce87fdda6bfdcf6656d555daae11e40d`, reconciled through the bounded MOD-0355 checkpoint
`846fcfb7b585a38dafaa6ad1f34690d013240588`. A contract alone is not migration evidence or proof that a
legacy page has been replaced. Only an actual bounded browser route may populate `New URL`, and it remains
unaccepted until the browser/user gates pass.

| Capability | Static target contract | Current limitation |
|---|---|---|
| Portfolio | MOD-0117 `api/v1/ppm/portfolios` | Backend CRUD/lifecycle only; no accepted browser page. |
| Investment Governance | MOD-0117 `api/v1/ppm/investment-cases` plus typed Gate-I references | Backend contracts only; MOD-0023 decision truth is not copied and no accepted browser page exists. |
| Benefits & Value | MOD-0117 `api/v1/ppm/benefit-commitments` plus typed MOD-0072 outcome references | Backend contracts only; measured Outcome truth remains MOD-0072-owned and no accepted browser page exists. |
| Initiatives | MOD-0117 `api/v1/ppm/initiatives` | The DTO is a narrow lifecycle contract, not parity for the 68-field legacy projection. |
| Projects | MOD-0117 `api/v1/ppm/projects` | The DTO is a narrow lifecycle contract, not parity for the 74-field legacy projection. |
| Programs | MOD-0117 `api/v1/ppm/programs` | Backend CRUD/lifecycle only; no accepted browser page. |
| Delivery structure | MOD-0354 `api/dws/structures` | Local-test/default-off structural API; no task/scheduling ownership or accepted browser page. |
| Process Architecture / Design | MOD-0355 `internal/local-test/v1/process-modeling` | Default-off local-test contract; not a production or browser URL. |
| Process Performance | MOD-0355 bounded KPI-binding contract | Mandatory reference contract unresolved; no successful handler/API/browser surface. |
| Process Improvement | MOD-0355 bounded opportunity/outcome contract, persistence, local-test API and read-only tenant route | API is loopback local-test only; the browser read is Gateway-default-off and has no populated/write/user-acceptance evidence. |

Static summary: 14 page rows, 142 field rows, zero browser-closed rows. Five page rows retain `Replaced`, one
retains `Out of Scope`, and eight retain `Missing`. The twelve narrow DTO target names above reduce `TBD`
metadata only; none changes a `Missing` disposition.

## Browser acceptance procedure

For each row:

1. Run the legacy and new applications on different approved local ports.
2. Open the exact old URL and capture page, empty state, loaded state and permitted actions.
3. Open the exact new URL and capture the same states.
4. Verify network calls reach real backend contracts rather than mock adapters or fixture-only data.
5. Compare every visible field, action, error state and lifecycle transition.
6. Record the evidence links and user verdict in this ledger.
7. Do not retire the old route until every required row is accepted.

## Current retirement verdict

**BLOCKED.** The Initiative/Project legacy inventory is complete, but approved target properties, remaining
new URLs and browser evidence remain incomplete. Process Performance remains without a target route;
Process Improvement now has a bounded route but remains open for populated/write/browser/user acceptance.
Legacy retirement, deletion and “full parity complete” are prohibited.
