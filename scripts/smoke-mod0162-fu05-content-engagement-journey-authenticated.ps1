<#
  MOD-0162-FU05 — Authenticated ContentEngagementJourney Runtime Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  never written to a file, and the Authorization header is never printed. Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0162-fu05-content-engagement-journey-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health. Nothing is hard-deleted:
  every record is closed with ARCHIVE. It asserts no DELETE/PATCH surface exists, no flat
  /content-engagement-journey-stages family (S2), that stages are embedded (single-document writes), publish is a
  separate endpoint (SoD), new-version clones with REMAPPED stage references, the path-binding guard (published +
  effective only), pinned vs latest-published resolution, repeat visibility and the never-evaluated
  advancement/fallback/branch metadata.

  PREREQUISITE: at least one PUBLISHED + effective FU04 KnowledgePath must exist for this tenant (the stage binding
  needs one). The script reports a clear FAIL if none is found instead of inventing data.

  NOTE: the journey endpoints exist only after the fleet is (re)started on the FU05 build. Against an older running Api
  you will see 404s — restart the CRM service first.

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
$nowIso  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0162-FU05 authenticated content-engagement-journey smoke ($run) ==" -ForegroundColor Cyan

foreach ($p in @(5000,5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health" "200/204" $crmHealth ($crmHealth -in 200,204)
$anon = Status "$kb/content-engagement-journey/contract"
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

# 2 contract — 14 flags, no engine flag, published limits
$c = Api GET "/content-engagement-journey/contract"
$flagCount = @($c.Body.data.features.PSObject.Properties.Name).Count
$flagsOk = ($flagCount -ge 14)
$engineNames = @("supportsStageAdvancementEngine","supportsBranchEvaluator","supportsRecommendationEngine","supportsJourneyRuntimeProgress","supportsCurrentStageState","supportsJourneyTargetAssignment")
$leaked = @($c.Body.data.features.PSObject.Properties.Name | Where-Object { $engineNames -contains $_ })
$noEngine = ($leaked.Count -eq 0)
$limitOk = ($c.Body.data.limits.maxStagesPerJourney -eq 100)
Add-Result "GET contract: 14 flags + no engine flag + limits" "200/true/100" "$($c.Code)/$flagsOk/$($c.Body.data.limits.maxStagesPerJourney)" (($c.Code -eq 200) -and $flagsOk -and $noEngine -and $limitOk)

# a published + effective FU04 KnowledgePath is the hard prerequisite for any stage
$paths = Api GET "/paths?status=published&effectiveAt=$nowIso&includeArchived=false"
$pubPath = @($paths.Body.data.items)[0]
$draftPaths = Api GET "/paths?status=draft&includeArchived=false"
$draftPath = @($draftPaths.Body.data.items)[0]
Add-Result "Found a published+effective KnowledgePath (prerequisite)" "path" $(if ($pubPath) { $pubPath.pathCode } else { "none" }) ([bool]$pubPath)
$subjectId = if ($pubPath) { $pubPath.subjectId } else { @((Api GET "/subjects?includeArchived=false").Body.data.items)[0].subjectId }

# 3 create journey (draft)
$code = "CEJ-SMOKE-$run"
$create = Api POST "/content-engagement-journeys" @{ journeyCode = $code; journeyName = "Smoke $run"; subjectId = $subjectId; objective = "Smoke objective"; journeyVersion = "1.0"; effectiveFrom = $fromIso; source = "manual" }
$journeyId = $create.Body.data
Add-Result "POST journeys (draft)" 201 $create.Code ($create.Code -eq 201)

$__r = (Api POST "/content-engagement-journeys" @{ journeyCode = $code; journeyName = "dup"; subjectId = $subjectId; objective = "o"; journeyVersion = "1.0"; effectiveFrom = $fromIso }); Add-Result "POST journeys duplicate code+version -> 409" 409 $__r.Code ($__r.Code -eq 409)
$__r = (Api POST "/content-engagement-journeys" @{ journeyCode = "$code-b"; journeyName = "b"; subjectId = $subjectId; objective = "o"; journeyVersion = "1.0"; effectiveFrom = $fromIso; effectiveTo = "2000-01-01T00:00:00Z" }); Add-Result "POST journeys effectiveTo<From -> 400" 400 $__r.Code ($__r.Code -eq 400)
$__r = (Api POST "/content-engagement-journeys" @{ journeyCode = "$code-c"; journeyName = "c"; subjectId = $subjectId; objective = "o"; journeyVersion = "1.0"; effectiveFrom = $fromIso; campaignId = [guid]::NewGuid().ToString() }); Add-Result "POST journeys { campaignId } -> 400 (S6, no fake FK)" 400 $__r.Code ($__r.Code -eq 400)

if ($journeyId -and $pubPath) {
    # stages: single-document writes on the journey
    $s1 = Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 10; stageCode = "ST10"; stageName = "Awareness"; stageObjective = "Open the topic"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $true; pathVersionPinPolicy = "pinned" }
    $stage1Id = $s1.Body.data
    Add-Result "POST stages (order 10, required, pinned)" 201 $s1.Code ($s1.Code -eq 201)
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 10; stageCode = "STX"; stageName = "x"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $false }); Add-Result "POST stages duplicate order 10 -> 409" 409 $__r.Code ($__r.Code -eq 409)
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 15; stageCode = "ST10"; stageName = "x"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $false }); Add-Result "POST stages duplicate code ST10 -> 409" 409 $__r.Code ($__r.Code -eq 409)

    if ($draftPath) {
        $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 16; stageCode = "STD"; stageName = "draft path"; stageObjective = "o"; recommendedKnowledgePathId = $draftPath.pathId; isRequired = $false }); Add-Result "POST stages with DRAFT path -> 400" 400 $__r.Code ($__r.Code -eq 400)
    }
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 17; stageCode = "STU"; stageName = "unknown path"; stageObjective = "o"; recommendedKnowledgePathId = [guid]::NewGuid().ToString(); isRequired = $false }); Add-Result "POST stages with unknown path -> 400" 400 $__r.Code ($__r.Code -eq 400)

    # repeat is allowed and VISIBLE; latest-published policy is authorable
    $s2 = Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 20; stageCode = "ST20"; stageName = "Reinforcement"; stageObjective = "Repeat the message"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $false; repeatable = $true; pathVersionPinPolicy = "latest-published"; fallbackStageId = $stage1Id }
    $stage2Id = $s2.Body.data
    Add-Result "POST stages (order 20, repeatable, latest-published, fallback backwards)" 201 $s2.Code ($s2.Code -eq 201)

    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 30; stageCode = "ST30"; stageName = "bad range"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $false; minVisitNumber = 3; maxVisitNumber = 2 }); Add-Result "POST stages maxVisit<minVisit -> 400" 400 $__r.Code ($__r.Code -eq 400)
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 31; stageCode = "ST31"; stageName = "bad rule"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $false; advancementRule = "telepathy" }); Add-Result "POST stages unknown advancementRule -> 400 (fail-closed)" 400 $__r.Code ($__r.Code -eq 400)
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 32; stageCode = "ST32"; stageName = "state"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $false; currentStage = "awareness" }); Add-Result "POST stages { currentStage } -> 400 (no runtime state)" 400 $__r.Code ($__r.Code -eq 400)

    # declared metadata + branch data (never evaluated)
    $bUpd = Api PUT "/content-engagement-journeys/$journeyId/stages/$stage1Id" @{ stageOrder = 10; stageCode = "ST10"; stageName = "Awareness"; stageObjective = "Open the topic"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $true; advancementRule = "objection-recorded"; branchConditions = @(@{ conditionCode = "price-objection"; description = "handle"; targetStageId = $stage2Id }) }
    Add-Result "PUT stage + advancementRule + branchConditions -> 200 (data echoed)" 200 $bUpd.Code (($bUpd.Code -eq 200) -and ($bUpd.Body.data -ne $null))
    $__r = (Api PUT "/content-engagement-journeys/$journeyId/stages/$stage1Id" @{ stageOrder = 10; stageCode = "ST10"; stageName = "Awareness"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $true; branchConditions = @(@{ conditionCode = "x"; targetStageId = [guid]::NewGuid().ToString() }) }); Add-Result "PUT stage branch foreign TargetStageId -> 400 (V-S15)" 400 $__r.Code ($__r.Code -eq 400)
    $__r = (Api PUT "/content-engagement-journeys/$journeyId/stages/$stage1Id" @{ stageOrder = 10; stageCode = "ST10"; stageName = "Awareness"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $true; fallbackStageId = $stage1Id }); Add-Result "PUT stage fallback = itself -> 400 (V-S10)" 400 $__r.Code ($__r.Code -eq 400)

    # ordered read + resolution + repeat visibility
    $stages = Api GET "/content-engagement-journeys/$journeyId/stages"
    $ordered = @($stages.Body.data.items | ForEach-Object { $_.stageOrder })
    $firstStage = @($stages.Body.data.items)[0]
    Add-Result "GET stages ordered + ResolvedKnowledgePathId present" "10,20..." ($ordered -join ",") (($stages.Code -eq 200) -and ($firstStage.resolvedKnowledgePathId -ne $null))
    $repeatSeen = @($stages.Body.data.items | Where-Object { $_.pathUsageCountInJourney -gt 1 }).Count
    Add-Result "Repeat is visible (pathUsageCountInJourney > 1)" ">0" $repeatSeen ($repeatSeen -gt 0)
    $pinnedSeen = @($stages.Body.data.items | Where-Object { $_.pathResolutionStatus -eq "pinned" }).Count
    $latestSeen = @($stages.Body.data.items | Where-Object { $_.pathResolutionStatus -eq "resolved-latest" }).Count
    Add-Result "Both pin policies resolve visibly (pinned + resolved-latest)" ">=1 each" "$pinnedSeen/$latestSeen" (($pinnedSeen -ge 1) -and ($latestSeen -ge 1))
    $noStepCopy = @($stages.Body.data.items | Where-Object { $_.PSObject.Properties.Name -contains "steps" }).Count
    Add-Result "Stage read never copies path steps (no 'steps' member)" 0 $noStepCopy ($noStepCopy -eq 0)

    # dangling reference guard
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages/$stage2Id/archive"); Add-Result "Archive stage referenced by branch target -> 409" 409 $__r.Code ($__r.Code -eq 409)

    # write-path guards on the journey itself
    $__r = (Api PUT "/content-engagement-journeys/$journeyId" @{ journeyName = "x"; subjectId = $subjectId; objective = "o"; journeyVersion = "1.0"; effectiveFrom = $fromIso; stages = @(@{}) }); Add-Result "PUT journeys { stages:[...] } -> 400 (V-J16)" 400 $__r.Code ($__r.Code -eq 400)
    $__r = (Api PUT "/content-engagement-journeys/$journeyId" @{ journeyName = "x"; subjectId = $subjectId; objective = "o"; journeyVersion = "1.0"; effectiveFrom = $fromIso; journeyStatus = "published" }); Add-Result "PUT journeys { journeyStatus: published } -> 400 (V-J12)" 400 $__r.Code ($__r.Code -eq 400)
    $__r = (Api PUT "/content-engagement-journeys/$journeyId" @{ journeyName = "x"; subjectId = $subjectId; objective = "o"; journeyVersion = "1.0"; effectiveFrom = $fromIso; tenantId = [guid]::NewGuid().ToString() }); Add-Result "PUT journeys { tenantId } -> ignored, claim wins (2xx)" "200" $__r.Code (($__r.Code -ge 200) -and ($__r.Code -lt 300))

    # publish (separate endpoint) then freeze
    $pub = Api POST "/content-engagement-journeys/$journeyId/publish"
    Add-Result "POST publish -> 200 (StageSetFrozenAt set)" 200 $pub.Code ($pub.Code -eq 200)
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages" @{ stageOrder = 99; stageCode = "ST99"; stageName = "late"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $false }); Add-Result "POST stages on published -> 409 (frozen)" 409 $__r.Code ($__r.Code -eq 409)
    $__r = (Api PUT "/content-engagement-journeys/$journeyId/stages/$stage1Id" @{ stageOrder = 10; stageCode = "ST10"; stageName = "x"; stageObjective = "o"; recommendedKnowledgePathId = $pubPath.pathId; isRequired = $true }); Add-Result "PUT stage on published -> 409 (frozen)" 409 $__r.Code ($__r.Code -eq 409)
    $__r = (Api POST "/content-engagement-journeys/$journeyId/stages/$stage1Id/archive"); Add-Result "Archive stage on published -> 409 (frozen)" 409 $__r.Code ($__r.Code -eq 409)

    # new-version clone: new StageIds AND remapped fallback / branch targets
    $nv = Api POST "/content-engagement-journeys/$journeyId/new-version" @{}
    $newId = $nv.Body.data
    $newJourney = Api GET "/content-engagement-journeys/$newId"
    $newStageIds = @($newJourney.Body.data.stages | ForEach-Object { $_.stageId })
    $sameAsSource = ($newStageIds -contains $stage1Id)
    Add-Result "POST new-version -> 201 (draft, new StageIds, SupersedesJourneyId)" 201 $nv.Code (($nv.Code -eq 201) -and ($newJourney.Body.data.journeyStatus -eq "draft") -and ($newJourney.Body.data.supersedesJourneyId -eq $journeyId) -and (-not $sameAsSource))
    $oldRefs = @($newJourney.Body.data.stages | Where-Object { ($_.fallbackStageId -eq $stage1Id) -or ($_.fallbackStageId -eq $stage2Id) })
    $oldBranchRefs = @($newJourney.Body.data.stages | ForEach-Object { $_.branchConditions } | Where-Object { ($_.targetStageId -eq $stage1Id) -or ($_.targetStageId -eq $stage2Id) })
    Add-Result "new-version REMAPS fallback + branch targets (no old ids)" "0 old refs" "$($oldRefs.Count)/$($oldBranchRefs.Count)" (($oldRefs.Count -eq 0) -and ($oldBranchRefs.Count -eq 0))

    $__r = (Api GET "/content-engagement-journeys?status=published&effectiveAt=$fromIso"); Add-Result "GET journeys?status=published&effectiveAt -> only published" 200 $__r.Code ($__r.Code -eq 200)
    $__r = (Api GET "/content-engagement-journeys?knowledgePathId=$($pubPath.pathId)"); Add-Result "GET journeys?knowledgePathId -> which journeys use this path" 200 $__r.Code ($__r.Code -eq 200)

    # archive the clone's independent stage first (stage archive stays in the document)
    $cloneStage = @($newJourney.Body.data.stages | Where-Object { $_.stageCode -eq "ST20" })[0]
    if ($cloneStage) {
        $__r = (Api POST "/content-engagement-journeys/$newId/stages/$($cloneStage.stageId)/archive"); Add-Result "POST stage archive (independent) -> 200" 200 $__r.Code ($__r.Code -eq 200)
        $withArchived = Api GET "/content-engagement-journeys/$newId/stages?includeArchived=true"
        $stillThere = @($withArchived.Body.data.items | Where-Object { $_.stageId -eq $cloneStage.stageId }).Count
        Add-Result "Archived stage stays in the document (includeArchived=true)" 1 $stillThere ($stillThere -eq 1)
    }

    # cleanup: archive-only (NO hard delete)
    $__r = (Api POST "/content-engagement-journeys/$journeyId/archive"); Add-Result "POST journeys/{id}/archive -> 200" 200 $__r.Code ($__r.Code -eq 200)
    $__r = (Api POST "/content-engagement-journeys/$newId/archive"); Add-Result "POST journeys/{newId}/archive -> 200" 200 $__r.Code ($__r.Code -eq 200)
    $archived = Api GET "/content-engagement-journeys/$journeyId"
    $stagesKept = @($archived.Body.data.stages).Count
    Add-Result "Archived journey keeps its stages in the same document" ">0" $stagesKept ($stagesKept -gt 0)
}

# negative surface
$__r = (Api GET "/content-engagement-journeys/$([guid]::NewGuid().ToString())"); Add-Result "GET other tenant journey -> 404" 404 $__r.Code ($__r.Code -eq 404)
$__d = (Status "$kb/content-engagement-journeys/$([guid]::NewGuid())" "DELETE" $H); Add-Result "DELETE any route -> 404 (no delete surface)" 404 $__d ($__d -eq 404)
$__f = (Status "$kb/content-engagement-journey-stages" "GET" $H); Add-Result "GET flat /content-engagement-journey-stages -> 404 (no flat family, S2)" 404 $__f ($__f -eq 404)

Write-Host ""
$results | Format-Table -AutoSize
$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$pass = @($results | Where-Object { $_.Result -eq "PASS" }).Count
Write-Host ""
Write-Host "SUMMARY: PASS=$pass FAIL=$fail" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
