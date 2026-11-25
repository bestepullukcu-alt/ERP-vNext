# PREP-WEB kanıt yayımı

2026-11-20. CT, bağımsız doğrulayıcının ham raporunu değiştirmeden sakladı. E2 görüşü yalnız test taşıma hazırlığı içindir; ana DL-12 veri koruma kabulü değildir.

`PUBLISH-MANIFEST.json`, kalıcı kanıtların ham SHA-256 değerlerini ve yalnız CRLF→LF normalizasyonu sonrası Git içerik değerlerini ayrı gösterir. Ham ve Git baytlarının aynı olduğu iddia edilmez. TRX/log/metadata ve bağımsız rapor kalıcı audit kaydıdır; yedi işçi ve iki bağımsız sabotajın RED kanıtı kabul için gerekli gerileme kanıtı olarak saklanır.

Doğrulayıcının ürettiği 13 yardımcı kaynak kopyası (sabotaj C# dosyaları, ölçüm Python betikleri ve tekrar diff) ürün veya kalıcı test kaynağı değildir. CT bunları silmeden manifestteki dış visualization dizinine taşıdı ve ham hashlerini doğruladı. Ham raporda geçen eski göreli kaynak yolları için manifestteki `preserved_external_supplemental_sources` eşlemesi kullanılır. Çalışma zamanı log/TRX'leri yerinde kaldı. Bu kaynak kopyaları stage veya commit edilmez.

Ham raporun sonunda belirtilen henüz commit edilmemiş durumu raporun üretildiği ana aittir. CT'nin daha sonraki commit/entegrasyon kaydı bu gözlemi geriye dönük değiştirmez. İşçi commit'i `15791333afa89e447210b8d877dc5407693e9b5b`; bağımsız doğrulama kaynakları bu kesin sürüme bağlıdır.
