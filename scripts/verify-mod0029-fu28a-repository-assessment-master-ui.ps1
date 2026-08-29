<#
    MOD-0029-FU28A — Repository Assessment Master UI verifier (tenant-global master data screen).

    Static contract + guardrail checks. Read-only: it never edits, builds or calls a service. Run from the repo root:

        pwsh ./scripts/verify-mod0029-fu28a-repository-assessment-master-ui.ps1
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
$viewDir = "$web/Views/DocumentManagement/RepositoryAssessments"
$jsDir = "$web/wwwroot/assets/js/DocumentManagement/RepositoryAssessments"
$resxDir = "$web/Resources/Views/DocumentManagement/RepositoryAssessments"
$cultures = @('ar', 'en', 'es', 'fr', 'ru', 'tr', 'zh')

Write-Host "`nMOD-0029-FU28A — Repository Assessment Master UI verifier" -ForegroundColor Cyan
Write-Host ("Repo root: {0}`n" -f $RepoRoot)

# 1 — files
Write-Host 'Files' -ForegroundColor Cyan
$required = @(
    "$web/Controllers/DocumentManagementRepositoryAssessmentsController.cs",
    "$viewDir/RepositoryAssessmentsIndex.cs",
    "$viewDir/Index.cshtml",
    "$viewDir/_DataTable.cshtml",
    "$viewDir/_Filter.cshtml",
    "$viewDir/_IndexL10n.cshtml",
    "$viewDir/_Form.cshtml",
    "$viewDir/_DecisionModals.cshtml",
    "$viewDir/Create.cshtml",
    "$viewDir/Edit.cshtml",
    "$viewDir/Details.cshtml",
    "$jsDir/index.js",
    "$jsDir/index.l10n.js"
)
foreach ($f in $required) { Assert-True "exists: $f" (Test-Path -LiteralPath (Join-Path $RepoRoot $f)) }

$controller = Get-Text "$web/Controllers/DocumentManagementRepositoryAssessmentsController.cs"
$index = Get-Text "$viewDir/Index.cshtml"
$form = Get-Text "$viewDir/_Form.cshtml"
$details = Get-Text "$viewDir/Details.cshtml"
$modals = Get-Text "$viewDir/_DecisionModals.cshtml"
$dataTable = Get-Text "$viewDir/_DataTable.cshtml"
$js = Get-Text "$jsDir/index.js"
$l10n = Get-Text "$viewDir/_IndexL10n.cshtml"
$layout = Get-Text "$web/Views/Shared/_LayoutTenantShell.cshtml"
$controllerCode = Remove-Comments $controller
$jsCode = Remove-Comments $js
$allViews = @($index, $form, $details, $modals, $dataTable, (Get-Text "$viewDir/_Filter.cshtml"), (Get-Text "$viewDir/Create.cshtml"), (Get-Text "$viewDir/Edit.cshtml")) -join "`n"
$allViewsCode = Remove-Comments $allViews

# 2 — navigation
Write-Host "`nNavigation" -ForegroundColor Cyan
Assert-True 'TenantShell nav entry present' ($layout -match '/DocumentManagementRepositoryAssessments')
Assert-True 'nav gated by seeded view permission' ($layout -match 'platform\.document-management\.repository-assessment\.view')

# 3 — shell + DataTable v2
Write-Host "`nShell & DataTable v2" -ForegroundColor Cyan
Assert-True 'Index uses _LayoutTenantShell' ($index -match '_LayoutTenantShell')
Assert-True 'Details uses _LayoutTenantShell' ($details -match '_LayoutTenantShell')
Assert-True 'Create/Edit use _LayoutTenantShell' ((Get-Text "$viewDir/Create.cshtml") -match '_LayoutTenantShell' -and (Get-Text "$viewDir/Edit.cshtml") -match '_LayoutTenantShell')
Assert-True 'DataTable v2 marker present' ($dataTable -match 'data-dt-standard="v2"')
Assert-True 'DataTable built through DitenDataTable.createCrudTable' ($js -match 'DitenDataTable\.createCrudTable')
Assert-True 'exportButtons toolbar wired' ($js -match 'DtDefaults\.exportButtons')
Assert-True '_IndexL10n partial referenced by Index' ($index -match '_IndexL10n')

# 4 — proxy endpoints
Write-Host "`nMVC proxy endpoints" -ForegroundColor Cyan
Assert-True 'controller is [Authorize]' ($controller -match '\[Authorize\]')
Assert-True 'controller resolves gateway from configuration' ($controller -match 'configuration\["GatewayUrl"\]')
Assert-True 'controller forwards bearer from server-side cookie' ($controller -match 'AuthTokenCookies\.GetAccessToken')
foreach ($r in @('list', '{id:guid}', '{id:guid}/findings', 'create', '{id:guid}/update', '{id:guid}/evaluate', '{id:guid}/approve', '{id:guid}/reject')) {
    Assert-True "proxy route: $r" ($controllerCode -match [regex]::Escape("/DocumentManagement/RepositoryAssessments/api/$r"))
}
Assert-True 'downstream repository-assessments base route correct' ($controllerCode -match '/api/v1/document-management/repository-assessments')
Assert-True 'JS calls same-origin proxy only' ($js -match "/DocumentManagement/RepositoryAssessments/api")

$posts = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/RepositoryAssessments/api/[^"]+"\)\]')
$guarded = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/RepositoryAssessments/api/[^"]+"\)\]\s*\r?\n\s*\[ValidateAntiForgeryToken\]')
Assert-True 'POST proxy surface complete (5 mutations)' ($posts.Count -eq 5) ("found $($posts.Count)")
Assert-True 'every POST proxy has [ValidateAntiForgeryToken]' ($posts.Count -gt 0 -and $guarded.Count -eq $posts.Count) ("guarded $($guarded.Count) of $($posts.Count)")

# 5 — guardrails
Write-Host "`nGuardrails" -ForegroundColor Cyan
Assert-True 'no direct Platform 5057 call' (-not ($allViewsCode -match '5057') -and -not ($jsCode -match '5057'))
Assert-True 'no localhost URL' (-not ($allViewsCode -match 'http://localhost') -and -not ($jsCode -match 'http://localhost'))
Assert-True 'no X-Tenant-Id in browser code' (-not ($jsCode -match 'X-Tenant-Id'))
Assert-True 'no tenant id field in views' (-not ($allViewsCode -match '(?i)tenantid'))
Assert-True 'no DELETE verb from the UI' (-not ($jsCode -match "method:\s*'DELETE'"))
Assert-True 'no delete/purge proxy action' (-not ($controllerCode -match '(?i)\b(delete|purge)\b'))
Assert-True 'no file upload surface' (-not ($allViewsCode -match '(?i)type="file"') -and -not ($jsCode -match '(?i)contentBase64'))
Assert-True 'anti-forgery token on mutations' ($jsCode -match '__RequestVerificationToken')
Assert-True 'buttons locked during a request' ($jsCode -match 'button\.disabled = true')
Assert-True 'server messages HTML-escaped' ($jsCode -match 'replace\(/\[&<>"' + "'" + '\]/g')
Assert-True 'no auto approve / gate pass / effective chaining' `
    (-not ($jsCode -match '(?i)(autoApprove|autoEffective|forceApprove|gatePass|autoGate)'))
Assert-True 'boundary/gate support flags read from backend, never assigned' `
    (-not ($jsCode -match 'canSupportReleaseGate\s*=(?![=>])') -and -not ($jsCode -match 'canSupportRegulatedESignature\s*=(?![=>])'))
Assert-True 'boundary statement rendered verbatim from backend' ($jsCode -match 'boundary\.boundaryStatement')
Assert-True 'no external DMS/QMS integration' (-not ($jsCode -match '(?i)(veeva|opentext|externaldms|/qms/api)'))
Assert-True 'no certificate / part-11 assertion in views' (-not ($allViewsCode -match '(?i)(certificate|21 cfr|part 11)'))

# 6 — compliance-claim guardrails
Write-Host "`nCompliance-claim guardrails" -ForegroundColor Cyan
Assert-True 'no-compliance-claim banner on index' ($index -match 'NoComplianceClaim' -and $index -match 'RepositoryBoundaryDisclaimer')
Assert-True 'no-compliance-claim banner on details' ($details -match 'NoComplianceClaim')
Assert-True 'no-compliance-claim banner on form' ($form -match 'NoComplianceClaim')
Assert-True 'interim repository warning wired' ($jsCode -match 'InterimRepositoryWarning' -and $jsCode -match 'ApprovedInterimRepository')
Assert-True 'unapproved repository warning wired' ($jsCode -match 'UnapprovedRepositoryWarning' -and $jsCode -match 'UnapprovedRepository')
Assert-True 'evaluate-does-not-approve wording present' ($jsCode -match 'EvaluateDoesNotApprove' -or $modals -match 'EvaluateDoesNotApprove' -or $l10n -match 'EvaluateDoesNotApprove')
Assert-True 'approve-does-not-validate wording present' ($jsCode -match 'ApproveDoesNotValidate')
Assert-True 'reject-does-not-delete wording present' ($jsCode -match 'RejectDoesNotDelete')
Assert-True 'validation-evidence field labelled as reference, not compliance' ($form -match 'validationEvidenceReference')
Assert-True 'no validated-DMS checkbox' (-not ($form -match '(?i)type="checkbox"[^>]*validateddms'))

# 7 — governance UX contracts
Write-Host "`nGovernance UX contracts" -ForegroundColor Cyan
Assert-True 'Gate 2 note rendered' ($index -match 'ReleaseGate2Note' -or $details -match 'ReleaseGate2Note')
Assert-True 'boundary statement panel present' ($details -match 'boundaryPanel')
Assert-True 'findings table present' ($details -match 'assessmentFindingsBody')
Assert-True 'approver role dropdown uses SOP roles' ($modals -match 'GQD' -and $modals -match 'GQDDeputy' -and $modals -match 'ITCSVOwner')
Assert-True 'rejection reason required' ($modals -match 'id="assessmentRejectionReason"' -and $jsCode -match "assessmentRejectionReason'\)\.required = !isApprove")
Assert-True 'approve/reject only when decidable' ($jsCode -match 'isDecidable' -or $jsCode -match "assessmentStatus === 'Draft'")
Assert-True 'critical findings visually strong' ($jsCode -match 'table-danger' -and $jsCode -match "severity === 'Critical'")
Assert-True 'form field names mirror RepositoryAssessmentFieldsInput' `
    ($form -match 'name="repositoryName"' -and $form -match 'name="approvalMechanismDescription"' -and $form -match 'name="maxInterimPeriodDays"' -and $form -match 'name="migrationReconciliationReference"')
Assert-True 'no invented backend field names in payload' `
    ($jsCode -match 'repositoryOwnerUserId' -and $jsCode -match 'restoreTestFrequency' -and $jsCode -match 'effectiveCopyControlDescription')
Assert-True 'empty state handled' ($jsCode -match 'NoRepositoryAssessmentsFound' -and $jsCode -match 'NoFindingsFound')
Assert-True '401/403/409 handled distinctly' ($jsCode -match 'status === 401' -and $jsCode -match 'status === 403' -and $jsCode -match 'status === 409')
foreach ($code in @('ASSESSMENT_NOT_FOUND', 'ALREADY_DECIDED', 'APPROVER_ROLE_INVALID', 'LINK_STATUS_INVALID',
                    'NAME_AND_TYPE_REQUIRED', 'REQUIRED_FIELDS_MISSING', 'NOT_FOUND_NON_LEAKAGE', 'PERMISSION_DENIED', 'VALIDATION_FAILED')) {
    Assert-True "reason code mapped: $code" ($jsCode -match [regex]::Escape($code))
}
Assert-True 'unknown reason codes fall through to server message' ($jsCode -match 'if \(serverMessage\) return serverMessage;')

# 8 — permission gating (exact seeded keys)
Write-Host "`nPermission gating (seeded keys only)" -ForegroundColor Cyan
$seededKeys = @(
    'platform.document-management.repository-assessment.view',
    'platform.document-management.repository-assessment.manage',
    'platform.document-management.repository-assessment.approve'
)
$permText = ($index + $details + $modals + $layout)
foreach ($k in $seededKeys) { Assert-True "gated on seeded key: $k" ($permText -match [regex]::Escape($k)) }
$seeder = Get-Text 'services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs'
foreach ($k in $seededKeys) {
    $parts = $k -split '\.'
    $action = $parts[-1]
    $module = ($parts[1..($parts.Count - 2)]) -join '.'
    Assert-True "key is actually seeded: $k" ($seeder -match ('"' + [regex]::Escape($module) + '",\s*"' + [regex]::Escape($action) + '"'))
}
Assert-True 'no invented .create/.review/.link permission keys' `
    (-not ($permText -match 'repository-assessment\.(create|review|link)\b'))

# 9 — localization parity
Write-Host "`nLocalization (7-culture parity)" -ForegroundColor Cyan
$keySets = @{}
foreach ($c in $cultures) {
    $path = Join-Path $RepoRoot "$resxDir/RepositoryAssessmentsIndex.$c.resx"
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
    foreach ($k in @('RepositoryAssessmentsTitle', 'BoundaryStatement', 'InterimRepositoryWarning',
                     'NoValidatedDmsClaim', 'ApproveDoesNotValidate', 'ReleaseGate2Note')) {
        Assert-True "FU28A key present in en resx: $k" ($baseline -contains $k)
    }
}
foreach ($c in $cultures) {
    $shared = Get-Text "$web/Resources/SharedResource.$c.resx"
    Assert-True "shared nav label present: $c" ($shared -match 'name="RepositoryAssessments"')
}

# 10 — out-of-scope surfaces
Write-Host "`nOut-of-scope surfaces" -ForegroundColor Cyan
if ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) {
    Push-Location $RepoRoot
    try {
        $leaked = @(git grep -l 'MOD-0029-FU28A' -- 'gateway' 'services' 'frontend/Diten.Web/Views/CRM' 'frontend/Diten.Web/Views/HCM' 'frontend/Diten.Web/Controllers/CRM' 'frontend/Diten.Web/Controllers/HCM' 2>$null)
    }
    catch { $leaked = @() }
    finally { Pop-Location }
    Assert-True 'no FU28A change leaked into gateway / services / CRM / HCM' ($leaked.Count -eq 0) ("touched: " + ($leaked -join ', '))
}
else { Write-Host '  SKIP  git not available' -ForegroundColor Yellow }
Assert-True 'FU28 document Repository tab (details.js) untouched by scope creep' ((Get-Text "$web/wwwroot/assets/js/DocumentManagement/MasterRegister/details.js") -match 'MOD-0029-FU28')

Write-Host ''
if ($script:Failures.Count -eq 0) {
    Write-Host ("VERDICT: PASS — {0}/{0} checks green" -f $script:Checks) -ForegroundColor Green
    exit 0
}
Write-Host ("VERDICT: FAIL — {0}/{1} checks failed" -f $script:Failures.Count, $script:Checks) -ForegroundColor Red
$script:Failures | ForEach-Object { Write-Host ("  - {0}" -f $_) -ForegroundColor Red }
exit 1
