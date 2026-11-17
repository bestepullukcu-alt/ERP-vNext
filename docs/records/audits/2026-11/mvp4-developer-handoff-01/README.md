# MVP-4 geliştirici hattı devir notu — 17 Kasım 2026

Bu kayıt çalışma ve karar envanteridir; yeni kabul ölçütü, pack onayı veya merge kararı değildir. Ölçüm tabanı: origin/main 0a4438aebf436bd5f6ca3a190df13c93e6b9f08a (git fetch origin --prune sonrası). Gizli bilgi ve ortam bağlantı değeri içermez.

## Modüller

| Modül | Pack ve teslim durumu | Kalan kapı |
|---|---|---|
| MOD-0188 Demand Planning | Pack ready-for-dev; PlanningService, dosya geçmişi, S3 tahmin/Draft/yayın ve Demand v2'nin dar atomiklik, checksum, CI broker taşıyıcı dilimleri main'de. #228, #422 ve #423 merge edildi. | AC-D07 canlı PRODUCT-MASTER kanıtı; AC-D18; AC-D24 boş/sıfır/eksik/bilinmeyen ayrımı; AC-D27 tam kullanıcı-şirket kapsamı; Demand v2 dondurması, Superseded bağlı tarihsel içerik ve canlı tüketici. MOD-0183 gerçekleşmiş sevkiyat aşaması üreticiye bağlı. Pack hedef alanı 20 Kasım; tam modül kabul tarihi değildir. |
| MOD-0189 MRP & Replenishment | Pack ready-for-dev; PlanningService içindeki haftalık netting, canlı Published Demand/Inventory okuması, tarih düzeltmesi ve SafetyStock tüketimi dar dilimleri main'de. | Açık PO bakiye/tarih üretici sözleşmesi ve MOD-0141 aktarımı; bound run ve Superseded kanıt sözleşmesinin ayrı onayı; Demand v2 freeze. Sonraki kod dilimi için güncel pack_approval bağımsız onay kapısı tekrar ölçülmeli; önceki onay geçerli varsayılmaz. Pack hedefi TBD. |
| MOD-0191 Safety Stock Optimization | Pack ready-for-dev; geçici SafetyStockService çekirdek, politika/revision, okuma, gerekçeli manuel miktar ve MRP tüketici sözleşmesi dilimleri main'de. | Etkinleştirme, override ve tenant ekranı yalnız kapsam önerisi; yeni pack revizyonu, ayrı aktör, eski Approved kayıtların geçişi, izin ve tarih çakışması ürün kararları bekliyor. Pack hedefi TBD. |
| MOD-0190 S&OP; MOD-0192 Capacity | MVP-6 sahipliğinde. | MVP-4 devir kapsamı dışında. |

Yukarıdaki main'e alınmış dar dilimler tüm modül AC'lerinin kapandığı anlamına gelmez.

## CT kuyruğu ve R8

Supply Chain CT-QUEUE kaynağında üç MOD-0188 satırı ölçüldü: WP-MOD0188-S3-CONTRACT-VERIFY = CT_ACCEPTED_E2; WP-MOD0188-S3-FORECAST-COMMAND ve WP-MOD0188-S3-FORECAST-PUBLISH-E2E = VERIFIED, canlı ve tarayıcı kanıtları açık. Kayıt, kalan tüm MVP-4 işlerinin yetkili görev kaynağı değildir. execution/delivery/ct-queue-registry.tsv içinde MVP-4 için doğrulanmış kaynak satırı görünmedi.

#411, yalnız MOD-0188/0189/0191 için 11 Kasım–31 Aralık süreli R8 istisnasını öneren açık PR'dır. Metnine göre istisna ancak main'e girince yürürlüğe girer. R8 genel geçişi 18 Kasım'da biter. Bu nedenle #411 şimdi kapatılmamalı: sahibi ya bu istisnayı inceleyip main'e almalı ya da kayıtlı MVP-4 görev kaynağını açmalıdır. #411 tek başına pack veya WP onayı vermez.

## Dallar ve inceleme

| Uzak dal | Devir kararı | Neden / inceleme |
|---|---|---|
| feature/sce/mod-0191-activation-override-ui-scope | PR | Tek dosyalık kapsam önerisi; pack revizyonu ve ürün kararları öncesi kod yetkisi vermez. Main'e göre güncellenip ayrı belge PR'ı açılmalı. |
| feature/sce/mod-0188-uom-pack-revision | PR | Zaten açık taslak #256. AC-D07 pack revizyonu bağımsız CT-APR-01/02 inceleme ve onay bekliyor. Yeniden PR açılmamalı. |
| feature/sce/mod-0188-product-master-uom | PR | Zaten açık taslak #227. #256 onayı ve PlanningService üzerinden canlı kabul/dönüşüm/karantina HTTP kanıtı olmadan merge hazır değil. Varsayılan SingleUnitInterim kalır. |
| feature/sce/mod-0188-demand-v2-hardening | eskidi | Eski Superseded niyeti ve taslak sayfalama dalı; sonraki #228/#422/#423 dar işleri main'e alındı. Ayrı merge öncesi kalmış farklar yeniden incelenmeli; dal otomatik silinmez. |
| feature/sce/mod-0189-mrp-replenishment | eskidi | Ekim pack dalı; güncel pack ve MRP çekirdeği main'de. Yereldeki tek ek izin commit'inin metni güncel main pack §14 içinde bulunuyor. Dal otomatik silinmez. |

#227/#256 sahibi incelemesi ve #411 kararı bekler. Başka açık ElikPenco PR'ı bu ölçümde yok. Merge yetkisi devralan sahip/CT'dedir.

## Yerel Git ve kayıp riski

İki worktree ölçüldü: ERP-vNext-git (başlangıçta feature/sce/mod-0188-outbox-index-followup, temiz) ve ERP-vNext-mod0188-evidence (feature/sce/mod-0189-mrp-core). Stash yok. git log --branches --not --remotes çıktısı yalnız 9c7ee5de5: MOD-0189 izin metni; aynı izinler güncel main pack'te bulunduğundan etkin bir kod farkı değildir. İkinci worktree'de izlenmeyen docs/records/analysis/mod0188-acceptance/2026-10-13-evidence-baseline.md var. Bu 13 Ekim tarihli salt okunur eski çizelge; 0 kanıt hazır dediği taban bugünkü main'i temsil etmiyor. Kaynak dosya silinmedi veya stage edilmedi. Bire bir arşiv kopyası bu PR'da LEGACY-EVIDENCE-BASELINE-2026-10-13.md adıyla bulunur (SHA-256 AD4D446FFA0371427CD4EAB486436A406668B4D831146D18CF74FC4D69EACC6A). Güncel kabul kanıtı olarak kullanılmaz.

## Açık kararlar, sözler ve tarihler

- #423 Y-3 main'de: riskli eski bileşik outbox indeksi varsa worker broker'dan önce durur; dar kusur kapanışı kaydedildi, yeni AC kabulü sayılmaz. Y-2 düşük öncelikli: Hint olmadan Mongo planlayıcısının yeni indeksi seçtiğini gösteren explain kanıtı. Eski indeksin canlı veritabanında kontrollü kaldırılma tarihi bildirilmedi; operasyon sahibi bildirmeli.
- MOD-0188: PRODUCT-MASTER v1.1 freeze ve canlı tüketici kanıtı; tarihsiz dönüşümün tarihsel otorite sayılmaması; v1.2 conversionId/asOf; Demand v2 merkezî dondurma ve bağlı Superseded okuma; kullanıcı-şirket yetkili kümesi. #256 bağımsız incelemesi #227'den önce.
- MOD-0189: MVP-2/MOD-0141 sürümlü açık PO kalan miktarı ve beklenen teslim tarihi; bound run kanıtı ve yeni bağımsız pack onayı. Açık PO gelene kadar DEGRADED işareti korunur.
- MOD-0191: ayrı Activate komutu ve yeni değişmez override kaydı ürün kararı var; izin, eski Approved kayıt geçişi, tarih çakışması ve Gateway/ekran kapsamı hâlâ onay ister.
- Merkezi ekiplere bırakılan işler: BL-501 audit bağlantısı, Platform yetkili şirketlerim, BL-628 şirket saat dilimi, BL-629 mali yıl; MOD-0183 sevkiyat 3.3 ve PRODUCT-MASTER tarihli dönüşüm üreticileri. Bunların tamamlandığı varsayılmamalı.
- Yakın tarih: R8 geçiş bitişi 18 Kasım; MOD-0188 pack target 20 Kasım; #411 önerilen istisna bitişi 31 Aralık. Diğer açık incelemeler için doğrulanmış kesin tarih yok.

## Devir sınırı

Bu notla yerel commit ve izlenmeyen eski çizelge görünür hâle gelir. Yeni kod, AC kapanışı, pack onayı veya başka ekibin servisine müdahale yapılmadı. Devir PR'ını Ali/CT inceler ve merge eder.
