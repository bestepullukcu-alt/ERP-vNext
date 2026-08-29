param([string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path)

$ErrorActionPreference = "Stop"
$checks = @(
    @{ File = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementMasterRegister/Services/DocumentLinkScopeCompatibilityValidator.cs"; Pattern = "CollectionInstanceMismatch" },
    @{ File = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementMasterRegister/Services/DocumentLinkScopeCompatibilityValidator.cs"; Pattern = "FolderScopeMismatch" },
    @{ File = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementMasterRegister/Services/DocumentMasterRegisterService.cs"; Pattern = "HasExplicitGrantAsync" },
    @{ File = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementMasterRegister/Services/DocumentMasterRegisterService.cs"; Pattern = "ControlledDocumentLinkReason" },
    @{ File = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementApproval/Services/DocumentApprovalService.cs"; Pattern = "DocumentLinkGovernanceGuard" },
    @{ File = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementTraining/Services/DocumentTrainingReadinessEvaluator.cs"; Pattern = "DocumentLinkGovernanceGuard" },
    @{ File = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementReleaseGates/Services/DocumentReleaseGateEvaluator.cs"; Pattern = "DocumentLinkGovernanceGuard" },
    @{ File = "frontend/Diten.Web/Views/DocumentManagement/MasterRegister/Details.cshtml"; Pattern = "master-register.registration.reconcile" },
    @{ File = "frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/MasterRegister/index.js"; Pattern = "reconciliationReason" },
    @{ File = "services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentManagement/Fu37CManualLinkScopeGuardrailTests.cs"; Pattern = "Cross_scope_is_blocked" }
)

$failed = @()
foreach ($check in $checks) {
    $path = Join-Path $RepoRoot $check.File
    if (!(Test-Path -LiteralPath $path) -or !(Select-String -LiteralPath $path -SimpleMatch $check.Pattern -Quiet)) {
        $failed += "$($check.File) :: $($check.Pattern)"
    }
}

if ($failed.Count -gt 0) {
    $failed | ForEach-Object { Write-Error "FAIL $_" }
    exit 1
}

Write-Host "PASS MOD-0029-FU37C manual-link scope guardrails ($($checks.Count) checks)"
