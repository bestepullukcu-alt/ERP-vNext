# Denetim defteri — Diten.PlanningService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001). Merkezi iletici BL-501, Ali/Control Tower kararıyla MVP'ler sonrasına bırakılmıştır. Aşağıdaki on yazma komutu için **BL-501 bekliyor**; bu ifade denetlenmiş komut, kabul edilmiş iz, istisna veya mimari test onayı değildir.

BL-501 bekliyor: CreatePlanningCycleCommand, CreateDemandHistoryImportBatchCommand, ReviewHistoryImportBatchCommand, CreateManualDraftCommand, EditManualDraftWeekCommand, ExcludeManualDraftSeriesCommand, TransitionManualDraftCommand, PublishDemandRevisionCommand, InvalidateDemandRevisionCommand, RollbackDemandRevisionCommand.

Bu defter mevcut Mongo işlem-içi iş audit'ini merkezi Platform audit ile eşdeğer ilan etmez. AUD-001 ölçümündeki yeni borç kırmızısı, BL-501 bağlantısı veya ayrı Control Tower kararı gelene kadar açık kalır.

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Yeni komutlar bu bölümde kabul edilmiş borç olarak kaydedilmedi; AUD-001 yalnız önceden sabitlenmiş borcun azalmasına izin verir.

## K2 borcu

K2 sınıflandırması ve merkezi iletici bağlantısı bekleniyor.

## Yazan sorgular

Bu modül için bilinen yazan sorgu yoktur.
