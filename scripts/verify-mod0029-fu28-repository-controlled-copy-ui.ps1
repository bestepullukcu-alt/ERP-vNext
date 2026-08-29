<#
    MOD-0029-FU28 — Repository Assessment & Controlled Copy verifier.

    Static contract + guardrail checks for the Repository (FU16) + Controlled Copy (FU17) detail tab. Read-only:
    it never edits, builds or calls a service. Run from the repository root:

        pwsh ./scripts/verify-mod0029-fu28-repository-controlled-copy-ui.ps1

    Companions: verify-mod0029-fu24-ui.ps1, -fu25-detail-governance-ui.ps1,
                -fu26-approval-release-gates-ui.ps1, -fu27-training-readiness-ui.ps1.
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

Write-Host "`nMOD-0029-FU28 — Repository & Controlled Copy verifier" -ForegroundColor Cyan
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
Assert-True 'Repository pane is a real container' ($detailsCode -match 'id="tab-repository"')
Assert-True 'Repository tab button present' ($detailsCode -match 'id="tabBtn-repository"')
Assert-True 'Repository removed from the deferred placeholder list' (-not ($detailsCode -match '"TabRepository"'))
Assert-True 'repository and copies rendered as inner tabs' ($detailsCode -match 'id="sub-repository"' -and $detailsCode -match 'id="sub-copies"')
Assert-True 'lazy loaded on shown.bs.tab' ($jsCode -match "tabBtn-repository'\)\?\.addEventListener\('shown\.bs\.tab")
Assert-True 'manual reload button exists' ($detailsCode -match 'btnRefreshRepository')

# 2 — earlier FUs preserved
Write-Host "`nFU24 - FU27 preserved" -ForegroundColor Cyan
Assert-True 'FU24 General tab intact' ($detailsCode -match 'id="tab-general"' -and $detailsCode -match 'detailIdentificationList')
Assert-True 'FU25 Identifiers tab intact' ($detailsCode -match 'id="tab-identifiers"' -and $detailsCode -match 'identifierLedgerBody')
Assert-True 'FU25 Lifecycle tab intact' ($detailsCode -match 'id="tab-lifecycle"' -and $detailsCode -match 'lifecycleHistoryBody')
Assert-True 'FU26 Approval tab intact' ($detailsCode -match 'id="tab-approval"' -and $detailsCode -match 'approvalRequirementsBody')
Assert-True 'FU26 Release Gates tab intact' ($detailsCode -match 'id="tab-gates"' -and $detailsCode -match 'gatesCardList')
Assert-True 'FU27 Training tab intact' ($detailsCode -match 'id="tab-training"' -and $detailsCode -match 'trainingRequirementsBody')
# MOD-0029-FU29 turned Retention/Signatures/Quality Events from placeholders into real tabs. This former
# "still deferred" assertion is reconciled to the new reality: the three tabs are now real containers and no
# deferred placeholder remains.
Assert-True 'FU29 Retention tab is now real' ($detailsCode -match 'id="tab-retention"')
Assert-True 'FU29 Signatures tab is now real' ($detailsCode -match 'id="tab-signatures"')
Assert-True 'FU29 Quality Events tab is now real' ($detailsCode -match 'id="tab-quality"')
Assert-True 'no deferred placeholder message remains' (-not ($detailsCode -match 'DeferredSectionMessage'))

# 3 — proxy endpoints
Write-Host "`nMVC proxy endpoints" -ForegroundColor Cyan
foreach ($r in @('repository/linked', 'repository/assessments', 'repository/link')) {
    Assert-True "repository proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
foreach ($r in @('evaluate', 'approve', 'reject')) {
    Assert-True "assessment action proxy route: $r" ($controllerCode -match ('repository/assessments/\{assessmentId:guid\}/' + [regex]::Escape($r)))
}
foreach ($r in @('controlled-copies', 'controlled-copies/readiness', 'controlled-copies/plans', 'controlled-copies/findings', 'controlled-copies/register')) {
    Assert-True "controlled copy proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
foreach ($r in @('withdraw', 'reconcile', 'mark-missing', 'mark-obsolete')) {
    Assert-True "copy action proxy route: $r" ($controllerCode -match ('controlled-copies/\{copyId:guid\}/' + [regex]::Escape($r)))
}
Assert-True 'plan complete proxy route' ($controllerCode -match 'controlled-copies/plans/\{planId:guid\}/complete')
Assert-True 'finding resolve proxy route' ($controllerCode -match 'controlled-copies/findings/\{findingId:guid\}/resolve')
Assert-True 'reconciliation evaluate proxy route' ($controllerCode -match 'controlled-copies/reconciliation/evaluate')

# Downstream must hit the REAL backend routes, which are not a single tree.
Assert-True 'downstream repository-assessments (tenant-global) route correct' ($controllerCode -match 'ApiRoot\}/repository-assessments')
Assert-True 'downstream per-entry repository-assessment link route correct' ($controllerCode -match '/repository-assessment/link')
Assert-True 'downstream copy-withdrawal-readiness route correct' ($controllerCode -match '/copy-withdrawal-readiness')
Assert-True 'downstream copy-withdrawal-plans route correct' ($controllerCode -match '/copy-withdrawal-plans')
Assert-True 'downstream obsolete-copy-findings route correct' ($controllerCode -match '/obsolete-copy-findings')
Assert-True 'downstream obsolete-copy-reconciliation route correct' ($controllerCode -match '/obsolete-copy-reconciliation/evaluate')

$fu28Posts = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(repository|controlled-copies)[^"]*"\)\]')
$fu28Guarded = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(repository|controlled-copies)[^"]*"\)\]\s*\r?\n\s*\[ValidateAntiForgeryToken\]')
# 4 repository (evaluate/approve/reject/link) + 9 copy (register/withdraw/reconcile/mark-missing/mark-obsolete/
# plan generate/plan complete/reconciliation evaluate/finding resolve).
Assert-True 'FU28 POST proxy surface complete (13 mutations)' ($fu28Posts.Count -eq 13) ("found $($fu28Posts.Count)")
Assert-True 'every FU28 POST proxy has [ValidateAntiForgeryToken]' `
    ($fu28Posts.Count -gt 0 -and $fu28Guarded.Count -eq $fu28Posts.Count) ("guarded $($fu28Guarded.Count) of $($fu28Posts.Count)")

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
Assert-True 'no "remove copy" wording (withdraw/reconcile only)' (-not ($detailsCode -match '(?i)>\s*(remove|destroy)\s+cop'))
Assert-True 'no auto withdraw / reconcile / approve / effective / sign chaining' `
    (-not ($jsCode -match '(?i)(autoWithdraw|autoReconcile|autoApprove|autoEffective|autoSign|forceApprove|forceEffective)'))
# Gate 2 and 6 change after these mutations, but persisting a gate evaluation stays an explicit user action.
Assert-True 'repository/copy mutation invalidates gates but does not auto-evaluate' `
    (($jsCode -match 'ReleaseGates\.invalidate\(\)') -and -not ($jsCode -match "afterMutation[\s\S]{0,300}?release-gates/evaluate"))
Assert-True 'boundary/gate support flags are read from backend, never assigned' `
    (-not ($jsCode -match 'canSupportReleaseGate\s*=(?![=>])') -and -not ($jsCode -match 'canSupportRegulatedESignature\s*=(?![=>])'))
Assert-True 'no external DMS/QMS integration introduced' (-not ($jsCode -match '(?i)(sharepoint|opentext|veeva|externaldms)'))

# 5 — compliance-claim guardrails (the point of FU16's boundary)
Write-Host "`nCompliance-claim guardrails" -ForegroundColor Cyan
Assert-True 'no-compliance-claim banner rendered' ($detailsCode -match 'NoComplianceClaim' -and $detailsCode -match 'NoValidatedDmsClaim')
Assert-True 'interim repository warning wired' ($jsCode -match 'InterimRepositoryWarning' -and $jsCode -match 'ApprovedInterimRepository')
Assert-True 'approve modal states it claims no validation' ($detailsCode -match 'repositoryDecisionNote' -and $jsCode -match 'ApproveDoesNotClaimValidation')
Assert-True 'link modal states it does not approve' ($detailsCode -match 'LinkDoesNotApproveRepository')
Assert-True 'boundary statement rendered verbatim from backend' ($jsCode -match 'boundary\.boundaryStatement')
# Positive-claim guardrail only. MOD-0029-FU29 later added a signature DISCLAIMER key (NoCertificateValidationClaim)
# to the same view; the pattern is narrowed to compliance-standard claims so it still catches a real assertion
# without flagging the negative disclaimer.
Assert-True 'no e-signature/certificate compliance assertion in the view' (-not ($detailsCode -match '(?i)(certificate valid|21 cfr|part 11|annex 11)'))

# 6 — copy governance UX contracts
Write-Host "`nControlled copy UX contracts" -ForegroundColor Cyan
Assert-True 'Gate 2 note rendered' ($detailsCode -match 'ReleaseGateRepositoryNote')
Assert-True 'Gate 6 note rendered' ($detailsCode -match 'ReleaseGateCopyNote')
Assert-True 'withdrawal-does-not-delete warning wired' ($jsCode -match 'WithdrawalDoesNotDelete')
Assert-True 'reconcile-does-not-delete warning wired' ($jsCode -match 'ReconcileDoesNotDelete')
Assert-True 'mark-missing/obsolete do-not-delete warnings wired' ($jsCode -match 'MarkMissingDoesNotDelete' -and $jsCode -match 'MarkObsoleteDoesNotDelete')
Assert-True 'resolve-finding-does-not-delete warning wired' ($jsCode -match 'ResolveFindingDoesNotDelete')
Assert-True 'issue-does-not-mark-effective warning rendered' ($detailsCode -match 'IssueDoesNotMarkEffective')
Assert-True 'withdrawal evidence required' ($jsCode -match "'withdraw':[^}]*evidenceRequired: true")
Assert-True 'reconciliation evidence required' ($jsCode -match "'reconcile':[^}]*evidenceRequired: true")
Assert-True 'repository rejection reason required' ($detailsCode -match 'id="repositoryRejectionReason"' -and $jsCode -match "repositoryRejectionReason'\)\.required = !isApprove")
Assert-True 'obsolete reason required' ($detailsCode -match 'id="copyActionReason"')
Assert-True 'assessments table present' ($detailsCode -match 'repositoryAssessmentsBody')
Assert-True 'repository findings table present' ($detailsCode -match 'repositoryFindingsBody')
Assert-True 'controlled copy log table present' ($detailsCode -match 'controlledCopiesBody')
Assert-True 'withdrawal plans table present' ($detailsCode -match 'withdrawalPlansBody')
Assert-True 'obsolete findings table present' ($detailsCode -match 'obsoleteFindingsBody')
Assert-True 'critical findings visually strong' ($jsCode -match 'table-danger' -and $jsCode -match "severity === 'Critical'")
Assert-True 'empty states handled' ($jsCode -match 'NoRepositoryAssessmentsFound' -and $jsCode -match 'NoControlledCopiesFound' -and $jsCode -match 'NoObsoleteFindingsFound' -and $jsCode -match 'NoWithdrawalPlansFound')
Assert-True '401/403/409 handled distinctly' ($jsCode -match 'status === 401' -and $jsCode -match 'status === 403' -and $jsCode -match 'status === 409')
foreach ($code in @('ASSESSMENT_NOT_FOUND', 'ALREADY_DECIDED', 'APPROVER_ROLE_INVALID', 'LINK_STATUS_INVALID',
                    'COPY_NOT_FOUND', 'PLAN_NOT_FOUND', 'FINDING_NOT_FOUND', 'DUPLICATE_COPY_NUMBER',
                    'HOLDER_OR_LOCATION_REQUIRED', 'PLAN_INCOMPLETE', 'DEVIATION_REQUIRED',
                    'REPOSITORY_ASSESSMENT_NOT_FOUND', 'REPOSITORY_BOUNDARY_BLOCKED', 'UNAPPROVED_REPOSITORY',
                    'VALIDATED_DMS_EVIDENCE_REQUIRED', 'INTERIM_REPOSITORY_LIMITATION', 'CONTROLLED_COPY_NOT_FOUND',
                    'CONTROLLED_COPY_WITHDRAWAL_REQUIRED', 'OBSOLETE_COPY_FINDING_OPEN', 'COPY_ALREADY_WITHDRAWN',
                    'COPY_RECONCILIATION_REQUIRED', 'NOT_FOUND_NON_LEAKAGE', 'PERMISSION_DENIED', 'VALIDATION_FAILED')) {
    Assert-True "reason code mapped: $code" ($jsCode -match [regex]::Escape($code))
}
Assert-True 'unknown reason codes fall through to the server message' ($jsCode -match 'if \(serverMessage\) return serverMessage;')

# 7 — permission gating on EXACT seeded keys
Write-Host "`nPermission gating (seeded keys only)" -ForegroundColor Cyan
$seededKeys = @(
    'platform.document-management.repository-assessment.view',
    'platform.document-management.repository-assessment.manage',
    'platform.document-management.repository-assessment.approve',
    'platform.document-management.master-register.controlled-copy.view',
    'platform.document-management.master-register.controlled-copy.manage',
    'platform.document-management.master-register.controlled-copy.reconcile'
)
foreach ($k in $seededKeys) { Assert-True "gated on seeded key: $k" ($details -match [regex]::Escape($k)) }

$seeder = Get-Text 'services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs'
foreach ($k in $seededKeys) {
    $parts = $k -split '\.'
    $action = $parts[-1]
    $module = ($parts[1..($parts.Count - 2)]) -join '.'
    Assert-True "key is actually seeded: $k" ($seeder -match ('"' + [regex]::Escape($module) + '",\s*"' + [regex]::Escape($action) + '"'))
}
# There is no repository-assessments.* / controlled-copies.* namespace and no separate issue/withdraw key.
Assert-True 'no invented repository/copy permission keys' `
    (-not ($details -match 'document-management\.(repository-assessments|controlled-copies)\.'))

# 8 — localization parity
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
    foreach ($k in @('RepositoryTabTitle', 'BoundaryStatement', 'InterimRepositoryWarning', 'NoValidatedDmsClaim',
                     'ReleaseGateRepositoryNote', 'ReleaseGateCopyNote', 'WithdrawalDoesNotDelete', 'ReconcileDoesNotDelete',
                     'IssueDoesNotMarkEffective', 'ObsoleteCopyFindings')) {
        Assert-True "FU28 key present in en resx: $k" ($baseline -contains $k)
    }
    foreach ($k in @('DocumentMasterRegisterTitle', 'IdentifiersTabTitle', 'LifecycleTabTitle', 'ApprovalTabTitle',
                     'ReleaseGatesTabTitle', 'TrainingTabTitle', 'DeferredSectionMessage')) {
        Assert-True "FU24-FU27 key preserved: $k" ($baseline -contains $k)
    }
}
Assert-True 'FU28 keys exported to window.L10n' ($l10n -match 'RepositoryTabTitle' -and $l10n -match 'ReleaseGateCopyNote')

# 9 — out-of-scope surfaces
Write-Host "`nOut-of-scope surfaces" -ForegroundColor Cyan
if ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) {
    Push-Location $RepoRoot
    try {
        $leaked = @(git grep -l 'MOD-0029-FU28' -- 'gateway' 'services' 'frontend/Diten.Web/Views/CRM' 'frontend/Diten.Web/Views/HCM' 'frontend/Diten.Web/Controllers/CRM' 'frontend/Diten.Web/Controllers/HCM' 2>$null)
    }
    catch { $leaked = @() }
    finally { Pop-Location }
    Assert-True 'no FU28 change leaked into gateway / services / CRM / HCM' ($leaked.Count -eq 0) ("touched: " + ($leaked -join ', '))
}
else { Write-Host '  SKIP  git not available' -ForegroundColor Yellow }

Write-Host ''
if ($script:Failures.Count -eq 0) {
    Write-Host ("VERDICT: PASS — {0}/{0} checks green" -f $script:Checks) -ForegroundColor Green
    exit 0
}
Write-Host ("VERDICT: FAIL — {0}/{1} checks failed" -f $script:Failures.Count, $script:Checks) -ForegroundColor Red
$script:Failures | ForEach-Object { Write-Host ("  - {0}" -f $_) -ForegroundColor Red }
exit 1
