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
