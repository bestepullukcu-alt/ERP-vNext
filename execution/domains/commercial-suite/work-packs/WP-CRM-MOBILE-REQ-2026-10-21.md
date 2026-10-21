# WP-CRM-MOBILE-REQ — Android "CRM tamamlama talebi" (2026-10-20) yanıtı ve CRM-ACC-EDIT

Kaynak: Android (DitenMultiply) notu "CRM tamamlama talebi — Android'den backend ekibine", 2026-10-20 (claude.ai artifact
`N2xUehhjgfB4jjVWsPNxru`; dayanak `main` @ `4728cb804`, `test/crm-content-visit-e2e` @ `18577e56a`). CT incelemesi kod + Blueprint
8.7 + plan üzerinden, 2026-10-21.

## Kararlar (kullanıcı, 2026-10-21)

| Talep | Ölçülen | Karar |
|---|---|---|
| CRM-EDIT-1…4 — müşteri düzenlemede veri kaybı | `UpdateAccountHandler` sürüm denetlemiyor (son yazan kazanır); `LogoDataUri` gönderilmezse logo siliniyor; 400 / 409'larda alan / makine kodu yok. MOD-0149 paketi §19 bunu zaten istiyor ("Concurrency conflict → 409 + reload, sessiz overwrite YOK"; kod / dış referans çakışması 409 + alan hatası) → **paket uyumsuzluğu**. Web müşteri formu da aynı uç. | **YAPILACAK — şimdi** (CRM-ACC-EDIT, W6-WEB-a ile paralel; farklı dosyalar) |
| CRM-ROLE-1 — saha temsilcisi rolü | Anahtarlar var; canlıda temsilci rolü olarak `CrmUser` kullanılıyor. | **YAPILACAK** — canlıda **`CrmUser` kalır** (ayrı rol yok); okuma anahtarları (`crm.account.read`, `crm.contact.read`, `crm.account-contact.read`, `crm.visit-plan.read`) + gerekirse yazma anahtarları **W6-CFG yetki betiğine** eklenir. |
| CRM-REF-1 — iş yeri türü Türkçe etiketleri | Kural: referans verisi etiketi betikle yamanmaz; veri sahibi işi. | **Veri sahibine** (Referans Verisi ekranı, MOD-0048 hazırlayan / onaylayan). |
| CRM-LIVE-1 — canlı / test ortamı + test kullanıcısı | Dağıtım CT işi değil; kimlik bilgisi CT'den paylaşılmaz. | **Kısmen** — çıkış tarihini dağıtım sahibi; test kullanıcısını kiracı yöneticisi açar, bilgiler güvenli yoldan. |
| CRM-OPP-1 — Fırsat | Blueprint MOD-0153 Opportunity & Pipeline Management (Sales, W-3) var; CRM planında yok. W4'teki "opportunity" doktor sınıflandırmasıdır (potansiyel × pay), satış fırsatı değil. | **ERTELENDİ.** Alınırsa önce modül paketi (DCP-002 + `verify_module_id.py`), sonra BE ∥ WEB + mobil not. Para birimi kuralı ön siparişteki gibi (K-W10 g). |
| CRM-ACT-1 — Aktivite | Blueprint'te CRM aktivite modülü yok; yüz yüze temas MOD-0155; "görev" için Platform Görev Merkezi (MOD-0024) var. Telefon / e-posta / toplantı kaydı yeni modül (MCR) ister. | **ERTELENDİ.** Öneri: takip / görev ihtiyacı Görev Merkezi ile; ziyaret raporu takiplerinin göreve dönüşmesi ayrı karar. |

## §36.1 Agent Prompt — CRM-ACC-EDIT (paste-ready · CRM + Web: müşteri güncellemede sürüm, logo koruma, alan / çakışma kodları · W6-WEB-a ile paralel)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-CRM-MOBILE-REQ · CRM-ACC-EDIT — MOD-0149 müşteri (Account) güncellemesinde iyimser eşzamanlılık (version / expectedVersion → 409), gönderilmeyen logonun korunması, 400 / 409'larda alan ve makine kodları; Web müşteri formu buna uyar; mobil sözleşme notu
Repository: C:\tmp\crm-acc-edit (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/crm-acc-edit · commit bu dala, push YOK. Kullanıcının checkout'una DOKUNMA. Kapsam: services/Diten.CrmService/** (yalnız Account özelliği + gerekiyorsa ortak depo yardımcısı), frontend/Diten.Web/** (yalnız CRM Accounts formu + resx), testler. W6 dosyalarına DOKUNMA (W6-WEB-a paralel koşuyor).
ÖNCE OKU: execution/domains/commercial-suite/work-packs/WP-CRM-MOBILE-REQ-2026-10-21.md (kararlar tablosu) · MOD-0149 paketi (ready-for-dev) §10 alan modeli (`Version` concurrency için rezerve), §16 yetki, §17 rota, §18 doğrulama, **§19 hata yolları** (kod / dış referans çakışması 409 + alan hatası; eşzamanlılık 409 + "veri değişti, yeniden yükle", sessiz overwrite YOK) · CRM: `Features/Account/**` (`UpdateAccountCommand`, `UpdateAccountHandler`, `UpdateAccountValidator`, Create akışı, DTO'lar), `Domain/Entities/EntityBase.cs` (`Version`), Account deposu · Örnek desen: MOD-0168 / MOD-0156 `ExpectedVersion` + `ReplaceAsync` sürüm süzgeci (W6-BE-a/b) · Web: `Views/CRM/Accounts/**` + js + resx.
NE:
(1) Sürüm: müşteri ayrıntı ve liste DTO'larında `version`; `UpdateAccountCommand.ExpectedVersion` ZORUNLU (yoksa 400 `account_expected_version_required` — mobil henüz canlıda değil, Web aynı commit'te güncellenir). Yazma atomik: depo güncellemesi `{Id, TenantId, Version == expected}` süzgeciyle; eşleşmezse **409 `account_version_conflict`**, kayıt değişmez; başarılı yazma `Version + 1`. Silinmiş kayıt güncellemesi 404 (paket §19).
(2) Logo: `LogoDataUri` gönderilmezse (null / boş) mevcut logo KORUNUR; silmek için açık `RemoveLogo: true`. Formda olmayan alanlar (ek nitelikler `PUT …/attributes` ile ayrı) güncellemede değişmez — `UpdateAccountCommand`'ın taşımadığı Account alanlarının listesini çıkar ve korunduklarını testle kanıtla.
(3) Hata kodları: hata zarfı `errors = [mesaj, kod]` deseni; 400'lerde alan adı + makine kodu (validator `WithErrorCode`, ör. `account_name_required`, `account_type_invalid`); 409'larda `account_code_taken`, `external_reference_taken`, `account_version_conflict` — Create ve Update ikisinde de aynı kodlar.
(4) Denetim: dokunulan yazma komutları (`UpdateAccountCommand`, gerekiyorsa `CreateAccountCommand`) `IAuditableCommand` olur (AUD-001; kategori Crm, kaynak modül MOD-0149); F8-b borç listesi / ledger AUD-001 kurallarına göre güncellenir; known_failures.py AuditTrail BİLİNEN kalmalı ya da kayıt değişikliği raporda gerekçeyle.
(5) Web: müşteri düzenleme formu `version`'ı taşır; 409 `account_version_conflict` → "Kayıt başka biri tarafından değiştirildi; yeniden yükleyin" (7 dil, yeniden yükle düğmesi); 409 kod / dış referans → ilgili alanın altında; logo değiştirilmediyse gönderilmez; "Logoyu kaldır" açık seçimi `RemoveLogo`.
(6) Mobil not: bu WP belgesine "Sözleşme eki — CRM-ACC-EDIT (mobil)" bölümü — alanlar (`version`, `expectedVersion`, `removeLogo`), uç (değişmeyen `PUT api/crm/accounts/{id}`), hata kodları tablosu, örnek istek / yanıt.
KORU/YAPMA: Gateway rotası değişmez (yeni uç yok); Create davranışı aynı (yalnız kodlar eklenir); diğer CRM modülleri değişmez; localhost:27017 YOK (gerçek Mongo testi geçici mongod).
DOĞRULA: eski sürümle güncelleme → 409 + kayıt aynı; eşzamanlı iki güncelleme → biri 409 (gerçek mongod testi, atomik süzgeç); expectedVersion yok → 400; logo gönderilmezse korunur, `RemoveLogo` siler; taşınmayan alanlar korunur; 400 alan kodları; 409 üç kod (Create + Update); kiracılar arası 404; yetki (`crm.account.update`). Web: xUnit + RunNode — form version taşır, 409 iletisi 7 dil, alan altı hata. Sabotajlar (en az 4): sürüm süzgecini kaldır, logo null → sil'e geri dön, `account_code_taken` kodunu kaldır, Web formu version göndermesin. Tam takımlar: CRM (-o repo içi; taban aynı baş), Web (taban aynı baş; bilinen `ShipmentDetailsDateNode` ×2), mimari (known_failures.py).
Commit: "fix(crm,web): WP-CRM-MOBILE-REQ CRM-ACC-EDIT — account update with optimistic concurrency (409 on a stale version), an omitted logo is kept, field and conflict codes on 400 / 409; the web form carries the version; mobile contract note" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## Sözleşme eki — CRM-ACC-EDIT (mobil)

> Kaynak: `wp/crm-acc-edit` (CRM-ACC-EDIT). Uç **değişmedi**: `PUT api/crm/accounts/{id}` (yetki `crm.account.update`, Gateway rotası aynı). Okuma uçları (`GET api/crm/accounts`, `GET api/crm/accounts/{id}`) kayda `version` ekler.

**Alanlar**

| Alan | Nerede | Kural |
|---|---|---|
| `version` | liste satırı + ayrıntı yanıtı | kaydın eşzamanlılık belirteci (tam sayı; her yazmada +1 — düzenleme, silme, üst müşteri bağla / çöz) |
| `expectedVersion` | `PUT` gövdesi | **zorunlu**; okunan `version`. Yoksa 400 `account_expected_version_required`; eskiyse 409 `account_version_conflict`, kayıt değişmez |
| `logoDataUri` | `PUT` gövdesi | **gönderilmezse (null / boş) logo korunur**; yeni logo göndermek için `data:image/...;base64,...` |
| `removeLogo` | `PUT` gövdesi | `true` → logo silinir. `logoDataUri` ile birlikte gönderilirse 400 `account_logo_conflict` |

Güncellemenin **taşımadığı** alanlar hiç değişmez: `accountCode`, `parentAccountId` (ayrı uç: üst müşteri bağla / çöz), dış referanslar ve ek nitelikler (kendi uçları), oluşturma zamanı.

**Hata zarfı:** `errors = [mesaj, kod, mesaj, kod, …]` — her sorun için bir çift; `mesaj` geliştirici içindir (İngilizce), ekran **kodu** kendi dilinde yazar.

| HTTP | Kod | Alan | Ne zaman |
|---|---|---|---|
| 400 | `account_expected_version_required` | — | `expectedVersion` yok |
| 400 | `account_name_required` · `account_name_too_long` | accountName | boş / > 200 |
| 400 | `account_type_required` · `account_type_invalid` | accountType | boş / yayımlı `account-type` setinde değil |
| 400 | `account_status_required` · `account_status_invalid` | status | boş / yayımlı `account-status` setinde değil |
| 400 | `account_category_invalid` | accountCategory | yayımlı `account-category` setinde değil |
| 400 | `account_reference_set_missing` | — | zorunlu referans seti yayımlanmamış |
| 400 | `account_address_too_long` · `account_latitude_invalid` · `account_longitude_invalid` | addressLine · latitude · longitude | > 500 / −90..90 / −180..180 dışı |
| 400 | `account_responsible_name_too_long` · `account_phone_invalid` · `account_email_invalid` | responsiblePerson* | > 200 / > 32 / e-posta değil |
| 400 | `account_notes_too_long` | notes | > 2000 |
| 400 | `account_logo_invalid` · `account_logo_too_large` · `account_logo_conflict` | logoDataUri | biçim / boyut / `removeLogo` ile birlikte |
| 404 | `account_not_found` | — | yok, silinmiş ya da başka kiracının |
| 409 | `account_version_conflict` | — | kayıt bu arada değişti → **yeniden oku, değişikliği tekrar uygula** (sessiz üzerine yazma yok) |
| 409 | `account_code_taken` · `external_reference_taken` | accountCode · externalReference | yalnız oluşturmada (güncelleme bu alanları taşımaz) |

**Örnek istek / yanıt**

```http
PUT /api/crm/accounts/5f1c…-…   (Authorization: Bearer …, X-Tenant-Id: …)
{ "accountName": "Eczane Şifa", "accountType": "pharmacy", "status": "active", "cityRef": "istanbul",
  "addressLine": "Bağdat Cad. 2", "expectedVersion": 3 }
→ 200 { "data": true, "statusCode": 200, "isSuccessful": true, "errors": null }      // logo korunur; yeni version = 4

PUT … { …, "expectedVersion": 3 }   // biri araya girdi
→ 409 { "data": false, "statusCode": 409, "isSuccessful": false,
        "errors": ["The account was changed by someone else; reload it and apply your change again.", "account_version_conflict"] }

PUT … { …, "accountType": "garage", "expectedVersion": 4 }
→ 400 { "errors": ["'garage' is not a valid published value of reference set 'account-type'.", "account_type_invalid"], … }
```

Başarılı yanıt yeni `version`'ı dönmez (`data: true`); sonraki düzenleme için kaydı yeniden okuyun (`GET api/crm/accounts/{id}`).
