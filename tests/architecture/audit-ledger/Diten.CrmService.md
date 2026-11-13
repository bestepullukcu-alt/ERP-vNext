# Denetim defteri — Diten.CrmService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.CrmService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| crm-contact-yayinci | aday | yazıcı | IContactAuditPublisher.PublishAsync |
| crm-account-yayinci | aday | yazıcı | IAccountAuditPublisher.PublishAsync |
| crm-content-yayinci | aday | yazıcı | IContentCompositionAuditPublisher.PublishAsync |
| crm-knowledge-yayinci | aday | yazıcı | IKnowledgeConceptAuditPublisher.PublishAsync |
| crm-territory-yayinci | aday | yazıcı | ITerritoryLifecycleAuditPublisher.PublishAsync |
| crm-visit-evidence-yayinci | aday | yazıcı | IVisitEvidenceAuditPublisher.PublishAsync |
| crm-merkezi-iletim | b | işaret | ICentralAuditDeclaration |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

> **crm-contact-yayinci** — Varsayılan kayıt: yalnız uygulama günlüğü (`Logging…AuditPublisher`); `Crm:Audit:Mode=http` hiçbir ayar dosyasında yok.
> **crm-account-yayinci** — Aynı: varsayılan yalnız uygulama günlüğü.
> **crm-content-yayinci** — Aynı: varsayılan yalnız uygulama günlüğü.
> **crm-knowledge-yayinci** — Aynı: varsayılan yalnız uygulama günlüğü.
> **crm-territory-yayinci** — Yalnız uygulama günlüğü; HTTP uygulaması yok.
> **crm-visit-evidence-yayinci** — (WP-VW-W3L, kapsam yaz / kaldır) Aynı: varsayılan yalnız uygulama günlüğü; `Crm:Audit:Mode=http` ile `HttpCrmAuditPublisher` (SourceModule MOD-0155).
> **crm-merkezi-iletim** — (WP-VW-W6 F8-a, 2026-10-20 → **F8-b ile kaldırıldı, 2026-11-13**) F8-a bu izi ikinci bir ortak kitaplıkla (`Diten.BuildingBlocks.Auditing`: `IAuditableCommand` + `AuditForwardingBehavior`, CRM ayarı `CrmAuditForwarding`) kurmuştu. O kitaplık ve CRM ayarı artık yok; iz tek ortak ileticidedir (aşağıdaki 2026-11-02 ve 2026-11-13 notları).

Ölçüm notları (2026-11-02, CRM-AUD-KN-01 / BL-501):

> **crm-merkezi-iletim** — ortak iletici (`Diten.BuildingBlocks.CentralAudit`, kural §5 K4): `CentralAuditBehavior` (Building.Blocks, pipeline'ın en içi) işaretli komut BAŞARIYLA bitince (istisna yok, `Response<T>.IsSuccessful = true`) olayı servisin kendi `central_audit_outbox` koleksiyonuna yazar; `CentralAuditDispatcher` onu `POST /api/internal/audit/append`'e (X-Internal-Api-Key, `eventId` ile tekil) iletir. Kayıt bu serviste: `Program.cs` → `AddCentralAudit<CrmCentralAuditScope>(…, "Diten.CrmService")`. Kategori `Crm` (Platform `AuditCategory.Crm = 19`, K-AUD-1), modül kodu `CRM` (Platform `CrmManifestProvider`). İlk dilim: Knowledge grubunun 34 komutu (KnowledgePath 12, ObjectionResponse 8, SafetyText 7, CountryLegalProfile 7).
> **İşlem bütünlüğü** — `RequireEnlistment = false`: Knowledge depoları Mongo işlemi açmıyor (ölçüldü: CRM'de işlem açan yalnız Territory / VisitPlanning / Concept birim-işleri), kayıt komut başarıyla bittikten HEMEN SONRA ayrı bir yazımla outbox'a girer. Sahip kararı K-AUD-2 (Beste, 2026-11-02): bu komutlar §4.4 K2 sınıfında DEĞİL — K2 borcu yok. Boşluk adıyla: komut yazdıktan sonra outbox yazımı düşerse kayıt kaybolur ve yalnız hata günlüğü söyler (`Central audit: the outbox row could not be written…`, LogError). Adres / anahtar boşsa komutlar durmaz, olaylar outbox'ta birikir, dakikada bir uyarı ve `/health/central-audit` Degraded (§4.3 "sessiz değil", K8).
> **Eski yayıncılar AYNEN** — beş `aday` iz (yukarıda) değişmedi; merkezi kayıt onların yanına eklenir.

Ölçüm notları (2026-11-13, WP-CRM-SYNC-S0B / F8-b — `CT-DECISIONS.md` 2026-11-13):

> **crm-merkezi-iletim** — F8-a'nın işaretlediği 40 komut `ICentralAuditDeclaration`'a çevrildi (Account 2, MOD-0168 ön sipariş 12, MOD-0156 fiyatlandırma 18, W3L kanıt kapsamı 2, W5 bildirim ayarı 1, W3E kurum QR 5; liste ve her komutun işlem / nesne türü: `CrmCentralAuditF8bTests.F8bCommands`). İşlem ve nesne türü F8-a'daki değerle aynı; kaynak modül `MOD-0149 / 0155 / 0156 / 0168` → `CRM` (D-3). `CrmCentralAudit`'e `Deactivate`, `Revoke`, `Export` eklendi (Platform `AuditOperation` 5 / 9 / 10).
> **Sözleşme farkı** — F8-a boru hattının EN DIŞINDAYDI ve reddedilen / hata veren komutu da `Failed` / `Denied` diye yazıyordu; ortak iletici yalnız BAŞARILI komutu yazar (BL-501). Reddedilen deneme artık merkezi günlükte yok. "Ne değişti" ayrıntısı da merkezi kayıtta yok (AUD-001 §3); alan içi geçmişte durur: MOD-0156 `PricingChangeLog`, MOD-0168 ön sipariş olay yükü + `commercial_pre_order_settings_history` (B0a), W3E QR geçmişi, W3L katman geçmişi.
> **Arka plan işi** — `RunPreOrderTransmissionRetryCommand` HTTP bağlamı olmadan çalışır: `CrmCentralAuditScope` kaydı `System` aktörü, aktör kimliği ve adı boş olarak, siparişin kendi kiracısına yazar (D-2). Kiracı yoksa kayıt yazılmaz, hata günlüğü düşer.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- ActivateCyclePeriodCommand
- ActivateSegmentCommand
- ActivateStrategyTemplateCommand
- ActivateTerritoryModelCommand
- AddContentEngagementJourneyStageCommand
- AddKnowledgePathStepCommand
- AddTargetCustomerCommand
- AmendVisitReportCommand
- ApplyAccountTerritoryAssignmentsCommand
- ApplyPlanningSessionCommand
- ArchiveAudienceProfileCommand
- ArchiveCampaignCommand
- ArchiveCampaignTargetCommand
- ArchiveClaimCommand
- ArchiveClaimCountryVersionCommand
- ArchiveConceptChainTemplateCommand
- ArchiveConceptNodeCommand
- ArchiveConceptRelationshipCommand
- ArchiveConceptTypeCommand
- ArchiveConsentRecordCommand
- ArchiveContactAvailabilityCommand
- ArchiveContactAvailabilityExceptionCommand
- ArchiveContentEngagementJourneyCommand
- ArchiveContentEngagementJourneyStageCommand
- ArchiveCycleCapacityCommand
- ArchiveEligibilityPolicyCommand
- ArchiveKnowledgeContentCommand
- ArchiveKnowledgeContentConceptLinkCommand
- ArchiveKnowledgePathCommand
- ArchiveKnowledgePathStepCommand
- ArchivePlannedVisitCommand
- ArchivePreferenceRecordCommand
- ArchiveSegmentCommand
- ArchiveStrategyTemplateCommand
- ArchiveSubjectCommand
- ArchiveTargetCustomerCommand
- ArchiveTerritoryModelCommand
- ArchiveTopicCommand
- ArchiveVisitFrequencyPolicyCommand
- BulkDeleteAccountCommand
- CancelPlannedVisitCommand
- CloseClaimCountryCommand
- CloseCyclePeriodCommand
- ConfirmPlannedVisitCommand
- CreateAccountRelationshipCommand
- CreateAudienceProfileCommand
- CreateCampaignCommand
- CreateCampaignTargetCommand
- CreateCampaignTargetSnapshotCommand
- CreateClaimCommand
- CreateClaimCountryNewVersionCommand
- CreateClaimCountryVersionCommand
- CreateClaimNewVersionCommand
- CreateConceptChainTemplateCommand
- CreateConceptNodeCommand
- CreateConceptNodeWithRelationshipCommand
- CreateConceptRelationshipCommand
- CreateConceptTypeCommand
- CreateConsentRecordCommand
- CreateContactAvailabilityCommand
- CreateContactAvailabilityExceptionCommand
- CreateContactCommand
- CreateContentEngagementJourneyCommand
- CreateContentEngagementJourneyVersionCommand
- CreateContentVariantCommand
- CreateCycleCapacityCommand
- CreateCyclePeriodCommand
- CreateEligibilityPolicyCommand
- CreateKnowledgeContentCommand
- CreateKnowledgeContentConceptLinkCommand
- CreateKnowledgePathCommand
- CreateKnowledgePathVersionCommand
- CreatePlannedVisitCommand
- CreatePlanningSessionCommand
- CreatePreferenceRecordCommand
- CreateSegmentCommand
- CreateSegmentVersionCommand
- CreateStrategyTemplateCommand
- CreateStrategyTemplateVersionCommand
- CreateSubjectCommand
- CreateTerritoryAssignmentRuleCommand
- CreateTerritoryModelCommand
- CreateTerritoryNodeCommand
- CreateTerritoryResourceAssignmentCommand
- CreateTopicCommand
- CreateVisitFrequencyPolicyCommand
- DeactivateContactAvailabilityCommand
- DeactivateContactAvailabilityExceptionCommand
- DeactivateTerritoryModelCommand
- DeleteAccountCommand
- DeleteAccountContactLinkCommand
- DeleteAccountRelationshipCommand
- DeleteContactCommand
- DeleteVisitFrequencyPolicyCommand
- EndAccountTerritoryAssignmentCommand
- EndTerritoryResourceAssignmentCommand
- ImportAccountContactsCommand
- ImportAccountRelationshipsCommand
- ImportContactWorkbookCommand
- ImportContactsCommand
- LinkClaimCountryVersionEvidenceCommand
- LinkClaimEvidenceCommand
- LinkContactToAccountCommand
- LinkParentAccountCommand
- MarkTranslationAssessedCommand
- PreviewTerritoryAssignmentsCommand
- PublishContentEngagementJourneyCommand
- PublishKnowledgePathCommand
- RecordVisitOutcomeCommand
- RemoveClaimEvidenceCommand
- ReopenClaimCountryCommand
- ReplaceTerritoryResourceAssignmentCommand
- ReplanPlanningSessionCommand
- SetConceptChainTemplateConformanceResolutionsCommand
- SoftDeleteDraftTerritoryModelCommand
- SoftDeleteDraftTerritoryNodeCommand
- SoftDeleteTerritoryAssignmentRuleCommand
- SoftDeleteTerritoryResourceAssignmentCommand
- SubmitClaimCountryVersionReviewCommand
- SubmitClaimReviewCommand
- SubmitVisitReportCommand
- TerritoryImportFileCommand
- TransferTerritoryResourceAssignmentCommand
- UnarchiveAudienceProfileCommand
- UnarchiveSubjectCommand
- UnarchiveTopicCommand
- UnlinkParentAccountCommand
- UpdateAccountContactLinkCommand
- UpdateAccountRelationshipCommand
- UpdateAudienceProfileCommand
- UpdateCampaignCommand
- UpdateCampaignTargetCommand
- UpdateClaimCommand
- UpdateClaimCountryVersionCommand
- UpdateConceptChainTemplateCommand
- UpdateConceptNodeCommand
- UpdateConceptRelationshipCommand
- UpdateConceptTypeCommand
- UpdateConsentRecordCommand
- UpdateContactAvailabilityCommand
- UpdateContactAvailabilityExceptionCommand
- UpdateContactCommand
- UpdateContentEngagementJourneyCommand
- UpdateContentEngagementJourneyStageCommand
- UpdateCycleCapacityCommand
- UpdateCyclePeriodCommand
- UpdateEligibilityPolicyCommand
- UpdateKnowledgeContentCommand
- UpdateKnowledgePathCommand
- UpdateKnowledgePathStepCommand
- UpdatePlannedVisitCommand
- UpdatePlanningSessionSelectionCommand
- UpdatePreferenceRecordCommand
- UpdateSegmentCommand
- UpdateStrategyTemplateCommand
- UpdateSubjectCommand
- UpdateTargetCustomerCommand
- UpdateTerritoryAssignmentRuleCommand
- UpdateTerritoryModelCommand
- UpdateTerritoryNodeCommand
- UpdateTerritoryResourceAssignmentCommand
- UpdateTopicCommand
- UpdateVisitFrequencyPolicyCommand
- UpsertAccountAttributeCommand
- ValidateTerritoryResourceConflictsCommand
- WithdrawClaimCountryVersionReviewCommand
- WithdrawClaimReviewCommand
