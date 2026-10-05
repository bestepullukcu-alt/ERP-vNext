# Denetim defteri — Diten.PpmService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.PpmService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| ppm-denetim-niyeti | aday | yazıcı | IAuditIntentRepository.AddAsync |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

> **ppm-denetim-niyeti** — `ppm_audit_intents` (iş yazmasıyla aynı işlemde) — üretici ayarda kapalı, Platform'da tüketici yok, okuma ucu yok.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|
| CreateBenefitCommitmentCommand | ppm-denetim-niyeti | BenefitCommitmentService |
| CreateInitiativeCommand | ppm-denetim-niyeti | InitiativeService |
| CreateInitiativeSuccessorCommand | ppm-denetim-niyeti | InitiativeService |
| CreateInvestmentCaseCommand | ppm-denetim-niyeti | InvestmentCaseService |
| CreatePortfolioCommand | ppm-denetim-niyeti | PortfolioService |
| CreateProgramCommand | ppm-denetim-niyeti | ProgramService |
| CreateProjectCommand | ppm-denetim-niyeti | ProjectService |
| SoftDeleteBenefitCommitmentCommand | ppm-denetim-niyeti | BenefitCommitmentService |
| SoftDeleteInitiativeCommand | ppm-denetim-niyeti | InitiativeService |
| SoftDeleteInvestmentCaseCommand | ppm-denetim-niyeti | InvestmentCaseService |
| SoftDeletePortfolioCommand | ppm-denetim-niyeti | PortfolioService |
| SoftDeleteProgramCommand | ppm-denetim-niyeti | ProgramService |
| SoftDeleteProjectCommand | ppm-denetim-niyeti | ProjectService |
| TransitionBenefitCommitmentLifecycleCommand | ppm-denetim-niyeti | BenefitCommitmentService |
| TransitionInitiativeLifecycleCommand | ppm-denetim-niyeti | InitiativeService |
| TransitionInvestmentCaseLifecycleCommand | ppm-denetim-niyeti | InvestmentCaseService |
| TransitionPortfolioLifecycleCommand | ppm-denetim-niyeti | PortfolioService |
| TransitionProgramLifecycleCommand | ppm-denetim-niyeti | ProgramService |
| TransitionProjectLifecycleCommand | ppm-denetim-niyeti | ProjectService |
| UpdateBenefitCommitmentCommand | ppm-denetim-niyeti | BenefitCommitmentService |
| UpdateInitiativeCommand | ppm-denetim-niyeti | InitiativeService |
| UpdateInvestmentCaseCommand | ppm-denetim-niyeti | InvestmentCaseService |
| UpdatePortfolioCommand | ppm-denetim-niyeti | PortfolioService |
| UpdateProgramCommand | ppm-denetim-niyeti | ProgramService |
| UpdateProjectCommand | ppm-denetim-niyeti | ProjectService |

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- CreateBenefitCommitmentCommand
- CreateInitiativeCommand
- CreateInitiativeSuccessorCommand
- CreateInvestmentCaseCommand
- CreatePortfolioCommand
- CreateProgramCommand
- CreateProjectCommand
- GateIRelationshipMutationCommand
- SoftDeleteBenefitCommitmentCommand
- SoftDeleteInitiativeCommand
- SoftDeleteInvestmentCaseCommand
- SoftDeletePortfolioCommand
- SoftDeleteProgramCommand
- SoftDeleteProjectCommand
- TransitionBenefitCommitmentLifecycleCommand
- TransitionInitiativeLifecycleCommand
- TransitionInvestmentCaseLifecycleCommand
- TransitionPortfolioLifecycleCommand
- TransitionProgramLifecycleCommand
- TransitionProjectLifecycleCommand
- UpdateBenefitCommitmentCommand
- UpdateInitiativeCommand
- UpdateInvestmentCaseCommand
- UpdatePortfolioCommand
- UpdateProgramCommand
- UpdateProjectCommand
