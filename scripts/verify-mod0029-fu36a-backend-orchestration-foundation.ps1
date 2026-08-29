param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()

function Require-File([string]$RelativePath) {
    if (-not (Test-Path (Join-Path $RepoRoot $RelativePath))) {
        $failures.Add("Missing file: $RelativePath")
    }
}

function Require-Text([string]$RelativePath, [string]$Pattern, [string]$Label) {
    $path = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path $path) -or -not (Select-String -Path $path -Pattern $Pattern -Quiet)) {
        $failures.Add("$Label ($RelativePath)")
    }
}

$entity = "services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/ControlledDocumentRegistrationOperation.cs"
$enum = "services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/ControlledDocumentRegistrationEnums.cs"
$repo = "services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IControlledDocumentRegistrationRepository.cs"
$mongoRepo = "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/ControlledDocumentRegistrationRepository.cs"
$indexes = "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs"
$controller = "services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementControlledDocumentRegistrationController.cs"
$request = "services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/ControlledDocumentRegistrationApiRequests.cs"
$service = "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocumentRegistration/Services/ControlledDocumentRegistrationService.cs"
$seeder = "services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs"

@($entity, $enum, $repo, $mongoRepo, $indexes, $controller, $request, $service) | ForEach-Object { Require-File $_ }

Require-Text $entity "TenantScopedEntity" "Entity is not tenant-scoped"
Require-Text $entity "IdempotencyKey" "Idempotency key missing"
Require-Text $enum "CompensationPending" "Compensation state missing"
Require-Text $mongoRepo "ExecutionFilter" "Repository tenant/soft-delete filter missing"
Require-Text $indexes "ux_dm_registration_tenant_idempotency_active" "Idempotency unique index missing"
Require-Text $indexes "ux_dm_registration_tenant_document_active" "ControlledDocument unique index missing"
Require-Text $indexes "ux_dm_registration_tenant_register_active" "MasterRegister unique index missing"
Require-Text $controller 'HttpPost\("controlled-document-registrations"\)' "Create endpoint missing"
Require-Text $controller 'HttpGet\("controlled-document-registrations/\{operationId:guid\}"\)' "Operation endpoint missing"
Require-Text $controller 'HttpPost\("controlled-document-registrations/\{operationId:guid\}/retry"\)' "Retry endpoint missing"
Require-Text $controller 'HttpGet\("controlled-documents/\{controlledDocumentId:guid\}/master-register"\)' "Reverse lookup endpoint missing"

if (Select-String -Path (Join-Path $RepoRoot $controller) -Pattern "HttpDelete|DeleteAsync" -Quiet) {
    $failures.Add("DELETE behavior found in registration controller")
}
if (Select-String -Path (Join-Path $RepoRoot $request) -Pattern "\bTenantId\b|\bEffectiveDate\b|\bLifecycleStatus\b|\bPermanentUid\b|\bDocumentCode\b" -Quiet) {
    $failures.Add("Server-owned field exposed by registration request DTO")
}
if (Select-String -Path (Join-Path $RepoRoot $entity) -Pattern "byte\[\]|ContentBase64|PublicUrl|DownloadUrl" -Quiet) {
    $failures.Add("Raw bytes or public URL field found in operation entity")
}
if (Select-String -Path (Join-Path $RepoRoot $service) -Pattern "DocumentIdentifierAllocation|AllocateUid|MarkEffective|ApproveAsync|SignAsync" -Quiet) {
    $failures.Add("Forbidden UID/effective/approve/sign automation found")
}

@("view", "create", "reconcile") | ForEach-Object {
    Require-Text $seeder ('"platform", "document-management\.master-register\.registration", "' + $_ + '"') "Permission key missing: $_"
}

$ocelot = Join-Path $RepoRoot "gateway/Diten.ApiGateway/ocelot.json"
if (-not (Select-String -Path $ocelot -Pattern '"/api/v1/document-management/\{everything\}"' -Quiet)) {
    $failures.Add("Existing document-management catch-all route missing")
}
if (Select-String -Path $ocelot -Pattern "controlled-document-registrations" -Quiet) {
    $failures.Add("Unexpected FU36-specific Ocelot route found")
}

$frontendFu36 = Get-ChildItem (Join-Path $RepoRoot "frontend") -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "ControlledDocumentRegistration|fu36" }
if ($frontendFu36) {
    $failures.Add("Unexpected FU36 frontend runtime artifact found")
}

$scoped = @(
    "services/Diten.Platform/src/Diten.Platform.API",
    "services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementControlledDocumentRegistration",
    "services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/ControlledDocumentRegistrationOperation.cs",
    "services/Diten.Platform/src/Diten.Platform.Domain/Enums/DocumentManagement/ControlledDocumentRegistrationEnums.cs",
    "services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IControlledDocumentRegistrationRepository.cs",
    "services/Diten.Platform/src/Diten.Platform.Infrastructure",
    "services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs",
    "services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Authorization/Mod0029Fu36RegistrationPermissionSeedTests.cs",
    "services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentManagement/ControlledDocumentRegistrationFoundationTests.cs",
    "gateway/Diten.ApiGateway.Tests/Mod0029Fu36RegistrationRouteCoverageTests.cs",
    "scripts/verify-mod0029-fu36a-backend-orchestration-foundation.ps1"
)
& git -C $RepoRoot diff --check -- @scoped
if ($LASTEXITCODE -ne 0) { $failures.Add("Scoped git diff --check failed") }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "PASS MOD-0029-FU36A backend orchestration foundation verifier"
