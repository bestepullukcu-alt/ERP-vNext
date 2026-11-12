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
