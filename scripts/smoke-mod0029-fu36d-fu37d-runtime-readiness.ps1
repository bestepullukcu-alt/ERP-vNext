param(
    [Parameter(Mandatory = $true)]
    [SecureString]$AccessToken,
    [string]$GatewayUrl = 'http://localhost:5000',
    [string]$RetentionSetCode = 'qms-document-retention'
)

$ErrorActionPreference = 'Stop'
$token = [System.Net.NetworkCredential]::new('', $AccessToken).Password
$headers = @{ Authorization = "Bearer $token" }

function Probe([string]$Name, [string]$Path, [int[]]$AllowedStatus = @(200)) {
    try {
        $response = Invoke-WebRequest -Uri "$GatewayUrl$Path" -Headers $headers -Method Get -UseBasicParsing
        $status = [int]$response.StatusCode
        if ($status -notin $AllowedStatus) {
            throw "$Name returned unexpected HTTP $status."
        }

        $body = $response.Content | ConvertFrom-Json
        $count = if ($null -ne $body.data -and $body.data -is [System.Array]) { $body.data.Count } else { $null }
        [pscustomobject]@{ Probe = $Name; Status = $status; Count = $count; Result = 'PASS' }
    }
    catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        if ($status -in $AllowedStatus) {
            [pscustomobject]@{ Probe = $Name; Status = $status; Count = $null; Result = 'CONTROLLED' }
            return
        }

        [pscustomobject]@{ Probe = $Name; Status = $status; Count = $null; Result = 'FAIL' }
        $script:failed = $true
    }
}

$failed = $false
$results = @(
    Probe 'Governed languages' '/api/v1/document-management/controlled-document-registrations/governed-languages'
    Probe 'Retention class' "/api/v1/reference-data/sets/$([Uri]::EscapeDataString($RetentionSetCode))/published-values" @(200, 404)
    Probe 'Controlled documents list' '/api/v1/document-management/controlled-documents'
    Probe 'Corporate collection instances' '/api/v1/document-management/corporate-collection-instances'
)

$nodeMajor = 0
try {
    $nodeVersion = (& node --version 2>$null).TrimStart('v')
    $nodeMajor = [int]($nodeVersion.Split('.')[0])
}
catch {
    $nodeVersion = 'unavailable'
}

$results | Format-Table -AutoSize
if ($nodeMajor -lt 22) {
    Write-Warning "Browser automation unavailable: Node $nodeVersion; HTTP readiness probes remain authoritative for this fallback run."
}

$token = $null
if ($failed) {
    throw 'One or more runtime readiness probes failed.'
}

