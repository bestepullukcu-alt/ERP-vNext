# Registry durum geçişleri

Kayıt zamanı UTC; işçi olayları CT’ye daha önce iletildi. Aşağıdaki saatler geçmişe atılmış işçi başlangıç/bitirme saatleri değildir; gözlenen kanıtın kuyrukta işlenme zamanıdır.

- 2026-11-20T17:56:58.565797+00:00 — DISPATCHED → IN_PROGRESS: İşçinin temizfff584a3 dalında başladığı ve yalnız izinli hücreleri ölçtüğü mesajları alındı; teslim ayrıca sonraki kapıda kaydedilecek.

- 2026-11-20T17:57:18.998305+00:00 — IN_PROGRESS → REPORTED: Yapılandırılmış işçi teslimi alındı:7dbdfd4 temiz; dış handoff.json/final-green.json; validate0/check0, beş hücre; bağımsız karar henüz kabul edilmedi.

- 2026-11-20T17:57:32.887951+00:00 — REPORTED → VERIFYING: Yazardan farklı pvg_dl12_prep_web_verify bağımsız doğrulamaya atandı; mevcut istenen modelastra/high, gerçekruntime kimliği doğrulanamaz.

- 2026-11-20T17:58:39.868597+00:00 — VERIFYING → VERIFIED: Bağımsız rapor UYGUN:7dbdfd4 temiz, exact5hücre, gerçek üretici0/validate0/check0, bellekRED, immutableTRX aynı; raporSHA1eb47520. Worker/independent ham kanıtı PUBLISH-MANIFEST ile eklendi.

- 2026-11-20T17:59:11.496412+00:00 — VERIFIED → INTEGRATION_READY: Tek işçi commit7dbdfd4 CT dalına no-ff merge gerektirir; insan XMC onayı ve bağımsız UYGUN/PASS tamam, başka bağımlılık yok. Main/push/PR izni değildir.
