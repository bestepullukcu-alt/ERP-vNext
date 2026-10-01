# WORK PACKAGE — WP-SB-3a · Strateji şablonu ürün satırı: rol (promo / non-promo) + yolculuk · dönem kapasitesinde 3 + 3 sınırı

> **CT (SoR).**
> - **Tasarım:** `DESIGN-SB-3-multi-product-visit-content.md` §2 (S3-2 / S3-3 / S3-4 / S3-8), §3.1, §3.5.
> - **Kullanıcı (2026-10-01):** non-promo ürünler **şablondaki non-promo satırlarından** gelir (tüm portföyden değil). Başlatma penceresi, "anlatıldı" varsayılanı ve kitle uyuşmazlığı **sonra** (SB-3b / c).
> - **Kapsam:** yalnız CrmService. Web SB-3-UI'de. Ziyaret çözücüsü SB-3b'de.
>
> **Çalışma yeri:** worktree `C:\tmp\sb-3a`, dal `wp/sb-3a`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Ziyarette hangi ürünlerin hangi rolde ve hangi yolculukla anlatılacağı **strateji şablonunda açıkça** tanımlansın:
- **ürün satırı:** rol (promo / non-promo) + yolculuk (o ürünün yayındaki Etkileşim Yolculuğu);
- **şablon düzeyindeki doğrudan Bilgi Yolu bağı** artık yeni bağ olarak kabul edilmez;
- **dönem kapasitesi** ziyaret başına en fazla promo / non-promo ürün sayısını taşır (varsayılan 3 / 3).

## Kanıt (CT)
- **`StrategyTemplateProductLine`** (`Domain/Entities/StrategyTemplate.cs` ~255-310): `LineId`, `GlobalProductId`, `GlobalProductCodeDisplay`, `LineWeightPercentage?`, `SkuAllocationMode`, `SkuAllocations[]`, `SortOrder`, `Notes`.
  - `MaxProductLines = 50`, `MaxContentBindings = 50` (`StrategyTemplateVocabulary.cs:248-250`).
- **`ContentBindings[]`** (`StrategyTemplate.cs:317-343`): türler `knowledge-path` / `content-engagement-journey` (`StrategyTemplateVocabulary.cs:107-108`). Bağlar etkinleştirmede donar (`areBindingsFrozen`).
- **Doğrulama:**
  - `Application/Features/StrategyTemplate/{StrategyTemplateValidation.cs (:361-394 bağ sınırı / tekrar), Binding/StrategyTemplateBindingValidator.cs (bağlı kaydın varlığı / yayın), Binding/IStrategyTemplateProductReferenceValidator.cs (MDM fail-closed)}`;
  - Create / Update / CreateVersion komutları + handler'lar; `StrategyTemplateMapper`, `StrategyTemplateModels`.
- **Yolculuk:**
  - `Domain/Entities/ContentEngagementJourney.cs` (`SubjectId` zorunlu, `AudienceProfileId?`, `LanguageCode?`, `JourneyStatus`);
  - okuyucu `IContentEngagementJourneyReader` (yayında / etkin / arşivsiz).
  - **Ürün çözümü:** konu → `ExternalReferences` `global-product` + `IsPrimary` (`Features/Knowledge/Chain/ChainContext.cs:45,87-100`; KP-1 ortak). **Kopyalama, kullan.**
- **`CycleCapacity`** (`Domain/Entities/CycleCapacity.cs`): `PromoProductTime` (:56), `NonPromoProductTime` (:61), `MinutesPerVisit()` (:118), `MaxMinutesPerVisit = 480` (:337). Create / Update handler'ları `Features/CycleCapacity/**`.
- **Canlı veri:**
  - STR-TUTUKON-URO **aktif** (1 ürün satırı, bağlar donmuş);
  - "test" taslak (1 ürün satırı, 2 `knowledge-path` bağı);
  - hiçbir play yolculuğa bağlı değil;
  - ALMIBA yolculukları taslak / aşamasız.

## NE
1. **`StrategyTemplateProductLine` genişler:**
   - `Role` (`promo` | `non-promo`);
   - `JourneyId` (Guid?; `JourneyCodeDisplay` görünen ad).
   - Yeni alanlar class-map'e (string-Guid). Sözlük sabitleri `StrategyProductLineRoles`.
2. **Yazma doğrulaması** (Create / Update / CreateVersion; taslak şablon):
   - `Role` zorunlu (400 `product_line_role_required`);
   - **`JourneyId` zorunlu** (400 `product_line_journey_required`);
   - yolculuk tenant'ta, arşivsiz ve **yayında** (409 `journey_not_published`);
   - yolculuğun konusunun birincil ürünü = satırın `GlobalProductId`'si (409 `journey_product_mismatch`);
   - yolculuğun dili şablonun ülke kapsamıyla uyumsuzsa **uyarı** (engel değil; okuma DTO'sunda `journeyWarnings`).
3. **Okuma uyumluluğu** (eski satırlar):
   - `Role` boşsa **`promo`** sayılır (bugünkü davranış: tüm satırlar promo);
   - `JourneyId` boşsa okuma DTO'sunda `journeyMissing: true`.
   - Veri yazılmaz, göç yok. Aktif şablon (STR-TUTUKON-URO) **yeni sürümle** düzeltilir; aktif / donmuş şablon değiştirilmez.
4. **Şablon düzeyi içerik bağları (S3-2):**
   - **yeni `knowledge-path` bağı kabul edilmez** (409 `content_binding_type_retired`);
   - mevcut bağlar okunur, DTO'da `retired: true`;
   - **`content-engagement-journey` bağları da yeni yazımda kabul edilmez**: yolculuk artık satırda (aynı kod).
   - Okuma geriye uyumlu. Mevcut ziyaret çözücüsü SB-3b'ye kadar **bugünkü gibi** okumaya devam eder (davranış değişmez).
5. **`CycleCapacity`:** `MaxPromoProducts` + `MaxNonPromoProducts` (int).
   - Varsayılan 3 / 3; 1..10 aralığı (400 `max_products_out_of_range`).
   - Eski kayıtta yoksa 3 / 3.
   - Create / Update / okuma DTO'ları.
   - `MinutesPerVisit` formülü bu pakette DEĞİŞMEZ (SB-3b'de ürün sayısıyla).
6. **Okuma DTO'ları:**
   - şablon detayı / bağlar: satırda `role`, `journeyId`, `journeyCode`, `journeyName`, `journeyStatus`, `journeyMissing`, `journeyWarnings`;
   - özet sayaçları: promo satır sayısı, non-promo satır sayısı, yolculuksuz satır sayısı.

## KORU / YAPMA
- **Ziyaret çözücüsü, planlama motoru, PlannedVisit, VisitReport DEĞİŞMEZ** (SB-3b / c).
- **Web DOKUNMA** (SB-3-UI). Platform / Auth DOKUNMA.
- SKU dağılımı ve ağırlık kuralları (Σ = 100 vb.), segment bağları, ülke / LE / BU kapsamı DEĞİŞMEZ.
- Aktif / donmuş şablon değiştirilmez (mevcut kural).
- Ham repository yazması yok. Yeni alanlar class-map'e.
- **DUR:**
  - mevcut ziyaret planlama ya da kampanya kodu şablon düzeyi `content-engagement-journey` bağını **yazıyorsa** (kabul etmemek onu kırar) → raporla;
  - yolculuk ürün çözümü (`ChainContext`) yolculuk için tek anlamlı değilse → raporla.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (taban — ajan ölçer; son CT 2186/0/5 + KP-4 etkisi; bilinen sıra flake'i hariç). **Web testleri 0 kırmızı** (taban 399). Build 0 hata.
  - **Yeni testler:**
    - rol / yolculuk zorunluluğu;
    - `journey_not_published` / `journey_product_mismatch`;
    - dil uyarısı;
    - eski satır okuması (rol yok → promo, yolculuk yok → `journeyMissing`);
    - `knowledge-path` ve şablon düzeyi yolculuk bağı yeni yazımda 409, mevcut bağ okunur + `retired`;
    - aktif şablon değiştirilemez (mevcut);
    - `CycleCapacity` max promo / non-promo (varsayılan, aralık, eski kayıt 3 / 3);
    - class-map round-trip;
    - mevcut ziyaret çözücüsü testleri yeşil (davranış değişmedi).
  - **Sabotaj:** `journey_product_mismatch` ve yolculuk zorunluluğu testleri kırmızıya dönmeli.
- **E4:** SB-3-UI sonrası.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SB-3a · Strateji şablonu ürün satırı: rol (promo/non-promo) + yolculuk · dönem kapasitesinde 3+3 sınırı
Repository: C:\tmp\sb-3a (worktree) · Branch: wp/sb-3a · commit bu dala, push YOK · yalnız CrmService

Amaç: DESIGN-SB-3 §3.1/§3.5: ziyarette hangi ürünlerin hangi rolde ve hangi yolculukla anlatılacağı strateji şablonunda açık olsun (ürün satırı = rol + yolculuk); şablon düzeyi doğrudan yol/yolculuk bağı yeni yazımda kabul edilmez; dönem kapasitesi ziyaret başına max promo / non-promo (varsayılan 3/3). Non-promo ürünler şablonun non-promo satırlarından. Ziyaret çözücüsü/planlama/PlannedVisit/VisitReport DEĞİŞMEZ (SB-3b/c).

Önce oku: execution/domains/commercial-suite/work-packs/WP-SB-3a-template-product-roles-journeys.md · …/DESIGN-SB-3-multi-product-visit-content.md · …/SCMM-studio-knowledge-bridge-decision.md (§9) · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{StrategyTemplate, StrategyTemplateVocabulary, ContentEngagementJourney, CycleCapacity}.cs · Application/Features/StrategyTemplate/** (Validation, Binding/*, Commands, Handlers, Mapper, Models) · Application/Features/CycleCapacity/** · Application/Features/Knowledge/Chain/ChainContext.cs (konu → birincil global-product; kullan, kopyalama) · Application/Features/Knowledge/ContentEngagementJourney/** (IContentEngagementJourneyReader) · Persistence/DependencyInjection.cs · memory mod0167-fu04-strategy-template, crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements.

NE:
 1) StrategyTemplateProductLine: Role (promo|non-promo; sabitler StrategyProductLineRoles) + JourneyId (Guid?) + JourneyCodeDisplay; class-map string-Guid.
 2) Yazma doğrulaması (taslak şablon Create/Update/CreateVersion): Role zorunlu (400 product_line_role_required); JourneyId zorunlu (400 product_line_journey_required); yolculuk tenant'ta + arşivsiz + YAYINDA (409 journey_not_published); yolculuk konusunun birincil ürünü = satır GlobalProductId (409 journey_product_mismatch); yolculuk dili şablon ülke kapsamıyla uyumsuzsa yalnız uyarı (DTO journeyWarnings).
 3) Okuma uyumluluğu: Role boş → promo; JourneyId boş → journeyMissing true; veri yazma/göç YOK; aktif/donmuş şablon değişmez (yeni sürümle düzeltilir).
 4) Şablon düzeyi ContentBindings: yeni knowledge-path ve yeni content-engagement-journey bağı kabul edilmez (409 content_binding_type_retired); mevcut bağlar okunur + DTO retired true; mevcut ziyaret çözücüsü SB-3b'ye kadar bugünkü gibi okur (davranış DEĞİŞMEZ).
 5) CycleCapacity: MaxPromoProducts + MaxNonPromoProducts (varsayılan 3/3; 1..10, 400 max_products_out_of_range; eski kayıtta 3/3); Create/Update/okuma DTO; MinutesPerVisit formülü DEĞİŞMEZ.
 6) Okuma DTO: satırda role, journeyId, journeyCode, journeyName, journeyStatus, journeyMissing, journeyWarnings; özet promo/non-promo/yolculuksuz satır sayıları.
KORU/YAPMA: ziyaret çözücüsü/planlama motoru/PlannedVisit/VisitReport DEĞİŞMEZ; Web/Platform/Auth DOKUNMA; SKU/ağırlık kuralları, segment bağları, ülke/LE/BU kapsamı DEĞİŞMEZ; aktif/donmuş şablon değiştirilmez; ham repository yazması yok; yeni alanlar class-map'e.
DOĞRULA (E2): cd C:\tmp\sb-3a; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (tabanı önce ölç; bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 399); build 0 hata. Yeni testler: rol/yolculuk zorunluluğu, journey_not_published, journey_product_mismatch, dil uyarısı, eski satır okuması (rol yok → promo, yolculuk yok → journeyMissing), knowledge-path + şablon düzeyi yolculuk bağı yeni yazımda 409 + mevcut bağ okunur retired, aktif şablon değişmez, CycleCapacity max promo/non-promo (varsayılan/aralık/eski kayıt), class-map round-trip, mevcut ziyaret çözücüsü testleri yeşil. Sabotaj: journey_product_mismatch + yolculuk zorunluluğu testleri kırmızıya dönmeli. Commit ("feat(crm): WP-SB-3a — strategy template product line role + journey, cycle capacity max promo/non-promo" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: mevcut planlama/kampanya kodu şablon düzeyi content-engagement-journey bağını YAZIYORSA ya da yolculuk ürün çözümü tek anlamlı değilse DUR + raporla.
```
