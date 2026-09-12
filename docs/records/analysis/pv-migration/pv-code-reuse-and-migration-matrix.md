# PV Code Reuse & Migration Classification (E)

> Kategoriler: DIRECT_REUSE / ADAPTER_REUSE / BUSINESS_RULE_REIMPLEMENT / DATA_MIGRATION_ONLY / REFERENCE_ONLY / RETIRE.
> Zorluk: S/M/L/XL. Confidence: High/Medium/Low.

## Genel ilke

Legacy sistem **monolitik god-entity, endpoint-auth'suz, host-based tenant izolasyonlu, testsiz ve audit/e-sig içermeyen** bir yapıdır. Bu nedenle **kodun doğrudan taşınması yanlıştır**. Değer, kodda değil **iş kuralı vokabülerinde ve veride**dir.

## Bileşen bazında karar

### 1. Safety Report çekirdek
- Legacy kaynak: `DitenPvOrganization/.../SafetyReport.cs` (+ Create/Update/Delete handler, controller, JS)
- Hedef: ERP-vNext yeni **Safety Case** aggregate (Safety/Regulatory domain)
- Karar: **BUSINESS_RULE_REIMPLEMENT** (+ veri için DATA_MIGRATION_ONLY)
- Gerekçe: 90 alanlı düz entity DDD-uyumsuz; seriousness/expectedness bool, causality tek boyut, versioning/follow-up yapısal değil.
- Korunacak iş kuralı: alan seti, MA/case ilişkisi, causality/seriousness vokabülerleri, submission due-date mantığı.
- Korunmayacak: god-entity yapısı, string Reporter/AdverseReaction, string Version, minimal validasyon.
- Dönüşüm: Case aggregate + alt varlıklar (Patient, Reporter, ProductInvolvement, Reaction, Assessment, FollowUp, CaseVersion).
- Bağımlılık: MOD-0290 (product), MOD-0048 (reference), MOD-0288 (person).
- Risk: Yüksek (klinik/regülatör doğruluk). Zorluk: **XL**. Confidence: High.

### 2. Marketing Authorization (Registration)
- Kaynak: `MarketingAuthorization.cs` (QPPV, PSMF, ATC, `MaStatus 0-5`, re-registration)
- Karar: **BUSINESS_RULE_REIMPLEMENT** + DATA_MIGRATION_ONLY
- Korunacak: MA lifecycle durum makinesi, QPPV/PSMF kavramları, re-registration ilişkisi.
- Zorluk: **L**. Confidence: Medium.

### 3. Regulatory Report + Task
- Kaynak: `RegulatoryReport/RegulatoryReportTask`
- Karar: **REFERENCE_ONLY** (task board), gerçek görevler → **MOD-0024** Task/Checklist + **MOD-0023** Workflow.
- Gerekçe: Legacy'deki ad-hoc task board ERP-vNext'te yatay motorla karşılanmalı; yeniden üretme.
- Zorluk: **M**. Confidence: Medium.

### 4. LCPPV Monthly Reconciliation
- Kaynak: `LcppvMonthlyReconcilationController` + `DitenPvSurvey`
- Karar: **BUSINESS_RULE_REIMPLEMENT** (süreç) + REFERENCE_ONLY (anket motoru)
- Korunacak: aylık LCPPV kontrol süreci ve soru seti; **DB-mutabakatı olmadığı** netleştirilmeli.
- Zorluk: **M**. Confidence: Medium.

### 5. Organization / GlobalSku / PharmaceuticalForm / ActiveIngredient / Country / Authority
- Karar: **DATA_MIGRATION_ONLY** → hedef SoR: MOD-0288 (org/person), MOD-0290 (product/SKU), MOD-0048 (reference).
- Korunmayacak: `DitenPvLookup` (tek `Country` entity) — **RETIRE** (bounded context değil).
- Zorluk: **M**. Confidence: Medium.

### 6. User / Role / Auth
- Kaynak: `DitenPvUser` (JWT üretimi, PasswordHash, RoleIds)
- Karar: **RETIRE (auth altyapısı)** + DATA_MIGRATION_ONLY (kullanıcı kayıtları)
- Gerekçe: ERP-vNext AuthService + MOD-0018 RBAC + tenant-scoped token zaten mevcut ve daha güçlü. Legacy auth taşınmamalı.
- Kullanıcı/rol verisi → ERP-vNext kimlik modeline eşlenmeli (parola hash'leri taşınmaz; reset akışı).
- Zorluk: **M**. Confidence: High.

### 7. TenantResolutionMiddleware (host-based)
- Karar: **RETIRE.** ERP-vNext tenant-claim/JWT tabanlı izolasyon kullanmalı.
- Gerekçe: Host-based izolasyon güvenlik borcudur; server-to-server `localhost` çağrısı context taşımıyor.
- Confidence: High.

### 8. Frontend (cshtml + Pagejs)
- Karar: **REFERENCE_ONLY** (terminoloji/UX/alan yerleşimi). ERP-vNext kendi frontend standardını (Diten.Web) kullanır.
- Confidence: High.

### 9. Attachments (local disk)
- Karar: **RETIRE (depolama mekanizması)** + DATA_MIGRATION_ONLY (dosyalar → MOD-0028).
- Risk: Disk path erişilebilirliği; migration'da içerik + metadata birlikte taşınmalı.

### 10. SQL Server config / Flurl senkron kuplaj / CORS AllowAll
- Karar: **RETIRE.** Ölü/güvensiz/anti-pattern.

## Özet reuse dağılımı (ağırlıklı, kabaca)

| Kategori | Pay (yaklaşık) |
|---|---|
| DIRECT_REUSE | ~%0–5 |
| ADAPTER_REUSE | ~%5 (yalnızca migration okuma) |
| BUSINESS_RULE_REIMPLEMENT | ~%55–65 (asıl değer) |
| DATA_MIGRATION_ONLY | ~%50–70 (verinin çoğu, kalite şartlı) |
| REFERENCE_ONLY | UI + task board |
| RETIRE | auth/tenant/lookup/SQL/CORS/disk |

> Not: DIRECT_REUSE'un ~%0 olması kasıtlıdır — legacy kodun ERP-vNext mimarisine doğrudan taşınması **önerilmez**.
