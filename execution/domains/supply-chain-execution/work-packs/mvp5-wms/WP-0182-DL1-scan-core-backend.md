# WP-0182-DL1 — MOD-0182 Barkod / RFID: SCAN-BUNDLE taslağı + cihaz + okutma çekirdeği (arka uç, DL-1)

- **Kimlik:** WP-0182-DL1 · **Modül:** MOD-0182 Barcode/RFID (supply-chain-execution, `Diten.InventoryService`, kiracı modülü).
- **Paket:** `execution/domains/supply-chain-execution/module-packs/MOD-0182-barcode-rfid.md` **r7**, LF sha256
  `9f8fcc3310e9d69f433a64dcd664ba04480979cea3db714bf0b75bfa5d5ce1e7`, `status: ready-for-dev`.
- **Onay satırı:** `execution/approvals/approvals.tsv:87` (commit `e12bf26c8`), onaylayan Ali (GitHub `GMG-ERP`), yazan `bestepullukcu-alt`, 2026-11-13.
- **`.antigravity` kapısı:** kapandı — commit `3a50c44c6` (PKS-001 `warehouse.barcode.*` satırı + `:92` / `:105`, RCS-001 §3 `DVC` / `LBL`,
  X-8 `RecordCodeTypeCatalogTests` `DVC` / `LBL`); X-9 MOD-0178 §14.1 notu MOD-0178 r30 ile. Bu WP `.antigravity/**`'a yazmaz.
- **Dilim:** DL-1 (paket §23 satırı, §24 DL-1 satırı). **Kanıt seviyesi:** E4 (§23).
- **Sahip:** geliştirici oturumu. **CT doğrulayıcı:** MVP-5 WMS CT (sahip Beste). **Durum:** ready — **başlama koşulu: MOD-0180 DL-1 CT
  doğrulaması** (sahip kararı, Beste 2026-11-13, "Önce 0180, sonra 0182": iki dilim aynı sayaçları ve tek çalışma dalını paylaşır). CT başlama
  mesajında yeni ff etiketini verir; Adım 0'daki etiket adı o mesajdakiyle değişir. Bu WP'deki "bugün" sayıları 0180 DL-1'den önceki HEAD'dendir;
  dilim başında yeniden ölçülür (`TypePrefixes`'te `WAV` olacaktır).
- **Dil:** kullanıcıya görünen her metin 7 dilde (en, tr, fr, es, zh, ar, ru); bir dil eksikse iş bitmemiştir.

## 1. Bu WP'ye yazılan kararlar

| # | Karar | Kaynak |
|---|---|---|
| Q-16 | **Yol b** — her başarılı okutma (`RecordScanCommand`) 1 merkezi kayıt doğurur; hacim ölçülünce yol c (okutma olayı defteri) değerlendirilir. Paketin r1 önerisiyle aynı (§8.12, §19.4, §21) | CT (MVP-5) kararı, 2026-11-13 |
| Q-18 | **Evet** — QR'da anahtar değil tek kullanımlık, 10 dk'lık eşleştirme kodu; anahtar `POST /barcode/devices/pair`'de üretilir, telefona bir kez gider (§8.2). DL-1 kapısı kapandı | `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-03.md:57-60` (Beste 2026-11-13; commit `3a50c44c6`) |
| Q-6 | **ZXing.Net** (Apache-2.0); NuGet DL-1'in ilk adımında (QR çizimi) | kararlar 03 `:37-39` (`bea6662d9`); paket §7 |
| Q-1, Q-2, Q-3, Q-4, Q-8, Q-15 | kapandı (öneri gibi) | kararlar 03 `:10-21`; §24 DL-1 |

Açık ama DL-1'i bekletmeyen: Q-13 (şirket bağlamı `legal_entities` + `X-Legal-Entity-Id`, Ali — §19.4), Q-14 (rol şablonu, Ali — DL-2 kapısı).

## 2. Amaç ve kapsam

**Amaç (§23 DL-1):** SCAN-BUNDLE sözleşme taslağı + cihaz kaydı ve yaşam döngüsü (QR eşleştirme dahil) + okutma olayı API'si (ayrıştırma,
bağlam doğrulaması, elle giriş, PIN aşımı) + ayar + izin, kiracı, denetim, kayıt kodu — **yalnız arka uç**.

**Kapsam (paket bölümleri):** §3 DL-1 nesneleri (`ScanDevice`, `ScanEvent`, `ScanOverride`, `SupervisorPin`, `BarcodeSettings`,
`BarcodeCommandRequest`, `ScanOverrideAttempt`, `BarcodeCollections`), §4.1–§4.4, §4.10, §4.11, §8.1–§8.6, §8.12, §8.13, §8.15, §8.16
(sözleşme kısmı), §10, §12, §13, §14, §21, §22. DL-1 yazma komutları (**10**): `RegisterScanDeviceCommand`, `UpdateScanDeviceCommand`,
`ActivateScanDeviceCommand`, `DeactivateScanDeviceCommand`, `PairScanDeviceCommand`, `RecordDeviceHeartbeatCommand`, `RecordScanCommand`,
`OverrideScanCommand`, `SetSupervisorPinCommand`, `UpdateBarcodeSettingsCommand` (§3). DL-1 servis ucu (**16**, §3): `POST /barcode/scans` ·
`POST /barcode/scans/{scanId}/override` · `GET /barcode/scan-events` · `GET /barcode/scan-events/{scanId}` · `GET /barcode/scan-events/dashboard` ·
`GET /barcode/devices` · `POST /barcode/devices` · `GET /barcode/devices/{deviceId}` · `POST …/{deviceId}/update` · `…/activate` · `…/deactivate` ·
`POST /barcode/devices/pair` · `POST …/{deviceId}/heartbeat` · `GET /barcode/settings` · `POST /barcode/settings/update` ·
`POST /barcode/supervisor-pin` (taban `/api/warehouse`). Eylem öznitelikleri `[HttpX("…")]` biçiminde, ek argümansız (§5.1 X-12, `:229` regex'i);
sabit kesimler parametreli yoldan önce, `{…Id}` `:guid` kısıtlı (§3). PUT / DELETE yok (§2.3 kural 6, AC-25).

**Kapsam DIŞI:**
- DL-2…DL-8: Web ekranları (DL-2), `scanRef` kapısı / `IScanSessionGate` / `ScanSessionBinding` ve X-2, X-3, X-14 (DL-3), etiket şablonu / basım,
  `LBL` (DL-4), GTIN çözümleme `identifiers/{gtin}`, SSCC, X-1, X-13 (DL-5 — MOD-0290-FU07 kapısı), RFID ve donanımı (DL-6, Q-7 TBD),
  uyum izi (DL-7), `Mod0182BarcodeRouteTests` kapsama testi (DL-8).
- Her türlü `frontend/**` dosyası, `.resx`'ler dahil Web kaynakları (X-10 DL-2'de).
- Mobil uygulama kodu ve mobil sözleşme notu (mobil ekipler; not CT işi — §8.16, §18). Mobil sınır:
  `execution/domains/supply-chain-execution/work-packs/mobile/2026-11-12-barcode-scanning/MOBILE-NOTE-2026-11-12-barcode-scanning.md`.
- Auth rol şablonu (X-11, ayrı WP), MOD-0365 nesneleri, çevrimdışı kuyruk (FU-0182-OFFLINE), AI (FU-0182-AI).

## 3. Adım 0 — dalı CT etiketine ileri sar

Ana checkout'ta, çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` üzerinde:

```bash
git checkout feature/sce/mod-0173-inventory-ledger-valuation
git merge --ff-only ct/line-ff-2026-11-13d
```

- **ff değilse dur**, hiçbir şey birleştirme, CT'ye bildir. (Ölçüm, bu WP yazılırken: dal `49fd410b7`'de, onay commit'i `e12bf26c8` ve kapı
  commit'i `3a50c44c6` dalda **yok**; dal `b4d3cd7bc`'nin atasıdır → ff mümkün olmalı.)
- İleri sarmadan sonra doğrula: `git merge-base --is-ancestor e12bf26c8 HEAD` ve `… 3a50c44c6 HEAD` çıkış 0; paket LF sha256 yukarıdakiyle aynı.
  Değilse dur.
- Dilim başı commit'ini (`<dilim-başı>`) kaydet; bütün "önce" ölçümleri bu commit'te alınır.

## 4. İlk adımlar (kod yazmadan önce)

### 4.1 XMC-001 kayıt denetimi
DL-1 seam'lerinin hepsi sahip onaylıdır: **X-12, X-15** (`ad1100158`), **X-6 (b)** (`4168e7a90`) ve **X-4, X-5, X-6 (a), X-7**
(kararlar 03 son maddeler, Beste 2026-11-13, "Evet, 0182 için de"). Hepsi yalnız eklemelidir; var olan satır değişmez. Bu listede olmayan
başka modül dosyası gerekirse **dur**, CT'ye yaz (XMC-001). Onay kayıtları teslim raporuna yazılır.

### 4.2 SCAN-BUNDLE sözleşme taslağı (DL-1'in ilk adımı, AC-1)
- Dosya: `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md` (yeni; §5 Repo Scope). Sürüm `1.0.0-draft`. Contract-first: controller'lardan önce.
- **Yazan:** bu dilim (geliştirici) — paket §5 ve §7 taslağı "bu paket (DL-1)" işi sayar, AC-1 DL-1 kabulüdür. **Dondurma** ayrı CT adımıdır;
  **mobil sözleşme notu** paket onayından sonra CT işidir (§8.16, §18). Taslak CT incelemesine gider; CT tek yazıcı değildir (bkz. §11 belirsizlik B-2).
- İçerik (AC-1, §8.2, §8.16 (a)–(g)):
  - §3'teki 16 DL-1 ucu: operationId, istek / cevap şeması, başlıklar (`Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`,
    `If-Match`, `Accept-Language`, `X-Scan-Device-Id`, `X-Scan-Device-Key` — §8.1, §15), §8.3 okutma sonucu kodları, §8.15 hata kodları ve gövde
    bildirimleri (`deviceKeyNotice`, `pairingCodeNotice`).
  - **QR eşleştirme akışı:** `keyDelivery` (`Direct` \| `Pairing`; Web proxy her zaman `Pairing`), `pairing: { code, qrPng, expiresAt }`, QR içeriği
    `diten-scan-pair:1:{code}`, ömür 10 dk (`BarcodeContract.PairingCodeLifetimeMinutes = 10`), `POST /barcode/devices/pair`
    `{ pairingCode, installationId }` (JWT + `devices.create`, cihaz başlığı yok), tek kullanım, 404 / 422 `BARCODE_PAIRING_EXPIRED` /
    422 `BARCODE_PAIRING_INVALID`, replay'de anahtar / kod dönmez.
  - **Şirket başına kayıt:** cihaz kaydı, `InstallationId` tekilliği, anahtar ve Web onayı `LegalEntityId` kapsamında; aynı telefon iki şirkette iki kayıt,
    iki onay; mobil anahtarı (kiracı, şirket) başına saklar; başka şirketin anahtarıyla 403 `BARCODE_DEVICE_KEY_INVALID`.
  - **`InstallationId` gösterimi:** mobil ayarlar ekranında ("Cihaz bilgisi") okunur, kopyalanabilir metin + QR (gruplama yalnız görsel; değer ham);
    Web formu QR okutmayı şart koşmaz.
  - **QR şirket taşımaz:** eşleştirme o an seçili şirketin `X-Legal-Entity-Id`'siyle gelir; seçili şirket kaydın şirketinden farklıysa **404**.
  - §8.16'nın DL-1'e düşen açıklamaları: (a) `DIFFERENT_LOT` = `Rejected`, aşılamaz; (b) önerisiz yerleştirme görevi; (e) 423 `details.lockedUntil`,
    kalan hak yok. (c), (d), (h) DL-3 davranışıdır — taslakta "DL-3'te gelir" notuyla anılır, uygulanmaz.
- Sözleşme testi controller cevaplarını taslağa karşı **kapalı şemayla** doğrular (AC-1, S-1).

### 4.3 ZXing.Net NuGet + güvenlik açığı taraması
- `ZXing.Net` paketini **yalnız** QR görüntüsünü üreten kodun bulunduğu InventoryService projesine ekle (QR_CODE çizimi, §8.2); sürümü sabit
  yaz. Paket §7 projeyi adlandırmaz — **CT kararı:** Domain projesine ekleme; QR çizimi uygulama / altyapı katmanındadır. Hangi projeye
  eklendiğini rapora yaz; emin değilsen eklemeden önce CT'ye sor. Tarama DL-1'de koşar (§7, §24); §19.4'teki "DL-4 kabul kaydında" ifadesi
  DL-4'te tekrar koşusudur — iki kayıtta da çıktı yer alır.
- Kanıt (kabul kaydına çıktı aynen): `dotnet list services/Diten.InventoryService/src/<proje>/<proje>.csproj package --vulnerable --include-transitive`
  → "bilinen güvenlik açığı yok" ya da açık listesi. Açık varsa dur, CT'ye yaz. Lisans: Apache-2.0 (kararlar 03 `:37-39`).

## 5. Dokunulacak dosyalar

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/` (§5).

**0182'nin kendi dosyaları (yeni):**
- `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md`
- `INV/Domain/Barcode/{Entities,Enums,Rules,Repositories}/` (durum makinesi, bağlam karşılaştırma ve aşılabilirlik matrisi — saf)
- `INV/Application/Features/Barcode/` (`BarcodeContract.cs`, `BarcodeErrorCodes.cs`, `BarcodeCentralAudit.cs`, `Services/` — okutma doğrulayıcı,
  bağlam okuyucuları, PIN hasher; `Resources/BarcodeText.{en,tr,fr,es,zh,ar,ru}.resx`). `Ports/IScanSessionGate` DL-3'tür, bu dilimde yazılmaz.
- `INV/Application/Features/{BarcodeScans,ScanDevices,BarcodeSettings}/` — Golden Slim (§10; `sealed record`, `Handlers/CommandHandlers` +
  `Handlers/QueryHandlers`, soneksiz handler / validator adları, dosya başına tek `public` tip, tek `{Module}Models.cs`; G-0182-01: Delete yok)
- `INV/Persistence/Barcode/` (depolar, `BarcodeCollections`, §4.10 index'leri; başka modül okumaları **yalnız `Find`**; tek ortak işlem yardımcısı
  önerisi `BarcodeTransactions.InTransactionAsync` — X-6 (b))
- `INV/Api/Controllers/Barcode/` (`BarcodeScansController`, `ScanDevicesController`, `BarcodeSettingsController`; namespace
  `Diten.InventoryService.Api.Controllers.Barcode`, `[Route("api/warehouse")]`) — **`Controllers.Warehouse`'a konmaz** (AC-6, S-6c)
- `INV/Api/Barcode/` (`BarcodeRfidManifestProvider` — ModuleCode `BARCODE-RFID`, SortOrder 182; `BarcodeServiceCollectionExtensions`; cihaz başlığı okuyucusu)
- `INV/Infrastructure/Authorization/BarcodePermissions.cs` (§14 DL-1 anahtarları; yeni fiil yok)
- `INVT/Barcode/` (birim, entegrasyon, mimari, sözleşme testleri)

**XMC-001 seam'leri (yalnız eklemeli; satır numaraları `<dilim-başı>`'nda yeniden ölçülür):**

| # | Dosya | Değişiklik | Sayı | Sahip / onay kaydı |
|---|---|---|---|---|
| X-4 | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` (`TypePrefixes`, bugün `:28`: PUT, PCK, OUT, PKG, CNT) | `DVC` sabiti + listeye ekleme (`LBL` DL-4) | +1 önek | MOD-0178 (merkez) — **onay kaydı pakette yok** (§4.1) |
| X-5 | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` | `BarcodeCollections.All` bildirimi (`wms_barcode_command_requests`, `wms_barcode_override_attempts` dahil); önek süzgeci değişmez | +1 bildirim | MOD-0173 (merkez) — **onay kaydı pakette yok** |
| X-6 (a) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(<n>, WriteCommands.Length)` (bugün `:53`, 117) + modül listesi (`:60-69`) | dilim başı ölçülen sayı **+10**, yorumda 10 komut adıyla; listeye `BarcodeRfidManifestProvider` | +10 (beklenen 117 → 127, çıkarım) | MOD-0173 (merkez, Beste) — **onay kaydı pakette yok** |
| X-6 (b) | aynı dosya `Assert.Equal(<n>, seen)` (bugün `:130`, 14; yorum `:127-129`) | dilim başı + 0182 Persistence'taki yeni `WithTransactionAsync(` çağrı yerleri; yorumda "+ BarcodeTransactions.InTransactionAsync (MOD-0182 DL-1, XMC-001)"; çağrı yeriyle **aynı commit** | öneri **+1** (14 → 15, çıkarım) | onaylı — `4168e7a90` |
| X-7 | `INV/Api/Program.cs` (manifest kayıtları, bugün `:47-63`) | `AddSingleton<IModuleManifestProvider, BarcodeRfidManifestProvider>()` + `AddBarcode()` | +2 satır | MOD-0173 (merkez) — **onay kaydı pakette yok** |
| X-15 | `INVT/Api/InventoryLedgerManifestCompletenessTests.cs` `Every_enforced_permission_is_reachable_from_the_manifest` dışlama listesi (bugün `:213-223`) | `.Where(t => t.Namespace?.EndsWith(".Controllers.Barcode", StringComparison.Ordinal) != true)` + yorum "MOD-0182: the barcode controllers belong to BARCODE-RFID (own completeness test, AC-6)" | +1 satır (+ yorum) | onaylı — `ad1100158` |
| X-12 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` (bugün `:97`, 73) ve üstündeki uç yorumu (`:80-96`) | dilim başı ölçülen sayı **+16**, yorumda 16 uç adıyla | +16 (beklenen 73 → 89, çıkarım) | onaylı — `ad1100158` (her artış dahil); **dosyayı integration-agent değiştirir** (§6 Adım G) |

X-8 (Platform `RecordCodeTypeCatalogTests`) ve X-9 (MOD-0178 §14.1) bu dilimde **değil** — `.antigravity` kapısında kapandı (`3a50c44c6`, MOD-0178 r30).

**Dokunulmayacaklar:**
- `.antigravity/**` (Çekirdek md. 6); `gateway/Diten.ApiGateway/**/ocelot.json` (değişmez — §15; yalnız integration-agent);
  `frontend/Diten.Web/Views/Shared/_Layout.cshtml` ve Archive; `services/Diten.Building.Blocks/**`.
- `INVT/Warehouse/**` ve `INVT/CycleCount/**` — **hiçbir satır** değişmez (§5.1 sonu, AC-41 kanıtı her dilimde).
- MOD-0173 / 0174 / 0175 / 0176 / 0177 / 0178 / 0180 / 0181 kodu ve testleri — yalnız yukarıdaki X satırları; listede olmayan bir dosya gerekirse
  **dur, XMC-001 ile sor** (§6).
- Donmuş sözleşmeler (`wms-bundle`, `wms-count-bundle`, `location`, `inventory-bundle`, PRODUCT-MASTER); `services/Diten.Platform/**`,
  `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, başka domain servisleri, MOD-0365 dosyaları (§6).
- Bu WP dosyası, paket, mobil not, brief'ler.

## 6. Uygulama sırası (öneri)

- **A.** Adım 0 → §4.1 → SCAN-BUNDLE taslağı (§4.2) → ZXing.Net + tarama (§4.3).
- **B.** Domain (saf kurallar ve birim testleri) → Persistence (`BarcodeCollections`, index'ler, ortak işlem yardımcısı) + X-5.
- **C.** Cihaz: kayıt (`Direct` / `Pairing`), durum makinesi, eşleştirme (atomik `findOneAndUpdate`), nabız (işlemsiz, idempotency'siz, `Version`'sız);
  `DVC` (X-4, `IWarehouseRecordCodeAllocator`).
- **D.** Okutma: §8.3 sırası (cihaz kimliği 403 → idempotency → cihaz durumu → oturum sahipliği → ayrıştırma `Gs1CodeParser.TryParse` → çözümleme →
  §8.4 bağlam → olay + merkezi kayıt aynı işlemde); elle giriş; kör sayım gizliliği (§2.3 kural 4).
- **E.** Aşım + PIN: §8.5 denetim sırası (1)–(11) bağlayıcı; sayaç ve `ScanOverrideAttempt` işlem dışında; PBKDF2-SHA256 ≥ 100.000 tur;
  parmak izinde `pin` yok; `ResponseBody` sırsız DTO.
- **F.** Ayar; manifest + izinler; X-6 (a) / (b), X-7, X-15; BL-501 işaretleri (her yazma komutu `ICentralAuditDeclaration`, `SourceModule = BARCODE-RFID`,
  nabız dahil; `ICentralAuditOutsideTransaction` yok — §8.12).
- **G. Gateway sayısı (X-12, AC-65 0178 sayı vakası) — integration-agent adımı:** controller uçları tamamlandığında integration-agent rolü
  `Mod0178WarehouseRouteTests.cs`'te yalnız sayı satırını ve yorumunu günceller; değişiklik **uçların eklendiği commit'le aynı commit'tedir**
  (arada kırmızı pencere yok). Önce: `<dilim-başı>`'nda ölçülen sayı (bugün 73); sonra: önce + 16. MOD-0180 dalı önce birleşmişse sayı yeniden
  ölçülür, yalnız 0182'nin 16 ucu eklenir. Kabul: `git diff <dilim-başı>..<dilim-sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs`
  yalnız sayı satırı + yorum. Kayıt satır numarasına değil `Assert.Equal(<n>, endpoints.Count)` satırına atıf yapar.
- **H.** Testler, sabotajlar, tam koşular, teslim raporu.

## 7. Kabul ölçütleri (DL-1: AC-1…AC-30 + AC-65 0178 sayı vakası)

Ayrıntı paket §16'dadır; her vaka paketteki metinle ölçülür (özet yetmez — aç).

| AC | Kısa başlık | Sabotajlar |
|---|---|---|
| AC-1 | SCAN-BUNDLE taslağı + kapalı şemalı sözleşme testi (QR eşleştirme, şirket başına kayıt, `InstallationId` gösterimi, QR şirket taşımaz) | S-1 |
| AC-2 | Cihaz kaydı 201 `Pending`, `DVC` numarası, anahtar bir kez; yalnız özet; replay anahtarsız (`deviceKeyNotice`) | S-2a, S-2b, S-2c |
| AC-3 | Durum makinesi; yeniden etkinleştirme eski anahtarı geçersiz kılar; QR eşleştirme (tek kullanım, 10 dk, eşzamanlılık, 404 / 422, replay) | S-3, S-3b, S-3c, S-3d |
| AC-4 | Kiracı / şirket izolasyonu 404; şirketler arası anahtar 403 | S-4a, S-4b |
| AC-5 | Gövde / sorguda `tenantId` / `legalEntityId` → 400 | S-5 |
| AC-6 | `[HasPermission]` her eylemde; manifest ad alanı; `WarehouseRegistrationTests` yeşil; kendi bütünlük vakası (X-15 karşılığı) | S-6a, S-6b, S-6c, S-6d |
| AC-7 | Cihaz kapısı: 403 / `DEVICE_PENDING` / `DEVICE_BLOCKED` / `DEVICE_WRONG_WAREHOUSE` | S-7a, S-7b |
| AC-8 | Okutma olayı insert-only; aşım ayrı `ScanOverride` | S-8 |
| AC-9 | Idempotency (okutma + komut deposu); replay sırası; oturum sahipliği; aşım replay'i 409 değil | S-9, S-9b, S-9c, S-9d, S-9e, S-9f |
| AC-10 | MOD-0174 `Gs1CodeParser` ile ayrıştırma; ham değer değişmez; 0182'de AI tablosu yok | S-10a, S-10b |
| AC-11 | `Putaway` bağlamı; `WRONG_LOCATION` tek aşılabilir durum; önerisiz görev | S-11, S-11b, S-11c |
| AC-12 | `Pick` bağlamı; hiçbir aşım yok (Q-3) | S-12 |
| AC-13 | `LOT_RECALLED` / `LOT_EXPIRED` aşılamaz | S-13a, S-13b |
| AC-14 | Kör sayımda `expected*` / bin stoğu yok; `foundStock` | S-14 |
| AC-15 | `SERIAL_DUPLICATE` | S-15 |
| AC-16 | `Query` ("Bu nedir?"); kör sayım bin'i ve toplam gizliliği | S-16, S-16b |
| AC-17 | Elle giriş gerekçesi | S-17 |
| AC-18 | Aşım §8.5: öz-aşım, PIN yok, sayaç, kilit 423 `details.lockedUntil`, denetim sırası, deneme izi, kalan hak yok | S-18a, S-18b, S-18c, S-18d, S-18e, S-18f, S-18g, S-18h, S-18i, S-18j |
| AC-19 | PIN saklama (PBKDF2); PIN log / OTel / kayıt / idempotency deposunda yok; parmak izinde `pin` yok | S-19a, S-19b, S-19c, S-19d |
| AC-20 | Nabız `Version` artırmaz, merkezi kayıt yok, `If-Match` istemez | S-20, S-20b |
| AC-21 | Sağlık okuma anında, eşik ayardan | S-21 |
| AC-22 | Ayar If-Match + aralıklar; `settings.configure` | S-22 |
| AC-23 | Merkezi denetim 1 / 0 / 0 / nabız 0; `CentralAuditDeclarationTests` +10 ve işlem çağrı yeri (X-6) | S-23, S-23b, S-23c |
| AC-24 | `DVC` MOD-0178 geçici sayacından | S-24 |
| AC-25 | PUT / DELETE yok; `LedgerImmutabilityArchitectureTests` değişmeden yeşil | S-25 |
| AC-26 | Tek SoR; başka modüle yazma yok; `BarcodeCollections.All` (X-5) | S-26a, S-26b |
| AC-27 | Eski `If-Match` 409 `BARCODE_CONFLICT`; eksik 400 | S-27 |
| AC-28 | `BarcodeText` 7 resx parite; `Accept-Language`; bilinmeyen dil `en` | S-28 |
| AC-29 | 200 okutma p95 ≤ 500 ms (bekçi değil — ölçüm kayda) | S-29 (eşiğin çalıştığını gösterir) |
| AC-30 | Bağımlılık hatası 503 `BARCODE_INVENTORY_UNAVAILABLE`, 0 olay, 0 kayıt | S-30 |
| AC-65 (0178 sayı vakası) | `Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` dilim başı + 16 ile yeşil; yalnız sayı satırı + yorum değişir (X-12) | S-65c |

**Sabotaj listesi (§24 DL-1 satırı, tam):** S-1, S-2a, S-2b, S-2c, S-3, S-3b, S-3c, S-3d, S-4a, S-4b, S-5, S-6a, S-6b, S-6c, S-6d, S-7a, S-7b, S-8,
S-9, S-9b, S-9c, S-9d, S-9e, S-9f, S-10a, S-10b, S-11, S-11b, S-11c, S-12, S-13a, S-13b, S-14, S-15, S-16, S-16b, S-17, S-18a, S-18b, S-18c, S-18d,
S-18e, S-18f, S-18g, S-18h, S-18i, S-18j, S-19a, S-19b, S-19c, S-19d, S-20, S-20b, S-21, S-22, S-23, S-23b, S-23c, S-24, S-25, S-26a, S-26b, S-27,
S-28, S-29, S-30, S-65c (**67 sabotaj**).

**Beklenen kırmızı test adları (pakette adı geçenler):** S-6c → `WarehouseRegistrationTests`; S-23 / S-23b / S-23c → `CentralAuditDeclarationTests`
(S-23b / S-23c: `Every_transaction_a_command_can_run_in_enlists_the_central_audit_event`); S-25 → `LedgerImmutabilityArchitectureTests`;
S-26b → `SingleSystemOfRecordArchitectureTests`; S-65c → `Mod0178WarehouseRouteTests.Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route`
(geçici `[HttpGet("barcode/count-sabotage")]` + `[HasPermission(BarcodePermissions.ScanEventsRead)]`). Diğerlerinin 0182 test adı **pakette yok** —
geliştirici `INVT/Barcode/` altında adlandırır ve raporda her `S-n` ile eşler.

Kurallar: her sabotaj **üretim koduna** uygulanır (kendi kopyasıyla ölçen test guard değildir — Çekirdek md. 10); kırmızı çıktı alınır, bozma bayt-eşit
geri alınır, **commit'e girmez**; listede olmayan her kırmızı adıyla raporlanır.

## 8. Test koşuları

- **InventoryService test paketi bütünüyle, filtre yok** (§17; servis dokunuldu): `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`.
  Taban `<dilim-başı>`'nda aynı gün ölçülür (kırmızılar adıyla); sonda yeni kırmızı yok.
- **Gateway test projesi:** `dotnet test gateway/Diten.ApiGateway.Tests` (AC-65 0178 sayı vakası; önce / sonra).
- **Platform `RecordCodeTypeCatalogTests` (DVC):** `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~RecordCodeTypeCatalogTests`
  — `3a50c44c6`'daki hâliyle değişmeden yeşil (bu WP o dosyaya dokunmaz).
- **MOD-0178 Warehouse bekçileri değişmeden yeşil:** `WarehouseRegistrationTests`, `INVT/Warehouse/**` ve `INVT/CycleCount/**`; kanıt:
  `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount` **boş**.
- Ayrıca değişmeden yeşil: `AuthorizationGuardTests`, `LedgerImmutabilityArchitectureTests`, `ManifestNamespaceOwnershipTests`; X-satırlarıyla yeşil:
  `SingleSystemOfRecordArchitectureTests`, `CentralAuditDeclarationTests`, `InventoryLedgerManifestCompletenessTests`.
- Mimari: `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` (AGENTS.md §5).
- Build: `Diten.InventoryService` PASS (§17).

**Ortam:**
- Port **27017 kullanma**. **`INVENTORY_TEST_MONGO` ayarlama** — paket ayar yoksa kendi replika setini başlatır (§17).
- **`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşma.**
- Portlar yalnız AGENTS.md §3 tablosundan (Inventory 5067, Gateway 5000); port uydurma.
- Ortam notu (paket dışı, CT bilgisi): ana checkout'ta Inventory 5067 `dotnet watch` ile çalışıyorsa derleme kilidi olabilir; derleme / sabotaj koşusu
  ayrık bir worktree'de yapılabilir — commit yine çalışma dalına.

## 9. Git kuralları (GIT-002)

- Çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`** — yeni dal açılmaz.
- `main`'e commit / push yok.
- Stage yalnız **açık dosya yoluyla** (`git add <yol> …`); `git add -A` / `git add .` yok. İzlenmeyen `__pycache__/` ve review ham dosyaları stage'e girmez.
- Hook atlama yok (`--no-verify` yok), imza atlama yok; yıkıcı git komutu (`reset --hard`, `push --force`, `checkout --`) onaysız yok.
- Yerel commit'ler dilim içinde serbest; X-6 (b) ve X-12 kendi çağrı yeri / uçlarıyla **aynı commit'te**.
- **Push / PR yok** — geliştirme sonunda ayrı kullanıcı onayıyla; push / PR'dan önce `origin/main` alınır (merge commit ile, squash değil).
- Commit mesajı sonu: `Co-Authored-By` satırı oturumun atıf kuralıyla.

## 10. Teslim raporu biçimi

1. **Commit listesi** (`git log --oneline <dilim-başı>..HEAD`) ve `git diff --stat`.
2. **Test sayıları önce / sonra:** InventoryService (toplam / geçen / kalan / atlanan, kırmızılar adıyla), Gateway, Platform `RecordCodeTypeCatalogTests`, mimari.
3. **Sabotaj tablosu:** her `S-n` (67) için — üretim dosyası:satır, komut, kırmızı test adı + çıktı özeti, geri alma, geri yeşil kanıtı.
4. **Sayılar önce → sonra:** Gateway `Assert.Equal(<n>, endpoints.Count)` (beklenen 73 → 89), `WriteCommands.Length` (117 → 127), `seen` (14 → 15) —
   her biri satıra atıfla, ölçüldüğü commit ile.
5. **NuGet:** ZXing.Net sürümü, eklendiği proje, `dotnet list … package --vulnerable --include-transitive` çıktısı aynen.
6. **SCAN-BUNDLE taslağı:** yol, uç sayısı (16), sözleşme testi adı.
7. **XMC-001:** X-4, X-5, X-6 (a), X-7 onay kayıtları (commit) ya da "onay yok — dokunulmadı"; `INVT/Warehouse/**` / `INVT/CycleCount/**` boş diff kanıtı.
8. **AC-29 ölçümü:** p95 değeri.
9. **Açık kalanlar:** sorular, ölçülemeyenler (nedeniyle), sonraki dilime devredilenler.

## 11. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001).
- **Backend / handler:** `handler-design`, `repository-standard`, `response-envelope`, `pipeline-behaviors`, `entity-base-template`, `entity-versioning`,
  `mongo-indexing`, `api-conventions`, `routes`, `ports`, `audit-trail-standard` (AUD-001).
- **Kullanıcıya görünen metin** (`BarcodeText` resx, okutma `message`): `localization-standard`, `dynamic-localization-standard`.
- **Yetki / izin:** `permission-key-standard` (PKS-001 `warehouse.barcode.*`), `business-module-enforcement-standard`, `data-scope-enforcement`.
- **Manifest / kayıt kodu:** `module-self-registration-standard`, `record-coding-standard` (RCS-001 `DVC`).
- **Ortam, kayıt:** `dev-runbook`, `configuration-safety` (yeni NuGet), `logging-observability` (PIN / anahtar / kod loglanmaz), `status-reporting-and-evidence` (REP-001).
- **Mimari:** `erp-architecture`, `diten_standards`.
- Ekran kuralları (`frontend-*`, `unauthorized-surface-standard`, `page-help-and-keyboard`) bu dilimde yok — DL-2.

## 12. Belirsizlikler (CT'ye; geliştirici kendi başına çözmez)

- **B-1 (kapandı):** X-4, X-5, X-6 (a), X-7 — sahip onayı kararlar 03 (Beste 2026-11-13); §4.1.
- **B-2 (CT kararı):** SCAN-BUNDLE taslağını geliştirici yazar (§5 / §7 / AC-1); §18'deki CT işi taslağın içeriğine (şirket başına kayıt, QR)
  yazılan CT kararlarıdır, taslak yazımı değil. CT taslağı inceler ve dondurur (MOD-0180 DL-1 ile aynı düzen).
- **B-3 (CT kararı):** X-12 satırını geliştirici oturumu **integration-agent alt ajanıyla** yazdırır (Adım G): geliştirici ölçülen önce sayıyı
  ve DL-1 uç listesini integration-agent'a verir, o yalnız sayı satırını ve yorumu düzenler; değişiklik uçlarla aynı commit'e stage edilir.
  Geliştirici `Mod0178WarehouseRouteTests.cs`'i kendisi düzenlemez.
- **B-4 (CT kararı):** ZXing.Net projesi — §4.3.
