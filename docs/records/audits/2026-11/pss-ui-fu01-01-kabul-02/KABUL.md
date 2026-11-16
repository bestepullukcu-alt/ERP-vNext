---
format: rep-001-v2
module: MOD-0286-FU01
work_package: PSS-UI-FU01-01
queue: pss-ui
evidence_level: E2
required_evidence_level: E2
required_evidence_source: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md@404dcfbcb6899d4e2d9b117c97946d254bb82922:291
decision: accepted
completion: WP Done
conditions: -
commit: efdfe197058876e9939e07fd321cc1be9a846528
evidence: docs/records/audits/2026-11/mod-0286-fu01-01/fu01-kanit-02/run-record-v1.json@b3b105977a3427e267886ceda4a243bea34a0648
acceptance_list: docs/records/audits/2026-11/pss-ui-fu01-01-kabul-02/LISTE.md@b7d44e0749aafad1bf3d6446aa753dbfdb3eb65a
date: 2026-11-16
supersedes: -
verifier: bağımsız doğrulama alt ajanı (Claude Opus 5.5 · high, işi yapmayan, salt okunur; kayıt mod-0286-fu01-01/dogrulama-01.md)
implementer: işçi oturumu (Claude Sonnet 5.5 · high; prompt PSS-UI-FU01-01 v1.0)
module_pack: execution/domains/platform-shared-services/module-packs/MOD-0286-FU01-page-js-reference-mapping.md@404dcfbcb6899d4e2d9b117c97946d254bb82922
---

# Kabul kaydı 02 — PSS-UI-FU01-01

MOD-0286-FU01 DL-1 kabul edildi: sayfa JS dosyaları görünüm başvurusuyla eşleniyor, yeni ihlal kırmızı veriyor, FU-19 taban satırları yazılı. Bu iş için yazılan ilk kabul kaydıdır. Adı 02, çünkü liste 01'in dizini (`pss-ui-fu01-01-kabul-01`) kayıtsız kaldı.

- **Kod:** işçinin tek commit'i `f4950c377` (taban `c412e40ef`). Yalnız `scripts/uic_changed_pages.py` ve `tests/ui-compliance-baseline.tsv` değişti (+22 FU-19 satırı, 92 eşleşme). Doğrulanan ağaç `efdfe1970`: kule dalı, bu işi, main birleştirmelerini ve kanıt betiği v02'yi taşır.
- **Kanıt:** kayıtlı koşu `fu01-kanit-02` (`run_record.py`, commit `efdfe1970`, ham çıkış 0). Sonuç `zorunlu geçti 13 / 13`; `run_record.py verify` çıkış 0. AC-1…AC-11 ve AC-13 iş commit'inde ölçülür (betik iş sha'sını argüman alır).
- **AC-12:** PR #429'un `ui-compliance` koşusu: run id 43848593956, head `cf2e391c6`, sonuç success, seçici çıkışı 0. Artefakt `ac12-ci-kaydi-01/` dizininde; sha256 `c1aff2f3…eacb`, CI logundaki satırla aynı. Betik v02 bu kaydı `--ci-kaydi` ile denetledi: iş commit'i PR başının atası ve artefakt özeti eşit (7 denetim).
- **Neden betik v02:** v01 `head_sha` alanının iş commit'ine eşit olmasını istiyordu. Paket AC-12 ise "the pull request's head" der ve PR başı işi birleştiren kule dalıdır. v02 yalnız AC-12'yi paket metnine çeker; diğer maddeler v01 ile bayt bayt aynıdır. İlk koşu `fu01-kanit-01` (v01, 12/13, ham çıkış 1) kayıtlı olarak duruyor.
- **Bu CI koşusunun sınırı:** PR sayfa değiştirmediği için seçici CI'da 0 sayfa ölçtü. Eşleme ve kırmızı davranışı kayıtlı koşuda ölçüldü.
- **Doğrulama:** `docs/records/audits/2026-11/mod-0286-fu01-01/dogrulama-01.md` (KOŞULLU, kritik 0, engelleyici 0). Tek koşulu AC-12'nin PR sonrası kapanmasıydı; bu kayıtla kapandı. Betik v02 farkı bağımsız olarak yeniden doğrulanmadı.
- **Gerekli kanıt seviyesi:** E2, paket rev 04 "Teslim dilimleri" DL-1 satırı (291).
- **Açık kalanlar (kabulü etkilemez):**
  - dogrulama-01'in düşük bulgusu (`scan()` tek seviye alt klasör) ölçülmedi.
  - Mimari denetim 01 takipleri sahibin onayını bekliyor: CI'da desen self-test adımı, CODEOWNERS, CI-14 sahipleri.
  - Geçici klonların temizliği için sahibin sözü gerekiyor.
- **Kapsam dışı:** PR #429'un birleştirilmesi (yalnız merge commit, ikinci insan) ve modülün bütün olarak kapanışı.
