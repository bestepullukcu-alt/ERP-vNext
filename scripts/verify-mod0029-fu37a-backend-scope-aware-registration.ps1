param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Require([string]$RelativePath, [string]$Pattern, [string]$Message) {
    $path = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path) -or -not (Select-String -LiteralPath $path -Pattern $Pattern -Quiet)) {
        $failures.Add($Message)
    }
}

$enum = 'services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/ControlledDocumentRegistrationEnums.cs'
$document = 'services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/ControlledDocument.cs'
$operation = 'services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/ControlledDocumentRegistrationOperation.cs'
$request = 'services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/ControlledDocumentRegistrationApiRequests.cs'
$validator = 'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocumentRegistration/Validators/CreateControlledDocumentRegistrationValidator.cs'
$service = 'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocumentRegistration/Services/ControlledDocumentRegistrationService.cs'
$tests = 'services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentManagement/Fu37ScopeAwareRegistrationTests.cs'
$storage = 'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementCorporateCollectionInstances/CorporateCollectionStoragePartitionBuilder.cs'
$controller = 'services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementControlledDocumentRegistrationController.cs'

Require $enum 'enum DocumentScope' 'DocumentScope enum missing.'
Require $enum 'Company\s*=\s*0' 'DocumentScope.Company missing.'
Require $enum 'Corporate\s*=\s*1' 'DocumentScope.Corporate missing.'
foreach ($field in @('DocumentScope', 'ScopeOwnerId', 'CorporateOwnerId', 'CompanyId', 'OwnerCompanyId',
        'CollectionInstanceId', 'FolderId', 'StoragePartition', 'GovernanceOwnerFunction', 'GovernanceOwnerRole')) {
    Require $document $field "ControlledDocument field missing: $field"
}
foreach ($field in @('DocumentScope', 'ScopeOwnerId', 'CollectionInstanceId', 'FolderId', 'StoragePartition',
        'ScopeFingerprint', 'GoverningLanguageId', 'RetentionClassId')) {
    Require $operation $field "Operation snapshot field missing: $field"
}
Require $request 'DocumentScope' 'Create request DocumentScope missing.'
Require $request 'CorporateOwnerId' 'Create request CorporateOwnerId missing.'
Require $request 'FolderId' 'Create request FolderId missing.'
Require $validator 'DocumentScope == DocumentScope\.Corporate' 'Corporate conditional validation missing.'
Require $validator 'Corporate registration cannot specify CompanyId' 'Corporate CompanyId rejection missing.'
Require $validator 'Corporate registration cannot specify OwnerCompanyId' 'Corporate OwnerCompanyId rejection missing.'
Require $validator 'DocumentScope == DocumentScope\.Company' 'Company conditional validation missing.'
Require $service 'CaptureScopeSnapshot' 'Immutable operation scope snapshot missing.'
Require $service 'ScopeFingerprint' 'Idempotency scope fingerprint missing.'
Require $service 'CorporateCollectionFolderAccessEvaluator' 'Corporate access evaluator is not used.'
Require $service 'DocumentAccessMatrixAction\.CreateDocument' 'Corporate create-document action is not checked.'
Require $storage 'tenant/\{_tenantContext\.TenantId:D\}/company/\{companyId:D\}/folder/\{folderId:D\}' 'Company partition changed.'
Require $storage 'tenant/\{_tenantContext\.TenantId:D\}/corporate/\{corporateOwnerId:D\}/folder/\{folderId:D\}' 'Corporate partition missing.'
Require $tests 'Company_requires_company_owners' 'FU37A Company validation tests missing.'
Require $tests 'Corporate_requires_corporate_owner' 'FU37A Corporate validation tests missing.'
Require $tests 'Operation_scope_snapshot_is_set_once' 'FU37A retry immutability test missing.'

if (Select-String -LiteralPath (Join-Path $RepoRoot $request) -Pattern '\bTenantId\b' -Quiet) {
    $failures.Add('Client request exposes TenantId.')
}
if (Select-String -LiteralPath (Join-Path $RepoRoot $service) -Pattern 'ProvisionAsync|CorporateCollectionInstanceProvisioningService' -Quiet) {
    $failures.Add('FU37A attempts to provision a Corporate CollectionInstance.')
}
if (Select-String -LiteralPath (Join-Path $RepoRoot $controller) -Pattern 'HttpDelete' -Quiet) {
    $failures.Add('Registration DELETE endpoint found.')
}
if (Select-String -LiteralPath (Join-Path $RepoRoot $service) -Pattern 'AllocateUid|DocumentIdentifierAllocation|MarkEffective|ApproveAsync|SignAsync' -Quiet) {
    $failures.Add('Forbidden downstream automation found.')
}
if (Select-String -LiteralPath (Join-Path $RepoRoot $service) -Pattern 'DummyCompany|dummy company' -Quiet) {
    $failures.Add('Dummy CompanyId behavior found.')
}

$scoped = @($enum, $document, $operation, $request, $validator, $service, $tests,
    $storage, $controller,
    'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocumentRegistration/DocumentManagementControlledDocumentRegistrationModels.cs',
    'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocuments/Services/ICollectionInstanceReferenceReader.cs',
    'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocuments/Services/IContentStorageGateway.cs',
    'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocuments/Services/DocumentVersioningService.cs',
    'services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/CollectionInstanceReferenceReader.cs',
    'services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs',
    'scripts/verify-mod0029-fu37a-backend-scope-aware-registration.ps1')
& git -C $RepoRoot diff --check -- @scoped
if ($LASTEXITCODE -ne 0) { $failures.Add('Scoped git diff --check failed.') }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'PASS MOD-0029-FU37A backend scope-aware registration verifier'
