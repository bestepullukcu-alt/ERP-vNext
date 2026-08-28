<#
    MOD-0029-FU25 — Master Register Detail Governance Pack verifier (Identifiers + Lifecycle tabs).

    Static contract + guardrail checks. Read-only: it never edits, builds or calls a service. Run from the repo root:

        pwsh ./scripts/verify-mod0029-fu25-detail-governance-ui.ps1

    Companion to scripts/verify-mod0029-fu24-ui.ps1, which still owns the FU24 list/create/edit contract.
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

# Guardrail greps must inspect executable code, not prose — doc comments legitimately name the things guardrails forbid.
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

Write-Host "`nMOD-0029-FU25 — Detail Governance Pack verifier" -ForegroundColor Cyan
Write-Host ("Repo root: {0}`n" -f $RepoRoot)

Write-Host 'Files' -ForegroundColor Cyan
foreach ($f in @("$viewDir/Details.cshtml", "$jsDir/details.js", "$web/Controllers/DocumentManagementMasterRegisterController.cs")) {
    Assert-True "exists: $f" (Test-Path -LiteralPath (Join-Path $RepoRoot $f))
}

$details = Get-Text "$viewDir/Details.cshtml"
$js = Get-Text "$jsDir/details.js"
$indexJs = Get-Text "$jsDir/index.js"
$controller = Get-Text "$web/Controllers/DocumentManagementMasterRegisterController.cs"
$l10n = Get-Text "$viewDir/_IndexL10n.cshtml"
$detailsCode = Remove-Comments $details
$jsCode = Remove-Comments $js
$controllerCode = Remove-Comments $controller

# 1 — real tab containers replace the FU24 placeholders
Write-Host "`nDetails tab wiring" -ForegroundColor Cyan
Assert-True 'Identifiers pane is a real container' ($detailsCode -match 'id="tab-identifiers"')
Assert-True 'Lifecycle pane is a real container' ($detailsCode -match 'id="tab-lifecycle"')
Assert-True 'Identifiers tab button present for lazy load' ($detailsCode -match 'id="tabBtn-identifiers"')
Assert-True 'Lifecycle tab button present for lazy load' ($detailsCode -match 'id="tabBtn-lifecycle"')
Assert-True 'Identifiers/Lifecycle removed from the deferred placeholder list' `
    (-not ($detailsCode -match '"TabIdentifiers"') -and -not ($detailsCode -match '"TabLifecycle"'))
Assert-True 'General tab preserved' ($detailsCode -match 'id="tab-general"' -and $detailsCode -match 'detailIdentificationList')
Assert-True 'lazy load bound to shown.bs.tab (no eager fetch)' ($jsCode -match "shown\.bs\.tab")
Assert-True 'ensureLoaded guards the single initial fetch' ($jsCode -match 'ensureLoaded')
Assert-True 'reload buttons exist for both tabs' (
    ($detailsCode -match 'btnReloadIdentifiers' -or $jsCode -match 'btnReloadIdentifiers') -and
    ($detailsCode -match 'btnReloadLifecycle' -or $jsCode -match 'btnReloadLifecycle')
)

# 2 — remaining placeholders preserved
#
# Approval and Release Gates were placeholders in FU25 and became real tabs in FU26 — their contract is now owned by
# verify-mod0029-fu26-approval-release-gates-ui.ps1. This script asserts only the tabs still deferred after FU26.
Write-Host "`nDeferred tabs preserved" -ForegroundColor Cyan
# MOD-0029-FU29 turned Retention/Signatures/Quality Events from placeholders into real tabs. This former
# "still deferred" assertion is reconciled to the new reality: the three tabs are now real containers and no
# deferred placeholder remains.
Assert-True 'FU29 Retention tab is now real' ($detailsCode -match 'id="tab-retention"')
Assert-True 'FU29 Signatures tab is now real' ($detailsCode -match 'id="tab-signatures"')
Assert-True 'FU29 Quality Events tab is now real' ($detailsCode -match 'id="tab-quality"')
Assert-True 'no deferred placeholder message remains' (-not ($detailsCode -match 'DeferredSectionMessage'))
# MOD-0029-FU29 wired real, lazy-loaded API calls for these three tabs; they are no longer deferred.
Assert-True 'FU29 retention/signatures/quality tabs now call the proxy' `
    ($jsCode -match '/retention/subject' -and $jsCode -match '/signatures/records' -and $jsCode -match '/quality-events')

# 3 — proxy endpoints
Write-Host "`nMVC proxy endpoints" -ForegroundColor Cyan
$identifierRoutes = @('identifiers', 'identifiers/ledger', 'identifiers/allocate-uid', 'identifiers/allocate-code', 'identifiers/allocate-both', 'identifiers/reserve', 'identifiers/cancel')
foreach ($r in $identifierRoutes) {
    Assert-True "identifier proxy route: $r" ($controllerCode -match ([regex]::Escape("/identifiers/$($r -replace '^identifiers/?','')") -replace '//', '/') -or $controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
foreach ($r in @('lifecycle/state', 'lifecycle/history', 'lifecycle/transition', 'lifecycle/mark-effective', 'lifecycle/supersede', 'lifecycle/retire')) {
    Assert-True "lifecycle proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
Assert-True 'downstream identifier list route is correct' ($controllerCode -match 'document-identifiers\?registerEntryId=')
Assert-True 'downstream allocate routes are correct' `
    ($controllerCode -match 'allocate-uid' -and $controllerCode -match 'allocate-code' -and $controllerCode -match 'allocate-identifiers')
Assert-True 'downstream lifecycle state/history routes are correct' `
    ($controllerCode -match '/lifecycle"' -and $controllerCode -match '/lifecycle/transitions')
Assert-True 'mark-effective/supersede/retire forward to the generic transition endpoint' `
    (([regex]::Matches($controllerCode, '/lifecycle/transition"')).Count -ge 4)
Assert-True 'target status pinned server-side' ($controllerCode -match 'WithTargetStatus')
Assert-True 'client-supplied target status is stripped on pinned routes' ($controllerCode -match 'payload\.Remove\(key\)')
Assert-True 'reserve pins registerEntryId server-side' ($controllerCode -match 'payload\["registerEntryId"\]')

# Scoped to FU25's own resources — later FUs add their own POST proxies to the same controller and own their counts.
$fu25Posts = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(identifiers|lifecycle)[^"]*"\)\]')
$fu25Guarded = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(identifiers|lifecycle)[^"]*"\)\]\s*\r?\n\s*\[ValidateAntiForgeryToken\]')
Assert-True 'FU25 POST proxy surface is complete (9 mutations)' ($fu25Posts.Count -eq 9) ("found $($fu25Posts.Count)")
Assert-True 'every FU25 POST proxy has [ValidateAntiForgeryToken]' `
    ($fu25Posts.Count -gt 0 -and $fu25Guarded.Count -eq $fu25Posts.Count) ("guarded $($fu25Guarded.Count) of $($fu25Posts.Count)")

# 4 — guardrails
Write-Host "`nGuardrails" -ForegroundColor Cyan
Assert-True 'JS uses same-origin MVC proxy only' ($jsCode -match '/DocumentManagement/MasterRegister/api')
Assert-True 'no direct Platform 5057 call' (-not ($jsCode -match '5057') -and -not ($detailsCode -match '5057'))
Assert-True 'no localhost URL' (-not ($jsCode -match 'http://localhost') -and -not ($detailsCode -match 'http://localhost'))
Assert-True 'no X-Tenant-Id in browser code' (-not ($jsCode -match 'X-Tenant-Id'))
Assert-True 'no tenant id field in the details view' (-not ($detailsCode -match '(?i)tenantid'))
Assert-True 'no DELETE verb from the UI' (-not ($jsCode -match "method:\s*'DELETE'"))
Assert-True 'no delete/purge proxy action' (-not ($controllerCode -match '(?i)\b(delete|purge)\b'))
Assert-True 'no hard-delete wording in the details view' (-not ($detailsCode -match '(?i)>\s*(delete|purge)\b'))
Assert-True 'no file upload surface' (-not ($detailsCode -match '(?i)type="file"') -and -not ($jsCode -match '(?i)contentBase64'))
Assert-True 'anti-forgery token sent on every mutation' ($jsCode -match '__RequestVerificationToken')
Assert-True 'buttons locked during a request' ($jsCode -match 'button\.disabled = true')
Assert-True 'server messages HTML-escaped' ($jsCode -match 'replace\(/\[&<>"' + "'" + '\]/g')

# No client-side auto-progression: the UI must not chain a transition after a refusal or fabricate a gate result.
Assert-True 'no auto approve/effective/sign/retire chaining' `
    (-not ($jsCode -match '(?i)(autoApprove|autoEffective|autoSign|autoRetire|forceEffective|bypassGate|skipGate)'))
Assert-True 'no retry-on-refusal loop' (-not ($jsCode -match '(?i)retry.*(403|409)'))
Assert-True 'allowed transitions derived from backend Can\* flags' ($jsCode -match "flag: 'canMarkEffective'")

# 5 — required-reason + warning contracts
Write-Host "`nGovernance UX contracts" -ForegroundColor Cyan
Assert-True 'cancel-allocation reason is required' (
    $detailsCode -match 'id="cancelAllocationReason"[\s\S]{0,200}?required' -or
    ($jsCode -match 'showInput:\s*true' -and
     $jsCode -match 'inputRequired:\s*true' -and
     $jsCode -match 'inputValidator:')
)
Assert-True 'transition reason field is required by default' ($detailsCode -match 'id="lifecycleReason"[\s\S]{0,200}?required')
Assert-True 'reason requirement mirrors the backend stop/end states' ($jsCode -match "REASON_MANDATORY = \['Suspended', 'Retired', 'Superseded'\]")
Assert-True 'Mark Effective warning shown' ($detailsCode -match 'MarkEffectiveWarning' -or $jsCode -match 'MarkEffectiveWarning')
Assert-True 'backend-guardrail note rendered' ($detailsCode -match 'BackendEnforcesReleaseGates')
Assert-True 'cancel-allocation is explained as not-a-delete' (
    $detailsCode -match 'ConfirmCancelAllocation' -or
    $jsCode -match "subtext:\s*t\('ConfirmCancelAllocation'\)"
)
Assert-True 'identifier never-reused note rendered' ($detailsCode -match 'IdentifierNeverReusedNote')
Assert-True 'ledger table present' ($detailsCode -match 'id="identifierLedgerTable"' -and $detailsCode -match 'identifierLedgerBody')
Assert-True 'lifecycle history table present' ($detailsCode -match 'id="lifecycleHistoryTable"' -and $detailsCode -match 'lifecycleHistoryBody')
Assert-True 'empty-state handled for ledger and history' ($jsCode -match 'NoIdentifierLedgerFound' -and $jsCode -match 'NoLifecycleHistoryFound')
Assert-True 'reason codes mapped to localized messages' ($jsCode -match 'REASON_CODE_KEYS')
foreach ($code in @('INVALID_TRANSITION', 'RELEASE_GATE_INCOMPLETE', 'APPROVAL_EVIDENCE_INCOMPLETE', 'MISSING_IDENTIFIER', 'DUPLICATE_IDENTIFIER', 'STALE_VERSION')) {
    Assert-True "reason code handled: $code" ($jsCode -match [regex]::Escape($code))
}
Assert-True '401/403/409 handled distinctly' ($jsCode -match 'status === 401' -and $jsCode -match 'status === 403' -and $jsCode -match 'status === 409')

# 6 — permission gating uses the EXACT seeded keys
Write-Host "`nPermission gating (seeded keys only)" -ForegroundColor Cyan
$seededKeys = @(
    'platform.document-management.identifiers.view',
    'platform.document-management.identifiers.allocate',
    'platform.document-management.identifiers.reserve',
    'platform.document-management.identifiers.cancel',
    'platform.document-management.master-register.lifecycle.view',
    'platform.document-management.master-register.lifecycle.manage'
)
foreach ($k in $seededKeys) {
    Assert-True "gated on seeded key: $k" ($details -match [regex]::Escape($k))
}
$seeder = Get-Text 'services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs'
foreach ($k in $seededKeys) {
    $parts = $k -split '\.'
    $action = $parts[-1]
    $module = ($parts[1..($parts.Count - 2)]) -join '.'
    Assert-True "key is actually seeded: $k" ($seeder -match ('"' + [regex]::Escape($module) + '",\s*"' + [regex]::Escape($action) + '"'))
}
# The backend has no dedicated mark-effective/supersede/retire key — inventing one would fail closed at runtime.
Assert-True 'no invented lifecycle action permission keys' `
    (-not ($details -match 'lifecycle\.(mark-effective|supersede|retire|transition)'))

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
    foreach ($c in $cultures) {
        $diff = Compare-Object -ReferenceObject $baseline -DifferenceObject $keySets[$c]
        Assert-True ("resx key parity en vs {0} ({1} keys)" -f $c, $baseline.Count) ($null -eq $diff) ("delta: " + (($diff | ForEach-Object { $_.InputObject }) -join ','))
    }
    foreach ($k in @('IdentifiersTabTitle', 'LifecycleTabTitle', 'IdentifierLedger', 'LifecycleHistory', 'MarkEffectiveWarning', 'BackendEnforcesReleaseGates', 'ConfirmCancelAllocation', 'InvalidTransition')) {
        Assert-True "FU25 key present in en resx: $k" ($baseline -contains $k)
    }
    foreach ($k in @('DocumentMasterRegisterTitle', 'TabGeneral', 'DeferredSectionMessage')) {
        Assert-True "FU24 key preserved: $k" ($baseline -contains $k)
    }
}
Assert-True 'FU25 keys exported to window.L10n' ($l10n -match 'IdentifiersTabTitle' -and $l10n -match 'LifecycleTransitionSucceeded')

# 8 — out-of-scope surfaces
Write-Host "`nOut-of-scope surfaces" -ForegroundColor Cyan
if ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) {
    Push-Location $RepoRoot
    try {
        $leaked = @(git grep -l 'MOD-0029-FU25' -- 'gateway' 'services' 'frontend/Diten.Web/Views/CRM' 'frontend/Diten.Web/Views/HCM' 'frontend/Diten.Web/Controllers/CRM' 'frontend/Diten.Web/Controllers/HCM' 2>$null)
    }
    catch { $leaked = @() }
    finally { Pop-Location }
    Assert-True 'no FU25 change leaked into gateway / services / CRM / HCM' ($leaked.Count -eq 0) ("touched: " + ($leaked -join ', '))
}
else {
    Write-Host '  SKIP  git not available' -ForegroundColor Yellow
}
Assert-True 'FU24 index.js untouched by FU25 scope creep' ($indexJs -match 'MOD-0029-FU24' -and -not ($indexJs -match 'lifecycle/transition'))

Write-Host ''
if ($script:Failures.Count -eq 0) {
    Write-Host ("VERDICT: PASS — {0}/{0} checks green" -f $script:Checks) -ForegroundColor Green
    exit 0
}
Write-Host ("VERDICT: FAIL — {0}/{1} checks failed" -f $script:Failures.Count, $script:Checks) -ForegroundColor Red
$script:Failures | ForEach-Object { Write-Host ("  - {0}" -f $_) -ForegroundColor Red }
exit 1
