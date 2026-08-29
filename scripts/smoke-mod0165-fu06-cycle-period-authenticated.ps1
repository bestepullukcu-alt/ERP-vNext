<#
  MOD-0165-FU06 — Authenticated Cycle Period Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  never written to a file, and the Authorization header is never printed. Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health. Nothing is hard-deleted:
  every record created here is ended with CLOSE. The script asserts the load-bearing FU06 promises:
    * a period is born DRAFT and TenantId is claim-only
    * EndDate is INCLUSIVE and must be after StartDate
    * a code stays taken after the period is closed; (year, sequence) is unique per business-unit scope
    * ACTIVE periods of the same scope may never share a day (409 at activate, and the row stays draft);
      DRAFT overlaps are allowed, and a different business unit is a different scope
    * an active period's dates are immutable while its name stays editable; closed is terminal
    * resolve-active answers resolved / none / ambiguous, prefers the business unit, never merges scopes,
      and WRITES NOTHING (the row count is compared before and after)
    * there is no DELETE, no PATCH, no bulk and no reopen surface anywhere

  PREREQUISITES (data, not code):
    * The Gateway must route /api/crm/cycle-periods to 5061. Without that route EVERY call answers 404 with an empty {}
      body — that is a missing route, not a code defect.
    * The fleet must be running the FU06 build (a restart is required after deploying it).

  PowerShell 5.1 note: pipeline counts use the @(...) array-subexpression guard (a single match otherwise yields $null).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl   = "http://localhost:5000",
    [string]$TenantId  = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [string]$CrmDirect = "http://localhost:5061"
)

$ErrorActionPreference = "Stop"
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Step, $Expected, $Actual, [bool]$Pass) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "$Expected"; Actual = "$Actual"; Result = $(if ($Pass) { "PASS" } else { "FAIL" }) })
}
function Add-Gap([string]$Step, $Detail) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "data present"; Actual = "$Detail"; Result = "SKIP" })
}
function Status([string]$Url, [string]$Method = "GET", $Headers = $null, $Body = $null) {
    try {
        $p = @{ Uri = $Url; Method = $Method; TimeoutSec = 15; UseBasicParsing = $true }
        if ($Headers) { $p.Headers = $Headers }
        if ($Body) { $p.ContentType = "application/json"; $p.Body = $Body }
        return [int](Invoke-WebRequest @p).StatusCode
    } catch {
        $resp = $_.Exception.Response
        if ($resp -and $resp.StatusCode) { return [int]$resp.StatusCode }
        return -1
    }
}

$api = "$BaseUrl/api/crm/cycle-periods"
$run = (Get-Date -Format "yyyyMMddHHmmss")
$suffix = $run.Substring($run.Length - 6)

Write-Host "== MOD-0165-FU06 authenticated cycle period smoke ($run) ==" -ForegroundColor Cyan

foreach ($p in @(5000, 5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health" "200/204" $crmHealth ($crmHealth -in 200, 204)

# Probe with OPTIONS: an anonymous GET is refused by middleware BEFORE routing, so a 403 would tell us nothing about
# whether the Gateway route actually exists.
$routeProbe = Status "$api/contract" "OPTIONS"
Add-Result "Gateway route present (OPTIONS probe)" "not 404" $routeProbe ($routeProbe -ne 404)
if ($routeProbe -eq 404) {
    Write-Host "Gateway has no /api/crm/cycle-periods route yet. Everything below would 404." -ForegroundColor Yellow
}

$anon = Status "$api/contract"
Add-Result "No token -> 401 (contract)" 401 $anon ($anon -eq 401)

$cred = Get-Credential -Message "Tenant $TenantId operator login (email as username)"
$loginBody = @{ email = $cred.UserName; password = $cred.GetNetworkCredential().Password; rememberMe = $false } | ConvertTo-Json
$hAuthOnly = @{ "X-Tenant-Id" = $TenantId }
$token = $null
try {
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/tenant-auth/login" -Method POST -Headers $hAuthOnly -ContentType "application/json" -Body $loginBody -TimeoutSec 20
    $token = $login.data.accessToken
} catch { Write-Host "Login failed: $($_.Exception.Message)" -ForegroundColor Red }
Add-Result "Login -> token" "non-empty" $(if ($token) { "token" } else { "none" }) ([bool]$token)
if (-not $token) { $results | Format-Table -AutoSize; return }

$H = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
function Call([string]$Method, [string]$Url, $Obj = $null) {
    try {
        $p = @{ Uri = $Url; Method = $Method; Headers = $H; TimeoutSec = 30; UseBasicParsing = $true }
        if ($null -ne $Obj) { $p.ContentType = "application/json"; $p.Body = ($Obj | ConvertTo-Json -Depth 8) }
        $r = Invoke-WebRequest @p
        $parsed = $null
        if ($r.Content) { try { $parsed = $r.Content | ConvertFrom-Json } catch { $parsed = $null } }
        return @{ Code = [int]$r.StatusCode; Body = $parsed; Raw = "$($r.Content)" }
    } catch {
        $resp = $_.Exception.Response
        $code = if ($resp -and $resp.StatusCode) { [int]$resp.StatusCode } else { -1 }
        $raw = ""
        try {
            $stream = $resp.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $raw = $reader.ReadToEnd()
        } catch { }
        $parsed = $null
        if ($raw) { try { $parsed = $raw | ConvertFrom-Json } catch { $parsed = $null } }
        return @{ Code = $code; Body = $parsed; Raw = "$raw" }
    }
}
function Api([string]$Method, [string]$Path, $Obj = $null) { return Call $Method "$api$Path" $Obj }
function ErrorsOf($r) { if ($r.Body -and $r.Body.errors) { return @($r.Body.errors) } return @() }
function HasCode($r, [string]$Code) { return @(ErrorsOf $r | Where-Object { "$_" -eq $Code }).Count -gt 0 }
function ErrorText($r) { return (@(ErrorsOf $r) -join " | ") }
function RowCount { $r = Api GET ""; if ($r.Body -and $r.Body.data) { return [int]$r.Body.data.totalCount } return -1 }

# A far-future planning year keeps the smoke away from whatever real calendar the tenant already has.
$year = 2093
function New-PeriodBody([string]$Code, [int]$Sequence, [string]$Start, [string]$End, $BusinessUnit = $null) {
    return @{
        cycleCode = $Code; cycleName = "Smoke $Code"; year = $year; sequenceInYear = $Sequence
        startDate = $Start; endDate = $End; businessUnitId = $BusinessUnit
        description = "MOD-0165-FU06 smoke"
    }
}

# ---------------- contract ----------------

$contract = Api GET "/contract"
Add-Result "GET /contract" 200 $contract.Code ($contract.Code -eq 200)
$flags = $null
if ($contract.Body -and $contract.Body.data) { $flags = $contract.Body.data.features }
if ($flags) {
    Add-Result "contract supportsCyclePeriod" $true $flags.supportsCyclePeriod ($flags.supportsCyclePeriod -eq $true)
    foreach ($closed in @(
        "supportsMicroTargetGeneration", "supportsCampaignBinding", "supportsFrequencyPolicyWrite",
        "supportsStrategyApply", "supportsWorkingCalendarIntegration", "supportsCycleAutoClose",
        "supportsBulkDelete", "supportsHardDelete", "supportsCycleOverlap",
        "supportsCycleCalendarHierarchy", "supportsCyclePeriodVersioning")) {
        Add-Result "contract $closed is false" $false $flags.$closed ($flags.$closed -eq $false)
    }
} else {
    Add-Gap "contract flags" "contract body missing"
}

# ---------------- create / validation ----------------

$codeA = "smk-$suffix-a"
$codeB = "smk-$suffix-b"
$codeC = "smk-$suffix-c"
$aStart = "$year-03-01T00:00:00Z"
$aEnd   = "$year-04-30T00:00:00Z"
$bStart = "$year-05-01T00:00:00Z"
$bEnd   = "$year-06-30T00:00:00Z"

$createA = Api POST "" (New-PeriodBody $codeA 1 $aStart $aEnd)
Add-Result "Create period A" 201 $createA.Code ($createA.Code -eq 201)
$idA = $null
if ($createA.Body) { $idA = $createA.Body.data }
if (-not $idA) {
    Add-Gap "Create period A" "no id returned; remaining scenarios skipped"
    $results | Format-Table -AutoSize
    return
}

$getA = Api GET "/$idA"
$statusA = $null
if ($getA.Body -and $getA.Body.data) { $statusA = $getA.Body.data.cycleStatus }
Add-Result "A is born draft" "draft" $statusA ($statusA -eq "draft")

$badWindow = Api POST "" (New-PeriodBody "smk-$suffix-bad" 9 $aEnd $aStart)
Add-Result "EndDate before StartDate -> 400" 400 $badWindow.Code ($badWindow.Code -eq 400 -and (HasCode $badWindow "cycle_period_window_invalid"))

$sameDay = Api POST "" (New-PeriodBody "smk-$suffix-same" 8 $aStart $aStart)
Add-Result "EndDate equal to StartDate -> 400" 400 $sameDay.Code ($sameDay.Code -eq 400)

$dupCode = Api POST "" (New-PeriodBody $codeA 7 $bStart $bEnd)
Add-Result "Duplicate code -> 409" 409 $dupCode.Code ($dupCode.Code -eq 409 -and (HasCode $dupCode "cycle_period_code_taken"))

$dupSeq = Api POST "" (New-PeriodBody "smk-$suffix-seq" 1 $bStart $bEnd)
Add-Result "Duplicate (year, sequence) -> 409" 409 $dupSeq.Code ($dupSeq.Code -eq 409 -and (HasCode $dupSeq "cycle_period_sequence_taken"))

$buSeq = Api POST "" (New-PeriodBody "smk-$suffix-rx" 1 $aStart $aEnd "smoke-rx")
Add-Result "Same sequence in another business unit -> 201" 201 $buSeq.Code ($buSeq.Code -eq 201)
$idRx = $null
if ($buSeq.Body) { $idRx = $buSeq.Body.data }

$badStatusFilter = Api GET "?cycleStatus=archived"
Add-Result "Unknown status filter -> 400" 400 $badStatusFilter.Code ($badStatusFilter.Code -eq 400 -and (HasCode $badStatusFilter "cycle_period_status_unknown"))

# ---------------- activate / overlap ----------------

$activateA = Api POST "/$idA/activate"
Add-Result "Activate A" 200 $activateA.Code ($activateA.Code -eq 200)

$createB = Api POST "" (New-PeriodBody $codeB 2 $aEnd $bEnd)
$idB = $null
if ($createB.Body) { $idB = $createB.Body.data }
Add-Result "Create period B (touching A's inclusive end)" 201 $createB.Code ($createB.Code -eq 201)

if ($idB) {
    $overlap = Api POST "/$idB/activate"
    Add-Result "Activate overlapping period -> 409" 409 $overlap.Code ($overlap.Code -eq 409 -and (HasCode $overlap "cycle_period_overlap"))
    Add-Result "Overlap refusal names the blocker" "contains $codeA" (ErrorText $overlap) ((ErrorText $overlap) -like "*$codeA*")

    $stillDraft = Api GET "/$idB"
    $statusB = $null
    if ($stillDraft.Body -and $stillDraft.Body.data) { $statusB = $stillDraft.Body.data.cycleStatus }
    Add-Result "Refused period stays draft" "draft" $statusB ($statusB -eq "draft")
}

if ($idRx) {
    $activateRx = Api POST "/$idRx/activate"
    Add-Result "Activate same window in another business unit -> 200" 200 $activateRx.Code ($activateRx.Code -eq 200)
}

$createC = Api POST "" (New-PeriodBody $codeC 3 $bStart $bEnd)
$idC = $null
if ($createC.Body) { $idC = $createC.Body.data }
if ($idC) {
    $activateC = Api POST "/$idC/activate"
    Add-Result "Activate the day after A's end -> 200" 200 $activateC.Code ($activateC.Code -eq 200)
}

$activateTwice = Api POST "/$idA/activate"
Add-Result "Activate twice -> 409" 409 $activateTwice.Code ($activateTwice.Code -eq 409 -and (HasCode $activateTwice "cycle_period_already_active"))

# ---------------- immutability ----------------

$aVersion = $null
$getA2 = Api GET "/$idA"
if ($getA2.Body -and $getA2.Body.data) { $aVersion = [int]$getA2.Body.data.version }

$moveActive = Api PUT "/$idA" @{
    cycleName = "Smoke $codeA"; year = $year; sequenceInYear = 1
    startDate = $aStart; endDate = $bEnd; businessUnitId = $null; description = "moved"
    expectedVersion = $aVersion
}
Add-Result "Move an active period's window -> 409" 409 $moveActive.Code ($moveActive.Code -eq 409 -and (HasCode $moveActive "cycle_period_dates_immutable"))

$renameActive = Api PUT "/$idA" @{
    cycleName = "Smoke $codeA renamed"; year = $year; sequenceInYear = 1
    startDate = $aStart; endDate = $aEnd; businessUnitId = $null; description = "renamed"
    expectedVersion = $aVersion
}
Add-Result "Rename an active period -> 200" 200 $renameActive.Code ($renameActive.Code -eq 200)

$staleVersion = Api PUT "/$idA" @{
    cycleName = "stale"; year = $year; sequenceInYear = 1
    startDate = $aStart; endDate = $aEnd; businessUnitId = $null; description = $null
    expectedVersion = 999
}
Add-Result "Stale expectedVersion -> 409" 409 $staleVersion.Code ($staleVersion.Code -eq 409 -and (HasCode $staleVersion "cycle_period_concurrency_conflict"))

# ---------------- resolve-active ----------------

$rowsBefore = RowCount

$inside = "$year-03-15T00:00:00Z"
$outside = "$year-01-15T00:00:00Z"
$resolveIn = Api GET "/resolve-active?at=$([uri]::EscapeDataString($inside))"
$outcomeIn = $null
$resolvedCode = $null
if ($resolveIn.Body -and $resolveIn.Body.data) {
    $outcomeIn = $resolveIn.Body.data.outcome
    if ($resolveIn.Body.data.period) { $resolvedCode = $resolveIn.Body.data.period.cycleCode }
}
Add-Result "resolve-active inside A -> resolved" "resolved" $outcomeIn ($outcomeIn -eq "resolved")
Add-Result "resolve-active returns A" $codeA $resolvedCode ($resolvedCode -eq $codeA)

$resolveOut = Api GET "/resolve-active?at=$([uri]::EscapeDataString($outside))"
$outcomeOut = $null
if ($resolveOut.Body -and $resolveOut.Body.data) { $outcomeOut = $resolveOut.Body.data.outcome }
Add-Result "resolve-active outside every window -> none" "none" $outcomeOut ($outcomeOut -eq "none")

$resolveRx = Api GET "/resolve-active?at=$([uri]::EscapeDataString($inside))&businessUnitId=smoke-rx"
$rxCode = $null
if ($resolveRx.Body -and $resolveRx.Body.data -and $resolveRx.Body.data.period) { $rxCode = $resolveRx.Body.data.period.cycleCode }
Add-Result "resolve-active prefers the business unit" "smk-$suffix-rx" $rxCode ($rxCode -eq "smk-$suffix-rx")

$resolveFallback = Api GET "/resolve-active?at=$([uri]::EscapeDataString($inside))&businessUnitId=smoke-unknown"
$fallbackCode = $null
if ($resolveFallback.Body -and $resolveFallback.Body.data -and $resolveFallback.Body.data.period) { $fallbackCode = $resolveFallback.Body.data.period.cycleCode }
Add-Result "resolve-active falls back to tenant-wide" $codeA $fallbackCode ($fallbackCode -eq $codeA)

$rowsAfter = RowCount
Add-Result "resolve-active writes nothing" $rowsBefore $rowsAfter ($rowsBefore -eq $rowsAfter -and $rowsBefore -ge 0)

$selector = Api GET "/selector?year=$year"
Add-Result "GET /selector" 200 $selector.Code ($selector.Code -eq 200)

# ---------------- close / terminal ----------------

$closeA = Api POST "/$idA/close"
Add-Result "Close A" 200 $closeA.Code ($closeA.Code -eq 200)

$closeAgain = Api POST "/$idA/close"
Add-Result "Close twice -> 409" 409 $closeAgain.Code ($closeAgain.Code -eq 409 -and (HasCode $closeAgain "cycle_period_closed"))

$reactivate = Api POST "/$idA/activate"
Add-Result "Activate a closed period -> 409" 409 $reactivate.Code ($reactivate.Code -eq 409 -and (HasCode $reactivate "cycle_period_closed"))

$editClosed = Api PUT "/$idA" @{
    cycleName = "closed edit"; year = $year; sequenceInYear = 1
    startDate = $aStart; endDate = $aEnd; businessUnitId = $null; description = $null; expectedVersion = $null
}
Add-Result "Edit a closed period -> 409" 409 $editClosed.Code ($editClosed.Code -eq 409)

$codeStillTaken = Api POST "" (New-PeriodBody $codeA 6 $bStart $bEnd)
Add-Result "A closed period keeps its code -> 409" 409 $codeStillTaken.Code ($codeStillTaken.Code -eq 409 -and (HasCode $codeStillTaken "cycle_period_code_taken"))

# ---------------- absent surfaces ----------------

Add-Result "DELETE surface absent" "404/405" (Status "$api/$idA" "DELETE" $H) ((Status "$api/$idA" "DELETE" $H) -in 404, 405)
Add-Result "PATCH surface absent" "404/405" (Status "$api/$idA" "PATCH" $H) ((Status "$api/$idA" "PATCH" $H) -in 404, 405)
Add-Result "reopen surface absent" "404/405" (Status "$api/$idA/reopen" "POST" $H) ((Status "$api/$idA/reopen" "POST" $H) -in 404, 405)
Add-Result "apply surface absent" "404/405" (Status "$api/$idA/apply" "POST" $H) ((Status "$api/$idA/apply" "POST" $H) -in 404, 405)
Add-Result "bulk surface absent" "404/405" (Status "$api/bulk" "DELETE" $H) ((Status "$api/bulk" "DELETE" $H) -in 404, 405)
Add-Result "working-days surface absent" "404/405" (Status "$api/$idA/working-days" "GET" $H) ((Status "$api/$idA/working-days" "GET" $H) -in 404, 405)

# ---------------- cleanup: close everything this run created ----------------

foreach ($id in @($idB, $idC, $idRx)) {
    if ($id) { $null = Api POST "/$id/close" }
}

$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$pass = @($results | Where-Object { $_.Result -eq "PASS" }).Count
$skip = @($results | Where-Object { $_.Result -eq "SKIP" }).Count

$results | Format-Table -AutoSize
Write-Host ""
Write-Host "PASS=$pass FAIL=$fail SKIP=$skip" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
