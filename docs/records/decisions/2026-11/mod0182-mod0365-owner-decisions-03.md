# MOD-0182 Barkod / RFID ve MOD-0365 Cihaz / Uç — sahip kararları 03 (2026-11-13)

Kaynak: sahip sohbet cevabı, Beste 2026-11-13 (MVP-5 WMS CT oturumu `ca7382cf`): sahibe sorulan Q-1, Q-2, Q-3, Q-4, Q-5, Q-7, Q-8, Q-9, Q-10,
Q-11, Q-12, Q-15, Q-17 için **"hepsi öneri gibi"**. Sorular ve öneriler: MOD-0182 paketi r1 §19.4
(`execution/domains/supply-chain-execution/module-packs/MOD-0182-barcode-rfid.md`, commit `dbbb8dd04`, LF sha256 `26b4b4a2…`). Kararlar 01 ve 02
geçerlidir. Paket r2 bu kararları işler. Sahip: Beste.

| # | Konu | Karar | Tarih |
|---|---|---|---|
| Q-1 | İzin ad alanı | **`warehouse.barcode.*`** — `warehouse.*` altında MOD-0182'ye ayrılmış adlı alt ad alanı (MOD-0180 `warehouse.waves.*` deseni; taban yol `/api/warehouse/barcode`) | 2026-11-13 |
| Q-2 | Cihaz kaydının (kayıt, onay, engelleme, sağlık) sahibi | **MOD-0182** (Blueprint SoR "device registrations"); MOD-0365 yalnız dış sağlayıcı profili | 2026-11-13 |
| Q-3 | Toplamada farklı lot / yanlış raf | **v1'de PIN ile de aşılamaz**; PIN aşımı yalnız yerleştirmedeki yanlış raf için; lot / bin değişimi FU (FU-0178-LOTSWAP) | 2026-11-13 |
| Q-4 | Sorumlu PIN'i | PIN 0182'de özetiyle, kişi kendisi belirler; **90 gün** geçerli; **3 yanlışta 5 dk kilit**; geri alınan yetki PIN süresi dolana kadar geçerli (bilinen sınır, FU-0182-PIN-LIVE) | 2026-11-13 |
| Q-5 | Etiketin yazıcıya ulaşması | **Sunucu PDF üretir**; Web tarayıcının, mobil Android'in yazdırma penceresiyle basar; sunucu yazıcıya bağlanmaz; RFID etikete yazma v1'de yok | 2026-11-13 |
| Q-7 | RFID okuma cihazı | **Android telefona bağlı RFID okuyucu** (ör. Bluetooth); donanım kararı gelene kadar DL-6 başlamaz. **Donanım seçimi açık — TBD, sahibi Beste** (mobil ekiple) | 2026-11-13 |
| Q-8 | Yeni cihaz kaydı | **Onay bekler** (`Pending`, Web'den onay) | 2026-11-13 |
| Q-9 | Okutma zorunluluğu | **Depo başına ayar, varsayılan kapalı** | 2026-11-13 |
| Q-10 | SSCC kapsamı | **0178 kolisine bir SSCC + içeriksiz palet etiketi**; koli → palet birleştirmesi MOD-0246; uzantı hanesi 0 | 2026-11-13 |
| Q-11 | SSCC etiketinde alıcı bilgisi | **Yalnız şehir ve posta kodu**; kişi / müşteri adı yok | 2026-11-13 |
| Q-12 | "RFID tek uç" | **Yalnız el okuyucu**; kapı / portal okuyucu yok | 2026-11-13 |
| Q-15 | Build / Buy | **Build** (Blueprint `Buy/Partner` sapması W-0182-01; düzeltme FU-0182-EA) | 2026-11-13 |
| Q-17 | Etiket boyutları | **100×150, 100×50, 50×25 mm** sabit liste | 2026-11-13 |

**Bu kararla açılan CT işi:** mobil not
`execution/domains/supply-chain-execution/work-packs/mobile/2026-11-12-barcode-scanning/MOBILE-NOTE-2026-11-12-barcode-scanning.md:106`
("Cihaz kaydı ve engelleme MOD-0365") Q-2'ye göre düzeltilecek. Paket r2 mobil nota dokunmaz.

**Açık kalanlar (bu kayıt kapatmaz):** Ali'ye Q-6 (barkod çizim kütüphanesi), Q-13 (şirket bağlamı teyidi), Q-14 (rol şablonları); CT'ye Q-16
(okutma olayının merkezi denetim yolu); Q-7'nin donanım seçimi (TBD, Beste). Q-1'e bağlı `.antigravity` kapısı (PKS-001 §4 `warehouse.barcode.*`
satırı + `:92` / `:105`, RCS-001 §3 `DVC` / `LBL`, aynı adımda `RecordCodeTypeCatalogTests` X-8 ve MOD-0178 §14.1 notu X-9) **paket onayından önce**
kapanacak iştir; `.antigravity/**` değişikliği ayrı sahip onayı ister.

- **2026-11-13 — Q-3 genişletme teyidi** (kaynak: sahip sohbet cevabı, Beste 2026-11-13, "evet"): farklı lot (`DIFFERENT_LOT`) hiçbir okutma
  bağlamında PIN ile aşılamaz — yerleştirmede de. v1'de PIN ile aşılabilen tek durum yerleştirmede yanlış raftır (`Putaway` x `WRONG_LOCATION`);
  lot değişimi FU-0178-LOTSWAP'a kalır. MOD-0182 r2'deki yazar çıkarımı böylece sahip kararı oldu.

- **2026-11-13 — Q-6 = ZXing.Net** (kaynak: sahip sohbet cevabı, Beste 2026-11-13: "Ali'ye sorma, biz karar veririz; ücretsizse
  kullanabiliriz"): barkod çizimi (Code 128, GS1-128, DataMatrix) için ZXing.Net kullanılır — Apache-2.0 lisanslı, ücretsiz ve ticari
  kullanıma açık. Soru Ali'ye gitmez; paket bir sonraki revizyonda Q-6'yı "karar 03" olarak işler.

- **2026-11-13 — XMC-001 sahip onayları** (MOD-0182 r3; kaynak: sahip sohbet cevabı, Beste 2026-11-13, "dördüne de evet"):
  - **X-15:** `InventoryLedgerManifestCompletenessTests.cs` dışlama listesine `.Controllers.Barcode` (DL-1) — onaylandı.
  - **X-12:** `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` uç sayısı onayı (`Assert.Equal(<n>, endpoints.Count)`) uç ekleyen
    her MOD-0182 dilimi (DL-1, DL-4, DL-6, DL-7) için, dilim başında ölçülen sayı + kendi uçları, uçlarla aynı commit'te; MOD-0180 ile
    hangisi önce gelirse ölçülen sayıdan devam; kabul kaydı önce → sonra yazar; dosyayı integration-agent değiştirir — onaylandı (her artış dahil).
  - **X-1:** MOD-0174 `Gs1CodeParser`'a kapalı "okunur, kullanılmaz" AI listesi (37), (310n), (240); liste dışı AI'lar reddedilmeye devam eder;
    MOD-0174 gs1-check ucunun da bu AI'ları kabul etmesi yan etkisi kabul — onaylandı (DL-5 öncesi).

- **2026-11-13 — r3 incelemesi sonrası sahip kararları** (kaynak: sahip sohbet cevabı, Beste 2026-11-13, "ikisine de evet"):
  - **X-6 genişletmesi (XMC-001):** `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs`
    `Every_transaction_a_command_can_run_in_enlists_the_central_audit_event` işlem sayısı satırı (bugün `Assert.Equal(14, seen)`, yorum
    `:127-129`) işlem açan her MOD-0182 dilimi tarafından dilim başında ölçülen sayı + dilimin yeni `WithTransactionAsync` çağrıları ile, aynı
    commit'te güncellenir; yorum satırına dilim adı yazılır; kabul kaydı önce → sonra yazar (MOD-0181 DL-1 ve MOD-0180 X-10 emsali) — onaylandı.
  - **Cihaz anahtarının telefona teslimi:** Web'den kayıt ya da yeniden etkinleştirmede üretilen anahtar telefona **QR kod** ile verilir;
    QR tek kullanımlık ve kısa ömürlüdür, anahtar sunucuda yalnız özetiyle tutulur — onaylandı (ayrıntı paket r4'te, SCAN-BUNDLE'da).

- **2026-11-13 — Q-18 = evet** (MOD-0182 r5 / r6 incelemeleri; kaynak: sahip sohbet cevabı, Beste 2026-11-13): QR'da anahtarın kendisi değil, tek
  kullanımlık ve kısa ömürlü (10 dk) bir **eşleştirme kodu** bulunur; telefon kodu `POST /barcode/devices/pair` ile gönderir, anahtar o anda
  üretilip yalnız telefona bir kez verilir; anahtar Web ekranında hiç görünmez, sunucuda yalnız özeti saklanır. 4168e7a90'daki "anahtar QR ile
  verilir" kararının uygulama biçimi budur. DL-1 kapısı kapandı.
- **2026-11-13 — `.antigravity` kapısı onaylandı** (sahip sohbet cevabı, Beste 2026-11-13, "ikisine de evet"): PKS-001 :92 adlı alt ad alanı cümlesi,
  `warehouse.*` satırı ve yeni `warehouse.barcode.*` satırı; RCS-001 §3 `DVC` ve `LBL` satırları; aynı adımda X-8 (`RecordCodeTypeCatalogTests`
  `DVC` / `LBL`); X-9 (MOD-0178 §14.1 notu) MOD-0178 r30 ile.

- **2026-11-13 — X-4, X-5, X-6 (a), X-7 (XMC-001 sahip onayı)** (WP-0182-DL1 taslağının bulgusu: paket §5.1 "dilim başlamadan sahibinden" der,
  kayıt yoktu; kaynak: sahip sohbet cevabı, Beste 2026-11-13, "Evet, 0182 için de" — MOD-0180'in X-6 / X-9 / X-10 / X-11 onayıyla aynı emsal):
  MOD-0182 yalnız eklemeli yazar — X-4 MOD-0178 `WarehouseRecordCodeRules.cs` `DVC` (DL-1) ve `LBL` (DL-4); X-5 MOD-0173
  `SingleSystemOfRecordArchitectureTests.cs` `BarcodeCollections.All`; X-6 (a) MOD-0173 `CentralAuditDeclarationTests.cs` yazma komutu
  sayısı (dilim başı ölçülen + dilimin komutları) ve sağlayıcı listesi; X-7 MOD-0173 `Program.cs` `BarcodeRfidManifestProvider` + `AddBarcode()`.
  Var olan satırlar değişmez.
- **2026-11-13 — Q-16 = yol b (CT — MVP-5 CT; paket "CT (+ Ali)" yazar, r1 önerisiyle aynı):** her okutma olayı merkezi denetime gider;
  hacim ölçülünce yol c (okutma olayı defteri) yeniden değerlendirilir. Ali'nin payı: yol c'ye geçiş kararı gelirse Ali'ye sorulur.
- **2026-11-13 — dilim sırası (sahip, Beste, "Önce 0180, sonra 0182"):** MOD-0182 DL-1, MOD-0180 DL-1'in CT doğrulamasından sonra başlar
  (aynı sayaçlar ve tek çalışma dalı); sayılar o anki değerden ölçülür, `TypePrefixes`'te `WAV` o zaman vardır.
