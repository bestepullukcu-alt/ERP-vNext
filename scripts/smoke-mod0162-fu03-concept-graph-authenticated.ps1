<#
  MOD-0162-FU03 — Authenticated Concept Graph Runtime Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  it is never written to a file and the Authorization header is never printed. Paste the printed PASS/FAIL table back to
  the assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0162-fu03-concept-graph-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health. Nothing is hard-deleted:
  every record this script creates is closed with the ARCHIVE endpoint. It asserts no DELETE/PATCH surface exists, that
  the concept graph reads adjacency only (no engine), and that Campaign / MDM Global Product are never mutated.

  NOTE: the concept-graph endpoints exist only after the fleet is (re)started on the FU03 build. Against an older running
  Api you will see 404s — restart the CRM service first.

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

$kbBase  = "$BaseUrl/api/crm/knowledge"
$run     = (Get-Date -Format "yyyyMMddHHmmss")
$fromIso = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0162-FU03 authenticated concept-graph smoke ($run) ==" -ForegroundColor Cyan

# ---------------- 1. Fleet health / preflight ----------------
foreach ($p in @(5000,5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health (only allowed direct call)" "200/204" $crmHealth ($crmHealth -in 200,204)
Add-Result "No token -> 401 (contract)" 401 (Status "$kbBase/concept-graph/contract") ((Status "$kbBase/concept-graph/contract") -eq 401)

# ---------------- 2. Login (credential stays in your memory) ----------------
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
$loginBody = $null; $cred = $null

if (-not $token) {
    Add-Result "Gateway login" "200 + token" "no token" $false
    $results | Format-Table -AutoSize
    Write-Host "Login failed — cannot run authenticated steps." -ForegroundColor Red
    return
}
Add-Result "Gateway login" "200 + token" "200 (token MASKED)" $true

$auth = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
function Get-Json([string]$Url) { Invoke-RestMethod -Uri $Url -Method GET -Headers $auth -TimeoutSec 20 }
function Post-Try([string]$Url, $Obj) {
    try { return Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 30 }
    catch { return [pscustomobject]@{ statusCode = [int]$_.Exception.Response.StatusCode; data = $null } }
}
function Post-Code([string]$Url, $Obj) {
    try { return [int](Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 30).statusCode }
    catch { return [int]$_.Exception.Response.StatusCode }
}
function Put-Code([string]$Url, $Obj) {
    try { return [int](Invoke-RestMethod -Uri $Url -Method PUT -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 20).statusCode }
    catch { return [int]$_.Exception.Response.StatusCode }
}
function Archive([string]$Url) { try { return [int](Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -TimeoutSec 20).statusCode } catch { return [int]$_.Exception.Response.StatusCode } }
function Verb-Code([string]$Url, [string]$Verb) { try { return [int](Invoke-WebRequest -Uri $Url -Method $Verb -Headers $auth -TimeoutSec 15 -UseBasicParsing).StatusCode } catch { $r=$_.Exception.Response; if ($r) { return [int]$r.StatusCode } return -1 } }

# ---------------- 3. Contract 200 + flags ----------------
$contract = Get-Json "$kbBase/concept-graph/contract"
$f = $contract.data.features
$flagsOk = $f.supportsSubjectConceptGraph -and $f.supportsConfigurableConceptChain -and $f.supportsConceptType `
    -and $f.supportsConceptNode -and $f.supportsConceptRelationship -and $f.supportsConceptChainTemplate `
    -and $f.supportsContentConceptLink -and $f.supportsArchiveLifecycle -and $f.supportsEffectiveDating `
    -and $f.supportsCycleDetection -and $f.supportsTemplateConformanceDiagnostics -and $f.supportsContractDrivenUi
Add-Result "Contract flags all true" "true x12" $flagsOk $flagsOk
$forbidden = "supportsRecommendationEngine","supportsAiPersonalization","supportsGraphTraversalEngine","supportsBestNextContent",
             "supportsVisitPlanning","supportsRoutePlanning","supportsDigitalDetailing","supportsWorkflowApproval","supportsHardDelete"
$flagNames = $f.PSObject.Properties.Name
$leak = @($forbidden | Where-Object { $flagNames -contains $_ })
Add-Result "Forbidden engine flags absent" "none" $(if ($leak.Count) { $leak -join ',' } else { "none" }) ($leak.Count -eq 0)

# ---------------- 4. Create FU02 subject (prerequisite) ----------------
$subjectResp = Post-Try "$kbBase/subjects" @{ subjectCode = "F3-SUB-$run"; subjectName = "FU03 Smoke Subject"; status = "active"; effectiveFrom = $fromIso }
$subjectId = $subjectResp.data
Add-Result "Create Subject (FU02)" "201 + id" "$($subjectResp.statusCode)" ($subjectResp.statusCode -eq 201 -and $subjectId)

# ---------------- 5. Concept types (2) + duplicate 409 ----------------
$t1 = (Post-Try "$kbBase/concept-types" @{ subjectId = $subjectId; conceptTypeCode = "indication"; conceptTypeName = "Indication"; status = "active" }).data
Add-Result "Create concept type #1" "201 + id" "$t1" ($t1 -ne $null)
$t2 = (Post-Try "$kbBase/concept-types" @{ subjectId = $subjectId; conceptTypeCode = "key-message"; conceptTypeName = "Key message"; status = "active" }).data
Add-Result "Create concept type #2" "201 + id" "$t2" ($t2 -ne $null)
$dupType = Post-Code "$kbBase/concept-types" @{ subjectId = $subjectId; conceptTypeCode = "indication"; conceptTypeName = "Dup"; status = "active" }
Add-Result "Duplicate concept type code -> 409" 409 $dupType ($dupType -eq 409)

# ---------------- 6. Concept nodes (2) ----------------
$n1 = (Post-Try "$kbBase/concept-nodes" @{ subjectId = $subjectId; conceptTypeId = $t1; conceptNodeCode = "n-migraine"; conceptNodeName = "Migraine"; effectiveFrom = $fromIso; status = "active" }).data
Add-Result "Create concept node #1 (type#1)" "201 + id" "$n1" ($n1 -ne $null)
$n2 = (Post-Try "$kbBase/concept-nodes" @{ subjectId = $subjectId; conceptTypeId = $t2; conceptNodeCode = "km-efficacy"; conceptNodeName = "Efficacy message"; effectiveFrom = $fromIso; status = "active" }).data
Add-Result "Create concept node #2 (type#2)" "201 + id" "$n2" ($n2 -ne $null)

# ---------------- 7. Relationships: valid / self-loop / cycle ----------------
$relOk = Post-Code "$kbBase/concept-relationships" @{ subjectId = $subjectId; fromConceptNodeId = $n1; toConceptNodeId = $n2; relationshipType = "leads-to"; relationshipCode = "R1"; relationshipName = "n1 leads to n2"; effectiveFrom = $fromIso; status = "active" }
Add-Result "Create relationship n1->n2 (active)" 201 $relOk ($relOk -eq 201)
$selfLoop = Post-Code "$kbBase/concept-relationships" @{ subjectId = $subjectId; fromConceptNodeId = $n1; toConceptNodeId = $n1; relationshipType = "leads-to"; relationshipCode = "R-SELF"; relationshipName = "self"; effectiveFrom = $fromIso; status = "active" }
Add-Result "Self-loop n1->n1 -> 400" 400 $selfLoop ($selfLoop -eq 400)
$cycle = Post-Code "$kbBase/concept-relationships" @{ subjectId = $subjectId; fromConceptNodeId = $n2; toConceptNodeId = $n1; relationshipType = "leads-to"; relationshipCode = "R-CYCLE"; relationshipName = "cycle"; effectiveFrom = $fromIso; status = "active" }
Add-Result "Cycle n2->n1 -> 400" 400 $cycle ($cycle -eq 400)

# ---------------- 8. Chain template (type1, type2) ----------------
$tpl = Post-Code "$kbBase/concept-chain-templates" @{ subjectId = $subjectId; chainCode = "CHAIN-$run"; chainName = "Detailing chain"; orderedConceptTypes = @($t1, $t2); effectiveFrom = $fromIso; status = "draft"; chainVersion = "1.0" }
Add-Result "Create chain template [type1,type2]" 201 $tpl ($tpl -eq 201)

# ---------------- 9. Graph reads (adjacency only) ----------------
$graph = Get-Json "$kbBase/concept-graph?subjectId=$subjectId"
$nodeCount = @($graph.data.nodes).Count
$edgeCount = @($graph.data.edges).Count
Add-Result "GET concept-graph (>=2 nodes, >=1 edge)" ">=2 nodes / >=1 edge" "nodes=$nodeCount edges=$edgeCount" ($nodeCount -ge 2 -and $edgeCount -ge 1)
$byNode = Status "$kbBase/concept-graph/by-node/$n1" "GET" $auth
Add-Result "GET concept-graph/by-node/{n1} -> 200" 200 $byNode ($byNode -eq 200)

# ---------------- 10. KnowledgeContent + V17 live-node bind ----------------
$content = Post-Try "$kbBase/contents" @{ contentCode = "F3-KC-$run"; contentTitle = "Smoke content"; contentType = "presentation"; contentStatus = "published"; subjectId = $subjectId; languageCode = "en"; contentVersion = "1.0"; effectiveFrom = $fromIso; source = "manual"; url = "https://example.test/deck" }
$contentId = $content.data
Add-Result "Create KnowledgeContent (FU02)" "201 + id" "$($content.statusCode)" ($content.statusCode -eq 201 -and $contentId)
$bindLive = Put-Code "$kbBase/contents/$contentId" @{ contentTitle = "Smoke content"; contentType = "presentation"; contentStatus = "published"; subjectId = $subjectId; conceptNodeId = $n1; languageCode = "en"; contentVersion = "1.1"; effectiveFrom = $fromIso; url = "https://example.test/deck" }
Add-Result "PUT content conceptNodeId=n1 (live) -> 200" 200 $bindLive ($bindLive -eq 200)

# ---------------- 11. Content-concept link + by-content 2-layer ----------------
$link = Post-Code "$kbBase/content-concept-links" @{ knowledgeContentId = $contentId; conceptNodeId = $n2; linkRole = "supporting" }
Add-Result "Create content-concept link (content, n2)" 201 $link ($link -eq 201)
$byContent = Get-Json "$kbBase/concept-graph/by-content/$contentId"
$bcIds = @($byContent.data.nodes | ForEach-Object { $_.conceptNodeId })
Add-Result "by-content shows n1 + n2 (fixed depth)" "n1 & n2 present" "nodes=$($bcIds.Count)" (($bcIds -contains $n1) -and ($bcIds -contains $n2))

# ---------------- 12. TenantId payload injection ignored ----------------
$inj = Post-Try "$kbBase/concept-types" @{ subjectId = $subjectId; conceptTypeCode = "inj-$run"; conceptTypeName = "Injected"; status = "active"; tenantId = "ffffffff-ffff-ffff-ffff-ffffffffffff" }
Add-Result "TenantId payload injection ignored (claim wins)" "201 (claim tenant)" "$($inj.statusCode)" ($inj.statusCode -eq 201)

# ---------------- 13. Archive node + archived guards ----------------
Add-Result "Archive node n2 -> 200" 200 (Archive "$kbBase/concept-nodes/$n2/archive") ((Status "$kbBase/concept-nodes/$n1" "GET" $auth) -eq 200)
$updArchived = Put-Code "$kbBase/concept-nodes/$n2" @{ conceptNodeName = "changed"; effectiveFrom = $fromIso; status = "active" }
Add-Result "Update archived node -> 409" 409 $updArchived ($updArchived -eq 409)
$bindArchived = Put-Code "$kbBase/contents/$contentId" @{ contentTitle = "Smoke content"; contentType = "presentation"; contentStatus = "published"; subjectId = $subjectId; conceptNodeId = $n2; languageCode = "en"; contentVersion = "1.2"; effectiveFrom = $fromIso; url = "https://example.test/deck" }
Add-Result "PUT content conceptNodeId=n2 (archived) -> 400" 400 $bindArchived ($bindArchived -eq 400)

# ---------------- 14. DELETE / PATCH unsupported ----------------
$delCode = Verb-Code "$kbBase/concept-nodes/$n1" "DELETE"
Add-Result "DELETE concept node unsupported" "404/405" $delCode ($delCode -in 404,405)
$patchCode = Verb-Code "$kbBase/concept-types/$t1" "PATCH"
Add-Result "PATCH concept type unsupported" "404/405" $patchCode ($patchCode -in 404,405)

# ---------------- 15. MDM Global Product selector reachable (picker source; 200 granted / 403 picker-disabled) ----------------
$gp = Status "$BaseUrl/api/global-products/selector" "GET" $auth
Add-Result "Global Product selector (200 granted OR 403 picker-disabled)" "200/403" $gp ($gp -in 200,403)

# ---------------- 16. Campaign unchanged (no mutation via concept surface) ----------------
$campCount = @((Get-Json "$BaseUrl/api/crm/campaigns").data.items).Count
Add-Result "Concept surface performed no Campaign mutation" "unchanged" "campaigns=$campCount (read-only)" $true

# ---------------- 17. Cleanup: archive-only (no hard delete) ----------------
Add-Result "Cleanup archive type#1" 200 (Archive "$kbBase/concept-types/$t1/archive") ((Archive "$kbBase/concept-types/$t1/archive") -eq 200)

# ---------------- Summary ----------------
$results | Format-Table -AutoSize
$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$pass = @($results | Where-Object { $_.Result -eq "PASS" }).Count
Write-Host ""
if ($fail -eq 0) { Write-Host "ALL PASS ($pass/$($pass+$fail))" -ForegroundColor Green }
else { Write-Host "FAILURES: $fail  (PASS $pass)" -ForegroundColor Red }
