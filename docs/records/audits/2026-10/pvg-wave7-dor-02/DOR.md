# PVG yedinci dalga — DoR (kart §1) kanıtı: WP-0230-DL7B-R1

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir. Dağıtım için iki koşul var:
> - `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli.
> - Sahip işçinin başlatılmasını bu sohbette onaylamalı.
>
> Tarih: 2026-10-30. Dayanak: `docs/records/audits/2026-10/pvg-wave7-verify-01/VERIFY-DL7B.md` (KABUL EDİLEMEZ), kart §0.2 Y1 ve Y4, SOP §28.2.

| DoR maddesi | WP-0230-DL7B-R1 |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 13 (`ee175d00…`), `main`'de onaylı (#290). Expected HEAD'deki paket bu sürümdür. Revizyon 14'ün onay isteği açık ve birleşmedi. |
| Sözleşme | AC-32 (görünen ad, CT kararı A-6). Yeni sözleşme yüzeyi yok. |
| Başarısız ölçüt ve kök neden | Köprü camelCase üretiyor, yükleyici çevirmeden yazıyor, ekran PascalCase okuyor. Kanıt `VERIFY-DL7B.md` "Engel". |
| Bağımlılık | WP-0230-DL7B (REWORK). İş commit'i `18e23b575`; aynı dal ve aynı worktree sürer. |
| Kapsam | Kalıcı: `index.l10n.js`, `pvg-case-intake-triage-ui.test.js`, `PvgCaseIntakeTriageWebTests.cs`. Yalnız geçici sabotaj: `_IndexL10n.cshtml`, `form.js`, denetleyici. |
| Kapsam dışı | Edit sessiz silme ve `step` farkı (servis değişikliği ister); servis, Gateway, `ocelot.json`; ortak dosyalar; resx. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl7b-r1`, dal `feature/pvg/wp-0230-dl7b-r1`, Expected HEAD `18e23b575`. `node_modules` önceki işten duruyor. |
| Yürütme / şerit | Risk HIGH (sözleşme, uyum, yetki, kiracı). Aynı anda başka bir PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. Dalı CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7B-R1-v1.1.md`, sha256 `b4a06904a78adbfa7ae77634c99dcdce666e9dc2fb74b1ebe983b8051fc4c1e6`. |
| Bağımsız inceleme (HIGH) | v1.0: KOŞULLU (E-1, 8 DÜŞÜK; `WP-0230-DL7B-R1-v1.0-inceleme-01.md`). v1.1: UYGUN (2 DÜŞÜK; `WP-0230-DL7B-R1-v1.1-inceleme-01.md`). İnceleyen prototiple V13, W13, W14 ve sabotajları Expected HEAD'in kopyasında koştu. |
| Kabul | E2 (işçi). DL-7'nin gerekli kanıtı E4 ve canlı kapıya bağlı (DALGA5-01 D5-5). |

## CT okumaları

Sahibin hızlanma isteğiyle (2026-10-30) DÜŞÜK bulgular yeni tur açmaz. İkisi işçiye başlatma mesajında verilir:

1. **F-9.** Taban ad listesi ve sabotaj kopyaları worktree dışında tutulur, yolları rapora yazılır. Yoksa commit sonrası `git status --short` boş çıkmaz.
2. **F-10.** Doğrulayıcı (madde 7) bugün de 12 maddeyle çıkış 1 verir; bu beklenen sonuçtur. Karşılaştırma madde kümesiyledir.

## Sıra notu

Bu iş revizyon 13'e bağlı. Onay isteği işçi başlamadan birleşirse dağıtım kapısı metni reddedebilir (bu risk ölçülmedi). Bu yüzden birleştirme mesajı işçi başladıktan sonra verilir.
