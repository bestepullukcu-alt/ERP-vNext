$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$controller = Join-Path $root 'frontend/Diten.Web/Controllers/DocumentManagementMasterRegisterController.cs'
$view = Join-Path $root 'frontend/Diten.Web/Views/DocumentManagement/MasterRegister/CreateControlledDocument.cshtml'
$form = Join-Path $root 'frontend/Diten.Web/Views/DocumentManagement/MasterRegister/_CreateControlledDocumentForm.cshtml'
$script = Join-Path $root 'frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/MasterRegister/create-controlled-document.js'
$masterIndex = Join-Path $root 'frontend/Diten.Web/wwwroot/assets/js/DocumentManagement/MasterRegister/index.js'
$controlledIndex = Join-Path $root 'frontend/Diten.Web/Views/DocumentManagement/ControlledDocuments/Index.cshtml'
$controlledController = Join-Path $root 'frontend/Diten.Web/Controllers/DocumentManagementControlledDocumentsController.cs'
$resources = Join-Path $root 'frontend/Diten.Web/Resources/Views/DocumentManagement/MasterRegister'

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "FAIL: $message" }
    Write-Host "PASS: $message"
}

function Read-Text([string]$path) {
    Assert-True (Test-Path -LiteralPath $path) "exists: $($path.Substring($root.Length + 1))"
    return Get-Content -Raw -LiteralPath $path
}

$controllerText = Read-Text $controller
$viewText = Read-Text $view
$formText = Read-Text $form
$scriptText = Read-Text $script
$masterIndexText = Read-Text $masterIndex
$controlledIndexText = Read-Text $controlledIndex
$controlledControllerText = Read-Text $controlledController

Assert-True ($controllerText.Contains('[HttpGet("CreateControlledDocument")]')) 'unified Master Register page route is declared'
Assert-True ($controllerText.Contains('[HttpPost("/DocumentManagement/MasterRegister/api/controlled-document-registrations")]')) 'registration POST proxy is declared'
Assert-True ($controllerText.Contains('[HttpGet("/DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId:guid}")]')) 'operation GET proxy is declared'
Assert-True ($controllerText.Contains('[HttpPost("/DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId:guid}/retry")]')) 'retry POST proxy is declared'
Assert-True (($controllerText | Select-String -Pattern '\[ValidateAntiForgeryToken\]' -AllMatches).Matches.Count -ge 2) 'POST proxy actions use antiforgery'
Assert-True ($controllerText.Contains('IFormFile? initialFile')) 'MVC boundary accepts the initial file as IFormFile'
Assert-True ($controllerText.Contains('Path.GetFileName(initialFile.FileName)')) 'uploaded filename is normalized at the MVC boundary'

$fieldIds = @(
    'registrationTitle',
    'registrationClass',
    'registrationCriticality',
    'registrationType',
    'registrationDescription',
    'registrationTags',
    'registrationLanguage',
    'registrationOwnerFunction',
    'registrationOwnerCompany',
    'registrationProcessRole',
    'registrationProcessUser',
    'registrationReviewCycle',
    'registrationRetentionClass',
    'registrationCompany',
    'registrationFolder',
    'registrationFile'
)
foreach ($fieldId in $fieldIds) {
    Assert-True ($formText.Contains("id=`"$fieldId`"")) "unified form field: $fieldId"
}
Assert-True ($fieldIds.Count -eq 16) 'unified form contract contains exactly 16 governed user fields'
Assert-True (-not ($formText -match 'TenantId|PermanentUid|DocumentCode|EffectiveDate|Approval|ReleaseGate|Signature')) 'server-owned fields are absent from the unified form'
Assert-True ($viewText.Contains('platform.document-management.master-register.registration.create')) 'create permission snapshot is checked'
Assert-True ($viewText.Contains('platform.document-management.master-register.registration.reconcile')) 'retry permission snapshot is checked'

Assert-True ($scriptText.Contains('new FormData()')) 'browser submission uses FormData'
Assert-True ($scriptText.Contains("body.append('initialFile', file)")) 'browser sends raw File through multipart transport'
Assert-True (-not ($scriptText -match 'FileReader|readAsDataURL|contentBase64|ContentBase64|btoa\(')) 'browser script does not create or store base64 file content'
Assert-True ($scriptText.Contains("const completedStatus = 'COMPLETED'")) 'Completed is the explicit success status'
Assert-True ($scriptText.Contains('normalized === completedStatus')) 'success is gated by Completed status'
Assert-True ($scriptText.Contains('/retry')) 'retry action is wired by operation id'

Assert-True ($masterIndexText.Contains('/DocumentManagementMasterRegister/CreateControlledDocument')) 'Master Register Add action targets unified create'
Assert-True ($controlledIndexText.Contains('href="/DocumentManagementMasterRegister/CreateControlledDocument"')) 'Controlled Documents Add Document targets unified create'
Assert-True ($controlledIndexText.Contains('href="/DocumentManagementControlledDocuments/Create?kind=template"')) 'template create route remains unchanged'
Assert-True ($controlledControllerText.Contains('Redirect("/DocumentManagementMasterRegister/CreateControlledDocument")')) 'normal direct create route redirects to unified create'
Assert-True ($controlledControllerText.Contains('string.Equals(kind, "template"')) 'template controller flow remains available'

$languages = @('en', 'fr', 'es', 'zh', 'ar', 'ru', 'tr')
$requiredKeys = @(
    'UnifiedCreateTitle', 'CreateAndRegister', 'UnifiedCreatePermissionRequired', 'Tags', 'TagsHint',
    'InitialFile', 'InitialFileHint', 'RepositoryPlacement', 'LegalEntityCompany', 'RegistrationOperation',
    'RetryRegistration', 'RegistrationCompleted', 'RegistrationIncomplete', 'RegistrationRetryStarted',
    'OperationId', 'CorrelationId', 'SelectOption', 'NoFolders', 'NewControlledDocument',
    'RegistrationSuccessRequiresCompleteRelationship', 'RegistrationDoesNotAllocateIdentifiers',
    'RegistrationDoesNotApproveReleaseSignOrEffective'
)
$baselineKeys = $null
foreach ($language in $languages) {
    $path = Join-Path $resources "MasterRegisterIndex.$language.resx"
    [xml]$xml = Read-Text $path
    $keys = @($xml.root.data | ForEach-Object { $_.name } | Sort-Object)
    Assert-True (($keys | Group-Object | Where-Object Count -gt 1).Count -eq 0) "$language RESX contains no duplicate keys"
    foreach ($key in $requiredKeys) {
        Assert-True ($keys -contains $key) "$language RESX contains $key"
    }
    if ($null -eq $baselineKeys) {
        $baselineKeys = $keys
    } else {
        Assert-True ((Compare-Object $baselineKeys $keys).Count -eq 0) "$language RESX key set matches the baseline"
    }
}

Assert-True ($controllerText.Contains('$"{_gatewayUrl}{path}"')) 'registration proxy uses the configured Gateway transport'
Assert-True (-not (($viewText + $formText + $scriptText) -match '5057|ocelot\.json')) 'FU36B browser surface does not bypass Gateway or reference route configuration'
Write-Host 'MOD-0029-FU36B unified create frontend verifier: PASS'
