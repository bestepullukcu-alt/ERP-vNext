# MOD-0151 FU05 — Account Assignment Apply + History Implementation

> Tarih: 2026-07-28 · Tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`  
> Verdict: **PARTIAL** — code/build/test/UI PASS; Gateway canlı apply zinciri matched preview row olmadığı için sınırlı.

## 1. Preflight

MOD-0151 pack `FU05-account-assignment-apply-history` runtime scope'u doğrulandı. FU02B/FU03/FU04 raporları ve
mevcut Territory/Account read seam'leri incelendi. Account/Contact SoR mutation ve FU06–FU09 sınırları kapalı tutuldu.

## 2. Scope Confirmation

Yalnız ayrı `AccountTerritoryAssignment`, apply/history/current coverage, conflict/override, contract, tenant-shell
UI, test ve smoke yüzeyleri açıldı. RBAC seed/grant, MOD-0048 publish, Gateway config, Account/Contact/resource
assignment, workflow/evidence/import-export/Brand/Product/visit-route kapsamları değiştirilmedi.

## 3. Data Model Summary

`AccountTerritoryAssignment`: tenant/account/model/node kimlikleri ve display snapshot'ları; BusinessScopes;
source/status; effective window; preview/rule provenance; conflict policy/override reason; created/updated/ended
metadata. Collection: `account_territory_assignments`. Model/account history ve current coverage indexleri eklendi.

## 4. API Summary

| Method | Endpoint | Behavior |
|---|---|---|
| GET | `/api/crm/territory-models/{id}/account-assignments` | Model history |
| GET | `/api/crm/territory-models/{id}/account-assignments/{assignmentId}` | Detail |
| POST | `/api/crm/territory-models/{id}/assignment-preview/apply` | Active-model, transactional apply |
| POST | `/api/crm/territory-models/{id}/account-assignments/{assignmentId}/end` | End without delete |
| GET | `/api/crm/accounts/{id}/territory-assignments` | Account history |
| GET | `/api/crm/accounts/{id}/territory-coverage-summary` | Effective-at current coverage |

## 5. Apply Behavior

Active model, active/effective node, tenant-owned account, model-contained business scope, valid date window and
published status/source/conflict values required. Bütün satırlar yazmadan önce doğrulanır. Mongo transaction,
override end işlemleri ile yeni kayıt insertlerini tek commit altında all-or-nothing uygular.

## 6. History Behavior

Eski kayıt silinmez. Override/end işlemi status=`ended`, EffectiveTo/EndedAt/UpdatedAt ile kapanır. Yeni assignment
yeni Id ile açılır. Ended/expired/future kayıtlar history'de kalır.

## 7. Conflict / Override Behavior

Örtüşen active account + business scope + effective window default 409 üretir ve yazma yapmaz. Override reason
zorunludur; reason yoksa 400. Reason varsa önceki kayıt transaction içinde kapanır ve yenisi açılır.

## 8. Coverage Summary Behavior

Account master'a alan veya update eklenmedi. Ayrı query, effective-at anında yalnız active ve tarih aralığı geçerli
assignment'ları döndürür; future/ended/expired kayıtları current dışında bırakır.

## 9. Contract Flags

Apply/history/coverage summary/resource/rules/preview true; workflow activation ve approval trace false.
RuntimeScope FU05 ile güncellendi.

## 10. UI Summary

Details sayfasına selected preview checkbox'ları, active-model Apply paneli, effective dates, conflict/override
warning/reason ve DataTable v2 history listesi eklendi. Frontend yalnız Gateway proxy kullanır. Workflow, evidence,
import/export, Brand/Product ve visit/route kontrolleri eklenmedi. RESX parity: **165 anahtar × 7 dil**.

## 11. Tests

| Suite | Result |
|---|---|
| CrmService API build | PASS, 0 warning/error |
| Territory tests | PASS 172/172 |
| Full CrmService tests | PASS 341/341 |
| Web isolated-output build | PASS, 0 error (14 önceden var olan nullable warning) |
| JS syntax | PASS |
| RESX parity | PASS 165 × 7 |

Testler active apply, non-active rejection, persistence, Account snapshot değişmezliği, overlap 409/all-or-nothing,
override reason, override history, future current exclusion, account history ve tenant isolation'ı kapsar.

## 12. Live Smoke

Gateway/Web/Auth/CRM/Mongo ayaktaydı; CRM ve Web yeni build ile launch profile üzerinden yeniden başlatıldı. Mevcut
authenticated Chrome tenant oturumunda Territory list/detail, FU05 Apply paneli/history listesi, workflow/evidence
yokluğu ve Gateway-backed preview doğrulandı. Mevcut `DENEME` modeli kontrollü lifecycle API ile active yapıldı,
preview çalıştırıldı; modelde rule olmadığı için matched row `0` ve apply/duplicate/override/end zinciri
çalıştırılamadı. Model operasyonel bırakılmadı, API ile `inactive` yapıldı. Direct 5061 business API, Mongo hand-edit,
hard delete ve TenantId payload kullanılmadı.

## 13. Guard Checks

Account/Contact/resource assignment mutation: No. Workflow/evidence/import-export/Brand/Product/visit-route: No.
Hard delete: No. Forbidden permission: No. RBAC seed: No. MOD-0048 publish: No. Gateway config: No. TenantId payload:
No. Direct 5061 business API: No. History preserved: Yes. Workflow flag false: Yes.

## 14. Created / Updated Files

Domain entity/repository; application AccountAssignments contracts/handlers; Mongo repository/index/DI; Territory
ve Account controllers; contract DTO/handler; Territory tests/fakes/guards; Web controller/view models/partial/JS;
7 Territory RESX ve bu rapor oluşturuldu/güncellendi. Pack authorization dosyaları bu implementation task'ında
değiştirilmedi.

## 15. Final Verdict

### PARTIAL

Implementation, build, 341 test, UI ve guardlar PASS. Ancak task'ın tam PASS ölçütündeki canlı apply →
duplicate 409 → override → end zinciri, mevcut canlı modelde matched preview row bulunmadığından tamamlanamadı.
Assignment RBAC anahtarları da pack kararı uyarınca model.read/manage fallback kullanır.

## 16. Next Recommended Prompt

`@orchestrator MOD-0151 FU05 Live Smoke Closeout — target tenant'ta active model + active node + evaluable assignment rule + matched account hazırlayıp Gateway-only apply/409/override/end/coverage zincirini tamamla; Mongo replica-set transaction readiness'i doğrula; runtime kodu değiştirme.`
