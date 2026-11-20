```text
hedef: docs/roadmap/plans/pvg-mod0230-windows-acl-scope-proposal-2026-11-20.md @ a948cf45e0063cfbc6bff1a5b23b8352d2c062bebd58ccf902ef7a8e716560ca
model: gpt-6.2-astra
effort: high
reviewer_session: 01a1f849-c6bd-7ba3-b788-6ebf7a7551e9
kaynak_commit: 35146a37674df2d6e2b2830947331962a29c2ac8

F-1 | ORTA | Öneri 39–43, dizin listeleme önkoşulunda UnauthorizedAccessException arıyor. Gerçek tarayıcı çağrısı ise ilgisiz bir hatayla da testi geçirebiliyor (PvgServiceApiHostTests.cs:1864, Assert.ThrowsAny<Exception>). Kabul ölçütü, erişilebilirlik kontrollerini ve atlama sabotajını koruyarak tarayıcı çağrısında da kilitli dizinden gelen UnauthorizedAccessException’ı aramalı. | açık | K5 | 2026-11-23
F-2 | ORTA | Unix doğrulamasının hangi commit üzerinde ve hangi kapıdan önce çalışacağı belirsiz (öneri 71–72, 109–118). Tek Unix kanıtı son PR’dan gelirse PREP-WIN entegrasyonu ile PR açılışı birbirini bekleyebilir. E2 öncesinde aday commit’i ayrıcalıksız bir Unix runner’da ölçüp kaydedecek adım tanımlanmalı veya son PR’a bırakılan doğrulamanın hangi kapanış koşulunu karşıladığı açıkça yazılmalı. | açık | K5 | 2026-11-23
F-3 | ORTA | Duran işçinin dört sabotajı hâlâ bekliyor (stopped-v1.2/WORKER-REPORT.md:9). Öneri 73–76 iki yeni ACL sabotajını açıkça sayıyor, 109–113 ise yeni ExpectedHEAD sonrasında yalnız “tüm” sabotajlara atıf veriyor. E2 kaydı, 58920519’dan son adaya kadar checkpoint’teki iki dosyayı da kapsamalı ve önceki dört sabotaj ile iki ACL sabotajının her biri için beklenen kırmızı sonucu ve özgün kodun yeşil sonucunu göstermeli. | açık | K5 | 2026-11-23

sonuç: KOŞULLU
```

**Bütünlük.** Resmî ZIP’in SHA-256 değeri `0ae8bd5e441fe8dddc982f46eac05632745f57481e9f243798faa12503a29b8c`. Manifestteki 301 dosyanın özeti eşleşti. Arşivdeki patch, iki WIP dosyası için kayıtlı Windows disk özetlerini yeniden üretti. TRX’te 394 başarılı, 1 başarısız ve 0 atlanan test var. Tek hata, hedef metodun Windows’ta `File.SetUnixFileMode` çağrısında. 35 Mongo testi geçti.

**Kapsam ve yetki.** Tek metot, tek geçici dizin ve geçerli test kimliğiyle sınırlanan ACL kapsamı korunabilir. İki dosyalık yerel checkpoint ile metot değişikliği ayrı ayrı doğrudan insan onayı gerektiriyor. İnceleme yalnız ZIP üzerinde, bellekte yapıldı. Windows ACL API’lerinin derlenmesi, gerçek erişim reddi, hata sonrasında DACL’in geri yüklenmesi ve Unix sonucu çalışma zamanı kanıtı bekliyor.
