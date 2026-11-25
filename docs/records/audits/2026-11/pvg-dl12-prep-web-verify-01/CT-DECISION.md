# PREP-WEB — bağımsız E2 geçti, CT kapısı durdu

2026-11-20. CT dalı `chore/ct/pvg-ct-01`; inceleme anı HEAD `06dd930b0`.

İşçi teslimi `15791333afa89e447210b8d877dc5407693e9b5b`, baz `58920519a17e8f4c085738642b33e36689bc8fee`. Bağımsız doğrulayıcı `pvg_dl12_prep_web_verify` yazan işçiden farklıdır. `VERIFY-WEB.md` transport hazırlığı için UYGUN; bağımsız tam modül Web koşusu 62/62 PASS, 0 skip, iki bağımsız derlenen sabotaj beklenen assertion ile RED. Yedi işçi sabotajı da kaynak/derleme/TRX zinciriyle incelendi. Yalnız üç izinli test/csproj/CI yolu değişti; ürün kaynakları ve pack/domain aynı. Sonuç ana feature'ın veri kaybını düzelttiğini göstermez.

CT'nin ayrıca çalıştırdığı gerçek `scripts/known_failures.py check independent.trx --suite Diten.Web.Tests` **çıkış 2** verdi. Ham argv/çıktı/TRX bağı `KNOWN-FAILURES-CHECK.json` içindedir. Ortak `tests/known-failures.tsv` satır 4–8'deki `*.xml` tetikleyicisi hiçbir izlenen yola eşleşmiyor; araç kayıt güvenilmezliği nedeniyle sınıflandırmayı durdurdu. Bağımsız TRX'in 62 yeşil olması bu zorunlu kapının 0 sonucu yerine kullanılmaz.

Kuyruk DISPATCHED→BLOCKED geçişi yapar; geliştirici/bağımsız doğrulayıcı çalışmasının tamamlandığı bu kayıtla korunur. Ara durumlar geriye dönük sahte commit'lerle üretilmez. VERIFIED, INTEGRATED veya ACCEPTED ilan edilmez; teslim CT'ye henüz merge edilmez. Sorumlu CT/K5, engel tarihi 2026-11-20. Kapı çözümü için salt okunur ayrı araştırma başlatıldı; ortak kayıt/araç değişikliği bu PVG paketinin yetkisinde değildir. Önce kapsam/onarım kararı gerekir.

İşçi ağacı/commit ve tüm kanıtlar korunur. Paket rev20 ve operasyon kapısı değişmez; parent DL-12 BLOCKED kalır. Birleştirme sonrası da gerekli entegrasyon kapısı ayrıca ölçülür. Bu karar push, PR veya main merge izni değildir.
