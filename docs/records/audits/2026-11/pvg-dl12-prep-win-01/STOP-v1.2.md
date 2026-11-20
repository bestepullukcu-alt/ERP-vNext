---
module: MOD-0230
work_package: WP-0230-DL12-PREP-WIN
status: blocked
date: 2026-11-20
---

# Windows hazırlığı — kapsam dışı testte duruş

Test · İşçi normal v1.2 kapısı0 ardından yalnız iki izinli dosyayı değiştirdi. Gerçek Windows API TRX395 test/394PASS/1FAIL/0skip. CT TRX'i XML olarak okudu; tek başarısız test `PvgServiceApiHostTests.Configuration_scan_fails_closed_on_inaccessible_directory`, hata `PlatformNotSupportedException: Unix file modes are not supported on this platform`. İlgili kaynak satır1856 File.SetUnixFileMode çağrısıdır; bu üçüncü dosya PREP-WIN yazma sınırının dışındadır.

Test · İşçi iki hedef guard/platform testi ile35 Mongo API/store/dedicated testinin geçtiğini bildirdi. Snapshot/transaction testleri TRX'te yeşil; kendi beş mongod sürecinin PID/port/veri kapanış kaydı arşivde. Bunlar dört sabotajı veya bağımsız E2 doğrulamayı tamamlamaz. Sabotaj ve yerel teslim commit'i yapılmadı. Ana feature BLOCKED kalır.

Belge · Seçilmiş ham TRX/komut/süreç/hash/araç kayıtları `stopped-v1.2/` altında, işçinin kendi son metni WORKER-REPORT.md'dedir. capture.json, ham kaynak ve beklenen Git LF-normalizasyon SHA'larını ayrı tutar. Test sayısı/exception, XML semantiği değiştirilmeden korunur. worker-wip.diff yalnız iki izinli test dosyasının dondurulmuş değişikliğidir; ürün teslimi veya CT'ye merge değildir. Kaynak worker HEAD58920519a17e8f4c085738642b33e36689bc8fee, dal feature/pvg/mod-0230-dl12-prep-win; iki dosya WIP olarak korunuyor, stage yok.

Karar · Kuyruk PREP-WIN BLOCKED. Üçüncü dosyaya izin verilmedi, test skip/ret gevşetme veya hata gizleme yok. Yazar yalnız bir test metoduna Windows ACL uyumu için somut dar kapsam önerisi hazırlıyor; bağımsız inceleme ve insan onayı gerekir. İki mevcut WIP'nin yerel checkpoint ile korunması da açık insan izni önerisine bağlanacak; şu anda yapılmaz. Pack186ad967/domain914c0c1d değişmedi; operasyon kapalı. WEB işçisi ayrı izinli yollarda devam ediyor.

Belge · .gitignore satır32 genel *.trx kuralı yüzünden iki kalıcı audit TRX dosyası açık dosya yollarıyla git add -f kullanılarak stage edildi; ignore kuralı veya guard değişmedi. Git yalnız CRLF→LF normalizasyonu yaptı; ham/Git SHA çiftleri capture.json'da ayrı, XML395/394/1 sonucu aynı. Staged whitespace kontrolü yalnız ham worker-wip.diff içindeki beş boş bağlam satırı ve son boş satır nedeniyle çıkış2 verdi. Bu, geçerli patch sözdizimi/hash baytları korunurken kaydedilen uyarıdır; diğer staged dosyalar ve işçinin gerçek iki kaynak dosyasındaki diff kontrolü temizdir. Bütün staged fark temizmiş gibi raporlanmaz.
