# DL-12 v1.0 — normal dağıtım duruşu

2026-11-20 (Asia/Baku). Gerçek normal BE kontrolü19:52:55UTC, L10N kontrolü19:55:07UTC çıkış1 verdi. İkisinde de tek ENGEL: feature-discovery-01 raporundaki araçSHA ae87a56615f5, raporun üretici commit'indeki canonical Git aracı72d27f77dcf4 ile uyuşmuyor. Önceki rapor ve bütün ham normal çıktıları korunur; başarıya çevrilmez.

Kök neden CT'nin keşfi Windows CRLF çalışma kopyasından çalıştırmasıdır. Python mantığına veya korunan araca müdahale edilmedi; gate gerçek Git bayt kimliğini doğru denetledi. CT bunu normal kontrolden önce yakalamalıydı. İlk DECISIONS kaydındaki ham/canonical ayrımı bu kapıyı karşılamaz ve dağıtım gerekçesi değildir.

Kapsam/onay/iki resmî UYGUN ve kesin HIGH kaydı geçerlidir; başka ENGEL yoktur. PREP-WIN/Web/registry teknik entegrasyonları geri alınmaz. Ürün/RESX/test kodu başlamadı; işçiler yalnız salt-okunur tuple/source doğrulayıp durdu. Bu duruş yeni insan onayı istemez.

CT düzeltmesi: daha önce başarılı kullanılan dış LF producer mirror'ındaki verify_module_id.py ve ortak ct_queue.py dosyalarını güncel Git blob'larıyla byte-byte doğrula; aynı gerçek repo/HEAD üzerinde değişmemiş canonical producer'ı yeniden çalıştır; yeni numaralı rapor/check-report/metadata ve bütün bulgu kararlarını kaydet. Korunan .antigravity veya guard/eşik değiştirme, eski rapor/hash üzerine yazma yok. Yeni DoR/temiz worker base ve kesin v1.1 prompt/source-pack incelemesi ardından gerçek normal kapılar yeniden geçmelidir. Manuel hash kontrolü dağıtım0 yerine kullanılamaz.

CT kararı: parent/BE/L10N READY→BLOCKED; neden bu tek teknik keşif girdisi. FE PLANNED. Canonical kaynak kaydı ve yenilenmiş DoR kanıtlanınca yeniden READY; kod yalnız yeni normal0 sonrası.
