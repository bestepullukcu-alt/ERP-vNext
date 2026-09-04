<#
  Authenticated smoke for the MOD-0155 Visit Planning program (FU01/FU03/FU04/FU05/FU02).
  YOU run this (not the assistant): logging in requires entering a password, which the assistant
  may not do. It prompts for the tenant 97C5 operator login, mints a JWT, and probes the new
  endpoints. The Authorization header is never printed. Paste the PASS/FAIL table back.

  Prereq: fleet running + you have re-logged in once in the browser AFTER the RBAC grant
  (the grant added crm.planned-visit.* / visit-route.preview / visit-content.preview /
  visit-plan.* / visit-report.* to the 97C5 Admin role; a JWT minted before the grant lacks them).

  Usage:  pwsh scripts/smoke-mod0155-visit-planning-authenticated.ps1
#>
param(
  [string]$BaseUrl  = "http://localhost:5000",
  [string]$TenantId = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
)
$ErrorActionPreference = "Stop"
$results = @()
function Add-Result($name, $expected, $actual, $ok) {
  $script:results += [pscustomobject]@{ Check=$name; Expected=$expected; Actual=$actual; Result= if($ok){"PASS"}else{"FAIL"} }
}
function Status([string]$Url, [string]$Method="GET", $Headers=@{}, $Body=$null) {
  try {
    $p = @{ Uri=$Url; Method=$Method; TimeoutSec=20; Headers=$Headers; UseBasicParsing=$true }
    if ($Body -ne $null) { $p.ContentType="application/json"; $p.Body = ($Body | ConvertTo-Json -Depth 8) }
    return [int](Invoke-WebRequest @p).StatusCode
  } catch { return [int]$_.Exception.Response.StatusCode }
}

# --- unauthenticated guards (must be 401 before login) ---
Add-Result "no-token 401 (planned-visits/contract)" 401 (Status "$BaseUrl/api/crm/planned-visits/contract") ((Status "$BaseUrl/api/crm/planned-visits/contract") -eq 401)
Add-Result "no-token 401 (visit-plan/sessions)"      401 (Status "$BaseUrl/api/crm/visit-plan/sessions")      ((Status "$BaseUrl/api/crm/visit-plan/sessions") -eq 401)
Add-Result "no-token 401 (visit-report/contract)"    401 (Status "$BaseUrl/api/crm/visit-report/contract")    ((Status "$BaseUrl/api/crm/visit-report/contract") -eq 401)

# --- login ---
$cred = Get-Credential -Message "Tenant $TenantId operator login (email as username)"
$loginBody = @{ email=$cred.UserName; password=$cred.GetNetworkCredential().Password; rememberMe=$false } | ConvertTo-Json
$hdr = @{ "X-Tenant-Id" = $TenantId }
$token = $null
try {
  $login = Invoke-RestMethod -Uri "$BaseUrl/api/tenant-auth/login" -Method POST -Headers $hdr -ContentType "application/json" -Body $loginBody -TimeoutSec 20
  $token = $login.data.accessToken
} catch { Add-Result "gateway login" "200 + token" "$([int]$_.Exception.Response.StatusCode) (login failed)" $false }
$loginBody=$null; $cred=$null
if (-not $token) { Add-Result "gateway login" "200+token" "no token" $false; $results | Format-Table -AutoSize; return }
Add-Result "gateway login" "200 + token" "200 (token MASKED)" $true
$auth = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }

# --- authed reads / contracts (200 proves RBAC grant + endpoint reachable) ---
Add-Result "FU01 planned-visits/contract 200" 200 (Status "$BaseUrl/api/crm/planned-visits/contract" "GET" $auth) ((Status "$BaseUrl/api/crm/planned-visits/contract" "GET" $auth) -eq 200)
Add-Result "FU01 planned-visits list 200"      200 (Status "$BaseUrl/api/crm/planned-visits?page=1&pageSize=5" "GET" $auth) ((Status "$BaseUrl/api/crm/planned-visits?page=1&pageSize=5" "GET" $auth) -eq 200)
Add-Result "FU05 visit-plan/sessions 200"      200 (Status "$BaseUrl/api/crm/visit-plan/sessions" "GET" $auth) ((Status "$BaseUrl/api/crm/visit-plan/sessions" "GET" $auth) -eq 200)
$rep = Status "$BaseUrl/api/crm/visit-report/contract" "GET" $auth
Add-Result "FU02 visit-report/contract 200"    200 $rep ($rep -eq 200)
$cal = Status "$BaseUrl/api/crm/visit-report/calendar?from=2026-08-01&to=2026-08-31" "GET" $auth
Add-Result "FU02 visit-report/calendar 200"    200 $cal ($cal -eq 200)

# --- preview endpoints (dry-run). Empty/minimal body -> 400 is a PASS (endpoint routes + validates, not 401/403/404) ---
$rt = Status "$BaseUrl/api/crm/route-optimization/preview" "POST" $auth @{}
Add-Result "FU03 route-optimization/preview reachable (200|400)" "200 or 400" $rt (($rt -eq 200) -or ($rt -eq 400))
$vc = Status "$BaseUrl/api/crm/visit-content/preview" "POST" $auth @{}
Add-Result "FU04 visit-content/preview reachable (200|400)" "200 or 400" $vc (($vc -eq 200) -or ($vc -eq 400))

# NOTE: write flows (FU05 preview/apply/re-plan with a real selection, FU02 outcome/submit/amend)
# need a CyclePeriod + selected accounts/doctors + a planned visit — exercise those in the UI (manual test).

Write-Host ""
$results | Format-Table -AutoSize
$fail = ($results | Where-Object Result -eq "FAIL").Count
Write-Host ("`nSUMMARY: {0} PASS / {1} FAIL" -f ($results.Count-$fail), $fail) -ForegroundColor (if($fail){"Yellow"}else{"Green"})
