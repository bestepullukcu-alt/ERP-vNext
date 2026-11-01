---
format: rep-001-v2
module: MOD-0286
work_package: PSS-UI-P1-04
queue: pss-ui
evidence_level: E2
required_evidence_level: E2
required_evidence_source: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md@819cc642a463049de07f9ebc598228fe97dc07d1:974
decision: accepted
completion: WP Done
conditions: -
commit: d49ddca5659c63327c8a26a9fed5043527e9e53f
evidence: docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-04/run-record-v1.json@e2ac4c84a83205a053d5bf4861b9c80ad4476599
acceptance_list: docs/records/audits/2026-10/pss-ui-p1-04-kabul-02/LISTE.md@b322c3aaf34185369b74cca60489e724e98158cb
date: 2026-10-31
supersedes: docs/records/audits/2026-10/pss-ui-p1-04-kabul-01/KABUL.md
verifier: bağımsız doğrulama alt ajanı (Claude, işi yapmayan ayrı oturum; kayıtlar pss-ui-p1-04-dogrulama-01 ve seçicinin rev 15 hali için pss-ui-p1-05-dogrulama-01)
implementer: işçi alt ajan (Claude, devops-agent; prompt PSS-UI-P1-04-P v1.4); CI iş akışı CT · Setup (sahip 'ci-kur')
module_pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md@819cc642a463049de07f9ebc598228fe97dc07d1
---

# Kabul kaydı 02 — PSS-UI-P1-04

MOD-0286 DL-3 (UI uyum CI bağlantısı: değişen sayfa seçicisi, ilk taban, ayrı CI işi) kabul edildi. Bu kayıt kabul kaydı 01'in yerine geçer.

**Neden 02:** paket rev 15 (sha `0c53533e…`; tek seviye sayfalar; PR #312, merge `819cc642a`, onaylayan bestepullukcu-alt) onaylandı. S-5 / CT-APR-01 kabulü main'deki güncel onaylı sürüme bağlar ve `ct_queue.py` her `validate`'te ACCEPTED satırın kaydını yeniden denetler. İş commit'i değişmedi; kanıt rev 15'i taşıyan commit'te yeniden koşuldu (kart S-86).

## Kod ve iş akışı

| Parça | Commit | Not |
|---|---|---|
| İşçinin commit'i (seçici + ilk taban) | `a37ae9988` | taban `0a69c9712` |
| İş akışı (`.github/workflows/ui-compliance.yml`) | `6aed3c788`, son değişiklik `fe5a4169d` | seçici çağrı satırı değişti; davranış aynı |
| Seçicinin sonraki tek değişikliği | `e3eda1752` | PSS-UI-P1-05 (rev 15, AC-21), ayrı kabul |
| Kabul commit'i = PR #325 head = doğrulanan ağaç | `d49ddca5659c63327c8a26a9fed5043527e9e53f` | iş, iş akışı, rev 15 ve main birleşik |

## AC-17 bölüm 2 — CI koşusu

| Alan | Değer |
|---|---|
| İş akışı yolu (kabul commit'inde) | `.github/workflows/ui-compliance.yml` |
| Koşu kimliği | 41172080715 |
| URL | https://github.com/bestepullukcu-alt/ERP-vNext/actions/runs/41172080715 |
| Head SHA | `d49ddca5659c63327c8a26a9fed5043527e9e53f` (= kabul commit'i) |
| İş sonucu | success; seçici çıkışı 0 |
| Artefakt | `uic-selector-stdout`, sha256 `c1aff2f3c5ebd1ae6ce2def6d186fd04fed3ba15a3e25a41a37eaf4bcd91eacb` (log'da yazan ile indirilen dosyanınki aynı); içerik `süre: 0.1 s` |

Bu PR hiçbir sayfaya dokunmuyor. Bu yüzden seçici yalnız süre satırını yazdı; aynı içerik PR #275'te de aynı sha'yı verdi. Bu, paketin beklediği "değişen sayfa yok" davranışıdır (AC-20). Yani bu koşu, gerçek bir sayfa değişikliğini CI'da denetlemedi; sayfa denetimi AC-17a ve AC-18'in yerel klon koşularıyla ölçüldü.

## Kanıt

- **Kabul koşusu:** `p1-04-kanit-04` (`run_record.py`, commit `d49ddca56`, ham çıkış 0). Kanıt betiği `uic-dl3-kanit-07.sh` ile koşuldu; zorunlu 13 kimliğin 13'ü geçti.
- **v05'ten farklar:**
  - CT-2 paket sha'sı.
  - CT-3: seçici işten sonra yalnız `e3eda1752`'de değişmiş olabilir.
  - AC-19: tabandaki üç sahip kararı sayılır (`pss-mod0286-taban-01`, `tek-seviye-borc-01`, `taban-pvg`); satır kümesi yine taslakla aynı olmalı.
  - AC-20: çağrı deseni `if python3 …` satırını görür (`fe5a4169d`). v06'nın kayıtsız denemesi bu yüzden 11/13 kaldı; betik hatası, işte hata yok.
- **v07 kırmızı kanıtı (`p1-04-v07-kirmizi-01.txt`):**
  - CI koşusu verilmeyince AC-17b kaldı, 12/13.
  - Eski koşu ve eski kabul commit'i verilince AC-17b kaldı, 12/13.
  - Tabanda başka karar adı ya da `tek-seviye-taban-01` yazımı AC-19 süzgecinde kırmızı (komut ayrı koşuldu, tam betikte değil).

## Doğrulama

- `docs/records/audits/2026-10/pss-ui-p1-04-dogrulama-01/DOGRULAMA.md`: UYGUN, engelleyici 0 (rev 14 ağacında).
- `docs/records/audits/2026-10/pss-ui-p1-05-dogrulama-01/DOGRULAMA.md`: UYGUN, engelleyici 0; seçicinin rev 15 halini ölçtü, iki seviyeli sayfalarda fark 0.
- Kanıt betiği v06 ve v07'yi bağımsız bir doğrulayıcı okumadı; yalnız CT yazdı ve kırmızısını ölçtü.

## Gerekli kanıt seviyesi

E2: paket rev 15, "Teslim dilimleri" DL-3 satırı (974).

## Açık kalanlar (kabulü etkilemez)

Kabul kaydı 01'in tablosu aynen geçerlidir: E-1, E-4, E-5, E-6 (sonraki seçici ya da paket revizyonu), E-2, E-3, E-7, E-8.

Ek açık noktalar:
- İş akışı seçici çıkışı 1 ya da 2 olmadıkça yeşil kalıyor. Bu, paketin kendi kuralı.
- Linux'ta harf duyarlılığı ölçülmedi.

## Kapsam dışı

- AC-21 (PSS-UI-P1-05'in kabulünde).
- P1'in bütün kabulü (DL-1 + DL-2 + DL-3): CT ayrıca bildirir.
- P2.
