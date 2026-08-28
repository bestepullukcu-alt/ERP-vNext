<#
.SYNOPSIS
  MOD-0151 published-values aggregate smoke for tenant 97c59330 (READ-ONLY). Verifies the 10 required + 2 optional
  territory reference sets are published with the correct value counts, string metadata and key attribute rules.

.DESCRIPTION
  Logs in as the given operator WITH the X-Tenant-Id header (tenant is resolved from the header, not the JWT), then
  reads published-values for each set through the Gateway. No writes, no publish, no SoD interaction. Expected values
  come from mod-0151-territory-reference-values.json (F1 template).

.EXAMPLE
  .\smoke-mod-0151-territory-publishedvalues.ps1 -Email you@corp.com -Password 'secret'
#>
[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:5000",
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$TenantId = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [string]$DataFile = "$PSScriptRoot\mod-0151-territory-reference-values.json"
)
$ErrorActionPreference = "Stop"

$login = Invoke-RestMethod -Method POST -Uri "$GatewayBaseUrl/api/tenant-auth/login" -ContentType "application/json" -Headers @{ "X-Tenant-Id" = $TenantId } -Body (@{ email = $Email; password = $Password; rememberMe = $true } | ConvertTo-Json)
$token = $login.data.accessToken
if (-not $token) { throw "Login returned no token (requiresMfa=$($login.data.requiresMfa))." }
$h = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
$data = Get-Content $DataFile -Raw | ConvertFrom-Json

function Get-Items($setCode) {
    try { $r = Invoke-RestMethod -Method GET -Uri "$GatewayBaseUrl/api/v1/reference-data/sets/$setCode/published-values?scope_key=$TenantId" -Headers $h; return @($r.data.items) }
    catch { return $null }
}

$rows = @(); $totExp = 0; $totAct = 0
foreach ($set in ($data.sets | Sort-Object order)) {
    $exp = $set.values.Count; $totExp += $exp
    $items = Get-Items $set.setCode
    if ($null -eq $items) { $rows += [pscustomobject]@{ Set = $set.setCode; Exp = $exp; Act = "NOPUB"; Meta = "-"; Result = "FAIL" }; continue }
    $codes = @($items | ForEach-Object { $_.code }); $act = $codes.Count; $totAct += $act
    $dupe = @($codes | Group-Object | Where-Object { $_.Count -gt 1 }).Count
    $kebab = @($codes | Where-Object { $_ -cnotmatch '^[a-z][a-z0-9-]*$' }).Count
    $allStr = $true; foreach ($it in $items) { if ($it.attributes) { foreach ($pp in $it.attributes.PSObject.Properties) { if ($pp.Value -isnot [string]) { $allStr = $false } } } }
    # per-set metadata rules
    $meta = "ok"
    if ($set.setCode -eq "territory-level") {
        $ranks = $items | Sort-Object sortOrder | ForEach-Object { [int]$_.attributes.rank }
        $mono = $true; for ($i = 1; $i -lt $ranks.Count; $i++) { if ($ranks[$i] -le $ranks[$i - 1]) { $mono = $false } }
        if (-not $mono -or ($ranks -join ',') -ne "10,20,30,40,50,60") { $meta = "rank!=10..60" }
    }
    if ($set.setCode -eq "business-scope-type") {
        $os = $items | Where-Object { $_.code -eq "operational-scope" }
        $ns = $items | Where-Object { $_.code -eq "non-sales-resource-planning" }
        if ($os.attributes.isSalesScopeDefault -ne "false" -or $ns.attributes.isSalesScopeDefault -ne "false") { $meta = "op/non-sales not false" }
    }
    if ($set.setCode -eq "territory-resource-role") {
        $covItems = Get-Items "territory-coverage-scope"
        $covCodes = @($covItems | ForEach-Object { $_.code })
        $bad = @($items | Where-Object { $covCodes -notcontains $_.attributes.defaultCoverageScope })
        if ($bad.Count -gt 0) { $meta = "coverage xref fail" }
    }
    $ok = ($act -eq $exp) -and ($dupe -eq 0) -and ($kebab -eq 0) -and $allStr -and ($meta -eq "ok")
    $rows += [pscustomobject]@{ Set = $set.setCode; Exp = $exp; Act = $act; Meta = $meta; Result = $(if ($ok) { "PASS" } else { "FAIL" }) }
}

Write-Host "MOD-0151 published-values smoke -> tenant $TenantId" -ForegroundColor White
$rows | Format-Table -AutoSize
Write-Host ("TOTAL expected=$totExp actual=$totAct (required 62 + optional 11 = 73)") -ForegroundColor White

# negative: these must NOT be published
Write-Host "`nMust-NOT-be-published:" -ForegroundColor White
foreach ($neg in @("product-portfolio", "brand-group", "commercial-role-scope-policy", "micro-zone")) {
    $n = Get-Items $neg
    $state = if ($null -eq $n) { "not published (OK)" } else { "PUBLISHED ($($n.Count)) -> REVIEW" }
    Write-Host ("  {0}: {1}" -f $neg, $state) -ForegroundColor $(if ($null -eq $n) { "Green" } else { "Yellow" })
}
$fail = @($rows | Where-Object { $_.Result -eq "FAIL" }).Count
Write-Host ("`nRESULT: {0}" -f $(if ($fail -eq 0 -and $totAct -eq 73) { "PUBLISHED_VALUES_READY" } else { "SMOKE_INCOMPLETE ($fail fail)" })) -ForegroundColor $(if ($fail -eq 0 -and $totAct -eq 73) { "Green" } else { "Red" })
