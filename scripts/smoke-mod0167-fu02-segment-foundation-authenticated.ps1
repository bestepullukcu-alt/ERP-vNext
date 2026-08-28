<#
  MOD-0167-FU02 — Authenticated Segment Foundation Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  never written to a file, and the Authorization header is never printed. Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0167-fu02-segment-foundation-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health. Nothing is hard-deleted:
  every record is closed with ARCHIVE. The script asserts the load-bearing FU02 promises:
    * a segment is a DEFINITION - resolve persists NOTHING (document counts and timestamps are compared around it)
    * resolution is DETERMINISTIC (three consecutive runs are compared bit for bit)
    * every eliminated candidate is returned WITH a reason - accepted + eliminated equals the candidate count
    * the attribute catalog is CLOSED, and concept.affinity is declared class D (+X on its value), never class X
    * activate FREEZES the criteria; changing the rule needs a new version whose node ids are REMAPPED
    * a superseded version stays RESOLVABLE and says so
    * a dynamic segment refuses manual membership rows
    * there is no DELETE and no PATCH surface anywhere

  PREREQUISITES (data, not code):
    * The Gateway must route /api/crm/segments and /api/crm/subjects to 5061 (follow-up F-GATEWAY). Without those
      routes EVERY call answers 404 with an empty {} body - that is a missing route, not a code defect.
    * The fleet must be running the FU02 build.
    * For the POSITIVE concept.affinity scenario the tenant needs a concept graph chain
      (global-product node --addresses/belongs-to--> reference-data-value node) and a contact whose specialty matches.
      Without it the script runs the EMPTY-SET scenario instead and marks it as a data gap, never as a code failure.

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

$seg = "$BaseUrl/api/crm/segments"
$run = (Get-Date -Format "yyyyMMddHHmmss")
$fromIso = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
$nowIso  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0167-FU02 authenticated segment foundation smoke ($run) ==" -ForegroundColor Cyan

foreach ($p in @(5000, 5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health" "200/204" $crmHealth ($crmHealth -in 200, 204)

# Probe with OPTIONS: an anonymous GET is refused by middleware BEFORE routing, so a 403 would tell us nothing about
# whether the Gateway route actually exists.
$routeProbe = Status "$seg/contract" "OPTIONS"
Add-Result "Gateway route present (OPTIONS probe)" "not 404" $routeProbe ($routeProbe -ne 404)
if ($routeProbe -eq 404) {
    Write-Host "Gateway has no /api/crm/segments route yet (follow-up F-GATEWAY). Everything below would 404." -ForegroundColor Yellow
}

$anon = Status "$seg/contract"
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
    $u = "$seg$Path"
    try {
        $p = @{ Uri = $u; Method = $Method; Headers = $H; TimeoutSec = 30; UseBasicParsing = $true }
        if ($null -ne $Obj) { $p.ContentType = "application/json"; $p.Body = ($Obj | ConvertTo-Json -Depth 10) }
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
function ErrorsOf($r) { if ($r.Body -and $r.Body.errors) { return @($r.Body.errors) } return @() }
function HasCode($r, [string]$Code) { return @(ErrorsOf $r | Where-Object { "$_" -eq $Code }).Count -gt 0 }

function Merge-Body($Base, $Overrides) {
    $copy = @{ }
    foreach ($k in $Base.Keys) { $copy[$k] = $Base[$k] }
    foreach ($k in $Overrides.Keys) { $copy[$k] = $Overrides[$k] }
    return $copy
}

function New-Predicate([string]$Attribute, [string]$Operator, [string]$ValueType, $Values, $Parameters = $null, [int]$SortOrder = 0) {
    return @{
        nodeId = [guid]::NewGuid().ToString(); parentNodeId = $null; nodeKind = "predicate"; groupOperator = $null
        attributeCode = $Attribute; operator = $Operator; values = @($Values); valueType = $ValueType
        parameters = $Parameters; negate = $false; sortOrder = $SortOrder; label = $null
    }
}
function New-SegmentBody([string]$Code, [string]$Type, [string]$Subject, $Criteria) {
    return @{
        segmentCode = $Code; segmentName = "Smoke $Code"; segmentType = $Type; subjectType = $Subject
        matchMode = "all"; effectiveFrom = $fromIso; effectiveTo = $null; businessUnitId = $null
        description = "MOD-0167-FU02 smoke"; notes = $null; criteria = $Criteria
    }
}

# ---------------------------------------------------------------- contract + catalog

$contract = Api GET "/contract"
Add-Result "GET /contract -> 200" 200 $contract.Code ($contract.Code -eq 200)
$flags = $contract.Body.data.features
Add-Result "contract: real-time resolution on" $true $flags.supportsRealTimeMembershipResolution ($flags.supportsRealTimeMembershipResolution -eq $true)
Add-Result "contract: product affinity on" $true $flags.supportsProductAffinityAttributes ($flags.supportsProductAffinityAttributes -eq $true)
Add-Result "contract: materialized membership OFF" $false $flags.supportsMaterializedMembership ($flags.supportsMaterializedMembership -eq $false)
Add-Result "contract: campaign target generation OFF" $false $flags.supportsCampaignTargetGeneration ($flags.supportsCampaignTargetGeneration -eq $false)
Add-Result "contract: frequency policy write OFF" $false $flags.supportsFrequencyPolicyWrite ($flags.supportsFrequencyPolicyWrite -eq $false)
Add-Result "contract: concept graph authoring OFF" $false $flags.supportsConceptGraphAuthoring ($flags.supportsConceptGraphAuthoring -eq $false)
Add-Result "contract: traversal engine OFF" $false $flags.supportsConceptGraphTraversalEngine ($flags.supportsConceptGraphTraversalEngine -eq $false)
Add-Result "contract: membership is NOT persisted" $false $contract.Body.data.limits.membershipIsPersisted ($contract.Body.data.limits.membershipIsPersisted -eq $false)
$maxCandidates = [int]$contract.Body.data.limits.maxCandidateSet
Add-Result "contract: candidate ceiling published" "10000" $maxCandidates ($maxCandidates -eq 10000)

$catalog = Api GET "/attribute-catalog"
Add-Result "GET /attribute-catalog -> 200" 200 $catalog.Code ($catalog.Code -eq 200)
$attributes = @($catalog.Body.data.attributes)
Add-Result "catalog is non-empty" "> 0" $attributes.Count ($attributes.Count -gt 0)
$affinity = $attributes | Where-Object { $_.attributeCode -eq "concept.affinity" } | Select-Object -First 1
Add-Result "concept.affinity declared class D (not X)" "D" $affinity.class ($affinity.class -eq "D")
Add-Result "concept.affinity value is proven cross-service (+X)" "D+X" $affinity.declaredClass ($affinity.declaredClass -eq "D+X")
Add-Result "concept.affinity is contact-only" "contact" (@($affinity.subjectTypes) -join ",") ((@($affinity.subjectTypes) -join ",") -eq "contact")
$consentAttr = $attributes | Where-Object { $_.attributeCode -eq "consent.eligibility" } | Select-Object -First 1
Add-Result "consent.eligibility declares required params" "channel+purpose" ((@($consentAttr.requiredParameters) | Sort-Object) -join ",") (((@($consentAttr.requiredParameters) | Sort-Object) -join ",") -eq "channel,purpose")
$forbidden = @($attributes | Where-Object { $_.attributeCode -like "visit.*" -or $_.attributeCode -like "frequency.*" -or $_.attributeCode -like "campaign.*" -or $_.attributeCode -like "segment.*" -or $_.attributeCode -like "icp.*" })
Add-Result "catalog carries no out-of-boundary attribute" 0 $forbidden.Count ($forbidden.Count -eq 0)

# ---------------------------------------------------------------- create + authoring guards

$dynCode = "smoke-dyn-$run"
$specialtyPredicate = @(New-Predicate "contact.specialty" "eq" "string" @("cardiology"))
$created = Api POST "" (New-SegmentBody $dynCode "dynamic" "contact" $specialtyPredicate)
Add-Result "POST /segments -> 201" 201 $created.Code ($created.Code -eq 201)
$dynId = $created.Body.data
Add-Result "create returns an id" "guid" $(if ($dynId) { "id" } else { "none" }) ([bool]$dynId)

$detail = Api GET "/$dynId"
Add-Result "new segment is draft" "draft" $detail.Body.data.segmentStatus ($detail.Body.data.segmentStatus -eq "draft")
Add-Result "new segment is business version 1" 1 $detail.Body.data.segmentVersion ([int]$detail.Body.data.segmentVersion -eq 1)
Add-Result "new segment is its own lineage root" "id" $(if ($detail.Body.data.versionLineageId -eq $dynId) { "same" } else { "different" }) ($detail.Body.data.versionLineageId -eq $dynId)
Add-Result "new segment criteria are NOT frozen" $false $detail.Body.data.isCriteriaFrozen ($detail.Body.data.isCriteriaFrozen -eq $false)

$dup = Api POST "" (New-SegmentBody $dynCode "dynamic" "contact" $specialtyPredicate)
Add-Result "duplicate SegmentCode -> 409" 409 $dup.Code ($dup.Code -eq 409)

$staticWithRule = Api POST "" (New-SegmentBody "smoke-badstatic-$run" "static" "contact" $specialtyPredicate)
Add-Result "static + criteria -> 400" 400 $staticWithRule.Code ($staticWithRule.Code -eq 400)

$dynWithoutRule = Api POST "" (New-SegmentBody "smoke-baddyn-$run" "dynamic" "contact" @())
Add-Result "dynamic without criteria -> 400" 400 $dynWithoutRule.Code ($dynWithoutRule.Code -eq 400)

$unknownAttr = Api POST "" (New-SegmentBody "smoke-badattr-$run" "dynamic" "contact" @(New-Predicate "contact.favourite-colour" "eq" "string" @("blue")))
Add-Result "undeclared attribute -> 400 segment_attribute_unknown" "segment_attribute_unknown" (ErrorsOf $unknownAttr | Select-Object -First 1) (($unknownAttr.Code -eq 400) -and (HasCode $unknownAttr "segment_attribute_unknown"))

$badOperator = Api POST "" (New-SegmentBody "smoke-badop-$run" "dynamic" "contact" @(New-Predicate "contact.is-primary" "between" "bool" @("true", "false")))
Add-Result "unsupported operator -> 400 segment_operator_not_supported" "segment_operator_not_supported" (ErrorsOf $badOperator | Select-Object -First 1) (($badOperator.Code -eq 400) -and (HasCode $badOperator "segment_operator_not_supported"))

$missingParam = Api POST "" (New-SegmentBody "smoke-badparam-$run" "dynamic" "contact" @(New-Predicate "consent.eligibility" "eq" "string" @("allowed")))
Add-Result "missing required parameter -> 400 segment_attribute_parameter_missing" "segment_attribute_parameter_missing" (ErrorsOf $missingParam | Select-Object -First 1) (($missingParam.Code -eq 400) -and (HasCode $missingParam "segment_attribute_parameter_missing"))

$tooManyValues = Api POST "" (New-SegmentBody "smoke-badin-$run" "dynamic" "contact" @(New-Predicate "contact.specialty" "in" "string" (0..50 | ForEach-Object { "v$_" })))
Add-Result "in with 51 values -> 400" 400 $tooManyValues.Code ($tooManyValues.Code -eq 400)

$tooManyNodes = Api POST "" (New-SegmentBody "smoke-badnodes-$run" "dynamic" "contact" (0..100 | ForEach-Object { New-Predicate "contact.specialty" "eq" "string" @("s$_") $null $_ }))
Add-Result "101 criteria nodes -> 400" 400 $tooManyNodes.Code ($tooManyNodes.Code -eq 400)

# concept.affinity guards. A real global-product id is needed, because the VALUE is proven cross-service before any
# write; a made-up id is correctly a 400 and would not exercise the depth/subject-type rules.
$productId = $null
try {
    $products = Invoke-RestMethod -Uri "$BaseUrl/api/global-products/selector?pageSize=1" -Headers $H -TimeoutSec 20
    $first = @($products.data) | Select-Object -First 1
    if ($first) { $productId = "$($first.id)"; if (-not $productId) { $productId = "$($first.value)" } }
} catch { }
Add-Result "PREREQ global product available for affinity" "one id" $(if ($productId) { "found" } else { "none (data gap)" }) ([bool]$productId)

if ($productId) {
    $depthParams = @{ maxDepth = "3" }
    $badDepth = Api POST "" (New-SegmentBody "smoke-baddepth-$run" "dynamic" "contact" @(New-Predicate "concept.affinity" "eq" "guid" @($productId) $depthParams))
    Add-Result "concept.affinity maxDepth=3 -> 400 segment_concept_depth_exceeded" "segment_concept_depth_exceeded" (ErrorsOf $badDepth | Select-Object -First 1) (($badDepth.Code -eq 400) -and (HasCode $badDepth "segment_concept_depth_exceeded"))

    $badSubject = Api POST "" (New-SegmentBody "smoke-badsubj-$run" "dynamic" "account" @(New-Predicate "concept.affinity" "eq" "guid" @($productId)))
    Add-Result "concept.affinity on an account segment -> 400" "segment_attribute_not_applicable_for_subject_type" (ErrorsOf $badSubject | Select-Object -First 1) (($badSubject.Code -eq 400) -and (HasCode $badSubject "segment_attribute_not_applicable_for_subject_type"))

    $affCode = "smoke-aff-$run"
    $affCreated = Api POST "" (New-SegmentBody $affCode "dynamic" "contact" @(New-Predicate "concept.affinity" "eq" "guid" @($productId)))
    Add-Result "concept.affinity segment with a REAL product -> 201" 201 $affCreated.Code ($affCreated.Code -eq 201)
    if ($affCreated.Code -eq 201) {
        $affId = $affCreated.Body.data
        $null = Api POST "/$affId/activate"
        $affResolve = Api POST "/$affId/resolve" @{ effectiveAt = $nowIso; limit = 50; offset = 0; includeExcluded = $true }
        Add-Result "affinity resolve -> 200 (never 503 for an in-service gap)" 200 $affResolve.Code ($affResolve.Code -eq 200)
        $affReasons = @($affResolve.Body.data.excluded | ForEach-Object { $_.reasonCodes }) -join ","
        $affMembers = @($affResolve.Body.data.members).Count
        if ($affMembers -gt 0) {
            Add-Result "affinity POSITIVE path (graph chain present)" "members > 0" $affMembers $true
        } else {
            Add-Result "affinity EMPTY-SET path (data gap, not a code fault)" "concept_* reason" $affReasons ($affReasons -like "*concept_*")
        }
        $null = Api POST "/$affId/archive"
    }
} else {
    Add-Result "concept.affinity guards skipped (no product)" "data prerequisite" "skipped" $true
}

# ---------------------------------------------------------------- lifecycle + freeze + versioning

$updateBody = @{
    segmentName = "Smoke $dynCode renamed"; segmentType = "dynamic"; segmentStatus = "draft"; matchMode = "all"
    effectiveFrom = $fromIso; effectiveTo = $null; businessUnitId = $null; description = "renamed"; notes = $null
    criteria = $null; expectedVersion = $null
}
$updated = Api PUT "/$dynId" $updateBody
Add-Result "PUT metadata (criteria omitted) -> 200" 200 $updated.Code ($updated.Code -eq 200)

$statusViaUpdate = Api PUT "/$dynId" (Merge-Body $updateBody @{ segmentStatus = "active" })
Add-Result "PUT cannot move the lifecycle -> 400" 400 $statusViaUpdate.Code ($statusViaUpdate.Code -eq 400)

$activated = Api POST "/$dynId/activate"
Add-Result "POST /activate -> 200/204" "200/204" $activated.Code ($activated.Code -in 200, 204)

$afterActivate = Api GET "/$dynId"
Add-Result "activate freezes the criteria" $true $afterActivate.Body.data.isCriteriaFrozen ($afterActivate.Body.data.isCriteriaFrozen -eq $true)
Add-Result "activate sets status active" "active" $afterActivate.Body.data.segmentStatus ($afterActivate.Body.data.segmentStatus -eq "active")
$v1Nodes = @($afterActivate.Body.data.criteria | ForEach-Object { "$($_.nodeId)" })

$frozenEdit = Api PUT "/$dynId" (Merge-Body $updateBody @{ segmentStatus = "active"; criteria = @(New-Predicate "contact.specialty" "eq" "string" @("oncology")) })
Add-Result "editing frozen criteria -> 409 segment_criteria_frozen" "segment_criteria_frozen" (ErrorsOf $frozenEdit | Select-Object -First 1) (($frozenEdit.Code -eq 409) -and (HasCode $frozenEdit "segment_criteria_frozen"))

$versioned = Api POST "/$dynId/new-version"
Add-Result "POST /new-version -> 201" 201 $versioned.Code ($versioned.Code -eq 201)
$v2Id = $versioned.Body.data
$v2 = Api GET "/$v2Id"
Add-Result "new version is draft" "draft" $v2.Body.data.segmentStatus ($v2.Body.data.segmentStatus -eq "draft")
Add-Result "new version is business version 2" 2 $v2.Body.data.segmentVersion ([int]$v2.Body.data.segmentVersion -eq 2)
Add-Result "new version keeps the lineage" "same" $(if ($v2.Body.data.versionLineageId -eq $afterActivate.Body.data.versionLineageId) { "same" } else { "different" }) ($v2.Body.data.versionLineageId -eq $afterActivate.Body.data.versionLineageId)
$v2Nodes = @($v2.Body.data.criteria | ForEach-Object { "$($_.nodeId)" })
$sharedNodes = @($v2Nodes | Where-Object { $v1Nodes -contains $_ })
Add-Result "clone REMAPS node ids (no id shared with v1)" 0 $sharedNodes.Count ($sharedNodes.Count -eq 0)

$null = Api POST "/$v2Id/activate"
$v1After = Api GET "/$dynId"
Add-Result "activating v2 supersedes v1" "v2 id" $(if ($v1After.Body.data.supersededBySegmentId -eq $v2Id) { "set" } else { "unset" }) ($v1After.Body.data.supersededBySegmentId -eq $v2Id)
Add-Result "the superseded version is NOT archived" $false $v1After.Body.data.isArchived ($v1After.Body.data.isArchived -eq $false)
$v1Resolve = Api POST "/$dynId/resolve" @{ effectiveAt = $nowIso; limit = 10; offset = 0; includeExcluded = $false }
Add-Result "a superseded version still resolves" 200 $v1Resolve.Code ($v1Resolve.Code -eq 200)
Add-Result "and it says superseded" $true $v1Resolve.Body.data.superseded ($v1Resolve.Body.data.superseded -eq $true)

# ---------------------------------------------------------------- determinism + persists-nothing

function Fingerprint($resolveResponse) {
    $members = @($resolveResponse.Body.data.members)
    return (@($members | ForEach-Object { "$($_.subjectId):$((@($_.reasonCodes)) -join '+')" }) -join "|")
}
$listBefore = Api GET "?includeArchived=true"
$countBefore = @($listBefore.Body.data.items).Count
$stampsBefore = (@($listBefore.Body.data.items | ForEach-Object { "$($_.segmentId):$($_.version):$($_.updatedAt)" }) -join "|")

$r1 = Api POST "/$v2Id/resolve" @{ effectiveAt = $nowIso; limit = 500; offset = 0; includeExcluded = $true }
$r2 = Api POST "/$v2Id/resolve" @{ effectiveAt = $nowIso; limit = 500; offset = 0; includeExcluded = $true }
$r3 = Api POST "/$v2Id/resolve" @{ effectiveAt = $nowIso; limit = 500; offset = 0; includeExcluded = $true }
Add-Result "POST /resolve -> 200" 200 $r1.Code ($r1.Code -eq 200)
$f1 = Fingerprint $r1; $f2 = Fingerprint $r2; $f3 = Fingerprint $r3
Add-Result "resolve run 1 == run 2 (same set, order and reasons)" "identical" $(if ($f1 -eq $f2) { "identical" } else { "different" }) ($f1 -eq $f2)
Add-Result "resolve run 2 == run 3" "identical" $(if ($f2 -eq $f3) { "identical" } else { "different" }) ($f2 -eq $f3)

$ids = @($r1.Body.data.members | ForEach-Object { "$($_.subjectId)" })
$sorted = @($ids | Sort-Object)
Add-Result "members are ordered by SubjectId" "sorted" $(if (($ids -join ",") -eq ($sorted -join ",")) { "sorted" } else { "unsorted" }) (($ids -join ",") -eq ($sorted -join ","))

$matched = [int]$r1.Body.data.matchedCount
$excluded = [int]$r1.Body.data.excludedCount
$candidates = [int]$r1.Body.data.candidateCount
Add-Result "accepted + eliminated == candidates" $candidates ($matched + $excluded) (($matched + $excluded) -eq $candidates)
$reasonless = @($r1.Body.data.excluded | Where-Object { @($_.reasonCodes).Count -eq 0 })
Add-Result "no candidate is eliminated silently" 0 $reasonless.Count ($reasonless.Count -eq 0)
Add-Result "resolve reports the published ceiling" $maxCandidates $r1.Body.data.maxCandidateSet ([int]$r1.Body.data.maxCandidateSet -eq $maxCandidates)

$listAfter = Api GET "?includeArchived=true"
$countAfter = @($listAfter.Body.data.items).Count
$stampsAfter = (@($listAfter.Body.data.items | ForEach-Object { "$($_.segmentId):$($_.version):$($_.updatedAt)" }) -join "|")
Add-Result "resolve creates no document" $countBefore $countAfter ($countBefore -eq $countAfter)
Add-Result "resolve changes no version or timestamp" "unchanged" $(if ($stampsBefore -eq $stampsAfter) { "unchanged" } else { "changed" }) ($stampsBefore -eq $stampsAfter)

# ---------------------------------------------------------------- manual membership

$dynTarget = Api POST "/$v2Id/targets" @{
    subjectType = "contact"; subjectId = [guid]::NewGuid().ToString(); membershipMode = "manual-include"
    selectionReason = "smoke"; reasonCodes = @("manual_include"); effectiveFrom = $fromIso; effectiveTo = $null
    subjectDisplayName = "Smoke"; notes = $null
}
Add-Result "manual row on a DYNAMIC segment -> 400" "segment_type_forbids_manual_membership" (ErrorsOf $dynTarget | Select-Object -First 1) (($dynTarget.Code -eq 400) -and (HasCode $dynTarget "segment_type_forbids_manual_membership"))

$hybridCode = "smoke-hyb-$run"
$hybrid = Api POST "" (New-SegmentBody $hybridCode "hybrid" "contact" $specialtyPredicate)
Add-Result "create hybrid segment -> 201" 201 $hybrid.Code ($hybrid.Code -eq 201)
$hybridId = $hybrid.Body.data
$null = Api POST "/$hybridId/activate"

$invited = [guid]::NewGuid().ToString()
$addTarget = Api POST "/$hybridId/targets" @{
    subjectType = "contact"; subjectId = $invited; membershipMode = "manual-include"
    selectionReason = "board decision"; reasonCodes = @("manual_include"); effectiveFrom = $fromIso; effectiveTo = $null
    subjectDisplayName = "Invited"; notes = $null
}
Add-Result "manual include on a HYBRID segment -> 201" 201 $addTarget.Code ($addTarget.Code -eq 201)
$targetId = $addTarget.Body.data

$dupTarget = Api POST "/$hybridId/targets" @{
    subjectType = "contact"; subjectId = $invited; membershipMode = "manual-exclude"
    selectionReason = "again"; reasonCodes = @("manual_exclude"); effectiveFrom = $fromIso; effectiveTo = $null
    subjectDisplayName = "Invited"; notes = $null
}
Add-Result "second live row for the same subject -> 409" 409 $dupTarget.Code ($dupTarget.Code -eq 409)

$noReason = Api POST "/$hybridId/targets" @{
    subjectType = "contact"; subjectId = [guid]::NewGuid().ToString(); membershipMode = "manual-include"
    selectionReason = "   "; reasonCodes = @("manual_include"); effectiveFrom = $fromIso; effectiveTo = $null
    subjectDisplayName = $null; notes = $null
}
Add-Result "manual membership without a reason -> 400" 400 $noReason.Code ($noReason.Code -eq 400)

$wrongSubject = Api POST "/$hybridId/targets" @{
    subjectType = "account"; subjectId = [guid]::NewGuid().ToString(); membershipMode = "manual-include"
    selectionReason = "wrong type"; reasonCodes = @("manual_include"); effectiveFrom = $fromIso; effectiveTo = $null
    subjectDisplayName = $null; notes = $null
}
Add-Result "subject type mismatch -> 400" "subject_type_mismatch" (ErrorsOf $wrongSubject | Select-Object -First 1) (($wrongSubject.Code -eq 400) -and (HasCode $wrongSubject "subject_type_mismatch"))

$hybridResolve = Api POST "/$hybridId/resolve" @{ effectiveAt = $nowIso; limit = 500; offset = 0; includeExcluded = $true }
$hybridMembers = @($hybridResolve.Body.data.members | ForEach-Object { "$($_.subjectId)" })
Add-Result "manual include appears in the hybrid result" "present" $(if ($hybridMembers -contains $invited) { "present" } else { "absent" }) ($hybridMembers -contains $invited)
$invitedRow = @($hybridResolve.Body.data.members | Where-Object { "$($_.subjectId)" -eq $invited }) | Select-Object -First 1
Add-Result "and it is labelled manual-include" "manual-include" $invitedRow.membershipSource ($invitedRow.membershipSource -eq "manual-include")

$switch = Api PUT "/$hybridId/targets/$targetId" @{
    membershipMode = "manual-exclude"; selectionReason = "escalated"; reasonCodes = @("manual_exclude")
    effectiveFrom = $fromIso; effectiveTo = $null; subjectDisplayName = "Invited"; notes = $null; expectedVersion = $null
}
Add-Result "include -> exclude is an UPDATE of the one row" 200 $switch.Code ($switch.Code -eq 200)
$afterSwitch = Api GET "/$hybridId/targets?includeArchived=true"
Add-Result "and it did not become a second row" 1 (@($afterSwitch.Body.data.items).Count) ((@($afterSwitch.Body.data.items).Count) -eq 1)

$excludeResolve = Api POST "/$hybridId/resolve" @{ effectiveAt = $nowIso; limit = 500; offset = 0; includeExcluded = $true }
$excludedIds = @($excludeResolve.Body.data.excluded | ForEach-Object { "$($_.subjectId)" })
Add-Result "manual exclude removes the subject from the result" "excluded" $(if ($excludedIds -contains $invited) { "excluded" } else { "still a member" }) ($excludedIds -contains $invited)

$archiveTarget = Api POST "/$hybridId/targets/$targetId/archive"
Add-Result "archive a manual row -> 200/204" "200/204" $archiveTarget.Code ($archiveTarget.Code -in 200, 204)
$updateArchived = Api PUT "/$hybridId/targets/$targetId" @{
    membershipMode = "manual-include"; selectionReason = "again"; reasonCodes = @("manual_include")
    effectiveFrom = $fromIso; effectiveTo = $null; subjectDisplayName = $null; notes = $null; expectedVersion = $null
}
Add-Result "an archived row accepts no update -> 409" 409 $updateArchived.Code ($updateArchived.Code -eq 409)

# ---------------------------------------------------------------- is-member + reverse question

$member = Api POST "/$v2Id/membership/evaluate" @{ subjectType = "contact"; subjectId = [guid]::NewGuid().ToString(); effectiveAt = $nowIso }
Add-Result "is-member -> 200" 200 $member.Code ($member.Code -eq 200)
Add-Result "an unknown subject is never 'member'" "not-member/unknown" $member.Body.data.verdict ($member.Body.data.verdict -in "not-member", "unknown")
Add-Result "and the verdict carries a reason" "reason" (@($member.Body.data.reasonCodes) -join ",") ((@($member.Body.data.reasonCodes)).Count -gt 0)

$mismatch = Api POST "/$v2Id/membership/evaluate" @{ subjectType = "account"; subjectId = [guid]::NewGuid().ToString(); effectiveAt = $nowIso }
Add-Result "asking an account question of a contact segment -> 400" 400 $mismatch.Code ($mismatch.Code -eq 400)

$reverse = $null
try {
    $reverse = Invoke-WebRequest -Uri "$BaseUrl/api/crm/subjects/contact/$([guid]::NewGuid())/segments" -Headers $H -TimeoutSec 30 -UseBasicParsing
} catch { $reverse = $_.Exception.Response }
$reverseCode = if ($reverse -and $reverse.StatusCode) { [int]$reverse.StatusCode } else { -1 }
Add-Result "GET /subjects/{type}/{id}/segments -> 200" 200 $reverseCode ($reverseCode -eq 200)

# ---------------------------------------------------------------- absent surfaces + isolation

$missing = Api GET "/$([guid]::NewGuid())"
Add-Result "a segment from outside this tenant -> 404" 404 $missing.Code ($missing.Code -eq 404)

$deleteCode = Status "$seg/$dynId" "DELETE" $H
Add-Result "DELETE surface does not exist" "404/405" $deleteCode ($deleteCode -in 404, 405)
$patchCode = Status "$seg/$dynId" "PATCH" $H
Add-Result "PATCH surface does not exist" "404/405" $patchCode ($patchCode -in 404, 405)

$archiveSegment = Api POST "/$hybridId/archive"
Add-Result "archive a segment -> 200/204" "200/204" $archiveSegment.Code ($archiveSegment.Code -in 200, 204)
$updateArchivedSegment = Api PUT "/$hybridId" (Merge-Body $updateBody @{ segmentStatus = "archived" })
Add-Result "an archived segment accepts no update -> 409" 409 $updateArchivedSegment.Code ($updateArchivedSegment.Code -eq 409)
$reactivate = Api POST "/$hybridId/activate"
Add-Result "an archived segment cannot be reactivated -> 409" 409 $reactivate.Code ($reactivate.Code -eq 409)

# Close the rest of the smoke fixtures. Archive, never delete.
foreach ($id in @($v2Id, $dynId)) { $null = Api POST "/$id/archive" }

# ---------------------------------------------------------------- report

$pass = @($results | Where-Object { $_.Result -eq "PASS" }).Count
$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$results | Format-Table -AutoSize
Write-Host ""
Write-Host "PASS: $pass   FAIL: $fail   TOTAL: $($results.Count)" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "NOT covered here (needs a controlled outage, so it is a manual check):" -ForegroundColor DarkGray
Write-Host "  * MDM unreachable while authoring a concept.affinity / consent.scope-* criterion must answer 503" -ForegroundColor DarkGray
Write-Host "    segment_dependency_unavailable AND leave NO new segment document behind." -ForegroundColor DarkGray
Write-Host "  * The 10.001-candidate ceiling (422 segment_candidate_set_too_large) needs a tenant large enough to" -ForegroundColor DarkGray
Write-Host "    exceed it; the unit suite covers the behaviour with a forced cap breach." -ForegroundColor DarkGray
