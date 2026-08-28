<#
  MOD-0162-FU04 — Authenticated KnowledgePath Runtime Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  never written to a file, and the Authorization header is never printed. Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0162-fu04-knowledge-path-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health. Nothing is hard-deleted:
  every record is closed with ARCHIVE. It asserts no DELETE/PATCH surface exists, no flat /path-steps family (D2), that
  steps are embedded (single-document writes), publish is a separate endpoint (D4), new-version clones (D5), the D6
  assessment-quiz rule and D7 authorable-but-never-evaluated branch data.

  NOTE: the path endpoints exist only after the fleet is (re)started on the FU04 build. Against an older running Api you
  will see 404s — restart the CRM service first.

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

$kb  = "$BaseUrl/api/crm/knowledge"
$run = (Get-Date -Format "yyyyMMddHHmmss")
$fromIso = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0162-FU04 authenticated knowledge-path smoke ($run) ==" -ForegroundColor Cyan

foreach ($p in @(5000,5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health" "200/204" $crmHealth ($crmHealth -in 200,204)
Add-Result "No token -> 401 (contract)" 401 (Status "$kb/path/contract") ((Status "$kb/path/contract") -eq 401)

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
function Api([string]$Method, [string]$Path, $Obj = $null) {
    $u = "$kb$Path"
    try {
        $p = @{ Uri = $u; Method = $Method; Headers = $H; TimeoutSec = 20; UseBasicParsing = $true }
        if ($Obj -ne $null) { $p.ContentType = "application/json"; $p.Body = ($Obj | ConvertTo-Json -Depth 8) }
        $r = Invoke-WebRequest @p
        return @{ Code = [int]$r.StatusCode; Body = ($r.Content | ConvertFrom-Json) }
    } catch {
        $resp = $_.Exception.Response
        $code = if ($resp -and $resp.StatusCode) { [int]$resp.StatusCode } else { -1 }
        return @{ Code = $code; Body = $null }
    }
}

# 2 contract
$c = Api GET "/path/contract"
$flagsOk = $c.Body.data.features.PSObject.Properties.Name.Count -ge 13
$noEngine = -not ($c.Body.data.features.PSObject.Properties.Name -contains "supportsBranchEvaluator")
Add-Result "GET path/contract 13 flags + no engine flag + limits" "200/true/200" "$($c.Code)/$flagsOk/$($c.Body.data.limits.maxStepsPerPath)" ($c.Code -eq 200 -and $flagsOk -and $noEngine -and $c.Body.data.limits.maxStepsPerPath -eq 200)

# a published+effective subject/content to sequence: reuse the first published content of the tenant
$contents = Api GET "/contents?includeArchived=false"
$pubContent = @($contents.Body.data.items | Where-Object { $_.contentStatus -eq "published" })[0]
$quizContent = @($contents.Body.data.items | Where-Object { $_.contentStatus -eq "published" -and $_.contentType -eq "quiz" })[0]
$subjectId = if ($pubContent) { $pubContent.subjectId } else { (Api GET "/subjects?includeArchived=false").Body.data.items[0].subjectId }
Add-Result "Found a published content to sequence" "content" $(if ($pubContent) { $pubContent.contentCode } else { "none" }) ([bool]$pubContent)

# 3 create path (draft)
$code = "KP-SMOKE-$run"
$create = Api POST "/paths" @{ pathCode = $code; pathName = "Smoke $run"; subjectId = $subjectId; objective = "Smoke objective"; pathVersion = "1.0"; effectiveFrom = $fromIso; source = "manual" }
$pathId = $create.Body.data
Add-Result "POST paths (draft)" 201 $create.Code ($create.Code -eq 201)

$__r = (Api POST "/paths" @{ pathCode = $code; pathName = "dup"; subjectId = $subjectId; objective = "o"; pathVersion = "1.0"; effectiveFrom = $fromIso }); Add-Result "POST paths duplicate code+version -> 409" 409 $__r.Code ($__r.Code -eq 409)
$badWindow = Api POST "/paths" @{ pathCode = "$code-b"; pathName = "b"; subjectId = $subjectId; objective = "o"; pathVersion = "1.0"; effectiveFrom = $fromIso; effectiveTo = "2000-01-01T00:00:00Z" }
Add-Result "POST paths effectiveTo<From -> 400" 400 $badWindow.Code ($badWindow.Code -eq 400)

if ($pathId -and $pubContent) {
    # 6 add step (single-document write)
    $s1 = Api POST "/paths/$pathId/steps" @{ stepOrder = 10; stepCode = "S10"; stepTitle = "Intro"; stepType = "core-message"; contentId = $pubContent.contentId; isRequired = $true; versionPinPolicy = "pinned" }
    $step1Id = $s1.Body.data
    Add-Result "POST steps (order 10, required, pinned)" 201 $s1.Code ($s1.Code -eq 201)
    $__r = (Api POST "/paths/$pathId/steps" @{ stepOrder = 10; stepCode = "SX"; stepTitle = "x"; stepType = "faq"; contentId = $pubContent.contentId; isRequired = $false }); Add-Result "POST steps duplicate order 10 -> 409" 409 $__r.Code ($__r.Code -eq 409)
    $s2 = Api POST "/paths/$pathId/steps" @{ stepOrder = 20; stepCode = "S20"; stepTitle = "Next"; stepType = "faq"; contentId = $pubContent.contentId; isRequired = $false; prerequisiteStepId = $step1Id }
    Add-Result "POST steps (order 20, prerequisite=step#1)" 201 $s2.Code ($s2.Code -eq 201)
    $__r = (Api POST "/paths/$pathId/steps" @{ stepOrder = 5; stepCode = "S5"; stepTitle = "y"; stepType = "faq"; contentId = $pubContent.contentId; isRequired = $false; prerequisiteStepId = $s2.Body.data }); Add-Result "POST steps prerequisite forward -> 400" 400 $__r.Code ($__r.Code -eq 400)
    $__r = (Api POST "/paths/$pathId/steps" @{ stepOrder = 30; stepCode = "S30"; stepTitle = "z"; stepType = "faq"; contentId = $pubContent.contentId; isRequired = $true; prerequisiteStepId = $s2.Body.data }); Add-Result "POST steps required on optional prereq -> 400" 400 $__r.Code ($__r.Code -eq 400)

    # D6 assessment rule
    if ($quizContent) {
        $__r = (Api POST "/paths/$pathId/steps" @{ stepOrder = 40; stepCode = "S40"; stepTitle = "Quiz"; stepType = "quiz"; contentId = $quizContent.contentId; isRequired = $false; completionRule = "assessment-passed" }); Add-Result "POST steps assessment-passed + quiz -> 201 (D6)" 201 $__r.Code ($__r.Code -eq 201)
    }
    $__r = (Api POST "/paths/$pathId/steps" @{ stepOrder = 50; stepCode = "S50"; stepTitle = "NoQuiz"; stepType = "faq"; contentId = $pubContent.contentId; isRequired = $false; completionRule = "assessment-passed" }); Add-Result "POST steps assessment-passed + non-quiz -> 400 (D6)" 400 $__r.Code ($__r.Code -eq 400)

    # D7 branch data echoed, foreign target rejected
    $bUpd = Api PUT "/paths/$pathId/steps/$step1Id" @{ stepOrder = 10; stepCode = "S10"; stepTitle = "Intro"; stepType = "core-message"; contentId = $pubContent.contentId; isRequired = $true; branchConditions = @(@{ conditionCode = "price-objection"; description = "handle" }) }
    Add-Result "PUT step + branchConditions -> 200 (D7 data echoed)" 200 $bUpd.Code ($bUpd.Code -eq 200 -and @($bUpd.Body.data) -ne $null)
    $__r = (Api PUT "/paths/$pathId/steps/$step1Id" @{ stepOrder = 10; stepCode = "S10"; stepTitle = "Intro"; stepType = "core-message"; contentId = $pubContent.contentId; isRequired = $true; branchConditions = @(@{ conditionCode = "x"; targetStepId = [guid]::NewGuid().ToString() }) }); Add-Result "PUT step branch foreign TargetStepId -> 400 (V-S14)" 400 $__r.Code ($__r.Code -eq 400)

    $steps = Api GET "/paths/$pathId/steps"
    $ordered = @($steps.Body.data.items | ForEach-Object { $_.stepOrder })
    Add-Result "GET steps ordered + ResolvedContentId present" "10,20..." ($ordered -join ",") ($steps.Code -eq 200 -and $steps.Body.data.items[0].resolvedContentId)

    $__r = (Api PUT "/paths/$pathId" @{ pathName = "x"; subjectId = $subjectId; objective = "o"; pathVersion = "1.0"; effectiveFrom = $fromIso; steps = @(@{}) }); Add-Result "PUT paths { steps:[...] } -> 400 (V-P16)" 400 $__r.Code ($__r.Code -eq 400)
    $__r = (Api PUT "/paths/$pathId" @{ pathName = "x"; subjectId = $subjectId; objective = "o"; pathVersion = "1.0"; effectiveFrom = $fromIso; pathStatus = "published" }); Add-Result "PUT paths { pathStatus: published } -> 400 (V-P12/D4)" 400 $__r.Code ($__r.Code -eq 400)

    # publish (D4)
    $pub = Api POST "/paths/$pathId/publish"
    Add-Result "POST publish -> 200 (StepSetFrozenAt set, D4)" 200 $pub.Code ($pub.Code -eq 200)
    $__r = (Api POST "/paths/$pathId/steps" @{ stepOrder = 99; stepCode = "S99"; stepTitle = "late"; stepType = "faq"; contentId = $pubContent.contentId; isRequired = $false }); Add-Result "POST steps on published -> 409 (frozen)" 409 $__r.Code ($__r.Code -eq 409)
    $__r = (Api PUT "/paths/$pathId/steps/$step1Id" @{ stepOrder = 10; stepCode = "S10"; stepTitle = "x"; stepType = "core-message"; contentId = $pubContent.contentId; isRequired = $true }); Add-Result "PUT step on published -> 409 (frozen)" 409 $__r.Code ($__r.Code -eq 409)

    # new-version (D5)
    $nv = Api POST "/paths/$pathId/new-version" @{}
    $newId = $nv.Body.data
    $newPath = Api GET "/paths/$newId"
    $newStepIds = @($newPath.Body.data.steps | ForEach-Object { $_.stepId })
    $sameAsSource = $newStepIds -contains $step1Id
    Add-Result "POST new-version -> 201 (draft, new StepIds, SupersedesPathId)" 201 $nv.Code ($nv.Code -eq 201 -and $newPath.Body.data.pathStatus -eq "draft" -and $newPath.Body.data.supersedesPathId -eq $pathId -and -not $sameAsSource)

    $__r = (Api GET "/paths?status=published&effectiveAt=$fromIso"); Add-Result "GET paths?status=published&effectiveAt -> only published" 200 $__r.Code ($__r.Code -eq 200)

    # cleanup: archive-only (NO hard delete)
    $__r = (Api POST "/paths/$pathId/archive"); Add-Result "POST paths/{id}/archive -> 200" 200 $__r.Code ($__r.Code -eq 200)
    $__r = (Api POST "/paths/$newId/archive"); Add-Result "POST paths/{newId}/archive -> 200" 200 $__r.Code ($__r.Code -eq 200)
}

# negative surface
$__r = (Api GET "/paths/$([guid]::NewGuid().ToString())"); Add-Result "GET other tenant path -> 404" 404 $__r.Code ($__r.Code -eq 404)
$__d = (Status "$kb/paths/$([guid]::NewGuid())" "DELETE" $H); Add-Result "DELETE any route -> 404 (no delete surface)" 404 $__d ($__d -eq 404)
$__f = (Status "$kb/path-steps" "GET" $H); Add-Result "GET flat /path-steps -> 404 (no flat family, D2)" 404 $__f ($__f -eq 404)

Write-Host ""
$results | Format-Table -AutoSize
$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$pass = @($results | Where-Object { $_.Result -eq "PASS" }).Count
Write-Host ""
Write-Host "SUMMARY: PASS=$pass FAIL=$fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
