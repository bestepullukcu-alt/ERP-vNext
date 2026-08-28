<#
    MOD-0029-FU26 — Approval Route & Release Gate Evidence Pack verifier.

    Static contract + guardrail checks for the Approval (FU09) and Release Gates (FU10) detail tabs. Read-only:
    it never edits, builds or calls a service. Run from the repository root:

        pwsh ./scripts/verify-mod0029-fu26-approval-release-gates-ui.ps1

    Companions: verify-mod0029-fu24-ui.ps1 (list/create/edit) and
                verify-mod0029-fu25-detail-governance-ui.ps1 (identifiers/lifecycle).
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

# Guardrail greps inspect executable code, not prose — doc comments legitimately name what guardrails forbid.
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

Write-Host "`nMOD-0029-FU26 — Approval & Release Gates verifier" -ForegroundColor Cyan
Write-Host ("Repo root: {0}`n" -f $RepoRoot)

$details = Get-Text "$viewDir/Details.cshtml"
$js = Get-Text "$jsDir/details.js"
$controller = Get-Text "$web/Controllers/DocumentManagementMasterRegisterController.cs"
$l10n = Get-Text "$viewDir/_IndexL10n.cshtml"
$detailsCode = Remove-Comments $details
$jsCode = Remove-Comments $js
$controllerCode = Remove-Comments $controller

# 1 — real tab containers
Write-Host 'Tab wiring' -ForegroundColor Cyan
Assert-True 'Approval pane is a real container' ($detailsCode -match 'id="tab-approval"')
Assert-True 'Release Gates pane is a real container' ($detailsCode -match 'id="tab-gates"')
Assert-True 'Approval tab button present' ($detailsCode -match 'id="tabBtn-approval"')
Assert-True 'Release Gates tab button present' ($detailsCode -match 'id="tabBtn-gates"')
Assert-True 'Approval/ReleaseGates removed from the deferred placeholder list' `
    (-not ($detailsCode -match '"TabApproval"') -and -not ($detailsCode -match '"TabReleaseGates"'))
Assert-True 'lazy loaded on shown.bs.tab' ($jsCode -match "tabBtn-approval'\)\?\.addEventListener\('shown\.bs\.tab" -and $jsCode -match "tabBtn-gates'\)\?\.addEventListener\('shown\.bs\.tab")
Assert-True 'manual reload buttons exist' ($detailsCode -match 'btnRefreshApproval' -and $detailsCode -match 'btnRefreshGates')

# 2 — earlier FUs preserved
Write-Host "`nFU24 / FU25 preserved" -ForegroundColor Cyan
Assert-True 'FU24 General tab intact' ($detailsCode -match 'id="tab-general"' -and $detailsCode -match 'detailIdentificationList')
Assert-True 'FU25 Identifiers tab intact' ($detailsCode -match 'id="tab-identifiers"' -and $detailsCode -match 'identifierLedgerBody')
Assert-True 'FU25 Lifecycle tab intact' ($detailsCode -match 'id="tab-lifecycle"' -and $detailsCode -match 'lifecycleHistoryBody')
# Training became a real tab in FU27 — its contract is owned by verify-mod0029-fu27-training-readiness-ui.ps1.
# MOD-0029-FU29 turned Retention/Signatures/Quality Events from placeholders into real tabs. This former
# "still deferred" assertion is reconciled to the new reality: the three tabs are now real containers and no
# deferred placeholder remains.
Assert-True 'FU29 Retention tab is now real' ($detailsCode -match 'id="tab-retention"')
Assert-True 'FU29 Signatures tab is now real' ($detailsCode -match 'id="tab-signatures"')
Assert-True 'FU29 Quality Events tab is now real' ($detailsCode -match 'id="tab-quality"')
Assert-True 'no deferred placeholder message remains' (-not ($detailsCode -match 'DeferredSectionMessage'))

# 3 — proxy endpoints
Write-Host "`nMVC proxy endpoints" -ForegroundColor Cyan
foreach ($r in @('approval/requirements', 'approval/readiness', 'approval/resolve', 'approval/evidence/record', 'approval/evidence/reject')) {
    Assert-True "approval proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
foreach ($r in @('release-gates', 'release-gates/readiness', 'release-gates/history', 'release-gates/evaluate')) {
    Assert-True "release gate proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
Assert-True 'gate evidence proxy route (gate key in path)' ($controllerCode -match 'release-gates/\{gateKey\}/evidence')

# Downstream must hit the REAL backend routes, which are not a single /approval tree.
Assert-True 'downstream approval-requirements route correct' ($controllerCode -match '/approval-requirements')
Assert-True 'downstream approval-readiness route correct' ($controllerCode -match '/approval-readiness')
Assert-True 'downstream approval-route/resolve route correct' ($controllerCode -match '/approval-route/resolve')
Assert-True 'downstream approval-evidence routes correct' ($controllerCode -match '/approval-evidence"' -and $controllerCode -match '/approval-evidence/reject')
Assert-True 'downstream release-readiness (non-persisting) used for tab open' ($controllerCode -match '/release-readiness')
Assert-True 'downstream release-gates/evaluate route correct' ($controllerCode -match '/release-gates/evaluate')

$fu26Posts = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(approval|release-gates)[^"]*"\)\]')
$fu26Guarded = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(approval|release-gates)[^"]*"\)\]\s*\r?\n\s*\[ValidateAntiForgeryToken\]')
Assert-True 'FU26 POST proxy surface complete (5 mutations)' ($fu26Posts.Count -eq 5) ("found $($fu26Posts.Count)")
Assert-True 'every FU26 POST proxy has [ValidateAntiForgeryToken]' `
    ($fu26Posts.Count -gt 0 -and $fu26Guarded.Count -eq $fu26Posts.Count) ("guarded $($fu26Guarded.Count) of $($fu26Posts.Count)")

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

# The whole point of a non-waivable gate: there must be no way to waive it from the UI. Rendering the backend's own
# `Waived` requirement status or the "no waiver" badge is not a waiver path, so those two are excluded by name —
# what must not exist is a waive/override/bypass ACTION or any write to the exception field.
$waiverCandidates = [regex]::Matches($jsCode, '(?i).{0,40}(waiv|override|bypass|forcePass|skipGate|exceptionPermitted).{0,40}') |
    ForEach-Object { $_.Value } |
    Where-Object {
        $_ -notmatch "Waived:\s*'" -and              # ApprovalRequirementStatus.Waived display map
        $_ -notmatch 'RequirementStatusWaived' -and  # its localization key
        # TrainingRequirementStatus.WaivedNotAllowed — a backend status meaning "a waiver was attempted and is
        # never honoured". Rendering it is the opposite of offering a waiver.
        $_ -notmatch 'WaivedNotAllowed' -and
        $_ -notmatch 'isNonWaivable' -and            # backend flag being read
        $_ -notmatch 'NoWaiverAllowed' -and          # the "no waiver" badge label
        $_ -notmatch 'NonWaivable'
    }
Assert-True 'no waiver/override/bypass control' ($waiverCandidates.Count -eq 0) `
    ("UI must never offer a waiver path — found: " + ($waiverCandidates -join ' | '))
Assert-True 'no auto approve / effective / sign / retire chaining' `
    (-not ($jsCode -match '(?i)(autoApprove|autoEffective|autoSign|autoRetire|forceEffective)'))
# `===` comparisons are reads; only an assignment would mean the UI is inventing a gate result.
Assert-True 'gate results are read from backend, never computed here' (-not ($jsCode -match 'gateResult\s*=(?![=>])'))
Assert-True 'evaluate does not chain into a lifecycle transition' `
    (-not ($jsCode -match "release-gates/evaluate[\s\S]{0,400}?lifecycle/(mark-effective|transition)"))

# 5 — governance UX contracts
Write-Host "`nGovernance UX contracts" -ForegroundColor Cyan
# The approval route is now auto-resolved as a side-effect of master-register create/update (deterministic projection
# of the entry's class/criticality/impact flags); there is no manual operator "Resolve Route" action or note.
Assert-True 'approval route auto-resolved (no manual resolve button)' (-not ($detailsCode -match 'id="btnResolveRoute"'))
Assert-True 'non-waivable explanation rendered' ($detailsCode -match 'NonWaivableExplanation' -and $detailsCode -match 'NoWaiverAllowed')
Assert-True 'non-waivable badge rendered per gate' ($jsCode -match 'isNonWaivable' -and $jsCode -match 'NoWaiverAllowed')
Assert-True 'gate evidence does not force pass note' ($detailsCode -match 'GateEvidenceDoesNotForcePass')
Assert-True 'evidence reference field exists (approval)' ($detailsCode -match 'id="approvalEvidenceReference"')
Assert-True 'evidence reference field exists + required (gate)' ($detailsCode -match 'id="gateEvidenceReference"[\s\S]{0,160}?required')
Assert-True 'rejection reason field exists' ($detailsCode -match 'id="approvalRejectionReason"')
Assert-True 'rejection reason enforced for reject mode' ($jsCode -match "approvalRejectionReason'\)\.required = isReject")
Assert-True 'author-sole-approver warning surfaced' ($jsCode -match 'AuthorSoleApproverBlocked')
Assert-True 'segregation failure warning surfaced' ($jsCode -match 'SegregationFailed' -and $detailsCode -match 'approvalSegregationAlert')
Assert-True 'missing mandatory evidence highlighted' ($jsCode -match 'MissingMandatoryEvidence' -and $jsCode -match 'missingMandatoryRoles')
Assert-True 'requirements table present' ($detailsCode -match 'approvalRequirementsBody')
Assert-True 'gate cards container present' ($detailsCode -match 'gatesCardList')
Assert-True 'gate evaluation history present' ($detailsCode -match 'gateHistoryBody')
foreach ($gate in @('MasterRegisterGate', 'ApprovedRepositoryGate', 'MandatoryApprovalEvidenceGate',
                    'RequiredExecutionMaterialsGate', 'TrainingReadinessGate', 'SupersededCopyWithdrawalGate')) {
    Assert-True "gate label wired: $gate" ($jsCode -match [regex]::Escape($gate))
}
foreach ($deferred in @('TrainingDetailsDeferred', 'RepositoryDetailsDeferred', 'ControlledCopyDetailsDeferred')) {
    Assert-True "deferred detail message wired: $deferred" ($jsCode -match [regex]::Escape($deferred))
}
Assert-True 'empty states handled' ($jsCode -match 'NoApprovalRequirementsFound' -and $jsCode -match 'NoGateEvaluationFound')
Assert-True '401/403/409 handled distinctly' ($jsCode -match 'status === 401' -and $jsCode -match 'status === 403' -and $jsCode -match 'status === 409')
foreach ($code in @('SEGREGATION_FAILED', 'REQUIREMENT_NOT_FOUND', 'WRONG_APPROVER_ROLE', 'INVALID_GATE_KEY',
                    'EVIDENCE_INCOMPLETE', 'APPROVAL_EVIDENCE_INCOMPLETE', 'RELEASE_GATE_INCOMPLETE',
                    'TRAINING_NOT_READY', 'REPOSITORY_NOT_APPROVED', 'REQUIRED_EXECUTION_MATERIALS_MISSING',
                    'SUPERSEDED_COPY_WITHDRAWAL_MISSING', 'MASTER_REGISTER_INACTIVE', 'UID_CODE_MISSING',
                    'EVIDENCE_REFERENCE_REQUIRED', 'GATE_EVIDENCE_REQUIRED', 'AUTHOR_SOLE_APPROVER_BLOCKED')) {
    Assert-True "reason code mapped: $code" ($jsCode -match [regex]::Escape($code))
}
Assert-True 'unknown reason codes fall through to the server message' ($jsCode -match 'if \(serverMessage\) return serverMessage;')

# 6 — permission gating on EXACT seeded keys
# NOTE: approval.manage is intentionally NOT in this UI-parity list. With the approval route now auto-resolved on
# master-register create/update, the manual "Resolve Route" button was removed, so the view no longer surfaces that
# permission. approval.manage still exists and still guards the backend /approval/resolve endpoint (retained for
# API/manual re-sync) — it is simply a backend-only permission now, not a UI-gated one.
Write-Host "`nPermission gating (seeded keys only)" -ForegroundColor Cyan
$seededKeys = @(
    'platform.document-management.master-register.approval.view',
    'platform.document-management.master-register.approval.evidence.record',
    'platform.document-management.master-register.release-gate.view',
    'platform.document-management.master-register.release-gate.evaluate',
    'platform.document-management.master-register.release-gate.evidence.record'
)
foreach ($k in $seededKeys) { Assert-True "gated on seeded key: $k" ($details -match [regex]::Escape($k)) }

$seeder = Get-Text 'services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs'
foreach ($k in $seededKeys) {
    $parts = $k -split '\.'
    $action = $parts[-1]
    $module = ($parts[1..($parts.Count - 2)]) -join '.'
    Assert-True "key is actually seeded: $k" ($seeder -match ('"' + [regex]::Escape($module) + '",\s*"' + [regex]::Escape($action) + '"'))
}
# The backend has no approval-routes.*/release-gates.* namespace and no separate reject key — inventing one fails closed.
Assert-True 'no invented approval-routes.* / release-gates.* keys' `
    (-not ($details -match 'document-management\.(approval-routes|release-gates)\.'))

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
    foreach ($k in @('ApprovalTabTitle', 'ReleaseGatesTabTitle', 'NonWaivableExplanation', 'AuthorSoleApproverBlocked',
                     'TrainingDetailsDeferred', 'RepositoryDetailsDeferred', 'ControlledCopyDetailsDeferred')) {
        Assert-True "FU26 key present in en resx: $k" ($baseline -contains $k)
    }
    foreach ($k in @('DocumentMasterRegisterTitle', 'IdentifiersTabTitle', 'LifecycleTabTitle', 'DeferredSectionMessage')) {
        Assert-True "FU24/FU25 key preserved: $k" ($baseline -contains $k)
    }
}
Assert-True 'FU26 keys exported to window.L10n' ($l10n -match 'ApprovalTabTitle' -and $l10n -match 'NonWaivableExplanation')

# 8 — out-of-scope surfaces
Write-Host "`nOut-of-scope surfaces" -ForegroundColor Cyan
if ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) {
    Push-Location $RepoRoot
    try {
        $leaked = @(git grep -l 'MOD-0029-FU26' -- 'gateway' 'services' 'frontend/Diten.Web/Views/CRM' 'frontend/Diten.Web/Views/HCM' 'frontend/Diten.Web/Controllers/CRM' 'frontend/Diten.Web/Controllers/HCM' 2>$null)
    }
    catch { $leaked = @() }
    finally { Pop-Location }
    Assert-True 'no FU26 change leaked into gateway / services / CRM / HCM' ($leaked.Count -eq 0) ("touched: " + ($leaked -join ', '))
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
