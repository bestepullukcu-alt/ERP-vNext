---
id: MOD-0181-FU01
name: Cycle Count Contract 1.2 — Line Display Codes and Assignee Filter
parent: MOD-0181
parent_name: Cycle Counting
domain: supply-chain-execution
service: Diten.InventoryService
port: 5067
shell: none
golden_reference: none
entity_base: EntityBase
form_field_count: 0
status: ready-for-dev
approval_status: pending
revision: "r4 (2026-11-13) — yalnız r3 incelemesinin F-10 bulgusu (docs/records/reviews/MOD-0181-FU01/2026-11-13-a78a6bbb.md, KOŞULLU) kapatıldı: release'in bin kodu ve lot numarası okuması işlem öncesi ön okumaya taşındı (ScopeSkusAsync yerine bin'leri, SKU anahtarlarını ve lot kimliklerini döndüren ön okuma), NeedsFlags yalnız ön okuma ile anlık görüntü arasında gelen değerler için yedek; ebeveynin AC52a testi (tek anlık görüntü işlemi) koruyucu olarak AC-5'te, yeni sabotaj S-FU01-5b (§5, §8.1, §16, §17, §19.6, §24). X-3 sahip teyidi kaydedildi (commit 4fa71d957, docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:67). Başka metin, AC kimliği ve dilim değişmedi. r3 (2026-11-13, c6a2b800f) — r2 bağımsız incelemesi (docs/records/reviews/MOD-0181-FU01/2026-11-13-96648a74.md, KOŞULLU; 1 engel, 4 orta, 4 düşük) kapatıldı; kapanış tablosu §19.5. F-1 (engel): submit (WithVariance) ve — r3 ölçümüyle — giriş (RecordCountLineHandler) satırı alan alan yeniden kurup dört üyeyi siliyordu; iki dosya DL-1 Repo Scope'ta, §4.1 düzeltildi, yeni AC-16 + S-FU01-16a / S-FU01-16b. F-2: AC-14 ve S-FU01-14 DL-1'e taşındı. F-3: CT dondurma adımı listesi tamamlandı (1.1.0 fikstürü sha256 sabitiyle, başlık testi, açıklama değişikliği çıkarıldı, contracts README satırı, Gateway testinin dört 1.1.0 metni). F-4: AC-7'ye kendi sabotajı S-FU01-7 (Accept-Language'e göre SkuName seçimi), eski metin AC-4 (a) olarak düzeltildi. F-5: sahip kararı (Beste, 'İkisi de SKU adı', docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:64-66) — skuName mobil yüzeylerinde her yerde getSku name; MOD-0182 ScanResolved FIX-01'de hizalanır (§2.3 kural 3, §8.2, §20). F-6…F-9: release okumalarının yeri, sahte PRODUCT-MASTER dosyası (X-3), tüketici toleransı, AC-12 ölçüm testleri. Künye ready-for-dev / pending kaldı. AC-1…AC-15 kimlikleri ve anlamları değişmedi (AC-7 sabotajı, AC-12 ölçüm adları, AC-13 izin listesi, AC-14 dilimi değişti); yeni AC-16. Kimlik ön kontrolü yeniden koşulmadı (kimlik ve ad değişmedi). r2 (2026-11-13, cd8e2641f) — sahip ve CT kararları işlendi (karar kaydı docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63, commit a1979335f): Q-1 = A (dört görünen üye satır açılırken bir kez yazılır, kanıt kopyası); Q-2 = MDM getSku name aynen (tek değerli, dil yok, yoksa üye yazılmaz; K-4 aynı kuralı kullanır); X-1 = evet (XMC-001 sahip onayı: MOD-0173 SkuReference.cs ve ProductMasterHttpClient.cs eklemeli SKU name okuması, Repo Scope); Q-3 = A (önce CT sözleşme dondurma, sonra DL-1); Q-4 = sürüm dondurulma sırasıyla numaralanır. §19.4 açık sorular tablosu karar tablosuna çevrildi. Künye status ready-for-dev (S-49), approval_status pending. AC, sabotaj, dilim ve kapsam değişmedi; değişen yalnız seçimlerin yazımı (§1, §2.3, §5, §7, §8.2, §8.5, §18, §19, §23, §24). Kimlik ön kontrolü yeniden koşulmadı (kimlik ve ad değişmedi). r1 (2026-11-13, b91577c74) — ilk taslak. Girdi: sahip kararı K-2 (Beste, 2026-11-13; docs/records/decisions/2026-11/mod0181-owner-decisions-2026-11-12.md 'Ek — 2026-11-13'), mobil sayım cevapları (work-packs/mobile/2026-11-13-cycle-count/, soru 2 ve 3; mobilin K-2 / K-3 diye andığı iki madde K-2 kararında birlikte kabul edildi), donmuş WMS-COUNT-BUNDLE 1.1.0, Inventory servis kodu (HEAD ca36b19be). Kimlik ön kontrolü bu oturumda koşuldu (§0). Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) yok."
status_note: "ready-for-dev r4 — approval_status pending; kod izni yok. r3 incelemesinin tek bulgusu F-10 r4'te kapandı (§19.6); r4 için fark üzerinden kısa yeniden inceleme (CT-APR-02), sonra onay satırı (CT-APR-01, onaylayan Ali). XMC-001 X-1, X-3 sahip onaylı; dondurmadan önce mobil bilinmeyen üye toleransı teyidi (CT). Önceki durum (r3): r2 incelemesinin F-1…F-9 bulguları r3'te kapandı (§19.5). Önceki durum (r2): Q-1…Q-4 ve X-1 seçildi (§19.4, docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63). Sırada: bağımsız inceleme (CT-APR-02), içerik onayı ve onay satırı (CT-APR-01, onaylayan Ali / GitHub GMG-ERP; satırları yazan hesap Beste'nin — yazan ≠ onaylayan, MOD-0181 D-1 emsali). XMC-001 X-1 (MOD-0173 PRODUCT-MASTER istemcisine SKU adı) sahip onaylı. Registry satırını CT yazar. target: TBD — sahip tarihi yok, plan satırı verilmedi."
risk: LOW
risk_reason: "Yalnız eklemeli: dört isteğe bağlı görünen kod üyesi ve iki isteğe bağlı liste süzgeci. Yeni uç, yeni izin, yeni yazma komutu, yeni hata kodu yok. Kör sayım kuralına ve defter yazımına dokunmaz. Risk iki noktada: PRODUCT-MASTER okumasının release'e yük getirmemesi (aynı çağrı, AC-5) ve sözleşme sürüm numarasının MOD-0182 eki ile sırası (Q-4: dondurulma sırasıyla)."
owner: supply-chain-execution / merkez (Beste) — MVP-5
approver: Ali (GitHub GMG-ERP)
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-13
target: TBD
mockup: "yok — ekran değişikliği yok (shell: none)"
dependencies: [MOD-0181 (ebeveyn; DL-1…DL-5 kodu, WMS-COUNT-BUNDLE 1.1.0 FROZEN), MOD-0173 (PRODUCT-MASTER istemcisi — X-1), MOD-0173-FU02 (Location kodu, aynı servis), MOD-0174 (Lot numarası, aynı servis), MOD-0290 (PRODUCT-MASTER getSku canonicalCode / name), MOD-0182 (aynı dört ad, ScanResolved emsali; WMS-COUNT-BUNDLE eklemeli sürüm sırası — Q-4)]
---

# MOD-0181-FU01 — Sayım sözleşmesi 1.2: satırda görünen kodlar ve atanan süzgeci

> **ready-for-dev (r4, 2026-11-13).** Bu paket `status: ready-for-dev`, `approval_status: pending`'dir; bağımsız inceleme (CT-APR-02) ve
> onay satırı (CT-APR-01, onaylayan Ali) olmadan **kod yazılmaz** (AGENTS.md Çekirdek madde 7). r2: Q-1…Q-4 ve X-1 seçildi (§19.4,
> `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63`). r3: r2 incelemesinin bulguları kapandı (§19.5) — en önemlisi, satırı alan alan yeniden kuran giriş ve submit
> işleyicileri de dört üyeyi korur (§4.1, AC-16). r4: release'in görünen değer okumaları anlık görüntü işleminden önce yapılır; release yine tek
> anlık görüntü işlemi açar (§8.1, AC-5, ebeveynin AC52a testi).
> Kapsam K-2 kararıyla sınırlıdır: WMS-COUNT-BUNDLE'ın eklemeli sürümünde `CycleCountLine`'a dört isteğe bağlı görünen üye
> (`locationCode`, `skuCode`, `skuName`, `lotNumber`) ve `listCycleCounts`'a iki isteğe bağlı süzgeç (`assignedTo`, `unassigned`).
> 1.1.0 FROZEN değişmez. Rol yetkisi genişletilmez. Kör sayım kuralı aynen kalır.

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — bu oturumda koşuldu (HEAD `ca36b19be`):**

| Komut | Sonuç |
|---|---|
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0181-FU01 --name "Cycle Count Contract 1.2 — Line Display Codes and Assignee Filter" --parent MOD-0181` | `OK  MOD-0181-FU01: proven against Blueprint/registry.` — çıkış 0 |
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --discover "cycle count contract 1.2"` | çıkış 3 ("inceleme gerekli"); 609 satır; rapor sha256 `1121a39a799814990eab0c0e2c5d3ed67abb9ddef692b820359b99822b4aa634`; araç sha256 `ae87a566…`; Blueprint 8.8 `Blueprint_Data` 1005 satır |

**FU numarası:** registry'de MOD-0181 için yalnız ebeveyn satırı var (`execution/registries/module-id-registry.md:324`, `reserved / planned`);
`MOD-0181-FU` hiçbir registry satırında ve hiçbir paket künyesinde geçmiyor (`grep`, bu oturum). İlk boş numara **FU01**. Ebeveyn pakette
anılan `FU-0181-ABC`, `-AI`, `-VAL`, `-HELD`, `-SERIAL`, `-BULK`, `-RCS`, `-REASON`, `-EA` takip notlarıdır, kimlik değildir
(`execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md:1401-1407`). Registry satırını CT yazar; bu paket yazmaz.

**Keşif bulgularına karar:**

| Bulgu | Karar |
|---|---|
| Blueprint 0, registry 0, paket 0 | Mükerrer yok. Kimlik ebeveynin (MOD-0181, Blueprint 8.8 satır 182, `WMS-COUNT-BUNDLE`) alt işidir: yeni yetenek değil, ebeveynin sözleşmesine eklemeli üye. |
| Gateway 1 — `contracts` → 5065 | İlgisiz: "contracts" sözcüğü Procurement Service (5065) rota ailesine eşleşti. Bu işin uçları `/api/warehouse` altındadır (§15). |
| Görev 1 — `WMS-MVP5-ANTIGRAVITY-0181` [VERIFIED] | İlgisiz: ebeveynin `.antigravity` iki satırı (PKS-001 `cycle-count.*`, RCS-001 `CNT`). Bu FU izin ya da kayıt kodu eklemez. |

**Yeni modül değil, takip paketi neden:** ebeveyn `ready-for-dev` r8'dir ve onay satırı bekler; sözleşme 1.1.0 donmuştur. Donmuş sözleşmeye
yeni üye yalnız yeni eklemeli sürümle gelir (sözleşme kuralı K16, `docs/analysis/contracts/wms-count-bundle.openapi.yaml:13`), ebeveyn
paketin onaylı metnine yeni dilim eklemek de yeni onay ister. K-2 kararı "MOD-0181 için yeni FU açılır" der
(`docs/records/decisions/2026-11/mod0181-owner-decisions-2026-11-12.md:13-16`).

## 1. Module Summary

Mobil sayım uygulaması (ve Sayan rolündeki her istemci) satırı bugün yalnız kimliklerle alır: `locationId`, `item { itemId, skuId, skuLevel }`,
`lotId` (`docs/analysis/contracts/wms-count-bundle.openapi.yaml:672-704`). Raf kodu, ürün kodu / adı ve lot numarası için üç ana veri okuması
gerekir; Sayan rolünde (`counts.read` + `counts.record`) bu okuma yetkileri yoktur ve verilmesi sayımla ilgisiz bütün ana veriyi açar
(mobil cevabı soru 2: `execution/domains/supply-chain-execution/work-packs/mobile/2026-11-13-cycle-count/WMS-CEVAP-2026-11-13-sayim.md:92-134`).
Liste ucunda "bana atanan" süzgeci yoktur (soru 3: aynı dosya `:136-155`).

Bu paket:

1. `CycleCountLine`'a dört **isteğe bağlı** görünen üye ekler: `locationCode`, `skuCode`, `skuName`, `lotNumber`. Satır yaratılırken
   (release, bulunan satır, yeniden sayım) **bir kez** yazılır, `expectedQuantity` gibi değişmez (Q-1 = A).
2. `listCycleCounts`'a iki **isteğe bağlı** süzgeç ekler: `assignedTo` (uuid) ve `unassigned` (boolean).
3. WMS-COUNT-BUNDLE'ın sıradaki eklemeli sürümünü (dondurulma sırasıyla numaralanır; bugün sıradaki **1.2.0**, Q-4) CT tek yazıcı adımıyla dondurur.

Hedef kullanıcı: sayan çalışan (mobil ve Web), sayım sorumlusu (liste süzgeci). Kapasite: yeni uç yok; satır başına ek veri ≈ dört kısa metin.

## 2. Ownership and Boundaries

### 2.1 Kapsam

- `CycleCountLine` kaydına ve cevabına dört görünen üye; yazımı release, `addCycleCountLine` ve `requestCountRecount` içinde (§8.1).
- `skuName` dil kuralı (§8.2).
- `listCycleCounts` süzgeci: sorgu, işleyici, depo süzgeci, bir indeks (§8.3).
- Sözleşme eki ve sözleşme testleri (CT dondurma adımı, §8.5).

### 2.2 Kapsam dışı

| Konu | Neden / nereye |
|---|---|
| Web sayım formunun adları saklanan kodlardan okuması (`MasterLabelResolver` yerine) | ayrı iş — FU-0181-FU01-WEB (§20). Web dosyası değişmez. |
| Evidence Pack'in saklanan kodları kullanması | değişmez; Evidence bugünkü okumasıyla kalır (§20). |
| Görevlere (WMS-BUNDLE `WarehouseTask`) raf / ürün kodu | K-4 — MOD-0178 için ayrı FU; dil kuralı bu paketin §8.2'sini kullanır. |
| Girişin atanan kişiyle sınırlanması | yok. Sunucu girişi `AssignedTo` ile sınırlamaz (mobil cevabı `:153-155`); bu paket bunu değiştirmez. |
| Eski satırlara geriye dönük doldurma | yok (§19.3: ebeveynin satırları canlıda değil). |
| Yeni uç, izin, hata kodu, olay türü, yazma komutu | yok. |

### 2.3 Sınır kuralları

1. **Kör sayım (ebeveyn Q-1 = A, sözleşme kural 12):** dört üye hiçbir miktar taşımaz ve miktardan türetilmez; kör sayımda da her cevapta
   bulunur. Beş kör üyenin (`expectedQuantity`, `movementsSinceSnapshot`, `expectedAtCount`, `varianceQuantity`, `adjustment.quantity`)
   görünürlüğü değişmez — kural tek yerde kalır (`CycleCountReads.ShowsExpected`,
   `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCount/Services/CycleCountReads.cs:47-48`).
2. **Kiracı ve şirket:** kod okumaları servisin kendi süzgeçli okumalarıdır (Location ve Lot: TenantId + LegalEntityId; PRODUCT-MASTER:
   kullanıcı jetonu + sunucunun çözdüğü şirket). Liste süzgeci şirket süzgecine **eklenir** (AND), onun yerine geçmez.
3. **Ana veri kopyası değil, kanıt kopyası:** domain-config "SCE … lokal kopya açmaz" der
   (`execution/domains/supply-chain-execution/domain-config.md:46`). Bu üyeler ana verinin kopyası **değildir**: yetkili değildir, hiçbir
   iş kuralı onları okumaz, güncellenmez, yalnız satırın yaratıldığı andaki görünen değeri kanıt olarak taşır (`expectedQuantity`'nin anlık
   görüntü olması gibi). Kimlik (`itemId` / `skuId` / `lotId` / `locationId`) yetkili kalır. Emsal: MOD-0182 okutma olayı aynı dört adı
   okutma anında kopyalar (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Barcode/Entities/ScanEvent.cs:25-48`);
   WMS-BUNDLE görevi `warehouseCode` ve `lotNumber` taşır. Bu yorum Q-1 = A ile seçildi (Beste, 2026-11-13).
   **`skuName`'in tek anlamı (sahip kararı, Beste, "İkisi de SKU adı", `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:64-66`; r2 inceleme F-5):** mobil yüzeylerinde
   `skuName` her yerde PRODUCT-MASTER `getSku` `name`'idir (SKU'ya özgü ad): bu paketin sayım satırı, MOD-0182 `ScanResolved` ve K-4
   görevleri. MOD-0182 bugün `ScanResolved.SkuName`'e `getProduct` `name` yazıyor (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Barcode/Services/ScanVerifier.cs:333-338`) — bu MOD-0182'nin
   FIX-01'inde hizalanır (SCAN-BUNDLE henüz dondurulmadı); bu paket MOD-0182 dosyasına dokunmaz. Evidence dosyasındaki "kod · ürün adı"
   metni (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CountEvidenceExports/CountEvidenceDocumentBuilder.cs:84-93`) bir `skuName` üyesi değil, dosya metnidir; değişmez (§20).
4. **Yetki genişlemez:** Sayan rolü kodları satır cevabından görür; MDM / Location / Lot okuma izni verilmez (K-2). Açılan bilgi yalnız o
   sayımın satır anahtarlarının kodlarıdır.

## 3. Owned Objects

| Tür | Ad | Değişiklik |
|---|---|---|
| Entity | `CycleCountLine` | + `LocationCode`, `SkuCode`, `SkuName`, `LotNumber` (string?, §4.1) |
| Repository sorgusu | `CycleCountQuery` | + `AssignedTo` (Guid?), `Unassigned` (bool) |
| Query | `GetCycleCountListQuery` | + `AssignedTo`, `Unassigned` (ham metin; işleyici çözer) |
| Query handler | `GetCycleCountListHandler` | iki süzgecin çözümü ve doğrulaması (§12) |
| Command handler (mevcut, yazım eklenir) | `ReleaseCycleCountHandler`, `AddCountLineHandler`, `RequestCountRecountHandler` | dört üyenin yazımı (§8.1) |
| Command handler (mevcut, koruma eklenir — r3, F-1) | `RecordCountLineHandler`, `SubmitCycleCountHandler` | satırı yeniden kurarken dört üyeyi taşır (§4.1, AC-16) |
| Servis | `CycleCountProductFlags` | aynı `GetSkuAsync` çağrısından `CanonicalCode` + `Name` döner (ek çağrı yok, AC-5) |
| DTO | `CycleCountLineDto` | + `LocationCode`, `SkuCode`, `SkuName`, `LotNumber` |
| Okuma eşlemesi | `CycleCountReads.Line` | dört üyeyi koşulsuz yazar (null → üye yok) |
| Upstream (MOD-0173 — X-1) | `SkuReference`, `ProductMasterHttpClient` | + `Name` (SkuRef `name`, yalnız gösterim) |
| API ucu | `GET /api/warehouse/cycle-counts` | + `assignedTo`, `unassigned` sorgu parametreleri; **yeni uç yok** |
| İzin | — | **yok**; uç `cycle-count.counts.read` ile kalır |
| Frontend route | — | yok |

Yeni yazma komutu yoktur; üç mevcut komutun yazdığı satır alanları artar (§21).

## 4. Entity Fields

### 4.1 `CycleCountLine` — eklenen alanlar

| Alan | Tip | Zorunlu | Kaynak (yazıldığı an) | Boşsa |
|---|---|---|---|---|
| `LocationCode` | string? | hayır | Location `Code` (MOD-0173-FU02, aynı servis; TenantId + LegalEntityId süzgeçli okuma) | null → cevapta üye yok |
| `SkuCode` | string? | hayır | PRODUCT-MASTER `getSku` `canonicalCode` (bugün okunuyor, `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Upstream/ProductMasterHttpClient.cs:82`) | null → üye yok |
| `SkuName` | string? | hayır | PRODUCT-MASTER `getSku` `name` (v1.1, yalnız gösterim — `docs/analysis/contracts/product-master-bundle.openapi.yaml:742`; X-1) | null → üye yok |
| `LotNumber` | string? | hayır | `LotId` kayıtlı lot biçimindeyse (küçük harfli UUID, `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Traceability/Rules/LotPostingRules.cs:129-130`) MOD-0174 `Lot.LotNumber` (Scope süzgeçli okuma); `LotId` elle yazılmış lot metniyse (UUID biçiminde değil) o metnin kendisi; `LotId` yoksa yok | kayıtlı biçimde ama lot bulunamadı → üye yok |

Kurallar: değer kırpılır; boş ya da yalnız boşluk → null. Hiçbir üyeye kimlik (UUID), yer tutucu ya da çeviri metni yazılmaz. Alan bir kez yazılır,
sonra **değişmez ve kaybolmaz** (r3, F-1). Satırı sonradan yazan her işleyici dört üyeyi olduğu gibi taşır:

- **Giriş** (`RecordCountLineHandler`) ve **submit** (`SubmitCycleCountHandler.WithVariance`) satırı **alan alan yeniden kurar**
  (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/RecordCountLineHandler.cs:53-61`, `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/SubmitCycleCountHandler.cs:181-189`) ve `ReplaceLinesAsync` belgeyi bütünüyle değiştirir
  (`services/Diten.InventoryService/src/Diten.InventoryService.Persistence/CycleCount/CycleCountRepository.cs:401-408`) — kurucuda olmayan alan silinir. Bu iki kurucu DL-1'de dört üyeyi taşır. Öneri: ikisi `l.Copy()` + değişen
  alanlara çevrilir (bundan sonra eklenen alan da korunur); açık kopya da kabul edilir. Ölçüm AC-16.
- Onay, ret, yeniden sayımın kaynak satırı, retry, resolve ve posting satırı `Copy()` (`MemberwiseClone`) ile değiştirir — dört üye kendiliğinden
  korunur; AC-16 bunu da ölçer.

Mongo'da isteğe bağlı alan; eski belgelerde yok (geriye uyumlu okuma).

### 4.2 Indexes

| Koleksiyon | İndeks | Alanlar | Neden |
|---|---|---|---|
| `wms_count_counts` (`CycleCountCollections.Counts`) | `ix_wms_count_assignee_status` (tekil değil) | TenantId, LegalEntityId, AssignedTo, Status | `assignedTo` / `unassigned` + `status` süzgeci; mevcut `ix_wms_count_warehouse_status` deseni (`services/Diten.InventoryService/src/Diten.InventoryService.Persistence/CycleCount/CycleCountIndexConfiguration.cs:43-44`) |

Satır koleksiyonuna indeks eklenmez (yeni alanlarla süzgeç / sıralama yok).

## 5. Repo Scope

Hepsi `services/Diten.InventoryService/` altında (aksi yazılmadıkça):

| Dosya | Değişiklik | Dilim |
|---|---|---|
| `src/Diten.InventoryService.Domain/CycleCount/Entities/CycleCountEntities.cs` | `CycleCountLine`'a dört alan | DL-1 |
| `src/Diten.InventoryService.Domain/CycleCount/Repositories/ICycleCountRepository.cs` | `CycleCountQuery`'ye iki alan; ön okuma imzası (`ScopeSkusAsync` yerine `ScopePreviewAsync`, §8.1 — r4, F-10) | DL-1 |
| `src/Diten.InventoryService.Persistence/CycleCount/CycleCountRepository.cs` | `ListAsync` süzgeci; ön okuma `ScopeSkusAsync` (bugün `:266-280`) → `ScopePreviewAsync`: aynı iki sorgu (kapsam bin'leri + bakiye satırları), bin'leri, SKU anahtarlarını ve kayıtlı lot kimliklerini döndürür (r4, F-10); bin kodu mevcut `LocationNamesAsync` ile (işlem dışında, §8.1) | DL-1 |
| `src/Diten.InventoryService.Persistence/CycleCount/CycleCountIndexConfiguration.cs` | §4.2 indeksi | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCounts/Queries/GetCycleCountListQuery.cs` | iki parametre | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/QueryHandlers/GetCycleCountListHandler.cs` | çözüm + doğrulama | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/ReleaseCycleCountHandler.cs` | Snapshot satırlarına dört üye; yeni kurucu bağımlılığı `ILotRepository`; görünen değer önbelleği **anlık görüntü işleminden önce** ön okumadan doldurulur, `NeedsFlags` yalnız arada gelen değerler için yedek (§8.1; r4, F-10) | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/RecordCountLineHandler.cs` | giriş satırını yeniden kurarken dört üyeyi taşır (r3, F-1 ölçümü) | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/SubmitCycleCountHandler.cs` | `WithVariance` dört üyeyi taşır (r3, F-1) | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/AddCountLineHandler.cs` | bulunan satıra dört üye | DL-1 |
| `src/Diten.InventoryService.Application/Features/CountVariances/Handlers/CommandHandlers/RequestCountRecountHandler.cs` | yeniden sayım satırı dört üyeyi kaynaktan kopyalar | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCount/Services/CycleCountProductFlags.cs` | aynı çağrıdan kod + ad | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCount/Services/CycleCountReads.cs` | `Line` eşlemesi | DL-1 |
| `src/Diten.InventoryService.Application/Features/CycleCounts/CycleCountModels.cs` | `CycleCountLineDto` dört üye | DL-1 |
| `src/Diten.InventoryService.Api/Controllers/CycleCount/CycleCountsController.cs` | `List` eylemine iki `[FromQuery]` | DL-1 |
| `src/Diten.InventoryService.Application/Common/Upstream/SkuReference.cs`, `src/Diten.InventoryService.Infrastructure/Upstream/ProductMasterHttpClient.cs` | eklemeli SKU `name` okuması: `SkuReference`'a sona `string? Name = null`, istemci `getSku` cevabından `name`'i okur — **X-1, MOD-0173; XMC-001 sahip onayı (Beste, 2026-11-13, `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63`)** | DL-1 |
| `tests/Diten.InventoryService.Tests/CycleCount/` altında yeni `CycleCountDisplayCodesTests.cs`, `CycleCountListFilterTests.cs` | testler | DL-1 |
| `tests/Diten.InventoryService.Tests/TestInfrastructure/StubProductMaster.cs` (paylaşılan sahte PRODUCT-MASTER; `GetSkuAsync` bugün `:90-105`) | eklemeli: `getSku` cevabına `SetDisplay` ile verilen adı `Name` olarak koyar (bugün yalnız `CanonicalCode`, `:103`) ve `GetSkuAsync` çağrı sayacı (`GetSkuCalls`, AC-5) — **X-3** | DL-1 |
| `tests/Diten.InventoryService.Tests/CycleCount/WmsCountBundleContractTests.cs` — yalnız yeni parametre eşitliği testi (AC-14) | `listCycleCounts` sorgu parametreleri ↔ `CycleCountsController.List` `[FromQuery]` (r3, F-2: DL-1'e taşındı) | DL-1 |
| `docs/analysis/contracts/wms-count-bundle.openapi.yaml` | eklemeli sürüm (§8.5) | CT dondurma adımı |
| `tests/Diten.InventoryService.Tests/Contract/Fixtures/wms-count-bundle-v1.1.0.openapi.yaml` (yeni; 1.1.0'ın birebir kopyası, LF sha256 `c78b20951446ec408f68a0583c1a7606a796ce48d1fdd852e233811fcd130b15`) | fikstür | CT dondurma adımı |
| `tests/Diten.InventoryService.Tests/CycleCount/WmsCountBundleContractTests.cs` — başlık, sürüm ve eklemeli-sürüm testleri (§8.5 madde a–b) | sürüm testleri | CT dondurma adımı |
| `docs/analysis/contracts/README.md` — WMS-COUNT-BUNDLE satırları (bugün `:37-38`) altına yeni `↳` satırı | sözleşme listesi | CT dondurma adımı |
| `gateway/Diten.ApiGateway.Tests/Mod0181CycleCountRouteTests.cs` — **yalnız** dört "1.1.0" metni: belge yorumu `:11`, hata iletisi `:40`, `ContractVersion` sabiti `:120`, özet `:149` | sürüm metni | CT dondurma adımı (X-2) |

## 6. Protected Paths

- `.antigravity/**` — dokunulmaz (izin ve kayıt kodu eklenmez).
- `gateway/Diten.ApiGateway/ocelot.json` — değişmez (§15).
- `frontend/**` — değişmez (Web takip işi §20).
- `docs/analysis/contracts/wms-count-bundle.openapi.yaml` 1.1.0 metni — yalnız CT tek yazıcı adımı eklemeli değiştirir; geliştirici yazmaz.
- `execution/registries/module-id-registry.md` — CT yazar.
- MOD-0173 dosyaları yalnız X-1 satırındaki kadar; paylaşılan test altyapısı yalnız X-3 satırındaki kadar; MOD-0182 `Barcode/**` dosyaları
  (`ScanVerifier.cs` dahil — F-5 hizalaması MOD-0182 FIX-01'indir) dokunulmaz.
- Başka domain servisleri dokunulmaz (MDM'e istek yok; PRODUCT-MASTER yalnız mevcut sözleşmeyle okunur).

## 7. Dependencies

| Bağımlılık | Durum | Etki |
|---|---|---|
| MOD-0181 DL-1…DL-5 kodu | CT VERIFIED (MVP-1 hattı) | FU bunun üstüne yazılır |
| WMS-COUNT-BUNDLE 1.1.0 FROZEN | var | eklemeli sürümün tabanı |
| PRODUCT-MASTER `getSku` `name` (v1.1) | sözleşmede var; MDM'in canlıda gönderdiği **ölçülmedi** | gönderilmezse `skuName` yok (AC-4); DL-1 canlı ölçümü (§17) |
| Location `Code`, Lot `LotNumber` | aynı servis, aynı veritabanı | ek servis çağrısı yok |
| Merkezi denetim altyapısı | var (`tests/architecture/audit-ledger/Diten.InventoryService.md`, `inventory-merkezi-iletim`, yol b) | yeni komut yok (§21) |
| MOD-0182 X-14 (WMS-COUNT-BUNDLE eklemeli sürümünde `scanRef` önek notu) | MOD-0182 DL-3'e bağlı | sürüm numarası dondurulma sırasıyla (Q-4) |

## 8. Runtime Constraints

### 8.1 Görünen kodların yazımı

- **Release (Snapshot satırları):** `SkuCode` ve `SkuName`, release'in bugün seri bayrağı için yaptığı `GetSkuAsync` çağrısından alınır
  (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCount/Services/CycleCountProductFlags.cs:32`;
  çağıran `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/ReleaseCycleCountHandler.cs:90`)
  — **ek HTTP çağrısı yok**, çağrı yine anlık görüntü işleminin **dışında**. Satır kurucusu `ReleaseCycleCountHandler.cs:152-160`.
  **Okumaların yeri (r3 F-6, r4 F-10 — ön okuma):** release bugün seri bayraklarını anlık görüntü işleminden **önce** okur: işlem dışı ön
  okuma `ScopeSkusAsync` (`services/Diten.InventoryService/src/Diten.InventoryService.Persistence/CycleCount/CycleCountRepository.cs:266-280`) kapsamın bin'lerini ve bakiye satırlarını okuyup yalnız SKU anahtarlarını döndürür,
  bayraklar ondan alınır (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/ReleaseCycleCountHandler.cs:50-53`, "normally ONE snapshot attempt" `:51`). FU01 bu ön okumayı genişletir:
  1. `ScopeSkusAsync` yerine `ScopePreviewAsync` — **aynı iki sorgu** (ek veritabanı turu yok), dönüşü `{ BinIds, Skus, LotIds }`: kapsam bin'leri,
     bakiye satırlarının SKU anahtarları ve kayıtlı lot biçimindeki (`LotPostingRules.IsRegisteredLotForm`) lot kimlikleri; elle yazılmış lot metni
     kimlik değil, numaranın kendisidir (§4.1). Tek çağıran release'tir (`:52`); imza değişir, `ScopeSkusAsync` kalmaz.
  2. İşlemden önce, sırayla: seri bayrağı + SKU kod / adı (aynı `GetSkuAsync`, SKU başına bir çağrı — AC-5), bin kodları (tek
     `LocationNamesAsync`), lot numaraları (tek `ILotRepository.FindManyAsync`; yeni kurucu bağımlılığı). Hepsi önbelleğe yazılır;
     ön okumada adı geçip değeri bulunamayan anahtar "yok" olarak önbelleğe girer (bir daha okunmaz, deneme doğurmaz).
  3. Anlık görüntü işlemi satırları **yalnız önbellekten** yazar; işlem içinde okuma ya da HTTP çağrısı yoktur, yeni oturum yöntemi
     (`ICycleCountWriteSession`) eklenmez.
  4. **Yedek (yalnız yarış):** ön okuma ile anlık görüntü arasında kapsama yeni bir SKU, bin ya da lot gelirse (önbellekte hiç adı geçmeyen
     anahtar) deneme bugünkü `NeedsFlags` yoluyla (`:89-93`) eksikleri adlandırıp işlemi yazmadan bırakır, eksikler işlem dışında okunur,
     sonraki deneme yazar. Bu bugünkü bayrak yarışıyla aynı davranıştır; `MaxAttempts` (3, `:25`) değişmez.
  **Koruyucu test:** ebeveynin `AC52a_a_posting_during_the_release_is_in_both_reads_or_in_neither`
  (`services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount/CycleCountReleaseTests.cs:222`, iddia `:250` `Assert.Equal(1, probeCalls)`) değişmeden yeşil kalır — stoklu, lotlu bir release **tek** anlık görüntü
  işlemi açar (AC-5, S-FU01-5b).
- **Bulunan satır (`addCycleCountLine`):** `SkuCode` / `SkuName` aynı `LineBehaviourAsync` içindeki `GetSkuAsync`'ten (`CycleCountProductFlags.cs:62`);
  `LotNumber` lot doğrulamasının zaten okuduğu lottan (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/CommandHandlers/AddCountLineHandler.cs:133-135`);
  `LocationCode` isteğin `locationId`'sinin kodundan (tek `LocationNamesAsync`, yazma döngüsünden önce, işlem dışında). Satır kurucusu
  `AddCountLineHandler.cs:91-96`.
- **Yeniden sayım:** satır dört üyeyi **kaynak satırdan kopyalar** (aynı anahtar, aynı kanıt); kaynakta yoksa yeni satırda da yok. Satır kurucusu
  `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CountVariances/Handlers/CommandHandlers/RequestCountRecountHandler.cs:236-242`.
- **Başarısızlık:** kod / ad okunamaması **hiçbir yazımı durdurmaz** ve yeni hata kodu doğurmaz. PRODUCT-MASTER'a erişilemezse release ve
  bulunan satır bugünkü gibi 503 `PRODUCT_MASTER_UNAVAILABLE` ile reddedilir (bu zaten seri bayrağı / davranış okumasının sonucudur; FU yeni
  ret eklemez). Location kodu ya da lot bulunamazsa ilgili üye yazılmaz.
- **Okuma:** `CycleCountReads.Line` dört üyeyi `ShowsExpected`'ten **bağımsız** yazar (`CycleCountReads.cs:50-83`). `CycleCountLine` gömülü
  olduğu her yerde aynı üyeler gelir: satır listesi, giriş / bulunan satır cevabı, `CountVariance` `line` (`docs/analysis/contracts/wms-count-bundle.openapi.yaml:775-786`).
- **Sonraki yazımlar:** giriş ve submit satırı yeniden kurarken dört üyeyi taşır (§4.1, AC-16).
- **Olaylar değişmez:** `Released` ve `LineAdded` olaylarının `Data`'sına yeni üye yazılmaz (ebeveyn F-13: Released yalnız sayılar taşır).

### 8.2 `skuName` dil kuralı (K-2 "dil kuralıyla"; K-4 bu kuralı kullanır)

Seçim (Q-2: MDM `getSku` `name` aynen — Beste, 2026-11-13):

1. `skuName` = PRODUCT-MASTER `getSku` `name`'in **olduğu gibi** kopyası. PRODUCT-MASTER adı tek değerlidir, dil boyutu yoktur
   (`product-master-bundle.openapi.yaml:742`: "yalnız gösterim içindir; ayrıştırılmaz") — ana veri, kullanıcının girdiği dilde durur.
2. `Accept-Language` `skuName`'i **etkilemez**; aynı satır her dilde aynı değeri döner. Servis adı çevirmez, resx metni yazmaz.
3. Ad yoksa üye **yazılmaz** (JSON null yok, sözleşme kural 4). "Okunamadı" sözcüğü istemcinin kendi dilindeki metnidir (mobil bugün
   "Ürün adı okunamadı" gösteriyor); servis bu sözcüğü satıra yazmaz. Kimliğe düşülmez.
4. `skuName` SKU düzeyinin adıdır (Global Ürün adı · paket miktarı + birimi, LSKU'da + pazar kodu; Item'da kalem adı) — Global Ürün adı
   (`getProduct`) değil; paket boyları ayrışır ve Gsku dışı seviyelerde ek çağrı gerekmez. Aynı anlam bütün mobil yüzeylerindedir (sahip
   kararı, `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:64-66`; §2.3 kural 3).
5. `locationCode`, `skuCode`, `lotNumber` kod / numaradır; çevrilmez.
6. Çok dilli ürün adı ileride PRODUCT-MASTER'a gelirse bu kural yeni eklemeli sürümle değişir; bu paket ondan önce bir dil seçmez.

Mevcut Evidence kuralı (`name, SKU or lot that cannot be read is the localized "could not be read" word, never an id`,
`wms-count-bundle.openapi.yaml:71-72`) dosya içindir; API cevabında karşılığı "üye yok"tur.

### 8.3 Liste süzgeci (`listCycleCounts`)

- `assignedTo=<uuid>`: yalnız `AssignedTo` bu kullanıcı olan sayımlar. Kullanıcının var olduğu denetlenmez (süzgeçtir); bilinmeyen kimlik boş sayfa.
  Çağıran kendi kimliğini ya da başka bir kullanıcınınkini verebilir — `counts.read` sahibi bugün de her sayımın `assignedTo`'sunu görür.
- `unassigned=true`: yalnız `AssignedTo` boş olan sayımlar. `unassigned=false` süzgeç uygulamaz (yokla aynı).
- İkisi **birlikte** (`assignedTo` + `unassigned=true`) → 400 `INVALID_REQUEST`.
- Süzgeçler mevcut `status`, `warehouseId`, `q` ile **AND**'lenir; şirket ve soft delete süzgeci önce gelir
  (`services/Diten.InventoryService/src/Diten.InventoryService.Persistence/CycleCount/CycleCountRepository.cs:85-97`). `total` süzülmüş kümenin
  sayısıdır, sayfalama ve sıralama (yeniden eskiye) aynen kalır.

### 8.4 Geriye uyum ve tüketiciler

- Her ek **isteğe bağlıdır**, ama 1.1.0 nesneleri **kapalıdır** (`additionalProperties: false`): şemayı katı uygulayan bir 1.1.0 tüketicisi yeni
  üyeyi reddedebilir. Bu yüzden "kırılmaz" yalnız ölçülen tüketici için yazılır (r3, F-8):
  - **Web:** cevabı `JsonNode` ile ve üye adıyla okur (`frontend/Diten.Web/Models/Warehouse/CycleCountProxy.cs:216-223`) — bilinmeyen üye yok sayılır (koddan okundu);
    AC-15 Web test projesini değişmeden koşar.
  - **Mobil:** ayrıştırıcının bilinmeyen üyeyi yok saydığı **ölçülmedi**. CT, dondurmadan önce mobil ekipten yazılı teyit alır (§18);
    teyit yoksa yeni sürüm mobil uygulama güncellenmeden canlıya çıkmaz.
- `contractVersion` `"v1"` sabit kalır (şema `const`), bu yüzden istemci sunucunun yeni sürümde olup olmadığını cevaptan anlayamaz.
- **Mobil kuralı (mobil cevabıyla aynı: "alan yoksa bugünkü davranış kalır"):** üye yoksa istemci bugünkü davranışını sürdürür. Süzgeç için
  istemci her `CycleCount`'un zaten taşıdığı `assignedTo` üyesine göre kendi tarafında süzmeyi bırakmamalıdır: 1.1 sunucusu bilinmeyen
  sorgu parametresini yok sayar (çıkarım, ölçülmedi: eylem yalnız adlı `[FromQuery]` parametrelerini bağlar; `services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/CycleCount/CycleCountsController.cs:32-36`),
  süzülmemiş liste döner. Bu not mobil sözleşme notuna CT tarafından yazılır (§18).
- Web bugünkü JSON'u okur; yeni üyeler Web'de kullanılmaz (§20).

### 8.5 Sözleşme eki (CT tek yazıcı adımı)

Eklemeli sürüm (numara dondurulma sırasıyla verilir — Q-4; bugün sıradaki **1.2.0**; MOD-0182 X-14 eki önce donarsa bir sonraki; emsal CT kararı WMS-BUNDLE sürüm sırası,
`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:57-59`):

| Yer | Ek |
|---|---|
| `components.schemas.CycleCountLine.properties` | `locationCode`, `skuCode`, `skuName`, `lotNumber` — her biri `$ref: '#/components/schemas/Text'`; `required` listesine **girmez** |
| `paths./cycle-counts.get.parameters` | `assignedTo` (query, uuid, isteğe bağlı), `unassigned` (query, boolean, isteğe bağlı; `true` = atanmamış, `false` = süzgeç yok; ikisi birlikte 400) |
| `info.version`, `x-changelog`, `info.description` | 1.1.0 deseni: sona eklenen bir paragraf (dört üyenin kuralı: satır açılırken bir kez yazılır, miktar değildir, `skuName` = PRODUCT-MASTER `getSku` `name` aynen, çevrilmez; süzgeç kuralı) + bir changelog satırı; 1.1.0 metni olduğu gibi kalır |
| `info.x-source-pack` | mevcut metnin sonuna `; MOD-0181-FU01 r<n> (sha256 …)` eklenir — r8 parçası kalır |

Değişmeyen: 25 işlem, bütün yollar, izinler, hata kataloğu (27 kod), enum'lar, `required` listeleri, kural 1–12, **`CycleCountLine.description`**
(r3, F-3 (c): şema açıklaması değişmez; kural `info.description` paragrafındadır). Sözleşmede `null` yok.

**Dondurma commit'inin tam listesi (r3, F-3; hepsi aynı commit'te, CT tek yazıcı):**

- (a) **Fikstür:** 1.1.0 dosyası `tests/Diten.InventoryService.Tests/Contract/Fixtures/wms-count-bundle-v1.1.0.openapi.yaml` olarak birebir kopyalanır;
  testte `FrozenV110Sha256 = "c78b20951446ec408f68a0583c1a7606a796ce48d1fdd852e233811fcd130b15"` sabiti (bugünkü 1.1.0'ın LF sha256'sı, HEAD'de ölçüldü; 1.0.0 emsali
  `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount/WmsCountBundleContractTests.cs:41`) ve fikstürün baytça eşitliği testi. Bugünkü eklemeli-sürüm testi (`:107-142`, "1.0.0 → DL-5") **1.0.0 fikstürü → 1.1.0
  fikstürü** karşılaştırmasına yöneltilir (tarihsel kanıt korunur); yeni test **1.1.0 fikstürü → güncel dosya** ve yalnız bu tablodaki ekleri kabul eder.
- (b) **Başlık testi** (`:69-87`): sürüm `:74`; `x-source-pack` `Contains` iddiası (ölçülen `:81`; inceleme `:80` der) r8 parçasını aramaya devam eder
  ve FU01 parçası için ikinci `Contains` eklenir; changelog uzunluğu (ölçülen `:84`; inceleme `:83` der) 2 → 3 ve üçüncü satır için
  `StartsWith`; test adı yeni sürümle.
- (c) `CycleCountLine.description` değişmez (yukarıda); AC-13'ün izin listesi buna göre.
- (d) **contracts README:** `docs/analysis/contracts/README.md` WMS-COUNT-BUNDLE satırlarının (`:37-38`) altına yeni sürümün `↳` satırı.
- (e) **Gateway testi (X-2):** `gateway/Diten.ApiGateway.Tests/Mod0181CycleCountRouteTests.cs` dört "1.1.0" metni — `:11`, `:40`, `:120` (`ContractVersion`), `:149`; uç sayısı 25 (`:32`) değişmez.

Parametre eşitliği testi (AC-14) bu listede **değildir**: controller parametreleri DL-1'de eklendiği için dondurma commit'inde kırmızı olur; DL-1'de
yazılır (r3, F-2). Dondurmadan sonra mobil ekibe sözleşme ve §8.4 notu gönderilir (CT).

## 9. Layout & Shell Contract

`shell: none` — Razor sayfası yok, `Layout = "..."` satırı yok. Bu FU hiçbir `.cshtml` dosyasına dokunmaz; ebeveynin `_LayoutTenantShell`
sayfaları değişmez.

## 10. Backend File Convention

Ebeveynin Golden Reference düzeni aynen (Commands / Queries / `Handlers/CommandHandlers` / `Handlers/QueryHandlers`; handler adı
`{Verb}{Module}Handler`, `Command` / `Query` soneki yok). Yeni dosya yok; değişen dosyalar §5'te. Liste parametreleri bugünkü gibi işleyicide
çözülür (FluentValidation validator'ı eklenmez — `GetCycleCountListHandler` deseni, `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CycleCounts/Handlers/QueryHandlers/GetCycleCountListHandler.cs:26-33`).
DTO'lar tek modeller dosyasında kalır (`CycleCountModels.cs`).

## 11. Frontend File Contract

Yok. `golden_reference: none`; ekran, resx, JS, sayfa yardımı değişmez. Web'in yeni üyeleri kullanması takip işidir (§20).

## 12. Validation Rules

| Alan | Kural | Hata |
|---|---|---|
| `assignedTo` (sorgu) | kanonik UUID (`CycleCountSupport.TryParseId`, `warehouseId` ile aynı) | 400 `INVALID_REQUEST` |
| `unassigned` (sorgu) | tam olarak `true` ya da `false` (küçük harf) | başka değer → 400 `INVALID_REQUEST` |
| `assignedTo` + `unassigned=true` | birlikte gelemez | 400 `INVALID_REQUEST` |
| `tenantId` / `legalEntityId` sorguda | bugünkü gibi kapsam ara katmanında 400 | değişmez |
| `locationCode`, `skuCode`, `skuName`, `lotNumber` | yalnız sunucu yazar; istekte kabul edilmez (istek gövdeleri değişmez — bilinmeyen üye bugünkü gibi 400) | 400 `INVALID_REQUEST` |
| dört üyenin değeri | kırpılmış, boş değil; kimlik / yer tutucu / çeviri metni değil (§4.1, §8.2) | — (yazılmaz) |

## 13. Failure Path to Verify

| Senaryo | Beklenen |
|---|---|
| Duplicate — aynı release / bulunan satır isteği tekrar (aynı anahtar) | bugünkü 200 replay; replay'deki satır dört üyeyi ilk yazımdaki gibi taşır |
| Missing — PRODUCT-MASTER cevabında `canonicalCode` / `name` yok | release / bulunan satır başarılı; `skuCode` / `skuName` üyesi yok |
| Missing — kayıtlı lot biçimindeki `LotId` lot koleksiyonunda yok | `lotNumber` yok; satır yazılır |
| Unavailable — PRODUCT-MASTER erişilemez | bugünkü 503 `PRODUCT_MASTER_UNAVAILABLE`, hiçbir şey yazılmaz (değişmez) |
| Unauthorized — `counts.read` yok | liste 403 (değişmez); yeni izin kontrolü yok |
| Cross-company — başka şirketin sayımı aynı kullanıcıya atanmış | `assignedTo` süzgeciyle de listede yok |
| Bad filter — `assignedTo=abc`, `unassigned=yes`, ikisi birlikte | 400 `INVALID_REQUEST` |
| Concurrency — release yarışı / bulunan satır yarışı | bugünkü davranış (409 `COUNT_CONFLICT` / replay); dört üye kazanan yazımla aynı işlemde |
| Blind — kör sayım, submit öncesi | dört üye var, beş kör üye yok |
| Rewrite — giriş, submit, onay, ret, yeniden sayım, retry, resolve sonrası | dört üye ilk yazımdaki gibi (AC-16) |

## 14. Authorization Convention

İzin değişmez. `listCycleCounts` `cycle-count.counts.read`; satır uçları ebeveyndeki gibi. `CycleCountPermissions.Implemented` ve manifest
(`CYCLE-COUNTING`) değişmez; `.antigravity` PKS-001 satırı değişmez. Aktör `tenant_user`, politika `[Authorize]` + `[HasPermission]`.
Dört üye satırı okuyabilen herkese gelir (ebeveyn §14: Sayan = `counts.read` + `counts.record`); MDM / Location / Lot okuma izni istenmez,
verilmez (K-2). Görünür kılınan bilgi yalnız o sayımın satır anahtarlarının kodlarıdır; kişisel veri yok.

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**. Uç `/api/warehouse/cycle-counts` mevcut `/api/warehouse/{everything}` → 5067 rotası altındadır;
`ocelot.json` değişmez. Yeni uç yoktur: `Mod0181CycleCountRouteTests` 25 uç (`:32`) ve `Mod0178WarehouseRouteTests` sayısı değişmez.
Tek dokunuş X-2: CT dondurma adımında `Mod0181CycleCountRouteTests.cs` `ContractVersion` sabiti (`:120`) yeni sürüme — aynı commit; dosyanın
sahibi integration-agent (ebeveyn DL-6), değişiklik sürüm metniyle sınırlı. Sorgu parametreleri Gateway'den olduğu gibi geçer.

## 16. Acceptance Criteria

- [ ] AC-1 (zorunlu) — Release edilen sayımın her Snapshot satırı, `GET /api/warehouse/cycle-counts/{countId}/lines` cevabında
  `locationCode` (bin'in Location `Code`'u), `skuCode` (`getSku` `canonicalCode`), `skuName` (`getSku` `name`) ve kayıtlı lotlu satırda
  `lotNumber` (`Lot.LotNumber`) taşır; aynı üyeler `getCountVariance` `line` içinde de gelir. Sabotaj **S-FU01-1:** release satır kurucusunda
  `SkuCode` atamasını sil → test kırmızı.
- [ ] AC-2 (zorunlu) — `addCycleCountLine` 201 cevabı ve sonraki satır listesi bulunan satırda dört üyeyi taşır; `lotNumber` doğrulanan kayıtlı
  lotun numarasıdır. Sabotaj **S-FU01-2:** `AddCountLineHandler`'da `LotNumber` atamasını sil → test kırmızı.
- [ ] AC-3 (zorunlu) — `requestCountRecount` ile açılan sayımın Recount satırları dört üyeyi kaynak satırdan aynen taşır. Sabotaj **S-FU01-3:**
  `RecountLine` içinde `LocationCode` kopyasını sil → test kırmızı.
- [ ] AC-4 (zorunlu) — Eksik kaynakta üye yazılmaz ve yazım durmaz: (a) sahte PRODUCT-MASTER `canonicalCode` / `name` göndermezse release 200,
  satırda `skuCode` / `skuName` yok; (b) kayıtlı biçimdeki `LotId` için lot yoksa `lotNumber` yok; (c) elle yazılmış lot metni (`LOT-A1`)
  `lotNumber` olarak aynen gelir; (d) hiçbir üye UUID, boş metin ya da JSON null değildir. Sabotaj **S-FU01-4a:** ad yokken `SkuName = SkuId`
  yaz → (d) kırmızı; **S-FU01-4b:** ad yokken release'i 422 ile reddet → (a) kırmızı.
- [ ] AC-5 (zorunlu) — Kodlar ek PRODUCT-MASTER çağrısı doğurmaz: N farklı SKU'lu release'te sahte istemcinin `GetSkuAsync` çağrı sayısı FU
  öncesindeki ile aynıdır (N); bulunan satırda 1. Release yine **tek anlık görüntü işlemi** açar (r4, F-10): ebeveynin
  `AC52a_a_posting_during_the_release_is_in_both_reads_or_in_neither` testi (`Assert.Equal(1, probeCalls)`) değişmeden yeşil kalır ve FU01'in
  lotlu, birden çok bin'li release vakası aynı sayıyı iddia eder. Sabotaj **S-FU01-5:** kod / ad için ayrı bir `GetSkuAsync` çağrısı ekle → sayı
  iddiası kırmızı; **S-FU01-5b:** ön okumadan bin kodlarının önbelleğe yazılmasını kaldır (yalnız `NeedsFlags` yedeği kalsın) → AC52a ve FU01
  vakası kırmızı (`probeCalls` 2).
- [ ] AC-6 (zorunlu) — Kör sayım kuralı korunur: kör sayımda submit öncesi (ve submit sonrası `variances.read` olmayan çağırana) satır cevabı
  dört görünen üyeyi taşır, beş kör üyeden hiçbirini taşımaz; kör olmayan sayımda ikisi birden gelir. Sabotaj **S-FU01-6a:** dört üyeyi
  `showExpected` koşuluna bağla → kör vakada üye eksik, kırmızı; **S-FU01-6b:** `Line` eşlemesinde `ExpectedQuantity`'yi koşulsuz yaz → kör
  vaka kırmızı.
- [ ] AC-7 (zorunlu) — `skuName` dil kuralı (§8.2): aynı satır `Accept-Language: tr` ve `Accept-Language: ar` ile aynı `skuName`'i döner ve bu
  değer sahte PRODUCT-MASTER'ın `name`'ine eşittir; servis resx metni yazmaz. Sabotaj **S-FU01-7 (r3, F-4):** `CycleCountReads.Line`'da
  `SkuName`'i isteğin diline göre seç (ör. `Accept-Language` `tr` değilse `SkuCode`'u yaz) → `ar` vakası kırmızı. (r2'deki "okunamadı
  metnini yaz" sabotajı bu AC'yi değil AC-4 (a)'yı kırmızıya çevirir; AC-4 S-FU01-4a / S-FU01-4b ile ölçülür.)
- [ ] AC-8 (zorunlu) — `GET /api/warehouse/cycle-counts?assignedTo={u}` yalnız `AssignedTo = u` sayımlarını döner; `status`, `warehouseId`, `q` ile
  AND'lenir; `total` süzülmüş sayıdır; 250 kayıtta `pageSize=200` ile ikinci sayfa kalan 50'yi verir. Sabotaj **S-FU01-8:** depo süzgecinde
  `AssignedTo` koşulunu atla → test kırmızı.
- [ ] AC-9 (zorunlu) — `unassigned=true` yalnız atanmamış sayımları döner; `unassigned=false` süzgeçsiz listeyle aynıdır; `assignedTo=abc`,
  `unassigned=yes` ve `assignedTo` + `unassigned=true` 400 `INVALID_REQUEST`. Sabotaj **S-FU01-9:** ikisi-birlikte denetimini sil → test kırmızı.
- [ ] AC-10 (zorunlu) — Kiracı / şirket izolasyonu: aynı kullanıcıya atanmış başka şirketin (ve başka kiracının) sayımı `assignedTo` süzgeciyle
  listede yoktur; o sayımın satırları 404'tür. Sabotaj **S-FU01-10:** `ListAsync`'te süzgeci `Company<CycleCount>()` yerine yalnız
  `AssignedTo` ile kur → test kırmızı.
- [ ] AC-11 (zorunlu) — İzinler değişmez: yalnız `cycle-count.counts.read` taşıyan kullanıcı (Sayan rolü) dört üyeyi ve süzgeci alır;
  `CycleCountPermissions.Implemented` FU öncesiyle aynıdır (mevcut `Every_served_cycle_count_route_is_an_operation_with_its_permission_key` yeşil).
  Sabotaj **S-FU01-11:** dört üyeyi `variances.read` sahibine sınırla → test kırmızı.
- [ ] AC-12 (zorunlu) — Olaylar ve denetim değişmez: `Released` / `LineAdded` olay `Data`'sında dört üye yoktur; merkezi denetim yazma komutu
  sayısı (`Every_write_command_declares_its_central_audit_record`, bugün `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs:59` `Assert.Equal(133, WriteCommands.Length)`) ve işlem
  çağrı yeri sayısı (`Every_transaction_a_command_can_run_in_enlists_the_central_audit_event`, bugün `:140` `Assert.Equal(16, seen)`) dilim başında
  ölçülen değerle aynıdır; ölçüm: `dotnet test --filter "FullyQualifiedName~CentralAuditDeclarationTests"` dilim başında ve sonunda (r3, F-9).
  Sabotaj **S-FU01-12:** `LineAdded` verisine `skuName` ekle → test kırmızı.
- [ ] AC-13 (zorunlu) — Sözleşme eki (CT adımı): yeni sürüm 1.1.0 fixture'ına göre **yalnız** §8.5 eklerini taşır (dört isteğe bağlı `Text`
  üyesi, iki isteğe bağlı sorgu parametresi, `info.version`, bir `x-changelog` satırı, sona eklenen `info.description` paragrafı, `x-source-pack`
  eki — `CycleCountLine.description` değişmez); 1.1.0 fikstürü sha256 sabitine eşittir; işlem sayısı 25, hata kataloğu 27 kod; servis
  cevapları yeni dosyaya göre geçerlidir (§8.5 a–e). Sabotaj **S-FU01-13:** `skuName`'i `CycleCountLine.required`'a ekle → eklemeli-sürüm testi kırmızı.
- [ ] AC-14 (zorunlu) — Sorgu parametresi eşitliği (DL-1; r3, F-2): yeni sözleşme testi `listCycleCounts` sorgu parametrelerini (`status`, `warehouseId`, `q`,
  `assignedTo`, `unassigned`, `page`, `pageSize`) `CycleCountsController.List` eyleminin `[FromQuery]` adlarıyla iki yönde eşit bulur.
  Sabotaj **S-FU01-14:** eylemdeki `unassigned` parametresini kaldır → test kırmızı.
- [ ] AC-15 (zorunlu) — Kanıt maddesi: Inventory test projesinin tamamı (süzgeçsiz), Gateway test projesi (`Mod0181CycleCountRouteTests` 25 uç,
  `Mod0178WarehouseRouteTests` sayısı değişmeden) ve Web test projesi değişmeden yeşil; DL-1 canlı ölçümünde (E4) PRODUCT-MASTER'ın `getSku`
  `name` gönderip göndermediği yazılır. Kendi sabotajı yok (AC-1…AC-14 ve AC-16'nın toplamı).
- [ ] AC-16 (zorunlu) — Dört üye satırın sonraki her yazımında korunur (r3, F-1): release edilen bir sayımda bir satıra giriş yapılır, sayım
  submit edilir, farklı satırlar onaylanır, reddedilir, yeniden sayıma gönderilir, `Pending` düzeltme retry edilir ve `IntegrityError` resolve
  edilir; her adımdan sonra satır listesi ve `getCountVariance` `line` dört üyeyi ilk yazımdaki değerle taşır. Sabotaj **S-FU01-16a:**
  `SubmitCycleCountHandler.WithVariance`'ta `SkuCode`'un sonuca taşınmasını kaldır (açık kopyayı sil ya da `Copy()` sonrası `SkuCode = null`) →
  submit sonrası vaka kırmızı; **S-FU01-16b:** `RecordCountLineHandler`'ın satır kurucusunda `LocationCode`'un taşınmasını kaldır → giriş
  sonrası vaka kırmızı.

## 17. Test Expectations

| Test | Yer | AC |
|---|---|---|
| Tek anlık görüntü işlemi (koruyucu) | ebeveynin `CycleCountReleaseTests.cs` `AC52a_a_posting_during_the_release_is_in_both_reads_or_in_neither` (değişmez) + `CycleCountDisplayCodesTests.cs` lotlu çok bin'li vaka | AC-5 |
| Release / bulunan satır / yeniden sayım kod yazımı, eksik kaynak, çağrı sayısı | yeni `CycleCountDisplayCodesTests.cs`; sahte PRODUCT-MASTER paylaşılan `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/TestInfrastructure/StubProductMaster.cs` (`SetDisplay` adı → `getSku` `name`; `GetSkuCalls` sayacı — X-3) | AC-1, AC-2, AC-3, AC-4, AC-5, AC-7 |
| Sonraki yazımlarda korunma (giriş, submit, onay, ret, yeniden sayım, retry, resolve) | `CycleCountDisplayCodesTests.cs` | AC-16 |
| Kör sayım iki yön | `CycleCountDisplayCodesTests.cs` (mevcut kör testleri değişmeden yeşil) | AC-6 |
| Liste süzgeci, doğrulama, sayfalama, izolasyon | yeni `CycleCountListFilterTests.cs` | AC-8, AC-9, AC-10 |
| İzin ve manifest değişmez | mevcut `WmsCountBundleContractTests`, `CycleCountAccessTests` + yeni Sayan vakası | AC-11 |
| Olay verisi ve denetim sayıları | yeni vaka + mevcut `CentralAuditDeclarationTests` | AC-12 |
| Eklemeli sürüm, fikstür sha256, başlık | `WmsCountBundleContractTests.cs` (CT dondurma adımı) | AC-13 |
| Parametre eşitliği | `WmsCountBundleContractTests.cs` (DL-1) | AC-14 |
| Tam koşu | Inventory (kendi mongod'u), Gateway, Web test projeleri; `dotnet build` uyarısız | AC-15 |

Her sabotaj tek satırdır, koşulur, kırmızı görülür ve geri alınır; kabul kaydı her S-FU01-n için önce / sonra sonucunu yazar. RESX, smoke,
`verify_datatable_page.py` yok (ekran yok).

## 18. Ready-for-dev Checklist

- [x] Golden Reference: `none` (ekran yok); ebeveynin backend düzeni okundu (§10)
- [x] Frontmatter zorunlu alanlar dolu (service, shell, golden_reference, entity_base)
- [x] Layout & Shell Contract: `shell: none`, Razor yok (§9)
- [x] Backend File Convention ebeveynle aynı (§10)
- [x] Frontend File Contract: yok (§11)
- [x] Validation Rules her yeni parametre / üye için (§12)
- [x] Failure Path en az 4 senaryo (§13)
- [x] Authorization: izin değişmez, aktör, politika (§14)
- [x] Gateway kararı: gereksiz; X-2 sürüm sabiti (§15)
- [x] Acceptance criteria kimlikli, her biri adlı sabotajlı (§16)
- [x] Test expectations (§17)
- [x] Audited Events tablosu (§21)
- [x] Sayfa yardımı: sayfa yok; tur yok (§25)
- [x] Q-1…Q-4 sahip / CT seçimi (§19.4; `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63`)
- [ ] Bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01, onaylayan Ali)
- [x] X-1 sahip onayı (XMC-001, Beste, 2026-11-13)
- [ ] CT: registry satırı `MOD-0181-FU01`; dondurmadan sonra mobil sözleşme notu (§8.4 süzgeç uyarısı dahil)
- [ ] CT: dondurmadan önce mobil ekipten bilinmeyen üye toleransı teyidi (§8.4, r3 F-8)
- [x] X-3 sahip teyidi (paylaşılan `StubProductMaster.cs`; Beste, `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:67`, commit `4fa71d957`)
- [x] r2 inceleme bulguları F-1…F-9 kapandı (§19.5)
- [x] r3 inceleme bulgusu F-10 kapandı (§19.6)

## 19. Implementation Notes

### 19.1 Okunan kaynaklar (bu oturum, HEAD `ca36b19be`)

- Sahip kararları: `docs/records/decisions/2026-11/mod0181-owner-decisions-2026-11-12.md` (D-1, K-2 eki `:13-16`);
  `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md` (K-4 `:6-8`, WMS-BUNDLE sürüm sırası `:57-59`).
- Mobil girdisi: `execution/domains/supply-chain-execution/work-packs/mobile/2026-11-13-cycle-count/WMS-CEVAP-2026-11-13-sayim.md` (soru 2, 3)
  ve `WMS-CEVAP-2026-11-13-sayim-02.md:18-21` (K-2 / K-3 kabulü, "o zamana kadar bugünkü davranışınız doğru").
- Ebeveyn paket r8 (§0, §14, §15, §20–§25); donmuş sözleşme 1.1.0; PRODUCT-MASTER sözleşmesi (`SkuRef.name`); domain-config;
  `.antigravity/rules/module-pack-standard.md` (§2, §6, §10.1, §16); MOD-0178-FU01 paketi (biçim emsali); MOD-0182 paketi (`Resolved` üyeleri).
- Kod: §5'teki dosyalar, `CountEvidenceDocumentBuilder.cs` (ad okuma emsali), `ScanEvent.cs`, `Mod0181CycleCountRouteTests.cs`,
  `WmsCountBundleContractTests.cs`, `CycleCountContract.cs` (test).

### 19.2 Ölçülen gerçekler

- `CycleCountLine` kapalı şema, kimlik dışında görünen üye yok (`wms-count-bundle.openapi.yaml:672-704`); sürüm `1.1.0` (`:74`).
- `listCycleCounts` parametreleri `status`, `warehouseId`, `q`, `page`, `pageSize` (`wms-count-bundle.openapi.yaml:117-135`); depo süzgeci
  `CycleCountRepository.cs:85-97`.
- Release seri bayrağı için SKU başına bir `GetSkuAsync` yapar (`CycleCountProductFlags.cs:30-44`); HTTP istemcisi `canonicalCode`'u okur,
  SKU `name`'ini okumaz (`ProductMasterHttpClient.cs:65-84`; `SkuReference`'ta `Name` yok — `services/Diten.InventoryService/src/Diten.InventoryService.Application/Common/Upstream/SkuReference.cs:16-28`).
- Bin kodu okuması var: `LocationNamesAsync` (`CycleCountRepository.cs:321-328`, TenantId + LegalEntityId süzgeçli).
- Evidence aynı adları dosya için okuyup "okunamadı" sözcüğüne düşer (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/CountEvidenceExports/CountEvidenceDocumentBuilder.cs:76-105`).
- Servis cevapları entegrasyon testlerinde donmuş dosyanın kapalı şemasına göre doğrulanır (`services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount/CycleCountContract.cs:9-14`)
  — dosyada olmayan bir üyeyi yazan servis cevabı kırmızı olur; Q-3 = A (önce dondurma) bu yüzden taslak doğrulayıcıyı gereksiz kılar.
- Gateway testi sürüm metnini sabitler (`Mod0181CycleCountRouteTests.cs:120` `"1.1.0"`) ve uç sayısını (`:32` 25); "1.1.0" metni ayrıca
  `:11`, `:40`, `:149`'da.
- (r3) Satırı alan alan yeniden kuran iki işleyici var: giriş (`RecordCountLineHandler.cs:53-61`) ve submit (`SubmitCycleCountHandler.cs:181-189`);
  `ReplaceLinesAsync` belgeyi bütünüyle değiştirir (`CycleCountRepository.cs:401-408`). Diğer satır yazanlar `Copy()` kullanır
  (`ApproveCountVarianceHandler`, `RejectCountVarianceHandler`, `RequestCountRecountHandler`, `RetryCountAdjustmentHandler`,
  `ResolveCountAdjustmentIntegrityHandler`, `CycleCountPostingEngine`). İnceleme yalnız submit'i adlandırdı; giriş r3 ölçümüdür.
- (r3) 1.1.0 dosyasının LF sha256'sı `c78b20951446ec408f68a0583c1a7606a796ce48d1fdd852e233811fcd130b15` (HEAD ve çalışma ağacı aynı). Sahte PRODUCT-MASTER `SetDisplay` adı bugün yalnız
  `getProduct`'a koyar (`StubProductMaster.cs:86`), `getSku`'ya koymaz (`:103`).

### 19.3 Çıkarımlar ve varsayımlar

- Ebeveynin DL-1…DL-5 kodu yalnız MVP-1 hattında ve canlıda değil; bu yüzden eski satırlar için doldurma gerekmez (varsayım: FU, hat main'e
  gelmeden ya da kiracı sayımı başlamadan canlıya çıkar; değilse eski satırlarda üyeler yoktur ve istemci kuralı §8.4 bunu karşılar).
- PRODUCT-MASTER `getSku` `name` canlı MDM'de gönderiliyor mu ölçülmedi; gönderilmiyorsa `skuName` gelmez, diğer üç üye gelir (AC-4, AC-15).
- Seçilen sıra A (Q-3) ile servis 1.1 cevapları yeni dosyaya göre de geçerlidir — bütün ekler isteğe bağlı (çıkarım, şemadan); dondurma
  commit'inde mevcut testlerin yeşil kalması bunun ölçüsüdür.

### 19.4 Seçimler (r1'de açık sorulardı; r2'de seçildi)

Kaynak: `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63` (commit `a1979335f`).

| # | Soru | Seçim | Kim | Pakette |
|---|---|---|---|---|
| Q-1 | Görünen üyeler ne zaman yazılır? | A — satır açılırken (release / bulunan / yeniden sayım) bir kez yazılır, sonra değişmez; kanıt kopyası (§2.3 kural 3). B (her okumada çözme) seçilmedi | Beste | §1, §2.3, §4.1, §8.1 |
| Q-2 | `skuName` dil kuralı | MDM `getSku` `name` aynen: tek değerli, dil boyutu yok, `Accept-Language` etkilemez, yoksa üye yazılmaz; K-4 aynı kuralı kullanır | Beste | §8.2, AC-4, AC-7 |
| Q-3 | Sözleşme ile kodun sırası | A — önce CT sözleşme dondurma, sonra DL-1 (donmuş dosyaya göre ölçülür; taslak doğrulayıcı yok) | CT | §8.5, §23, §24 |
| Q-4 | Sürüm numarası | dondurulma sırasıyla numaralanır (WMS-BUNDLE kararı emsal); bugün sıradaki 1.2.0, MOD-0182 X-14 eki önce donarsa bir sonraki | CT | §1, §8.5 |
| X-1 | MOD-0173 iki dosyaya eklemeli SKU `name` okuması | evet — XMC-001 sahip onayı | Beste | §5, §24 |

### 19.5 r2 → r3 kapanış tablosu (inceleme `docs/records/reviews/MOD-0181-FU01/2026-11-13-96648a74.md`)

| Bulgu | Önem | Kapanış | Yer |
|---|---|---|---|
| F-1 | ENGEL | `SubmitCycleCountHandler.cs` DL-1 Repo Scope'ta; §4.1 "submit dokunmaz" kaldırıldı; AC-16 + S-FU01-16a. r3 ölçümüyle aynı kusur girişte de var (`RecordCountLineHandler`) — Repo Scope + S-FU01-16b | §3, §4.1, §5, §8.1, §13, §16, §24 |
| F-2 | ORTA | AC-14 ve S-FU01-14 DL-1'de; dondurma listesinde değil | §5, §8.5, §16, §17, §24 |
| F-3 | ORTA | dondurma listesi (a) 1.1.0 fikstürü + `FrozenV110Sha256` ve iki karşılaştırma testi, (b) başlık testi `:81` / `:84` (inceleme `:80` / `:83`), (c) `CycleCountLine.description` değişmez — izin listesinden çıkarıldı, (d) README `↳` satırı, (e) Gateway testi `:11` / `:40` / `:120` / `:149` | §5, §8.5, AC-13, §24 |
| F-4 | ORTA | S-FU01-7 artık AC-7'nin kendi sabotajı (dile göre `SkuName` seçimi); eski metin AC-4 (a)'ya bağlandı | §16 |
| F-5 | ORTA | sahip kararı (Beste, `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:64-66`): `skuName` her yerde `getSku` `name`; MOD-0182 `ScanResolved` FIX-01'de hizalanır | §2.3 kural 3, §8.2, §20 |
| F-6 | DÜŞÜK | release'te bin kodu ve lot numarası işlem dışında, `NeedsFlags` deseniyle okunur; `ILotRepository` yeni bağımlılık | §5, §8.1 |
| F-7 | DÜŞÜK | sahte PRODUCT-MASTER `StubProductMaster.cs` adıyla Repo Scope'ta; çağrı sayacı orada (X-3) | §5, §17, §24 |
| F-8 | DÜŞÜK | "kırılmaz" yalnız ölçülen tüketici için; Web koddan okundu, mobil teyidi CT'de | §8.4, §18 |
| F-9 | DÜŞÜK | AC-12'ye iki test adı, satırları ve koşu komutu | §16 |

### 19.6 r3 → r4 kapanış tablosu (inceleme `docs/records/reviews/MOD-0181-FU01/2026-11-13-a78a6bbb.md`)

| Bulgu | Önem | Kapanış | Yer |
|---|---|---|---|
| F-10 | ORTA | bin kodu ve lot numarası işlemden önce, genişletilmiş ön okumadan (`ScopePreviewAsync`: aynı iki sorgu, bin + SKU + lot kimliği) önbelleğe; anlık görüntü işlemi yalnız önbellekten yazar; `NeedsFlags` yalnız arada gelen değerler için yedek; ebeveynin AC52a testi koruyucu olarak AC-5'te, S-FU01-5b | §5, §8.1, §16, §17, §24 |

## 20. Follow-up Items

- **FU-0181-FU01-WEB** — Web sayım formu ve Variance Trace adları saklanan dört üyeden okur (`MasterLabelResolver` ana veri okumaları yerine);
  Sayan rolü Web'de de adı görür (mobil cevabı soru 2 "Web de aynı sorunu yaşıyor"). Ayrı paket ya da ebeveyn DL; XMC yok (MOD-0181 Web dosyaları).
- **FU-0181-FU01-EVIDENCE** — Evidence Pack, satırda saklanan kodu varsa onu kullanır (aynı değer, daha az okuma). Davranış değişikliği yok; isteğe bağlı.
- **K-4** — WMS-BUNDLE `WarehouseTask` için aynı desen (MOD-0178 FU'su; §8.2 dil kuralı).
- **MOD-0182 FIX-01** — `ScanResolved.SkuName` `getProduct` `name` yerine `getSku` `name` (sahip kararı, `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:64-66`); MOD-0182'nin işi,
  bu paket o dosyaya dokunmaz.
- **Çok dilli ürün adı** — PRODUCT-MASTER'a gelirse `skuName` kuralı yeni eklemeli sürümle değişir (§8.2 madde 6).

## 21. Audited Events

Servisin denetim altyapısı **bugün var** (`tests/architecture/audit-ledger/Diten.InventoryService.md`, `inventory-merkezi-iletim`, yol b).
Bu FU yeni yazma komutu eklemez; üç mevcut komutun yazdığı satır alanları artar. Olay adları, yol ve kayıt sayıları ebeveyn §21 ile aynıdır.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| ReleaseCycleCountCommand | ReleaseCycleCountCommand + LifecycleTransition | CycleCount | b | ebeveynle aynı; dört görünen üye olay verisine **yazılmaz** (türetilmiş gösterim verisi; satırda durur) | — |
| AddCountLineCommand | AddCountLineCommand + Create | CycleCountLine | b | ebeveynle aynı (bin, SKU, lot, CountedQuantity); dört üye olay verisine yazılmaz | — |
| RequestCountRecountCommand | RequestCountRecountCommand + Create | CycleCount (yeni) | b | ebeveynle aynı; dört üye kaynak satırdan kopyalanır, olay verisine yazılmaz | — |

`RecordCountLineCommand` ve `SubmitCycleCountCommand` (r3, F-1) dört üyeyi yalnız taşır, yeni değer yazmaz; olay adları ve verileri ebeveyn
§21'deki gibi kalır. Liste süzgeci okumadır, kayıt doğurmaz. Kayda yazılmayacak alan: yok (parola / token / özel nitelikli veri yok).

## 22. Record Codes

Yeni kayıt türü yok; kod üretilmez. `locationCode` ve `skuCode` başka modüllerin kodlarının kopyasıdır, RCS-001 kapsamında yeni tür değildir.

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek: satırda dört görünen üye (release / bulunan / yeniden sayım yazımı, okuma eşlemesi, dil kuralı) + liste `assignedTo` / `unassigned` süzgeci + indeks (arka uç; izin ve uç değişmez) | Owned Objects, Entity Fields, Indexes, Görünen kodların yazımı, `skuName` dil kuralı (K-2 "dil kuralıyla"; K-4 bu kuralı kullanır), Liste süzgeci (`listCycleCounts`), Geriye uyum ve tüketiciler, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events | E4 | plan kimliği yok | plan satırı yok |

**CT sözleşme dondurma adımı** (dilim değildir; CT tek yazıcı): §8.5 — Sözleşme eki (CT tek yazıcı adımı), Gateway / API Routing Decision
(X-2 Gateway testi sürüm metinleri), AC-13 (§8.5 a–e). Sıra Q-3 = A: CT dondurması DL-1'den **önce**. AC-14 DL-1'dedir (r3, F-2).

Plan hizalaması: plan satırı bu oturuma verilmedi; plan dosyası depoda değil — "plan satırı yok" (`module-pack-standard.md` §16).

## 24. Dilim kapıları ve bağlı kararlar

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 inceleme kaydı + CT-APR-01 onay satırı, onaylayan Ali) hiçbir dilim başlamaz.

| Adım | Başlamadan kapanması gereken | Seam'ler (XMC-001) | AC | Sabotajlar |
|---|---|---|---|---|
| CT dondurma | seçimler yapıldı (Q-1…Q-4); paket onayı; mobil bilinmeyen üye toleransı teyidi (§8.4) | X-2 `Mod0181CycleCountRouteTests.cs` dört sürüm metni (integration-agent dosyası; yalnız sürüm metni, aynı commit) | AC-13 | S-FU01-13 |
| DL-1 | CT dondurması (Q-3 = A); X-1 ve X-3 onaylı | X-1 MOD-0173 `SkuReference.cs` + `ProductMasterHttpClient.cs` (sona isteğe bağlı `Name`; mevcut çağıranlar değişmez — konumlu kurucu korunur); X-3 paylaşılan `StubProductMaster.cs` | AC-1…AC-12, AC-14, AC-15, AC-16 | S-FU01-1, S-FU01-2, S-FU01-3, S-FU01-4a, S-FU01-4b, S-FU01-5, S-FU01-5b, S-FU01-6a, S-FU01-6b, S-FU01-7, S-FU01-8, S-FU01-9, S-FU01-10, S-FU01-11, S-FU01-12, S-FU01-14, S-FU01-16a, S-FU01-16b (AC-15 kanıt maddesi — kendi sabotajı yok) |

| XMC | Dosya | Değişiklik | Sahip | Durum |
|---|---|---|---|---|
| X-1 | `services/Diten.InventoryService/src/Diten.InventoryService.Application/Common/Upstream/SkuReference.cs`, `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Upstream/ProductMasterHttpClient.cs` | `SkuReference`'a sona `string? Name = null`; istemci `getSku` cevabından `name`'i `OptionalString` ile okur (`canonicalCode` satırının yanında). Yalnız gösterim; hiçbir kural okumaz. Emsal: "owner step MOD-0173 (upstream okumaları): SKU kodu" (`bd50db68b`) | MOD-0173 (merkez, Beste) | sahip onaylı (XMC-001, Beste, 2026-11-13, `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63`) |
| X-2 | `gateway/Diten.ApiGateway.Tests/Mod0181CycleCountRouteTests.cs` | dört "1.1.0" metni (`:11`, `:40`, `:120` `ContractVersion`, `:149`) yeni sürüm; uç sayısı 25 değişmez | integration-agent (ebeveyn DL-6) | CT dondurma commit'inde |
| X-3 | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/TestInfrastructure/StubProductMaster.cs` | eklemeli: `GetSkuAsync` cevabına `SetDisplay` adı (`Name`, `:103` yanında) ve `GetSkuCalls` sayacı; mevcut testler değişmeden yeşil | paylaşılan test altyapısı (MOD-0173 kökenli; merkez, Beste) | sahip onaylı (XMC-001 X-1 onayının kapsamında; Beste, "Evet", `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:67`, commit `4fa71d957`) |

Tek çalışma dalı: `feature/sce/mod-0173-inventory-ledger-valuation` (ebeveyn Q-23 = A).

## 25. Page Help (UXH-001)

Sayfa yok; yardım anahtarı eklenmez, değişmez. **Tur yok.**
