# Entegrasyon aşaması ve MVP-5 başlangıç planı (2026-10-21)

> Hazırlayan: MVP-1 Control Tower. Kaynak ölçümler `origin/main` `61450ee75` üzerinde, salt okunur (2026-10-21).
> Bu belge plan ve karar listesidir; onay ya da iş dağıtımı değildir. Kararlar sahibindir.
> **Sahip kararları (Beste, 2026-10-21):** E-1…E-7, W-1, W-4 ve W-10 verildi; bkz. §2.1 ve §4.1.

## 0. Neden şimdi
MVP-1 (MOD-0173…0177, MOD-0022) ve merkezi denetim iletimi (BL-501) main'de (PR #183). MVP-2/3/4/6'nın MVP-1'e ve MDM'e
bağlanması büyük ölçüde **taklit (mock) ya da izin verici (permissive) stub** üzerinden duruyor; MVP-5 (Depo / WMS) ise hiç
başlamadı ve MVP-1'in en yoğun tüketicisi olacak. **R8:** 2026-11-18'den sonra yeni işler yalnız kayıtlı görev kaynağından
(`execution/delivery/ct-queue-registry.tsv` + `scripts/ct_queue.py`) ya da sahibin süreli istisnasıyla dağıtılabilir.

## 1. Bugünkü bağlantı durumu (özet)

| MVP | MVP-1'e bağlantı | MDM PRODUCT-MASTER | BL-501 denetim | Correlation |
|---|---|---|---|---|
| MVP-2 Procurement (5065) | **GRN → Inventory hareketi MOCK** (`MockInventoryPostingClient`); lot TRACE'te açılmıyor; GoodsReceived olayının tüketicisi yok | **Permissive stub** (her ürünü kabul) | yok (35 komut, borç) | işlenmiyor |
| MVP-3 Manufacturing (5070) | tüketmiyor (kapsam gereği) | HTTP istemcisi hazır, varsayılan `Permissive` | yerel iz; merkeze iletim yok (hazırlık geri alınmış) | değiştirir |
| MVP-4 Planning (5068) / SafetyStock (5069) | **availability GERÇEK** (MRP); MRP ucu Gateway'de route'suz | kısmi; UoM dalı merge edilmemiş; SafetyStock URL'si boş | yerel izler (geçici karar) | işlenmiyor |
| MVP-6 SupplyChain (5066) | Recall'un sevkiyat okuması **GERÇEK**; Inventory okuma istemcisi DI'da yok; iade → CUSTOMER_RETURN yok | ürün doğrulaması yok | **defter yok** (17 komut) | reddeder (400) |

En yüksek risk: **MVP-2'de GRN "Posted" görünüyor ama stok oluşmuyor** (hareket taklitte kalıyor).

## 2. Önce verilecek sahip kararları (entegrasyon)

| # | Karar | Seçenekler | CT önerisi |
|---|---|---|---|
| E-1 | Platform genelinde correlation kuralı | A: geçersizi reddet (MVP-6) · B: değiştir (Inventory, Manufacturing) | Kimliği yalnız Gateway üretir; **her servis başlığı okur ve iletir** (bugün üç servis hiç işlemiyor); geçersiz için A (ret) — sonra Inventory küçük bir işle 400'e döner |
| E-2 | BL-501'e geçiş zamanı | A: şimdi, her servis · B: MVP'ler sonrası | **A** — iletim main'de, rehber (`central-audit-connect.md`) hazır; MVP-6'nın defteri yok (mimari test riski) |
| E-3 | Karantinada tedarikçi kimliği | A: içsel uuid (`EntityBase.Id`) · B: kod `SUP-…` | **A** + ekranda kod; sözleşme değişmez |
| E-4 | GRN → kalite / karantina | A: hareketin `toStockStatus=QUARANTINE` olması yeter · B: GoodsReceived olayı (MOD-0035 bus) | **A** (bus bağlı değil; Inventory karantinası hareketten çalışıyor) |
| E-5 | SupplyChain'in MDM'e doğrudan 5059'dan gitmesi | A: Gateway üzerinden · B: istisna | **A** (Çekirdek 5) |
| E-6 | "Bugün" için gün sınırı | A: UTC · B: kiracı / şirket saat dilimi | MVP-1 bugün A; Planning kendi `TimeZoneId`'sini taşıyor → **karar: A** (§2.1) |
| E-7 | MVP-6 kapsamı: sevkiyat satırında ürün doğrulaması; iadenin Inventory'ye CUSTOMER_RETURN olarak yazılması | dahil / sonra | dahil (iade → karantina zinciri MVP-1'de hazır) |

### 2.1 Verilen kararlar (2026-10-21)

| # | Karar |
|---|---|
| E-1 | Kimliği Gateway üretir; her servis `X-Correlation-Id`'yi okur ve her giden çağrıya iletir; geçersiz kimlik 400 ile reddedilir (Inventory ve Manufacturing ayrı küçük işle 400'e döner) |
| E-2 | Şimdi, her servis; BL-501 merkezi iletimi (`AddCentralAudit`, `central-audit-connect.md`) |
| E-3 | İçsel uuid tutulur, ekranda `SUP-…` kodu gösterilir |
| E-4 | GRN hareketi `toStockStatus=QUARANTINE` ile yazılır; ayrı olay yok |
| E-5 | SupplyChain MDM'e Gateway üzerinden gider |
| E-6 | **UTC.** Saklanan her zaman ve her kural gün sınırı (son kullanma, yeniden test, recall, `postingDate`) UTC gündür. Planning'in döngü `TimeZoneId`'si yalnız planlama takviminin hafta kovaları için kalır; Inventory / MDM ile tarih karşılaştırması UTC'ye çevrilerek yapılır |
| E-7 | Dahil: sevkiyat satırında MDM ürün doğrulaması ve iadenin `CUSTOMER_RETURN` (`toStockStatus=QUARANTINE`) olarak yazılması |

## 3. Entegrasyon işleri (dalga sırasıyla)

**Dalga 1: stok gerçeği (en yüksek risk)**

| İş | Sahip | Bağımlılık | Kabul |
|---|---|---|---|
| Procurement → MDM `POST /api/product-master/validate` (Manufacturing'deki `HttpProductReferenceValidator` deseni) | MVP-2 | PM-LIVE (anahtar beyanı) | MVP-1 CT |
| Procurement: GRN'de lot varsa önce TRACE `createLot`, sonra hareket `lotId` | MVP-2 | — | MVP-1 CT |
| Procurement: gerçek `IInventoryPostingClient` (Gateway `/api/inventory/movements`); DTO sözleşmeye eşlenir (`to*Id`, `lotId`, `transactionDate/postingDate`, Idempotency-Key, X-Correlation-Id) | MVP-2 | önceki iki iş | MVP-1 CT (INVENTORY tüketici testi) |
| MVP-6: denetim defteri + `AddCentralAudit` (önce zarflarda `IsSuccessful`) | MVP-6 | E-2 | MVP-1 CT + mimari test |

**Dalga 2: diğer bağlantılar**

| İş | Sahip |
|---|---|
| Manufacturing `ProductMaster:Mode=Http` + BL-501 hazırlığının geri getirilmesi | MVP-3 |
| Planning: MRP ucuna Gateway route'u; availability çağrısına X-Correlation-Id; PM UoM dalının bitirilip merge edilmesi | MVP-4 + integration-agent (ocelot) |
| SafetyStock: `Mdm:ProductMasterUrl` / `LegalEntitiesUrl` (Gateway), Gateway route'u, port 5069'un §3 tablosuna kaydı | MVP-4 + integration-agent + EA |
| SupplyChain: `InventoryReadClient` DI kaydı; iade → `CUSTOMER_RETURN` (E-7); MDM'e Gateway üzerinden (E-5) | MVP-6 |
| Karantina "tedarikçiye iade" alanı ↔ Procurement tedarikçisi (E-3, FU-0175-SUPPLIER-REF) | MVP-1 CT (bizim) + MVP-2 |
| Bloke / beklemedeki tedarikçiye işlem açılmaması (MVP-2 K-7) — tüketicilere iletim | MVP-2 + tüketici sahipleri |

**Dalga 3: ortak kurallar**
- Correlation kuralının (E-1) her serviste uygulanması; Inventory'nin 400'e dönmesi (küçük WP).
- Gün sınırı kararının (E-6) uygulanması.
- Olay taşıma (BOM sürüm olayları, sevkiyat olayları, MOD-0035 bus): bugün kapalı / kayıtsız; ayrı karar.

## 4. MVP-5 (Depo / WMS) başlangıç planı

**Modüller (Blueprint 8.8):** MOD-0178 Yerleştirme / Toplama / Paketleme (W-3) · MOD-0179 3PL Entegrasyonu (W-3, koşullu) ·
MOD-0180 Dalga Planlama (W-4) · MOD-0181 Sayım (W-4, yalnız 0173'e bağlı) · MOD-0182 Barkod / RFID (W-4, MOD-0365'e bağlı).
Paket, kod, servis ve dal yok; WAREHOUSE-OUTBOUND 1.0.0 (sahibi MOD-0178) **donmuş** ve MVP-6 onu bekliyor.

**MVP-1'de hazır olanlar:** hareket tipleri, kullanılabilirlik, LOCATION, TRACE, QUARANTINE, FEFO sırası, RECALL çıkış engeli.
**Boşluklar:**
- rezervasyon yalnız SKU düzeyinde; toplama için **lot / lokasyon rezervasyonu** gerekiyor. Bu, INVENTORY'ye eklemeli değişiklik; sahibi MOD-0173, yani biz.
- ambalaj seviyesi / GTIN MDM'de yok (P6 / P9, "MVP-5'ten önce" kabul edilmişti).
- GRN → yerleştirme tetikleyicisi tanımsız.

**MVP-5 kararları:**

| # | Karar | CT önerisi |
|---|---|---|
| W-1 | Yap mı / satın al mı (Blueprint "Buy/Partner" diyor, diğer belgeler "yap") | **karar: yap** |
| W-2 | Servis yeri: `Diten.SupplyChainService` mi, yeni servis mi (port §3'ten) | domain-config SupplyChain diyor; yeni servis ise EA kararı |
| W-3 | MOD-0179 dahil mi | ertelensin |
| W-4 | `GOODS_ISSUE_SALES`'i kim kaydeder: 0178 (toplama / paketleme) mı, 0183 (sevkiyat) mı | **karar: 0183, `Dispatched`'ta** |
| W-5 | Lot / lokasyon rezervasyonu (INVENTORY eklemeli) | MVP-1 CT yapar (MOD-0173 sahibi), MVP-5 S2'den önce |
| W-6 | WAREHOUSE-OUTBOUND'a `Item` seviyesi | sahip kararı |
| W-7 | MDM P6 / P9 takvimi (GTIN / ambalaj) | MDM sahibine sorulur; MOD-0182'den önce şart |
| W-8 | MOD-0182'nin MOD-0365'e (cihaz / uç) bağımlılığı | MOD-0182 sona |
| W-9 | Koşullu G1 MVP-5'i açmaya yeter mi | evet; G1-D1 / D2 MVP-5'i etkilemiyor |
| W-10 | MVP-5 Control Tower'ı kim | **karar: MVP-1 CT** |
| W-11 | Dal adı | `feature/sce/mod-0178-…` (güncel düzen) |

### 4.1 Verilen kararlar (2026-10-21)
- **W-1: yapılacak** (satın alma / partner değil). Blueprint'teki "Buy/Partner" işareti bu kararla güncellenmeli (EA).
- **W-4: `GOODS_ISSUE_SALES`'i MVP-6 (MOD-0183) yazar**, sevkiyat `Dispatched` olduğunda; tek yazıcı. MVP-5 toplama / paketlemede
  stoğu düşürmez: rezervasyon + sevk alanına `LOCATION_TRANSFER` yapar, `ReadyToShip` yayınlar; MVP-6'nın dispatch olayıyla
  outbound'u `Shipped` yapar. Gerekçe: mülkiyet sevkte çıkar; MVP-5'i olmayan kiracıda da sevkiyat stoğu düşürür; mevcut
  sözleşmeler bunu varsayıyor (INVENTORY 1.4.0 "MVP-6 shipment issues", FEFO taslağı `sourceModule: MOD-0183`, WAREHOUSE-OUTBOUND
  "Shipped = terminal, MVP-6 yazmaz").
- **W-10: MVP-5 Control Tower'ı biz (MVP-1 CT).** R8 görev kaynağı kaydı 2026-11-18'den önce bizim işimiz.
- **Açık:** W-2 (servis yeri). CT önerisi: yeni servis (`Diten.WarehouseService`), çünkü `Diten.SupplyChainService` MVP-6'nın;
  port §3'ten EA kararıyla.

**Sıra:**
1. Kararlar (W-2) ve MVP-5 görev kaynağının kaydı (R8) ve MVP-5 görev kaynağının kaydı (R8).
2. **P0:** MOD-0178 module pack (`supply-chain-execution`) + WMS-BUNDLE taslağı (WAREHOUSE-OUTBOUND 1.0.0'ı kapsar).
3. **Paralel:**
   - INVENTORY lot / lokasyon rezervasyonu (W-5, MVP-1 CT);
   - MDM'e P6 / P9 talebi;
   - MVP-2'ye GRN → yerleştirme yolu sorusu.
4. **S1 yerleştirme** (LOCATION_TRANSFER; bugünkü sözleşmelerle yapılabilir) → **S2 toplama / paketleme** (FEFO + rezervasyon + `LOT_UNDER_RECALL` + outbound `ReadyToShip`; MVP-6'nın önünü açar) · MOD-0181 sayım (0178 ile paralel) → MOD-0180 dalga → MOD-0182 barkod (en son).

**İlk kapsam önerisi:** MOD-0178 pack + S1 yerleştirme.

## 5. R8 (2026-11-18) etkisi
- Kayıtlı görev kaynağı yalnız `sce-mvp6` ve `pvg` için var. **MVP-1 Control Tower'ın ve MVP-5'in kaydı yok.**
- 2026-11-18'den sonra dağıtılacak entegrasyon ve MVP-5 WP'leri için iki yol var:
  - A: kuyruk kaydı (registry satırı + CT-QUEUE.tsv + `ct_queue.py require … --in-force`);
  - B: sahibin süreli istisnası (CT kimliği, gerekçe, kapsam, başlangıç / bitiş).
- **Öneri:** A. Kaydı 2026-11-18'den önce açmak, ya da Dalga 1 WP'lerini o tarihten önce onaylı WP olarak başlatmak.

## 6. Doğrulanamayanlar
Testler koşulmadı; canlı Gateway / MDM / Inventory çağrısı yapılmadı. MVP-6 defter testinin kırmızı olduğu statik okumaya
dayanıyor. Başka makinelerdeki itilmemiş işler görülemez. R8 bitiş tarihi (2026-11-18) sahip teyidi bekleyen bir varsayıma
dayanıyor.
