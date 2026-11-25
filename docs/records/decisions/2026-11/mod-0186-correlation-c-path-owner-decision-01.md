# MOD-0186 İadeler — korelasyon "C yolu" sahip kararı (2026-11-20)

Sahip: Beste (MVP-6 hattı, 2026-11-17'den beri). Kayıt: MVP-1 / MVP-5 CT oturumu.

## Neden

CT bağımsız incelemesi (2026-11-20, main `2de950edc`): Gateway gelen `X-Correlation-Id`'yi kabul etmez, kendi değerini üretir
(`gateway/Diten.ApiGateway/Observability/EdgeCorrelation.cs:17`). İade adaptörü yazma isteklerinde sevkiyatın `lifecycleCorrelationId`'sini
bu başlıkla gönderir (`SupplyChainReturnsController.cs:551-576`, `ProxyReturnsMutationAsync`: `:560` `ReturnRootResolution.From`, `:573`
`AddReturnsHeaders(request, scope, root.Root)`; dosya başı açıklaması `:17`, `:26`, `:30` yalnız tarif eder — düzeltme 2026-11-24, aşağıya bakın); servis sevkiyat köküyle karşılaştırır (`ReturnRepository.cs:31,71,109`,
`ReturnReceiptCoordinator.cs:50`). Sonuç (çıkarım, canlıda ölçülmedi): Gateway arkasında her oluşturma ve geçiş 409
`CORRELATION_ROOT_MISMATCH` alır; testler sahte servis yankısı (`ReturnsClaimsHttpHost.cs:43`) yüzünden yakalamaz. Aynı gün canlı MOD-0183
turu (CT, 2026-11-20) C yolunun sevkiyatlarda Gateway arkasında çalıştığını ölçtü (oluşturma 201, üç geçiş 200, POD 201).

## Karar (Beste, 2026-11-20 — "tamam iadelere de uygula")

MOD-0183 r11'deki C yolu iadelere uygulanır:
1. İade kaydının korelasyon kökünü servis belirler: oluşturmada servisin aldığı değer (Gateway'in ürettiği `X-Correlation-Id`).
2. Sonraki komutlarda, §34 Received yolunda ve tekrar isteğinde istek korelasyonu sevkiyat köküyle karşılaştırılmaz; yalnız iz olarak
   loglanır. `CORRELATION_ROOT_MISMATCH` iade uçlarından kalkar (sabit Loads için kalır).
3. Web adaptörü her işlemde kendi UUID'sini gönderir; sevkiyat kökünü okumaz.
4. İade olayları ve audit saklı iade kökünü taşır; sevkiyata bağ `ShipmentId` üzerindendir (ek belge :76 "inherited Shipment root" kuralı
   bilinçli olarak değişir — CT-APR-02'de teyit edilir).

## Sonuç işler

- Paket: MOD-0186 r11 (yeni §36, AC RC-1…RC-9, dilim DL-6); bağımsız inceleme (CT-APR-02) ve onay satırı (CT-APR-01) gerekir.
- Sözleşme: SHIPMENT-BUNDLE yeni sürümü + `returns-semantics` ek belgesinin yeni sürümü (sürüm türü CT kararı); DL-6 bunlar hatta gelmeden
  birleşmez.
- 0187 (Hasar talepleri) ve 0185 (Yükler) aynı desen için incelemede (`LoadRepository.cs:17,45`).

## O-R11-1 — seçenek C (Beste, 2026-11-24)

Karar veren: Beste (MVP-6 CT). Kaynak: bağımsız inceleme `docs/records/reviews/MOD-0186/2026-11-24-f533773b.md` (r12, KOŞULLU),
bulgu F-1 ve öneri paragrafındaki A / B / C seçenekleri; paket r12 §36.2 (C-3), §36.5 RC-9, §36.7 O-R11-1. Ölçüm: HEAD `a0c3c7194`.

**Karar.** Seçenek C — SHIPMENT-BUNDLE **3.6.0 (minor) artı gerekçeli semver istisnası**:

1. `CORRELATION_ROOT_MISMATCH` örneği `createReturn` ve `transitionReturn` 409 yanıtlarından kalkar (C-3); 409 durumu ve diğer
   örnekleri (`IDEMPOTENCY_KEY_REUSED`, `RETURN_SOURCE_CHANGED`, `RETURN_RECEIPT_IN_PROGRESS`) kalır.
2. İade ek belgesi `returns-semantics-v3.1.0.md` olur (yeni dosya; `returns-semantics-v3.0.0.md` tarih olarak değişmeden kalır).
3. İstisna bu kayıtta ve sözleşmenin sürüm notunda (`info.x-changelog`, 3.6.0 satırı) yazılır.
4. RR-9 tam uygulanır: kod iade UI'sinden, adaptör listesinden, 7 dil `ReturnsIndex` anahtarından ve Web testlerinden DL-6'da kalkar.

**İstisnanın gerekçesi.** Bildirilmiş bir hata örneğini kaldırmak eklemeli değildir; katı semver 4.0.0 isterdi. Minor kalır, çünkü:
(a) DL-6'dan sonra hiçbir iade yolu bu kodu göndermez (RR-7: `ReturnRepository.cs:31`, `:71`, `:109`, `ReturnReceiptCoordinator.cs:50`
kalkar) — sunucunun hiç göndermediği bir yanıtı kaldırmak hiçbir tüketiciyi kırmaz; (b) kodu iadeler için anan tek tüketici Web iade
adaptörü ve sayfasıdır ve onu aynı DL-6 değişikliğinde bırakır; (c) kod Loads'ta kalır (`LoadRepository.cs:17`, `:45`), dolayısıyla
`ContractErrorCodes.CorrelationRootMismatch` (`ContractErrorCodes.cs:38`, `:110`) ve `ContractErrorCodeParityTests` değişmez.
Seçenek B (4.0.0) değişmeyen Yükler ve Talepler dahil her tüketiciyi yeniden sabitlemek demekti; seçenek A ölü kodu sözleşmede ve 7 dilde
bırakırdı.

**Tüketiciler (ölçüldü, HEAD `a0c3c7194`; `git grep CORRELATION_ROOT_MISMATCH|CorrelationRootMismatch`, `api/shipment-bundle`).**

| Tüketici | Yer | Durum |
|---|---|---|
| `/api/shipment-bundle/returns`'ün tek çağıranı: Web iade adaptörü | `frontend/Diten.Web/Controllers/SupplyChainReturnsController.cs:45` (yol), `:55` (yayımlanan kod listesi), `:348` (açıklama) | DL-6'da kalkar (RR-9) |
| İade sayfası | `wwwroot/assets/js/SupplyChain/Returns/index.js:84`, `:876`, `:924`, `:1226`; `index.l10n.js:157`; `Views/SupplyChain/Returns/_IndexL10n.cshtml:130`; `Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx:104` | DL-6'da kalkar (RR-9) |
| Web testleri | `frontend/Diten.Web.Tests/Forms/ReturnFormContractTests.cs:22`; `JavaScript/ReturnIndexBehaviorTests.cs:18`, `:177` | DL-6'da güncellenir |
| Üretici (servis) ve testleri | `ReturnRepository.cs:31`, `:71`, `:109`; `ReturnReceiptCoordinator.cs:50`; `Returns/ReturnReplayTests.cs:13`; açıklamalar `Common/ReturnsClaimsHttpHost.cs:43`, `Common/ReturnsClaimsObservabilityTests.cs:72` | DL-6 (RR-2, RR-5) |
| Sözleşmeyi okuyan testler | `Common/ShipmentBundleContract.cs:17`, `Shipments/ContractErrorCodeParityTests.cs:12` | eşlik değişmez (kod Loads'ta kalır) |
| Loads (kodu kullanır, iadeyle ilgisiz) | `LoadRepository.cs:17`, `:45`; `SupplyChainLoadsController.cs:16`; Loads `index.js:274`, `:338`, `:450`; `LoadsIndex.*.resx:63`; `LoadFormContractTests.cs:14`, `LoadIndexBehaviorTests.cs:18`, `LoadReplayTests.cs:19`, `:22` | değişmez |
| Diğer SHIPMENT-BUNDLE okuyucuları | `ReturnReferenceReader.cs:85`, `ClaimReferenceReader.cs:120`, `LoadReferenceReader.cs:66` (getShipment), Inventory `ShipmentBundleHttpReader.cs:66`, `:118`, `:135` | iade yollarını çağırmaz; değişmez |
| İade olayı tüketicisi | SupplyChain dışında `ReturnRequested` / `returns_outbox` adı geçen kod yok | yok |

Gateway `ocelot.json` iade rotaları yalnız geçirir; kod içermez. Kodu anan diğer bulgular `docs/records/audits/2026-09/**` altındaki
tarihsel ham kayıtlardır, tüketici değildir.

**Öneri ve yama (RC-9).** `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.6.0-PROPOSAL.md` ve
`SHIPMENT-BUNDLE-3.6.0.patch` (3.4.0 / 3.5.0 / 3.5.1 düzeni). `git apply --check` (çalışma ağacı ve `--cached`) HEAD `a0c3c7194`
üzerinde geçer; taban 3.5.1'dir (MOD-0183 DL-8 `20aaffc78`). Yama uygulanmadı.

**Açık kalanlar (karar Beste'nin).**
- Uygulama zamanı — CT önerisi: yama DL-6 koduyla aynı değişiklikte uygulanır (MOD-0183 DL-8 deseni), böylece sözleşmenin sunucunun
  hâlâ gönderdiği bir kodu atladığı bir aralık olmaz; istisnanın (a) gerekçesi buna dayanır. **TBD.**
- DocsPathGuard yeniden sabitlemesi (`docs/reference/architecture/docs-path-authority.json`, YAML hedefi `8f7564b3…e241` → yamalı LF
  dosyanın özeti `6024d3f0…d7bb`): 3.5.0 / 3.5.1'deki gibi ayrı sahip kaydı. **TBD.**
- O-R11-2 (503 / 502 kök kodları) bu kararla değişmez; v3.1.0 ek belgesi onları yalnız create için korur.

**Satır düzeltmesi (inceleme bilgi notu 1).** "Neden" bölümündeki adaptör atfı `SupplyChainReturnsController.cs:17,26,30` idi; bunlar
dosya başı açıklamalarıdır. Kod `ProxyReturnsMutationAsync` içinde `:551-576` aralığındadır (`:560` `ReturnRootResolution.From`, `:573`
`AddReturnsHeaders(request, scope, root.Root)`, `:575-576` ve `:582-583` kökü günlüğe yazar); HEAD `a0c3c7194`'te ölçüldü, metin
yerinde düzeltildi.
