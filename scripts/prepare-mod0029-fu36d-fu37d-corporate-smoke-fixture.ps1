param(
    [Parameter(Mandatory = $true)]
    [SecureString]$AdministratorAccessToken,
    [Parameter(Mandatory = $true)]
    [Guid]$BaselineReleaseId,
    [Parameter(Mandatory = $true)]
    [Guid]$CorporateOwnerId,
    [Parameter(Mandatory = $true)]
    [Guid]$SmokeUserId,
    [string]$GatewayUrl = 'http://localhost:5000'
)

$ErrorActionPreference = 'Stop'
$token = [System.Net.NetworkCredential]::new('', $AdministratorAccessToken).Password
$headers = @{ Authorization = "Bearer $token" }
$jsonHeaders = $headers + @{ 'Content-Type' = 'application/json' }
$idempotencyKey = "mod0029-runtime-smoke:$BaselineReleaseId`:$CorporateOwnerId"

$provisionBody = @{
    baselineReleaseId = $BaselineReleaseId
    corporateOwnerId = $CorporateOwnerId
    idempotencyKey = $idempotencyKey
    displayName = 'FU36D/FU37D Corporate Runtime Smoke'
    description = 'Tenant-scoped, additive runtime smoke fixture. Retained for audit; no hard delete.'
} | ConvertTo-Json

$provision = Invoke-RestMethod `
    -Uri "$GatewayUrl/api/v1/document-management/corporate-collection-instances/provision" `
    -Headers $jsonHeaders `
    -Method Post `
    -Body $provisionBody

if (-not $provision.isSuccessful -or $provision.data.status -ne 'COMPLETED') {
    throw "Corporate provisioning did not complete. CorrelationId=$($provision.correlation_id)"
}

$instances = Invoke-RestMethod `
    -Uri "$GatewayUrl/api/v1/document-management/corporate-collection-instances?baselineReleaseId=$BaselineReleaseId&corporateOwnerId=$CorporateOwnerId" `
    -Headers $headers `
    -Method Get

if (-not $instances.isSuccessful -or @($instances.data).Count -eq 0) {
    throw 'Provisioned corporate collection instances could not be resolved for governed grant preparation.'
}

foreach ($instance in @($instances.data)) {
    $grantBody = @{
        targetType = 'CollectionInstance'
        targetId = $instance.id
        principalType = 'User'
        principalId = $SmokeUserId
        actions = @('View', 'CreateDocument')
        effect = 'Allow'
        inheritFromParent = $true
        status = 'Active'
        reason = 'FU36D/FU37D tenant-scoped corporate runtime smoke grant'
    } | ConvertTo-Json

    Invoke-RestMethod `
        -Uri "$GatewayUrl/api/v1/document-management/access-policies" `
        -Headers $jsonHeaders `
        -Method Post `
        -Body $grantBody | Out-Null
}

$token = $null
[pscustomobject]@{
    OperationId = $provision.data.operationId
    RootCollectionInstanceId = $provision.data.collectionInstanceId
    GrantedInstanceCount = @($instances.data).Count
    SmokeUserId = $SmokeUserId
    Status = 'READY'
} | Format-List
