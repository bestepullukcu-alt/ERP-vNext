# MOD-0230 — inceleme 13 Control Tower karşılaştırma notu

- **İnceleme:** `docs/records/reviews/MOD-0230/2026-10-29-2d3f1177.md`. Codex, GPT-6; kimlik `/root`, UUID doğrulanamadı.
- **Hedef:** revizyon 13, `2d3f1177e763bc403a679ad945dcff39f47b05820d85e1d8c6cef19407597e16`.
- **Sonuç:** KOŞULLU. İki bulgu var: F-1 ve F-2, ikisi de ORTA. ENGEL yok.
- **Kayıt notu:** Sahip yanıtı Codex arayüzünden kopyaladı. Kopyada ilk satırın "M" harfi düşmüştü; dosyaya geri kondu. Sohbet arayüzüne ait satırlar ("Benim için onayla", model seçici, sürükle-bırak yardımı) incelemenin parçası olmadığı için alınmadı. Bulgu satırları ve Sonuç satırı aynen kopyalandı.

## F-1 — CT değerlendirmesi: doğru

- **Bulgunun söylediği:** AC-32, altı seçenek kodunun modülün hiçbir görünüm ve JS kaynağında geçmemesini istiyor. Ama ayrı SourceType alanı bu kodlardan üçünü meşru olarak kullanıyor. Doğru bir uygulama bile taramadan geçemez.
- **Ölçtüğüm:** bulgu doğru.
  - `_Form.cshtml:60–62` SourceType seçiminde `Spontaneous`, `Literature` ve `Study` değerlerini taşıyor.
  - Paket satır 1104 ("Option-Set Ownership") SourceType'ı yeni ucun kapsamı dışında tutuyor.
  - Koddan resx adını çözen köprü anahtarları da kodu içerebilir. Bu yüzden tarama etiket eşlemesini de liste sayar.
- **Etkisi:** Ölçütün amacı doğru: rapor türü ve ürün rolü listesi ekrana kopyalanmasın. Ölçüm biçimi yanlış: kodun geçtiği her yeri kopya sayıyor.

## F-2 — CT değerlendirmesi: doğru

- **Bulgunun söylediği:** `pvg event country offers only unknown` testi koşulsuz. Ülke listesi geldikten sonraki onaylı davranışı reddediyor.
- **Ölçtüğüm:** bulgu doğru.
  - "Country rule table" Present satırı (paket ~482) ve AC-26 (satır 1420), liste geldiğinde listedeki kodların kabul edilmesini istiyor.
  - Yalnız "unknown" beklentisi, listenin bulunmadığı bugünkü duruma (Absent satırı) aittir.
- **Etkisi:** Test bugünkü aşama için doğru, sonraki aşama için yanlış. Yazıldığı haliyle onaylı kapsamı daraltıyor.

## Karar: onaydan önce düzelt (CT)

Revizyon 13 henüz onaylanmadı ve hiçbir iş bu sürüme bağlı olarak dağıtılmadı. İki bulgu da yalnız AC-32'nin metnine dokunuyor. Bu yüzden düzeltme revizyon 13 içinde yapılır ve yeni sha için dar bir inceleme turu (14) açılır.

Neden önce düzeltiliyor? Revizyon 12'de sürüm açık bulgusuyla onaylanmış, düzeltme sonraki revizyona bırakılmıştı. Burada aynı yol iki onay ve iki PR satırı gerektirirdi. Üstelik WP-0230-DL4D dağıtıldıktan sonra yapılacak bir revizyon, kart S-86 gereği o işin ve WP-0230-DL8'in kabulünün yeniden bağlanmasını zorunlu kılardı. Onaydan önce düzeltmek tek onay, tek satır demek ve yeniden bağlama gerektirmiyor.

Kapanış: F-1 ve F-2, düzeltilmiş metin inceleme 14'te kontrol edilince kapanır. "düzeltildi@commit" bir kapanış değildir.
