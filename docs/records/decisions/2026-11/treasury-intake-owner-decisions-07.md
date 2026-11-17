# Treasury intake — sahip kararları 07

Önceki dosyalar: `docs/records/decisions/2026-10/treasury-intake-owner-decisions-01…06.md`. Bu dosya, açık PR #434'ün 06 dosyasıyla çakışmamak için ayrı açıldı; numara D145 oradan devam eder. Bu kayıt onay satırı değildir.

## D145 — DCP-011 kod alanı altı üyeye; D137 taslağının yerine (2026-11-16)

Onay kanıtı: K1 / gmg-10 (soru kutusu, 2026-11-16: "Add all 6 (Recommended)"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Ölçüm: `origin/main` `c3547081a`.

1. **D145/1 — kapsam:** DCP-011 `runtime_code_scope` MOD-0289'a ek olarak MOD-0126 (TRM-S1), MOD-0119, MOD-0120, MOD-0124 (TRM-S2; D139) ve MOD-0217'yi (TRM-S4; D134) kapsar. Ölçüldü: altısı `ready-for-dev`, sürüme bağlı CT-APR-01 satırları `execution/approvals/approvals.tsv`'de (0126 `4fc00697…`, 0119 `c6e72989…`, 0120 `bbdc0ae1…`, 0124 `c9935483…`, 0289 `0056f86e…`, 0217 `46b25e6c…`) ve paketlerin bugünkü sha'larıyla aynı.
2. **D145/2 — koşullar:** bir üyenin kodu yalnız o paketin sürümüne bağlı onay satırı main'deyken, §8 teslim sırasıyla (TRM-S2 MOD-0126 DL-1'den sonra), dilim sırasıyla, paketin kendi dilim kapıları kapanınca ve dilimin WP prompt'u kapıdan geçince yazılır. Kod alanı bu DCP sürümünün CT-APR-01 satırı main'e girince yürürlüğe girer; o ana kadar `168c71b0…` (yalnız MOD-0289) geçerlidir.
3. **D145/3 — D137:** 2026-11-05 D137 taslağı (yalnız MOD-0217; yerel dal `docs/ct/treasury-dcp011-mod0217-code-01` @ `f4b5ab78a`, main'e gitmedi, incelenmedi) bu kararla yerini D145'e bırakır; D137 numarası kullanılmaz.
4. **D145/4 — F-1:** 2026-11-12 bağımsız incelemenin F-1 düşük bulgusu (üye tablosu, §8.1 TRM-S1 / S2 / S4 ve §8 madde 8'de bayat "draft") bu sürümde kapandı.
5. **D145/5 — sınır:** MOD-0269 (dış sağlayıcı, pack yok) ve MOD-0057 (park) kod alanı dışında. Üyeler, teslim sırası, AC aralıkları ve kapsam dışılar değişmedi; ürün kodu yok. Bağımsız inceleme (CT-APR-02) ve DCP onay satırı (CT-APR-01, onaylayan `bestepullukcu-alt`) bu sürüm için gerekir.

## D146 — D82/2 TRM-14 dilim kapısı `MDM-0220-DL1` kabulüyle açılır; D10/2 yerine MDM-önce teslim (2026-11-16)

Onay kanıtı: K1 / gmg-10 (soru kutusu, 2026-11-16: "Open on MDM-0220-DL1 (Recommended)"); oturum Treasury Intake CT (`f8880468-240a-4bd1-8ab8-bbdbecf875dd`). Soru kaynağı: `docs/records/decisions/2026-11/mdm-mod0220-treasury-notice-01.md` ve MOD-0220 §22 "D82/2 gate — what the MDM side delivers" (`1b1a0680…`, onay satırı main'de, PR #408; check: onaylı).

1. **D146/1 — kapı:** dört finans paketinin (MOD-0126, MOD-0119, MOD-0120, MOD-0124) DL-1 dilim kapısı TRM-14 (D82/2, D135/2), MDM WP'si `MDM-0220-DL1` kabul edilince kapanmış sayılır: kod main'de, AC-T1…T5 geçti (tüketici okuma biçimi testi AC-T3, AC-T4'ün MDM WP kısmı dahil). TRM-14'ün tam kapanışı (her tüketici sonucunun sahibinin kanıtına bağlanması, §22.6a) Treasury DL-1'ini bekletmez; o sonuçlar her finans paketinin kendi DL-1 kabulünde kanıtlanır.
2. **D146/2 — D10/2:** D10/2'deki "MOD-0220 değişikliği MOD-0126 ile birlikte kodlanır" ifadesinin yerini MDM-önce teslim (`MDM-0220-DL1`, `docs/records/decisions/2026-11/mdm-mod0220-s22-delivery-owner-decision-01.md`) alır; Treasury bunu kabul eder.
3. **D146/3 — bugünkü durum:** `MDM-0220-DL1` MDM kuyruğunda `DISCOVERED` (`execution/domains/master-data-management/work-packs/CT-QUEUE.tsv`, `c3547081a`); TRM-14 henüz kapalı değil — finans DL-1'leri onu bekler.
4. **D146/4 — sınır:** finans paketlerinin metni, AC'leri ve `status`'u değişmedi; bu karar kapının hangi kanıtla kapandığını belirler. Bildirim: MDM CT ve Beste'ye (mesaj Natig'den).
