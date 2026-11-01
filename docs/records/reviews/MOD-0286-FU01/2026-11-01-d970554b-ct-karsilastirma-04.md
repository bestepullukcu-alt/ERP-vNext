# MOD-0286-FU01 — Control Tower karşılaştırması 04 (inceleme 04, SOP §20.4-4)

- **Hedef:** paket revizyon 04 @ `404dcfbcb` (sha256 `d970554b…`). Bulgu dosyası `2026-11-01-d970554b.md`.
- **İnceleyen:** Claude alt ajan (`claude-opus-5-5`, high istendi, ajan beyanı 30, doğrulanamadı). Kule oturumu başlattı, geçmişsiz, girdi yalnız ZIP `b6c678b1…`. Yazar ajandan ayrı, ama aynı üst oturumda; yol sahibin kararıdır (Kurulum sohbetinde, Yönetici iletti). Bağımsızlığın sınırı SOP §20.4/5.
- **Sonuç:** UYGUN. Ham metin sha `6717d7ee…`, hiçbir karakteri değişmedi.
- **Kayıt:** sahibin sözü "Evet, hazırla" (2026-11-01, bu sohbet). Kart §1.1/7: düşük bulgu paketi değiştirmez ve tur açmaz; paket bu sürümde donar.

## Değerlendirme

| Bulgu | Düzey | CT | Ne zaman, nerede |
|---|---|---|---|
| F-27 | düşük | açık | Kanıt betiği: SB-14 satırlarında önek eşleşmesi ve `<TAB>` açılımı; SB-14e'nin düzeltilmiş koşusunun çıkışı 1 olarak denetlenir (dağıtım öncesi). |
| F-28 | düşük | açık | Kanıt betiği bozulmayı metin değiştirerek değil `ast` konumuyla yapar ya da işçi prompt'u `r'''...'''` yazımını ister (dağıtım öncesi). |
| F-29 | düşük | açık | Yorum içi etiketle başvurulan satır vakası işçi prompt'una ek vaka olarak yazılır (dağıtım öncesi). |
| F-30 | düşük | açık | Paket başlığının tarihi eski. İlk onayda paket donar; paket bir daha değişirse düzeltilir. |

Önceki açık düşükler (F-16...F-21, F-23...F-26) karşılaştırma 02 ve 03'teki yerlerinde duruyor. F-11 bu hazırlıkta kapanır: kayıt defteri satırı aynı dalda eklendi.

## Tür olarak öğrenme

Sık bulgular listesine önerilecek tür: "tek karakterlik sabotaj, birden çok yoldan yakalanan bir biçimle ölçülürse hiçbir şey kanıtlamaz; her dal için yalnız o dalın yakaladığı biçim seçilmeli". Sıradaki kural penceresinde Kurulum'a iletilecek.
