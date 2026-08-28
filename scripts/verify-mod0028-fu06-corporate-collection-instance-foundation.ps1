param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Message) {
    $fullPath = Join-Path $repoRoot $Path
    if (-not (Test-Path -LiteralPath $fullPath)) {
        $failures.Add("Missing file: $Path")
        return
    }
    if (-not (Select-String -LiteralPath $fullPath -Pattern $Pattern -Quiet)) {
        $failures.Add($Message)
    }
}

function Assert-NotContains([string]$Path, [string]$Pattern, [string]$Message) {
    $fullPath = Join-Path $repoRoot $Path
    if ((Test-Path -LiteralPath $fullPath) -and (Select-String -LiteralPath $fullPath -Pattern $Pattern -Quiet)) {
        $failures.Add($Message)
    }
}

$enum = 'services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/DocumentManagementEnums.cs'
$instance = 'services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/CollectionInstance.cs'
$feature = 'services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementCorporateCollectionInstances'
$controller = 'services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementCorporateCollectionInstancesController.cs'
$request = 'services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/CorporateCollectionInstanceRequests.cs'
$tests = 'services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentManagement/CorporateCollectionInstanceFoundationTests.cs'
$indexes = 'services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs'

Assert-Contains $enum 'Corporate\s*=\s*3' 'CollectionScopeType.Corporate is missing.'
Assert-Contains $instance 'ScopeOwnerId' 'CollectionInstance is not scope-aware.'
Assert-Contains $instance 'CorporateOwnerId' 'CollectionInstance has no CorporateOwnerId.'
Assert-Contains $instance 'CompanyId' 'Company compatibility field was removed.'
Assert-Contains "$feature/CorporateCollectionInstanceProvisioningService.cs" 'CollectionScopeType\.Corporate' 'Corporate provisioning service is missing.'
Assert-Contains $instance 'BsonIgnoreIfDefault' 'Default CompanyId must not be persisted for Corporate instances.'
Assert-NotContains "$feature/CorporateCollectionInstanceProvisioningService.cs" 'CompanyId\s*=' 'Corporate provisioning must not assign CompanyId.'
Assert-Contains "$feature/CorporateCollectionStoragePartitionBuilder.cs" 'tenant/\{_tenantContext\.TenantId:D\}/company/\{companyId:D\}/folder/\{folderId:D\}' 'Company partition literal changed.'
Assert-Contains "$feature/CorporateCollectionStoragePartitionBuilder.cs" 'tenant/\{_tenantContext\.TenantId:D\}/corporate/\{corporateOwnerId:D\}/folder/\{folderId:D\}' 'Corporate partition literal is missing.'
Assert-Contains "$feature/CorporateCollectionFolderAccessEvaluator.cs" 'matching\.Count > 0' 'Corporate access is not deny-by-default.'
Assert-NotContains "$feature/CorporateCollectionFolderAccessEvaluator.cs" 'BelongsToCompany' 'Company membership must not grant Corporate access.'
Assert-Contains $controller 'HttpPost\("provision"\)' 'Provision endpoint is missing.'
Assert-Contains $controller 'provisioning-operations/\{operationId:guid\}/retry' 'Retry endpoint is missing.'
Assert-NotContains $controller '\[HttpDelete' 'DELETE endpoint is prohibited.'
Assert-NotContains $request 'TenantId' 'Provision request must not accept TenantId.'
Assert-NotContains $request 'CompanyId' 'Corporate provision request must not accept CompanyId.'
Assert-Contains $indexes 'ux_dm_collection_instances_corporate_owner_baseline_node_active' 'Corporate uniqueness index is missing.'
Assert-Contains $indexes 'ux_dm_corporate_provisioning_tenant_idempotency_active' 'Provisioning idempotency index is missing.'
Assert-Contains $tests 'IdempotentReplay' 'Provisioning idempotency test is missing.'
Assert-Contains $tests 'deny_by_default' 'Corporate access deny-by-default test is missing.'
Assert-Contains $tests 'Partition_builder_pins_company_compatibility' 'Partition compatibility test is missing.'
Assert-NotContains "$feature/CorporateCollectionInstanceModels.cs" 'public\s+(byte\[\]|string)\s+PublicUrl' 'Raw bytes/public URL contract is prohibited.'

if (-not $SkipBuild) {
    & dotnet build (Join-Path $repoRoot 'services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj') -c Debug -o (Join-Path $repoRoot '.tmp/verify-mod0028-fu06-platform-api-build')
    if ($LASTEXITCODE -ne 0) {
        $failures.Add('Platform API build failed.')
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PASS: MOD-0028-FU06 Corporate Collection Instance Foundation verifier.'
