# PVG yedinci dalga — DoR (kart §1) kanıtı: WP-0230-DL7B

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir. Dağıtım için iki koşul var:
> - `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli.
> - Sahip işçinin başlatılmasını bu sohbette onaylamalı.
>
> Tarih: 2026-10-30. Kararlar:
> - `docs/records/decisions/2026-10/pvg-mod0230-secenek-kaynagi-01.md` (SECENEK-01: S-2 ekran seçenekleri vekil üzerinden AC-34 ucundan alır; S-3 DL7B, DL4D'den sonra);
> - `docs/records/decisions/2026-10/pvg-mod0230-ceviri-dl7b-01.md` (CEVIRI-01).

| DoR maddesi | WP-0230-DL7B |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 13 (`ee175d00…`), `main`'de onaylı (#290, HIGH, bestepullukcu-alt, 2026-10-29). `pack_approval.py check --ref origin/main` 2026-10-30'da çıkış 0. |
| Sözleşme | AC-32 (alan girdileri ve seçenek kaynağı), AC-15 (çeviri, CEVIRI-01), AC-2 (11. eylem). Prompt'taki 45 alıntı Expected HEAD'de birebir; kapı bunlardan 34'ünü denetler. |
| Bağımlılık | WP-0230-DL4D CT dalında (`edfde7292`), VERIFIED (`docs/records/audits/2026-10/pvg-wave6-verify-01/VERIFY-DL4D.md`), kabul kaydı 02 E4 (`docs/records/audits/2026-10/pvg-dl4d-acceptance-02/RECORD.md`). |
| Kapsam dışı | Servis, Gateway ve `ocelot.json`; ortak görünümler ve SharedResource; triyajın dört ölçütü (QPPV AL-5 sonrası, AC-28 işi); ülke listesi (Present durumu). |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl7b-r1`, dal `feature/pvg/wp-0230-dl7b-r1`, Expected HEAD `ef68ec8d3`, temiz (2026-10-30'da oluşturuldu). `frontend/Diten.Web/node_modules` CT tarafından `npm ci` ile sağlandı (inceleme 01 F-7); dizin git-ignore'da. |
| Yürütme / şerit | Risk HIGH (sözleşme, uyum, yetki). Aynı anda başka bir PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. `origin/main` birleştirmesi yapmaz. İşçi dalını CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7B-v1.7.md`, sha256 `1c455145e580555096a227a0f8093ec517b4d568a95a8a042780500ff4c25978` @ af1ec41e4. |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0230/prompts/WP-0230-DL7B-v1.7-inceleme-01.md` @ b7048bcba: UYGUN (kritik 0, engelleyici 0, 10 DÜŞÜK). Öncesinde beş ara inceleme yapıldı (v1.0–v1.5, kayıt dışı). |
| Kabul | E2 (işçi: vitest ve C# testleri ile adlı sabotajlar). Ekran turu ve canlı ölçüm, CT dalına birleştirmeden sonra CT tarafından yapılır. |

## CT okumaları

1. **İnceleme 01 F-6 (Edit sessiz silme) bu işin kapsamını büyütmez.** Servis güncellemeyi isteğin tamamından kurar. Edit formunun doldurmadığı üç alan boş gönderilirse kayıttan silinir. Bu davranış DL7A'dan kalıyor. Takip için sahibe ayrı bir karar sorusu olarak gidecek; dağıtımı durdurmaz.
2. **Diğer DÜŞÜK bulgular** (F-1…F-5, F-7…F-10) dağıtımı durdurmaz. F-7 bu kayıtla kapandı (node_modules sağlandı). F-10 bu kayıt ve kuyruk geçişiyle kapanır.

## Dağıtım notu

Yük kuralı: her dotnet ve vitest komutundan önce `uptime`; 1 dakikalık yük 12'nin üstündeyse bekle. CT'nin yeşil koşusu ve ekran turu, işçi dalı CT dalına birleştikten sonra yapılır.
