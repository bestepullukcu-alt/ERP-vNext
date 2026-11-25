# MOD-0230 — G-25 kararı: düzenleme kayıtlı veriyi silmez (revizyon 16)

> Control Tower kaydı, 2026-11-19. Sahibin bu sohbetteki sözlerinin kaydıdır. Paket onayı değildir; revizyon 16'nın onayı ayrıca sahibin «onayla» sözüyle verilir.

## Sahibin sözleri

1. **G-25 ne zaman:** «şimdi düzelt» (2026-11-19). Aynı mesajda: PVG başkasına devredilecek, kalan işler başkasına verilecek; devir buna göre hazırlanacak.
2. **OD-PVG-15 — triyaj ya da yönlendirme sonrası düzenleme reddinin kodu:** sahip "Yeni kod" seçeneğini seçti. Kullanıcıya "bu vaka triyajdan geçti, artık düzenlenemez" anlamını veren yeni bir neden kodu eklenir, HTTP 409. Var olan sürüm çakışması kodu kullanılmaz (yanıltıcı "yenileyip tekrar deneyin" mesajı). Kodun adı paket revizyon 16'da yazılır.
3. **OD-PVG-16 — öğe cevabının 15 kayıtlı alanı:** sahip "Şimdi dönsün, canlıda maskeleme şart" seçeneğini seçti. Yapı/test kapısı altında öğe cevabı 15 alanın hepsini döner (canlı ekran kapalı, izin kapısı her şeyi reddediyor; gerçek veri yok). Canlı ekranın açılması (OD-PVG-14) ve operasyonel çalışma kapısı, MOD-0019 alan maskelemesinin bu alanlara uygulanmasını **zorunlu şart** olarak taşır.

## Seçeneklerin karşılaştırması (sahibe sunulan)

- OD-PVG-15: SAP ve Oracle'da onaylanmış belge kilitlenir ve kullanıcıya nedeni söylenir; yeni kod bu davranışa uyar.
- OD-PVG-16: "maskeleme gelene kadar bekle" seçilseydi düzenleme formu dolu açılamazdı ve G-25 yarım kalırdı.

## Kapsam

Revizyon 16, DL-12 dilimi (AC-40 … AC-44). Kapsam dışı: 404 (AC-6), denetim yazımı (DL-5), canlı ekran (OD-PVG-14), başka modül ve servis.
