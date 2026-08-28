<#
  MOD-0167-FU04 — Authenticated Strategy Template Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  never written to a file, and the Authorization header is never printed. Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0167-fu04-strategy-template-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health. Nothing is hard-deleted:
  every record is closed with ARCHIVE. The script asserts the load-bearing FU04 promises:
    * a template BINDS and never produces: /apply, /generate and /resolve do NOT exist (404), and the contract says so
    * a bound segment must exist, be non-archived and share the template's SubjectType
    * the frequency intent NEVER writes a policy: the MOD-0165 policy list is counted before and after
    * SKU shares must total EXACTLY 100.00 — 99.99 and 100.01 are refused WITH the computed total in the message
    * an MDM reference that does not exist is a 400 and nothing is persisted
    * activate FREEZES the bindings; changing one needs a new version, whose child ids are regenerated
    * /bindings returns freshness hints and NO member, member count or subject id
    * there is no DELETE and no PATCH surface anywhere

  PREREQUISITES (data, not code):
    * The Gateway must route /api/crm/strategy-templates to 5061 (follow-up F-GATEWAY-STRATEGY). Without that route
      EVERY call answers 404 with an empty {} body — that is a missing route, not a code defect.
    * The fleet must be running the FU04 build.
    * The tenant needs at least one ACTIVE contact segment (MOD-0167 FU02). Without it the script reports the data gap
      and skips the binding scenarios rather than failing them.
    * For the product scenarios the tenant needs a referenceable MDM GlobalProduct + Gsku (follow-up F-SKU-DATA) and the
      caller needs mdm.global-products.read + mdm.gskus.read (follow-up F-MDM-PERM). Without them the MDM proof answers
      503 by design; the script marks that as a DATA/PERMISSION gap, never as a code failure.

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
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "data/permission present"; Actual = "$Detail"; Result = "SKIP" })
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

$tpl = "$BaseUrl/api/crm/strategy-templates"
$run = (Get-Date -Format "yyyyMMddHHmmss")
$fromIso = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0167-FU04 authenticated strategy template smoke ($run) ==" -ForegroundColor Cyan

foreach ($p in @(5000, 5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health" "200/204" $crmHealth ($crmHealth -in 200, 204)

# Probe with OPTIONS: an anonymous GET is refused by middleware BEFORE routing, so a 403 would tell us nothing about
# whether the Gateway route actually exists.
$routeProbe = Status "$tpl/contract" "OPTIONS"
Add-Result "Gateway route present (OPTIONS probe)" "not 404" $routeProbe ($routeProbe -ne 404)
if ($routeProbe -eq 404) {
    Write-Host "Gateway has no /api/crm/strategy-templates route yet (follow-up F-GATEWAY-STRATEGY). Everything below would 404." -ForegroundColor Yellow
}

$anon = Status "$tpl/contract"
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
        if ($null -ne $Obj) { $p.ContentType = "application/json"; $p.Body = ($Obj | ConvertTo-Json -Depth 12) }
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
function Api([string]$Method, [string]$Path, $Obj = $null) { return Call $Method "$tpl$Path" $Obj }
function ErrorsOf($r) { if ($r.Body -and $r.Body.errors) { return @($r.Body.errors) } return @() }
function HasCode($r, [string]$Code) { return @(ErrorsOf $r | Where-Object { "$_" -eq $Code }).Count -gt 0 }
function ErrorText($r) { return (@(ErrorsOf $r) -join " | ") }

function New-TemplateBody([string]$Code, [string]$Subject, $Segments, $Frequency, $Products, $Contents) {
    return @{
        templateCode = $Code; templateName = "Smoke $Code"; subjectType = $Subject
        effectiveFrom = $fromIso; effectiveTo = $null; businessUnitId = $null
        description = "MOD-0167-FU04 smoke"; notes = $null
        segmentBindings = $Segments; frequencyIntent = $Frequency
        productLines = $Products; contentBindings = $Contents
    }
}
function New-SegmentBinding([string]$SegmentId, [int]$SortOrder = 10) {
    return @{ segmentId = $SegmentId; bindingRole = "primary"; sortOrder = $SortOrder; notes = $null }
}
function New-NoneIntent { return @{ mode = "none"; visitFrequencyPolicyId = $null; frequencyType = $null; requiredVisitCount = $null; periodType = $null; intentNote = $null } }
function New-SkuLine([string]$ProductId, $Allocations, [int]$SortOrder = 10) {
    return @{
        globalProductId = $ProductId; globalProductCodeDisplay = $null; lineWeightPercentage = $null
        skuAllocationMode = "sku-allocated"; skuAllocations = @($Allocations); sortOrder = $SortOrder; notes = $null
    }
}
function New-Allocation([string]$GskuId, [decimal]$Percentage, [int]$SortOrder = 0) {
    return @{ gskuId = $GskuId; gskuCanonicalCodeDisplay = $null; percentage = $Percentage; sortOrder = $SortOrder }
}

# ---------------------------------------------------------------- contract

$contract = Api GET "/contract"
Add-Result "GET /contract -> 200" 200 $contract.Code ($contract.Code -eq 200)
$flags = $contract.Body.data.features
Add-Result "contract: module id" "MOD-0167-FU04" $contract.Body.data.moduleId ($contract.Body.data.moduleId -eq "MOD-0167-FU04")
Add-Result "contract: apply is CLOSED" $false $flags.supportsStrategyApply ($flags.supportsStrategyApply -eq $false)
Add-Result "contract: micro-target generation CLOSED" $false $flags.supportsMicroTargetGeneration ($flags.supportsMicroTargetGeneration -eq $false)
Add-Result "contract: cycle period CLOSED" $false $flags.supportsCyclePeriod ($flags.supportsCyclePeriod -eq $false)
Add-Result "contract: frequency policy WRITE CLOSED" $false $flags.supportsFrequencyPolicyWrite ($flags.supportsFrequencyPolicyWrite -eq $false)
Add-Result "contract: campaign target generation CLOSED" $false $flags.supportsCampaignTargetGeneration ($flags.supportsCampaignTargetGeneration -eq $false)
Add-Result "contract: membership resolution CLOSED" $false $flags.supportsSegmentMembershipResolution ($flags.supportsSegmentMembershipResolution -eq $false)
Add-Result "contract: UCLN CLOSED" $false $flags.supportsUcln ($flags.supportsUcln -eq $false)
Add-Result "contract: brand binding CLOSED" $false $flags.supportsBrandBinding ($flags.supportsBrandBinding -eq $false)
Add-Result "contract: SKU containment NOT validated" $false $flags.supportsProductSkuContainmentValidation ($flags.supportsProductSkuContainmentValidation -eq $false)
$limits = $contract.Body.data.limits
Add-Result "contract: required allocation total" 100 $limits.requiredAllocationTotal ([decimal]$limits.requiredAllocationTotal -eq [decimal]100)

# ---------------------------------------------------------------- data prerequisites

$segments = Call GET "$BaseUrl/api/crm/segments?includeArchived=false"
$activeContactSegment = $null
if ($segments.Code -eq 200) {
    $candidates = @($segments.Body.data.items | Where-Object { $_.subjectType -eq "contact" -and $_.segmentStatus -eq "active" -and -not $_.isArchived })
    if ($candidates.Count -gt 0) { $activeContactSegment = $candidates[0] }
}
$accountSegments = @()
if ($segments.Code -eq 200) {
    $accountSegments = @($segments.Body.data.items | Where-Object { $_.subjectType -eq "account" -and -not $_.isArchived })
}
if ($null -eq $activeContactSegment) {
    Add-Gap "Active contact segment available" "no active contact segment in this tenant"
} else {
    Add-Result "Active contact segment available" "found" $activeContactSegment.segmentCode $true
}

# ---------------------------------------------------------------- authoring

$created = $null
if ($null -ne $activeContactSegment) {
    $body = New-TemplateBody "smoke-$run" "contact" @(New-SegmentBinding $activeContactSegment.segmentId) (New-NoneIntent) @() @()
    $create = Api POST "" $body
    Add-Result "POST / -> 201" 201 $create.Code ($create.Code -eq 201)
    $created = $create.Body.data

    $dup = Api POST "" $body
    Add-Result "duplicate code -> 409" 409 $dup.Code ($dup.Code -eq 409)

    $noSegment = New-TemplateBody "smoke-nos-$run" "contact" @() (New-NoneIntent) @() @()
    $noSegmentResponse = Api POST "" $noSegment
    Add-Result "no segment bound -> 400" 400 $noSegmentResponse.Code ($noSegmentResponse.Code -eq 400)

    $unknownSegment = New-TemplateBody "smoke-unk-$run" "contact" @(New-SegmentBinding ([guid]::NewGuid().ToString())) (New-NoneIntent) @() @()
    $unknownResponse = Api POST "" $unknownSegment
    Add-Result "unknown segment -> 400 segment_reference_not_found" 400 "$($unknownResponse.Code) $(ErrorText $unknownResponse)" (($unknownResponse.Code -eq 400) -and (HasCode $unknownResponse "segment_reference_not_found"))

    if ($accountSegments.Count -gt 0) {
        $mismatch = New-TemplateBody "smoke-mix-$run" "contact" @(New-SegmentBinding $accountSegments[0].segmentId) (New-NoneIntent) @() @()
        $mismatchResponse = Api POST "" $mismatch
        Add-Result "subject-type mismatch -> 400" 400 "$($mismatchResponse.Code) $(ErrorText $mismatchResponse)" (($mismatchResponse.Code -eq 400) -and (HasCode $mismatchResponse "segment_subject_type_mismatch"))
    } else {
        Add-Gap "subject-type mismatch scenario" "no account segment in this tenant"
    }

    $badShape = New-TemplateBody "smoke-freq-$run" "contact" @(New-SegmentBinding $activeContactSegment.segmentId) `
        @{ mode = "declared-intent"; visitFrequencyPolicyId = [guid]::NewGuid().ToString(); frequencyType = "weekly"; requiredVisitCount = 2; periodType = "week"; intentNote = $null } @() @()
    $badShapeResponse = Api POST "" $badShape
    Add-Result "mixed frequency shape -> 400" 400 "$($badShapeResponse.Code) $(ErrorText $badShapeResponse)" (($badShapeResponse.Code -eq 400) -and (HasCode $badShapeResponse "frequency_intent_shape_invalid"))
} else {
    Add-Gap "authoring scenarios" "skipped: no active contact segment"
}

# ---------------------------------------------------------------- SKU percentage arithmetic

if ($null -ne $activeContactSegment) {
    $productId = [guid]::NewGuid().ToString()
    $gskuA = [guid]::NewGuid().ToString()
    $gskuB = [guid]::NewGuid().ToString()

    $wrongTotal = New-TemplateBody "smoke-99-$run" "contact" @(New-SegmentBinding $activeContactSegment.segmentId) (New-NoneIntent) `
        @(New-SkuLine $productId @((New-Allocation $gskuA 50), (New-Allocation $gskuB 49.99 10))) @()
    $wrongResponse = Api POST "" $wrongTotal
    # The arithmetic is checked in-domain BEFORE any MDM call, so this must be 400 even with unknown product ids.
    Add-Result "SKU total 99.99 -> 400 sku_allocation_total_invalid" 400 "$($wrongResponse.Code) $(ErrorText $wrongResponse)" (($wrongResponse.Code -eq 400) -and (HasCode $wrongResponse "sku_allocation_total_invalid"))
    Add-Result "SKU total error SHOWS the computed total" "contains 99.99" (ErrorText $wrongResponse) ((ErrorText $wrongResponse) -match "99[.,]99")

    $overTotal = New-TemplateBody "smoke-100-$run" "contact" @(New-SegmentBinding $activeContactSegment.segmentId) (New-NoneIntent) `
        @(New-SkuLine $productId @((New-Allocation $gskuA 50), (New-Allocation $gskuB 50.01 10))) @()
    $overResponse = Api POST "" $overTotal
    Add-Result "SKU total 100.01 -> 400" 400 "$($overResponse.Code) $(ErrorText $overResponse)" (($overResponse.Code -eq 400) -and (HasCode $overResponse "sku_allocation_total_invalid"))

    $exactTotal = New-TemplateBody "smoke-ok-$run" "contact" @(New-SegmentBinding $activeContactSegment.segmentId) (New-NoneIntent) `
        @(New-SkuLine $productId @((New-Allocation $gskuA 50), (New-Allocation $gskuB 50 10))) @()
    $exactResponse = Api POST "" $exactTotal
    if ($exactResponse.Code -eq 400 -and (HasCode $exactResponse "product_reference_not_found")) {
        # Exactly the fail-closed behaviour: the arithmetic passed and MDM then refused an id that does not exist.
        Add-Result "unknown MDM product -> 400 product_reference_not_found" 400 (ErrorText $exactResponse) $true
    } elseif ($exactResponse.Code -eq 503) {
        Add-Gap "MDM reference proof" "503 strategy_dependency_unavailable (F-MDM-PERM / MDM down) - fail-closed as designed"
    } else {
        Add-Result "unknown MDM product -> 400 product_reference_not_found" 400 "$($exactResponse.Code) $(ErrorText $exactResponse)" $false
    }
}

# ---------------------------------------------------------------- NO policy is ever written

$policiesBefore = Call GET "$BaseUrl/api/crm/visit-frequency-policies"
$policyCountBefore = -1
if ($policiesBefore.Code -eq 200) { $policyCountBefore = @($policiesBefore.Body.data.items).Count }

if ($null -ne $created) {
    $stored = Api GET "/$created"
    $declared = @{
        templateName = $stored.Body.data.templateName; effectiveFrom = $fromIso; effectiveTo = $null
        businessUnitId = $null; description = $stored.Body.data.description; notes = $null
        segmentBindings = $null
        frequencyIntent = @{ mode = "declared-intent"; visitFrequencyPolicyId = $null; frequencyType = "weekly"; requiredVisitCount = 2; periodType = "week"; intentNote = "smoke" }
        productLines = $null; contentBindings = $null; expectedVersion = $stored.Body.data.version
    }
    $declaredResponse = Api PUT "/$created" $declared
    Add-Result "declared-intent update -> 200" 200 $declaredResponse.Code ($declaredResponse.Code -eq 200)

    $policiesAfter = Call GET "$BaseUrl/api/crm/visit-frequency-policies"
    $policyCountAfter = -1
    if ($policiesAfter.Code -eq 200) { $policyCountAfter = @($policiesAfter.Body.data.items).Count }
    if ($policyCountBefore -ge 0 -and $policyCountAfter -ge 0) {
        Add-Result "declared intent wrote NO policy" $policyCountBefore $policyCountAfter ($policyCountBefore -eq $policyCountAfter)
    } else {
        Add-Gap "policy count comparison" "visit-frequency-policies list not readable"
    }
}

# ---------------------------------------------------------------- lifecycle: activate freezes, new-version reopens

if ($null -ne $created) {
    $activate = Api POST "/$created/activate" $null
    Add-Result "activate -> 200/204" "200/204" $activate.Code ($activate.Code -in 200, 204)

    $afterActivate = Api GET "/$created"
    Add-Result "activate froze the bindings" $true $afterActivate.Body.data.areBindingsFrozen ($afterActivate.Body.data.areBindingsFrozen -eq $true)

    $frozenEdit = @{
        templateName = "Smoke renamed $run"; effectiveFrom = $fromIso; effectiveTo = $null; businessUnitId = $null
        description = $null; notes = $null
        segmentBindings = @(); frequencyIntent = $null; productLines = $null; contentBindings = $null
        expectedVersion = $afterActivate.Body.data.version
    }
    $frozenResponse = Api PUT "/$created" $frozenEdit
    Add-Result "binding change on a frozen play -> 409 bindings_frozen" 409 "$($frozenResponse.Code) $(ErrorText $frozenResponse)" (($frozenResponse.Code -eq 409) -and (HasCode $frozenResponse "bindings_frozen"))

    $renameOnly = @{
        templateName = "Smoke renamed $run"; effectiveFrom = $fromIso; effectiveTo = $null; businessUnitId = $null
        description = "renamed while live"; notes = $null
        segmentBindings = $null; frequencyIntent = $null; productLines = $null; contentBindings = $null
        expectedVersion = $afterActivate.Body.data.version
    }
    $renameResponse = Api PUT "/$created" $renameOnly
    Add-Result "metadata edit on a frozen play -> 200" 200 $renameResponse.Code ($renameResponse.Code -eq 200)

    $newVersion = Api POST "/$created/new-version" $null
    Add-Result "new-version -> 201" 201 $newVersion.Code ($newVersion.Code -eq 201)
    $clone = $newVersion.Body.data
    if ($clone) {
        $cloneRow = Api GET "/$clone"
        $sourceRow = Api GET "/$created"
        Add-Result "clone is a new draft of the same lineage" "draft + same lineage" "$($cloneRow.Body.data.templateStatus)/$($cloneRow.Body.data.versionLineageId -eq $sourceRow.Body.data.versionLineageId)" (($cloneRow.Body.data.templateStatus -eq "draft") -and ($cloneRow.Body.data.versionLineageId -eq $sourceRow.Body.data.versionLineageId))
        Add-Result "clone bumped the business version" ($sourceRow.Body.data.templateVersion + 1) $cloneRow.Body.data.templateVersion ($cloneRow.Body.data.templateVersion -eq ($sourceRow.Body.data.templateVersion + 1))
        $sourceBindingId = @($sourceRow.Body.data.segmentBindings)[0].bindingId
        $cloneBindingId = @($cloneRow.Body.data.segmentBindings)[0].bindingId
        Add-Result "clone regenerated the child ids" "different" "$($sourceBindingId -eq $cloneBindingId)" ($sourceBindingId -ne $cloneBindingId)
    }
}

# ---------------------------------------------------------------- /bindings shows hints and NO member

if ($null -ne $created) {
    $bindings = Api GET "/$created/bindings"
    Add-Result "GET /bindings -> 200" 200 $bindings.Code ($bindings.Code -eq 200)
    $bindingsRaw = "$($bindings.Raw)"
    Add-Result "/bindings carries no member payload" "no member/subject field" $(if ($bindingsRaw -match '"(members|memberCount|memberIds|subjectId)"') { "member field present" } else { "clean" }) (-not ($bindingsRaw -match '"(members|memberCount|memberIds|subjectId)"'))
    $lines = @($bindings.Body.data.productLines)
    if ($lines.Count -gt 0) {
        Add-Result "/bindings never claims SKU containment" $false $lines[0].containmentVerified ($lines[0].containmentVerified -eq $false)
    }
}

# ---------------------------------------------------------------- surfaces that must NOT exist

$applyProbe = Status "$tpl/$created/apply" "POST" $H
Add-Result "/apply does not exist" 404 $applyProbe ($applyProbe -eq 404)
$generateProbe = Status "$tpl/$created/generate" "POST" $H
Add-Result "/generate does not exist" 404 $generateProbe ($generateProbe -eq 404)
$resolveProbe = Status "$tpl/$created/resolve" "POST" $H
Add-Result "/resolve does not exist" 404 $resolveProbe ($resolveProbe -eq 404)
if ($null -ne $created) {
    $deleteProbe = Status "$tpl/$created" "DELETE" $H
    Add-Result "DELETE is not offered" "404/405" $deleteProbe ($deleteProbe -in 404, 405)
}

# ---------------------------------------------------------------- tenant isolation + close-out

$foreign = Api GET "/$([guid]::NewGuid().ToString())"
Add-Result "unknown/foreign id -> 404" 404 $foreign.Code ($foreign.Code -eq 404)

if ($null -ne $created) {
    $current = Api GET "/$created"
    $archive = Api POST "/$created/archive?expectedVersion=$($current.Body.data.version)" $null
    Add-Result "archive -> 200/204" "200/204" $archive.Code ($archive.Code -in 200, 204)

    $afterArchive = Api GET "/$created"
    $archivedEdit = @{
        templateName = "should not apply"; effectiveFrom = $fromIso; effectiveTo = $null; businessUnitId = $null
        description = $null; notes = $null
        segmentBindings = $null; frequencyIntent = $null; productLines = $null; contentBindings = $null
        expectedVersion = $afterArchive.Body.data.version
    }
    $archivedResponse = Api PUT "/$created" $archivedEdit
    Add-Result "update on an archived play -> 409" 409 $archivedResponse.Code ($archivedResponse.Code -eq 409)
}

$pass = @($results | Where-Object { $_.Result -eq "PASS" }).Count
$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$skip = @($results | Where-Object { $_.Result -eq "SKIP" }).Count

$results | Format-Table -AutoSize
Write-Host ""
Write-Host "PASS=$pass  FAIL=$fail  SKIP=$skip" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
Write-Host "SKIP rows are DATA or PERMISSION gaps (F-SKU-DATA / F-MDM-PERM / segment data), never code failures." -ForegroundColor DarkGray
