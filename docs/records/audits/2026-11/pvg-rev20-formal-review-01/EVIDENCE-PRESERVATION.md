# Kaynak baytlarını koruma — boşluk kontrolü istisnası

2026-11-20. Git diff --cached --check iki ham kanıt dosyasında toplam beş satır sonu boşluğu bildirdi:

- pvg-rev20-discovery-01/discovery-report.txt: üreticinin kısalttığı WP-0230-DL7A bulgu satırının son boşluğu; v2 rapor hash'ine dahildir.
- docs/records/reviews/MOD-0230/2026-11-20-rev20-formal-dialogue-01.md: ilk dört satırdaki Markdown iki-boşluk hard-break işaretleri, inceleyenin özgün çıktısıdır.

Bu iki kaynak trim edilmedi, rapor hash'i yeniden uydurulmadı. Gerçek check-report sıfır ve parse_findings sıfır biçim hatası verdi. Üretici veya guard kodu değiştirilmedi. Genel whitespace kontrolünün bu beş notu ham kanıt koruma gerekçesiyle kaydedilir; diğer dosyalar ayrı gerçek git diff --check ile ölçülür. Kod veya ürün doğrulamasında istisna yoktur.

Önceki eksik-PYTHONPATH ve CRLF taşıma denemeleri Git dışında, aynı görevin reviews/diagnostics/pvg-rev20-discovery klasöründe saklandı. Son kanonik rapor gerçek üreticinin LF stdout modu ile üretildi; capture.json gerçek çıkış ve hash'leri içerir.
