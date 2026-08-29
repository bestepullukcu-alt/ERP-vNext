<#
    MOD-0029-FU24 — Document Master Register UI verifier.

    Static contract + guardrail checks for the TenantShell Master Register screens. Read-only: it never edits,
    builds or calls a service. Run from the repository root:

        pwsh ./scripts/verify-mod0029-fu24-ui.ps1
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
    if ($Condition) {
        Write-Host ("  PASS  {0}" -f $Name) -ForegroundColor Green
    }
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

<#
    Guardrail greps must inspect executable code, not prose. Documentation comments legitimately mention the very
    things the guardrails forbid ("no direct Platform 5057 call", "no delete/lifecycle mutation"), so comments are
    stripped before any forbidden-token scan.
#>
function Remove-Comments {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $t = $Text
    $t = [regex]::Replace($t, '@\*[\s\S]*?\*@', ' ')      # Razor
    $t = [regex]::Replace($t, '<!--[\s\S]*?-->', ' ')     # HTML
    $t = [regex]::Replace($t, '/\*[\s\S]*?\*/', ' ')      # C# / JS block
    $t = [regex]::Replace($t, '(?m)^\s*//.*$', ' ')       # C# / JS line
    return $t
}

$web = 'frontend/Diten.Web'
$viewDir = "$web/Views/DocumentManagement/MasterRegister"
$jsDir = "$web/wwwroot/assets/js/DocumentManagement/MasterRegister"
$resxDir = "$web/Resources/Views/DocumentManagement/MasterRegister"
$cultures = @('ar', 'en', 'es', 'fr', 'ru', 'tr', 'zh')

Write-Host "`nMOD-0029-FU24 — Document Master Register UI verifier" -ForegroundColor Cyan
Write-Host ("Repo root: {0}`n" -f $RepoRoot)

# 1 — core files exist
Write-Host 'Files' -ForegroundColor Cyan
$required = @(
    "$web/Controllers/DocumentManagementMasterRegisterController.cs",
    "$viewDir/MasterRegisterIndex.cs",
    "$viewDir/Index.cshtml",
    "$viewDir/_DataTable.cshtml",
    "$viewDir/_Filter.cshtml",
    "$viewDir/_IndexL10n.cshtml",
    "$viewDir/_Form.cshtml",
    "$viewDir/Create.cshtml",
    "$viewDir/Edit.cshtml",
    "$viewDir/Details.cshtml",
    "$jsDir/index.js",
    "$jsDir/index.l10n.js"
)
foreach ($f in $required) {
    Assert-True "exists: $f" (Test-Path -LiteralPath (Join-Path $RepoRoot $f))
}

$controller = Get-Text "$web/Controllers/DocumentManagementMasterRegisterController.cs"
$index = Get-Text "$viewDir/Index.cshtml"
$dataTable = Get-Text "$viewDir/_DataTable.cshtml"
$form = Get-Text "$viewDir/_Form.cshtml"
$details = Get-Text "$viewDir/Details.cshtml"
$js = Get-Text "$jsDir/index.js"
$layout = Get-Text "$web/Views/Shared/_LayoutTenantShell.cshtml"
$allUi = @($controller, $index, $dataTable, $form, $details, $js, (Get-Text "$viewDir/_Filter.cshtml"), (Get-Text "$viewDir/_IndexL10n.cshtml"), (Get-Text "$viewDir/Create.cshtml"), (Get-Text "$viewDir/Edit.cshtml")) -join "`n"
$allUiCode = Remove-Comments $allUi
$jsCode = Remove-Comments $js
$controllerCode = Remove-Comments $controller

# 2 — navigation
Write-Host "`nNavigation" -ForegroundColor Cyan
Assert-True 'TenantShell nav entry present' ($layout -match '/DocumentManagementMasterRegister')
Assert-True 'nav gated by seeded view permission' ($layout -match 'platform\.document-management\.master-register\.view')

# 3 — layout + DataTable v2
Write-Host "`nShell & DataTable v2" -ForegroundColor Cyan
Assert-True 'Index uses _LayoutTenantShell' ($index -match '_LayoutTenantShell')
Assert-True 'Details uses _LayoutTenantShell' ($details -match '_LayoutTenantShell')
Assert-True 'DataTable v2 marker present' ($dataTable -match 'data-dt-standard="v2"')
Assert-True 'DataTable built through DitenDataTable.createCrudTable' ($js -match 'DitenDataTable\.createCrudTable')
Assert-True 'exportButtons toolbar wired' ($js -match 'DtDefaults\.exportButtons')
Assert-True '_IndexL10n partial referenced by Index' ($index -match '_IndexL10n')

# 4 — proxy profile
Write-Host "`nMVC proxy profile" -ForegroundColor Cyan
Assert-True 'controller is [Authorize]' ($controller -match '\[Authorize\]')
Assert-True 'controller resolves gateway from configuration' ($controller -match 'configuration\["GatewayUrl"\]')
Assert-True 'controller forwards bearer from server-side cookie' ($controller -match 'AuthTokenCookies\.GetAccessToken')
Assert-True 'JS calls same-origin proxy only' ($js -match "/DocumentManagement/MasterRegister/api")
Assert-True 'mutations post an anti-forgery token' ($js -match '__RequestVerificationToken')
Assert-True 'create/update proxy actions validate anti-forgery' (([regex]::Matches($controller, '\[ValidateAntiForgeryToken\]')).Count -ge 2)

# 5 — guardrails
Write-Host "`nGuardrails" -ForegroundColor Cyan
Assert-True 'no direct Platform 5057 call' (-not ($allUiCode -match '5057'))
Assert-True 'no localhost URL in UI assets' (-not ($allUiCode -match 'http://localhost'))
Assert-True 'no X-Tenant-Id in browser code' (-not ($jsCode -match 'X-Tenant-Id'))
Assert-True 'no tenant id field in views' (-not ((Remove-Comments ($index + $form + $details)) -match '(?i)tenantid'))
Assert-True 'no DELETE verb from the UI' (-not ($jsCode -match "method:\s*'DELETE'" -or $jsCode -match 'HttpMethod\.Delete'))
Assert-True 'no delete/purge proxy action' (-not ($controllerCode -match '(?i)(delete|purge)'))
Assert-True 'no delete row action' (-not ($jsCode -match "key:\s*'delete'"))
# FU36B adds a separate unified controlled-document registration route with an intentional multipart upload.
# FU24's legacy metadata-only create/edit/detail surfaces must remain upload-free.
$fu24MetadataSurface = $form + $details + $jsCode
Assert-True 'no file upload surface in FU24 metadata create/edit/details' `
    (-not ($fu24MetadataSurface -match '(?i)(contentBase64|IFormFile|type="file")'))
Assert-True 'no lifecycle/approval/sign mutation call' (-not ($jsCode -match '(?i)/(approve|effective|retire|sign|dispose|suspend)'))

# 6 — create/edit/details behaviour
Write-Host "`nCreate / Edit / Details" -ForegroundColor Cyan
Assert-True 'form declares create/edit mode' ($form -match 'data-form-mode')
Assert-True 'required metadata fields marked required' ($form -match 'id="documentTitle"[^>]*required' -or $form -match 'id="documentTitle"(.|\n)*?required')
$updateBlock = [regex]::Match($jsCode, 'buildUpdatePayload\s*=\s*\(\)\s*=>\s*\(\{[\s\S]*?\}\);').Value
Assert-True 'update payload block located' (-not [string]::IsNullOrWhiteSpace($updateBlock))
Assert-True 'edit payload omits protected allocation/lifecycle fields' `
    (-not ($updateBlock -match '(?im)^\s*(permanentUid|documentCode|lifecycleStatus|registerStatus|effectiveDate|nextReviewDueDate|approvalEvidenceStatus|lastReleaseGate\w*|isControlledDocument|isRecord|isVariant)\s*:'))
# MOD-0029-FU29 replaced the last deferred placeholders with real tabs; assert the detail screen has real tab panes.
Assert-True 'details renders real governance tab panes (no placeholder remains)' `
    (($details -match 'id="tab-general"') -and -not ($details -match 'DeferredSectionMessage'))
Assert-True 'details calls only the detail endpoint' (($js -match "endpoint\}/detail/") -and -not ($js -match '/training|/release-gates|/signatures'))

# 7 — localization parity
Write-Host "`nLocalization (7-culture parity)" -ForegroundColor Cyan
$keySets = @{}
foreach ($c in $cultures) {
    $path = Join-Path $RepoRoot "$resxDir/MasterRegisterIndex.$c.resx"
    if (-not (Test-Path -LiteralPath $path)) {
        Assert-True "resx exists: $c" $false
        continue
    }
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
}
foreach ($c in $cultures) {
    $shared = Get-Text "$web/Resources/SharedResource.$c.resx"
    Assert-True "shared nav label present: $c" ($shared -match 'name="DocumentMasterRegister"')
}

# 8 — permission gating
Write-Host "`nPermission gating" -ForegroundColor Cyan
Assert-True 'Index gates create/edit on master-register.manage' ($index -match 'platform\.document-management\.master-register\.manage')
Assert-True 'Details gates edit on master-register.manage' ($details -match 'platform\.document-management\.master-register\.manage')

# 9 — untouched surfaces
Write-Host "`nOut-of-scope surfaces untouched" -ForegroundColor Cyan
$gitAvailable = $null -ne (Get-Command git -ErrorAction SilentlyContinue)
if ($gitAvailable) {
    Push-Location $RepoRoot
    try {
        $changed = @(git status --porcelain | ForEach-Object { ($_ -replace '^.{3}', '').Trim() })
    }
    finally { Pop-Location }
    # The working tree carries pre-existing modifications from earlier FUs and the CRM branch. FU24 cannot assert
    # those files are pristine — it asserts it did not ADD anything to them, i.e. no FU24 marker leaked outside the
    # Master Register surface + the TenantShell nav entry.
    $outOfScope = @($changed | Where-Object {
        $_ -match '^gateway/' -or
        $_ -match '^services/Diten\.AuthService/' -or
        $_ -match '^services/Diten\.CrmService/' -or
        $_ -match '^frontend/Diten\.Web/(Views|Controllers|wwwroot/assets/js)/(CRM|HCM)/'
    })
    if ($outOfScope.Count -gt 0) {
        Write-Host ("  INFO  pre-existing out-of-scope modifications (not FU24): {0}" -f ($outOfScope -join ', ')) -ForegroundColor Yellow
    }

    Push-Location $RepoRoot
    try {
        $leaked = @(git grep -l 'MOD-0029-FU24' -- 'gateway' 'services' 'frontend/Diten.Web/Views/CRM' 'frontend/Diten.Web/Views/HCM' 'frontend/Diten.Web/Controllers/CRM' 'frontend/Diten.Web/Controllers/HCM' 2>$null)
    }
    catch { $leaked = @() }
    finally { Pop-Location }
    Assert-True 'no FU24 change leaked into gateway / services / CRM / HCM' ($leaked.Count -eq 0) ("touched: " + ($leaked -join ', '))
}
else {
    Write-Host '  SKIP  git not available — working-tree scope check skipped' -ForegroundColor Yellow
}

Write-Host ''
if ($script:Failures.Count -eq 0) {
    Write-Host ("VERDICT: PASS — {0}/{0} checks green" -f $script:Checks) -ForegroundColor Green
    exit 0
}

Write-Host ("VERDICT: FAIL — {0}/{1} checks failed" -f $script:Failures.Count, $script:Checks) -ForegroundColor Red
$script:Failures | ForEach-Object { Write-Host ("  - {0}" -f $_) -ForegroundColor Red }
exit 1
