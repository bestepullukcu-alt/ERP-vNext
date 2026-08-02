# Portfolio Delivery (PPM) — Domain Config

> Bu dosya domain'in **sınırlarını ve kararlarını** tanımlar. Engineering NASIL kuralları [.antigravity/rules/](../../../.antigravity/rules/)'da; modül envanteri [execution/portfolio/master-development-plan.md](../../portfolio/master-development-plan.md)'de; aktif 1.3/MOD-0117 orchestration sözleşmesi [DCP-006](../../portfolio/delivery-capability-packs/DCP-006-portfolio-delivery-process-core.md)'dadır. [DCP-003](../../portfolio/delivery-capability-packs/DCP-003-ppm-work-management.md) yalnız deferred/non-executable legacy-parity planning kaynağıdır.

## Purpose

Portfolio Delivery (PPM) domain'inin Blueprint-kanonik kimliği `MOD-0117 — Project & Portfolio Management (PPM)`'dir. Aktif DCP-006 dilimi Portfolio, Initiative, Program, Project, PPM-owned InvestmentCase ve planned-benefit BenefitCommitment kayıtlarını kapsar. DCP-003 Work Records/Task/Workstream/Calendar/Effort/Meeting özellikleri yalnız deferred, non-executable legacy backlog'dur. Blueprint-wide Demand, capacity ve diğer MOD-0117 bağlamları bu Gate L kapsamına otomatik olarak dahil olmaz.

## In-Scope Modules

> Sıra, faz ve bloklayıcılar için [DCP-003](../../portfolio/delivery-capability-packs/DCP-003-ppm-work-management.md) §9/§14. Burada sadece sahiplik listesi. Kesin FU numaraları henüz rezerve edilmedi — pack authoring'de `--parent MOD-0117` preflight'ı zorunlu.

**MVP:** MOD-0117 (parent dilimi) — PPM Work Records Core
**Planlanmış (FU adayları, numarasız):** PPM Task Core / My Tasks, PPM Workstream & Hierarchy, PPM Calendar Scheduling, PPM Project Effort Log (ASSUMPTION rejimi), PPM Meeting / Status Reports

## Out-of-Scope

- Onay/SLA/eskalasyon motoru → `MOD-0023 Workflow Designer` (PSS)
- Görev/checklist şablonları → `MOD-0024 Task & Checklist Engine` (PSS)
- Bildirim gönderimi → `MOD-0027 Notification Service` (PSS)
- Kalıcı doküman/binary depolama → `MOD-0028 / MOD-0262` (PSS)
- Kurumsal lookup SSOT → `MOD-0048 Reference Data Management` (PSS)
- Time Entry / Attendance / Leave SoR → `MOD-0280` (HCM — repoda henüz yok; EA-TBD)
- Organizasyon dizini → `MOD-0288` (PSS)
- Google Calendar / Meet, SignalR hub, AI Action Extraction, TimerPopup, Excel import → ilk faz dışı (DCP-003 §6)
- MDM / ESBP / diğer tenant-side iş süreçleri → ilgili domain'ler

## Domain-Level Repo Scope

- `execution/domains/portfolio-delivery/**`
- `services/Diten.PpmService/**` — MOD-0117 Phase 2A servisi mevcuttur; yalnızca pack ve Control Tower tarafından açıkça yetkilendirilmiş dilimler değiştirilebilir
- `frontend/Diten.Web/Views/Ppm/**` (tenant shell modülleri)
- `frontend/Diten.Web/wwwroot/assets/js/Ppm/**`
- `frontend/Diten.Web/Resources/Views/Ppm/**` (7 dil .resx)
- Gateway route'ları: yalnızca `integration-agent` üzerinden ([routes.md](../../../.antigravity/rules/routes.md))

## Protected Paths

- `.antigravity/**` (global engineering system)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**`
- `services/Diten.AuthService/**`, `services/Diten.Platform/**`, `services/Diten.Platform.Common/**`, `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**` (diğer domain'lerin servisleri)
- `gateway/Diten.ApiGateway/**/ocelot.json` (yalnız integration-agent)
- `C:\CRM2\**` eski projeler: salt-okunur referans; **dosya kopyalama yasak**

## Ownership Boundaries

- **MOD-0023 (Workflow Designer):** PPM approval/SLA/escalation engine **yazamaz**; Blueprint dependency gate'i gereği MOD-0023 API tüketicisidir. MOD-0023 hazır olana dek Work Record/Task yalnızca **yalın status lifecycle** (statusId alanı) kullanır.
- **"Workflow" adlandırma yasağı:** Eski `Workflow*` adları bu domain'in hiçbir yeni route, permission, UI metni, class, namespace, JS, koleksiyon veya pack adında kullanılamaz. Onaylı sözlük: Work Record, Project Record, Work Item, PPM Task, Project Effort Log, Meeting / Status Report, Workstream, Schedule Slot.
- **MOD-0024 (Task & Checklist Engine):** Task/checklist **şablonlarının** SoR'u MOD-0024'tür. PPM yalnızca task **instance** sahibidir; kendi şablon tablosunu açamaz, ileride MOD-0024 şablon tüketicisi olur.
- **MOD-0028 / MOD-0262 (Doküman):** PPM kalıcı binary/document owner **değildir**; hedef desen evidence/document **link**'tir. Zorunlu geçici PPM-local attachment ancak açık teknik borç kaydı + devir planıyla kabul edilir.
- **MOD-0048 (Reference Data):** PPM-yerel lookup/enum'lar geçicidir; hedef SSOT MOD-0048'dir. Hardcoded fallback lookup listeleri yasaktır.
- **MOD-0280 (Time, Attendance & Leave) — ASSUMPTION-1:** PPM, "Project Effort Log" nesnesini MOD-0117 altında **geçici olarak** sahiplenir; MOD-0280 hayata geçtiğinde Time Entry SoR'u ile resmi kontrat kurulur ve gerekiyorsa sahiplik devri yapılır. **Kesin karar değildir; EA-TBD.** PPM tarafında "Time Entry" terimi ve `ppm.time-entries.*` permission adı kullanılamaz — doğru ad: `ppm.effort-logs.*`.
- **MOD-0288 (Organization Directory):** FunctionalDomain/SubDomain benzeri organizasyon referansları geçici PPM-yerel snapshot olabilir; kalıcı kaynak MOD-0288'dir, besleme kontratı follow-up'tır.
- **Gate I-A — Decision Trace:** Generic decision/rationale kalıcı SoR'u `MOD-0007 — Decision & Rationale Log`'dur. `InvestmentCase.GoverningDecisionReference` `0..1`'dir ve `UnderAnalysis → Closed` öncesinde zorunludur; supporting decision references `0..n`'dir. Approval yalnız MOD-0007 veya authoritative governance policy gerektirirse koşullu `MOD-0023` producer'ı üzerinden `ApprovalOutcomeReference 0..1` olarak tüketilir. Generic Decision ile ApprovalOutcome aynı kavram değildir; PPM decision/rationale/evidence payload'ı, approve/reject davranışı veya yerel Approved/Rejected truth üretmez.
- **Phase 2B Gate L ownership:** MOD-0117 `InvestmentCase` ve `BenefitCommitment` sahibidir. InvestmentCase tam olarak bir Portfolio'ya; BenefitCommitment tam olarak bir InvestmentCase'e bağlıdır. BenefitCommitment üzerinde ikinci `PortfolioId` yasaktır.
- **Gate I-B — Funding & Scenario:** `MOD-0136` budget/version SoR'udur; PPM yalnız `SelectedBudgetVersionReference 0..1` tutabilir. `MOD-0138` scenario/comparator ve selected-state SoR'udur; PPM scenario/comparator references `0..n` ve `SelectedScenarioReference 0..1` tutabilir. Analytical budget versions, scenario payload/output ve selected-state truth PPM'ye kopyalanamaz.
- **Gate I-C — Benefit Realization:** `MOD-0072` actual outcome/realization SoR'udur. `BenefitCommitment` planned targetı PPM'de tutar ve outcome/realization references `0..n` tüketebilir; actual measure, period, evidence ve realized state PPM'de saklanamaz. `BenefitCommitment.Closed` realized outcome anlamına gelmez.
- **Gate I PPM-owned consumer profiles — CLOSED governance baseline:** Exact v1 ledger: `ppm.investment-case-context`, `ppm.investment-case-governing-decision-reference`, `ppm.investment-case-supporting-decision-reference`, conditional `ppm.investment-case-approval-outcome-reference`, `ppm.investment-case-selected-budget-version-reference`, `ppm.investment-case-scenario-version-reference`, `ppm.investment-case-comparator-output-reference`, `ppm.investment-case-selected-scenario-reference` ve `ppm.benefit-commitment-outcome-reference`; tümünde `ContractVersion = 1.0`. Wrapper'lar yalnız PPM context/relationship identity'si ile ilgili producer'ın exact minimal reference tuple'ını saklar; producer DTO'suna rol, PPM lifecycle, tenant/actor veya yabancı payload eklemez. Ortak InvestmentCase context exactly `ContractName`, `ContractVersion`, `InvestmentCaseId` alanlarından oluşur; `TenantId`/`ActorId` server context'ten gelir ve free-text kind/discriminator yasaktır. Conditional approval wrapper exactly `ContractName`, `ContractVersion`, `InvestmentCaseContext`, `ApprovalOutcomeReference`; nested MOD-0023 tuple exactly `ContractName`, `ContractVersion`, `ApprovalOutcomeId`; relationship yalnız policy approval gerektirirse `0..1` ve non-selection'dır.
- **Gate I validation modes/errors — CLOSED PPM consumer baseline:** Closed modes `HistoricalResolve`, `NewReferenceEligibility` ve yalnız selection sözleşmelerinde `CurrentSelectionEligibility`'dir. Malformed caller request `400`; authentication/context `401`; permission/S2S entitlement `403`; missing/cross-tenant/invisible/non-disclosable `404`; visible retired/ineligible/stale/not-current reference `409`; unknown/unsupported version, timeout, unavailable provider veya malformed authoritative response `503` döner. Producer-specific response DTO'ları ortak DTO'ya zorlanamaz; consumer exact producer compatibility adapter'ı kullanır ve `IsReferenceable` boolean'ını tek başına authority sayamaz.
- **Conditional ApprovalOutcome boundary — NON-EXECUTABLE:** `InvestmentCaseApprovalOutcomeReferenceV1` yalnız `HistoricalResolve` ve `NewReferenceEligibility` destekler; `CurrentSelectionEligibility` `400` döner. MOD-0023 terminal truth, workflow/task/route ve MOD-0018-backed eligibility/delegation sahibidir; PPM yalnız opaque reference ve conditional `0..1` ilişkiyi saklar. Yeniden karar yeni MOD-0023 `WorkflowInstance` gerektirir; yeni outcome eski reference'ı otomatik değiştirmez. Replacement ayrı explicit, transactional ve audited MOD-0117 lifecycle mutation'ıdır. MOD-0023 amendment promotion + Gate 2 + bilateral fixtures + explicit runtime authority olmadan slice executable değildir.
- **PPM source attestation — governance baseline only:** Exact trusted-server-context profile `ppm.investment-case-source-attestation` / `1.0`; closed claims `ContractName`, `ContractVersion`, `TenantId`, `SubmittingServiceId`, `EffectiveActorId`, `DelegatedActorChain`, `InvestmentCaseId`, `WorkflowTemplateVersionId`, `SubmissionRequestDigest`, `IssuedAtUtc`, `ExpiresAtUtc`, `Nonce`. Browser/client payload değildir ve aggregate payload taşımaz. PPM tenant-first authoritative lookup sonrası üretir; missing/cross-tenant/invisible InvestmentCase için PPM `404` döner ve MOD-0023 çağrısı yapmaz. Signing, credential rotation, validity, nonce retention ve replay fixtures runtime blocker'dır; bu baseline implementation authority vermez.
- **Gate I staged authority:** Producer module-pack authoring Gate I-A, I-B ve I-C için paralel ilerleyebilir; herhangi bir MOD-0117 consumer adapter'ı ancak ilgili producer contract approved olduktan, MOD-0117 consumer amendment onaylandıktan ve açık runtime authority verildikten sonra executable olur. Logical routing fiziksel service placement değildir.
- Eski `DitenPPM` kodu yalnızca **iş kuralı referansıdır**; dosya/kod kopyalanmaz.

## Runtime Decisions

> Tüm domain modüllerine uygulanır. Engineering detayları için `.antigravity/rules/` linklerine bak; içerik burada tekrarlanmaz.

- **Shell:** Tenant modülleridir → `_LayoutTenantShell.cshtml`. Ref: [views-organization.md](../../../.antigravity/rules/views-organization.md)
- **Lokalizasyon:** Tenant tarafı → **7 dil** (en, fr, es, zh, ar, ru, tr) + `window.L10n` köprüsü. Eski UI'ın hardcoded metinleri devralınmaz. Ref: [dynamic-localization-standard.md](../../../.antigravity/rules/dynamic-localization-standard.md)
- **Kimlik/Tenant:** `userId`/`tenantId` asla client'tan alınmaz; JWT claim + tenant middleware. DTO'larda `TenantId` yasak; cross-tenant erişim 404. Ref: [multi-tenancy.md](../../../.antigravity/rules/multi-tenancy.md), [security-jwt.md](../../../.antigravity/rules/security-jwt.md)
- **Permission ailesi:** `ppm.work-records.*`, `ppm.tasks.*`, `ppm.workstreams.*`, `ppm.calendar.*`, `ppm.effort-logs.*`, `ppm.meeting-reports.*`, `ppm.reference-data.read` (❌ `ppm.workflows.*`).
- **Soft delete / audit:** `IsDeleted` + `DeletedAt` zorunlu (eski `Status` bool semantiği devralınmaz); AuditEvent v1 hizalaması (APP-PPM-BUNDLE) hedeftir. Ref: [entity-base-template.md](../../../.antigravity/rules/entity-base-template.md)
- **Id / Mongo:** GUID (subtype-4) — eski ObjectId string deseni devralınmaz; tenant-first compound index (ESR). Ref: [mongo-indexing.md](../../../.antigravity/rules/mongo-indexing.md)
- **Concurrency:** MeetingReport/MeetingInvite `Version` tabanlı optimistic concurrency davranışı korunur (taşıma sırasında sessiz veri kaybı yasağı).
- **Gateway:** Tüm frontend istekleri Gateway (5000) üzerinden; servis portu doğrudan çağrılamaz. Port rezervasyonu C1 module pack aşamasında `ports.md`'ye işlenir (bu scaffold'da yapılmadı). Ref: [ports.md](../../../.antigravity/rules/ports.md)
- **Dış entegrasyonlar:** Google/SignalR/AI/SMTP bu domain'in ilk fazlarında **yoktur**; ileride External Systems Register / MOD-0027 kalıplarıyla ele alınır.
- **External context provider:** MOD-0117, default-disabled
  `POST /internal/v1/ppm/external-context-references/validate` endpoint'inde
  `ppm.external-context-reference` `1.0` typed doğrulamasını sağlar. Endpoint browser/Gateway dışıdır;
  dedicated `Diten.ManagementGovernanceService` credential'ı ile framework-doğrulanmış delegated JWT'yi birlikte
  ister. ContextKind izinleri yalnızca mevcut exact `ppm.*.read` anahtarlarıdır. V1'de non-null
  `VisibilityPolicyKey` fail-closed `404` döner; object-level policy desteği MOD-0018 versioned contract ve ayrı
  provider revision/FU olmadan açılamaz.
- **Gate L permissions:** exact closed set `ppm.investment-cases.{read,create,update,change-lifecycle}` and
  `ppm.benefit-commitments.{read,create,update,change-lifecycle}`. `.view`, `.delete`, wildcard, alias and
  uppercase keys are forbidden; soft delete uses the existing update/surface contract.
- **Completion wording:** Gate L completion is only “1.3 PPM-owned local slice complete; external integration
  Gate I remains open.” Gate I-A completion is only “Decision-to-investment trace integrated; funding,
  scenario and realization slices remain open.” Only Gate L + Gate I-A + Gate I-B + Gate I-C + cross-service
  security/compatibility tests + integrated browser flow may yield “1.3 Portfolio, Investment & Value
  Management integrated business flow complete.” None automatically changes MOD-0117 to `done`.

## Domain Bootstrap Notes

- Teknik standartlar [AGENTS.md](../../../AGENTS.md) ve [.antigravity/rules/](../../../.antigravity/rules/) altından devralınır — burada tekrarlanmaz.
- Modül kimliği: parent `MOD-0117`; alt yetenekler `MOD-0117-FUxx` (preflight zorunlu, FU numarası uydurulamaz).
- Bu domain'in kuruluş kanıtları: Blueprint preflight (exit 0), [DCP-003](../../portfolio/delivery-capability-packs/DCP-003-ppm-work-management.md), migration feasibility + governance audit raporları (2026-07-07).
