# MOD-0151 FU02B — Territory Lifecycle Activation, Computed Expiry and Draft Soft Delete

> **Tarih:** 2026-07-25 · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93` · **Verdict:** **PARTIAL**

## 1. Preflight

MOD-0151 pack, FU02B pack-update audit'i, FU01/FU02/FU02A implementation raporları, Territory backend/UI kodu,
audit/correlation örnekleri, `AGENTS.md`, domain config ve protected path kuralları incelendi.

Pack `ready-for-dev`, `runtime_code_allowed: true` ve
`FU02B-lifecycle-computed-expiry-draft-soft-delete` runtime scope'u açık durumdadır.

## 2. Scope Confirmation

Uygulanan kapsam: manual model activate/deactivate/archive, draft model/node soft-delete, modelle senkron node
lifecycle, computed expiry, single-active-model guard, UI action visibility, contract flags, audit seam ve testler.

Workflow approval, MOD-0023, assignments, resources, evidence pack, import/export, Brand Scope, Product/Brand master,
background scheduler, hard delete, RBAC seed/grant ve Gateway route değişikliği yapılmadı.

## 3. Governance Confirmation

FU02B manual lifecycle'dır. FU06'nın submit/approve/reject, transition gate, approval trace, evidence-backed
activation ve approval-based immutable lifecycle sahipliği korunmuştur.

## 4. Lifecycle Implementation Summary

| Entity | From | Action | To | Implemented | Notes |
|---|---|---|---|---|---|
| Model | draft/inactive | activate | active | Yes | Readiness, node ve overlap guard |
| Model | active | deactivate | inactive | Yes | Active node'lar inactive |
| Model | inactive/computed-expired | archive | archived | Yes | Node'lar archived; read-only |
| Model | draft | delete-draft | soft-deleted | Yes | IsDeleted/DeletedAt; nodes cascade |
| Node | draft | model activation | active | Yes | Tek başına activate endpoint yok |
| Node | active | model deactivation | inactive | Yes | Model lifecycle ile |
| Node | draft | delete-draft | soft-deleted | Yes | Default hierarchy'den gizli |

## 5. API Changes

| Endpoint | Method | Behavior | Guard |
|---|---|---|---|
| `/api/crm/territory-models/{id}/activate` | POST | Manual activation | model.manage; draft/inactive; readiness; nodes; overlap |
| `/api/crm/territory-models/{id}/deactivate` | POST | Active → inactive | model.manage; active-only |
| `/api/crm/territory-models/{id}/archive` | POST | Archive | model.manage; inactive/computed-expired |
| `/api/crm/territory-models/{id}/delete-draft` | POST | Draft soft-delete | model.manage; draft-only |
| `/api/crm/territory-models/{modelId}/nodes/{nodeId}/delete-draft` | POST | Node soft-delete | node.manage; draft model + draft node |

HTTP `DELETE` ve hard delete eklenmedi.

## 6. Contract Flags

| Flag | Value | Notes |
|---|---:|---|
| `supportsLifecycleActions` | true | FU02B |
| `supportsComputedExpiry` | true | Read-time |
| `supportsDraftSoftDelete` | true | Draft-only |
| `supportsWorkflowActivation` | false | FU06 future |
| `supportsApprovalTrace` | false | FU06 future |

## 7. UI Changes

| Surface | Action | Visibility | Notes |
|---|---|---|---|
| Model DataTable | Activate | draft/inactive + manage | Expired kayıt hariç |
| Model DataTable | Deactivate | active + manage | SweetAlert confirmation |
| Model DataTable | Archive | inactive/computed-expired + manage | Active doğrudan yok |
| Model DataTable | Delete Draft | draft + manage | Hard delete değil |
| Model detail | Lifecycle buttons | status + manage | Same guards |
| Hierarchy | Delete Draft Node | draft node/model + node.manage | Active node action yok |
| Model/node badges | Expired | `isExpired/computedStatus` | Stored status korunur |

Yeni lifecycle etiketleri 7 dil RESX'e eklendi; parity **76/76**.

## 8. Computed Expiry Behavior

`EffectiveTo < now` ise DTO `isExpired=true`, `computedStatus=expired` döndürür. `status` ve `storedStatus` mevcut
DB status'unu korur. Query/read hiçbir DB mutation yapmaz. Draft geçmiş tarihli kayıt draft kalır ve UI warning
gösterir. Background job eklenmedi.

## 9. Delete / Archive Behavior

Yalnız draft model/node soft-delete edilebilir. Active/inactive/archived veya expired non-draft kayıt delete-draft
işleminde kontrollü 409 alır. Active model archive edilemez. Archived model ve node mevcut update guard'ları
nedeniyle değiştirilemez. Repository default filtreleri `IsDeleted=true` kayıtları gizler.

## 10. Single Active Model Guard

Activation sırasında aynı tenant içindeki active modeller:

- normalized CountryScope,
- `business-unit` scope code'larının case-insensitive, duplicate-free ve sıra-bağımsız seti,
- açık uçlu `EffectiveTo` dahil overlapping date window

üzerinden karşılaştırılır. Eşleşme varsa kontrollü 409 döner. FU02A `BusinessScopes` persistence kullanılmıştır.

## 11. Audit / Observability

| Event | Payload | Notes |
|---|---|---|
| `territory.model.activated` | tenant/model/status/actor/reason/correlation/timestamp | Structured logging seam |
| `territory.model.deactivated` | aynı | Success |
| `territory.model.archived` | computedStatus dahil olabilir | Success |
| `territory.model.soft_deleted` | draft → soft-deleted | Success |
| `territory.node.soft_deleted` | nodeId dahil | Success |
| `territory.model.activation_rejected` | previous/new status | Controlled rejection |
| `territory.model.delete_rejected` | previous/new status | Controlled rejection |
| `territory.node.delete_rejected` | nodeId dahil | Controlled rejection |

CRM'nin mevcut audit yaklaşımına uygun structured logging seam kullanılmıştır; yeni audit store oluşturulmamıştır.

## 12. Tests

| Suite | Result | Notes |
|---|---|---|
| CrmService API build | PASS | 0 warning, 0 error |
| Territory tests | PASS | 63/63 |
| Full CrmService Application tests | PASS | 232/232 |
| Web build | PASS | İzole output; çalışan Web process kilidine dokunulmadı |
| JavaScript syntax | PASS | `index.js`, `hierarchy.js` |
| RESX parity | PASS | 76 key × 7 dil |
| DataTable verification script | BLOCKED | Ortamda `python` executable yok |

## 13. Live / Manual Smoke

| Step | Result | Notes |
|---|---|---|
| In-app browser discovery | BLOCKED | Uygulama içi browser oturumu mevcut değil |
| Authenticated gateway lifecycle smoke | NOT RUN | Tarayıcı session/token alınamadı |
| Mongo hand-edit / local seed workaround | NOT USED | Yasaklar korundu |

Canlı çalışan Web process ayrıca eski output'u kilitliyordu; kullanıcı sürecini durdurmadan yeni build izole output'ta
doğrulandı. Bu nedenle runtime smoke kanıtı üretilmedi ve verdict PASS yerine PARTIAL'dır.

## 14. Created / Updated Files

Backend: Territory command/DTO/handler/controller/repository/contract/audit seam ve ilgili DI dosyaları.

Frontend: Territory view model, Details Razor, model/hierarchy JavaScript ve 7 dil Territory RESX dosyaları.

Tests: fake repository/audit seam, lifecycle tests, contract ve scope guard testleri.

Evidence: bu rapor.

## 15. Guard Checks

| Check | Result |
|---|---|
| Workflow approval implemented? | No |
| Submit/approve/reject added? | No |
| Assignment/resource/evidence/import/export implemented? | No |
| Brand Scope / Product-Brand master touched? | No |
| Account/Contact touched? | No |
| Hard delete exists? | No |
| Active record delete allowed? | No |
| Draft soft-delete only? | Yes |
| Archive active allowed? | No |
| EffectiveTo auto-mutates DB status? | No |
| Computed expired implemented? | Yes |
| Background job added? | No |
| Direct 5061 used? | No |
| TenantId payload sent? | No |
| RBAC seed changed? | No |
| Forbidden permission added? | No |
| `crm.territory.delete` introduced? | No |
| `crm.micro-zone.manage` introduced? | No |
| Gateway route changed? | No |
| RESX parity passed? | Yes |
| Tests passed? | Yes |
| Live smoke passed? | Not run / blocked |

## 16. Final Verdict

**PARTIAL.** Lifecycle, computed expiry, draft soft-delete, overlap guard, UI, contract, audit seam ve tests
tamamlandı; test/build sonuçları yeşildir. Uygulama içi browser oturumu olmadığı için authenticated Gateway smoke
çalıştırılamadı.

## 17. Next Recommended Prompt

`MOD-0151 FU02B — Authenticated Gateway Live Smoke Closeout`

Browser smoke PASS sonrasında sıradaki feature adayı: `MOD-0151 FU03 Assignment Rules + Preview`.
