# WORK PACKAGE — WP-CL-FE-1 · İddialar v2 arayüzü: liste + hızlı görünüm + Web proxy katmanı + liste sayaçları (frontend + küçük CRM okuma eki)

> **CT (SoR).** İddialar v2 Faz 3, ilk arayüz paketi. Kaynaklar:
> - Mockup: `mockups/claims-v2/` (senaryo 1 liste, 12 boş liste, 13 yetkisiz)
> - Plan: `SCMM-claims-v2-mockup-analysis-plan.md` (CL-FE-1)
> - Arka uç: BE-1…BE-6 (hepsi birleşik, `test/crm-content-visit-e2e`)
>
> **Amaç:**
> - İddia listesini mockup'a göre yeniden kurmak: ülke durum çipleri, kanıt/onay/kullanım sayıları, filtreler, hızlı görünüm paneli.
> - **Tüm v2 arka uç uçları için Web tarafında aynı-origin proxy katmanını tek seferde kurmak.** Sonraki arayüz paketleri (FE-3 form, FE-2 matris…) bu katmanı kullanacak.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-fe-1`, dal `wp/cl-fe-1`. Commit bu dala, push YOK.

## Mockup (okunacak dosyalar)
`execution/domains/commercial-suite/work-packs/mockups/claims-v2/`:
- `claims-v2-prototype.html`: özgün prototip (tarayıcıda açılır).
- `claims-v2-screens.decoded.html`: ekranların çözülmüş HTML yapısı.
- `claims-v2-logic-and-sample-data.decoded.js`: durumlar (`ST`), roller, örnek iddialar (`CLAIMS`), kanıtlar, kullanım, senaryolar.
- `claims-v2-screen-texts.txt`: ekran metinleri.

> **Mockup veri ve kişi adları ÖRNEKTİR.** Görsel dil, yerleşim ve davranış mockup'tan alınır; veri gerçek API'den gelir.
> **Mockup'tan sapmalar (kullanıcı kararları):**
> - Ülke kısıtı YOK (D1). "Yetki alanınız dışında" maskelemesi yok.
> - Ruhsat no ve sahibi YOK (D3). Kapalı hücrede yalnız neden görünür.
> - Onay sıralı (D2).

## Kanıt (CT kod okuması)
- **Mevcut Web:**
  - `frontend/Diten.Web/Controllers/CRM/ClaimsController.cs` (401 satır): Index, Create/Edit (MVC form post), `api/claims` (liste), `api/claims/{id}/approve` (**artık 409**), archive, contents.
  - Proxy yardımcıları: `ProxyGetAsync` / `ProxyJsonAsync`, `RequirePage` / `RequireJson`.
  - Görünümler: `Views/CRM/Claims/{Index, _DataTable, _Filter, _Form, Create, Edit, _IndexL10n}.cshtml`, `ClaimsIndex.cs`. JS: `wwwroot/assets/js/CRM/Claims/{index, form, index.l10n}.js`.
- **CRM v2 uçları** (`CrmService.Api/Controllers/CRM/ClaimsController.cs` + `ClaimUsageController.cs`), hepsi `api/crm/content-composition/claims…`:
  - **Liste ve detay:** list, get, create, update, archive, coverage, new-version.
  - **Ülke kapatma:** country-closures (+ reopen).
  - **Ülke sürümü:** country-versions (list, create, get, update, new-version, archive).
  - **Onay:** submit-review ve withdraw-review (iddia + ülke sürümü), review-history (iki tür).
  - **Kanıt:** list (iki tür), link (iki tür), remove, document-options.
  - **Kullanım:** usage.
  - **Kaldırıldı:** approve (iki tür) → 409 `approval_via_workflow_only`.
- **Yeniden kullanılacak lookup desenleri:**
  - `Controllers/CRM/KnowledgeConceptsController.cs`: `api/audience-profiles`, `api/reference-data/{setCode}/values` (scope_key), `api/global-product-options` (MDM).
  - `Controllers/WorkflowController.cs:130`: `lookup/positions`.
  - Org birimleri: `/api/platform/organization-units…`.
- **CRM liste DTO'su** (`ClaimDto`): `kind`, `product`, `audienceProfileIds`, `countrySummary[{countryCode, state}]`, `EvidenceExpiring`. **Kanıt sayısı, onaylı ülke sayısı ve kullanım sayısı YOK** → bu pakette eklenir.
- **Referans setleri:** `COUNTRY_CODES` (Global, 6), `country-content-languages` (Global, attribute Languages), `claim-country-closure-reason` / `claim-adaptation-type` (tenant), `evidence-type` (Global).

## NE

### A. Web proxy katmanı (`ClaimsController`; mevcut `Proxy*` / `Require*` yardımcıları ile)
1. `/CRM/Claims/api/v2/…` altında **v2 uçlarının hepsi** için aynı-origin geçiş (yukarıdaki liste, birebir yol ve metot).
   - İzin: GET → `crm.claim.read`, yazma → `crm.claim.manage`.
   - Gövde ve hata `[code, message]` aynen iletilir. 204 ve gövdesiz yanıtlar güvenli (memory `proxy-forward-204-content-length-crash`).
2. **Lookup'lar:** `api/v2/lookups/`
   - `countries` → `COUNTRY_CODES`, global; `country-content-languages` ile birleştirilmiş `{code, name, languages[]}`.
   - `closure-reasons` (tenant scope_key).
   - `adaptation-types` (tenant scope_key).
   - `evidence-types` (global).
   - `products` → MDM global ürün seçici, Knowledge deseni.
   - `audience-profiles`.
   - `org-units` → sorumlu ekip.
   - `workflow-template?code=` → şablon adım adları + aday pozisyon adları (FE-3 akış önizlemesi için).
   - `workflow-history/{instanceId}` → Platform `api/v1/workflow/instances/{id}/history`.
3. **Eski uçlar:** `api/claims/{id}/approve` kaldırılır. Eski liste ucu v2 listesine yönlenir.

### B. CRM okuma eki (küçük; yalnız okuma)
4. `GET claims?includeCounts=true` → her satıra:
   - `evidenceCount`: kendi aktif kanıtı; BE-5 toplu query, **sayfa başına tek çağrı**; kanıt servisi yoksa `null`.
   - `approvedCountryCount`: onaylı + review-required ülke sürümü sayısı.
   - `usageCount`: içerik + set + yolculuk; BE-6 mantığı, sayfa için toplu.
   - Sayım yapılamazsa `null` döner; liste bozulmaz.

### C. Liste sayfası (mockup senaryo 1 / 12 / 13)
5. **Başlık:** "İddialar" + breadcrumb. Araç çubuğu (`crm.claim.manage` varsa): **"Yeni çekirdek iddia"**, **"Yeni yerel iddia"** (FE-3 sayfasına gider; o gelene kadar mevcut Create sayfası kullanılır).
6. **Standart DataTable v2** (satır içi filtre, kolon görünürlüğü, arama, dışa aktarma, satır işlem menüsü):

   | Kolon | İçerik |
   |---|---|
   | İddia | kod + ad; alt satırda çekirdek "v1.0" ya da yerel ülke |
   | Ürün | |
   | Tür | Çekirdek / Yerel rozeti |
   | Kitle | profil adları |
   | **Ülke durumu** | `COUNTRY_CODES` sırasıyla ülke çipleri, mockup renk dili. Kapalıda tooltip "Açılmadı · {neden}"; yerelde diğer ülkeler "kapsam dışı". |
   | **Kanıt** | sayı; 0 ise kırmızı **"0 · eksik"** |
   | **Onaylı** | `n / ülke sayısı` |
   | **Kullanım** | sayı |
   | İşlemler | Görüntüle (hızlı görünüm) · Düzenle (yalnız taslak) · Yeni sürüm (onaylı / gözden geçirilmeli) · Arşivle |

7. **Filtreler:** ürün, ülke, dil, durum, kitle, tür, **"kanıt eksik"**, **"gözden geçirilmeli"**, **"süresi doluyor"**.
8. **Hızlı görünüm paneli** (satıra tıkla; mevcut offcanvas dili):
   - kod, ad, tür, ürün, çekirdek metin, niteleyiciler, kanıt / kullanım / kitle sayıları, sorumlu ekip;
   - **ülke sürümleri listesi** (ülke, sürüm, diller, durum rozeti, metin kısaltması, not);
   - düğmeler: **Kapat** / **Ayrıntıyı aç** (detay sayfası FE-6'da; o gelene kadar Düzenle ya da gizli).
9. **Durumlar:**
   - boş kütüphane: "Kütüphanede henüz iddia yok" + "Yeni çekirdek iddia";
   - filtre sonucu boş: "Filtreleri temizle";
   - salt okuma notu ("Salt okuma yetkisi…");
   - **yetkisiz → iskelet çizilmez** (UAS-001).
10. **L10n:** `ClaimsIndex` resx **7 dil** (en, tr, fr, es, zh, ar, ru). Köprü deseni (memory `l10n-bridge-pascalcase-loader`). Durum ve kapatma nedeni etiketleri value_code anahtarıyla resx'ten gelir.

## KORU / YAPMA
- **Create / Edit sayfaları ve `form.js` bu pakette DEĞİŞMEZ** (FE-3 yeniden yazacak). Liste "Düzenle" / "Yeni" onlara gider.
- **CRM'de yalnız okuma eki** (`includeCounts`). Yazma kuralları, hata kodları, BE-1…6 davranışı DEĞİŞMEZ. Platform, Auth, gateway DOKUNMA.
- Başka modüllerin sayfaları DOKUNMA (Knowledge / Workflow proxy desenleri yalnız örnek alınır).
- Tema, ikon, DataTable ve offcanvas bileşenleri mevcut Golden Reference dilinde. Yeni görsel dil icat edilmez. Mockup renkleri tema değişkenlerine eşlenir. **Açık ve koyu tema** çalışmalı.
- **Tarayıcı doğrulaması:** oturum açık sekmelere mock enjekte etme, ayrı sekme kullan. **Canlıda yazma YOK** (liste ve okuma). Veri yoksa boş durumları doğrula.
- **DUR:**
  - `includeCounts` bir sayfada kanıt veya kullanım için sayfa başına tek çağrıyla yapılamıyorsa → sayacı `null` bırak ve raporla (N+1 YAPMA).
  - Org birimi ya da şablon lookup'ı için mevcut Platform ucu yoksa → o lookup'ı atla ve raporla.

## Acceptance
- **E2:**
  - `dotnet test frontend/Diten.Web.Tests -c Release --nologo` → 0 kırmızı (taban 229).
  - `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo` → 0 kırmızı (taban 2085/0/5 + yeni).
  - Web ve CRM build 0 hata. Compact / Golden verifier varsa PASS.
- **Yeni testler:**
  - Proxy: izin kapıları (read / manage), 409 gövdesinin iletimi, 204 güvenliği.
  - CRM `includeCounts`: toplu tek çağrı; kanıt servisi yokken `null`.
  - L10n: 7 dilde anahtar eşliği ve anahtar-echo yok.
- **E4 (fleet restart sonrası, yazmasız):**
  - Liste boş durumu.
  - Tarayıcı tablosu, filtre, kolon görünürlüğü ve dışa aktarma çalışıyor.
  - Veri varsa (TUTUKON/ALMIBA eski iddiaları) hızlı görünüm açılıyor, ülke çipleri 6 ülke.
  - TR ve bir diğer dil; koyu tema.
  - Onayla düğmesi yok.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CL-FE-1 · İddialar v2 arayüzü: liste + hızlı görünüm + Web proxy katmanı + liste sayaçları (frontend + küçük CRM okuma eki)
Repository: C:\tmp\cl-fe-1 (worktree) · Branch: wp/cl-fe-1 · commit bu dala, push YOK

Amaç: İddia listesini mockup'a göre yeniden kur (ülke durum çipleri, kanıt/onay/kullanım sayıları, filtreler, hızlı görünüm) ve TÜM v2 arka uç uçları için Web aynı-origin proxy katmanını tek seferde kur (sonraki arayüz paketleri kullanacak). Create/Edit bu pakette DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-FE-1-claims-list-quickview-proxy.md · execution/domains/commercial-suite/work-packs/mockups/claims-v2/ (claims-v2-prototype.html tarayıcıda; claims-v2-screens.decoded.html; claims-v2-logic-and-sample-data.decoded.js [ST durumlar, CLAIMS]; claims-v2-screen-texts.txt) · …/SCMM-claims-v2-mockup-analysis-plan.md (kararlar D1–D5) · frontend/Diten.Web/Controllers/CRM/ClaimsController.cs + Views/CRM/Claims/** + wwwroot/assets/js/CRM/Claims/** · services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/{ClaimsController,ClaimUsageController}.cs (v2 uç listesi) · Application/Features/ContentComposition/Claims/{ClaimDtos,ClaimQueryHandlers,ClaimEvidenceCore,ClaimUsageQueryHandlers}.cs · frontend/Diten.Web/Controllers/CRM/KnowledgeConceptsController.cs (audience-profiles, reference-data scope_key, global-product-options desenleri) · Controllers/WorkflowController.cs (lookup/positions) · memory: l10n-bridge-pascalcase-loader, proxy-forward-204-content-length-crash, dt-inline-filter-host-class, updatevisualstate-global-selectors, crm-classmap-rejects-unknown-elements.

NE:
 A1) /CRM/Claims/api/v2/… altında TÜM CRM v2 claims uçları için aynı-origin proxy (birebir yol+metot; GET crm.claim.read, yazma crm.claim.manage; [code,message] gövdeleri aynen; 204 güvenli).
 A2) /CRM/Claims/api/v2/lookups/: countries (COUNTRY_CODES global + country-content-languages birleşik {code,name,languages[]}), closure-reasons, adaptation-types (tenant scope_key), evidence-types (global), products (MDM), audience-profiles, org-units, workflow-template?code= (adım adları + aday pozisyon adları), workflow-history/{instanceId} (Platform api/v1/workflow/instances/{id}/history).
 A3) Eski api/claims/{id}/approve proxy'sini kaldır; eski liste v2 listesine.
 B4) CRM GET claims?includeCounts=true → evidenceCount (BE-5 toplu query, sayfa başına TEK çağrı; servis yoksa null), approvedCountryCount (onaylı+review-required), usageCount (BE-6 mantığı toplu); yapılamazsa null, liste bozulmaz.
 C5) Liste sayfası (mockup senaryo 1): başlık + "Yeni çekirdek iddia"/"Yeni yerel iddia" (manage); DataTable v2 kolonları: İddia (kod+ad+alt satır), Ürün, Tür rozeti, Kitle, Ülke durumu çipleri (COUNTRY_CODES sırası, mockup renk dili, kapalıda "Açılmadı · {neden}" tooltip, yerelde diğerleri kapsam dışı), Kanıt ("0 · eksik" kırmızı), Onaylı (n/ülke), Kullanım, İşlemler (Görüntüle / Düzenle yalnız taslak / Yeni sürüm onaylı-gözden geçirilmeli / Arşivle). ONAYLA düğmesi YOK.
 C6) Filtreler: ürün, ülke, dil, durum, kitle, tür, kanıt eksik, gözden geçirilmeli, süresi doluyor.
 C7) Hızlı görünüm offcanvas: kod/ad/tür/ürün/çekirdek metin/niteleyiciler/sayılar/sorumlu ekip + ülke sürümleri listesi (ülke, sürüm, diller, durum, metin kısaltması, not) + Kapat/Ayrıntıyı aç (detay FE-6'ya kadar Düzenle ya da gizli).
 C8) Durumlar: boş kütüphane, filtre boş, salt okuma notu, yetkisizde iskelet YOK (UAS-001).
 C9) ClaimsIndex resx 7 dil + köprü; durum/neden etiketleri value_code ile resx'ten.
KORU/YAPMA: Create/Edit + form.js DEĞİŞMEZ; CRM'de yalnız okuma eki (BE-1…6 kural/kod aynı); Platform/Auth/gateway ve diğer modül sayfaları DOKUNMA; Golden Reference görsel dili, mockup renkleri tema değişkenlerine, açık+koyu tema; tarayıcıda oturumlu sekmeye mock enjekte etme, ayrı sekme, canlıda YAZMA YOK.
DOĞRULA (E2): cd C:\tmp\cl-fe-1; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 229); dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2085/0/5 + yeni); Web+CRM build 0 hata; verifier varsa PASS; yeni testler: proxy izin kapıları + 409 iletimi + 204, includeCounts tek çağrı + servis yokken null, L10n 7 dil eşliği/echo yok. E4 (fleet restart sonrası, yazmasız): boş durum, tablo/filtre/kolon/dışa aktar, veri varsa hızlı görünüm + 6 ülke çipi, TR + bir diğer dil, koyu tema. Commit ("feat(crm): WP-CL-FE-1 — claims v2 list, quick view, web proxy layer, list counts" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: includeCounts sayfa başına tek çağrıyla yapılamıyorsa sayaç null + raporla (N+1 YAPMA); org-units/şablon lookup'ı için Platform ucu yoksa o lookup'ı atla + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-29) → **ACCEPTED (E2)** — E4 birleştirme + fleet restart sonrası CT yapacak
```
Commit: d6d250df · Agent: PASS (Web 256/0, CRM 2089/0/5, 4 sabotaj) · CT: worktree C:\tmp\cl-fe-1 → Web 256/0, CRM 2089/0/5, index.js sözdizimi temiz
```
- ✅ **Kapsam:** 25 dosya. Web (controller, görünümler, JS, resx, testler) + CRM okuma eki (`includeCounts`, `ClaimListCounts`). Create / Edit / form.js, Platform, Auth, gateway diff YOK.
- ✅ **Proxy güvenliği (CT okudu):**
  - `ClaimsController.V2.cs` **açık liste**: 29 iddia ucu + 9 lookup, tek tek `[HttpGet/Post/Put]` ile. **Joker (`{**everything}`) YOK.**
  - GET → `crm.claim.read`, yazma → `crm.claim.manage`.
  - Gövdede `tenantId` → red. 204 gövdesiz. Eski approve proxy kaldırıldı.
- ✅ `includeCounts`: sayfa başına toplu okuma (N+1 yok); servis yoksa `null`. Spec dışı ek `expiringCountryCodes` (kabul; ülke bazında süresi dolan çip ve filtre için).
- ➕ **Notlar (kabul):**
  - "Yeni çekirdek / yerel" düğmeleri `?kind=` ile mevcut Create'e gidiyor → FE-3.
  - Liste kayıt başına satır (v1 onaylı + v2 taslak = 2 satır). FE-6'da gruplama değerlendirilecek.
  - Eski fr / es echo değerleri gerçek kelime.
- ⏳ **E4 (tarayıcı):** birleştirme + fleet restart sonrası CT canlıda yazmasız kontrol eder.
