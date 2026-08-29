# MOD-0151 FU04 — Resource Assignments (Implementation)

> **Tarih:** 2026-07-28 · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Korelasyon:** `smoke-fu04-20260728134919`
> **Verdict:** **PASS** — Territory 160/160 test · tüm CrmService suite 331/331 · canlı Gateway smoke 52/52

FU04; territory node'larına **kişi** sorumluluğu atar (MR, Area/Regional Manager, Product Manager…), rol/kapsam/
business-scope validasyonunu MOD-0048 metadata'sından sürer, exclusivity guard'ını uygular ve resource assignment
UI'ını açar. **Müşteri ataması yapmaz:** `AccountTerritoryAssignment` hâlâ yoktur ve FU05'e aittir.

---

## 1. Preflight

### Files reviewed

| Kaynak | Ne için |
|---|---|
| MOD-0151 pack **§7.5 · §9 · §10 · §11 · §16 · §17 · §19 · §20 · §21 · §22** | `TerritoryResourceAssignment` alanları, rol×coverage×exclusivity matrisi, business scope kararları, PersonRef seam, validation kuralları, FU05/FU06 sınırı |
| [FU03 report](./mod-0151-fu03-assignment-rules-preview-implementation-2026-07-28.md) | Rule/preview deseni, "preview asla atamaz" guard yapısı, reference-driven validation |
| [FU02B RETRY-2 closeout](./mod-0151-fu02b-live-smoke-closeout-retry-2-after-status-publish-2026-07-28.md) | Lifecycle canlı PASS; draft-only mutasyon kuralı |
| [FU02A](./mod-0151-fu02a-country-business-unit-scope-selector-hardening-2026-07-25.md) | `BusinessScopes` sözleşmesi (scopeType=business-unit) |
| [FU02 UI](./mod-0151-fu02-territory-hierarchy-ui-viewer-2026-07-25.md) · FU01 raporları | UI/handler konvansiyonları |
| Territory backend (model, node, lifecycle, rules, preview, repository, controller, contract, testler) | Mevcut desenler |
| Gateway `ocelot.json`, `/api/v1/platform/persons`, `/api/v1/hcm/employees` | Employee/person seam olasılıkları |

### Scope confirmation

Pack frontmatter `runtime_code_scope`'una **`FU04-resource-assignments`** eklendi; §1 giriş notu FU03/FU04 sınırını
(FU04 **kişi** atar, **müşteri** atamaz; employee/person master MOD-0151'e ait değil) açıkça yazar.
FU05–FU09 kapalı kalmaya devam eder.

### FU03 PASS confirmation

FU03 canlı smoke **49/49 PASS**; assignment rule + yan etkisiz preview çalışıyor ve `AccountTerritoryAssignment`
hâlâ yok. FU04 bu zeminin üstüne kuruldu.

---

## 2. Implementation Summary

| Area | Implemented | Notes |
|---|---|---|
| Domain — `TerritoryResourceAssignment` | ✅ | Pack §7.5 alanları + soft-delete/audit (EntityBase) |
| Domain — `TerritoryResourceRef` | ✅ | PersonRef seam: ResourceId + ResourceType + DisplayName snapshot + Email. **Employee master yok** |
| Repository | ✅ | Model-scoped, soft-delete filtreli |
| **Metadata-driven validation** | ✅ | Rol/coverage/status/source kuralları MOD-0048 `attributes`'ından okunur — hardcoded eşleme yok |
| Coverage ↔ TerritoryId tutarlılığı | ✅ | `requiresTerritoryId` / `allowsTerritoryId` metadata'sından |
| Business scope kuralları | ✅ | `requiresBusinessScope` / `allowsBusinessScope` + **model scope'unu genişletememe** |
| Primary kuralı | ✅ | `canBePrimary=false` rolde primary reddedilir (viewer/hoc/admin/operational-resource) |
| ChangeReason zorunluluğu | ✅ | `requiresReason` metadata'sından (manual + override) |
| Rol ↔ node level | ✅ | MR → zone/microzone **blok**; area/regional/division/commercial manager → **uyarı** |
| Exclusivity guard | ✅ | Duplicate primary (node+rol+BU seti, çakışan dönem) + MR cross-scope bloğu + override kaçışı |
| Conflict report endpoint | ✅ | Salt okunur; blok + uyarıları birlikte döner |
| Lifecycle (end vs delete) | ✅ | `end` → status=ended + validTo; `delete-draft` yalnız `proposed` |
| Contract flags | ✅ | 1 yeni flag + `resourceAssignments`/`supportsResourceAssignments` true; apply **false** |
| UI | ✅ | Liste + form offcanvas + conflict paneli; Apply/Assign-customers butonu **yok** |
| Lokalizasyon | ✅ | 7 dil × **164 anahtar**, parity doğrulandı (+34 yeni) |
| Tests | ✅ | 42 yeni test; Territory toplam 160 |
| Live smoke | ✅ | Gateway-only 52/52 |

---

## 3. Data Model Summary

| Entity | Purpose | Key Fields |
|---|---|---|
| `TerritoryResourceAssignment` | Bir territory node'u / ticari kapsam için **kişi sorumluluğu** | `ModelId` · `TerritoryId?` · `Resource` · `RoleCode` · `CoverageScope` · `BusinessScopes[]` · `Status` · `AssignmentSource` · `IsPrimary` · `ValidFrom`/`ValidTo` · `ChangeReason` · `CorrelationId` · `IsDeleted`/`DeletedAt` |
| `TerritoryResourceRef` (VO) | Kişiye **referans** — master değil | `ResourceId` · `ResourceType` · `DisplayName` (yalnız gösterim snapshot'ı) · `Email?` |
| `TerritoryBusinessScope` (mevcut VO) | BU kapsamı | `ScopeType=business-unit` · `ScopeCode` |

`TerritoryId` **nullable**: `business-unit` / `product-portfolio` / `business-scope` / `model-wide` /
`all-business-scopes` kapsamlarında null olmak **zorundadır**; `exact-territory` / `territory-subtree` içinse
**zorunludur**. Bu kararı kod değil, coverage scope'un yayınlanmış metadata'sı verir.

**Oluşturulmayanlar:** `AccountTerritoryAssignment`, assignment history tablosu, Employee/Person/Position aggregate'i.

---

## 4. API Summary

| Endpoint | Method | Behavior | Side Effect? |
|---|---|---|---|
| `…/{id}/resource-assignments` | GET | Model atamalarını + model BU kapsamını listeler | **Hayır** |
| `…/{id}/resource-assignments/{assignmentId}` | GET | Tek atama | **Hayır** |
| `…/{id}/resource-assignments` | POST | Atama oluşturur (**yalnız draft model**), status=`proposed` | Evet — yalnız atama kaydı |
| `…/{id}/resource-assignments/{assignmentId}` | PUT | Atama günceller (yalnız `allowsMutation` statüsünde) | Evet — yalnız atama kaydı |
| `…/{id}/resource-assignments/{assignmentId}/delete-draft` | POST | **Yalnız `proposed`** atamayı soft-delete eder | Evet — `IsDeleted` bayrağı |
| `…/{id}/resource-assignments/{assignmentId}/end` | POST | `Status=ended` + `ValidTo`. **Silme değildir** | Evet — status geçişi |
| `…/{id}/resource-assignments/validate-conflicts` | POST | Exclusivity raporu (blok + uyarı) | **HAYIR — yazmaz** |

**Permission:** okuma → `crm.territory.model.read`; yazma → `crm.territory.model.manage`. **Yeni permission
açılmadı** (FU02B/FU03 precedent'i). Pack §19 `crm.territory.resource.manage` önerir; bu anahtar RBAC kataloğunda
yok ve bu task RBAC seed/grant değiştiremez — bilinçli sapma, §14 follow-up'ında kayıtlı.

---

## 5. Contract Summary

| Flag | Value | Notes |
|---|---|---|
| `resourceAssignments` | **true** | FU04 ile açıldı |
| `supportsResourceAssignments` | **true** | FU03'te false'tu |
| `supportsResourceExclusivityGuard` | **true** | Yeni |
| `supportsAccountAssignmentApply` | **false** | FU05 |
| `accountAssignmentApply` | **false** | FU05 |
| `assignmentRules` / `supportsAssignmentRules` / `supportsAssignmentPreview` | true | FU03 bozulmadı |
| `supportsLifecycleActions` / `supportsComputedExpiry` / `supportsDraftSoftDelete` | true | FU02B bozulmadı |
| `supportsWorkflowActivation` / `supportsApprovalTrace` | false | FU06 |
| `workflowActivation` / `evidencePack` / `importExport` | false | Bozulmadı |
| `runtimeScope` | `… ; FU04-resource-assignments` | Canlıda doğrulandı |
| `permissions` | **5 anahtar** (değişmedi) | |

---

## 6. UI Summary

| Surface | Behavior | Notes |
|---|---|---|
| Resource Assignments kartı (Details) | Kaynak, rol, hedef node, kapsam, BU, primary, status, geçerlilik | Model draft değilse salt okunur |
| **Check Conflicts** butonu | `validate-conflicts` çağırır, blok/uyarıları panelde gösterir | Her statüde açık (salt okuma) |
| **Assign Resource** offcanvas | Rol + coverage scope (**referans kaynaklı**), node, BU (**model kapsamıyla sınırlı**), kaynak, tarihler, source, primary, gerekçe | Yalnız draft + `model.manage` |
| Coverage scope davranışı | Seçime göre node/BU alanları gösterilip gizlenir — backend metadata kurallarının aynası | Form, API'nin reddedeceği kombinasyonu üretmez |
| Kaynak seçici | Person/HCM dizini varsa dropdown; yoksa **PersonRef seam** (id + display) + açık uyarı | Sahte/hardcoded kişi listesi **yok** |
| **End Assignment** | Onaylı status geçişi | "Geçmiş korunur" mesajı |
| **Delete** | Yalnız `proposed` satırda görünür | Hard delete yok |
| Guard metinleri | "Bu atamalar kişilere sorumluluk verir, müşteri atamaz" · "Müşteri kapsamı FU05" · "Sonlandırmak silmek değildir" | Kartın üstünde kalıcı |
| RESX | 7 dil × 164 anahtar, parity ✅ | +34 yeni |

**UI'da bilinçli olarak YOK:** Account assignment apply · Assign customers · Persist · Workflow approval ·
Evidence export · Brand/Product seçici · Visit/route planlama.

---

## 7. Resource Source Decision

**Karar: pack §10 PersonRef seam.**

Canlı ortamda her iki aday kaynak da **erişilebilir değil**:

| Kaynak | Sonuç |
|---|---|
| `GET /api/v1/platform/persons` (MOD-0288) | **HTTP 403** — tenant admin'de person izni yok |
| `GET /api/v1/hcm/employees` | **HTTP 403** — aynı |
| `/api/v1/platform/positions` | **HTTP 404** |

İzin vermek RBAC seed/grant değişikliği gerektirirdi; bu task bunu yasaklıyor. Bu yüzden:

- MOD-0151 `TerritoryResourceRef` (id + tip + görünen ad + e-posta) **saklar**, employee master **tutmaz** —
  pack §10'un öngördüğü seam budur, bir gerileme değil.
- UI seçici, iki endpoint'i **sırayla dener**; erişilemezse `resourceLookupReady=false` döner ve form manuel
  PersonRef girişine düşer, kullanıcıya sebebi yazılı olarak gösterilir.
- **Sahte employee seed'i, hardcoded kişi listesi veya uydurma isim üretilmedi.**
- Guard testi, domain assembly'de `Employee`/`Person`/`Position` tipi bulunmadığını doğrular.

**Follow-up:** person/employee okuma izni verildiğinde selector kod değişikliği olmadan dropdown'a döner
(`resourceLookupReady` true olur). Ayrı RBAC task'ı gerekir.

---

## 8. Validation / Conflict Behavior

| Scenario | Expected | Result |
|---|---|---|
| Publish edilmemiş rol / coverage scope | 400 fail-closed | ✅ 400 |
| `territory-resource-role` seti publish değil | 400 fail-closed | ✅ 400 |
| Rol metadata'sı yok | 400 kontrollü | ✅ (testle) |
| CoverageScope boş → rolün `defaultCoverageScope`'u | uygulanır | ✅ MR → `exact-territory` |
| `requiresTerritoryId=true` + node yok | 400 | ✅ 400 |
| `allowsTerritoryId=false` + node var | 400 | ✅ 400 |
| Node model dışında | 404 | ✅ 404 |
| **MR area node'una** | 409 | ✅ 409 *"can only be assigned to a zone or microzone"* |
| area-manager zone node'una | izin + **uyarı** | ✅ conflict report'ta `unexpected-node-level` |
| BU model kapsamı dışında (`gamma`) | 400 | ✅ 400 |
| Rol/coverage BU ister, BU yok | 400 | ✅ 400 |
| `allowsBusinessScope=false` + BU var | 400 | ✅ 400 |
| `canBePrimary=false` rolde primary | 400 | ✅ 400 (viewer) |
| `requiresReason=true` source + gerekçe yok | 400 | ✅ 400 |
| ResourceId boş | 400 | ✅ 400 |
| `ValidTo < ValidFrom` / model penceresi dışı | 400 | ✅ 400 |
| **Duplicate primary** (aynı node+rol+BU, çakışan dönem) | 409 | ✅ 409 |
| Aynı node **farklı BU** | izinli | ✅ 201 (paketin kendi örneği: Beylikdüzü+Alpha / +Beta) |
| **Non-primary** (yedek) | exclusivity'den muaf | ✅ 201 |
| Çakışmayan dönemler | izinli | ✅ 201 |
| **Aynı MR iki BU'da primary** | 409 | ✅ 409 |
| Aynı MR iki BU'da + `source=override` + gerekçe | izinli | ✅ 201 |
| Aynı MR aynı BU'da çok node | izin + **uyarı** | ✅ `multi-node-coverage` |
| Draft olmayan modelde yazma | 409 | ✅ 409 |
| `ended` atamayı güncelleme | 409 | ✅ 409 |
| `active`/`ended` atamayı silme | 409 | ✅ 409 *"end the assignment instead"* |
| `proposed` atamayı silme | 200 + listeden düşer | ✅ 200 |
| **Sonlandırma** | status=ended + validTo, kayıt kalır | ✅ 200 |
| Sonlandırılmış atama kapsamı serbest bırakır | yeni primary açılabilir | ✅ 201 |
| Henüz başlamamış atamayı sonlandırma | ValidFrom'a sıkışır | ✅ (aşağıda) |
| Açık end date < ValidFrom | 400 | ✅ 400 |
| Tenant izolasyonu | 404 | ✅ 404 |
| Conflict report | salt okunur | ✅ kayıt sayısı değişmedi |

### Smoke sırasında bulunan ve düzeltilen davranış

İlk canlı koşuda **gelecek tarihli bir atamayı sonlandırma 400** verdi: end date varsayılanı "şimdi" idi ve
`ValidFrom` (2031) ondan sonraydı. Teknik olarak doğru ama planlamacı için yanlış — henüz başlamamış bir planı
iptal etmek en sık istenen şeydir. Varsayılan artık `max(now, ValidFrom)`: gelecek bir atama kendi başlangıcına
sıkıştırılarak sonlandırılır (hiç yürürlüğe girmemiş olarak kayda geçer). **Açıkça** ValidFrom'dan önce bir tarih
verilmesi hâlâ 400'dür; iki regresyon testi bunu pinler.

---

## 9. Tests

| Suite | Result | Notes |
|---|---|---|
| CrmService API build | **PASS** | 0 error |
| `TerritoryResourceAssignmentTests` | **PASS 38/38** | create (17), exclusivity (6), update/end/soft-delete (7), query/report (4), tenant isolation, tarih kuralları |
| `TerritoryScopeGuardTests` (genişletildi) | **PASS** | FU04 route'ları var · account-assignments/apply/evidence/export **yok** · **`AccountTerritoryAssignment` tipi yok** · **Employee/Person/Position tipi yok** · permission 5 |
| `TerritoryContractTests` (güncellendi) | **PASS** | FU04 flag'leri + apply false + runtimeScope |
| **Territory toplam** | **160/160** | 118 → 160 (+42) |
| **Tüm CrmService Application suite** | **331/331** | 290 → 331 |
| Web build (C# + Razor) | **PASS** | İzole output; çalışan Web process'ine dokunulmadı |
| JavaScript syntax | **PASS** | `resource-assignments.js` |
| RESX parity | **PASS** | 164 anahtar × 7 dil |
| Static guard grep | **PASS** | direct `:5061` yok; UI'da apply/assign-customer çağrısı yok |

---

## 10. Live / Manual Smoke

Gateway-only (`:5000`), `X-Tenant-Id` header, payload'da TenantId yok. **52/52 PASS.**

| Step | Result | Notes |
|---|---|---|
| Contract — FU04 flag'leri | ✅ 10/10 | resource true, apply false, FU03/FU02B bozulmadı, permission 5 |
| Draft model (BU alpha+beta) + country/area/zone/zone | ✅ | |
| Boş liste | ✅ | `modelBusinessUnitScopes=alpha,beta`, editable |
| **MR → Beylikdüzü + Alpha** | ✅ 201 | coverage `exact-territory` **rolden default'landı**, status `proposed` |
| **Aynı zone + Beta → başka MR** | ✅ 201 | Paketin kendi senaryosu |
| Area Manager → area node | ✅ 201 | |
| **Product Manager, node'suz** | ✅ 201 | `product-portfolio` kapsamı |
| Güncelleme + geri okuma | ✅ | |
| 9 validation reddi | ✅ 9/9 | rol, coverage, MR-level, node, BU, gerekçe, ResourceId, node-on-BU-scope, primary |
| **Duplicate primary** | ✅ 409 | |
| Non-primary yedek | ✅ 201 | |
| **Aynı MR iki BU'da** | ✅ 409 | |
| **Override + gerekçe** | ✅ 201 | |
| `validate-conflicts` | ✅ 200 | 6 atama, 1 conflict (`cross-scope-primary-resource`), salt okunur |
| **End** (gelecek tarihli atama) | ✅ 200 | status `ended`, validTo=2031-01-01, kayıt duruyor |
| Sonlandırma kapsamı serbest bıraktı | ✅ 201 | |
| `ended` atamayı silme | ✅ 409 | *"end the assignment instead"* |
| `proposed` atamayı silme | ✅ 200 + listeden düştü | |
| apply / evidence-pack / submit-approval / export | ✅ **404 ×4** | Endpoint yok |
| Account master | ✅ 200, 10 hesap | FU04'te yazma yolu yok |
| Active modelde yazma | ✅ 409 | |
| Active modelde liste | ✅ 200, `isEditable=false` | |
| Temizlik (deactivate + archive) | ✅ 200/200 | Tenant'ta **0 aktif model** |

---

## 11. Guard Checks

| Check | Result |
|---|---|
| Account assignment apply implemented? | **No** |
| AccountTerritoryAssignment persisted? | **No** — aggregate/repository/koleksiyon yok (test ile pinlendi) |
| Account master changed? | **No** — FU04'te account yazma yolu yok |
| Contact changed? | **No** |
| Account/Contact entity'ye Territory alanı eklendi mi? | **No** |
| Assignment rule apply implemented? | **No** — FU03 preview hâlâ yan etkisiz |
| Workflow approval implemented? | **No** |
| Submit/approve/reject eklendi mi? | **No** |
| Evidence / import / export implemented? | **No** |
| Brand Scope implemented? | **No** |
| Product/Brand master touched? | **No** |
| Visit/route planning implemented? | **No** |
| Employee/Person master oluşturuldu mu? | **No** — yalnız PersonRef seam (guard testi) |
| Sahte employee seed / hardcoded kişi listesi? | **No** |
| Hard delete added? | **No** — soft-delete (yalnız proposed) + end |
| Mongo hand-edit? | **No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** (yalnız okundu) |
| Gateway route changed? | **No** |
| Forbidden permissions added? | **No** — `crm.territory.delete` / `crm.micro-zone.manage` yok; permission 5'te sabit |
| Direct `:5061` used? | **No** (yalnız health) |
| TenantId payload used? | **No** |
| Contract account apply flag false? | **Yes** |
| Hardcoded reference fallback? | **No** — kurallar published metadata'dan |
| HEAD / porcelain | `094e3a86` sabit · 426 → 427 (bu rapor) |

---

## 12. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `services/…/Domain/Entities/TerritoryResourceAssignment.cs` | **Created** | Aggregate + `TerritoryResourceRef` VO |
| `services/…/Domain/Repositories/ITerritoryResourceAssignmentRepository.cs` | **Created** | Model-scoped repo |
| `…/Features/Territory/ResourceAssignments/TerritoryResourceAssignmentContracts.cs` | **Created** | Metadata anahtarları, rol→level beklentileri, komutlar, DTO'lar, mapper |
| `…/ResourceAssignments/TerritoryResourceAssignmentValidation.cs` | **Created** | Metadata-driven resolve + validate |
| `…/ResourceAssignments/TerritoryResourceConflictEngine.cs` | **Created** | Saf exclusivity guard + rapor |
| `…/ResourceAssignments/Handlers/TerritoryResourceAssignmentHandlers.cs` | **Created** | Create/Update/SoftDelete/End |
| `…/ResourceAssignments/Handlers/TerritoryResourceAssignmentQueryHandlers.cs` | **Created** | List/GetById/ValidateConflicts |
| `services/…/Features/Territory/ITerritoryReferenceValidator.cs` | Updated | `GetValueMetadataAsync` eklendi |
| `services/…/Features/Territory/TerritoryReferenceValidator.cs` | Updated | Metadata erişimi |
| `services/…/Persistence/Repositories/TerritoryResourceAssignmentRepository.cs` | **Created** | Mongo repo |
| `services/…/Persistence/DependencyInjection.cs` | Updated | DI + class map |
| `services/…/Api/Controllers/CRM/TerritoryModelsController.cs` | Updated | 7 FU04 endpoint + end request record |
| `…/Features/Territory/Contract/TerritoryContractDto.cs` | Updated | `SupportsResourceExclusivityGuard`; resource flag'leri true |
| `…/Features/Territory/Contract/GetTerritoryContractHandler.cs` | Updated | runtimeScope + limitations |
| `services/…/tests/…/Territory/TerritoryResourceAssignmentTests.cs` | **Created** | 38 test |
| `services/…/tests/…/Territory/FakeTerritoryInfrastructure.cs` | Updated | Resource repo fake + rol/coverage/status/source sözlüğü ve **metadata** |
| `services/…/tests/…/Territory/TerritoryScopeGuardTests.cs` | Updated | FU04 sınırına taşındı + 2 yeni guard |
| `services/…/tests/…/Territory/TerritoryContractTests.cs` | Updated | FU04 flag testleri |
| `frontend/…/Controllers/CRM/TerritoryManagementController.cs` | Updated | 6 proxy action + resource lookup + payload dönüştürücü |
| `frontend/…/Models/CRM/TerritoryViewModels.cs` | Updated | FU04 view model + payload'ları |
| `frontend/…/Views/CRM/TerritoryManagement/_ResourceAssignments.cshtml` | **Created** | Liste + conflict paneli + form offcanvas |
| `frontend/…/Views/CRM/TerritoryManagement/Details.cshtml` | Updated | Partial + veri bloğu + script |
| `frontend/…/wwwroot/assets/js/CRM/TerritoryManagement/resource-assignments.js` | **Created** | Liste/form/end/delete/conflict davranışı |
| `frontend/…/Resources/Views/CRM/TerritoryManagement/*.resx` (7 dil) | Updated | +34 anahtar → 164, parity ✅ |
| `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` | Updated | frontmatter scope + FU03/FU04 sınır notu |
| `docs/audits/mod-0151-fu04-resource-assignments-implementation-2026-07-28.md` | **Created** | Bu rapor |

---

## 13. Final Verdict

### **PASS**

- Resource assignment CRUD çalışıyor (draft-only, reference fail-closed, metadata-driven).
- Rol / business scope / node / coverage / kaynak validasyonu çalışıyor — hepsi published metadata'dan.
- Exclusivity guard çalışıyor: duplicate primary, MR cross-scope bloğu, override kaçışı, çok-node uyarısı.
- Sonlandırma **silme değildir**; `proposed` dışındaki hiçbir kayıt silinemez.
- Contract flag'leri doğru; account apply **false**; FU02B/FU03 bozulmadı.
- UI mevcut; Apply/Assign-customers butonu yok, FU05 beklentisi kullanıcıya yazılı.
- Testler/build/smoke geçiyor: **160/160 Territory · 331/331 suite · 52/52 canlı**.
- Guardrail'ler korundu; yeni permission, RBAC/Gateway/Mongo/publish değişikliği yok; employee master yaratılmadı.

**Neden PARTIAL değil:** görev tanımındaki PARTIAL satırı "employee/person source limited" diyor. Kaynak
gerçekten sınırlı — ancak bu bir eksik uygulama değil, **pack §10'un açıkça öngördüğü tasarım**dır ("HR/HCM
olgunlaşana kadar `PersonRef` / `UserId` seam ile ilerlenir; MOD-0151 employee master tutmaz"). Selector, dizin
erişilebilir olduğunda kod değişmeden dropdown'a döner. Bu nedenle FU04'ün kendi kapsamı eksiksiz kabul edilmiştir;
izin bağımlılığı §14'te ayrı follow-up olarak izlenir.

---

## 14. Next Recommended Prompt

`@orchestrator MOD-0151 FU05 — Account Assignment Apply + History`

Artık her iki girdi de hazır: FU03 hangi hesabın hangi node'a düşeceğini **önizliyor**, FU04 o node'dan kimin
**sorumlu** olduğunu tanımlıyor. FU05 bunları efektif-tarihli `AccountTerritoryAssignment` kayıtlarına dönüştürür
(eski atama `ended`, silinmez), manual override'ı korur ve MOD-0149 CoverageSummary projection'ını doldurur.

### Bu task'tan çıkan follow-up'lar

| # | Konu | Neden ayrı |
|---|---|---|
| 1 | **Person/Employee okuma izni** (`/api/v1/platform/persons`, `/api/v1/hcm/employees` → 403) | Verildiğinde resource selector kod değişmeden dropdown'a döner. RBAC seed/grant bu task'ın dışında |
| 2 | **`crm.territory.resource.manage` / `assignment.*` RBAC seed** | Pack §19 önerir; katalogda yok. FU05'te assignment yüzeyi büyüdüğünde tek bir RBAC task'ıyla hizalanmalı |
| 3 | **`expectedLevels` metadata'sı** `territory-resource-role` setine | Rol→node-level beklentileri şu an kodda tek bir tabloda (`TerritoryRoleLevelExpectations`); metadata'ya taşınırsa vocabulary yine tek kaynak olur |
| 4 | **Assignment status geçişi** (`proposed → active`) | FU04 `proposed` üretir; operasyonel aktivasyon FU06 workflow'una veya ayrı bir lifecycle adımına bağlanmalı |
| 5 | **Lifecycle audit log sink** (FU02B'den devam) | Structured event'ler hâlâ dosyaya düşmüyor |
