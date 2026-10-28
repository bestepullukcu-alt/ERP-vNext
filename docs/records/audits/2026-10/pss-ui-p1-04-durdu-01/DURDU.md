# PSS-UI-P1-04 — durma kaydı 01 (DISPATCHED → BLOCKED)

> Control Tower kaydı, 2026-10-27. İşçi alt ajan (sahip `alt-ajan`), prompt PSS-UI-P1-04-P v1.1. Worktree `.claude/worktrees/pss-ui-p1-04` @ 5341e0dad.

## Adı konmuş durma koşulu

Prompt Durma koşulları madde 2: "Paketin DL-3 metni bir noktada iki farklı davranışa izin veriyor ya da komut sözleşmesiyle
çelişiyor → seçme, DUR". İşçi seçiciyi ve tabanı yazmadı; commit yok, ağaç temiz (`git status` boş, HEAD 5341e0dad).

## Çelişki (CT doğruladı)

- Paket rev 13 satır 459 (CI-11): Runs on «an `_IndexL10n.cshtml` / `_L10n.cshtml` **added** by the pull request»; Red when
  «no `GetAllStrings(true)`»; Reds today «none (added files only)».
- Paket satır 679 (AC-18 (c)): «a commit that deletes only the `_IndexL10n.cshtml` of one changed list page»; her geri alma
  «run with `--base <the sabotage commit>` ... must exit 0»; satır 726 aynı.
- Geri alma commit'i silinen dosyayı yeniden ekler: `git diff --name-status <sabotaj>...HEAD` yolu `A` verir. Harfiyen okumayla
  CI-11 koşar; Golden Slim `_IndexL10n.cshtml`'de `GetAllStrings(true)` sayısı 0 (CT ölçümü; paket satır 224 RC-5 "legacy
  bridge fixture, expected"). Taslak kipinde CI-11 satırı yoktur ("added files only") → `YENİ KIRMIZI`, geri alma çıkış 1 → AC-18c kalır.
- Sayfa seçimiyle aşılamaz: işçinin ölçümüne göre `_DataTable.cshtml` ve `_IndexL10n.cshtml` taşıyan 152 liste sayfasının
  hiçbirinde `GetAllStrings(true)` yok.
- İkinci okuma: UIC-6 (satır 170) «pattern A: new surfaces only» ve CI-11 başlığı «on a new surface» — yalnız tabanda hiç dosyası
  olmayan sayfaya eklenen partial. Bu okuma AC-18'i geçirir ama "Runs on" hücresinde yazılı değil.

## Sonraki adım

Paket revizyonu (rev 14) — CI-11 "Runs on" hücresinin yeni yüzey okumasına daraltılması; aynı revizyonda inceleme 5'in açık
bulgusu (CI tetikleyici yollarına seçici, taban ve workflow dosyası) eklenebilir. Sahip kararı bekleniyor.
