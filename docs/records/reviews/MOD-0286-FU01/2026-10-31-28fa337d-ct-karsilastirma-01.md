# MOD-0286-FU01 — Control Tower karşılaştırması 01 (inceleme 01, SOP §20.4-4)

- **Hedef:** paket revizyon 01 @ `fc8b0fef0` (sha256 `28fa337d…`). Bulgu dosyası `2026-10-31-28fa337d.md`.
- **İnceleyen:** Claude alt ajan (`claude-opus-5-5`, high istendi, ölçülemedi), Yönetici · Ofis oturumu başlattı, yazar oturumdan ayrı, girdi yalnız ZIP `20ee8ee9…`. Sahibin kararı Yönetici sohbetinde, bu sohbette değil; kart §1.1/3 ile SOP §20.4/2 farkı Kurulum'a soruldu, cevap yok (Yönetici'den aktarım).
- **Sonuç:** ham metnin son satırı "KOŞULLU", ama açık bir engel var. İstek metni bu durumu söylemiyordu (CT eksiği). Satırda `UYGUN DEĞİL`, istek 02 kuralı yazıyor.
- **Kayıt:** sahibin sözü "Evet" (2026-10-31, bu sohbet).

## Değerlendirme

| Bulgu | Düzey | CT | Ölçüm ya da gerekçe |
|---|---|---|---|
| F-1 | engel | kabul, başka çözüm | `scripts/uic_changed_pages.py:1094-1095` okundu. İnceleyen AC-10'a istisna önerdi; CT tersini seçti: boş farkta yalnız süre satırı, çünkü üst paket AC-20 ve PR #325 artefaktı buna dayanıyor. Revizyon 02. |
| F-2 | orta | kabul | `?v=@ver` iki desene de takıldı (`fu01_inceleme_olc.py`). Revizyon 02: SCRIPT önce gelir. |
| F-3 | orta | kabul | Birleştirmeli `Url.Content` ve tırnaksız `src` ikisine de takılmadı; CDN yolu ve `.json` yanlış yakalandı (aynı betik). Revizyon 02. |
| F-4 | orta | kabul | AC-8 sahibi ölçmüyordu (paket okundu). Revizyon 02. |
| F-5 | orta | kabul, kısmen | İstek 02 ZIP'ine araçlar, onay dosyaları ve üç kabul kaydı eklendi. Blueprint eklenmedi; `verify_module_id.py` çıktısı pakette. |
| F-6 | düşük | kabul | Görünüm ve JS listeleri, bağımsız ölçüm çıktıları ZIP 02'de. |
| F-7 | düşük | kabul | Revizyon 02: sabotaj sayıları iş tabanından türer. |
| F-8 | düşük | kabul | Revizyon 02. |
| F-9 | düşük | kabul | Revizyon 02. |
| F-10 | düşük | kabul | Revizyon 02. |
| F-11 | düşük | açık | Kayıt defteri satırı henüz yok (paketin O-2'si); onay satırından önce sahibin sözü gerekir. |
| F-12 | düşük | kabul | Bu kayıtta hücreler dolduruldu; istek 02 çıktı biçimi araca uyuyor. |

## Tür olarak öğrenme

Sık bulgular listesine önerilecek iki tür: "bir satırın her zaman basılacağını söyleyen paket, var olan boş çıktı testini aramalı" ve "makineyle okunan desen, olumsuz örneklerle (birleştirme, tırnaksız, dış URL) sınanmalı". Liste başka kulelerin de dosyası; 14 Kasım kuralı sonrası değil, sıradaki kural penceresinde Kurulum'a iletilecek.
