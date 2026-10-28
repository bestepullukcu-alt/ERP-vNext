---
format: rep-001-v2
module: MOD-0286
work_package: PSS-UI-P1-04
queue: pss-ui
evidence_level: E2
required_evidence_level: E2
required_evidence_source: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md@f0c5014dceefb8c42758dc8a8c92af17dfe8e7d2:822
decision: accepted
completion: WP Done
conditions: -
commit: 90af2618a1c4ea3b5b4388ca8203bf3e5a8efd92
evidence: docs/records/audits/2026-10/mod-0286-uic-ci-01/p1-04-kanit-03/run-record-v1.json@96b2c9c4e67f8af7356f1b29deeff500a6aa852b
acceptance_list: docs/records/audits/2026-10/pss-ui-p1-04-kabul-01/LISTE.md@ead63de8502b8ac5b42d944208d9be8a98d982e2
date: 2026-10-28
supersedes: -
verifier: bağımsız doğrulama alt ajanı (Claude, işi yapmayan ayrı oturum; kayıt pss-ui-p1-04-dogrulama-01)
implementer: işçi alt ajan (Claude, devops-agent; prompt PSS-UI-P1-04-P v1.4); CI iş akışı CT · Setup (sahip 'ci-kur')
module_pack: execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md@f0c5014dceefb8c42758dc8a8c92af17dfe8e7d2
---

# Kabul kaydı 01 — PSS-UI-P1-04

MOD-0286 DL-3 (UI uyum CI bağlantısı: değişen sayfa seçicisi, ilk taban, ayrı CI işi) kabul edildi.

## Kod ve iş akışı

| Parça | Commit | Not |
|---|---|---|
| İşçinin commit'i (seçici + ilk taban) | `a37ae9988` | taban `0a69c9712` |
| İş akışı (`.github/workflows/ui-compliance.yml`) | `6aed3c788` | CT · Setup yazdı, sahip 'ci-kur' |
| Kabul commit'i = PR #275 head | `9f3f100baff7bd4026458cfd025cbfd48f2fad79` | iş, iş akışı ve main `7495c4d27` birleşik |
| Doğrulanan ağaç (kanıt koşusu) | `90af2618a` | `9f3f100ba`'nın torunu; arada tetikleyici yolların hiçbiri değişmedi (kanıt AC-17b satırı) |

## AC-17 bölüm 2 — CI koşusu

| Alan | Değer |
|---|---|
| İş akışı yolu (kabul commit'inde) | `.github/workflows/ui-compliance.yml` |
| Koşu kimliği | 40678833945 |
| URL | https://github.com/bestepullukcu-alt/ERP-vNext/actions/runs/40678833945 |
| Head SHA | `9f3f100baff7bd4026458cfd025cbfd48f2fad79` (= kabul commit'i) |
| İş sonucu | success; seçici çıkışı 0 |
| Artefakt | `uic-selector-stdout`, sha256 `c1aff2f3c5ebd1ae6ce2def6d186fd04fed3ba15a3e25a41a37eaf4bcd91eacb` (log'da yazan ile indirilen dosyanınki aynı); içerik `süre: 0.1 s` |

Bu PR hiçbir sayfaya dokunmuyor. Bu yüzden seçici yalnız süre satırını yazdı. Bu, paketin beklediği "değişen sayfa yok" davranışıdır (AC-20).

## Kanıt

- **Kabul koşusu:** `p1-04-kanit-03` (`run_record.py`, commit `90af2618a`, ham çıkış 0). Kanıt betiği `uic-dl3-kanit-05.sh` ile koşuldu; zorunlu 13 kimliğin 13'ü geçti.
- **v04 koşusu:** `p1-04-kanit-02` (betik v04) 11/13'te kaldı. Sebep iki betik hatası, işte hata yok:
  - AC-20, iş akışında seçicinin geçtiği her satırı saydı. Rev 14 seçici yolunu tetikleyici listesine de koyduğu için ikinci bir satır çıktı.
  - AC-17b, gh'nin vermediği `path` alanını istedi.
- **v05 düzeltmeleri:**
  - AC-20 artık yalnız seçici çağrısını sayıyor.
  - AC-17b iş akışı yolunu `gh api` ile okuyor.
  - Kabul commit'i ayrı bir argüman.
  - BASE sabit.
- **v05 kırmızı kanıtı (`p1-04-v05-kirmizi-01.txt`):**
  - CI kimliği verilmeyince AC-17b kaldı.
  - Yanlış CI koşusu (phase1-gates) verilince AC-17b kaldı.
  - İki durumda da sonuç 12/13.
- **İşçinin ara koşusu:** `p1-04-kanit-01` (betik v04, 11/13; o an iş akışı yoktu).

## Doğrulama

`docs/records/audits/2026-10/pss-ui-p1-04-dogrulama-01/DOGRULAMA.md`: UYGUN, engelleyici 0. Kanıt betiği v04 HEAD'de yeniden koşuldu ve kayıtlı koşuyla aynı çıktı.

## Gerekli kanıt seviyesi

E2: paket rev 14, "Teslim dilimleri" DL-3 satırı (822).

## Açık kalanlar (kabulü etkilemez)

Doğrulamanın engelleyici olmayan bulguları:

| Bulgu | Konu | Nereye gider |
|---|---|---|
| E-1 | `Meetings/series` harf büyüklüğü; CI-4 76/77 ile paketteki 77/78 farkı | paket revizyonu girdisi |
| E-4 | Sayfa liste olmaktan çıkınca eski CI-3/CI-9 taban satırları "kapandı" yazılmıyor | seçicinin sonraki sürümü |
| E-5 | argparse `allow_abbrev` | seçicinin sonraki sürümü |
| E-6 | CI-11, CI-3, CI-14 ve CI-15 örnek biçimleri; CI-14'ün 376 satırı için sahip kararı | paket revizyonu girdisi |
| E-2, E-3, E-8 | Rapor ifadesi | — |
| E-7 | v04 yorumu | — |

Ek açık noktalar:
- İş akışı seçici çıkışı 1 ya da 2 olmadıkça yeşil kalıyor. Bu, paket satır 437'nin kendi kuralı.
- Linux'ta harf duyarlılığı ölçülmedi.

## Kapsam dışı

- P1'in bütün kabulü (DL-1 + DL-2 + DL-3): CT ayrıca bildirir.
- P2.
