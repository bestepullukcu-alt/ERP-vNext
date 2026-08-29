# MOD-0150 — Contact Location & PII/KVKK Hardening

**Date:** 2026-07-21 · **Module:** Contact & Relationship Management (`Diten.CrmService` + `Diten.Web`) ·
**Type:** Post-closeout enhancement (no new module / permission / reference set / seed) · **Verdict:** **PASS**

Bu gate MOD-0150 Contact create/edit, Contact Details, `AccountContactLink` ve `AccountRelationship` yüzeylerinde
**location eksikliği**, **cross-country ilişki kontrolü** ve **PII/KVKK sertleştirmesi** risklerini kapatır. Yeni CRM
capability modülü değildir; MOD-0150 closeout PASS %100 durumu bozulmaz.

---

## 1. Preflight

- **MOD-0150 status:** Closeout PASS (Review-ready) %100 — FU01–FU06 + FU05 doğrulanmış.
- **Existing Contact model/UI:** `Contact` aggregate (First/Last/DisplayName, ContactType, Status, title/specialty/
  department/phone/email/notes + external refs) — **location alanı yoktu**. Golden Reference Compact vertical
  (list/create/edit/details) mevcut.
- **Existing relationship lifecycle:** `AccountContactLink` (M:N) + `AccountRelationship` (directional) + historical
  lifecycle (End = Status=ended+ValidTo, never delete) PASS. Account 360 Add/Edit/End management UI mevcut.
- **Scope confirmation:** Location + cross-country + PII/KVKK hardening only. **Yok:** Patient module, consent
  engine/capture, MOD-0164 gerçek consent, Territory/Zone/MicroZone/SalesRep, Visit/Route, yeni permission, yeni
  reference set, CRM local seed, hardcoded location list, direct Mongo mutation, hard delete.

## 2. Location Model Decision

`Account` (MOD-0149) zaten `CountryRef/CityRef/DistrictRef/AddressLine` taşıyor; Contact minimal ve uyumlu şekilde
aynı MOD-0048 location setlerini (`country`/`city`/`district`) tüketir — **yeni set yok, CRM local seed yok**.

| Field | Required? | Source / Validation | Reason |
|---|---|---|---|
| `CountryRef` | Hayır | MOD-0048 `country` (opsiyonel: InvalidValue→400, SetMissing tolere) | Cross-country kontrol + MOD-0151/0155 hazırlığı |
| `CityRef` | Hayır | MOD-0048 `city` (opsiyonel) | Konum detayı |
| `DistrictRef` | Hayır | MOD-0048 `district` (opsiyonel) | Konum detayı |
| `AddressLine` | Hayır | Free-text, maxlen 256 | Adres |
| `PostalCode` | Hayır | Free-text, maxlen 16 | Adres |
| `PreferredLanguage` | Hayır | BCP-47-ish regex (`^[A-Za-z]{2,3}(-…)?$`) | İletişim/UI ipucu |
| `PhoneCountryCode` | Hayır | Dialing-code regex (`^\+?\d{1,5}$`) | Phone normalizasyonu |

**Kararlar:** Country zorunlu değildir; country yoksa Details/Edit'te **"Location incomplete"** rozeti gösterilir
(non-blocking). Location alanları Account master veya Territory yerine geçmez; **Zone/MicroZone/Territory/SalesRep
eklenmemiştir**. `ix_contacts_tenant_country` sparse index eklendi (cross-country + gelecek territory sorguları).

## 3. Cross-Country Relationship Decision

**Seçilen model: Seçenek 1 — Soft-warning + reason-required.** Gerekçe: hard-block override ayrı bir permission
gerektirir; bu taskta yeni permission icat edilmez. Reason zorunluluğu yalnızca **iki tarafın da bilinen ve farklı**
country'si olduğunda tetiklenir; country bilinmiyorsa ilişki **bloklanmaz**. `CrossCountryPolicy` ortak helper'ı bu
kararı verir; audit'e yalnızca country kodları yazılır, **reason metni asla loglanmaz** (PII taşıyabilir).

| Relationship | Rule | UI Behavior | Backend Behavior | Audit Behavior |
|---|---|---|---|---|
| Contact↔Account (same country) | Allowed | Reason alanı opsiyonel/yok sayılır | 201 | `sameCountry` |
| Contact↔Account (cross, reason yok) | Controlled | Validation/400 mesajı | **400** controlled | — (kayıt oluşmaz) |
| Contact↔Account (cross, reason var) | Allowed | Reason girilir | 201, reason entity'de saklanır | `crossCountry=TR->US` (kod, PII yok) |
| Contact↔Account (country bilinmiyor) | Allowed | Uyarı yok | 201 | `sameCountry` (inconclusive) |
| Account↔Account (same country) | Allowed | — | 201 | `sameCountry` |
| Account↔Account (cross, reason yok) | Controlled | 400 mesajı | **400** controlled | — |
| Account↔Account (cross, reason var) | Allowed | Reason girilir | 201, reason saklanır | `crossCountry=TR->US` |

**nearby / location-sensitive tipler:** İlk sürüm tüm cross-country tipler için reason-required (conservative). Tip
bazlı sert blok (ör. `nearby` cross-country hard-block) için `account-relationship-type` metadata'sına bir
`crossCountryPolicy` attribute eklenmesi **follow-up** olarak kaydedildi — bu taskta yeni metadata set üretilmez.

## 4. PII/KVKK Classification

| Field | Classification | UI | Audit/Log | Export/Import |
|---|---|---|---|---|
| FirstName / LastName / DisplayName | **PII** | Authorized görür | **Raw yazılmaz** (entityId+corrId) | Export'ta var (authorized); import hata mesajı raw isim döndürmez |
| Phone / PhoneCountryCode | **PII** | Authorized görür | **Raw yazılmaz** (7+ digit maskeli) | Export'ta var; import "invalid" mesajı raw döndürmez |
| Email | **PII** | Authorized görür | **Raw yazılmaz** (`***@***`) | Export'ta var; import mesajı raw döndürmez |
| AddressLine / PostalCode | **PII** | Authorized görür | Raw yazılmaz | Export kapsamı (authorized) |
| Notes (free-text) | **Potansiyel PII/özel nitelikli** | Safety notice + maxlen 2000 + trim | Raw yazılmaz | Export risk notu |
| External reference id | **Potansiyel PII** | Authorized görür | Conflict'te `source=<sys>` (raw id yok) | — |
| CountryRef/CityRef/DistrictRef | Düşük risk (kod) | Görünür | Audit'e yazılabilir (kod) | — |
| ContactId/TenantId/ContactType/Status/RoleCode/LinkId/RelationshipId | Non-PII | Görünür | Audit anahtarı | — |

**Kurallar uygulandı:** Contact create/update/delete + link/relationship create audit çağrıları artık **raw isim/
telefon/e-posta taşımıyor** (entityId + korelasyon + non-PII descriptor). `PiiMasking.Redact` defence-in-depth olarak
audit publisher'da e-posta ve 7+ hane telefon şekillerini maskeler (GUID/country kodu korunur).

## 5. Patient Scope Guard

| Topic | Decision | Reason | Follow-up |
|---|---|---|---|
| Patient = sıradan Contact? | **Hayır** | Hasta-doktor klinik bağlamı özel nitelikli veri; PII/KVKK + sağlık gizliliği gerektirir | Ayrı **Patient/Healthcare Privacy Scope** değerlendirmesi |
| Notes'a sağlık verisi | **Engellenir (uyarı+guard)** | Free-text sızıntı riski | UI safety notice (7 dil) + maxlen + audit masking eklendi |
| `contact-type` içinde `patient` var mı? | **Reference set operatör-yönetimli** | MOD-0048 published değerleri kod tarafında listelenmez; bu taskta contact-type değerleri değiştirilmez | Operatör `contact-type`'ta `patient` benzeri değer yayınladıysa **governance-disabled** önerisi (kodda zorlama yok) |
| CRM genel closeout | **Patient scope değerlendirilmeden "tamamlandı" sayılmaz** | Healthcare privacy açık risk | Açık follow-up olarak kaydedildi |

## 6. Implementation Summary

- **Contact model/API:** `Contact` + Create/Update command + validator + mapper + `ContactDetailDto`'ya 7 opsiyonel
  location alanı; opsiyonel `country/city/district` MOD-0048 doğrulaması (SetMissing tolere, InvalidValue→400); phone
  country code + preferred language shape validation; email format zaten mevcut. Sparse `ix_contacts_tenant_country`.
- **Professional alanları reference-driven (kullanıcı isteği 2026-07-21):** `ProfessionalTitle/Specialty/Department`
  free-text input → **select2 dropdown** (MOD-0048 `professional-title`/`medical-specialty`/`department-type`, pack §10
  opsiyonel setleri); Account'un fallback-option pattern'i (kayıtlı değer listede yoksa korunur); backend opsiyonel
  doğrulama (SetMissing tolere → 201, InvalidValue → 400). Set yayınlanmamışsa dropdown boş kalır — **CRM local fallback
  yok**. **Contact-Type'a göre cascade** veri-güdümlü metadata gerektirir → §15 follow-up (hardcoded mapping eklenmedi).
- **Contact UI:** `_Form.cshtml` Location section (Country/City/District select + Address/PostalCode/PhoneCountryCode/
  PreferredLanguage) + **PII/KVKK notice** + **Notes safety notice**; `Details.cshtml` Location kartı + **Location
  incomplete** rozeti (section-map parity korunur). **Location picker MOD-0149 Account paritesi (kullanıcı isteği
  2026-07-21):** Contact Create/Edit'e inline **Leaflet + OSM harita** + arama + "Use my location" eklendi
  (`Contacts/map.js`, Account `map.js` mirror'ı); harita reverse/forward geocode ile AddressLine'ı doldurur ve
  Country/City/District select2'lerini **aynı kebab-normalize eşleştirme mantığıyla** otomatik seçer; select2'ler
  Account'un fallback-option pattern'ini kullanır (kayıtlı değer mevcut listede yoksa korunur). **Latitude/Longitude
  saklanmaz** (Contact bir kişidir — koordinat gerekmedi); harita yalnızca alan doldurma yardımcısıdır.
- **Link/relationship cross-country:** `LinkContactToAccountCommand` / `UpdateAccountContactLinkCommand` /
  `CreateAccountRelationshipCommand` / `UpdateAccountRelationshipCommand` + API request DTO'ları + entity'lere
  `CrossCountryReason`; `CrossCountryPolicy` reason-required kontrolü; ContactLinkForm/RelationshipForm reason alanı +
  Notes safety notice.
- **Notes safety:** UI uyarısı (7 dil) + backend maxlen/trim; raw Notes audit/log'a yazılmaz.
- **Audit/log masking:** `PiiMasking.Redact` + call-site'larda raw isim/telefon/e-posta kaldırıldı (create/update/
  delete/link/relationship).
- **Import/export:** Zaten PII-safe (audit counts-only, hata satırları row#+field name; raw isim/telefon/e-posta
  döndürmez) — doğrulandı; ek masking gerekmedi.
- **Tests:** `ContactLocationPiiHardeningTests` (21 yeni) — PiiMasking, CrossCountryPolicy, location persist, invalid
  country 400, PII-safe audit, cross-country 400/201, same-country/missing-country allowed, validator shape.

## 7. Changed Files

| File | Change | Why |
|---|---|---|
| `Domain/Entities/Contact.cs` | +7 location alanı | Location model |
| `Domain/Entities/AccountContactLink.cs` / `AccountRelationship.cs` | +`CrossCountryReason` | Cross-country reason kaydı |
| `Application/Common/PiiMasking.cs` | **yeni** | Audit/log PII redaction (defence-in-depth) |
| `Application/Common/CrossCountryPolicy.cs` | **yeni** | Cross-country reason-required kararı + non-PII audit note |
| `Application/Features/Contact/Commands/{Create,Update}ContactCommand.cs` | +location alanları | Command taşıma |
| `Application/Features/Contact/Validators/ContactValidators.cs` | +location/phone-cc/lang shape | Validation |
| `Application/Features/Contact/ContactReferenceValidation.cs` | +opsiyonel country/city/district | MOD-0048 validate |
| `Application/Features/Contact/ContactMapper.cs` + `ContactModels.cs` | DTO'ya location + `IsLocationIncomplete` | Detail çıktısı |
| `Application/Features/Contact/Handlers/CommandHandlers/{Create,Update,Delete}ContactHandler.cs` | location map + **PII-safe audit** | Persist + PII fix |
| `Application/Features/AccountContact/Commands/AccountContactCommands.cs` + Handlers | +reason + cross-country kontrol | Controlled link |
| `Application/Features/AccountRelationship/Commands/AccountRelationshipCommands.cs` + Handlers | +reason + cross-country kontrol | Controlled relationship |
| `Api/Controllers/CRM/{AccountContact,AccountRelationship}Controller.cs` + `Models/CRM/*Requests.cs` | +reason mapping | API taşıma |
| `Infrastructure/Audit/LoggingContactAuditPublisher.cs` | `PiiMasking.Redact(detail)` | Audit masking |
| `Persistence/DependencyInjection.cs` | `ix_contacts_tenant_country` sparse | MOD-0151/0155 hazırlığı |
| `frontend/.../Models/CRM/ContactViewModels.cs` + `AccountRelationshipManagementViewModels.cs` | +location + reason | VM/payload |
| `frontend/.../Controllers/CRM/{Contacts,Accounts}Controller.cs` | location options + reason mapping | Proxy |
| `frontend/.../Views/CRM/Contacts/{_Form,Details}.cshtml` | Location + PII/KVKK + Notes notice | UI |
| `frontend/.../Views/CRM/Contacts/{Create,Edit}.cshtml` | Leaflet CSS/JS + `map.js` scripts | Account-parite harita |
| `frontend/.../wwwroot/assets/js/CRM/Contacts/map.js` | **yeni** (Account `map.js` mirror, no lat/lng) | Location picker |
| `frontend/.../Views/CRM/Accounts/{ContactLinkForm,RelationshipForm}.cshtml` | reason + Notes notice | UI |
| `Resources/Views/CRM/Contacts/ContactIndex.*.resx` (7) | +13 key (65/65) | L10n parity |
| `Resources/Views/CRM/Accounts/AccountIndex.*.resx` (7) | +3 key (98/98) | L10n parity |
| `tests/.../ContactLocationPiiHardeningTests.cs` | **yeni** (21 test) | Coverage |

## 8. UI Proof

> Not: Aşağıdaki UI/failure/permission proof'ları **kod + build + compact verifier + unit test** ile kanıtlanmıştır.
> Canlı authenticated browser golden flow, çalışan fleet + runtime 97c5 token gerektirir (MOD-0150 closeout'un da
> ertelediği gibi) — **Open Items** altında Low olarak açık bırakıldı, sahte PASS yok.

| Surface | Expected | Observed | Status |
|---|---|---|---|
| Contact Create/Edit Location section | Country/City/District + Address/Postal/PhoneCC/Lang | `_Form.cshtml` Location `<section>` | ✅ (kod + verifier) |
| PII/KVKK notice | Create/Edit formda görünür | `_Form.cshtml` alert-info | ✅ |
| Notes safety notice | Notes üstünde uyarı | `_Form.cshtml` + link/rel formlar alert-warning | ✅ |
| Contact Details Location + incomplete | Konum + "Location incomplete" rozeti | `Details.cshtml` Location kartı + badge | ✅ |
| Compact section parity | _Form ↔ Details aynı sıra | verifier PASS | ✅ |
| Link/Relationship reason alanı | Cross-country reason textarea | ContactLinkForm/RelationshipForm | ✅ |
| 7-dil RESX | Ham key yok, parity | ContactIndex 65×7, AccountIndex 98×7 | ✅ |

## 9. Failure Path Proof

| Failure | Expected | Observed | Status |
|---|---|---|---|
| Invalid country code | 400 | `Create_Invalid_Country_Code_Returns_400` | ✅ |
| Invalid phone country code / language | validation error | `Validator_Rejects_Bad_PhoneCountryCode_And_Language` | ✅ |
| Cross-country link reason yok | controlled 400 | `Link_CrossCountry_Without_Reason_Returns_400` | ✅ |
| Cross-country relationship reason yok | controlled 400 | `Relationship_CrossCountry_Without_Reason_Returns_400` | ✅ |
| Country bilinmiyor | bloklanmaz | `Link_MissingCountry_Does_Not_Block` | ✅ |
| Same-country | allowed | `Link_SameCountry_Allowed`, `Relationship_SameCountry_Allowed_Without_Reason` | ✅ |
| DisplayName auto-derive korunur | "Ahmet Yilmaz" | `Create_Persists_Location_And_AutoDerives_DisplayName` | ✅ |

## 10. PII Leak Guard Proof

| Guard | Expected | Observed | Status |
|---|---|---|---|
| Contact create/update/delete audit raw PII | yok | call-site `detail: null` + guard grep CLEAN | ✅ |
| Link/relationship audit raw reason/PII | yok, sadece country kodu | `Link_CrossCountry_With_Reason...` reason denetlenmez | ✅ |
| PiiMasking e-posta/telefon | maskeler, GUID/country korur | `PiiMasking_Redacts_Email_And_Phone_But_Keeps_Guid_And_Country` | ✅ |
| Import/export raw PII | yok (counts-only, row#+field) | FU06 kodu + inceleme | ✅ |
| Grep: `PublishAsync(...DisplayName/Phone/Email...)` | 0 hit | CLEAN | ✅ |

## 11. Permission / Export Proof

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| Export permission ayrı | `crm.contact.export` (mevcut) | ContactController `[HasPermission("crm.contact.export")]` | ✅ |
| Yeni permission icat | yok | grep: yalnızca mevcut `crm.*` anahtarlar | ✅ |
| Cross-country override permission | icat edilmedi (soft-warning tercih) | Follow-up olarak kaydedildi | ✅ |

## 12. Validation Commands

| Command | Result | Notes |
|---|---|---|
| build CrmService.Api | ✅ 0 hata | — |
| test CrmService.Application.Tests | ✅ **98/98** | 77 → 98 (+21 hardening) |
| build Diten.Web | ✅ 0 hata | — |
| build Diten.ApiGateway | ✅ 0 hata | route değişmedi |
| RESX parity (7 lang) | ✅ ContactIndex 65 / AccountIndex 98 | eşit |
| DataTable compact verifier (Contacts) | ✅ **94 PASS / 0 FAIL** | section parity düzeltildi |
| Search guards (PII/hardcoded/5061/patient/consent/territory/seed) | ✅ CLEAN | §10 + boundary |

## 13. Boundary / SoR Check

| Object/Capability | Owner | Touched? | Boundary Risk |
|---|---|---|---|
| Contact master (location alanları) | MOD-0150 | ✅ genişletildi | none |
| Account master | MOD-0149 | Hayır (Contact fields eklenmedi) | none |
| Country/City/District reference | MOD-0048 | consume-only (yeni set yok) | none |
| Consent engine | MOD-0164 | Hayır | none |
| Territory/Zone/MicroZone/SalesRep | MOD-0151 | Hayır (yok) | none |
| Visit/Route | later | Hayır | none |
| Permission engine | MOD-0018 | consume-only (yeni key yok) | none |

## 14. Out-of-Scope Guard

| Forbidden Item | Found? | Status |
|---|---|---|
| Patient module impl | Hayır | ✅ |
| Consent engine/capture | Hayır | ✅ |
| Territory/SalesRep/Visit/Route | Hayır | ✅ |
| Yeni permission | Hayır | ✅ |
| Yeni reference set / CRM local seed | Hayır | ✅ |
| Hardcoded location list | Hayır | ✅ |
| Direct Mongo mutation / hard delete | Hayır (ReplaceOne + soft delete) | ✅ |
| Direct 5061 frontend call | Hayır (yalnızca yorum) | ✅ |
| Notes'a health/patient data teşvik metni | Hayır (tersine yasaklayan uyarı) | ✅ |

## 15. Open Items

| Item | Severity | Owner | Blocks MOD-0151? | Notes |
|---|---|---|---|---|
| **Patient/Healthcare Privacy Scope** değerlendirmesi | High (governance) | EA/Security | No | CRM genel closeout bu yapılmadan tamamlanmaz |
| Authenticated browser golden flow (Location/cross-country) | Low | operator | No | Runtime 97c5 token + fleet gerekir; kod-proof mevcut |
| `account-relationship-type` `crossCountryPolicy` metadata (nearby hard-block) | Medium | MOD-0048/EA | No | Follow-up; bu taskta metadata üretilmez |
| Cross-country override permission (hard-block modeli için) | Medium | MOD-0018/EA | No | Yeni permission önerisi (bu taskta icat edilmedi) |
| Link/relationship Edit'te reason display (detail DTO'ya alan) | Low | UI | No | Reason saklanır; edit'te reload minör follow-up |
| **Professional alanları Contact-Type'a göre cascade** | Medium | MOD-0048/EA + UI | No | Değerlere `contactTypes`/`appliesTo` metadata authoring (relationship-type metadata pattern'i gibi) + frontend filtre gerekir; hardcoded mapping **eklenmedi** |
| Account (MOD-0149) `ResponsiblePerson*` audit PII taraması | Low | MOD-0149 | No | Aynı masking pattern MOD-0149 audit'e de uygulanabilir |

## 16. Registry / Status Update

- **Previous:** MOD-0150 — Closeout PASS (Review-ready), %100.
- **New:** MOD-0150 — Closeout PASS (Review-ready), %100 **+ Contact Location & PII/KVKK Hardening enhancement PASS**.
- **Reason:** Closeout kapsamı bozulmadan location model, cross-country controlled relationship ve PII/KVKK
  sertleştirmesi eklendi; builds/tests/verifier/parity yeşil; boundary temiz. Patient/Healthcare Privacy Scope açık
  follow-up olarak kaydedildi.

## 17. Final Verdict

**PASS** — Contact location and PII/KVKK hardening implemented without boundary drift. Build/test/verifier/RESX yeşil,
PII sızıntısı kapatıldı (audit call-site + masking), cross-country ilişkiler controlled (reason-required, non-PII
audit), Notes safety + PII/KVKK notice 7 dilde, hardcoded fallback / direct 5061 / patient-as-contact / new permission
/ new reference set / local seed yok. Tek gerçek açık: canlı authenticated browser smoke (Low, kod-proof mevcut) ve
governance düzeyinde Patient/Healthcare Privacy Scope (High, ayrı değerlendirme).

### Zorunlu karar cümleleri

1. **Contact identity and communication fields are PII and must not be written raw into audit/log/error telemetry.**
2. **Cross-country Contact↔Account and Account↔Account relationships are controlled relationships, not silent defaults.**
3. **Patient-related data must not be treated as ordinary CRM Contact data without a dedicated healthcare privacy scope.**
4. **Contact location hardening prepares MOD-0151 Territory and MOD-0155 Visit Planning, but does not implement those modules.**

## 18. Next Recommended Prompt

**MOD-0151 Territory Management Pack Prep** — Contact/Account location seam artık cross-country + territory sorgularına
hazır (`ix_contacts_tenant_country`, `CountryRef`). Alternatif önce-kapatılabilir: **Patient/Healthcare Privacy Scope
değerlendirmesi** (governance) — CRM genel closeout için önerilir.
