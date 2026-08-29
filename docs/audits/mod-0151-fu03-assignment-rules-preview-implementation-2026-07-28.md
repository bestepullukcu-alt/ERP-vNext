# MOD-0151 FU03 — Assignment Rules + Preview (Implementation)

> **Tarih:** 2026-07-28 · **Tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Korelasyon:** `smoke-fu03-20260728131818`
> **Verdict:** **PASS** — Territory 118/118 test · tüm CrmService suite 290/290 · canlı Gateway smoke 49/49

FU03; `TerritoryAssignmentRule` CRUD'unu ve **yan etkisiz** assignment preview'unu (aday hesaplar + çakışma tespiti +
preview UI) açar. **Bu bir apply task'ı değildir:** hiçbir `AccountTerritoryAssignment` kaydı yazılmaz, Account/Contact
master'a dokunulmaz, resource/MR ataması, workflow approval, evidence pack ve import/export eklenmez.

---

## 1. Preflight

### Files reviewed

| Kaynak | Ne için |
|---|---|
| MOD-0151 pack **§7.3 · §11 · §16 · §17 · §19 · §20 · §21 · §22** | `TerritoryAssignmentRule` alanları, account coverage sınırı, rule/conflict reference setleri, permission önerisi, endpoint ve validation kuralları, FU03/FU05 sınırı |
| [FU02B RETRY-2 closeout](./mod-0151-fu02b-live-smoke-closeout-retry-2-after-status-publish-2026-07-28.md) | Lifecycle'ın canlı PASS olduğu teyidi (FU03'ün ön koşulu) |
| [FU02B vocabulary reconciliation](./mod-0151-fu02b-lifecycle-status-vocabulary-reconciliation-2026-07-28.md) | Reference sözlüğü/kod hizalama dersi ve vocabulary-aware test seam'i |
| [FU02B implementation](./mod-0151-fu02b-territory-lifecycle-activation-expiry-soft-delete-implementation-2026-07-25.md) · [FU02A](./mod-0151-fu02a-country-business-unit-scope-selector-hardening-2026-07-25.md) · [FU02 UI](./mod-0151-fu02-territory-hierarchy-ui-viewer-2026-07-25.md) · [FU01](./mod-0151-fu01-contract-territory-model-node-backend-2026-07-23.md) · [FU01 live smoke](./mod-0151-fu01-live-smoke-retry-2026-07-23.md) | Handler/validator/DTO/UI konvansiyonları, fail-closed deseni, Gateway-only kuralı |
| Territory backend (`TerritoryModel`, `TerritoryNode`, lifecycle handlers, repository, controller, contract, testler) | Mevcut desenler |
| MOD-0149 `Account` entity + `IAccountRepository` + `AccountRepository` | Eşleşmeye uygun alanlar (`CountryRef`, `CityRef`, `DistrictRef`, `AccountType`, `AccountCategory`, `Status`), tenant izolasyonu |
| `Views/CRM/TerritoryManagement/*`, `TerritoryManagementController`, RESX | UI yerleşimi ve lokalizasyon |

### Scope confirmation

Pack frontmatter `runtime_code_scope`'una **`FU03-assignment-rules-and-preview`** eklendi ve §1 giriş notu FU03
sınırını (preview asla atamaz; apply FU05) açıkça yazacak şekilde güncellendi. FU04–FU09 kapalı kalmaya devam eder.

### FU02B closeout confirmation

FU02B canlı closeout'u **PASS (72/72)** olduğundan FU03 açılabilir: aktif bir territory modeli oluşturulup güvenle
geri alınabiliyor, bu da rule/preview testlerinin ön koşulu.

---

## 2. Implementation Summary

| Area | Implemented | Notes |
|---|---|---|
| Domain — `TerritoryAssignmentRule` | ✅ | Pack §7.3 alanları: ModelId, TerritoryId, RuleCode, Name, RuleType, ConflictPolicy, Priority, IsEnabled, Criteria, EffectiveFrom/To, audit + soft-delete (EntityBase) |
| Domain — `TerritoryRuleCriteria` | ✅ | **Typed whitelist value object** — free-form JSON/expression yok. Alan içi OR, alanlar arası AND |
| Domain — `ITerritoryAssignmentRuleRepository` | ✅ | Model-scoped CRUD; soft-delete filtreli |
| Domain — `ITerritoryAccountReader` | ✅ | **Yalnız okuma** seam'i (`ListForPreviewAsync`, `CountAsync`). `IAccountRepository`'ye (yazma içerir) bağımlılık bilinçli olarak kurulmadı |
| Application — rule CRUD handlers | ✅ | Create/Update/SoftDelete + List/GetById; hepsi draft-model kısıtlı |
| Application — reference validation | ✅ | `territory-rule-type` + `territory-conflict-policy` MOD-0048'e karşı **fail-closed**; hardcoded fallback yok |
| Application — criteria validation | ✅ | Boş kriter reddi, rule-type'a özel zorunlu alanlar, include∩exclude reddi, 200 değer üst sınırı |
| Application — `TerritoryAssignmentPreviewEngine` | ✅ | **Saf fonksiyon**: repository yok, saat yok, yazma yolu yok. Öncelik sıralaması + çakışma tespiti |
| Application — preview handler | ✅ | Draft/active/inactive'de çalışır, archived'de 409; reference fail-closed; scan cap + uyarılar |
| Persistence | ✅ | `territory_assignment_rules` koleksiyonu, `TerritoryAccountReader` (salt okuma), Mongo class map (Guid-as-string) |
| API | ✅ | 6 endpoint (aşağıda); yeni permission açılmadı |
| Contract | ✅ | 4 yeni flag; apply/resource **false** kaldı |
| UI | ✅ | Details sayfasında Assignment Rules bölümü + Preview paneli + offcanvas form; Apply/Assign butonu **yok** |
| Lokalizasyon | ✅ | 7 dil × **130 anahtar**, parity doğrulandı (+54 yeni) |
| Tests | ✅ | 47 yeni test (rule CRUD 20, preview 22, guard 5); toplam Territory 118 |
| Live smoke | ✅ | Gateway-only 49/49 |

### Priority konvansiyonu

**Düşük değer kazanır.** Aynı hesap birden fazla kurala uyduğunda `Priority` küçük olan kazanır; eşitlikte `CreatedAt`,
sonra `RuleCode` (case-insensitive) belirler — deterministik.

---

## 3. API Summary

| Endpoint | Method | Behavior | Side Effect? |
|---|---|---|---|
| `/api/crm/territory-models/{id}/assignment-rules` | GET | Model kurallarını listeler (+ hedef node, `isEditable`) | **Hayır** — read |
| `/api/crm/territory-models/{id}/assignment-rules/{ruleId}` | GET | Tek kural detayı | **Hayır** — read |
| `/api/crm/territory-models/{id}/assignment-rules` | POST | Kural oluşturur (**yalnız draft model**) | Evet — yalnız `territory_assignment_rules` |
| `/api/crm/territory-models/{id}/assignment-rules/{ruleId}` | PUT | Kural günceller (**yalnız draft model**; RuleCode değişmez) | Evet — yalnız kural kaydı |
| `/api/crm/territory-models/{id}/assignment-rules/{ruleId}/delete-draft` | POST | Kuralı **soft-delete** eder (hard delete yok) | Evet — `IsDeleted` bayrağı |
| `/api/crm/territory-models/{id}/assignment-preview` | POST | Kuralları çalıştırır; aday + çakışma döner | **HAYIR — hiçbir şey yazmaz** |

**Permission:** okuma/preview → `crm.territory.model.read`; yazma → `crm.territory.model.manage`.
**Yeni permission açılmadı** (FU02B precedent'i). Pack §19 `assignment.read/manage` önerir; bu anahtarlar RBAC
kataloğunda seed değil ve bu task RBAC seed/grant değiştiremez — bilinçli sapma, aşağıdaki follow-up'ta kayıtlı.

### Preview response

`modelId · modelStatus · previewRunId (transient) · generatedAt · effectiveAt · correlationId ·
persistedAssignments (**daima false**) · evaluatedRuleCount · skippedRuleCount · totalTenantAccounts ·
scannedAccounts · totalCandidateAccounts · unmatchedAccountsCount · conflictCount · matchedAccounts[] ·
conflicts[] · warnings[] · criteriaSummary[]`

`matchedAccounts[]`: accountId, accountCode, accountName, targetTerritoryNodeId/Code/Name/Level, ruleId, ruleCode,
ruleType, priority, matchReason, conflictStatus (`none` / `conflict-winner` / `conflict-loser`).

`conflicts[]`: accountId, accountCode, accountName, candidateTerritoryNodes[] (isWinner ile), conflictingRuleIds[],
conflictPolicy, resolutionSuggestion (**preview-only tavsiye**; FU03 hiçbir çakışmayı çözmez).

---

## 4. Contract Summary

| Flag | Value | Notes |
|---|---|---|
| `assignmentRules` | **true** | FU03 ile açıldı |
| `supportsAssignmentRules` | **true** | Yeni |
| `supportsAssignmentPreview` | **true** | Yeni |
| `supportsAccountAssignmentApply` | **false** | Yeni — FU05 |
| `supportsResourceAssignments` | **false** | Yeni — FU04 |
| `accountAssignmentApply` | false | Değişmedi |
| `resourceAssignments` | false | Değişmedi |
| `supportsLifecycleActions` | true | FU02B bozulmadı |
| `supportsComputedExpiry` / `supportsDraftSoftDelete` | true / true | Bozulmadı |
| `supportsWorkflowActivation` / `supportsApprovalTrace` | false / false | Bozulmadı |
| `workflowActivation` / `evidencePack` / `importExport` | false | Bozulmadı |
| `runtimeScope` | `… ; FU03-assignment-rules-and-preview` | Canlıda doğrulandı |
| `permissions` | **5 anahtar** (değişmedi) | `crm.territory.delete` / `crm.micro-zone.manage` yok |

---

## 5. UI Summary

| Surface | Behavior | Notes |
|---|---|---|
| Assignment Rules kartı (Details) | Kural listesi: kod, ad, hedef node, tip, kriter özeti, öncelik, çakışma politikası, etkin/pasif | Model draft değilse salt okunur |
| **Run Preview** butonu | Preview'u çalıştırır, paneli açar | Archived modelde gösterilmez |
| **Create / Edit Rule** offcanvas | Hedef node, rule type, conflict policy (**referans kaynaklı select**), öncelik, etkin, kriter alanları, tarih aralığı | Yalnız draft + `model.manage` |
| **Delete Rule** | Onaylı **soft-delete** | Hard delete yok |
| Preview paneli | 4 istatistik kutusu + uyarılar + 3 sekme (Eşleşenler / Çakışmalar / Kural özeti) | Çakışma satırlarında kazanan işaretli |
| Guard metinleri | "Preview does not assign accounts." · "Apply and assignment history will come in FU05." · "Resource / MR assignment will come in FU04." | Kartın üstünde kalıcı bilgi kutusu |
| `persistedAssignments=false` rozeti | "No assignments were persisted." | Backend flag'i **birebir** yansıtılır |
| RESX | 7 dil × 130 anahtar, parity ✅ | +54 yeni anahtar |

**UI'da bilinçli olarak YOK:** Apply · Assign Accounts · Persist · Resource/MR assignment · Workflow approval ·
Evidence export butonları. Rule type seçicisi yalnız FU03'ün değerlendirdiği tipleri sunar, böylece form backend'in
reddedeceği bir kural üretemez.

---

## 6. Preview Behavior

| Scenario | Result | Notes |
|---|---|---|
| Kural yok | Kontrollü boş preview + uyarı | 200, `matchedAccounts=[]` |
| Tek geography kuralı | Yalnız kriterlere uyan hesaplar aday | `matchReason` = `country=tr AND city=istanbul` biçiminde |
| account-type kuralı | Sınıflandırmaya göre eşleşme | type / category / status |
| account-list kuralı | **Yalnız** include listesindeki hesaplar | Attribute filtreleri uygulanmaz |
| Exclude listesi | Adayı kaldırır | Eşleşmeye her zaman baskın |
| İki kural → **farklı** node | **Çakışma** | Kazanan düşük Priority; kaybeden `conflict-loser` |
| İki kural → **aynı** node | Çakışma **değil** | Yalnızca yedekli kapsam |
| Devre dışı kural | Atlanır (`rule disabled`) | `skippedRuleCount` ile raporlanır |
| Etkinlik penceresi dışındaki kural | Atlanır (`not yet effective` / `expired`) | |
| Tek kural preview (`ruleId`) | Yalnız o kural | Diğerleri hiç değerlendirilmez |
| Gelecek tarihli model | **Model penceresine göre** değerlendirilir | `effectiveAt` + uyarı; aksi hâlde planlamacıya boş preview dönerdi |
| Hesap tabanı büyükse | `scannedAccounts` + cap uyarısı | Varsayılan 2000, tavan 10000 |
| **Her senaryoda** | `persistedAssignments=false` | Hiçbir atama yazılmaz |

### `effectiveAt` kararı (smoke sırasında bulundu)

İlk canlı koşuda 2030 pencereli bir model için preview boş döndü: bütün kurallar "henüz yürürlükte değil" diye
atlanıyordu. Bu teknik olarak doğru ama **planlamacı için yanıltıcıydı** — gelecek dönemin modelini kuran kullanıcı
"yürürlüğe girdiğinde ne üretir?" sorusunu sorar. Preview artık kural pencerelerini **model penceresine sıkıştırılmış
bir ana** göre değerlendirir (`effectiveAt`), bugün yürürlükte olan model için bu yine "şimdi"dir. Model penceresinden
tamamen önce biten bir kural hâlâ `expired` olarak atlanır (regresyon testi mevcut).

---

## 7. Validation / Conflict Behavior

| Scenario | Expected | Result |
|---|---|---|
| Model yok / cross-tenant | 404 | ✅ 404 |
| Model draft değil (active/inactive/archived) → rule create/update/delete | 409 | ✅ 409 |
| Duplicate `RuleCode` (model içinde) | 409 | ✅ 409 |
| Publish edilmemiş `RuleType` değeri | 400 fail-closed | ✅ 400 |
| Publish edilmiş ama FU03'ün değerlendirmediği tip (`product-portfolio` vb.) | 400 "later FU" | ✅ 400 |
| Geçersiz `ConflictPolicy` | 400 | ✅ 400 |
| `territory-rule-type` seti publish değil | 400 fail-closed | ✅ 400 |
| Hedef node model dışında | 404 | ✅ 404 |
| Boş kriter | 400 | ✅ 400 |
| `geography` kuralında coğrafya kriteri yok | 400 | ✅ 400 |
| `account-list` kuralında include listesi yok | 400 | ✅ 400 |
| Aynı account id include **ve** exclude'da | 400 | ✅ 400 |
| Kural tarihleri model penceresi dışında | 400 | ✅ 400 |
| `EffectiveTo < EffectiveFrom` | 400 | ✅ 400 |
| Kriter değerleri trim + case-insensitive dedupe | normalize | ✅ `[" tr ","TR","","us"] → ["tr","us"]` |
| Archived modelde preview | 409 | ✅ 409 |
| Payload'da TenantId | asla gönderilmez / server-resolved | ✅ komut kaydında alan yok |

---

## 8. Tests

| Suite | Result | Notes |
|---|---|---|
| CrmService API build | **PASS** | 0 error |
| `TerritoryAssignmentRuleTests` | **PASS 20/20** | create/update/soft-delete/list; reference + criteria + draft-only + tenant isolation |
| `TerritoryAssignmentPreviewTests` | **PASS 22/22** | geography/account-type/account-list, AND-kombinasyonu, conflict + non-conflict, disabled/expired skip, exclude, single-rule, scan cap, archived reddi, fail-closed, **mutasyon yokluğu**, `effectiveAt` (3 test) |
| `TerritoryScopeGuardTests` (genişletildi) | **PASS** | apply/resource/evidence/export route yok · FU03 route'ları var · permission sayısı 5 · **`AccountTerritoryAssignment` tipi domain assembly'de yok** · account seam'inde yazma üyesi yok |
| `TerritoryContractTests` (genişletildi) | **PASS** | FU03 flag'leri + apply/resource false + runtimeScope |
| **Territory toplam** | **118/118** | 71 → 118 (+47) |
| **Tüm CrmService Application suite** | **290/290** | 240 → 290 |
| Web build (C# + Razor) | **PASS** | İzole output; çalışan Web process'ine dokunulmadı |
| JavaScript syntax | **PASS** | `assignment-rules.js`, `hierarchy.js` |
| RESX parity | **PASS** | 130 anahtar × 7 dil, birebir aynı anahtar kümesi |
| Static guard grep | **PASS** | direct `:5061` yok · JS'te apply/assign çağrısı yok |

> İki guard testi (scope + contract) FU03 kapsam genişlemesini **doğru şekilde kırmıştı**; sınırı FU03'e taşıyarak
> güncellendi ve aynı sertlikte tutuldu (apply/resource/evidence hâlâ yasak, artı yeni "aggregate hiç yok" testi).

---

## 9. Live / Manual Smoke

Gateway-only (`:5000`), `X-Tenant-Id` header, payload'da TenantId yok. **49/49 PASS.**

| Step | Result | Notes |
|---|---|---|
| Login (hedef tenant) | ✅ | 5/5 territory claim |
| Contract — FU03 flag'leri | ✅ 11/11 | rules/preview true; apply/resource false; lifecycle bozulmadı; permission 5 |
| Draft model + 3 node | ✅ | country + 2 zone |
| Boş kural listesi | ✅ | `totalCount=0`, `isEditable=true` |
| Geography kuralı oluştur | ✅ 201 | `country=tr` |
| Duplicate RuleCode | ✅ 409 | |
| Publish edilmemiş rule type | ✅ 400 | *"is not a published value of reference set 'territory-rule-type'"* |
| Deferred rule type (`product-portfolio`) | ✅ 400 | *"published but not evaluated in FU03 (later FU)"* |
| Geçersiz conflict policy | ✅ 400 | |
| Model dışı hedef node | ✅ 404 | |
| Boş kriter | ✅ 400 | |
| Kural güncelle + geri oku | ✅ | `warn`/priority 5 yansıdı |
| **Preview çalıştır** | ✅ 200 | **7 aday / 10 hesap**, 3 eşleşmeyen, `matchReason=country=tr` |
| `persistedAssignments` | ✅ **false** | |
| İkinci kural (farklı node) → **çakışma** | ✅ | **4 çakışma**, 2 aday node, kazanan p5, winner/loser 4/4 |
| `resolutionSuggestion` | ✅ | *"Policy 'warn': rule 'R-GEO-…' wins on priority; the conflict is reported only."* |
| Devre dışı kural atlanır | ✅ | 1 evaluated / 1 skipped / 0 conflict |
| Tek kural preview | ✅ | 1 evaluated |
| **Apply / resource / evidence / submit-approval / export endpoint'i** | ✅ **404 ×5** | Endpoint'ler mevcut değil |
| Active modelde kural create | ✅ 409 | *"can only be changed on a draft territory model"* |
| Active modelde preview | ✅ 200 | 7 aday |
| Archived modelde preview | ✅ 409 | |
| Archived modelde kural listesi | ✅ 200, `isEditable=false` | Salt okunur |
| Account master değişmedi | ✅ | 10 hesap, `updatedAt` boş |
| Tenant son durumu | ✅ | Smoke modelleri archived; **0 aktif model** |

---

## 10. Guard Checks

| Check | Result |
|---|---|
| Account assignment apply implemented? | **No** |
| AccountTerritoryAssignment persisted? | **No** — aggregate/repository/koleksiyon hiç yok (test ile pinlendi) |
| Account master changed? | **No** — preview seam'inde yazma üyesi yok; canlıda `updatedAt` boş |
| Contact changed? | **No** |
| Account/Contact entity'ye Territory alanı eklendi mi? | **No** |
| Resource assignment implemented? | **No** |
| Workflow approval implemented? | **No** |
| Submit/approve/reject eklendi mi? | **No** |
| Evidence / import / export implemented? | **No** |
| Brand Scope implemented? | **No** |
| Product/Brand master touched? | **No** |
| Hard delete added? | **No** — yalnız soft-delete |
| Mongo hand-edit? | **No** |
| MOD-0048 publish changed? | **No** |
| RBAC seed/grant changed? | **No** |
| Gateway route changed? | **No** — mevcut `/api/crm/*` route'u kullanıldı |
| Forbidden permissions added? | **No** — `crm.territory.delete` / `crm.micro-zone.manage` yok; permission sayısı 5'te sabit |
| Direct `:5061` used? | **No** (yalnız health) |
| TenantId payload used? | **No** |
| Contract apply flag false? | **Yes** |
| Preview side-effect-free? | **Yes** — yapısal olarak (read-only seam + aggregate yokluğu) ve testle |
| Hardcoded reference fallback? | **No** — fail-closed |
| HEAD / porcelain | `094e3a86` sabit · 422 → 426 (bu task'ın 4 yeni dosyası + rapor) |

---

## 11. Created / Updated Files

| File | Action | Notes |
|---|---|---|
| `services/…/Domain/Entities/TerritoryAssignmentRule.cs` | **Created** | Aggregate + `TerritoryRuleCriteria` typed value object |
| `services/…/Domain/Repositories/ITerritoryAssignmentRuleRepository.cs` | **Created** | Rule repo + **read-only** `ITerritoryAccountReader` + `TerritoryAccountSnapshot` |
| `services/…/Application/Features/Territory/AssignmentRules/TerritoryRuleTypes.cs` | **Created** | FU03'ün değerlendirdiği tipler (fallback değil) |
| `…/AssignmentRules/TerritoryAssignmentRuleCommands.cs` | **Created** | Create/Update/SoftDelete/Preview komutları |
| `…/AssignmentRules/TerritoryAssignmentRuleDtos.cs` | **Created** | Query + DTO'lar + mapper/özetleyici |
| `…/AssignmentRules/TerritoryAssignmentRuleValidation.cs` | **Created** | Normalize + reference/criteria/date validation |
| `…/AssignmentRules/TerritoryAssignmentPreviewEngine.cs` | **Created** | Saf matcher + çakışma + resolution suggestion |
| `…/AssignmentRules/Handlers/TerritoryAssignmentRuleCommandHandlers.cs` | **Created** | Draft-only write handlers |
| `…/AssignmentRules/Handlers/TerritoryAssignmentRuleQueryHandlers.cs` | **Created** | List + GetById |
| `…/AssignmentRules/Handlers/PreviewTerritoryAssignmentsHandler.cs` | **Created** | Preview orkestrasyonu + `effectiveAt` |
| `services/…/Persistence/Repositories/TerritoryAssignmentRuleRepository.cs` | **Created** | Mongo repo + `TerritoryAccountReader` (salt okuma) |
| `services/…/Persistence/DependencyInjection.cs` | Updated | DI kaydı + `TerritoryAssignmentRule`/`TerritoryRuleCriteria` class map |
| `services/…/Api/Controllers/CRM/TerritoryModelsController.cs` | Updated | 6 FU03 endpoint + preview request record |
| `…/Features/Territory/Contract/TerritoryContractDto.cs` | Updated | 4 yeni flag; `Fu01` → `Current` |
| `…/Features/Territory/Contract/GetTerritoryContractHandler.cs` | Updated | runtimeScope + limitations |
| `services/…/tests/…/Territory/TerritoryAssignmentRuleTests.cs` | **Created** | 20 test |
| `services/…/tests/…/Territory/TerritoryAssignmentPreviewTests.cs` | **Created** | 22 test |
| `services/…/tests/…/Territory/FakeTerritoryInfrastructure.cs` | Updated | Rule repo + account reader fake; rule-type/policy sözlüğü; readiness `MissingSets`'i yansıtır |
| `services/…/tests/…/Territory/TerritoryScopeGuardTests.cs` | Updated | FU03 sınırına taşındı + 4 yeni guard |
| `services/…/tests/…/Territory/TerritoryContractTests.cs` | Updated | FU03 flag + runtimeScope testleri |
| `frontend/…/Controllers/CRM/TerritoryManagementController.cs` | Updated | 5 proxy action + rule payload dönüştürücü |
| `frontend/…/Models/CRM/TerritoryViewModels.cs` | Updated | FU03 view model + payload'ları |
| `frontend/…/Views/CRM/TerritoryManagement/_AssignmentRules.cshtml` | **Created** | Kural listesi + preview paneli + form offcanvas |
| `frontend/…/Views/CRM/TerritoryManagement/Details.cshtml` | Updated | Partial + veri bloğu + script |
| `frontend/…/wwwroot/assets/js/CRM/TerritoryManagement/assignment-rules.js` | **Created** | Liste/form/preview davranışı |
| `frontend/…/Resources/Views/CRM/TerritoryManagement/*.resx` (7 dil) | Updated | +54 anahtar → 130, parity ✅ |
| `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` | Updated | frontmatter `runtime_code_scope` + FU03 sınır notu |
| `docs/audits/mod-0151-fu03-assignment-rules-preview-implementation-2026-07-28.md` | **Created** | Bu rapor |

---

## 12. Final Verdict

### **PASS**

- Assignment rule CRUD çalışıyor (draft-only, reference fail-closed, kriter whitelist).
- Preview çalışıyor: aday hesaplar, eşleşme gerekçesi, hedef node, kural izi.
- Çakışma tespiti çalışıyor: farklı node'lar için çakışma, öncelikle kazanan, politika + öneri.
- **Preview yan etkisizdir** — yapısal olarak (read-only seam + `AccountTerritoryAssignment` aggregate'inin hiç
  bulunmaması) ve canlıda doğrulanmıştır.
- Contract flag'leri doğru; apply/resource **false**; FU02B lifecycle bozulmadı.
- UI mevcut; Apply/Assign/Persist butonu yok, FU04/FU05 beklentisi kullanıcıya açıkça yazılı.
- Testler/build/smoke geçiyor: **118/118 Territory · 290/290 suite · 49/49 canlı**.
- Guardrail'ler korundu; yeni permission, RBAC/Gateway/Mongo/publish değişikliği yok.

---

## 13. Next Recommended Prompt

**Sıradaki iş — iki geçerli seçenek var; öneri FU04:**

`@orchestrator MOD-0151 FU04 — Resource Assignments`

Gerekçe: FU05 (Account Assignment Apply + History) preview çıktısını gerçek atamaya çevirir ve `ended`/effective-dating
semantiği ile MOD-0149 CoverageSummary projection'ını da beraberinde getirir — yani **geri alması en pahalı** adımdır.
FU04 ise zone/microzone'a MR ve yönetici bağlayarak modülün asıl saha değerini açar, FU05'ten bağımsızdır ve FU05
uygulanmadan önce coverage tablosunun tamamlanmasını sağlar. Alternatif olarak `MOD-0151 FU05 — Account Assignment
Apply + History` da doğrudan açılabilir; sıralama kararı pack sahibinindir.

### Bu task'tan çıkan follow-up'lar

| # | Konu | Neden ayrı |
|---|---|---|
| 1 | **`crm.territory.assignment.read/manage` RBAC seed** | Pack §19 rule/preview için bu anahtarları önerir; FU03 mevcut `model.read`/`model.manage` ile ilerledi (FU02B precedent'i) çünkü bu task RBAC seed/grant değiştiremez. FU04/FU05'te assignment yüzeyi büyüdüğünde ayrı bir RBAC task'ı ile hizalanmalı |
| 2 | **Kalan rule type'ları** (`product-portfolio`, `business-scope`, `channel`, `segment`, `manual`, `import`) | Publish edilmiş ama FU03 değerlendirmiyor; kontrollü 400 dönüyor. Product/Brand master ve business scope olgunlaştıkça açılmalı |
| 3 | **Preview scan cap** | Varsayılan 2000 hesap; büyük tenant'larda sayfalama/indeks stratejisi FU05 apply öncesi gözden geçirilmeli |
| 4 | **Lifecycle audit log sink** (FU02B'den devam) | CrmService structured event'leri hâlâ dosyaya düşmüyor |
