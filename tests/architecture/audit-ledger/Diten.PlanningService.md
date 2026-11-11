# Denetim defteri — Diten.PlanningService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001). Ali'nin 2026-10-14 kararı, aynı Mongo atomik yazımında iş durumu ve zorunlu yerel iz üreten komutlara geçici `c` yolu verir. Bu iz Platform merkezi denetim günlüğü değildir; BL-501 bağlanınca yerel iz silinmeden merkezi kayıt yanına eklenir. Mimari tarayıcı bağlantıyı, aşağıdaki Mongo testleri ise yazma hatasında sıfır iş etkisini ölçer.

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| batch-inceleme-izi | c | yazıcı | IHistoryImportBatchStore.TryDecideAsync |
| satir-kabul-izi | c | yazıcı | IAcceptedDemandHistoryStore.AcceptAsync |
| draft-olusturma-izi | c | yazıcı | IManualDraftStore.CreateAsync |
| forecast-secim-izi | c | yazıcı | IForecastDraftStore.CreateForecastAsync |
| draft-duzenleme-izi | c | yazıcı | IManualDraftStore.EditWeekAsync |
| draft-dislama-izi | c | yazıcı | IManualDraftStore.ExcludeSeriesAsync |
| draft-inceleme-izi | c | yazıcı | IManualDraftStore.TransitionReviewAsync |
| yayin-izi | c | yazıcı | IInternalPublishedRevisionStore.PublishAsync |
| gecersiz-kilma-izi | c | yazıcı | IInternalRevisionInvalidator.InvalidateAsync |
| geri-donus-izi | c | yazıcı | IRollbackDraftStore.CreateAsync |
| mrp-lead-time-izi | c | yazıcı | ILeadTimeMutationStore.ExecuteAsync |

> `ReviewHistoryImportBatchCommand`: durum ve karar izi tek Mongo belge güncellemesindedir; `HistoryImportMongoIntegrationTests` karar/audit ve hata yollarını sınar. İz görünürlüğü bekliyor (dilim: S1).
> `AcceptDemandHistoryRowsCommand`: satır ve kabul izi tek Mongo transaction'ındadır; `AcceptedDemandHistoryMongoIntegrationTests` audit yazma hatasında sıfır kabulü sınar. İz görünürlüğü bekliyor (dilim: S1 tarayıcı kanıtı).
> `CreateForecastRunCommand`: `CreateForecastRunHandler` → `ForecastWorkflow` → `IForecastDraftStore.CreateForecastAsync`. Ali'nin 2026-10-14 dar onayıyla geçici AUD-001 `c` yolu: ForecastRun, Draft manifesti + seri parçası, Draft oluşturma `ManualDraftAuditRecord` izi ve yöntem seçimi `ForecastSelectionAudit` izi aynı Mongo transaction'ında kalıcılaşır. İki audit'ten herhangi birinin insert hatasında Run, Draft ve yeni audit kayıtları bütünüyle geri alınır; HTTP 503 döner. `PublishedRevisionMongoIntegrationTests.ForecastHttp_EitherMandatoryAuditInsertFailure_RollsBackAllWrites` gerçek loopback Mongo/JWT yolunda her iki hatayı ölçer; `ForecastHttp_ReevaluatesAcceptedHistoryAndWritesRunDraftAndAudits` açık seçim 201, aynı içerik tekrarında 200, farklı içerikte 409 ve kalıcı iki izi ölçer. Bu yerel iz Platform merkezi denetim günlüğü değildir; `BL-501 bekliyor` (merkezî ekip, MVP'ler sonrası). Ekran iz görünürlüğü henüz tamamlanmadı: oluşturulan Draft'ın aktör/zaman/gerekçesi okunabiliyor, fakat iki audit'in kayıt ekranında okunması ve gerçek S1/S3 login/tarayıcı kanıtı açık. Defter/scanner kabulü bu görünürlük veya S3 kabul kapısını kapatmaz.
> `CreateManualDraftCommand`: `ManualDraftMongoIntegrationTests.MongoAuditInsertFailure_RollsBackManifestAndAllSeriesParts`; iz görünürlüğü bekliyor (dilim: S3).
> `EditManualDraftWeekCommand`: `ManualDraftMongoIntegrationTests.MongoEditAuditFailure_RollsBackVersionAndWeekChange`; iz görünürlüğü bekliyor (dilim: S3).
> `ExcludeManualDraftSeriesCommand`: `ManualDraftApiMongoIntegrationTests.ExclusionAuditFailure_RollsBackReopenSlotAndContent`; iz görünürlüğü bekliyor (dilim: S3).
> `TransitionManualDraftCommand`: `ManualDraftReviewMongoIntegrationTests.ReviewAuditInsertFailure_RollsBackStateAndCandidateSlot`; iz görünürlüğü bekliyor (dilim: S3).
> `PublishDemandRevisionCommand`: `PublishedRevisionMongoIntegrationTests.Publish_AuditFailure_RollsBackStateSnapshotSlotAndOutbox`; iz görünürlüğü bekliyor (dilim: S3).
> `InvalidateDemandRevisionCommand`: `InvalidationMongoIntegrationTests.Invalidate_AuditCollision_RollsBackStateSlotAndOutbox`; iz görünürlüğü bekliyor (dilim: S3).
> `RollbackDemandRevisionCommand`: `RollbackDraftMongoIntegrationTests.Rollback_AuditWriteFailure_RollsBackAllNewDocuments`; iz görünürlüğü bekliyor (dilim: S3).
> MOD-0189 `LeadTimeCommand` (`Create`, `Revise`, `Submit`, `Approve`, `Reject`): `LeadTimeMongoStore.ExecuteAsync` iş kaydı, tekrar isteği alındısı ve append-only yerel audit'i aynı Mongo transaction'ında yazar. `MrpLeadTimeMongoIntegrationTests.AuditFailure_RollsBackEveryCommand_AndItsReceipt` beş komutun her birinde audit hatasıyla sıfır yeni iş etkisini ölçer. Geçici AUD-001 c yolu merkezi audit değildir; iz görünürlüğü bekliyor (dilim: MOD-0189 kullanıcı yüzeyi), BL-501 bekliyor (merkezi ekip, MVP'ler sonrası).
> Her komut için BL-501 bekliyor (merkezi ekip, MVP'ler sonrası). `CreatePlanningCycleCommand` ve `CreateDemandHistoryImportBatchCommand` henüz işlem-içi audit yazmaz; aşağıda bilinen borçtur.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|
| CreateManualDraftCommand | draft-olusturma-izi | ManualDraftWorkflow |
| CreateForecastRunCommand | forecast-secim-izi | ForecastWorkflow |
| EditManualDraftWeekCommand | draft-duzenleme-izi | ManualDraftWorkflow |
| ExcludeManualDraftSeriesCommand | draft-dislama-izi | ManualDraftWorkflow |
| TransitionManualDraftCommand | draft-inceleme-izi | ManualDraftWorkflow |

## Bilinen borç

- CreatePlanningCycleCommand
- CreateDemandHistoryImportBatchCommand

## K2 borcu

K2 sınıflandırması ve merkezi iletici bağlantısı bekleniyor.

## Yazan sorgular

Bu modül için bilinen yazan sorgu yoktur.
