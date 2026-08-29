# MOD-0029-FU29 — Permission / RBAC Seed Hardening

**Tarih:** 2026-07-22
**Kapsam:** AuthService permission catalog/seed hardening (additive / non-destructive)
**Commit/push:** YOK — tüm değişiklikler working tree'de
**Final verdict:** **PASS_WITH_GAPS**

---

## 1. Initial Audit Summary

MOD-0029 Document Control governance zinciri (FU06–FU23) backend olarak tamamlandı. Ancak FU14–FU23
arası eklenen governance endpoint'lerinin çoğu, kendi adanmış permission key'leri **kod sabiti olarak
tanımlı olmasına rağmen**, runtime'da genel `controlled-documents.view/create` key'lerini reuse ediyor.
Adanmış key'ler ne controller'da enforce ediliyor ne de AuthService seed catalog'unda mevcut.

Örnek (kanıt): `DocumentManagementRetentionController.cs` — `legal-holds/{id}/release`,
`disposition-requests/{id}/execute-marker`, `retention-policies/{id}/retire` dahil **tüm** yazma
işlemleri `ControlledDocumentsPermissions.ControlledDocumentsCreate` ile korunuyor. Controller'ın kendi
doc-comment'i de bunu itiraf ediyor: *"Layer 1 RBAC REUSES the seeded controlled-documents view/create
keys … dedicated DocumentRetentionPermissions keys … should be seeded in a later hardening FU."*

## 2. Existing Permission Model Summary

- **Permission entity** (`Permission.cs`): `Key = "{module}.{resource}.{action}"` (lowercase).
  `Scope` = `PlatformAdmin` iff `module ∈ {platform, reference-data}`, aksi halde `Tenant`.
- **DefaultRolePermissionTemplate.SelectFor**:
  - `SuperAdmin` → full catalog (her key).
  - Tenant `Admin` → yalnızca `AdminModules` (access-governance, legal-entity, crm-account, crm-contact)
    Tenant-scoped key'leri + curated `TenantSelfServicePermissions` (tenant-security/navigation).
  - Tenant `Viewer` → yalnızca Tenant-scoped `read` action'ları (self-service hariç).
  - **Escalation boundary:** `platform.*` (PlatformAdmin scope) key'leri tenant Admin/Viewer'a ASLA
    otomatik verilmez.
- **Seed** (`DataSeeder.cs`): key'ler idempotent insert (Key existence check); startup'ta Module/Scope/
  casing reconcile'ları var.
- **Runtime self-registration**: `DocumentManagementManifestProvider` yalnızca FU01–FU05 sayfa/aksiyon
  key'lerini kapsıyor; FU06–FU23 governance key'leri **hiçbir manifest'te yok** → catalog'a yalnızca
  static seed üzerinden girebilirler.

## 3. New Permission Key Inventory (69 key — additive)

Naming: task'ın önerdiği taslak (ör. `legal-holds.release`) yerine **kodun gerçek sabitleri** SSOT
alındı (ör. `legal-hold.release`) — enforcement/catalog drift'ini önlemek için. Hepsi `module=platform`
→ **PlatformAdmin scope**.

| FU | Alan | Key sayısı |
|----|------|-----------|
| FU06 | master-register (view/manage/link/audit.view) | 4 |
| FU07 | identifiers (view/allocate/reserve/cancel) | 4 |
| FU08/08A | master-register.lifecycle (view/manage) | 2 |
| FU09 | master-register.approval (view/manage/evidence.record) | 3 |
| FU10 | master-register.release-gate (view/evaluate/evidence.record) | 3 |
| FU11 | master-register.training (view/manage/verify) | 3 |
| FU12 | master-register.periodic-review (view/manage/approve-extension/escalation.view) | 4 |
| FU13 | master-register.suspension (view/manage/approve) + retirement.approve | 4 |
| FU14 | external-documents (view/manage/monitoring.record/impact.manage) | 4 |
| FU15 | retention (view/manage), legal-hold (view/manage/release), disposition (manage/approve) | 7 |
| FU16 | repository-assessment (view/manage/approve) | 3 |
| FU17 | master-register.controlled-copy (view/manage/reconcile) | 3 |
| FU18 | template-variants.localization (view/manage) + translation-review.record + local-approval.record | 4 |
| FU20 | downtime (view/manage/temporary-issue/reconcile) | 4 |
| FU21 | gdocp-corrections (view/record/review) + gdocp-correction-policies.manage | 4 |
| FU22 | quality-events (view/manage), deviations (view/manage), capa (view/manage), quality-bridge.manage | 7 |
| FU23 | signatures (view/request/sign/verify/invalidate) + signature-policies.manage | 6 |
| **Toplam** | | **69** |

## 4. Seed Implementation Summary

`DataSeeder.SeedPermissionsAsync` içine, FU04 access blokunun ardına 69 additive `Permission` girişi
eklendi (module `"platform"`). Idempotent insert mevcut Key-existence check ile korunuyor. Mevcut hiçbir
key kaldırılmadı/yeniden adlandırılmadı. Startup Module/Scope reconcile'ları yeni key'leri de kapsıyor.

## 5. Default Role Behavior Summary

- Yeni 69 key **PlatformAdmin scope** → yalnızca `SuperAdmin` (full catalog) alır.
- Tenant `Admin` ve `Viewer` **hiçbirini** otomatik almaz (escalation boundary korunur).
- `IsTenantAssignable` → yeni key'ler için `false` (manuel tenant-assign de reddeder).
- Testlerle doğrulandı (aşağı bkz.).

## 6. Endpoint Attribution Audit — enforcement gap

Aşağıdaki kritik yüksek-yetki işlemleri **hâlâ** genel `controlled-documents.view/create` ile korunuyor;
adanmış key seed'e eklendi ama controller attribution'ı FU29 kapsamı dışında (runtime authorization
rewrite hard-boundary). Follow-up FU'da koordineli geçiş önerilir:

| Endpoint | Şu anki enforce | Hedef key (artık seed'de) |
|----------|-----------------|---------------------------|
| `legal-holds/{id}/release` | controlled-documents.create | legal-hold.release |
| `disposition-requests/{id}/execute-marker` | controlled-documents.create | disposition.manage |
| `retention-policies/{id}/activate\|retire` | controlled-documents.create | retention.manage |
| GDocP correction review/reject | (reuse) | gdocp-corrections.review |
| Quality event close/cancel | (reuse) | quality-events.manage |
| Deviation close/cancel | (reuse) | deviations.manage |
| CAPA effectiveness/close/cancel | (reuse) | capa.manage |
| Signature sign/verify/invalidate | (reuse) | signatures.sign/verify/invalidate |
| Downtime temporary-issue approve/reconcile | (reuse) | downtime.temporary-issue / .reconcile |
| External impact complete | (reuse) | external-documents.impact.manage |

## 7. Runtime Authorization Changes

**YOK.** Controller attribute'ları değiştirilmedi. Gerekçe: (a) hard boundary "controller behavior /
runtime authorization rewrite etme"; (b) key'ler PlatformAdmin-scoped olduğundan attribute'u yeni key'e
geçirmek, bugün genel key üzerinden erişen tenant aktörleri (ör. tenant-97c5 manual doc-mgmt grant) için
koordineli grant değişikliği gerektirir → gerçek davranış değişikliği, FU29 kapsamı dışı. Task talimatı:
*"Eğer runtime değişiklik riskliyse: seed + raporla bırak."*

## 8. Tests Added / Updated

**Yeni:** `Mod0029Fu29PermissionSeedHardeningTests.cs`
- Her 69 governance key seed'de mevcut (MemberData theory)
- `platform.document-management.` prefix uyumu
- resource+action mapping (Key kompozisyonu)
- SuperAdmin tüm governance key'lerini alır
- Tenant Admin / Viewer hiç almaz + `IsTenantAssignable=false` + PlatformAdmin scope
- Seed catalog'da **duplicate key yok** (tüm katalog parse edilerek)
- Kritik işlemler (legal-hold.release, disposition.manage, gdocp review, quality/deviation/capa manage,
  signatures sign/verify/invalidate, downtime temporary-issue/reconcile, external impact, retention manage)
  seed'de mevcut
- Mevcut doc-management key regresyon örneklemi hâlâ seed'de
- Governance key'lerinin display+description'ları boş değil

**Güncellenen (yalnızca test path resolver — assertion değişmedi):**
`DocumentManagementPermissionSeedTests.cs`, `UserLookupValidationSeedTests.cs` — `-o` alternate output
altında da çalışabilmeleri için location-independent DataSeeder.cs resolver fallback'i eklendi (fleet bin
kilidi nedeniyle in-place build engelli olduğundan).

## 9. Build / Test Results

- `dotnet build Diten.AuthService.API` → **0 hata** (fleet bin kilidi nedeniyle `-o .tmp/...` ile).
- `dotnet test Diten.AuthService.Application.Tests` (full) → **452 başarılı, 0 başarısız**.
- Platform build/test **çalıştırılmadı** — Platform shared constants / authorization attribute
  değiştirilmedi (gerekmiyor).

## 10. Remaining Gaps

1. Controller attribution switch (Bölüm 6) — ayrı, koordineli runtime authorization FU'su gerekli.
2. Yeni governance key'leri tenant governance kullanıcılarına ulaştırmak için (gerekliyse) grant
   stratejisi (module-grant / manual grant) ayrı iş.
3. Manifest/self-registration henüz governance key'lerini kapsamıyor (UI görünürlüğü/aksiyon gating).

## 11. Guardrail Confirmations

- ✅ Gateway/ocelot değişikliği YOK
- ✅ Frontend değişikliği YOK
- ✅ MOD-0028 baseline lifecycle mutation YOK
- ✅ Controller / business validation / aggregate behavior rewrite YOK
- ✅ Raw bytes Mongo write YOK · hard delete YOK · direct 5057 YOK · client TenantId/X-Tenant-Id YOK
- ✅ Yeni workflow/e-sign/provider/CAPA behavior YOK
- ✅ Permission key duplicate YOK (test ile doğrulandı)
- ✅ Commit/push YOK

## Final Verdict: **PASS_WITH_GAPS**

Seed hardening tamam ve testlerle doğrulandı; endpoint attribution gap'i açıkça raporlandı ve adanmış
key'ler koordineli follow-up switch için hazır bırakıldı.
