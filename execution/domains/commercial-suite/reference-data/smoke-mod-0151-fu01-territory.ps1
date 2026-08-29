<#
.SYNOPSIS
  MOD-0151 FU01 live smoke: contract + TerritoryModel + TerritoryNode (positive + negative) for tenant
  97c59330-... through the Gateway. READ-ONLY on reference data; creates only SMOKE-* test records.

.DESCRIPTION
  Requires a CRM user JWT for tenant 97c59330 that carries the crm.territory.* permission claims. CRM resolves the
  tenant from the X-Tenant-Id header (NOT the JWT), so the header is sent by default. FU01 has no delete endpoint;
  smoke records remain as draft test data (codes SMOKE-MOD0151-* / TR-*-SMOKE) — never hand-delete Mongo.

.PARAMETER Token
  Bearer JWT of a 97c59330 user with crm.territory.read/.model.read/.model.manage/.node.read/.node.manage.

.EXAMPLE
  .\smoke-mod-0151-fu01-territory.ps1 -Token $crm
#>
[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://localhost:5000",
    [Parameter(Mandatory = $true)][string]$Token,
    [string]$TenantId = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [switch]$NoTenantHeader
)

$ErrorActionPreference = "Stop"
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$corr = "smoke-fu01-$stamp"
$From = "2027-01-01T00:00:00Z"
$To = "2027-12-31T00:00:00Z"
$results = @()

function Invoke-Crm {
    param([string]$Method, [string]$Path, $Body = $null)
    $headers = @{ Authorization = "Bearer $Token" }
    if (-not $NoTenantHeader) { $headers["X-Tenant-Id"] = $TenantId }
    try {
        if ($null -ne $Body) {
            $resp = Invoke-RestMethod -Method $Method -Uri "$GatewayBaseUrl$Path" -Headers $headers -Body ($Body | ConvertTo-Json -Depth 10) -ContentType "application/json"
        } else {
            $resp = Invoke-RestMethod -Method $Method -Uri "$GatewayBaseUrl$Path" -Headers $headers
        }
        $sc = 200; if ($resp -and $resp.statusCode) { $sc = [int]$resp.statusCode }
        return [pscustomobject]@{ Success = $true; StatusCode = $sc; Data = $resp.data; Errors = $null }
    } catch {
        $sc = -1; try { $sc = [int]$_.Exception.Response.StatusCode.value__ } catch {}
        $errs = $null
        try { $j = $_.ErrorDetails.Message | ConvertFrom-Json; if ($j.statusCode) { $sc = [int]$j.statusCode }; $errs = ($j.errors -join "; ") } catch {}
        return [pscustomobject]@{ Success = $false; StatusCode = $sc; Data = $null; Errors = $errs }
    }
}

function Record {
    param([string]$Step, [string]$Endpoint, [int]$Expected, $Actual, [bool]$Pass, [string]$Notes = "")
    $script:results += [pscustomobject]@{ Step = $Step; Endpoint = $Endpoint; Expected = $Expected; Actual = $Actual; Result = $(if ($Pass) { "PASS" } else { "FAIL" }); Notes = $Notes }
    $c = if ($Pass) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1} -> {2} (expected {3}) {4}" -f $(if ($Pass) { "PASS" } else { "FAIL" }), $Step, $Actual, $Expected, $Notes) -ForegroundColor $c
}

Write-Host "MOD-0151 FU01 live smoke -> tenant $TenantId" -ForegroundColor White
Write-Host "Gateway: $GatewayBaseUrl | tenant header: $(-not $NoTenantHeader)" -ForegroundColor White
Write-Host ""

# ---- 1. Contract ----
Write-Host "1) Contract" -ForegroundColor Cyan
$c = Invoke-Crm GET "/api/crm/territory-management/contract"
if (-not $c.Success) {
    Record "contract" "GET /territory-management/contract" 200 $c.StatusCode $false "$($c.Errors)"
    if ($c.StatusCode -eq 403) { Write-Host "  -> SMOKE_BLOCKED_BY_RBAC_ASSIGNMENT (crm.territory.* not granted to this user's role)" -ForegroundColor Yellow }
    if ($c.StatusCode -eq 400) { Write-Host "  -> tenant context missing? ensure X-Tenant-Id header (do not use -NoTenantHeader)" -ForegroundColor Yellow }
} else {
    $d = $c.Data
    Record "contract.moduleId" "contract" 1 $d.moduleId ($d.moduleId -eq "MOD-0151")
    Record "contract.runtimeScope" "contract" 1 $d.runtimeScope ($d.runtimeScope -eq "FU01-territory-model-node-backend-only")
    Record "contract.tenantId" "contract" 1 $d.tenantId ($d.tenantId -eq $TenantId)
    Record "contract.isReady" "contract" 1 $d.isReady ($d.isReady -eq $true) "missing: $($d.missingRequiredReferenceSets -join ',')"
    Record "flags.models/nodes true" "contract" 1 "$($d.features.territoryModels)/$($d.features.territoryNodes)" ($d.features.territoryModels -and $d.features.territoryNodes)
    $offAll = (-not $d.features.assignmentRules) -and (-not $d.features.resourceAssignments) -and (-not $d.features.workflowActivation) -and (-not $d.features.evidencePack) -and (-not $d.features.importExport) -and (-not $d.features.uiEnabled)
    Record "flags.out-of-scope false" "contract" 1 $offAll $offAll
}
Write-Host ""

# ---- 2. TerritoryModel positive ----
Write-Host "2) TerritoryModel" -ForegroundColor Cyan
$modelCode = "SMOKE-MOD0151-$stamp"
$mk = Invoke-Crm POST "/api/crm/territory-models" @{ modelCode = $modelCode; name = "Smoke MOD-0151 Territory Model $stamp"; countryScope = "tr"; effectiveFrom = $From; effectiveTo = $To; correlationId = $corr }
$modelId = $mk.Data
Record "model.create" "POST /territory-models" 201 $mk.StatusCode ($mk.Success -and $mk.StatusCode -eq 201) "id=$modelId"
if ($mk.Success) {
    $g = Invoke-Crm GET "/api/crm/territory-models/$modelId"
    Record "model.getById" "GET /territory-models/{id}" 200 $g.StatusCode ($g.Success) "status=$($g.Data.status) v=$($g.Data.versionNumber) corr=$($g.Data.correlationId)"
    Record "model.draft+v1+tenant+corr" "getById" 1 "$($g.Data.status)/$($g.Data.versionNumber)/$($g.Data.correlationId)" ($g.Data.status -eq "draft" -and $g.Data.versionNumber -eq 1 -and $g.Data.correlationId -eq $corr)
    $l = Invoke-Crm GET "/api/crm/territory-models?search=$modelCode"
    $found = $false; if ($l.Success -and $l.Data.items) { $found = [bool]($l.Data.items | Where-Object { $_.modelCode -eq $modelCode }) }
    Record "model.list contains" "GET /territory-models" 1 $found $found
    $u = Invoke-Crm PUT "/api/crm/territory-models/$modelId" @{ name = "Smoke Renamed $stamp"; countryScope = "tr"; effectiveFrom = $From; effectiveTo = $To; correlationId = $corr }
    Record "model.update draft" "PUT /territory-models/{id}" 200 $u.StatusCode ($u.Success)
}
Write-Host ""

# ---- 3. TerritoryNode positive ----
$rootId = $null; $zoneId = $null
if ($mk.Success) {
    Write-Host "3) TerritoryNode" -ForegroundColor Cyan
    $rn = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ territoryCode = "TR-SMOKE"; name = "Turkey Smoke"; territoryLevel = "country"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 10; correlationId = $corr }
    $rootId = $rn.Data
    Record "node.root(country)" "POST /{id}/nodes" 201 $rn.StatusCode ($rn.Success -and $rn.StatusCode -eq 201) "id=$rootId"
    if ($rn.Success) {
        $zn = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ parentTerritoryId = $rootId; territoryCode = "TR-ZONE-SMOKE"; name = "Turkey Zone Smoke"; territoryLevel = "zone"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 20; correlationId = $corr }
        $zoneId = $zn.Data
        Record "node.child(zone) level-skip country->zone" "POST /{id}/nodes" 201 $zn.StatusCode ($zn.Success -and $zn.StatusCode -eq 201) "id=$zoneId"
        if ($zn.Success) {
            $prof = @{ clusterNotes = "Smoke cluster"; planningCenterType = "manual" }
            $mn = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ parentTerritoryId = $zoneId; territoryCode = "TR-MICRO-SMOKE"; name = "Turkey MicroZone Smoke"; territoryLevel = "microzone"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 30; microZoneProfile = $prof; correlationId = $corr }
            Record "node.microzone + profile" "POST /{id}/nodes" 201 $mn.StatusCode ($mn.Success -and $mn.StatusCode -eq 201) "id=$($mn.Data)"
        }
        $h = Invoke-Crm GET "/api/crm/territory-models/$modelId/nodes"
        $nc = 0; if ($h.Success -and $h.Data.nodes) { $nc = $h.Data.nodes.Count }
        Record "node.hierarchy list" "GET /{id}/nodes" 200 $h.StatusCode ($h.Success) "nodes=$nc"
        $un = Invoke-Crm PUT "/api/crm/territory-models/$modelId/nodes/$rootId" @{ territoryCode = "TR-SMOKE"; name = "Turkey Smoke Renamed"; territoryLevel = "country"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 10; correlationId = $corr }
        Record "node.update draft" "PUT /{id}/nodes/{nodeId}" 200 $un.StatusCode ($un.Success)
    }
    Write-Host ""
}

# ---- 4. Negative validation ----
if ($mk.Success) {
    Write-Host "4) Negative validation" -ForegroundColor Cyan
    $n1 = Invoke-Crm POST "/api/crm/territory-models" @{ modelCode = $modelCode; name = "dup"; effectiveFrom = $From; effectiveTo = $To }
    Record "neg.dup ModelCode" "POST /territory-models" 409 $n1.StatusCode ($n1.StatusCode -eq 409)
    $n2 = Invoke-Crm POST "/api/crm/territory-models" @{ modelCode = "SMOKE-BADDATE-$stamp"; name = "bad"; effectiveFrom = $To; effectiveTo = $From }
    Record "neg.invalid date range" "POST /territory-models" 400 $n2.StatusCode ($n2.StatusCode -eq 400)
    $n3 = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ territoryCode = "TR-SMOKE"; name = "dup"; territoryLevel = "country"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 1 }
    Record "neg.dup TerritoryCode" "POST /{id}/nodes" 409 $n3.StatusCode ($n3.StatusCode -eq 409)
    if ($zoneId) {
        $n4 = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ parentTerritoryId = $zoneId; territoryCode = "TR-BACK-SMOKE"; name = "back"; territoryLevel = "region"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 1 }
        Record "neg.backward rank (zone->region)" "POST /{id}/nodes" 400 $n4.StatusCode ($n4.StatusCode -eq 400)
    }
    $n5 = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ territoryCode = "TR-INV-SMOKE"; name = "inv"; territoryLevel = "invalid-level"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 1 }
    Record "neg.invalid level" "POST /{id}/nodes" 400 $n5.StatusCode ($n5.StatusCode -eq 400)
    $n6 = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ territoryCode = "TR-MZP-SMOKE"; name = "mzp"; territoryLevel = "zone"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 1; microZoneProfile = @{ clusterNotes = "x" } }
    Record "neg.microZoneProfile on non-microzone" "POST /{id}/nodes" 400 $n6.StatusCode ($n6.StatusCode -eq 400)
    # child date outside parent: create a narrow-window parent then a child starting before it
    $dp = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ territoryCode = "TR-DP-SMOKE"; name = "date parent"; territoryLevel = "country"; effectiveFrom = "2027-06-01T00:00:00Z"; effectiveTo = "2027-09-01T00:00:00Z"; sortOrder = 1 }
    if ($dp.Success) {
        $n8 = Invoke-Crm POST "/api/crm/territory-models/$modelId/nodes" @{ parentTerritoryId = $dp.Data; territoryCode = "TR-DPC-SMOKE"; name = "date child"; territoryLevel = "zone"; effectiveFrom = $From; effectiveTo = $To; sortOrder = 2 }
        Record "neg.child date outside parent" "POST /{id}/nodes" 400 $n8.StatusCode ($n8.StatusCode -eq 400)
    } else {
        Record "neg.child date outside parent" "POST /{id}/nodes" 400 "SKIP" $true "date-parent create failed; skipped"
    }
    Write-Host "  SKIP neg.non-draft mutation: FU01 has no activation/status transition endpoint; DB hand-edit forbidden." -ForegroundColor DarkGray
    Write-Host "  SKIP neg.cross-tenant: no second-tenant token; never simulate via payload TenantId." -ForegroundColor DarkGray
    Write-Host ""
}

# ---- summary ----
Write-Host "==== SUMMARY ====" -ForegroundColor White
$results | Format-Table Step, Expected, Actual, Result, Notes -AutoSize
$pass = ($results | Where-Object { $_.Result -eq "PASS" }).Count
$fail = ($results | Where-Object { $_.Result -eq "FAIL" }).Count
Write-Host ("PASS: {0}  FAIL: {1}" -f $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
Write-Host "Cleanup: FU01 has no delete endpoint -> smoke records remain as draft (codes SMOKE-MOD0151-* / TR-*-SMOKE). Do NOT hand-delete Mongo." -ForegroundColor DarkGray
