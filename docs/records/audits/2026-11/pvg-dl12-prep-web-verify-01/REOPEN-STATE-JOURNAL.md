# Web doğrulama devamı — durum geçişleri

Kayıt zamanı UTC; işçi olayları CT’ye daha önce iletildi. Aşağıdaki saatler geçmişe atılmış işçi başlangıç/bitirme saatleri değildir; gözlenen kanıtın kuyrukta işlenme zamanıdır.

- 2026-11-20T18:04:23.169987+00:00 — READY → DISPATCHED: Mevcut15791333 tesliminin salt-okunur bağımsız doğrulaması yeniden atandı; ürün yazımı/yeniden dağıtımı değil; DoR3b5 ve registryCTmerge0/0, önceki normal dağıtım kaydı korunur.

- 2026-11-20T18:04:38.447294+00:00 — DISPATCHED → IN_PROGRESS: Bağımsız doğrulayıcı kendi salt-okunur incelemesini başlattı;15791333 temiz ve üç dosya hashleri ölçüldü, ürün dosyası değiştirilmedi.

- 2026-11-20T18:04:56.495913+00:00 — IN_PROGRESS → REPORTED: Mevcut yazılı işçi15791333 raporu/PUBLISH-MANIFEST/VERIFY-WEB korunmuş; aynı baytların yeniden ölçüm sonuçları alındı, yeni kod veya yeni geçmiş test sonucu üretilmedi.

- 2026-11-20T18:05:10.039824+00:00 — REPORTED → VERIFYING: Yazardan farklı pvg_dl12_prep_web_verify aynı15791333 için bağımsız yeniden doğrulamaya atandı; kayıt engeli kalktı, öncekiE2 koşusu kaynak bağıyla inceleniyor; yeni ürün yazımı yok.
