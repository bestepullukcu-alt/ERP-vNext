# MOD-0182 Barkod / RFID ve MOD-0365 Cihaz / Uç — sahip kararları 02 (2026-11-12)

Kaynak: mockup tur 1 (`Barkod RFID Prototip (tek dosya).html`, açık sorular S-1…S-12; CT incelemesi 2026-11-12) ve kararlar 01
(`mod0182-mod0365-owner-decisions-01.md`). Sahip: Beste (MVP-5 WMS CT oturumu `ca7382cf`). Paket yok.

| # | Konu | Karar | Tarih |
|---|---|---|---|
| S-7 | Vardiya sorumlusu PIN'i neyi aşar | **Farklı lot ve yanlış raf** aşılabilir; **geri çağrılmış lot ve SKT'si geçmiş ürün hiçbir zaman** aşılamaz; aşım tarama olayına sorumlunun adıyla yazılır | 2026-11-12 |
| S-8 | RFID raf taramasında beklenen ≠ okunan | Fark listesi + yeniden tarama; fark sürerse sayım (MOD-0181) başlatılabilir; **stok kendiliğinden değişmez** | 2026-11-12 |
| S-9 | Yetkisiz kullanıcıda menü | UAS-001: menü ve üst çubuk kalır, içerikte tek açıklama | kural |
| S-10 | Elle giriş / yeniden basım gerekçe listeleri | **Sabit liste** + sonda "Diğer" (açıklama zorunlu) | 2026-11-12 |
| S-11 | Sessiz cihaz ve düşük pil eşiği | **24 saat** ve **%20**; ayar sayfasından değiştirilebilir | 2026-11-12 |
| S-12 | Tam ekran geri bildirimin kapanması | **Kabul 1 sn'de kendiliğinden** kapanır; **retler dokunuşla** kapanır | 2026-11-12 |
| E-1 | Etiket basımında görüntü | **Basmadan önce etiket önizlemesi** gösterilir (barkod + insan okunur alanlar; GS1 alanları) | 2026-11-12 |
| E-2 | Mockup tur 2 | **Yaptırılır** (sahip: "mockup'ı düzelttirelim bir daha"); kararlar 01 / 02 ile hizalı düzeltme listesi CT'den | 2026-11-12 |

Kararlar 01'in mockup'a yansımayanları (tur 2'de düzelir): S-1 v1 yalnız çevrimiçi (06-11), S-2 RFID + etiket v1'de, S-3 telefon kameraları
**cihaz olarak kaydedilir**, S-5 MOD-0365 sahibi merkez, S-6 İTS ayrı modül (MOD-0247). S-4 (barkoddan ürün bulma MOD-0290-FU07 GTIN'e bağlı) —
bağımlılık, Ali (MDM).

| E-3 | Okutma ekranının yeri | **Mevcut mobil uygulamada** (iOS / Android ekipleri) — depo çalışanının okutma akışları; yönetim ekranları (tarama olayları, uyum izi, cihaz sağlığı, dış sistemler, ayarlar) Web'de. Mobil ekiplere erken ihtiyaç notu: `execution/domains/supply-chain-execution/work-packs/mobile/2026-11-12-barcode-scanning/MOBILE-NOTE-2026-11-12-barcode-scanning.md`; API sözleşme notu paket onayından sonra | 2026-11-12 |
| E-4 | Sahadaki diğer iş akışları | **A:** sahada barkod okutarak yapılan bütün akışlar (MOD-0178 yerleştirme, toplama, paketleme; MOD-0181 sayım formu) mobil uygulamada toplanır; mevcut donmuş API'lerle (WMS-BUNDLE 1.1.0, WMS-COUNT-BUNDLE 1.0.0) MOD-0182 paketini beklemeden başlanabilir; Web'deki telefon uyumlu ekranlar yedek olarak kalır. Mobil not §3A ve mobil mockup brief'i güncellendi | 2026-11-12 |

Mockup tur 2 (2026-11-12): Web `Barkod RFID Prototip v2` — CT incelemesi: S-1 (çevrimiçi, "Bağlantı yok" uyarısı), S-2, S-3 (Telefon-kamera cihazı, engellenebilir), S-5, S-6, S-7 (PIN yalnız farklı lot / yanlış raf), S-8 (Yeniden tara / Sayım başlat), S-10, S-11 (ayar sayfası: sessiz cihaz süresi, düşük pil eşiği), S-12 ve E-1 (etiket önizlemesi, SSCC) işlenmiş; paket yazımına girdi olarak **kabul**. Küçük notlar pakette düzeltilir: eski açık sorular listesi (S-3 "iki seçenek", S-5 "Belirlenmedi") karar listesiyle yan yana duruyor; dil seçici görünmüyor (7 dil kuralı paketten). Mobil `Mobil Okutma Prototip`: beş görev akışı (kabul, yerleştirme, toplama, paketleme, sayım) görev listesinden açılıyor; MS-1…MS-10 önerileri mockup'ta; MS-8 — mobil uygulamanın gerçek tasarım dili mockup'çıya verilmedi (uyarlama sonra).
| E-5 | Mobil ana sayfa / menü | Web'deki gezinme çubuğuyla aynı mantık: kullanıcının erişimi olan **bütün modüller** mobil alışkanlığına uygun kutucuklar olarak; bölümler Web menü gruplarıyla aynı; izni olmayan görünmez; kaynak modül kataloğu + sayfa manifestleri + izinler (mobil "benim menüm" ucu CT netleştirir) | 2026-11-12 |
| E-6 | Görevlerin kaynağı | **Bütün depo görevleri Görev Merkezi'nden** (kişiye ne atandıysa; e-imza onaylarındaki desen). Bugün depo görevleri Görev Merkezi'ne bağlı değil → MOD-0178 / MOD-0181 görevleri için Görev Merkezi sağlayıcı bağlantısı yeni iş (merkez); mobil ara dönemde modül uçlarından okur, ekranı Görev Merkezi modeline göre kurar. Ayrıca sahip talebi: **Web'de hangi modüllerin Görev Merkezi'ne bağlanması gerektiği analiz edilecek** (mobil netleştikten sonra) | 2026-11-12 |
