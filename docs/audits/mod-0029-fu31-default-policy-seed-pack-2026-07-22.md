# MOD-0029-FU31 — Default Policy / Seed Pack

**Tarih:** 2026-07-22
**Kapsam:** SOP-uyumlu, tenant-scoped default governance policy seed pack (additive / non-destructive)
**Commit/push:** YOK — tüm değişiklikler working tree'de
**Final verdict:** **PASS_WITH_GAPS**

---

## 1. Initial Audit Summary

FU06–FU23 policy entity'leri + evaluator'ları kuruldu ama tenant bootstrap'ında SOP-uyumlu minimum policy seti
otomatik oluşmuyor; ekranlar boş policy evreniyle başlıyor ve evaluator'lar safe-default'a düşüyor. Audit'te
üç kalıcı policy ailesinin (Retention/FU15, GDocP/FU21, Signature/FU23) tam yapısı çıkarıldı:

- **DocumentRetentionPolicy** (`TenantScopedEntity`, `required PolicyKey/PolicyName`, settable alanlar).
- **DocumentGDocPCorrectionPolicy** (aynı desen; `FieldPathPattern` glob; most-restrictive-wins).
- **DocumentSignaturePolicy** (aynı desen; `AllowedRepositoryTypes`; `RequiresSecondFactor` FU23'te
  karşılanamaz → talep eden policy imzalamayı bloklar).
- Üç repo da `CreateAsync` + `GetByKeyAsync` (tenant-scoped uniqueness) + `GetAllForTenant` sunuyor.
- Construction deseni (FU15 service): `TenantGuard.RequireTenant(_tenantContext)` (server-side tenant, client
  TenantId yok) + entity init + `GetByKeyAsync` idempotency + key `.Trim().ToUpperInvariant()`.
- `BaseEntity`: `Guid Id { get; init; }`, `CreatedAt`, `CreatedBy`.

## 2. Default Policy Pack Model

Yeni (Application katmanı, `Features/DocumentManagementGovernancePolicyPack/`):
- **GovernancePolicyPackModels.cs** — manifest record'ları (`RetentionPolicyDefinition`,
  `GDocPPolicyDefinition`, `SignaturePolicyDefinition`), `PolicyPackItemStatus`,
  `PolicyPackItemOutcome`, `GovernancePolicyPackApplicationResult` (created/skipped/conflict count + warnings
  + created ids).
- **DocumentGovernancePolicyPackManifest.cs** — statik SOP-uyumlu default tanımlar (SSOT).
- **DocumentGovernancePolicyPackSeeder.cs** — `PreviewDefaultPolicyPackAsync` + `ApplyDefaultPolicyPackAsync`.

## 3–4–5. Default Policies (42 tanım)

- **Retention (20):** controlled document (retain-while-effective + 10y), version 10y, master register &
  identifier ledger permanent, approval/release/training/review/impact/GDocP/signature/quality/downtime/variant
  evidence 10y, legal-hold permanent-while-active, disposition 10y. (`RetentionSubjectType`'ta LegalHold /
  DispositionRequest üyesi olmadığından `Other + RetentionClass` ile modellendi — bkz. gaps.)
- **GDocP (10):** timestamp (backdating-sensitive), status, evidence-reference, reconstruction &
  data-integrity (deviation-for-high-risk), approved/effective metadata, legal-hold & signature (allow-after
  approval/effective=false), retention-disposition. Hepsinde reason+evidence+review required.
- **Signature (12):** approval/QA-GQD, release-gate, training ack/effectiveness, GDocP review, deviation
  closure, CAPA completion/effectiveness, repository-assessment, legal-hold release, disposition, temporary
  issue. Hepsinde meaning+fingerprint+manifestation+repository-assessment required; **second factor asla
  required değil**; allowed repos = ValidatedDms/ApprovedInterim/SeparateApproval (**UnapprovedRepository yok**);
  interim boundary statement eklendi (compliance claim yok).

## 6–7. Quality/CAPA, External Monitoring, Repository Boundary Defaults

Bu üç alan için kalıcı config/policy persistence modeli yok. Task'ın kendi yönergesi gereği ("if no
persistence model exists, do not add a new config engine; report as code-based defaults") **yeni config engine
eklenmedi** — mevcut evaluator/mapper davranışları korundu. Bu FU31 kapsamında **deferred/reported** (bkz. gaps).

## 8. Seeder Behavior — Idempotency / Conflict

- Tenant-scoped (`TenantGuard.RequireTenant`; unresolved → `InvalidOperationException`).
- Idempotent: mevcut `PolicyKey` skip (create-missing-only).
- Non-destructive: mevcut key ama core alanlar farklı → **Conflict** (warning + count), **overwrite yok**.
- Seeded policy'ler **Active** (baseline hemen evaluator'larca kullanılabilir — pack'in amacı bu).
- Apply hiç `UpdateAsync` çağırmaz, subject evaluate etmez, mevcut kaydı mutate etmez, permission/rol vermez,
  workflow/signature/CAPA event üretmez.
- Result DTO: PackKey/Version, TenantId, ApplicationStatus (Preview/Applied/AppliedWithWarnings), created/
  skipped/conflict counts, warnings, created ids per family, per-item outcomes.

## 9. API / Command Changes

Bu pass'te API endpoint (preview/apply/applications) **eklenmedi** — seeder DI'a kaydedildi
(`DocumentGovernancePolicyPackSeeder`) ve command/API üzerinden apply edilebilir hale hazır. Endpoint + kalıcı
application-history aggregate takip işi olarak raporlandı (bkz. gaps). Önerilen permission: preview →
`platform.document-management.retention.view`, apply → `platform.document-management.retention.manage`
(FU29 seed'de mevcut); ileride adanmış `platform.document-management.governance-policy-pack.manage` önerilir.

## 10. Tests Added

**Yeni:** `DocumentGovernancePolicyPackTests.cs` (Platform.Application.Tests) — **18 test**, in-memory fake
repo/tenant/user. Kapsam: preview sections (yazma yok), apply creates all families (20/10/12, Active),
idempotency, skip-existing, conflict-without-overwrite, retention controlled-doc retain-while-effective+10y,
identifier ledger permanent, signature-record 10y, GDocP timestamp evidence/review/backdating, GDocP
reconstruction deviation-for-high-risk, signature meaning/fingerprint/manifestation, signature no
UnapprovedRepository, signature no second factor, result summary, tenant-scope + cross-tenant isolation,
unresolved-tenant throws, apply-never-calls-update, RetentionSubjectType ordinal stability.

## 11. Build / Test Results

- Platform Application build → **0 hata** (warning'ler pre-existing).
- FU31 testleri → **18 başarılı / 0 başarısız**.
- Full Platform Application suite → **1844 başarılı / 0 başarısız** (1826 + 18 yeni; regresyon yok).
- AuthService / Gateway test **N/A** — o servislere dokunulmadı.

## 12. Remaining Gaps

1. **API endpoints** (preview/apply/applications list/detail) — seeder hazır & DI'lı; ince controller +
   permission wiring takip işi.
2. **Persisted application history aggregate** — şu an sonuç DTO'da; kalıcı sidecar aggregate eklenmedi.
3. **Quality/Deviation/CAPA, External monitoring, Repository boundary** default'ları — kalıcı config modeli
   olmadığından code-based bırakıldı; persistence gerekiyorsa ayrı FU.
4. **Legal hold / Disposition retention** subject-type — `RetentionSubjectType` bu üyeleri içermediğinden
   `Other + RetentionClass` ile modellendi; enum genişletmesi (ordinal shift riski) ayrı iş.
5. **GDocP immutable-field disallow** (SignedAt/CreatedAt/Id/TenantId) — model per-field disallow
   ifade edemiyor; en-kısıtlayıcı mevcut kontrol (allow-after-approval/effective=false) uygulandı.

## 13. Guardrail Confirmations

- ✅ AuthService seed değişmedi · Gateway değişmedi · Frontend değişmedi · MOD-0028 mutation yok
- ✅ Existing policy overwrite yok · existing tenant data silinmedi/değişmedi · hard delete / purge yok
- ✅ Subject evaluation / state mutation yok (apply Update çağırmaz) · scheduler yok · e-sign provider /
  certificate validation yok · compliance claim yok · external QMS API yok
- ✅ raw bytes yok · direct 5057 yok · client TenantId / X-Tenant-Id yok (server-resolved) · Commit/push yok

## Final Verdict: **PASS_WITH_GAPS**

SOP-uyumlu default governance policy pack manifest (42 tanım) + idempotent, non-destructive, tenant-scoped
seeder (preview/apply) + 18 test teslim edildi ve full suite (1844) yeşil. API endpoint'leri, kalıcı
application history ve Quality/External/Repository persisted config'i açıkça takip işi olarak raporlandı.
