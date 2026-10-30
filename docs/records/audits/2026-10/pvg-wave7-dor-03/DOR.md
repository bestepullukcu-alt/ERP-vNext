# PVG yedinci dalga — DoR (kart §1) kanıtı: WP-0230-DL7C

> Control Tower kaydı. Bu kayıt `PLANNED → READY` geçişinin kanıtıdır; dağıtım izni değildir. Dağıtım için iki koşul var:
> - `prompt_gate.py check` dağıtım kipinde çıkış 0 vermeli.
> - Sahip işçinin başlatılmasını bu sohbette onaylamalı.
>
> Tarih: 2026-10-30. Dayanak: `docs/records/audits/2026-10/pvg-wave7-verify-02/VERIFY-DL7B-R1.md` (KABUL EDİLEBİLİR; bir ORTA, üç DÜŞÜK açık) ve `pvg-wave7-verify-01/VERIFY-DL7B.md` (`step` farkı).

| DoR maddesi | WP-0230-DL7C |
|---|---|
| Kimlik + sahip | MOD-0230 Case Intake & Triage; paket sahibi "PVG paket sahibi" |
| Paket yetkisi | Revizyon 13 (`ee175d00…`), `main`'de onaylı (#290). Expected HEAD'deki paket bu sürümdür. Revizyon 14'ün onay isteği açık ve birleşmedi. |
| Sözleşme | AC-32. Yeni sözleşme yüzeyi yok. |
| Bağımlılık | WP-0230-DL7B-R1 (VERIFIED). İş commit'i `9e9cf178f`; aynı dal ve aynı worktree sürer. |
| Kapsam | Kalıcı: `_IndexL10n.cshtml`, `_Form.cshtml`, `pvg-case-intake-triage-ui.test.js`, `PvgCaseIntakeTriageWebTests.cs`. Yalnız geçici sabotaj: dört sayfa görünümü, `index.l10n.js`, `index.js`. |
| Kapsam dışı | Edit'in üç alanı sessizce silmesi (servis değişikliği ister); servis, Gateway, `ocelot.json`; ortak dosyalar; resx. |
| Git yalıtımı | Worktree `.claude/worktrees/pvg-wp0230-dl7b-r1`, dal `feature/pvg/wp-0230-dl7b-r1`, Expected HEAD `9e9cf178f`. `node_modules` önceki işten duruyor. |
| Yürütme / şerit | Risk HIGH (sözleşme, uyum). Aynı anda başka bir PVG işçisi yok. |
| Teslim (S-63, S-76) | İşçi yalnız kendi dalında commit eder, bildirir ve durur. Dalı CT dalına CT birleştirir. |
| Prompt (§36.1) | `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7C-v1.0.md`, sha256 `60f5e06987aa07b412fc63eef7238e9cfb8a3813400a3807cf21182d7ff8589c`. |
| Bağımsız inceleme (HIGH) | v1.0: UYGUN (4 DÜŞÜK; `WP-0230-DL7C-v1.0-inceleme-01.md`). İnceleyen prototiple yeni testleri ve S1–S11'i Expected HEAD'in kopyasında koştu: PVG vitest 34, C# üçlü filtre 72, 0 başarısız. |
| Kabul | E2 (işçi). DL-7'nin gerekli kanıtı E4 ve canlı kapıya bağlı (DALGA5-01 D5-5). |

## CT okumaları

Sahibin hızlanma isteğiyle (2026-10-30) DÜŞÜK bulgular yeni tur açmaz. Üçü işçiye başlatma mesajında verilir:

1. **F-1.** V15'in köprü anahtarları arasında `recordSaved` da bulunur ve yükleyiciden `RecordSaved` olarak çıktığı iddia edilir. V13 bu anahtarı yalnız yük tarafında kapsıyor.
2. **F-2.** Sabotajı geri yazdıktan sonra `.cshtml` dosyasına `touch` uygulanır. Değişiklik zamanını koruyan kopyalamada dotnet görünümü yeniden derlemiyor; sha256 aynı olsa da test kırmızı kalıyor.
3. **F-3.** `index.js` "Önce oku" listesinde değil ama S10'un çapası ve V14'ün sınadığı dosyadır; okunur.

F-4 (iki gerekçe cümlesi): `form.js:261-281` Edit'te `ReceivedAtUtc`'yi doldurmuyor; `:369` fikstüründe `NoRecords` ve `ErrorOccurred` anahtardan farklı. İşi değiştirmez; bilgi olarak kayıtta kalır.

## Sıra notu

Bu iş revizyon 13'e bağlı. Revizyon 14'ün onay isteği işçi başlamadan birleşirse dağıtım kapısı metni reddedebilir (bu risk ölçülmedi). Bu yüzden birleştirme mesajı işçi başladıktan sonra verilir.
