# WORK PACKAGE — WP-CL-FIX-1 · İddialar v2 canlı test bulguları (kaynak bağlantısı · belge kodu/durumu · matris etiketi · sekme başlığı · dil etiketi · hızlı görünüm bağlantısı)

> **CT (SoR).** Kaynak: CL-E4-1 canlı uçtan uca test (2026-09-29), `WP-CL-FE-4-country-version-form.md` §37-E4 bulguları **1, 6, 7, 9, 10, 11** + FE-4'ün açık bıraktığı hızlı görünüm bağlantısı.
> - **Kapsam dışı:** Bulgu 2–5 WorkCenterNext'te; talep olarak **REQ-WCN-01**'e yazıldı, bu pakette YOK.
> - **Kapsam dışı:** Bulgu 8 (çöp org birimleri) kullanıcı Organizasyon ekranından arşivleyecek; kod YOK.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-fix-1`, dal `wp/cl-fix-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Dokunulan yerler:** Platform (EvidenceLinking okuma), CRM (DisplayContext bağlantısı, kanıt DTO'su), Web (İddialar ekranları).

## Bulgular → düzeltme

**F-1 · WCN "Kaynak kaydını aç" 404 (bulgu 1, 9)**
- CRM `ClaimReviewHandlers.cs:146` → `/CRM/Claims/Details/{id}`.
- CRM `ClaimReviewHandlers.cs:285` → `/CRM/Claims/Details/{id}?country=TR`.
- İkisi de yok (detay sayfası FE-6'da). Doğru bağlantılar:
  - çekirdek / yerel iddia → `/CRM/Claims/Edit/{claimId}` (kilitliyken salt okunur açılıyor);
  - ülke sürümü → `/CRM/Claims/CountryVersions/{versionId}/Edit`.
- Bağlantılar tek yerde üretilsin (ör. `ClaimReviewRules.SourceLink…`). FE-6 geldiğinde tek satır değişir.

**F-2 · Belge kodu ve durumu yanlış (bulgu 7)**
- Platform `EvidenceLinkingHandlers.cs:711` document-options: `code = d.CanonicalId ?? d.DocumentKey`. `CanonicalId` birden fazla belgede **aynı** değeri taşıyor (canlı: 3 ALMIBA belgesi `CAN-QMS-71626930FFC1-908994`).
- `status = d.Status` (`ControlledItemStatus.Active`) yaşam döngüsü durumu değil. Oysa belge Master Register'da **Effective**.
- İstenen:
  - **kod** = Master Register `DocumentCode`, yoksa `CanonicalId`, o da yoksa `DocumentKey`;
  - **durum** = BE-5'in `EvidenceDocumentStateResolver`'ının verdiği yaşam döngüsü durumu (effective / draft / superseded / suspended / unknown).
- Aynı `documentCode` (ve varsa `documentState`) **kanıt bağı okuma DTO'suna** eklensin: Platform `EvidenceLinkDto` → CRM kanıt DTO'su → Web.
- Web kanıt kartı `claim-evidence.js:74` şu an `documentId.slice(0, 8)` gösteriyor; **belge kodunu** göstersin, kod yoksa kimlik parçasına düşsün.
- Seçicideki ve karttaki durum **resx etiketiyle** gösterilsin: "Yürürlükte", "Taslak"… (`ClaimsForm` resx, 7 dil).

**F-3 · Matris açıklamasında iki durum aynı adla (bulgu 6)**
- `closed` ve `not-opened` durumlarının ikisi de "Açılmadı".
- `closed` → **"Kapatıldı"**, `not-opened` → "Açılmadı" kalsın.
- Hücre alt notu kapalıda "Kapatıldı · {neden}" olsun. `ClaimsIndex` resx 7 dil.

**F-4 · Tarayıcı sekme başlığı (bulgu 10)**
- `CountryVersion.cshtml` başlığı her durumda "Ülke Sürümü Aç · {kod}".
- Create'te "Ülke sürümü aç · {ülke adı}", Edit'te "Ülke sürümü · {ülke adı}". Sayfa yüklenip ülke adı çözülünce `document.title` güncellensin.

**F-5 · Dil sekmesi "Türkçe (Tr)" (bulgu 11)**
- `country-version.js` dil sekmesi etiketinde kod büyük harfle başlıyor. Kod **küçük harf** "(tr)" olsun; sayfanın geri kalanıyla aynı biçim.

**F-6 · Hızlı görünüm → ülke sürümü bağlantısı (FE-4 açık notu)**
- FE-1 liste hızlı görünümündeki ülke sürümü satırları `/CRM/Claims/CountryVersions/{versionId}/Edit`'e bağlansın (`index.js`).

## KORU / YAPMA
- **WorkCenterNext / WorkAggregation DOKUNMA** (REQ-WCN-01).
- MOD-0023, EvidenceLinking yazma kuralları, onay mantığı DEĞİŞMEZ; yalnız **okuma** DTO'larına alan eklenir (geriye uyumlu).
- Org birimi verisine DOKUNMA.
- Tarayıcıda ayrı sekme; canlıda yazma YOK (E4'ü CT yapar).
- **DUR:**
  - Master Register'da belge ↔ register eşlemesi tek anlamlı değilse (bir belgeye birden çok register satırı) → kural koyma, raporla.
  - `EvidenceDocumentStateResolver` document-options için toplu okunamıyorsa (N+1) → belge başına çağırma, toplu okuma yolunu raporla.

## Acceptance
- **E2:**
  - Platform Application testleri: yeni kalıcı kırmızı yok (taban 173 ortam kırmızısı, TRX karşılaştır).
  - CRM testleri 0 kırmızı (bilinen tek sıra flake'i hariç).
  - Web testleri 0 kırmızı (taban 302). `node --check` temiz. Build'ler 0 hata.
  - Yeni testler:
    - DisplayContext bağlantıları (çekirdek + ülke sürümü);
    - document-options kod önceliği (register kodu > CanonicalId > DocumentKey) ve yaşam döngüsü durumu;
    - kanıt DTO'sunda `documentCode`;
    - `closed` etiketi "Kapatıldı" (7 dil, ≠ not-opened);
    - L10n eşliği.
  - **Sabotaj:** en az bağlantı + kod önceliği testleri kırmızıya dönmeli.
- **E4 (CT, birleştirme + fleet restart sonrası):**
  - yeni gönderilen bir iddiada WCN bağlantısı Edit'i açıyor;
  - seçici ve kartta belge kodu ayrışık, durum "Yürürlükte";
  - matriste "Kapatıldı";
  - sekme başlığı ve "(tr)".

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-CL-FIX-1 · İddialar v2 canlı test bulguları (kaynak bağlantısı · belge kodu/durumu · matris etiketi · sekme başlığı · dil etiketi · hızlı görünüm bağlantısı)
Repository: C:\tmp\cl-fix-1 (worktree) · Branch: wp/cl-fix-1 · commit bu dala, push YOK

Amaç: CL-E4-1 canlı testinin iddialar tarafı bulgularını kapat. WorkCenterNext maddeleri (REQ-WCN-01) ve org birimi temizliği bu pakette YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-FIX-1-claims-live-findings.md · …/WP-CL-FE-4-country-version-form.md (§37-E4 1.+2. bölüm) · …/REQ-WCN-01-claims-approval-workcenter-ux.md (kapsam sınırı) · services/Diten.CrmService/src/**/Claims/ClaimReviewHandlers.cs (146, 285) · services/Diten.Platform/src/Diten.Platform.Application/Features/EvidenceLinking/EvidenceLinkingHandlers.cs (document-options ~690–750, link DTO) + EvidenceDocumentStateResolver (BE-5) · Diten.Platform.Domain/Entities/DocumentManagement/DocumentMasterRegisterEntry.cs (DocumentCode) · CRM kanıt DTO'su (ClaimEvidenceCore/Handlers) · frontend/Diten.Web/wwwroot/assets/js/CRM/Claims/{claim-evidence.js, coverage.js, country-version.js, index.js} · Views/CRM/Claims/CountryVersion.cshtml · Resources/Views/CRM/Claims/{ClaimsForm, ClaimsIndex}.*.resx.

NE:
 F-1) CRM DisplayContext kaynak bağlantısı: çekirdek/yerel → /CRM/Claims/Edit/{claimId}; ülke sürümü → /CRM/Claims/CountryVersions/{versionId}/Edit; tek yerde üret.
 F-2) Platform document-options: kod = Master Register DocumentCode ?? CanonicalId ?? DocumentKey; durum = EvidenceDocumentStateResolver yaşam döngüsü (toplu okuma). Kanıt bağı okuma DTO'suna documentCode (+documentState) ekle → CRM DTO → Web. Kanıt kartı documentId.slice(0,8) yerine kod (yoksa kimlik parçası). Seçici + kartta durum resx etiketiyle (ClaimsForm 7 dil).
 F-3) Matris: closed → "Kapatıldı" (not-opened "Açılmadı" kalır); kapalı hücre notu "Kapatıldı · {neden}"; ClaimsIndex 7 dil.
 F-4) CountryVersion sekme başlığı: Create "Ülke sürümü aç · {ülke adı}", Edit "Ülke sürümü · {ülke adı}" (document.title yüklemede güncellenir).
 F-5) country-version.js dil sekmesi etiketi kod küçük harf "(tr)".
 F-6) index.js hızlı görünüm ülke sürümü satırı → /CRM/Claims/CountryVersions/{versionId}/Edit.
KORU/YAPMA: WorkCenterNext/WorkAggregation DOKUNMA; MOD-0023 + EvidenceLinking yazma kuralları + onay mantığı DEĞİŞMEZ (yalnız okuma DTO'suna geriye uyumlu alan); org birimi verisi DOKUNMA; tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\cl-fix-1; Platform Application testleri → yeni kalıcı kırmızı yok (taban 173, TRX karşılaştır); dotnet test services/Diten.CrmService/tests/... → 0 kırmızı (bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests → 0 kırmızı (taban 302); node --check temiz; build'ler 0 hata. Yeni testler: DisplayContext bağlantıları, kod önceliği + yaşam döngüsü durumu, DTO documentCode, "Kapatıldı" 7 dil ≠ not-opened, L10n eşliği. Sabotaj: bağlantı + kod önceliği testleri kırmızıya dönmeli. Commit ("fix(crm): WP-CL-FIX-1 — claims live E2E findings (source links, document code/state, closed label, titles, quick-view link)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: register ↔ belge eşlemesi tek anlamlı değilse ya da state resolver toplu okunamıyorsa (N+1) DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-09-29) — **ACCEPTED (E2)**
- **Commit:** `3de5b538` (`wp/cl-fix-1`) → `test/crm-content-visit-e2e` fast-forward. 31 dosya.
- **Diff (K13 okuma):**
  - **F-1:** `ClaimReviewRules.ClaimSourceLink` / `CountryVersionSourceLink` tek yerde, iki çağrı yeri kullanıyor.
  - **F-2:**
    - `ControlledDocumentCode` (register kodu → CanonicalId → DocumentKey) ve `ControlledLifecycleState` BE-5 çözücüsünden statik parça olarak ayrıldı; eşleme aynı.
    - document-options register'ı sayfa başına bir kez okuyor (N+1 yok). Bir belgeye birden çok register satırı bağlıysa tahmin yok: kod geri düşüyor, durum `unknown`.
    - DTO eklemeleri isteğe bağlı parametre (geriye uyumlu).
  - WCN / WorkAggregation / org birimi dosyalarına dokunulmadı.
- **CT testleri:**
  - Platform `EvidenceLinking|SetCodeCase` **57/0**;
  - CRM **2092/0/5**;
  - Web **305/0**.
  - Platform tam koşusu ajan raporuna göre 173 taban kırmızı; CT tekrarlamadı.
- **CT sabotajı:** kod önceliğinde CanonicalId register kodunun önüne alındı → `Option_code_prefers_the_register_code_then_the_canonical_id_then_the_key` kırmızı. Kod geri alındı.
- **Ajan DUR notu (kabul):** register ↔ belge bağı 1:1 zorunlu değil. Canlıda 720 satırın 4'ü bağlı ve hepsi farklı belgelerde. Kalıcı 1:1 koruması MOD-0029'da ayrı iş (kullanıcı kararı).
- **F-5 kök nedeni:** temadaki `.nav-tabs .nav-link { text-transform: capitalize }` kuralı.
- **CT E4 dikkat:** ALMIBA belgelerinin register satırında `DocumentCode` boşsa (FU07 atama motoru doldurur), kod yine ortak CanonicalId'ye düşer. E4'te canlıda bakılacak.
