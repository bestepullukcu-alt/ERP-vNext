hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01
model: gpt-6.2-astra
effort: high
reviewer_session: 01a1f849-c6bd-7ba3-b788-6ebf7a7551e9
kaynak_commit: 7df5adce29943a999f7a4a4551ee87dad5637c97

F-1 | ORTA | Paketin 1580–1585 ve 1954–1966. satırları eski kiracı ve aktör başlıklarını ve `pvg.mod0230.intake.*` izin ailesini güncel davranış olarak anlatıyor. `PvgCaseIntakeTriageEndpoints.cs:591–621` kiracı için doğrulanmış `tenant_id` ile `X-Tenant-Id` eşleşmesini arıyor ve aktörü doğrulanmış kullanıcıdan alıyor. `PvgIntakeDraftApplicationService.cs:7–11` ise `pvg.case-intake-triage.*` kullanıyor. Ana DL-12 iş emri eski ifadeleri tarihsel olarak işaretlemeli ve mevcut güvenlik sözleşmesini korumalı. | açık | K5 | 2026-11-23
F-2 | ORTA | AC-42(f) gömülü CR/LF’yi kanıt referanslarında ölçüyor. `SourceReference` içinse `PvgIntakeDraftValidator.cs:218` kırpılmış uzunluğu denetliyor, `PvgIntakeDraftApplicationService.cs:834` özgün değeri saklıyor ve `_Form.cshtml:71` tek satırlı girdi kullanıyor. Ana kabul testi, örneğin `A\r\nB` değerini gerçek API ile oluşturup dokunulmamış Edit kaydından sonra depoda birebir karşılaştırmalı. Özgün değer yerine DOM değerini gönderen sabotaj bu testi düşürmeli. | açık | K5 | 2026-11-23
F-3 | ORTA | AC-41(b) aynı kiracının liste yanıtında 15 ayrıntı değerinin bulunmamasını istiyor. `PvgCaseIntakeApiContractTests.cs:565–568` listeyi alan politikası reddedilmişken çağırıp 409’u doğruluyor. Bu ölçüm, izinli bir listenin ayrıntı verisi sızdırmasını yakalayamaz. Ana kabul testi aynı kiracı için 200 yanıtını, beklenen kayıt kimliğini, liste öğesinde yalnız `intakeDraftId`, `status`, `version` bulunmasını ve yanıtın tamamında ayrıntı verisinin bulunmamasını birlikte doğrulamalı. | açık | K5 | 2026-11-23

sonuç: KOŞULLU

ZIP 7.757.706 bayt. SHA-256 değeri `dd9a128e6771efdfdb45dee4c134be9723782b93029121fd825ade31b6e498bb` ve manifestteki 285 dosyanın tamamı eşleşti. Paket 451.065 bayt ve hedef hash ile eşleşti. Domain dosyası 18.582 bayt ve SHA-256 değeri `914c0c1dab0979d3ec48e3d4f6e08646ad0bf4494f9b73a97fa27bca4c5ead9c`.

`Dilim ayrıntıları` satırının başındaki üç baytı bellekte kaldırınca önceki `729ac909…` hash’i aynen elde edildi. ZIP’teki `pack_approval.py` dosyasının `slices` işlevi yeni baytlarda `DL-1` ile `DL-12` arasındaki 12 dilimi sırasıyla okudu. Bu düzeltme 45 kabul ölçütünü veya onaylı dosya sınırlarını değiştirmiyor. İki hazırlık işi kendi dağıtım kapılarından geçerek uygulanabilirlik kanıtını üretiyor. Ana iş her iki E2 doğrulamasını ve entegrasyonunu bekliyor. Üç açık koşul ana DL-12 işine ait. Operasyon kapısı kapalı.

ZIP’te bulunmayan onay defteri, iki hazırlık DoR dosyası, tarihsel kısa test yolu, üretilen `bin` içeriği ve planlanan `PvgCaseIntakeEditKeepsStoredDataTests.cs` incelenmedi. Tam onay ve dağıtım kapıları, derleme, restore, npm, çalışma testleri ve sabotajlar çalıştırılmadı. Hüküm yalnız ilk satırdaki kesin paket hash’i için geçerlidir.
