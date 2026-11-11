# PVG sekizinci dalga — DoR (kart §1) kanıtı: WP-0230-DL10

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir. Dağıtım için iki koşul var:
> - `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli.
> - Sahip işçinin başlatılmasını bu sohbette onaylamalı.
>
> Tarih: 2026-10-31.

| DoR maddesi | WP-0230-DL10 |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 14 (`61c7902f…`), `main`'de onaylı (#317, `b901be248`). `pack_approval.py check --ref 6fd49036e` çıkış 0. Expected HEAD'deki paket bu sürümdür. |
| Sözleşme | AC-13, AC-35. |
| Bağımlılık | WP-0230-DL4D ACCEPTED (`pvg-dl4d-acceptance-03`). DL7B-R1 ve DL7C dağıtılmış ve doğrulanmış; paketin "DL4D ve DL7B dağıtılmış" koşulu sağlanıyor. |
| Kapsam | Prompt'taki İzinli yollar; paketin DL-10 listesiyle birebir (inceleme kaydı). |
| Kapsam dışı | Denetim yazımı (DL-5), kapalı katalog kaydı (DL-11, revizyon 15 taslağı), servis kapıları, ekran. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl10`, dal `feature/pvg/wp-0230-dl10`, Expected HEAD `da3cb0ce9` (push edildi). |
| Yürütme / şerit | Risk HIGH. Aynı anda başka bir PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. Dalı CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL10-v1.1.md`, sha256 `e244e401cb37661036b901e84ad8b107879df9113a6c1983f151468c163ec97e`. |
| Bağımsız inceleme (HIGH) | v1.1: UYGUN (bir DÜŞÜK; `docs/records/reviews/MOD-0230/prompts/WP-0230-DL10-v1.1-inceleme-01.md`). v1.0'ın beş DÜŞÜK bulgusu v1.1'de kapandı. |
| Kabul | E2 (işçi). DL-10'un gerekli kanıtı E4; canlı kısım CT'nin kabul betiğinde. Servisin dört kapısı reddettiği için canlı yazma ölçülemez (`pvg-mod0230-kapali-kayit-02.md`); kabul sınırı o sırada ayrıca değerlendirilir. |

## DoR ölçümleri (paket DL-10, DEPO-03 S-11)

CT, 2026-10-31 21:36, salt okunur:

1. **Geliştirme mongod'u replica set mi (S-11):** evet. `db.hello()` → küme `rs0`, yazılabilir birincil.
2. **Hosted servis tabanı:** v1.0 DoR'u iki tür ölçtü; inceleme `services/Diten.PvgService` için `6309e8089→da3cb0ce9` farkı olmadığını ölçtü. Yeniden koşulmadı.
3. **`AuditTrailStandardTests` PvgService sayımı:** Debt 13, Exceptions 0 (`AuditTrailStandardTests.cs:91`); aynı fark ölçümüyle geçerli.

İnceleme F-1 bu bölümle karşılandı: S-11 dağıtımdan önce ölçüldü.

## Sıra notu

Paket revizyon 15'in PR'ı bu iş bitmeden açılmaz (`pvg-mod0230-kapali-kayit-01.md` KK-5). Bu iş `feature/pvg/wp-0230-dl10` dalında kalır; revizyon 15 yalnız yerel `docs/pvg/mod0230-rev15-draft` dalındadır.
