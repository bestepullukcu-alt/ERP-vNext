# Denetim defteri — Diten.EnterpriseStrategyService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.EnterpriseStrategyService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| esbp-denetim-deposu | aday | yazıcı | IEnterpriseStrategyAuditSink.WriteAsync/EnterpriseStrategyAuditExtensions.WriteMutationAsync |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

> **esbp-denetim-deposu** — Kendi Mongo `AuditEvent` koleksiyonu; kayıtta KİRACI KİMLİĞİ ve sonuç yok; okuma ucu yalnız Project için.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|
| ArchiveGoalCommand | esbp-denetim-deposu | IGoalService |
| ArchiveObjectiveCommand | esbp-denetim-deposu | IObjectiveService |
| ChangeConnectionStatusCommand | esbp-denetim-deposu | IConnectionService |
| ChangeGoalStatusCommand | esbp-denetim-deposu | IGoalService |
| ChangeInitiativeStrategyLinkStatusCommand | esbp-denetim-deposu | IInitiativeOrchestrationService |
| ChangeObjectiveStatusCommand | esbp-denetim-deposu | IObjectiveService |
| CreateConnectionCommand | esbp-denetim-deposu | IConnectionService |
| CreateGoalCommand | esbp-denetim-deposu | IGoalService |
| CreateInitiativeCommand | esbp-denetim-deposu | IInitiativeOrchestrationService |
| CreateObjectiveCommand | esbp-denetim-deposu | IObjectiveService |
| DeleteConnectionCommand | esbp-denetim-deposu | IConnectionService |
| DeleteInitiativeStrategyLinkCommand | esbp-denetim-deposu | IInitiativeOrchestrationService |
| RestoreGoalCommand | esbp-denetim-deposu | IGoalService |
| RestoreObjectiveCommand | esbp-denetim-deposu | IObjectiveService |
| SyncInitiativesCommand | esbp-denetim-deposu | IInitiativeOrchestrationService |
| UpdateConnectionCommand | esbp-denetim-deposu | IConnectionService |
| UpdateGoalCommand | esbp-denetim-deposu | IGoalService |
| UpdateInitiativeCommand | esbp-denetim-deposu | IInitiativeOrchestrationService |
| UpdateObjectiveCommand | esbp-denetim-deposu | IObjectiveService |
| UpsertInitiativeStrategyLinkCommand | esbp-denetim-deposu | IInitiativeOrchestrationService |

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- ArchiveGoalCommand
- ArchiveObjectiveCommand
- ChangeConnectionStatusCommand
- ChangeGoalStatusCommand
- ChangeInitiativeStrategyLinkStatusCommand
- ChangeObjectiveStatusCommand
- ChangePlanningCycleStatusCommand
- ChangeProjectStrategyLinkStatusCommand
- ChangeStrategyPeriodStatusCommand
- CreateConnectionCommand
- CreateDemandIdeaDraftCommand
- CreateGoalCommand
- CreateInitiativeCommand
- CreateObjectiveCommand
- CreatePlanningCycleCommand
- CreateProjectCommand
- CreateStrategyLinkedProjectCommand
- CreateStrategyPeriodCommand
- DeleteConnectionCommand
- DeleteInitiativeStrategyLinkCommand
- DeleteProjectStrategyLinkCommand
- RestoreGoalCommand
- RestoreObjectiveCommand
- SubmitDemandIdeaCommand
- SyncInitiativesCommand
- SyncProjectsCommand
- UpdateConnectionCommand
- UpdateDemandIdeaCommand
- UpdateGoalCommand
- UpdateInitiativeCommand
- UpdateObjectiveCommand
- UpdatePlanningCycleCommand
- UpdateProjectCommand
- UpdateStrategyPeriodCommand
- UpsertInitiativeStrategyLinkCommand
- UpsertProjectStrategyLinkCommand
