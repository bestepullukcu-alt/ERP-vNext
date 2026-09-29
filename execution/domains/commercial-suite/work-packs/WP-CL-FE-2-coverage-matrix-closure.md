# WORK PACKAGE — WP-CL-FE-2 · Kapsama matrisi + ülke kapatma / yeniden açma + görünen adlar (ülke / dil) + boş durum ikonu (frontend)

> **CT (SoR).** İddialar v2 Faz 3, 2. tur. **FE-4 ile paralel** çalışır. Kaynaklar:
> - Mockup senaryo **2** (iddia × ülke kapsama matrisi)
> - Kararlar: D1 ülke kısıtı YOK; D3 kapatma nedeni **tek seçimli select2**, ülke ekseni `COUNTRY_CODES`
> - FE-1 / FE-3 canlı kontrol bulguları #1–#3
>
> **Çalışma yeri:** worktree `C:\tmp\cl-fe-2`, dal `wp/cl-fe-2`. Taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
>
> **FE-4 ile çakışmayı önlemek için:**
> - `form.js`, `ClaimsForm` resx ve FE-4'ün yeni dosyalarına DOKUNMA.
> - `ClaimsController.cs`'e yalnız **yeni bir action** (Coverage sayfası) ekle.

## Mockup
`mockups/claims-v2/`: senaryo `matrix`.
- **Başlık hücresi:** ülke adı · diller (`{mc.cn} · {mc.langs}`) + özet (`{ok} · {warn}`).
- **Satır:** kod, tür, ad; alt satırda "ürün · N kanıt · N kullanım".
- **Hücre:** durum rozeti (`ST` renkleri) + sürüm + alt not. Hücre `none` iken **"Ülke sürümü aç"** eylemi.

## Kanıt (CT)
- **CRM:**
  - `GET claims/coverage?productId&kind&status&search` → `{countries[{countryCode, displayName}], rows[{claimId, claimCode, claimName, kind, …, cells[{countryCode, state, versionId, version, boundCoreVersion, closureReasonCode, isExpiring}]}], expiringWindowDays}`.
  - `state` ∈ approved / in-review / draft / review-required / closed / not-opened / not-applicable.
  - Kapatma: `POST claims/{id}/country-closures {countryCode, reasonCode}` (409 `country_has_version`, 400 `not_applicable`).
  - Yeniden açma: `POST claims/{id}/country-closures/{cc}/reopen {note?}`.
- **Web (FE-1):**
  - `/CRM/Claims/api/v2/claims/coverage`, `…/country-closures`, `…/reopen` proxy'leri.
  - `lookups/countries` → `{code, name, languages[]}`. **`name` = kod, diller kod** (canlı bulgu #2 / #3).
  - `lookups/closure-reasons` → `name` = kod. Etiket resx'ten, value_code anahtarıyla.
- **Canlı bulgu #1:** liste boş durum ikonu `bx bx-library` bu Boxicons sürümünde yok, kırık glif görünüyor.
- **Liste sayfası:** `Views/CRM/Claims/Index.cshtml` + `index.js` (FE-1).

## NE
1. **Görünen adlar (lookup; FE-4 de kullanır):** `lookups/countries` yanıtı genişler: `{code, name, nativeName, languages:[{code, name, nativeName}]}`.
   - `name`: istek UI kültüründe ülke adı (`RegionInfo(code)` üzerinden; bilinmeyen kod → kod).
   - `nativeName`: ülke kendi dilinde.
   - Dil `name`: UI kültüründe (`CultureInfo(code).DisplayName`); dil `nativeName`: kendi dilinde (ör. "Oʻzbekcha", "Русский").
   - **Geriye uyum:** eski `languages[]` string dizisini okuyan yer varsa (FE-3 `form.js`) → bu pakette **yalnız ekleme**. `languages` string dizisi kalsın, yeni alan `languageDetails` olsun. FE-4 yeni alanı kullanır.
   - Ülke ve dil seçicilerinde görünen metin: **"Türkiye (TR)"**, **"Oʻzbekcha (uz)"**.
2. **Boş durum ikonu:** mevcut Boxicons setinde olan bir ikon (ör. `bx-book-open` / `bx-collection`). Hangi ikonun var olduğunu CSS'ten doğrula.
3. **Kapsama matrisi sayfası** `/CRM/Claims/Coverage` (`crm.claim.read`; yetkisizde iskelet YOK):
   - Liste ve matris sayfalarının ikisinde de **"Liste | Kapsama matrisi"** sekme gezintisi.
   - **Filtreler:** ürün, tür, durum, arama.
   - **Sütunlar:** `COUNTRY_CODES` sırası. Başlıkta ülke adı + diller + özet (onaylı sayısı · uyarı sayısı).
   - **Hücre:**
     - durum rozeti (mockup `ST` renkleri, tema değişkenleri) + `v{sürüm}`;
     - alt not: kapalıda **"Açılmadı · {neden etiketi}"**; süresi doluyorsa uyarı; review-required'da "çekirdek değişti / kanıt değişti" kısa notu.
   - **Hücre eylemleri** (`crm.claim.manage`):
     - `not-opened` → menü: **"Ülke sürümü aç"** (FE-4 rotası `/CRM/Claims/{claimId}/Countries/{cc}/Create`) · **"Açılmayacak olarak işaretle"** → modal: **tek seçimli select2 neden** (zorunlu) + onay → `country-closures`.
     - `closed` → **"Yeniden aç"** (opsiyonel not) → reopen.
     - Sürümü olan hücre → **"Düzenle / görüntüle"** (FE-4 rotası `/CRM/Claims/CountryVersions/{versionId}/Edit`).
     - `not-applicable` → eylem yok.
     - Yalnız `core` iddia için "aç" var. Çekirdek onaylı değilse "aç" pasif ve tooltip "Önce çekirdek onaylanmalı".
   - Hata kodları kullanıcı dilinde: `country_has_version`, `not_applicable`, `reference_set_missing`.
   - Satır kodu tıklanınca FE-1 hızlı görünümü (yeniden kullan) ya da Düzenle.
   - Boş durum: matris için "Henüz iddia yok".
   - Açık ve koyu tema. Yatay kaydırma. Ülke sütunları çoğalınca başlık yapışkan (sticky).
4. **L10n:** `ClaimsIndex` resx 7 dil (matris anahtarları + kapatma nedeni etiketleri value_code ile).

## KORU / YAPMA
- **`form.js`, `ClaimsForm` resx, FE-4 dosyaları DOKUNMA.** FE-1 proxy yolları DEĞİŞMEZ (lookup yanıtına yalnız ekleme).
- CRM, Platform, Auth DOKUNMA.
- **Ülke kısıtı YOK.** Ruhsat no ve sahibi YOK.
- **Tarayıcı:** ayrı sekme, canlıda yazma YOK (E4 CT yapar).
- **DUR:** `RegionInfo` / `CultureInfo` sunucuda bir kodu tanımıyorsa (ör. `XK`) → kodu göster ve raporla.

## Acceptance
- **E2:**
  - Web testleri 0 kırmızı (taban 270).
  - Web build 0 hata.
  - JS sözdizimi temiz.
  - Yeni testler:
    - lookup görünen adları (TR → Türkiye / Turkey kültüre göre; uz → Oʻzbekcha);
    - geriye uyum (`languages` string dizisi duruyor);
    - Coverage sayfası izin kapısı;
    - L10n 7 dil eşliği.
- **E4:** CT, birleştirme + fleet restart sonrası yapar.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CL-FE-2 · Kapsama matrisi + ülke kapatma/yeniden açma + görünen adlar (ülke/dil) + boş durum ikonu (frontend)
Repository: C:\tmp\cl-fe-2 (worktree) · Branch: wp/cl-fe-2 · commit bu dala, push YOK · FE-4 PARALEL: form.js / ClaimsForm resx / FE-4 dosyaları DOKUNMA; ClaimsController.cs'e yalnız yeni action

Amaç: Mockup senaryo 2 kapsama matrisi (iddia × COUNTRY_CODES), hücreden "Ülke sürümü aç" (FE-4 rotası) / "Açılmayacak olarak işaretle" (tek seçimli select2 neden) / "Yeniden aç"; lookups/countries görünen adları (ülke + dil, UI kültüründe + native); liste boş durum ikonu düzeltmesi.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-FE-2-coverage-matrix-closure.md · mockups/claims-v2/ (senaryo matrix; logic-and-sample-data.decoded.js ST/CC/CN/CL) · …/WP-CL-FE-1-claims-list-quickview-proxy.md + …/WP-CL-FE-3-core-claim-form-evidence-submit.md (§37-E4 bulguları #1–#3) · frontend/Diten.Web/Controllers/CRM/{ClaimsController.cs, ClaimsController.V2.cs} (coverage/closure proxy, lookups/countries) · Views/CRM/Claims/Index.cshtml + wwwroot/assets/js/CRM/Claims/index.js · services/Diten.CrmService/src/**/Claims/ClaimDtos.cs (ClaimCoverage*) · memory l10n-bridge-pascalcase-loader, dt-inline-filter-host-class.

NE:
 1) lookups/countries yanıtına EKLE: name (UI kültüründe RegionInfo), nativeName, languageDetails[{code, name (CultureInfo.DisplayName), nativeName}]; eski languages string dizisi KALSIN (geriye uyum); seçici metni "Türkiye (TR)" / "Oʻzbekcha (uz)".
 2) Boş durum ikonu: Boxicons setinde olan ikon (bx-book-open/bx-collection; CSS'ten doğrula).
 3) /CRM/Claims/Coverage (crm.claim.read, yetkisizde iskelet YOK): "Liste | Kapsama matrisi" sekme gezintisi (iki sayfada); filtreler ürün/tür/durum/arama; sütunlar COUNTRY_CODES sırası, başlıkta ad + diller + özet; hücre durum rozeti (ST renkleri → tema değişkenleri) + v{sürüm} + alt not (kapalı "Açılmadı · {neden etiketi}", süresi doluyor, review-required notu); hücre eylemleri (crm.claim.manage): not-opened → "Ülke sürümü aç" (/CRM/Claims/{claimId}/Countries/{cc}/Create; çekirdek onaysızsa pasif + tooltip) + "Açılmayacak olarak işaretle" (modal, tek seçimli select2 neden zorunlu → country-closures); closed → "Yeniden aç" (not ops.); sürümlü hücre → /CRM/Claims/CountryVersions/{versionId}/Edit; not-applicable eylemsiz; hata kodları kullanıcı dilinde (country_has_version, not_applicable, reference_set_missing); satır kodu → FE-1 hızlı görünüm; boş durum; açık/koyu tema; yatay kaydırma + yapışkan başlık.
 4) ClaimsIndex resx 7 dil (matris anahtarları + neden etiketleri value_code ile).
KORU/YAPMA: form.js/ClaimsForm resx/FE-4 dosyaları DOKUNMA; FE-1 proxy yolları DEĞİŞMEZ (lookup'a yalnız ekleme); CRM/Platform/Auth DOKUNMA; ülke kısıtı/ruhsat no YOK; tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\cl-fe-2; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 270); Web build 0 hata; node --check temiz; yeni testler: lookup görünen adları (kültüre göre), languages geriye uyum, Coverage izin kapısı, L10n 7 dil eşliği. Commit ("feat(crm): WP-CL-FE-2 — claims coverage matrix, country closure/reopen, country & language display names" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: RegionInfo/CultureInfo bir kodu (ör. XK) tanımıyorsa kodu göster + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-09-29) — **ACCEPTED (E2)**
- **Commit:** ajan `5adde76e` → `test/crm-content-visit-e2e` üzerine rebase → **`9cced593`** (fast-forward).
- **Diff (K13 okuma):** 18 dosya, yalnız `frontend/`.
  - `ClaimsController.cs` → yalnız `Coverage` action: `RequirePage(ReadPermission)` + `CanManageClaims`.
  - `lookups/countries` → `name` (ICU, UI kültürü), `nativeName`, `languageDetails`; `languages` kod dizisi korunuyor.
  - `coverage.js` → kullanıcı verisinin tamamı `esc()`/`encodeURIComponent` ile basılıyor; FE-4 rotaları doğru.
- **CT testleri:** Web **280/0** (taban 270 + 10).
- **CT sabotajı:** `Coverage` action'ındaki `RequirePage` kaldırıldı → `Coverage_without_read_is_a_plain_403` kırmızı. Kod geri alındı.
- **Ajan bulguları (kabul edildi):**
  - `RegionInfo.DisplayName` yerelleşmiyor; ülke adı `CultureInfo("en-XX").DisplayName` içindeki bölge kısmından alınıyor.
  - Boş durum ikonunun asıl nedeni tanımsız `bx-lg` sınıfıydı → `bx-book-open` + açık boyut.
  - Özbekçe yerel adı ICU'da "O‘zbek" ("Oʻzbekcha" değil) — kabul edildi.
  - Satır kodu hızlı görünüm yerine Düzenle'ye gidiyor (hızlı görünüm index.js'e bağlı) — kabul edildi.
- **E4:** CT, fleet restart sonrası matris + kapatma / yeniden açma.
