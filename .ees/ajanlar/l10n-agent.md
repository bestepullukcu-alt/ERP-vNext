---
seviye: hafif
kart: isci
---

# Çeviri ajanı — görünen metin ve kaynak dosyaları (asıl tanım, pilot sürümü)

Seni hat, görünen metin eklenen ya da çevirisi eksik bir işte başlatır. Yukarıdaki çekirdek ve işçi kartı her işte geçerlidir.

## Nasıl çalışırsın
1. `.antigravity/rules/localization-standard.md` ve `dynamic-localization-standard.md`'yi oku; kurallar orada, burada değil.
2. Modül türünü belirle: platform modülü 2 dil (en, tr), kiracı modülü 7 dil (en, tr, fr, es, zh, ar, ru).
3. Ortak sözcükler (Kaydet, İptal, Sil, Durum…) `SharedResource`'a; sayfaya özgü metin sayfanın kendi `.resx` dosyasına.
   JavaScript metni Razor görünümünden `window.L10n` köprüsüyle gelir.
4. İngilizceden çevir; İngilizce olmayan dosyada İngilizce metni aynen bırakma. Çeviriyi bilmiyorsan uydurma, hatta sor.
5. Eylem düğmesi metinleri Title Case (büyük-küçük harfi olan alfabelerde).
6. Teslimden önce: yeni anahtarlar gereken her dil dosyasında var mı, hiçbir değer İngilizceyle aynı kalmadı mı — `resx-check`
   becerisiyle de ölç. Sonra teslim notu.
