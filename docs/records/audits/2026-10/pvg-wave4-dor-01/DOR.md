# PVG dördüncü dalga — DoR (kart §1) kanıtı: WP-0230-DL6

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır, dağıtım izni değildir.
> Dağıtım için iki şart var:
> - `prompt_gate.py check` dağıtım kipinde (`--prepare` olmadan) çıkış 0 vermeli.
> - Sahip, işçi oturumunun açılmasını bu CT sohbetinde onaylamalı.
>
> Tarih 2026-10-27. Plan kararı: `docs/records/decisions/2026-10/pvg-mod0230-dalga4-plani-01.md` (DALGA4-01, D4-1).

| DoR maddesi | WP-0230-DL6 |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 11 (`918dd660…`). `main`'de onaylı: #261, HIGH. Expected HEAD'de bayt bayt aynı. `pack_approval.py check` çıkış 0. |
| Sözleşme | AC-4. Tarama komutu paketin satır 1291'iyle birebir aynı. 15 alıntı Expected HEAD'de birebir. |
| Bağımlılık | OD-PVG-1 onaylı: `pvg-mod0230-od-pvg-1-izin-01.md`. `pvg.*` PKS-001 §4'te. WP-0230-DL4A INTEGRATED. |
| Kapsam dışı | Katalog, seed ve grant. Denetleyici öznitelikleri (DL-7). MOD-0231 anahtarları (XMC-001). |
| Kapsam | `PvgIntakeDraftApplicationService.cs` satır 7–11 ve `PvgApplicationContractShapeTests.cs`. CT okumasıyla ayrıca iki test dosyası (aşağıda). |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl6-r1`, dal `feature/pvg/wp-0230-dl6-r1`, Expected HEAD `17b7ff220` (CT dalı ucu), temiz. |
| Yürütme / şerit | Risk HIGH (yetki, sözleşme). WP-0230-DL3D ile dosyaları ayrık; paralel gidebilir (D4-4). |
| Teslim (S-63, S-76) | İşçi CT dalı ucundan çıkar, yalnız kendi dalında yerel commit yapar, dal + sha + test sonucunu bildirir ve durur. Bu dalgada işçi `origin/main` birleştirmesi yapmaz. İşçi dalını CT dalına CT birleştirir (S-76) ve birleşik hâlde testleri CT koşar. Push ve PR dalga sonunda yapılır; ikisi de sahibin dal + sha onayına bağlıdır. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL6-v1.0.md`, sha256 `afc12f539cbe19eb5947365632793317b224b998395c8da831d4f27d3bb7f9db` @ e880b8138. |
| Bağımsız inceleme (HIGH) | `docs/records/reviews/MOD-0230/prompts/WP-0230-DL6-v1.0-inceleme-01.md`, UYGUN. ENGEL 0, YÜKSEK 0. |
| Kabul | AC-4 taraması tam 10 satır gösterir; hepsi yeni ailedendir. P1 testi ve üç sabotaj geçer. **Kanıt sınırı:** DL-6 için gerekli seviye E5, bu iş E2 üretir. Kabul canlı kapıya kadar açık kalır. |

## CT okuması — iki ek test dosyası (inceleme bulgusu 1)

`tests/Diten.PvgService.RegPvBase.Tests/PvgIntakeDraftApplicationServiceTests.cs` ve `PvgIntakeDraftApplicationServiceTests.Revision04.cs`, eski anahtar değerlerini 37 iddiada birebir sorguluyor. Sabitler değişince bu iddialar kırmızıya döner.

Paketin DL-6 "Yollar" alanı yalnız şekil testini sayıyor. CT bu iki dosyadaki değişikliği aynı sözleşmenin zorunlu sonucu olarak okur. Değişiklik yalnız anahtar önekiyle (`pvg.mod0230.intake.` → `pvg.case-intake-triage.`) sınırlıdır.

## Dağıtım mesajında işçiye ayrıca söylenecekler

- **Bulgu 2.** Teslim adımı kart S-76'ya dayanır. S-63'ün "main birleştirmesi" adımı bu dalgada işçinin değil, CT'nindir.
- **Bulgu 3.** "Yalnız önek değişimi" ölçütü şöyle ölçülür:
  1. İki dosyanın Expected HEAD'deki hâline `sed 's/pvg\.mod0230\.intake\./pvg.case-intake-triage./g'` uygulanır.
  2. Sonuç, yeni hâlle `diff` ile karşılaştırılır.
  3. Fark boş olmalı.
