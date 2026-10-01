# WORK PACKAGE — WP-SB-3-UIa · Strateji şablonu formu: ürün satırında rol + yolculuk · eski şablon bağları salt okunur · dönem kapasitesinde 3 + 3

> **CT (SoR), 2026-10-01.**
> - **Tasarım:** `DESIGN-SB-3-multi-product-visit-content.md` §3.1, §3.5; paket tablosundaki SB-3-UI'nin **şablon formu + kapasite** kısmı. (Planlama önizlemesi, "Ziyaret yap" / "Tamamla", rapor ürün listesi → SB-3-UIb, SB-3b / c sonrası.)
> - **Neden şimdi:** SB-3a (`a318fd82`) rol + yolculuğu zorunlu yaptı; bugünkü form göndermiyor → Web'den yeni şablon ya da taslakta satır değişikliği 400. Kullanıcı (2026-10-01): geçiş açığı kabul, form SB-3b ile **paralel**.
> - **Kapsam:** yalnız Web (`frontend/Diten.Web`). CRM sözleşmesi SB-3a'da hazır.
>
> **Çalışma yeri:** worktree `C:\tmp\sb-3-ui-a`, dal `wp/sb-3-ui-a`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Yetkili kullanıcı strateji şablonunu Web'den yeniden oluşturabilsin / düzenleyebilsin:
- her ürün satırında **rol** (promo / non-promo) ve o ürünün **yayındaki yolculuğu** seçilsin;
- şablon düzeyi Bilgi Yolu / yolculuk bağı **eklenemesin**, mevcutlar "eski bağ" olarak görünsün, taslakta kaldırılabilsin;
- dönem kapasitesinde ziyaret başına **en fazla promo / non-promo ürün** (varsayılan 3 / 3) girilsin.

## Kanıt (CT)
- **Form:** `Views/CRM/StrategyTemplates/{Create, Edit, _Form, Details, _SidePanel}.cshtml`, `wwwroot/assets/js/CRM/StrategyTemplates/{form.js (78 kb), details.js, index.js}`.
  - Ürün satırları `ProductLinesJson` (gizli alan, `form.js:94, :859`), içerik bağları `ContentBindingsJson` (`:95, :860`); donmuş şablonda ikisi boş gönderilir (`:859-860`).
  - Yol / yolculuk seçenekleri `load(${endpoint}/knowledge-paths | content-engagement-journeys)` (`:172-196`), içerik kartları `contentBindingList` (`:612-760`).
- **Controller:** `Controllers/CRM/StrategyTemplatesController.cs` — sayfalar + `api/templates/*`, `api/knowledge-paths` (:247), `api/content-engagement-journeys` (:252), `api/global-products` (:260), `api/gskus` (:267). Görünüm modelleri `Models/CRM/StrategyTemplateViewModels.cs`.
- **Kapasite:** `Views/CRM/CycleCapacities/{_Form, Details}.cshtml`, `wwwroot/assets/js/CRM/CycleCapacities/form.js`, `Models/CRM/CycleCapacityFormViewModels.cs`, `Controllers/CRM/CycleCapacitiesController.cs`.
- **L10n:** `Resources/**/StrategyTemplatesIndex.{en,tr,fr,es,zh,ar,ru}.resx`, `CycleCapacitiesIndex.*.resx`; anahtarlar `_IndexL10n.cshtml` dizisinde. Tenant modülü → **7 dil**, TR diakritik.
- **CRM sözleşmesi (SB-3a):**
  - satır yazımı: `role` (`promo` | `non-promo`), `journeyId`;
  - okuma: `role`, `journeyId`, `journeyCode`, `journeyName`, `journeyStatus`, `journeyMissing`, `journeyWarnings[]` (`journey_language_not_in_country`, `journey_not_published`, `journey_not_found`); özet `promoLineCount`, `nonPromoLineCount`, `linesWithoutJourneyCount`; içerik bağında `retired: true`;
  - hata kodları: 400 `product_line_role_required` / `product_line_role_invalid` / `product_line_journey_required`; 409 `journey_not_published` / `journey_product_mismatch` / `content_binding_type_retired` / `bindings_frozen`;
  - kapasite: `maxPromoProducts` / `maxNonPromoProducts` (1..10, varsayılan 3, gönderilmezse kayıttaki korunur), 400 `max_products_out_of_range`.
- **Yolculuğun ürünü:** CRM'de konu → birincil `global-product` dış referansı (`ChainContextResolver.PrimaryGlobalProduct`). Web'deki konu DTO'su dış referansları taşıyor (MOD-0162 SUBJECT-UI, memory `mod0162-subject-ui-global-product-link`).

## NE
1. **Ürün satırı (form):**
   - **Rol** seçici (promo / non-promo). Yeni satırda **promo** seçili gelir; kullanıcı değiştirebilir.
   - **Yolculuk** seçici: yalnız **yayındaki** ve **ürünü bu satırın ürünü olan** yolculuklar.
     - Filtre Web controller'da sunucu tarafında: yeni uç `api/line-journeys?productId=` (yayındaki yolculuklar × konunun birincil global-product'ı). Bu yalnız **seçenek filtresi**; karar CRM'de (409 `journey_product_mismatch`).
     - Seçenekte kod — ad · dil gösterilir; dil şablonun ülke kapsamıyla uyumsuzsa seçenekte uyarı rozeti (CRM okumada `journey_language_not_in_country` döner; formda anında göstermek için ülke-dil setini tekrar uydurma — yoksa kayıttan sonra detayda görünür).
     - Ürün için yayında yolculuk yoksa satırda bilgi: "Bu ürün için yayında etkileşim yolculuğu yok" + Etkileşim Yolculukları sayfasına bağlantı.
   - `ProductLinesJson` satıra `role` + `journeyId` ekler (mevcut alanlar aynı).
   - Eski satır (rol / yolculuk yok) düzenlemeye açılınca: rol **promo** gösterilir ama satır "yolculuk eksik" uyarısıyla işaretlenir; kullanıcı yolculuk seçmeden kaydederse CRM 400'ü alan altında gösterilir.
   - **Donmuş (aktif) şablon:** rol / yolculuk salt okunur (bugünkü donmuş davranış); "Yeni sürüm" ile düzeltilir.
2. **Şablon düzeyi içerik bağları:**
   - "Bağ ekle" seçicisi / kartları **kaldırılır** (yeni bağ yok).
   - Mevcut bağlar "Eski bağ — ziyarette kullanılmıyor; ürün satırındaki yolculuk geçerli" notuyla **salt okunur** listelenir; **taslakta** kaldırma düğmesi.
   - `ContentBindingsJson` yalnız kalanları gönderir (eklemeye yol yok).
3. **Hata gösterimi:** yukarıdaki SB-3a hata kodları **ilgili satırın altında**, yerelleştirilmiş, anlaşılır metinle (kod değil). `content_binding_type_retired` bağ bölümünde.
4. **Detay + yan panel + liste:**
   - Detayda satır başına rol rozeti, yolculuk kodu / adı / durumu, `journeyMissing` ve `journeyWarnings` uyarıları; özet sayaçları (promo / non-promo / yolculuksuz satır).
   - Yan panel ("tarif") rol dağılımını göstersin (ör. "2 promo · 1 non-promo").
   - Eski bağlar detayda "eski bağ" etiketiyle.
5. **Dönem kapasitesi formu + detay:** "Ziyaret başına en fazla promo ürün" / "… non-promo ürün" (1..10, varsayılan 3). Düzenlemede kayıttaki değer gelir. Detayda iki değer. `max_products_out_of_range` alan altında.
6. **L10n:** tüm yeni metinler 7 dilde (en, tr, fr, es, zh, ar, ru), TR diakritik; `_IndexL10n.cshtml` anahtar dizisi + PascalCase köprü (memory `l10n-bridge-pascalcase-loader`).

## KORU / YAPMA
- **CRM / Platform / Auth DOKUNMA.** Yeni CRM ucu YOK (filtre Web controller'da, mevcut CRM okumalarıyla).
- SKU dağılımı, ağırlık (Σ = 100), segment / frekans bağları, ülke / LE / BU kapsamı, sürüm / etkinleştirme / arşiv akışları DEĞİŞMEZ.
- Liste ekranlarının altın şablon yapısı bozulmaz; yeni liste ekranı yok.
- Kullanıcı girdisi DOM'a `esc()` / `textContent` ile. Proxy 204 tuzağı (memory `proxy-forward-204-content-length-crash`).
- Planlama önizlemesi / ziyaret ekranı / rapor DOKUNMA (SB-3-UIb).
- **DUR:** Web'deki konu DTO'su birincil global-product'ı taşımıyorsa (filtre kurulamıyorsa) → uydurma, raporla (geçici çözüm: ürün filtresiz yayındaki yolculuklar + CRM 409'u satırda göster).

## Acceptance
- **E2:**
  - Web testleri 0 kırmızı (taban **396/0**). CRM testleri 0 kırmızı (taban 2123/0/5; dokunulmadığı doğrulansın). Build 0 hata.
  - **Yeni testler:**
    - `ProductLinesJson` round-trip: `role` + `journeyId` CRM isteğine geçiyor; eski satır alanları kaybolmuyor;
    - `api/line-journeys`: yalnız yayındaki + ürünü eşleşen yolculuklar; başka tenant / taslak / arşiv dönmez; ürün yoksa boş liste;
    - içerik bağı ekleme yolu yok; mevcut bağ kaldırılınca `ContentBindingsJson`'dan düşüyor;
    - SB-3a hata kodlarının yerelleştirilmiş eşlemesi (7 dil anahtarı mevcut);
    - kapasite: iki alan gönderiliyor, boşsa gönderilmiyor (kayıttaki korunur), aralık;
    - L10n: yeni anahtarlar 7 resx'te, değer ≠ anahtar (mevcut NavL10n / L10n guard'ları).
  - **Sabotaj:** (1) `ProductLinesJson`'dan `journeyId` çıkarılınca round-trip testi kırmızı; (2) `api/line-journeys` ürün filtresi kaldırılınca filtre testi kırmızı.
- **E4 (CT, fleet sonrası):** ALMIBA ürünü + yayındaki yolculukla yeni taslak şablon oluştur → kaydet → detayda rol / yolculuk; "test" taslağında eski bağlar salt okunur + kaldırma; kapasitede 3 / 3 görünür ve değiştirilebilir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SB-3-UIa · Strateji şablonu formu: ürün satırında rol + yolculuk · eski şablon bağları salt okunur · dönem kapasitesinde 3+3
Repository: C:\tmp\sb-3-ui-a (worktree) · Branch: wp/sb-3-ui-a · commit bu dala, push YOK · yalnız frontend/Diten.Web

Amaç: SB-3a (a318fd82) CRM'de ürün satırına rol + yolculuğu zorunlu yaptı; bugünkü Web formu göndermiyor (yeni şablon / satır değişikliği 400). Formu yeni sözleşmeye taşı: satırda rol (promo/non-promo) + o ürünün yayındaki yolculuğu; şablon düzeyi yol/yolculuk bağı eklenemez, mevcutlar "eski bağ" salt okunur (taslakta kaldırılabilir); dönem kapasitesinde ziyaret başına max promo / non-promo (varsayılan 3/3).

Önce oku: execution/domains/commercial-suite/work-packs/WP-SB-3-UIa-template-form-roles-journeys.md · …/WP-SB-3a-template-product-roles-journeys.md (§37: sözleşme + hata kodları) · …/DESIGN-SB-3-multi-product-visit-content.md §3.1/§3.5 · frontend/Diten.Web/Views/CRM/StrategyTemplates/** · wwwroot/assets/js/CRM/StrategyTemplates/{form,details}.js · Controllers/CRM/StrategyTemplatesController.cs · Models/CRM/StrategyTemplateViewModels.cs · Views/CRM/CycleCapacities/{_Form,Details}.cshtml · wwwroot/assets/js/CRM/CycleCapacities/form.js · Models/CRM/CycleCapacityFormViewModels.cs · Controllers/CRM/CycleCapacitiesController.cs · Resources/**/StrategyTemplatesIndex.*.resx + CycleCapacitiesIndex.*.resx · memory l10n-bridge-pascalcase-loader, proxy-forward-204-content-length-crash, mod0162-subject-ui-global-product-link, mod0167-fu04-strategy-template.

NE:
 1) Ürün satırı: rol seçici (yeni satırda promo seçili) + yolculuk seçici (yalnız yayındaki ve ürünü satırın ürünü olan; yeni Web ucu api/line-journeys?productId= — yayındaki yolculuklar × konunun birincil global-product dış referansı; yalnız seçenek filtresi, karar CRM'de). Yolculuk yoksa bilgi + Etkileşim Yolculukları bağlantısı. ProductLinesJson'a role + journeyId (mevcut alanlar aynı). Eski satır: promo gösterilir + "yolculuk eksik" uyarısı. Donmuş şablonda salt okunur.
 2) Şablon düzeyi içerik bağları: ekleme seçicisi/kartları KALKAR; mevcutlar "Eski bağ — ziyarette kullanılmıyor" salt okunur, taslakta kaldır düğmesi; ContentBindingsJson yalnız kalanlar.
 3) SB-3a hata kodları (product_line_role_required/_invalid, product_line_journey_required, journey_not_published, journey_product_mismatch, content_binding_type_retired, bindings_frozen) ilgili satırın altında yerelleştirilmiş metinle.
 4) Detay + yan panel: satır başına rol rozeti, yolculuk kodu/adı/durumu, journeyMissing + journeyWarnings; özet sayaçları; eski bağ etiketi; tarifte rol dağılımı.
 5) Dönem kapasitesi form + detay: maxPromoProducts / maxNonPromoProducts (1..10, varsayılan 3; düzenlemede kayıttaki; boşsa gönderme); max_products_out_of_range alan altında.
 6) L10n 7 dil (en, tr, fr, es, zh, ar, ru), TR diakritik, _IndexL10n anahtar dizisi + PascalCase köprü.
KORU/YAPMA: CRM/Platform/Auth DOKUNMA, yeni CRM ucu yok; SKU/ağırlık/segment/frekans/kapsam/sürüm akışları DEĞİŞMEZ; liste altın şablonu bozulmaz; esc()/textContent; planlama önizlemesi/ziyaret/rapor DOKUNMA (SB-3-UIb).
DOĞRULA (E2): cd C:\tmp\sb-3-ui-a; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 396); dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2123/0/5); build 0 hata. Yeni testler WP Acceptance listesindeki her madde. Sabotaj: (1) ProductLinesJson'dan journeyId çıkar → round-trip testi kırmızı; (2) line-journeys ürün filtresini kaldır → filtre testi kırmızı. Commit ("feat(web): WP-SB-3-UIa — strategy template form product line role + journey, retired bindings read-only, cycle capacity max products" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: Web konu DTO'su birincil global-product'ı taşımıyorsa uydurma → DUR + raporla (geçici: filtresiz yayındaki yolculuklar + CRM 409 satırda).
```
