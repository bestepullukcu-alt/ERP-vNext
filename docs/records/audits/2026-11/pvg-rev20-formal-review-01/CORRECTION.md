# Aynı onaylı kapsam — son teknik netleştirme

2026-11-20. Yazar yalnız mevcut pack/domain dosyalarını değiştirdi; rev20 numarası, ready-for-dev/build open/human approved ve operational closed korundu.

Yeni pack SHA256: 729ac90976f8038b7d6e7c39b0348a51be485999ae7e094c46aea87264bcdab5.
Yeni domain SHA256: 914c0c1dab0979d3ec48e3d4f6e08646ad0bf4494f9b73a97fa27bca4c5ead9c.

CT gerçek parser ile 45 kriter/sıfır sorun ölçtü; yalnız AC42 ve45 netleşti, AC1–39/43/44 aynı. Her iki dosyanın ürün/test/CI yol kümeleri aynı. Windows sentetik fixture taşınabilirliği, normal kapılı iki ürün hazırlık WP'sinin feasibility çıktıları, kabul edilmiş ±ofsetli zamanlar ve mevcut port tahsisinin doğru durumu kaynakla karşılaştırıldı. Teknik ölçüm ve yürütme netleştirmeleri mevcut kullanıcı scope/build-test onayı içinde; yeni iş kuralı, dosya yolu, kimlik veya canlı izin yok.

Bu, yeni baytların insan tarafından ayrıca okunmuş veya resmî incelenmiş olduğu iddiası değildir. Kullanıcının onayladığı rev20 kapsamı aynen uygulanır; kesin yeni hash'in resmî inceleme ve HIGH defter kapıları zorunludur. Eski7f hashinin iki olumsuz resmî incelemesi gerçek review-row aracıyla append-only deftere kaydedilir, silinmez. Yeni hash bu eski olumsuzları ezmez; ayrı sürüm olarak tekrar ölçülür. Sonraki kapsam genişletmesi mevcut rızadan türetilemez.

Ürün/test/CI kodu, build/test/npm-ci/restore/Mongo/sabotaj/CI çalıştırması yok. Portable araç işinin yalnız smoke başarısı ayrı kayıttadır. Ana feature WP iki hazırlığın bağımsız E2 entegrasyonu gelene kadar BLOCKED; hazırlıklar kendi normal kapıları geçmeden başlamaz.
