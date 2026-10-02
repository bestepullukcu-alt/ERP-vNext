# Denetim defteri — Diten.Platform

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.Platform/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| platform-merkezi | a | işaret | IAuditableCommand+IAuditMetadataProvider+!IAuditExcludedRequest+!ITransactionOwnedAuditCommand |
| platform-meta-denetim | a | yazıcı | IAuditMetaAuditWriter |
| gorev-etkinlik-akisi | c | yazıcı | TaskTransitionKind |
| is-akisi-gecis-gunlugu | c | yazıcı | IWorkflowTransitionLogRepository |
| platform-islem-ici | aday | işaret | ITransactionOwnedAuditCommand |
| arayuz-kayit-sink | aday | yazıcı | IInterfaceRegistryAuditSink |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

* **platform-merkezi** — `AuditBehavior` → `IAuditService` → `audit_outbox` → `audit_events`.
* **platform-meta-denetim** — Denetim modülünün kendi yazma/okuma işlemleri; `IsMetaAudit=true` ile merkezi günlüğe.
* **gorev-etkinlik-akisi** — `task_transitions`: kim (ActorUserId) · ne (Kind + alan değişiklikleri) · ne zaman; depo arayüzünde güncelleme/silme yok; görev etkinlik akışında okunur. CT onayı: 2026-10-02.
* **is-akisi-gecis-gunlugu** — `workflow_transition_logs`: aktör, eylem, önceki/sonraki durum, korelasyon; arayüzde güncelleme/silme yok; `…/instances/{id}/history` ile okunur. CT onayı: 2026-10-02.
* **platform-islem-ici** — `AuditBehavior` bu komutları atlar; kayıt handler'ın işlem içinde yazdığı `audit_outbox` satırıdır. Kod okumasıyla: çağıranların yükü eşleyicinin zorunlu alanlarını (TenantId, ActorType, Category, SourceService) taşımıyor → DeadLetter. **Dev veritabanında ölçüldü 2026-10-02 (salt-okunur):** `audit_outbox` 1468 satır = 1461 `Completed` (hepsinin anahtarı `audit:` — `AuditBehavior` yolu) + 7 `DeadLetter` (hepsi `global-applicability:` / `RegisterModuleManifestCommand`). İşlem içi yoldan teslim edilmiş TEK satır yok; `physical-entitlement:` ve `tenant-subscription:` anahtarlı satır hiç yok. `AddTenantModuleEntitlementCommand` (17), `AssignPlanToTenantCommand` (10), `ActivateTenantSubscriptionCommand` (1) teslimleri işaretin eklendiği commit'ten (d3098d729, 2026-08-31) ÖNCE, eski yoldan. Kanıt olmadığı için 25 komutun hepsi borçta (kural §10-K1).
* **arayuz-kayit-sink** — Kayıtlı uygulama `NullInterfaceRegistryAuditSink` (hiçbir şey yazmaz).

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

- ActivateModuleCatalogItemCommand
- ActivateModulePageDescriptorCommand
- ActivateSubscriptionPlanCommand
- ActivateTenantAdminUserCommand
- ActivateTenantSubscriptionCommand
- ActivateWorkingCalendarCommand
- AddAgendaItemCommand
- AddChecklistItemCommand
- AddMeetingAttendeesCommand
- AddTaskCommentCommand
- AddTaskDependencyCommand
- AddTaskPersonalNoteCommand
- AddTenantModuleEntitlementCommand
- ApplyGovernancePolicyPackCommand
- ApplyWorkingCalendarImportCommand
- ArchiveCollectionInstanceCommand
- ArchiveNotificationEventCommand
- ArchiveWorkingCalendarCommand
- ArchiveWorkingCalendarDayCommand
- AssignPlanToTenantCommand
- BulkDeleteModuleCatalogItemsCommand
- BulkDeleteTaskFieldDefinitionCommand
- BulkDeleteTaskItemCommand
- CancelMeetingCommand
- CancelTenantSubscriptionCommand
- ConfirmInterfaceDiffItemRequest
- ConfirmInterfaceDiscoveryBatchRequest
- CorrectPublishedMinutesCommand
- CreateChecklistTemplateCommand
- CreateControlledDocumentRegistrationCommand
- CreateEvidenceLinkCommand
- CreateMeetingCommand
- CreateMeetingSeriesCommand
- CreateMeetingTypeCommand
- CreateModuleCatalogItemCommand
- CreateModuleDomainCommand
- CreateModulePageActionDescriptorCommand
- CreateModulePageDescriptorCommand
- CreateModuleServiceCommand
- CreateSlaEscalationRuleCommand
- CreateSubscriptionPlanCommand
- CreateTaskFieldDefinitionCommand
- CreateTaskFromMeetingCommand
- CreateTaskItemFromTemplateCommand
- CreateTaskRecurrenceRuleCommand
- CreateTaskTemplateCommand
- CreateTaskTypeCommand
- CreateTenantSubscriptionCommand
- CreateWorkflowDefinitionCommand
- CreateWorkingCalendarCommand
- DeactivateModuleCatalogItemCommand
- DeactivateModulePageDescriptorCommand
- DeactivateSubscriptionPlanCommand
- DecideWorkingCalendarImportBatchCommand
- DecideWorkingCalendarImportCandidateCommand
- DeleteAgendaItemCommand
- DeleteChecklistTemplateCommand
- DeleteMeetingSeriesCommand
- DeleteMeetingTypeCommand
- DeleteModuleCatalogItemCommand
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
- DisableTenantModuleEntitlementCommand
- DiscardWorkingCalendarImportCommand
- DispatchNotificationByEventCodeCommand
- DryRunAccessProfileTemplatesCommand
- DryRunDocumentRegisterImportCommand
- DryRunFolderShareCommand
- DryRunInstantiationCommand
- DryRunQmsBaselineImportCommand
- EnableTenantModuleEntitlementCommand
- EnsureVerifiedGskuTenantAssignmentsCommand
- ExecuteInstantiationCommand
- ExpireTenantSubscriptionCommand
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
- ReactivateTenantSubscriptionCommand
- ReassignMeetingOrganizerCommand
- ReconciliationDryRunCommand
- RefreshTenantModuleEntitlementProjectionCommand
- RegisterModuleManifestCommand
- RejectInterfaceDiffItemRequest
- RejectInterfaceDiscoveryBatchRequest
- RemoveChecklistItemCommand
- RemoveEvidenceLinkCommand
- RemoveMeetingAttendeeCommand
- RemoveTaskDependencyCommand
- RemoveTenantManualModuleOverrideCommand
- RenewTenantSubscriptionCommand
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
- SeedDefaultSubscriptionPlansCommand
- SendDueSoonRemindersCommand
- SetChecklistItemStateCommand
- SetTaskPinnedCommand
- SetTaskSnoozeCommand
- SetTaskTypeActiveCommand
- StartWorkingCalendarImportCommand
- SuspendPlatformAdministratorCommand
- SuspendTenantCommand
- SuspendTenantSubscriptionCommand
- SyncNotificationEventsFromManifestCommand
- ToggleControlledDocumentFavoriteCommand
- UpdateAgendaItemCommand
- UpdateChecklistItemCommand
- UpdateChecklistTemplateCommand
- UpdateMeetingCommand
- UpdateMeetingSeriesCommand
- UpdateMeetingTypeCommand
- UpdateModuleCatalogItemCommand
- UpdateModuleDomainCommand
- UpdateModulePageActionDescriptorCommand
- UpdateModulePageDescriptorCommand
- UpdateModuleServiceCommand
- UpdateNotificationEventCommand
- UpdateSubscriptionPlanCommand
- UpdateTaskCommentCommand
- UpdateTaskFieldDefinitionCommand
- UpdateTaskRecurrenceRuleCommand
- UpdateTaskTemplateCommand
- UpdateTaskTypeCommand
- UpdateTenantBrandingCommand
- UpdateTenantModuleEntitlementExpiryCommand
- UpdateTenantSettingsCommand
- UpdateWorkingCalendarCommand
- UpsertWorkingCalendarDayCommand
- ValidateBusinessReferenceDataVersionCommand
- ValidateQmsBaselineDraftCommand
- WithdrawTaskCommentCommand
