# WP-0280-FU02-DL0 · dağıtım ve doğrulama notu

- Tarih: 2026-10-31 · Yazan: CT · İzin ve devamsızlık · Sahibin başlatma sözü: "Evet", bu sohbette
- Kapsam: MOD-0280-FU02 İzin ve Devamsızlık Yönetimi, dilim DL-0. Prompt `HCM-LEAVE-DL0-P v1.3`, inceleme kaydı
  `docs/records/reviews/MOD-0280-FU02/prompt-review-WP-0280-FU02-DL0-v1.3.md`, kapı çıkış 0.

## Prompt'tan sapmalar (CT kararı; ürün kodunu değiştirmez)

1. **İşçinin yeri.** Sahip uzaktan bağlıydı, ayrı oturum açılamadı; alt ajan prompt'taki çalışma alanına yazamadı (oturum
   kendi klasörüne bağlı). İşçi kendi ayrı çalışma alanında, aynı Expected HEAD'den açılan
   `feature/hcm/wp-0280-fu02-dl0-r2` dalında yürüdü. Kapı, prompt'taki `-r1` çalışma alanında geçmişti; taban ve metin aynı.
2. **Aralıklı kırmızı.** İşçinin tam taban koşusunda `ESignReauthEndpointTests.No_log_line_contains_the_password_or_the_plain_ticket`
   kırıldı; işçi durma koşuluna uyup durdu. CT'nin tam koşusunda ve son tam koşuda geçti, tek başına geçti. CT bunu bu işle ilgisiz,
   aralıklı kırmızı saydı. Nedeni kanıtlanmadı; konsol yakalamasının paralel testlerle çakışması olabilir. Test düzeltilmedi.
3. **Süre.** Sabotaj koşuları ve commit sonrası koşu üç test sınıfıyla süzüldü; tam proje commit'ten önce bir kez koşuldu.

## Ölçülenler

- Son tam koşu (commit'ten önce): 2931 satır, 2911 geçti, 1 kırmızı, 19 koşulmadı. Tek kırmızı `OwnedMongoResolutionTests` Mac yolu.
- Üç sabotaj beklenen testleri kırmızıya çevirdi (işçi raporu).
- CT, kodu okudu: üç izinli yol, ortak dosyada silinen tek satır virgül eklenen `Keys` satırı.
- CT kanıt betiği birleşik dalda çıkış 0: `docs/records/audits/2026-10/hcm-leave-dl0-green-01/`.

Bu not kabul değildir; kabul kaydı kod ana dala girince yazılır. AuthService ekini sahibin incelemesi gerekir (kart §7c/3).
