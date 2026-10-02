# Denetim defteri — Diten.HcmService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.HcmService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| hcm-taslak-denetimi | aday | yazıcı | IDraftAuditService |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

* **hcm-taslak-denetimi** — Uygulaması yalnız uygulama günlüğüne yazar ("non-authoritative audit fallback").

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- CleanupEmployeeSmokeFixtureCommand
- ConsumeWorkflowDecisionCommand
- CreateEmployeeDraftCommand
- EnsureEmployeeSmokeFixtureCommand
- PatchEmployeeDraftCommand
- ReviewEmployeeDraftCommand
- SubmitEmployeeDraftCommand
- ValidateDraftReferencesCommand
