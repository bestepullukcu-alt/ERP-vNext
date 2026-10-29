# PSS-UI-P1-04 — DoR 02 (BLOCKED → READY): engel kalktı, DoR yeniden geçti

> Control Tower kaydı, 2026-10-27. Bu kayıt, geçiş tablosundaki `BLOCKED → READY` geçişinin kanıtıdır ("engelin kalktığının kanıtı + DoR yeniden geçti"); dağıtım izni değildir. İşçiyi başlatma sözü sahibindir.
> Önceki DoR: `docs/records/audits/2026-10/pss-ui-p1-04-dor-01/DOR.md`. Durma kaydı: `docs/records/audits/2026-10/pss-ui-p1-04-durdu-01/DURDU.md`.

## Engelin kalktığının kanıtı

- Çelişki: CI-11 "PR'ın eklediği her partial" ile AC-18 (c) geri alma koşusunun çıkış 0 beklentisi.
- Paket rev 14 bu çelişkiyi çözdü (sha256 `87f46dcf…`). CI-11 artık yalnız yeni yüzeyde koşuyor (satır 459), CI tetikleyicisine seçici, taban ve workflow eklendi (satır 433).
- Rev 14 bağımsız incelemeden geçti: Codex inceleme 14 UYGUN (`docs/records/reviews/MOD-0286/2026-10-27-87f46dcf.md`).
- Rev 14 main'de onaylı: PR #266, merge `f0c5014dc`, onaylayan bestepullukcu-alt. `pack_approval.py check --ref origin/main` "onaylı — HIGH", çıkış 0.
- İnceleme 7 ölçtü: AC-18 (c) geri almasında Golden Slim yeni yüzey değil, bu yüzden geri alma çıkış 0 verir.

## DoR (önceki kayda göre değişenler)

| Madde | Değer |
|---|---|
| Paket yetkisi | rev 14, sha256 `87f46dcf035555c7c9c1b02a5b45579c66385a082fd7eeb36a2e49cd3e98dfe3`; DL-3 E2 (paket satır 822) |
| Bağımlılık | PSS-UI-P1-03 ACCEPTED, kabul kaydı 03 rev 14'e bağlı (`efc9c88f0`, `acceptance_record check` çıkış 0) |
| Git yalıtımı | Worktree `.claude/worktrees/pss-ui-p1-04`, dal `feature/pss/pss-ui-p1-04`, Expected HEAD `0a69c97120c2659781d6891a482b090491ec0953` |
| Prompt (§36.1) | `execution/domains/platform-shared-services/work-packs/prompts/PSS-UI-P1-04-v1.4.md`, sha256 `a1f826a8665f75a2ce045a53d2fed7d2da3c322c53ea314d9183ffe786fb76cd` @ 03f267b03 |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0286/prompts/PSS-UI-P1-04-v1.4-inceleme-01.md` @ 12e31ab84 (inceleyen 9): kritik 0, engelleyici 0, yeni bulgu yok. Ara turlar: v1.2 inceleme 7 ve v1.3 inceleme 8; ikisi de 0 kritik, 0 engelleyici, bulguları kapandı. |
| Kanıt koruması | `docs/records/audits/2026-10/mod-0286-uic-ci-01/uic-dl3-kanit-04.sh` @ 0a69c9712 (BASE = bu commit): bugün kırmızı 2/13; işçinin kayıtlı koşusunda beklenen 11/13 (AC-17b ve AC-20 workflow gelene dek kalır). |

## Kabulde ayrıca bakılacak notlar

DoR 01'deki dört not geçerli. Şunlar eklenir:

1. **Taban satırı olmaması gereken kontroller.** Doğrulayıcı, tabanda CI-11, CI-1, CI-8, CI-13 ve CI-15 satırı olmadığını ölçer (`cut -f2 tests/ui-compliance-baseline.tsv | grep -c -x CI-11` = 0, öteki dördü için de aynı). Kanıt betiği v04 bunu ölçmüyor; prompt v1.4 bunu Adım 2 ve bir durma koşuluyla istiyor (inceleme 7/2, inceleme 8).
2. **Eski yorum.** Kanıt betiği v04'ün 42. satırındaki yorum hâlâ "paket rev 13" diyor. Davranışı etkilemez; bir sonraki betik sürümünde düzeltilir (inceleme 8 F3a).
3. **CI-11 örnek biçimi.** CI-11 örnek biçimi `<path>:yok:GetAllStrings` prompt'ta tanımlı, ama pakette yazmıyor (satır 381-384, 612). Bu bir paket revizyonu girdisidir.
