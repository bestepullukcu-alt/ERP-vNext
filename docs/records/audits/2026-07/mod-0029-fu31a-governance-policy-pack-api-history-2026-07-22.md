# MOD-0029-FU31A — Governance Policy Pack API & Application History

**Tarih:** 2026-07-22
**Kapsam:** FU31 seeder'ının API + kalıcı application-history ile tamamlanması (additive / non-destructive)
**Commit/push:** YOK — tüm değişiklikler working tree'de
**Final verdict:** **PASS**

---

## 1. Initial Audit Summary

FU31'de kurulan `DocumentGovernancePolicyPackSeeder` (42 default policy: 20 Retention + 10 GDocP + 12 Signature)
tenant-scoped, idempotent, create-missing-only çalışıyordu ama API yüzeyi ve kalıcı history yoktu. Audit'te
doğrulanan mevcut pattern'ler:

- **Repository:** `TenantRepository<T>` + `ExecutionFilter` (tenant scoping), `new CreateAsync` → base.
- **Reason codes:** feature başına `static class ...ReasonCodes` const string'ler.
- **Controller:** `CustomBaseController` + `IMediator` + `CreateActionResultInstance(...)` + `ICorrelationContext`.
- **Response:** `Response<T>.Success(data, statusCode, correlationId)` / `.Fail(error, statusCode, reasonCode, correlationId)`.
- **Index:** `collection.Indexes.CreateManyAsync(...)` + `CreateIndexOptions{Name=...}`; unique'ler
  `PartialFilterExpression IsDeleted=false` ile.
- **Permission:** FU29A sonrası `[HasPermission(<FeaturePermissions>.<Key>)]`. FU29 seed'inde
  **governance-policy-pack için adanmış key YOK** → en yakın seeded retention key'leri kullanıldı (yeni unseeded
  key icat edilmedi).

## 2. Application History Model

**Yeni aggregate** `DocumentGovernancePolicyPackApplication : TenantScopedEntity` (append-only):
PackKey/PackName/PackVersion/SopReference, `ApplicationStatus`, AppliedAt, AppliedByUserId/AppliedByRole,
CreatedPolicyCount / SkippedExistingCount / ConflictCount, WarningMessages, ConflictMessages,
Created{Retention,GDocP,Signature}PolicyIds, Created/Skipped/ConflictPolicyKeys, PreviewOnly, CorrelationId.

**Yeni enum** `DocumentGovernancePolicyPackApplicationStatus`: `Applied` / `AppliedWithWarnings` / `Failed`.

## 3. Repository / Index Changes

- **Interface** `IDocumentGovernancePolicyPackApplicationRepository`: CreateAsync, GetByIdAsync,
  GetAllForTenantAsync, GetLatestByPackKeyAsync. **Delete/Update yok** (append-only).
- **Impl** `DocumentGovernancePolicyPackApplicationRepository : TenantRepository<...>`, collection
  `document_management_governance_policy_pack_applications`.
- **Index (additive, 3):** `(TenantId, PackKey, AppliedAt desc)`, `(TenantId, PackVersion)`,
  `(TenantId, ApplicationStatus)`. **PackKey'de unique index YOK** — pack idempotent olarak tekrar uygulanabilir
  ve her çalıştırma ayrı bir denetim satırıdır.
- DI: Infrastructure'a repository, Application'a servis kaydı (additive birer satır).

## 4. Seeder Integration — **Option 2 seçildi**

FU31 `DocumentGovernancePolicyPackSeeder` **saf bırakıldı** (constructor değişmedi) → 18 FU31 testi olduğu gibi
yeşil kaldı ve seeder history store olmadan da kullanılabilir durumda. History yazımı yeni
`DocumentGovernancePolicyPackApplicationService` içinde: seeder çağrılır, sonuç history aggregate'ine map edilir
ve persist edilir. **Preview hiçbir şey yazmaz** (ne policy ne history).

Status mapping: conflict>0 veya warning>0 → `AppliedWithWarnings`; aksi halde `Applied`; exception →
best-effort `Failed` history satırı + 500 `GOVERNANCE_POLICY_PACK_APPLY_FAILED`.

## 5. API / Command Changes

**Controller** `DocumentManagementGovernancePolicyPackController`
(`/api/v1/document-management/governance-policy-pack`):

| Endpoint | Permission (FU29 seeded) |
|---|---|
| `GET default/preview` | `platform.document-management.retention.view` |
| `POST default/apply` | `platform.document-management.retention.manage` |
| `GET applications` | `platform.document-management.retention.view` |
| `GET applications/{id}` | `platform.document-management.retention.view` |

MediatR: `PreviewGovernancePolicyPackQuery`, `ApplyGovernancePolicyPackCommand`,
`GetGovernancePolicyPackApplicationsQuery`, `GetGovernancePolicyPackApplicationByIdQuery` + ince handler'lar.
DTO'lar: Preview (pack meta + total/family/existing/missing/conflict + warnings + definition summary), Apply
(ApplicationId + status + counts + key listeleri), Applications list/detail.

**Reason codes:** `GovernancePolicyPackReasonCodes` — PackNotFound, ApplicationNotFound, ApplyFailed,
TenantRequired, ConflictsDetected, PreviewFailed.

**Gateway:** değişiklik **gerekmedi** — FU30'daki `/api/v1/document-management/{everything}` catch-all bu yeni
route ailesini zaten kapsıyor.

## 6. Permission Attribution

FU29 seed'inde adanmış governance-policy-pack key'i olmadığından FU29A kuralı gereği en yakın seeded key'ler
kullanıldı; **unseeded key icat edilmedi**. Gelecek öneri (ayrı AuthService seed FU'su):
`platform.document-management.governance-policy-pack.view` / `.apply` / `.manage`.

## 7. Idempotency / Conflict Behavior

- Preview: saf hesaplama, sıfır yazma (test ile create/update çağrı sayısı 0 doğrulandı).
- Apply: yalnızca eksik policy'leri create eder; mevcut key skip; divergent core alan → **Conflict**, overwrite
  yok; her çalıştırma **yeni** history satırı (2. run: 0 created / 42 skipped).
- Cross-tenant detail → 404 (varlık sızıntısı yok). Bilinmeyen id → 404. Tenant yoksa → 400 TenantRequired.
- Hiçbir `UpdateAsync` çağrılmaz; retention subject evaluate edilmez; subject state mutate edilmez.

## 8. Tests Added

**Yeni:** `DocumentGovernancePolicyPackApplicationTests.cs` — **16 test**: preview no-history/no-policy/no-mutating
calls, preview missing/existing/conflict sayıları, apply history + counts + key listeleri, 2. run idempotent +
yeni history, existing overwrite edilmiyor, conflict → AppliedWithWarnings (+ConflictMessages), history list,
detail full key lists, cross-tenant blocked, unknown id 404, tenant required, apply mutate etmiyor, controller
permission attribution (preview/list/detail → RetentionView; apply → RetentionManage).

## 9. Build / Test Results

- Platform API build → **0 hata**.
- FU31 + FU31A hedefli testler → **34 başarılı / 0 başarısız**.
- Full Platform Application suite → **1860 başarılı / 0 başarısız** (1844 + 16 yeni; regresyon yok).
- AuthService / Gateway test **N/A** — dokunulmadı.

## 10. Remaining Gaps

1. **Adanmış permission key'leri** seed edilmedi (retention key'leri reuse ediliyor) — ayrı AuthService seed FU'su.
2. **Transaction yok**: apply sırasında exception olursa daha önce oluşturulan policy'ler kalır (additive ve
   zararsız); `Failed` history satırı best-effort yazılır. Distributed transaction altyapısı mevcut değil.
3. **AppliedByRole** null — `ICurrentUserContext` rol bilgisi taşımıyor.
4. `IAuditableCommand` audit wiring eklenmedi (mevcut DM feature'larında da bu komut tipi için kullanılmıyor).
5. FU31'den devreden: Quality/External/Repository default'ları hâlâ code-based (persisted config yok);
   LegalHold/Disposition retention subject-type `Other + RetentionClass` ile modelli.

## 11. Guardrail Confirmations

- ✅ FU31 manifest/seeder davranışı rewrite edilmedi (seeder constructor'ı değişmedi, 18 FU31 testi yeşil)
- ✅ Existing policy overwrite yok · existing tenant data silinmedi/değişmedi · hard delete / purge yok
- ✅ Subject evaluation / state mutation yok · scheduler yok · e-sign provider / certificate validation yok
- ✅ Compliance claim yok · external QMS API yok · MOD-0023 workflow entegrasyonu yok
- ✅ AuthService seed değişmedi · Gateway değişmedi · Frontend değişmedi · MOD-0028 mutation yok
- ✅ raw bytes yok · direct 5057 yok · client TenantId / X-Tenant-Id yok (server-resolved) · Commit/push yok

## Final Verdict: **PASS**

FU31'in API + persistence gap'i kapatıldı: append-only application-history aggregate + repository + index,
preview/apply/list/detail endpoint'leri, reason code'lar ve 16 test teslim edildi; seeder saf bırakıldığından
FU31 davranışı ve testleri bozulmadı; full suite (1860) yeşil.
