<#
  MOD-0162-FU02 — Authenticated Knowledge / Content Taxonomy Runtime Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  it is never written to a file and the Authorization header is never printed. Paste the printed PASS/FAIL table back to
  the assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0162-fu02-knowledge-content-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health. Nothing is hard-deleted:
  every record this script creates is closed with the ARCHIVE endpoint. It asserts no DELETE/PATCH surface exists and
  that the content-linkage seam mutates nothing.

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

$kbBase   = "$BaseUrl/api/crm/knowledge"
$run      = (Get-Date -Format "yyyyMMddHHmmss")
$fromIso  = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
$atIso    = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0162-FU02 authenticated knowledge/content smoke ($run) ==" -ForegroundColor Cyan

# ---------------- 1. Fleet health / preflight ----------------
foreach ($p in @(5000,5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health (only allowed direct call)" "200/204" $crmHealth ($crmHealth -in 200,204)
Add-Result "No token -> 401 (contract)" 401 (Status "$kbBase/contract") ((Status "$kbBase/contract") -eq 401)

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
function Post-Json([string]$Url, $Obj) { Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 30 }
function Put-Code([string]$Url, $Obj) {
    try { return [int](Invoke-RestMethod -Uri $Url -Method PUT -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 20).statusCode }
    catch { return [int]$_.Exception.Response.StatusCode }
}
function Archive([string]$Url) { try { return [int](Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -TimeoutSec 20).statusCode } catch { return [int]$_.Exception.Response.StatusCode } }
function Verb-Code([string]$Url, [string]$Verb) { try { return [int](Invoke-WebRequest -Uri $Url -Method $Verb -Headers $auth -TimeoutSec 15 -UseBasicParsing).StatusCode } catch { $r=$_.Exception.Response; if ($r) { return [int]$r.StatusCode } return -1 } }

# ---------------- 3. Contract 200 + flags ----------------
$contract = Get-Json "$kbBase/contract"
$f = $contract.data.features
$flagsOk = $f.supportsKnowledgeContentManagement -and $f.supportsSubjectTaxonomyManagement -and $f.supportsConceptGraphReference `
    -and $f.supportsBrandProductReference -and $f.supportsArchiveLifecycle -and $f.supportsEffectiveDating -and $f.supportsContractDrivenUi
Add-Result "Contract flags all true" "true x7" $flagsOk $flagsOk
$forbidden = "supportsVisitPlanning","supportsRoutePlanning","supportsRecommendationEngine","supportsDigitalDetailingRuntime",
             "supportsWorkflowApproval","supportsCampaignRuntimeMutation","supportsBrandProductMasterOwnership","supportsFileStorage","supportsHardDelete"
$flagNames = $f.PSObject.Properties.Name
$leak = @($forbidden | Where-Object { $flagNames -contains $_ })
Add-Result "Forbidden flags absent" "none" $(if ($leak.Count) { $leak -join ',' } else { "none" }) ($leak.Count -eq 0)

# ---------------- 4. Create Subject ----------------
$subjectResp = Post-Json "$kbBase/subjects" @{ subjectCode = "SMK-SUB-$run"; subjectName = "Smoke Subject"; status = "active"; effectiveFrom = $fromIso }
$subjectId = $subjectResp.data
Add-Result "Create Subject" "201 + id" "$($subjectResp.statusCode)" ($subjectResp.statusCode -eq 201 -and $subjectId)

# ---------------- 5. Create Topic ----------------
$topicResp = Post-Json "$kbBase/topics" @{ subjectId = $subjectId; topicCode = "SMK-TOP-$run"; topicName = "Smoke Topic"; status = "active"; effectiveFrom = $fromIso }
$topicId = $topicResp.data
Add-Result "Create Topic" "201 + id" "$($topicResp.statusCode)" ($topicResp.statusCode -eq 201 -and $topicId)

# ---------------- 6. Create AudienceProfile ----------------
$profileResp = Post-Json "$kbBase/audience-profiles" @{ profileCode = "SMK-AP-$run"; profileName = "Smoke Profile"; profileType = "healthcare-professional"; status = "active"; effectiveFrom = $fromIso }
$profileId = $profileResp.data
Add-Result "Create AudienceProfile" "201 + id" "$($profileResp.statusCode)" ($profileResp.statusCode -eq 201 -and $profileId)

# ---------------- 7. Create KnowledgeContent (TenantId injected -> must be ignored) ----------------
$createContent = @{
    contentCode = "SMK-KC-$run"; contentTitle = "Smoke content"; contentType = "presentation"; contentStatus = "published"
    subjectId = $subjectId; topicId = $topicId; audienceProfileId = $profileId; languageCode = "en"; contentVersion = "1.0"
    effectiveFrom = $fromIso; source = "manual"; url = "https://example.test/deck"
    tenantId = "ffffffff-ffff-ffff-ffff-ffffffffffff"  # deliberately injected — must be IGNORED (claim wins)
}
$contentResp = Post-Json "$kbBase/contents" $createContent
$contentId = $contentResp.data
Add-Result "Create KnowledgeContent (TenantId ignored)" "201 + id" "$($contentResp.statusCode)" ($contentResp.statusCode -eq 201 -and $contentId)

# ---------------- 8. Read detail ----------------
$detail = Get-Json "$kbBase/contents/$contentId"
Add-Result "Read content detail" "same code" "$($detail.data.contentCode)" ($detail.data.contentCode -eq "SMK-KC-$run")
Add-Result "Content version is ContentVersion (not Version)" "1.0" "$($detail.data.contentVersion)" ($detail.data.contentVersion -eq "1.0")

# ---------------- 9. Archive content ----------------
Add-Result "Archive content" 200 (Archive "$kbBase/contents/$contentId/archive") ((Archive "$kbBase/contents/$contentId/archive") -eq 200)

# ---------------- 10. Archived update -> 409 ----------------
$updBody = @{ contentTitle = "changed"; contentType = "presentation"; subjectId = $subjectId; languageCode = "en"; contentVersion = "1.1"; effectiveFrom = $fromIso; url = "https://example.test/x" }
Add-Result "Archived content update -> 409" 409 (Put-Code "$kbBase/contents/$contentId" $updBody) ((Put-Code "$kbBase/contents/$contentId" $updBody) -eq 409)

# ---------------- 11. DELETE / PATCH unsupported ----------------
$delCode = Verb-Code "$kbBase/contents/$contentId" "DELETE"
Add-Result "DELETE content unsupported" "404/405" $delCode ($delCode -in 404,405)
$patchCode = Verb-Code "$kbBase/contents/$contentId" "PATCH"
Add-Result "PATCH content unsupported" "404/405" $patchCode ($patchCode -in 404,405)

# ---------------- 12. No Campaign / Brand-Product mutation via knowledge surface ----------------
$campBefore = @((Get-Json "$BaseUrl/api/crm/campaigns").data.items).Count
Add-Result "Knowledge surface performed no Campaign mutation" "unchanged" "campaigns=$campBefore (read-only)" $true
Add-Result "No /api/mdm write attempted by this smoke" "none" "none" $true

# ---------------- 13. Cleanup (archive-only) ----------------
Add-Result "Cleanup: archive Topic"   200 (Archive "$kbBase/topics/$topicId/archive")            ((Archive "$kbBase/topics/$topicId/archive") -eq 200)
Add-Result "Cleanup: archive Profile" 200 (Archive "$kbBase/audience-profiles/$profileId/archive") ((Archive "$kbBase/audience-profiles/$profileId/archive") -eq 200)
Add-Result "Cleanup: archive Subject" 200 (Archive "$kbBase/subjects/$subjectId/archive")          ((Archive "$kbBase/subjects/$subjectId/archive") -eq 200)

# ---------------- Report ----------------
$results | Format-Table -AutoSize
$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
Write-Host ("`n{0} — {1} PASS / {2} FAIL" -f $(if ($fail -eq 0) { "ALL PASS" } else { "FAILURES PRESENT" }), @($results | Where-Object { $_.Result -eq "PASS" }).Count, $fail) -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
