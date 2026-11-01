# PVG sekizinci dalga — DoR (kart §1) kanıtı: WP-0230-DL10-R1

> Control Tower kaydı, 2026-11-02. `PLANNED → READY` geçişinin kanıtıdır. Dağıtım, `prompt_gate.py check` çıkış 0 ile olur; başlatma sözü sahibin «onayla» sözüdür (`docs/records/decisions/2026-11/pvg-mod0230-onayla-01.md`).

| DoR maddesi | WP-0230-DL10-R1 |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 15 (`1ba98662…`), `main`'de onaylı (PR #329). `pack_approval.py check` çıkış 0, 2026-11-01. |
| Sözleşme | AC-13, AC-35 (revizyon 15 metni). |
| Bağımlılık | WP-0230-DL10 `REWORK` (KK-15); teslimi `990ceb5fb` bu işte alınır. |
| Kapsam | Prompt'taki İzinli yollar. KK-15'in saydığı parola sızıntısı, test adı ve test boşluklarına ek olarak depo değerinin sıkılığı ve gizli değer sağlayıcısı; ikisi paket şartıdır ("Build/Test Persistence (Mongo Store)"), kapsam eklemesi değildir (prompt incelemesi F-8). |
| Kapsam dışı | 404 (AC-6), depo kesintisinde gövdesiz 500 (DL-4), denetim yazımı (DL-5), katalog kaydı (DL-11). |
| Git yalıtımı | Worktree `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-wp0230-dl10-r1`, dal `feature/pvg/wp-0230-dl10-r1`, Expected HEAD `6422dd703`. |
| Yürütme / şerit | Risk HIGH. Alt ajan işçi, uzaktan erişim kısıtı (S-114). Aynı anda başka bir PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. Dalı CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL10-R1-v1.1.md`, sha256 `0cbe77d7f2307e0b3714b06e2a843ef457c88c85f1fef556637303c39b2ef635`. |
| Bağımsız inceleme (HIGH) | v1.0: UYGUN DEĞİL (bir ENGEL, iki ORTA, yedi DÜŞÜK). v1.1: UYGUN, iki DÜŞÜK açık (F-8 bu kayıtla, F-11 işçi görevinde: sabotaj kapsamını DOĞRULA 6 belirler). Kayıtlar `docs/records/reviews/MOD-0230/prompts/`. Aynı inceleyen, yeniden okuma. |
| Kabul | E2 (işçi). DL-10'un gerekli kanıtı E4; kabul canlı yol açılana kadar ertelenir (KK-8). |

## Ölçülmeyenler

- Süitler bu kayıt için koşulmadı; taban sayıları işçi kendi B commit'inde ölçer.
- Geliştirme mongod'u yeniden sorgulanmadı (S-11, `pvg-wave8-dor-01` ölçümü, 2026-10-31).
