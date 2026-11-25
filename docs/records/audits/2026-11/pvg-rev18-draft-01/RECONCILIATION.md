# Revizyon 18 — ayrı sohbet sonucunun CT karşılaştırması

Tarih: 2026-11-20. Taban `9fa2ed6755ade67c3a09d70681560e75139f9e4c`.
Hedef paket: `058deb6bda47eae34d742b2c4a3014ddf1fa8fb8b1d32361fd8e60c9d833a61b`.
Ham rapor: `docs/records/reviews/MOD-0230/2026-11-20-rev18-separate-chat-01.md`.
İncelenen gerçek baytlar reviewed-pack.txt ve reviewed-domain-config.txt içinde hash doğrulanarak saklandı; bunlar alternatif otorite dosyaları değildir.

## Sonuç ve CT kararı

Ayrı inceleyen F-1–F-5'i taslak düzeyinde karşılanmış buldu; F-5 politika kararı hâlâ sahibi bekliyor. Yeni bir ENGEL ve üç ORTA bulguyla UYGUN DEĞİL dedi. CT kaynakları okudu ve dördünü de kabul etti:

- F-6: `PvgIntakeDraftApplicationServiceTests.Cross_tenant_mutations_do_not_leak_existence_through_evidence_link_denial` permission-only günlüğü ve field çağrısı yokluğu istiyor. Politika önerisi benimsenirse yalnız Update beklentisinin hizalanması gerekir; tam test yolu ve pvg-regpv-base doğrulaması kapsamda eksikti. Diğer yanıt/veri/workflow/evidence/Triage/Route korumaları korunmalı.
- F-7: `_Form.cshtml` AntiForgeryToken'ı gerçek HTML'ye yazıyor, `form.js/antiForgeryHeader` o girdiyi başlığa koyuyor. Browser-visible sentetik istek tokenı gerçek gönderimle taşınmalı; yalnız cookie/JWT C# tarafında kalır. Köprünün token ekleyerek üretim JS kusurunu örtmemesi açık olmalı. Önceki CT briefindeki genel 'antiforgery C# tarafında' cümlesi bu ayrımı eksik ifade ediyordu.
- F-8: Doğrulayıcı evidence dizisinin sayısını kontrol ediyor; application BuildIntake diziyi aynı elemanlarla taşıyor. Null eleman reddi görünmüyor. Kaynak çıkarımı, çalışmış API deneyi değildir. Taslak ölçütü null eleman için gerçek API create/read olumlu örneği, kayıpsız koruma ve hatalı sayı/nesne elemanları için güvenli ret ayrımını tanımlamalı; API sessizce sıkılaştırılmaz.
- F-9: Aynı doğrulayıcı, sahip sözleşmeleri olmadığı için dolu CompanyRecipientPersonId, GlobalProductId ve PresentationGskuId değerlerini reddediyor. Her create alanını doldur talimatı bu yüzden uygulanamaz. Bugün kabul edilen alanlar ve açık null kalması gereken üç alan ayrı yazılmalı; sözleşme kapıları aşılmaz.

## İleri adım

Module-pack-author'a yalnız pack/domain revizyon 19 taslağı verildi; ürün/test/CI koduna dokunulmaz. Yeni kapsam, onaylı veya uygulanmış sayılmaz. Sonraki ayrı sohbet turunda yeni kesin baytlar okunacak. DL-12 BLOCKED kalır; ürün testi, derleme, CI, sabotaj veya geliştirme dağıtımı yapılmadı.
