PROMPT İNCELEME KAYDI v1
wp: WF-S17-DL1
prompt: PSS-WF-S17-DL1-P v1.6
prompt-sha256: bac7753c8cb819215208166fa522eaced766f2ea642ad001bd27bfff5ee35c26
kaynak-paketi-sha256: ae3fa51cc3baaa5885d81dea5dce2259f78889038b4d728ce90a1de0bf767e54
yazar: CT · İş akışı prompt yazarı (commit 5818b8abb)
inceleyen: Bağımsız inceleme alt ajanı (Claude, prompt'u yazmayan; DL-1 v1.6 fark incelemesi)
karar: KOŞULLU — Ölçüt özünde doğru ve sıkı, F-1…F-7 doğru kapanmış, kırmızı gizleyen yol bulunmadı. Üç ORTA bulgu işçiyi yanlış durmaya götürebilir (G-1, G-2, G-3); düzeltilen sürüm yeni inceleme ister.
kontrol sözleşme: tamam — execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md: kaynak paketi digest'i v1.5 kaydıyla aynı; dört alıntı Expected HEAD'de birebir; fark sözleşme yüzeyine dokunmuyor.
kontrol dosyalar: tamam — execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md §17.9/2: İzinli ve Korunan yollar v1.5 ile aynı; MongoIntegrationHarness ve örnek sınıf kodda doğru; AuditOutboxTemporalReplicaSetFixture kendi mongod'unu başlatıyor (AuditOutboxTemporalStorageMigrationMongoTests.cs:708-730).
kontrol ct-onayı: tamam — execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md §17.8/2 ve §17.9/1a: sahip sayımı raporda; filtreyi tutmak yetkiyi aşmıyor; S17-8 uyumlu.
kontrol kabul: eksik — execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md AC-W15 ve scripts/known_failures.py:664: 146/143/3 ve üç parmak izi TRX'te doğru; "birebir" ölçütü aracın satır biçimiyle uyuşmuyor (G-1), ad kuralı zorunlu okuma üyesinin doğal adını yakalıyor (G-2).
kontrol durma: eksik — execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md §17.9/1d: yeni durma cümlesi işçinin kendi yeni kuralının geliştirme kırmızısını da kapsıyor (G-3).
kontrol risk: tamam — execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md §17.9/15 ve Teslim dilimleri DL-1 E4: Risk HIGH değişmedi; bulgular yanlış durma üretir, yanlış geçiş üretmez.

| ID | Önem | Yer | Bulgu | Gerekli değişiklik |
|---|---|---|---|---|
| G-1 | ORTA | v1.6:60-63, :81, :96-97 | Aracın çıktısı TAB ayrılmış üç sütun (`Failed` sütunu, ad alanı önekli ad); prompttaki satırlar farklı biçimde, "birebir" her koşuda fark verir. | Karşılaştırmayı sütun bazında yaz. |
| G-2 | ORTA | v1.6:68, :55 (3) | "Delete içeren üye" kuralı `ListByObjectIdsIncludingDeletedAsync` gibi doğal adı yakalar; istisna kuralı zayıflatır. | Üye adını sabitle, kuralı yorum dışı üye adına uygula. |
| G-3 | ORTA | v1.6:97 | Durma cümlesi işçinin kendi yeni kuralının kırmızısını da kapsıyor. | Yeni kurallar dışı 146 test ve sabotajsız koşuyla sınırla. |
| G-4 | DÜŞÜK | v1.6:81, :92 | Silinen ya da atlanan yeşil test görünmez; "yalnız yeni dosya" ölçülmüyor. | NotExecuted = 0 ve değiştirilen dosya denetimi ekle. |
| G-5 | DÜŞÜK | v1.6:60-63 | `msg:` parmak izi dosya gezinme sırasına bağlı; Linux'ta ölçülmedi. | CT kabul ölçümünü aynı makinede yapar, CI ile karşılaştırmaz. |

bulgu: diğer · açık · G-1 (ORTA) DOĞRULA 2 "birebir" ölçütü aracın TAB'lı, Failed sütunlu, tam adlı çıktısıyla uyuşmuyor.
bulgu: diğer · açık · G-2 (ORTA) "Delete içeren üye" kuralı zorunlu okuma üyesinin doğal adını yakalıyor.
bulgu: diğer · açık · G-3 (ORTA) Değişiklik sonrası durma cümlesi işçinin kendi yeni kuralının kırmızısını da kapsıyor.
bulgu: diğer · açık · G-4 (DÜŞÜK) Silinen ya da atlanan yeşil test parmak iziyle görünmez; "yalnız yeni dosya" ölçülmüyor.
bulgu: diğer · açık · G-5 (DÜŞÜK) msg parmak izi dosya sistemi sırasına bağlı; CI ile karşılaştırılamaz.
bulgu: diğer · kapandı · F-1…F-7 v1.6'da doğru kapandı; F-5 tanımı G-2'yi doğurdu.
bulgu: diğer · kapandı · Ölçüldü: TRX total=146, passed=143, failed=3, notExecuted=0; check çıkış 1, YENİ=0. Ölçülmedi: dotnet test, build, değişiklik sonrası TRX, CI parmak izleri.
kritik-açık: 0
engelleyici-açık: 0
