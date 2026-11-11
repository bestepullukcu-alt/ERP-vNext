# MOD-0286-FU01 — Control Tower karşılaştırması 03 (inceleme 03, SOP §20.4-4)

- **Hedef:** paket revizyon 03 @ `705917c5c` (sha256 `411cb1c4…`). Bulgu dosyası `2026-11-01-411cb1c4.md`.
- **İnceleyen:** Claude alt ajan (`claude-opus-5-5`, high istendi, ajan beyanı 30, doğrulanamadı). Kule oturumu başlattı, geçmişsiz, girdi yalnız ZIP `52559018…`. Yazar ajandan ayrı, ama aynı üst oturumda; yol sahibin kararıdır (Kurulum sohbetinde, Yönetici iletti). Araç hücreleri metin olarak karşılaştırır; üst oturumun aynı olduğu burada açıkça yazılır.
- **Sonuç:** KOŞULLU, engel yok. Ham metin sha `9d115179…`, hiçbir karakteri değişmedi.
- **Kayıt ve sıradaki tur:** sahibin sözü "Düzelt, bir tur daha" (2026-11-01, bu sohbet; ikinci kez, bu sürüm için). Kart §1.1/7: tur sonunda orta kalırsa yine sahibe sorulur.

## Değerlendirme

| Bulgu | Düzey | CT | Ölçüm ya da gerekçe |
|---|---|---|---|
| F-22 | orta | kabul, öneri değişti | Paket satır 69 yalnız sözlüğü tutmayı ister, SB-14 `--base` koşmaz (paket okundu). İnceleyenin önerdiği FU01-18 `VARIABLE`'ın tek karakter bozulmasını yakalamaz: bu biçime iki kol da uyar (`src="~/assets/js/@x.js"`), bir kol bozulunca diğeri yine yakalar. Her anahtar için bozulma ve FAIL veren vaka revizyon 04'te ölçülerek seçildi. |
| F-23 | düşük | açık | Kart §1.1/7: düşük bulgu paketi değiştirmez. `secici` tip açıklamalı atamayı okumaz; işçi prompt'una "tip açıklamasız atama" yazılacak (dağıtım öncesi). |
| F-24 | düşük | açık | Aynı. İşçi prompt'unda ya da sonraki pakette. |
| F-25 | düşük | açık | Aynı. Bugünkü 1305 yakalamayı etkilemiyor. |
| F-26 | düşük | açık | Aynı. Dağıtım öncesi `oracle siniflar` ile yeniden ölçülecek. |

## Tür olarak öğrenme

Sık bulgular listesine önerilecek tür: "sabit ile paket aynı diye ölçmek, kodun o sabiti kullandığını göstermez; sabotaj, sabiti bozup davranışın değiştiğini görmeli". Sıradaki kural penceresinde Kurulum'a iletilecek.
