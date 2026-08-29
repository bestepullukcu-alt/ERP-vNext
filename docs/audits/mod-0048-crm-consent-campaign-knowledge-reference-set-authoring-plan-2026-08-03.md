# MOD-0048 — CRM Consent / Campaign / Knowledge Reference Set Authoring Plan (2026-08-03)

> **Görev tipi:** Yalnız **analiz + authoring plan**. Kod yazma **yok**, runtime **yok**, MOD-0048 publish **yok**, seed/grant **yok**, registry write **yok**, UI **yok**, Mongo hand-edit **yok**.
> **Amaç:** MOD-0164 (Consent/Preference), MOD-0165 (Campaign/Targeting), MOD-0167 (Segmentation boundary), MOD-0162 (Knowledge/Content/Path/Journey/Concept Graph), MOD-0165-FU03 (Visit Frequency) ve MOD-0290 (Brand/Product boundary) için gereken controlled vocabulary / reference set ihtiyaçlarını çıkarmak, initial value önermek, required/optional ayrımı yapmak, çakışmaları raporlamak ve authoring artifact önermek.
> **Sonuç:** **PARTIAL** (gerekçe §18) — inventory çıkarıldı; iki set için governance çakışması ve iki taxonomy için owner belirsizliği follow-up'a bırakıldı.

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Görev türü | Documentation / authoring-plan (implementation değil) |
| Değiştirilen runtime kod | **0 dosya** |
| MOD-0048 authoring modeli | **Doğrulandı** — PSS-012 Business Reference Data: `BusinessReferenceDataSet` → `Version` → `Value`; `TenantScopedEntity`; `SetCode` tenant başına unique |
| Value şeması | `BusinessReferenceDataValue { valueCode, displayName, sortOrder, isDeprecated, parentValueCode?, attributes{} }` — `description` first-class değil, `attributes` altında taşınır (kaynak: [mod-0149 authoring template](mod-0149-crm-reference-data-authoring-template.json)) |
| Authoring path | Governance UI create-set → add values → validate → submit → approve → publish **VEYA** `POST /imports/preview` + `POST /imports/{previewId}/commit` (Idempotency-Key zorunlu); tümü **Gateway 5000** `/api/v1/reference-data/*` üzerinden |
| JSON naming | **camelCase** (MOD-0048 pack policy) |
| Mevcut CRM tüketim pattern'i | **Doğrulandı** — `ContactAvailabilityReferenceSets` / `VisitFrequencyPolicyReferenceSets`: static const set-code sınıfı; yayımlanmamış zorunlu set = **fail-closed 400**, asla local fallback list yok |
| Baseline duplicate taraması | `contact-availability-*`, `account-*`, `workplace-*`, `address-type`, `status-reason` mevcut/taslak; önerilen consent/campaign/frequency/knowledge/concept set kodları ile **çakışma yok** (§15) |

**Kaynak dosyalar (okundu, değiştirilmedi):**
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/ContactAvailability/ContactAvailabilityModels.cs` (tüketim pattern'i)
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/VisitFrequencyPolicy/VisitFrequencyPolicyReferenceSets.cs` (frequency set kodları + beklenen value sayıları)
- `docs/audits/mod-0164-consent-preference-management-boundary-pack-authorization-2026-08-02.md`
- `docs/audits/campaign-targeting-boundary-pack-authorization-2026-08-02.md`
- `docs/audits/knowledge-content-subject-taxonomy-pack-authorization-2026-08-02.md`
- `docs/audits/mod-0162-fu01c-subject-concept-graph-configurable-concept-chain-pack-authorization-2026-08-02.md`
- `docs/audits/mod-0149-crm-reference-data-authoring-template.json` + `mod-0048-contact-availability-reference-sets-publish-2026-08-02.md`

---

## 2. Dependency Confirmation

| Karar / Ön koşul | Durum | Bu plana etkisi |
|---|---|---|
| MOD-0164-FU01 Consent & Preference Boundary | **PASS** | Consent set kodları + vocab boundary'de zaten sabitlendi (§13 boundary raporu); bu plan onu takip eder |
| MOD-0164-FU02 Consent Runtime planı | Planlı | Consent setleri **runtime-required** — bu plan bu FU'yu unblock eder |
| MOD-0165-FU02 Campaign / Targeting Boundary | **PASS** | `campaign-target-type` vocab boundary'de sabit |
| MOD-0165-FU03 Visit Frequency runtime | **PASS** | Runtime **in-domain constants** ile çalışıyor; setler MOD-0048 **alignment** için, runtime bloklamaz |
| MOD-0167-FU01 Segment-sourced frequency policy | **PASS** | Segment consent filter tüketir; ayrı reference set üretmez |
| MOD-0162-FU01/A/B/C Knowledge / Path / Journey / Concept Graph | **PASS** | Knowledge & concept set kodları boundary'lerde belirtildi; **content-type vocab çakışması var** (§15) |
| MOD-0290-FU01 Brand/Product Boundary | **PASS** | Brand/Product master ayrı SoR; therapeutic-area/atc karar noktası (§10) |

---

## 3. Scope Confirmation

**Yapıldı (bu plan):** (1) reference set listesi, (2) her set için code/name/description, (3) initial values, (4) tüketen modül, (5) required/optional ayrımı, (6) runtime dependency risk işareti, (7) publish sırası, (8) çakışma/duplicate raporu, (9) authoring JSON + import template kolon önerisi, (10) evidence report.

**Yapılmadı (kapsam dışı):** MOD-0048 publish · runtime code · UI · seed/grant · permission change · registry write · hardcoded enum migration · existing value destructive change · delete · Mongo hand-edit.

---

## 4. Reference Set Inventory

Toplam **33 reference set önerisi** (2 taxonomy — `therapeutic-area`, `atc-code` — reference set OLARAK **önerilmez**, §10/§15 follow-up).

| # | SetCode | Grup | ModuleOwner | RequiredLevel |
|---|---|---|---|---|
| 1 | `consent-channel` | Consent | MOD-0164 | Runtime-required |
| 2 | `consent-purpose` | Consent | MOD-0164 | Runtime-required |
| 3 | `consent-legal-basis` | Consent | MOD-0164 | Runtime-required ⚠️ vocab çakışması |
| 4 | `consent-status` | Consent | MOD-0164 | Runtime-required |
| 5 | `consent-source` | Consent | MOD-0164 | Optional (provenance) |
| 6 | `preference-type` | Preference | MOD-0164 | Runtime-required |
| 7 | `preference-value` | Preference | MOD-0164 | Optional (bkz. §5, hibrit) |
| 8 | `campaign-type` | Campaign | MOD-0165 | UI-authoring |
| 9 | `campaign-status` | Campaign | MOD-0165 | Runtime-required |
| 10 | `campaign-target-status` | Campaign | MOD-0165 | UI-authoring |
| 11 | `campaign-target-type` | Campaign | MOD-0165 | Runtime-required |
| 12 | `campaign-target-source` | Campaign | MOD-0165 | Runtime-required |
| 13 | `campaign-objective-type` | Campaign | MOD-0165 | Optional/future |
| 14 | `visit-frequency-type` | Frequency | MOD-0165/0167 | Alignment (runtime in-domain) |
| 15 | `visit-frequency-period-type` | Frequency | MOD-0165/0167 | Alignment |
| 16 | `visit-frequency-source` | Frequency | MOD-0165/0167 | Alignment |
| 17 | `visit-frequency-status` | Frequency | MOD-0165/0167 | Alignment |
| 18 | `visit-frequency-target-type` | Frequency | MOD-0165/0167 | Alignment |
| 19 | `knowledge-content-type` | Knowledge | MOD-0162 | Runtime-required ⚠️ vocab çakışması |
| 20 | `knowledge-content-status` | Knowledge | MOD-0162 | Runtime-required |
| 21 | `knowledge-path-step-type` | Knowledge | MOD-0162 | Runtime-required |
| 22 | `knowledge-path-status` | Knowledge | MOD-0162 | UI-authoring |
| 23 | `knowledge-journey-status` | Knowledge | MOD-0162 | UI-authoring |
| 24 | `knowledge-content-source` | Knowledge | MOD-0162 | Optional (provenance) |
| 25 | `knowledge-version-pin-policy` | Knowledge | MOD-0162 | Optional/future |
| 26 | `knowledge-completion-rule` | Knowledge | MOD-0162 | Optional/future |
| 27 | `concept-relationship-type` | Concept Graph | MOD-0162-FU01C | Runtime-required |
| 28 | `concept-status` | Concept Graph | MOD-0162-FU01C | Runtime-required |
| 29 | `concept-chain-template-status` | Concept Graph | MOD-0162-FU01C | UI-authoring |
| 30 | `concept-external-ref-type` | Concept Graph | MOD-0162-FU01C | Optional |
| 31 | `product-dosage-form` | Brand/Product | MOD-0290 | Optional/future |
| 32 | `product-status` | Brand/Product | MOD-0290 | UI-authoring |
| 33 | `brand-status` | Brand/Product | MOD-0290 | UI-authoring |
| — | `therapeutic-area` | Brand/Product | **belirsiz** | **ConceptNode olarak kalmalı** (§10 follow-up) |
| — | `atc-code` | Brand/Product | **belirsiz** | **External taxonomy** (§10 follow-up) |

---

## 5. Consent / Preference Sets

> **Kaynak boundary:** MOD-0164-FU01 §5/§6/§13. Channel/Purpose/Status/PreferenceType vocab boundary ile **birebir** eşleşir; **`consent-legal-basis` çakışır** (§15-A).

**1. `consent-channel`** — Consent Channel — *Consent'in geçerli olduğu iletişim kanalı.*
`visit` · `email` · `sms` · `phone` · `whatsapp` · `portal` · `digital-detailing` · `training` · `other`

**2. `consent-purpose`** — Consent Purpose — *Consent'in verildiği işleme amacı.*
`campaign` · `medical-visit` · `product-information` · `training` · `marketing` · `service` · `compliance` · `research` · `other`

**3. `consent-legal-basis`** — Consent Legal Basis — *KVKK/GDPR hukuki dayanak.* ⚠️ **Boundary ile hizalanmalı**
Task önerisi: `explicit-consent` · `contract` · `legal-obligation` · `legitimate-interest` · `public-interest` · `vital-interest` · `other`
**MOD-0164-FU01 §12'nin sabitlediği vocab:** `consent` · `legitimate-interest` · `contract` · `legal-obligation` · `vital-interest` · `public-task`
**Önerilen çözüm:** boundary vocab'ı **canonical** kabul et (`consent`, `public-task`), value code'ları GDPR maddeleriyle hizala; `explicit-consent`/`public-interest`/`other` yalnız EA onayı sonrası eklenmeli. **Karar EA'ya (F1) — publish öncesi netleşmeli.**

**4. `consent-status`** — Consent Status — *Consent yaşam döngüsü durumu.*
`granted` · `denied` · `withdrawn` · `restricted` · `unknown` · `expired`
Kural (boundary §5): **`unknown` sessizce `granted` sayılmaz**; `expired`/window-dışı targeting'e giremez.

**5. `consent-source`** — Consent Source — *Consent kaydının provenance'ı.*
`manual` · `import` · `legacy-import` · `external-consent-center` · `campaign` · `system` · `other`

**6. `preference-type`** — Preference Type — *Tercih kaydı tipi.*
`preferred-channel` · `do-not-contact` · `do-not-visit` · `preferred-visit-window` · `language-preference` · `content-preference` · `frequency-cap` · `topic-interest`

**7. `preference-value`** — Preference Value — *Tercih değeri (hibrit).*
**Değerlendirme:** tek generic set olarak **tutulmamalı**. Öneri: **hibrit** —
- `preferred-channel` değeri → `consent-channel`'dan referanslanır (parentValueCode ile),
- `language-preference` değeri → platform dil listesinden referanslanır,
- `content-preference`/`topic-interest` → `knowledge-content-type` / concept node'lardan,
- `do-not-contact`/`do-not-visit` → boolean-benzeri, ayrı value gerekmez,
- `frequency-cap` → sayısal, controlled value değil.
Yalnızca gerçekten serbest-metin olması gereken alanlar generic value taşır. **Serbest metin yerine controlled value tercih edilir.** Bu setin nihai kırılımı MOD-0164-FU02 runtime tasarımıyla kesinleşmeli (follow-up).

---

## 6. Campaign / Targeting Sets

> **Kaynak boundary:** MOD-0165-FU02 §… `campaign-target-type` = `account` · `contact` · `account-contact-link` · `segment` · `territory-node` · `concept-node` · `audience-profile` (7 değer).

**8. `campaign-type`** — Campaign Type
`product-campaign` · `education-campaign` · `awareness-campaign` · `service-campaign` · `compliance-campaign` · `training-campaign` · `other`

**9. `campaign-status`** — Campaign Status
`draft` · `active` · `paused` · `completed` · `cancelled` · `archived`

**10. `campaign-target-status`** — Campaign Target Status
`draft` · `active` · `inactive` · `completed` · `excluded` · `archived`

**11. `campaign-target-type`** — Campaign Target Type ⚠️ **8. değer `campaign-target` boundary'de yok**
Task önerisi: `account` · `contact` · `account-contact-link` · `segment` · `territory-node` · `concept-node` · `audience-profile` · `campaign-target`
Boundary vocab (7): son `campaign-target` **yok**. **Öneri:** `campaign-target`'ı **çıkar** (bir target'ın kendisini hedef göstermesi döngü riski) veya boundary'yi güncelle; boundary canonical → **çıkarmayı öner**. Not: frequency tarafı `TargetType=campaign-target` kullanıyor (§7) → set **ortaklaştırılmamalı** (§15-E).

**12. `campaign-target-source`** — Campaign Target Source
`manual` · `segment` · `import` · `legacy-import` · `business-rule` · `manager-selection` · `campaign-rule` · `other`

**13. `campaign-objective-type`** — Campaign Objective Type *(optional/future)*
`awareness` · `education` · `conversion` · `reinforcement` · `objection-handling` · `retention` · `compliance` · `training` · `other`

---

## 7. Visit Frequency Sets

> **Kaynak:** `VisitFrequencyPolicyReferenceSets.cs` — runtime **in-domain constants** ile doğruluyor; bu setler **MOD-0048 alignment** amaçlı, `Optional`/non-blocking. Beklenen value sayıları koda gömülü (F1 template).

**14. `visit-frequency-type`** (kod beklenen sayı **5**) — `weekly` · `biweekly` · `monthly` · `cycle-based` · `custom`
**15. `visit-frequency-period-type`** (**7**) — `day` · `week` · `month` · `quarter` · `cycle` · `campaign-period` · `custom`
**16. `visit-frequency-source`** (**7**) — `campaign` · `segmentation` · `manual` · `legacy-import` · `business-rule` · `manager-override` · `other`
**17. `visit-frequency-status`** (**4**) — `draft` · `active` · `inactive` · `archived`
**18. `visit-frequency-target-type`** (**8**) — `account` · `contact` · `account-contact-link` · `segment` · `territory-node` · `campaign-target` · `concept-node` · `audience-profile`

**Alignment notu:** Önerilen value sayıları koddaki beklenen sayılarla **birebir** (5/7/7/4/8). Publish edilirse `VisitFrequencyPolicyReferenceSets.Optional` readiness raporu "published" gösterecek; runtime davranışı **değişmez** (in-domain vocab canonical kalır). Bu yüzden bu 5 set **runtime-blocking değil**.

---

## 8. Knowledge / Content Sets

> **Kaynak boundary:** MOD-0162-FU01 §… Vocab'lar MOD-0048 set'i olacak (hardcoded fallback yasak). ⚠️ **`knowledge-content-type` boundary'de FARKLI vocab ile taahhüt edildi** (§15-D).

**19. `knowledge-content-type`** — Knowledge Content Type ⚠️ **BÜYÜK ÇAKIŞMA**
Task önerisi: `article` · `document` · `slide-deck` · `video` · `image` · `faq` · `script` · `message` · `clinical-summary` · `lesson` · `quiz` · `assignment` · `checklist` · `external-link` · `other`
**MOD-0162-FU01 §… boundary vocab'ı:** `quiz` · `video` · `pdf` · `html-detail` · `sop` · `training-material` · `message-script` · `knowledge-article`
**Fark:** `document`↔`pdf`, `article`↔`knowledge-article`, `script`+`message`↔`message-script`, ek `html-detail`/`sop`/`training-material`; task tarafında `slide-deck`/`image`/`faq`/`clinical-summary`/`lesson`/`assignment`/`checklist`/`external-link` yeni. **Öneri:** boundary vocab canonical; task'taki ek tipler **superset** olarak boundary'yi genişletmek üzere önerilmeli, ama code'lar boundary ile hizalanmalı (`knowledge-article`, `pdf`, `message-script` korunmalı). **Publish öncesi MOD-0162 owner ile uzlaştır — F2.**

**20. `knowledge-content-status`** — `draft` · `review` · `approved` · `published` · `inactive` · `archived`
**21. `knowledge-path-step-type`** — `intro` · `core-message` · `clinical-evidence` · `indication` · `brand-message` · `objection-handling` · `faq` · `practice` · `quiz` · `assignment` · `summary` · `closing` · `lesson` · `vocabulary` · `grammar` · `listening` · `speaking` · `reading` · `homework`
> Not: bu set pharma + dil eğitimi step'lerini birlikte taşır (concept graph pack'in "aynı motor, farklı subject" kararıyla tutarlı). Multi-domain olduğu için **generic** kalmalı, tenant/subject bazlı alt-set açılmamalı.

**22. `knowledge-path-status`** — `draft` · `review` · `approved` · `published` · `inactive` · `archived`
**23. `knowledge-journey-status`** — `draft` · `review` · `approved` · `published` · `inactive` · `archived`
> §15-C: path/journey/content status'ları **ayrı setler** olarak kalmalı (aynı değerleri taşısalar da yaşam döngüleri bağımsız evrilir).

**24. `knowledge-content-source`** — `manual` · `legacy-import` · `document-management` · `campaign` · `training` · `external` · `other`
**25. `knowledge-version-pin-policy`** *(optional)* — `pinned` · `latest-published`
**26. `knowledge-completion-rule`** *(optional)* — `none` · `viewed` · `acknowledged` · `assessment-passed` · `duration-met`

---

## 9. Subject Concept Graph Sets

> **Kaynak boundary:** MOD-0162-FU01C §… `concept-relationship-type` bir MOD-0048 set'i (`RelationshipCode`/`Name`/`Direction`); `atc-code`/`therapeutic-area`/`specialty` **ConceptNode** olarak modellenir, reference set değil.

**27. `concept-relationship-type`** — `related-to` · `depends-on` · `supports` · `addresses` · `requires` · `belongs-to` · `targets` · `maps-to` · `replaces` · `other`
> Boundary `Direction` alanı taşıyor (`outbound` default); bu MOD-0048 value `attributes`'ında `direction` olarak taşınabilir veya runtime alanı olarak kalır (MOD-0162-FU01C runtime kararı).

**28. `concept-status`** — `draft` · `active` · `inactive` · `archived`
**29. `concept-chain-template-status`** — `draft` · `active` · `inactive` · `archived`
**30. `concept-external-ref-type`** — `brand` · `product` · `document` · `audience-profile` · `reference-value` · `external-system` · `other`

---

## 10. Brand/Product Adjacent Sets

> **Kaynak:** MOD-0290-FU01 Brand/Product master ayrı SoR; knowledge/concept pack'leri Brand/Product/Indication/ATC/TherapeuticArea'yı **metadata/future ref** olarak tutuyor.

**31. `product-dosage-form`** *(optional/future)* — `tablet` · `capsule` · `solution` · `suspension` · `injection` · `cream` · `gel` · `spray` · `other`
**32. `product-status`** — `draft` · `active` · `inactive` · `archived`
**33. `brand-status`** — `draft` · `active` · `inactive` · `archived`

**`therapeutic-area` (H4):** MOD-0162-FU01C `therapeutic-area`'yı **ConceptNode** olarak listeliyor (subject concept graph örnekleri). **Karar:** MOD-0048 flat reference set yerine **ConceptNode olarak kalmalı** (ilişkiler/hiyerarşi taşıdığı için). Net değil → **governance follow-up F3** (owner: MOD-0162 vs MOD-0290).

**`atc-code` (H5):** Uluslararası WHO ATC taksonomisi. **Karar:** MOD-0048 içinde local master **açılmamalı**; **external reference / taxonomy** olarak ele alınmalı (`ExternalReferences` seam'i üzerinden). **External taxonomy follow-up F4.** (MOD-0149 external-reference-gap raporuyla tutarlı.)

---

## 11. Required / Optional Classification

| Sınıf | Setler |
|---|---|
| **Runtime validation required** | `consent-channel`, `consent-purpose`, `consent-legal-basis`, `consent-status`, `preference-type`, `campaign-status`, `campaign-target-type`, `campaign-target-source`, `knowledge-content-type`, `knowledge-content-status`, `knowledge-path-step-type`, `concept-relationship-type`, `concept-status` |
| **UI authoring required** | `campaign-type`, `campaign-target-status`, `knowledge-path-status`, `knowledge-journey-status`, `concept-chain-template-status`, `product-status`, `brand-status` |
| **Optional / future** | `consent-source`, `preference-value`, `campaign-objective-type`, `knowledge-content-source`, `knowledge-version-pin-policy`, `knowledge-completion-rule`, `concept-external-ref-type`, `product-dosage-form` |
| **Alignment (runtime in-domain, non-blocking)** | `visit-frequency-type`, `visit-frequency-period-type`, `visit-frequency-source`, `visit-frequency-status`, `visit-frequency-target-type` |
| **External taxonomy (MOD-0048'e açılmaz)** | `atc-code` |
| **Should remain ConceptNode (reference set değil)** | `therapeutic-area` |

**Runtime dependency riski:** Consent setleri MOD-0164-FU02 runtime'ında **fail-closed 400** üretir (contact-availability pattern'i ile aynı) → FU02 canlıya alınmadan **önce publish edilmeli**. Visit-frequency setleri publish edilmese de runtime çalışır (in-domain vocab). Knowledge runtime-required setleri MOD-0162 runtime FU'larından önce publish edilmeli.

**Önerilen publish sırası:**
1. Consent required (1–4, +5/6) → MOD-0164-FU02 unblock
2. Campaign required (9, 11, 12) → sonra 8, 10, 13
3. Knowledge required (19*, 20, 21) → sonra 22–26 *(19, çakışma çözülünce)*
4. Concept required (27, 28) → sonra 29, 30
5. Visit-frequency (14–18) → alignment, herhangi bir zaman
6. Brand/Product (31–33) → MOD-0290 runtime'ıyla eş zamanlı

---

## 12. Naming Convention

**Set code:** lowercase · kebab-case · stable (asla değişmez) · rename yalnız `displayName` ile · **hard delete yok** · archived value yeni authoring'de kullanılmaz · historical reference korunur.
**Value code:** lowercase · kebab-case · semantic · whitespace yok · magic number yok · tenant-specific display name code'a gömülmez · stable.
**Deprecation:** referanslanan value hard-delete edilmez → `isDeprecated=true`; deprecated value yeni create/update'te seçilemez, geçmiş kayıtlarda görünür kalır.
**scopeType:** default **tenant**-scoped.

---

## 13. Multi-language Display Policy

⚠️ **Çakışma (§15-F): task 7 dil = `tr-TR, en-US, ru-RU, az-AZ, ka-GE, kk-KZ, uz-UZ`; platform mevcut 7 dil = `en, fr, es, zh, ar, ru, tr`** (SharedResource.*.resx ve mod-0149 template `["en","fr","es","zh","ar","ru","tr"]`).

- **MOD-0048 value modeli tek `displayName` alanı taşır** — API yüzeyinde 7-dil lokalizasyonu **yoktur** (contact-availability publish raporu §4 ile doğrulandı). Çok dilli gösterim **consumer tarafında RESX/L10n** ile yapılır.
- **Bu plan translation finalizasyonu yapmaz;** key/list hazırlar.
- **Öneri:** `displayName` **en-US** yazılır; her value code için RESX key = `ReferenceValue.<setCode>.<valueCode>` (mevcut L10n bridge PascalCase loader kuralına uyar). Diller **platformun canlı dil setini** takip etmeli — task'ın az/ka/kk/uz listesi henüz platformda yok. **Dil seti farkı EA/platform kararı → follow-up F5.**
- 7-dil RESX key listesi authoring artifact'in `LanguageDisplayNames` kolonunda planlandı (§14).

---

## 14. Authoring Artifact Proposal

**İki artifact önerilir:**

**(1) MOD-0048 authoring JSON taslağı** — oluşturuldu: [`mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json`](mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json). `_meta` bölümünde **OPERATOR AID ONLY — NOT a seed/migration** uyarısı; camelCase; mod-0149 template ile aynı şema (`valueCode/displayName/sortOrder/isDeprecated/attributes.description`). Çakışmalı setler (`consent-legal-basis`, `knowledge-content-type`, `campaign-target-type`) `_conflict` bayrağı ve `_status: "blocked-pending-reconciliation"` ile işaretlendi — **publish öncesi çözülmeli**.

**(2) Spreadsheet-style import template kolon listesi** (her value satırı için):
`SetCode` · `SetName` · `SetDescription` · `ValueCode` · `ValueName` · `ValueDescription` · `SortOrder` · `Status` · `EffectiveFrom` · `EffectiveTo` · `LanguageDisplayNames` · `ModuleOwner` · `RequiredLevel` · `Notes`

> Not: MOD-0048 `/imports` sözleşmesi `description`'ı `attributes` altında taşır; `EffectiveFrom/To` ve `Status` version/governance seviyesinde yönetilir. Import template bu kolonları operatör kolaylığı için düz tutar; commit sırasında PSS-012 şemasına map edilir.

---

## 15. Governance / Conflict Checks

| Kod | Konu | Bulgu | Öneri |
|---|---|---|---|
| **A** | `consent-legal-basis` value çakışması | Boundary `consent`/`public-task`, task `explicit-consent`/`public-interest`/`other` | Boundary canonical; EA onayı (F1) publish öncesi |
| **B** | Mevcut MOD-0048 setleriyle **duplicate** | `contact-availability-*`, `account-*`, `workplace-*`, `address-type`, `status-reason` var; önerilen 33 set kodu ile **çakışma yok** | Yeni set kodları güvenli |
| **C** | Knowledge status setleri ayrı mı? | `knowledge-content-status`/`-path-status`/`-journey-status` aynı değerleri taşıyor | **Ayrı** kalmalı — yaşam döngüleri bağımsız evrilir; tek set paylaşımı gelecekte kırılım riski |
| **D** | `knowledge-content-type` vocab çakışması | Boundary `pdf`/`html-detail`/`sop`/`message-script`/`knowledge-article`; task farklı superset | Boundary code'ları koru, ekleri superset olarak öner; MOD-0162 owner uzlaşısı (F2) publish öncesi |
| **E** | `TargetType` Campaign vs Frequency ortak mı? | `campaign-target-type` (7) ve `visit-frequency-target-type` (8) farklı üye setleri (frequency `campaign-target` içerir, campaign içermemeli) | **Ayrı setler** — ortaklaştırma yanlış; sadece `campaign-target-type`'tan `campaign-target` çıkarılmalı |
| **F** | Consent values vs Campaign values çakışması | Farklı code uzayları (`campaign` hem consent-purpose hem campaign-target-source'ta var ama farklı setlerde, **bilinçli**) | Sorun yok — aynı value farklı setlerde bilinçli tekrar, code stable |
| **G** | `therapeutic-area` reference set mi ConceptNode mu? | MOD-0162-FU01C ConceptNode olarak listeliyor | **ConceptNode** — reference set açılmaz (F3) |
| **H** | `atc-code` local mi external mi? | WHO uluslararası taksonomi | **External taxonomy** — MOD-0048 local master açılmaz (F4) |
| **I** | Dil seti farkı | Task az/ka/kk/uz; platform en/fr/es/zh/ar/ru/tr | Platform canlı dil seti canonical; fark EA kararı (F5) |

**Açılan follow-up'lar:** F1 (legal-basis vocab), F2 (content-type vocab), F3 (therapeutic-area owner), F4 (atc external taxonomy), F5 (dil seti).

---

## 16. Explicit Exclusions

Runtime implementation · backend/frontend/Gateway code change · MOD-0048 publish · seed/grant · permission change · registry write · hardcoded enum migration · existing value destructive rename · delete · Mongo hand-edit · Consent/Campaign/Knowledge runtime · visit planning · route planning · digital detailing · recommendation engine · workflow approval — **hiçbiri yapılmadı**.

---

## 17. Created / Updated Files

| Dosya | İşlem |
|---|---|
| `docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-plan-2026-08-03.md` | **Oluşturuldu** (bu rapor) |
| `docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json` | **Oluşturuldu** (operator aid — seed değil) |

Runtime kod, config, gateway, RBAC, reference data (publish), registry **değiştirilmedi**.

---

## 18. Final Verdict

### **PARTIAL**

**PASS tarafı:**
- CRM Consent/Campaign/Knowledge/Frequency/Concept/Brand-Product reference set **inventory'si çıkarıldı** (33 set + 2 taxonomy kararı).
- Her set için code/name/description + initial values önerildi; tüketen modül belirtildi.
- Required / optional / alignment / external / ConceptNode sınıflandırması yapıldı.
- Runtime dependency riski + publish sırası verildi.
- Naming convention + 7-dil display policy planlandı.
- Governance çakışmaları raporlandı (9 madde); authoring JSON + import template önerildi.
- **MOD-0048 publish yapılmadı · runtime code değişmedi · seed/grant/registry yok · existing scope bozulmadı.**

**PARTIAL nedeni (FAIL değil):**
- İki set (`consent-legal-basis`, `knowledge-content-type`) ve bir set üyesi (`campaign-target-type` → `campaign-target`) **boundary vocab'ı ile çakışıyor** → publish öncesi uzlaşı (F1/F2) gerekiyor.
- `therapeutic-area` (owner belirsiz) ve `atc-code` (external taxonomy) kararları **follow-up'a** bırakıldı (F3/F4).
- Dil seti farkı (F5) EA kararına bağlı.
- Existing MOD-0048 setleri okundu ama canlı `GET /sets` ile **runtime doğrulaması yapılmadı** (bu görev runtime/gateway erişimi kapsamı dışı — statik repo kanıtına dayanıldı).

FAIL kriterlerinin **hiçbiri** tetiklenmedi (publish yok, runtime değişmedi, hardcoded enum gömülmedi, destructive değişiklik yok, registry/seed/grant yok, runtime scope açılmadı).

---

## 19. Next Recommended Prompt

Çakışmalar (F1/F2) EA/owner tarafından uzlaştırıldıktan sonra, plan PASS olarak kabul edilirse:

```
MOD-0164-FU02 — Consent & Preference Runtime / Evaluation Provider Implementation
```

> Ön koşul: publish öncesi F1 (`consent-legal-basis` vocab) çözülmeli; consent required setleri (1–4) publish edilmeli — aksi halde FU02 fail-closed 400 üretir.
