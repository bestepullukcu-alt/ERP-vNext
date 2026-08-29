<#
    MOD-0029-FU27 — Training Matrix & Effective Release Readiness verifier.

    Static contract + guardrail checks for the Training (FU11) detail tab. Read-only: it never edits, builds or
    calls a service. Run from the repository root:

        pwsh ./scripts/verify-mod0029-fu27-training-readiness-ui.ps1

    Companions: verify-mod0029-fu24-ui.ps1, verify-mod0029-fu25-detail-governance-ui.ps1,
                verify-mod0029-fu26-approval-release-gates-ui.ps1.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}

$script:Failures = @()
$script:Checks = 0

function Assert-True {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')
    $script:Checks++
    if ($Condition) { Write-Host ("  PASS  {0}" -f $Name) -ForegroundColor Green }
    else {
        Write-Host ("  FAIL  {0} {1}" -f $Name, $Detail) -ForegroundColor Red
        $script:Failures += $Name
    }
}

function Get-Text {
    param([string]$RelativePath)
    $full = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $full)) { return $null }
    return [System.IO.File]::ReadAllText($full)
}

function Remove-Comments {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $t = $Text
    $t = [regex]::Replace($t, '@\*[\s\S]*?\*@', ' ')
    $t = [regex]::Replace($t, '<!--[\s\S]*?-->', ' ')
    $t = [regex]::Replace($t, '/\*[\s\S]*?\*/', ' ')
    $t = [regex]::Replace($t, '(?m)^\s*//.*$', ' ')
    return $t
}

$web = 'frontend/Diten.Web'
$viewDir = "$web/Views/DocumentManagement/MasterRegister"
$jsDir = "$web/wwwroot/assets/js/DocumentManagement/MasterRegister"
$resxDir = "$web/Resources/Views/DocumentManagement/MasterRegister"
$cultures = @('ar', 'en', 'es', 'fr', 'ru', 'tr', 'zh')

Write-Host "`nMOD-0029-FU27 — Training readiness verifier" -ForegroundColor Cyan
Write-Host ("Repo root: {0}`n" -f $RepoRoot)

$details = Get-Text "$viewDir/Details.cshtml"
$js = Get-Text "$jsDir/details.js"
$controller = Get-Text "$web/Controllers/DocumentManagementMasterRegisterController.cs"
$l10n = Get-Text "$viewDir/_IndexL10n.cshtml"
$detailsCode = Remove-Comments $details
$jsCode = Remove-Comments $js
$controllerCode = Remove-Comments $controller

# 1 — real tab container
Write-Host 'Tab wiring' -ForegroundColor Cyan
Assert-True 'Training pane is a real container' ($detailsCode -match 'id="tab-training"')
Assert-True 'Training tab button present' ($detailsCode -match 'id="tabBtn-training"')
Assert-True 'Training removed from the deferred placeholder list' (-not ($detailsCode -match '"TabTraining"'))
Assert-True 'lazy loaded on shown.bs.tab' ($jsCode -match "tabBtn-training'\)\?\.addEventListener\('shown\.bs\.tab")
Assert-True 'manual reload button exists' ($detailsCode -match 'btnRefreshTraining')

# 2 — earlier FUs preserved
Write-Host "`nFU24 / FU25 / FU26 preserved" -ForegroundColor Cyan
Assert-True 'FU24 General tab intact' ($detailsCode -match 'id="tab-general"' -and $detailsCode -match 'detailIdentificationList')
Assert-True 'FU25 Identifiers tab intact' ($detailsCode -match 'id="tab-identifiers"' -and $detailsCode -match 'identifierLedgerBody')
Assert-True 'FU25 Lifecycle tab intact' ($detailsCode -match 'id="tab-lifecycle"' -and $detailsCode -match 'lifecycleHistoryBody')
Assert-True 'FU26 Approval tab intact' ($detailsCode -match 'id="tab-approval"' -and $detailsCode -match 'approvalRequirementsBody')
Assert-True 'FU26 Release Gates tab intact' ($detailsCode -match 'id="tab-gates"' -and $detailsCode -match 'gatesCardList')
# MOD-0029-FU29 turned Retention/Signatures/Quality Events from placeholders into real tabs. This former
# "still deferred" assertion is reconciled to the new reality: the three tabs are now real containers and no
# deferred placeholder remains.
Assert-True 'FU29 Retention tab is now real' ($detailsCode -match 'id="tab-retention"')
Assert-True 'FU29 Signatures tab is now real' ($detailsCode -match 'id="tab-signatures"')
Assert-True 'FU29 Quality Events tab is now real' ($detailsCode -match 'id="tab-quality"')
Assert-True 'no deferred placeholder message remains' (-not ($detailsCode -match 'DeferredSectionMessage'))

# 3 — proxy endpoints
Write-Host "`nMVC proxy endpoints" -ForegroundColor Cyan
foreach ($r in @('training/readiness', 'training/requirements', 'training/resolve', 'training/assignments')) {
    Assert-True "training proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
foreach ($r in @('complete', 'effectiveness', 'restrict')) {
    Assert-True "assignment action proxy route: $r" ($controllerCode -match ('training/assignments/\{assignmentId:guid\}/' + [regex]::Escape($r)))
}
Assert-True 'downstream training-matrix routes correct' ($controllerCode -match '/training-matrix/resolve' -and $controllerCode -match '/training-matrix/requirements')
Assert-True 'downstream training-readiness route correct' ($controllerCode -match '/training-readiness')
Assert-True 'downstream training-assignments route correct' ($controllerCode -match '/training-assignments')

$fu27Posts = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/training[^"]*"\)\]')
$fu27Guarded = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/training[^"]*"\)\]\s*\r?\n\s*\[ValidateAntiForgeryToken\]')
Assert-True 'FU27 POST proxy surface complete (5 mutations)' ($fu27Posts.Count -eq 5) ("found $($fu27Posts.Count)")
Assert-True 'every FU27 POST proxy has [ValidateAntiForgeryToken]' `
    ($fu27Posts.Count -gt 0 -and $fu27Guarded.Count -eq $fu27Posts.Count) ("guarded $($fu27Guarded.Count) of $($fu27Posts.Count)")
# The backend has no unrestrict endpoint — the UI must not pretend otherwise.
Assert-True 'no unrestrict proxy invented' (-not ($controllerCode -match 'unrestrict'))

# 4 — guardrails
Write-Host "`nGuardrails" -ForegroundColor Cyan
Assert-True 'JS uses same-origin MVC proxy only' ($jsCode -match '/DocumentManagement/MasterRegister/api')
Assert-True 'no direct Platform 5057 call' (-not ($jsCode -match '5057') -and -not ($detailsCode -match '5057'))
Assert-True 'no localhost URL' (-not ($jsCode -match 'http://localhost') -and -not ($detailsCode -match 'http://localhost'))
Assert-True 'no X-Tenant-Id in browser code' (-not ($jsCode -match 'X-Tenant-Id'))
Assert-True 'no tenant id field in the details view' (-not ($detailsCode -match '(?i)tenantid'))
Assert-True 'no DELETE verb from the UI' (-not ($jsCode -match "method:\s*'DELETE'"))
Assert-True 'no delete/purge proxy action' (-not ($controllerCode -match '(?i)\b(delete|purge)\b'))
Assert-True 'no file upload surface' (-not ($detailsCode -match '(?i)type="file"') -and -not ($jsCode -match '(?i)contentBase64'))
Assert-True 'anti-forgery token on every mutation' ($jsCode -match '__RequestVerificationToken')
Assert-True 'buttons locked during a request' ($jsCode -match 'button\.disabled = true')
Assert-True 'server messages HTML-escaped' ($jsCode -match 'replace\(/\[&<>"' + "'" + '\]/g')
Assert-True 'no bulk complete-all control' (-not ($jsCode -match '(?i)(completeAll|markAllComplete|bulkComplete)'))
Assert-True 'no auto complete / effective / approve / sign chaining' `
    (-not ($jsCode -match '(?i)(autoComplete|autoEffective|autoApprove|autoSign|autoRetire|forceEffective|autoPass)'))
# Training changes gate 5, but persisting a gate evaluation must stay an explicit user action.
Assert-True 'training mutation invalidates gates but does not auto-evaluate' `
    (($jsCode -match 'ReleaseGates\.invalidate\(\)') -and -not ($jsCode -match "afterMutation[\s\S]{0,400}?release-gates/evaluate"))
Assert-True 'effectiveness result is sent, never derived from a pass assumption' ($jsCode -match "effectivenessResult'\)\.value === 'true'")

# 5 — governance UX contracts
Write-Host "`nGovernance UX contracts" -ForegroundColor Cyan
Assert-True 'Release Gate 5 note rendered' ($detailsCode -match 'ReleaseGateTrainingNote')
Assert-True 'completion-does-not-mark-effective acknowledgement present' ($detailsCode -match 'CompletionDoesNotMarkEffective')
Assert-True 'acknowledgement checkbox is required' ($detailsCode -match 'id="completionAcknowledge"[\s\S]{0,120}?required')
Assert-True 'restriction-does-not-delete warning present' ($detailsCode -match 'RestrictionDoesNotDelete')
Assert-True 'assign-does-not-complete note present' ($detailsCode -match 'AssignDoesNotCompleteTraining')
Assert-True 'effectiveness-is-recorded-not-computed note present' ($detailsCode -match 'EffectivenessIsRecordedNotComputed')
Assert-True 'assignment-list-limitation disclosed' ($detailsCode -match 'AssignmentListNotAvailableNote')
Assert-True 'completion evidence field required' ($detailsCode -match 'id="trainingCompletionEvidence"[\s\S]{0,160}?required')
Assert-True 'effectiveness evidence field required' ($detailsCode -match 'id="effectivenessEvidence"[\s\S]{0,160}?required')
Assert-True 'restriction reason field required' ($detailsCode -match 'id="trainingRestrictionReason"[\s\S]{0,200}?required')
Assert-True 'requirements table present' ($detailsCode -match 'trainingRequirementsBody')
Assert-True 'assignments table present' ($detailsCode -match 'trainingAssignmentsBody')
Assert-True 'readiness panel present' ($detailsCode -match 'trainingReadinessList' -and $detailsCode -match 'trainingBlockingList')
Assert-True 'blocking and warning reasons both rendered' ($jsCode -match 'blockingReasons' -and $jsCode -match 'warningReasons')
Assert-True 'failed effectiveness stays visible' ($jsCode -match 'failedCount' -and $jsCode -match 'TrainingEffectivenessFailed')
Assert-True 'empty states handled' ($jsCode -match 'NoTrainingRequirementsFound' -and $jsCode -match 'NoTrainingAssignmentsFound')
Assert-True '401/403/409 handled distinctly' ($jsCode -match 'status === 401' -and $jsCode -match 'status === 403' -and $jsCode -match 'status === 409')
foreach ($code in @('ASSIGNMENT_NOT_FOUND', 'EVIDENCE_REQUIRED', 'REASON_REQUIRED', 'TRAINING_MATRIX_MISSING',
                    'TRAINING_ASSIGNMENT_MISSING', 'TRAINING_COMPLETION_REQUIRED', 'TRAINING_EFFECTIVENESS_REQUIRED',
                    'TRAINING_EFFECTIVENESS_FAILED', 'TRAINING_RESTRICTION_REQUIRED', 'INVALID_TRAINING_STATUS',
                    'NOT_FOUND_NON_LEAKAGE', 'PERMISSION_DENIED', 'VALIDATION_FAILED')) {
    Assert-True "reason code mapped: $code" ($jsCode -match [regex]::Escape($code))
}
Assert-True 'unknown reason codes fall through to the server message' ($jsCode -match 'if \(serverMessage\) return serverMessage;')

# 6 — permission gating on EXACT seeded keys
Write-Host "`nPermission gating (seeded keys only)" -ForegroundColor Cyan
$seededKeys = @(
    'platform.document-management.master-register.training.view',
    'platform.document-management.master-register.training.manage',
    'platform.document-management.master-register.training.verify'
)
foreach ($k in $seededKeys) { Assert-True "gated on seeded key: $k" ($details -match [regex]::Escape($k)) }

$seeder = Get-Text 'services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs'
foreach ($k in $seededKeys) {
    $parts = $k -split '\.'
    $action = $parts[-1]
    $module = ($parts[1..($parts.Count - 2)]) -join '.'
    Assert-True "key is actually seeded: $k" ($seeder -match ('"' + [regex]::Escape($module) + '",\s*"' + [regex]::Escape($action) + '"'))
}
# There is no training.assign / .complete / .effectiveness / .restrict key — inventing one would fail closed.
Assert-True 'no invented training action keys' `
    (-not ($details -match 'training\.(assign|complete|effectiveness|restrict)\b'))

# 7 — localization parity
Write-Host "`nLocalization (7-culture parity)" -ForegroundColor Cyan
$keySets = @{}
foreach ($c in $cultures) {
    $path = Join-Path $RepoRoot "$resxDir/MasterRegisterIndex.$c.resx"
    if (-not (Test-Path -LiteralPath $path)) { Assert-True "resx exists: $c" $false; continue }
    [xml]$xml = Get-Content -LiteralPath $path -Encoding UTF8 -Raw
    $keySets[$c] = @($xml.root.data | ForEach-Object { $_.name }) | Sort-Object
    Assert-True "resx exists: $c" $true
}
if ($keySets.Count -eq $cultures.Count) {
    $baseline = $keySets['en']
    Assert-True 'no duplicate keys in en resx' (($baseline | Group-Object | Where-Object { $_.Count -gt 1 }).Count -eq 0)
    foreach ($c in $cultures) {
        $diff = Compare-Object -ReferenceObject $baseline -DifferenceObject $keySets[$c]
        Assert-True ("resx key parity en vs {0} ({1} keys)" -f $c, $baseline.Count) ($null -eq $diff) ("delta: " + (($diff | ForEach-Object { $_.InputObject }) -join ','))
    }
    foreach ($k in @('TrainingTabTitle', 'ReleaseGateTrainingNote', 'CompletionDoesNotMarkEffective',
                     'RestrictionDoesNotDelete', 'AssignmentListNotAvailableNote', 'TrainingMatrixMissing')) {
        Assert-True "FU27 key present in en resx: $k" ($baseline -contains $k)
    }
    foreach ($k in @('DocumentMasterRegisterTitle', 'IdentifiersTabTitle', 'LifecycleTabTitle',
                     'ApprovalTabTitle', 'ReleaseGatesTabTitle', 'DeferredSectionMessage')) {
        Assert-True "FU24-FU26 key preserved: $k" ($baseline -contains $k)
    }
}
Assert-True 'FU27 keys exported to window.L10n' ($l10n -match 'TrainingTabTitle' -and $l10n -match 'ReleaseGateTrainingNote')

# 8 — out-of-scope surfaces
Write-Host "`nOut-of-scope surfaces" -ForegroundColor Cyan
if ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) {
    Push-Location $RepoRoot
    try {
        $leaked = @(git grep -l 'MOD-0029-FU27' -- 'gateway' 'services' 'frontend/Diten.Web/Views/CRM' 'frontend/Diten.Web/Views/HCM' 'frontend/Diten.Web/Controllers/CRM' 'frontend/Diten.Web/Controllers/HCM' 2>$null)
    }
    catch { $leaked = @() }
    finally { Pop-Location }
    Assert-True 'no FU27 change leaked into gateway / services / CRM / HCM' ($leaked.Count -eq 0) ("touched: " + ($leaked -join ', '))
}
else { Write-Host '  SKIP  git not available' -ForegroundColor Yellow }
Assert-True 'no external LMS/HCM integration introduced' (-not ($jsCode -match '(?i)(lms|/hcm/|hcmservice)'))

Write-Host ''
if ($script:Failures.Count -eq 0) {
    Write-Host ("VERDICT: PASS — {0}/{0} checks green" -f $script:Checks) -ForegroundColor Green
    exit 0
}
Write-Host ("VERDICT: FAIL — {0}/{1} checks failed" -f $script:Failures.Count, $script:Checks) -ForegroundColor Red
$script:Failures | ForEach-Object { Write-Host ("  - {0}" -f $_) -ForegroundColor Red }
exit 1
