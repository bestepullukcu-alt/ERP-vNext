# WP-0049-FU01-DL2 — İş Ortağı Ana Verisi DL-2: MDM kiracı ekranı (liste, oluştur, düzenle, ayrıntı), Web vekili, hata metinleri, AC-15 canlı kanıtı

- **Kimlik:** WP-0049-FU01-DL2 · **Modül:** MOD-0049-FU01 Business Partner Master Foundation (master-data-management, `Diten.MdmService` 5059 +
  `Diten.Web` 5001, kiracı modülü, Golden **Compact**, `data_mode: server`).
- **Paket:** `execution/domains/master-data-management/module-packs/MOD-0049-FU01-business-partner-master-foundation.md` **r4**, LF sha256
  `a4968cb5f0f513803eeccd7ac29b05f1336e2e33657872461ca015d5cc29dcf8` (CT ölçümü 2026-11-14, `git show HEAD:<paket> | sha256sum`, HEAD `60379b0c1`).
  Paket bu dilimde **değişmez** (değişirse onay düşer).
- **Onay satırı:** `execution/approvals/approvals.tsv:96` (onaylayan Ali / GitHub `GMG-ERP`, yazan `bestepullukcu-alt`, 2026-11-13, HIGH). İnceleme r4:
  `docs/records/reviews/MOD-0049-FU01/2026-11-13-a4968cb5.md` (UYGUN).
- **Önceki dilim:** DL-1 **KOŞULLU** — kabul kaydı `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`, "Ek — MOD-0049-FU01 DL-1" ve
  "MOD-0049-FU01 DL-1 — kapılar (2026-11-14)" bölümleri. DL-1 commit'leri `4cba4d69e`, `ec1cbd139`, `54e24a94c`, `f7a2a7d46`, `916f207a8`; CT kapıları
  `28e2e9f2e` (RECORD-CODE-ALLOCATE 1.1.0, BUSINESS-PARTNER-READ 1.0.0 FROZEN, Gateway iki MDM rotası), kayıt `98a5bb2c9`. **CT kararı:** AC-15 canlı
  kanıtı DL-2 kabulüne taşındı; DL-1, DL-2 ile birlikte ACCEPTED olur.
- **Sözleşmeler (okunur, yazılmaz):** `docs/analysis/contracts/business-partner-read.md` 1.0.0 FROZEN (Web görünüm modelleri bu dosyaya karşı yazılır),
  `docs/analysis/contracts/record-code-allocate.md` 1.1.0 FROZEN (yalnız AC-15 bağlamı).
- **Dilim:** DL-2 (paket §23 DL-2 satırı, §24 DL-2 satırı) + DL-1'den taşınan AC-15 / S-15. **Kanıt seviyesi:** E4 (§23).
- **Sahip:** geliştirici oturumu (merkez, T-5). **CT doğrulayıcı:** MVP-1 CT (sahip Beste). **Durum:** ready — başlama koşulları §1.2'de.
- **Dil:** kullanıcıya görünen her metin 7 dilde (en, tr, fr, es, zh, ar, ru); `ar` sağdan sola. Bir dil eksikse iş bitmemiştir.

## 1. Kararlar ve kapılar

### 1.1 Bu WP'ye yazılan kararlar (bağlayıcı)
| # | Karar | Kaynak |
|---|---|---|
| K-1 | Hata metinleri Web'de, anahtar biçimi **`Error.{KOD}`** (ör. `Error.MDM_BP_INACTIVE`; paket §8.5 / §5'teki `Error_{kod}` yerine); köprü ortak `wwwroot/assets/js/shared/diten-refusal.js` — ekrana özel kod çeviri JS'i yazılmaz | WP-0049-FU01-DL1 §2 F-4; kabul kaydı CT kararı (2) |
| K-2 | **19 kodun hepsi** için `Error.{KOD}` 7 dilde (§5.3 tam liste) — paket §8.5'in 15 kodu + `MDM_BP_VALIDATION_FAILED`, `EXPORT_TOO_LARGE` (DL-1 WP F-1), `MDM_BP_STORE_UNAVAILABLE` (503), `MDM_BP_UNEXPECTED_ERROR` (500) | kabul kaydı CT kararları (1), (2); `BusinessPartnerModels.cs:112-144` |
| K-3 | AC-15 + S-15 canlı kanıtı bu dilimde, **kullanıcı yolu** üzerinden (tarayıcı → Web 5001 vekili → Gateway 5000 → MDM 5059 → Platform kod servisi); Web jetonu HttpOnly olduğundan tarayıcı Gateway'e doğrudan gitmez | kabul kaydı "kapılar" CT kararı |
| K-4 | **Menü kuralı:** modül kiracı planına (A-3) DL-2 ekranı hatta girmeden eklenmez; plan girişi açılmadıkça menü satırı görünmez. DL-2 bunu kodla gizlemeye çalışmaz (manifest değişmez) | kabul kaydı CT kararı "Menü" |
| K-5 | Vekil, Brand'in `Reason_{code}` çevirisini (`MasterDataGatewayController.RefusalLocalizer`) **kullanmaz**: `RefusalLocalizer` null kalır, MDM ret gövdesi (`errorCodes[{ code }]`) tarayıcıya aynen gider, cümleyi `diten-refusal.js` `Error.{KOD}`'dan kurar. Web'in kendi ürettiği retler (izin yok 403, Gateway yok 502, gövdede `tenantId` 400) yerelleştirilmiş cümleyle ve `local: true` ile döner — İngilizce sabit metin (`"Permission denied."`, `"Gateway unavailable."`) kullanıcıya ulaşmaz | ölçüm: `MasterDataGatewayController.cs:122-149`, `:245-289`, `:366-370`; `diten-refusal.js:1-12` |
| K-6 | `Accept-Language` her vekil çağrısında iletilir (AC-24). Ölçüm: tabandaki `ProxyGetAsync` / `ProxyJsonAsync` iletmez (`SendGatewayAsync` `configure` parametresi yalnız Brand dışa aktarımında kullanılıyor, `BrandsController.cs:300-308`). Çözüm **yeni controller içinde**: `SendGatewayAsync(..., configure: r => r.Headers.TryAddWithoutValidation("Accept-Language", …))` + `RelayAsync`. **`MasterDataGatewayController.cs` değişmez** (MOD-0290-FU02 ortak taban; değişiklik gerekirse dur, XMC-001) | ölçüm |
| K-7 | AC-22 / S-22'nin istediği vitest vakasının dosyası paket §5'te adsız: yeni dosya `frontend/Diten.Web/tests/business-partners-status-reason.test.js` bu WP ile Repo Scope'a eklenir (yalnız bu modülün JS'ini test eder; domain-config "MasterData/BusinessPartners surface" istisnası içinde). Paket bir sonraki içerik revizyonunda hizalanır | paket AC-22, §5; domain-config `:73-93` |
| K-8 | AC-27 (b) (her `BusinessPartnerErrorCodes` sabiti için `Error.{KOD}` 7 Web resx'te, yansıma) **aynı yeni dosyada** yazılır: `services/Diten.MdmService/tests/Diten.MdmService.Api.Tests/BusinessPartnerExportWordsGuardTests.cs` (§5'te DL-2'ye adlı tek MDM test dosyası); MDM'de başka yeni / değişen test dosyası yok | paket §5, AC-27 |
| K-9 | Doğrulayıcı komutu: paket §11 / AC-18'deki çıplak `--data-mode` betikte geçersiz (`choices=[server, client]`, `verify_datatable_page.py:901-906`). Doğru komut §8'de: `--reference compact --data-mode server --api-profile proxy` | ölçüm |
| K-10 | Pasif kayıtta `Edit` sayfası formu çizmez; `Error.MDM_BP_INACTIVE` cümlesini ve Ayrıntı bağlantısını gösterir (Q-6 "pasif kayıt salt okunur"; liste zaten Düzenle'yi yalnız `Active` satırda çizer — AC-20). Sunucu yine 409 döner; istemci denetimi yalnız kolaylıktır | paket §8.2, §11 kural 2 |

### 1.2 Açık kapılar

**DL-2'yi BAŞLAMADAN bekleten (CT / Beste):**
- **B-1 — X-9'un kalan parçası: `PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` `Help.BUSINESSPARTNERS.*` (yalnız ekleme).** Paket §5.1 X-9 + §24 DL-2
  "X-9 Web ortak sahibi onayı". Ölçüm (HEAD `60379b0c1`): `SharedResource` parçası DL-1'de yazıldı (`916f207a8`, CT onayı 2026-11-14: `Nav.Module.BUSINESSPARTNERMASTER`,
  `Nav.Page.BUSINESSPARTNERS`, `Perm.Entity.BUSINESSPARTNERS`, 7 dilin her birinde 3/3); `Help.BUSINESSPARTNERS` 7 dilde **0** — onay kaydı yok
  (`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md`'de X-9 satırı yok). **Onay kaydı gelmeden `PageHelpResource.*`'a dokunma**;
  diğer işler (vekil, görünümler, JS, ekran resx'i) beklemeden başlar. `PageHelpCatalog.cs` **değişmez** (yalnız ilişkili sayfa / ikon tutar; MOD-0179 `TPLHEALTH`
  depoda yok — §25 "ekranı gelmeden link çizilmez").
- **B-2 — ekran brief / mockup (paket §24 DL-2 "merkez kuralı — yeni modül ekranı").** Ölçüm: `execution/domains/*/work-packs/mockups/` altında MOD-0049-FU01 briefi
  **yok**. CT / Beste kararı gerekir: ya brief (+ mockup istemi) DL-2'den önce yazılır, ya da Golden Compact + Brand emsaliyle muafiyet kayda geçer. Karar
  gelmeden geliştirici görünüm düzenine (sekmeler, kolon sırası) bağlayıcı bir tasarım varsaymaz; paket §11 dosya seti ve kuralları her durumda bağlayıcıdır.

**DL-2'yi başlamadan bekletmeyen, kabulü (ACCEPTED) bekleten (Ali):**
- **A-2 — rol şablonu (X-10, Auth, ayrı WP):** "Ana Veri Sorumlusu" = 6 anahtar, "3PL Operatörü" / "Görüntüleyici" = `read` (paket §14). Bu dilim
  `services/Diten.AuthService/**`'a dokunmaz.
- **A-3 — kiracı planına `business-partner-master` (Platform yönetimi):** K-4 gereği DL-2 ekranı hatta girdikten **sonra** açılır. Plan senkronu Admin = hepsi,
  Viewer = `read` verir; menü satırı ancak o zaman görünür.
- A-2 / A-3 açılmadan gerçek kullanıcıyla AC-15 ve AC-24 tarayıcı turu koşulamaz (izin talebi). Dev ortamında izni elle verme yolu varsa (Platform yöneticisi
  / Ali) geliştirici turu koşar ve "elle verildi" diye yazar; yoksa tur CT canlı turuna devredilir (§12 B-5).

**Bilgi (DL-2'yi etkilemez):** DL-3 Gateway kapsama testi (integration-agent, AC-25) ayrı iş; A-4 (servis kimliğiyle okuma, MOD-0179 Q-12); MOD-0179 DL-4 kapısı
(DL-1 kabulü + rota + FROZEN sözleşme — DL-2 bunu bekletmez).

## 2. Amaç ve kapsam

**Amaç (§23 DL-2):** MDM kiracı ekranı `Master Data → İş Ortakları` — liste, oluştur, düzenle, ayrıntı (Genel · İletişim kişileri · Roller · Geçmiş); same-origin
Web vekili; 7 dil; hata kodu metinleri; sayfa yardımı; DL-1'den taşınan AC-15 canlı kanıtı.

**Kapsam (paket bölümleri):** §9 Layout & Shell, §11 Frontend File Contract (dosya seti + kurallar 1–8), §25 Page Help, §8.5 (yerelleştirme yeri — K-1, K-2),
§4.6 madde 5 (ekranda maskeli vergi no rozeti), §8.1 (ülke listesi, sabit yedek yok), §13 ekran tarafı başarısızlık yolları, §14 (düğme = izin), AC-15, AC-17…AC-24,
AC-27.

**Servis (MDM) değişikliği: YOK.** DL-2'de MDM üretim kodu, manifest, sözleşme, Gateway rotası değişmez. MDM'de tek yeni dosya `BusinessPartnerExportWordsGuardTests.cs`
(K-8). DL-1 kodunda hata bulunursa düzeltme bu dilimde yapılmaz: dur, CT'ye yaz (FIX WP'si açılır).

**Kapsam DIŞI:** rol ver / rol kaldır (C-5 — düğme, modal, vekil eylemi yok), silme / toplu silme / toplu seçim, içe aktarma, Gateway testi (DL-3), Auth rol şablonu
(X-10), kiracı planı (A-3), `.antigravity/**`, `ocelot.json`, sözleşme dosyaları, MOD-0179 ekranları.

## 3. Adım 0 — hattı al

Ana checkout'ta, çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` üzerinde (yeni dal açılmaz):

```bash
git checkout feature/sce/mod-0173-inventory-ledger-valuation
git merge-base --is-ancestor ct/line-ff-2026-11-14d HEAD && echo "ata — devam"
# ata değilse:
git merge --ff-only ct/line-ff-2026-11-14d
# ff olmazsa:
git merge --no-ff ct/line-ff-2026-11-14d
# çakışmada: git merge --abort, dur, CT'ye bildir (çakışmayı kendin çözme)
```

- Etiketi CT oluşturur (2026-11-14 itibarıyla son etiket `ct/line-ff-2026-11-14c`); etiket yoksa dur, CT'ye sor.
- Sonra doğrula (her biri çıkış 0): `git merge-base --is-ancestor 916f207a8 HEAD`, `… 28e2e9f2e HEAD`, `… 98a5bb2c9 HEAD`; paket LF sha256 yukarıdakiyle aynı
  (`git show HEAD:<paket> | sha256sum`). Değilse dur.
- Dilim başı commit'ini (`<dilim-başı>`) kaydet; bütün "önce" ölçümleri bu commit'te, aynı gün:
  - Web.Tests, MDM App, MDM Api, Gateway filtresiz taban (kırmızılar adıyla — §8); vitest `role-assignments-label-ledger`, `diten-enter-primary`,
    `dialog-one-implementation`.
  - `NavManifestL10nGuardTests` (§5.4): `NavVisiblePageKeyFloor` (bugün `:37` **45**), `PagelessUntil` girdileri (bugün 4: `WAVE-PLANNING`, `BARCODE-RFID`,
    `mrp-replenishment`, `safety-stock-optimization`; `business-partner-master` **yok**). DL-2 bu dosyaya **dokunmaz**.
  - `PYTHONUTF8=1 python scripts/check_pack_nav_keys.py` (CT ölçümü 2026-11-14: `12 key(s) off form, 12 known, 0 new`, çıkış 0).
  - `Help.BUSINESSPARTNERS` sayısı 7 `PageHelpResource` dosyasında (bugün 0).

## 4. İlk adımlar (kod yazmadan önce)

### 4.1 XMC-001 kayıt denetimi
- B-1 (Help anahtarları) ve B-2 (brief) kararlarını oku; kayıt yoksa ilgili dosyaya / tasarım varsayımına girme.
- §5.4 tablosunda "dokunulmaz" yazan dosyaların hiçbirine dokunma; listede olmayan bir dosya gerekirse **dur**, CT'ye yaz. Onay kayıtları teslim raporuna yazılır.

### 4.2 Sözleşme önce
- Görünüm / vekil modelleri `docs/analysis/contracts/business-partner-read.md` 1.0.0'a karşı yazılır (liste öğesi, ayrıntı, `roles` liste `string[]` / ayrıntı
  `[{ role, assignedAt }]`, `isTaxNumberMasked`). Yazma gövdeleri DL-1 DTO'larına karşı: `CreateBusinessPartnerRequest` (Roles var), `BusinessPartnerWriteRequest`
  (Roles **yok**), `BusinessPartnerStatusChangeRequest` (`reason`, `expectedVersion`). Uyuşmazlık görürsen dur, CT'ye yaz (sözleşme ve MDM değişmez).

### 4.3 Ekran ölçümü (dokunma protokolü)
- Liste ekranı: `PYTHONUTF8=1 python .antigravity/scripts/verify_datatable_page.py . --area MasterData --module BusinessPartners --reference compact --data-mode server --api-profile proxy --format gaps`
  (K-9). Yeni kiracı ekranı bekçi tuhaflıkları (kayıtlı): proxy profili `const endpoint = '/MasterData/BusinessPartners/api'` ister; en az bir `fetch`'te
  `headers: getAuthHeaders()`; `export: { mode: 'server', url }` ve gerçek bir `api/export` eylemi (BL-452); `_IndexL10n`'de `Unknown` (+ `Edit`);
  `HasSelection = false` olduğundan `/bulk` muafiyeti betiğin kendi beyanıyla (`verify_datatable_page.py:1534-1542`).
- Golden Compact emsali: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/` (9 dosya) + `wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/`
  (3 JS). Brand ekranı (`Views/MasterData/Brands/`) vekil / UAS-001 / dışa aktarım emsalidir ama **Compact değildir** (`_DetailsQuickView.cshtml` taşır — kopyalama).

## 5. Dokunulacak dosyalar

`WEB` = `frontend/Diten.Web/`, `WEBT` = `frontend/Diten.Web.Tests/`, `MDMT` = `services/Diten.MdmService/tests/` (paket §5 DL-2 satırları + K-7, K-8).

### 5.1 Modülün kendi dosyaları (yeni)
- `WEB/Views/MasterData/BusinessPartners/` — §11 birebir: `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml` (her birinde Razor bloğunda
  **açıkça** `Layout = "_LayoutTenantShell";`), `_Form.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`,
  `HasSelection = false`), `_IndexL10n.cshtml`, `BusinessPartnersIndex.cs` (marker). Partial'larda `Layout =` yok. **Yasak:** `_CreateEditOffcanvas.cshtml`,
  `_DetailsQuickView.cshtml`, Index içinde oluştur / düzenle paneli, `_BulkActionBar`.
- `WEB/Controllers/MasterData/BusinessPartnersController.cs` — `MasterDataGatewayController`'dan türer, `[Route("MasterData/BusinessPartners")]`. Sayfa eylemleri:
  Index / Details (`read`), Create (`create`), Edit (`update`) — izin yoksa yalnız `_AccessDenied` + 403, yönlendirme yok (UAS-001). Vekil eylemleri (hepsi
  Gateway 5000 `/api/mdm/business-partners…`, `Accept-Language` iletir — K-6):

  | Vekil | Gateway | İzin | Not |
  |---|---|---|---|
  | `GET api` | `GET /api/mdm/business-partners?role&status&search&countryCode&page&pageSize&orderBy&orderDir` | `read` | DataTables `start/length/order` → `page/pageSize/orderBy/orderDir` (beyaz liste `code, shortName, legalName, status, updatedAt`) |
  | `GET api/{id:guid}` | `GET …/{id}` | `read` | |
  | `GET api/{id:guid}/history` | `GET …/{id}/history` | `read` | Geçmiş sekmesi |
  | `GET api/export` | `GET …/export?format&columns&<liste süzgeçleri>` | `export` | Brand emsali bayt aktarımı (`BrandsController.cs:292-322`): `Cache-Control: no-store`, dosya adı başlıktan; ret JSON olarak döner |
  | `POST api` | `POST …` | `create` | `[ValidateAntiForgeryToken]`; gövde `CreateBusinessPartnerRequest` |
  | `PUT api/{id:guid}` | `PUT …/{id}` | `update` | `[ValidateAntiForgeryToken]`; gövdede `roles` gönderilmez |
  | `POST api/{id:guid}/deactivate` | `POST …/{id}/deactivate` | `deactivate` | `[ValidateAntiForgeryToken]` |
  | `POST api/{id:guid}/activate` | `POST …/{id}/activate` | `activate` | `[ValidateAntiForgeryToken]` |
  | `GET api/countries` | `GET /api/lookups/reference/countries` | `create` ∨ `update` | `LegalEntitiesController.cs:118-123` emsali; mevcut `/api/lookups/{everything}` rotası — Gateway değişmez |

  `DELETE`, `…/roles…` vekil eylemi **yok**. Gövdede `tenantId` → 400 (taban `ProxyJsonAsync` davranışı; cümle K-5). JWT ve istek / yanıt gövdesi **loglanmaz**
  (taban yalnız yöntem + yol loglar).
- `WEB/Models/MasterData/BusinessPartnerViewModels.cs` — vekil / görünüm modelleri (`TenantId` alanı yok).
- `WEB/wwwroot/assets/js/MasterData/BusinessPartners/index.js` (`const endpoint = '/MasterData/BusinessPartners/api'`, `export: { mode: 'server', url }`),
  `index.l10n.js`, `form.js` (ülke listesi yalnız vekilden; uç düşerse alan hata durumu; **sabit ülke dizisi yok**).
- `WEB/Resources/Views/MasterData/BusinessPartners/BusinessPartnersIndex.{en,tr,fr,es,zh,ar,ru}.resx` — ekran metinleri + §5.3 + dışa aktarım sözcükleri (§5.2).
- `WEBT/MasterData/BusinessPartnersViewTests.cs` — Layout / dosya seti / UAS-001 / L10n paritesi / yardım / JS taraması / vekil yansıması.
- `WEB/tests/business-partners-status-reason.test.js` (K-7) — vitest: Pasifleştir / Etkinleştir modalı gerekçe ister (10..500), `data-dt-primary="chord"`.
- `MDMT/Diten.MdmService.Api.Tests/BusinessPartnerExportWordsGuardTests.cs` (K-8) — AC-27 (a) ve (b).

### 5.2 Dışa aktarım sözcükleri (AC-27 (a) — ekran resx'inde aynı anahtar, aynı değer)
MDM `Resources/BusinessPartnerExport*.resx` anahtarları (DL-1, ölçüldü): `Code`, `LegalName`, `ShortName`, `CountryCode`, `City`, `Roles`, `Status`, `UpdatedAt`,
`Status_Active`, `Status_Inactive`, `Role_ThirdPartyLogistics` (+ `Title`). `BusinessPartnersIndex.{lang}.resx` bu 11 anahtarı **birebir aynı değerle** taşır
(Brand emsali `BrandExportWordsGuardTests.cs`). MDM resx'i bu dilimde değişmez; ekran metni MDM'e uyar. Bir değer ekranda kötü duruyorsa MDM resx değişikliği
gerekir → dur, CT'ye yaz (MDM üretim dosyası).

### 5.3 Hata kodu metinleri — `Error.{KOD}` (K-1, K-2), 7 dilde, boş değil, `en` dışında `en`'den farklı
| # | Kod | HTTP | Ekranın söylediği (özet; metni l10n yazar) |
|---|---|---|---|
| 1 | `MDM_BP_NOT_FOUND` | 404 | kayıt bulunamadı (yok / başka kiracı — aynı cümle) |
| 2 | `MDM_BP_SHORT_NAME_DUPLICATE` | 409 | bu kısa ad kullanılıyor — formda Kısa ad alan hatası |
| 3 | `MDM_BP_TAX_NUMBER_DUPLICATE` | 409 | bu ülke + vergi no kayıtlı — Vergi no alan hatası |
| 4 | `MDM_BP_EXPECTED_VERSION_REQUIRED` | 400 | sayfayı yenileyin |
| 5 | `MDM_BP_VERSION_CONFLICT` | 409 | veri değişti, yeniden yükleyin (§13) |
| 6 | `MDM_BP_INACTIVE` | 409 | pasif kayıt salt okunur; önce etkinleştirin (K-10) |
| 7 | `MDM_BP_ALREADY_ACTIVE` | 409 | zaten etkin |
| 8 | `MDM_BP_ALREADY_INACTIVE` | 409 | zaten pasif |
| 9 | `MDM_BP_REASON_REQUIRED` | 400 | gerekçe 10–500 karakter |
| 10 | `MDM_BP_ROLE_UNKNOWN` | 400 | tanımsız rol |
| 11 | `MDM_BP_STATUS_UNKNOWN` | 400 | tanımsız durum |
| 12 | `MDM_BP_ROLES_REQUIRED` | 400 | en az bir rol seçin |
| 13 | `MDM_BP_CONTACT_INVALID` | 400 | kişi kuralı (ad 2–120; e-posta ya da telefon; en çok 10 kişi) |
| 14 | `MDM_BP_RECORD_CODE_UNAVAILABLE` | 503 | kod servisine ulaşılamadı, kayıt oluşmadı (S-15 adımında görünür) |
| 15 | `RECORD_CODE_PERMISSION_MISSING` | 403 | kod alma izni yok |
| 16 | `MDM_BP_VALIDATION_FAILED` | 400 | girilen değerleri kontrol edin |
| 17 | `EXPORT_TOO_LARGE` | 413 | dışa aktarım 50 000 satırı aşıyor; süzgeci daraltın |
| 18 | `MDM_BP_STORE_UNAVAILABLE` | 503 | veri deposuna şu an yazılamıyor, tekrar deneyin |
| 19 | `MDM_BP_UNEXPECTED_ERROR` | 500 | beklenmeyen hata |

Ek olarak genel cümle (`ErrorOccurred` — bilinmeyen kod; `diten-refusal.js` sözleşmesi) ve Web'in kendi retleri (K-5: izin yok, Gateway yok, `tenantId` reddi) 7 dilde.
`index.l10n.js` / `_IndexL10n` bu 19 anahtarı `window.L10n`'e taşır ve `diten-refusal.js`'e kod → anahtar haritasını verir (`{ MDM_BP_INACTIVE: 'Error.MDM_BP_INACTIVE', … }`);
harita 19 kodun hepsini kapsar (Web testi — S-WP2).

### 5.4 Ortak dosyalar ve XMC-001 (yalnız adlı satırlar)
| # | Dosya | DL-2'de | Ölçüm (HEAD `60379b0c1`) | Sahip / onay |
|---|---|---|---|---|
| X-9a | `WEB/Resources/SharedResource.{7}.resx` — `Nav.Module.BUSINESSPARTNERMASTER`, `Nav.Page.BUSINESSPARTNERS`, `Perm.Entity.BUSINESSPARTNERS` | **değişiklik yok** (ölç; eksik dil varsa dur, CT'ye yaz) | 7 dilin her birinde 3/3 (`916f207a8`, `en.resx:1362-1369`) | CT onayı 2026-11-14 (DL-1'de kullanıldı) |
| X-9b | `WEB/Resources/PageHelpResource.{7}.resx` — `Help.BUSINESSPARTNERS.Purpose`, `.Audience`, `.Step1…Step4`, `.Step1Title…Step4Title`, `.Immediate` | **yalnız ekleme**, 7 dil (§25 metinleri) | 0 anahtar | **açık — B-1** |
| X-9c | `WEB/Services/Help/PageHelpCatalog.cs` | **değişiklik yok** (`Related.TPLHEALTH` MOD-0179 ekranı gelince) | `TPLHEALTH` depoda yok | — |
| Rol köprüsü | `WEB/Views/Governance/RoleAssignments/_IndexL10n.cshtml`, `WEB/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.{7}.resx`, `WEB/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js` | **dokunulmaz** — yeni izin fiili yok | altı fiilin hepsi köprüde: `read` `:165`, `create` `:167`, `update` `:168`, `export` `:179`, `activate` `:188`, `deactivate` `:243`; DL-1 vitest label-ledger 28/28 | (yeni fiil çıkarsa dur — XMC-001, Beste) |
| Nav bekçisi | `WEBT/Navigation/NavManifestL10nGuardTests.cs` | **dokunulmaz** — `PagelessUntil` girdisi gerekmez (DL-1 manifesti sayfayı zaten bildiriyor: `BusinessPartnerMasterManifestProvider.cs:34-42`, `BUSINESSPARTNERS`, `IsNavigationVisible: true`); `NavVisiblePageKeyFloor` bir **taban**dır (`>=`), yeni sayfa artırmayı gerektirmez | floor 45; 4 girdi, `business-partner-master` yok | — |
| Vekil tabanı | `WEB/Controllers/MasterData/MasterDataGatewayController.cs` | **dokunulmaz** (K-6) | `Accept-Language` yalnız `configure` ile | MOD-0290-FU02 (değişiklik gerekirse dur) |
| — | `WEB/Program.cs`, `_LayoutTenantShell.cshtml`, `_Layout.cshtml`, `Views/Archive/**`, `tests/ui-compliance-baseline.tsv` | **dokunulmaz** (yeni sayfa tabana satır eklemeden 0 sapmayla geçer) | — | — |

**Dokunulmayacaklar (ayrıca):** paket dosyası; iki sözleşme; `services/Diten.MdmService/src/**` (DL-2'de MDM üretim değişikliği yok); `MDMT/**`'te K-8 dosyası dışında her
şey (mevcut MDM test dosyaları satır değişmeden); `services/Diten.Platform/**`; `services/Diten.AuthService/**` (X-10); `services/Diten.Building.Blocks/**`;
`gateway/**` (rota `28e2e9f2e`'de; test DL-3); `.antigravity/**`; `execution/registries/**`; bu WP dosyası.

## 6. Uygulama sırası (öneri)

- **A.** Adım 0 → §4.1 → taban koşuları (§8).
- **B.** Vekil controller + görünüm modelleri (K-5, K-6); vekil yansıma testleri.
- **C.** Görünümler (Compact) → JS → ekran resx (7 dil; §5.2 + §5.3) → `BusinessPartnerExportWordsGuardTests` (K-8).
- **D.** Yardım anahtarları (B-1 onayıyla, aynı commit'te yardım testi).
- **E.** Testler, sabotajlar (§7), tam koşular (§8), `verify_datatable_page.py` / `check_pack_nav_keys.py` / `resx-check` / `uic_changed_pages.py`.
- **F.** Tarayıcı smoke (AC-24) ve AC-15 canlı kanıt (§7.2) — A-2 / A-3 ya da elle izin sonrası; değilse CT turuna devret.

## 7. Kabul ölçütleri (DL-2: AC-17…AC-24, AC-27 + DL-1'den taşınan AC-15 + CT ekleri)

Ayrıntı paket §16'dadır; her vaka paketteki metinle ölçülür (özet yetmez — aç).

| AC | Kısa başlık | Sabotajlar |
|---|---|---|
| AC-17 | Index / Create / Edit / Details'ta `Layout = "_LayoutTenantShell";` açıkça; partial'larda `Layout =` yok | S-17 |
| AC-18 | Compact dosya seti tam; yasak dosyalar yok; `verify_datatable_page.py` (K-9 komutu) PASS + `.antigravity/workflows/quality-gate-datatable.md` PASS | S-18a, S-18b |
| AC-19 | 7 dil parite (ekran resx + Nav / Perm.Entity + rol etiketi "3PL ortağı" + durumlar + kişi türleri `Integration`/`Operations`/`Finance`/`Other` + 19 `Error.{KOD}`); sabit kullanıcı metni yok; `check_pack_nav_keys.py` PASS; vitest label-ledger yeşil (yeni fiil yok) | S-19 |
| AC-20 | UAS-001 (izin denetimi filtre / başlıktan önce; 403; yönlendirme yok); düğmeler izinle; Düzenle yalnız `Active`; Roller sekmesi salt okunur, rol ver / kaldır yok (C-5) | S-20a, S-20b, S-20c |
| AC-21 | Formda `Code` girdisi yok; ülke listesi yalnız vekilden, uç düşerse alan hata durumu, sabit liste yok; maskeli vergi no "maskeli" rozeti | S-21a, S-21b |
| AC-22 | Pasifleştir / Etkinleştir Premium SweetAlert2 (MOD-0013) gerekçe 10..500 (istemci + sunucu), hata kodları 7 dilde, birincil düğme `data-dt-primary="chord"` (vitest BL-586 `diten-enter-primary` yeşil); `window.confirm` yok | S-22 |
| AC-23 | `Help.BUSINESSPARTNERS.*` (Purpose, Audience, Step1…4 + Title, Immediate) 7 dilde; tur yok (B-1) | S-23 |
| AC-24 | Tarayıcı yalnız same-origin vekile gider (5059 / 5000 yok); her yazma `[ValidateAntiForgeryToken]`; `Accept-Language` iletilir; tarayıcı smoke E3: oluştur → ayrıntı → düzenle → pasifleştir (Düzenle kaybolur) → etkinleştir, tr ve en | S-24, **S-WP1**, **S-WP3** |
| AC-27 | (a) 11 dışa aktarım sözcüğü 7 dilde ekran resx'iyle birebir, `Title` dolu ve `en` dışında farklı; (b) 19 `BusinessPartnerErrorCodes` sabitinin her biri için `Error.{KOD}` 7 Web resx'te, boş değil (yansıma) | S-27a, S-27b |
| AC-15 (DL-1'den) | Canlı kanıt, kullanıcı yolu (K-3) — §7.2 | S-15 |
| CT-1 (bu WP) | Kod → anahtar haritası 19 kodu kapsar; bilinmeyen kodda genel cümle, MDM'in İngilizce `errors` metni ekrana çıkmaz | **S-WP2** |

### 7.1 Sabotaj tanımları
Paketteki tanımlar aynen: S-17, S-18a, S-18b, S-19, S-20a, S-20b, S-20c, S-21a, S-21b, S-22, S-23, S-24, S-27a, S-27b (§16 metni). Okuma notları:
- **S-27b** `Error.MDM_BP_INACTIVE` biçimiyle okunur (K-1): `BusinessPartnersIndex.ar.resx`'ten `Error.MDM_BP_INACTIVE`'i sil → AC-27 (b) kırmızı.
- **S-22**'nin hedefi K-7 dosyasıdır.
- **S-18b**: `_DataTable.cshtml`'de `data-dt-data-mode="client"` → K-9 komutu kırmızı (pakette `--data-mode server` verildiği için "both present and different" FAIL).

**CT ekleri (paket dışı; bu WP):**
- **S-WP1:** `BusinessPartnersController`'da `Accept-Language` iletimini kaldır → vekil testi (Gateway'e giden istekte başlık iddiası, sahte `HttpMessageHandler`) kırmızı.
- **S-WP2:** `index.l10n.js` / `_IndexL10n` haritasından `MDM_BP_VERSION_CONFLICT` girdisini sil → harita kapsama testi kırmızı.
- **S-WP3:** `api/{id}/deactivate` eyleminden `[ValidateAntiForgeryToken]`'ı kaldır → yazma eylemleri yansıma testi kırmızı.

**Sabotaj listesi (tam):** S-17, S-18a, S-18b, S-19, S-20a, S-20b, S-20c, S-21a, S-21b, S-22, S-23, S-24, S-27a, S-27b (**14 paket DL-2**) + S-15 (**1, DL-1'den
taşındı**) + S-WP1, S-WP2, S-WP3 (**3 CT eki**) = **18**.

Kurallar: her sabotaj **üretim koduna** uygulanır (kendi kopyasıyla ölçen test guard değildir — Çekirdek md. 10); kırmızı çıktı alınır, bozma bayt-eşit geri alınır,
**commit'e girmez**, `git status` temiz; listede olmayan her kırmızı adıyla raporlanır. Test adlarını geliştirici koyar ve raporda her `S-n` ile eşler.

### 7.2 AC-15 + S-15 canlı kanıt (K-3)
**Önkoşullar (raporda tek tek "var / yok"):** A-5 dev kimlik bilgisi (Platform `TrustedRecordCodeCredential:Mdm`, `AllowedRecordTypes` `BPT` dahil; MDM
`RecordCodeAllocator:*`) ortam değişkeniyle **bu PC'de** ayarlı (CT kaydı 2026-11-14 08:25–08:28) — sır repoya, kayda, sohbete **yazılmaz**; kiracı grup öneki
hizalı (`GMG`); rota `28e2e9f2e` çalışan Gateway'de; DL-2 kodu çalışan Web'de; `mdm.business-partners.*` izinli bir test kullanıcısı (A-2 / A-3 ya da elle — §1.2) ve
ikinci kiracıda bir kullanıcı.

**Adımlar (Web 5001 üzerinden, tr):** (1) oluştur → kod `^GMG-BPT-\d{6,}$`; (2) liste `role=ThirdPartyLogistics&status=Active` yeni kaydı döndürür; (3) ikinci
kiracının kullanıcısı aynı `id`'nin Ayrıntı sayfasını açar → `Error.MDM_BP_NOT_FOUND` (vekil 404); (4) **S-15 / negatif adım (zorunlu):** Platform kod servisi
durdurulur → oluştur → vekil 503 `MDM_BP_RECORD_CODE_UNAVAILABLE`, ekranda `Error.MDM_BP_RECORD_CODE_UNAVAILABLE`, MDM'de kayıt ve geçmiş satırı **yok**; Platform
yeniden başlatılır, bir oluşturma daha 201 (servis geri geldi). 201 dönerse kanıt kırmızıdır.
- Platform'u durdurmak bu PC'deki diğer oturumları etkiler (ana checkout'ta `dotnet watch`): durdurmadan önce CT'ye haber ver; adım bitince yeniden başlat ve
  `/health`'i raporla.
- **Kayıt:** `scripts/run_record.py run --out docs/records/audits/2026-11/<yeni-dizin> -- <komut>` — temiz çalışma ağacında, salt okunur bir doğrulama komutuyla
  (ör. adım 4 sonrası "kayıt yok" sayımı); tarayıcı adımlarının vekil yanıtları (durum + `errorCodes`) zaman damgasıyla kabul kaydına yazılır. Çıktıda jeton /
  sır varsa commit'ten önce temizlenir. Hangi komutun koşulacağı **§12 B-6** (CT onayı).

## 8. Test koşuları (tam koşu filtresiz)

- **MDM App + MDM Api, filtre yok:** `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`; `dotnet test services/Diten.MdmService/tests/Diten.MdmService.Application.Tests`
  ve `…/Diten.MdmService.Api.Tests`. 27017 **kullanılmaz**. MDM App tabanında ~72–125 ortam kırmızısı (DL-1: 72 / 3056, tabanın 125'inin alt kümesi) — dilim başı
  koşusuyla **adıyla** karşılaştırılır; **yeni kırmızı 0**. MDM Api DL-1'de 0 / 1268; sonda yalnız K-8 vakaları eklenmiş olmalı.
- **Web.Tests, filtre yok:** `dotnet test frontend/Diten.Web.Tests`. Taban DL-1'de 3 / 4554 (üçü main'de de kırmızı); dilim başında yeniden ölçülür.
  `NavManifestL10nGuardTests` yeşil kalmalı.
- **Gateway, filtre yok:** `dotnet test gateway/Diten.ApiGateway.Tests` (taban 384 / 384, `28e2e9f2e`); DL-2 Gateway'e dokunmaz — önce = sonra.
- **Platform:** DL-2 Platform'a dokunmaz → tam Platform koşusu **gerekmez**. Bir Platform dosyası değişirse (beklenmez) dur, CT'ye yaz; değişirse bütün Platform
  test paketi filtresiz koşulur.
- **vitest — yalnız repodaki `node_modules`** (`frontend/Diten.Web/node_modules/.bin/vitest(.cmd)`; `npx`'in npm'den indirmesine izin verme — o koşu sayılmaz;
  ayrık worktree'de kayıtlı junction yöntemi): `tests/role-assignments-label-ledger.test.js` (AC-19), `tests/diten-enter-primary.test.js` (AC-22, BL-586),
  `tests/business-partners-status-reason.test.js` (K-7), `tests/dialog-one-implementation.test.js` (Windows'ta tabanla karşılaştır — yol kırmızıları main'de de var).
- **Araçlar:** `verify_datatable_page.py` (K-9 komutu, `--format gaps` → 0 sapma); `PYTHONUTF8=1 python scripts/check_pack_nav_keys.py` (0 new);
  `resx-check` (SharedResource, kiracı bağlamı — değişmediğini teyit); `PYTHONUTF8=1 PYTHONIOENCODING=utf-8 python scripts/uic_changed_pages.py --base <dilim-başı>`
  (CI ui-compliance; Windows tuhaflıkları kayıtlı — LF taban kopyası, ayrık worktree) → yeni sayfa için **YENİ KIRMIZI 0**.
- **Bekçi kanıtı:** `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.MdmService` yalnız `MDMT/Diten.MdmService.Api.Tests/BusinessPartnerExportWordsGuardTests.cs`
  (yeni) gösterir; `git diff <dilim-başı>..<dilim-sonu> -- frontend/Diten.Web.Tests/Navigation gateway services/Diten.Platform` **boş**.
- **Build:** `Diten.Web`, `Diten.MdmService.sln` PASS.

**Ortam:** Port **27017 kullanma**. **`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşma.** Portlar yalnız AGENTS.md §3'ten (Web 5001, Gateway 5000, MDM 5059);
port uydurma. Ana checkout'ta servisler `dotnet watch` ile çalışıyorsa derleme / test / sabotaj ayrık worktree'de yapılır — commit yine çalışma dalına.
**Eşzamanlılık:** MOD-0182 DL-2 aynı anda `PageHelpResource.*`, `SharedResource.*` ve `NavManifestL10nGuardTests` (`PagelessUntil["BARCODE-RFID"]` silmesi) dosyalarına
dokunuyor. Her commit'ten hemen önce hattı al (Adım 0 kuralı); çakışmada `--abort`, dur, CT'ye yaz; birleşince Web.Tests'i yeniden koş.

## 9. Git kuralları (GIT-002)

- Çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`** — yeni dal açılmaz; `main`'e commit / push yok.
- Stage yalnız **açık dosya yoluyla** (`git add <yol> …`); `git add -A` / `git add .` yok. İzlenmeyen `__pycache__/` ve `docs/records/reviews/MOD-0178/*-raw-*` dosyaları
  stage'e girmez.
- Hook atlama yok (`--no-verify`, `core.hooksPath` oynama yok), imza atlama yok; yıkıcı git komutu (`reset --hard`, `push --force`, `checkout --`) onaysız yok.
- Yerel commit'ler serbest; yardım anahtarları yardım testiyle, ekran resx'i K-8 bekçisiyle **aynı commit'te** (biri gelip öbürü gelmezse MDM Api / Web.Tests kırmızı).
- **Push / PR yok.** Commit mesajı sonu: `Co-Authored-By` satırı oturumun atıf kuralıyla.

## 10. Teslim raporu biçimi

1. **Commit listesi** (`git log --oneline <dilim-başı>..HEAD`), `git diff --stat`, Adım 0'da hangi yol (ata / ff / no-ff).
2. **Test sayıları önce / sonra:** MDM App, MDM Api, Web.Tests, Gateway (toplam / geçen / kalan / atlanan; kırmızılar adıyla, tabanla ad ad karşılaştırma), vitest (dört dosya).
3. **AC tablosu:** AC-15, AC-17…AC-24, AC-27, CT-1 — her biri için test adı ve sonuç.
4. **Sabotaj tablosu (18):** her `S-n` için üretim dosyası:satır, değişiklik, kırmızı test adı + çıktı özeti, bayt-eşit geri alma, geri yeşil kanıtı.
5. **Araç çıktıları:** `verify_datatable_page.py` (K-9 komutu, tam çıktı), `check_pack_nav_keys.py`, `resx-check`, `uic_changed_pages.py`.
6. **Ölçümler önce → sonra:** `NavVisiblePageKeyFloor`, `PagelessUntil` (değişmedi), `Help.BUSINESSPARTNERS` sayısı (0 → 7 dilde N), SharedResource 3 anahtar (değişmedi).
7. **AC-15 kaydı:** önkoşullar var / yok, adım adım vekil yanıtları, S-15 negatif adımı, `run_record` dizini ve doğrulaması (`run_record.py verify`).
8. **Tarayıcı smoke (AC-24):** tr ve en; izin elle mi verildi, A-2 / A-3 ile mi.
9. **XMC-001:** B-1 onay kaydı (commit) ya da "onay yok — dokunulmadı"; B-2 kararı; §5.4 "dokunulmaz" dosyalarının boş diff'i.
10. **Açık kalanlar:** sorular, ölçülemeyenler (nedeniyle), CT turuna devredilenler; A-2 / A-3 durumu.

## 11. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001).
- **Frontend:** `frontend-datatable-template`, `.antigravity/workflows/quality-gate-datatable.md`, `unauthorized-surface-standard` (UAS-001), `page-help-and-keyboard`
  (UXH-001), `_LayoutTenantShell` kabuk kuralları, same-origin vekil deseni (`MasterDataGatewayController`), Golden Compact.
- **Metin:** `localization-standard` (kiracı 7 dil, marker sınıfı yolu, RTL mantıksal CSS), `dynamic-localization-standard` (`window.L10n` köprüsü).
- **Yetki / manifest:** `permission-key-standard` (yeni anahtar ve fiil yok), `module-self-registration-standard` (manifest değişmez).
- **Kayıt / kanıt:** `logging-observability` (jeton / gövde loglanmaz), `status-reporting-and-evidence` (REP-001), `dev-runbook`.

## 12. Belirsizlikler (CT'ye; geliştirici kendi başına çözmez)

- **B-1 (açık — Beste / CT):** X-9b `PageHelpResource.*` `Help.BUSINESSPARTNERS.*` eklemeli onayı (§1.2). Gelmeden yardım dosyalarına dokunulmaz; diğer işler başlar.
- **B-2 (açık — Beste / CT):** ekran brief / mockup ya da muafiyet kararı (paket §24, merkez kuralı).
- **B-3 (CT kararı, K-7 / K-8):** iki test dosyası yeri (vitest dosyası Repo Scope eki; AC-27 (b) MDM bekçi dosyasında). Paket sonraki revizyonda hizalanır.
- **B-4 (CT kararı, K-9):** paket `--data-mode` yazımı hatası; doğru komut §4.3. Paket sonraki revizyonda hizalanır.
- **B-5 (Ali):** A-2 / A-3 açılmadan gerçek kullanıcıyla tarayıcı turu ve AC-15 koşulamaz; dev'de izni elle verme yolu yoksa ikisi CT canlı turuna devredilir ve
  DL-1 + DL-2 ACCEPTED A-2 / A-3'ü bekler.
- **B-6 (CT onayı):** AC-15 `run_record.py` komutu (salt okunur doğrulama — ör. MDM dev veritabanında `mdm_business_partners` ve `mdm_business_partner_history`
  sayımı ya da yeni bir kanıt betiği). Yeni betik repoya eklenecekse Repo Scope dışıdır → CT onayı ve yeri (`docs-organization`) gerekir.
- **B-7 (ölçülmedi):** `MDM_BP_STORE_UNAVAILABLE` / `MDM_BP_UNEXPECTED_ERROR` paket §8.5'te yok (CT kararı (2): paket sonraki revizyonda); metinleri bu dilimde yazılır.

## Ek — B-1 ve B-2 kapandı (CT, 2026-11-14)

Sahip (Beste): X-9b `Help.BUSINESSPARTNERS.*` yalnız ekleme onaylı; ekran brief / mockup şartından muaf (Compact datatable kalıbı). Kayıt:
`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md` "Ek — MOD-0049-FU01 DL-2 ve MOD-0178-FU01 DL-3 kapıları". DL-2
başlamak için bekleyen kapı kalmadı; A-2 / A-3 kabul kapısıdır. Adım 0 etiketi: `ct/line-ff-2026-11-14e`.
