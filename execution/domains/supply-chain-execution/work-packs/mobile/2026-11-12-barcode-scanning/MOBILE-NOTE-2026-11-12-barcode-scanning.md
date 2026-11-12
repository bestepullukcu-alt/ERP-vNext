# Mobil ekiplere not — Depo barkod / RFID okutma (MOD-0182) ve cihaz kaydı (MOD-0365)

> **Kime:** iOS ve Android ekipleri · **Kimden:** MVP-5 WMS Control Tower · **Tarih:** 2026-11-12
> **Durum:** **erken ihtiyaç notu.** Modülün paketi henüz yazılmadı; API uçları, alan adları ve hata kodları paket onaylanınca ayrı
> **sözleşme notu** ile gelecek. Bu not ne yapılacağını ve nasıl davranılacağını anlatır; alan adı ve uç adresi vermez.
> **Kaynak:** mockup brief'i (`execution/domains/supply-chain-execution/work-packs/mockups/barcode-scanning/BRIEF-MOD-0182-MOD-0365-barcode-devices.md`),
> mockup tur 1 (`Barkod RFID Prototip`, telefon görünümü), sahip kararları `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-01.md`
> ve `-02.md`. Ürün sahibi kararı (Beste, 2026-11-12): **depo çalışanının okutma ekranı mevcut mobil uygulamada** yapılır; yönetim
> ekranları (tarama olayları, uyum izi, cihaz sağlığı, ayarlar) Web'de kalır.

## 1. Kısa özet
- Depo çalışanı mal kabul, rafa yerleştirme, toplama, paketleme ve sayım işlerinde barkodu / karekodu (GS1 DataMatrix) / RFID'yi **mobil
  uygulamayla** okutur; sistem her okutmayı **anında doğrular** ve sonucu tam ekran gösterir.
- Okutma kaynakları: **el terminali tarayıcısı** (ör. Zebra), **telefon kamerası**, **RFID el okuyucu**. Telefon da **cihaz olarak kayıtlıdır**.
- v1 **yalnız çevrimiçi**: bağlantı yoksa okutma yapılmaz (çevrimdışı tampon yok).
- Sizden istenen (şimdi): §7'deki sorulara yanıt; uygulamanızın bugünkü durumu (§6 kontrol listesi). Geliştirme, sözleşme notu gelince başlar.

## 2. Kararlar (ürün sahibi) ve mobil için anlamı
| # | Karar | Mobil için anlamı |
|---|---|---|
| S-1 | v1 yalnız çevrimiçi | Bağlantı yoksa okutma ekranı "Bağlantı yok, okutma yapılamaz" der; yerelde kuyruk tutulmaz, sonradan gönderme yok |
| S-2 | RFID ve etiket basımı v1'de | RFID el okuyucu desteği ve "Etiket bas" akışı v1 kapsamında |
| S-3 | Telefon kameraları **cihaz olarak kaydedilir** | Uygulama ilk açılışta cihaz kimliğini kaydettirir (tür: Telefon-kamera); engellenmiş cihazdan okutma reddedilir |
| S-7 | Vardiya sorumlusu PIN'i | Yalnız **farklı lot** ve **yanlış raf** uyarısında PIN ile aşılabilir; **geri çağrılmış lot** ve **SKT geçmiş** retlerinde PIN seçeneği **hiç gösterilmez** |
| S-8 | RFID raf taramasında fark | Fark listesi + "Yeniden tara" + "Sayım başlat"; stok mobilde değişmez |
| S-10 | Gerekçe listeleri | Elle giriş ve yeniden basım gerekçeleri sabit liste; sonda "Diğer" (açıklama zorunlu) |
| S-12 | Tam ekran geri bildirim | **Kabul 1 sn'de kendiliğinden kapanır**; **ret dokununca** kapanır |
| E-1 | Etiket önizlemesi | Basmadan önce etiketin görüntüsü (barkod + insan okunur alanlar) gösterilir |

## 3. Mobilde olacak ekranlar / akışlar
| # | Akış | Ne olur |
|---|---|---|
| A-1 | Görev bağlamında okutma | Mal kabul, yerleştirme, toplama, paketleme, sayım görevinde beklenen ürün / raf / miktar görünür; okutma sunucuya gider, sonuç döner |
| A-2 | Sorgu ("Bu nedir?") | Okutulan barkodun ürün, lot, SKT, raf ve durum bilgisi |
| A-3 | Kamera ile okut | Kamera açılır, karekod çerçeveye hizalanır; izin yoksa "Kamera izni gerekli" + elle giriş seçeneği |
| A-4 | Elle giriş | Barkod / ürün kodu, lot, seri (varsa), **gerekçe zorunlu**; kayıt "elle girildi" olarak işaretlenir |
| A-5 | Sorumlu PIN onayı | Yalnız S-7'deki iki uyarıda; onay sorumlunun adıyla kaydedilir |
| A-6 | RFID toplu okuma | Beklenen / okunan sayısı, fark listesi, yeniden tara / sayım başlat |
| A-7 | Etiket bas | Tür (raf, palet SSCC, koli GTIN + lot + SKT, RFID kodlama), yazıcı (her basımda seçilir), adet, **önizleme**, yeniden basımda gerekçe. Basım **sunucu üzerinden** ağ yazıcısına gider; mobil yazıcıya doğrudan bağlanmaz (öneri, §7 soru 4) |
| A-8 | Ses ve titreşim | Cihaz bazında ayar (kabul / ret sesleri, titreşim) |

**Sonuç türleri (tam ekran):** Kabul · Ret: yanlış ürün · Ret: yanlış raf · Ret: SKT geçmiş · Ret: geri çağrılmış lot · Ret: tanınmayan barkod ·
Ret: engelli cihaz · Uyarı: farklı lot (PIN) · Uyarı: seri tekrar. Metinler sunucudan / yerelleştirmeden gelir, uygulamada sabit Türkçe metin olmaz.

**Mobilde OLMAYACAKLAR (Web'de):** tarama olayları listesi, uyum izi ve kanıt paketi (PDF / CSV), cihaz sağlığı ve cihaz engelleme,
dış sistemler, eşik ayarları.

## 4. Doğrulama kuralları — kim yapar
- **Bütün doğrulama sunucuda.** Mobil yalnız okunan ham değeri, görev bağlamını ve cihaz kimliğini gönderir; ürün / raf / lot / SKT / geri
  çağırma kararını sunucu verir. Mobil kendi başına "kabul" demez.
- GS1 çözümleme (GTIN, lot, SKT, seri, SSCC) sunucuda yapılır; mobil ham dizgeyi (FNC1 / grup ayırıcı dahil) **değiştirmeden** gönderir.
- Her okutmanın bir kimliği (`scanRef`) olur; aynı okutma tekrar gönderilirse sunucu aynı cevabı verir (idempotent).

## 5. Bağımlılıklar (mobil dışı, bilgi)
- Barkoddan ürün bulma, MDM'de GTIN işinin (MOD-0290-FU07) main'e girmesine bağlı.
- Cihaz kaydı ve engelleme MOD-0365 (merkez / Platform).
- Kimlik doğrulama, kiracı / şirket başlıkları ve yanıt zarfı mevcut mobil sözleşmelerle aynı olacak (`X-Tenant-Id`, `Response<T>`).

## 6. Bugünkü durum — kontrol edin, yazın
| # | Soru |
|---|---|
| D-1 | Uygulama el terminali tarayıcısından (Zebra DataWedge / Honeywell vb.) okuma alabiliyor mu? Hangi yöntemle (klavye benzetimi, intent, SDK)? |
| D-2 | Kamera ile GS1 DataMatrix okunabiliyor mu (FNC1 / grup ayırıcı korunuyor mu)? Hangi kütüphane? |
| D-3 | RFID el okuyucu SDK'sı (ör. Zebra RFID) entegrasyonu var mı, iOS'ta mümkün mü? |
| D-4 | Uygulamada cihaz kimliği / kaydı kavramı var mı? |
| D-5 | Depo kullanıcıları (depo çalışanı, vardiya sorumlusu) bugün uygulamaya giriyor mu, yoksa uygulama yalnız saha satış ekibi için mi? |

## 7. Sorular
1. iOS ve Android ikisi de mi gerekir, yoksa depoda yalnız Android (el terminalleri) mi? (Öneri: v1 Android; iOS yalnız kamera ile sorgu — ekip görüşü?)
2. Tahmini iş büyüklüğü ve takvim (paket onayından sonra).
3. Ses / titreşim ve tam ekran geri bildirim için mevcut tasarım bileşeniniz var mı?
4. Etiket basımı: sunucu üzerinden ağ yazıcısı (öneri) mı, yoksa Bluetooth mobil yazıcı da gerekir mi?
5. Uygulamanın dil desteği: 7 dil (tr, en, fr, es, zh, ar, ru) ve sağdan sola (ar) var mı?
