# MOD-0262-FU01 — sahip kararları (2026-11-11)

- Sahip: Beste (bestepullukcu-alt; platform-shared-services, merkez). Kayıt: MVP-1 CT (oturum `ca7382cf`), sohbetteki sahip sözünden («evet üçüne de ekle»).
- Paket: `execution/domains/platform-shared-services/module-packs/MOD-0262-FU01-document-binary-store.md` (r7 → r8).
- Kaynak: r7 bağımsız incelemesi `docs/records/reviews/MOD-0262-FU01/2026-11-11-a59af819.md` (KOŞULLU; F-1, F-2 orta, F-3, F-4 düşük).

| # | Karar | Gerekçe | Pakette |
|---|---|---|---|
| D-1 | **`None` yalnız güvenilen yolda.** Güvenilen yol (`TrustedDocumentRepository`) için getirilen `None` davranışı yalnız o yolda uygulanır; kullanıcı yolu ve süreç içi yol bugünkü davranışında kalır (yanıt ve kayıt değişmez). Son bakış 02 F-2 için kayda geçen CT kararı geçerlidir; paketin §5 (ortak kod) ve §8.1 ifadeleri buna göre düzeltilir. | Daha dar ve güvenli; kullanıcı yolunda denetimsiz kalıcı satır penceresi açılmaz (AC-8 ile tutarlı). | §5, §8.1, ilgili AC'ler |
| D-2 | **Denetim kaydı sayısı.** "Toplam kayıt 1" testlerde sahte yazıcıyla ölçülür; üretim Mongo yazıcısında 2 kayıt kabul edilen sınırdır (çıkarım, ölçülmedi). DL-2 geliştiricisi gerçek yazıcıda sayıyı ölçer ve raporlar; 2'den fazla ise kusurdur. Son bakış 02 F-1 için kayda geçen CT kararıyla tutarlı. | Kabul ölçütü kesin olmayan bir sayıyı kesin ölçmesin. | §8.1, AC-33 (4), AC-35 (g), §17 |
| D-3 | **Onaylayan Ali.** MOD-0178 karar kaydı D-6'daki "onaylayan Ali (`GMG-ERP`)" kararı MOD-0262-FU01'i de kapsar (EES 2.14: satırları yazan hesap Beste'nin; yazan ≠ onaylayan). | r7 incelemesi F-3. | §18, §19 |
