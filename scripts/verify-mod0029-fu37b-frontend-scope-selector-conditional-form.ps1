param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Text([string]$relativePath) {
    $path = Join-Path $RepoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        $failures.Add("Missing file: $relativePath")
        return ''
    }
    return Get-Content -LiteralPath $path -Raw
}

function Require([string]$text, [string]$pattern, [string]$message) {
    if ($text -notmatch $pattern) { $failures.Add($message) }
}

function Forbid([string]$text, [string]$pattern, [string]$message) {
    if ($text -match $pattern) { $failures.Add($message) }
}

$controller = Text 'frontend/Diten.Web/Controllers/DocumentManagementMasterRegisterController.cs'
$view = Text 'frontend/Diten.Web/Views/DocumentManagement/MasterRegister/CreateControlledDocument.cshtml'
$form = Text 'frontend/Diten.Web/Views/DocumentManagement/MasterRegister/_CreateControlledDocumentForm.cshtml'
$l10n = Text 'frontend/Diten.Web/Views/DocumentManagement/MasterRegister/_CreateControlledDocumentL10n.cshtml'
$script = Text 'frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/MasterRegister/create-controlled-document.js'
$controlledIndex = Text 'frontend/Diten.Web/Views/DocumentManagement/ControlledDocuments/Index.cshtml'
$controlledController = Text 'frontend/Diten.Web/Controllers/DocumentManagementControlledDocumentsController.cs'

Require $form 'id="registrationDocumentScope"' 'DocumentScope selector missing.'
Require $form '<option value="Company" selected>' 'Company must be the default scope.'
Require $form '<option value="Corporate">' 'Corporate scope option missing.'
Require $form 'data-company-field' 'Company conditional field group missing.'
Require $form 'data-corporate-field' 'Corporate conditional field group missing.'
Require $form 'id="registrationCorporateOwner"' 'Corporate owner selector missing.'
Require $form 'id="registrationCollectionInstance"' 'CollectionInstance selector missing.'
Require $form 'id="registrationFolder"' 'Folder selector missing.'
Require $form 'CorporateCollectionInstanceProvisioningRequired' 'Corporate missing-instance blocking state missing.'
Require $form 'id="registrationLanguage"[^>]*required' 'Governed language select missing.'
Require $form 'id="registrationRetentionClass"[^>]*data-reference-set=' 'Governed retention Select2 source missing.'
Forbid $form '<input[^>]+id="registrationLanguage"' 'Language must not be free text.'
Forbid $form '<input[^>]+id="registrationRetentionClass"' 'Retention class must not be free text.'

Require $controller 'api/corporate-collection-instances' 'Same-origin Corporate instance proxy missing.'
Require $controller 'ProxyGetAsync\(\$"\{ApiRoot\}/corporate-collection-instances' 'Corporate proxy must use Gateway transport.'
Require $controller 'api/governed-languages' 'Same-origin governed language proxy missing.'
Require $controller 'controlled-document-registrations/governed-languages' 'Language proxy must use the document-management governed lookup contract.'
Forbid $controller 'corporate-collection-instances/provision' 'FU37B must not expose Corporate provisioning.'

Require $script 'documentScope:\s*currentScope\(\)' 'Payload must include DocumentScope.'
Require $script 'payload\.corporateOwnerId\s*=' 'Corporate payload must include CorporateOwnerId.'
Require $script 'payload\.companyId\s*=' 'Company payload must include CompanyId.'
Require $script 'payload\.ownerCompanyId\s*=' 'Company payload must include OwnerCompanyId.'
Require $script 'if \(corporate\)' 'Payload must branch by scope.'
Require $script 'collectionInstanceId:\s*folderId' 'Payload must align CollectionInstanceId with the selected runtime folder node.'
Require $script 'folderId' 'Payload must include FolderId.'
Require $script 'governingLanguageId' 'Payload must include stable governed language value.'
Require $script 'retentionClassId' 'Payload must include stable governed retention value.'
Require $script 'applyScope' 'Scope-switch reset behavior missing.'
Require $script 'bindSelectChange\(''registrationDocumentScope'',\s*applyScope\)' 'DocumentScope must use the Select2-compatible change binding.'
Require $script '\.on\(''change\.controlled-document-registration''' 'Select2 change events must be handled through the jQuery event bridge.'
Forbid $script 'registrationDocumentScope''\)\?\.addEventListener\(''change''' 'Native-only DocumentScope binding breaks Select2 scope changes.'
Require $script 'resetSelect\(el\(''registrationCollectionInstance''' 'Scope switch must clear CollectionInstance.'
Require $script 'resetSelect\(el\(''registrationFolder''' 'Scope switch must clear Folder.'
Require $script 'GovernedLookupUnavailable' 'Unavailable governed lookups must block registration.'
Require $script 'error\.status === 409' '409 idempotency/scope conflict handling missing.'
Require $script 'error\.status === 403' '403 access handling missing.'
Require $script 'error\.status === 404' '404 non-leaking mismatch handling missing.'
Require $script "const completedStatus = 'COMPLETED'" 'Completed success gate missing.'
Require $script 'normalized === completedStatus' 'Success must require Completed.'
Require $script 'new FormData\(\)' 'Multipart FormData transport missing.'
Forbid $script 'FileReader|readAsDataURL|contentBase64|ContentBase64|btoa\(' 'File must not be Base64 encoded.'
Forbid $script 'TenantId|X-Tenant-Id|localhost|5057' 'Browser surface contains forbidden tenant/direct-service transport.'
Forbid $script 'provisioning-operations|/provision' 'Frontend must not provision Corporate instances.'
Forbid $script 'uid|documentCode|effectiveDate|registerStatus|lifecycleStatus|releaseGate|signature' 'Forbidden lifecycle/identifier fields found in registration JS.'

Require $view 'platform\.document-management\.master-register\.registration\.create' 'Create permission gate missing.'
Require $view 'platform\.document-management\.master-register\.registration\.reconcile' 'Retry permission gate missing.'
Require $controlledIndex 'CreateControlledDocument' 'Controlled Documents Add redirect was not preserved.'
Require $controlledIndex 'Create\?kind=template' 'Template create route was not preserved.'
Require $controlledController 'Redirect\("/DocumentManagementMasterRegister/CreateControlledDocument"\)' 'Controlled Documents direct create redirect missing.'
Require $controlledController 'string\.Equals\(kind, "template"' 'Template controller path missing.'

$languages = @('en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr')
$requiredKeys = @(
    'DocumentScope', 'DocumentScopeCompany', 'DocumentScopeCorporate', 'CompanyScopeHelp', 'CorporateScopeHelp',
    'ScopeSwitchClearsSelections', 'CorporateOwner', 'CorporateCollectionInstance', 'CompanyCollectionInstance',
    'CorporateFolder', 'CompanyFolder', 'CorporateCollectionInstanceProvisioningRequired',
    'NoCorporateCollectionInstancesAvailable', 'SelectCorporateOwner', 'SelectCorporateCollectionInstance',
    'SelectCorporateFolder', 'SelectCompanyCollectionInstance', 'SelectCompanyFolder', 'GovernedRetentionClass',
    'LanguageSelectionRequired', 'RetentionClassSelectionRequired', 'GovernedLookupUnavailable',
    'IdempotencyScopeConflict', 'ScopeMismatch', 'CorporateAccessDenied', 'CompanyAccessDenied'
)
$baseline = $null
foreach ($language in $languages) {
    $relative = "frontend/Diten.Web/Resources/Views/DocumentManagement/MasterRegister/MasterRegisterIndex.$language.resx"
    $content = Text $relative
    try { [xml]$xml = $content } catch { $failures.Add("Invalid XML: $relative"); continue }
    $keys = @($xml.root.data | ForEach-Object { $_.name } | Sort-Object)
    foreach ($key in $requiredKeys) {
        if ($keys -notcontains $key) { $failures.Add("$language RESX missing $key") }
    }
    if (($keys | Group-Object | Where-Object Count -gt 1).Count -gt 0) {
        $failures.Add("$language RESX contains duplicate keys")
    }
    if ($null -eq $baseline) { $baseline = $keys }
    elseif ((Compare-Object $baseline $keys).Count -gt 0) { $failures.Add("$language RESX parity mismatch") }
}

Require $l10n 'IdempotencyScopeConflict' '409 localization bridge missing.'
Require $l10n 'CorporateAccessDenied' 'Corporate access localization bridge missing.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASS MOD-0029-FU37B frontend scope selector and conditional form verifier'
