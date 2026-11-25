# Revizyon 17 — ayrı sohbet incelemesinin CT karşılaştırması

Tarih: 2026-11-20. Taban: `9fa2ed6755ade67c3a09d70681560e75139f9e4c`, dal `chore/ct/pvg-ct-01`.
Hedef paket SHA-256: `ecd02b5e1af5173d003d32bf496ad9988507b26b76770e81005f3c6b0eb342e1`.
Ham sonuç: `docs/records/reviews/MOD-0230/2026-11-20-rev17-separate-chat-01.md`.
İnceleyen: ayrı sohbet `01a1f849-c6bd-7ba3-b788-6ebf7a7551e9`, Codex `gpt-6.2-astra/high`.

## Kaynakla karşılaştırma

- F-1 kabul: `_Form.cshtml` SourceReference/PatientSubjectCode girdilerinde `required` var; `PvgIntakeFieldDefinition.ApprovedFields` ikisini de isteğe bağlı tanımlıyor. `form.js` submit yolu `checkValidity()` başarısızsa ağ isteğinden önce dönüyor. Edit için dar kapsam ve gerçek Razor test örnekleri gerekiyor; Create davranışı bu bulguyla değiştirilmez.
- F-2 kabul: `form.js/loadVersion`, sürüm mevcutsa eksik `draft` ile de `versionReady=true` yapıyor. Mevcut sürüm çakışması testi yalnız `items[].version` yanıtıyla başarılı gönderim bekliyor. `PvgCaseIntakeDraftResponse` saklanan intake üyelerinin tamamını henüz taşımıyor. Eksik üye ile açık null ayrımı ve sıfır güncelleme koşulu sözleşmede ölçülmeli.
- F-3 kabul: `SafetyCaseIntake.MarkTriaged` TriageOutcome, `MarkRoutePending` RouteTargetQueue dolduruyor. Bu örneklerde yalnız durum korumasını kaldırmak diğer alan korumasını kaldırmaz; önerilen sabotajın 200 vereceği iddiası geçersiz. Durum ve her marker koruması bağımsız fixture ile ölçülmeli.
- F-4 kabul, uygulanabilirlik incelemesi sürüyor: `run_phase1_gates.sh` Web testlerini dotnet ile çalıştırıyor; jsdom kurulumu ayrı JS workflow'unda. Tek ProjectReference, Node/jsdom araçlarını aynı çalışma ortamına sağlamaz. Yeni CI yolu varsaymadan temiz çalışma alanı, kesin kurulum komutu ve bağımlılık eksikliğinde skip yerine başarısızlık sözleşmesi hazırlanacak. Kaynak okuması çalıştırılmış deney sayılmaz.
- F-5 kabul, sahip kararı açık: update yolu kayıt bulunamadığında field-policy öncesinde farklı yanıt veriyor; detay okuması policy'yi depodan önce değerlendiriyor. Paketin önerdiği sıranın kayıt varlığı üzerindeki sonucu açık değildi. Yazar tek gerekçeli öneri ve var/yok kayıt ölçütlerini taslakta sunacak; CT politika seçimini sahip adına yapmaz.

## Karar ve görev dağılımı

Önceki alt ajan taslak incelemesinin UYGUN sonucu bu ayrı sohbetin olumsuz sonucunu geçersiz kılmaz. Güncel revizyon 17 için üç açık ENGEL ve iki ORTA bulgu var; DL-12 BLOCKED kalır.

Kullanıcının devam talebi kapsamında mevcut module-pack-author ajanına yalnız paket/domain revizyon 18 taslağı verildi. Ayrı readiness ajanına F-4 için salt okunur, dar uygulanabilirlik görevi verildi. En çok iki etkin ajan; yazıcı yalnız paket yazarıdır. Ürün/test/CI kodu, build, ürün testi, dağıtım, push veya PR başlatılmadı.

Yeni paket baytlarına önceki onay taşınmaz. Yeni kapsam/ortak dosya/politika kararları ve kesin sürüm inceleme/onay kapıları tamamlanmadan geliştirme işçisi başlatılmaz.
