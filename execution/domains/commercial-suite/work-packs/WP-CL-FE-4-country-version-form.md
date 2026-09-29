# WORK PACKAGE — WP-CL-FE-4 · Ülke sürümü formu (dil başına metin · uyarlama · kitle daraltma · miras + yerel kanıt · yerel onay) + form düzeltmeleri (frontend)

> **CT (SoR).** İddialar v2 Faz 3, 2. tur. **FE-2 ile paralel** çalışır. Kaynaklar:
> - Mockup senaryo **4** (çekirdekten ülke sürümü açma) ve **5** (yerel kanıt: çekirdek kanıtı + yerel KÜB)
> - Kararlar: D1, D2 (sıralı), D3 (ruhsat no / sahibi YOK)
> - FE-3 canlı bulguları: kanıt tipi etiketleri kod görünüyor; kod önerisi ürün adı yerine MDM kodu kullanıyor
>
> **Çalışma yeri:** worktree `C:\tmp\cl-fe-4`, dal `wp/cl-fe-4`. Taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
>
> **FE-2 ile çakışmayı önlemek için:** `Index.cshtml`, `index.js`, `ClaimsIndex` resx ve `ClaimsController.V2.cs` lookup'larına DOKUNMA. `ClaimsController.cs`'e yalnız **yeni action'lar** ekle.

## Mockup
`mockups/claims-v2/`: senaryolar `ver` (4) ve `evid` (5). Ekran metinleri:
- "Ülke Sürümü Aç · Özbekistan"
- "Çekirdek iddia · salt okunur"
- "Ülke dilleri · ülke ana kaydından"
- "Metin ve niteleyiciler · dil başına"
- "Uyarlama — Ülke sürümü çekirdeğe göre daraltılabilir ya da yumuşatılabilir; kapsamı genişletilemez."
- "Hedef kitle — Yalnız çekirdeğin kitlelerinden daraltılabilir."
- "Yerel kanıt ekle"
- "Yerel onay akışı"
- "Yerel kanıt yok. Özbekistan KÜB'ü gibi yerel belgeleri buraya bağlayın."

**Mockup'taki "Ruhsat sahibi / Ruhsat no" kartı YOK** (D3).

## Kanıt (CT)
- **CRM:**
  - Ülke sürümü CRUD (`claims/{id}/country-versions` POST, `country-versions/{id}` GET/PUT, `…/new-version`, `…/archive`).
  - Onay: `…/submit-review`, `…/withdraw-review`, `…/review-history`. Kanıt: `…/evidence` (GET, POST).
  - Hata kodları: `core_not_approved`, `country_closed`, `country_version_exists`, `languages_incomplete`, `audience_not_narrowing`, `evidence_required`, `evidence_locked`, `in_review_locked`, `approval_template_missing`, `workflow_unavailable`, `reference_set_missing`.
- **Kanıt listesi:** `origin: core|local` (miras). Yerel iddiada miras yok.
- **Web (FE-1):** tüm bu uçlar ve lookup'lar (`adaptation-types`, `evidence-types`, `audience-profiles`, `workflow-template`) `/CRM/Claims/api/v2/…`. `lookups/countries`: FE-2 `languageDetails` ekliyor; yoksa `languages` string dizisine geri düş.
- **FE-3:**
  - `Views/CRM/Claims/{Create, Edit, _ClaimForm…}.cshtml` + `wwwroot/assets/js/CRM/Claims/form.js`.
  - Kanıt modalı ve kaldırma modalı form içinde. `ClaimsForm` resx (151 anahtar).
- **Canlı bulgular:**
  - Kanıt tipi seçicide metin kod ("smpc-pil").
  - Kod önerisi `CLM-GP000000000001-01` (ürün adı yerine canonical kod).

## NE
1. **Kanıt modalını paylaşılır yap:**
   - FE-3 formundaki kanıt ekleme ve kaldırma modalı + JS mantığı **ayrı bir partial + JS modülüne** (`_ClaimEvidenceModal.cshtml` + `claim-evidence.js`) çıkarılır.
   - Çekirdek form ve ülke sürümü formu aynı modülü kullanır.
   - FE-3 davranışı **birebir korunur**: adımlar, sürüm sabitleme, fare ve klavye ile işaretleme, en çok 10 parça.
2. **FE-3 düzeltmeleri:**
   - (a) Kanıt tipi, uyarlama tipi ve durum etiketleri **resx'ten, value_code anahtarıyla** (KÜB/KT, Klinik çalışma…). Kod gösterilmez.
   - (b) Kod önerisi **ürün adından**: `CLM-{ÜRÜN ADI büyük harf, harf/rakam dışı karakterler "-"}-{NN}` (ör. `CLM-ALMIBA-03`). NN, aynı önekle var olan kodlara göre bir sonraki numara.
3. **Ülke sürümü sayfası:** `/CRM/Claims/{claimId}/Countries/{countryCode}/Create` ve `/CRM/Claims/CountryVersions/{versionId}/Edit`. Yetki: `crm.claim.manage`; okuma `crm.claim.read` + salt okunur görünüm. **8/4 düzen.**
   - **Çekirdek kartı (salt okunur):** kod · v{çekirdek sürüm} · durum rozeti · çekirdek metin (en) · niteleyici · kitle · kanıt sayısı.
   - **Ülke kartı:** ülke adı (`name`, yoksa kod) + diller "· ülke ana kaydından".
   - **Metin ve niteleyiciler · dil başına:** ülke dillerinin her biri için sekme. Sekmede "Çekirdek (en)" metin ve niteleyici **salt okunur referans** olarak durur + **yerel metin*** (sayaçlı) + **yerel niteleyiciler** listesi. Eksik dil sekmesi işaretli.
   - **Uyarlama:** tip (tek seçim; resx etiketleri) + neden (`verbatim` dışında zorunlu).
   - **Geçerlilik:** başlangıç*, bitiş (flatpickr).
   - **Hedef kitle:** çoklu seçim, **yalnız çekirdeğin kitleleri** seçenek olarak gelir.
   - **Kanıtlar:** miras (`origin=core`, salt okunur, "Çekirdekten" rozeti) + yerel (`origin=local`) + **"Yerel kanıt ekle"** (paylaşılan modal). Boş yerel durum metni mockup'taki gibi.
   - **Yan panel:**
     - Onaya hazırlık: çekirdek onaylı · tüm dillerde metin · uyarlama nedeni · geçerlilik · ≥1 etkin kanıt (miras + yerel) · kaydedildi.
     - Yerel onay akışı önizlemesi (`workflow-template?code=CLAIM-LOCAL-MLR-{cc}`).
     - Durum kartı: taslak → Taslağı kaydet / Onaya gönder; incelemede → Geri çek + kilit; onaylı / gözden geçirilmeli → kilit bandı + **Yeni sürüm aç** (`country-versions/{id}/new-version`).
   - Hata kodları kullanıcı dilinde (listedeki kodlar).
4. **Bağlantılar:**
   - FE-2 matrisi "Ülke sürümü aç" ve "Düzenle" rotaları bu sayfaya gelir.
   - FE-1 hızlı görünümündeki ülke sürümü satırı → Edit.
5. **L10n:** yeni resx ailesi `ClaimsCountryVersion.{7 dil}.resx` + köprü. Paylaşılan kanıt modülünün anahtarları `ClaimsForm` resx'te kalır (FE-3'ten taşınmaz).

## KORU / YAPMA
- **FE-2 dosyaları** (`Index.cshtml`, `index.js`, `ClaimsIndex` resx, V2 lookup'ları) DOKUNMA. FE-1 proxy yolları DEĞİŞMEZ.
- CRM, Platform, Auth DOKUNMA.
- FE-3 çekirdek form davranışı yalnız bu WP'deki düzeltmeler kadar değişir.
- **Ruhsat no / sahibi YOK.** Ülke kısıtı YOK. Paralel onay YOK.
- **Tarayıcı:** ayrı sekme, canlıda yazma YOK (E4 CT yapar).
- **DUR:** kanıt modalını paylaşılır yapmak FE-3 testlerini bozuyorsa → modalı kopyalamadan, formun içinde bırakıp ülke sayfasında aynı partial'ı `include` ederek çöz ve raporla.

## Acceptance
- **E2:**
  - Web testleri 0 kırmızı (taban 270).
  - Web build 0 hata.
  - `node --check` temiz.
  - Yeni testler:
    - `ClaimsCountryVersion` L10n 7 dil + JS anahtar-resx eşliği;
    - sayfa izin kapıları;
    - kod önerisi fonksiyonu (ürün adı → önek, NN artışı);
    - kanıt tipi etiketlerinin resx'ten gelmesi.
- **E4:** CT, birleştirme + fleet restart sonrası yapar (onaylı çekirdekten TR ülke sürümü aç → onaya gönder).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CL-FE-4 · Ülke sürümü formu (dil başına metin · uyarlama · kitle daraltma · miras + yerel kanıt · yerel onay) + form düzeltmeleri (frontend)
Repository: C:\tmp\cl-fe-4 (worktree) · Branch: wp/cl-fe-4 · commit bu dala, push YOK · FE-2 PARALEL: Index.cshtml / index.js / ClaimsIndex resx / ClaimsController.V2.cs lookup'ları DOKUNMA; ClaimsController.cs'e yalnız yeni action'lar

Amaç: Mockup senaryo 4/5 ülke sürümü sayfası (Create /CRM/Claims/{claimId}/Countries/{cc}/Create, Edit /CRM/Claims/CountryVersions/{versionId}/Edit) + FE-3 kanıt modalını paylaşılır modüle çıkar + canlı bulgular: kanıt/uyarlama/durum etiketleri resx'ten (kod değil), kod önerisi ürün adından (CLM-ALMIBA-NN).

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-FE-4-country-version-form.md · mockups/claims-v2/ (senaryolar ver/evid; screens.decoded.html + logic-and-sample-data.decoded.js [VERS, EV, CL]) · …/WP-CL-FE-3-core-claim-form-evidence-submit.md (+§37-E4 bulguları) · frontend/Diten.Web/Controllers/CRM/{ClaimsController.cs, ClaimsController.V2.cs} · Views/CRM/Claims/** (FE-3 formu + kanıt modalı) · wwwroot/assets/js/CRM/Claims/form.js · services/Diten.CrmService/src/**/Claims/{ClaimDtos.cs [ClaimCountryVersionDto], ClaimV2Support.cs [hata kodları], ClaimEvidenceHandlers.cs [origin]} · memory l10n-bridge-pascalcase-loader.

NE:
 1) FE-3 kanıt ekleme+kaldırma modalını _ClaimEvidenceModal.cshtml + claim-evidence.js paylaşılır modüle çıkar (davranış birebir: adımlar, sürüm sabitleme, fare+klavye işaretleme, ≤10 parça); çekirdek ve ülke formu ortak kullanır.
 2) FE-3 düzeltmeleri: (a) kanıt tipi/uyarlama/durum etiketleri resx'ten value_code anahtarıyla (kod gösterme); (b) kod önerisi ürün ADINDAN CLM-{AD büyük harf, harf/rakam dışı "-"}-{NN} (NN var olan kodlara göre sonraki).
 3) Ülke sürümü sayfası 8/4: çekirdek kartı salt okunur (kod·v·durum·en metin·niteleyici·kitle·kanıt sayısı); ülke kartı (ad [lookups/countries name, yoksa kod] + diller "· ülke ana kaydından"; RUHSAT KARTI YOK); dil başına sekmeler (çekirdek en metin/niteleyici salt okunur referans + yerel metin* sayaçlı + yerel niteleyiciler; eksik dil işaretli); uyarlama tipi (tek seçim, resx) + neden (verbatim dışı zorunlu); geçerlilik başlangıç*/bitiş; hedef kitle yalnız çekirdek kitlelerinden; kanıtlar: miras origin=core salt okunur "Çekirdekten" + yerel + "Yerel kanıt ekle" (paylaşılan modal) + boş yerel metin; yan panel hazırlık (çekirdek onaylı, tüm diller, uyarlama nedeni, geçerlilik, ≥1 etkin kanıt, kaydedildi) + yerel akış önizlemesi (workflow-template CLAIM-LOCAL-MLR-{cc}) + durum kartı (taslak kaydet/onaya gönder; incelemede geri çek+kilit; onaylı/gözden geçirilmeli kilit+Yeni sürüm aç); hata kodları kullanıcı dilinde (core_not_approved, country_closed, country_version_exists, languages_incomplete, audience_not_narrowing, evidence_required, evidence_locked, in_review_locked, approval_template_missing, workflow_unavailable, reference_set_missing). lookups/countries languageDetails yoksa languages string dizisine geri düş.
 4) FE-1 hızlı görünümdeki ülke sürümü satırı → Edit bağlantısı (index.js'e DOKUNMADAN yapılamıyorsa atla + raporla).
 5) Yeni resx ailesi ClaimsCountryVersion.{en,tr,fr,es,zh,ar,ru}.resx + köprü; paylaşılan kanıt modülü anahtarları ClaimsForm resx'te kalır.
KORU/YAPMA: FE-2 dosyaları DOKUNMA; FE-1 proxy yolları DEĞİŞMEZ; CRM/Platform/Auth DOKUNMA; FE-3 çekirdek form davranışı yalnız bu düzeltmeler kadar; ruhsat no/sahibi, ülke kısıtı, paralel onay YOK; tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\cl-fe-4; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 270); Web build 0 hata; node --check temiz; yeni testler: ClaimsCountryVersion L10n 7 dil + JS anahtar-resx eşliği, sayfa izin kapıları, kod önerisi (ad→önek, NN), kanıt tipi etiketleri resx. Commit ("feat(crm): WP-CL-FE-4 — claim country version form, shared evidence modal, label + code-suggestion fixes" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: kanıt modalını paylaşılır yapmak FE-3 testlerini bozuyorsa formun içinde bırakıp ülke sayfasında aynı partial'ı include et + raporla.
```
