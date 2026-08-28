<#
  MOD-0165-FU03 — Authenticated Positive Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering
  passwords/tokens to authenticate is outside what the assistant may do on your behalf. This
  script keeps the credential in YOUR process memory only — it is never written to a file and the
  Authorization header is never printed (masked). Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0165-fu03-visit-frequency-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl  = "http://localhost:5000",
    [string]$TenantId = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [string]$CrmDirect = "http://localhost:5061"
)

$ErrorActionPreference = "Stop"
$results = [System.Collections.Generic.List[object]]::new()
function Add-Result([string]$Step, $Expected, $Actual, [bool]$Pass) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "$Expected"; Actual = "$Actual"; Result = $(if ($Pass) { "PASS" } else { "FAIL" }) })
}
# PS 5.1-compatible status probe (no -SkipHttpErrorCheck, which is PowerShell 7+ only).
function Status([string]$Url, [string]$Method = "GET", $Headers = $null) {
    try {
        $p = @{ Uri = $Url; Method = $Method; TimeoutSec = 10; UseBasicParsing = $true }
        if ($Headers) { $p.Headers = $Headers }
        return [int](Invoke-WebRequest @p).StatusCode
    } catch {
        $resp = $_.Exception.Response
        if ($resp -and $resp.StatusCode) { return [int]$resp.StatusCode }
        return -1
    }
}

$base = "$BaseUrl/api/crm/visit-frequency-policies"
$run  = (Get-Date -Format "yyyyMMddHHmmss")
$linkTarget    = [guid]::NewGuid().Guid   # synthetic account-contact-link TargetId (no cross-aggregate FK by design)
$segTarget     = [guid]::NewGuid().Guid   # synthetic segment TargetId
$emptyTarget   = [guid]::NewGuid().Guid   # a target that has NO policy (for the unknown case)

Write-Host "== MOD-0165-FU03 authenticated smoke ($run) ==" -ForegroundColor Cyan

# ---------------- A. Preflight ----------------
foreach ($p in @(5000,5001,5056,5057,5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health (only allowed direct call)" "200/ok" $crmHealth ($crmHealth -in 200,204)

# ---------------- Login (credential stays in your memory) ----------------
$cred = Get-Credential -Message "Tenant $TenantId operator login (email as username)"
$loginBody = @{ email = $cred.UserName; password = $cred.GetNetworkCredential().Password; rememberMe = $false } | ConvertTo-Json
$headers = @{ "X-Tenant-Id" = $TenantId }
$token = $null
try {
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/tenant-auth/login" -Method POST -Headers $headers -ContentType "application/json" -Body $loginBody -TimeoutSec 20
    $token = $login.data.accessToken
} catch {
    Add-Result "Gateway login" "200 + token" "$([int]$_.Exception.Response.StatusCode) (login failed)" $false
}
$loginBody = $null; $cred = $null  # drop the plaintext password ASAP

if (-not $token) {
    Add-Result "Gateway login" "200 + token" "no token" $false
    $results | Format-Table -AutoSize
    Write-Host "Login failed — cannot run authenticated steps." -ForegroundColor Red
    return
}
Add-Result "Gateway login" "200 + token" "200 (token MASKED)" $true

# tenant claim check (decode JWT payload locally; no secret printed)
$payload = ($token.Split('.')[1]).Replace('-','+').Replace('_','/')
switch ($payload.Length % 4) { 2 { $payload += '==' } 3 { $payload += '=' } }
$claims = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
$tenantClaim = $claims.tenant_id
Add-Result "Tenant claim == target" $TenantId $tenantClaim ($tenantClaim -eq $TenantId)

$auth = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
function Get-Json([string]$Url) { Invoke-RestMethod -Uri $Url -Method GET -Headers $auth -TimeoutSec 20 }
function Post-Json([string]$Url, $Obj) { Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 6) -TimeoutSec 20 }

# ---------------- C. Contract smoke ----------------
$contract = Get-Json "$base/contract"
$f = $contract.data.features
$flagsOk = $f.supportsVisitFrequencyPolicy -and $f.supportsCallCyclePolicy -and $f.supportsFrequencyPolicyPriority -and $f.supportsFrequencyPolicyEffectiveWindow -and $f.supportsFrequencyPolicyProvider
Add-Result "Contract flags all true" "true x5" $flagsOk $flagsOk
$forbidden = "supportsVisitPlanning","supportsRoutePlanning","supportsDueOverdueEngine","supportsDigitalDetailing","supportsRecommendationEngine","supportsConsentEvaluationEngine","supportsWorkflowApproval"
$flagNames = $f.PSObject.Properties.Name
$leak = $forbidden | Where-Object { $flagNames -contains $_ }
Add-Result "Forbidden flags absent" "none" $(if ($leak) { $leak -join ',' } else { "none" }) (-not $leak)

# ---------------- D. Positive create smoke ----------------
$createA = @{
    policyCode = "SMOKE-$run-A"; policyName = "FU03 smoke policy A"; targetType = "account-contact-link"; targetId = $linkTarget
    businessUnit = "gamma"; frequencyType = "monthly"; requiredVisitCount = 2; periodType = "month"
    effectiveFrom = "2026-08-02T00:00:00Z"; effectiveTo = "2027-07-31T00:00:00Z"; priority = 300; source = "manual"; status = "active"
    notes = "smoke created through authenticated Gateway"
    tenantId = "ffffffff-ffff-ffff-ffff-ffffffffffff"  # deliberately injected — must be IGNORED (claim wins)
}
$respA = Post-Json $base $createA
$policyA = $respA.data
Add-Result "Create policy A (201, PolicyId)" "201 + guid" "$($respA.statusCode) / $policyA" ($respA.statusCode -eq 201 -and $policyA)

# ---------------- E. Resolve smoke ----------------
$resolveUrl = "$base/resolve?targetType=account-contact-link&targetId=$linkTarget&effectiveAt=2026-08-11T09:00:00Z&businessUnit=gamma&includeDiagnostics=true"
$res1 = (Get-Json $resolveUrl).data
$e = ($res1.frequencyStatus -eq "resolved") -and ($res1.selectedFrequencyPolicyId -eq $policyA) -and ($res1.requiredVisitCount -eq 2) -and ($res1.frequencyType -eq "monthly") -and ($res1.periodType -eq "month") -and ($res1.source -eq "manual")
Add-Result "Resolve selects A (resolved, 2/monthly/month)" "resolved+A" "$($res1.frequencyStatus)/$($res1.selectedPolicyCode)" $e
Add-Result "Resolve reasonCodes has resolved reason" "frequency_policy_resolved" ($res1.reasonCodes -join ',') ($res1.reasonCodes -contains "frequency_policy_resolved")

# TenantId-injection guard: A must belong to the claim tenant, not the injected ffff...
$readA = (Get-Json "$base/$policyA").data
Add-Result "TenantId payload ignored (A readable in claim tenant)" "readable" "status=$($readA.status)" ($readA -and $readA.status -eq "active")

# ---------------- F. Priority / specificity smoke ----------------
# B: same target, higher priority number -> loses on priority
$createB = @{ policyCode = "SMOKE-$run-B"; policyName = "FU03 smoke policy B"; targetType = "account-contact-link"; targetId = $linkTarget
    frequencyType = "monthly"; requiredVisitCount = 4; periodType = "month"; effectiveFrom = "2026-08-02T00:00:00Z"; priority = 500; source = "manual"; status = "active" }
$respB = Post-Json $base $createB; $policyB = $respB.data
# C: segment target, same priority -> loses on specificity (account-contact-link is more specific)
$createC = @{ policyCode = "SMOKE-$run-C"; policyName = "FU03 smoke policy C"; targetType = "segment"; targetId = $segTarget
    frequencyType = "monthly"; requiredVisitCount = 6; periodType = "month"; effectiveFrom = "2026-08-02T00:00:00Z"; priority = 300; source = "segmentation"; segmentId = $segTarget; status = "active" }
$respC = Post-Json $base $createC; $policyC = $respC.data

$res2 = (Get-Json "$resolveUrl&segmentId=$segTarget").data
$selOk = $res2.selectedFrequencyPolicyId -eq $policyA
$losersVisible = ($res2.candidatePolicies.policyId -contains $policyB) -and ($res2.candidatePolicies.policyId -contains $policyC)
Add-Result "Priority/specificity: A wins over B(priority) & C(specificity)" "A selected" "$($res2.selectedPolicyCode)" $selOk
Add-Result "Eliminated candidates visible (B,C)" "both present" $losersVisible $losersVisible
Add-Result "SelectionReason carries basis" "priority/specificity text" "$($res2.selectionReason)" ($res2.reasonCodes -contains "policy_selected_by_priority" -or $res2.reasonCodes -contains "policy_selected_by_specificity")

# ---------------- H. Archive smoke ----------------
$arch = Invoke-RestMethod -Uri "$base/$policyA/archive" -Method POST -Headers $auth -TimeoutSec 20
Add-Result "Archive A (success)" "200" "$($arch.statusCode)" ($arch.statusCode -in 200,204)
$readArchived = (Get-Json "$base/$policyA").data
Add-Result "Archived A still readable + ArchivedAt set" "archived+stamp" "$($readArchived.status)/$($readArchived.archivedAt)" ($readArchived.status -eq "archived" -and $readArchived.archivedAt)
# NOTE: resolve WITHOUT segmentId here, so only account-contact-link policies (A archived, B active) are candidates.
# (With segmentId, segment policy C[priority 300] would beat B[priority 500] — lower priority wins — which is correct
# product behaviour but a different assertion.)
$res3 = (Get-Json $resolveUrl).data
Add-Result "Resolve no longer selects archived A -> falls to B" "B selected" "$($res3.selectedPolicyCode)" ($res3.selectedFrequencyPolicyId -eq $policyB)

# no-policy -> unknown (never a default)
$resUnknown = (Get-Json "$base/resolve?targetType=account-contact-link&targetId=$emptyTarget&effectiveAt=2026-08-11T09:00:00Z").data
Add-Result "No policy -> unknown (no invented default)" "unknown+null" "$($resUnknown.frequencyStatus)/$($resUnknown.requiredVisitCount)" ($resUnknown.frequencyStatus -eq "unknown" -and -not $resUnknown.requiredVisitCount)

# ---------------- I. Negative / auth guards ----------------
$noTok = Status "$base/contract"
Add-Result "No token contract -> 401" 401 $noTok ($noTok -eq 401)
$garb = Status "$base/contract" "GET" @{ Authorization = "Bearer x.y.z" }
Add-Result "Garbage token -> 401" 401 $garb ($garb -eq 401)
$del = Status "$base/$policyB" "DELETE" $auth
Add-Result "DELETE unsupported -> 404/405" "404/405" $del ($del -in 404,405)

$upd = @{ policyName = "x"; frequencyType = "monthly"; requiredVisitCount = 1; periodType = "month"; effectiveFrom = "2026-08-02T00:00:00Z"; priority = 300; source = "manual" }
$updCode = try { (Invoke-RestMethod -Uri "$base/$policyA" -Method PUT -Headers $auth -ContentType "application/json" -Body ($upd | ConvertTo-Json) -TimeoutSec 15).statusCode } catch { [int]$_.Exception.Response.StatusCode }
Add-Result "Update archived A -> 409" 409 $updCode ($updCode -eq 409)

$badCount = @{ policyCode="SMOKE-$run-NEG1"; policyName="n"; targetType="account-contact-link"; targetId=[guid]::NewGuid().Guid; frequencyType="monthly"; requiredVisitCount=0; periodType="month"; effectiveFrom="2026-08-02T00:00:00Z"; priority=300; source="manual" }
$c1 = try { (Post-Json $base $badCount).statusCode } catch { [int]$_.Exception.Response.StatusCode }
Add-Result "RequiredVisitCount<=0 -> 400" 400 $c1 ($c1 -eq 400)

$badWindow = @{ policyCode="SMOKE-$run-NEG2"; policyName="n"; targetType="account-contact-link"; targetId=[guid]::NewGuid().Guid; frequencyType="monthly"; requiredVisitCount=2; periodType="month"; effectiveFrom="2027-01-01T00:00:00Z"; effectiveTo="2026-01-01T00:00:00Z"; priority=300; source="manual" }
$c2 = try { (Post-Json $base $badWindow).statusCode } catch { [int]$_.Exception.Response.StatusCode }
Add-Result "EffectiveTo<EffectiveFrom -> 400" 400 $c2 ($c2 -eq 400)

$badTarget = @{ policyCode="SMOKE-$run-NEG3"; policyName="n"; targetType="planet"; targetId=[guid]::NewGuid().Guid; frequencyType="monthly"; requiredVisitCount=2; periodType="month"; effectiveFrom="2026-08-02T00:00:00Z"; priority=300; source="manual" }
$c3 = try { (Post-Json $base $badTarget).statusCode } catch { [int]$_.Exception.Response.StatusCode }
Add-Result "Unknown TargetType -> 400" 400 $c3 ($c3 -eq 400)

# ---------------- J. Response shape guard ----------------
$rawResolve = Invoke-WebRequest -Uri $resolveUrl -Method GET -Headers $auth -TimeoutSec 15
$banned = "dueStatus","lastVisitDate","routeOrder","distance","travelTime","visitPlanId","routeId","consentAllowed","consentStatus","dailyPlanId"
$leaked = $banned | Where-Object { $rawResolve.Content -match "`"$_`"" }
Add-Result "Response shape clean (no route/visit/due/consent field)" "none" $(if ($leaked) { $leaked -join ',' } else { "none" }) (-not $leaked)

# ---------------- K. Data mutation guard ----------------
$before = ((Get-Json "$base").data.total)
Get-Json $resolveUrl | Out-Null
Get-Json "$resolveUrl&segmentId=$segTarget" | Out-Null
$after = ((Get-Json "$base").data.total)
Add-Result "Resolve is write-free (policy count unchanged)" "$before" "$after" ($before -eq $after)

# cleanup (soft archive only; codes are per-run unique so re-runs never 409)
foreach ($id in @($policyB,$policyC)) { try { Invoke-RestMethod -Uri "$base/$id/archive" -Method POST -Headers $auth -TimeoutSec 15 | Out-Null } catch {} }
$token = $null; $auth = $null

# ---------------- Summary ----------------
Write-Host "`n== RESULTS (paste this back; contains no secret) ==" -ForegroundColor Cyan
($results | Format-Table Step,Result,Expected,Actual -AutoSize | Out-String -Width 4096).TrimEnd() | Write-Host
$fail = ($results | Where-Object Result -eq "FAIL").Count
if ($fail -gt 0) {
    Write-Host "`n== FAILURES ONLY (untruncated — paste THIS) ==" -ForegroundColor Yellow
    ($results | Where-Object Result -eq "FAIL" | Format-List Step,Expected,Actual | Out-String -Width 4096).TrimEnd() | Write-Host
    # Also write a secret-free JSON next to the repo temp for easy sharing.
    $out = "$PSScriptRoot/../.smoke-mod0165-fu03-results.json"
    $results | ConvertTo-Json -Depth 4 | Set-Content -Path $out -Encoding utf8
    Write-Host "`n(Full results also written to: $out — no secret inside)" -ForegroundColor DarkGray
}
Write-Host ("`nOVERALL: {0}  ({1} checks, {2} fail)" -f $(if ($fail -eq 0) { "PASS" } else { "FAIL" }), $results.Count, $fail) -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
