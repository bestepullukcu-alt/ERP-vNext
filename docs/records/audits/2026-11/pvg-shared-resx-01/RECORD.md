# PVG ortak kaynak çeviri denetimi

2026-11-20. CT kaynak `cc27da0a5dca2b554601a659103383c9cd985aa1`; karşılaştırılan origin/main `2de950edcaeb9d8a153465e5f29d7c71eeb31144`. `resx-check` becerisinin mevcut `resx_sharedresource_checker.py --context tenant` komutu değişmeden çalıştırıldı. Aynı checker'ın iki ref'teki baytlarının aynı olduğu ölçüldü. Main'in yedi SharedResource dosyası ayrı dış dizine birebir Git blob'larından çıkarıldı; main sonucu bu dosya snapshot'ına aittir, gerçek checkout değiştirilmedi.

Test · **İki tam denetim de FAIL, çıkış1**. Ham [X] satırları ve araç tarafından gösterilen anahtar listeleri `ct-head.txt` ve `origin-main.txt` dosyalarında aynen saklanır; araç uzun listeleri kendi `...` satırıyla kısaltır. Komut/exit/hash ve yeni iki anahtarın yedi dildeki gerçek değerleri `record.json` içindedir. Bunlar çalışma/test/CI veya yayın kapısı değildir; kaynak dosyaları değiştirilmedi.

CT [X] satırları:

```text
[X] fr: Missing 35 key(s) from SharedResource.en.resx
[X] es: Missing 35 key(s) from SharedResource.en.resx
[X] zh: Missing 35 key(s) from SharedResource.en.resx
[X] ar: Missing 35 key(s) from SharedResource.en.resx
[X] ru: Missing 35 key(s) from SharedResource.en.resx
[X] fr: 44 key(s) still equal to English (placeholder translation?)
[X] es: 32 key(s) still equal to English (placeholder translation?)
[X] zh: 31 key(s) still equal to English (placeholder translation?)
[X] ar: 31 key(s) still equal to English (placeholder translation?)
[X] ru: 31 key(s) still equal to English (placeholder translation?)
[X] tr: 5 key(s) still equal to English (placeholder translation?)
```

Belge · Eksik35'er anahtar ve eşit-değer sayıları main'de de aynıdır; yalnız Fransızca eşit-değer sayısı main43 → CT44. PVG'nin eklediği `Nav.Domain.PHARMACOVIGILANCE` ve `Nav.Module.CASEINTAKETRIAGE` **yedi dilin tamamında mevcut**. Yeni eksik PVG anahtarı yok. Fransızca domain adı hem fr hem en dosyasında `Pharmacovigilance`; yeni bir eşit-değer bulgusudur. Araç bir teknik terimin iki dilde aynı yazılmasının dilsel doğruluğunu ölçmez; bulgu gizlenmez ve FAIL, PASS diye yeniden adlandırılmaz. Diğer modüllerin eksik/yer tutucu çevirileri bu PVG işinde düzeltilmez. Sayfaya özgü resx, JS l10n ve anlam doğruluğu bu becerinin kapsamı dışındadır.
