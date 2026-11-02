# MOD-0286-FU01 — Control Tower karşılaştırması 02 (inceleme 02, SOP §20.4-4)

- **Hedef:** paket revizyon 02 @ `391e6fe5c` (sha256 `065a45b4…`). Bulgu dosyası `2026-10-31-065a45b4.md`.
- **İnceleyen:** Claude alt ajan (`claude-opus-5-5`, high istendi, ölçülemedi), Yönetici · Ofis oturumu başlattı, yazar oturumdan ayrı, girdi yalnız ZIP `87463b07…`. İnceleme yolu sahibin kararıdır; karar Kurulum sohbetinde verildi, Yönetici iletti.
- **Sonuç:** KOŞULLU, engel yok. Ham metin sha `771e8175…`, hiçbir karakteri değişmedi (sahip ve tarih hücreleri istek 02'nin biçimiyle dolu geldi).
- **Kayıt ve sıradaki tur:** sahibin sözü "Düzelt, bir tur daha" (2026-11-01, bu sohbet). Kart §1.1/7: bu tur ortalar için son turdur; ondan sonra engel yoksa kendiliğimden yeni tur açmam.

## Değerlendirme

| Bulgu | Düzey | CT | Ölçüm ya da gerekçe |
|---|---|---|---|
| F-13 | orta | kabul | Oracle'ın `rules` işlevi deseni paketten okuyor, `VARIABLE` yok (`uic-fu01-oracle.py:40-44`); AC-7 kendi kendini ölçüyor. Üst paketin karşılığı `uic_changed_pages.py:1310-1316`. Revizyon 03: seçici sabitinin paket bloğuyla karşılaştırılması ve yedi biçim için öz test. |
| F-14 | orta | kabul | Oracle'da üç kip var, yalnız sınıf dosyaları sayılıyor (`:129`, `:144`); pakette `:205` oracle'dan türetildi diyor, AC-3 sayıları sabit. Kayıtlar değişmez olduğu için yeni dosya: `uic-fu01-oracle-02.py`. Revizyon 03. |
| F-15 | orta | kabul | CT eksiği: ZIP'e önceki bulgu dosyası konmadı. İstek 03'te iki önceki bulgu dosyası ve karşılaştırmalar ZIP'te. Paket değişmez. F-11 açık (paketin O-2'si), F-12 kapalı (karşılaştırma 01). |
| F-16 | düşük | kabul, kısmen | Kanıt betiği ve oracle sabotajı dağıtım öncesi şartı, paket öyle diyor. İstek 03 ZIP'ine önceki eksiklerden yalnız var olan girdiler eklenir. |
| F-17 | düşük | açık | Kart §1.1/7: düşük bulgu paketi değiştirmez. Görünüme HEAD'de olmayan J eklenmesi ve sınıf dışı K'ye taşıma, işçi notu olarak dağıtım prompt'una yazılacak. |
| F-18 | düşük | açık | Aynı. Kanıt betiğine iki dal için sabotaj eklenecek (dağıtım öncesi). |
| F-19 | düşük | açık | Aynı. Bugünkü 1305 yakalamayı etkilemiyor. |
| F-20 | düşük | açık | Aynı. Dağıtım prompt'unda açıklanacak. |
| F-21 | düşük | açık | Aynı. Madde paketin kendi listesi; araç KOŞULLU sonucu engel yoksa durdurmuyor. Son inceleme de KOŞULLU kalırsa sahibin kararıyla gidilir. |

## Tür olarak öğrenme

Sık bulgular listesine önerilecek tür: "bir ölçüm betiği paketten okuduğu değeri paketle karşılaştırıyorsa ölçtüğü şey kendisidir; bağlayıcı değerin üretim kodundaki kopyası ayrıca ölçülmeli". Sıradaki kural penceresinde Kurulum'a iletilecek.
