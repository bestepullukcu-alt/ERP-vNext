<#
  MOD-0151-FU09B — Frequency Provider Integration for Route Candidate Readiness (run this YOURSELF).

  Same secure-credential model as the FU03 smoke: login uses Get-Credential (credential stays in YOUR process
  memory only), the Authorization header is never printed, nothing secret is written to disk. All business calls go
  through the Gateway (5000). Paste the printed RESULTS / FAILURES back to finalize the evidence report.

  Usage (repo root, PowerShell):
      ./scripts/smoke-mod0151-fu09b-frequency-provider-route-candidate.ps1
#>
[CmdletBinding()]
param(
    [string]$BaseUrl     = "http://localhost:5000",
    [string]$TenantId    = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [string]$AccountId   = "25464183-95d0-4bae-bf26-9dbe79d56063",
    [string]$ResourceId  = "fu04b-mehmet-20260731225851",
    [string]$BusinessUnit= "gamma",
    [string]$EffectiveAt = "2026-08-11T09:00:00Z",
    [string]$Date        = "2026-08-11",
    [string]$Weekday     = "tuesday"
)

$ErrorActionPreference = "Stop"
$results = [System.Collections.Generic.List[object]]::new()
function Add-Result([string]$Step, $Expected, $Actual, [bool]$Pass) {
    $results.Add([pscustomobject]@{ Step = $Step; Result = $(if ($Pass) { "PASS" } else { "FAIL" }); Expected = "$Expected"; Actual = "$Actual" })
}
# PS 5.1-compatible status probe (no -SkipHttpErrorCheck).
function Status([string]$Url, [string]$Method = "GET", $Headers = $null) {
    try {
        $p = @{ Uri = $Url; Method = $Method; TimeoutSec = 15; UseBasicParsing = $true }
        if ($Headers) { $p.Headers = $Headers }
        return [int](Invoke-WebRequest @p).StatusCode
    } catch { $r = $_.Exception.Response; if ($r -and $r.StatusCode) { return [int]$r.StatusCode } return -1 }
}

$vfp   = "$BaseUrl/api/crm/visit-frequency-policies"
$route = "$BaseUrl/api/crm/territory-management/readiness/route-candidates"
$run   = (Get-Date -Format "yyyyMMddHHmmss")

Write-Host "== MOD-0151-FU09B frequency provider route-candidate smoke ($run) ==" -ForegroundColor Cyan

# ---------------- Login ----------------
$cred = Get-Credential -Message "Tenant $TenantId operator login (email as username)"
$loginBody = @{ email = $cred.UserName; password = $cred.GetNetworkCredential().Password; rememberMe = $false } | ConvertTo-Json
$token = $null
try { $token = (Invoke-RestMethod -Uri "$BaseUrl/api/tenant-auth/login" -Method POST -Headers @{ "X-Tenant-Id" = $TenantId } -ContentType "application/json" -Body $loginBody -TimeoutSec 20).data.accessToken } catch {}
$loginBody = $null; $cred = $null
if (-not $token) { Add-Result "Gateway login" "200 + token" "no token" $false; $results | Format-Table -AutoSize; return }
Add-Result "Gateway login" "200 + token" "200 (MASKED)" $true

$claimPayload = ($token.Split('.')[1]).Replace('-','+').Replace('_','/'); switch ($claimPayload.Length % 4) { 2 { $claimPayload += '==' } 3 { $claimPayload += '=' } }
$tenantClaim = ([System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($claimPayload)) | ConvertFrom-Json).tenant_id
Add-Result "Tenant claim == target" $TenantId $tenantClaim ($tenantClaim -eq $TenantId)

$auth = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
function Get-Json([string]$Url) { Invoke-RestMethod -Uri $Url -Method GET -Headers $auth -TimeoutSec 25 }
function Post-Json([string]$Url, $Obj) { Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 6) -TimeoutSec 25 }

# ---------------- Contract flags ----------------
$c = (Get-Json "$BaseUrl/api/crm/territory-management/contract").data.features
Add-Result "supportsFrequencyProviderIntegration true" "true" $c.supportsFrequencyProviderIntegration ($c.supportsFrequencyProviderIntegration -eq $true)
$fu09aOk = $c.supportsVisitRouteReadiness -and $c.supportsRouteCandidateReadiness -and $c.supportsContactAvailabilityInputBoundary -and $c.supportsVisitFrequencyInputBoundary -and ($c.supportsWorkflowActivation -eq $false)
Add-Result "FU09A flags preserved (+workflowActivation false)" "true" $fu09aOk $fu09aOk
$flagNames = $c.PSObject.Properties.Name
$forbidden = "supportsVisitPlanning","supportsRoutePlanning","supportsDueOverdueEngine","supportsDigitalDetailing","supportsRecommendationEngine","supportsConsentEvaluationEngine","supportsWorkflowApproval"
$leak = $forbidden | Where-Object { $flagNames -contains $_ }
Add-Result "Forbidden flags absent" "none" $(if ($leak) { $leak -join ',' } else { "none" }) (-not $leak)

# ---------------- Create an active account-target frequency policy ----------------
$code = "FU09B-$run"
$create = @{ policyCode = $code; policyName = "FU09B route smoke"; targetType = "account"; targetId = $AccountId
    frequencyType = "monthly"; requiredVisitCount = 2; periodType = "month"; effectiveFrom = "2026-08-02T00:00:00Z"
    priority = 500; source = "manual"; status = "active" }
$resp = Post-Json $vfp $create
$policyId = $resp.data
Add-Result "Create account frequency policy (201)" "201 + guid" "$($resp.statusCode) / $policyId" ($resp.statusCode -eq 201 -and $policyId)

# ---------------- Route candidates: frequency now RESOLVED ----------------
$routeUrl = "$route?accountId=$AccountId&resourceId=$ResourceId&includeNonReady=true&effectiveAt=$([uri]::EscapeDataString($EffectiveAt))&businessUnit=$BusinessUnit&date=$Date&weekday=$Weekday"
$rawRoute = Invoke-WebRequest -Uri $routeUrl -Method GET -Headers $auth -TimeoutSec 25
$rc = ($rawRoute.Content | ConvertFrom-Json).data
$resolvedRow = $rc.items | Where-Object { $_.frequencyStatus -eq "resolved" -and $_.selectedFrequencyPolicyCode -eq $code } | Select-Object -First 1
Add-Result "Route candidate frequency resolved by provider" "resolved+$code" "$($resolvedRow.frequencyStatus)/$($resolvedRow.selectedFrequencyPolicyCode)" ($null -ne $resolvedRow)
if ($resolvedRow) {
    Add-Result "Selected metadata (2/monthly/month)" "2/monthly/month" "$($resolvedRow.requiredVisitCount)/$($resolvedRow.frequencyType)/$($resolvedRow.periodType)" (($resolvedRow.requiredVisitCount -eq 2) -and ($resolvedRow.frequencyType -eq "monthly") -and ($resolvedRow.periodType -eq "month"))
    Add-Result "FrequencyReasonCodes carries provider reason" "frequency_policy_resolved" ($resolvedRow.frequencyReasonCodes -join ',') ($resolvedRow.frequencyReasonCodes -contains "frequency_policy_resolved")
    Add-Result "DueStatus stays unknown" "unknown" "$($resolvedRow.dueStatus)" ($resolvedRow.dueStatus -eq "unknown")
    Add-Result "LastVisitDate stays null" "null" "$($resolvedRow.lastVisitDate)" ($null -eq $resolvedRow.lastVisitDate)
    Add-Result "No coverage/resource blocker on this row" "no blockers" "$($resolvedRow.reasonCodes -join ',')" (-not ($resolvedRow.reasonCodes -contains "coverage_not_current" -or $resolvedRow.reasonCodes -contains "resource_not_current_owner"))
}

# ---------------- Response shape guard ----------------
$banned = "routeOrder","suggestedOrder","distance","travelTime","optimizationScore","dailyPlanId","visitPlanId","routeId","gps","checkIn","checkOut","consentAllowed","consentStatus"
$leaked = $banned | Where-Object { $rawRoute.Content -match "`"$_`"" }
Add-Result "Response shape clean (no planner/consent fields)" "none" $(if ($leaked) { $leaked -join ',' } else { "none" }) (-not $leaked)

# ---------------- Data mutation guard (route GET is write-free) ----------------
$before = (Get-Json $vfp).data.total
Get-Json $routeUrl | Out-Null
$after = (Get-Json $vfp).data.total
Add-Result "Route-candidate GET is write-free (policy count unchanged)" "$before" "$after" ($before -eq $after)

# ---------------- Archive -> frequency falls back to unknown ----------------
$arch = Invoke-RestMethod -Uri "$vfp/$policyId/archive" -Method POST -Headers $auth -TimeoutSec 20
Add-Result "Archive policy (success)" "200" "$($arch.statusCode)" ($arch.statusCode -in 200,204)
$rc2 = (Get-Json $routeUrl).data
$stillResolved = $rc2.items | Where-Object { $_.selectedFrequencyPolicyCode -eq $code } | Select-Object -First 1
Add-Result "After archive, provider no longer selects it (unknown/next)" "not $code" "$($stillResolved.selectedFrequencyPolicyCode)" ($null -eq $stillResolved)

# ---------------- Negative auth ----------------
$noTok = Status $routeUrl
Add-Result "Route candidates no token -> 401" 401 $noTok ($noTok -eq 401)

$auth = $null; $token = $null

# ---------------- Summary ----------------
Write-Host "`n== RESULTS (paste back; no secret) ==" -ForegroundColor Cyan
($results | Format-Table Step,Result,Expected,Actual -AutoSize | Out-String -Width 4096).TrimEnd() | Write-Host
$fail = ($results | Where-Object Result -eq "FAIL").Count
if ($fail -gt 0) {
    Write-Host "`n== FAILURES ONLY (paste THIS) ==" -ForegroundColor Yellow
    ($results | Where-Object Result -eq "FAIL" | Format-List Step,Expected,Actual | Out-String -Width 4096).TrimEnd() | Write-Host
}
Write-Host ("`nOVERALL: {0}  ({1} checks, {2} fail)" -f $(if ($fail -eq 0) { "PASS" } else { "FAIL" }), $results.Count, $fail) -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
