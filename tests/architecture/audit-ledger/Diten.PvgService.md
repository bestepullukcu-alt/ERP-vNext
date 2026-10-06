# Denetim defteri — Diten.PvgService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.PvgService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|

Bu serviste bugün hiçbir denetim izi yok (ölçüm 2026-10-02). `PvgAuditIntent` bellek içi bir kayıttır; hiçbir depoya yazılmaz, aktör ve kiracı kimliği taşımaz.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

Ad kuralı (CT kararı 2026-10-02): bu serviste adı `Command` ile biten her tür yazma komutudur — servis MediatR kullanmıyor; 13 komutun hepsi adla ölçülür.

- AcceptMod0230HandoffCommand
- AttachSignalMetricDataProductReferenceCommand
- CreateIntakeDraftCommand
- CreateMeddraCodingWorkItemCommand
- CreateSignalHypothesisContractCommand
- MarkMeddraCodingReviewedCommand
- MarkSignalMinimumReadyCommand
- MarkSignalReviewDecisionContractCommand
- ProposeMeddraCodedTermCommand
- RouteIntakeDraftCommand
- TriageIntakeDraftCommand
- UpdateIntakeDraftCommand
- UpdateSignalMinimumAssessmentCommand
