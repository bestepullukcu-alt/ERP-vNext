# PVG dokuzuncu dalga — DoR (kart §1) kanıtı: WP-0230-DL11

> Control Tower kaydı, 2026-11-02. `PLANNED → READY` geçişinin kanıtıdır. Dağıtım, `prompt_gate.py check` çıkış 0 ile olur. Başlatma sözü sahibin «onayla» sözüdür (`docs/records/decisions/2026-11/pvg-mod0230-onayla-01.md`).

| DoR maddesi | WP-0230-DL11 |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 15 (`1ba98662…`), `main`'de onaylı (PR #329). "Closed Catalog Registration" bölümü, OD-PVG-13 sahip sözü «Evet, sürsün» (`pvg-mod0230-kapali-kayit-03.md`). |
| Sözleşme | AC-13 (f), AC-36, AC-37. AC-38 (E5, çapraz modül canlı koşu) işçinin değil, kabulde CT'nindir. |
| Bağımlılık | WP-0230-DL10-R1 `INTEGRATED` (CT dalı `27b952a9d`). KK-7'deki "ana dal" tek PR düzeninde CT dalı okunur (`docs/records/decisions/2026-11/pvg-mod0230-sira-kosulu-okuma-01.md`, CT okuması). |
| Taban | `NavManifestL10nGuardTests`'in MOD-0189 ve MOD-0191 kaynaklı iki kırmızısı tabandır: sahip sözü «taban-kabul» (`docs/records/decisions/2026-11/pvg-mod0230-nav-taban-01.md`). |
| Kapsam | Prompt'taki İzinli yollar: `src/Diten.PvgService.Api/ModuleRegistration/**` (beş dosya), tek proje referansı, `AddPvgServiceApiHost`'ta tek satır, testler, yedi `SharedResource.*.resx` dosyasında iki anahtar (l10n kapısı). |
| Kapsam dışı | Platform, Auth, Gateway, `.antigravity/**`, menü, seed, grant, appsettings, izin kapısı adaptörleri (OD-PVG-14), denetim yazımı (DL-5), 404 (AC-6), MOD-0189 ve MOD-0191 (XMC-001). |
| Git yalıtımı | Worktree `/Users/alitufanoglu/ERP-vNext/.claude/worktrees/pvg-wp0230-dl11`, dal `feature/pvg/wp-0230-dl11`, Expected HEAD `ec18ebae6`. |
| Yürütme / şerit | Risk HIGH. Alt ajan işçi, uzaktan erişim kısıtı (S-114): işçi kendi yalıtılmış worktree'sinde aynı dala geçer (WP-0230-DL10-R1 dağıtım eki 01 emsali). Model Sonnet 5.5 · high. Aynı anda başka PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. Dalı CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL11-v1.1.md`, sha256 `35ea8247db3bd2c87e41d8692131f6a00c2b8dc3ade48492ed474844aac83403`. |
| Bağımsız inceleme (HIGH) | v1.0: UYGUN DEĞİL (1 ENGEL, 3 ORTA, 8 DÜŞÜK; ENGEL sahip sözüyle kapandı). v1.1: UYGUN; 12 bulgunun hepsi kapandı, yeni DÜŞÜK N-1 prompt'un Known gaps satırında. Kayıtlar `docs/records/reviews/MOD-0230/prompts/`, v1.1 kaydı `@33016a8ae`. Aynı inceleyen yeniden okudu (alt ajan, Opus 5.5 · high istendi). |
| Kabul | İşçi E2 üretir. DL-11'in gerekli kanıtı E5 (AC-38): canlı çapraz modül koşusu ve yeniden başlatma denetimi; kabulde CT koşar. |

## Ölçülmeyenler

- Süitler bu kayıt için koşulmadı; taban sayılarını işçi kendi başlangıç merge'ünden sonra ölçer.
- N-1 (sayfa ayrıştırma eşitliği taban sürdükçe ölçülmüyor) için incelemenin önerdiği karalama koşusu istenmedi. Prompt'taki Known gaps satırı yeterli sayıldı, çünkü sayfalar gizli ve AC-36 (a) değerleri çalışma anında ölçüyor.
