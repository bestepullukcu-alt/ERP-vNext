# VER — MOD-0183 DL-1/DL-2 ve MOD-0186 DL-1/DL-2 (Q500, Q501, Q503, Q504)

## 1. Başlık

- **Donmuş kaynak:** `<projeler>/ERP-vNext-recovery-accept`, HEAD `35e973bd22af68168686862f5b80f417c174a563`
  (= `origin/main`, PR #355 birleştirmesi). Dal adı `docs/sce/mvp6-acceptance-01` (ayrık HEAD değil, HEAD aynı).
- **Mod:** worktree-read-only. Ağaçta checkout, commit, derleme, test, süreç başlatma yok; `bin/`, `obj/` açılmadı.
  Port ya da mongod kullanılmadı. Tek yazılan dosya bu rapor (ağacın dışında).
- **Test koşturulmadı.** Testler CT tarafından ayrıca koşuluyor; aşağıdaki "test" kanıtları test kodunun okunmasıdır,
  yeşil sonuç değildir.
- **Paket sürümleri (sha256 ölçüldü, `git show <commit>:<pack> | shasum -a 256`):**
  - MOD-0183 r6 `07152235c49640c6f89b48663949ca0aff72e01b745044ebdaa12ac70f2d9337` — commit `723de51e4` (onay satırı
    `execution/approvals/approvals.tsv:23`, "only DL-1, DL-2").
  - MOD-0183 r7 `22fe550daa31c20c3957730fffce82fd4ea726d401c82a450c3f2ab758ea8309` — commit `89399d0dd`, HEAD'deki dosya bu
    (onay `approvals.tsv:27`, inceleme `docs/records/reviews/MOD-0183/2026-10-28-22fe550d.md`, KOŞULLU, 0 yüksek). Ara r7
    `b2e02544…` (`26f9d0642`) de var.
  - MOD-0186 r8 `e96a9add5b9213ecc444a826ac86e61957b5673027b3e809c2da02e781776e89` — commit `fe450dc15`, HEAD'deki dosya bu
    (onay `approvals.tsv:24`).
- **Birleştirmeler (ölçüldü):** #294 `7ac15454e`, #307 `3fc2efcdc`, #319 `763e1062a`, #323 `6fd49036e`, #333 `8698dbddc`,
  #334 `b0ca699d7`, #336 `98dddffd8`, #352 `5742a8696`, #355 `35e973bd2`.
- **Okunan kurallar ve kaynaklar:** `.antigravity/agents/read-only-auditor.md`; `AGENTS.md` Çekirdek ve §6.1 kural haritası
  (Daima: multi-tenancy, security-jwt, git-safety; Backend; Ekran; Metin: localization-standard);
  `docs/guides/operations/mvp6-development-process-v1.0.md` §2–§5; `docs/guides/operations/control-tower-sop.md` §17.4
  (satır 1023+); `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` Q500/Q501/Q503/Q504 satırları; paketlerin §23 / §34
  bölümleri (r6 ve r7 farkı dahil); `DL1-IMPLEMENTATION-PLAN.md` §3–§5, `MOD-0186-DL1-IMPLEMENTATION-PLAN.md` §5; aşağıdaki
  kanıt tablosundaki üretim ve test dosyaları; `services/Diten.InventoryService/.../InventoryErrorCodes.cs` ve `ReversalRules.cs`.
- **G3 (ajan sözleşmesi):** SOP §17.4'te `read-only-auditor` satırı **var** (`control-tower-sop.md`, §17.4 tablosu 18. satır:
  denetim modu, kapsam, `path:line` zorunluluğu, "düzeltme yok"). Prompt kapsamı, `path:line` şartını ve düzeltme yasağını
  veriyor; denetim modu açıkça yazılmamış, "salt-okunur" ifadesinden worktree-read-only olarak alındı. Ajan dosyası yazma
  aracına izin vermiyor; çağıran yalnız bu rapor dosyasına yazma izni verdi — tek sapma budur.
- AUD-001 sayım sorusu: `TenantArchitecture` mimari testi koşturulmadığı için sayı **ölçülmedi** (bkz. §4).

Kısaltmalar: `SC` = `services/Diten.SupplyChainService`, `SCsrc` = `SC/src`, `SCt` = `SC/tests/Diten.SupplyChainService.Tests`,
`Web` = `frontend/Diten.Web`, `WebT` = `frontend/Diten.Web.Tests`.
`Coord` = `SCsrc/Diten.SupplyChainService.Application/Features/Shipments/Dispatch/ShipmentDispatchCoordinator.cs`,
`DT` = `SCt/Shipments/Dispatch/ShipmentDispatchTests.cs`,
`RCoord` = `SCsrc/Diten.SupplyChainService.Application/Features/Returns/Receipt/ReturnReceiptCoordinator.cs`,
`RT` = `SCt/Returns/Receipt/ReturnReceiptTests.cs`.

Test düzeni: gerçek `Program` host'u, gerçek koordinatör ve repository, kendi replica set'inde Mongo
(`MOD0183_TEST_MONGO` / `RETURNS_MONGO_URI`, yedek port yok), gerçek INVENTORY HTTP istemcisi; yalnız INVENTORY ve sevkiyat
okuma HTTP sınırı sahte (`SCt/Shipments/Dispatch/FakeInventory.cs`, `SCt/Returns/Receipt/ReturnReceiptTestHost.cs:27-38`).
Sahte olan, test edilen şey değil dış bağımlılık; bu yüzden "test edilen şeyi taklit eden test" sayılmadı.

## 2. Kabul tabloları

### Q500 — MOD-0183 DL-1 (r6 satırları; r7 farkı not sütununda)

| Satır | Ölçüt (kısa) | Sonuç | Kanıt | Not |
|---|---|---|---|---|
| S23-1 | Tek satır → bir `GOODS_ISSUE_SALES`, §23.1 alanları, `X-Legal-Entity-Id` = sevkiyat LE, stok düşer | GEÇTİ | Kod: `InventoryMovementMapper.cs:19-46`, `SCsrc/Diten.SupplyChainService.Infrastructure/Features/Shipments/InventoryMovementClient.cs:25-28`, `Coord:95-97`. Test: `DT:152-179`, `SCt/Shipments/Dispatch/InventoryMovementClientTests.cs:35` | Anahtar `…:1:dispatch:a1`, LE başlığı, tarih ve Net −30 doğrulanıyor; başlık düşerse test kırmızı. Gerçek INVENTORY ile "stok düştü" canlı turu ölçülmedi |
| S23-2 | `LOT_UNDER_RECALL` → 422 `INVENTORY_REFUSED`, `Dispatched` değil, olay yok | GEÇTİ | Kod: `Coord:108-110,159-161`. Test: `DT:183-199` | Aynı anahtar yeniden → kayıtlı cevap, INVENTORY çağrılmıyor |
| S23-3 | Aynı `Idempotency-Key` ile tekrar → ikinci hareket ve olay yok | GEÇTİ | Kod: `Coord:29-30`. Test: `DT:170-173` | |
| S23-4 | 503 → 503 ve ilk postadan önce değişiklik yok; 403 → 403 `INVALID_REQUEST`; LE başlığı sabotajı | GEÇTİ | Kod: `InventoryMovementClient.cs:38-52`, `Coord:122-128,285-301`. Test: `DT:203-222`, `InventoryMovementClientTests.cs:66-106`, `DT:640-659` | r7 farkı (zarfsız 503 / 500 / 502 / 504 / zaman aşımı belirsiz → satır *sent*, deneme `InProgress`; 401 kesin) uygulanmış ve test edilmiş: zarfsız 503 ve 401 yalnız istemci birim testinde (`InventoryMovementClientTests.cs:76,98-99`), uçtan uca değil |
| S23-5 | `skuLevel` yok → 422 `SHIPMENT_LINE_STOCK_INCOMPLETE`, çağrı yok; `LOT_REQUIRED` → 422, telafi | GEÇTİ | Kod: `Coord:55-56`. Test: `DT:226-242` | Kod ayrıca `fromLocationId` boşsa da yeni denemeyi reddediyor (`Coord:55`, `DT:245-262`) — pakette yazılı değil, bkz. F-1 |
| S23-6 | Başka kiracı / LE → 404, hareket yok | GEÇTİ | Kod: `SCsrc/Diten.SupplyChainService.Api/Middleware/ShipmentContextMiddleware.cs:28-29`, `SCsrc/Diten.SupplyChainService.Persistence/Features/Shipments/ShipmentRepository.cs:13-16`. Test: `DT:288-294` | |
| S23-8 | 3 satır, satır 2 ret → satır 1 `…:1:reversal:a1`, `Compensated`, `n=2`, `Planned`, olay yok | GEÇTİ | Kod: `Coord:154-185,227-239`, `ShipmentRepository.cs:95-121`. Test: `DT:298-317` | Sıra (saklanan sıra) de doğrulanıyor |
| S23-9 | Satırlar arası çökme, kira sonrası yeniden deneme → aynı gövdeyle tekrar, `Dispatched` bir kez | GEÇTİ | Kod: `Coord:69-70,91-97`. Test: `DT:335-357` | |
| S23-10 | REVERSAL hatası → `CompensationFailed`; geçiş ve POD 409; `Cancelled` / `Dispatched` çıkışı önce ters kaydı yeniden gönderir; `Dispatched` → a2 | GEÇTİ | Kod: `Coord:34-39,188-198,258-265`, `TransitionShipmentHandler.cs:19-20,32`, `CapturePodHandler.cs:20`. Test: `DT:363-401` | |
| S23-11 | Saklanan tarihler yeniden gönderilir; tarih değişikliği → `IDEMPOTENCY_KEY_REUSED` bütünlük hatası | GEÇTİ | Kod: `Coord:111-117,209-225`. Test: `DT:406-427`, `DT:881-897`, `DT:811-833` | r7 farkı (500 cevap, kayıtlı çıkışlar ters kaydedilir, her çıkış 409) uygulanmış ve test edilmiş |
| S23-12 | Arka plan işi göndermez / ters kaydetmez | GEÇTİ | Test: `DT:432-446` | Yalnız yapıcı parametresine bakan yansıma testi; `IServiceProvider` üzerinden çözümü yakalamaz (F-4). "Süresi dolmuş kirayı işaretleyen" iş yok — paket bunu zorunlu tutmuyor |
| S23-13 | Telafi sırasında çökme → aynı anahtar ve tarihle REVERSAL tekrarı, ikinci ters kayıt yok | GEÇTİ | Kod: `Coord:166,236-238`. Test: `DT:450-468` | |
| S23-14 | Telafiden sonra yeniden sevk → `…:a2` anahtarları, yeni hareketler, net −toplam | GEÇTİ | Kod: `Domain/Features/Shipments/Shipment.cs:33`, `Coord:65`. Test: `DT:319-330` | |
| S23-15 | Eşzamanlı iki sevk → biri 409 `DISPATCH_IN_PROGRESS`; `InProgress` sırasında iptal ve POD 409 | GEÇTİ | Kod: `Coord:71`, `ShipmentRepository.cs:70-89`, `TransitionShipmentHandler.cs:33`, `CapturePodHandler.cs:19`. Test: `DT:473-489` | |
| S23-17 | REVERSAL gövdesi yalnız §23.4 alanları; 400 bütünlük hatası | GEÇTİ | Kod: `InventoryMovementMapper.cs:52-73`. Test: `DT:310-312` (alan listesi eşitliği), `DT:611-618`, `InventoryMovementClientTests.cs:122` | `lotId` ya da `from*` eklenirse alan listesi testi kırmızı olur |
| S23-18 | Çitleme (a)–(e) | GEÇTİ | Kod: `LateWriterPolicy.cs:17-49`, `ShipmentRepository.cs:67-69,90-94`. Test: (a) `DT:494-515`, (b) `DT:519-541`, (c) `DT:545-559`, (d) `DT:563-585`, (e) `DT:335-357` | r7 (f) de uygulanmış: `LateWriterPolicy.cs:24-25`, `DT:919-944` |
| S23-19 | Dışarıdan ters kayıt → `ALREADY_REVERSED` ters sayılır, `Compensated`, döngü yok | GEÇTİ | Kod: `Coord:240-245`. Test: `DT:589-607` | |
| S23-20 | REVERSAL'da 403 → `CompensationFailed`, 409 (403 değil) | GEÇTİ | Kod: `Coord:246-250,258-265`. Test: `DT:366-376` (`InlineData("Cancelled", 403)`) | |
| 3.4.0 | Sözleşme yaması + `ContractErrorCodeParityTests` + dönüş okuyucusunun 3.4.0 alanlarını kabulü | GEÇTİ | `docs/analysis/contracts/shipment-bundle.openapi.yaml:15` (3.4.0), `SCsrc/Diten.SupplyChainService.Application/Common/ContractErrorCodes.cs:56-59,84-86,113-119`, `SCt/Shipments/ContractErrorCodeParityTests.cs`, `DT:174-178` | |
| S23-7 | Metinler | KAPSAM DIŞI | — | DL-2 (Q501) |
| S23-16 | Ortak senaryo | KAPSAM DIŞI | — | Q503'te R34-8 olarak ölçüldü |
| S23-21 | Başarısız sonuçların denetim satırı | KAPSAM DIŞI (r7) | `ShipmentRepository.cs:95-140`, `DT:970-1030` | r6'da yok; r7 ile DL-1'e eklendi, kodda var ve test edilmiş |

**r7 farkının DL-1 anlamına etkisi:** r7 DL-1'i değiştiriyor — belirsiz cevapta ilk postada bile deneme `InProgress` kalır
(r6 "ilk postadan önce hiçbir şey değişmez" diyordu), `IDEMPOTENCY_KEY_REUSED` 500 ile cevaplanır ve deneme kalıcı
bloklanır, geç yazıcı yalnız *kaydedilmemiş* hareketi ters kaydeder, (f) durumu ve S23-21 eklenir. HEAD'deki kod r7'yi
izliyor; r7 onaylı (`approvals.tsv:27`). r6'nın S23-4 metnine harfiyen bakılırsa, zarfsız 5xx'te durum değişiyor — bu r7'de
onaylı bir anlam değişikliği, kusur sayılmadı.

### Q501 — MOD-0183 DL-2 (S23-7 metinleri)

| Satır | Ölçüt (kısa) | Sonuç | Kanıt | Not |
|---|---|---|---|---|
| S23-7a | Kapalı listedeki 25 `inventoryCode` metni, 7 dil | GEÇTİ | `Web/Resources/Views/SupplyChain/Shipments/ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`; ölçüm: her dilde 25/25 dolu, hiçbiri İngilizceyle aynı değil | Liste `services/Diten.InventoryService/.../InventoryErrorCodes.cs` 422 kodlarıyla aynı (ölçüldü) |
| S23-7b | `InventoryRefused.Unknown` genel metni (kodu gösterir) | GEÇTİ | Anahtar `InventoryRefusedUnknown`, 7/7 dolu; en: "Unrecognized reason (code: {0})." | |
| S23-7c | Modül kodları: `INVENTORY_REFUSED`, `SHIPMENT_LINE_STOCK_INCOMPLETE`, `DISPATCH_IN_PROGRESS`, `DISPATCH_COMPENSATION_FAILED`, `DEPENDENCY_UNAVAILABLE` | GEÇTİ | `Err*` anahtarları 5/5 × 7 dil dolu | |
| S23-7d | `dispatchState` etiketleri `InProgress`, `CompensationFailed` | GEÇTİ | `DispatchStateInProgress`, `DispatchStateCompensationFailed` 7/7; `Web/wwwroot/assets/js/SupplyChain/Shipments/details.js:73-75` | |
| S23-7e | Ekran bu kodlar için `validationError`'a düşmez | GEÇTİ | `details.js:62-72` (kodlar ayrı eşlenir, `own()` ile prototip adları dışlanır), `Web/Views/SupplyChain/Shipments/_IndexL10n.cshtml:5` (anahtar köprüsü). Test: `WebT/JavaScript/Node/shipment-details-error-text.test.mjs` (gönderilen `details.js` üzerinde), `WebT/JavaScript/ShipmentDispatchTextTests.cs:39-100` | |
| Eşleşme sayısı | Toplam | GEÇTİ | 33 zorunlu anahtar × 7 dil = 231 MATCH; her dosyada 112 anahtar, küme eşit | |

### Q503 — MOD-0186 DL-1 (r8)

| Satır | Ölçüt (kısa) | Sonuç | Kanıt | Not |
|---|---|---|---|---|
| R34-1 | N satır → N `CUSTOMER_RETURN` QUARANTINE'e, §34.1 alanları, LE başlığı; `lines[]` referansı | GEÇTİ | Kod: `RCoord:91-117,161-228`, `CustomerReturnMovementMapper.cs:23-47`. Test: `RT:34-76` | `uomId` dönüş satırından; dönüş oluşturmada kaynakla eşitliği zorunlu (`ReturnRepository.cs:271-273`) |
| R34-2 | Tekrar → ikinci hareket ve olay yok | GEÇTİ | Kod: `RCoord:43-44`. Test: `RT:80-97`, `RT:100-120` | |
| R34-3 (metin dışı) | Reddedilen satır: dönüş `InTransit`, gönderilen satır kalır, tekrar replay ile tamamlanır | GEÇTİ | Kod: `RCoord:207-212,174-195`. Test: `RT:124-156` | |
| R34-4 | INVENTORY yok → 503, geçiş ve olay yok; 403 → 403 | GEÇTİ | Kod: `RCoord:218-224`. Test: `RT:159-186` | |
| R34-5 | Başka kiracı / LE → 404, hareket yok | GEÇTİ | Kod: `ReturnRepository.cs:18-19,218-220`. Test: `RT:190-205`, `SCt/Returns/Receipt/ReceiptAttemptStorageTests.cs:86` | |
| R34-6 | `stockIssued` var ama satırın hareketi yok → 422 `RETURN_SOURCE_MOVEMENT_MISSING`, lot tahmini yok | GEÇTİ | Kod: `RCoord:107-110`. Test: `RT:208-225` | |
| R34-7 | Reddedilen / bilinmeyen satır yokken başka değer → 409 saklanan değerlerle; aynı değer → bayt bayt tekrar | GEÇTİ | Kod: `RCoord:68-72`. Test: `RT:229-253` | |
| R34-8 | MOD-0183 ile ortak senaryo (S23-16) | GEÇTİ | Test: `SCt/Returns/Receipt/ReturnReceiptJointScenarioTests.cs:24-111` (gerçek Shipments API'si aynı host'ta) | AVAILABLE −30, QUARANTINE +14, a2 hareketleri okunuyor, T1 değil |
| R34-9 | Seri izli SKU → INVENTORY seri reddi, o satır için gönderim yok | GEÇTİ | Kod: `CustomerReturnMovementMapper.cs` (`serialIds` hiç yok). Test: `RT:257-271` | |
| R34-10 | Pre-WP-P1 (`stockIssued = null`) → INVENTORY çağrısı yok, `NotPosted` / `PRE_WP_P1` | GEÇTİ | Kod: `RCoord:100-104`, `Shipment.cs:34-36`. Test: `RT:275-294`, `RT:297-305`, `RT:308-325` | `stockIssued = true` sabotajını `RT:34-76` karşılar |
| R34-11 | Reddedilen satırın düzeltilmesi `a2` ile; satır 1 değişmeden replay | GEÇTİ | Kod: `RCoord:140-157`. Test: `RT:405-425` | |
| R34-12 | Kalıcı ret → `unpostedLines` tamamlar; düzeltilebilir ret → 422 | GEÇTİ | Kod: `RCoord:25,121-136`. Test: `RT:429-458`, `RT:461-487`, `RT:490-509` | |
| R34-13 | Unknown düzeltilemez; önce saklanan istek tekrar gönderilir | GEÇTİ | Kod: `RCoord:68,142,174`. Test: `RT:516-546` | |
| R34-14 | 400 → 500, satır Blocked; `INTEGRITY_FAULT` çıkışı; Posted / `KEY_REUSED` satırı çıkamaz | GEÇTİ | Kod: `RCoord:166-171,213-217,128-132`. Test: `RT:551-579`, `RT:582-602` | |
| R34-15 | Düzeltilmiş satırda kaybolan cevap → sonraki komut `a2`'yi X ile tekrarlar, tek gönderim | GEÇTİ | Kod: `RCoord:174`. Test: `RT:607-634` | |
| R34-16 | Geç yazıcı, eski sürüm → kendi hareketini `…:customer-return-reversal:a1` ile ters kaydeder | GEÇTİ | Kod: `ReturnLateWriterPolicy.cs:29-47`. Test: `RT:660-698` | Gerçek INVENTORY'nin QUARANTINE'den çıkan bu REVERSAL'ı kabul edip etmediği açık (R-1), bkz. F-5 |
| R34-17 | Geç yazıcı, aynı sürüm, satır Refused / NotPosted → ters kayıt | GEÇTİ | Kod: `ReturnLateWriterPolicy.cs:29-30`. Test: `RT:703-733` | |
| §34.2 | `Received`'de istemci `inventoryTransactionReferenceId` → 400 | GEÇTİ | Kod: `RCoord:31`. Test: `RT:737-760` | |
| R34-3 metinleri | | KAPSAM DIŞI | — | DL-2 (Q504) |

### Q504 — MOD-0186 DL-2 (R34-3 metinleri)

| Satır | Ölçüt (kısa) | Sonuç | Kanıt | Not |
|---|---|---|---|---|
| R34-3a | Reddedilen satırın INVENTORY kodu (25 kod + Unknown), 7 dil | GEÇTİ | `Web/Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx`; ölçüm: her dilde 26/26 dolu, İngilizceyle aynı yok | |
| R34-3b | `RETURN_SOURCE_MOVEMENT_MISSING`, `RETURN_RECEIPT_IN_PROGRESS` (+ `INVENTORY_REFUSED`) | GEÇTİ | `ErrReturnSourceMovementMissing`, `ErrReturnReceiptInProgress`, `ErrInventoryRefused` 7/7 | `ErrReturnLineNotUnpostable`, `ErrDependencyUnavailable` da var |
| R34-3c | `NotPosted` / `PRE_WP_P1` etiketleri | GEÇTİ | `ReceiptLineNotPosted`, `ReceiptReasonPreWpP1` 7/7; `Web/wwwroot/assets/js/SupplyChain/Returns/index.js:129-134` | |
| R34-3d | Ekran `validationError`'a düşmez | GEÇTİ | `index.js:91-97` (kod → anahtar), `index.js:100-126` (kapalı liste), `index.js:283-284,302-311` (bilinmeyen kod genel metinle, son çare `ErrInternalError`). Test: `WebT/JavaScript/Node/return-receipt-text.test.mjs:46-99`, `WebT/JavaScript/ReturnReceiptTextTests.cs:47-85` | |
| Eşleşme sayısı | Toplam | GEÇTİ | 31 zorunlu anahtar × 7 = 217 MATCH; ek 15 alım anahtarı da 7/7 dolu; her dosyada 148 anahtar, küme eşit | |

### Ortak kontroller

| Kontrol | Sonuç | Kanıt |
|---|---|---|
| Kiracı izolasyonu (her sorguda TenantId + LE + `!IsDeleted`) | GEÇTİ | `ShipmentRepository.cs:13-16,40-41,67-69,73,110,140`; `ReturnRepository.cs:18-19,218,229-240`; kiracılar arası 404 testleri `DT:288`, `RT:190` |
| Yetki — API | GEÇTİ (desen notlu) | `SCsrc/Diten.SupplyChainService.Api/Controllers/ShipmentsController.cs:25-30` (`[HasPermission]`); dönüşler `[ReturnPermission]` filtresi `Api/Features/Returns/ReturnsController.cs:20` + hedef yetkisi `ReturnContextMiddleware.cs:104` (`.receive`); işleyicide ikinci kontrol `TransitionShipmentHandler.cs:14-15`. Dönüş tarafı `[HasPermission]` adını değil kendi filtresini kullanıyor; bu desen DL-1 öncesinden, yeni uç yok |
| Yetki — Web | Notlu | `Web/Controllers/SupplyChainReturnsController.cs:183,199` metot içi kontrol; `Index()` (`:91`) yetki denetlemiyor — DL-1/DL-2 öncesinden, kapsam dışı |
| Yalnız soft delete | GEÇTİ | Shipments / Returns persistence'ta `DeleteOne/DeleteMany` yok (grep); `DropUnpostedAttemptAsync` belge silmez, alt alanı `null` yapar (`ShipmentRepository.cs:122-124`) |
| Dokunulan görünümlerde sabit metin | KISMİ | `Web/Views/SupplyChain/Shipments/_Form.cshtml:30` ve `_IndexL10n.cshtml:5` içinde `GSKU` / `LSKU` sabit (F-3); dönüş görünümü ekleri tümü `Localizer[...]` |

## 3. Bulgular

- **F-1 (DÜŞÜK) — Paket dışı kural: `fromLocationId` boş satır yeni denemede reddediliyor.** `Coord:55` (commit `fc03f9f81`,
  #352) boş `fromLocationId` için 422 `SHIPMENT_LINE_STOCK_INCOMPLETE` veriyor. MOD-0183 r6/r7 §23.1 yalnız `skuLevel`
  ön kontrolünü yazıyor; `fromLocationId` isteğe bağlı gönderilen alan (`InventoryMovementMapper.cs:42`). Davranış makul ve
  test edilmiş (`DT:245-283`), ama kayıtlı bir karar ya da paket satırı yok. Paket sonraki revizyonda bunu yazmalı.
- **F-2 (DÜŞÜK) — Paket başlık metinleri bayat.** MOD-0183 ön bilgi `status_note` hâlâ "r7 … awaits CT-APR-02 and a
  CT-APR-01 row" diyor, oysa r7 onaylı (`approvals.tsv:27`). MOD-0186 §34 açılış notu hâlâ "Draft revision — not
  approved" diyor (`MOD-0186-reverse-logistics.md:917`), oysa r8 onaylı (`approvals.tsv:24`); plan açık noktası 3 aynı şeyi
  kaydetmiş. Kodu etkilemez; okuyanı yanıltır.
- **F-3 (DÜŞÜK) — Sabit kullanıcı metni `GSKU` / `LSKU`.** `Web/Views/SupplyChain/Shipments/_Form.cshtml:30`
  (`<option value="Gsku">GSKU</option>`) ve `_IndexL10n.cshtml:5` (`["Gsku"] = "GSKU"`); commit `a958ae52b` (#355) ve #336.
  Kısaltma kodları olsa da localization-standard kuralına göre resx'ten gelmeli ya da bilinçli istisna olarak yazılmalı.
  Q501 satırlarını etkilemez.
- **F-4 (DÜŞÜK) — S23-12 koruması yalnız yapıcı parametresine bakıyor.** `DT:432-446` bir hosted service'in
  `IInventoryMovementClient`'ı `IServiceProvider` ya da fabrika ile çözmesini yakalamaz. Bugün böyle bir servis yok;
  koruma dar. Dönüş tarafı için metin taraması var (`RT:308-325`), sevkiyat için yok.
- **F-5 (ORTA) — Gerçek INVENTORY ile ölçülmemiş varsayımlar.** R34-16 / R34-17 bir `CUSTOMER_RETURN`'ün QUARANTINE'den
  REVERSAL ile geri alınmasına dayanıyor; bu yalnız `FakeInventory` ile ölçüldü. MVP-1 CT R-1 (genişletilmiş) ve R-2
  (`ALREADY_REVERSED` geç yazıcıda) açık (`MOD-0186-DL1-IMPLEMENTATION-PLAN.md` §5 madde 1-2). INVENTORY'nin genel kuralı
  (`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Rules/ReversalRules.cs:37-55`) bir REVERSAL olmayan her
  hareketi kabul ediyor gibi görünüyor, ama QUARANTINE bakiyesi ve yön türetmesi canlı olarak ölçülmedi. Paketler de
  kabulü MVP-1 CT canlı turlarına bırakıyor.
- **F-6 (DÜŞÜK) — r7 belirsiz cevap sınıfları uçtan uca kısmen.** Zarfsız 503 ve 401 yalnız istemci birim testinde
  (`InventoryMovementClientTests.cs:76,98-99`); koordinatör testinde (`DT:640-659`) yalnız 500/502/504 var. Eşleme tek
  yerde (`InventoryMovementClient.cs:44-51`) olduğu için risk düşük.

YÜKSEK bulgu yok. Kod kusuru olarak sınıflanan bulgu yok; F-1 ve F-3 kural/kayıt eksikliği, F-5 ölçüm eksikliği.

## 4. Ölçülmeyenler

- Hiçbir test koşturulmadı (SupplyChain test derlemesi, `Diten.Web.Tests`, Node testleri, `scripts/tests`). Sonuçlar CT'nin
  ayrı koşusundan gelmeli.
- Sabotaj kanıtları (plan §3 tabloları) yeniden üretilmedi; yalnız testlerin sabotajda kırmızı olacak doğrudan
  iddia içerip içermediği okunarak değerlendirildi.
- Gerçek INVENTORY, Gateway, Auth, tarayıcı ve MVP-1 CT canlı turları ("Dispatched → stok düştü", "recall altındaki lot →
  reddedildi", "iade → karantina") — paketlere göre kabulün asıl kaynağı — ölçülmedi.
- Tarayıcıda ekran görüntüsü yok: metinlerin gerçekten ekranda göründüğü yalnız JS kaynağı ve Node test kodu ile değerlendirildi.
- AUD-001 sayısı ("kaç yazma komutu denetleniyor / istisna / borç"): `TenantArchitecture.ArchitectureTests` koşturulmadı.
- UAS-001 (yetkisiz kullanıcıya `CompensationFailed` çıkış eylemlerinin gizlenmesi) ve Web `Index()` yetkisi DL satırı
  olmadığı için ayrıntılı ölçülmedi.
- Kanıt kiti (§5: binary hash, PID, DB önce/sonra) bu VER'in kapsamında değil; yazarın kanıt belgesi
  (`docs/roadmap/plans/mvp6-integration-wp-p1-01/DL1-EVIDENCE-MOD0183.md`) okunmadı, ona güvenilmedi.

## 5. Kararlar

Q500: KOŞULLU — kod ve test kodu tüm DL-1 satırlarını karşılıyor; koşul: CT'nin filtresiz SupplyChain koşusunun yeşil olması (test koşturulmadı). F-1, F-2, F-4, F-6 düşük.
Q501: KABUL EDİLEBİLİR
Q503: KOŞULLU — kod ve test kodu tüm DL-1 satırlarını karşılıyor; koşullar: CT'nin test koşusunun yeşil olması ve F-5 (R-1/R-2, gerçek INVENTORY ile ters kayıt) MVP-1 CT canlı turunda kapanması.
Q504: KABUL EDİLEBİLİR

## 6. Değişmezlik kanıtı

Başlangıç:
```
$ git -C <projeler>/ERP-vNext-recovery-accept rev-parse HEAD
35e973bd22af68168686862f5b80f417c174a563
$ git -C <projeler>/ERP-vNext-recovery-accept status --porcelain
(boş)
```
Bitiş (rapor yazılmadan hemen önce ve sonra):
```
$ git -C <projeler>/ERP-vNext-recovery-accept rev-parse HEAD
35e973bd22af68168686862f5b80f417c174a563
$ git -C <projeler>/ERP-vNext-recovery-accept status --porcelain
(boş)
$ git -C <projeler>/ERP-vNext-recovery-accept branch --show-current
docs/sce/mvp6-acceptance-01
$ git -C <projeler>/ERP-vNext-recovery-accept diff --cached --name-only
(boş)
```
HEAD ve durum başta ve sonda aynı. Ağaç içinde dosya yazılmadı; bu rapor ağacın dışında.
