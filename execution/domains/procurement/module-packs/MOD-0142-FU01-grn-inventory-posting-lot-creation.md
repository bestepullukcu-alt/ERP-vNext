---
id: MOD-0142-FU01
name: GRN Inventory Posting and Lot Creation
domain: procurement
service: Diten.ProcurementService
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
revision: "r4 (2026-11-25) — bağımsız inceleme docs/records/reviews/MOD-0142-FU01/2026-11-25-bdea9ecf.md (r3, LF sha256 bdea9ecf…, KOŞULLU, F-1…F-9) işlendi: Web anahtarı yalnız belirsiz sonuçta korur, kesin ret ya da gövde değişince yeni anahtar; silinmiş kaydın anahtarı 409 IDEMPOTENCY_KEY_REUSED (index değişmez); Details'ten kayıtlı kayıtla yeniden deneme (RetryGrnPostingCommand, DL-1 + DL-3); çözme komutu kirası süren kayıtta 409; adım 2 sırası ve parmak izsiz eski kayıtlar; her yukarı akış çağrısından önce kira yenileme; telafide konum pasifleşmesi; açılış doğrulaması, seri kontrollü satır ve BulkDelete denetim satırı AC'leri; S-9 ve S-12 DL-1 kapısında onay satırıyla kabul edilecek öneri. r3 onaylanmadı ve r4 ile yer değiştirir. r3 (2026-11-25) — bağımsız inceleme docs/records/reviews/MOD-0142-FU01/2026-11-25-4803c8de.md (r2, LF sha256 4803c8de…, UYGUN DEĞİL, F-1…F-13) işlendi: telafi REVERSAL tarihi satır başına bir kez kalıcı (GrnLine.CompensationStartedAt), telafide kesin ret için CompensationBlocked + çıkış yolları (aynı anahtarla yeniden deneme ya da ResolveGrnCompensationCommand ile elle çözüm) + eskalasyon, aynı GRN'de kira (claim), tarihlerin ms'e kırpılması + gerçek Mongo gidiş-dönüş testi, S-3 DL-1 kapısından ayrıldı, S-4 onay satırıyla kabul edilecek öneri, lot çözüm kuralları, sahip soruları O-R3-1…O-R3-3, eksik AC ve sabotajlar (AC-20…AC-29, S15…S29), Audited Events gerçek merkezi denetim mekanizmasıyla yeniden yazıldı; ölçüm tabanı tek: origin/main 82af5c24a. r2 onaylanmadı ve r3 ile yer değiştirir. r2 (2026-11-25) — sahip kararları S-1, S-2, S-5 işlendi. r1 (2026-11-25) — ilk taslak."
owner: procurement / merkez (Beste) — MVP-2 sahibi; geliştirme merkezi ekip
approver: Beste (MVP-2 sahibi)
risk: HIGH
branch: feature/procurement/mod-0142-fu01-grn-inventory-posting (henüz açılmadı)
started: 2026-11-25
target: TBD
form_field_count: 12
parent: MOD-0142
---

# MOD-0142-FU01 — Mal kabul (GRN) → Inventory gerçek stok kaydı + lot açma

> **Taslak — kod yetkisi yok.** `status: draft`, `approval_status: pending`. Kod; bu sürümün bağımsız incelemesi (CT-APR-02),
> onay satırı (CT-APR-01, yüksek risk: entegrasyon + izin + sözleşme) ve Bölüm 23'teki açık soruların DL-1'i bekletenleri
> kapandıktan sonra, dilim sırasıyla yazılır. Sahip Beste "açalım" dedi (2026-11-25); geliştirme merkezi ekipte, Cihan
> beklenmiyor.
>
> **Revizyon r4 (2026-11-25) — taslak, onay bekliyor.** r3'ün bağımsız incelemesi
> (`docs/records/reviews/MOD-0142-FU01/2026-11-25-bdea9ecf.md`, KOŞULLU; dört ORTA F-1…F-4, beş DÜŞÜK F-5…F-9) bu sürümde işlendi;
> bulgu başına kod doğrulaması ve kapanış Bölüm 19.4'te `F-n@bdea9ecf` satırlarındadır. Ölçüm tabanı değişmedi (`origin/main`
> `82af5c24a`). Yeni sahip sorusu yok; S-4 ve O-R3-1…O-R3-3 açık ve metinleri değişmedi. DL-1 kapısına S-9 ve S-12 "onay satırıyla
> kabul edilecek öneri" olarak eklendi (Bölüm 22). Yeni uç `POST /api/grn/{grnId}/posting/retry` mevcut `procurement.grn.create`
> iznini kullanır; yeni izin anahtarı yok.
>
> **Revizyon r3 (2026-11-25) — taslak, r4 ile yer değiştirdi.** r2'nin bağımsız incelemesi
> (`docs/records/reviews/MOD-0142-FU01/2026-11-25-4803c8de.md`, UYGUN DEĞİL; iki ENGEL F-1, F-2 ve F-3…F-13) bu sürümde işlendi;
> bulgu başına kod doğrulaması ve kapanış Bölüm 19.4'tedir. Yeni sahip soruları O-R3-1…O-R3-3 (Bölüm 23). DL-1'i bekletenler S-4
> ve O-R3-1…O-R3-3'tür; onay satırı notu paketin önerilerini açıkça kabul ederse kapanırlar (Bölüm 22). S-3 artık yalnız GRN-EVENT
> sözleşme PR'ını (C-3) ve DL-4'ü bekletir.
>
> **Revizyon r2 (2026-11-25).** Beste S-1, S-2 ve S-5'i cevapladı
> (`docs/records/decisions/2026-11/mod-0142-fu01-owner-decisions-01.md`): **S-1 = Evet** (kullanıcı token'ı; Bölüm 14),
> **S-2 = A** (kesin rette hemen geri alma; Bölüm 8.2 adım 5), **S-5 = konum zorunlu ve LOCATION listesinden seçilir** (Web seçici
> + API doğrulaması; Bölüm 8.2 adım 2a, 11, 12, AC-18, AC-19).
>
> **Ölçüm tabanı (r3, tek):** `origin/main` `82af5c24a` (2026-11-25 10:49 +0300). Bütün `yol:satır` atıfları bu commit'tedir.
> r1 atıfları `22d30cc75`'te alınmıştı: `git diff --stat 22d30cc75 origin/main` atıf yapılan bütün yollarda (Procurement ve Inventory
> kaynakları, SupplyChain, dört sözleşme, `ocelot.json`, Web GRN dosyaları, denetim defteri, registry, ana paket) boş — satırlar
> aynı. r2 atıfları MVP-1 hattındaki bir HEAD'te (`32ce7df6c`) alınmıştı; o hat main'den farklıdır, r3'te her biri `origin/main`'de
> tek tek yeniden okundu ve tutuyor (Bölüm 19.3). Bu paketi yazan çalışma ağacının HEAD'i (`c815d95d0`, MVP-1 hattı) `origin/main`'i
> içermez; hiçbir kod atfı oradan alınmadı. Satır atıfları DL-1 başında bir kez daha doğrulanır (F-13).
>
> **Ana paket (MOD-0142) bu işi açıkça erteledi:** `MOD-0142-receiving-grn.md:87` ("mock şimdi, gerçek 0173 sonra; GRN kodu
> değişmez"), `:143` (mock/gerçek), `:174` (progressive integration), `:181` ("mock→gerçek geçiş doğrulaması → integration smoke
> follow-up"). Bu FU o ertelemenin karşılığıdır. Ölçüm, ana paketin "GRN kodu değişmez" varsayımının **tutmadığını** gösterdi
> (Bölüm 1.1): gerçek INVENTORY/TRACE sözleşmesiyle çalışmak için GRN el kodunda da değişiklik gerekir.

## 0. Kimlik ve keşif (DCP-002)

### 0.1 Kimlik ön kontrolü

- `python .antigravity/scripts/verify_module_id.py . --check-id MOD-0142-FU01 --name "GRN Inventory Posting and Lot Creation" --parent MOD-0142`
  → `OK  MOD-0142-FU01: proven against Blueprint/registry.` (çıkış 0, 2026-11-25). Ebeveyn `MOD-0142 Receiving (GRN)`
  Blueprint'te ve registry'de (`module-id-registry.md:355`); `--check-id MOD-0142 --name "Receiving (GRN)"` de çıkış 0.
- `MOD-0142-FU*` hiçbir yerde yok: `git grep "MOD-0142-FU" origin/main` boş; çalışma ağacında `execution/` + `docs/` boş;
  yerel/uzak ref adlarında `0142|grn` yok. Kimlik **uydurulmadı**: yeni modül değil, MOD-0142'nin takip paketi (yeni SoR
  nesnesi yok, yeni servis yok, yeni rota yok).
- Registry satırı henüz yok → CT adımı (Bölüm 24, C-1). Bu paket registry'yi değiştirmez.

### 0.2 Keşif çıktısı ve bulgu kararları

Komut: `python .antigravity/scripts/verify_module_id.py . --discover "<sorgu>"` (araç sha256 `ae87a566…b51a`, depo HEAD
`103c6b2fe`, origin/main `22d30cc75`, 568 ref, Blueprint 8.8 1005 satır, registry 287 satır).

| Sorgu | Çıkış | rapor-sha256 | Bulgular |
|---|---|---|---|
| `goods receipt inventory posting` | 3 (inceleme gerekli) | `1cb12daa9c6d6376ce9f68a37a2296578dfe2e7ad5abb082928a2e76ab0cdc00` | sor 1, gateway 1 |
| `GRN lot` | 3 (inceleme gerekli) | `306b20414170bfc2be264e8ea5eae1c2dc0b8ccfdc7bfdaa878e14a3007e0f72` | gateway 1 |
| `inventory posting` | 3 (inceleme gerekli) | `4436d6cd07cefa0f1bc9cb97d065e46bb26e6fcfbab290698cdfa27bbb14ae64` | gateway 1 |
| `receipt lot creation` | 0 (eşleşme yok) | `280d37fa7542d42be95c910e51b06e5614165e8e299279c32fc639f8099d157e` | — |

Her bulgu için karar:

| # | Bulgu | Karar | Gerekçe |
|---|---|---|---|
| K-1 | `BULGU sor: 'goods receipts' sahibi MOD-0142 (Receiving (GRN))` | **Mükerrer değil** — bu FU'nun ebeveyni | SoR nesnesi (goods receipts) MOD-0142'de kalır; FU yeni SoR nesnesi açmaz, yalnız ebeveynin mevcut `RecordGrn`/`ReverseGrn` akışını gerçek tüketiciye bağlar. Açıklama türü: onaylı ebeveyn–takip ilişkisi. |
| K-2 | `BULGU gateway: 'inventory' → servis port(lar)ı 5067` (iki sorguda) | **Mükerrer değil** — üretici–tüketici | `/api/inventory` (`ocelot.json:890`) ve `/api/inventory/{everything}` (`:907`, GET/POST/OPTIONS `:908-912`) MOD-0173'ün rotası; GRN yalnız `postMovement` tüketir. Yeni rota yok, rota değişikliği yok. |
| K-3 | `BULGU gateway: 'grn' → servis port(lar)ı 5065` | **Mükerrer değil** — ebeveynin kendi rotası | `/api/grn` MOD-0142'nin mevcut rotası; FU yeni uç açmaz. |
| K-4 | `blueprint 0 · registry 0 · paket 0 · görev 0` (dört sorguda) | Çakışma yok | Ne Blueprint'te ayrı bir "GRN posting" yeteneği, ne registry'de, ne bir dalda/pakette, ne kayıtlı CT kuyruklarında aynı iş var. |
| K-5 | Keşfin dışında okunan ilgili iş: TRACE `createLot` özeti "Lot oluştur (GR sırasında 0142 çağırır)" (`trace.openapi.yaml:82`); SupplyChain `InventoryMovementClient` (MOD-0183, `InventoryMovementClient.cs:10-28`) | **Mükerrer değil** | TRACE ucu bu tüketici için tasarlanmış üretici; SCE istemcisi başka modülün (sevkiyat çıkışı) istemcisi — **emsal** olarak alınır, paylaşılmaz (servisler arası iç tip paylaşımı yok). |

**Kısmi inceleme (aracın kendi EKSİK satırları):** bu makinede olmayan ya da son fetch'ten sonra itilen dallar, commit edilmemiş
ya da başka makinedeki işler, kayıtlı olmayan görev kaynakları (sohbetler, notlar) görülmedi; paketlerin iş kapsamı okunmadı
(dosya adı + künye id). Araç "son fetch: 2026-11-20T13:06" yazdı; fetch 2026-11-25'te koştu, origin/main `22d30cc75`'e ilerledi
(araç FETCH_HEAD zamanını okuyor). Blueprint ve registry çalışma ağacından (HEAD) okundu; registry'nin HEAD↔main farkı yalnız
MOD-0193 ve MOD-0040 satırları (bu işle ilgisiz).

### 0.3 XMC-001 tablosu

| Yol | Sahip | Bu FU ne yapar | İzin |
|---|---|---|---|
| `services/Diten.ProcurementService/**` (Grn, Common, Infrastructure, Api `Program.cs`/appsettings, Persistence DI, testler) | MOD-0142 — merkez (Beste, MVP-2) | kod (DL-1, DL-2) | kendi modülü. `Persistence/DependencyInjection.cs`, `Program.cs`, `ProcurementApiFactory.cs` MOD-0140 ile **paylaşılan** dosyalar → aynı servis, aynı sahip; değişiklik yalnız INVENTORY/TRACE kayıt satırları, MOD-0140 satırlarına dokunulmaz |
| `docs/analysis/contracts/grn-event.openapi.yaml` | MOD-0142 (OWNED-FROZEN v1) | additive 1.1.0 (Bölüm 15.1; r3: çözme işlemi + isteğe bağlı `postingState`) | sahip onayı (Beste) — Bölüm 23 S-3; ayrı sözleşme PR'ı (C-3) |
| `frontend/Diten.Web/Controllers/GrnController.cs`, `Views/Procurement/Grn/**`, `Resources/Views/Procurement/Grn/**`, `Models/Procurement/Grn/**` | MOD-0142 ekranı | kod (DL-3) | kendi ekranı |
| `services/Diten.InventoryService/**` (MOD-0173 INVENTORY + MOD-0174 TRACE) | merkez (bizim) | **kod değişikliği YOK** — yalnız tüketim | gerekmez |
| `docs/analysis/contracts/inventory-bundle.openapi.yaml` (1.8.0 FROZEN), `trace.openapi.yaml` (1.4.0 FROZEN), `location.openapi.yaml` (1.1.0 FROZEN, MOD-0173-FU02; r2, S-5) | MOD-0173 / MOD-0174 / MOD-0173-FU02 | değişiklik yok — yalnız tüketim | — |
| `gateway/Diten.ApiGateway/ocelot.json` | `integration-agent` | değişiklik yok (`:890`, `:907`, `:924`, `:942`, `:960`, `:978` + mevcut `/api/grn`) | — |
| `services/Diten.MdmService/**`, `services/Diten.AuthService/**` | MDM / Auth sahipleri | **yok** — servis token'ı yok (Bölüm 14, S-1 kararı), izinler kiracı rol yapılandırması | — |
| `services/Diten.SupplyChainService/**` (`InventoryMovementClient`) | MVP-5/MVP-6 | dokunulmaz — yalnız emsal | — |
| `services/Diten.ProcurementService/**/InvoiceMatch/**` (MOD-0143 üçlü eşleme; r3, F-12) | Procurement (merkez) | **kod değişikliği yok — bilgilendirme.** Eşleme yalnız `Posted` GRN'i okur (`RunThreeWayMatchHandler.cs:73-74`); FU ile listede daha çok `Draft` GRN (Uncertain, CompensationPending, CompensationBlocked, Compensated, ManuallyResolved) görünür, hiçbiri eşlemeye girmez | gerekmez; sahibe bilgi notu C-8 |
| `services/Diten.InventoryService/**/Quarantine/**` (MOD-0175; r3, F-12) | merkez | **kod değişikliği yok — bilgilendirme.** `QUALITY_INSPECTION` / `QUARANTINE`'e giren her GRN satırı INVENTORY'de bir giriş tutumu açar (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Quarantine/Services/QuarantineIntake.cs:15-20`); kalite kararı başlamış / bitmiş kaynağın REVERSAL'ı 422 `REVERSAL_QUALITY_RECORD_LOCKED` alır (`InventoryErrorCodes.cs:101-107`) → telafide `CompensationBlocked` (Bölüm 8.2 adım 5e), ters kayıtta S-8 | gerekmez |
| `services/Diten.InventoryService/**/Locations/**`, `location.openapi.yaml` (MOD-0173-FU02 LOCATION; r3, F-12) | merkez | **kod / sözleşme değişikliği yok — bilgilendirme.** Yeni tüketici: Procurement API (yeni kayıt ve her yeni deneme başına iki GET) ve Web seçici proxy'leri; yeni izin yükü: mal kabul rolünde `inventory.locations.read` (O-R3-1) | gerekmez |
| Platform merkezi denetim (`/api/internal/audit/append`; r3, F-9) | merkez | **değişiklik yok.** GRN kayıtları mevcut kategori / işlem adlarıyla gelir (Bölüm 21); kaynak modül alanı serbest metin | gerekmez |
| `tests/architecture/audit-ledger/Diten.ProcurementService.md` | Control Tower | "Bilinen borç"tan dört GRN satırı çıkar (Bölüm 21) | CT adımı C-4 |

## 1. Module Summary

MOD-0142 GRN bugün INVENTORY'ye **mock** ile post ediyor: `Persistence/DependencyInjection.cs:74-78`
`services.AddSingleton<IInventoryPostingClient, MockInventoryPostingClient>()` (görevde verilen `:61` atfı eskimiş; `:61` artık
MOD-0140 tedarikçi deposu seçimi). Mock deterministik `txn-…`/`lot-…` üretir (`IInventoryPostingClient.cs:56-72`), stok hiçbir
yerde artmaz. Bu FU, GRN kaydını **gerçek** MOD-0173 INVENTORY `postMovement` ve MOD-0174 TRACE `createLot` uçlarına Gateway
(5000) üzerinden, kullanıcının kendi token'ıyla bağlar; lotlu SKU'da önce lot açılır, hareket gerçek `lotId` ile post edilir.
Mal kabul konumu zorunludur ve şirketin LOCATION kayıtlarından seçilir; API konumu hiçbir yukarı akış yazımından önce LOCATION'da
doğrular (S-5 kararı, r2). Kesin rette o denemede yazılmış satırlar hemen geri alınır, GRN `Posted` olmaz (S-2 kararı, r2).
Geri almanın kendisi kesin retle karşılaşırsa GRN `CompensationBlocked`'a geçer ve görünür olur; çıkış ya engel kalkınca aynı
anahtarla yeniden deneme ya da yetkili kişinin elle çözüp işaretlemesidir (r3, F-2). Aynı GRN'de aynı anda tek istek işler (kira;
r3, F-3). Yarıda kalan (`Uncertain`, `InProgress`, `CompensationPending`, `CompensationBlocked`) kayıt, form sayfası kapansa da
Details sayfasından kaydın kendi anahtarı ve gövdesiyle yeniden denenir (r4, F-3@bdea9ecf).
Hedef kullanıcı: depo/mal kabul operatörü. Kazanç: mal kabul sonrası stok MOD-0173 bakiyesinde gerçekten görünür (G2A golden
flow'un GRN→0173 segmenti, E5).

### 1.1 Ölçülen boşluklar (gerçek uçla çalışmayı engelleyen)

| # | Ölçüm | Sonuç gerçek uçta |
|---|---|---|
| B-1 | `RecordGrnHandler.cs:117-135` her satırı `PostMovementAsync` ile post ediyor; `try/catch` yok, dönüş yalnız `InventoryMovementResult(TransactionId, LotId, IdempotentReplay)` (`IInventoryPostingClient.cs:31-34`) — ret/erişilemezlik ifade edilemiyor | INVENTORY 422/503 → istisna → 500; satır 0 post edilmiş, satır 1 düşmüşse stok kısmen artmış, GRN hiç yazılmamış |
| B-2 | Satır anahtarı `{key}#{i}` (`:118`, `:188-189`) kararlı; ama `GrnId` (`:98`, `:184-185`) ve `receivedAt` (`:101`) **her denemede yeniden üretiliyor** | INVENTORY parmak izi `documentGroupId`, `sourceDocumentId`, `transactionDate`, `postingDate` içerir (`RequestFingerprint.cs:21,39,42-43`); aynı anahtarla farklı gövde → **400 `IDEMPOTENCY_KEY_REUSED`** (`InternalMovementPostingService.cs:342-347`). Yani yarıda kalan GRN'i aynı anahtarla tekrar göndermek bugünkü kodla mümkün değil |
| B-3 | İstek kaydı `LotNumber` taşıyor (`IInventoryPostingClient.cs:19`); INVENTORY `lotId` ister (`PostMovementCommand.cs:35`; şema `inventory-bundle.openapi.yaml:413`) | Lotlu SKU → 422 `LOT_REQUIRED` (`InventoryErrorCodes.cs:34`); uydurma id → 422 `UNKNOWN_LOT` (`:47`). Önce TRACE `createLot` gerekir |
| B-4 | İstek kaydı `WarehouseId/LocationId` (`:15-16`); INVENTORY giriş hareketi `toWarehouseId` **ve** `toLocationId` ister (`MovementDirectionRules.cs:56-61`); GRN `locationId` nullable (`grn-event.openapi.yaml:81`) | Konumsuz GRN → INVENTORY 400 `INVALID_REQUEST` |
| B-5 | `ReverseGrnHandler.cs:57-75` REVERSAL'ı `sourceModule=MOD-0142`, `sourceType=GOODS_RECEIPT`, `sourceDocumentId=GrnId`, depo/konum/lot/seri dolu gönderiyor | INVENTORY REVERSAL zarfı `sourceModule=MOD-0173`, `sourceType=REVERSAL`, `sourceDocumentId=<orijinal transactionId UUID>` ister (`ReversalRules.cs:7-34`), yön ve lot/seri **gönderilmez** (`MovementDirectionRules.cs:109-112`, `MovementRequestParser.cs:125-126`) → bugünkü ters kayıt gerçek uçta **400** |
| B-6 | `transactionDate`/`postingDate` zorunlu (`inventory-bundle.openapi.yaml:398`); istek kaydında yok | 400 |
| B-7 | Web her POST'ta yeni anahtar üretiyor (`frontend/Diten.Web/Controllers/GrnController.cs:313-319`) | 503 sonrası kullanıcı tekrar kaydederse yeni GRN açılır; yarıda kalan Draft sahipsiz kalır |
| B-8 | GRN-EVENT `recordGrn` yalnız 201/422 bildiriyor (`grn-event.openapi.yaml:32-46`) | 503/409/403 sözleşmede yok → additive 1.1.0 gerekir (Bölüm 15.1) |
| B-9 | `DeleteGrnHandler.cs:30-34` ve `BulkDeleteGrnHandler.cs:38` her Draft'ı siler | Satırları kısmen post edilmiş Draft silinirse stok sahipsiz kalır |

## 2. Ownership and Boundaries

- **In-scope:** GRN→INVENTORY ve GRN→TRACE gerçek HTTP istemcileri; `Inventory:Mode` anahtarı; Draft-önce kayıt + kaldığı yerden
  devam + telafi (Bölüm 8.2 durum makinesi); hata eşlemesi; REVERSAL zarfı; GRN-EVENT 1.1.0 additive; Web'de anahtarın
  tekrar gönderimde korunması ve yeni hata kodlarının 7 dilde gösterimi; dört GRN komutunun merkezi denetim işareti;
  **(r2, S-5)** depo ve konumun şirketin LOCATION kayıtlarından seçilmesi (Web seçici) ve API'de konumun LOCATION'da doğrulanması;
  **(r3)** telafide kesin ret için `CompensationBlocked` durumu ve `ResolveGrnCompensationCommand` çıkış yolu, aynı GRN'de kira
  (claim), Details sayfasında durum rozeti ve çöz formu; **(r4)** `RetryGrnPostingCommand` ve Details "Kaydı yeniden dene" düğmesi,
  Web'de anahtarın yalnız belirsiz sonuçta korunması, silinmiş kaydın anahtarının yeniden kullanılmaması.
- **Out-of-scope:** INVENTORY/TRACE/LOCATION kodu ya da sözleşmesi (değişmez); servis token'ı (MOD-0033-FU02; S-1 kararı: kullanıcı
  token'ı); depo varsayılan mal kabul konumu (FU02 adayı) ve konum ana verisi yönetimi (MOD-0173-FU02'nin ekranı);
  `SUPPLIER_RETURN` (iade iş akışı); üretim/son kullanma tarihi
  ve tedarikçi lotu alanları (S-6); `SkuLevel=Item` (S-7); `GoodsReceived` olayının gerçek event bus'a yayını (ana paket);
  PRODUCT-MASTER gerçek doğrulayıcısı (`PermissiveProductReferenceValidator`, `Persistence/DependencyInjection.cs:70-72` —
  INVENTORY zaten fail-closed doğruluyor, ayrı iş); **(r3)** `CompensationBlocked` için kişiye bildirim / Görev Merkezi iş öğesi
  (O-R3-3 önerisi: FU02) ve GRN liste ekranında `PostingState` filtresi (FU02).
- **Shadow stock yasağı korunur:** GRN yalnız dönen `transactionId`/`lotId` referanslarını saklar; bakiye INVENTORY'dedir.

## 3. Owned Objects

- **Entity (mevcut, additive alanlar):** `GoodsReceipt` (+ `RequestFingerprint`, `PostingState`, `PostingAttempt`,
  `ReverseStartedAt`; r3: `ClaimToken`, `ClaimExpiresAt`, `CompensationBlockedAt`, `ResolvedBy`, `ResolvedAt`, `ResolutionNote`),
  `GrnLine` (+ `PostedAttempt`, `ReversalTransactionId`, `CompensationTransactionId`; r3: `CompensationStartedAt`,
  `CompensatedExternally`, `CompensationBlockedCode`, `CompensationBlockedRecord`, `PostingHistory`).
- **Commands (mevcut, davranışı değişir):** `RecordGrnCommand`, `ReverseGrnCommand`, `DeleteGrnCommand`, `BulkDeleteGrnCommand`.
- **Command (yeni, r3 — F-2; tasarım O-R3-2 sahip onayı bekler):** `ResolveGrnCompensationCommand` — `CompensationBlocked` GRN'i
  yetkili kişinin zorunlu notla elle çözüp `ManuallyResolved` işaretlemesi (Bölüm 8.2 adım 5e). Yukarı akış çağrısı yok.
- **Command (yeni, r4 — F-3@bdea9ecf):** `RetryGrnPostingCommand(GrnId)` — `Uncertain`, `InProgress`, `CompensationPending` ya da
  `CompensationBlocked` Draft'ı **kayıttaki** anahtar ve gövdeyle (istek gövdesi yok) Bölüm 8.2 adım 2'nin aynı dalından sürdürür
  (Bölüm 8.2 adım 2c). Parmak izi denetimi yapılmaz (gövde kayıttır, istekten gelmez); kira, deneme sınırı ve LOCATION kuralları
  aynıdır.
- **Queries:** değişmez (`GetGrnList`, `GetGrnById`; Draft zaten listede görünür). `GrnResponseDto`'ya isteğe bağlı `PostingState`
  eklenir (r3; Details rozeti için; GRN-EVENT 1.1.0 additive, Bölüm 15.1).
- **Seam'ler (Application/Common):** `IInventoryPostingClient` (sonuç türü genişler: `InventoryCallOutcome`), yeni `ITraceLotClient`
  (`CreateOrResolveLotAsync`), yeni `ILocationReferenceClient` (`ValidateReceivingLocationAsync`; r2, S-5).
- **Uygulamalar:** `HttpInventoryPostingClient`, `HttpTraceLotClient`, `HttpLocationReferenceClient` (Infrastructure);
  `MockInventoryPostingClient`, `MockTraceLotClient`, `MockLocationReferenceClient` (yalnız `Testing` ortamında seçilebilir).
- **Web seçici proxy'leri (r2, S-5; servis ucu değil):** `GrnController` içinde `GET Grn/api/warehouses?search=` ve
  `GET Grn/api/locations?parentId=&search=` — Gateway `/api/locations/search`'e kullanıcı token'ıyla gider (emsal
  `SupplyChainShipmentsController.cs:234-251`, `:698-710`).
- **Servis:** `GrnInventoryPostingService` (Application/Features/Grn/Services) — lot + hareket + telafi orkestrasyonu; handler
  yalnız kurar, çağırır, kaydeder (handler-design).
- **API uçları:** mevcut dördü değişmez (`POST /api/grn`, `POST /api/grn/{grnId}/reverse`, `DELETE /api/grn/{grnId}`,
  `DELETE /api/grn/bulk`); yeni (r3): `POST /api/grn/{grnId}/compensation/resolve` (`procurement.grn.reverse` — O-R3-2); yeni (r4):
  `POST /api/grn/{grnId}/posting/retry` (`procurement.grn.create` — kaydetme yetkisinin aynısı; Bölüm 14).
- **İzinler:** yeni GRN izin anahtarı yok (çözme mevcut `procurement.grn.reverse`, yeniden deneme mevcut `procurement.grn.create` ile). Tüketilen izinler Bölüm 14.

## 4. Entity Fields

| Alan | Tür | Kural | Index |
|---|---|---|---|
| GoodsReceipt.RequestFingerprint | string (SHA-256 hex) | Draft yazılırken istek gövdesinden (normalize: trim, Decimal kanonik, lot numarası TRACE kuralıyla) **ve çağıran kullanıcının kimliğinden** (`sub`; r3, F-10) hesaplanır; aynı anahtarla farklı gövde ya da başka kullanıcı → 409 `IDEMPOTENCY_KEY_REUSED` — `Completed` kayıtta da (r4, F-5@bdea9ecf). FU01 öncesi yazılmış kayıtta `null`'dır ve geriye doldurulmaz; davranışı Bölüm 8.2 adım 2 madde (ii) | — |
| GoodsReceipt.IdempotencyKey | (mevcut) | **r4, F-2@bdea9ecf:** index (`ux_tenant_le_idempotencykey`, tenant + LE, `Unique`, `Sparse`; `GrnIndexConfiguration.cs:31-35`) **değişmez**; soft delete edilmiş kaydın anahtarı da index'te kalır ve yeniden kullanılamaz (Bölüm 8.2 adım 2 madde (i)). `RetryGrnPostingCommand` bu alandaki anahtarı kullanır | (mevcut) |
| GoodsReceipt.PostingState | enum(NotStarted, InProgress, Uncertain, CompensationPending, CompensationBlocked, Compensated, Completed, ManuallyResolved) | Bölüm 8.2; `Status=Posted` ⇔ `Completed`; `CompensationBlocked` ve `ManuallyResolved` r3 (F-2) | — |
| GoodsReceipt.PostingAttempt | int ≥ 1 | yalnız kesin ret + tamamlanmış telafiden sonra artar; üst sınır `Grn:MaxPostingAttempts` (r3, F-11) | — |
| GoodsReceipt.ReverseStartedAt | DateTimeOffset? | ters kaydın ilk denemesinde bir kez yazılır (UTC, ms'e kırpılmış — r3, F-4); REVERSAL `transactionDate`/`postingDate` buradan (kararlı parmak izi) | — |
| GoodsReceipt.ClaimToken / ClaimExpiresAt | string? / DateTimeOffset? | kira (claim; r3, F-3): `Version` koşullu atomik güncellemeyle alınır, her durum yazımında **ve her yukarı akış çağrısından hemen önce** (r4, F-6@bdea9ecf) yenilenir, akış biterken boşaltılır (Bölüm 8.2 adım 2b) | — |
| GoodsReceipt.CompensationBlockedAt | DateTimeOffset? | ilk `CompensationBlocked` geçişinde bir kez (r3, F-2) | — |
| GoodsReceipt.ResolvedBy / ResolvedAt / ResolutionNote | string? / DateTimeOffset? / string? (≤ 500) | yalnız `ResolveGrnCompensationCommand` yazar (r3, F-2) | — |
| GrnLine.PostedAttempt | int? | satır hangi denemede post edildi | — |
| GrnLine.CompensationTransactionId | string? | telafi REVERSAL'ının id'si | — |
| GrnLine.CompensationStartedAt | DateTimeOffset? | **r3, F-1:** `PostingState=CompensationPending`'i yazan sürüm denetimli yazımda, satırda boşsa bir kez yazılır (UTC, ms'e kırpılmış); telafi REVERSAL'ının `transactionDate`/`postingDate`'i; yeniden denemede değişmez | — |
| GrnLine.CompensatedExternally | bool | telafi 422 `ALREADY_REVERSED` ile kapandı (id yok; r3) | — |
| GrnLine.CompensationBlockedCode / CompensationBlockedRecord | string? / string? | telafinin kesin ret kodu ve zarftaki `recordNumber` (r3, F-2) | — |
| GrnLine.PostingHistory | liste (deneme, transactionId, compensationTransactionId, compensatedExternally, compensationStartedAt, compensationBlockedCode) | yeni deneme başlarken önceki denemenin satır alanları buraya taşınır (r3) | — |
| GrnLine.ReversalTransactionId | string? | `ReverseGrn` REVERSAL'ının id'si (satır satır kalıcı) | — |
| GoodsReceipt.GrnId / ReceivedAt | (mevcut) | **Draft'ta bir kez** üretilir (`ReceivedAt` UTC, ms'e kırpılmış — r3, F-4); devam eden denemeler aynı değeri kullanır (B-2) | (mevcut) |
| GrnLine.LotId / InventoryTransactionId | (mevcut) | gerçek TRACE `lotId` / INVENTORY `transactionId` | — |

`form_field_count: 12` ana paketten (`MOD-0142-receiving-grn.md:68`); bu FU forma kullanıcı alanı eklemez (anahtar gizli teknik
alan). Mongo: yeni alanlar additive, mevcut belgeler `PostingState` yokken `Status`'tan türetilir (Posted→Completed,
Draft→NotStarted); taşıma betiği yok.

## 5. Repo Scope

**DL-1 / DL-2 (backend):**
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Common/IInventoryPostingClient.cs` (sözleşme
  kaydı + sonuç türü; Mock aynı dosyada kalabilir ya da `MockInventoryPostingClient.cs`'e ayrılır)
- `…Application/Common/ITraceLotClient.cs` (yeni) + `MockTraceLotClient`
- `…Application/Common/ILocationReferenceClient.cs` (yeni, r2 — S-5) + `MockLocationReferenceClient`
- `…Application/Features/Grn/Validators/RecordGrnValidator.cs` (`LocationId` boş olamaz; r2 — S-5)
- `…Application/Features/Grn/Services/GrnInventoryPostingService.cs` (yeni)
- `…Application/Features/Grn/Handlers/CommandHandlers/{RecordGrn,ReverseGrn,DeleteGrn,BulkDeleteGrn}Handler.cs`
- `…Application/Features/Grn/Commands/{RecordGrn,ReverseGrn,DeleteGrn,BulkDeleteGrn}Command.cs` (merkezi denetim bildirimi)
- `…Application/Features/Grn/Commands/ResolveGrnCompensationCommand.cs`, `…Handlers/CommandHandlers/ResolveGrnCompensationHandler.cs`,
  `…Validators/ResolveGrnCompensationValidator.cs`, `…Application/Features/Grn/GrnCentralAudit.cs` (yeni, r3 — F-2, F-9; tasarım O-R3-2)
- `…Application/Features/Grn/Commands/RetryGrnPostingCommand.cs`, `…Handlers/CommandHandlers/RetryGrnPostingHandler.cs` (yeni, r4 —
  F-3@bdea9ecf; gövdesiz komut, doğrulayıcı gerekmez — yalnız `GrnId` yol parametresi)
- `services/Diten.ProcurementService/src/Diten.ProcurementService.Api/Controllers/GrnController.cs` (yalnız yeni
  `POST {grnId}/compensation/resolve` (r3) ve `POST {grnId}/posting/retry` (r4) eylemleri; mevcut eylemler değişmez)
- `…Domain/Repositories/IGrnRepository.cs` (r4, F-2@bdea9ecf: silinmişleri de gören, tenant + LE kapsamlı
  `GetByIdempotencyKeyIncludingDeletedAsync`)
- `…Application/Features/Grn/GrnModels.cs` (REVERSAL zarf sabitleri, hata kodları)
- `…Domain/Entities/GoodsReceipt.cs` (additive alanlar)
- `…Persistence/DependencyInjection.cs` (yalnız `:74-78` Mock kaydı kaldırılır), `…Persistence/Repositories/GrnRepository.cs`
  (r3: kira koşullu güncelleme; son durum yazımı Mongo işleminde + merkezi denetim enlist; r4: kira yenileme yazımı, silinmişleri
  gören anahtar araması). `…Persistence/GrnIndexConfiguration.cs` **değişmez** (r4, F-2@bdea9ecf: index'e kısmi süzgeç eklenmez)
- `…Infrastructure/Clients/HttpInventoryPostingClient.cs`, `…Infrastructure/Clients/HttpTraceLotClient.cs`,
  `…Infrastructure/Clients/HttpLocationReferenceClient.cs` (yeni, r2 — S-5),
  `…Infrastructure/Clients/InventoryModeRegistration.cs` (yeni), `…Infrastructure/DependencyInjection.cs`
- `…Api/Program.cs` (`AddHttpClient` + zaman aşımı), `…Api/appsettings.json`, `…Api/appsettings.Development.json`
- `docs/analysis/contracts/grn-event.openapi.yaml` (1.1.0 additive — sahip onayından sonra)
- Testler: `services/Diten.ProcurementService/tests/Diten.ProcurementService.Api.Tests/Grn/**`,
  `…/TestInfrastructure/ProcurementApiFactory.cs` (yalnız `Inventory:Mode=Mock` ayarı); gerçek Mongo vakaları mevcut `…/TestInfrastructure/ProcurementMongoReplicaSet.cs` ile (r3)

**DL-3 (Web):** `frontend/Diten.Web/Controllers/GrnController.cs` (r2: iki seçici proxy eylemi), `Models/Procurement/Grn/GrnViewModels.cs`
(r2: `LocationId` zorunlu; seçili depo / konumun görünen kod ve adı geri çizim için), `Views/Procurement/Grn/{Create,_Form}.cshtml`,
`wwwroot/assets/js/Procurement/Grn/form.js` (r2: depo ve konum seçicisi; emsal `wwwroot/assets/js/SupplyChain/Shipments/create.js`),
`Views/Procurement/Grn/Details.cshtml` ve `wwwroot/assets/js/Procurement/Grn/details.js` (r3: durum rozeti + çöz formu; Web
`GrnController`'da çöz proxy eylemi; r4: "Kaydı yeniden dene" düğmesi ve yeniden deneme proxy eylemi; Create POST'unda anahtar
yenileme kuralı ve gizli `IdempotencyBodyHash` alanı — F-1@bdea9ecf, F-3@bdea9ecf),
`Resources/Views/Procurement/Grn/GrnIndex.{en,tr,fr,es,zh,ar,ru}.resx`.
Liste ekranı dosyaları (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`, `index.js`) **dokunulmaz** (BL-440 tetiklenmez).

## 6. Protected Paths

- `.antigravity/**`
- `services/Diten.InventoryService/**`, `services/Diten.SupplyChainService/**`, `services/Diten.MdmService/**`,
  `services/Diten.AuthService/**` ve diğer domain servisleri
- `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `trace.openapi.yaml`, `product-master-bundle.openapi.yaml`,
  `location.openapi.yaml` (CONSUMED — K16)
- `gateway/Diten.ApiGateway/**/ocelot.json` (değişiklik gerekmiyor)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`
- `execution/registries/**` (CT adımı)

## 7. Dependencies

- **INVENTORY-BUNDLE 1.8.0 FROZEN** (`inventory-bundle.openapi.yaml:9-10`): `POST /api/inventory/movements`
  (`InventoryController.cs:19,27-28`, `[HasPermission("inventory.movements.create")]`; `InventoryPermissions.cs:14`). Yanıtlar
  201 yeni / 200 replay / 400 / 403 / 422 / 503 (`inventory-bundle.openapi.yaml:97-137`). Doğrulama okumaları `GET balance`
  (`InventoryController.cs:92-93`), `GET transactions` (`:109-110`), izin `inventory.stock.read` (`InventoryPermissions.cs:12`).
- **TRACE-BUNDLE 1.4.0 FROZEN** (`trace.openapi.yaml:8-9`): `POST /api/trace/lots` (`TraceController.cs:28,61-67`,
  `trace.lots.create` — `TracePermissions.cs:12`); 201 / 200 replay / 400 / 409 `LOT_ALREADY_EXISTS` / 422 / 503
  (`trace.openapi.yaml:80-104`). 409 **lotId döndürmez** (`CreateLotHandler.cs:216-233`) → mevcut lotu bulmak için
  `GET /api/trace/lots?skuId&skuLevel&q` (`TraceController.cs:43-59`, `trace.lots.read` — `TracePermissions.cs:10`).
  Yeni lot `Draft` doğar ve `Draft` stok alabilir (`LotStatusRules.cs:31`).
- **LOCATION 1.1.0 FROZEN (r2, S-5)** (`location.openapi.yaml:8-11`, sahip MOD-0173-FU02, Inventory 5067): API doğrulaması
  `GET /api/locations/{id}` (getLocation, `LocationsController.cs:35-38`) ve `GET /api/locations/{id}/path` (getLocationPath,
  `:49-52`); Web seçicisi `GET /api/locations/search` (searchLocations, `:110-111`; `type`, `status`, `parentId`, `search`,
  `pageSize ≤ 200` — `location.openapi.yaml:105-125`). Hepsi `inventory.locations.read` (`InventoryPermissions.cs:20`).
  INVENTORY giriş hareketi aynı kuralı ayrıca uygular: konum var, `Active`, `isStockable`, çağıranın LE'si ve yolu `toWarehouseId`
  deposunu içerir; değilse 422 `UNKNOWN_LOCATION` (`MovementMasterDataValidator.cs:308-339`).
- **Gateway:** `/api/inventory/{everything}` (`ocelot.json:907`, POST var), `/api/trace/{everything}` (`:978`, POST var),
  `/api/locations` ve `/api/locations/{everything}` (`:924`, `:942`, GET var; r2).
- **Emsal:** `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Shipments/InventoryMovementClient.cs:10-28`
  (kullanıcı token'ı + `Idempotency-Key` + `X-Legal-Entity-Id` + `X-Correlation-Id`, Gateway), sonuç sınıflaması `:36-55`;
  kayıt `Diten.SupplyChainService.Api/Program.cs:94-97`; ayar `appsettings.Development.json:11-13`
  (`Inventory:ReferenceBaseUrl = http://127.0.0.1:5000/`).
- **Merkezi denetim:** Procurement'ta yol `b` kabul edilmiş (`tests/architecture/audit-ledger/Diten.ProcurementService.md:12`;
  `Program.cs:39` `AddCentralAudit<ProcurementCentralAuditScope>(…, RequireEnlistment = true)`). Engel yok.
- **Rol yapılandırması (kod değil):** GRN kullanıcısının kiracı rolünde Bölüm 14'teki izinler (C-5).
- **Çalışan filo (yalnız DL-4):** Gateway 5000, Procurement 5065, Inventory 5067, Auth, MDM (INVENTORY ürün/konum/LE okur).

## 8. Runtime Constraints

### 8.1 Anahtar ve ayarlar

- `Inventory:Mode` = `Http` | `Mock`. Yok → `Http`. Tanınmayan değer → açılış durur (MOD-0140 `Suppliers:Model` deseni,
  `Persistence/DependencyInjection.cs:44-46`). `Mock` yalnız `IHostEnvironment.EnvironmentName == "Testing"` iken kabul edilir
  (test fabrikası `ProcurementApiFactory.cs:50` bunu kurar); başka ortamda `Mock` → açılış durur. Mode INVENTORY, TRACE ve
  LOCATION istemcilerini birlikte seçer (LOCATION: r2).
- `Inventory:ReferenceBaseUrl`: `appsettings.json` `http://localhost:5000/`, `appsettings.Development.json`
  `http://127.0.0.1:5000/` (Gateway; servis portu 5067'ye doğrudan gidilmez). `Http` modunda mutlak http/https değilse açılış
  durur (emsal çağrı anında 503'e düşüyor; burada erken hata seçildi — S-9; r4: DL-1 kapısında onay satırıyla kabul edilecek
  öneri, Bölüm 22). Ölçüt AC-30.
- `Inventory:RequestTimeoutSeconds` (varsayılan 10; emsal `Program.cs:94`). Tek `AddHttpClient` kaydı iki istemci için ayrı
  tipli istemci.
- **(r3)** `Grn:PostingLeaseSeconds` (kira süresi, varsayılan 60; `3 × Inventory:RequestTimeoutSeconds`'tan küçükse açılış durur —
  F-3) ve `Grn:MaxPostingAttempts` (varsayılan 5; 1–20 dışı değer açılışı durdurur — F-11). **r4 (F-6@bdea9ecf):** 3 kat kuralı tek
  çağrıyı kapsar ve kira her yukarı akış çağrısından hemen önce yenilendiği için (Bölüm 8.2 adım 2b) her çağrıyı kapsar: yenileme
  anında kalan süre `PostingLeaseSeconds`, çağrının üst sınırı `RequestTimeoutSeconds` (tipli istemcinin `HttpClient.Timeout`'u;
  istemci içinde yeniden deneme yok), aradaki pay (varsayılanlarla 50 sn) örnekler arası saat kayması içindir. Açılış doğrulamalarının
  üçü (ReferenceBaseUrl, kira / zaman aşımı oranı, deneme sınırı aralığı) AC-30'da ölçülür (F-8@bdea9ecf).
- Başlıklar (her çağrı): `Authorization` = gelen isteğin token'ı aynen; `Idempotency-Key` = gövdedeki `idempotencyKey`;
  `X-Legal-Entity-Id` = `ITenantContext.LegalEntityId` (GRN'in LE'si); `X-Correlation-Id` = gelen istekte UUID ise o, yoksa
  GRN başına bir kez üretilen UUID (bütün satırlarda aynı). `X-Tenant-Id` gönderilmez (emsal F-8).

### 8.2 Kayıt durum makinesi (recordGrn)

1. **Ön koşul (çağrı yok):** `Idempotency-Key` zorunlu (sözleşme `grn-event.openapi.yaml:76` zaten zorunlu yazıyor; kod bugün
   zorlamıyor) → yoksa 422 `VALIDATION_FAILED`. `locationId` boşsa 422 `VALIDATION_FAILED` (B-4; S-5 kararı). Mevcut doğrulamalar
   (`RecordGrnHandler.cs:57-95`) aynı.
2. **Replay / devam:** anahtarla kayıt var mı? Arama Tenant + LE kapsamlıdır (`GrnRepository.cs:124-129`; başka LE'nin ya da
   kiracının Draft'ı görünmez — r3, F-10). **r4 (F-2@bdea9ecf):** bu arama soft delete edilmiş kayıtları da görür
   (`GetByIdempotencyKeyIncludingDeletedAsync`; süzgeç Tenant + LE + anahtar, `IsDeleted` süzgeci yok); diğer bütün okumalar
   `TenantFilter`'ı (`IsDeleted = false`) korur. **Maddeler yazıldığı sırayla denenir, ilk tutan uygulanır (r4, F-5@bdea9ecf):**
   - (i) kayıt silinmiş (`IsDeleted = true`) → 409 `IDEMPOTENCY_KEY_REUSED` (ileti: anahtar silinmiş bir mal kabule ait; yeni kod
     yok), çağrı yok, kayıt değişmez (r4, F-2@bdea9ecf — gerekçe Silme maddesinde).
   - (ii) kayıtta `RequestFingerprint` yok (FU01 öncesi yazılmış kayıt; geriye doldurma yok): `Completed` → mevcut GRN 201 (bugünkü
     davranış, `RecordGrnHandler.cs:48-55`; karşılaştırılacak parmak izi olmadığından gövde ve kullanıcı denetlenemez — kabul edilen
     sınır: FU01 öncesi `Posted` kayıtlar Mock INVENTORY ile yazıldı, gerçek stok etkileri yok, anahtarları Web'in ürettiği GUID'ler);
     diğer her durum → 409 `IDEMPOTENCY_KEY_REUSED`, çağrı yok (gövde doğrulanamaz, kapalı-başarısız; `NotStarted` Draft silinebilir).
     (r4, F-5@bdea9ecf)
   - (iii) parmak izi farklı (gövde ya da kullanıcı; Bölüm 4) → 409 `IDEMPOTENCY_KEY_REUSED`, çağrı yok — **durumdan bağımsız,
     `Completed` dahil** (r4, F-5@bdea9ecf). Bugünkü kod `Completed` kayıtta gövdeye bakmadan 201 döndürüyor
     (`RecordGrnHandler.cs:48-55`); FU01 bunu değiştirir (AC-7, S15).
   - (iv) `Completed` → mevcut GRN 201; LOCATION dahil hiçbir yukarı akış çağrılmaz, kira alınmaz.
   - (v) `ManuallyResolved` → 409 `INVALID_STATE`, çağrı yok (r3, F-2).
   - (vi) `InProgress` / `Uncertain` → 2b, sonra aynı `GrnId`, `ReceivedAt`, `PostingAttempt` ile 3'ten devam.
   - (vii) `CompensationPending` / `CompensationBlocked` → 2b, önce telafiyi bitir (5), sonra 2a ve `PostingAttempt+1` ile 3.
   - (viii) `Compensated` → `PostingAttempt = Grn:MaxPostingAttempts` ise 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED`, çağrı yok (r3,
     F-11); değilse 2b, 2a, sonra `PostingAttempt+1` ile 3 (API istemcisi ana veriyi düzeltip aynı anahtarla yeniden gönderebilir;
     Web kesin retten sonra yeni anahtar üretir — Bölüm 11, F-1@bdea9ecf).
   - (ix) yok → 2a, sonra Draft yaz (`Status=Draft`, `PostingState=InProgress`, `PostingAttempt=1`, `GrnId`, `ReceivedAt`, parmak
     izi, kira alanları — Draft'ı yazan istek kirayı tutar). Benzersiz index çakışması (eşzamanlı aynı anahtar ya da silinmiş kaydın
     anahtarı) → kaydı silinmişler dahil **bir kez** yeniden oku ve (i)'den uygula; silinmiş kayıt (i)'ye düşer, döngü ve 500 olmaz
     (r4, F-2@bdea9ecf). İkinci okumada da kayıt yoksa (tenant + LE kapsamlı index ile beklenmez) 503 `GRN_POSTING_UNCERTAIN` + hata
     günlüğü; yeniden okuma tekrarlanmaz.
2a. **LOCATION doğrulaması (r2, S-5 kararı — stok ve lot etkisi yok):** yeni kayıtta ve her yeni denemeden (`PostingAttempt+1`)
   önce, Draft yazılmadan ve TRACE / INVENTORY çağrılmadan; `InProgress` / `Uncertain` devamında tekrarlanmaz (gövde aynı, parmak
   izi denetli; INVENTORY her harekette aynı kuralı ayrıca uygular). `ILocationReferenceClient` Gateway üzerinden, §8.1 başlıklarıyla
   (`Idempotency-Key` hariç) `GET api/locations/{locationId}` ve `GET api/locations/{locationId}/path` çağırır. Geçerli: 200,
   `status = Active`, `isStockable = true`, `legalEntityId` = GRN'in LE'si ve yolda `type = Warehouse`, `id = WarehouseId` öğesi
   (INVENTORY kuralının aynısı, `MovementMasterDataValidator.cs:308-339`). Sonuçlar: kural dışı ya da 404 → 422 `UNKNOWN_LOCATION`
   (INVENTORY ile aynı kod; yeni kod yok); 401 → 401; 403 → 403 `INVENTORY_FORBIDDEN`; zarflı 503, zaman aşımı, bağlantı hatası,
   5xx ya da okunamayan 2xx → 503 `LOCATION_MASTER_UNAVAILABLE`. Hata yollarında yeni kayıtta Draft yazılmaz; mevcut Draft'ın durumu ve
   `PostingAttempt`'i değişmez.
   **Politika farkı (r3, F-7):** INVENTORY konumun LE eşleşmesini `Inventory:Policy:EnforceLocationLegalEntityMatch` ayarına bağlı
   uygular (`MovementMasterDataValidator.cs:327`); ayar varsayılan `true`'dur ve `false` yalnız INVENTORY'nin `Mock` ortamında kabul
   edilir (`InventoryPolicyOptions.cs:16`;
   `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/DependencyInjection.cs:36-39`;
   `services/Diten.InventoryService/src/Diten.InventoryService.Api/appsettings.Mock.json:20`). GRN doğrulaması LE eşleşmesini
   **koşulsuz** uygular (GRN, INVENTORY'nin ayarını okuyamaz; fail-closed). Fark yalnız INVENTORY `Mock` ortamında görünür: orada
   başka LE'nin konumu GRN'de 422 `UNKNOWN_LOCATION` alır, INVENTORY'ye doğrudan gönderilen hareket ise kabul edilirdi; geliştirme ve
   üretim ortamında iki kural aynıdır. Bu doğrulamanın kendisi sahip onayı bekleyen bir türetmedir (O-R3-1).
2b. **Kira (claim) — aynı GRN'de tek işleyen (r3, F-3):** devam, telafi ve yeni deneme kayıt üzerinde atomik koşullu güncellemeyle
   kira alır: süzgeç `Id` + `Version = v` + (`ClaimExpiresAt` boş **ya da** geçmiş), yazılan `ClaimToken` = yeni UUID,
   `ClaimExpiresAt = şimdi + Grn:PostingLeaseSeconds`, `Version = v+1` (`GrnRepository.cs:77-90`'daki sürüm deseninin koşullu
   güncelleme hâli). Kira alınamazsa (başka istek tutuyor, süresi dolmamış) → 409 `GRN_POSTING_IN_PROGRESS`; yukarı akış çağrısı
   yok, kayıt değişmez. Sonraki her durum yazımı `Version` + `ClaimToken` koşulludur ve kirayı yeniler; koşul tutmazsa (kira süresi
   dolmuş, başka istek devralmış) istek **yeni yukarı akış çağrısı yapmadan** durur ve 503 `GRN_POSTING_UNCERTAIN` döner — devralan
   istek aynı anahtarlarla sürdürür. Akış biterken (Completed, Compensated, Uncertain, CompensationPending, CompensationBlocked)
   kira son yazımda boşaltılır. Aynı kira `ReverseGrn`'de (DL-2), silmede (kirası süren Draft → 409 `GRN_POSTING_IN_PROGRESS`),
   `RetryGrnPostingCommand`'da (r4, 2c) ve **`ResolveGrnCompensationCommand`'da** (r4, F-4@bdea9ecf) geçerlidir: çözme yazımı
   `Version` + `PostingState = CompensationBlocked` + (`ClaimExpiresAt` boş **ya da** geçmiş) koşulludur; kirası süren kayıtta (ör.
   aynı anahtarla telafi yeniden denemesi sürüyor) → 409 `GRN_POSTING_IN_PROGRESS`, kayıt değişmez. Böylece devam eden telafi
   tamamlanıp stoğu sıfırlanan bir kayıt `ManuallyResolved` olarak işaretlenemez; telafi bittiğinde kayıt `Compensated`'a geçer ve
   çözme 409 `INVALID_STATE` alır. Kira alınamadığında kayıt silinmişler dahil yeniden okunur: silinmişse (eşzamanlı silme) 409
   `IDEMPOTENCY_KEY_REUSED` (madde (i)), değilse 409 `GRN_POSTING_IN_PROGRESS`.
   **Kira yenileme (r4, F-6@bdea9ecf):** kirayı tutan istek **her yukarı akış çağrısından hemen önce** (mevcut kayıtta 2a'nın iki
   LOCATION okuması — yeni kayıtta 2a Draft'tan önce kirasız koşar, stok ve lot etkisi yoktur —, adım 3'ün her `createLot`'u ve her `listLots` sayfası, adım 4'ün her hareketi, adım 5'in her telafi REVERSAL'ı) kirayı
   `Version` + `ClaimToken` koşullu yazımla yeniler (`ClaimExpiresAt = şimdi + Grn:PostingLeaseSeconds`). Yenileme tutmazsa çağrı
   yapılmaz, 503 `GRN_POSTING_UNCERTAIN`. Çağrı `RequestTimeoutSeconds` ile sınırlı olduğundan (8.1) bir çağrı hiçbir zaman süresi
   dolmuş kirayla başlamaz ya da sürmez; "eski sahip yeni çağrı yapmadan durur" iddiası lot aşaması dahil her aşamada tutar. Bedeli
   yukarı akış çağrısı başına bir koşullu Mongo yazımıdır. **İki 409'un ayrımı:** INVENTORY `postMovement` sözleşmesinde 409 yoktur (201 / 200 / 400 / 403 / 422 / 503,
   `inventory-bundle.openapi.yaml:98-137`); aynı anahtarla eşzamanlı iki çağrı INVENTORY içinde 200 replay'e çözülür
   (`InternalMovementPostingService.cs:249-255`). Bu yüzden INVENTORY'den gelen 409 bildirilmemiş yanıttır → belirsiz (adım 6,
   `Uncertain`); "işlem sürüyor" 409'u yalnız GRN'in kendi kirasından gelir.
2c. **Kayıttan yeniden deneme — `RetryGrnPostingCommand` (r4, F-3@bdea9ecf):** `POST /api/grn/{grnId}/posting/retry`,
   `procurement.grn.create`, gövde yok. Kayıt `GrnId` ile Tenant + LE + `IsDeleted = false` kapsamında bulunur (yoksa 404). Durum
   `Uncertain` / `InProgress` → adım 2 madde (vi); `CompensationPending` / `CompensationBlocked` → madde (vii); diğer her durum
   (`NotStarted`, `Compensated`, `Completed`, `ManuallyResolved`) → 409 `INVALID_STATE`, çağrı yok. Gövde **kayıttır** (depo, konum,
   satırlar, PO); yukarı akış anahtarları kaydın `IdempotencyKey`'inden adım 3, 4, 5'in kalıplarıyla türetilir, tarihler kayıttaki
   `ReceivedAt` / `CompensationStartedAt`'tir — form POST'unun göndereceği yukarı akış isteklerinin birebir aynısı, INVENTORY ve TRACE
   replay döndürür. Parmak izi denetimi yapılmaz; kira (2b), kira yenileme, deneme sınırı, 2a ve hata eşlemesi (Bölüm 13) aynıdır.
   Çağıran, aynı LE'de `procurement.grn.create` sahibi herhangi bir kullanıcı olabilir (kaydı başlatan kişi ayrılmış olabilir);
   yukarı akış çağrıları onun token'ıyla gider. Bu güvenlidir: INVENTORY ve TRACE parmak izleri aktörü içermez
   (`RequestFingerprint.cs:18-44`; `CreateLotHandler.cs:87-92`), aynı anahtarla başka kullanıcının isteği replay alır. Başarı 200
   (Posted `GrnResponse`); diğer yanıtlar adım 2'deki kodlardır. **Neden form üzerinden değil:** GRN parmak izi kullanıcıyı içerir
   (Bölüm 4) — başka kullanıcı formdan aynı anahtarla 409 alır; gövdeyi Web'de kayıttan yeniden kurmak parmak izinin her alanının
   kayıtta aynen saklanmasına dayanırdı. Ayrı uç ikisini de gerektirmez.
3. **Lotlar (stok etkisi yok; r3, F-6):** `lotNumber` TRACE kuralıyla normalize edilir (trim + büyük harf,
   `LotIdentityPolicy.cs:33`; TRACE de aynısını uygular, `CreateLotHandler.cs:66`) ve gruplama normalize değerle yapılır: dolu her
   ayrı (skuId, skuLevel, normalize lotNumber) için TRACE `createLot`; anahtar `{key}#lot#{n}` (n = ilk görülme sırası; denemeden
   bağımsız → replay 200 aynı lot). Gövde: `idempotencyKey, itemId, lotNumber, skuId, skuLevel`; diğer alanlar gönderilmez (S-6).
   409 `LOT_ALREADY_EXISTS` (lotId döndürmez) → çözüm: `GET /api/trace/lots?skuId=&skuLevel=&q=<normalize lotNumber>&page=p&pageSize=200`
   (p = 1, 2, …; `total` kadar, en çok 5 sayfa). `q` içerir-arama ve harf duyarsızdır, `manufacturerLot` / `supplierLot` alanlarını
   da eşler (`LotRepository.cs:90-93`; sözleşme `trace.openapi.yaml:67`); sayfasız çağrı bütün listeyi döndürür
   (`LotRepository.cs:117-125`), bu yüzden sayfalı okunur. Sayılan kayıt: `lotNumber` normalize değere **ordinal birebir** eşit
   **ve** `skuId`, `skuLevel` istekteki ile aynı. Tam bir kayıt → onun `lotId`'si; sıfır, birden çok (iş anahtarı kiracı genelinde
   benzersiz olduğundan beklenmez, `TraceIndexConfiguration.cs:38-39`) ya da 5 sayfada bitmeyen sonuç → 422 `LOT_RESOLUTION_FAILED`
   + hata günlüğü, hareket yok. Lot kiracı genelidir; başka şirketin açtığı lot da bulunur ve kullanılır (`Lot.cs:8-9`; S-4
   önerisi). Lot `Blocked` / `Expired` ise INVENTORY hareketi kendi kuralıyla reddeder (adım 5).
4. **Hareketler:** her satır için `POST /api/inventory/movements`, anahtar `{key}#a{PostingAttempt}#{i}`. Gövde eşlemesi Bölüm 19.1.
   Başarılı satırın `InventoryTransactionId`, `LotId`, `PostedAttempt` değeri satır satır kalıcı yazılır (sürüm + kira koşullu
   güncelleme, 2b). INVENTORY 201'inden sonra, satır yazımından önce kesinti ya da sürüm çakışması → devamda aynı anahtar, INVENTORY
   200 replay aynı `transactionId`'yi döndürür (r3, F-9; AC-28). Hepsi başarılı → tek Mongo işleminde `Status=Posted`,
   `PostingState=Completed`, `GoodsReceived` kayıtları ve merkezi denetim outbox satırı (Bölüm 21), 201.
5. **Kesin ret (422 / 400 / 403) satır k'da — S-2 kararı: A, hemen geri alma (Beste 2026-11-25); r3: F-1, F-2:**
   a. **Önce** tek sürüm + kira koşullu yazım: `PostingState=CompensationPending` ve bu denemede post edilmiş (`PostedAttempt = n`)
      her satırın `CompensationStartedAt`'i — **yalnız boşsa** — şimdiki zaman (UTC, ms'e kırpılmış). Sonraki hiçbir yazım bu alanı
      değiştirmez.
   b. Telafisi bitmemiş her satır için REVERSAL: anahtar `{key}#a{n}#{i}#comp`, zarf Bölüm 19.2, `transactionDate = postingDate =
      CompensationStartedAt`. Yeniden denemede (çökme, 503, `CompensationBlocked` sonrası) anahtar ve tarih aynıdır → INVENTORY
      parmak izi aynı (`RequestFingerprint.cs:20,42-43`), replay 200; 400 `IDEMPOTENCY_KEY_REUSED` oluşmaz.
   c. 201 / 200 replay → id `CompensationTransactionId`'ye; 422 `ALREADY_REVERSED` (`InventoryErrorCodes.cs:35`) → başarı,
      `CompensatedExternally = true` (id yok).
   d. 503 zarflı, zaman aşımı, bağlantı, 5xx, zarfsız ya da okunamayan yanıt → dur; `CompensationPending` kalır; 503
      `GRN_POSTING_UNCERTAIN`. 401 → dur; `CompensationPending`; 401.
   e. **Telafide kesin ret (r3, F-2)** — 422 (`ALREADY_REVERSED` dışı; ör. `REVERSAL_QUALITY_RECORD_LOCKED`,
      `InventoryErrorCodes.cs:101-107`; **r4, F-7@bdea9ecf:** `UNKNOWN_LOCATION` — kayıttan sonra konum pasifleştirilmiş ya da
      stoklanamaz yapılmışsa INVENTORY konumu REVERSAL dahil işlem içinde yeniden doğrular ve reddeder,
      `InternalMovementPostingService.cs:236-241`), her 400, her 403 (izin kaybı, `CROSS_LE_FORBIDDEN`): satıra `CompensationBlockedCode`
      (+ zarfta varsa `recordNumber` → `CompensationBlockedRecord`) yazılır, **kalan satırların telafisi sürer**. Sonunda engelli
      satır varsa: `PostingState=CompensationBlocked` (yalnız aşağıdaki iki yolla çıkılır), `CompensationBlockedAt` (bir kez),
      `ReceivingExceptions`'a `GRN_COMPENSATION_BLOCKED` + satır + yukarı akış kodu, hata günlüğü (8.3 Eskalasyon); GRN `Draft`;
      yanıt 409 `GRN_COMPENSATION_BLOCKED` (ayrıntı: ilk ret kodu ve satırı; engelli satırlar ve kodları). Engelli satırın stoğu
      INVENTORY'de kalır ve `sourceDocumentId = GrnId` ile izlenir. **Çıkış yolları:** (1) engel kalkınca (ör. kalite kararı geri
      alındı, izin yeniden verildi, konum yeniden etkinleştirildi — r4) aynı anahtarla yeniden gönderim ya da Details'ten
      `RetryGrnPostingCommand` (2c; r4) → adım 2 → yalnız telafisi bitmemiş satırlar, aynı anahtar ve tarihle; hepsi biterse
      `Compensated` ve kural gereği yeni deneme (2a yeniden doğrular; konum hâlâ pasifse 422 `UNKNOWN_LOCATION`, kayıt `Compensated`
      kalır); (2) engel kalkmayacaksa (ör. kalite kararı verildi)
      `procurement.grn.reverse` yetkili kişi `POST /api/grn/{grnId}/compensation/resolve` ile zorunlu notla "elle çözüldü"
      işaretler → `ManuallyResolved` (terminal; INVENTORY / TRACE çağrısı yok; hareket ve telafi alanları silinmez; kalan stok kalite
      / stok süreciyle ele alınır ve not bunu adlandırır). Tasarım ve izin O-R3-2.
   f. Hepsi telafi edildiyse `PostingState=Compensated`, `ReceivingExceptions`'a ilk ret kodu + satır numarası; yanıt Bölüm 13
      eşlemesine göre (422 + kod).
   Yeni deneme (`PostingAttempt+1`) başlarken aynı yazımda önceki denemenin satır alanları (`PostedAttempt`, `InventoryTransactionId`,
   `CompensationTransactionId`, `CompensatedExternally`, `CompensationStartedAt`, `CompensationBlockedCode`) `GrnLine.PostingHistory`'ye
   taşınır ve satırdan silinir; Posted GRN satırı yalnız son denemenin id'sini taşır (AC-8).
6. **Geçici / belirsiz (503 zarflı; zaman aşımı; bağlantı; 409; 500/502/504; zarfsız 503; okunamayan 2xx):** satır post
   edilmemiş **ya da belirsiz** → `PostingState=Uncertain`, GRN `Draft`, yanıt 503. Aynı anahtarla tekrar → 2'deki devam;
   INVENTORY replay'i ilk sonucu döndürür, mükerrer hareket olmaz (emsal `InventoryMovementClient.cs:43-51`).
- **Değişmez:** `Status=Posted` ⇔ bütün satırlar bu denemede post edildi. Posted olmayan GRN'in net stok etkisi ya sıfırdır
  (`Compensated`, `NotStarted`) ya da sıfır değildir / belirsizdir ve GRN `Draft` + `Uncertain` / `CompensationPending` /
  `CompensationBlocked` / `ManuallyResolved` olarak görünür; MOD-0143 yalnız Posted GRN'i eşler (`RunThreeWayMatchHandler.cs:73-74`).
- **Silme:** `DeleteGrn`/`BulkDeleteGrn` yalnız `PostingState ∈ {NotStarted, Compensated}` ve kirası olmayan Draft'ı siler; diğer
  Draft → 409 `INVALID_STATE` (B-9; mevcut kod, `DeleteGrnHandler.cs:33`), kirası süren Draft → 409 `GRN_POSTING_IN_PROGRESS`.
  `CompensationBlocked` ve `ManuallyResolved` silinmez: INVENTORY'de bu GRN'e bağlı stok hareketleri durur, kayıt onların
  belgesidir (r3, F-2). **Silinen kaydın anahtarı (r4, F-2@bdea9ecf):** soft delete edilmiş GRN'in `Idempotency-Key`'i yeniden
  kullanılamaz; aynı LE'de o anahtarla gelen kayıt isteği 409 `IDEMPOTENCY_KEY_REUSED` alır (adım 2 madde (i)). İki seçenekten en az
  değişiklikli olan seçildi: (a) index'e `IsDeleted = false` kısmi süzgeci eklemek mevcut `Sparse` index'in düşürülüp yeniden
  kurulmasını ister (MongoDB bir index'te `sparse` ile `partialFilterExpression`'ı birlikte kabul etmez, aynı adla farklı seçenek
  açılışta çakışma verir — çalışan her ortamda index göçü) ve silinen Draft'ın anahtarını yeni bir GRN'e açar; (b) seçilen kural
  index'e ve göçe dokunmaz, yalnız bir depo okuması (silinmişleri de gören arama) ve bir dal ekler. Kural kullanıcıyı engellemez:
  silinebilen Draft'lar `NotStarted` ve `Compensated`'dır, `Compensated` kesin retten doğar ve Web kesin retten sonra zaten yeni
  anahtar üretir (Bölüm 11); Create sayfası her açılışta yeni anahtar alır. Çakışmayı yalnız aynı anahtarı tekrar gönderen API
  istemcisi görür ve 409 ona doğru cevaptır.

### 8.3 Diğer

- Tenant + LE izolasyonu her sorguda (değişmez). Soft delete. Decimal string, float yok.
- INVENTORY tek satırlık uçtur; çok satırlı atomik post yok (`inventory-bundle.openapi.yaml` yalnız `/movements`) — telafi bu
  yüzden gerekli.
- **Tarihler (r3, F-1, F-4):** `ReceivedAt`, `CompensationStartedAt` ve `ReverseStartedAt` üretildikleri anda UTC'ye çevrilir ve
  tam milisaniyeye kırpılır; bir kez yazılır, yeniden üretilmez. Gerekçe: INVENTORY parmak izi tarihleri `UtcTicks` olarak katar
  (`RequestFingerprint.cs:42-43`); Procurement Mongo sürücüsü 2.27.0 (`Diten.ProcurementService.Persistence.csproj:10`) ve
  `DateTimeOffset` için kayıtlı serileştirici yok (yalnız Guid,
  `services/Diten.ProcurementService/src/Diten.ProcurementService.Persistence/DependencyInjection.cs:31`) — varsayılan temsilin tick
  korumasına güvenilmez, ölçülmedi. Kırpma + gerçek Mongo gidiş-dönüş testi (AC-27) ikisini de kapatır. Saat `TimeProvider` ile
  verilir (testte ilerletilebilir; kira süresi vakası).
- **Kiracı / LE (r3, F-10):** devam, telafi, çözme ve silme aramaları Tenant + LE kapsamlıdır (`GrnRepository.cs:124-129`);
  benzersiz anahtar index'i tenant + LE bazlıdır (`GrnIndexConfiguration.cs:31-35`) — başka LE'de aynı anahtar bağımsız yeni GRN'dir.
  Yukarı akış başlığı `X-Legal-Entity-Id` = çağıranın LE'si = bulunan GRN'in LE'si (arama zaten o LE ile süzülür; uyumsuzluk kurgu
  gereği oluşamaz, AC-25 bunu ölçer). INVENTORY ve TRACE oluşturma anahtarları da tenant + LE kapsamlıdır
  (`InternalMovementPostingService.cs:27-28`; `LotRepository.cs:74-78`); lot iş anahtarı ise kiracı genelidir (adım 3). Aynı LE'de
  başka kullanıcının aynı anahtarla isteği 409 `IDEMPOTENCY_KEY_REUSED` alır (kullanıcı parmak izinde, Bölüm 4).
- **Eskalasyon (r3, F-2; kanal O-R3-3):** `CompensationBlocked`'a geçişte (1) kaydeden kullanıcıya 409 `GRN_COMPENSATION_BLOCKED` ve
  7 dilde ileti (mal kabul kısmen stokta kaldı, ters kayıt yetkilisi çözmeli); (2) yapılandırılmış hata günlüğü satırı
  (`GRN compensation blocked` + TenantId, LE, GrnId, satır, kod; token ve anahtar değeri yok) — operasyon izlemesi bu satıra alarm
  kurar (C-8); (3) Details'te rozet, engelli satırlar ve çöz formu (DL-3, yalnız `procurement.grn.reverse`). Kişiye bildirim (Görev
  Merkezi iş öğesi ya da Platform kullanıcı bildirimi) bugün yapılamıyor: `origin/main`'de Platform dışındaki servis kaynaklarında
  görev / bildirim istemcisi yok (ölçüldü) ve Görev Merkezi köprüsünün bağlanma tarifi henüz yazılmadı
  (`.antigravity/rules/workcenter-bridge-standard.md`, WC-D1 yalnız yasak listesidir). Paket önerisi: bildirim FU02 (O-R3-3).
- **Sahipsiz kayıtlar (r3, F-11):** `Compensated` ve `NotStarted` Draft kullanıcı tarafından silinir (mevcut uç); otomatik temizlik
  yok (FU02 adayı). **r4 (F-3@bdea9ecf):** `Uncertain`, `InProgress` ve `CompensationPending` silinemez ve çözme komutu onları
  çözmez; çıkışları form sayfasına bağlı değildir — GRN listede `Draft` görünür, Details sayfası durumu rozetle gösterir ve
  `procurement.grn.create` sahibine "Kaydı yeniden dene" düğmesini çizer (2c, AC-31, AC-32). Kaydı başlatan kişinin sayfayı
  kapatması, oturumunun düşmesi (401) ya da ayrılması kaydı sahipsiz bırakmaz. **Ertelenen ve maliyeti:** kendiliğinden yeniden deneme
  (arka plan süpürücüsü) ve takılı kayıt için kişiye bildirim FU02'dedir; FU02'ye kadar yarıda kalmış bir kayıt ancak biri Details'ten
  yeniden denediğinde kapanır, o zamana dek kısmen post edilmiş stok INVENTORY bakiyesinde görünür ve GRN eşlemeye (MOD-0143)
  girmez — bunu yakalayan tek şey listedeki `Draft` kayıtlar ve hata günlüğü alarmıdır (C-8; `GRN_POSTING_UNCERTAIN` satırları
  da alarma eklenir). Kesin retle biten denemenin açtığı TRACE lotu `Draft` ve stoksuz kalır; TRACE lotu hiç silinmez (`Lot.cs:11-12`)
  ve aynı SKU + lot numarasının sonraki mal kabulünde adım 3 çözümüyle yeniden kullanılır — kabul edilir. Aynı anahtarla yeniden
  deneme `Grn:MaxPostingAttempts` ile sınırlıdır (her deneme INVENTORY defterinde bir post + REVERSAL çifti bırakır).

## 9. Layout & Shell Contract

- `shell: tenant` → DL-3'te dokunulan `Views/Procurement/Grn/Create.cshtml` ve `_Form.cshtml`'in bağlı olduğu sayfalarda
  `Layout = "_LayoutTenantShell"` **açıkça** yazılı kalır (ana paket `MOD-0142-receiving-grn.md:100`). Yeni sayfa yok.

## 10. Backend File Convention

Golden Reference **Compact** yapısı korunur (ana paket §10): `Features/Grn/{Commands,Queries,Handlers/CommandHandlers,
Handlers/QueryHandlers,Validators}/`, `GrnModels.cs`. Ek: `Features/Grn/Services/GrnInventoryPostingService.cs` (dış çağrı
orkestrasyonu handler'da değil — handler-design "Dış servis HTTP çağrısı" satırı; emsal InventoryService
`Features/Inventory/Services/`). Arayüzler `Application/Common/`, HTTP uygulamaları `Infrastructure/Clients/` (handler-design
"Dış Servis Client Interface'leri"). Adlandırma: handler/validator sonekleri yok (`RecordGrnHandler`). Servisler arası iç tip
paylaşımı yok; INVENTORY/TRACE gövdeleri sözleşme alan adlarıyla JSON olarak yazılır.

## 11. Frontend File Contract

DL-3 yalnız mevcut Compact dosyalarına dokunur (Bölüm 5). `_CreateEditOffcanvas`, `_DetailsQuickView` yok (compact).
- Create formu gizli `IdempotencyKey` alanı taşır: GET'te bir kez üretilir. `GrnController.cs:313-319`'daki "her POST'ta yeni
  anahtar" kaldırılır. **Anahtar yalnız belirsiz sonuçta korunur (r4, F-1@bdea9ecf):**
  - **Korunur** (sonuç belirsiz ya da kayıt yarıda; aynı gövdeyle yeniden göndermek doğru devamdır): her 503 (`GRN_POSTING_UNCERTAIN`,
    `INVENTORY_UNAVAILABLE`, `LOCATION_MASTER_UNAVAILABLE` ve diğer geçirilen 503 kodları), 409 `GRN_POSTING_IN_PROGRESS`, 409
    `GRN_COMPENSATION_BLOCKED`. Form aynı anahtarla ve gönderilen gövdenin özetiyle (gizli `IdempotencyBodyHash` = SHA-256, Web'in
    API'ye gönderdiği gövdenin kanonik JSON'u, anahtar hariç) geri çizilir; ileti "kayıt tamamlanamadı; değiştirmeden yeniden kaydedin
    ya da kaydı Details'ten yeniden deneyin" der (7 dil).
  - **Yenilenir** (kesin ret; aynı anahtarla düzeltilmiş gövde API parmak izinde 409 `IDEMPOTENCY_KEY_REUSED` alırdı): her 422
    (`VALIDATION_FAILED`, `LOT_REQUIRED`, `ITEM_NOT_ACTIVE`, `UNKNOWN_LOCATION`, `LOT_NUMBER_INVALID`, `LOT_RESOLUTION_FAILED` ve
    geçirilen diğer kodlar), 400, 403, 409 `IDEMPOTENCY_KEY_REUSED`, 409 `INVALID_STATE`, 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED`. Form
    yeni anahtarla ve boş `IdempotencyBodyHash` ile geri çizilir. Kesin retle biten deneme API'de `Compensated` Draft bırakır (net
    stok 0; silinebilir — Bölüm 8.3 Sahipsiz kayıtlar).
  - **Gövde değişince yenilenir:** POST'ta `IdempotencyBodyHash` doluysa Web gönderilecek gövdenin özetini yeniden hesaplar; farklıysa
    API'ye göndermeden önce yeni anahtar üretir (kullanıcı belirsiz sonuçtan sonra formu değiştirdi → yeni mal kabul; önceki yarım
    kayıt listede `Draft` kalır ve Details'ten yeniden denenir). `IdempotencyBodyHash` boşsa (ilk gönderim ya da kesin ret sonrası)
    formdaki anahtar olduğu gibi kullanılır. Gizli alanın elle değiştirilmesi yalnız kullanıcının kendisine zarar verir: API parmak izi
    son savunmadır (409 `IDEMPOTENCY_KEY_REUSED`, ardından yeni anahtar).
  - Başarıda yeni form yeni anahtar alır. 401'de Web oturum açmaya yönlendirir ve form kaybolur; kayıt Details'ten yeniden denenir.
- **Depo ve konum listeden seçilir (r2, S-5 kararı):** bugünkü serbest metin alanları (`_Form.cshtml:50-62`; `LocationId` isteğe bağlı,
  `GrnViewModels.cs:19-23`) kaldırılır. Depo: LOCATION `type = Warehouse`, `status = Active` kayıtlarında aramalı seçici
  (`GET Grn/api/warehouses?search=` → Gateway `/api/locations/search`). Konum (zorunlu, `*`): seçilen deponun altında katman katman
  açılan liste (`GET Grn/api/locations?parentId=&search=`, `status = Active`); yalnız `isStockable = true` düğüm seçilebilir, diğerleri
  yalnız açılır. Depo değişince konum temizlenir. Seçici öğesi kod + ad gösterir, forma kimlik gider. Emsal: MOD-0183 sevkiyat
  seçicisi (`SupplyChainShipmentsController.cs:234-251`, `:698-710`; `wwwroot/assets/js/SupplyChain/Shipments/create.js`).
- Proxy eylemleri `procurement.grn.create` yoksa 403 döner (form zaten çizilmez); yukarı akış 403 (`inventory.locations.read` yok) ve
  503 / zaman aşımında seçici yerelleştirilmiş ileti gösterir ve kaydet kapalı kalır; elle metin girişi ya da sabit yedek liste yoktur.
  Tarayıcı servis portunu (5067) çağırmaz; arama ≤ 100 karakter, sayfa boyu sabit (emsal).
- Başarısız POST'ta (422 / 409 / 503) seçili depo ve konum kimlik + görünen kod / ad ile geri çizilir. Sunucu doğrulaması
  `LocationId`'yi zorunlu tutar (`[Required]`, yerelleştirilmiş ileti); API ayrıca LOCATION'da doğrular (Bölüm 8.2 adım 2a).
- Hata kodları resx anahtarlarına eşlenir (bugün ham metin, `GrnController.cs:295-311`): en az `INVENTORY_UNAVAILABLE`,
  `IDEMPOTENCY_KEY_REUSED`, `INVENTORY_FORBIDDEN`, `LOT_RESOLUTION_FAILED`, `GRN_POSTING_UNCERTAIN` ve geçirilen yukarı akış
  kodları için genel "Mal kabul stok kaydına işlenemedi: {kod}" metni. **7 dil** (en, tr, fr, es, zh, ar, ru); bir dil eksikse
  dilim bitmez.
- **Details durum rozeti ve çöz formu (r3, F-2; DL-3):** `Details.cshtml` + `details.js` `postingState`'i rozetle gösterir;
  `CompensationBlocked`'ta engelli satırlar + kodlar ve zorunlu notlu "Elle çözüldü olarak işaretle" formu yalnız
  `procurement.grn.reverse` iznine sahip kullanıcıya çizilir (izinsizde düğme yok, Web proxy'si 403). Yeni kodlar
  `GRN_COMPENSATION_BLOCKED`, `GRN_POSTING_IN_PROGRESS`, `GRN_POSTING_ATTEMPTS_EXHAUSTED` resx'e eşlenir; 409
  `GRN_POSTING_IN_PROGRESS`'te form anahtarı korur (birkaç saniye sonra yeniden kaydetme iletisi). 7 dil.
- **Details "Kaydı yeniden dene" (r4, F-3@bdea9ecf; DL-3):** `postingState ∈ {Uncertain, InProgress, CompensationPending,
  CompensationBlocked}` iken düğme yalnız `procurement.grn.create` iznine sahip kullanıcıya çizilir (izinsizde düğme yok, UAS-001);
  Web `GrnController`'daki proxy eylemi (`POST Grn/{grnId}/retry`, izin yoksa 403) Gateway üzerinden `POST /api/grn/{grnId}/posting/retry`
  çağırır — gövde ve anahtar göndermez (anahtar kayıtta). Sonuç iletisi yerelleştirilmiştir (200 → Posted, sayfa yenilenir; 409
  `GRN_POSTING_IN_PROGRESS` → "işlem sürüyor, birkaç saniye sonra deneyin"; 409 `GRN_COMPENSATION_BLOCKED` → engelli satırlar; 422
  → kod; 503 → yeniden deneme iletisi). `CompensationBlocked`'ta düğme çöz formunun yanında durur (önce yeniden dene, olmazsa çöz).
  7 dil.

## 12. Validation Rules

| Alan | Zorunlu | Kural | Ön kontrol |
|---|---|---|---|
| `Idempotency-Key` başlığı | Evet | boş değil (sözleşme `:76`) | çağrıdan önce 422 `VALIDATION_FAILED` |
| WarehouseId | Evet | (mevcut) + Web'de LOCATION `type = Warehouse`, `Active` listesinden seçilir (r2, S-5); API'de konumun yolu bu depoyu içermeli | LOCATION (2a) → 422 `UNKNOWN_LOCATION`; INVENTORY ayrıca |
| LocationId | **Evet (FU01 ile; S-5 kararı)** | boş değil; şirketin LOCATION kaydı: var, `Active`, `isStockable`, GRN'in LE'si, yolu `WarehouseId` deposunu içerir; Web'de listeden seçilir (serbest metin yok) | boşsa çağrıdan önce 422 `VALIDATION_FAILED`; kural dışıysa LOCATION doğrulamasında (2a) 422 `UNKNOWN_LOCATION` — TRACE / INVENTORY çağrısı ve Draft yazımı yok |
| Line.LotNumber | Hayır | doluysa TRACE `createLot`; SKU lotsuz → TRACE 422 `PRODUCT_NOT_LOT_CONTROLLED` geçirilir (sessizce düşürülmez); boşsa ve SKU lotlu → INVENTORY 422 `LOT_REQUIRED` geçirilir | TRACE / INVENTORY |
| Line.SkuLevel | Evet | (mevcut) Gsku/Lsku/FinishedGood; `Item` yok (S-7) | — |
| Aynı anahtar, farklı gövde | — | 409 `IDEMPOTENCY_KEY_REUSED` | parmak izi |
| Aynı anahtar, başka kullanıcı (r3, F-10) | — | 409 `IDEMPOTENCY_KEY_REUSED` | parmak izi (kullanıcı kimliği) |
| Çözüm notu (`ResolveGrnCompensation`, r3) | Evet | trim sonrası 1–500 karakter | 422 `VALIDATION_FAILED`, yazım yok |

## 13. Failure Path to Verify

Hata eşlemesi (recordGrn; `ReverseGrn` aynı tabloyu kullanır, telafi yerine "kısmi ters kayıt" kuralı Bölüm 19.2):

| Yukarı akış yanıtı | Sınıf | GRN yanıtı | GRN durumu |
|---|---|---|---|
| LOCATION (2a) 404 ya da kural dışı (pasif, stoklanamaz, başka LE, başka depo) — r2, S-5 | kesin ret, işlenmedi | 422 `UNKNOWN_LOCATION` | yeni kayıtta Draft yok; mevcut Draft değişmez |
| LOCATION (2a) 403 | kesin ret, işlenmedi | 403 `INVENTORY_FORBIDDEN` | aynı |
| LOCATION (2a) 503 zarflı, zaman aşımı, bağlantı, 5xx, okunamayan 2xx | geçici, işlenmedi | 503 `LOCATION_MASTER_UNAVAILABLE` | aynı |
| INVENTORY 201 / 200 (`transactionId` okunur) | başarı | — | satır post edildi |
| 2xx ama gövde okunamıyor / `transactionId` yok | belirsiz | 503 `GRN_POSTING_UNCERTAIN` | Draft, Uncertain |
| INVENTORY/TRACE 422 (her kod) | kesin ret | 422, kod **aynen** (ör. `LOT_REQUIRED`, `UNKNOWN_LOT`, `UNKNOWN_LOCATION`, `ITEM_NOT_ACTIVE`, `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`) | telafi → Draft, Compensated |
| INVENTORY/TRACE 400 (`INVALID_REQUEST`, `LOT_NUMBER_INVALID`, `LOT_DATES_INVALID`) | kesin ret | 422, kod aynen | telafi → Compensated |
| INVENTORY/TRACE 400 `IDEMPOTENCY_KEY_REUSED` | kesin ret (anahtar tasarımı bozuk) | 409 `IDEMPOTENCY_KEY_REUSED` + hata günlüğü | telafi → Compensated |
| 403 (izin yok ya da `CROSS_LE_FORBIDDEN`) | kesin ret | 403 `INVENTORY_FORBIDDEN` (zarfta `CROSS_LE_FORBIDDEN` varsa o) | telafi → Compensated |
| TRACE 409 `LOT_ALREADY_EXISTS` | çözülür | (adım 3 araması) | — |
| 401 | kesin, işlenmedi | 401 (token süresi) | Draft, Uncertain (yeniden giriş sonrası devam) |
| 503 + INVENTORY/TRACE zarfı (`INVENTORY_UNAVAILABLE`, `PRODUCT_MASTER_UNAVAILABLE`, `LOCATION_MASTER_UNAVAILABLE`, `LEGAL_ENTITY_MASTER_UNAVAILABLE`) | geçici, işlenmedi | 503, kod aynen | Draft, Uncertain |
| zaman aşımı, bağlantı hatası, 409 (INVENTORY), 500, 502, 504, zarfsız 503, 3xx | belirsiz | 503 `GRN_POSTING_UNCERTAIN` | Draft, Uncertain |
| INVENTORY 409 — r3, F-3 açıklaması | sözleşmede yok (`inventory-bundle.openapi.yaml:98-137`; aynı anahtar yarışı INVENTORY içinde 200 replay) → bildirilmemiş yanıt, belirsiz | 503 `GRN_POSTING_UNCERTAIN` | Draft, Uncertain |
| TRACE 409 (`LOT_ALREADY_EXISTS` dışı; `dateOverride` gönderilmediği için beklenmez) — r3 | belirsiz, stok etkisi yok (lot aşaması hareketlerden önce) | 503 `GRN_POSTING_UNCERTAIN` | Draft, Uncertain |
| GRN'in kendi kirası başka istekte — r3, F-3 | eşzamanlı, işlenmedi | 409 `GRN_POSTING_IN_PROGRESS` | değişmez |
| Telafi REVERSAL'ı 201 / 200 / 422 `ALREADY_REVERSED` — r3 | telafi başarılı | — | satır telafi edildi |
| Telafi REVERSAL'ı diğer 422 (ör. `REVERSAL_QUALITY_RECORD_LOCKED`; r4, F-7@bdea9ecf: kayıttan sonra pasifleşen konum için `UNKNOWN_LOCATION`), 400, 403 — r3, F-2 | telafide kesin ret | 409 `GRN_COMPENSATION_BLOCKED` | Draft, CompensationBlocked (net stok ≠ 0, görünür) |
| Telafi REVERSAL'ı 503 / zaman aşımı / belirsiz — r3 | telafi geçici | 503 `GRN_POSTING_UNCERTAIN` | Draft, CompensationPending |
| Telafi REVERSAL'ı 401 — r3 | işlenmedi | 401 | Draft, CompensationPending |
| `Compensated` + deneme sınırı — r3, F-11 | — | 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED` | değişmez |
| `ManuallyResolved` + aynı anahtar — r3 | — | 409 `INVALID_STATE` | değişmez |
| Silinmiş GRN'in anahtarıyla kayıt — r4, F-2@bdea9ecf | — | 409 `IDEMPOTENCY_KEY_REUSED` | silinmiş kayıt değişmez; yeni kayıt yok |
| `RequestFingerprint` olmayan (FU01 öncesi) kayıt + aynı anahtar — r4, F-5@bdea9ecf | — | `Completed` → 201 mevcut GRN; diğer durum → 409 `IDEMPOTENCY_KEY_REUSED` | değişmez |
| `Completed` + aynı anahtar, farklı gövde ya da başka kullanıcı — r4, F-5@bdea9ecf | — | 409 `IDEMPOTENCY_KEY_REUSED` | değişmez |
| Çözme (`compensation/resolve`) kirası süren kayıtta — r4, F-4@bdea9ecf | eşzamanlı, işlenmedi | 409 `GRN_POSTING_IN_PROGRESS` | değişmez (`CompensationBlocked`) |
| Yeniden deneme (`posting/retry`) `NotStarted` / `Compensated` / `Completed` / `ManuallyResolved` kayıtta — r4, F-3@bdea9ecf | — | 409 `INVALID_STATE` | değişmez |
| Yeniden deneme kirası süren kayıtta — r4 | eşzamanlı, işlenmedi | 409 `GRN_POSTING_IN_PROGRESS` | değişmez |
| Kira yenilemesi tutmadı (başka istek devraldı) — r4, F-6@bdea9ecf | işlenmedi (çağrı yapılmadı) | 503 `GRN_POSTING_UNCERTAIN` | devralanın yazdığı durum |

Ek senaryolar:
- **Replay** (aynı anahtar, Completed) → 201 aynı GRN, INVENTORY/TRACE çağrısı yok.
- **Devam** (aynı anahtar, Uncertain) → INVENTORY 200 replay'leri + kalan satırlar; tek hareket/satır.
- **Eşzamanlı aynı anahtar** → tek Draft (benzersiz index); devam / telafi aşamasında ikinci istek kira alamaz → 409
  `GRN_POSTING_IN_PROGRESS`, yukarı akış çağrısı yok (r3, F-3; AC-23); tek hareket/satır.
- **Yetkisiz GRN kullanıcısı** (`procurement.grn.create` yok) → 403, çağrı yok (mevcut).
- **Cross-tenant/LE okuma** → 404 (mevcut). **r3 (F-10):** devam / telafi / çözme / silme aramaları da tenant + LE kapsamlı;
  başka LE'de aynı anahtar bağımsız yeni GRN; aynı LE'de başka kullanıcı aynı anahtar → 409 `IDEMPOTENCY_KEY_REUSED` (AC-25).
- **Telafi sırasında süreç çökmesi** → `CompensationPending` kalır; sonraki deneme telafiyi aynı anahtar ve aynı
  `CompensationStartedAt` ile bitirir, ardından yeni deneme (AC-8, AC-20).
- **Telafide kalite kilidi (r3, F-2)** → `CompensationBlocked`, 409 `GRN_COMPENSATION_BLOCKED`; kilit kalkınca aynı anahtarla
  yeniden gönderim telafiyi bitirir; kalkmayacaksa yetkili kişi elle çözer, `ManuallyResolved` (AC-21, AC-22).
- **Telafi sırasında konum pasifleşmesi (r4, F-7@bdea9ecf)** — satır 1 post edildikten sonra konum pasifleştirilir, satır 2 kesin
  ret alır → telafi REVERSAL'ı 422 `UNKNOWN_LOCATION` → `CompensationBlocked`, 409; konum yeniden etkinleştirilince Details'ten
  yeniden deneme telafiyi bitirir; etkinleştirilmeyecekse elle çözüm (AC-21). Ters kayıtta (DL-2) aynı kod S-8 kuralına düşer: GRN
  `Posted` kalır, 422 `UNKNOWN_LOCATION` (AC-9).
- **Form kapandıktan sonra yarım kayıt (r4, F-3@bdea9ecf)** — 503 sonrası kullanıcı sayfayı kapatır; başka bir kullanıcı Details'ten
  "Kaydı yeniden dene" → aynı yukarı akış anahtarları, INVENTORY replay, satır başına tek hareket, `Posted` (AC-31, AC-32).
- **Listede olmayan konum (r2, S-5)** — Web seçicisi atlanıp API'ye bilinmeyen, pasif, stoklanamaz ya da başka depodaki konum
  gönderilir → 422 `UNKNOWN_LOCATION`, TRACE lot açılmaz, INVENTORY hareketi yok, Draft yok.

## 14. Authorization Convention

- GRN uçları: `procurement.grn.{read,create,reverse,delete,bulk-delete}` (ana paket §14). **Yeni uç (r3):**
  `POST /api/grn/{grnId}/compensation/resolve` → `[HasPermission("procurement.grn.reverse")]` — ters kayıt yetkilisi, stok etkili
  GRN durumunu kapatma yetkisinin de sahibidir; yeni izin anahtarı açılmaz (rol köprüsü, katalog ve 7 dilli izin etiketleri
  değişmez). Bu eşleme sahip onayı bekler (O-R3-2). **Yeni uç (r4, F-3@bdea9ecf):** `POST /api/grn/{grnId}/posting/retry` →
  `[HasPermission("procurement.grn.create")]` — kaydı yeniden denemek kaydetme eyleminin kendisidir (aynı yukarı akış istekleri, aynı
  stok etkisi), bu yüzden kaydetme izniyle eşlenir; yeni izin anahtarı, rol köprüsü ya da etiket değişikliği yok. Yeni bir sahip kararı
  değildir: kayıt eyleminin mevcut izninin aynı eyleme uygulanmasıdır ve onay satırı paketin tamamıyla birlikte onu da onaylar.
- **Kimlik yayılımı: kullanıcı token'ı (servis token'ı yok) — S-1 kararı: Evet (Beste, 2026-11-25, karar kaydı
  `docs/records/decisions/2026-11/mod-0142-fu01-owner-decisions-01.md`; "Sevkiyatta da aynı yöntem kullanılıyor").** Procurement →
  INVENTORY / TRACE / LOCATION çağrıları kullanıcının token'ıyla Gateway üzerinden yapılır (emsal SupplyChain `InventoryMovementClient`).
  GRN kullanıcısının kiracı rolünde ayrıca: `inventory.movements.create` (`InventoryPermissions.cs:14`), `trace.lots.create`
  (`TracePermissions.cs:12`), `trace.lots.read` (`TracePermissions.cs:10`; yalnız 409 çözümü için) — S-1 kararı;
  **`inventory.locations.read`** (`InventoryPermissions.cs:20`) — S-5 kararından **türetildi, sahip onayı bekliyor (O-R3-1; r3,
  F-7)**: Web seçicisi ve API'nin LOCATION doğrulaması aynı kullanıcı token'ıyla LOCATION okur. Doğrulama/E5 kullanıcısında `inventory.stock.read`. Rol şablonu / atama CT
  adımı C-5'tir (kod değil).
- **Sahip onayı bekleyen türetmeler (O-R3-1; r3, F-7):** karar kaydı S-5'te yalnız "zorunlu ve listeden seçilir" der. Paket bundan
  iki şey türetti: (1) API'nin konumu yukarı akış yazımından önce LOCATION'da doğrulaması (Bölüm 8.2 adım 2a, AC-18) ve (2) mal kabul
  rolüne `inventory.locations.read`. İkisi Beste'nin onayını bekler (onay satırı notunda açık kabul ya da ayrı cevap). (1)
  reddedilirse 2a ve AC-18 çıkar, konumu yalnız INVENTORY doğrular (422 lot açıldıktan sonra gelir); (2) reddedilirse Web seçici
  kullanıcı token'ıyla LOCATION okuyamaz ve S-5'in "listeden seçim" kararı bu FU'da uygulanamaz.
- **Kabul edilen risk (S-1) ve telafi önlemleri (r3, F-8):** `inventory.movements.create` hareket türüne bağlı değildir — GRN
  kullanıcısı INVENTORY'de her `movementType`'ı (düzeltme, hurda, sayım dahil) doğrudan post edebilir (`InventoryController.cs:27-28`;
  görev ayrılığı riski); sahip kullanıcı token'ını MVP-2 için seçti. Önlemler:
  - **Rol şablonu notu (C-5):** `inventory.movements.create`, `trace.lots.create`, `trace.lots.read` ve `inventory.locations.read`
    yalnız **mal kabul (GRN) rolüne** verilir; bu rol depo yöneticisi / stok düzeltme rolünden **ayrık** bir şablondur, iki rol aynı
    kullanıcıya varsayılan olarak birlikte atanmaz; birlikte atama gerekçesi rol atama kaydına yazılır.
  - **Servis kimliği geçişi (MOD-0033-FU02):** izinlerin kullanıcıdan alınıp servis kimliğine taşınması — sahip/tarih TBD (Ali —
    Auth). Bu FU'nun kapsamı dışında (Bölüm 20).
  - **İzleme önerisi:** INVENTORY her hareketi kullanıcı aktörüyle merkezi denetime yazar (`PostMovementCommand.cs:14-17`); mal kabul
    rolündeki kullanıcıların `GOODS_RECEIPT_PO` ve `REVERSAL` dışındaki hareketleri aylık gözden geçirilir. Rapor kodu bu FU'da yok
    (Follow-up); sahip/tarih TBD (Ali — Auth ile birlikte).
- Eksik izinde INVENTORY/TRACE/LOCATION 403 döner → GRN 403 `INVENTORY_FORBIDDEN`, stok etkisi yok (ilk çağrıda düşer; LOCATION
  doğrulaması ve lot aşaması hareketlerden önce olduğundan). UAS-001: Web, kullanıcının `procurement.grn.create`'i yoksa formu zaten çizmiyor (değişmez).
- Permission formatı PKS-001 (tenant servis, lowercase-dotted). Yeni izin anahtarı yok → katalog / rol köprüsü değişmez.

## 15. Gateway / API Routing Decision

- **Gateway değişikliği gerekmiyor.** `/api/inventory/{everything}` (`ocelot.json:907`, POST), `/api/trace/{everything}`
  (`:978`, POST) ve `/api/locations`, `/api/locations/{everything}` (`:924`, `:942`, GET; r2) mevcut; `/api/grn` mevcut (keşif K-3).
  Yeni çözme ucu (r3) `POST /api/grn/{grnId}/compensation/resolve` mevcut `/api/grn/{everything}` rotasından geçer
  (`ocelot.json:750`, POST var); yeniden deneme ucu (r4) `POST /api/grn/{grnId}/posting/retry` aynı rotadan geçer. Web seçicileri,
  çözme formu ve yeniden deneme düğmesi `GrnController` proxy'si üzerinden Gateway'e gider.
  `integration-agent` görevi yok.

### 15.1 GRN-EVENT 1.1.0 (additive, sahip onayı — S-3)

`recordGrn` yanıtlarına 403, 409, 503 eklenir; açıklamaya: geçirilen yukarı akış 422 kodları, `VALIDATION_FAILED` için
`Idempotency-Key` ve `locationId` zorunluluğu (çalışma kuralı — S-5 kararı; şema alanı `nullable` kalır), LOCATION doğrulaması
(422 `UNKNOWN_LOCATION`, 503 `LOCATION_MASTER_UNAVAILABLE`; r2), Draft devam semantiği; r3: 409 kodları `GRN_POSTING_IN_PROGRESS`,
`GRN_COMPENSATION_BLOCKED`, `GRN_POSTING_ATTEMPTS_EXHAUSTED`, `INVALID_STATE`. Yeni işlem (r3): `resolveGrnCompensation`
(`POST /grn/{grnId}/compensation/resolve`; 200 / 403 / 404 / 409 / 422; r4: 409 `GRN_POSTING_IN_PROGRESS` dahil). Yeni işlem (r4):
`retryGrnPosting` (`POST /grn/{grnId}/posting/retry`; gövde yok; 200 / 401 / 403 / 404 / 409 / 422 / 503; `recordGrn` ile aynı kodlar).
`recordGrn` açıklamasına (r4): silinmiş kaydın anahtarı ve `Completed` kayıtta farklı gövde 409 `IDEMPOTENCY_KEY_REUSED`. `GrnResponse`'a isteğe bağlı `postingState` alanı (r3;
NotStarted, InProgress, Uncertain, CompensationPending, CompensationBlocked, Compensated, Completed, ManuallyResolved).
`reverseGrn` (ASSUMPTION-GRN-02) için aynı yanıt ekleri. Mevcut yol, parametre, alan, tür, zorunlu liste ve enum değeri değişmez;
`contractVersion` `v1` kalır. Sözleşme dizinine satır (CT adımı C-3).
**S-3 yalnız bu sözleşme PR'ını (C-3) bekletir, DL-1 kodunu bekletmez (r3, F-5).** DL-1 kanıt dosyası sözleşmeye henüz yazılmamış
yanıt / alan / yolu "bilinen sözleşme farkı" olarak listeler; DL-4 (canlı kanıt) başlamadan C-3 main'de olur.

## 16. Acceptance Criteria

- [ ] AC-1 (zorunlu) — `Inventory:Mode` yok ya da `Http` iken DI `IInventoryPostingClient` → `HttpInventoryPostingClient`,
  `ITraceLotClient` → `HttpTraceLotClient`, `ILocationReferenceClient` → `HttpLocationReferenceClient` (r2) çözer; `Mock` yalnız `Testing` ortamında kabul edilir, `Development`/`Production`'da
  açılış hata ile durur; tanınmayan değer açılışı durdurur; `Persistence/DependencyInjection.cs`'de Mock kaydı kalmaz.
- [ ] AC-2 (zorunlu) — Her INVENTORY/TRACE/LOCATION çağrısı `{Inventory:ReferenceBaseUrl}api/inventory/movements` /
  `api/trace/lots` / `api/locations/{id}` ve `api/locations/{id}/path` (r2) adresine gider ve `Authorization` (gelen token aynen),
  `Idempotency-Key` (= gövde anahtarı; LOCATION okumalarında yok),
  `X-Legal-Entity-Id` (= GRN LE), `X-Correlation-Id` (GRN boyunca tek UUID) taşır; `X-Tenant-Id` taşımaz.
- [ ] AC-3 (zorunlu) — `GOODS_RECEIPT_PO` gövdesi `inventory-bundle.openapi.yaml` 1.8.0 `MovementRequest` şemasına (dosyadan
  okunan şema; kopya değil) uyar: `toWarehouseId`, `toLocationId`, `toStockStatus`, `lotId` (TRACE'ten), `documentGroupId=GrnId`,
  `sourceModule=MOD-0142`, `sourceType=GOODS_RECEIPT`, `sourceDocumentId=GrnId`, `transactionDate=postingDate=ReceivedAt`;
  `from*`, `lotNumber` yok.
- [ ] AC-4 (zorunlu; r3 F-6 ile genişledi) — Lotlu SKU satırında hareketten önce TRACE `createLot` çağrılır ve hareket dönen
  **gerçek** `lotId` ile post edilir; lot numarası TRACE kuralıyla normalize edilir (trim + büyük harf) ve gruplama normalize değerle
  yapılır (`l-1` ile `L-1` aynı lot; aynı (sku, seviye, lotNumber) için GRN başına tek `createLot`). 409 `LOT_ALREADY_EXISTS` →
  `listLots` sayfa sayfa (`pageSize=200`, en çok 5 sayfa) okunur; yalnız `lotNumber`'ı normalize değere **ordinal birebir** eşit ve
  `skuId`, `skuLevel`'i istekteki ile aynı kayıtlar sayılır; tam bir kayıt → onun id'si; sıfır, birden çok ya da 5 sayfada bitmeyen
  sonuç → 422 `LOT_RESOLUTION_FAILED`, hareket yok. Sahte TRACE vakası: `L1` için `L1`, `L10`, `XL1` ve `manufacturerLot = L1` olan
  dört kayıt → yalnız `L1`; birebir eşleşme ikinci sayfada olsa da bulunur; başka `skuLevel`'deki aynı numara sayılmaz.
- [ ] AC-5 (zorunlu; S-2 kararı A) — INVENTORY ya da TRACE 422 (ve 400) döndüğünde GRN `Posted` **olmaz**: yanıt 422 + yukarı akış kodu; bu
  denemede post edilmiş satırlar REVERSAL ile telafi edilir; sonunda GRN `Draft` + `Compensated`, net stok etkisi sıfır;
  `ReceivingExceptions` kod + satır numarası taşır. Telafinin kendisi reddedilirse AC-21 (r3).
- [ ] AC-6 (zorunlu) — INVENTORY 503 (zarflı) ya da belirsiz sonuçta (zaman aşımı, 502/504, zarfsız 503, 409) GRN `Posted`
  **olmaz**: yanıt 503, GRN `Draft` + `Uncertain`; aynı anahtarla yeniden gönderim aynı `GrnId`/`ReceivedAt` ile devam eder,
  her satır için INVENTORY'de **tek** hareket oluşur (INVENTORY 200 replay), sonuç `Posted`. Tarih kararlılığı gerçek Mongo ile
  AC-27'de, kısmi yazım AC-28'de (r3).
- [ ] AC-7 (zorunlu) — Idempotent tekrar: `Posted` GRN'i aynı anahtarla yeniden gönderme 201 + aynı `GrnId` döndürür, INVENTORY ve
  TRACE çağrılmaz; aynı anahtar + farklı gövde 409 `IDEMPOTENCY_KEY_REUSED`, çağrı yok. `Idempotency-Key` yoksa ya da
  `locationId` boşsa 422 `VALIDATION_FAILED`, çağrı yok. **r4 (F-5@bdea9ecf):** adım 2 maddeleri yazıldığı sırayla uygulanır —
  `Completed` GRN'e aynı anahtarla farklı gövde **ya da** başka kullanıcı → 409 `IDEMPOTENCY_KEY_REUSED` (201 değil); `RequestFingerprint`
  alanı olmayan (FU01 öncesi biçimde elle yazılmış) `Posted` kayda aynı anahtar → 201 mevcut GRN, `Draft` kayda → 409
  `IDEMPOTENCY_KEY_REUSED`; üç vakada da yukarı akış çağrı sayısı 0.
- [ ] AC-8 (zorunlu) — Telafi ortasında kesinti: `CompensationPending` kalıcıdır; sonraki aynı-anahtar isteği önce telafiyi
  aynı anahtar ve aynı `CompensationStartedAt` tarihleriyle tamamlar (replay ya da `ALREADY_REVERSED` başarı sayılır; AC-20), sonra
  `PostingAttempt+1` anahtarlarıyla yeniden dener; önceki denemenin satır alanları `PostingHistory`'ye taşınır; yeniden denemede ters
  çevrilmiş bir hareketin id'si Posted GRN satırına **yazılmaz**.
- [ ] AC-9 (zorunlu) — `ReverseGrn` REVERSAL'ı INVENTORY zarfıyla gönderir: `sourceModule=MOD-0173`, `sourceType=REVERSAL`,
  `sourceDocumentId=<satırın InventoryTransactionId>`, item/sku/seviye/miktar/birim orijinalle aynı, `from*`/`to*`/`lotId`/
  `serialIds` yok, tarihler `ReverseStartedAt`; satır satır `ReversalTransactionId` yazılır; hepsi bitince `Reversed`. Satır k'da
  422 (ör. `REVERSAL_QUALITY_RECORD_LOCKED`) → GRN `Posted` kalır, yanıt 422 + kod, ters çevrilmiş satırlar kayıtlı; aynı anahtarla
  tekrar kalan satırlardan devam eder (S-8). `ReverseStartedAt` ilk denemede bir kez yazılır (UTC, ms'e kırpılmış) ve kesinti
  sonrası devamda yeniden üretilmez (sahte INVENTORY 200 replay; S16); aynı GRN'e eşzamanlı ikinci ters kayıt isteği 409
  `GRN_POSTING_IN_PROGRESS` (AC-23 kirası; r3). Kayıttan sonra pasifleşen konum için satır k'da 422 `UNKNOWN_LOCATION` → GRN
  `Posted` kalır, yanıt 422 + kod (r4, F-7@bdea9ecf).
- [ ] AC-10 (zorunlu) — `DeleteGrn`/`BulkDeleteGrn` `PostingState ∈ {InProgress, Uncertain, CompensationPending,
  CompensationBlocked, ManuallyResolved}` Draft'ı 409 `INVALID_STATE` ile, kirası süren Draft'ı 409 `GRN_POSTING_IN_PROGRESS` ile
  reddeder; `NotStarted`/`Compensated` Draft silinir (r3: iki yeni durum ve kira).
- [ ] AC-11 (zorunlu) — `RecordGrnCommand`, `ReverseGrnCommand`, `DeleteGrnCommand`, `BulkDeleteGrnCommand`, (r3)
  `ResolveGrnCompensationCommand` ve (r4) `RetryGrnPostingCommand` `ICentralAuditDeclaration` taşır (yol `b`, `RequireEnlistment`; kategori `MasterData`, kaynak modül
  `procurement-grn`, nesne türü `GoodsReceipt`, işlemler Bölüm 21); `audit-ledger/Diten.ProcurementService.md` "Bilinen borç"tan dört
  GRN satırı çıkar, yeni komut borca girmez; `AuditTrailStandardTests` yeşil. Kayıt içeriği ve outbox atomikliği AC-24.
- [ ] AC-12 (zorunlu) — **E5 canlı (Gateway 5000, gerçek filo):** lotlu SKU ile `POST /api/grn` → 201 `Posted`; satırın `lotId`'si
  `GET /api/trace/lots/{lotId}` ile okunur (aynı lotNumber/SKU); `GET /api/inventory/transactions?lotId=` `GOODS_RECEIPT_PO`,
  `sourceDocumentId=GrnId` hareketini gösterir ve `transactionId` GRN satırındakiyle aynıdır; `GET /api/inventory/balance`
  (warehouse/location/lot/status) önceki değere göre tam GRN miktarı kadar artmıştır.
- [ ] AC-13 (zorunlu) — **E5 canlı idempotent tekrar:** AC-12 isteği aynı anahtarla yeniden → 201 aynı `GrnId`; `transactions` sayısı
  ve `balance` değişmez.
- [ ] AC-14 (zorunlu) — **E5 canlı ret ve kesinti:** (a) bilinmeyen konumlu GRN (API'ye doğrudan) → 422 `UNKNOWN_LOCATION`
  (LOCATION doğrulamasından, r2), GRN listesinde kayıt yok, `GET /api/trace/lots?q=<lotNumber>` yeni lot göstermez, `transactions` ve
  `balance` değişmez; (b) Inventory (5067) durdurulmuşken GRN → 503, GRN Draft; Inventory açıldıktan sonra aynı anahtarla
  → 201 Posted, satır başına tek hareket.
- [ ] AC-15 (zorunlu) — **E5 canlı ters kayıt:** AC-12 GRN'i `POST /api/grn/{grnId}/reverse` → 200 `Reversed`; `transactions`'ta
  `REVERSAL` (`sourceDocumentId` = orijinal `transactionId`) görünür, `balance` AC-12 öncesine döner.
- [ ] AC-16 (zorunlu; r4 F-1@bdea9ecf ile genişledi) — Web anahtar kuralı (Bölüm 11), sahte API handler'ıyla iki ardışık POST'un
  `Idempotency-Key` başlıkları karşılaştırılarak: (a) 503 (`GRN_POSTING_UNCERTAIN`, `INVENTORY_UNAVAILABLE`,
  `LOCATION_MASTER_UNAVAILABLE`), 409 `GRN_POSTING_IN_PROGRESS` ve 409 `GRN_COMPENSATION_BLOCKED` sonrası **aynı gövdeyle** tekrar
  kaydetme aynı anahtarı gönderir (yeni GRN açılmaz); (b) aynı belirsiz sonuçlardan sonra gövde değiştirilip kaydedilirse farklı
  anahtar gider; (c) 422 (`LOT_REQUIRED`, `ITEM_NOT_ACTIVE`, `UNKNOWN_LOCATION`, `LOT_NUMBER_INVALID`, `VALIDATION_FAILED`), 400, 403,
  409 `IDEMPOTENCY_KEY_REUSED`, 409 `INVALID_STATE` ve 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED` sonrası form yeni anahtar ve boş
  `IdempotencyBodyHash` ile geri çizilir ve düzeltilmiş gövde yeni anahtarla gider; (d) uçtan uca vaka: parmak izi denetleyen sahte
  API ile 422 `LOT_REQUIRED` → lot numarası eklenir → kaydet → 201 (409 `IDEMPOTENCY_KEY_REUSED` değil); (e) başarıdan sonra yeni
  form yeni anahtar. Yeni hata kodları 7 dilde yerelleştirilmiş görünür; `Create.cshtml` `Layout = "_LayoutTenantShell"` açık; resx 7-dil parite testi yeşil
  (bir dilde eksik anahtar testi kırmızı yapar — S27).
- [ ] AC-17 (zorunlu) — INVENTORY ve TRACE kodunda, sözleşmelerinde (`location.openapi.yaml` dahil) ve `ocelot.json`'da değişiklik
  yoktur (`git diff --stat` boş); GRN-EVENT değişikliği yalnız additive (1.1.0, Bölüm 15.1 listesi; mevcut yol / parametre / alan /
  tür / zorunlu liste / enum değeri değişmez) ve ayrı sözleşme PR'ındadır (C-3, S-3).
- [ ] AC-18 (zorunlu, DL-1; r2 — S-5 kararı) — `recordGrn` yeni kayıtta ve her yeni denemeden önce, Draft yazılmadan ve TRACE /
  INVENTORY çağrılmadan konumu LOCATION'da doğrular (`GET api/locations/{locationId}` + `/path`, kullanıcı token'ıyla Gateway
  üzerinden): kayıt yok (404), `status ≠ Active`, `isStockable = false`, başka LE ya da yolu `WarehouseId` deposunu içermeyen konum →
  422 `UNKNOWN_LOCATION`; LOCATION 503 / zaman aşımı / okunamayan yanıt → 503 `LOCATION_MASTER_UNAVAILABLE`; LOCATION 403 → 403
  `INVENTORY_FORBIDDEN`. Üç durumda da yeni kayıtta Draft yazılmaz, mevcut Draft'ın durumu değişmez, sahte TRACE ve INVENTORY
  istemcilerinin çağrı sayısı 0'dır. Geçerli konumda akış lot adımından sürer; `Completed` replay'de ve `InProgress` / `Uncertain`
  devamında LOCATION çağrılmaz. `locationId` boşsa LOCATION da çağrılmadan 422 `VALIDATION_FAILED` (AC-7).
- [ ] AC-19 (zorunlu, DL-3; r2 — S-5 kararı) — Web Create formunda depo ve konum serbest metin değildir: depo LOCATION
  `type = Warehouse`, `Active` kayıtlarında aranarak seçilir (`GET Grn/api/warehouses?search=`); konum seçilen deponun altında katman
  katman açılan listeden seçilir (`GET Grn/api/locations?parentId=&search=`) ve yalnız `isStockable = true` düğüm seçilebilir; iki
  proxy de Gateway `/api/locations/search`'e kullanıcı token'ıyla gider, render edilen HTML / JS'de `5067` yoktur; `procurement.grn.create`
  olmayan çağrı 403. Konum seçilmeden kaydet sunucu doğrulamasında reddedilir (`LocationId` zorunlu, 7 dilde ileti, API çağrısı yok);
  seçici 403 / 503'te yerelleştirilmiş ileti görünür, kaydet kapalıdır, elle metin girişi ya da sabit yedek liste yoktur; başarısız
  POST'tan sonra form seçili depo ve konumu (kimlik + kod / ad) geri çizer; yeni resx anahtarları 7 dilde (parite testi yeşil).
- [ ] AC-20 (zorunlu, DL-1; r3 — F-1) — Telafi REVERSAL'ının `transactionDate` ve `postingDate`'i satırın `CompensationStartedAt`
  alanından gelir; alan, `PostingState=CompensationPending`'i yazan aynı sürüm denetimli yazımda, telafi edilecek her satıra
  (`PostedAttempt = n`) **yalnız boşsa** bir kez yazılır (UTC, ms'e kırpılmış) ve sonraki hiçbir yazım onu değiştirmez. Telafi
  ortasında süreç kesintisi, 503 ya da `CompensationBlocked` sonrası aynı anahtarla yeniden deneme aynı REVERSAL anahtarı
  (`{key}#a{n}#{i}#comp`) ve aynı tarihlerle gider; parmak izi denetleyen sahte INVENTORY 200 replay döner, 400
  `IDEMPOTENCY_KEY_REUSED` dönmez, satır başına tek telafi hareketi oluşur.
- [ ] AC-21 (zorunlu, DL-1; r3 — F-2) — Telafi REVERSAL'ı kesin ret alırsa (422 `ALREADY_REVERSED` dışındaki her 422, ör.
  `REVERSAL_QUALITY_RECORD_LOCKED` ve — r4, F-7@bdea9ecf — kayıttan sonra pasifleşen konum için `UNKNOWN_LOCATION` ayrı vaka; her
  400; her 403) o satıra `CompensationBlockedCode` (+ zarfta varsa `recordNumber`) yazılır ve
  kalan satırların telafisi sürer; en az bir satır engelliyse `PostingState=CompensationBlocked`, `CompensationBlockedAt` bir kez,
  `ReceivingExceptions`'ta `GRN_COMPENSATION_BLOCKED` + satır + kod; GRN `Draft` kalır, `Posted` olmaz; yanıt 409
  `GRN_COMPENSATION_BLOCKED` (ilk ret kodu ve satırı, engelli satırlar ve kodları); hata günlüğünde GrnId, satır ve kod var, token ve
  anahtar değeri yok. 401 ve geçici / belirsiz sonuçlar engel sayılmaz (`CompensationPending`; 401 / 503). `CompensationBlocked` GRN'e
  aynı anahtarla yeniden gönderim yalnız telafisi bitmemiş satırları aynı anahtar ve tarihle yeniden dener: hepsi başarılı →
  `Compensated` ve kural gereği yeni deneme (2a, `PostingAttempt+1`); yine ret → `CompensationBlocked`, 409.
- [ ] AC-22 (zorunlu, DL-1; r3 — F-2, O-R3-2) — `POST /api/grn/{grnId}/compensation/resolve` (`ResolveGrnCompensationCommand`,
  `procurement.grn.reverse`) yalnız `PostingState=CompensationBlocked` GRN'de, zorunlu notla (trim sonrası 1–500 karakter)
  `PostingState=ManuallyResolved`, `ResolvedBy`, `ResolvedAt`, `ResolutionNote` yazar ve 200 döner; INVENTORY / TRACE çağrılmaz,
  engelli satırların hareket ve telafi alanları silinmez. Başka durumda 409 `INVALID_STATE`; boş / uzun not 422 `VALIDATION_FAILED`;
  izinsiz 403; başka kiracı / LE 404. `ManuallyResolved` GRN'e aynı anahtarla kayıt 409 `INVALID_STATE` (yukarı akış çağrısı yok);
  ters kayıt ve silme 409 `INVALID_STATE`. **r4 (F-4@bdea9ecf):** kirası süren `CompensationBlocked` kayıtta (aynı anahtarla telafi
  yeniden denemesi sürüyor) çözme 409 `GRN_POSTING_IN_PROGRESS` döner ve kayıt değişmez; gerçek Mongo vakası: telafi yeniden denemesi
  sahte INVENTORY'de bekletilirken çözme gönderilir → 409; telafi tamamlanınca kayıt `Compensated` (ya da kural gereği yeni deneme)
  olur, `ManuallyResolved` olmaz; ardından çözme 409 `INVALID_STATE`. Çözme yazımı `Version` + durum + kira koşulludur (koşul tutmazsa
  yazım yok).
- [ ] AC-23 (zorunlu, DL-1; r3 — F-3) — Aynı GRN'de kira alamayan istek (aynı anahtarla eşzamanlı devam / telafi; eşzamanlı iki ters
  kayıt) 409 `GRN_POSTING_IN_PROGRESS` döner, LOCATION / TRACE / INVENTORY'yi çağırmaz, kaydı değiştirmez. Kira `Version` +
  `ClaimToken` koşullu atomik güncellemeyle alınır; süresi (`Grn:PostingLeaseSeconds`) dolmuş kira devralınabilir; eski sahip bir
  sonraki yazımında koşulu tutturamaz, yeni yukarı akış çağrısı yapmadan durur ve 503 `GRN_POSTING_UNCERTAIN` döner. Gerçek Mongo ile
  iki paralel istek: satır başına tek `InventoryTransactionId`, sahte INVENTORY'de satır başına tek yeni hareket. INVENTORY'den gelen
  409 (sözleşmede yok) `Uncertain`'a düşer, GRN'in kendi 409'u ile karışmaz. **r4 (F-6@bdea9ecf):** her yukarı akış çağrısından
  (LOCATION, `createLot`, her `listLots` sayfası, hareket, telafi REVERSAL'ı) hemen önce bir kira yenileme yazımı vardır (sahte
  istemcilerin çağrı kaydı ile depo yazım kaydı sıralanarak ölçülür). Lot aşaması vakası: üç ayrı lot + birinde 409
  `LOT_ALREADY_EXISTS` ve 5 sayfalık `listLots`, `TimeProvider` her yukarı akış çağrısında 15 sn ilerletilir (toplam > 60 sn) —
  kira hiç düşmez, ikinci bir istek devralamaz (409 `GRN_POSTING_IN_PROGRESS`); aynı vakada yenileme kaldırılırsa ikinci istek
  devralır ve ilk istek devralmadan sonra TRACE'i çağırır (S30). Devralma vakası: yenileme koşulu tutmazsa ilk istek o çağrıyı
  yapmaz, 503 `GRN_POSTING_UNCERTAIN`.
- [ ] AC-24 (zorunlu, DL-1; r3 — F-9, BL-501) — Başarılı `RecordGrn` (Posted), `ReverseGrn` (Reversed), `DeleteGrn`, `BulkDeleteGrn`,
  `ResolveGrnCompensation` ve (r4) `RetryGrnPosting` (Posted) her biri **tam bir** merkezi denetim outbox satırı bırakır. **BulkDelete
  satır sayısı (r4, F-8@bdea9ecf):** silinen kayıt sayısı N ≥ 1 ise N'den bağımsız **tek** satır (nesne kimliği boş; emsal
  `BulkDeleteSupplierCommand.cs`, `CentralAuditEntityId => null`), N = 0 ise satır yok (yazım olmadığından enlist yok; emsal
  `TenantSharedSupplierStore.cs:289-304` "yalnız yazdıysa"); vakalar N = 0, 1, 3 ile ölçülür. Satır komutun son durum yazımıyla **aynı
  Mongo işleminde** yazılır (`CentralAuditTransaction.EnlistAsync`); satır Bölüm 21'deki kategori / işlem / nesne türü / kaynak
  modülü taşır; istek gövdesini, token'ı, `Idempotency-Key` değerini ve çözüm notunu taşımaz. Outbox yazımı başarısızsa son durum
  yazımı da geri alınır: GRN `Posted` olmaz, yanıt 503 `GRN_POSTING_UNCERTAIN`; aynı anahtarla tekrar tamamlar. Başarısız yanıtla
  biten komut (422 / 409 / 503) teslim edilecek satır bırakmaz; `Completed` replay satır bırakmaz.
- [ ] AC-25 (zorunlu, DL-1; r3 — F-10) — Kiracı / LE: (a) LE-B kullanıcısı LE-A'daki Draft'ın anahtarıyla kayıt gönderirse LE-A
  Draft'ı bulunmaz ve değişmez, LE-B'de bağımsız yeni GRN açılır; (b) başka kiracıda aynısı; (c) devam ve telafide her yukarı akış
  çağrısının `X-Legal-Entity-Id` değeri GRN'in `LegalEntityId`'sine eşittir; (d) aynı LE'de başka kullanıcının aynı anahtarla isteği
  409 `IDEMPOTENCY_KEY_REUSED` alır, yukarı akış çağrısı yok; (e) başka LE'nin GRN'ine `reverse`, `compensation/resolve`, `DELETE` →
  404. (a), (b) ve (e) gerçek Mongo ile.
- [ ] AC-26 (zorunlu, DL-1; r3 — F-11) — `PostingAttempt`'i `Grn:MaxPostingAttempts`'e (varsayılan 5) ulaşmış `Compensated` GRN'e
  aynı anahtarla yeniden gönderim 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED` döner; LOCATION / TRACE / INVENTORY çağrılmaz; Draft silinebilir.
- [ ] AC-27 (zorunlu, DL-1; r3 — F-4) — `ReceivedAt`, `CompensationStartedAt` ve `ReverseStartedAt` üretildiklerinde UTC ve tam
  milisaniyedir (`Ticks % TimeSpan.TicksPerMillisecond == 0`); gerçek Mongo (Procurement test replika kümesi) ile Draft yaz → oku →
  INVENTORY gövdesi kur testinde `transactionDate` / `postingDate` `UtcTicks` değerleri ilk denemedekiyle aynıdır ve parmak izi
  denetleyen sahte INVENTORY devamda 200 replay döner.
- [ ] AC-28 (zorunlu, DL-1; r3 — F-9) — Kısmi yazım: satır k için INVENTORY 201 alındıktan sonra, satır yazımından önce süreç kesilirse
  (hata enjeksiyonu) ya da satır yazımı sürüm çakışması alırsa aynı anahtarla devam satır k'yı aynı anahtarla yeniden gönderir ve
  INVENTORY 200 replay aynı `transactionId`'yi döndürür; satır başına INVENTORY'de tek hareket, GRN'de tek `InventoryTransactionId`.
- [ ] AC-29 (zorunlu, DL-3; r3 — F-2, F-3, F-11) — Web Details sayfası `postingState`'i rozetle gösterir (Uncertain,
  CompensationPending, CompensationBlocked, Compensated, ManuallyResolved; 7 dil); `CompensationBlocked`'ta engelli satırlar ve kodları
  görünür ve zorunlu notlu çöz formu yalnız `procurement.grn.reverse` iznine sahip kullanıcıya çizilir; izinsiz kullanıcıya düğme
  çizilmez, Web proxy'si 403 döner. Create formu 409 `GRN_POSTING_IN_PROGRESS`, `GRN_COMPENSATION_BLOCKED`,
  `GRN_POSTING_ATTEMPTS_EXHAUSTED` için yerelleştirilmiş ileti gösterir; anahtarı ilk ikisinde korur, üçüncüsünde yeniler (r4,
  F-1@bdea9ecf; AC-16). Yeni resx anahtarları 7 dilde (parite testi yeşil).
- [ ] AC-30 (zorunlu, DL-1; r4 — F-8@bdea9ecf (a), S-9 önerisi) — Açılış doğrulamaları, ortam × değer matrisiyle (AC-1 deseni):
  `Http` modunda `Inventory:ReferenceBaseUrl` boş, göreli (`api/`), mutlak olmayan ya da http/https dışı (`ftp://…`) → açılış hata ile
  durur; `http://127.0.0.1:5000/` ve `https://…` geçer. `Grn:PostingLeaseSeconds < 3 × Inventory:RequestTimeoutSeconds` (ör. 29 / 10)
  → durur; tam eşit (30 / 10) geçer. `Grn:MaxPostingAttempts` 0, 21, negatif ya da sayı değil → durur; 1 ve 20 geçer. Her ret iletisi
  ayar adını taşır; değer gizli bilgi değildir.
- [ ] AC-31 (zorunlu, DL-1; r4 — F-3@bdea9ecf) — `POST /api/grn/{grnId}/posting/retry` (`RetryGrnPostingCommand`,
  `procurement.grn.create`): (a) `Uncertain` kayıt → kayıttaki anahtar ve tarihlerle devam, parmak izi denetleyen sahte INVENTORY 200
  replay + kalan satırlar, satır başına tek hareket, 200 `Posted`; (b) `CompensationPending` → telafi aynı `CompensationStartedAt` ile
  biter, sonra `PostingAttempt+1`; (c) `CompensationBlocked` engel kalkmışsa telafiyi bitirir, kalkmamışsa 409
  `GRN_COMPENSATION_BLOCKED`; (d) süresi dolmuş kiralı `InProgress` devralınır, kirası süren 409 `GRN_POSTING_IN_PROGRESS`, yukarı
  akış çağrısı yok; (e) `NotStarted`, `Compensated`, `Completed`, `ManuallyResolved` → 409 `INVALID_STATE`, çağrı yok; (f) kaydı
  başlatandan **farklı** bir kullanıcı (aynı LE, `procurement.grn.create`) yeniden dener → GRN parmak izi denetlenmez, yukarı akış
  istekleri ilk denemenin anahtar ve gövdeleriyle birebir aynı (sahte INVENTORY / TRACE istek kaydı karşılaştırılır), `Authorization`
  yeni kullanıcının token'ı; (g) izinsiz 403; başka kiracı / LE ve silinmiş kayıt 404 (gerçek Mongo); (h) deneme sınırı ve 2a aynı
  (AC-26, AC-18).
- [ ] AC-32 (zorunlu, DL-3; r4 — F-3@bdea9ecf) — Web Details: `postingState ∈ {Uncertain, InProgress, CompensationPending,
  CompensationBlocked}` iken "Kaydı yeniden dene" düğmesi yalnız `procurement.grn.create` sahibine çizilir; diğer durumlarda ve
  izinsiz kullanıcıda düğme yoktur, Web proxy'si izinsizde 403 döner; proxy Gateway `POST /api/grn/{grnId}/posting/retry`'ı gövdesiz
  ve `Idempotency-Key` başlıksız çağırır; render edilen HTML / JS'de `5065` yoktur; 200, 409 (`GRN_POSTING_IN_PROGRESS`,
  `GRN_COMPENSATION_BLOCKED`, `INVALID_STATE`), 422 ve 503 için yerelleştirilmiş ileti (7 dil, parite testi yeşil).
- [ ] AC-33 (zorunlu, DL-1; r4 — F-2@bdea9ecf) — Gerçek Mongo (index'ler `GrnIndexConfiguration.EnsureIndexesAsync` ile kurulu):
  `Compensated` Draft silinir, ardından aynı anahtarla kayıt → 409 `IDEMPOTENCY_KEY_REUSED`; yeni belge yok, silinmiş belge değişmez,
  LOCATION / TRACE / INVENTORY çağrı sayısı 0; 500 yok, döngü yok (depo okuma sayısı sınırlı). Yarış vakası: arama kaydı silinmemiş
  görür, kira alınmadan önce kayıt silinir → 409 `IDEMPOTENCY_KEY_REUSED`. Başka LE'de aynı anahtar bağımsız yeni GRN açar (AC-25 ile
  tutarlı). `GrnIndexConfiguration.cs` diff'i boş.
- [ ] AC-34 (zorunlu, DL-1; r4 — F-8@bdea9ecf (b)) — Seri kontrollü satır (`serialIds` dolu), parmak izi denetleyen sahte INVENTORY ile:
  hareket gövdesi `serialIds`'i satırdakiyle aynı sırada taşır ve her denemede (`PostingAttempt` 1 ve 2) aynıdır; telafi REVERSAL'ı
  `serialIds` taşımaz (AC-9 kuralı); INVENTORY 422 `SERIAL_ALREADY_EXISTS` / `SERIAL_LOT_MISMATCH` kesin ret sayılır → telafi →
  `Compensated`, kod aynen geçirilir. Sahte INVENTORY'nin seri kuralı (giriş hareketi `Active` seriyi reddeder; giriş REVERSAL'ı seriyi
  `Consumed` yapar; `Consumed` seri yeniden girişte `Active` olur) INVENTORY kodundan alınmıştır
  (`InventoryLotDirectory.cs:173-182`, `:227-245`; `InventoryLedgerRepository.cs:461-499`; `LotPostingContext.cs:21-22`) ve AC-35
  canlıda ölçer.
- [ ] AC-35 (zorunlu, DL-4 E5; r4 — F-8@bdea9ecf (b)) — Canlı, seri kontrollü SKU: iki satırlı GRN, satır 1 iki seriyle, satır 2 kesin
  ret (ör. lotlu SKU'da lot numarası yok → `LOT_REQUIRED`) → GRN `Compensated`; satır 1'in serileri `GET /api/trace/serials?q=`
  (`trace.serials.read`) ile okunur ve kaydedilir (beklenen `Consumed`); satır 2'ye lot numarası eklenip form yolu izlenir (kesin ret
  sonrası **yeni anahtar**, AC-16 — Web'in gerçek yolu; aynı anahtarla düzeltilmiş gövde 409 alırdı) → 201 `Posted`, iki seri yine
  `Active` ve GRN konumunda; `transactions`'ta aynı seriler için ilk GRN'in girişi + telafi REVERSAL'ı + yeni GRN'in girişi.
  INVENTORY açısından aynı anahtarla `PostingAttempt+1` denemesi de yeni bir giriş hareketidir (farklı hareket anahtarı, aynı seriler);
  o yol DL-1'de AC-34'ün sahtesiyle ölçülür. Sonuç beklenenden farklıysa (ör. 422 `SERIAL_ALREADY_EXISTS`) kanıt dosyasına yazılır ve DL-4 kabul edilmez; S-12 ile
  birlikte CT'ye açık bulgu olur.

## 17. Test Expectations

**Birim / entegrasyon (DL-1, DL-2; Procurement test projesi, `Testing` ortamı):**
- HTTP istemcileri `HttpMessageHandler` sahtesiyle: başlıklar (AC-2), gövde (AC-3, şema `inventory-bundle.openapi.yaml`'dan
  okunur), Bölüm 13 tablosunun her satırı ayrı vaka.
- `GrnInventoryPostingService`: parmak izi denetleyen sahte INVENTORY (gerçek kuralı taklit eder: aynı anahtar + farklı gövde →
  400 `IDEMPOTENCY_KEY_REUSED`; REVERSAL zarf kuralları; 422 `ALREADY_REVERSED`). **Sahtenin kendisi** ayrı bir testle INVENTORY'nin
  `RequestFingerprint` alan listesine bağlanır: sahte, parmak izi alanlarını `RequestFingerprint.cs:18-44`'ten okunmuş sabit
  listeden alır ve liste o dosyadaki anahtarlarla karşılaştırılır (dosya metni okunur; kopya değil).
- Durum makinesi: devam, telafi, telafi ortasında kesinti, eşzamanlı aynı anahtar, silme kuralı (AC-5…AC-10).
- Mode anahtarı: ortam × değer matrisi (AC-1); üretim ortamında DI çözümü `HttpInventoryPostingClient`.
- LOCATION doğrulaması (AC-18, r2): `HttpLocationReferenceClient` sahte handler'la — 200 geçerli; 404; `Inactive`; `isStockable=false`;
  başka LE; yolda depo yok; 401; 403; zarflı 503; zaman aşımı; okunamayan 2xx. Her ret vakasında yeni kayıt yok, mevcut Draft
  değişmez, sahte TRACE / INVENTORY çağrı sayısı 0; `Completed` replay ve `Uncertain` devamında LOCATION çağrı sayısı 0.
- Web (AC-19, r2): proxy eylemleri (izin 403, arama > 100 → 400, yukarı akış 403 / 503 eşlemesi, `isStockable` alanı), `_Form.cshtml`
  taraması (depo / konum için serbest metin `input` yok, seçici öğeleri var), `LocationId` zorunluluğu, geri çizim, resx 7-dil parite.
- **r3 vakaları:**
  - Lot çözümü (AC-4): sahte TRACE `listLots` — `L1` için `L1`, `L10`, `XL1` ve `manufacturerLot = L1` kayıtları (yalnız `L1`
    sayılır); birebir eşleşme ikinci sayfada; başka `skuLevel`'de aynı numara (sayılmaz); `l1` girdisi `L1` ile tek `createLot`.
  - Telafi tarihi (AC-20): telafinin i'nci çağrısından sonra hata enjeksiyonu + yeniden deneme; `CompensationStartedAt` değişmez,
    parmak izi denetleyen sahte INVENTORY 200 replay döner.
  - Telafi engeli (AC-21): REVERSAL'a 422 `REVERSAL_QUALITY_RECORD_LOCKED`, 400 `INVALID_REQUEST`, 403 — ayrı vakalar; 401 ve 503
    engel değil; engel kalktıktan sonra yeniden gönderim → `Compensated` → yeni deneme.
  - Elle çözüm (AC-22): her `PostingState` × izin × not matrisi; `ManuallyResolved`'a kayıt / ters kayıt / silme 409.
  - Kira (AC-23): gerçek Mongo (`ProcurementMongoReplicaSet`), iki paralel istek; `TimeProvider` ile kira süresinin dolması ve
    devralma; eski sahibin koşul tutmayınca yukarı akış çağrısı yapmadan durması.
  - Denetim (AC-24): gerçek Mongo'da `central_audit_outbox` okunur; outbox yazımına hata enjeksiyonu → son durum yazımı geri alınır.
  - Kiracı / LE (AC-25): gerçek Mongo, iki LE ve iki kiracı; aynı LE'de başka kullanıcı aynı anahtar.
  - Deneme sınırı (AC-26); tarih kırpma + Mongo gidiş-dönüş (AC-27); kısmi yazım (AC-28): hata enjeksiyonu satır k'nın INVENTORY
    201'inden sonra.
  - Web (AC-29): Details rozeti ve çöz formu izin matrisi, proxy 403, resx 7-dil parite.
- **r4 vakaları (bağımsız inceleme `bdea9ecf`):**
  - Web anahtar kuralı (AC-16, F-1): Web test projesinde sahte API handler'ı; her yanıt kodu için iki ardışık POST'un
    `Idempotency-Key` başlığı; gövde değişimi; parmak izi denetleyen sahte API ile 422 → düzelt → 201 uçtan uca.
  - Silinmiş anahtar (AC-33, F-2): gerçek Mongo, index'ler kurulu; sil → aynı anahtar; yarış (kira öncesi silme).
  - Yeniden deneme (AC-31, F-3): her `PostingState` × izin × kullanıcı (başlatan / başka) matrisi; sahte INVENTORY / TRACE istek
    kaydının ilk denemeyle birebir karşılaştırılması; gerçek Mongo'da kiracı / LE / silinmiş 404. Web (AC-32): düğme izin matrisi.
  - Çözme kirası (AC-22, F-4): gerçek Mongo; telafi yeniden denemesi sahte INVENTORY'de bekletilirken çözme.
  - Adım 2 sırası (AC-7, F-5): `Completed` + farklı gövde / başka kullanıcı; `RequestFingerprint` alanı olmayan `Posted` ve `Draft`
    belgeler Mongo'ya doğrudan yazılarak (FU01 öncesi biçim).
  - Kira yenileme (AC-23, F-6): `TimeProvider` ile lot aşaması > 60 sn; çağrı / yazım sırası kaydı.
  - Konum pasifleşmesi (AC-21, AC-9, F-7): telafi ve ters kayıt REVERSAL'ına 422 `UNKNOWN_LOCATION`.
  - Açılış (AC-30), seri satırı (AC-34), BulkDelete satır sayısı N = 0 / 1 / 3 (AC-24) (F-8).
- Mevcut `GrnBackendTests` `FakeInventoryPostingClient` ile kalır; yeni sonuç türüne uyarlanır.

**Sabotajlar (her guard için; kanıt dosyasında kırmızı çıktı):**
| # | Sabotaj | Kırmızı olması gereken |
|---|---|---|
| S1 | `Mock` ortam kontrolünü kaldır | AC-1 Development vakası |
| S2 | `Authorization` ya da `X-Legal-Entity-Id` başlığını düşür | AC-2 |
| S3 | Gövdeye `lotNumber` yaz, `lotId` yazma | AC-3 + AC-4 |
| S4 | Devamda `GrnId`'yi yeniden üret | AC-6 (sahte 400 `IDEMPOTENCY_KEY_REUSED`) |
| S5 | 422'de satırı Posted say (catch + devam) | AC-5 |
| S6 | Telafiyi atla | AC-5 (net stok ≠ 0) |
| S7 | `CompensationPending`'i telafiden sonra yaz | AC-8 kesinti vakası |
| S8 | REVERSAL'da GRN zarfını (MOD-0142/GOODS_RECEIPT/GrnId) geri koy | AC-9 |
| S9 | Silmede `PostingState` kontrolünü kaldır | AC-10 |
| S10 | `RecordGrnCommand`'dan denetim bildirimini kaldır | AC-11 (`AuditTrailStandardTests`) |
| S11 | Web'de anahtarı her POST'ta yenile | AC-16 |
| S12 | LOCATION doğrulamasını (adım 2a) atla | AC-18 (bilinmeyen konumda sahte TRACE / INVENTORY çağrısı > 0, 422 yok) |
| S13 | Yol denetimini (konum `WarehouseId` altında) kaldır | AC-18 "başka depodaki konum" vakası |
| S14 | Web'de konum seçicisini serbest metin `input`'a geri çevir ya da `LocationId` zorunluluğunu kaldır | AC-19 form taraması / doğrulama vakası |
| S15 | Parmak izi denetimini kaldır (aynı anahtar + farklı gövde devam eder) | AC-7 409 vakası |
| S16 | `ReverseStartedAt`'i her denemede yeniden üret | AC-9 kesinti sonrası devam (sahte 400 `IDEMPOTENCY_KEY_REUSED`) |
| S17 | `CompensationStartedAt`'i her denemede yeniden üret ya da "yalnız boşsa" koşulunu kaldır | AC-20 (sahte 400 `IDEMPOTENCY_KEY_REUSED`) |
| S18 | Telafide kesin reti `CompensationPending`'de bırak (`CompensationBlocked` yazma) | AC-21 |
| S19 | Çözme komutunda durum denetimini kaldır | AC-22 (`Uncertain` Draft çözülür) |
| S20 | Kira koşulunu kaldır (koşulsuz güncelleme) | AC-23 paralel vaka (`GRN_POSTING_IN_PROGRESS` yok, aynı satıra ikinci yazım) |
| S21 | Son durum yazımından `CentralAuditTransaction.EnlistAsync`'i kaldır | AC-24 (outbox satırı yok) |
| S22 | Devam aramasından LE süzgecini kaldır ya da parmak izinden kullanıcıyı çıkar | AC-25 (a) / (d) |
| S23 | Deneme sınırını kaldır | AC-26 |
| S24 | Tarihlerin ms kırpmasını kaldır | AC-27 (kırpma iddiası) |
| S25 | Satır anahtarında deneme numarası yerine istek başına artan sayaç kullan | AC-28 (satır başına iki hareket) |
| S26 | Lot çözümünde birebir süzgeci kaldır (ilk öğeyi al) ya da yalnız ilk sayfayı oku | AC-4 (`L10` seçilir / ikinci sayfa bulunmaz) |
| S27 | Yeni resx anahtarlarından birini bir dilde sil | AC-16, AC-19, AC-29 parite testi |
| S28 | LOCATION istemcisinde `status`, `isStockable` ve LE kurallarını tek tek kaldır (üç alt sabotaj) | AC-18 ilgili vakalar |
| S29 | Çöz formunu izinsiz kullanıcıya çiz ya da Web proxy'sinde izin denetimini kaldır | AC-29 |
| S30 | Lot aşamasında (ya da herhangi bir yukarı akış çağrısından önce) kira yenilemesini kaldır — r4, F-6@bdea9ecf | AC-23 lot aşaması vakası (ikinci istek devralır, ilk istek devralmadan sonra TRACE'i çağırır) |
| S31 | Web'de kesin ret (422) sonrası anahtarı koru — r4, F-1@bdea9ecf | AC-16 (c), (d) (409 `IDEMPOTENCY_KEY_REUSED`) |
| S32 | Web'de `IdempotencyBodyHash` karşılaştırmasını kaldır — r4, F-1@bdea9ecf | AC-16 (b) (değişen gövde eski anahtarla gider) |
| S33 | Web'de anahtarı yalnız 503'te koru (409 `GRN_POSTING_IN_PROGRESS` / `GRN_COMPENSATION_BLOCKED`'ta yenile) — r4, F-1@bdea9ecf | AC-16 (a) |
| S34 | Adım 2 aramasını silinmişleri görmeyen `TenantFilter` aramasına geri çevir — r4, F-2@bdea9ecf | AC-33 (409 yerine 500 ya da yeniden okuma döngüsü) |
| S35 | Çözme yazımından kira koşulunu kaldır — r4, F-4@bdea9ecf | AC-22 r4 vakası (kirası süren kayıt `ManuallyResolved` olur) |
| S36 | Adım 2'de `Completed` maddesini parmak izi maddesinin önüne al — r4, F-5@bdea9ecf | AC-7 r4 vakası (farklı gövdeye 201) |
| S37 | Parmak izsiz eski kayıt dalını kaldır (null ya "farklı" ya "aynı" sayılsın; iki alt sabotaj) — r4, F-5@bdea9ecf | AC-7 eski `Posted` (409 olur) / eski `Draft` (yukarı akış çağrısı > 0) vakaları |
| S38 | Üç açılış doğrulamasını tek tek kaldır (üç alt sabotaj) — r4, F-8@bdea9ecf | AC-30 ilgili vakalar |
| S39 | `RetryGrnPostingCommand`'da durum denetimini kaldır — r4, F-3@bdea9ecf | AC-31 (e) (`Compensated` / `Completed`'de yukarı akış çağrısı > 0) |
| S40 | `RetryGrnPostingCommand`'da yukarı akış anahtarını kayıttan değil yeni üret — r4, F-3@bdea9ecf | AC-31 (a), (f) (satır başına iki hareket / istek kaydı farkı) |
| S41 | Details "Kaydı yeniden dene" düğmesini izinsize çiz ya da proxy izin denetimini kaldır — r4, F-3@bdea9ecf | AC-32 |
| S42 | Telafi sınıflamasında 422'yi kod listesiyle sınırla (`UNKNOWN_LOCATION` geçici sayılır) — r4, F-7@bdea9ecf | AC-21 konum vakası (`CompensationBlocked` yerine `CompensationPending`, 503) |
| S43 | BulkDelete'te silinen her kayıt için ayrı enlist et — r4, F-8@bdea9ecf | AC-24 N = 3 vakası (üç satır) |
| S44 | Telafi REVERSAL'ına satırın `serialIds`'ini ekle — r4, F-8@bdea9ecf | AC-34 (sahte INVENTORY 400 `INVALID_REQUEST`) |

**Canlı (DL-4, E5):** AC-12…AC-15 ve AC-35 (r4; seri kontrollü SKU), Gateway 5000 üzerinden, istek/yanıt gövdeleri ve `balance`/`transactions` önce-sonra değerleri
kanıt dosyasında; filo Inventory 5067'yi ana çalışma kopyasından `dotnet watch` ile koşturuyor olabilir — build/sabotaj ayrık
çalışma ağacında.
**Build:** ProcurementService + Web 0 hata; Procurement test takımının tamamı (filtre yok); Inventory takımı koşturulmaz (kod
değişmiyor) ama AC-17 diff kanıtı zorunlu.

## 18. Ready-for-dev Checklist

- [x] Golden Reference Compact (ana paket) okundu; yeni sayfa yok
- [x] Frontmatter tam (service/shell/golden_reference/data_mode/entity_base)
- [x] Layout & Shell Contract açık (`_LayoutTenantShell`)
- [x] Backend/Frontend dosya yolları yazılı
- [x] Validation her alan için
- [x] Failure Path ≥4 senaryo + hata eşleme tablosu (r3: telafi aşaması ve iki 409 ayrımı)
- [x] Authorization: izinler + kimlik yayılımı + S-1 kararı (r2) + S-5'ten türetilen `inventory.locations.read` (r3: O-R3-1 sahip
  onayı bekler) + çözme ucu izni (O-R3-2) + rol şablonu notu
- [x] Gateway kararı (değişiklik yok; çözme ucu mevcut `/api/grn/{everything}` rotasında)
- [x] Audited Events (beş komut; r3'te gerçek mekanizmaya göre yeniden yazıldı)
- [x] S-1, S-2, S-5 cevaplandı (Beste, 2026-11-25; karar kaydı `docs/records/decisions/2026-11/mod-0142-fu01-owner-decisions-01.md`)
  ve r2'ye işlendi
- [x] r2 bağımsız incelemesinin bulguları F-1…F-13 r3'te işlendi (Bölüm 19.4)
- [x] r3 bağımsız incelemesinin (`bdea9ecf`, KOŞULLU) bulguları F-1…F-9 r4'te işlendi (Bölüm 19.4, `F-n@bdea9ecf` satırları)
- [ ] S-9 ve S-12 paket önerileri onay satırı notunda açıkça kabul edildi (DL-1 kapısı; r4, F-9@bdea9ecf)
- [ ] S-4 paket önerisi onay satırı notunda açıkça kabul edildi ya da Beste ayrıca cevapladı (DL-1 kapısı)
- [ ] O-R3-1, O-R3-2, O-R3-3 aynı biçimde kapandı (DL-1 kapısı)
- [ ] Registry satırı (C-1), r3'ün bağımsız incelemesi + onay satırı (C-2)
- [ ] S-3 cevaplandı ve GRN-EVENT 1.1.0 sahip onayı (C-3) — yalnız sözleşme PR'ı ve DL-4 kapısı; DL-1'i bekletmez

## 19. Implementation Notes

### 19.1 Alan eşlemesi — `GOODS_RECEIPT_PO` (INVENTORY `MovementRequest`)

| INVENTORY alanı | Kaynak |
|---|---|
| idempotencyKey (+ başlık) | `{grnKey}#a{PostingAttempt}#{lineIndex}` |
| documentGroupId | `GrnId` (sözleşme örneği `inventory-bundle.openapi.yaml:79`) |
| movementType | `GOODS_RECEIPT_PO` |
| movementReason | gönderilmez |
| itemId, skuId, skuLevel | satır |
| toWarehouseId, toLocationId | başlık `WarehouseId`, `LocationId` |
| toStockStatus | satır `ToStockStatus` |
| quantity, uomId | satır (Decimal string) |
| lotId | TRACE `createLot` / çözüm sonucu; lotNumber yoksa gönderilmez |
| serialIds | satır (varsa); her denemede aynı liste ve sıra; telafi ve ters kayıt REVERSAL'ında gönderilmez (19.2) — seri yaşam döngüsünü INVENTORY yürütür (girişte `Active`, giriş REVERSAL'ında `Consumed`, yeniden girişte `Active`; AC-34, AC-35; r4, F-8@bdea9ecf) |
| sourceModule / sourceType | `MOD-0142` / `GOODS_RECEIPT` (`GrnModels.cs:23-26`) |
| sourceDocumentId | `GrnId` |
| sourceLineId | `PoLineId` (mevcut davranış; boşsa gönderilmez) |
| sourceSystem | gönderilmez (INVENTORY anlamı DEC-INV-19 dış LE; GRN `SourceSystem` farklı anlam) |
| transactionDate, postingDate | `ReceivedAt` (Draft'ta bir kez) |

Yanıt: `transactionId` → `GrnLine.InventoryTransactionId`; `balanceSnapshot.lotId` bilgi amaçlı okunur, GRN'deki `LotId` TRACE'ten
gelir (ikisi farklıysa belirsiz → 503 `GRN_POSTING_UNCERTAIN`, hata günlüğü).

### 19.2 REVERSAL eşlemesi (ölçülen kısım + ölçülemeyen)

Ölçülen (`ReversalRules.cs:7-34`, `MovementDirectionRules.cs:109-112`, `MovementRequestParser.cs:125-126`,
`InternalMovementPostingService.cs:357-381`): `movementType=REVERSAL`, `sourceModule=MOD-0173`, `sourceType=REVERSAL`,
`sourceDocumentId=<orijinal transactionId, UUID>`, `itemId/skuId/skuLevel/quantity/uomId` orijinalle **birebir**, `from*`/`to*`/
`lotId`/`serialIds` **yok**; ters bacakları sunucu türetir. Zarf hatası 400 `INVALID_REQUEST`; ikinci ters kayıt 422
`ALREADY_REVERSED` (`InventoryErrorCodes.cs:35`); MOD-0175 kalite kaydı kilidi 422 `REVERSAL_QUALITY_RECORD_LOCKED` (`:107`;
sözleşme 1.8.0 `inventory-bundle.openapi.yaml:60-68`), okunamazsa 503 `INVENTORY_UNAVAILABLE`. Anahtar `{reverseKey}#{i}`;
`reverseKey` ilk denemede GRN'e yazılır (bugün yalnız başarıda yazılıyor — `ReverseGrnHandler.cs:78-79`), farklı anahtarla gelen
yeni istek bekleyen ters kaydı kayıtlı anahtarla sürdürür.
**Ölçülemeyen → açık soru:** (S-8) kalite kilidi ya da başka kesin ret satır k'da gelirse "kısmen ters çevrilmiş" GRN'in iş
anlamı; GRN-EVENT `status` enum'unda böyle bir değer yok. (S-10; r3'te koddan ölçüldü) `QUALITY_INSPECTION` /
`QUARANTINE` statüsüne giren her GRN satırı INVENTORY'de bir giriş tutumu açar
(`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Quarantine/Services/QuarantineIntake.cs:15-20`;
GRN varsayılanı `QUALITY_INSPECTION`, `GoodsReceipt.cs:65`). Kaynak ters çevrildiğinde yalnız giriş tutumu boşa çıkarılabilir; kural
tablosunun eşleştirmediği birleşim değişiklik yapmaz (fail-closed;
`services/Diten.InventoryService/src/Diten.InventoryService.Domain/Quarantine/Rules/SourceReversalRules.cs:87-91`); kalite kararı
başlamış / bitmiş kaynağın REVERSAL'ı 422 `REVERSAL_QUALITY_RECORD_LOCKED` alır (`InventoryErrorCodes.cs:101-107`). Kilidin ne
sıklıkla oluşacağı canlıda ölçülmedi. (S-11 — r3'te sonucu tanımlandı, F-2) Telafi REVERSAL'ı bu kilide takılırsa GRN
`CompensationBlocked`'a geçer (Bölüm 8.2 adım 5e); olasılık düşük (telafi saniyeler içinde) ama sonuç ve çıkış yolu artık tanımlı.

### 19.3 Diğer notlar

- Ana paketin "contract sınırı → GRN kodu değişmez" beklentisi (`MOD-0142-receiving-grn.md:87,174`) bu ölçümle düzeltilir:
  B-1…B-6 GRN el kodunda değişiklik ister. Ana pakete not düşmek CT adımıdır (C-6), bu FU ana paketi değiştirmez.
- Plan hizalaması: bu modülün plan dosyası satırı bana verilmedi ve depoda bulunamadı (`docs/roadmap/**`'ta MOD-0142 için MVP
  satırı yok; yalnız MVP-6 KPI tanımlarında kaynak olarak geçiyor, `mvp6-development-plan-v10.0.md:122-124`). MVP-2 ataması sahip
  kararıdır (Beste, 2026-11-25), plan satırı değildir → dilim tablosunda `plan kimliği yok` / `plan satırı yok`.
- r2 ölçümleri (HEAD `32ce7df6c`; r3'te hepsi `origin/main` `82af5c24a`'de yeniden okundu ve tutuyor): `LocationsController.cs:25` (rota) ve `:35-60` (frozen v1 okumaları, hepsi `inventory.locations.read`) ve
  `:110-111` (search); `InventoryPermissions.cs:20`; `MovementMasterDataValidator.cs:308-339` (INVENTORY konum kuralı, tek kod
  `UNKNOWN_LOCATION`); `ocelot.json:924`, `:942` (GET var); bugünkü form `_Form.cshtml:50-62` serbest metin, `GrnViewModels.cs:19-23`
  (`WarehouseId` `[Required]`, `LocationId` isteğe bağlı); emsal seçici `SupplyChainShipmentsController.cs:234-251`, `:698-710`.
- r3 ölçümleri (`origin/main` `82af5c24a`): `RequestFingerprint.cs:42-43` (UtcTicks); `InternalMovementPostingService.cs:27-28`,
  `:249-255` (tenant + LE kapsamlı anahtar; eşzamanlı aynı anahtar 200 replay); postMovement yanıtları
  `inventory-bundle.openapi.yaml:98-137` (409 yok); `MovementMasterDataValidator.cs:327` ve `InventoryPolicyOptions.cs:16`;
  `LotIdentityPolicy.cs:12-13`, `:33`; `LotRepository.cs:74-78`, `:90-93`, `:117-125`; `ListLotsHandler.cs:66-70`;
  `TraceIndexConfiguration.cs:38-39`; `Lot.cs:8-12`; `CentralAuditBehavior.cs:8-13`; `CentralAuditRecorder.cs:11-12`, `:39-50`,
  `:160-168`; `SupplierCentralAudit.cs:5-15`; `PlatformAuditAppendController.cs:106`; `GrnRepository.cs:77-90`, `:124-129`;
  `GrnIndexConfiguration.cs:31-35`; `RunThreeWayMatchHandler.cs:73-74`; `InventoryErrorCodes.cs:101-107`; `GoodsReceipt.cs:65`;
  `InventoryController.cs:27-28`; `ocelot.json:750`. `verify_module_id.py --check-id MOD-0142-FU01 … --parent MOD-0142` yeniden
  koşuldu: OK (çıkış 0, 2026-11-25).
- Procurement test takımı Windows'ta: yeni çalışma ağacında tedarikçi yaml'ları CRLF ile çıkarsa 15 yanlış kırmızı görülür —
  ağaç `core.autocrlf=false` ile alınır.

### 19.4 Bağımsız incelemelerin kapanışı (r3 ve r4)

Kaynaklar: `docs/records/reviews/MOD-0142-FU01/2026-11-25-4803c8de.md` (r2 LF sha256 `4803c8de…`, UYGUN DEĞİL; satırlar `F-1`…`F-13`,
r3'te kapandı) ve `docs/records/reviews/MOD-0142-FU01/2026-11-25-bdea9ecf.md` (r3 LF sha256 `bdea9ecf…`, KOŞULLU; satırlar
`F-1@bdea9ecf`…`F-9@bdea9ecf`, r4'te kapandı). Her bulgu `origin/main` `82af5c24a` koduyla yeniden doğrulandı; sonuç "Kod doğrulaması"
sütunundadır.

| Bulgu | Ciddiyet | Kod doğrulaması | Kapanış (bu pakette) | AC / sabotaj |
|---|---|---|---|---|
| F-1 | ENGEL | Tutuyor: parmak izi `transactionDate` / `postingDate`'i `UtcTicks` ile katar (`RequestFingerprint.cs:42-43`); aynı anahtar + farklı gövde 400 (`InternalMovementPostingService.cs:342-347`) | `GrnLine.CompensationStartedAt` (Bölüm 4); 8.2 adım 5a–5b: `CompensationPending` ile aynı yazımda, yalnız boşsa bir kez; yeniden denemede aynı anahtar + tarih; 8.3 Tarihler | AC-20, AC-8; S17 |
| F-2 | ENGEL | Tutuyor: kalite kilidi REVERSAL'ı 422 ile reddeder (`InventoryErrorCodes.cs:101-107`); GRN satırı varsayılan `QUALITY_INSPECTION` (`GoodsReceipt.cs:65`) ve giriş tutumu açar | `CompensationBlocked` + `ManuallyResolved`; 8.2 adım 5e (kesin ret sınıfı, kalan satırlar sürer, 409 `GRN_COMPENSATION_BLOCKED`); iki çıkış: engel kalkınca aynı anahtarla yeniden deneme, yetkili kişinin `ResolveGrnCompensationCommand`'ı (O-R3-2); 8.3 Eskalasyon (O-R3-3); silme kuralı; 19.2 S-11 | AC-21, AC-22, AC-10, AC-29; S18, S19, S29 |
| F-3 | ORTA | Kısmen: INVENTORY `postMovement` sözleşmesinde 409 yok (`inventory-bundle.openapi.yaml:98-137`), aynı anahtar yarışı INVENTORY içinde 200 replay'e çözülür (`InternalMovementPostingService.cs:249-255`) — bir INVENTORY "in-flight 409" yanıtı yok; GRN tarafında kira gerçekten yoktu (`GrnRepository.cs:77-90` yalnız sürüm) | 8.2 adım 2b kira (`ClaimToken` + `ClaimExpiresAt`, `Version` koşullu atomik güncelleme), 409 `GRN_POSTING_IN_PROGRESS`; iki 409 ayrımı Bölüm 13; `Grn:PostingLeaseSeconds` | AC-23, AC-9; S20 |
| F-4 | ORTA | Tutuyor (risk): sürücü 2.27.0, `DateTimeOffset` serileştiricisi kayıtlı değil (yalnız Guid); varsayılan temsil ölçülmedi | 8.3 Tarihler: üç tarih UTC + ms kırpma, `TimeProvider`; gerçek Mongo gidiş-dönüş testi | AC-27, AC-6; S24 |
| F-5 | ORTA | — (kapı yapısı) | Bölüm 22: S-3 DL-1 kapısından çıktı (yalnız C-3 ve DL-4); S-4 onay satırıyla kabul edilecek paket önerisi, DL-1 buna bağlı; 15.1; 18; 23 | — |
| F-6 | ORTA | Tutuyor ve genişledi: `q` içerir + harf duyarsız ve üretici / tedarikçi lotunu da eşler (`LotRepository.cs:90-93`); sayfasız çağrı bütün listeyi döner (`LotRepository.cs:117-125`); iş anahtarı normalize (trim + büyük harf) ve kiracı geneli (`LotIdentityPolicy.cs:12-13`) | 8.2 adım 3: normalize gruplama, sayfalı okuma (200, en çok 5 sayfa), ordinal birebir `lotNumber` + `skuId` + `skuLevel` süzgeci | AC-4; S26 |
| F-7 | ORTA | Tutuyor: LE eşleşmesi ayara bağlı (`MovementMasterDataValidator.cs:327`), varsayılan `true`, `false` yalnız INVENTORY `Mock` ortamında (`InventoryPolicyOptions.cs:16`) | 14 ve 23 O-R3-1: iki türetme (API doğrulaması, `inventory.locations.read`) sahip onayı bekler; 8.2 adım 2a politika farkı yazıldı | AC-18 (O-R3-1 cevabına bağlı) |
| F-8 | ORTA | Tutuyor: izin hareket türüne bağlı değil (`InventoryController.cs:27-28`) | 14: rol şablonu notu (yalnız mal kabul rolü, depo yöneticisi rolünden ayrık), servis kimliği geçişi sahip/tarih TBD (Ali — Auth), izleme önerisi; 20; 24 C-5 | — (rol yapılandırması) |
| F-9 | ORTA | Tutuyor ve düzeltildi: yol `b` serbest olay adı taşımaz, başarısız komut merkezi kayıt bırakmaz (`CentralAuditBehavior.cs:8-13`, `CentralAuditRecorder.cs:39-50`) — r2'deki `posting-failed` adı mekanizmada yoktu | 21 yeniden yazıldı (kategori / işlem / nesne türü / kaynak modül; yarım akışın izi; outbox atomikliği); 17 eksik sabotajlar | AC-11, AC-16, AC-18, AC-24, AC-28; S15, S16, S21, S25, S27, S28 |
| F-10 | ORTA | Tutuyor: index tenant + LE (`GrnIndexConfiguration.cs:31-35`), arama tenant + LE (`GrnRepository.cs:124-129`); yeni yollar sınanmıyordu | 8.3 Kiracı / LE; parmak izine kullanıcı (Bölüm 4); 13 senaryo | AC-25; S22 |
| F-11 | DÜŞÜK | Tutuyor: TRACE lotu silinmez (`Lot.cs:11-12`) | `Grn:MaxPostingAttempts` + 409 `GRN_POSTING_ATTEMPTS_EXHAUSTED`; 8.3 Sahipsiz kayıtlar (temizlik FU02, Draft lot kabul) | AC-26; S23 |
| F-12 | DÜŞÜK | Tutuyor: MOD-0143 yalnız Posted eşler (`RunThreeWayMatchHandler.cs:73-74`) | 0.3'e MOD-0143, MOD-0175, MOD-0173-FU02 ve Platform merkezi denetim bilgilendirme satırları; `verify_module_id.py --check-id` yeniden koşuldu (OK) | — |
| F-13 | DÜŞÜK | Tutuyor: r1 `22d30cc75`, r2 `32ce7df6c` (MVP-1 hattı) tabanlıydı | Tek taban `origin/main` `82af5c24a` (başlık notu, 19.3); r1 atıflarında `git diff --stat` boş; r2 atıfları main'de tek tek yeniden okundu | — (DL-1 başında yeniden doğrulama) |
| F-1@bdea9ecf | ORTA | Tutuyor: Web her POST'ta anahtarı başlığa yeni GUID olarak yazıyor (`frontend/Diten.Web/Controllers/GrnController.cs:313-319`); r3 Bölüm 11 anahtarı 422 / 409 / 503'ün hepsinde koruyordu; GRN parmak izi gövdeyi içerdiğinden (Bölüm 4) 422 sonrası düzeltilmiş gövde 409 alırdı | Bölüm 11: anahtar yalnız belirsiz sonuçta (her 503, 409 `GRN_POSTING_IN_PROGRESS`, 409 `GRN_COMPENSATION_BLOCKED`) korunur; kesin ret (422, 400, 403, diğer 409'lar) sonrası ya da `IdempotencyBodyHash` farklıysa yeni anahtar; 8.2 adım 2 (viii) notu; AC-29 düzeltildi | AC-16 (a)–(e), AC-29; S11, S31, S32, S33 |
| F-2@bdea9ecf | ORTA | Tutuyor: index tenant + LE + anahtar, `Unique`, `Sparse`, kısmi süzgeç yok (`GrnIndexConfiguration.cs:31-35`); arama `TenantFilter` ile `IsDeleted = false` süzer (`GrnRepository.cs:57-63`, `:124-129`); silme soft delete (`:94-107`) → silinmiş kaydın anahtarıyla InsertOne benzersiz index hatası verir, arama kaydı bulamaz | En az değişiklikli seçenek: kural (index ve göç yok) — adım 2 araması silinmişleri de görür, silinmiş kayıt 409 `IDEMPOTENCY_KEY_REUSED`; index çakışmasında tek yeniden okuma, döngü yok; kira alınamazsa silinmişler dahil yeniden okuma. Kısmi süzgeç reddedildi: `Sparse` index'in düşürülüp yeniden kurulmasını (göç) ister ve silinen Draft'ın anahtarını yeni GRN'e açar (8.2 Silme gerekçesi) | AC-33; S34 |
| F-3@bdea9ecf | ORTA | Tutuyor: GRN parmak izi kullanıcıyı içerir (Bölüm 4) — form yolu başka kullanıcıya kapalı; kayıt anahtarı zaten saklanıyor (`GoodsReceipt.IdempotencyKey`, `GoodsReceipt.cs:143`); INVENTORY ve TRACE parmak izleri aktörü içermez (`RequestFingerprint.cs:18-44`, `CreateLotHandler.cs:87-92`) — başka kullanıcının aynı anahtarlı çağrısı replay alır | Seçim: Details'ten yeniden deneme (FU02'den çekildi). Yeni `RetryGrnPostingCommand` + `POST /api/grn/{grnId}/posting/retry` (`procurement.grn.create`, gövdesiz, kayıttaki anahtar ve gövde; DL-1) ve Details "Kaydı yeniden dene" düğmesi (DL-3); 8.2 adım 2c; 8.3 Sahipsiz kayıtlar'a erteleme maliyeti cümlesi (süpürücü ve bildirim FU02); 13, 14, 15, 15.1, 20, 21 | AC-31, AC-32; S39, S40, S41 |
| F-4@bdea9ecf | ORTA | Tutuyor: r3 kira kapsamı (8.2 adım 2b) ReverseGrn ve silmeyi sayıyor, çözmeyi saymıyordu; bugünkü güncelleme yalnız sürüm koşullu (`GrnRepository.cs:77-92`) | 8.2 adım 2b: çözme yazımı `Version` + `PostingState = CompensationBlocked` + kira boş / geçmiş koşullu; kirası süren kayıtta 409 `GRN_POSTING_IN_PROGRESS`; 13 satırı; 15.1 | AC-22 (r4 vakası); S35 |
| F-5@bdea9ecf | DÜŞÜK | Tutuyor: bugünkü replay parmak izine bakmadan 201 döndürüyor (`RecordGrnHandler.cs:48-55`); r3 adım 2'de `Completed` maddesi parmak izi maddesinden önceydi; FU01 öncesi kayıtlarda `RequestFingerprint` alanı yok | 8.2 adım 2: maddeler sıralı ve numaralı (i)…(ix), "ilk tutan uygulanır"; parmak izi `Completed`'den önce; parmak izsiz eski kayıt: `Completed` → 201 (kabul edilen sınır, gerekçeli), diğer → 409; Bölüm 4 | AC-7 (r4 cümlesi); S15, S36, S37 |
| F-6@bdea9ecf | DÜŞÜK | Tutuyor: r3'te kira yalnız durum yazımlarında yenileniyordu; lot aşamasında durum yazımı yok (8.2 adım 3) — birden çok `createLot` ve en çok 5 `listLots` sayfası × 10 sn > 60 sn | İddia daraltılmadı, güçlendirildi: her yukarı akış çağrısından hemen önce koşullu kira yenileme; yenileme tutmazsa çağrı yok, 503; 8.1'de 3 kat kuralının her çağrıyı kapsadığı gerekçe; Bölüm 4; 13 satırı | AC-23 (r4 vakası); S30 |
| F-7@bdea9ecf | DÜŞÜK | Tutuyor: INVENTORY konumu REVERSAL dahil işlem içinde yeniden doğrular ve 422 `UNKNOWN_LOCATION` verir (`InternalMovementPostingService.cs:236-241`, `LocationNotPostable`) | 8.2 adım 5e kesin ret örneklerine ve çıkış yolu (1)'e konum; 13 satırı ve ek senaryo; ters kayıtta S-8 kuralı | AC-21 (konum vakası), AC-9; S42 |
| F-8@bdea9ecf | DÜŞÜK | Tutuyor: (a) açılış doğrulamaları hiçbir AC'de yoktu; (b) seri: giriş `Active` seriyi `SERIAL_ALREADY_EXISTS` ile reddeder (`InventoryLotDirectory.cs:173-182`), REVERSAL özgün hareketin seri yaşam döngüsünü tersine yürütür (`LotPostingContext.cs:21-22`, `InventoryLedgerRepository.cs:461-499` — giriş REVERSAL'ı çıkıştır → `Consumed`), `Consumed` seri yeniden girişte `Active` olur (`InventoryLotDirectory.cs:227-245`) — kod okuması, canlıda ölçülmedi; (c) BulkDelete emsali komut başına tek kayıt, nesne kimliği boş (`BulkDeleteSupplierCommand.cs`), yazım yoksa enlist yok (`TenantSharedSupplierStore.cs:289-304`) | (a) AC-30; (b) AC-34 (DL-1, sahte) + AC-35 (DL-4, canlı; DL-4 kapısına seri kontrollü SKU ve `trace.serials.read`), 19.1 `serialIds` satırı; (c) Bölüm 21 BulkDelete satırı ve AC-24: N ≥ 1 → tek satır, N = 0 → satır yok | AC-24, AC-30, AC-34, AC-35; S38, S43, S44 |
| F-9@bdea9ecf | DÜŞÜK | Tutuyor: S-9 (8.1 açılış hatası) ve S-12 (K2) DL-1 davranışını etkiliyordu, DL-1 kapısında yoktu | Bölüm 22 DL-1 kapısı ve 18: S-9 ve S-12 "onay satırıyla kabul edilecek öneri"; 23'te öneriler açık yazıldı (S-9: açılışta hata; S-12: K2, başarısız akış için ek GRN kaydı yok); 8.1; 21 | AC-30 (S-9), AC-24 (S-12) |

## 20. Follow-up Items

- **FU02 adayı:** depo varsayılan mal kabul konumu (LOCATION rol verisi; konum seçici r2'de S-5 kararıyla FU01'e alındı), lot
  üretim/son kullanma/tedarikçi lotu alanları (S-6), `SkuLevel=Item` (S-7);
  **r3:** `CompensationBlocked` için kişiye bildirim / Görev Merkezi iş öğesi (O-R3-3 önerisi A ise), GRN listesinde `PostingState`
  filtresi ve rozeti (liste ekranı — BL-440 dokunma protokolüyle), `Compensated` / `NotStarted` Draft için otomatik temizlik;
  **r4:** yarıda kalmış kayıtlar için kendiliğinden yeniden deneme (arka plan süpürücüsü). Details "Kaydı yeniden dene" düğmesi r4'te
  FU02'den FU01'e alındı (F-3@bdea9ecf; DL-1 + DL-3); erteleme maliyeti Bölüm 8.3 Sahipsiz kayıtlar'dadır.
- Servis kimliği token'ına geçiş (MOD-0033-FU02) — S-1 kararı MVP-2 için kullanıcı token'ı; geçiş ayrı iş, sahip/tarih TBD (Ali —
  Auth).
- **İzleme raporu (r3, F-8):** mal kabul rolündeki kullanıcıların `GOODS_RECEIPT_PO` / `REVERSAL` dışındaki INVENTORY hareketleri —
  merkezi denetim kayıtlarından aylık rapor; sahip/tarih TBD (Ali — Auth ile birlikte).
- `GoodsReceived` olayının gerçek event bus'a yayını (ana paket §16).
- PRODUCT-MASTER gerçek doğrulayıcısı (`PermissiveProductReferenceValidator`).

## 21. Audited Events

Servisin bugünkü durumu (`tests/architecture/audit-ledger/Diten.ProcurementService.md`): kabul edilmiş iz
`procurement-merkezi-iletim` (yol `b`, `:12`; `Program.cs:39` `AddCentralAudit<ProcurementCentralAuditScope>(…, RequireEnlistment = true)`).
GRN'in dört komutu "Bilinen borç"ta (`:36`, `:48`, `:54`, `:56`). Bu FU dördünün davranışını değiştirdiği için (geçiş kuralı,
module-pack-standard §10.1) dördü de işaretlenir; borç listesi dört satır küçülür. Yeni `ResolveGrnCompensationCommand` (r3) ve
`RetryGrnPostingCommand` (r4) ilk gününden işaretlidir, borca girmez. Stok ve lot yazımları ayrıca INVENTORY'de merkezi denetimli (`PostMovementCommand.cs:14-17`,
`CreateLotCommand.cs:9-12`).

**Mekanizma (r3, F-9 — ölçüldü):** yol `b`'de kayıt serbest bir olay adı taşımaz; komut `ICentralAuditDeclaration` ile
`CentralAuditMetadata(Category, Operation, EntityType, SourceModule)` bildirir
(`services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit.Abstractions/ICentralAuditDeclaration.cs:13-30`). Kategori ve
işlem Platform `AuditCategory` / `AuditOperation` adlarıdır; satın alma kategorisi yok → `MasterData` (MOD-0140 ve INVENTORY seçimi,
`SupplierCentralAudit.cs:5-15`). Kaynak modül: GRN'in kendi manifest'i yok; öneri `procurement-grn` (Platform alanı serbest metin
saklar, `PlatformAuditAppendController.cs:106`); sabitler `GrnCentralAudit.cs`'de (emsal `SupplierCentralAudit`). Kayıt istek
gövdesini taşımaz, nesneyi adlandırır (AUD-001 §3). r2'deki `procurement.grn.recorded` / `procurement.grn.posting-failed` gibi adlar
bu mekanizmada yoktur ve kaldırıldı: **başarısız yanıtla biten komut merkezi kayıt bırakmaz** (`CentralAuditBehavior.cs:8-13`;
işlemle yazılmış satır `Abandoned` kalır ve teslim edilmez — `CentralAuditRecorder.cs:39-50`).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| RecordGrnCommand | `Create` · `GoodsReceipt` (yalnız `Posted` sonucu) | GoodsReceipt | b | Merkezi kayıt alan taşımaz (nesne kimliği = GoodsReceipt.Id). Sonra-durum GRN'in kendi alanlarında: GrnId, PoId, WarehouseId, LocationId, Status, PostingState, PostingAttempt, satır başına ItemId/SkuId/Quantity/UomId/LotId/InventoryTransactionId, PostingHistory | — |
| ReverseGrnCommand | `LifecycleTransition` · `GoodsReceipt` (`Reversed` sonucu) | GoodsReceipt | b | Önce/sonra: Status; satır başına ReversalTransactionId (GRN'in kendi alanları) | — |
| DeleteGrnCommand | `Delete` · `GoodsReceipt` | GoodsReceipt | b | Önce: GrnId, Status, PostingState (soft delete; belge kalır) | — |
| BulkDeleteGrnCommand | `Delete` · `GoodsReceipt` (komut başına tek kayıt; emsal `BulkDeleteSupplierCommand`). **r4 (F-8@bdea9ecf):** silinen kayıt sayısı N ≥ 1 → N'den bağımsız tam **bir** satır, nesne kimliği boş (`CentralAuditEntityId => null`); N = 0 → yazım yok, enlist yok, satır yok (emsal `TenantSharedSupplierStore.cs:289-304`) | GoodsReceipt | b | silinen kayıtlar `IsDeleted` / `DeletedAt` ile kendi koleksiyonunda; merkezi satır silinen kimlikleri taşımaz | — |
| ResolveGrnCompensationCommand (r3) | `LifecycleTransition` · `GoodsReceipt` (`ManuallyResolved`) | GoodsReceipt | b | Önce/sonra: PostingState; ResolvedBy, ResolvedAt (GRN'in kendi alanları); not metni merkezi kayda girmez | — |
| RetryGrnPostingCommand (r4) | `Create` · `GoodsReceipt` (yalnız `Posted` sonucu) — GRN'in ilk başarılı kaydı hangi komutla biterse `Create` onundur; `Posted` GRN başına tam bir `Create` (form yolu yarıda kaldıysa `RecordGrn` kayıt bırakmamıştır) | GoodsReceipt | b | `RecordGrnCommand` satırıyla aynı sonra-durum alanları; aktör yeniden deneyen kullanıcı | — |

- **Başarısız / yarım kalan akışların izi (r3, F-9):** Draft yazımı, satır post'ları, telafiler, `CompensationBlocked` ve `Uncertain`
  başarısız yanıtla (422 / 409 / 503) biten komuttur → merkezi GRN kaydı yoktur (BL-501 tasarımı). İzleri iki yerde durur:
  (1) GRN'in kendi alanları (`PostingHistory`, `ReceivingExceptions`, `CompensationBlockedCode`, `CompensationStartedAt`,
  `CompensationBlockedAt`) — modülün ürün içi geçmişi; (2) INVENTORY ve TRACE'in kendi merkezi kayıtları — her hareket ve telafi
  REVERSAL'ı `Create` · `InventoryTransaction`, her lot `Create` · `Lot`, aktör aynı kullanıcı. Stok etkisi olan her adım merkezi
  kayıttadır; GRN'in kendi durum geçişi yalnız başarıda.
- **Outbox atomikliği (AC-24):** GRN deposu son durum yazımını (Posted / Reversed / silme / ManuallyResolved) bir Mongo işleminde
  yapar ve işlem içinde `CentralAuditTransaction.EnlistAsync`'i çağırır; yalnız bu son yazım enlist eder (aynı komutun ilk enlist'i
  kazanır, sonraki çağrı etkisizdir — `CentralAuditRecorder.cs:11-12`), ara yazımlar etmez. Outbox yazılamazsa son durum yazımı da
  geri alınır (kapalı-başarısız). `RequireEnlistment` gereği enlist etmeyen başarılı komut (ör. `Completed` replay) kayıt bırakmaz
  (`CentralAuditRecorder.cs:160-168`) — beklenen davranış. Bugünkü `GrnRepository` işlem kullanmıyor (`GrnRepository.cs:77-90` tek
  `ReplaceOne`); işlemli yazım DL-1 işidir (Bölüm 5).
- **Kayda yazılmayan alanlar:** `Authorization` token'ı, `Idempotency-Key` değeri (yalnız varlığı), yukarı akış yanıt gövdelerinin
  ham metni, çözüm notu.
- **K2 sınıfı (S-12):** GxP mal kabul kaydı sayılırsa K2; yol `b` + `RequireEnlistment` başarılı komut için kapalı-başarısızdır.
  Başarısız akış için ayrıca merkezi kayıt gerekip gerekmediği CT'nin S-12 cevabıdır. **Paket önerisi (r4, F-9@bdea9ecf; DL-1
  kapısında onay satırıyla kabul edilir):** GRN komutları K2 sayılır; başarılı komutun kaydı veriyle aynı işlemde yazılır (AC-24);
  başarısız akış için ek bir GRN merkezi kaydı açılmaz — stok ve lot etkili her adım INVENTORY / TRACE merkezi kaydındadır ve GRN'in
  kendi alanları yarım akışı taşır (yukarıdaki "izi" maddesi). Bu öneri DL-1 kodunu değiştirmez; reddedilirse başarısız akış kaydı
  ayrı bir tasarım ister (yol `b` başarısız komut kaydı bırakmaz).

## 22. Bağımlılık kapıları (dilim başına)

| Dilim | Başlamadan önce |
|---|---|
| DL-1 | C-1 registry satırı; C-2 r4'ün bağımsız incelemesi + onay satırı — **onay satırı notu S-4 önerisini ve O-R3-1, O-R3-2, O-R3-3 önerilerini açıkça kabul eder ya da Beste bunları ayrıca cevaplar; biri açık kalırsa DL-1 başlamaz** (r3, F-5). **r4 (F-9@bdea9ecf): onay satırıyla kabul edilecek öneriler — S-9 (Http modunda geçersiz ya da eksik `Inventory:ReferenceBaseUrl` açılışı durdurur; Bölüm 8.1, AC-30) ve S-12 (GRN komutları K2; başarısız akış için ek GRN merkezi kaydı yok; Bölüm 21). Onay satırı notu ikisini de açıkça kabul eder ya da Beste (S-9) / CT (S-12) ayrıca cevaplar; biri açık kalırsa DL-1 başlamaz.** S-1, S-2, S-5 r2'de kapandı; C-4 defter adımı DL-1 PR'ında. **S-3 DL-1'i bekletmez** — yalnız sözleşme PR'ını (C-3) ve DL-4'ü |
| DL-2 | DL-1 kabul; S-8 cevaplı |
| DL-3 | DL-1 kabul (hata kodları sabit) |
| DL-4 | DL-1, DL-2, DL-3 kabul; C-3 (GRN-EVENT 1.1.0, S-3) main'de; C-5 rol izinleri (`inventory.locations.read` dahil; doğrulama kullanıcısında r4: `trace.serials.read`) test kiracısında verilmiş; lotlu + lotsuz birer SKU, **seri kontrollü bir SKU (r4, AC-35)**, aktif depo + altında stoklanabilir konum (LOCATION) hazır; filo ayakta |

## 23. Açık sorular

r3 durumu (2026-11-25): S-1, S-2 ve S-5 Beste tarafından cevaplandı ve kapandı (karar kaydı
`docs/records/decisions/2026-11/mod-0142-fu01-owner-decisions-01.md`). Açık: S-3 (yalnız sözleşme PR'ı C-3 ve DL-4), S-4 (paket
önerisi), S-6…S-12 ve r3'ün yeni soruları O-R3-1…O-R3-3. DL-1'i bekletenler S-4 ve O-R3-1…O-R3-3'tür; onay satırı notu paketin
önerilerini açıkça kabul ederse ya da Beste ayrıca cevaplarsa kapanırlar (Bölüm 22; r3, F-5).

r4 durumu (2026-11-25): yeni sahip sorusu yok. S-4 ve O-R3-1…O-R3-3 açık ve metinleri değişmedi. S-9 ve S-12 artık DL-1 kapısındadır:
ikisi de paketin önerisiyle yazılı, onay satırıyla kabul edilecek önerilerdir (Bölüm 22; F-9@bdea9ecf). r4'ün yeni ucu (yeniden
deneme) mevcut `procurement.grn.create` iznini kullanır ve ayrı bir sahip sorusu açmaz (Bölüm 14).

**Beste'ye (MVP-2 sahibi, MOD-0142):**
- **S-1 — kimlik yayılımı — Kapandı: Evet (2026-11-25).** GRN kullanıcısının token'ı + rolüne `inventory.movements.create`,
  `trace.lots.create`, `trace.lots.read`; çağrılar Gateway üzerinden (SupplyChain `InventoryMovementClient` emsali). Görev ayrılığı
  riski (aynı kullanıcı INVENTORY'de doğrudan hareket post edebilir) kabul edildi; servis token'ı (MOD-0033-FU02) ayrı iş. Rol
  ataması C-5. İşlendiği yerler: Bölüm 14, 0.3, 20, 24.
- **S-2 — kesin rette telafi — Kapandı: A, hemen geri alma (2026-11-25).** Bu denemede post edilmiş satırlar aynı işlem akışında
  REVERSAL ile telafi edilir; GRN `Posted` olmaz, `Draft` + `Compensated` (net stok 0); yarım GRN stokta görünmez. B seçilmedi.
  İşlendiği yerler: Bölüm 1, 8.2 adım 5, AC-5, AC-8.
- **S-3 — GRN-EVENT 1.1.0 additive** (403/409/503 + yeni kodlar `IDEMPOTENCY_KEY_REUSED`, `INVENTORY_FORBIDDEN`,
  `GRN_POSTING_UNCERTAIN`, `LOT_RESOLUTION_FAILED`; r3: `GRN_POSTING_IN_PROGRESS`, `GRN_COMPENSATION_BLOCKED`,
  `GRN_POSTING_ATTEMPTS_EXHAUSTED` + geçirilen yukarı akış kodları; r3: `resolveGrnCompensation` işlemi ve isteğe bağlı `postingState`
  alanı; r4: `retryGrnPosting` işlemi — Bölüm 15.1). Onay? **r3 (F-5):** yalnız sözleşme PR'ını (C-3) ve DL-4'ü bekletir; DL-1 kodu beklemez.
- **S-4 — mevcut lot — paket önerisi, onay satırıyla kabul edilecek (r3, F-5; DL-1'i bekletir):** TRACE 409 `LOT_ALREADY_EXISTS`
  lotId vermiyor. Öneri: aynı SKU + aynı lot numarası (büyük/küçük harf farkı yok sayılır) ikinci teslimatta **yeniden kullanılır**,
  reddedilmez; mevcut lot `listLots` ile birebir eşleşmeyle bulunur (Bölüm 8.2 adım 3, AC-4; `trace.lots.read` gerekir). Gerekçe:
  TRACE iş anahtarı Tenant + SKU + LotNumber ve kiracı geneli (`LotIdentityPolicy.cs:12-13`; `Lot.cs:8-9`); aynı tedarikçi lotunun
  iki teslimatla gelmesi olağandır. Ret istenirse AC-4 değişir (422 `LOT_ALREADY_EXISTS` kullanıcıya geçirilir). `q` aramasının
  davranışı r3'te koddan ölçüldü (içerir, harf duyarsız, üretici / tedarikçi lotunu da eşler); birebir süzgeç istemcide.
- **S-5 — `locationId` — Kapandı: zorunlu ve listeden seçilir (2026-11-25).** Serbest metin depo / konum yerine şirketin LOCATION
  kayıtlarından seçim; INVENTORY giriş hareketinin `toLocationId` zorunluluğuyla tutarlı. Seçici FU02'ye değil FU01'e (DL-3) girer;
  API ayrıca LOCATION'da doğrular (DL-1). Gereği: GRN rolüne `inventory.locations.read` (Bölüm 14, C-5). Depo varsayılan mal kabul
  konumu FU02 adayı kalır. İşlendiği yerler: Bölüm 2, 3, 5, 7, 8.2 adım 2a, 11, 12, 13, 15, AC-14, AC-18, AC-19, 17.

- **O-R3-1 — konumla ilgili iki türetme (r3, F-7; DL-1'i bekletir).** S-5'te "konum zorunlu, listeden seçilsin" dediniz. Paket
  bundan iki şey çıkardı: (1) sistem kaydetmeden önce konumu konum kayıtlarında da kontrol eder (yanlış ya da pasif konumda kayıt hiç
  açılmaz, stok ve lot oluşmaz); (2) mal kabul rolü konum listesini okuyabilmek için `inventory.locations.read` iznini alır. Öneri:
  ikisi de kabul. Küçük fark: stok servisi şirket–konum eşleşmesini bir ayara bağlı kontrol ediyor (yalnız test ortamında
  kapatılabiliyor); mal kabul her ortamda kontrol eder. Uygun mu?
- **O-R3-2 — geri alma takılırsa çıkış (r3, F-2; DL-1'i bekletir).** Kesin hatada yazılan satırlar geri alınıyor (S-2). Geri alma da
  bir satırda reddedilirse (ör. kalite kararı başlamış, izin kaldırılmış) kayıt "geri alma engellendi" durumunda kalır ve o satırın
  stoğu sistemde görünür. Öneri: (a) engel kalkınca kullanıcı aynı kaydı yeniden gönderir, sistem geri almayı tamamlar; (b) engel
  kalkmayacaksa ters kayıt yetkisi olan kişi not yazarak "elle çözüldü" işaretler (yeni izin açılmaz, mevcut `procurement.grn.reverse`
  kullanılır). Elle çözülen kayıt silinemez ve yeniden gönderilemez. Ayrıca aynı kayıt en çok 5 kez yeniden denenebilir. Uygun mu;
  çözme yetkisi ters kayıt yetkilisinde mi olsun?
- **O-R3-3 — kime ve nasıl haber verilir (r3, F-2; DL-1'i bekletir).** Öneri A: bu işte kaydeden kişiye anında uyarı, kayıt
  detayında durum rozeti ve çöz düğmesi, sistem günlüğüne alarm satırı; yetkili kişiye görev / bildirim (Görev Merkezi) sonraki
  pakette (FU02). B: bildirim bu pakette (Görev Merkezi bağlantısı henüz hiçbir modülde kurulmadığı için iş büyür). Öneri A. Hangisi?

**Beste'ye (DL-2 / DL-4'ü bekletir):**
- **S-6 — lot tarihleri:** GRN v1'de üretim/son kullanma/tedarikçi lotu alanı yok; lot tarihsiz açılır. INVENTORY'nin raf ömrü
  kancası tarihsiz lotta `NotApplicable` der (`inventory-bundle.openapi.yaml:524`). Tarihler sonra TRACE `updateLot` (Draft lot)
  ile mi girilecek, yoksa GRN'e additive alan mı (FU02)?
- **S-7 — `SkuLevel=Item`:** GRN-EVENT SkuLevel `[Gsku, Lsku, FinishedGood]` (`grn-event.openapi.yaml:60`); hammadde/ambalaj
  (`Item`) GRN ile alınamıyor. Additive enum değeri tüketicileri (MOD-0143) etkileyebilir — FU02 mi, şimdi mi?
- **S-8 — kısmi ters kayıt:** satır k'da kesin ret gelirse GRN `Posted` kalır, ters çevrilen satırlar kayıtlı, aynı anahtarla
  devam. Bu yeterli mi, yoksa ekranda "kısmen ters çevrildi" göstergesi (additive alan) gerekir mi?
- **S-9 — ReferenceBaseUrl yoksa (r4: DL-1 kapısında, onay satırıyla kabul edilecek öneri — F-9@bdea9ecf):** öneri açılışta hata
  (erken); emsal çağrı anında 503 veriyor. Paket önerisi: açılışta hata — yanlış yapılandırılmış servis hiç ayağa kalkmaz, kullanıcı
  mal kabulü yarıda bırakan bir 503 görmez (Bölüm 8.1, AC-30). Hangisi?
- **S-10 / S-11 — MOD-0175 karantina etkileşimi** (Bölüm 19.2): kilit davranışı r3'te koddan ölçüldü, sıklığı canlıda ölçülecek.
  Telafide kilit → `CompensationBlocked` (r3, F-2); ters kayıtta → S-8 kuralı.

**Control Tower'a:**
- **S-12 — K2 sınıfı (r4: DL-1 kapısında, onay satırıyla kabul edilecek öneri — F-9@bdea9ecf):** GRN komutları GxP kaydı sayılıp
  K2 mi? Yol `b` + `RequireEnlistment` iki durumda da kapalı-başarısız. Paket önerisi: K2; başarısız akış için ek GRN merkezi kaydı
  yok (Bölüm 21).

## 24. CT adımları

- **C-1:** `execution/registries/module-id-registry.md`'ye satır: `MOD-0142-FU01 | GRN Inventory Posting and Lot Creation |
  grn-inventory-posting-lot-creation | Follow-up | draft | | MOD-0142 | procurement | DCP-002 --check-id … --parent MOD-0142 OK
  (2026-11-25); pack execution/domains/procurement/module-packs/MOD-0142-FU01-grn-inventory-posting-lot-creation.md; sahip Beste
  (MVP-2).` (Bu paket registry'yi değiştirmez.)
- **C-2:** bağımsız inceleme (CT-APR-02, `reviews.tsv` + bulgu dosyası) → onay satırı (CT-APR-01, yüksek risk; onaylayıcı Beste,
  paketi yazmamış olmalı).
- **C-3:** GRN-EVENT 1.1.0 sahip onayı + sözleşme dizini satırı.
- **C-4:** `tests/architecture/audit-ledger/Diten.ProcurementService.md` "Bilinen borç"tan `BulkDeleteGrnCommand`,
  `DeleteGrnCommand`, `RecordGrnCommand`, `ReverseGrnCommand` çıkar (DL-1 PR'ında; test zorlar).
- **C-5:** test kiracısında GRN rolüne `inventory.movements.create`, `trace.lots.create`, `trace.lots.read` (S-1 kararı, 2026-11-25)
  ve `inventory.locations.read` (S-5 kararından türetildi; O-R3-1) (+ doğrulama kullanıcısına `inventory.stock.read` ve r4:
  `trace.serials.read` — yalnız DL-4 kanıtı, AC-35) — rol
  yapılandırması (kod değil). Rol şablonu notu (r3, F-8): bu izinler yalnız mal kabul (GRN) rolündedir; depo yöneticisi / stok
  düzeltme rolünden ayrık şablon, iki rol varsayılan olarak aynı kişiye atanmaz.
- **C-6:** ana pakete (`MOD-0142-receiving-grn.md` §19/§20) "gerçek entegrasyon MOD-0142-FU01'de; 'GRN kodu değişmez' varsayımı
  B-1…B-6 ile düzeltildi" notu.
- **C-7:** kod gelince `execution/registries/module-implementation-status.md` MOD-0142 satırı; MVP-2 CT kuyruğuna DL-1…DL-4
  satırları.
- **C-8 (r3):** MOD-0143 sahibine bilgi notu (Draft durumları çoğaldı, eşleme yalnız Posted — etki yok); operasyon izlemesine
  `GRN compensation blocked` hata satırı için alarm (Bölüm 8.3 Eskalasyon); r4: `GRN_POSTING_UNCERTAIN` ile biten kayıtlar için de
  günlük satırı ve alarm (yarıda kalmış kayıt Details'ten yeniden denenene kadar; Bölüm 8.3 Sahipsiz kayıtlar).

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | Çekirdek: HTTP istemcileri (LOCATION doğrulaması + TRACE createLot + INVENTORY postMovement), `Inventory:Mode`, ayarlar, Draft-önce durum makinesi (devam + kira ve kira yenileme + telafi + telafi engeli + elle çözüm ucu + kayıttan yeniden deneme ucu — r4), hata eşlemesi, silme kuralı ve silinmiş kaydın anahtarı (r4), altı komutun denetimi (GRN-EVENT 1.1.0 ayrı sözleşme PR'ı, C-3) | Owned Objects, Entity Fields, Repo Scope, Dependencies, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | REVERSAL eşlemesi: INVENTORY zarfı, satır satır ters kayıt kimliği, kararlı tarih/anahtar, kısmi ters kayıtta devam | Runtime Constraints, Failure Path to Verify, Implementation Notes, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | Web: anahtarın yalnız belirsiz sonuçta korunması (r4), depo / konum seçicisi (S-5), yeni hata kodlarının 7 dilde gösterimi, Details durum rozeti ve çöz formu (r3), Details "Kaydı yeniden dene" düğmesi (r4) | Layout & Shell Contract, Frontend File Contract, Validation Rules, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | plan satırı yok |
| DL-4 | Canlı uçtan uca kanıt (Gateway 5000; getBalance/getTransactions, tekrar, 422/503, lot, ters kayıt, seri kontrollü satır — r4) | Acceptance Criteria, Test Expectations | E5 | plan kimliği yok | plan satırı yok |

Kabul ölçütleri (r2'de bölüm adlarından ayrıldı; pack_readiness R1): DL-1 → AC-1…AC-8, AC-10, AC-11, AC-17, AC-18, AC-20…AC-28
(r3), AC-30, AC-31, AC-33, AC-34 (r4); DL-2 → AC-9 (Implementation Notes 19.2); DL-3 → AC-16, AC-19, AC-29 (r3), AC-32 (r4); DL-4 →
AC-12…AC-15, AC-35 (r4).

## Değişiklik günlüğü

- **r4 (2026-11-25) — r3 bağımsız incelemesinin bulguları F-1…F-9 (`bdea9ecf`), taslak.** Yazar: module-pack-author ajanı (Claude,
  CT oturumu); onaylayıcı: Beste (yazar ≠ onaylayıcı). Kaynak: `docs/records/reviews/MOD-0142-FU01/2026-11-25-bdea9ecf.md` (r3, LF
  sha256 `bdea9ecf…`, KOŞULLU); ölçüm tabanı değişmedi: `origin/main` `82af5c24a`. r3 onaylanmadı; r4 onun yerine geçer. Değişenler:
  künye `revision`; başta r4 notu; 1; 2; 3 (`RetryGrnPostingCommand`, yeni uç, izin); 4 (`RequestFingerprint` eski kayıt notu, yeni
  `IdempotencyKey` satırı, kira yenileme); 5 (yeni dosyalar; `GrnIndexConfiguration.cs` değişmez; Web anahtar kuralı); 8.1 (3 kat
  kuralının gerekçesi, AC-30 bağı, S-9 öneri notu); 8.2 (adım 2 sıralı (i)…(ix): silinmiş kayıt, parmak izsiz eski kayıt, parmak izi
  `Completed`'den önce, index çakışmasında tek yeniden okuma; 2b çözme kirası ve kira yenileme; yeni 2c yeniden deneme; 5e konum
  pasifleşmesi ve Details çıkışı; Silme: silinmiş kaydın anahtarı ve seçenek gerekçesi); 8.3 (Sahipsiz kayıtlar: Details çıkışı ve
  erteleme maliyeti); 11 (anahtar yalnız belirsiz sonuçta korunur, `IdempotencyBodyHash`, Details yeniden dene); 13 (yedi satır, iki
  senaryo); 14 (yeniden deneme izni); 15 ve 15.1 (`retryGrnPosting`, yeni 409'lar); 16 (AC-7, AC-9, AC-11, AC-16, AC-21, AC-22, AC-23,
  AC-24, AC-29 güncellendi; yeni AC-30…AC-35); 17 (r4 vakaları, S30…S44, DL-4'e AC-35); 18; 19.1 (`serialIds`); 19.4 (başlık,
  `F-1@bdea9ecf`…`F-9@bdea9ecf` satırları); 20 (Details düğmesi FU02'den çıktı, süpürücü FU02); 21 (`RetryGrnPostingCommand`
  satırı, BulkDelete satır sayısı, S-12 önerisi); 22 (DL-1 kapısına S-9 ve S-12 önerileri; DL-4'e seri kontrollü SKU); 23 (r4 durumu,
  S-3 listesi, S-9 ve S-12 önerileri); 24 (C-5 `trace.serials.read`, C-8 alarm); Teslim dilimleri (adlar, AC eşlemesi). Değişmeyenler:
  S-1, S-2, S-5 kararları; S-4 ve O-R3-1…O-R3-3 (açık, metinleri aynı); izin anahtarları; Gateway; INVENTORY / TRACE / LOCATION
  kodu ve sözleşmeleri; index tanımları. Yeni sahip sorusu yok. r4 kod izni vermez; CT-APR-02 ve CT-APR-01 gerekir.
- **r3 (2026-11-25) — bağımsız inceleme bulguları F-1…F-13, taslak.** Yazar: module-pack-author ajanı (Claude, CT oturumu);
  onaylayıcı: Beste (yazar ≠ onaylayıcı). Kaynak: `docs/records/reviews/MOD-0142-FU01/2026-11-25-4803c8de.md` (r2, LF sha256
  `4803c8de…`, UYGUN DEĞİL); ölçüm tabanı tek: `origin/main` `82af5c24a`. r2 onaylanmadı; r3 onun yerine geçer. Değişenler: künye
  `revision`; başta r3 notu ve tek ölçüm tabanı; 0.3 (MOD-0143, MOD-0175, MOD-0173-FU02, Platform satırları; GRN-EVENT satırı); 1;
  2; 3 (yeni alanlar, `ResolveGrnCompensationCommand`, yeni uç, `PostingState` yanıt alanı); 4 (yeni alanlar; parmak izine kullanıcı;
  ms kırpma); 5 (yeni dosyalar, Details); 8.1 (`Grn:PostingLeaseSeconds`, `Grn:MaxPostingAttempts`); 8.2 (adım 2 yeni durumlar, 2a
  politika farkı, yeni 2b kira, 3 lot çözüm kuralları, 4 kısmi yazım, 5a–5f telafi tarihi + `CompensationBlocked` + çıkış yolları,
  Değişmez, Silme); 8.3 (Tarihler, Kiracı / LE, Eskalasyon, Sahipsiz kayıtlar); 11 (Details rozeti, yeni kodlar); 12 (iki satır); 13
  (telafi aşaması, iki 409 ayrımı, yeni senaryolar); 14 (çözme izni, O-R3-1 işareti, rol şablonu notu, servis kimliği TBD, izleme);
  15 ve 15.1 (çözme rotası, additive kapsam, S-3 yalnız C-3); 16 (AC-4, AC-5, AC-6, AC-8, AC-9, AC-10, AC-11, AC-16, AC-17
  güncellendi; yeni AC-20…AC-29); 17 (r3 vakaları, sabotaj S15…S29); 18; 19.2 (S-10 / S-11); 19.3 (r3 ölçümleri); yeni 19.4 (kapanış
  tablosu); 20; 21 (gerçek merkezi denetim mekanizmasıyla yeniden yazıldı, beşinci komut); 22 (DL-1 kapısı S-4 ve O-R3 önerilerine
  bağlandı, S-3 çıkarıldı; DL-4'e C-3); 23 (S-3, S-4, S-10 / S-11, yeni O-R3-1…O-R3-3); 24 (C-5 rol şablonu, yeni C-8); Teslim
  dilimleri (DL-1, DL-3 adları; AC eşlemesi). S-1, S-2, S-5 kararları ve AC-1…AC-3, AC-7, AC-12…AC-15, AC-18, AC-19 değişmedi. r3 kod
  izni vermez; CT-APR-02 ve CT-APR-01 gerekir.
- **r2 (2026-11-25) — sahip kararları S-1, S-2, S-5, taslak.** Yazar: module-pack-author ajanı (Claude, CT oturumu); onaylayıcı:
  Beste (yazar ≠ onaylayıcı). Kaynak: `docs/records/decisions/2026-11/mod-0142-fu01-owner-decisions-01.md`; ölçüm tabanı HEAD
  `32ce7df6c` (r1 atıfları `22d30cc75`'te kaldı). r1 (`9eb3c649a`, LF sha256 `d6a16816…`) incelenmedi ve onaylanmadı; r2 onun yerine
  geçer. Değişenler: künyeye `revision`; başta r2 notu; Bölüm 0.3 (LOCATION sözleşmesi, Gateway satırları), 1 (S-2, S-5 özeti), 2
  (kapsam: seçici FU01'e girdi), 3 (`ILocationReferenceClient`, Web seçici proxy'leri), 5 (yeni dosyalar), 7 (LOCATION 1.1.0
  bağımlılığı, Gateway), 8.1 (Mode LOCATION'ı da seçer), 8.2 (yeni adım 2a, adım 5'te S-2 kararı), 11 (depo / konum seçicisi), 12
  (WarehouseId, LocationId satırları), 13 (üç LOCATION satırı, yeni senaryo), 14 (S-1 kararı, `inventory.locations.read`, kabul
  edilen risk), 15 ve 15.1 (LOCATION rotaları ve kodları), 16 (AC-1, AC-2, AC-5, AC-14, AC-17; yeni AC-18, AC-19), 17 (LOCATION ve
  Web test maddeleri, sabotaj S12–S14), 18 (S-1 / S-2 / S-5 işaretlendi), 19.3 (r2 ölçümleri), 20 (FU02 adayı, servis token'ı), 22
  (DL-1 ve DL-4 kapıları), 23 (S-1, S-2, S-5 "Kapandı"), 24 C-5; Teslim dilimleri tablosunda "kapsadığı bölümler" yalnız başlık
  adlarıyla yazıldı ve AC eşlemesi tablonun altına alındı (r1'deki beş R1 ENGEL'i giderildi). S-3, S-4, S-6…S-12 ve diğer davranış
  (durum makinesi, REVERSAL, denetim satırları, izin anahtarları) değişmedi. r2 kod izni vermez; CT-APR-02 ve CT-APR-01 gerekir.
- **r1 (2026-11-25, `9eb3c649a`)** — ilk taslak.
