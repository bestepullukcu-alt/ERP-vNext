---
name: kullanim-kilavuzu
description: "EES kullanıcı kılavuzu adımları: modülün tek dosya HTML kılavuzunu yazar. Yeni akışın belge ajanı kullanır."
disable-model-invocation: true
---

> **Üretilmiş kopya — elle düzenlemeyin.** Kaynak: `.ees/beceriler/kullanim-kilavuzu/SKILL.md` · kaynak özeti: `sha256:a71b218df6cd` · yeniden üret: `python3 .antigravity/scripts/sync_adapters.py`

# Kullanım kılavuzu — modülün kullanıcı kılavuzunu yazma (asıl tanım)

`belge-ajani`'nın kullandığı adımlar. Eski `user-manual-generator` tanımından sadeleştirildi.

## Çıktı
- Tek dosya HTML: `docs/guides/<modül>/index.html`; dış kaynak yok, görüntüler dosyaya gömülü.
- Açık ve koyu temada okunur; telefon genişliğinde yatay kaydırma yok.

## İçerik
1. Modül ne işe yarar (iki üç cümle) ve kim kullanır (rol).
2. Her ekran için: nereden açılır, alanlar ne anlama gelir, sık yapılan işler adım adım.
3. Yetkisiz kullanıcı ne görür; hangi izin gerekir.
4. Sık sorulanlar ve hata mesajlarının anlamı.

## Kurallar
- Ekran görüntüsü yalnız yerel geliştirme ortamındaki örnek veriden; canlı veri, kişi adı, müşteri verisi yok.
- Yalnız birleşmiş kodda var olanı anlat; planlanan özellik "henüz yok" diye bile yazılmaz.
- Kılavuz metni modülün dillerinde gerekiyorsa brifte yazar; yazmıyorsa hatta sor.
