# Windows r2 — CT kanıt üreticisi ve kırmızı taban

2026-11-20. S-83: CT yalnız mevcut test/TRX/skip/regresyon araçlarının çıktılarını bağlayan rapor üreticisini yazdı; ürün C#/CI/guard kodu değişmedi. Üretici canonical-LF SHA256 `f4e386487608e06a13ffd520ebaab9a81102af86d42c1758dce279d20c982d5d`.

`SAVED-BASELINE.json` korunmuş eski normal API koşusunun yeniden okunmasıdır; **yeni test koşusu değildir**. Gerçek üretici `--saved-baseline` çıkış1;395 sonuç,394PASS/1FAIL/0skip,35 gerçek Mongo testi; tek hata Unix chmod çağrısının Windows PlatformNotSupportedException sonucudur. Gerçek check_trx_no_skipped çıkış0. Immutable GitLF TRX `1f7a5f2188bf8eb691c8c654dc9ce3a1713926a5abf9b538a9f6cdbb73f18846`, özgün ham TRX `c3b30fb5a2ce4881d6a24e60480b2c4c6cdac69d1178a0455a6791b65c559404`. JSON ham SHA `b4ba19c83c21bd41bf033078bd3c00b108bf54bf1b30a654f57b4138482abbf8`.

Normal kip, bu tabanı başarı saymaz: kesin ExpectedHEAD'e göre yalnız üç izinli yol, taze derlenmiş test assembly'si/kaynak hashleri ve zamanları, ondan sonra başlamış gerçek395/395 TRX,35Mongo/üç hedef test ve değişmemiş skip+known_failures CLI0 gerektirir. Bu çıktı tek başına method-only diff, gerçek ACL ret/rollback, PID temizliği veya altı derlenmiş sabotaj grubunu kanıtlamaz; bunlar işçi ham kanıtı ve bağımsız E2'de ayrıca ölçülür.

`WIP-CHAIN.json` eski iki uncommitted dosyanın ham ve LF hashlerini donmuş6166bayt patch ile bağlar. Eski ağaç salt okunur korundu. Temiz r2 başlangıcında patch önce `git apply --check` ile denenir; normal gate0 sonrası uygulanır, iki dosyanın LF hashleri zincirle aynı olmalıdır. Ek üçüncü dosyada yalnız insan onaylı ACL testinin gövdesi değişebilir.

Registry7dbdfd4 bağımsız doğrulama/CT entegrasyonu tamamlanınca yeni temiz r2 ağacı CT'nin kesin commit'ine fast-forward edilir. HIGH/prompt kapısı hazırlık sonucu değildir; yeni ürün yazımı ancak normal dağıtım kapısı0 sonrasında başlar. Bu not E2/operasyon kabulü değildir.
