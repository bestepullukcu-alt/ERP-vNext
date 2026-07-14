# Portfolio Delivery (PPM) — Domain Config

> Bu dosya domain'in **sınırlarını ve kararlarını** tanımlar. Engineering NASIL kuralları [.antigravity/rules/](../../../.antigravity/rules/)'da; modül envanteri [execution/portfolio/master-development-plan.md](../../portfolio/master-development-plan.md)'de; capability-level sözleşme [DCP-003](../../portfolio/delivery-capability-packs/DCP-003-ppm-work-management.md)'tedir.

## Purpose

Portfolio Delivery (PPM) domain'i; Work Records (proje/iş kayıtları), PPM Task instance'ları, workstream hiyerarşisi, proje takvim planlaması, Project Effort Log ve Meeting / Status Reports yeteneklerini sahiplenir. Blueprint-kanonik kimliği `MOD-0117 — Project & Portfolio Management (PPM)`'dir. Eski `DitenPPM` sisteminin paritesinin güvenli alt kümesi hedeflenir; Blueprint'in tam MOD-0117 kapsamı (demand intake, benefits, capacity planning vb.) bu domain'in aktif kapsamı değildir.

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
- `services/Diten.PpmService/**` — ⚠️ henüz mevcut değil; yalnızca C1 module pack `approved`/`ready-for-dev` + açık kullanıcı onayı sonrası oluşturulur
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
- **MOD-0007 (Decision & Rationale Log):** Meeting/Status Report içindeki Decision kayıtlarının kalıcı SoR'u MOD-0007'dir; ilk fazlarda PPM-yerel tutulur, link kontratı follow-up'tır.
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

## Domain Bootstrap Notes

- Teknik standartlar [AGENTS.md](../../../AGENTS.md) ve [.antigravity/rules/](../../../.antigravity/rules/) altından devralınır — burada tekrarlanmaz.
- Modül kimliği: parent `MOD-0117`; alt yetenekler `MOD-0117-FUxx` (preflight zorunlu, FU numarası uydurulamaz).
- Bu domain'in kuruluş kanıtları: Blueprint preflight (exit 0), [DCP-003](../../portfolio/delivery-capability-packs/DCP-003-ppm-work-management.md), migration feasibility + governance audit raporları (2026-07-07).
