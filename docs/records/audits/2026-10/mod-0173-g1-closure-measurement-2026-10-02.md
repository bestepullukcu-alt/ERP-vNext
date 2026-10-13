# MOD-0173 — G1 çıkış listesi ölçümü (MVP-1 Control Tower), 2026-10-02

**Kapı:** G1 — Product & Inventory Truth Ready (`docs/analysis/inventory-capability-scope-and-dependency-report.md` §20.1)
**Ölçen:** MVP-1 Control Tower (bağımsız ölçüm, K13) · **Dal:** `feature/sce/mod-0173-inventory-ledger-valuation` @ `8cea2cde1`
**Ortam:** sahibin canlı yerel filosu — Gateway 5000 → InventoryService 5066, MDM 5059, Auth 5056, Platform 5057, Web 5001,
Prism product-master mock 4010 (geliştirme köprüsü, FU-PM-01). MongoDB rs0 27017, `diten_inventory_dev`.

**Sonuç: G1 KAPANMADI.** 17 maddeden 11'i PASS, 6'sı OPEN. OPEN maddelerin hepsi MVP-1 dışındaki sahiplere bağlı
(MDM MOD-0290: 4 madde, merkez FU-LE-01: 1 madde) ya da ekran kanıtına (E4: 1 madde). MOD-0173'ün kendi stok
gerçeği maddelerinin tamamı canlıda PASS.

> Yerleşim: `docs/records/audits/` — olmuş bir ölçümün kaydı (docs-organization §1).

## 1. Ölçüm yöntemi

- **API:** gerçek giriş (`POST /api/tenant-auth/login`, dev-runbook seed kullanıcısı, tenant `…0001`; parola ve token
  hiçbir yere yazılmadı), sonra Gateway üzerinden tam akış. Koşular: `G1X9U4R` (41/46, rezervasyon yetkisi yoktu),
  `G1Y13QY` (41/46, aynı), **`G10G21Z` (46/46, FU-RSV-01 kapandıktan sonra)**. Şirket (LE) `66068d61-…`,
  karşılaştırma şirketi `099219a0-…` (MDM `/api/legal-entities/lookup`, 5 aktif şirket).
- **Kalıcılık:** salt-okur .NET aracı (MongoDB.Driver 2.27.0, yalnız `Find`/`CountDocuments`): `inventory_transactions`,
  `inventory_audit`, `inventory_outbox`, `inventory_reservations`; Auth `diten_auth_v3.permissions`.
- **Test verisi (sahibin dev veritabanında kalıyor):** lokasyonlar ve hareketler `G1X94EL-*`, `G1X9U4R-*`, `G1Y13QY-*`,
  `G10G21Z-*` kodlarıyla; ürün kimlikleri Prism örnekleri (item `b1f2c3d4-…0001`, sku `c3d4e5f6-…0002`, Lsku, EA).

## 2. Madde madde sonuç

### Kimlik — MOD-0290 (Model A)

| # | Madde | Sonuç | Kanıt |
|---|---|---|---|
| 1 | Canonical kimlik resolve (`ItemId→GlobalProduct`, `SkuId+SkuLevel→Gsku\|Lsku\|FinishedGood`) | **OPEN** | Gateway `/api/product-master` → 404 (G10G21Z). MDM sözleşmeyi sunmuyor; stok Prism mock'a karşı doğrulanıyor. FU-PM-01, istek belgesi `docs/roadmap/plans/mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md` R-01, R-05 |
| 2 | `Gsku→GlobalProduct` (D-SKU-LINK) | **OPEN** | aynı; R-05 |
| 3 | Base UoM + conversion | **OPEN** | MDM'de `baseUomId` ve dönüşüm varlığı yok; R-03 |
| 4 | Identity contract versioned + frozen | **OPEN** | `product-master-bundle.openapi.yaml` v1 FROZEN olarak yayınlı, ama sahibi (MOD-0290) onu sunmuyor. Madde, sözleşme gerçek sağlayıcıdan sunulunca kapanır |

### Stok gerçeği — MOD-0173

| # | Madde | Sonuç | Kanıt (G10G21Z, aksi yazmıyorsa) |
|---|---|---|---|
| 5 | Opening golden flow `+100 → persist → Balance=100 → reload stabil` | **PASS** | 201; onHand 100, available 100; yeniden okuma 100. Mongo (G1X9U4R): `INV-2026-0000007` OpeningBalance 100, baseQty 100 |
| 6 | `Issue −20 → 2. immutable transaction → 80, 1. hareket değişmez` | **PASS** | 201; onHand 80; hareket listesi 1 → 2, ilk kayıt aynı |
| 7 | Idempotency: aynı anahtar → 2. posting yok | **PASS** | aynı anahtar + aynı gövde → 200, `idempotentReplay: true`, aynı transaction; onHand 100'de kaldı |
| 8 | Negative stock enforce (DEC-INV-12) | **PASS** | 1000 adet çıkış → 422; onHand 80'de kaldı, hiçbir şey yazılmadı |
| 9 | StockStatus boyutu | **PASS** | AVAILABLE 77 · QUALITY_INSPECTION 10 · QUARANTINE 5 · BLOCKED 4 · IN_TRANSIT 3 (STATUS_TRANSFER ile); available yalnız AVAILABLE'ı sayıyor (77, onHand 99) |
| 10 | Reservation: `Available = OnHand − Reserved` | **PASS** | reserve 30 → 201 (`W/"1"`); reserved 30, available 278 = AVAILABLE 308 − 30 (SKU düzeyi, tüm koşuların toplamı); güncel If-Match ile release → 200, available 308; eski If-Match → 409 `RESERVATION_CONFLICT`. Mongo: 1 rezervasyon (Released, Version 2), 2 audit, 2 outbox. Önceki 403'ler FU-RSV-01 idi (WP-0173-RSV1 ile kapandı) |
| 11 | Movement schema + availability contract versioned + frozen | **PASS** | `docs/analysis/contracts/inventory-bundle.openapi.yaml` `version 1.0.0`, `x-status: FROZEN`; tüm yanıtlar `contractVersion: v1` |

### Scoping & security

| # | Madde | Sonuç | Kanıt |
|---|---|---|---|
| 12 | Tenant + LE izolasyonu; cross-LE leak bloklu | **OPEN (kısmi)** | Ölçülen: tenantId sorguda → 400; bilinmeyen şirket başlığı → 403; aynı tenant'ın diğer şirketi G1 deposu için 0 satır görüyor; her Mongo kaydı aynı tenant + LE. **Açık:** AuthService `legal_entity_id` claim'i vermiyor (token'da yok, ölçüldü), şirket `X-Legal-Entity-Id` başlığından geliyor ve yalnız MDM'de varlığı doğrulanıyor. Kullanıcı↔şirket ataması olmadığından, bir kullanıcının yetkisi olmayan şirketin başlığını göndermesi engellenemiyor. Waiver W-0173-02, FU-LE-01 (merkez) |
| 13 | Tüm mutation'larda server-side RBAC | **PASS** | `InventoryController` ve `LocationsController`'daki her POST/PATCH `[HasPermission]` taşıyor (kod okundu); token'sız → 401; rezervasyon yetkisi yokken reserve/release → 403 (G1X9U4R, G1Y13QY). SuperAdmin bypass'ı FU-S1-02 ile kapatıldı |
| 14 | Her posting'de audit event + correlation ID | **PASS** | G1X9U4R: 6/6 transaction'da correlation = isteğin `X-Correlation-Id` başlığı; her biri için tam 1 audit + 1 outbox; actor kayıtlı. Yanıtlar correlation'ı geri yansıtıyor |

### Data quality (STOP-SHIP)

| # | Madde | Sonuç | Kanıt |
|---|---|---|---|
| 15 | Rakip ürün/stok gerçeği yok (tek SoR) | **PASS** | Bu dalda InventoryService dışında stok bakiyesi tutan kod yok (arama: `StockBalance`, `OnHandQuantity`, `inventory_balances`). Model B (`mdm_products`) kullanılmıyor. Not: `SupplyChainService` yalnız `origin/feature/mvp6-logistics` dalında; MVP-6 birleşirken yeniden kontrol edilmeli |
| 16 | `SourceSystem` / `ExternalRef` alanları | **PASS** | G1X9U4R: 6/6 transaction'da iki alan da mevcut |

### Kanıt seviyesi

| # | Madde | Sonuç | Kanıt |
|---|---|---|---|
| 17 | State-changing akışlar E4 (UI + API + persisted + audit birlikte) | **OPEN** | API + kalıcılık + audit canlıda birlikte ölçüldü (yukarıda). **UI ayağı CT tarafından canlı ölçülmedi:** CT tarayıcısında Web girişi `tenant-unavailable` (FIX-4 koruması tenant `…0001`'i "Provisioning" görüyor) ve Chrome uzantısı bağlı değil. Sahip 2026-10-02'de ekran kanıtını bu tur için atlamaya karar verdi. UI'ın kendisi UI1/UI2 WP'lerinde ajan ekran görüntüleriyle kabul edilmişti; bu, E4 için "birlikte" ölçümün yerini tutmaz |

## 3. Kapanış için gerekenler

| Madde | Ne gerekiyor | Sahibi |
|---|---|---|
| 1–4 | PRODUCT-MASTER-BUNDLE v1'in MDM'den gerçek sunulması (R-01…R-05) + gateway rotası; ardından istek belgesi §5 doğrulaması ve Prism köprüsünün kaldırılması | MDM (MOD-0290) |
| 12 | Kullanıcı↔şirket ataması + AuthService'in `legal_entity_id` vermesi (FU-LE-01); W-0173-02 kalkar | Merkez / PSS |
| 17 | Aktif bir tenant oturumunda Stok Defteri, hareket geçmişi, fiş ve lokasyon ağacı ekranlarının API + Mongo ile aynı anda ölçümü (yalnız okuma) | CT, sahibin oturumu gerektiğinde |

## 4. G1 dışı ama kayda geçen bulgular

- **FU-LOC-01** pack §20'de hâlâ "blocks G1" yazıyor, ama lokasyon master'ı MOD-0173-FU02 olarak yapıldı ve G1 koşuları
  gerçek `/api/locations` ile lokasyon ağacı kurdu (5 × 201). Kapatılmalı.
- **FU-INT1-02** (before G1): UUID olmayan `X-Correlation-Id` → 400. G1 maddesi değil; tüketicilerle (MOD-0142, WMS) karar bekliyor.
- **Outbox:** tüm olaylar `Pending`, çünkü bu makinede RabbitMQ (5672) kapalı. Ledger yazımı etkilenmiyor; yayın ölçülmedi.
- **Ortam (MVP-1 dışı):** Platform test suite'inde 172 kırmızı, hepsi ortamsal ve WP-0173-RSV1 öncesinde de var:
  Windows `mongod` `--nounixsocket`'i reddediyor (117), Mongo 9.0 `Timestamp` deserializasyonu (49), kilitli parmak izi (5),
  kilit (1), kararsız sweeper (1). Ayrıntı: `wp-0173-rsv1-evidence-2026-10-02.md`.
