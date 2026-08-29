param([string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path)

$ErrorActionPreference = "Stop"
$controller = Join-Path $RepoRoot "frontend/Diten.Web/Controllers/DocumentManagementControlledDocumentsController.cs"
$view = Join-Path $RepoRoot "frontend/Diten.Web/Views/DocumentManagement/ControlledDocuments/Details.cshtml"
$js = Join-Path $RepoRoot "frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/ControlledDocuments/detail.js"
$index = Join-Path $RepoRoot "frontend/Diten.Web/Views/DocumentManagement/ControlledDocuments/Index.cshtml"
$registrationController = Join-Path $RepoRoot "services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementControlledDocumentRegistrationController.cs"
$manualLinkService = Join-Path $RepoRoot "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementMasterRegister/Services/DocumentMasterRegisterService.cs"

$checks = @(
    @{ Name = "Master Register card"; File = $view; Pattern = 'id="masterRegisterCard"' },
    @{ Name = "Read-only card title"; File = $view; Pattern = 'id="mr_card_title"' },
    @{ Name = "MVC reverse proxy"; File = $controller; Pattern = '[HttpGet("master-register/{controlledDocumentId:guid}")]' },
    @{ Name = "Backend reverse route"; File = $controller; Pattern = '/controlled-documents/{controlledDocumentId}/master-register' },
    @{ Name = "Neutral missing state"; File = $js; Pattern = "renderMasterRegisterMissing" },
    @{ Name = "Missing state identifier"; File = $js; Pattern = "missing_link_message" },
    @{ Name = "Unverified not success"; File = $js; Pattern = "ReadinessFailClosedDueToLink" },
    @{ Name = "Compatible-only open action"; File = $js; Pattern = "if (compatible)" },
    @{ Name = "Open Master Register route"; File = $js; Pattern = "/DocumentManagementMasterRegister/Details/" },
    @{ Name = "Normal GET redirects"; File = $controller; Pattern = 'Redirect("/DocumentManagementMasterRegister/CreateControlledDocument")' },
    @{ Name = "Normal toolbar redirects"; File = $index; Pattern = 'href="/DocumentManagementMasterRegister/CreateControlledDocument"' },
    @{ Name = "Direct POST blocked"; File = $controller; Pattern = '"LEGACY_CREATE_RESTRICTED"' },
    @{ Name = "Template route preserved"; File = $index; Pattern = '/DocumentManagementControlledDocuments/Create?kind=template' },
    @{ Name = "Version upload preserved"; File = $controller; Pattern = '[HttpPost("upload-version/{id:guid}")]' },
    @{ Name = "Explorer preserved"; File = $controller; Pattern = 'public IActionResult Index()' },
    @{ Name = "Backend controlled view permission"; File = $registrationController; Pattern = 'ControlledDocumentsView' },
    @{ Name = "Backend register view permission"; File = $registrationController; Pattern = 'DocumentMasterRegisterPermissions.View' },
    @{ Name = "FU37C reason preserved"; File = $manualLinkService; Pattern = "ControlledDocumentLinkReason" },
    @{ Name = "FU37C compatibility preserved"; File = $manualLinkService; Pattern = "LinkScopeCompatibilityStatus" }
)

$failures = @()
foreach ($check in $checks) {
    if (!(Test-Path -LiteralPath $check.File) -or
        !(Select-String -LiteralPath $check.File -SimpleMatch $check.Pattern -Quiet)) {
        $failures += $check.Name
    }
}

$browserFiles = @($view, $js, $index)
foreach ($file in $browserFiles) {
    $content = Get-Content -Raw -LiteralPath $file
    if ($content -match 'localhost|5057|X-Tenant-Id|type="hidden"[^>]*TenantId') {
        $failures += "Browser boundary violation: $file"
    }
}

$resourceDir = Join-Path $RepoRoot "frontend/Diten.Web/Resources/Views/DocumentManagement/ControlledDocuments"
$cultures = @("ar", "en", "es", "fr", "ru", "tr", "zh")
$requiredKeys = @(
    "MasterRegister", "LinkedMasterRegister", "OpenMasterRegister", "MasterRegisterEntry",
    "MasterRegisterLinkStatus", "NoLinkedMasterRegisterLegacyHint", "ReverseLookupFailed",
    "ReverseLookupForbidden", "ReadinessFailClosedDueToLink", "GovernedRegistrationComplete"
)
$baseline = $null
foreach ($culture in $cultures) {
    $path = Join-Path $resourceDir "ControlledDocumentsIndex.$culture.resx"
    [xml]$xml = Get-Content -Raw -LiteralPath $path
    $keys = @($xml.root.data | ForEach-Object { $_.name } | Sort-Object)
    foreach ($key in $requiredKeys) {
        if ($keys -notcontains $key) { $failures += "$culture missing $key" }
    }
    if ($null -eq $baseline) { $baseline = $keys }
    elseif (Compare-Object $baseline $keys) { $failures += "$culture resource parity mismatch" }
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Error "FAIL: $_" }
    exit 1
}

Write-Host "PASS MOD-0029-FU36C reverse navigation and legacy bypass ($($checks.Count) structural checks + 7 RESX parity)"
