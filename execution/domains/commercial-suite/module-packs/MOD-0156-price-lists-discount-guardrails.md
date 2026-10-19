---
id: MOD-0156
name: Price Lists & Discount Guardrails
domain: commercial-suite
service: Diten.CrmService
frontend: frontend/Diten.Web
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
form_field_count: 14
runtime_code_allowed: false
flip_approved_by: ""
status: draft
owner: module-pack-author
branch: feature/crm/mod-0156-price-lists-discount-guardrails
started: 2026-10-19
target: "ready-for-dev onayından sonra CT belirler"
first_scope: "Fiyat listesi + MF / iskonto kuralları + değerlendirme API'si + ara kur tablosu ilk dilimi (WP-VW-W6 · W6-BE-b + W6-WEB-b)"
screens:
  - module: PriceLists
    data_mode: server
  - module: PricingRules
    data_mode: server
  - module: FxRates
    data_mode: server
  - module: PricingHistory
    data_mode: server
dependencies:
  - MOD-0168 (Order Capture — değerlendirme API'sinin ilk tüketicisi; paket MOD-0168-order-capture.md, draft)
  - MDM (product-master-bundle v1.1 SKU — getSku seviyesiz, bulkReadSkus, lifecycleStatus; LegalEntity CountryCode / BaseCurrencyCode; salt okuma)
  - MOD-0149 + MOD-0048 (eczane segmenti — BRD pharmacy-segment, D8; salt okuma, girdi olarak gelir)
  - MOD-0023 Workflow / Görev Merkezi (dört göz yayımlama onayı, W3-CFG deseni)
  - MOD-0277 (FX Rates Provider — ileride; W6'da ara kur tablosu)
  - MOD-0018 (RBAC — commercial.* anahtarları; tanım bu pakette, tohum / atama F-RBAC)
  - DEV-0001 (Golden Reference Compact — liste yüzeyleri)
---

# MOD-0156 — Price Lists & Discount Guardrails · ilk dilim

> **DRAFT — kod yetkisi YOK.** `status: draft` · `runtime_code_allowed: false` · `flip_approved_by` boş. Kullanıcı onaylayınca
> CT `ready-for-dev`'e çevirir. Kararlar **KİLİTLİ** (§19 / §20: DESIGN-GAP-W6 §5 D7, D8, D11, D12, D13, D14 + §3 + §7,
> WP-VW-W6 K-O1…K-O5); §20.2'deki **AÇIK** noktalar cevap bekler.
>
> **Dayanaklar:** [WP-VW-W6](../work-packs/WP-VW-W6-pharmacy-pre-order.md) · [DESIGN-GAP-W6](../work-packs/mockups/visit-workspace/DESIGN-GAP-W6-2026-10-19.md)
> (§1 ekran D, §2 kurallar, §3 farklar, §4 standartlar, §5, §6 W6-BE-b, §7) · [BRIEF-W6](../work-packs/mockups/visit-workspace/BRIEF-W6-pharmacy-pre-order.md)
> (kavram #3–#5, #9–#11, ekran D) · [ROADMAP-visit-workspace](../work-packs/ROADMAP-visit-workspace.md) K-W10 (a), (c), (d), (f), (g) ·
> mockup demo kuralları `decoded-w6/W6-on-siparis.dc.html` satır 730–732.
>
> Yetki sırası: **Blueprint 8.7** > bu paket > [domain-config](../domain-config.md) > [crm-sor-boundary](../crm-sor-boundary.md) >
> `AGENTS.md` > `.antigravity/rules/`.

---

## 0. Kimlik kapısı ve yer kararı

### 0.1 DCP-002 — GEÇTİ (2026-10-19)

```text
$ py -3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0156 --name "Price Lists & Discount Guardrails"
OK  MOD-0156: proven against Blueprint/registry.
EXIT=0
```

Kimlik Blueprint 8.7'de `MOD-0156 | Price Lists & Discount Guardrails` (Commercial Suite (CRM + O2C), CPQ & Pricing; 8.7 SoR:
*price lists, discount rules, pricing approvals*); kayıt tablosunda `reserved / planned`, sahip `commercial-suite`. Yeni MOD / FU /
CAND-CAP açılmadı. İlk dilim künyedeki `first_scope` ile sınırlıdır; Program T2 genişletir (K-W10 a).

### 0.2 Yer — `Diten.CrmService`, ayrı sınır (K-O1, D17)

Kendi klasörü (`Features/Commercial/Pricing/**`), kendi koleksiyonları (`commercial_price_*`, `commercial_pricing_*`,
`commercial_fx_rates`), kendi izin ad alanı (`commercial.*`), kendi API yolu (`/api/commercial/...`). Program T2'de taşınırsa
yalnız Gateway hedefi değişir. **"Pricing approvals"** = liste yayımlama ve kural değişikliği onayı (dört göz, D13);
**sipariş onayı MOD-0168'dedir** (DESIGN-GAP §3 "Onayın sahibi").

---

## 1. Modül özeti

### 1.1 Ne yapar (ilk dilim)

1. **Fiyat listeleri** — satan şirket (tüzel kişilik) × ülke × para birimi; tür **Resmî** / **Şirket listesi** (K-W10 adı);
   kademeler **depo fiyatı**, **eczacı alış**, **halka satış**; KDV oranı ürün başına; geçerlilik tarihleri; görünen durumlar
   **Taslak / Planlandı / Yürürlükte / Geçmiş** (+ dört göz için "Onay bekliyor"). TR resmî listesi aylık içe alınır.
2. **İçe aktarma** — dosya (Excel / CSV) → **hazırlık alanı** → önizleme (fark, eşleşmeyen, Retired) → taslak liste → yayıma
   gönder → ikinci kişi onayı → yayımlandı (D13). Para birimi alanı zorunlu (DESIGN-GAP §3).
3. **MF / iskonto kuralları** — anahtar **tüzel kişilik × SKU × isteğe bağlı eczane segmenti / dönem** (DESIGN-GAP §3, D8);
   her ölçüt için **yumuşak / sert** çift: en yüksek iskonto %, en yüksek MF oranı (10+2 yazımı), birleşik etkin indirim tavanı,
   en az / yumuşak üst / sert üst adet; **en özel kural kazanır** (D8); değişiklik dört göz (D13).
4. **Değerlendirme API'si** — süreç içi arayüz (MOD-0168 için) + kural deneme ucu: satır başına fiyat, KDV, bedava kutu, MF
   oranı, birleşik indirim, sonuç (`ok` / `soft` / `hard`) ve **gerekçe kodu + parametre**; metin istemcide 7 dilde (DESIGN-GAP §4).
5. **Kural deneme** — ürün + adet + MF + iskonto (+ şirket, segment, tarih) → "sınır içi / onay gerekir / girilemez".
6. **Değişiklik geçmişi** — kim, ne zaman, eski → yeni (liste, kalem, kural, kur).
7. **Ara kur tablosu** — tüzel kişilik × kaynak × tarih × para birimi çifti; satış operasyonu elle girer / içe aktarır (D14);
   MOD-0277 FX Rates Provider gelince yerini alır (K-W10 g).

### 1.2 Ne değildir

- Sipariş, sipariş onayı, sipariş belgesi değildir (MOD-0168).
- Teklif / CPQ yapılandırma değildir (MOD-0157 / MOD-0159); Deal Desk istisna akışı değildir (MOD-0284).
- Kampanya bağlı kural değildir — dönem şimdilik yalnız geçerlilik tarihleri; kampanya bağı (MOD-0165) sonra (DESIGN-GAP §3, D8).
- Dış kur sağlayıcı değildir (MOD-0277); muhasebe kur farkı değildir (Finance).
- Ürün ana verisi değildir; SKU MDM'dendir, burada yalnız referans + anlık görüntü.

### 1.3 Kullanıcılar (ticari ekip, BRIEF #12)

| Rol | Ne yapar |
|---|---|
| Pazarlama | liste taslağı / içe aktarma, kural taslağı / revizyon, yayıma gönderme (D13 "pazarlama düzenler") |
| Satış operasyonu | yayımlama onayı (dört göz, D13 "satış operasyonu onaylar"); kur tablosu (D14) |
| Okuyucular (yönetici, temsilci yok) | liste / kural / geçmiş okuma; kural deneme |

Temsilci bu ekranı görmez; fiyat ve kural ona MOD-0168 ekran A'da sonuç olarak gelir.

---

## 2. Sahiplik ve sınırlar

### 2.1 Kapsam içi / dışı

| Kapsam | Karar |
|---|---|
| **Kapsam içi** | `PriceList` (+ kalemler) · `PriceListImport` (hazırlık alanı) · `PricingRule` (sürümlü) · `FxRate` · `PricingChangeLog` · yayımlama / kural değişikliği dört göz (Workflow) · değerlendirme + fiyat çözümü + kur okuma arayüzleri · kural deneme · ekran D (sekmeler: Fiyat listeleri / MF-iskonto kuralları / Kural deneme / Değişiklik geçmişi / Kurlar) · izin anahtarları (tanım) · Gateway rotaları + kapsama testi |
| **Kapsam dışı** | sipariş ve sipariş onayı (MOD-0168) · teklif (MOD-0157) · Deal Desk (MOD-0284) · kampanya bağı (MOD-0165) · dış kur sağlayıcı (MOD-0277) · müşteri / sözleşme özel fiyatı, hacim kademesi, paket fiyat (Program T2) · eczane segmenti alanının yazımı (MOD-0149 / MOD-0048) |

### 2.2 SoR

| Nesne | Sahip | Bu pakette |
|---|---|---|
| **`PriceList`**, **`PriceListItem`**, **`PriceListImport`** | **MOD-0156** | **AÇILIR** (8.7: price lists) |
| **`PricingRule`** | **MOD-0156** | **AÇILIR** (8.7: discount rules) |
| **Yayımlama / değişiklik onayı** | **MOD-0156** | **AÇILIR** (8.7: pricing approvals) — Workflow görevi; karar kaydı kayıtta |
| **`FxRate`** (ara) | **MOD-0156** (MOD-0277'ye kadar) | **AÇILIR** (K-W10 g) |
| **`PricingChangeLog`** | **MOD-0156** | **AÇILIR** — yalnız ekleme |
| SKU, tüzel kişilik | MDM | salt okuma (anlık görüntü) |
| Eczane segmenti | MOD-0149 alanı + MOD-0048 seti | salt okuma (değerlendirme girdisi; AÇIK-3) |
| Sipariş, sipariş onayı | MOD-0168 | **dokunulmaz**; MOD-0168 bu modülün arayüzlerini çağırır |

### 2.3 Kalıcı yasaklar

```text
Yayımlanmış (Planlandı / Yürürlükte / Geçmiş) liste ya da kalemi yerinde değişir   ❌  yeni sürüm açılır (değişiklik = yeni liste)
Yayımlayan = gönderen (dört göz delinir)                                          ❌  D13, SoD
Yürürlükteki kural yerinde değişir                                                ❌  revizyon taslağı → onay → yeni sürüm (D13)
Değerlendirme birleştirilmiş cümle döner                                          ❌  gerekçe kodu + parametre (DESIGN-GAP §4)
Retired SKU listeye / kurala girer                                                ❌  K-O3; içe aktarmada işaretlenir, alınmaz
Kur değeri geçmişe dönük değişir ve siparişi etkiler                               ❌  sipariş kendi anlık görüntüsünü taşır (K-W10 g); kur düzeltmesi yeni satır + geçmiş
Fiyat / kural sipariş kaydına yazılır (MOD-0168 koleksiyonu)                       ❌  MOD-0168 yazar; MOD-0156 yalnız sonuç döner
Değişiklik geçmişi silinir                                                        ❌  yalnız ekleme
ocelot.json / RBAC tohumu / kayıt tablosu bu paketle yazılır                       ❌  ilan; integration-agent / F-RBAC / CT
```

---

## 3. Sahip olunan nesneler

| Katman | Nesne | Not |
|---|---|---|
| **Kök** | `PriceList` (`EntityBase`) + `IPriceListRepository` | §4.1 |
| **Kök (ayrı koleksiyon)** | `PriceListItem` (`EntityBase`, `PriceListId` ile) + depo | büyük listeler (TR resmî liste binlerce satır) belgeye gömülmez; yayımlandıktan sonra değişmez |
| **Kök** | `PriceListImport` (`EntityBase`) + satırları (`PriceListImportRow`, ayrı koleksiyon) | hazırlık alanı §4.2 |
| **Kök** | `PricingRule` (`EntityBase`, sürümlü) + `IPricingRuleRepository` | §4.3 |
| **Kök** | `FxRate` (`EntityBase`) + `IFxRateRepository` | §4.5 |
| **Kök** | `PricingChangeLog` (`EntityBase`, yalnız ekleme) | §4.7 |
| **Komutlar** | `CreatePriceListCommand` · `UpdatePriceListCommand` (taslak; kalem ekle / çıkar / değiştir dahil) · `CreatePriceListVersionCommand` (yayımlanmış listeden yeni taslak) · `DiscardPriceListCommand` (taslak) · `SubmitPriceListCommand` · `DecidePriceListCommand` (`publish` \| `return`) · `UploadPriceListImportCommand` · `ConvertPriceListImportCommand` (hazırlık → taslak) · `DiscardPriceListImportCommand` · `CreatePricingRuleCommand` · `UpdatePricingRuleCommand` (taslak ya da revizyon taslağı) · `RequestPricingRuleRetirementCommand` · `DiscardPricingRuleDraftCommand` · `SubmitPricingRuleCommand` · `DecidePricingRuleCommand` (`publish` \| `return`) · `CreateFxRateCommand` · `UpdateFxRateCommand` · `ImportFxRatesCommand` | her biri §21'de |
| **Sorgular** | `GetPriceListListQuery` · `GetPriceListByIdQuery` · `GetPriceListItemsQuery` (sayfalı) · `GetPriceListImportQuery` (önizleme + fark) · `GetPricingRuleListQuery` · `GetPricingRuleByIdQuery` · `EvaluatePricingTrialQuery` (kural deneme) · `GetFxRateListQuery` · `GetPricingChangeLogQuery` · `GetPricingContractQuery` (sözlük: türler, kademeler, gerekçe kodları) | |
| **Süreç içi arayüzler (MOD-0168 tüketir)** | `IPriceResolver` · `IPricingEvaluator` · `IFxRateReader` | §4.6; `Features/Commercial/Pricing/Contracts/` altında; imza değişikliği MOD-0168 ile birlikte |
| **Sözlük** | `PriceListKind` (`official` \| `company`) · `PriceTier` (`depot` \| `pharmacist-purchase` \| `public`) · `PriceListStatus` · `PriceListPhase` (türetilir) · `PricingRuleStatus` · `PricingOutcome` (`ok` \| `soft` \| `hard`) · `PricingReasonCodes` (§4.6.3) · `FxRateSource` (serbest metin + `manual` \| `import`) | kapalı küme; küme dışı → 400 |
| **Uçlar** | `PriceListsController` · `PriceListImportsController` · `PricingRulesController` · `FxRatesController` · `PricingHistoryController` | §15 |
| **İzinler** | `commercial.price-lists.*`, `commercial.pricing-rules.*`, `commercial.fx-rates.*` — **YALNIZ TANIM** | §14 |
| **Workflow** | objectType `commercial.price-list` ve `commercial.pricing-rule`, şablon kodu ayarı `Commercial:Pricing:Workflow:TemplateCode` = `COM-PRICING-PUBLISH` | §4.4; canlı şablon W6-CFG |
| **Ön yüz** | ekran D: `Views/Commercial/Pricing/` sekme başlığı + `PriceLists` / `PricingRules` / `FxRates` / `PricingHistory` Compact setleri + `RuleTrial` | §9, §11 |

---

## 4. Varlık alanları ve normatif kurallar

> Hepsi `EntityBase`: `TenantId` sunucuda, kiracılar arası 404, soft delete, `Version` (bayat → 409). Para `decimal`. Takvim
> alanları `DateOnly`; iki `DateTimeOffset` birlikte sıralanmaz (CRM paralel dizi 500). Sınıf eşlemesi zorunlu.

### 4.1 `PriceList` (koleksiyon `commercial_price_lists`) + `PriceListItem` (`commercial_price_list_items`)

| Alan | Tür | Kural |
|---|---|---|
| `Name` | string | zorunlu, ≤ 120 (ör. "TR Resmî Fiyat Listesi · Ekim 2026") |
| `LegalEntityId` (+ ad, kısa kod anlık görüntüsü) | Guid | MDM tüzel kişiliği (satan şirket; RO şirketleri dahil — K-W10 e) |
| `CountryCode` | string | tüzel kişiliğin ülkesi (MDM `LegalEntity.CountryCode`); elle seçilmez |
| `Currency` | string | ISO 4217, zorunlu (DESIGN-GAP §3: içe aktarmada para birimi alanı) — TR resmî liste TRY; diğer şirketler EUR / USD vb. (K-W10 g) |
| `Kind` | string | `official` (Resmî) \| `company` (Şirket listesi) |
| `ValidFrom` | DateOnly | zorunlu |
| `ValidTo` | DateOnly? | isteğe bağlı; aynı (tüzel kişilik, ülke) için sonraki yayımlanmış liste `ValidFrom - 1` ile örtük olarak bitirir (§4.1.2) |
| `Status` | string | `draft` → `in_review` → `published`; `in_review` → `draft` (iade); `draft` → `discarded` |
| `Phase` (türetilir, `published`'da) | string | `planned` (ValidFrom > bugün) · `in-force` · `past` — **okuma anında**; planlanan liste tarihinde kendiliğinden yürürlüğe girer, zamanlayıcı yok (DESIGN-GAP §2) |
| `ListVersion` | int | aynı ad ailesinde 1, 2, … (`CreatePriceListVersion`) |
| `PreviousPriceListId` | Guid? | yeni sürümün kaynağı (fark için) |
| `SourceImportId` | Guid? | içe aktarmadan geldiyse |
| `Review` | `{ SubmittedBy, SubmittedAt, DecidedBy?, DecidedAt?, Decision?, Reason?, WorkflowInstanceId }` | dört göz kaydı (D13) |
| `ItemCount` | int | özet |

**`PriceListItem`:** `PriceListId`, `SkuId` + `SkuLevel` (sunucu çözer) + anlık görüntü (ad, form, kutu adedi, GTIN, kod),
`DepotPrice`, `PharmacistPurchasePrice`, `PublicPrice` (her biri `decimal?` ≥ 0; en az biri dolu), `VatRate` (0 ≤ x ≤ 100).
Tekillik: (`PriceListId`, `SkuId`). Liste `published` olunca kalemler değişmez.

#### 4.1.1 Görünen durum eşlemesi (ekran D rozetleri)

| Rozet | Kaynak |
|---|---|
| Taslak | `Status = draft` |
| Onay bekliyor | `Status = in_review` |
| Planlandı | `published` + `Phase = planned` |
| Yürürlükte | `published` + `Phase = in-force` |
| Geçmiş | `published` + `Phase = past` |

#### 4.1.2 Yürürlük kuralı (fiyat çözümünün temeli)

- Belirli bir tarihte **(tüzel kişilik, ülke) başına en çok bir yürürlükte liste** vardır (§20.2 AÇIK-1 onayına bağlı). Yeni bir
  liste yayımlanınca aynı (tüzel kişilik, ülke) için `ValidFrom`'u daha erken olan liste o tarihten itibaren `past` olur ("eski
  liste geçmişte kalır", BRIEF ekran D).
- Aynı (tüzel kişilik, ülke, `ValidFrom`) ile ikinci liste yayımlanamaz → 409 `price_list_overlap`.
- Fiyatlı / adetli mod (K-O4, K-W10 f): `IPriceResolver` o tarihte yürürlükte liste bulursa `priced`, bulamazsa `quantity`.
- Listede olmayan SKU fiyatlı modda satıra girmez: bulgu `price.sku-not-in-list` (sert).

### 4.2 `PriceListImport` — hazırlık alanı (koleksiyon `commercial_price_list_imports` + `commercial_price_list_import_rows`)

```text
① Yükle      UploadPriceListImportCommand: dosya (xlsx / csv, ≤ 5 MB, ≤ 10.000 satır) + tüzel kişilik + para birimi + tür
             + ValidFrom + sütun eşlemesi (GTIN ya da SKU kodu · depo · eczacı alış · halka satış · KDV)
② Eşleştir   her satır: SKU = GTIN (FinishedGood satış kutusu) ya da kod (LS- / FG-) ile MDM'de aranır (bulkReadSkus / arama)
             → matched · unmatched · retired (Retired işaretlenir, ALINMAZ) · duplicate · invalid-price
③ Fark       aynı (tüzel kişilik, ülke) için yürürlükteki (yoksa son yayımlanmış) listeyle: eklenen · çıkan · fiyatı değişen
             (kademe başına eski → yeni, % fark) · KDV'si değişen
④ Önizleme   ekran D içe aktarma 2. adım: sayılar + satır tablosu (süzgeç: eşleşmeyen / retired / değişen)
⑤ Dönüştür   ConvertPriceListImportCommand → yalnız matched satırlarla `draft` PriceList (SourceImportId)
             unmatched / retired satırlar taslağa girmez; önizlemede kalır
⑥ Yayımla    SubmitPriceListCommand → in_review → (ikinci kişi) DecidePriceListCommand publish → published  (D13)
```

- Hazırlık kaydı `uploaded` → `validated` → `converted` \| `discarded`; 30 gün sonra satırları temizlenebilir (özet kalır).
- Dosya içeriği kalıcı ikili depoya yazılmaz; yalnız ayrıştırılmış satırlar + dosya adı / özeti tutulur.

### 4.3 `PricingRule` (koleksiyon `commercial_pricing_rules`; sürümlü)

| Alan | Tür | Kural |
|---|---|---|
| `RuleKey` | Guid | kuralın kimliği (sürümler boyunca sabit) |
| `RuleVersion` | int | 1, 2, … |
| `LegalEntityId` | Guid | zorunlu (DESIGN-GAP §3: kural formunda tüzel kişilik alanı) |
| `SkuId` (+ anlık görüntü) | Guid | zorunlu; `Retired` olamaz |
| `SegmentCode` | string? | BRD `pharmacy-segment` değer kodu (D8); boş = tüm eczaneler |
| `ValidFrom` / `ValidTo` | DateOnly? | "dönem" = geçerlilik tarihleri (D8); ikisi de boş = süresiz |
| `MaxDiscountPct` | `{ Soft?, Hard? }` | 0–100, 1 ondalık; Soft ≤ Hard |
| `MaxMf` | `{ Soft? (a+b), Hard? (a+b) }` | "10+2" saklanır + oran b / (a + b) türetilir; oran(Soft) ≤ oran(Hard) |
| `MaxCombinedPct` | `{ Soft?, Hard? }` | 0–100; Soft ≤ Hard |
| `Quantity` | `{ Min?, SoftMax?, HardMax? }` | tam sayı; Min ≤ SoftMax ≤ HardMax |
| `Status` | string | `draft` → `in_review` → `active`; `in_review` → `draft` (iade); yeni sürüm `active` olunca önceki `superseded`; emekliye ayırma talebi onaylanınca `retired` |
| `ChangeKind` | string | `create` \| `revise` \| `retire` (dört göz talebinin türü) |
| `Review` | MOD-0156 §4.1 ile aynı biçim | D13 |

- En az bir ölçüt dolu olmalı. Demo değerleri (TUT, ALM, GLU, URO; "Zincir eczane" segmenti) testlerde örnek veri (DESIGN-GAP §2).
- **Çakışma:** aynı (tüzel kişilik, SKU, segment) için aynı **özgüllükte** tarih aralığı kesişen iki `active` kural olamaz →
  yayımlamada 409 `pricing_rule_overlap`.

### 4.4 Dört göz — yayımlama ve kural değişikliği (D13, "pricing approvals")

```text
Submit{PriceList|PricingRule}Command  (pazarlama; commercial.*.submit)
  └─ Workflow örneği objectType commercial.price-list | commercial.pricing-rule, şablon COM-PRICING-PUBLISH
     (aday = satış operasyonu pozisyonu — W3-CFG deseni; canlı şablon W6-CFG)
Decide{PriceList|PricingRule}Command  (satış operasyonu; commercial.*.publish)
  - SoD: gönderen karar veremez (403); Workflow WithoutStarter + komut denetimi
  - publish → published / active (öncekini past / superseded yapar)
  - return (gerekçe zorunlu) → draft
  - ilk karar geçerli; ikinci karar 409 (durum + Version koşulu)
```

Görev Merkezi derin bağlantısı: `/Commercial/Pricing/PriceLists/Details/{id}` ve `/Commercial/Pricing/PricingRules/Details/{id}`.
Kur tablosu dört gözsüz (D14: satış operasyonu doğrudan girer), değişikliği geçmişe yazılır.

### 4.5 `FxRate` — ara kur tablosu (koleksiyon `commercial_fx_rates`; K-W10 g, D14)

| Alan | Kural |
|---|---|
| `LegalEntityId` | zorunlu (kur tüzel kişilik başına) |
| `FromCurrency` / `ToCurrency` | ISO 4217; farklı; ör. USD → AED, EUR → KGS |
| `RateDate` | DateOnly |
| `Rate` | `decimal` > 0, en çok 6 ondalık (ör. 3,6725) |
| `Source` | zorunlu metin ≤ 60 (ör. "Merkez Bankası", "manual") — belgede gösterilir (DESIGN-GAP §3) |
| Tekillik | (`LegalEntityId`, `FromCurrency`, `ToCurrency`, `RateDate`) başına bir kur |
| Okuma kuralı | `IFxRateReader.GetAsync(le, from, to, asOf)` → `RateDate ≤ asOf` olan **en son** kur + tarih + kaynak; yoksa null (sipariş yine verilir, yerel karşılık gösterilmez) |
| Düzeltme | `UpdateFxRateCommand` aynı satırı değiştirir; eski değer geçmişe yazılır. Gönderilmiş siparişler kendi anlık görüntüsünü korur |
| Geçiş | MOD-0277 gelince okuyucu sağlayıcıya bağlanır; tablo elle düzeltme için kalabilir (T2 kararı) |

### 4.6 Değerlendirme API'si (normatif)

#### 4.6.1 Süreç içi arayüzler

```text
IPriceResolver.ResolveAsync(tenantId, legalEntityId, countryCode, asOf, tier, skuIds[])
  → { Mode: priced | quantity, PriceList?: { Id, Version, Name, Kind, Currency, ValidFrom },
      Items: [{ SkuId, UnitPrice?, VatRate?, Found }] }

IPricingEvaluator.EvaluateAsync(EvaluationRequest)
  EvaluationRequest  = { TenantId, LegalEntityId, CountryCode, AsOf, Tier, PharmacySegmentCode?,
                         Lines: [{ LineNo, SkuId, Quantity, Mf?: { Buy, Free }, DiscountPct? }] }
  EvaluationResult   = { Mode, Currency?, PriceList?, Outcome (en kötü satır),
                         Lines: [{ LineNo, SkuId, Outcome, UnitPrice?, VatRate?, NetAmount?, VatAmount?,
                                   FreeQuantity, MfRatioPct?, CombinedDiscountPct?,
                                   Findings: [{ Code, Severity (soft|hard), Params{} }],
                                   Rule?: { RuleKey, RuleVersion, Specificity } }],
                         Totals?: { Net, Vat, Gross, EffectiveDiscountPct },
                         RuleSetFingerprint }

IFxRateReader.GetAsync(tenantId, legalEntityId, from, to, asOf) → { Rate, RateDate, Source } | null
```

Kademe (`Tier`) çağıran tarafından verilir (MOD-0168 ülke ayarı; D11). Arayüzler saf okumadır; yazmaz, yan etkisizdir.

#### 4.6.2 Hesap kuralları (DESIGN-GAP §2, D7, D8)

| Kural | Tanım |
|---|---|
| MF oranı | a + b → b / (a + b); 10+2 → %16,7 (D7) |
| Bedava kutu | floor(adet / a) × b (D7) |
| Birleşik indirim | 1 − (1 − iskonto) × (1 − MF oranı), yüzde olarak 0,1'e yuvarlanır; **yalnız fiyatlı modda** denetlenir |
| Yüzde yuvarlama | bütün yüzdeler 0,1 (D7) |
| İskonto | iskonto > Hard → sert; > Soft → yumuşak (yalnız fiyatlı mod) |
| MF | satır oranı > oran(Hard) → sert; > oran(Soft) → yumuşak |
| Adet | adet < Min → sert; > HardMax → sert; > SoftMax → yumuşak |
| Satır sonucu | satırdaki en kötü bulgu; bulgu yoksa `ok`. Kural yoksa `ok` (sınır yok) |
| Sipariş sonucu | satırların en kötüsü |
| Birim fiyat | yürürlükteki listenin `Tier` kademesi (varsayılan **eczacı alış**, D11) |
| Net | adet × birim fiyat × (1 − iskonto); bedava kutu tutara girmez |
| KDV | net × KDV oranı (ülke / ürün; TR %10, BAE %0 örnek) |
| Para yuvarlama | para biriminin küsuratına, `MidpointRounding.AwayFromZero`, satır düzeyinde |
| Etkin indirim (sipariş) | 1 − Σ net / Σ (birim fiyat × (adet + bedava kutu)) — 0,1 |
| Adetli mod | fiyat, KDV, iskonto, birleşik yok; yalnız MF ve adet kuralları (K-W10 f) |

**En özel kural (D8):** aday kurallar = aynı tüzel kişilik + SKU, `active`, `AsOf` geçerlilik içinde, segmenti boş ya da eczane
segmentine eşit. Özgüllük puanı: **segment + dönem (3) > segment (2) > dönem (1) > genel (0)** — D8'in "segment + dönem >
segment > genel" sırası; "yalnız dönem"in yeri bu paketin yorumudur (§20.2 AÇIK-4). En yüksek puanlı kural kazanır; aynı
puanda çakışma yayımlamada engellenmiştir (§4.3).

#### 4.6.3 Gerekçe kodları (metin YOK; istemci resx'te 7 dil)

| Kod | Önem | Parametreler |
|---|---|---|
| `discount.soft-exceeded` / `discount.hard-exceeded` | soft / hard | `value`, `limit` |
| `mf.soft-exceeded` / `mf.hard-exceeded` | soft / hard | `value` (a+b), `valuePct`, `limit` (a+b), `limitPct` |
| `combined.soft-exceeded` / `combined.hard-exceeded` | soft / hard | `valuePct`, `limitPct` |
| `quantity.below-min` | hard | `value`, `min` |
| `quantity.soft-exceeded` / `quantity.hard-exceeded` | soft / hard | `value`, `limit` |
| `sku.retired` | hard | `skuId` |
| `price.sku-not-in-list` | hard | `skuId`, `priceListName` |
| `discount.not-allowed-quantity-mode` | hard | — |

Rozet metinleri ("Sınır içi / Onay gerekir / Girilemez") ve iletiler ("Yönetici onayı gerekir: iskonto %12 > sınır %10") resx'te
yer tutucularla; sayılar kullanıcı kültürüyle biçimlenir.

### 4.7 `PricingChangeLog` (koleksiyon `commercial_pricing_change_log`)

`{ EntityType (price-list | price-list-item | pricing-rule | fx-rate), EntityId, EntityVersion, Action, ByUserId, At,
Changes: [{ Field, Old, New }] }` — yalnız ekleme; ekran D "Değişiklik geçmişi" sekmesinin kaynağı (kim, ne zaman, eski → yeni).
Kalem farkları liste yayımlanınca toplu satır olarak yazılır (içe aktarma farkından).

---

## 5. Repo kapsamı (paket onaylanınca)

```text
── services/Diten.CrmService/ ──
src/Diten.CrmService.Domain/Entities/Commercial/{PriceList,PriceListItem,PriceListImport,PricingRule,FxRate,PricingChangeLog}.cs (YENİ)
src/Diten.CrmService.Domain/Repositories/Commercial/I{…}Repository.cs                                                         (YENİ)
src/Diten.CrmService.Application/Features/Commercial/Pricing/
├── Contracts/{IPriceResolver,IPricingEvaluator,IFxRateReader}.cs + istek / sonuç tipleri   (MOD-0168'in tükettiği yüzey)
├── PriceLists/ · PriceListImports/ · PricingRules/ · FxRates/ · PricingHistory/
│   └── her biri Commands/ · Queries/ · Handlers/{CommandHandlers,QueryHandlers}/ · Validators/ · {Alt}Models.cs
├── Evaluation/PricingEvaluator.cs · PriceResolver.cs · RuleSpecificity.cs   (hesap TEK yerde)
├── PricingPermissions.cs · PricingReasonCodes.cs
src/Diten.CrmService.Infrastructure/Persistence/Commercial/Pricing/**     (YENİ — depolar, sınıf eşlemesi, indeksler)
src/Diten.CrmService.Infrastructure/Commercial/PriceListFileParser.cs     (YENİ — xlsx / csv)
src/Diten.CrmService.Api/Controllers/Commercial/{PriceLists,PriceListImports,PricingRules,FxRates,PricingHistory}Controller.cs (YENİ)
tests/Diten.CrmService.Application.Tests/Commercial/Pricing/**            (YENİ)

── services/Diten.Platform/ ── (yalnız CrmManifestProvider satırları: menü sayfası + izin anahtarları)

── frontend/Diten.Web/ ──
Controllers/Commercial/CommercialPricingController.cs        (aynı köken vekil)
Views/Commercial/Pricing/{Index,_PricingTabs,RuleTrial}.cshtml          (_TabCard başlığı + kural deneme)
Views/Commercial/PriceLists/{Index,Create,Edit,Details,_Form,_Filter,_DataTable,_IndexL10n,PriceListsIndex.cs,Import}.cshtml
Views/Commercial/PricingRules/{Index,Create,Edit,Details,_Form,_Filter,_DataTable,_IndexL10n,PricingRulesIndex.cs}
Views/Commercial/FxRates/{Index,Create,Edit,Details,_Form,_Filter,_DataTable,_IndexL10n,FxRatesIndex.cs}
Views/Commercial/PricingHistory/{Index,_Filter,_DataTable,_IndexL10n,PricingHistoryIndex.cs}
wwwroot/assets/js/Commercial/{Pricing,PriceLists,PricingRules,FxRates,PricingHistory}/**
Resources/Views/Commercial/{Pricing,PriceLists,PricingRules,FxRates,PricingHistory}/*.{ar,en,es,fr,ru,tr,zh}.resx

── gateway/ ── (İLAN — integration-agent)
gateway/Diten.ApiGateway/ocelot.json · gateway/Diten.ApiGateway.Tests/VwW6PricingRouteCoverageTests.cs

── Bu paket ──
execution/domains/commercial-suite/module-packs/MOD-0156-price-lists-discount-guardrails.md
```

---

## 6. Korunan yollar

| Yol | Neden |
|---|---|
| `.antigravity/**` | küresel sistem |
| `services/Diten.CrmService/**/Features/Commercial/PreOrders/**` | MOD-0168; bu modül onu çağırmaz, o bunu çağırır |
| `services/Diten.CrmService/**/Features/{Visit*,Account,Contact,Territory,Segment*,Campaign*}/**` | CRM modülleri; git diff ∅ |
| `services/Diten.MdmService/**`, `services/Diten.Platform/**` (manifest satırı dışında) | başka domain |
| `gateway/**/ocelot.json` | integration-agent |
| RBAC tohumu, `execution/registries/**` | F-RBAC / CT |
| `_Layout*.cshtml`, `Archive/**` | DONUK |

---

## 7. Bağımlılıklar

| Bağımlılık | Yön | Durum | Not |
|---|---|---|---|
| **MDM** product-master v1.1 | salt okuma (Gateway) | main'de | `bulkReadSkus` (≤ 200 / çağrı), `searchStockableItems`, `getSku`; GTIN ile arama v1.2'de (BL-584 W-04) → içe aktarmada GTIN eşleşmesi bugün toplu okuma + arama ile; büyük listede parçalı (§20.2 AÇIK-5) |
| **MDM** LegalEntity | salt okuma | main'de | ülke, para birimi, ad |
| **MOD-0149 / MOD-0048** eczane segmenti | girdi | **yok** | AÇIK-3 |
| **MOD-0023** Workflow | çağrı + geri çağrı | shipped | şablon `COM-PRICING-PUBLISH` (W6-CFG); aday = satış operasyonu pozisyonu |
| **MOD-0168** | tüketici | draft paket | arayüz imzası iki pakette aynı (§4.6.1) |
| **MOD-0277** FX Rates Provider | ileride | Blueprint'te, repoda yok | ara tablo |
| **AUD-001** CRM izi | — | `aday` | §21, AÇIK-2 |
| **DEV-0001** Golden Compact | şablon | shipped | liste yüzeyleri |

---

## 8. Çalışma zamanı kısıtları

- Kiracı süzgeci her sorguda; kiracılar arası 404.
- Değerlendirme **saf ve deterministik**: aynı girdi + aynı yayımlanmış veri → aynı sonuç; `RuleSetFingerprint` (kullanılan
  liste sürümü + kural sürümleri özeti) sonuca yazılır, sipariş bunu saklar.
- Performans: 50 satırlık değerlendirme tek liste okuması + tek kural sorgusu (SKU kümesiyle) — satır başına sorgu yok;
  indeksler: `commercial_price_lists (TenantId, LegalEntityId, CountryCode, Status, ValidFrom)`,
  `commercial_price_list_items (TenantId, PriceListId, SkuId)` tekil, `commercial_pricing_rules (TenantId, LegalEntityId, SkuId, Status)`,
  `commercial_fx_rates (TenantId, LegalEntityId, FromCurrency, ToCurrency, RateDate)` tekil. `$ne` kısmi indeks yok.
- Yayımlama (önceki listeyi / kural sürümünü kapatma + yenisini açma) çok belgeli → `SupportsTransactionsAsync` korumalı işlem
  + telafi.
- "Bugün" = kiracı saat dilimindeki tarih; `TimeProvider` enjekte.
- İçe aktarma ayrıştırması istek içinde (≤ 10.000 satır); MDM eşleşmesi 200'lük parçalarla.

---

## 9. Yerleşim ve kabuk sözleşmesi (ekran D)

`shell: tenant` ⇒ her sayfada açıkça `Layout = "_LayoutTenantShell";`.

- **Sekme başlığı:** ortak `_TabCard` (wcn-tabcard pills; DESIGN-GAP §4) — **Fiyat listeleri · MF-iskonto kuralları · Kural
  deneme · Değişiklik geçmişi · Kurlar**. Her sekme kendi sayfasıdır (aynı `_TabCard` başlığını paylaşır); alt çizgili mockup
  sekmeleri alınmaz.
- **Listeler** (Fiyat listeleri, kurallar, kurlar, geçmiş): **Golden Compact DataTable** (`data-dt-standard="v2"`,
  `data-dt-data-mode="server"`), `_Filter` kartı (tüzel kişilik, ülke, para birimi, tür, durum; kurallarda SKU + segment +
  tüzel kişilik), Görünümü Kaydet, iskelet, standart birincil düğme ("Yeni liste" / "İçe aktar" / "Yeni kural" / "Yeni kur"),
  hızlı görünüm offcanvas. Fiyat listesi sütunları: ad, **satan şirket**, **ülke**, **para birimi**, tür, geçerlilik, durum
  rozeti, kalem sayısı (BRIEF #9).
- **Liste ayrıntısı:** kalemler tablosu (SKU, barkod, **depo fiyatı / eczacı alış / halka satış**, KDV) + önceki sürüme göre fark.
- **İçe aktarma:** 3 adım (dosya → önizleme: fark, eşleşmeyen, retired → yayıma gönder) — `PriceLists/Import.cshtml`.
- **Kural formu:** tüzel kişilik, SKU, segment, geçerlilik, yumuşak / sert çiftleri; MF girişinde "10+2" ve yanında etkin %.
- **Kural deneme:** ürün + adet + MF + iskonto (+ şirket, segment, tarih) → sonuç rozeti + gerekçe iletileri + uygulanan kural.
- **Kurlar:** tüzel kişilik, çift, tarih, kur, kaynak.
- Standartlar: Golden başlık `div.mb-3 > h5.mb-0 + p.mb-0.text-muted` · `.diten-field` simgeli alanlar · `--bs-primary` (sabit
  lacivert yok) · RTL mantıksal özellikler · sayı / tarih kültürle · menü manifestten · masaüstü öncelikli, tablette bozulmaz ·
  yetkisiz iskelet yok (UAS-001).
- **Menü anahtarı:** `Nav.Page.COMMERCIALPRICING` (tek menü öğesi; sekmeler sayfa içinde).

---

## 10. Backend dosya düzeni

Golden Compact birebir, alt modül başına: `Features/Commercial/Pricing/{PriceLists,PriceListImports,PricingRules,FxRates,PricingHistory}/
{Commands,Queries,Handlers/CommandHandlers,Handlers/QueryHandlers,Validators}` + alt modül başına tek `{Alt}Models.cs`.
Adlandırma: `{Fiil}{Varlık}Command` (record), `Get{…}Query` (record), `{Fiil}{Varlık}Handler` (sonek yok),
`{Fiil}{Varlık}Validator` (sonek yok). Hesap yalnız `Evaluation/` altında (iki kopya yasak; MOD-0168 dahil).
`BulkDelete`: **uygulanmaz** — yayımlanmış kayıt silinmez (geçmiş + siparişlerin anlık görüntü kaynağı); taslaklar tek tek
atılır. `Response<T>` + `CustomBaseController`; dört pipeline davranışı.

---

## 11. Ön yüz dosya sözleşmesi

§5'teki Compact setleri. Compact'ta `_CreateEditOffcanvas.cshtml` **YASAK**. Kural iletileri `Rule.{code}` anahtarlarıyla
**7 dilde** (bu modülün resx'i; MOD-0168 aynı kodları kendi resx'inde taşır ya da paylaşılan `Commercial` kaynağı — W6-WEB-a /
W6-WEB-b ortak kararı, tek kaynak tercih edilir). Liste ekranına dokunan iş `verify_datatable_page.py --area Commercial --module
{PriceLists|PricingRules|FxRates|PricingHistory} --format gaps` koşturur, sapmaları numaralı listeyle sorar. Sayfa yardımı
(UXH-001): `COM-PRICELIST-LIST`, `COM-PRICELIST-FORM`, `COM-PRICELIST-IMPORT`, `COM-PRICINGRULE-LIST`, `COM-PRICINGRULE-FORM`,
`COM-PRICING-TRIAL`, `COM-PRICING-HISTORY`, `COM-FXRATE-LIST` — 7 dil; tur **yok**. `.resx` değişikliği tam fleet yeniden başlatma
ister.

---

## 12. Durum makineleri

```text
PriceList:    draft ──submit──► in_review ──publish (ikinci kişi)──► published  [phase: planned → in-force → past, okuma anında]
                 ▲                  │
                 └────return (gerekçe)┘
              draft ──discard──► discarded
              published ──(yeni sürüm)──► yeni draft (kaynak liste değişmez)

PricingRule:  draft ──submit──► in_review ──publish──► active ──(yeni sürüm active olunca)──► superseded
                 ▲                  │                    │
                 └────return────────┘                    └─(retire talebi onaylanınca)──► retired
              revizyon / emekliye ayırma = aynı RuleKey'de yeni sürüm taslağı (ChangeKind = revise | retire)

PriceListImport: uploaded ──► validated ──► converted | discarded
FxRate:          (durumsuz) oluştur / düzelt — geçmişe yazılır (D14, dört göz yok)
```

- Yayımlanmış liste ve `active` kural yerinde değişmez; ters geçiş yalnız `in_review → draft` (iade).
- Her geçiş `PricingChangeLog`'a yazılır.

---

## 13. Doğrulama kuralları ve hata yolları

### 13.1 Alan doğrulama

| Alan | Kural | Hata |
|---|---|---|
| Liste `Name` | zorunlu, ≤ 120 | 400 |
| `LegalEntityId` | MDM'de var ve kiracının | 400 `pricing_legal_entity_invalid` |
| `Currency` | ISO 4217 3 harf, zorunlu | 400 |
| `Kind` / `Tier` | kapalı küme | 400 |
| `ValidFrom` / `ValidTo` | `ValidTo ≥ ValidFrom` | 400 |
| Kalem fiyatları | ≥ 0, en az bir kademe dolu, en çok 4 ondalık | 400 `price_list_item_invalid` |
| Kalem `VatRate` | 0–100 | 400 |
| Kalem `SkuId` | MDM'de var, `Retired` değil; listede tekil | 400 `pricing_sku_retired` / 409 `price_list_item_duplicate` |
| İçe aktarma dosyası | xlsx / csv, ≤ 5 MB, ≤ 10.000 satır, eşleme sütunları dolu | 400 `price_list_import_invalid` |
| Kural ölçütleri | en az biri dolu; Soft ≤ Hard; Min ≤ SoftMax ≤ HardMax; yüzdeler 0–100; MF a, b ≥ 1 | 400 `pricing_rule_limits_invalid` |
| Kural `SegmentCode` | yayımlanmış BRD `pharmacy-segment` değeri (set yoksa alan kapalı) | 400 |
| Kur `Rate` | > 0, ≤ 6 ondalık; çift farklı | 400 |
| Karar gerekçesi (iade) | zorunlu, ≤ 500 | 400 |

### 13.2 Hata yolları

| Senaryo | Beklenen |
|---|---|
| Gönderen kendi listesini / kuralını yayımlar | 403 (SoD, D13) |
| İki karar aynı anda | ilki geçerli; ikincisi 409 |
| Aynı (tüzel kişilik, ülke, ValidFrom) ikinci liste yayımı | 409 `price_list_overlap` |
| Aynı özgüllükte kesişen iki `active` kural | 409 `pricing_rule_overlap` |
| Yayımlanmış listeyi / kalemi düzenleme | 409 `price_list_not_editable` |
| `active` kuralı yerinde düzenleme | 409; yalnız revizyon taslağı |
| İçe aktarmada eşleşmeyen / retired satır | önizlemede sayılır; taslağa girmez |
| Aynı (tüzel kişilik, çift, tarih) ikinci kur | 409 `fx_rate_duplicate` |
| Yürürlükte liste yokken değerlendirme | `Mode = quantity`; fiyat bulgusu üretilmez |
| Listede olmayan SKU (fiyatlı mod) | satır `hard` + `price.sku-not-in-list` |
| Bayat `Version` | 409 |
| Kiracılar arası kimlik | 404 |
| Yetkisiz kullanıcı ekran D | iskelet yok, tek açıklama (UAS-001) |

---

## 14. Yetki sözleşmesi

PKS-001 (3 segment, lowercase-dotted), ad alanı `commercial.*` (K-O2). Aktör `tenant_user`. Her uç `[HasPermission]`.

| Anahtar | Katman | Ayrım | Korur |
|---|---|---|---|
| `commercial.price-lists.read` | T1 | okuma | liste, ayrıntı, kalemler, fark, geçmiş (liste) |
| `commercial.price-lists.create` | T1 | yazma | yeni taslak, yeni sürüm, taslağı atma |
| `commercial.price-lists.update` | T1 | yazma | taslak düzenleme (kalemler dahil) |
| `commercial.price-lists.import` | T2 | yazma | dosya yükleme, hazırlık alanı, taslağa dönüştürme |
| `commercial.price-lists.submit` | T3 | yazma | yayıma gönderme |
| `commercial.price-lists.publish` | T3 | **yayımlama** (dört göz onayı) | yayımla / iade |
| `commercial.price-lists.export` | T2 | okuma | Excel |
| `commercial.pricing-rules.read` | T1 | okuma | kural listesi, ayrıntı, **kural deneme**, geçmiş (kural) |
| `commercial.pricing-rules.create` | T1 | yazma | yeni kural taslağı |
| `commercial.pricing-rules.update` | T1 | yazma | revizyon / emekliye ayırma taslağı, taslağı atma |
| `commercial.pricing-rules.submit` | T3 | yazma | onaya gönderme |
| `commercial.pricing-rules.publish` | T3 | **onay** (dört göz) | yürürlüğe al / iade |
| `commercial.fx-rates.read` | T1 | okuma | kur listesi, geçmiş |
| `commercial.fx-rates.create` | T1 | yazma | kur girişi (D14) |
| `commercial.fx-rates.update` | T1 | yazma | kur düzeltme |
| `commercial.fx-rates.import` | T2 | yazma | kur içe aktarma |

- Rol önerisi (tohum değil): Pazarlama = price-lists read/create/update/import/submit/export + pricing-rules read/create/update/submit;
  Satış operasyonu = price-lists read/publish/export + pricing-rules read/publish + fx-rates read/create/update/import.
- Süreç içi arayüzler (§4.6.1) izin denetimi yapmaz; çağıran uç (MOD-0168) kendi izniyle korunur.
- Tohum / atama yok → F-RBAC (W6-CFG grant betiği).

---

## 15. Gateway / API rota kararı

**Karar: Gateway değişikliği GEREKLİ.** Her BE istemi kendi rotalarını + rota kapsama satırlarını içerir (W3L / W5 dersi: **yeni uç =
ocelot yöntemi + kapsama satırı aynı iste**). OPTIONS dahil; gövdesiz 204'te vekil `IsBodilessStatus`.

```text
GET  /api/commercial/price-lists                         read
POST /api/commercial/price-lists                         create
GET  /api/commercial/price-lists/{id}                    read
PUT  /api/commercial/price-lists/{id}                    update
GET  /api/commercial/price-lists/{id}/items              read      (sayfalı; ?diff=previous)
POST /api/commercial/price-lists/{id}/versions           create    (yayımlanmıştan yeni taslak)
POST /api/commercial/price-lists/{id}/discard            create
POST /api/commercial/price-lists/{id}/submit             submit
POST /api/commercial/price-lists/{id}/decision           publish   (publish | return)
GET  /api/commercial/price-lists/export                  export
POST /api/commercial/price-list-imports                  import    (multipart)
GET  /api/commercial/price-list-imports/{id}             import    (önizleme + fark; ?rowStatus=…)
POST /api/commercial/price-list-imports/{id}/convert     import
POST /api/commercial/price-list-imports/{id}/discard     import
GET  /api/commercial/pricing-rules                       read
POST /api/commercial/pricing-rules                       create
GET  /api/commercial/pricing-rules/{id}                  read
PUT  /api/commercial/pricing-rules/{id}                  update    (taslak)
POST /api/commercial/pricing-rules/{id}/revisions        update    (revize | retire taslağı)
POST /api/commercial/pricing-rules/{id}/discard          update
POST /api/commercial/pricing-rules/{id}/submit           submit
POST /api/commercial/pricing-rules/{id}/decision         publish
POST /api/commercial/pricing-rules/trial                 read      (kural deneme; kaydetmez)
GET  /api/commercial/pricing-contract                    price-lists.read | pricing-rules.read
GET  /api/commercial/fx-rates                            fx-rates.read
POST /api/commercial/fx-rates                            fx-rates.create
PUT  /api/commercial/fx-rates/{id}                       fx-rates.update
POST /api/commercial/fx-rates/import                     fx-rates.import
GET  /api/commercial/pricing-history                     price-lists.read | pricing-rules.read | fx-rates.read (türüne göre)
```

- Ocelot: her kök (`price-lists`, `price-list-imports`, `pricing-rules`, `pricing-contract`, `fx-rates`, `pricing-history`) için
  `/api/commercial/{kök}` + `/api/commercial/{kök}/{everything}`, ilgili yöntemler + OPTIONS. Çok parçalı yükleme gövde sınırı
  ayarı (5 MB) Gateway'de ölçülür.
- **Rota kapsama testi:** `gateway/Diten.ApiGateway.Tests/VwW6PricingRouteCoverageTests.cs` — her (yol, yöntem) bir satır; sabotaj:
  bir rota çıkarılınca kırmızı.
- Web aynı köken vekil (`/Commercial/Pricing/api/...`); tarayıcı servis portuna gitmez.

---

## 16. Kabul ölçütleri

**AC-LIST**
- [ ] **AC-LIST-1** Liste = tüzel kişilik × ülke × para birimi; tür Resmî / Şirket listesi; üç kademe + ürün başına KDV.
- [ ] **AC-LIST-2** Görünen durum Taslak / Onay bekliyor / Planlandı / Yürürlükte / Geçmiş; Planlandı → Yürürlükte geçişi tarihte,
      zamanlayıcısız (okuma anında).
- [ ] **AC-LIST-3** Yeni liste yayımlanınca aynı (tüzel kişilik, ülke) için önceki liste o tarihten Geçmiş olur; yayımlanmış liste
      düzenlenemez.
- [ ] **AC-LIST-4** İçe aktarma: para birimi zorunlu; eşleşmeyen + Retired satırlar önizlemede sayılır, taslağa girmez; fark eklenen /
      çıkan / değişen (kademe başına) gösterir.

**AC-RULE**
- [ ] **AC-RULE-1** Kural anahtarı tüzel kişilik × SKU × isteğe bağlı segment / dönem; yumuşak / sert çiftleri.
- [ ] **AC-RULE-2** En özel kural kazanır (segment + dönem > segment > dönem > genel); aynı özgüllükte çakışma yayımlanamaz.
- [ ] **AC-RULE-3** Formüller DESIGN-GAP §2 ile birebir: 10+2 → %16,7; bedava = floor(adet / a) × b; birleşik 1 − (1 − i)(1 − m),
      0,1; birleşik yalnız fiyatlı modda; adet Min altı sert, HardMax üstü sert, SoftMax üstü yumuşak; satır = en kötü.
- [ ] **AC-RULE-4** Mockup demo değerleriyle (TUT / ALM / GLU / URO, "Zincir eczane") mockup senaryoları (ok / soft / hard; TR,
      AE, KG) aynı sonucu verir.

**AC-EVAL**
- [ ] **AC-EVAL-1** Değerlendirme gerekçe **kodu + parametre** döner; sunucu yanıtında birleştirilmiş cümle yok; 7 dil resx.
- [ ] **AC-EVAL-2** Yürürlükte liste yoksa `quantity` modu; iskonto / birleşik / fiyat bulgusu üretilmez.
- [ ] **AC-EVAL-3** Hesap yalnız `Evaluation/` altında; MOD-0168 ve ön yüzde ikinci kopya yok (yapısal test).
- [ ] **AC-EVAL-4** Aynı girdi + aynı yayımlanmış veri → aynı sonuç ve aynı `RuleSetFingerprint`.

**AC-4EYES (D13)**
- [ ] **AC-4EYES-1** Liste yayımlama ve kural değişikliği (oluştur / revize / emekliye ayır) ikinci kişi onayı ister; gönderen karar
      veremez; iade gerekçe ister.

**AC-FX (D14, K-W10 g)**
- [ ] **AC-FX-1** Kur tüzel kişilik × çift × tarih başına tek; okuma `RateDate ≤ asOf` en son kur + kaynak; yoksa null.
- [ ] **AC-FX-2** Kur düzeltmesi geçmişe yazılır; MOD-0168 siparişinin anlık görüntüsü değişmez.

**AC-HIST / AC-SEC / AC-UI / AC-GW**
- [ ] **AC-HIST-1** Her yazma ve yayımlama değişiklik geçmişinde kim / ne zaman / eski → yeni ile görünür.
- [ ] **AC-SEC-1** Kiracılar arası 404; her uç `[HasPermission]`; anahtarlar §14 ile birebir.
- [ ] **AC-UI-1** Ekran D `_TabCard` beş sekme; listeler Golden Compact (`verify_datatable_page` sapmaları numaralı listede);
      `_LayoutTenantShell` açıkça; h5 başlık; `.diten-field`; `--bs-primary`; RTL doğru.
- [ ] **AC-GW-1** §15'teki her (yol, yöntem) Gateway'de ve kapsama testinde.
- [ ] **AC-L10N-1** Bütün görünen metin 7 dilde (sayfa, kural iletileri, içe aktarma hataları, yardım).

---

## 17. Test beklentileri (sabotaj kanıtlı)

- **Hesap (tablo güdümlü):** MF oranı, bedava kutu, birleşik, yuvarlama, adet sınırları, en kötü sonuç, adetli mod, kural yok,
  özgüllük (4 düzey + eşitlik), mockup demo senaryoları (TR / AE / KG × ok / soft / hard), etkin indirim (sipariş).
- **Liste:** yayımlama ve örtük bitiş, çakışma 409, Planlandı → Yürürlükte tarih geçişi (enjekte `TimeProvider`), yayımlanmışı
  düzenleme 409, yeni sürüm, içe aktarma (eşleşen / eşleşmeyen / retired / mükerrer / geçersiz fiyat; fark).
- **Kural:** sürüm, revizyon, emekliye ayırma, çakışma, SoD.
- **Kur:** tekillik, en son ≤ asOf, null.
- **Kalıcılık (gerçek mongod):** gidiş-dönüş + sınıf eşlemesi, tekil indeksler, işlem desteksiz ortamda telafi.
- **Gateway:** `VwW6PricingRouteCoverageTests`.
- **Sabotaj (en az 5; `.bak` + touch; kırmızı görülmeli):** (1) birleşik formülü toplama çevir (i + m); (2) özgüllükte segment ile
  dönemi yer değiştir; (3) SoD denetimini kaldır; (4) adetli modda iskonto denetimini aç; (5) bedava kutuyu floor yerine round
  yap; (6) bir Gateway rotasını çıkar. CT ayrıca ajanın yazmadığı bir kuralı sabote eder.
- **Tam takım:** CRM + mimari (`-o` repo içi) + Gateway; `py -3 scripts/known_failures.py check` taban karşılaştırması.
- **Doğrulayıcılar:** `verify_module_id --check-id MOD-0156` exit 0 · `verify_datatable_page` dört modül · `check_pack_nav_keys` ·
  resx-check (7 dil).
- **Duman (kullanıcı, E4):** TR resmî liste içe aktar → fark → yayıma gönder → ikinci kullanıcı yayımla; Planlandı liste; kural
  oluştur + onay; kural deneme (üç sonuç); BAE USD → AED kur; değişiklik geçmişi; Arapça RTL; mockup görsel karşılaştırması.

---

## 18. Ready-for-dev kontrol listesi

- [x] DCP-002 kimlik kapısı GEÇTİ (§0.1)
- [x] Kayıt tablosu kontrol edildi; `commercial.*` ad alanı PKS-001 §4'e yazıldı (bu WP)
- [x] Golden Compact (kural formu 14 alan; liste formu kalem tablosuyla ayrı sayfa) — dört ekran `data_mode: server` (`screens:`)
- [x] Künye tek `id`, tek `status`; Layout açıkça `_LayoutTenantShell`
- [x] Backend / ön yüz dosya düzeni (§10, §11); doğrulama + hata yolları (§13); yetki (§14); Gateway + kapsama şartı (§15)
- [x] Kabul ölçütleri (§16) + sabotajlı testler (§17); denetlenen olaylar (§21); yardım kodları, tur yok (§11)
- [x] D7, D8, D11, D12, D13, D14 + K-O1…K-O5 KİLİTLİ (§19, §20.1)
- [ ] **§20.2 AÇIK-1…AÇIK-4** cevapları
- [ ] **Ayrı karar:** `ready-for-dev` + `runtime_code_allowed: true` + `flip_approved_by` — kullanıcı onayıyla CT

---

## 19. Uygulama notları / karar kaydı

### 19.a Bağlam

| # | Karar | Kaynak |
|---|---|---|
| K-O1 | Fiyat listesi + MF / iskonto kuralları + değerlendirme API'si + ara kur = MOD-0156 ilk dilim; CrmService, ayrı sınır | WP-VW-W6 |
| K-O2 | `commercial.*` ad alanı | kullanıcı 2026-10-19 |
| K-O3 | Ürün kimliği v1.1 SKU; Retired girmez | K-W10 h |
| K-O4 | Fiyatlı mod = yürürlükte liste var; tutar liste para biriminde; yerel karşılık kur anlık görüntüsüyle | K-W10 f / g |
| K-W10 (a) | MOD-0156 asgari dilim W6'da, Program T2 genişletir; kural dışı istisna ileride MOD-0284 | ROADMAP |
| K-W10 (d) | TR resmî liste "düzenlenmiş" liste türü olarak içe alınır, aylık | ROADMAP |
| DESIGN-GAP §3 | kural = tüzel kişilik × SKU × isteğe bağlı segment / dönem; liste türü adı "Resmî / Şirket listesi"; içe aktarmada para birimi; D'ye "Kurlar" sekmesi; belgede kur kaynağı; dönem şimdilik tarihler | DESIGN-GAP |
| Paket kararı | API `/api/commercial/...`, klasör `Features/Commercial/Pricing`, kalemler ayrı koleksiyon | bu paket |

### 19.b D-soruları — KİLİTLİ (MOD-0156'ya düşenler)

| # | Kilitli karar | Etkisi |
|---|---|---|
| **D7** | MF oranla denetlenir; bedava kutu floor(adet / a) × b; yüzdeler 0,1 | §4.6.2; AC-RULE-3; sabotaj (1), (5) |
| **D8** | En özel kural kazanır: segment + dönem > segment > genel; dönem = geçerlilik tarihleri; segment BRD `pharmacy-segment` | §4.3, §4.6.2; AC-RULE-2; AÇIK-3, AÇIK-4 |
| **D11** | Fiyat kademesi ülke ayarı, varsayılan eczacı alış | §4.6.1 `Tier` girdisi (ayar MOD-0168'de) |
| **D12** | Fiyat gönderimde sabitlenir | MOD-0168 yazar; burada değerlendirme saf + `RuleSetFingerprint` |
| **D13** | Liste yayımlama ve kural değişikliği dört göz; pazarlama düzenler, satış operasyonu onaylar | §4.4, §12, §14; AC-4EYES-1; sabotaj (3) |
| **D14** | Ara kur tablosunu satış operasyonu girer ("Kurlar" sekmesi) | §4.5; §14 |
| **D17** | Yer: CrmService, ayrı sınır | §0.2 |

D1–D6, D9, D10, D15, D16 MOD-0168'e aittir (o paketin §19.b'si).

---

## 20. Çözüm tablosu, AÇIK noktalar, takipler

### 20.1 Çözüm tablosu — KİLİTLİ

| ID | ✅ Karar | Reddedilen |
|---|---|---|
| D7 | oranla denetim | bedava kutu adediyle denetim |
| D8 | en özel kazanır; dönem = tarihler | en kısıtlayıcı kazanır · kampanya bağı şimdi |
| D11 | ülke ayarı, eczacı alış | sabit kademe |
| D13 | dört göz (Workflow) | tek kişi yayımlama (mockup) |
| D14 | satış operasyonu girer | dış sağlayıcı şimdi (MOD-0277 ileride) |
| DESIGN-GAP §3 kural anahtarı | tüzel kişilik × SKU × segment / dönem | ürün × dönem / segment (tüzel kişiliksiz) |

### 20.2 AÇIK noktalar (yeni D-sorusu değil)

| # | Nokta | Neden açık | CT önerisi |
|---|---|---|---|
| **AÇIK-1** | "(tüzel kişilik, ülke) başına bir yürürlükte liste" kuralı | K-O4 / K-W10 f "yürürlükte liste varsa fiyatlı" der; aynı şirket için aynı anda Resmî + Şirket listesi ya da iki para birimi olursa hangisinin kullanılacağı yazılı değil | Bu paketteki kural (§4.1.2): aynı anda tek liste; yenisi öncekini bitirir. Kullanıcı onayı |
| **AÇIK-2** | Denetim kaydı (AUD-001) | CRM izleri `aday`; kural §5 K4 yeni yazma komutunu engeller; proje uygulaması Faz 8 (kullanıcı 2026-10-06) | MOD-0168 AÇIK-6 ile aynı: §21 `aday→b`, Faz 8 istisnası onayı |
| **AÇIK-3** | Eczane segmenti alanı ve BRD `pharmacy-segment` seti | MOD-0149 Account'ta alan ölçülmedi / yok; set yok | MOD-0168 AÇIK-5 ile aynı; alan gelene kadar segment alanı kapalı, kurallar genel |
| **AÇIK-4** | "Yalnız dönem" kuralının sırası | D8 üç düzey sayar (segment + dönem > segment > genel); dönemli ama segmentsiz kural sayılmamış | Bu paketin yorumu: segment + dönem (3) > segment (2) > dönem (1) > genel (0). Kullanıcı onayı |
| **AÇIK-5** | GTIN ile MDM araması | product-master v1.1'de GTIN ile arama yok (v1.2, BL-584 W-04); LSKU `gtin` P6'ya kadar null | İlk dilimde eşleştirme SKU kodu (LS- / FG-) + FinishedGood GTIN'i (toplu okuma ile); TR resmî listesi barkodla gelirse v1.2 gelene kadar eşleşmeyenler önizlemede kalır — MDM sahibine bildirilir |

### 20.3 Takipler

- **F-GW** — §15 rotaları + kapsama testi (W6-BE-b isteminin içinde).
- **F-RBAC** — anahtar kataloğu + W6-CFG grant betiği.
- **F-WF** — W6-CFG: `COM-PRICING-PUBLISH` şablonu (aday satış operasyonu pozisyonu) canlı oluştur + yayımla.
- **F-FX-0277** — MOD-0277 FX Rates Provider gelince `IFxRateReader` sağlayıcıya bağlanır.
- **F-T2** — müşteri / sözleşme fiyatı, hacim kademesi, kampanya bağlı kural (MOD-0165), Deal Desk istisnası (MOD-0284).
- **F-MOBILE** — mobil yalnız MOD-0168 üzerinden sonuç görür (W8 notu MOD-0168 §22).

---

## 21. Denetlenen olaylar (AUD-001)

> CRM'in bütün denetim izleri `aday` (`tests/architecture/audit-ledger/Diten.CrmService.md`); kabul edilmiş iz yok → **bağımlılık
> / engel** (§7, AÇIK-2). Yol sütunu Faz 8'de (AUD-CRM-1) hedeflenen iletimi gösterir (`crm-commercial-yayinci`,
> `HttpCrmAuditPublisher`, SourceModule MOD-0156).

| Komut | Olay adı | Nesne | Yol | Değişen alanlar | İstisna |
|---|---|---|---|---|---|
| CreatePriceListCommand | commercial.price-list.created | PriceList | aday→b | After: Name, LE, Country, Currency, Kind, ValidFrom | — |
| UpdatePriceListCommand | commercial.price-list.updated | PriceList | aday→b | Before/After: başlık alanları + kalem farkı özeti | — |
| CreatePriceListVersionCommand | commercial.price-list.version-created | PriceList | aday→b | PreviousPriceListId, ListVersion | — |
| DiscardPriceListCommand | commercial.price-list.discarded | PriceList | aday→b | Status | — |
| SubmitPriceListCommand | commercial.price-list.submitted | PriceList | aday→b | Status, Review.SubmittedBy | — |
| DecidePriceListCommand | commercial.price-list.decided | PriceList | aday→b | Decision, Reason, Status; kapatılan önceki liste | — |
| UploadPriceListImportCommand | commercial.price-list-import.uploaded | PriceListImport | aday→b | dosya adı / özeti, satır sayıları | — |
| ConvertPriceListImportCommand | commercial.price-list-import.converted | PriceListImport | aday→b | oluşan PriceListId | — |
| DiscardPriceListImportCommand | commercial.price-list-import.discarded | PriceListImport | aday→b | Status | — |
| CreatePricingRuleCommand | commercial.pricing-rule.created | PricingRule | aday→b | After: anahtar + sınırlar | — |
| UpdatePricingRuleCommand | commercial.pricing-rule.updated | PricingRule | aday→b | Before/After: sınırlar, geçerlilik, segment | — |
| RequestPricingRuleRetirementCommand | commercial.pricing-rule.retirement-requested | PricingRule | aday→b | ChangeKind = retire | — |
| DiscardPricingRuleDraftCommand | commercial.pricing-rule.discarded | PricingRule | aday→b | Status | — |
| SubmitPricingRuleCommand | commercial.pricing-rule.submitted | PricingRule | aday→b | Status | — |
| DecidePricingRuleCommand | commercial.pricing-rule.decided | PricingRule | aday→b | Decision, Reason, Status; superseded sürüm | — |
| CreateFxRateCommand | commercial.fx-rate.created | FxRate | aday→b | After: LE, çift, tarih, kur, kaynak | — |
| UpdateFxRateCommand | commercial.fx-rate.updated | FxRate | aday→b | Before/After: kur, kaynak | — |
| ImportFxRatesCommand | commercial.fx-rate.imported | FxRate | aday→b | satır sayısı, tarih aralığı | — |

- Kayda yazılmayanlar: içe aktarılan dosyanın içeriği. K2 sınıfı komut yok.
- Bu komutlar `## Bilinen borç` listesine eklenemez; AUD-001 mimari testi bu komutlar için kırmızı olur → AÇIK-2.

---

## Teslim

Paket **`status: draft`** — `runtime_code_allowed: false`, `flip_approved_by` boş. DCP-002 GEÇTİ. D7, D8, D11, D12, D13, D14, D17 +
K-O1…K-O5 KİLİTLİ; AÇIK-1…AÇIK-5 cevap bekler. Golden Reference **compact** şablon alındı; bilinçli sapmalar: `_TabCard` sekme
başlığı (DESIGN-GAP §4), kural deneme sayfası, `BulkDelete` yokluğu (gerekçe §10). Onaydan sonra CT `ready-for-dev`'e çevirir;
W6-BE-b ve W6-WEB-b istemleri WP-VW-W6'ya eklenir.
