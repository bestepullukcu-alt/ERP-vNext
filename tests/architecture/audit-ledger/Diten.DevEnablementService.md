# Denetim defteri — Diten.DevEnablementService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.DevEnablementService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|

Bu serviste bugün hiçbir denetim izi yok: işaret, iletici, kendi günlüğü — hiçbiri (ölçüm 2026-10-02).

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- BulkDeleteGoldenReferenceCompactCommand
- BulkDeleteGoldenReferenceSlimCommand
- CreateGoldenReferenceCompactCommand
- CreateGoldenReferenceSlimCommand
- DeleteGoldenReferenceCompactCommand
- DeleteGoldenReferenceSlimCommand
- UpdateGoldenReferenceCompactCommand
- UpdateGoldenReferenceSlimCommand
