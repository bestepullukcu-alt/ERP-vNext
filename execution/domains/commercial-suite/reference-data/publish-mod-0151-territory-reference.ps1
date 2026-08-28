<#
.SYNOPSIS
  MOD-0151 Territory reference-set publish driver (MOD-0048 maker-checker flow) for tenant 97c59330-....

.DESCRIPTION
  Reads mod-0151-territory-reference-values.json and drives the governed publish flow through the Gateway:
    ensure set -> ensure draft version -> replace values -> validate -> submit   (MAKER token)
    -> approve -> publish -> published-values smoke                              (CHECKER token, != maker)

  It NEVER hand-edits Mongo, seeds locally, or bypasses SoD. Approve requires a CHECKER token that differs from
  the maker (the platform enforces `sod_submitter_cannot_approve` in code). Attributes are sent as STRINGS
  (MOD-0048 attributes = Dictionary<string,string>).

.PARAMETER MakerToken
  Bearer JWT of a user with Platform.BusinessReferenceData.Create/.Version.Create/.Update/.Validate/.Submit
  for tenant 97c59330-dbc4-4665-b29c-0c26dbb5cc93.

.PARAMETER CheckerToken
  Bearer JWT of a DIFFERENT user with Platform.BusinessReferenceData.Version.Approve/.Publish/.Consumer.Read.
  Omit it (or pass -StopBeforeApprove) to run only the maker steps and approve+publish manually.

.EXAMPLE
  # Full flow (you provide both tokens):
  .\publish-mod-0151-territory-reference.ps1 -MakerToken $maker -CheckerToken $checker

.EXAMPLE
  # Only add the data + submit; you approve+publish manually afterwards:
  .\publish-mod-0151-territory-reference.ps1 -MakerToken $maker -StopBeforeApprove

.EXAMPLE
  # Single set, useful for retries:
  .\publish-mod-0151-territory-reference.ps1 -MakerToken $maker -CheckerToken $checker -OnlySet territory-level
#>
[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:5000",
    [Parameter(Mandatory = $true)][string]$MakerToken,
    [string]$CheckerToken = "",
    [string]$DataFile = "$PSScriptRoot\mod-0151-territory-reference-values.json",
    [string]$TenantId = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [switch]$StopBeforeApprove,
    [switch]$SkipOptional,
    [switch]$SendTenantHeader,
    [string]$OnlySet = ""
)

$ErrorActionPreference = "Stop"
$idemStamp = (Get-Date -Format "yyyyMMdd")

function Invoke-Brd {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token,
        $Body = $null,
        [string]$IdempotencyKey = ""
    )
    $headers = @{ Authorization = "Bearer $Token" }
    if ($SendTenantHeader) { $headers["X-Tenant-Id"] = $TenantId }
    if ($IdempotencyKey) { $headers["Idempotency-Key"] = $IdempotencyKey }
    $uri = "$GatewayBaseUrl$Path"
    try {
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 12 -Compress
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -Body $json -ContentType "application/json"
        }
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }
    catch {
        $detail = ""
        try { $detail = $_.ErrorDetails.Message } catch {}
        if (-not $detail -and $_.Exception.Response) {
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $detail = $sr.ReadToEnd()
            } catch {}
        }
        throw ("HTTP {0} {1} FAILED: {2} | {3}" -f $Method, $Path, $_.Exception.Message, $detail)
    }
}

function Get-SetId {
    param($set)
    $list = Invoke-Brd -Method GET -Path "/api/v1/reference-data/sets?search=$($set.setCode)&scope_type=tenant&page_size=50" -Token $MakerToken
    $existing = $null
    if ($list.data -and $list.data.items) {
        $existing = $list.data.items | Where-Object { $_.setCode -eq $set.setCode } | Select-Object -First 1
    }
    if ($existing) {
        Write-Host "    set exists -> $($existing.setId)" -ForegroundColor DarkGray
        return $existing.setId
    }
    $body = @{ set_code = $set.setCode; name = $set.name; scope_type = "tenant"; description = $set.description; status = "Active" }
    $resp = Invoke-Brd -Method POST -Path "/api/v1/reference-data/sets" -Token $MakerToken -Body $body
    Write-Host "    set created -> $($resp.data.setId)" -ForegroundColor DarkGray
    return $resp.data.setId
}

function Get-DraftVersionId {
    param([string]$setId)
    $detail = Invoke-Brd -Method GET -Path "/api/v1/reference-data/sets/$setId" -Token $MakerToken
    if ($detail.data.activeDraftVersionId) {
        Write-Host "    draft version exists -> $($detail.data.activeDraftVersionId)" -ForegroundColor DarkGray
        return $detail.data.activeDraftVersionId
    }
    $resp = Invoke-Brd -Method POST -Path "/api/v1/reference-data/sets/$setId/versions" -Token $MakerToken -Body @{}
    Write-Host "    version created -> $($resp.data.versionId)" -ForegroundColor DarkGray
    return $resp.data.versionId
}

function Convert-Values {
    param($set)
    $out = @()
    foreach ($v in $set.values) {
        $attrs = @{}
        foreach ($p in $v.attributes.PSObject.Properties) { $attrs[$p.Name] = [string]$p.Value }
        $out += @{ code = $v.code; label = $v.label; is_active = $true; sort_order = [int]$v.sortOrder; attributes = $attrs }
    }
    return , $out
}

function Publish-Set {
    param($set)
    $idem = "mod-0151-territory-$($set.setCode)-$TenantId-$idemStamp-v1"
    Write-Host "[$($set.order)] $($set.setCode) ($($set.values.Count) values)" -ForegroundColor Cyan

    $setId = Get-SetId -set $set
    $versionId = Get-DraftVersionId -setId $setId

    # MAKER: replace values (string attributes), validate, submit
    $values = Convert-Values -set $set
    Invoke-Brd -Method PUT -Path "/api/v1/reference-data/versions/$versionId/values" -Token $MakerToken -Body @{ values = $values } | Out-Null
    Write-Host "    values written ($($values.Count))" -ForegroundColor DarkGray
    Invoke-Brd -Method POST -Path "/api/v1/reference-data/versions/$versionId/validate" -Token $MakerToken | Out-Null
    Write-Host "    validated" -ForegroundColor DarkGray
    Invoke-Brd -Method POST -Path "/api/v1/reference-data/versions/$versionId/submit" -Token $MakerToken -Body @{} | Out-Null
    Write-Host "    submitted (maker)" -ForegroundColor Green

    if ($StopBeforeApprove -or -not $CheckerToken) {
        Write-Host "    STOP: approve+publish left to operator (versionId=$versionId, idempotency-key=$idem)" -ForegroundColor Yellow
        return [pscustomobject]@{ SetCode = $set.setCode; VersionId = $versionId; Expected = $set.values.Count; Actual = "-"; Idem = $idem; Status = "SUBMITTED (awaiting checker)" }
    }

    # CHECKER (!= maker): approve, publish
    Invoke-Brd -Method POST -Path "/api/v1/reference-data/versions/$versionId/approve" -Token $CheckerToken -Body @{ decision = "approve"; comment = "MOD-0151 territory reference publish" } -IdempotencyKey $idem | Out-Null
    Write-Host "    approved (checker)" -ForegroundColor Green
    Invoke-Brd -Method POST -Path "/api/v1/reference-data/versions/$versionId/publish" -Token $CheckerToken -Body @{ publish_mode = "Immediate" } -IdempotencyKey $idem | Out-Null
    Write-Host "    published" -ForegroundColor Green

    # SMOKE (tenant-scoped published-values)
    $smokeToken = $CheckerToken
    $pv = Invoke-Brd -Method GET -Path "/api/v1/reference-data/sets/$($set.setCode)/published-values?scope_key=$TenantId" -Token $smokeToken
    $items = @()
    if ($pv.data -and $pv.data.items) { $items = $pv.data.items } elseif ($pv.items) { $items = $pv.items } elseif ($pv.data) { $items = $pv.data }
    $actual = ($items | Measure-Object).Count
    $ok = ($actual -eq $set.values.Count)
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host "    smoke: expected $($set.values.Count), actual $actual" -ForegroundColor $color
    return [pscustomobject]@{ SetCode = $set.setCode; VersionId = $versionId; Expected = $set.values.Count; Actual = $actual; Idem = $idem; Status = $(if ($ok) { "PUBLISHED+SMOKE_PASS" } else { "PUBLISHED_SMOKE_MISMATCH" }) }
}

# ---- main ----
if (-not (Test-Path $DataFile)) { throw "Data file not found: $DataFile" }
$data = Get-Content $DataFile -Raw | ConvertFrom-Json
if ($data.tenantId -ne $TenantId) {
    Write-Host "WARNING: data.tenantId ($($data.tenantId)) != -TenantId ($TenantId). Tokens must belong to -TenantId." -ForegroundColor Yellow
}

$sets = $data.sets | Sort-Object order
if ($SkipOptional) { $sets = $sets | Where-Object { $_.required } }
if ($OnlySet) { $sets = $sets | Where-Object { $_.setCode -eq $OnlySet } }

Write-Host "MOD-0151 territory reference publish -> tenant $TenantId (scope_type=tenant)" -ForegroundColor White
Write-Host "Gateway: $GatewayBaseUrl | sets: $($sets.Count) | mode: $(if ($StopBeforeApprove -or -not $CheckerToken) { 'MAKER-ONLY (submit; you approve+publish)' } else { 'FULL maker-checker' })" -ForegroundColor White
Write-Host ""

$results = @()
foreach ($set in $sets) {
    try { $results += Publish-Set -set $set }
    catch {
        Write-Host "    ERROR: $($_.Exception.Message)" -ForegroundColor Red
        $results += [pscustomobject]@{ SetCode = $set.setCode; VersionId = "-"; Expected = $set.values.Count; Actual = "-"; Idem = "-"; Status = "FAILED" }
    }
    Write-Host ""
}

Write-Host "==== SUMMARY ====" -ForegroundColor White
$results | Format-Table SetCode, Expected, Actual, Status, VersionId -AutoSize
$totalExpected = ($sets | ForEach-Object { $_.values.Count } | Measure-Object -Sum).Sum
Write-Host "Total expected values: $totalExpected (required 64 + optional 11 = 75 when all 12 sets run)" -ForegroundColor White
Write-Host "Reminder: SoD requires MakerToken != CheckerToken. Never bypass with publish-override." -ForegroundColor DarkGray
