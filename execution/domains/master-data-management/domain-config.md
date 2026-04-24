# Master Data Management — Domain Config

## Purpose
Master Data Management domain'i, ERP genelinde tekrar kullanılan kurumsal ana verinin ortak sözlüğünü, sahiplik sınırlarını ve modül bazlı yürütme çerçevesini tanımlar.

## In-Scope Modules
- `MDM-001` — System-of-Record & Ownership Registry (kaynak: MOD-0001)
- `MDM-002` — Interface Registry (kaynak: MOD-0002)
- `MDM-003` — Data Contract Registry (kaynak: MOD-0003)
- `MDM-004` — Policy & Control Library (kaynak: MOD-0005)
- `MDM-005` — Policy Exception / Waiver Register (kaynak: MOD-0006)
- `MDM-006` — Decision & Rationale Log (kaynak: MOD-0007)
- `MDM-007` — Enterprise Capability / Product Catalog (kaynak: MOD-0008)
- `MDM-008` — Tenant / Environment Management (kaynak: MOD-0009)
- `MDM-009` — Release Governance & Promotion (DEV/UAT/PROD) (kaynak: MOD-0010)
- `MDM-010` — Feature Flags & Configuration Profiles (kaynak: MOD-0011)
- `MDM-011` — Secrets & Configuration Vault (kaynak: MOD-0012)
- `MDM-012` — Platform Standards Registry (kaynak: MOD-0013)
- `MDM-013` — Module Boundary Registry (kaynak: MOD-0014)
- `MDM-014` — Reuse Enforcement Checks (kaynak: MOD-0015)
- `MDM-015` — SSO / MFA (kaynak: MOD-0017)
- `MDM-016` — RBAC / ABAC Authorization (kaynak: MOD-0018)
- `MDM-017` — Data Masking & Row/Field Security (kaynak: MOD-0019)
- `MDM-018` — Segregation of Duties (SoD) Controls (kaynak: MOD-0020)
- `MDM-019` — Audit Trail Service (kaynak: MOD-0021)
- `MDM-020` — Workflow Designer (Approvals/SLAs/Escalations) (kaynak: MOD-0023)
- `MDM-021` — Task & Checklist Engine (kaynak: MOD-0024)
- `MDM-022` — Business Rules Engine (kaynak: MOD-0025)
- `MDM-023` — Scheduler / Job Orchestration (kaynak: MOD-0026)
- `MDM-024` — Notification Service (Email/SMS/WhatsApp) (kaynak: MOD-0027)
- `MDM-025` — Document Management (Templates/Versioning) (kaynak: MOD-0028)
- `MDM-026` — Controlled Documents (SOPs/Work Instructions) (kaynak: MOD-0029)
- `MDM-027` — Records Management (Retention/Legal Hold) (kaynak: MOD-0030)
- `MDM-028` — Evidence Linking Service (object ↔ evidence) (kaynak: MOD-0031)
- `MDM-029` — API Gateway (kaynak: MOD-0032)
- `MDM-030` — API Consumer & Credential Management (Developer Portal) (kaynak: MOD-0033)
- `MDM-031` — Webhook Service (kaynak: MOD-0034)
- `MDM-032` — Event Bus / Message Queue (kaynak: MOD-0035)
- `MDM-033` — Integration Monitoring & Reconciliation (kaynak: MOD-0037)
- `MDM-034` — Event Taxonomy & Naming Standard (kaynak: MOD-0038)
- `MDM-035` — Schema Compatibility & Deprecation Policy (kaynak: MOD-0039)
- `MDM-036` — Canonical ID & Correlation Standard (kaynak: MOD-0040)
- `MDM-037` — Logging & Monitoring (kaynak: MOD-0041)
- `MDM-038` — Alerting & Incident Runbooks (kaynak: MOD-0042)
- `MDM-039` — SLO/SLA Monitoring (kaynak: MOD-0043)
- `MDM-040` — Backup & Restore (kaynak: MOD-0044)
- `MDM-041` — Reference Data Management (kaynak: MOD-0048)
- `MDM-042` — Master Data Management (MDM) (kaynak: MOD-0049)
- `MDM-043` — Metadata Catalog (kaynak: MOD-0052)
- `MDM-044` — Data Dictionary / Glossary (kaynak: MOD-0053)
- `MDM-045` — Data Product Registry (kaynak: MOD-0054)
- `MDM-046` — Customer 360 / Account Hierarchy (kaynak: MOD-0149)
- `MDM-047` — Contact & Relationship Management (kaynak: MOD-0150)
- `MDM-048` — Opportunity & Pipeline Management (kaynak: MOD-0153)
- `MDM-049` — Product Configuration (kaynak: MOD-0159)
- `MDM-050` — Order Capture (kaynak: MOD-0168)
- `MDM-051` — Allocation & ATP/CTP (kaynak: MOD-0172)
- `MDM-052` — Inventory Ledger & Valuation (kaynak: MOD-0173)
- `MDM-053` — Lot/Batch/Serial Tracking (kaynak: MOD-0174)
- `MDM-054` — Quarantine / Blocked Stock (kaynak: MOD-0175)
- `MDM-055` — Expiry / FEFO Management (kaynak: MOD-0176)
- `MDM-056` — Recall Readiness (kaynak: MOD-0177)
- `MDM-057` — Putaway / Picking / Packing (kaynak: MOD-0178)
- `MDM-058` — 3PL Integration (kaynak: MOD-0179)
- `MDM-059` — Wave Planning (kaynak: MOD-0180)
- `MDM-060` — Cycle Counting (kaynak: MOD-0181)
- `MDM-061` — Barcode/RFID (kaynak: MOD-0182)
- `MDM-062` — Shipment Tracking & POD (kaynak: MOD-0183)
- `MDM-063` — Carrier Management (kaynak: MOD-0184)
- `MDM-064` — Routing & Load Planning (kaynak: MOD-0185)
- `MDM-065` — Reverse Logistics (kaynak: MOD-0186)
- `MDM-066` — Claims Management (kaynak: MOD-0187)
- `MDM-067` — Demand Planning (kaynak: MOD-0188)
- `MDM-068` — MRP & Replenishment (kaynak: MOD-0189)
- `MDM-069` — Third-Party Risk Management (TPRM) (kaynak: MOD-0216)
- `MDM-070` — Contract Lifecycle Management (CLM) (kaynak: MOD-0217)
- `MDM-071` — Control ↔ Evidence Mapping (kaynak: MOD-0227)
- `MDM-072` — Automated Evidence Collection (kaynak: MOD-0228)
- `MDM-073` — Audit Pack Generator (kaynak: MOD-0229)
- `MDM-074` — Labeling Lifecycle (kaynak: MOD-0238)
- `MDM-075` — Country Requirements Matrix (kaynak: MOD-0239)
- `MDM-076` — HRIS (Workday/SuccessFactors/Oracle HCM) (kaynak: MOD-0251)
- `MDM-077` — ERP Core (SAP/Oracle/Dynamics) (kaynak: MOD-0252)
- `MDM-078` — PLM (Teamcenter/Windchill/3DEXPERIENCE) (kaynak: MOD-0253)
- `MDM-079` — Time & Attendance (UKG/Kronos) (kaynak: MOD-0280)

## Out-of-Scope
- Kimlik/yetki, tenant altyapısı ve platform seviyesindeki yatay servisler
- Strateji, KPI ve performans odaklı iş yönetimi modülleri
- MDM dışı domain'lere ait servis iç uygulama detayları

## Ownership Boundaries
- MDM modülleri yalnızca `master-data-management` domain kapsamındaki iş nesnelerini sahiplenir.
- Domain dışı yetenekler (PSS/ESBP) yalnızca entegrasyon sözleşmesi üzerinden tüketilir.
- Her yeni modül için tek bir module pack sorumluluğu tanımlanır.

## Shared Dependencies
- Kurumsal planlama Excel'i (`execution/modules_pages_planning_v3.xlsx`) ile modül envanteri hizalanır.
- Domain yürütmesi, repo kökündeki `AGENTS.md` otorite hiyerarşisine bağlıdır.
- Global standartlar `.antigravity/rules/` referans alınır, bu dosyada tekrar edilmez.

## Domain-Level Repo Scope
- `execution/domains/master-data-management/**`
- `services/Diten.MdmService/**`
- `frontend/Diten.Web/Areas/MDM/**` (modül geliştirmesi başladığında)
- `frontend/Diten.Web/Views/MDM/**` (modül geliştirmesi başladığında)

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.AuthService/**`
- `services/Diten.Platform/**`
- `services/Diten.EnterpriseStrategyService/**`

## Domain Bootstrap Notes
- Bu dosya bilinçli olarak teknik implementasyon detayı içermez.
- Teknik standartlar domain-config yerine global kural setlerinden devralınır.
