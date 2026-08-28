# MOD-0150-FU — Contact Availability and Visit Preference — Pack Authorization

- **Tarih:** 2026-08-01
- **Modül:** MOD-0150 — Contact & Relationship Management (`Diten.CrmService`)
- **Task tipi:** Module pack authorization / governance hizalama (**kod değil, runtime değil, route planning değil**)
- **Target file:** `execution/domains/commercial-suite/module-packs/MOD-0150-contact-relationship-management.md`
- **Owner:** module-pack-author
- **Verdict:** **PASS**

---

## 1. Preflight

- Bu task, MOD-0150 module pack içinde **Contact Availability and Visit Preference** kapsamını yetkilendirme ve
  governance hizalama task'ıdır. **Kod yazma / runtime implementation / route planning implementation task'ı
  değildir**; hiçbir servis, gateway, frontend, seed, reference-data veya test dosyası değiştirilmemiştir.
- Amaç: **MOD-0155 Visit / Route Planning başlamadan önce**, contact'ın hangi account/lokasyonda hangi günler ve
  hangi saatlerde ziyaret edilebilir olduğunu **doğru master data** olarak tanımlamak.
- Bu task **route planning · visit planning · frequency/call-cycle engine · campaign engine · GPS/check-in-out
  değildir**. Workflow / approval / ChangeRequest işleri en sona bırakılmıştır.
- Otorite sırası korundu: Module Pack > [Domain Config](../../execution/domains/commercial-suite/domain-config.md) >
  `AGENTS.md` > `.antigravity/rules/`.
- Referans desenler: **MOD-0150 D1** (Contact↔Account **M:N** `AccountContactLink`) — availability'nin doğru anahtarı;
  **MOD-0149/MOD-0150 reference-data governance** (pack önerir, operator publish eder, eksik set → fail-closed 400);
  **MOD-0151 FU04A-RBAC / FU05-RBAC / FU08-RBAC** — "katalog hazır değilse seed etme, fallback + follow-up" deseni.

## 2. MOD-0151 FU09A Dependency Confirmation

| Ön koşul | Durum | Kanıt |
|---|---|---|
| MOD-0150 Closeout PASS / %100 (FU01–FU06) | **PASS** | `module-implementation-status.md` MOD-0150 satırı; `mod-0150-final-validation-closeout.md` |
| Contact master mevcut | **PASS** | pack §9.1 · CrmService `Features/Contact` |
| `AccountContactLink` mevcut (M:N) | **PASS** | pack §9.2 · D1 approved · FU03 |
| Contact ↔ Account M:N ilişki modeli | **PASS** | pack §16 D1 |
| MOD-0151 FU09A Pack Authorization | **PASS** | `mod-0151-fu09a-visit-route-readiness-boundaries-pack-authorization-2026-08-01.md` |
| FU09A availability master sahipliğini MOD-0150'ye bıraktı | **Doğrulandı** | MOD-0151 pack §22.6 "Contact availability boundary" tablosu + §21 MOD-0150 satırı + F21 follow-up'ı |
| MOD-0155 Visit Planning | **Başlamadı** | `module-id-registry.md` MOD-0155 = *reserved / planned* |
| Workflow / approval / ChangeRequest | **bilinçli olarak ertelendi** | MOD-0151 §22.1 FU06 boundary |

**FU09A'dan devralınan kayıt (birebir korunmuştur):** *"availability **link bazlıdır** (contact bazlı değil);
`Avoid*` preferred'ın tersi değil, ayrı ve daha güçlü bir kısıttır; veri yoksa `AvailabilityStatus=unknown` döner ve
candidate sessizce düşmez."* Bu FU o sözleşmeyi **daraltmadan** master tarafına taşır.

**Governance drift bulgusu (kayda geçti, düzeltilmedi):** MOD-0150 frontmatter'ındaki `runtime_code_scope` hâlâ
orijinal **FU01-only** parantezini ("NO AccountContactLink/AccountRelationship/frontend/import-export until later
FUs") taşıyor; oysa FU01–FU06 **Closeout PASS (2026-07-20)**. Header banner da hâlâ
`APPROVED-PENDING-PREREQ` diyor. Bu authorization **mevcut metni yeniden yazmadı** — yeni scope'u **append** etti ve
drift'i §19 follow-up'ı olarak açtı. Geçmişi sessizce düzeltmek bu task'ın yetkisi değildir.

## 3. Business Need Summary

Route planning'in cevaplayamazsa çalışamayacağı soru: *"Bu doktoru **bu lokasyonda**, hangi gün, hangi saat
aralığında ziyaret edebilirim?"* Gerçek saha verisi:

```
Dr. Ayşe

Medicana Beylikdüzü:      Klinik X:
Pazartesi 09:00–13:00     Salı      10:00–16:00
Çarşamba  14:00–17:00     Perşembe  09:00–12:00
```

Bu veri bugün **hiçbir modülde sahiplenilmemiştir**. Sahiplenilmezse iki sonuç doğar: (a) MOD-0155 başladığında
availability'yi kendi içinde uydurur ve contact master'dan kopar, (b) ya da acil çözüm olarak Contact üzerine düz bir
"çalışma saati" alanı eklenir — bu, çok-lokasyonlu doktoru **tek yanlış takvime** indirger ve geri dönüşü pahalıdır.
Bu FU, veriyi doğru anahtarla (`AccountContactLink`) ve doğru modülde (contact ilişkisinin master'ı MOD-0150)
sahiplendirir.

Ayrıca gerçek hayat haftalık düzenle bitmez: izin, kongre, ameliyat günü, geçici lokasyon değişikliği. Bu yüzden
**date-specific exception** aynı yetkilendirmede yer alır — haftalık desen tek başına saha gerçeğini temsil etmez.

## 4. AccountContactLink Ownership Decision

**Karar (D8): availability `AccountContactLink` bazlıdır; `Contact` üzerinde düz alan olarak tutulamaz.**

Gerekçe: MOD-0150 D1 zaten Contact↔Account'u **M:N** modellemiştir. Aynı contact iki hastanede farklı gün/saat
taşıdığına göre availability'nin doğal sahibi **ilişkinin kendisidir**, taraflardan biri değil. `ContactId` /
`AccountId` alanları kayıtta **link'ten türetilen navigasyon kopyalarıdır** — bağımsız olarak set edilemezler, aksi
hâlde link'in sahip olmadığı bir contact/account çifti iddia edilebilirdi.

| Katman | Sorumluluk |
|---|---|
| **MOD-0150** | `ContactAvailability` / `VisitPreference` / `ContactAvailabilityException` **master** |
| **MOD-0151 (FU09A)** | **Read-only** tüketici (route readiness); veri kopyalanmaz, aggregate açılmaz |
| **MOD-0155** | Tüketici; visit plan / route plan **üretir** |
| **MOD-0165 / MOD-0167** | Frequency / call-cycle policy **üreticisi** — bu FU'nun kapsamı **değil** |

## 5. Authorized Scope

Frontmatter'a **additive** eklenen scope: **`FU-contact-availability-visit-preference`**.

1. **`ContactAvailability`** (pack §9.4) — link bazlı haftalık pencereler, effective-dated, status'lu; 21 alan
   (`AvailabilityId` … `UpdatedBy`), `AvailabilityType` ve `Status` MOD-0048 doğrulamalı.
2. **`VisitPreference`** (§9.5) — preferred/avoid window, `AppointmentRequired` + `AppointmentLeadTimeDays`,
   `PreferredVisitDurationMinutes`, `PreferredContactMethod`; link bazlı, contact-level default **opsiyonel fallback**.
3. **`ContactAvailabilityException`** (§9.6) — tarihe özel override (izin, kongre, ameliyat, geçici lokasyon).
4. **Read API'leri** (§20.4) — contact · link · account · `date` lookup (haftalık desen **+ exception uygulanmış**).
5. **Write API'leri** (§20.5) — create / update / deactivate / archive (availability + exception). **Hard delete yok.**
6. **Minimal Compact UI** (§13 madde 8–10) — Contact Details Availability tab, AccountContactLink Availability panel,
   Account 360 read-only panel; 7 dil RESX paritesi.
7. Reference validation (3 yeni MOD-0048 seti), contract flag'leri, backend/frontend testleri, Gateway-only
   authenticated smoke ve implementation evidence report.

**Temel mimari kural (pack'e yazıldı):** *availability "bu kişi **burada** ne zaman ziyaret edilebilir" sorusunu
cevaplar; "bugün kim, hangi sırayla ziyaret edilmeli" sorusunu **asla** cevaplamaz.*

## 6. Explicit Exclusions

Route optimization algorithm · günlük rota oluşturma · visit plan oluşturma · visit execution · check-in/check-out ·
GPS validation · visit report · digital detailing · survey · campaign engine · frequency / call-cycle engine ·
territory assignment · `ContactTerritoryAssignment` · Account master mutation · territory model mutation · workflow
approval · ChangeRequest · MOD-0023 entegrasyonu · evidence pack · yeni import/export scope · Brand/Product master ·
patient (hasta) verisi · hard delete · Mongo hand-edit · RBAC seed/grant (ayrıca yetkilendirilmedikçe) ·
MOD-0048 publish (ayrıca yetkilendirilmedikçe) · request payload'ında `TenantId` · direct port 5061 business API
çağrısı.

## 7. Data Model Policy

| Karar | Sonuç |
|---|---|
| Sahiplik anahtarı | **`AccountContactLinkId`** (D8); `ContactId`/`AccountId` türetilmiş kopya |
| Contact'ta düz alan? | **Asla** (D8) — çok-lokasyonlu doktoru tek yanlış takvime indirger |
| Bir contact, çok account | **Her link için bağımsız takvim** (D9); okumalar link-izole |
| Preference yeri | Link bazlı (availability içinde); contact-level default **opsiyonel fallback** (D10) |
| Preferred window zorunlu mu? | **Hayır** (D11) — yoksa available window kullanılır; tercih **uydurulmaz** |
| Avoid window anlamı | Available window içindeki **daha güçlü kısıt**; preferred'ın tersi **değil** (D13) |
| Haftalık desen vs tarih | **Exception kazanır** (D12) |
| Silme | **Hard delete yasak** — `inactive` / `archived` |
| Zaman semantiği | Account lokasyonunun **local wall-clock** saati; MOD-0150 timezone master'ı sahiplenmez |
| Değerler | MOD-0048 (`contact-availability-type`, `contact-availability-status`, `availability-exception-reason`); **hardcoded fallback yok** |

`AvailabilityType` değerleri: `working-hours` · `visiting-hours` · `preferred-window` · `restricted-window` ·
`appointment-only` · `temporary-exception`. `Status`: `active` · `inactive` · `archived`.

## 8. Validation Policy

| Kural | Davranış |
|---|---|
| `StartTime` < `EndTime` | İhlal → **400** |
| Preferred window ⊆ available window | İhlal → **400** |
| Avoid window | Available window ile **çakışabilir** (amacı budur); daha güçlü kısıt olarak yorumlanır |
| `EffectiveTo < EffectiveFrom` | **400** |
| Link durumu | Inactive/ended `AccountContactLink` üzerine `active` availability → **400** |
| Tenant izolasyonu | Cross-tenant Contact/Account/Link → **404**; tenant **claim'den**, payload'dan değil |
| Overlap conflict | Aynı `(AccountContactLinkId, Weekday)` için örtüşen **active** pencere → kontrollü **409**, iki kaydın kimliği raporlanır; **sessiz merge / sessiz overwrite yasak** |
| Idempotency | Birebir aynı satır **no-op**'tur, duplicate üretmez |
| Exception tekilliği | `(link, Date)` için tek active exception; ikincisi → **409** (mevcut kayıt güncellenir) |
| Reference set yayınlanmamış | **Fail-closed** kontrollü 400 (MOD-0149/0150 paritesi) |
| Delete denemesi | Delete endpoint'i **yoktur**; delete şeklindeki istek → kontrollü `unsupported_operation` |

## 9. UI Policy

**Yer:** (1) Contact Details → **Availability tab** (linkli account'a göre gruplu + exception'lar), (2)
AccountContactLink detail/relationship section → **Availability panel** (asıl editör burasıdır), (3) Account 360 →
**read-only** contact availability paneli (MOD-0149 render eder, MOD-0150 sahiplenir — Related Contacts ile aynı
kural).

**Gösterilecek:** account/lokasyon · weekday · start–end · preferred window · avoid window · appointment required
(+ lead time) · average duration · effective dates · status · source · notes · date-specific exception'lar.

**Kesinlikle olmayacak:** rota oluştur · visit plan oluştur · GPS/check-in/check-out · campaign veya frequency
konfigürasyonu · territory assignment düzenleme · workflow approval aksiyonu · **herhangi bir hard-delete butonu**.

Golden Reference **Compact**, DataTable v2, 7 dil RESX, Gateway-only (tarayıcı 5061'e gitmez).

## 10. MOD-0151 Integration Boundary

```
MOD-0150 ContactAvailability master'dır.
MOD-0151 route readiness içinde bu veriyi yalnız okur.
MOD-0155 visit planning / route planning içinde bu veriyi kullanır.
```

- MOD-0151'e veri **kopyalanmaz**; MOD-0151 içinde `ContactAvailability` master aggregate'i **açılmaz**
  (MOD-0151 §22.6 boundary'si ile birebir tutarlı).
- Availability yoksa MOD-0151 `AvailabilityStatus=unknown` döner — **`contact_not_available_on_day` değil** (D15;
  MOD-0151 R11 "veri yok ≠ uygun değil").
- `AppointmentRequired` candidate'ı düşürmez; **reason/warning** üretir (D14).
- Contact territory coverage **türetilmiş** kalır (`Contact → AccountContactLink → Account → current coverage`);
  bu FU hiçbir territory alanı ve `ContactTerritoryAssignment` eklemez.

**Bu task MOD-0151 pack'ini değiştirmemiştir** — FU09A §22.6 ve F21 zaten bu sahipliği doğru tarif ediyor.

## 11. MOD-0155 Integration Boundary

MOD-0155 bu veriyi şunlar için tüketecek: visit candidate uygunluk kontrolü · available time window · preferred visit
window · avoid window · appointment required (+ lead time) · average visit duration · date-specific exception.

**MOD-0150 planın hiçbir parçasını üretmez:** sıralama yok, seyahat süresi yok, günlük plan yok, cadence compliance
yok, ziyaret kaydı yok. Frequency / call-cycle policy **MOD-0165 / MOD-0167 → MOD-0155** zincirinde kalır.

## 12. RBAC Notes

Canonical hedefler (ikisi de PKS-001 geçerli): **`crm.contact.availability.read`** ve
**`crm.contact.availability.manage`**. Katalog/grant hazır değilse implementation **seed/grant değiştirmez**; geçici
fallback read için `crm.contact.read`, manage için `crm.contact.update`'tir ve **yalnız availability endpoint'leri**
ile sınırlıdır. Fallback **yetki genişletmez** — §20.3 guard'larının tamamı yine çalışır. Ayrı **delete permission'ı
tanımlanmamıştır** (hard delete yok). Follow-up: **`MOD-0150-FU-RBAC — Contact Availability Permission Catalog
Alignment`** (§19).

## 13. Contract Flag Notes

```json
{
  "supportsContactAvailability": true,
  "supportsAccountContactLinkAvailability": true,
  "supportsVisitPreference": true,
  "supportsAvailabilityExceptions": true
}
```

Bu flag'ler **yalnız availability/preference master data desteğini** ifade eder; visit planning, route planning veya
frequency desteği anlamına **gelmez**. `supportsVisitPlanning` / `supportsRoutePlanning` / `supportsVisitFrequency`
gibi bir flag **eklenmemiştir**. Mevcut MOD-0150 sözleşme yüzeyi korunmuştur.

## 14. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| MOD-0151 code changed? | **No** (MOD-0151 pack'i de değiştirilmedi) |
| MOD-0155 code changed? | **No** |
| Route planning opened? | **No** |
| Visit planning opened? | **No** |
| Frequency/call-cycle engine opened? | **No** |
| Campaign engine opened? | **No** |
| Territory assignment opened? | **No** |
| ContactTerritoryAssignment opened? | **No** |
| Account master mutation opened? | **No** |
| Contact master mutation opened? | **No** (availability ayrı aggregate; `Contact`'a alan eklenmedi) |
| Patient data opened? | **No** |
| Workflow scope opened? | **No** |
| Import/export opened? | **No** |
| Hard delete allowed? | **No** |
| RBAC seed/grant changed? | **No** (yalnız follow-up açıldı) |
| MOD-0048 publish changed? | **No** (yalnız 3 set **önerildi**) |
| GPS / check-in-out opened? | **No** |
| ContactAvailability scope added? | **Yes** |
| Existing MOD-0150 scopes preserved? | **Yes** (FU01–FU06 + D1–D7 dokunulmadı) |

## 15. Created / Updated Files

- **Updated:** `execution/domains/commercial-suite/module-packs/MOD-0150-contact-relationship-management.md`
  - frontmatter `runtime_code_scope` (**+`FU-contact-availability-visit-preference`**, additive; FU01 clause aynen korundu)
  - header'a **Scope update (2026-08-01)** bloğu (sahiplik zinciri + "genel runtime izni değildir")
  - §3 Owns satırına link-scoped availability/preference/exception eklendi
  - §4 Owned Objects: `ContactAvailability` · `ContactAvailabilityException` · `VisitPreference` satırları
  - §5 Out-of-Scope'a "§20 clarification" (availability sahipliği planner yapmaz)
  - **yeni §9.4** `ContactAvailability` · **yeni §9.5** `VisitPreference` · **yeni §9.6** `ContactAvailabilityException`
  - §10 reference data: `contact-availability-type` · `contact-availability-status` · `availability-exception-reason`
    (+ "pack hiçbir şey publish etmez / fail-closed" notu)
  - §11 permission: iki yeni canonical anahtar + fallback + RBAC follow-up kararı
  - §12 integration contracts: MOD-0151 FU09A ve MOD-0155 tüketici satırları
  - §13 UI: madde 8–10 + "gösterilecek / kesinlikle olmayacak" listeleri
  - §14 sequencing: **FU07** satırı (scope + dependencies + acceptance)
  - §15 acceptance criteria: §20 blokları (11 madde)
  - §16 decisions: **D8–D15**
  - §17 out-of-scope guard: 6 yeni yasak satırı
  - §19 follow-ups: RBAC alignment · MOD-0048 template extension · frequency sahipliği notu · timezone deferral ·
    **frontmatter scope-string drift** kaydı
  - **yeni §20** FU — Contact Availability & Visit Preference (allowed scope · data model · validation · read API ·
    write API · MOD-0151 boundary · MOD-0155 boundary · contract flags · test expectations · exclusions)
- **Created:** `docs/audits/mod-0150-contact-availability-visit-preference-pack-authorization-2026-08-01.md` (bu rapor)

**Kod, test, gateway, frontend, seed veya reference-data dosyası değiştirilmemiştir.**

## 16. Final Verdict

**PASS**

- `FU-contact-availability-visit-preference` scope'u **additive** olarak eklendi; MOD-0150'nin mevcut FU01–FU06
  scope'ları ve D1–D7 kararlarının hiçbiri silinmedi veya daraltılmadı.
- **`AccountContactLink` bazlı sahiplik netleşti (D8/D9):** availability Contact'ta düz alan **değildir**; aynı
  doktorun her lokasyonu bağımsız takvim taşır; `ContactId`/`AccountId` yalnız link'ten türetilen kopyalardır.
- **VisitPreference ve exception policy netleşti:** preference link bağlamında okunur (D10), preferred opsiyoneldir
  (D11), avoid window **daha güçlü kısıttır** (D13), date-specific exception haftalık deseni **ezer** (D12).
- **MOD-0151 read-only tüketim boundary'si netleşti:** veri kopyalanmaz, MOD-0151'de master aggregate açılmaz,
  availability yoksa `unknown` döner (D15) ve `AppointmentRequired` candidate düşürmez (D14).
- **MOD-0155 planning tüketim boundary'si netleşti:** MOD-0155 planı üretir, MOD-0150 planın hiçbir parçasını üretmez.
- **Route / visit / frequency / campaign implementation açılmadı; patient data açılmadı; Account/Contact master
  mutasyonu ve `ContactTerritoryAssignment` açılmadı; hard delete açılmadı.**
- Ek governance kazanımları: (a) **conflict policy** (aynı link+weekday örtüşmesi → 409, sessiz merge/overwrite yasak)
  ve **idempotency** kuralı yazıldı; (b) üç MOD-0048 seti **öneri** olarak eklendi ve fail-closed davranış kayda
  geçti; (c) RBAC için seed'siz fallback + `MOD-0150-FU-RBAC` follow-up'ı açıldı; (d) **timezone/wall-clock** kararı
  açıkça deferred yazıldı — sessiz varsayım bırakılmadı; (e) frontmatter'daki **FU01-only scope drift'i** düzeltilmek
  yerine kayda geçirildi (geçmişi sessizce yeniden yazmamak için).
- Implementation prompt'u hazırlanabilir.

## 17. Next Recommended Prompt

```
@orchestrator MOD-0150-FU — Contact Availability and Visit Preference
```
