# SCAN-BUNDLE v1 — proposal (MOD-0182 Barcode/RFID / Barkod – RFID)

> **Durum: TASLAK (proposal) — sözleşme değil. Sürüm `1.0.0-draft`.** `docs/analysis/contracts/` altında SCAN-BUNDLE yok (bu dosya ilk
> taslaktır). Blueprint 8.8 satır 183 sözleşmeyi "SCAN-BUNDLE (scan event schema, device identity, offline buffer/replay contract)" diye
> adlandırır. **Dondurma ayrı CT adımıdır** (paket §5, §18; WP-0182-DL1 §4.2, B-2): geliştirici bu dosyayı dondurmaz, `FROZEN` işareti koymaz.
> Dondurmada `docs/analysis/contracts/scan-bundle.openapi.yaml` (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0182`,
> `x-owner-service: Diten.InventoryService`) yazılır; donduktan sonra yalnız eklemeli değişiklik. **Mobil sözleşme notu** bu taslaktan CT
> tarafından yazılır (paket §8.16, §18); bu dosya mobil nota dokunmaz.
>
> **Kaynak (davranış otoritesi):** paket `execution/domains/supply-chain-execution/module-packs/MOD-0182-barcode-rfid.md` **r7**
> (LF sha256 `9f8fcc3310e9d69f433a64dcd664ba04480979cea3db714bf0b75bfa5d5ce1e7`, `status: ready-for-dev`, onay satırı
> `execution/approvals/approvals.tsv` — onaylayan Ali / `GMG-ERP` 2026-11-13, commit `e12bf26c8`). Bu dosya yalnız kablo şeklidir; paketle
> çelişirse **paket kazanır** ve bu dosya düzeltilir. İş paketi:
> `execution/domains/supply-chain-execution/work-packs/mvp5-wms/WP-0182-DL1-scan-core-backend.md`. Kararlar:
> `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-03.md` (Q-18 = eşleştirme kodu; Q-16 = yol b, CT).
>
> **Dilim durumu:** paket §3'teki **33 işlemin hepsi** burada. **§4.1 (DL-1, 16 işlem) bağlayıcı taslaktır** — kod bu tabloya karşı yazılır
> ve sözleşme testi (`INVT/Barcode/` altında; emsal `INVT/WavePlanning/WaveDraftContract.cs`) her `/api/warehouse/barcode/*` DL-1 cevabını §3
> şemalarına karşı kapalı (bilinmeyen üye = kırmızı) doğrular (paket AC-1, S-1). **§4.2 (DL-4, DL-6, DL-7; 17 işlem)** paketin §3 / §8'inden
> çıkarılmış **ön taslaktır**; her dilim kendi satırlarını ilk adımında bağlayıcı yapar (yalnız eklemeli). DL-3 ve DL-5 servis ucu eklemez;
> onların kablo ekleri §7'dedir (bilgi).

## 1. Rules

1. Sunucu `/api/warehouse` (Gateway → `Diten.InventoryService` **5067**, mevcut `/api/warehouse/{everything}` rotası; `ocelot.json` değişmez —
   paket §15). Bütün işlemler `/api/warehouse/barcode/…` ailesindedir. Çağıranlar kiracı kabuğu kullanıcılarıdır (`actor_type = tenant_user`):
   Web (same-origin MVC proxy → Gateway) ve mobil uygulama (Gateway).
2. `TenantId` token'dan, şirket `X-Legal-Entity-Id`'den sunucuda çözülür (servisin scope middleware'i; mobil şirket seçimi paket Q-13 — açık).
   Başka kiracı / başka şirket / bilinmeyen id → **404** (varlık açılmaz). `tenantId` / `legalEntityId` gövdede → 400 `INVALID_REQUEST` (istek
   okuyucu), sorguda → 400 `INVALID_REQUEST` (scope middleware) (paket AC-5).
3. Zarf: InventoryService'in ölçülmüş kablosu — başarı gövdesi şemanın kendisi (sarmalanmamış); hata `Error` =
   `{ error: { code, message, details?, correlationId }, contractVersion: "v1" }` (INVENTORY `Error`; WMS-BUNDLE / WMS-COUNT-BUNDLE /
   WMS-WAVE-BUNDLE ile aynı). `error.message` `Accept-Language`'a göre 7 dilde (kural 12).
4. **Boş isteğe bağlı üye yazılmaz** (JSON `null` yok). İstekte `null` isteğe bağlı üyenin yokluğu sayılır. Şemalarda `?` isteğe bağlı üyedir.
5. **Başlıklar:** her istek `Authorization: Bearer`, `X-Legal-Entity-Id`, `X-Correlation-Id` (yoksa sunucu üretir; her cevapta döner),
   `Accept-Language` (kural 12); yazmalarda `Idempotency-Key` (kural 6); değişebilir kayıtta `If-Match` (kural 7); **cihaz başlığı isteyen
   uçlarda** (`recordScan`, `overrideScan`, `recordDeviceHeartbeat`) `X-Scan-Device-Id` (cihaz kimliği, uuid) + `X-Scan-Device-Key` (cihaz
   anahtarı, base64url) (kural 8). Gateway bu başlıkların hepsini iletir (ölçümü DL-8).
6. **Idempotency.** Her yazma `Idempotency-Key` başlığı alır, gövde `idempotencyKey` ile eşit (8–100 karakter; servis geneli desen). **Adlı
   muafiyet:** `recordDeviceHeartbeat` anahtar istemez, gelirse yok sayılır (tekrarı aynı `$set`'tir; kayıt yazmaz). Anahtar isteyen kullanıcı +
   şirket + işlem (`operationId`) kapsamındadır; okutmada (`recordScan`) kullanıcı + şirket (olayın üzerinde). Aynı anahtar + aynı gövde →
   **ilk cevabın durum kodu ve gövdesi** (replay; yeni yazım, merkezi kayıt, numara, sayaç değişimi yok) — sır taşıyan üyeler hariç (kural 9);
   farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. **Sıra (bağlayıcı, paket §8.1):** izin (403) → cihaz kimliği (cihaz başlığı isteyen uçlarda,
   403) → biçim (400) → **anahtar araması** → ancak sonra varlık (404), `If-Match` (409), durum (409), tekillik (409) ve iş kuralları (422 /
   423). Başarılı bir düzenlemenin ya da aşımın replay'i bu yüzden 409 değil ilk cevabı alır. Parmak izi = işlem + hedef + normalleşmiş gövde
   (adlı alanlar üzerinde düz SHA-256); başlıklar girmez; **`pin` hiçbir parmak izine girmez** (alan listesinde yoktur): aynı anahtarla yalnız
   PIN'i farklı gövde **replay'dir** (ilk cevap; PIN değerlendirilmez). Eşleştirme kodu parmak izine yalnız SHA-256 özetiyle girer. Başarısız
   komut anahtar bırakmaz (aynı anahtarla yeniden deneme yeniden çalışır). Çarpışmada (eşzamanlı ikiz) anahtar yeniden aranır: aynı anahtar →
   replay, farklı anahtar → 409. **Replay ayrıntısı (CT kararı 2026-11-13):** (a) replay'de `message` / `noticeMessage` **o anki**
   `Accept-Language`'a göre yeniden üretilir — kodlar, kimlikler ve diğer üyeler ilk cevabınkidir, gövde bayt bayt aynı olmak zorunda
   değildir; (b) replay'in `ETag`'i saklanan gövdedeki `version`'dır (kaydın o anki sürümü değil). 201'in replay'i 201'dir — servisin
   200 emsalinden (WMS-WAVE-BUNDLE) **bilinçli ayrışma**, dondurma notuna yazılır.
7. **If-Match.** Değişebilir kayıt (`ScanDevice`, `BarcodeSettings`) yaratma dışı yazmada `If-Match: W/"<version>"` ister; eksik → 400
   `INVALID_REQUEST` (428 değil), bayat → 409 `BARCODE_CONFLICT`. Cevap `ETag: W/"<version>"`. Bayatlık kural 6'daki anahtar aramasından sonra
   değerlendirilir. **Adlı muafiyetler:** `recordDeviceHeartbeat` (nabız alanları `version`'ı artırmaz; `If-Match` gelirse yok sayılır),
   `pairScanDevice` (kod tek kullanımlıktır; eşleştirme `version`'ı artırmaz), aşım denemesinin PIN sayacı / kilit yazımları ve
   `setSupervisorPin` (kişi yalnız kendi PIN'ini yazar; `SupervisorPin` kablo üyesi olarak `version` taşımaz — §9 açık nokta 2). Ayar kaydı
   yokken `getBarcodeSettings` varsayılanları ve **`version: 0`** döner; ilk yazım `If-Match: W/"0"` ister (WMS-WAVE-BUNDLE emsali).
8. **Cihaz kapısı (paket §8.2).** Cihaz başlıkları yetki değildir — JWT + izin'e **ek** kimlik kapısıdır. Başlık yok / uuid değil, o
   **kiracı + şirkette** cihaz yok, cihazın anahtarı yok (`KeyHash` boş — Web kaydı ya da yeniden etkinleştirme sonrası, eşleşmeden önce) ya
   da anahtarın SHA-256 özeti uyuşmuyor → **403 `BARCODE_DEVICE_KEY_INVALID`**, hiçbir şey yazılmaz (olay yok, merkezi kayıt yok). Başka
   şirkette kayıtlı cihazın kimliği + anahtarı da 403'tür (o şirkette cihaz yok sayılır). Cihaz **durumu** kimlik kapısı değildir: `Pending` /
   `Blocked` cihazın okutması **olay olarak yazılır** (201, `Rejected` `DEVICE_PENDING` / `DEVICE_BLOCKED` — §6.2).
9. **Sırlar.** Cihaz anahtarı (`deviceKey`), eşleştirme kodu (`pairing.code`, `pairing.qrPng`) ve PIN sunucuda yalnız özetiyle (anahtar ve
   kod SHA-256; PIN PBKDF2-SHA256 ≥ 100.000 tur, tuzlu) durur; hiçbir `GET` cevabında, listede, olay verisinde, logda, OTel'de, merkezi
   kayıtta ya da idempotency deposunda yoktur. Anahtar ve kod **yalnız üretildikleri ilk cevapta, bir kez** döner; replay cevabı aynı gövdedir
   ama `deviceKey` / `pairing` üyesi yoktur ve `deviceKeyNotice: "BARCODE_DEVICE_KEY_NOT_REPLAYED"` /
   `pairingCodeNotice: "BARCODE_PAIRING_CODE_NOT_REPLAYED"` + `noticeMessage` (7 dil) taşır. Replay yeni anahtar / kod **üretmez**.
10. **Bu sözleşme stok, rezervasyon, görev, sayım ya da koli yazmaz** (paket §2.3). MOD-0173 / 0174 / 0178 / 0181 verisi süreç içi **yalnız
    `Find`** ile okunur. **Okutma sonucu HTTP hatası değildir:** yanlış ürün / raf, SKT geçmiş … okutmanın **sonucudur** — olay yazılır, 201
    döner, sonuç gövdededir (§6.2). HTTP hataları yalnız geçersiz istek, cihaz kimliği, yetki, kapsam ve erişilemeyen bağımlılık içindir.
11. **PUT / DELETE yok** (paket §2.3 kural 6, AC-25): güncelleme, onay, engelleme ve ayar yazımı `POST` komut uçlarıdır. Silme yoktur — cihaz
    engellenir; okutma olayı ve aşım insert-only'dir.
12. **Dil:** `message`, `noticeMessage` ve `error.message` `Accept-Language`'a göre **7 dilde** (en, tr, fr, es, zh, ar, ru) sunucuda
    üretilir; bilinmeyen / eksik dil `en`. İstemci kod tablosunu kendi çevirisiyle de gösterebilir; kodlar dile bağlı değildir.
13. Tarihler `date-time` = UTC an (`…Z`, milisaniye). Sunucu yalnız kendi zamanını (`receivedAt`) geçerli sayar; `capturedAt` saklanır, karar
    vermez (v1 yalnız çevrimiçi — paket §8.6; çevrimdışı yeniden oynatma FU-0182-OFFLINE'da eklemeli gelir).
14. Yollardaki `{scanId}`, `{deviceId}` `:guid` kısıtlıdır; sabit kesimler (`scan-events/dashboard`, `devices/pair`) parametreli yoldan önce
    eşleşir. `:guid` kısıtına uymayan yol hiçbir işleme eşleşmez: cevap **boş gövdeli 404**'tür (ölçüldü 2026-11-13,
    `GET /barcode/devices/not-a-uuid` — yönlendirici cevabı, `Error` zarfı yok; WMS-WAVE-BUNDLE taslağının "aynı zarf" ifadesi bu
    servis için ölçümle doğrulanmadı).
15. **İzin:** her işlem `x-permission` anahtarını `[HasPermission]` ile zorlar; eksik izin → 403 (gövde servis genelinin yetki cevabı).
16. **Ekranda kimlik (GUID) gösterilmez** (paket §4): şemalar raf kodu, lot numarası, SKU kodu / adı, kişi adı, cihaz adı / numarası taşır.
17. **Kör sayım sızdırılmaz** (paket §2.3 kural 4): hiçbir cevap açık kör sayımın bin'inde beklenen miktarı ya da o bin'in stok miktarını
    vermez (§6.4). Okutma cevabı ve olay listesi **hiçbir bağlamda `expected*` üyesi taşımaz** (§9 açık nokta 5).

## 2. Sabitler

| Sabit | Değer | Kaynak |
|---|---|---|
| Modül / manifest kodu | `MOD-0182` / `BARCODE-RFID` (SortOrder 182) | paket §14 |
| Merkezi denetim kaynak modülü | `BARCODE-RFID` | paket §8.12, §21 |
| Kayıt kodu | `ScanDevice.number` = `{ŞİRKET}-DVC-{000001}` — MOD-0178 geçici sayacı, tür `DVC`; hedef MOD-0040-FU02 | paket §8.13, §22 |
| `deviceType` | `PhoneCamera` \| `HandheldScanner` \| `RfidReader` | paket §4.1 |
| `platform` | `Android` \| `iOS` \| `Other` | paket §4.1 |
| `deviceStatus` | `Pending` \| `Active` \| `Blocked` | paket §4.1, §8.2 |
| `keyDelivery` | `Direct` (varsayılan — çağıran telefonun kendisi) \| `Pairing` (Web proxy **her zaman**) | paket §8.2, §12 |
| Cihaz `name` | trim, 2..80; şirkette `Blocked` olmayan cihazlar arasında tekil | paket §12 |
| Cihaz `installationId` | 8..100, `^[A-Za-z0-9._:-]+$`; şirkette tekil | paket §12 |
| Cihaz `note` | ≤ 500 (trim; boş = yok) | paket §4.1 |
| Engelleme / yeniden etkinleştirme gerekçesi | trim, 10..500 | paket §12 |
| Cihaz anahtarı | 32 bayt rastgele, base64url (43 karakter, dolgusuz) | paket §8.2 |
| Eşleştirme kodu | 32 bayt rastgele, base64url (43 karakter, dolgusuz); **tek kullanımlık**; ömür **10 dk** (`BarcodeContract.PairingCodeLifetimeMinutes = 10`, ayar değil) | paket §8.2, §12 |
| Eşleştirme QR içeriği | `diten-scan-pair:1:{code}` (kiracı / şirket **taşımaz**) | paket §8.2 |
| Nabız `appVersion` / `batteryPercent` | 1..40 / 0..100 | paket §12 |
| `source` | `Scanner` \| `Camera` \| `Rfid` \| `Manual` | paket §4.2 |
| `context.kind` | `Putaway` \| `Pick` \| `Count` \| `Pack` \| `Query` | paket §4.2 |
| `context.step` | `Location` \| `Product` \| `Lot` \| `Serial` \| `Package` (bağlamda geçerli adımlar §6.3) | paket §4.2, §8.4 |
| `outcome` | `Accepted` \| `Warning` \| `Rejected` | paket §4.2 |
| `rawValue` | 1..500; yazdırılabilir karakter + U+001D (GS) (+ baştaki `]xx` sembol tanıtıcısı); başka denetim karakteri 400; **değiştirilmeden** saklanır | paket §12 |
| `context.lineRef` | 1..100 | paket §12 |
| `manual.reasonCode` | `ScannerFault` \| `DamagedBarcode` \| `NoLabel` \| `NoCameraPermission` \| `Other` (`Other`'da `note` 10..500 zorunlu) | paket §8.5 |
| PIN | 4..8 rakam; aynı rakamın tekrarı (`1111`) ve ardışık dizi (`1234`, `4321`) reddedilir | paket §12 |
| Aşım `note` | ≤ 500 | paket §4.3 |
| Ayar varsayılanları / aralıkları | `silentDeviceHours` 24 (1..720), `lowBatteryPercent` 20 (1..90), `pinMaxAttempts` 3 (1..10), `pinLockMinutes` 5 (1..1440), `pinValidityDays` 90 (1..365), `scanSessionMaxMinutes` 30 (1..480) | paket §4.4 |
| Olay listesi tarih aralığı | `from ≤ to`, en çok 31 gün; ikisi de yoksa son 24 saat | paket §8.14 |
| Sayfa | `page` ≥ 1 (varsayılan 1), `pageSize` 1..200 (varsayılan 25) | WMS-BUNDLE emsali |

## 3. Schemas

### 3.1 `ScanDevice`
```json
{
  "deviceId": "uuid", "number": "GMG-DVC-000001", "name": "string",
  "deviceType": "PhoneCamera | HandheldScanner | RfidReader", "platform": "Android | iOS | Other",
  "installationId": "string", "warehouseId": "uuid?", "warehouseCode": "string?",
  "status": "Pending | Active | Blocked",
  "keySet": true, "pairingExpiresAt": "date-time?", "pairedAt": "date-time?",
  "lastSeenAt": "date-time?", "lastUserId": "uuid?", "lastUserName": "string?", "appVersion": "string?",
  "batteryPercent": 0, "batteryReportedAt": "date-time?",
  "blockedReason": "string?", "blockedAt": "date-time?", "blockedBy": "uuid?",
  "note": "string?",
  "health": { "silent": false, "lowBattery": false, "pinLock": false },
  "overrideAttempts": [ { "…": "ScanOverrideAttempt (§3.6)" } ],
  "createdBy": "uuid", "createdByName": "string", "createdAt": "date-time", "updatedAt": "date-time?",
  "version": 1, "contractVersion": "v1"
}
```
- Zorunlu: `deviceId`, `number`, `name`, `deviceType`, `platform`, `installationId`, `status`, `keySet`, `health`, `createdBy`,
  `createdByName`, `createdAt`, `version`, `contractVersion`. Diğerleri isteğe bağlıdır (kural 4; `batteryPercent` yalnız raporlandıysa).
- **Sır yok:** anahtar, anahtar özeti, eşleştirme kodu ve özeti bu şemada **yoktur** (kural 9; paket AC-2, S-2a). `keySet` = cihazın geçerli
  bir anahtarı var (özet dolu) — Web "eşleşme bekliyor" durumunu buradan gösterir; `pairingExpiresAt` yalnız kullanılmamış kod varken yazılır.
- `health` **okuma anında** hesaplanır, saklanmaz (paket §8.2, AC-21): `silent` = `lastSeenAt + silentDeviceHours < şimdi` (hiç görülmemiş
  cihaz `silent` değildir); `lowBattery` = son pil `< lowBatteryPercent` ve rapor `silentDeviceHours` içinde; `pinLock` = son
  `silentDeviceHours` içinde bu cihazdan bir aşım denemesi kilit kurdu (`LockSet`).
- `overrideAttempts` yalnız `getScanDevice` cevabında yazılır (cihazın son 20 aşım denemesi, yeni → eski); listede yoktur.
- `number`, `status`, nabız alanları, `health`, `keySet` sunucu üyesidir; istekte gelirse 400 `INVALID_REQUEST`.
- `version` nabız, okutma ve eşleştirme ile **artmaz** (kural 7).

### 3.2 `ScanDeviceResult` (kayıt, onay / yeniden etkinleştirme, eşleştirme, engelleme, düzenleme cevabı)
```json
{
  "device": { "…": "ScanDevice (§3.1; overrideAttempts yok)" },
  "deviceKey": "base64url?", "deviceKeyNotice": "BARCODE_DEVICE_KEY_NOT_REPLAYED?",
  "pairing": { "code": "base64url", "qrPng": "base64 (image/png)", "expiresAt": "date-time" },
  "pairingCodeNotice": "BARCODE_PAIRING_CODE_NOT_REPLAYED?",
  "noticeMessage": "string?",
  "contractVersion": "v1"
}
```
- Zorunlu: `device`, `contractVersion`. Hangi üye ne zaman (paket §8.2):

| İşlem | İlk cevap | Replay (aynı anahtar + gövde) |
|---|---|---|
| `registerScanDevice`, `keyDelivery = Direct` | `deviceKey` (bir kez) | `deviceKey` yok; `deviceKeyNotice` + `noticeMessage` |
| `registerScanDevice`, `keyDelivery = Pairing` | `pairing` (bir kez); `deviceKey` **yok** | `pairing` yok; `pairingCodeNotice` + `noticeMessage` |
| `activateScanDevice`, `Pending → Active` | yalnız `device` | aynı |
| `activateScanDevice`, `Blocked → Active` | `pairing` (bir kez); eski anahtar o anda geçersiz (`keySet: false`) | `pairing` yok; `pairingCodeNotice` + `noticeMessage` |
| `pairScanDevice` | `deviceKey` (bir kez); `device.keySet: true` | `deviceKey` yok; `deviceKeyNotice` + `noticeMessage` |
| `updateScanDevice`, `deactivateScanDevice` | yalnız `device` | aynı |

- `pairing.qrPng` = ZXing.Net ile çizilmiş QR'ın PNG'si (base64), içerik `diten-scan-pair:1:{code}`; kod QR'ın altında kopyalanabilir metin
  olarak da gösterilir (Web, DL-2). İdempotency deposu bu şemanın **sırsız** hâlini saklar (`deviceKey`, `pairing` o tipte yoktur).

### 3.3 `ScanResult` (`recordScan` cevabı)
```json
{
  "scanId": "uuid", "outcome": "Accepted | Warning | Rejected", "reasonCode": "WRONG_LOCATION?", "overridable": false,
  "message": "string",
  "parsed": { "kind": "Gs1 | Plain", "gtin": "string?", "lot": "string?", "expiryRaw": "string?", "expiry": "date?", "serial": "string?", "sscc": "string?" },
  "resolved": { "locationId": "uuid?", "locationCode": "string?", "skuId": "uuid?", "itemId": "uuid?", "skuLevel": "string?", "skuCode": "string?",
                "skuName": "string?", "lotId": "uuid?", "lotNumber": "string?", "packageId": "uuid?", "packageNumber": "string?" },
  "foundStock": true,
  "query": { "…": "ScanQuerySummary (§3.4)?" },
  "contractVersion": "v1"
}
```
- Zorunlu: `scanId`, `outcome`, `overridable`, `message`, `parsed`, `resolved`, `contractVersion`. `reasonCode` `Accepted`'da yoktur (§6.2
  kodları); `message` her sonuçta vardır (kural 12).
- `overridable = true` **yalnız** `context.kind = Putaway` + `reasonCode = WRONG_LOCATION` + `outcome = Warning` iken (§6.5); başka her
  durumda `false`. `false` iken mobil PIN düğmesi göstermez.
- `foundStock` yalnız `Count` bağlamında, okunan SKU / lot sayımın satırlarında yokken ve `true` olarak yazılır (istemci MOD-0181 "bulunan
  stok" yoluna gider). `query` yalnız `context.kind = Query` iken yazılır.
- `parsed.sscc`, `resolved.packageId` / `packageNumber` DL-5'ten önce yazılmaz (SSCC çözümü DL-5). `parsed.expiry` = `yyyy-MM-dd`.
- **`expected*`, miktar ya da bin stoğu üyesi yoktur** (kural 17; paket AC-14).

### 3.4 `ScanQuerySummary` ("Bu nedir?")
```json
{
  "kind": "Location | Lot",
  "expiry": "date?", "recalled": false,
  "buckets": [ { "status": "Available | Quarantine | Blocked", "quantity": "12.000", "baseUomId": "uuid?" } ],
  "quantitiesHidden": true, "hiddenReason": "BLIND_COUNT_OPEN?"
}
```
- `kind = Location`: okunan bin'in stoğunun statü kovaları. `kind = Lot`: lotun SKT'si (`expiry`), geri çağırma uyarısı (`recalled`) ve
  **depodaki** statü kovaları (lotun o depodaki bütün bin'leri üzerinden toplam). `recalled` yalnız `Lot`'ta yazılır.
- **Kör sayım (kural 17; paket §2.3 kural 4, AC-16):** okunan bin açık kör sayımdaysa (MOD-0181 bin talebi açık + `Blind = true`), ya da lot
  toplamına açık kör sayım bin'inin bakiyesi **katılıyorsa**, `buckets` **yazılmaz**, `quantitiesHidden: true` ve
  `hiddenReason: "BLIND_COUNT_OPEN"` yazılır — bin dışlanıp kalan **gösterilmez**. Gizli değilken `quantitiesHidden` / `hiddenReason` yazılmaz.
- GTIN / SSCC ile sorgu DL-5'tedir (§7); DL-1'de `Query` sırası: bin kodu → lot numarası → `UNKNOWN_BARCODE` (GS1 dizgesinde (10) lot).

### 3.5 `ScanEvent` (liste ve ayrıntı)
```json
{
  "scanId": "uuid", "sessionId": "uuid?", "deviceId": "uuid", "deviceNumber": "string", "deviceName": "string",
  "warehouseId": "uuid", "userId": "uuid", "userName": "string",
  "source": "Scanner | Camera | Rfid | Manual", "rawValue": "string",
  "parsed": { "…": "§3.3 parsed" }, "resolved": { "…": "§3.3 resolved" },
  "context": { "kind": "Putaway | Pick | Count | Pack | Query", "refId": "uuid?", "lineRef": "string?", "step": "Location | Product | Lot | Serial | Package" },
  "outcome": "Accepted | Warning | Rejected", "reasonCode": "string?", "overridable": false,
  "manual": { "reasonCode": "ScannerFault | DamagedBarcode | NoLabel | NoCameraPermission | Other", "note": "string?" },
  "receivedAt": "date-time", "capturedAt": "date-time?",
  "override": { "overriddenReasonCode": "WRONG_LOCATION", "supervisorUserId": "uuid", "supervisorName": "string", "deviceId": "uuid",
                "approvedAt": "date-time", "note": "string?" },
  "overrideAttempts": [ { "…": "ScanOverrideAttempt (§3.6)" } ],
  "contractVersion": "v1"
}
```
- Zorunlu: `scanId`, `deviceId`, `deviceNumber`, `deviceName`, `warehouseId`, `userId`, `userName`, `source`, `rawValue`, `parsed`,
  `resolved`, `context`, `outcome`, `overridable`, `receivedAt`. `contractVersion` yalnız tek kayıt cevabında (`getScanEvent`) yazılır.
- Olay **değişmez** (insert-only): "Aşıldı" hâli olaya yazılmaz; okumada `override` üyesi olarak birleştirilir (olay başına en çok bir).
  `outcome` / `reasonCode` aşımdan sonra da ilk değeridir; ekran `override` varsa "Aşıldı — {supervisorName}" gösterir.
- `manual` yalnız `source = Manual` iken yazılır ("elle" rozeti). `overrideAttempts` yalnız `getScanEvent` cevabındadır (olayın bütün
  denemeleri, eski → yeni). `rawValue` GS (U+001D) dahil saklandığı gibi döner (JSON kaçışıyla). Miktar ve `expected*` üyesi yoktur.

### 3.6 `ScanOverrideAttempt` (başarısız aşım denemesinin yerel izi)
```json
{ "scanId": "uuid", "deviceId": "uuid", "deviceNumber": "string", "attemptedBy": "uuid", "attemptedByName": "string",
  "supervisorUserId": "uuid", "supervisorName": "string", "result": "PinInvalid | LockSet | Locked", "lockedUntil": "date-time?", "at": "date-time" }
```
- `PinInvalid` = yanlış PIN; `LockSet` = bu yanlış deneme kilidi kurdu; `Locked` = kilitliyken deneme. `lockedUntil` yalnız `LockSet` /
  `Locked`'da. **Deneme sayısı / kalan hak ve PIN'in hiçbir türevi yoktur** (paket §8.15, AC-18). Insert-only; merkezi kayıt doğurmaz.

### 3.7 `ScanOverrideResult` (`overrideScan` cevabı)
```json
{ "scanId": "uuid", "override": { "…": "§3.5 override" }, "message": "string", "contractVersion": "v1" }
```
- `message` = "Aşıldı — {supervisorName}" (7 dil).

### 3.8 `ScanEventDashboard`
```json
{ "warehouseId": "uuid", "from": "date-time", "to": "date-time", "scanCount": 0, "rejectedCount": 0, "rejectRatePercent": 0.0,
  "topRejectReason": "string?", "manualCount": 0, "manualRatePercent": 0.0, "contractVersion": "v1" }
```
- Okuma anında hesaplanır (paket §8.14 üst kartlar): pencere = **çağrı anından geriye son 24 saat** (`from` / `to` cevapta; takvim günü değil —
  sahip kararı 2026-11-13, §9); oranlar bir ondalık,
  `scanCount = 0` iken `0.0`; `topRejectReason` yalnız `rejectedCount > 0` iken (eşitlikte alfabetik ilk kod).

### 3.9 `BarcodeSettings`
```json
{ "silentDeviceHours": 24, "lowBatteryPercent": 20, "pinMaxAttempts": 3, "pinLockMinutes": 5, "pinValidityDays": 90,
  "scanSessionMaxMinutes": 30, "updatedAt": "date-time?", "version": 0, "contractVersion": "v1" }
```
- Şirket başına bir kayıt. Kayıt yokken varsayılanlar ve `version: 0` döner (kural 7). `updateBarcodeSettings` **bütün ayarı** yazar (altı
  alanın hepsi zorunlu).
- **DL-3 / DL-5 ekleri (eklemeli, o dilimlerde bağlayıcı):** `scanRequiredWarehouseIds: uuid[]` (DL-3), `ssccCompanyPrefix?` ve
  `ssccExtensionDigit` (DL-5). DL-1'de bu üyeler şemada **yoktur**; istekte gelirse 400 `INVALID_REQUEST` (bilinmeyen üye).
  Eklendiklerinde **isteğe bağlı** istek üyesi olurlar (gönderilmezse mevcut değer korunur): "altı alan zorunlu, bütün ayarı yazar" kuralı
  sonradan gelen üyeler için kırıcı olmaz (CT kararı 2026-11-13).

### 3.10 `SupervisorPin` (`setSupervisorPin` cevabı)
```json
{ "setAt": "date-time", "expiresAt": "date-time", "contractVersion": "v1" }
```
- `expiresAt = setAt + pinValidityDays`. PIN, özeti, tuzu, deneme sayacı **yoktur**.

### 3.11 `DeviceHeartbeat` (`recordDeviceHeartbeat` cevabı)
```json
{ "deviceId": "uuid", "status": "Pending | Active | Blocked", "lastSeenAt": "date-time", "contractVersion": "v1" }
```

### 3.12 Sayfalar
`{ items, total, page, pageSize, contractVersion }`, `pageSize ≤ 200`.

## 4. Operations

### 4.1 DL-1 — bağlayıcı (16 işlem)

| Yöntem + yol | operationId | x-permission | Gövde / parametre | Başarı | x-error-codes |
|---|---|---|---|---|---|
| `POST /barcode/scans` | `recordScan` | `warehouse.barcode.scans.create` | cihaz başlıkları; `{ idempotencyKey, sessionId?, warehouseId, source, rawValue, context: { kind, refId?, lineRef?, step }, manual?: { reasonCode, note? }, capturedAt? }` — `Query` dışında `sessionId`, `context.refId` zorunlu; `Query`'de `sessionId` / `refId` / `lineRef` **üye değildir** | 201 `ScanResult` (replay 201, ilk gövde) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, BARCODE_DEVICE_KEY_INVALID, BARCODE_CONTEXT_NOT_FOUND, UNKNOWN_LOCATION, BARCODE_SESSION_INVALID, BARCODE_INVENTORY_UNAVAILABLE |
| `POST /barcode/scans/{scanId}/override` | `overrideScan` | `warehouse.barcode.scans.create` | cihaz başlıkları; `{ idempotencyKey, supervisorUserId, pin, note? }` | 201 `ScanOverrideResult` (replay 201) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, BARCODE_DEVICE_KEY_INVALID, BARCODE_SCAN_NOT_FOUND, BARCODE_OVERRIDE_NOT_ALLOWED, BARCODE_OVERRIDE_SELF, BARCODE_ALREADY_OVERRIDDEN, BARCODE_PIN_NOT_SET, BARCODE_PIN_LOCKED, BARCODE_PIN_INVALID, BARCODE_INVENTORY_UNAVAILABLE |
| `GET /barcode/scan-events` | `listScanEvents` | `warehouse.barcode.scan-events.read` | `warehouseId` (zorunlu), `from?`, `to?` (`date-time`; ≤ 31 gün), `userId?`, `deviceId?`, `outcome*`, `contextKind*` (tekrarlanabilir), `overridden?` (`true` \| `false`), `manual?` (`true` \| `false`), `page`, `pageSize` (sıra: `receivedAt` azalan) | 200 sayfa `ScanEvent` | INVALID_REQUEST |
| `GET /barcode/scan-events/dashboard` | `getScanEventDashboard` | `warehouse.barcode.scan-events.read` | `warehouseId` (zorunlu) | 200 `ScanEventDashboard` | INVALID_REQUEST |
| `GET /barcode/scan-events/{scanId}` | `getScanEvent` | `warehouse.barcode.scan-events.read` | — | 200 `ScanEvent` (+ `override`, `overrideAttempts`) | BARCODE_SCAN_NOT_FOUND |
| `GET /barcode/devices` | `listScanDevices` | `warehouse.barcode.devices.read` | `status*`, `deviceType*` (tekrarlanabilir), `warehouseId?`, `q?` (ad / numara içerir), `page`, `pageSize` (sıra: `number`) | 200 sayfa `ScanDevice` | INVALID_REQUEST, BARCODE_INVENTORY_UNAVAILABLE |
| `POST /barcode/devices` | `registerScanDevice` | `warehouse.barcode.devices.create` | `{ idempotencyKey, name, deviceType, platform, installationId, warehouseId?, note?, keyDelivery? }` — cihaz **her zaman `Pending`** doğar | 201 `ScanDeviceResult` + ETag (replay 201, sırsız) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, UNKNOWN_LOCATION, BARCODE_DEVICE_ALREADY_REGISTERED, BARCODE_DEVICE_NAME_TAKEN, BARCODE_COMPANY_CODE_INVALID, LEGAL_ENTITY_MASTER_UNAVAILABLE, BARCODE_RECORD_CODE_UNAVAILABLE, BARCODE_INVENTORY_UNAVAILABLE |
| `POST /barcode/devices/pair` | `pairScanDevice` | `warehouse.barcode.devices.create` | **cihaz başlığı yok**; `{ idempotencyKey, pairingCode, installationId }` | 200 `ScanDeviceResult` (`deviceKey` bir kez; replay 200, sırsız) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, BARCODE_PAIRING_NOT_FOUND, BARCODE_PAIRING_EXPIRED, BARCODE_PAIRING_INVALID, BARCODE_INVENTORY_UNAVAILABLE |
| `GET /barcode/devices/{deviceId}` | `getScanDevice` | `warehouse.barcode.devices.read` | — | 200 `ScanDevice` (+ `overrideAttempts`) + ETag | BARCODE_DEVICE_NOT_FOUND, BARCODE_INVENTORY_UNAVAILABLE |
| `POST /barcode/devices/{deviceId}/update` | `updateScanDevice` | `warehouse.barcode.devices.update` | If-Match; `{ idempotencyKey, name, warehouseId?, note? }` — düzenlenebilir alanların tamamı (yok olan isteğe bağlı üye temizlenir); `deviceType` / `platform` / `installationId` üye değildir | 200 `ScanDeviceResult` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, BARCODE_DEVICE_NOT_FOUND, BARCODE_CONFLICT, UNKNOWN_LOCATION, BARCODE_DEVICE_NAME_TAKEN, BARCODE_INVENTORY_UNAVAILABLE |
| `POST /barcode/devices/{deviceId}/activate` | `activateScanDevice` | `warehouse.barcode.devices.activate` | If-Match; `{ idempotencyKey, reason? }` — `Pending → Active` (onay; `reason` üye olabilir, zorunlu değil) ya da `Blocked → Active` (yeniden etkinleştirme; `reason` **zorunlu** 10..500) | 200 `ScanDeviceResult` + ETag (replay 200, sırsız) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, BARCODE_REASON_REQUIRED, BARCODE_DEVICE_NOT_FOUND, BARCODE_CONFLICT, BARCODE_INVALID_STATE, BARCODE_DEVICE_NAME_TAKEN |
| `POST /barcode/devices/{deviceId}/deactivate` | `deactivateScanDevice` | `warehouse.barcode.devices.deactivate` | If-Match; `{ idempotencyKey, reason }` — yalnız `Active → Blocked` | 200 `ScanDeviceResult` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, BARCODE_REASON_REQUIRED, BARCODE_DEVICE_NOT_FOUND, BARCODE_CONFLICT, BARCODE_INVALID_STATE |
| `POST /barcode/devices/{deviceId}/heartbeat` | `recordDeviceHeartbeat` | `warehouse.barcode.scans.create` | cihaz başlıkları (`X-Scan-Device-Id` = `{deviceId}`); `{ appVersion, batteryPercent? }` — `Idempotency-Key` ve `If-Match` **istenmez** | 200 `DeviceHeartbeat` | INVALID_REQUEST, BARCODE_DEVICE_KEY_INVALID |
| `GET /barcode/settings` | `getBarcodeSettings` | `warehouse.barcode.settings.read` | — | 200 `BarcodeSettings` + ETag | BARCODE_INVENTORY_UNAVAILABLE |
| `POST /barcode/settings/update` | `updateBarcodeSettings` | `warehouse.barcode.settings.configure` | If-Match (`W/"0"` kayıt yokken); `{ idempotencyKey, silentDeviceHours, lowBatteryPercent, pinMaxAttempts, pinLockMinutes, pinValidityDays, scanSessionMaxMinutes }` | 200 `BarcodeSettings` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, BARCODE_CONFLICT |
| `POST /barcode/supervisor-pin` | `setSupervisorPin` | `warehouse.barcode.scan-overrides.approve` | `{ idempotencyKey, pin }` — çağıranın **kendi** PIN'i (başka kullanıcı hedeflenemez) | 200 `SupervisorPin` (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED |

**Cihaz kaydı (`registerScanDevice`; paket §8.2, AC-2):** sıra — gövde şekli (400) → anahtar (kural 6) → depo (`warehouseId` doluysa LOCATION'da
`Warehouse`, `Active`, şirketin — aksi 422 `UNKNOWN_LOCATION`; LOCATION okunamazsa 503) → `installationId` şirkette kayıtlı mı (409
`BARCODE_DEVICE_ALREADY_REGISTERED`) → ad tekilliği (409 `BARCODE_DEVICE_NAME_TAKEN`) → şirket öneki → numara (geçici sayaç, işleme katılmaz) → tek
işlem (cihaz + idempotency kaydı + merkezi denetim). `Direct`: rastgele 32 baytlık anahtar üretilir, yalnız SHA-256 özeti saklanır, anahtar cevapta
bir kez döner. `Pairing`: anahtar **üretilmez** (`keySet: false`); eşleştirme kodu üretilir (özet + son geçerlilik + üreten kullanıcı saklanır),
cevap `pairing` taşır. Tekil index'e çarpan eşzamanlı ikiz önce anahtarını yeniden arar (replay), değilse 409.

**Şirket başına kayıt (paket §8.2, AC-4):** cihaz kaydı, `installationId` tekilliği, anahtar ve Web onayı **şirket (`LegalEntityId`)
kapsamındadır**. Aynı telefon iki şirkette okutacaksa her şirkette **ayrı kayıt** (ayrı `number`, ayrı `deviceId`, ayrı anahtar) ve her şirkette
**ayrı Web onayı** gerekir. Mobil uygulama cihaz kimliği + anahtarı **(kiracı, şirket) başına** saklar ve şirket değiştirdiğinde o şirketin
kimliğini ve anahtarını gönderir. Başka şirketin cihaz kimliği / anahtarıyla gelen istek → 403 `BARCODE_DEVICE_KEY_INVALID` (kural 8).

**QR eşleştirme akışı (paket §8.2, AC-3; karar 03 Q-18):**
1. Web'de `devices.create` taşıyan kullanıcı cihazı kaydeder (`keyDelivery = Pairing`; formda telefonun `installationId`'si girilir) **ya da**
   `devices.activate` taşıyan kullanıcı engelli cihazı yeniden etkinleştirir. Cevap `pairing { code, qrPng, expiresAt }` taşır; Web QR'ı +
   kopyalanabilir kodu + kalan süreyi **yalnız o panelde** gösterir (panel kapanınca bir daha gösterilmez).
2. Telefon QR'ı okutur (`diten-scan-pair:1:{code}`) ve **o an seçili şirketin** bağlamıyla (`X-Legal-Entity-Id`) `pairScanDevice`'ı çağırır:
   çalışanın JWT'si + `warehouse.barcode.devices.create` + `{ pairingCode, installationId }`; cihaz başlığı gönderilmez (anahtar henüz yoktur).
3. Sunucu kodu kiracı + şirket kapsamında özetiyle arar ve **tek belgelik atomik** yazımla tüketir (filtre: özet + `PairingExpiresAt > şimdi` +
   `installationId` + `status ≠ Blocked`): yeni 32 baytlık anahtarı üretir, özetini yazar, kodu siler, `pairedAt`'i yazar. Anahtar cevapta **bir
   kez** döner. Eşleştirme cihazın **durumunu değiştirmez** (Web'den kaydedilen cihaz `Pending` kalır, Web onayı yine gerekir; `Pending` cihaz
   eşleşebilir, okutması `DEVICE_PENDING` olur) ve `version`'ı artırmaz.
4. Sonuçlar: kod **tek kullanımlıktır** — kullanılmış, bilinmeyen, başka kiracının ya da **başka şirketin** kodu → 404
   `BARCODE_PAIRING_NOT_FOUND` (varlık açılmaz); eşzamanlı iki kullanımdan **tam biri** 200; süresi dolmuş kod → 422 `BARCODE_PAIRING_EXPIRED`
   (anahtar üretilmez); `installationId` uyuşmuyor ya da cihaz `Blocked` → 422 `BARCODE_PAIRING_INVALID` (kod geçerli kalır, süresi işler).
5. Yeni kod (yeniden etkinleştirme) önceki kullanılmamış kodu geçersiz kılar. **Süresi dolan ya da kaybolan kod:** v1'de ayrı "yeni kod" ucu
   yoktur; yeni kod Web'de engelle (gerekçe) → yeniden etkinleştir ile alınır (bilinen sınır, FU-0182-PAIR-REISSUE).
6. **QR kiracı / şirket taşımaz:** telefonda seçili şirket Web'de kaydın yapıldığı şirketten farklıysa cevap **404**'tür. Mobil 404'te "Kod
   bulunamadı — seçili şirketi kontrol edin" gösterir (`error.message`, 7 dil).
7. **`installationId` gösterimi (mobil ekip işi):** mobil uygulama **ayarlar ekranında** ("Cihaz bilgisi") `installationId`'yi okunur,
   kopyalanabilir metin (ör. 4'erli gruplanmış — gruplama yalnız görseldir; kopyalanan ve QR'daki değer ham `installationId`'dir) **ve** QR
   olarak gösterir. Web formu QR okutmayı **şart koşmaz** (değer elle ya da yapıştırılarak girilir). Sunucu yalnız §2'deki biçimi doğrular.

**Kurtarma (ilk cevap ulaşmadıysa; paket §8.2):** `Pending` cihaz → Web'de onayla → engelle (gerekçe) → yeniden etkinleştir → telefon QR'ı
okutup eşleşir; `Blocked → Active` için yeniden etkinleştirme + eşleştirme tekrarlanır. Yeni durum geçişi yoktur.

**Durum makinesi (§6.1):** geçersiz geçiş 409 `BARCODE_INVALID_STATE` (`details.status`). `updateScanDevice` her durumda izinlidir.

**Nabız (`recordDeviceHeartbeat`; paket §8.2, AC-20):** `lastSeenAt` / `lastUserId` / `lastUserName` / `appVersion` (+ gelirse `batteryPercent`,
`batteryReportedAt`) tek belgelik atomik `$set` ile yazılır; `version` artmaz, işlem açılmaz, idempotency ve merkezi kayıt yazılmaz. Her durumdaki
(`Pending` / `Active` / `Blocked`) cihaz nabız gönderebilir (kimlik kapısı geçerliyse); cevap `status` taşır. Yoldaki `{deviceId}` başlıktaki
`X-Scan-Device-Id`'ye eşit olmalıdır (değilse 403 `BARCODE_DEVICE_KEY_INVALID`). Her `recordScan` da aynı nabız alanlarını yazar.

**Okutma (`recordScan`; paket §8.3 — sıra bağlayıcı):** (1) izin, cihaz **kimlik** kapısı (403; kural 8); (2) gövde şekli (400); (3) anahtar
araması (replay → ilk cevap; **cihaz durumu değerlendirmesinden önce** — cihaz ilk istekten sonra engellendiyse replay yeni `DEVICE_BLOCKED`
olayı yazmaz); (4) cihaz durumu → `DEVICE_PENDING` / `DEVICE_BLOCKED` / `DEVICE_WRONG_WAREHOUSE` (cihazın deposu doluysa ve okutma başka
depodaysa) — **olay yazılır**, 201 `Rejected`; (5) oturum sahipliği: `sessionId`'de başka kullanıcının olayı varsa 422
`BARCODE_SESSION_INVALID`, olay yazılmaz; (6) bağlam kaydı (`context.refId`): yoksa / başka şirketin / `warehouseId` bağlamın deposu değilse
404 `BARCODE_CONTEXT_NOT_FOUND`, olay yazılmaz; kapanmışsa olay yazılır, `Rejected` `CONTEXT_CLOSED`; `Query`'de bağlam kaydı yoktur — gövdedeki `warehouseId` şirketin etkin deposu değilse
422 `UNKNOWN_LOCATION`, olay yazılmaz (CT kararı 2026-11-13); (7) ayrıştırma (MOD-0174
`Gs1CodeParser`; ikinci ayrıştırıcı yok) → çözümleme → bağlam karşılaştırması (§6.3); (8) olay + merkezi kayıt **aynı işlemde** (Q-16 = yol b:
her başarılı okutma 1 merkezi kayıt). Bağımlılık (Mongo, LOCATION, PRODUCT-MASTER) okunamazsa 503 `BARCODE_INVENTORY_UNAVAILABLE`, olay ve kayıt
yazılmaz. Elle giriş (`source = Manual`): `manual.reasonCode` zorunlu, `Other`'da `note` zorunlu (400); doğrulama okutmayla aynıdır.

**Aşım (`overrideScan`; paket §8.5 — denetim sırası bağlayıcı):** (1) izin `scans.create` (403) ve cihaz kimliği (403); (2) biçim (400);
(3) **anahtar araması** (tekillikten önce: başarılı aşımın replay'i 201 + ilk gövde; PIN değerlendirilmez, sayaç ve iz yazılmaz); (4) olay
yok / başka kiracı – şirket / **başka kullanıcının** → 404 `BARCODE_SCAN_NOT_FOUND`; (5) aşılamaz neden (§6.5) → 422
`BARCODE_OVERRIDE_NOT_ALLOWED`; (6) sorumlu = okutan → 422 `BARCODE_OVERRIDE_SELF`; (7) olay zaten aşılmış → 409 `BARCODE_ALREADY_OVERRIDDEN`;
(8) sorumlunun geçerli PIN'i yok / süresi dolmuş → 422 `BARCODE_PIN_NOT_SET`; (9) kilit (`lockedUntil > şimdi`) → 423 `BARCODE_PIN_LOCKED`
(`Locked` izi); (10) PIN doğrulaması — yanlışsa sayaç atomik artar, 422 `BARCODE_PIN_INVALID` (`PinInvalid` izi); sayaç `pinMaxAttempts`'e
ulaşırsa kilit kurulur ve **o deneme** 423 `BARCODE_PIN_LOCKED` alır (`LockSet` izi); (11) doğruysa sayaç sıfırlanır ve `ScanOverride` +
idempotency kaydı + merkezi kayıt tek işlemde yazılır (tekil index son bekçidir — eşzamanlı ikinci aşım 409). Çağrı okutmanın yapıldığı cihazdan,
**çalışanın JWT'siyle** gelir; sorumlunun yetkisi JWT'den değil **PIN kaydından** gelir (`scan-overrides.approve` bu ucu **korumaz**). PIN
sayacı ve deneme izi aşım işleminin **dışında**, tek belgelik atomik yazımla yazılır (422 / 423'e rağmen kalır); iz yazılamazsa cevap 503.
**423 gövdesi** `error.details.lockedUntil` (UTC) taşır; deneme sayısı / kalan hak 422 ve 423 gövdelerinde **yoktur**.

**PIN (`setSupervisorPin`; paket §8.5, AC-19):** kullanıcı yalnız **kendi** PIN'ini belirler / değiştirir; uç `scan-overrides.approve` ister
(yetki PIN belirlenirken ölçülür; sonradan geri alınan yetki PIN süresi dolana kadar geçerli kalır — bilinen sınır, FU-0182-PIN-LIVE). Yeni PIN
sayacı ve kilidi sıfırlar. Zayıf PIN (§2) → 400 `INVALID_REQUEST`.

### 4.2 DL-4, DL-6, DL-7 — ön taslak (17 işlem; paket §3; her dilimde bağlayıcı olur)

| Yöntem + yol | operationId | x-permission | Başarı | Dilim |
|---|---|---|---|---|
| `GET /barcode/label-templates` | `listLabelTemplates` | `warehouse.barcode.label-templates.read` | 200 sayfa `LabelTemplate` | DL-4 |
| `POST /barcode/label-templates` | `createLabelTemplate` | `warehouse.barcode.label-templates.create` | 201 `LabelTemplate` + ETag | DL-4 |
| `GET /barcode/label-templates/{templateId}` | `getLabelTemplate` | `warehouse.barcode.label-templates.read` | 200 `LabelTemplate` + ETag | DL-4 |
| `POST /barcode/label-templates/{templateId}/update` | `updateLabelTemplate` | `warehouse.barcode.label-templates.update` | 200 `LabelTemplate` + ETag | DL-4 |
| `POST /barcode/label-templates/{templateId}/activate` | `activateLabelTemplate` | `warehouse.barcode.label-templates.activate` | 200 `LabelTemplate` + ETag | DL-4 |
| `POST /barcode/label-templates/{templateId}/deactivate` | `deactivateLabelTemplate` | `warehouse.barcode.label-templates.deactivate` | 200 `LabelTemplate` + ETag | DL-4 |
| `POST /barcode/label-previews` | `previewLabel` | `warehouse.barcode.label-prints.create` | 200 önizleme (salt okuma; yazım yok) | DL-4 |
| `GET /barcode/label-prints` | `listLabelPrintJobs` | `warehouse.barcode.label-prints.read` | 200 sayfa `LabelPrintJob` | DL-4 |
| `POST /barcode/label-prints` | `createLabelPrintJob` | `warehouse.barcode.label-prints.create` | 201 `LabelPrintJob` | DL-4 (SSCC ve lot / koli türleri DL-5) |
| `GET /barcode/label-prints/{jobId}` | `getLabelPrintJob` | `warehouse.barcode.label-prints.read` | 200 `LabelPrintJob` | DL-4 |
| `GET /barcode/label-prints/{jobId}/file` | `getLabelPrintJobFile` | `warehouse.barcode.label-prints.read` | 200 `application/pdf` | DL-4 |
| `POST /barcode/rfid-sweeps` | `recordRfidSweep` | `warehouse.barcode.scans.create` | 201 `RfidSweep` | DL-6 |
| `GET /barcode/rfid-sweeps/{sweepId}` | `getRfidSweep` | `warehouse.barcode.scans.create` | 200 `RfidSweep` | DL-6 |
| `GET /barcode/trace` | `getScanTrace` | `warehouse.barcode.trace.read` | 200 `ScanTrace` | DL-7 |
| `GET /barcode/trace/exports` | `listScanTraceExports` | `warehouse.barcode.trace.read` | 200 sayfa `ScanTraceExport` | DL-7 |
| `POST /barcode/trace/exports` | `createScanTraceExport` | `warehouse.barcode.trace.export` | 201 `ScanTraceExport` | DL-7 |
| `GET /barcode/trace/exports/{exportId}/file` | `getScanTraceExportFile` | `warehouse.barcode.trace.export` | 200 PDF / CSV | DL-7 |

**İşlem sayımı:** §4.1 16 + §4.2 17 = **33** (paket §3 uç listesiyle birebir). DL-2 (yalnız Web), DL-3 (0178 / 0181 içinde port), DL-5 ve
DL-8 (yalnız test) servis ucu eklemez. `getRfidSweep` izni paket §14'te adsızdır — ön taslak `scans.create` yazar; DL-6'da kesinleşir.

## 5. Error codes

### 5.1 SCAN-BUNDLE'ın kendi kodları (paket §8.15)

| Kod | HTTP | Ne zaman | Dilim |
|---|---|---|---|
| `BARCODE_DEVICE_KEY_INVALID` | 403 | cihaz başlığı yok / cihaz o şirkette yok / anahtar yok ya da özeti uyuşmuyor; hiçbir şey yazılmaz | DL-1 |
| `BARCODE_DEVICE_NOT_FOUND` | 404 | cihaz yok, başka kiracı / şirket — **ek ad** (paket "404 —"; §9 açık nokta 1) | DL-1 |
| `BARCODE_SCAN_NOT_FOUND` | 404 | okutma olayı yok, başka kiracı / şirket; aşımda başka kullanıcının olayı — **ek ad** | DL-1 |
| `BARCODE_CONTEXT_NOT_FOUND` | 404 | okutmanın bağlam kaydı (görev / sayım / outbound) yok, başka şirketin ya da depo uyuşmuyor; olay yazılmaz — **ek ad** | DL-1 |
| `BARCODE_PAIRING_NOT_FOUND` | 404 | bilinmeyen, kullanılmış, başka kiracının / şirketin eşleştirme kodu — **ek ad** | DL-1 |
| `BARCODE_CONFLICT` | 409 | `If-Match` bayat | DL-1 |
| `BARCODE_INVALID_STATE` | 409 | cihaz durum makinesinde geçersiz geçiş (`details.status`) | DL-1 |
| `BARCODE_DEVICE_ALREADY_REGISTERED` | 409 | `installationId` şirkette kayıtlı | DL-1 |
| `BARCODE_DEVICE_NAME_TAKEN` | 409 | ad şirkette `Blocked` olmayan başka cihazda — **ek** (paket §4.1 / §12 tekillik kuralının kodu; §8.15'te adsız; §9 açık nokta 1) | DL-1 |
| `BARCODE_ALREADY_OVERRIDDEN` | 409 | olay zaten aşılmış | DL-1 |
| `BARCODE_REASON_REQUIRED` | 400 | engelleme / yeniden etkinleştirme gerekçesi yok ya da 10..500 dışı — **ek ad** (paket AC-3 "gerekçesiz 400"; MOD-0180 `WAVE_REASON_REQUIRED` emsali) | DL-1 |
| `BARCODE_OVERRIDE_NOT_ALLOWED` | 422 | aşılamaz neden / bağlam (geçerli PIN'le bile) | DL-1 |
| `BARCODE_OVERRIDE_SELF` | 422 | sorumlu = okutan | DL-1 |
| `BARCODE_PIN_NOT_SET` | 422 | sorumlunun PIN'i yok / süresi dolmuş | DL-1 |
| `BARCODE_PIN_INVALID` | 422 | yanlış PIN (gövdede deneme sayısı / kalan hak **yok**) | DL-1 |
| `BARCODE_PIN_LOCKED` | 423 | kilit; `details.lockedUntil` (UTC); kalan hak **yok** | DL-1 |
| `BARCODE_SESSION_INVALID` | 422 | DL-1: `sessionId` başka kullanıcının oturumu (olay yazılmaz); DL-3: geçersiz oturum / bin eşitliği | DL-1, DL-3 |
| `BARCODE_PAIRING_EXPIRED` | 422 | süresi dolmuş eşleştirme kodu | DL-1 |
| `BARCODE_PAIRING_INVALID` | 422 | `installationId` uyuşmuyor ya da cihaz `Blocked` | DL-1 |
| `BARCODE_COMPANY_CODE_INVALID` | 422 | MDM şirket `Code`'u `^[A-Z]{2,5}$` değil (numara öneki) — **ek** (MOD-0180 / 0181 emsali) | DL-1 |
| `BARCODE_RECORD_CODE_UNAVAILABLE` | 503 | sayaç yazılamadı / tür reddedildi; cihaz yaratılmaz — **ek** (MOD-0180 / 0181 emsali) | DL-1 |
| `BARCODE_INVENTORY_UNAVAILABLE` | 503 | Mongo / LOCATION / PRODUCT-MASTER / MOD-0178 – 0181 okuması yapılamadı; aşım denemesi izi yazılamadı; yazım yok | DL-1 |
| `BARCODE_SESSION_REQUIRED`, `BARCODE_SESSION_CONSUMED` | 422 | okutma zorunlu depoda okutmasız onay; oturum harcanmış (0178 / 0181 onay uçlarında — X-14) | DL-3 |
| `LABEL_TEMPLATE_NAME_TAKEN` | 409 | şablon adı şirkette var | DL-4 |
| `LABEL_SOURCE_INVALID`, `LABEL_REPRINT_REASON_REQUIRED` | 422 | basım kaynağı / yeniden basım gerekçesi | DL-4 |
| `SSCC_PREFIX_NOT_SET`, `SSCC_CAPACITY_EXHAUSTED` | 422 | SSCC öneki / kapasite | DL-5 |
| `BARCODE_GTIN_RESOLUTION_UNAVAILABLE` | 503 | MOD-0290-FU07 okuması yapılamadı | DL-5 |
| `BARCODE_TRACE_TOO_LARGE` | 422 | iz sorgusu sınırı | DL-7 |

### 5.2 Yeniden tanımlanmayan, kullanılan kodlar (`InventoryErrorCodes`)

| Kod | HTTP | Bu sözleşmede ne zaman |
|---|---|---|
| `INVALID_REQUEST` | 400 | gövde / sorgu şekli, bilinmeyen ya da sunucu üyesi, eksik `If-Match`, `tenantId` / `legalEntityId` istekte, aralık dışı ayar, zayıf PIN, elle girişte gerekçe yok. **Paket §8.15 bu durumu `VALIDATION_FAILED` diye adlandırır; servis kablosunun kodu `INVALID_REQUEST`'tir — §9 açık nokta 3** |
| `IDEMPOTENCY_KEY_REUSED` | 400 | aynı anahtar, farklı gövde |
| `UNKNOWN_LOCATION` | 422 | gövdedeki `warehouseId` depo değil / `Active` değil / başka şirketin (MOD-0180 / 0181 emsali) — cihaz kaydı, düzenleme ve `Query` okutması |
| `LEGAL_ENTITY_MASTER_UNAVAILABLE` | 503 | MDM tüzel kişilik okunamadı (şirket öneki; scope middleware) |
| `CROSS_LE_FORBIDDEN` | 403 | başlık / token / üyelik uyuşmazlığı (scope middleware — servis geneli) |

### 5.3 Gövde bildirimleri (HTTP hatası değil)
`deviceKeyNotice: BARCODE_DEVICE_KEY_NOT_REPLAYED` ("Anahtar yalnız ilk cevapta verildi; kaybolduysa cihazı Web'den yeniden etkinleştirin"),
`pairingCodeNotice: BARCODE_PAIRING_CODE_NOT_REPLAYED`, `hiddenReason: BLIND_COUNT_OPEN` — metinleri `noticeMessage` / istemci çevirisi, 7 dil.

## 6. Durum makinesi ve okutma kuralları

### 6.1 Cihaz durumu (paket §8.2)

| Durum | Geçiş | İşlem | Anahtar |
|---|---|---|---|
| (yok) | → `Pending` | `registerScanDevice` (kendiliğinden `Active` doğan kayıt **yok** — Q-8) | `Direct`: anahtar cevapta; `Pairing`: kod cevapta |
| `Pending` | → `Active` | `activateScanDevice` (onay) | değişmez |
| `Active` | → `Blocked` | `deactivateScanDevice` (gerekçe zorunlu) | değişmez (okutmalar `DEVICE_BLOCKED`) |
| `Blocked` | → `Active` | `activateScanDevice` (gerekçe zorunlu) | **eski anahtar o anda geçersiz** (`keySet: false`, eski anahtar 403); yeni anahtar yalnız QR eşleştirmesiyle |
| herhangi | (durum değişmez) | `pairScanDevice` (`Blocked` hariç), `updateScanDevice`, `recordDeviceHeartbeat` | eşleştirme yeni anahtarı yazar |

Diğer her geçiş (`Pending → Blocked`, `Active → Active`, `Blocked → Blocked`) 409 `BARCODE_INVALID_STATE`. Silme yoktur.

### 6.2 Okutma sonucu kodları (`reasonCode` — HTTP değil; paket §8.3)

| Kod | `outcome` | Ne zaman |
|---|---|---|
| `UNREADABLE` | Rejected | GS1 biçiminde ama ayrıştırıcı reddetti (DL-5'e kadar ham biçimde tablo dışı AI — ör. (37) — taşıyan dizge dahil) |
| `UNKNOWN_BARCODE` | Rejected | düz dizge adımın beklediği nesneye çözülmedi (bin kodu / lot numarası yok) |
| `WRONG_PRODUCT` | Rejected | okunan (01) beklenen SKU'nun GTIN'i değil; `Pack`'te outbound'da olmayan SKU |
| `PRODUCT_NOT_VERIFIABLE` | **Warning** (bilgi) / **Rejected** | `Warning`: (01) okundu ama beklenen SKU'nun ana veride GTIN'i yok; `Rejected`: `Product` adımında okunan GS1 dizgesinde (01) **yok** (koli / palet etiketi) — asla `Accepted` değil |
| `WRONG_LOCATION` | Warning (yalnız `Putaway`, öneri varken, bin 0178 kurallarından geçiyorsa) / Rejected | §6.3 |
| `DIFFERENT_LOT` | **Rejected** | okunan lot bağlamın lotu değil — **her bağlamda `Rejected`, `overridable = false`** |
| `LOT_EXPIRED` | Rejected | lotun kayıttaki SKT'si MOD-0176 gün sınırı kuralına göre geçmiş |
| `LOT_RECALLED` | Rejected | lot geri çağırmada (MOD-0177) |
| `LOT_DATA_MISMATCH` | Rejected | karekoddaki (17) SKT lot kaydındakiyle uyuşmuyor |
| `SERIAL_DUPLICATE` | Warning | aynı oturumda aynı seri ikinci kez (ikinci okutma sayılmaz) |
| `DEVICE_PENDING`, `DEVICE_BLOCKED`, `DEVICE_WRONG_WAREHOUSE` | Rejected | cihaz durumu / deposu (§4.1 okutma sırası 4) |
| `CONTEXT_CLOSED` | Rejected | görev / sayım / outbound kapanmış |

### 6.3 Bağlam kuralları (paket §8.4)

| `context.kind` | Geçerli `step` | `refId` | Karşılaştırma |
|---|---|---|---|
| `Putaway` | `Product`, `Lot`, `Location` | MOD-0178 yerleştirme görevi | ürün / lot görevinkiyle; **(b) hedef bin:** öneri (planlı hedef) varken okunan bin = öneri → `Accepted`; okunan ≠ öneri ve bin 0178 hedef kurallarından geçiyor → `Warning` `WRONG_LOCATION` **`overridable = true`**; geçmiyor (kilitli, karantina, mal kabul / sevkiyat hazırlık rolü, başka depo, saklama koşulu uyumsuz, kaynakla aynı) → `Rejected` `WRONG_LOCATION` `overridable = false`. **Öneri yokken:** bin 0178 kurallarından geçiyor → `Accepted`; geçmiyor → aşılamaz `Rejected` `WRONG_LOCATION`. Aşılabilir `Warning` **yalnız öneri varken** doğar |
| `Pick` | `Location`, `Product`, `Lot`, `Serial` | MOD-0178 toplama görevi | bin ≠ görevin kaynak bin'i → `Rejected` `WRONG_LOCATION`; lot ≠ → `Rejected` `DIFFERENT_LOT`; **ikisi de aşılamaz** (geçerli PIN'le bile; `message`: "Sorumluya başvurun — tahsis değişmeli") |
| `Count` | `Location`, `Product`, `Lot` | MOD-0181 sayımı | bin sayım kapsamında değil → `Rejected` `WRONG_LOCATION`; satırda olmayan SKU / lot → `Accepted` + `foundStock: true`; kör sayımda beklenen miktar / bin stoğu **yok** |
| `Pack` | `Product`, `Lot`, `Package` | MOD-0178 outbound | outbound'un toplanmış tahsislerinde olmayan SKU / lot → `Rejected` `WRONG_PRODUCT` / `DIFFERENT_LOT`; `Package` adımı DL-5'e kadar `UNKNOWN_BARCODE` (koli bağlaması v1'de yalnız kayıt) |
| `Query` | (adım `Location` \| `Lot` \| `Product` — bilgi; çözümleme sırası §3.4) | yok | `query` özeti (§3.4) |

- Bağlamda geçerli olmayan `step` → 400 `INVALID_REQUEST`.
- **(a) Farklı lot:** `reasonCode = DIFFERENT_LOT`, `outcome = Rejected`, `overridable = false` — **her bağlamda** (lot / bin değişimi
  FU-0178-LOTSWAP).
- Ürün karşılaştırması (DL-5'ten önce): beklenen SKU'nun PRODUCT-MASTER `gtin`'i doluysa okunan (01) ile eşitlik; boşsa `Warning`
  `PRODUCT_NOT_VERIFIABLE` (aşılamaz, bilgi). Düz (GS1 olmayan) dizge `Product` adımında → `UNKNOWN_BARCODE`; `Lot` adımında beklenen SKU içinde lot
  numarası; `Location` adımında depo içinde bin kodu tam eşleşmesi.
- Lot çözülen her okutmada (`Query` hariç) sırayla: `LOT_RECALLED` → `LOT_EXPIRED` → `LOT_DATA_MISMATCH` → bağlam lotu (`DIFFERENT_LOT`).
- **DL-3'te gelir (burada uygulanmaz — bilgi):** (c) yerleştirmede PIN aşımından sonra MOD-0178 onayında `deviationReason`'ı **sunucu**
  "Sorumlu onayı — {sorumlu}, {zaman}" olarak yazar, istemci değeri yok sayılır; (d) okutma ile onay arasındaki bağ
  `scanRef = scan-session:{sessionId}`'dir — bu önekli değer depo ayarından bağımsız oturum kapısında doğrulanır, öneksiz değer opaktır;
  (h) oturum kapısına giden onayda onayın bin'i oturumun `Location` adımında okunan bin olmalıdır (değilse 422 `BARCODE_SESSION_INVALID`).
  WMS-BUNDLE / WMS-COUNT-BUNDLE eklemeli sürümleri CT adımıdır (X-14).

### 6.4 Kör sayım (kural 17)
Açık kör sayım bin'i = MOD-0181 bin talebi açık + sayım `Blind = true`. `Count` cevabı ve olayı beklenen miktar / bin stoğu taşımaz; `Query`
özeti §3.4 kuralıyla gizlenir; kural sayım kapanana kadar sürer, kullanıcıya göre ayrılmaz.

### 6.5 Aşılabilirlik matrisi (paket §8.5; Q-3 = A)
`overridable = true` ⇔ `context.kind = Putaway` **ve** `reasonCode = WRONG_LOCATION` **ve** `outcome = Warning`. Matrisin geri kalanı `false`:
`Pick` × her neden, her bağlam × `DIFFERENT_LOT` / `LOT_EXPIRED` / `LOT_RECALLED` / `LOT_DATA_MISMATCH` / `WRONG_PRODUCT` /
`PRODUCT_NOT_VERIFIABLE` / `SERIAL_DUPLICATE` / `UNREADABLE` / `UNKNOWN_BARCODE` / `DEVICE_*` / `CONTEXT_CLOSED`, ve `Rejected`
`WRONG_LOCATION`. `Accepted` olay da aşılamaz (422 `BARCODE_OVERRIDE_NOT_ALLOWED`).

## 7. DL-3 / DL-5 kablo ekleri (bilgi — bu dilimde uygulanmaz)
- **DL-3:** servis ucu yok; `ScanSessionBinding` MOD-0178 / 0181 onay işleminin içinde yazılır; `BarcodeSettings.scanRequiredWarehouseIds`.
- **DL-5:** `parsed.sscc`, `resolved.packageId` / `packageNumber`, GTIN'den ürün (`identifiers/{gtin}`), `Query`'de SSCC ve GTIN yolu, kapalı AI
  toleransı ((37), (310n), (240)), `BarcodeSettings.ssccCompanyPrefix` / `ssccExtensionDigit`.

## 8. Merkezi denetim (paket §8.12, §21)

Her yazma komutu BL-501 kaydı bildirir: kategori `MasterData`, kaynak modül `BARCODE-RFID`, işlem Platform enum adı. DL-1 komutları (10):
`RegisterScanDeviceCommand` (`Create`), `UpdateScanDeviceCommand` (`Update`), `ActivateScanDeviceCommand` (`LifecycleTransition`),
`DeactivateScanDeviceCommand` (`LifecycleTransition`), `PairScanDeviceCommand` (`Update`), `RecordDeviceHeartbeatCommand` (işaret taşır,
**kayıt doğurmaz** — işlem açmaz, enlist etmez), `RecordScanCommand` (`Create`), `OverrideScanCommand` (`Execute`), `SetSupervisorPinCommand`
(`Update`), `UpdateBarcodeSettingsCommand` (`Update`). Başarılı ve yazan komut 1 kayıt; replay 0; başarısız 0; nabız 0. Yanlış PIN / kilit
merkezi kayıt doğurmaz (yerel iz `ScanOverrideAttempt`). Kayıt PIN, anahtar, kod ve özetlerini taşımaz.

## 9. Açık noktalar ve kararları (paketle çelişki değil; CT incelemesi 2026-11-13 — kararlar bölüm sonunda)

1. **Ek kodlar / adlar:** paket §8.15 404'ü kodsuz yazar ("404 —"); servisin `Error` zarfı kod ister, taslak dört 404 adı verir
   (`BARCODE_DEVICE_NOT_FOUND`, `BARCODE_SCAN_NOT_FOUND`, `BARCODE_CONTEXT_NOT_FOUND`, `BARCODE_PAIRING_NOT_FOUND`). Ayrıca paketin kuralını
   yazıp kod vermediği durumlar için: `BARCODE_DEVICE_NAME_TAKEN` (409), `BARCODE_REASON_REQUIRED` (400), `BARCODE_COMPANY_CODE_INVALID` (422),
   `BARCODE_RECORD_CODE_UNAVAILABLE` (503) — MOD-0180 / 0181 emsalleri. Paket revizyonunda §8.15'e eklenmesi önerilir.
2. **`setSupervisorPin` ve `If-Match`:** paket §12 "değişebilir kayıtta yaratma dışı yazma `If-Match` ister" der ve iki adlı muafiyet sayar
   (nabız, PIN sayacı); `SupervisorPin` de `Version` taşır ama §3'te PIN'i okuyan bir `GET` ucu yoktur (DL-1 16 uç), istemci sürümü öğrenemez.
   Taslak `setSupervisorPin`'i `If-Match`'siz yazar (kişi yalnız kendi kaydını yazar; son yazım geçerli) — **üçüncü adlı muafiyet, CT kararı
   ister**. Bağlı konu: `/Warehouse/SupervisorPin` sayfasının (DL-2) son kullanma tarihi ve son kilit zamanı için bir okuma gerekir; paket §5.1
   X-12 "DL-1 bir GET eklerse sayı ölçümden gelir" der. WP DL-1'i 16 uçla sabitlediği için taslak bu GET'i **eklemez**; DL-2'nin ilk adımında
   karar verilmeli (ör. `GET /barcode/supervisor-pin` → `{ setAt?, expiresAt?, lockedUntil? }`; Gateway sayısı +1).
3. **`VALIDATION_FAILED` ↔ `INVALID_REQUEST`:** paket §8.15 biçim hatasını `VALIDATION_FAILED` diye adlandırır; InventoryService'in bütün
   sözleşmeleri (INVENTORY, WMS-BUNDLE, WMS-COUNT-BUNDLE, WMS-WAVE-BUNDLE) ve istek okuyucuları `INVALID_REQUEST` kullanır, `VALIDATION_FAILED`
   kodu serviste yoktur. Taslak `INVALID_REQUEST` yazar. Paket adı isteniyorsa dondurmadan önce söylenmeli (servis genelinden ayrışır).
4. **Idempotency anahtarının yeri:** paket "yazmalarda `Idempotency-Key`" başlığını yazar; servis deseni başlık + gövde `idempotencyKey`
   eşitliğidir (WMS-BUNDLE ailesi). Taslak servis desenini kullanır. **Replay durum kodu:** paket §8.1 "ilk cevabın durum kodu" der (201
   yaratmanın replay'i 201'dir); WMS-WAVE-BUNDLE'da replay 200'dür. Taslak **paketi** izler.
5. **`expected*` üyeleri:** paket §8.3 "kör sayım bağlamında `expected*` alanı hiç yoktur" der; kör olmayan bağlamlarda beklenen değerin
   cevapta olup olmayacağını yazmaz. Taslak en dar yolu seçer: okutma cevabı **hiçbir bağlamda** `expected*` taşımaz (mobil görevi zaten
   WMS-BUNDLE'dan bilir). Mobil ekip "beklenen raf kodu" isterse eklemeli üye olarak eklenir.
6. **Aşımda cihaz durumu:** paket §8.5 denetim sırası cihaz **kimliğini** sayar, durumunu saymaz. Taslak sırayı aynen uygular: okutmadan
   sonra engellenen cihazdan gelen aşım isteği kimlik kapısını geçer (anahtar hâlâ geçerlidir — engelleme anahtarı silmez). `Blocked` cihazdan
   aşımın reddedilmesi isteniyorsa sıraya adlı bir adım eklenmeli (CT kararı).
7. **`Pending → Blocked`:** paket durum makinesi yalnız `Active → Blocked` yazar; onaylanmamış bir kaydı reddetmenin yolu v1'de onayla →
   engelle'dir (ya da `Pending` bırakmak). Taslak paketi izler (409).
8. **`ScanDevice.keySet` / `pairingExpiresAt`:** paket §8.14 Web panelinde "kalan süre"yi yalnız kayıt / yeniden etkinleştirme cevabından
   gösterir; taslak ek olarak `GET` cevaplarına sır olmayan iki üye yazar (anahtar var mı, kullanılmamış kodun son geçerliliği) — Web "eşleşme
   bekliyor" rozetini panel kapandıktan sonra da gösterebilsin diye. İstenmiyorsa dondurmadan önce çıkarılır.
9. **Nabız ve cihaz durumu:** taslak her durumdaki cihazın nabzını kabul eder (sağlık sinyali; `Blocked` cihaz da görülür) ve cevapta `status`
   döner. Paket nabzın durum koşulunu yazmaz.
10. **`BarcodeSettings` DL-1 kapsamı:** `scanRequiredWarehouseIds` (DL-3), `ssccCompanyPrefix` / `ssccExtensionDigit` (DL-5) paket §4.4'te
    dilimleriyle yazılı; taslak bunları DL-1 şemasına **koymaz** (doğrulamaları o dilimlerin okumalarına bağlı), eklemeli gelir.
11. **`Putaway` / `Pack` / `Pick` için `Lot` adımı:** paket §8.4 adım listesinde `Lot`'u ayrı saymaz ama §8.3 "adım `Lot` ise beklenen SKU
    içinde lot numarası" der; taslak `Lot`'u ürün bağlamlı bağlamlarda geçerli adım sayar (GS1 karekodu olmayan, yalnız lot barkodu taşıyan
    ürünler için).

**CT kararları 2026-11-13 (MVP-5 WMS CT; 63cf96ead incelemesi):**
- **Madde 1 — kabul:** dört 404 adı (`BARCODE_DEVICE_NOT_FOUND`, `BARCODE_SCAN_NOT_FOUND`, `BARCODE_CONTEXT_NOT_FOUND`, `BARCODE_PAIRING_NOT_FOUND`) ve ek
  kodlar (`BARCODE_DEVICE_NAME_TAKEN` 409, `BARCODE_REASON_REQUIRED` 400, `BARCODE_COMPANY_CODE_INVALID` 422, `BARCODE_RECORD_CODE_UNAVAILABLE` 503).
  `Query` bağlamında bilinmeyen ya da başka şirketin `warehouseId`'si **422 `UNKNOWN_LOCATION`** (yeni kod yok). `:guid` kısıtına uymayan yol: ölçüldü —
  boş gövdeli 404 (kural 14).
- **Madde 2 — kabul:** `setSupervisorPin` `If-Match`'sizdir (her belirleme yeni bir kimlik bilgisi yaratır, kişi yalnız kendi kaydını yazar, okuyan GET
  yok). **`pairScanDevice`'ın `If-Match` muafiyeti de adıyla:** paket §12 yalnız nabzı ve PIN sayacını sayar; eşleştirme tek kullanımlık kodun atomik
  tüketimidir (§8.2 filtresi), `version`'ı artırmaz ve `If-Match` istemez (kural 7).
- **Madde 3 — karar:** `INVALID_REQUEST` kullanılır, `VALIDATION_FAILED` değil (servis genelinde tek kod); paket §8.15'teki ad bir sonraki paket
  revizyonunda errata olarak düzeltilir.
- **Madde 4 — karar:** anahtar başlıkta ve gövdede, ikisi eşit. Replay paketi izler (201'in replay'i 201 — bilinçli ayrışma, dondurma notu); replay'de
  `message` / `noticeMessage` o anki dile göre yeniden üretilir; replay `ETag`'i saklanan gövdedeki `version`'dır (kural 6).
- **Madde 5 — kabul:** hiçbir bağlamda `expected*` yok.
- **Madde 6 — karar:** engellenmiş cihazdan aşım taslaktaki gibi kalır (paket §8.5 sırası bağlayıcı).
- **Madde 7 — karar:** `Pending → Blocked` 409 (paketteki gibi).
- **Madde 8 — kabul:** `keySet` ve `pairingExpiresAt` GET cevaplarında kalır (sır değil).
- **Madde 9 — kabul:** her durumdaki cihaz nabız gönderebilir.
- **Madde 10 — kabul, koşullu:** DL-3 / DL-5 ayar üyeleri DL-1 şemasında yok; eklendiklerinde isteğe bağlı istek üyesidirler (§3.9).
- **Madde 11 — kabul:** `Lot` adımı Putaway / Pick / Pack / Count'ta geçerlidir.

**CT incelemesinin ek bulguları (2026-11-13):**
- **Pano penceresi — sahip kararı (Beste, 2026-11-13):** `getScanEventDashboard` **son 24 saati** (çağrı anından geriye) sayar; takvim günü yoktur
  (serviste depo saat dilimi kaynağı yok). Paketin "bugünkü okutma" ifadesi paket r8'de hizalanır; ekran etiketi DL-2'de.
- `getBarcodeSettings`, `listScanDevices`, `getScanDevice` ve `pairScanDevice` hata listelerine `BARCODE_INVENTORY_UNAVAILABLE` (503) eklendi (§4.1).
- **`ScanOverrideAttempt` kablosunda `failedAttemptsAfter` yoktur — bilinçli daraltma:** paket §4.3 alanı varlıkta sayar; kabloya çıkmaz ("kalan hak /
  deneme sayısı yok" ilkesi, paket §8.15). Alan yalnız depoda durur.
- **`warehouseId` ↔ bağlam deposu uyuşmazlığı 404 `BARCODE_CONTEXT_NOT_FOUND` — CT yorumu:** paket §12 "bağlamın deposuyla aynı" der, durum kodunu
  yazmaz; uyuşmayan depo için bağlam kaydı yok sayılır.
- **`SupervisorPin` okuma ucu yok** (son kullanma ve son kilit zamanı — paket §8.14): paket değişikliği ister (CT paket r8'i açacak); DL-2'yi etkiler,
  DL-1'i bekletmez. Bu taslak ucu **eklemez**.
- **ZXing.Net — CT kararı B-4 = A:** `Diten.InventoryService.Api` (çizici `Api/Barcode/PairingQrRenderer.cs`, portu
  `Application/Features/Barcode/Ports/IPairingQrRenderer.cs`) — paket §5 lafzından sapma, emsal PDFsharp.

**Mobil ekip akışı — `Count` bağlamı (CT bilgisi, 2026-11-13):** mobil "raf okut → ürün okut → sunucu satırı ya da bulunan stoğu söyler" akışını ve
sayım girişinde `scanRef = scan-session:{sessionId}` gönderimini bekliyor.
- **Uyumlu:** `Count` bağlamı `Location` → `Product` (→ `Lot`) adımlarını kabul eder; okutmalar aynı `sessionId` altında yazılır; WMS-COUNT-BUNDLE'da
  `scanRef` 1..200 karakterlik opak dizgedir (`wms-count-bundle.openapi.yaml` `scanRef: { type: string, minLength: 1, maxLength: 200 }`) —
  `scan-session:` + uuid (49 karakter) bu alana sığar; bugün opaktır, kapı DL-3'tedir (§6.3 (d)). Ürün okutmasının cevabı satırda **olmayan** SKU /
  lot için `foundStock: true` verir.
- **Açık nokta (12):** cevap "satırı" **kimliğiyle söylemez** — `resolved` SKU ve lotu (kod, ad, numara) taşır, sayım satırının `lineId`'sini taşımaz;
  mobil satırı WMS-COUNT-BUNDLE'dan aldığı satır listesinde (lokasyon + SKU + lot) kendisi eşler. Mobil satır kimliğini cevapta isterse eklemeli bir üye
  (`resolved.lineId` — yalnız `Count`, miktar taşımadan) eklenebilir; kör sayım kuralını bozmaz (kimlik miktar değildir). **Karar CT / mobil ekip.**

## 10. Doğrulama kaydı (geliştirici oturumu, 2026-11-13)

Bu dosya paket r7'ye (LF sha256 `9f8fcc33…ce1e7`) karşı yazıldı: §3 uç listesi (33 işlem; DL-1 16 = WP §2), §4.1–§4.4, §4.10, §4.11,
§8.1–§8.6, §8.12, §8.13, §8.15, §8.16 (a), (b), (e), (f), (g) — (c), (d), (h) "DL-3'te gelir" notuyla §6.3'te —, §12, §13, §14 izin anahtarları
(DL-1 10 anahtar, işlem başına eşleşme), §21 denetim işlemleri, §22 kayıt kodu, AC-1…AC-30. Taslağın pakete eklediği her şey §9'da adıyla
yazılıdır. Dilim başı: `5591b1213` (`ct/line-ff-2026-11-13i`). **İkinci sürüm (CT incelemesi sonrası):** §9'daki CT kararları, sahip kararı
(pano penceresi) ve ek bulgular işlendi; §4.1 hata listeleri, kural 6 / 14, §3.8, §3.9 güncellendi. Üretim kodu bu sürümle aynıdır: sözleşme testi
(`INVT/Barcode/BarcodeDraftContract.cs` + `BarcodeArchitectureTests.AC1_*` + `BarcodeAccessTests.AC1_*`) 16 işlemi kapalı şemayla doğrular.
