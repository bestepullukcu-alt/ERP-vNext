# WORK PACKAGE — WP-MOD0162-AUD-UI · AudienceProfile dimension builder (reference-data-driven, cascade + custom)

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test tasarım geri bildirimi (2. tur). AudienceProfile'ın multi-axis `Dimensions`'ı serbest-metin yerine **business reference data'dan (account-type/contact-type/medical-specialty) seçilebilmeli**, + custom eksen, + soft cascade (contact-type=doctor → medical-specialty). Module: **MOD-0162** (AudienceProfile; SCMM-11-AUD ForWhom tüketicisi). Branch: `feature/scmm-content-studio` (HEAD `b743e090`). **Yalnız frontend** (Diten.Web); backend/API/RBAC'a **DOKUNMA** (hazır).

## KARAR — ne saklanır (owner sorusu üzerine, KİLİTLİ)
> **Stabil `ValueCode` saklanır, DisplayName DEĞİL, opaque version-id DE DEĞİL.** Reference value şekli `{ValueCode, DisplayName, IsDeprecated, ReplacementValueCode, ParentValueCode}`. `AudienceDimensionAssignment.AxisCode` = reference **set kodu** (`medical-specialty`), `Values` = reference **ValueCode**'lar (`nephrology`). DisplayName render'da `published-values` lookup ile çözülür. **Version pinleme YOK** (audience targeting canlı çözer; SCMM Content-Set'in provenance-pin'inden farklı — kasıtlı). Deprecated değer: kayıtlı kod korunur + UI'da "deprecated/replacement" rozeti (IsDeprecated/ReplacementValueCode). Custom eksen = serbest AxisCode + serbest Values (reference dışı; net ayrım).

## Ölçülmüş girdi (CT)
- **Backend HAZIR (DOKUNMA):** `AudienceProfile.Dimensions = List<AudienceDimensionAssignment{AxisCode, Values[]}>` (Domain). CQRS: `AudienceDimensionAssignmentInput(AxisCode, Values)` + `Dimensions` Create/Update command'de; handler `ValidateDimensions`/`ToDomain` (full-replace); API `KnowledgeAudienceProfilesController` Create/Update `Dimensions` (`AudienceDimensionAssignmentRequest{AxisCode,Values}`) kabul ediyor. DTO Dimensions döndürüyor. **Backend değişikliği YOK** (bu WP frontend-only).
- **Backend kuralı (frontend uymalı):** `ValidateDimensions` — AxisCode sabit vocab'a **kısıtlı DEĞİL** (medical-specialty/contact-type/custom hepsi kabul), tekrarlı eksen 400, **her eksen ≥1 değer** (0-değerli eksen 400). → UI, 0-değerli/boş eksen satırını submit'ten ÖNCE düşürmeli.
- **Mevcut UI EKSİK:** AudienceProfile formu (`Views/CRM/Knowledge/Taxonomy.cshtml` `tax-only-profile` bloğu ~250-262 + `wwwroot/assets/js/CRM/Knowledge/taxonomy.js`) yalnız ProfileType/Status/SortOrder/Description veriyor; **dimension builder YOK**, `writePayload` (taxonomy.js ~582) `dimensions` göndermiyor → şu an dimension girilemiyor/edit'te korunmuyor.
- **Reference data + lookup HAZIR:** `business_reference_data_sets`'te **account-type · contact-type · medical-specialty** yayımlı; değerler `GET /api/v1/reference-data/sets/{setCode}/published-values?scope_key={tenant}` (mirror: `ContactsController` ~585-600 · `TerritoryManagementController` published-values). medical-specialty ~22 kod dolu.
- **Proxy deseni:** KnowledgeController same-origin proxy (audience-profiles gibi) — published-values için yeni proxy ucu buraya eklenir.

## Kapsam (yalnız frontend)
1. **Dimension builder** (AudienceProfile formu, `tax-only-profile` bloğu): "eksen ekle/çıkar" satırları. Her satır:
   - **Axis** = single select2, kaynak = **reference-set listesi** (`account-type` / `contact-type` / `medical-specialty` + **`custom`**). (Küçük, genişletilebilir sabit liste + custom.)
   - **Values** = multi-select2; axis reference-set ise o setin `published-values`'ından (ham kod yok, adla ara-seç); axis `custom` ise serbest AxisCode input + tag Values.
   - Ekle/çıkar; en az 0 satır (dimensions opsiyonel).
2. **Soft cascade (contact-type → medical-specialty):** bir contact-type dimension'ında **doctor/physician** değeri seçilince medical-specialty ekseni **otomatik açılır/önerilir** (UI kolaylığı; veri-güdümlü parent-child YOK — doctor değeri kod/adla tespit edilir). Doctor değeri güvenilir tespit edilemezse → medical-specialty'yi normal seçilebilir eksen olarak bırak (DUR değil, kararı belgele).
3. **Payload + load:** `writePayload('audience-profiles', …)` `dimensions: [{axisCode, values[]}]` ekle (full-replace; boş liste = temizle). Edit'te mevcut `dimensions` yüklenip render (kayıtlı ama artık listede olmayan değer korunur — mevcut "archived reference kept" deseni).
4. **published-values proxy:** KnowledgeController'a `GET api/reference-data/{setCode}/values` → `/api/v1/reference-data/sets/{setCode}/published-values?scope_key={tenant}` (ContactsController deseni; ReadPermission).
5. **7-dil L10n:** eksen/values/ekle-eksen etiketleri + custom — gerçek çeviri, key-echo yok.

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA (Dimensions uçtan uca hazır). AudienceProfile aggregate/command/DTO şeklini DEĞİŞTİRME. Reference-data yazma/publish (yalnız oku published-values). Subject/Topic formunu bozma (yalnız profile bölümü). Ham AxisCode/değer kodu girişi (custom hariç — reference eksende hep adla-seç). Cascade'i sert veri-bağı gibi kurma (soft UI). 7-dil key-echo. Başka modül/konsol.

## Acceptance
- **E2:** build temiz; AudienceProfile formunda dimension builder (axis select2 = account-type/contact-type/medical-specialty/custom; values reference-driven multi-select2 veya custom tag); ekle/çıkar; contact-type=doctor → medical-specialty cascade (veya belgelenmiş fallback); kaydet → `dimensions:[{axisCode,values}]` payload; edit → mevcut dimensions yüklenir; published-values proxy doğru uca; 7-dil key-echo yok; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** Nefrolog profili = contact-type:[doctor] + medical-specialty:[nephrology] seç+kaydet → DB'de `Dimensions` doğru; SCMM Chain Template ForWhom'da bu profil seçilebilir.
- Kapsam: yalnız Diten.Web (Taxonomy profile formu + taxonomy.js + KnowledgeController published-values proxy + resx). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-AUD-UI · Prompt v1.0  (AudienceProfile reference-data-driven dimension builder — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: b743e090 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-AUD-UI-reference-driven-dimensions.md (bu WP)
2. frontend/Diten.Web/Views/CRM/Knowledge/Taxonomy.cshtml (tax-only-profile bloğu ~250-262 + taxonomyCanvas offcanvas) + wwwroot/assets/js/CRM/Knowledge/taxonomy.js (writePayload ~567-586, openForm/load ~490-549, SPECS audience-profiles)
3. Reference-values MIRROR: frontend/Diten.Web/Controllers/CRM/ContactsController.cs (~585-600 published-values fetch) + TerritoryManagementController.cs (~1612)
4. Backend sözleşme (DOKUNMA, sadece şekil): services/Diten.CrmService/.../Application/Features/Knowledge/AudienceProfile/Commands/AudienceProfileCommands.cs (AudienceDimensionAssignmentInput{AxisCode,Values} + Dimensions) + Api/Models/CRM/KnowledgeRequests.cs (AudienceDimensionAssignmentRequest)
5. frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs (proxy deseni — published-values ucu buraya)

NE (yalnız frontend Diten.Web):
 0) KARAR (KİLİTLİ): SAKLANAN = stabil ValueCode (AxisCode=set kodu, Values=ValueCode'lar) — DisplayName DEĞİL, opaque version-id DE DEĞİL. DisplayName render'da published-values lookup ile çözülür. Version pinleme YOK (canlı çöz). Deprecated değer: kod korunur + rozet (IsDeprecated/ReplacementValueCode).
 1) AudienceProfile formuna (tax-only-profile) dimension builder: "eksen ekle/çıkar" satırları. Axis = single select2 (account-type/contact-type/medical-specialty/custom). Values = multi-select2 (reference axis→ o setin published-values'ından adla-seç ama SAKLANAN=ValueCode; custom→ serbest AxisCode input + tag Values). Dimensions opsiyonel (0+ satır).
 2) Soft cascade: contact-type dimension'ında doctor/physician değeri seçilince medical-specialty ekseni otomatik açıl/öner (UI kuralı; doctor değeri kod/adla tespit; güvenilir değilse medical-specialty'yi normal eksen bırak + kararı belgele, DUR değil).
 3) writePayload('audience-profiles',…) dimensions:[{axisCode,values[]}] ekle (full-replace; boş=temizle). Edit: mevcut dimensions yükle+render; listede olmayan kayıtlı değer korunur (mevcut archived-reference-kept deseni).
 4) KnowledgeController: GET api/reference-data/{setCode}/values → /api/v1/reference-data/sets/{setCode}/published-values?scope_key={tenant} (ContactsController deseni, ReadPermission).
 5) 7-dil resx (eksen/values/ekle-eksen/custom) — gerçek çeviri, key-echo YOK.
NASIL: ContactsController published-values fetch + mevcut taxonomy select2/writePayload/load desenini birebir izle. Backend Dimensions uçtan uca hazır — yalnız UI besler.
YAPMA: backend/API/RBAC/ocelot DEĞİŞTİR; aggregate/command/DTO şekli; reference-data yaz/publish (yalnız oku); Subject/Topic formunu boz; reference eksende ham kod girişi (adla-seç); cascade'i sert veri-bağı yap; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; dimension builder (axis select2 + reference/custom values); ekle/çıkar; contact-type=doctor→medical-specialty cascade (veya belgeli fallback); kaydet→dimensions payload; edit→mevcut dimensions yüklenir; published-values proxy; 7-dil key-echo yok.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: published-values ucu beslenemezse · Dimensions payload/DTO şekli beklenenden farklıysa · cascade doctor-değeri güvenilir tespit edilemezse (fallback+belgele, DUR değil) · kapsam frontend dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → sonra ALMIBA retest'inde AudienceProfile'ı reference-driven gir (contact-type=doctor + medical-specialty=nephrology) → A2..A8 devam.
