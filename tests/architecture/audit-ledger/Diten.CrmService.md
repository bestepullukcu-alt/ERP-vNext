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

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

> **crm-contact-yayinci** — Varsayılan kayıt: yalnız uygulama günlüğü (`Logging…AuditPublisher`); `Crm:Audit:Mode=http` hiçbir ayar dosyasında yok.
> **crm-account-yayinci** — Aynı: varsayılan yalnız uygulama günlüğü.
> **crm-content-yayinci** — Aynı: varsayılan yalnız uygulama günlüğü.
> **crm-knowledge-yayinci** — Aynı: varsayılan yalnız uygulama günlüğü.
> **crm-territory-yayinci** — Yalnız uygulama günlüğü; HTTP uygulaması yok.

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
- CreateAccountCommand
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
- UpdateAccountCommand
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
