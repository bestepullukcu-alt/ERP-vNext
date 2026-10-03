# Denetim defteri — Diten.Platform

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.Platform/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| platform-merkezi | a | işaret | IAuditableCommand+IAuditMetadataProvider+!IAuditExcludedRequest+!ITransactionOwnedAuditCommand |
| platform-meta-denetim | a | yazıcı | IAuditMetaAuditWriter.WriteAsync |
| gorev-etkinlik-akisi | c | yazıcı | TaskItem.Declare |
| is-akisi-gecis-gunlugu | c | yazıcı | IWorkflowTransitionLogRepository.CreateAsync/IWorkflowTransitionLogRepository.AppendAsync/WorkflowTaskTransitionSupport.TransitionAsync/WorkflowTaskTransitionSupport.DelegateAsync/WorkflowTaskTransitionSupport.RequestInfoAsync/WorkflowTaskTransitionSupport.CancelAsync |
| platform-islem-ici | a | yazıcı | PhysicalEntitlementAuditIntent.EnqueueAsync/TenantSubscriptionTransactionWriter.UpdateAsync/TenantSubscriptionCommandSupport.CreateAsync/IGlobalApplicabilityTransactionCoordinator.ExecuteAsync/IGlobalApplicabilityTransactionCoordinator.ExecuteBatchAsync |
| arayuz-kayit-sink | aday | yazıcı | IInterfaceRegistryAuditSink.EmitAsync |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

> **platform-merkezi** — `AuditBehavior` → `IAuditService` → `audit_outbox` → `audit_events`.
> **platform-meta-denetim** — Denetim modülünün kendi yazma/okuma işlemleri; `IsMetaAudit=true` ile merkezi günlüğe.
> **gorev-etkinlik-akisi** — Belirteç yazma üyesidir: handler `task.Declare(tür, aktör)` çağırır, depo aynı yazımda `task_transitions` satırını üretir. `task_transitions`: kim (ActorUserId) · ne (Kind + alan değişiklikleri) · ne zaman; depo arayüzünde güncelleme/silme yok; görev etkinlik akışında okunur. CT onayı: 2026-10-02.
> **is-akisi-gecis-gunlugu** — Belirteç yazma üyeleridir: günlüğe doğrudan `CreateAsync` / `AppendAsync`, ya da her geçişte günlüğe yazan `WorkflowTaskTransitionSupport` metotları. `workflow_transition_logs`: aktör, eylem, önceki/sonraki durum, korelasyon; arayüzde güncelleme/silme yok; `…/instances/{id}/history` ile okunur. CT onayı: 2026-10-02.
> **platform-islem-ici** — Kayıt, handler'ın iş verisiyle AYNI işlemde yazdığı `audit_outbox` satırıdır (K2: kayıt yoksa işlem yok). Belirteç bir ÇAĞRIDIR: handler işlem içi kapıya giden yazma üyelerinden birini çağırır — yetkilendirme `PhysicalEntitlementAuditIntent.EnqueueAsync`, abonelik `TenantSubscriptionTransactionWriter.UpdateAsync` / `TenantSubscriptionCommandSupport.CreateAsync`, modül kataloğu · abonelik planı · manifest kaydı `IGlobalApplicabilityTransactionCoordinator.ExecuteAsync` / `ExecuteBatchAsync`; üçü de tek girişten (`CanonicalTransactionalAuditOutboxWriter`) geçer. İşaret (`ITransactionOwnedAuditCommand`) yalnız komutu merkezi borudan çıkarır, not vermez: işareti taşıyıp bu üyelerden birini çağırmayan komut hiçbir ize gitmez ve test kırmızıdır. **Kanıt 2026-10-02 (WP-PLATFORM-AUDIT-INTX-01):** gerçek HTTP → gerçek işlem → `audit_outbox` → üretimdeki `AuditOutboxProcessor` → `audit_events`; üç çağıranın her biri için aktör + kategori + varlık + işlem + önceki/sonraki dolu tek satır, ikinci işleme çift satır üretmiyor, adı konamayan aktörde ret ve iş verisi değişmiyor (`TenantModulesScreenHttpMongoTests` K1_*, `TenantSubscriptionEndpointsHttpMongoTests`, `TransactionOwnedAuditDeliveryMongoTests`). Önceki durum (dev, salt-okunur, aynı gün): bu yoldan gelen 7 satırın 7'si `DeadLetter`, teslim edilmiş satır yok; sekiz abonelik komutu `AuditBehavior`'ın izin listesinde olmadığı için handler'a hiç ulaşmıyordu. 25 komut borçtan çıktı. Dev ve canlıdaki eski `DeadLetter` satırlarına dokunulmadı (ayrı karar).
> **arayuz-kayit-sink** — Kayıtlı uygulama `NullInterfaceRegistryAuditSink` (hiçbir şey yazmaz).

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|
| CreateSavedViewCommand | İ1 | Kullanıcının yalnız kendi liste görünümünü kaydeder; başka kimsenin verisini ya da yetkisini değiştirmez. |
| UpdateSavedViewCommand | İ1 | Kullanıcının yalnız kendi liste görünümünü günceller; başka kimsenin verisini ya da yetkisini değiştirmez. |
| DeleteSavedViewCommand | İ1 | Kullanıcının yalnız kendi liste görünümünü siler; başka kimsenin verisini ya da yetkisini değiştirmez. |
| MarkMyNotificationReadCommand | İ1 | Kullanıcının yalnız kendi bildiriminin okundu işaretini değiştirir; iş kaydı değişmez. |
| MarkAllMyNotificationsReadCommand | İ1 | Kullanıcının yalnız kendi bildirimlerinin okundu işaretini değiştirir; iş kaydı değişmez. |

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- ActivateModulePageDescriptorCommand
- ActivateTenantAdminUserCommand
- ActivateWorkingCalendarCommand
- AddAgendaItemCommand
- AddChecklistItemCommand
- AddMeetingAttendeesCommand
- AddTaskCommentCommand
- AddTaskDependencyCommand
- AddTaskPersonalNoteCommand
- ApplyGovernancePolicyPackCommand
- ApplyWorkingCalendarImportCommand
- ArchiveCollectionInstanceCommand
- ArchiveNotificationEventCommand
- ArchiveWorkingCalendarCommand
- ArchiveWorkingCalendarDayCommand
- BulkDeleteTaskFieldDefinitionCommand
- BulkDeleteTaskItemCommand
- CancelMeetingCommand
- ConfirmInterfaceDiffItemRequest
- ConfirmInterfaceDiscoveryBatchRequest
- CorrectPublishedMinutesCommand
- CreateChecklistTemplateCommand
- CreateControlledDocumentRegistrationCommand
- CreateEvidenceLinkCommand
- CreateMeetingCommand
- CreateMeetingSeriesCommand
- CreateMeetingTypeCommand
- CreateModuleDomainCommand
- CreateModulePageActionDescriptorCommand
- CreateModulePageDescriptorCommand
- CreateModuleServiceCommand
- CreateSlaEscalationRuleCommand
- CreateTaskFieldDefinitionCommand
- CreateTaskFromMeetingCommand
- CreateTaskItemFromTemplateCommand
- CreateTaskRecurrenceRuleCommand
- CreateTaskTemplateCommand
- CreateTaskTypeCommand
- CreateWorkflowDefinitionCommand
- CreateWorkingCalendarCommand
- DeactivateModulePageDescriptorCommand
- DecideWorkingCalendarImportBatchCommand
- DecideWorkingCalendarImportCandidateCommand
- DeleteAgendaItemCommand
- DeleteChecklistTemplateCommand
- DeleteMeetingSeriesCommand
- DeleteMeetingTypeCommand
- DeleteModuleDomainCommand
- DeleteModulePageActionDescriptorCommand
- DeleteModulePageDescriptorCommand
- DeleteModuleServiceCommand
- DeleteTaskFieldDefinitionCommand
- DeleteTaskItemCommand
- DeleteTaskPersonalNoteCommand
- DeleteTaskRecurrenceRuleCommand
- DeleteTaskTemplateCommand
- DeprecateInterfaceRequest
- DiscardWorkingCalendarImportCommand
- DispatchNotificationByEventCodeCommand
- DryRunAccessProfileTemplatesCommand
- DryRunDocumentRegisterImportCommand
- DryRunFolderShareCommand
- DryRunInstantiationCommand
- DryRunQmsBaselineImportCommand
- EnsureVerifiedGskuTenantAssignmentsCommand
- ExecuteInstantiationCommand
- GenerateDueMeetingSeriesCommand
- GenerateDueRecurringTasksCommand
- ImportInterfaceManifestRequest
- IngestDocumentMasterRegisterCommand
- InvitePlatformAdministratorCommand
- InviteTenantAdminUserCommand
- LinkExistingTaskCommand
- ProvisionCorporateCollectionInstanceCommand
- PublishMinutesCommand
- PublishWorkflowDefinitionCommand
- ReassignMeetingOrganizerCommand
- ReconciliationDryRunCommand
- RefreshTenantModuleEntitlementProjectionCommand
- RejectInterfaceDiffItemRequest
- RejectInterfaceDiscoveryBatchRequest
- RemoveChecklistItemCommand
- RemoveEvidenceLinkCommand
- RemoveMeetingAttendeeCommand
- RemoveTaskDependencyCommand
- ReorderAgendaCommand
- ReorderChecklistCommand
- ReplaceTenantNavPreferencesCommand
- ResendPlatformAdministratorInviteCommand
- RespondToInvitationCommand
- RestoreCollectionInstanceCommand
- RetryControlledDocumentRegistrationCommand
- RetryCorporateCollectionProvisioningCommand
- RetryInstantiationCommand
- RunAllGovernanceSweepsCommand
- RunDowntimeTemporaryIssueSweepCommand
- RunExternalDocumentSweepCommand
- RunLegalHoldScopeSweepCommand
- RunPeriodicReviewSweepCommand
- RunQualityCapaSweepCommand
- RunRetentionEligibilitySweepCommand
- RunSignatureRequestSweepCommand
- RunTemporaryInstructionSweepCommand
- SaveMinutesDraftCommand
- ScheduleFollowUpMeetingCommand
- ScheduleReviewMeetingForTaskCommand
- SendDueSoonRemindersCommand
- SetChecklistItemStateCommand
- SetTaskPinnedCommand
- SetTaskSnoozeCommand
- SetTaskTypeActiveCommand
- StartWorkingCalendarImportCommand
- SuspendPlatformAdministratorCommand
- SuspendTenantCommand
- SyncNotificationEventsFromManifestCommand
- ToggleControlledDocumentFavoriteCommand
- UpdateAgendaItemCommand
- UpdateChecklistItemCommand
- UpdateChecklistTemplateCommand
- UpdateMeetingCommand
- UpdateMeetingSeriesCommand
- UpdateMeetingTypeCommand
- UpdateModuleDomainCommand
- UpdateModulePageActionDescriptorCommand
- UpdateModulePageDescriptorCommand
- UpdateModuleServiceCommand
- UpdateNotificationEventCommand
- UpdateTaskCommentCommand
- UpdateTaskFieldDefinitionCommand
- UpdateTaskRecurrenceRuleCommand
- UpdateTaskTemplateCommand
- UpdateTaskTypeCommand
- UpdateTenantBrandingCommand
- UpdateTenantSettingsCommand
- UpdateWorkingCalendarCommand
- UpsertWorkingCalendarDayCommand
- ValidateBusinessReferenceDataVersionCommand
- ValidateQmsBaselineDraftCommand
- WithdrawTaskCommentCommand

## K2 borcu

Sahip kararı (2026-10-02, kural §4.3): kimlik · yetki · kiracı durumu · GxP kaydı · KVKK-özel veri değiştiren komutta kayıt
yazılamazsa işlem DURUR. Aşağıdaki komutlar denetleniyor ama bugün kayıt yazılamasa da tamamlanıyor (en iyi çaba).
Bu liste de **yalnız küçülür**: komut kapalı-başarısız yola geçince satırı çıkarılır.

Liste nasıl çıkarıldı (özellik klasörüne göre, komut tek tek okunmadı): `PlatformAdministrators`, `PlatformAccount` (kimlik) · `Tenants` (kiracı durumu) · `DocumentManagement*`, `DocumentRepository` (GxP). Hepsi yol `a`: `AuditBehavior` yazma hatasında devam eder. `TenantOrganization`, `SubscriptionFeatures`, `Quotas`, `BusinessReferenceData`, `TimeEntry`, `Tasks`, `Workflow`, `Notifications` K2 sınıfı SAYILMADI — sınır Control Tower'ın; genişletmek bu listeye satır eklemektir ve sabit sayıyı yükseltir.

- AcceptDeviationCommand
- ActivateGDocPCorrectionPolicyCommand
- ActivateLegalHoldCommand
- ActivateRetentionPolicyCommand
- ActivateSignaturePolicyCommand
- AddLegalHoldSubjectCommand
- AddManualTrainingRequirementCommand
- AllocateCodeCommand
- AllocateIdentifiersCommand
- AllocateUidCommand
- AllowTemporaryEnglishMasterCommand
- ApplyAccessProfileTemplatesCommand
- ApproveDispositionRequestCommand
- ApprovePeriodicReviewExtensionCommand
- ApproveQmsBaselineCommand
- ApproveRepositoryAssessmentCommand
- ApproveRetirementCommand
- ApproveSuspensionCommand
- ApproveTemporaryControlledIssueCommand
- ArchiveExternalDocumentCommand
- AssignPlatformAdministratorRolesCommand
- AssignTrainingCommand
- BridgeQualityEventFromExternalImpactCommand
- BridgeQualityEventFromGDocPCorrectionCommand
- BridgeQualityEventFromObsoleteCopyFindingCommand
- BridgeQualityEventFromSourceCommand
- BridgeQualityEventFromTemporaryIssueCommand
- BulkDeleteDocumentAccessPolicyCommand
- BulkDeletePlatformAdministratorsCommand
- BulkDeleteTemplateMasterCommand
- BulkDeleteTenantsCommand
- CancelCAPAActionCommand
- CancelDocumentDeviationCommand
- CancelDocumentQualityEventCommand
- CancelIdentifierCommand
- CancelSignatureRequestCommand
- CancelTemporaryControlledIssueCommand
- CloseCAPAActionCommand
- CloseDocumentDeviationCommand
- CloseDocumentQualityEventCommand
- CloseExternalDocumentInternalLinkCommand
- CloseRepositoryDowntimeEventCommand
- CloseSuspensionCaseCommand
- CloseTemporaryInstructionCommand
- CommitDocumentRegisterImportCommand
- CommitQmsBaselineImportCommand
- CompensateRepositoryObjectCommand
- CompleteCAPAActionCommand
- CompleteExternalDocumentImpactAssessmentCommand
- CompletePeriodicReviewCommand
- CompleteTrainingCommand
- CompleteWithdrawalPlanCommand
- CopyControlledDocumentCommand
- CopyTemplateCommand
- CreateControlledDocumentCommand
- CreateControlledDocumentVersionCommand
- CreateDispositionRequestCommand
- CreateDocumentAccessPolicyCommand
- CreateDocumentCAPAActionCommand
- CreateDocumentDeviationCommand
- CreateDocumentQualityEventCommand
- CreateExternalDocumentImpactAssessmentCommand
- CreateExternalDocumentRegisterEntryCommand
- CreateGDocPCorrectionPolicyCommand
- CreateLegalHoldCommand
- CreateManualQmsBaselineCommand
- CreateMasterRegisterEntryCommand
- CreateQmsBaselineDefinitionCommand
- CreateRepositoryAssessmentCommand
- CreateRetentionPolicyCommand
- CreateSignaturePolicyCommand
- CreateSignatureRequestCommand
- CreateTemplateDocumentCommand
- CreateTemplateMasterCommand
- CreateTemplateVariantCommand
- CreateTemplateVersionCommand
- CreateTenantAdminUserCommand
- DeleteControlledDocumentCommand
- DeleteDocumentAccessPolicyCommand
- DeletePlatformAdministratorCommand
- DeleteQmsBaselineDefinitionCommand
- DeleteTemplateMasterCommand
- DeleteTenantAdminUserCommand
- DeleteTenantCommand
- DeprecateTemplateMasterCommand
- EditControlledDocumentCommand
- EscalateSuspensionCaseCommand
- EvaluateDowntimeEscalationCommand
- EvaluateObsoleteCopyReconciliationCommand
- EvaluatePeriodicReviewOverdueCommand
- EvaluateReleaseGatesCommand
- EvaluateRepositoryAssessmentCommand
- EvaluateRetentionSubjectCommand
- EvaluateTemporaryInstructionExpiryCommand
- EvaluateTemporaryIssueOverdueCommand
- EvaluateVariantParentChangeCommand
- ExecuteDispositionMarkerCommand
- ExecuteFolderShareCommand
- ExecuteRetirementCommand
- ExecuteSuspensionCommand
- GenerateWithdrawalPlanCommand
- InitiatePeriodicReviewCommand
- InvalidateSignatureCommand
- IssueTemporaryControlledCopyCommand
- LinkControlledDocumentToRegisterEntryCommand
- LinkExternalDocumentToInternalRegisterEntryCommand
- LinkQualityEventSourceCommand
- LinkRepositoryAssessmentToRegisterEntryCommand
- MarkControlledCopyMissingCommand
- MarkControlledCopyObsoleteCommand
- MarkEffectiveQmsBaselineCommand
- MarkExternalDocumentSupersededCommand
- MarkPermissionsAppliedCommand
- MarkQaVerifiedCommand
- MarkRepositoryRestoredCommand
- MoveControlledDocumentCommand
- MoveQmsBaselineDefinitionCommand
- OpenDocumentDeviationCommand
- OpenDocumentQualityEventCommand
- OpenRepositoryDowntimeEventCommand
- OpenSuspensionCaseCommand
- PublishQmsBaselineCommand
- PublishTemplateMasterVersionCommand
- ReactivatePlatformAdministratorCommand
- ReactivateTenantCommand
- RebaseTemplateVariantCommand
- ReconcileControlledCopyCommand
- ReconcileTemporaryControlledIssueCommand
- ReconciliationApplyFindingsCommand
- RecordApprovalEvidenceCommand
- RecordBilingualReviewEvidenceCommand
- RecordCAPAEffectivenessCommand
- RecordDeviationInvestigationCommand
- RecordExternalDocumentMonitoringCheckCommand
- RecordGDocPCorrectionCommand
- RecordLocalApprovalEvidenceCommand
- RecordReleaseGateEvidenceCommand
- RecordTrainingEffectivenessCommand
- RegisterControlledCopyCommand
- RegisterTenantCommand
- RejectApprovalCommand
- RejectBilingualReviewCommand
- RejectDispositionRequestCommand
- RejectGDocPCorrectionCommand
- RejectLocalApprovalCommand
- RejectPeriodicReviewExtensionCommand
- RejectRepositoryAssessmentCommand
- RejectRetirementCommand
- RejectSignatureRequestCommand
- RejectSuspensionCommand
- ReleaseLegalHoldCommand
- RequestPeriodicReviewExtensionCommand
- RequestRetirementCommand
- RequestTemporaryControlledIssueCommand
- RequireBilingualReviewCommand
- RequireCAPAForDeviationCommand
- RequireLocalApprovalCommand
- ReserveIdentifierCommand
- ResolveApprovalRouteCommand
- ResolveDeviationCommand
- ResolveObsoleteCopyFindingCommand
- ResolveTrainingMatrixCommand
- RestrictTrainingCommand
- RetireGDocPCorrectionPolicyCommand
- RetireRetentionPolicyCommand
- RetireSignaturePolicyCommand
- ReviewGDocPCorrectionCommand
- RevokeTemporaryEnglishMasterCommand
- ShareControlledDocumentCommand
- ShareTemplateCommand
- SignDocumentSubjectCommand
- StartCAPAActionCommand
- StartTemporaryInstructionControlCommand
- StoreRepositoryObjectCommand
- SubmitDispositionRequestCommand
- TransitionDocumentLifecycleCommand
- UpdateDocumentAccessPolicyCommand
- UpdateExternalDocumentRegisterEntryCommand
- UpdateMasterRegisterMetadataCommand
- UpdatePlatformAccountSettingsCommand
- UpdatePlatformAdministratorCommand
- UpdateQmsBaselineDefinitionCommand
- UpdateRepositoryAssessmentCommand
- UpdateRetentionPolicyCommand
- UpdateTenantAdminUserCommand
- UpdateTenantCommand
- UpdateTenantLoginSettingsCommand
- UpsertFolderDocumentAccessCommand
- UpsertProvisioningEvidenceCommand
- UpsertVariantLocalizationProfileCommand
- VerifySignatureCommand
- WithdrawControlledCopyCommand

## Yazan sorgular

Sorgu olarak adlandırılmış ama işleyicisi depoya yazan istekler (ölçüm 2026-10-02). Sorgu kuralı bunları her komut listesinden
gizler ve `AuditBehavior` sorguyu denetlemez — yani bu yazmaların HİÇBİR kaydı yok. Bu liste **yalnız küçülür**: yazma bir komuta
taşınınca satır çıkarılır; yeni bir yazan sorgu kabul edilmez.

- GetTenantAdminUsersQuery
- GetTenantLoginSettingsQuery
- GetTenantUsersSummaryQuery
