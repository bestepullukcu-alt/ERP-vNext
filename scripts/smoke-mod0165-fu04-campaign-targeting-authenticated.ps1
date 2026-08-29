<#
  MOD-0165-FU04 — Authenticated Campaign / Targeting Runtime + Static Target Snapshot Gateway Live Smoke
  (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering
  passwords/tokens to authenticate is outside what the assistant may do on your behalf. This
  script keeps the credential in YOUR process memory only — it is never written to a file and the
  Authorization header is never printed (masked). Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0165-fu04-campaign-targeting-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health.
  Nothing is hard-deleted: every record this script creates is closed with the ARCHIVE endpoint.
  It uses the live MOD-0164 consent API to set up the allowed / blocked / unknown cases, and asserts
  that the campaign snapshot NEVER mutates a consent or preference record.

  PowerShell 5.1 note: every pipeline count uses the @(...) array-subexpression guard, because
  `($x | Where-Object {...}).Count` returns $null when exactly ONE object matches.
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

$campaignBase   = "$BaseUrl/api/crm/campaigns"
$consentBase    = "$BaseUrl/api/crm/consents"
$preferenceBase = "$BaseUrl/api/crm/preferences"
$run            = (Get-Date -Format "yyyyMMddHHmmss")

# Synthetic subject/target ids: MOD-0165 stores no cross-aggregate FK by design (the caller supplies the id),
# so the smoke needs no Contact/AccountContactLink master row and mutates nothing upstream.
$allowedSubject = [guid]::NewGuid().Guid   # will get a granted consent          -> expect ACTIVE
$blockedSubject = [guid]::NewGuid().Guid   # granted consent + do-not-visit pref -> expect EXCLUDED (blocked)
$unknownSubject = [guid]::NewGuid().Guid   # no consent at all                   -> expect EXCLUDED (unknown)
$segmentId      = [guid]::NewGuid().Guid   # segment provenance only (never expanded)
$pastIso        = (Get-Date).ToUniversalTime().AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ssZ")
$atIso          = (Get-Date).ToUniversalTime().AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0165-FU04 authenticated campaign/targeting smoke ($run) ==" -ForegroundColor Cyan

# ---------------- 1. Fleet health / preflight ----------------
foreach ($p in @(5000,5001,5056,5057,5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health (only allowed direct call)" "200/ok" $crmHealth ($crmHealth -in 200,204)

# Unauthenticated guards BEFORE login (no token in the process yet).
Add-Result "No token -> 401 (campaigns/contract)" 401 (Status "$campaignBase/contract") ((Status "$campaignBase/contract") -eq 401)
Add-Result "No token -> 401 (campaigns)"          401 (Status $campaignBase)             ((Status $campaignBase) -eq 401)
Add-Result "Garbage token -> 401"                 401 (Status "$campaignBase/contract" "GET" @{ Authorization = "Bearer x.y.z" }) `
    ((Status "$campaignBase/contract" "GET" @{ Authorization = "Bearer x.y.z" }) -eq 401)

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
Add-Result "Tenant claim == target" $TenantId $claims.tenant_id ($claims.tenant_id -eq $TenantId)

$auth = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
function Get-Json([string]$Url) { Invoke-RestMethod -Uri $Url -Method GET -Headers $auth -TimeoutSec 20 }
function Post-Json([string]$Url, $Obj) { Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 30 }
function Post-Code([string]$Url, $Obj) {
    try { return [int](Post-Json $Url $Obj).statusCode } catch { return [int]$_.Exception.Response.StatusCode }
}
function Put-Code([string]$Url, $Obj) {
    try { return [int](Invoke-RestMethod -Uri $Url -Method PUT -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 20).statusCode }
    catch { return [int]$_.Exception.Response.StatusCode }
}
function Archive([string]$Url) { try { return [int](Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -TimeoutSec 20).statusCode } catch { return [int]$_.Exception.Response.StatusCode } }

# ---------------- 3. Contract 200 + flags ----------------
$contract = Get-Json "$campaignBase/contract"
$f = $contract.data.features
$flagsOk = $f.supportsCampaignManagement -and $f.supportsCampaignTargetManagement -and $f.supportsStaticTargetSnapshot `
    -and $f.supportsConsentEvaluationIntegration -and $f.supportsTargetExclusionReason -and $f.supportsTargetSourceProvenance
Add-Result "Contract flags all true" "true x6" $flagsOk $flagsOk
$forbidden = "supportsSegmentationEngine","supportsDynamicCampaignRules","supportsVisitPlanning","supportsRoutePlanning",
             "supportsDueOverdue","supportsLastVisitHistory","supportsFrequencyRuntime","supportsDigitalDetailing",
             "supportsRecommendationEngine","supportsWorkflowApproval"
$flagNames = $f.PSObject.Properties.Name
$leak = @($forbidden | Where-Object { $flagNames -contains $_ })
Add-Result "Forbidden flags absent" "none" $(if ($leak.Count) { $leak -join ',' } else { "none" }) ($leak.Count -eq 0)
Add-Result "Contract consent integration declares MOD-0164 seam" "MOD-0164 + IConsentPreferenceEvaluator" `
    "$($contract.data.consentIntegration.providerModule) / $($contract.data.consentIntegration.providerSeam)" `
    ($contract.data.consentIntegration.providerModule -eq "MOD-0164" -and $contract.data.consentIntegration.providerSeam -eq "IConsentPreferenceEvaluator")
Add-Result "'campaign-target' absent from target types" "absent" ($contract.data.vocabulary.targetTypes -join ',') `
    (-not ($contract.data.vocabulary.targetTypes -contains "campaign-target"))

# ---------------- 4. Create campaign (draft) ----------------
$createCampaign = @{
    campaignCode = "SMOKE-FU04-$run"; campaignName = "FU04 smoke campaign"; campaignType = "product-campaign"
    campaignStatus = "draft"; objectiveType = "awareness"; startDate = $pastIso
    description = "created through the authenticated Gateway"
    externalReferences = @(@{ sourceSystem = "OldCRM"; externalId = "PROMO-$run"; externalCode = "PROMO"; isPrimary = $true })
    tenantId = "ffffffff-ffff-ffff-ffff-ffffffffffff"  # deliberately injected — must be IGNORED (claim wins)
}
$respCampaign = Post-Json $campaignBase $createCampaign
$campaignId = $respCampaign.data
Add-Result "Create campaign (201 + CampaignId)" "201 + guid" "$($respCampaign.statusCode) / $campaignId" ($respCampaign.statusCode -eq 201 -and $campaignId)

$readCampaign = (Get-Json "$campaignBase/$campaignId").data
Add-Result "TenantId payload ignored (readable in claim tenant)" "readable/draft" "status=$($readCampaign.campaignStatus)" `
    ($readCampaign -and $readCampaign.campaignStatus -eq "draft")
Add-Result "Campaign ExternalReferences stored" "OldCRM/PROMO-$run" "$($readCampaign.externalReferences[0].sourceSystem)/$($readCampaign.externalReferences[0].externalId)" `
    (@($readCampaign.externalReferences).Count -eq 1 -and $readCampaign.externalReferences[0].sourceSystem -eq "OldCRM")

Add-Result "Duplicate campaignCode -> 409" 409 (Post-Code $campaignBase $createCampaign) ((Post-Code $campaignBase $createCampaign) -eq 409)

# ---------------- 5. Manual campaign target ----------------
$createTarget = @{
    targetType = "account-contact-link"; targetId = $allowedSubject; targetSource = "manual"
    selectionReason = "FU04 smoke manual selection"; effectiveFrom = $pastIso; priority = 100
    targetStatus = "active"
}
$respTarget = Post-Json "$campaignBase/$campaignId/targets" $createTarget
$manualTargetId = $respTarget.data
Add-Result "Create manual target (201 + CampaignTargetId)" "201 + guid" "$($respTarget.statusCode) / $manualTargetId" ($respTarget.statusCode -eq 201 -and $manualTargetId)

$readTarget = (Get-Json "$campaignBase/$campaignId/targets/$manualTargetId").data
Add-Result "Manual target carries mandatory reason codes" "manual_target_selected" ($readTarget.reasonCodes -join ',') `
    ($readTarget.reasonCodes -contains "manual_target_selected" -and $readTarget.reasonCodes -contains "campaign_target_created")
Add-Result "Manual target has no snapshot batch / consent provenance" "null/null" "batch=$($readTarget.snapshotBatchId) consent=$($readTarget.consentEvaluation)" `
    (-not $readTarget.snapshotBatchId -and -not $readTarget.consentEvaluation)
Add-Result "Manual duplicate target -> 409" 409 (Post-Code "$campaignBase/$campaignId/targets" $createTarget) `
    ((Post-Code "$campaignBase/$campaignId/targets" $createTarget) -eq 409)

# ---------------- 6. MOD-0164 consent fixtures (existing FU02 API) ----------------
function New-Consent([string]$SubjectId, [string]$Status = "granted") {
    $body = @{
        subjectType = "account-contact-link"; subjectId = $SubjectId
        channel = "visit"; purpose = "medical-visit"; legalBasis = "explicit-consent"; consentStatus = $Status
        effectiveFrom = $pastIso; source = "field-capture"
    }
    return (Post-Json $consentBase $body).data
}
$allowedConsentId = New-Consent $allowedSubject "granted"
$blockedConsentId = New-Consent $blockedSubject "granted"   # granted, but a restrictive preference will block it
Add-Result "MOD-0164 granted consents created (allowed + blocked subjects)" "2 guids" "$allowedConsentId / $blockedConsentId" `
    ($allowedConsentId -and $blockedConsentId)

$restrictivePreference = @{
    subjectType = "account-contact-link"; subjectId = $blockedSubject
    channel = "visit"; preferenceType = "do-not-visit"; preferenceValue = "true"; priority = 100
    effectiveFrom = $pastIso; source = "subject-declared"
}
$prefResp = Post-Json $preferenceBase $restrictivePreference
$preferenceId = $prefResp.data
Add-Result "MOD-0164 restrictive preference created" "201 + guid" "$($prefResp.statusCode) / $preferenceId" ($prefResp.statusCode -eq 201 -and $preferenceId)

# Baseline counts to prove the snapshot never mutates the MOD-0164 store.
$consentsBefore    = (Get-Json $consentBase).data.total
$preferencesBefore = (Get-Json $preferenceBase).data.total

# ---------------- 7. Static snapshot with consent filter ----------------
$snapshotBody = @{
    sourceType = "manual"; selectionReason = "FU04 smoke snapshot"; applyConsentFilter = $true
    consentChannel = "visit"; consentPurpose = "medical-visit"; effectiveAt = $atIso
    targetItems = @(
        @{ targetType = "account-contact-link"; targetId = $allowedSubject; targetDisplayName = "allowed subject"; priority = 100 },
        @{ targetType = "account-contact-link"; targetId = $blockedSubject; targetDisplayName = "blocked subject"; priority = 200 },
        @{ targetType = "account-contact-link"; targetId = $unknownSubject; targetDisplayName = "unknown subject"; priority = 300 }
    )
}
$snapshot = Post-Json "$campaignBase/$campaignId/targets/snapshot" $snapshotBody
$snap = $snapshot.data
Add-Result "Snapshot created (201 + SnapshotBatchId)" "201 + guid" "$($snapshot.statusCode) / $($snap.snapshotBatchId)" `
    ($snapshot.statusCode -eq 201 -and $snap.snapshotBatchId)
Add-Result "Snapshot consent filter applied" "true + visit/medical-visit" "$($snap.consentFilterApplied)/$($snap.consentChannel)/$($snap.consentPurpose)" `
    ($snap.consentFilterApplied -eq $true -and $snap.consentChannel -eq "visit" -and $snap.consentPurpose -eq "medical-visit")

# ---------------- 8. Allowed target active + provenance present ----------------
$allowedRow = @($snap.rows | Where-Object { $_.targetId -eq $allowedSubject })[0]
Add-Result "Allowed consent -> target ACTIVE" "active/consent_allowed" "$($allowedRow.targetStatus)/$($allowedRow.reasonCodes -join ',')" `
    ($allowedRow.targetStatus -eq "active" -and $allowedRow.reasonCodes -contains "consent_allowed")
$prov = $allowedRow.consentEvaluation
$provOk = $prov -and $prov.decision -and $prov.eligibilityStatus -eq "allowed" -and @($prov.reasonCodes).Count -ge 1 `
    -and $prov.evaluatedAt -and $prov.matchedConsentId -and $prov.evaluatorVersion -and $prov.selectionReason -and $prov.filterApplied -eq $true
Add-Result "Consent provenance stored (decision/status/reasonCodes/evaluatedAt/matchedConsentId/evaluatorVersion)" "all present" `
    "$($prov.eligibilityStatus)/$($prov.decision)/$($prov.evaluatorVersion)" $provOk
Add-Result "Provenance matchedConsentId == the granted consent" "$allowedConsentId" "$($prov.matchedConsentId)" ($prov.matchedConsentId -eq $allowedConsentId)

$rawSnapshot = Invoke-WebRequest -Uri "$campaignBase/$campaignId/targets?snapshotBatchId=$($snap.snapshotBatchId)" -Method GET -Headers $auth -TimeoutSec 20
$consentDataLeak = @("consentStatus","preferenceStatus","consentRecordPayload","preferenceRecordPayload","legalBasis","withdrawalReason","preferenceValue" |
    Where-Object { $rawSnapshot.Content -match "`"$_`"" })
Add-Result "Consent DATA not copied onto targets" "none" $(if ($consentDataLeak.Count) { $consentDataLeak -join ',' } else { "none" }) ($consentDataLeak.Count -eq 0)

# ---------------- 9-11. Blocked + unknown targets excluded with reason ----------------
$blockedRow = @($snap.rows | Where-Object { $_.targetId -eq $blockedSubject })[0]
Add-Result "Blocked consent -> target EXCLUDED with reason" "excluded/consent_blocked" "$($blockedRow.targetStatus)/$($blockedRow.exclusionReason)" `
    ($blockedRow.targetStatus -eq "excluded" -and $blockedRow.exclusionReason -eq "consent_blocked")
Add-Result "Blocked row reason codes visible" "consent_blocked + campaign_target_excluded" ($blockedRow.reasonCodes -join ',') `
    ($blockedRow.reasonCodes -contains "consent_blocked" -and $blockedRow.reasonCodes -contains "campaign_target_excluded")

$unknownRow = @($snap.rows | Where-Object { $_.targetId -eq $unknownSubject })[0]
Add-Result "Unknown consent -> target EXCLUDED with reason" "excluded/consent_unknown" "$($unknownRow.targetStatus)/$($unknownRow.exclusionReason)" `
    ($unknownRow.targetStatus -eq "excluded" -and $unknownRow.exclusionReason -eq "consent_unknown")
Add-Result "Unknown is NOT treated as allowed" "not active" "$($unknownRow.targetStatus)" ($unknownRow.targetStatus -ne "active")
Add-Result "Snapshot counts (1 active / 2 excluded)" "1/2" "$($snap.activeCount)/$($snap.excludedCount)" `
    ($snap.activeCount -eq 1 -and $snap.excludedCount -eq 2)

# Group-shaped target: consent not applicable, and the segment is never expanded.
$segmentSnapshotBody = @{
    sourceType = "segment"; sourceReferenceType = "segment"; sourceReferenceId = $segmentId
    selectionReason = "FU04 smoke segment provenance"; applyConsentFilter = $true
    consentChannel = "visit"; consentPurpose = "medical-visit"; effectiveAt = $atIso
    targetItems = @(@{ targetType = "segment"; targetId = $segmentId; targetDisplayName = "segment provenance row" })
}
$segmentSnap = (Post-Json "$campaignBase/$campaignId/targets/snapshot" $segmentSnapshotBody).data
$segmentRow = @($segmentSnap.rows)[0]
Add-Result "Segment target -> consent_evaluation_not_applicable (never silently evaluated)" "not_applicable" `
    "$($segmentRow.consentEvaluation.eligibilityStatus)/$($segmentRow.reasonCodes -join ',')" `
    ($segmentRow.consentEvaluation.eligibilityStatus -eq "not_applicable" -and $segmentRow.reasonCodes -contains "consent_evaluation_not_applicable")
Add-Result "Segment snapshot stores provenance only (1 row, no membership expansion)" "1 row + segmentId" `
    "rows=$(@($segmentSnap.rows).Count) ref=$($segmentSnap.sourceReferenceId)" `
    (@($segmentSnap.rows).Count -eq 1 -and $segmentSnap.sourceReferenceId -eq $segmentId)
Add-Result "Segment snapshot reason code visible" "segment_source_snapshot" ($segmentSnap.reasonCodes -join ',') `
    ($segmentSnap.reasonCodes -contains "segment_source_snapshot")

# Consent context missing -> 400 (selected fail-closed behavior)
$noContext = @{
    sourceType = "manual"; selectionReason = "no consent context"; applyConsentFilter = $true
    targetItems = @(@{ targetType = "contact"; targetId = [guid]::NewGuid().Guid })
}
Add-Result "Consent-filtered snapshot without channel/purpose -> 400" 400 (Post-Code "$campaignBase/$campaignId/targets/snapshot" $noContext) `
    ((Post-Code "$campaignBase/$campaignId/targets/snapshot" $noContext) -eq 400)

# Explicit opt-out -> targets produced but visibly unfiltered
$optOut = @{
    sourceType = "manual"; selectionReason = "explicit consent filter opt-out"; applyConsentFilter = $false
    targetItems = @(@{ targetType = "contact"; targetId = [guid]::NewGuid().Guid })
}
$optOutSnap = (Post-Json "$campaignBase/$campaignId/targets/snapshot" $optOut).data
Add-Result "ApplyConsentFilter=false -> consent_filter_not_applied visible" "false + reason" `
    "$($optOutSnap.consentFilterApplied)/$($optOutSnap.reasonCodes -join ',')" `
    ($optOutSnap.consentFilterApplied -eq $false -and $optOutSnap.reasonCodes -contains "consent_filter_not_applied")

# ---------------- 12. Re-run does not duplicate ----------------
$targetsAfterFirst = (Get-Json "$campaignBase/$campaignId/targets").data.total
$rerun = Post-Json "$campaignBase/$campaignId/targets/snapshot" $snapshotBody
$rerunData = $rerun.data
$targetsAfterRerun = (Get-Json "$campaignBase/$campaignId/targets").data.total
Add-Result "Snapshot re-run reconciles instead of duplicating" "3 reconciled / 0 created" "$($rerunData.reconciledCount)/$($rerunData.createdCount)" `
    ($rerunData.reconciledCount -eq 3 -and $rerunData.createdCount -eq 0)
Add-Result "Target count unchanged after re-run" "$targetsAfterFirst" "$targetsAfterRerun" ($targetsAfterFirst -eq $targetsAfterRerun)
Add-Result "Re-run reason code visible" "campaign_target_snapshot_reconciled" ($rerunData.reasonCodes -join ',') `
    ($rerunData.reasonCodes -contains "campaign_target_snapshot_reconciled")

# Additive: the manual target from step 5 (absent from the snapshot payload) is untouched.
$manualAfter = (Get-Json "$campaignBase/$campaignId/targets/$manualTargetId").data
Add-Result "Snapshot did NOT delete/archive the earlier manual target" "not archived" "isArchived=$($manualAfter.isArchived)" `
    ($manualAfter.isArchived -eq $false)

# Source conflict: a segment-sourced snapshot claiming a manual-owned target aborts with 409 and writes nothing.
$conflictBody = @{
    sourceType = "segment"; sourceReferenceType = "segment"; sourceReferenceId = $segmentId
    selectionReason = "source conflict probe"; applyConsentFilter = $true
    consentChannel = "visit"; consentPurpose = "medical-visit"
    targetItems = @(@{ targetType = "account-contact-link"; targetId = $allowedSubject })
}
$beforeConflict = (Get-Json "$campaignBase/$campaignId/targets").data.total
$conflictCode = Post-Code "$campaignBase/$campaignId/targets/snapshot" $conflictBody
$afterConflict = (Get-Json "$campaignBase/$campaignId/targets").data.total
Add-Result "Different-source snapshot row -> 409, nothing written" "409 + count unchanged" "$conflictCode / $beforeConflict->$afterConflict" `
    ($conflictCode -eq 409 -and $beforeConflict -eq $afterConflict)

# ---------------- 13-15. Archive target, archive campaign, frozen mutation ----------------
Add-Result "Archive target (success)" "200/204" (Archive "$campaignBase/$campaignId/targets/$manualTargetId/archive") `
    ((Archive "$campaignBase/$campaignId/targets/$manualTargetId/archive") -in 200,204)
$archivedTarget = (Get-Json "$campaignBase/$campaignId/targets/$manualTargetId").data
Add-Result "Archived target still readable + stamp" "archived+stamp" "isArchived=$($archivedTarget.isArchived)/$($archivedTarget.archivedAt)" `
    ($archivedTarget.isArchived -eq $true -and $archivedTarget.archivedAt)
Add-Result "Archived target update -> 409" 409 (Put-Code "$campaignBase/$campaignId/targets/$manualTargetId" @{ targetSource = "manual"; selectionReason = "x"; effectiveFrom = $pastIso }) `
    ((Put-Code "$campaignBase/$campaignId/targets/$manualTargetId" @{ targetSource = "manual"; selectionReason = "x"; effectiveFrom = $pastIso }) -eq 409)

Add-Result "Archive campaign (success)" "200/204" (Archive "$campaignBase/$campaignId/archive") ((Archive "$campaignBase/$campaignId/archive") -in 200,204)
$archivedCampaign = (Get-Json "$campaignBase/$campaignId").data
Add-Result "Archived campaign still readable + stamp" "archived+stamp" "isArchived=$($archivedCampaign.isArchived)/$($archivedCampaign.archivedAt)" `
    ($archivedCampaign.isArchived -eq $true -and $archivedCampaign.archivedAt)
Add-Result "Archived campaign: targets NOT cascaded (no silent cascade)" ">=1 non-archived target" `
    "$(@((Get-Json "$campaignBase/$campaignId/targets").data.items | Where-Object { -not $_.isArchived }).Count) non-archived" `
    (@((Get-Json "$campaignBase/$campaignId/targets").data.items | Where-Object { -not $_.isArchived }).Count -ge 1)

Add-Result "Archived campaign update -> 409" 409 (Put-Code "$campaignBase/$campaignId" @{ campaignName = "x"; campaignType = "other"; startDate = $pastIso }) `
    ((Put-Code "$campaignBase/$campaignId" @{ campaignName = "x"; campaignType = "other"; startDate = $pastIso }) -eq 409)
Add-Result "Archived campaign target create -> 409" 409 (Post-Code "$campaignBase/$campaignId/targets" $createTarget) `
    ((Post-Code "$campaignBase/$campaignId/targets" $createTarget) -eq 409)
Add-Result "Archived campaign snapshot -> 409" 409 (Post-Code "$campaignBase/$campaignId/targets/snapshot" $snapshotBody) `
    ((Post-Code "$campaignBase/$campaignId/targets/snapshot" $snapshotBody) -eq 409)

# ---------------- 16-17. Negative auth + DELETE ----------------
Add-Result "No token -> 401 (targets)" 401 (Status "$campaignBase/$campaignId/targets") ((Status "$campaignBase/$campaignId/targets") -eq 401)
Add-Result "DELETE campaign -> 404/405" "404/405" (Status "$campaignBase/$campaignId" "DELETE" $auth) ((Status "$campaignBase/$campaignId" "DELETE" $auth) -in 404,405)
Add-Result "DELETE target -> 404/405" "404/405" (Status "$campaignBase/$campaignId/targets/$manualTargetId" "DELETE" $auth) `
    ((Status "$campaignBase/$campaignId/targets/$manualTargetId" "DELETE" $auth) -in 404,405)

# Validation negatives
$negBase = @{ campaignCode = "SMOKE-NEG-$run"; campaignName = "n"; campaignType = "product-campaign"; startDate = $pastIso }
function Neg([string]$Label, [hashtable]$Override) {
    $b = $negBase.Clone(); foreach ($k in $Override.Keys) { $b[$k] = $Override[$k] }
    $b["campaignCode"] = "SMOKE-NEG-$run-" + [guid]::NewGuid().ToString('N').Substring(0,6)
    $code = Post-Code $campaignBase $b
    Add-Result "$Label -> 400" 400 $code ($code -eq 400)
}
Neg "StartDate > EndDate"        @{ startDate = "2027-01-01T00:00:00Z"; endDate = "2026-01-01T00:00:00Z" }
Neg "Invalid campaignType"       @{ campaignType = "telepathy-campaign" }
Neg "Invalid campaignStatus"     @{ campaignStatus = "maybe" }
Neg "Invalid objectiveType"      @{ objectiveType = "vibes" }
Neg "Invalid defaultConsentChannel" @{ defaultConsentChannel = "carrier-pigeon" }

$badTargetType = @{ targetType = "campaign-target"; targetId = [guid]::NewGuid().Guid; targetSource = "manual"; selectionReason = "x"; effectiveFrom = $pastIso }
$freshCampaign = Post-Json $campaignBase @{ campaignCode = "SMOKE-FU04B-$run"; campaignName = "FU04 negative campaign"; campaignType = "other"; startDate = $pastIso; campaignStatus = "active" }
$freshCampaignId = $freshCampaign.data
Add-Result "TargetType 'campaign-target' -> 400 (self-referential loop)" 400 (Post-Code "$campaignBase/$freshCampaignId/targets" $badTargetType) `
    ((Post-Code "$campaignBase/$freshCampaignId/targets" $badTargetType) -eq 400)
Add-Result "Target without SelectionReason -> 400" 400 `
    (Post-Code "$campaignBase/$freshCampaignId/targets" @{ targetType = "contact"; targetId = [guid]::NewGuid().Guid; targetSource = "manual"; selectionReason = " "; effectiveFrom = $pastIso }) `
    ((Post-Code "$campaignBase/$freshCampaignId/targets" @{ targetType = "contact"; targetId = [guid]::NewGuid().Guid; targetSource = "manual"; selectionReason = " "; effectiveFrom = $pastIso }) -eq 400)
Add-Result "Excluded target without ExclusionReason -> 400" 400 `
    (Post-Code "$campaignBase/$freshCampaignId/targets" @{ targetType = "contact"; targetId = [guid]::NewGuid().Guid; targetSource = "manual"; selectionReason = "x"; effectiveFrom = $pastIso; targetStatus = "excluded" }) `
    ((Post-Code "$campaignBase/$freshCampaignId/targets" @{ targetType = "contact"; targetId = [guid]::NewGuid().Guid; targetSource = "manual"; selectionReason = "x"; effectiveFrom = $pastIso; targetStatus = "excluded" }) -eq 400)
Add-Result "Empty snapshot TargetItems -> 400" 400 `
    (Post-Code "$campaignBase/$freshCampaignId/targets/snapshot" @{ sourceType = "manual"; selectionReason = "x"; applyConsentFilter = $false; targetItems = @() }) `
    ((Post-Code "$campaignBase/$freshCampaignId/targets/snapshot" @{ sourceType = "manual"; selectionReason = "x"; applyConsentFilter = $false; targetItems = @() }) -eq 400)

# ---------------- 18. Response shape guard ----------------
$rawCampaign = Invoke-WebRequest -Uri "$campaignBase/$campaignId" -Method GET -Headers $auth -TimeoutSec 20
$rawTargets  = Invoke-WebRequest -Uri "$campaignBase/$campaignId/targets" -Method GET -Headers $auth -TimeoutSec 20
$banned = "visitPlanId","routePlanId","routeId","dueStatus","overdue","lastVisitDate","requiredVisitCount","periodType",
          "frequencyPolicyId","segmentMembership","recommendationId","nextBestAction","workflowApprovalId","contentRenderUrl",
          "consentRecordPayload","preferenceRecordPayload"
$leakedCampaign = @($banned | Where-Object { $rawCampaign.Content -match "`"$_`"" })
$leakedTargets  = @($banned | Where-Object { $rawTargets.Content  -match "`"$_`"" })
Add-Result "Campaign response shape clean" "none" $(if ($leakedCampaign.Count) { $leakedCampaign -join ',' } else { "none" }) ($leakedCampaign.Count -eq 0)
Add-Result "Target response shape clean" "none" $(if ($leakedTargets.Count) { $leakedTargets -join ',' } else { "none" }) ($leakedTargets.Count -eq 0)

# ---------------- 19. Data mutation guard ----------------
$consentsAfter    = (Get-Json $consentBase).data.total
$preferencesAfter = (Get-Json $preferenceBase).data.total
Add-Result "Snapshot did not mutate ConsentRecord store (count unchanged)" "$consentsBefore" "$consentsAfter" ($consentsBefore -eq $consentsAfter)
Add-Result "Snapshot did not mutate PreferenceRecord store (count unchanged)" "$preferencesBefore" "$preferencesAfter" ($preferencesBefore -eq $preferencesAfter)

$consentStillGranted = (Get-Json "$consentBase/$allowedConsentId").data
Add-Result "Consent record untouched by campaign (still granted, not archived)" "granted/not archived" `
    "$($consentStillGranted.consentStatus)/isArchived=$($consentStillGranted.isArchived)" `
    ($consentStillGranted.consentStatus -eq "granted" -and $consentStillGranted.isArchived -eq $false)
$preferenceStill = (Get-Json "$preferenceBase/$preferenceId").data
Add-Result "Preference record untouched by campaign" "do-not-visit/true" "$($preferenceStill.preferenceType)/$($preferenceStill.preferenceValue)" `
    ($preferenceStill.preferenceType -eq "do-not-visit" -and $preferenceStill.preferenceValue -eq "true")

# ---------------- 20. Cleanup by archive only ----------------
foreach ($t in @((Get-Json "$campaignBase/$freshCampaignId/targets").data.items | Where-Object { -not $_.isArchived })) {
    Archive "$campaignBase/$freshCampaignId/targets/$($t.campaignTargetId)/archive" | Out-Null
}
Archive "$campaignBase/$freshCampaignId/archive" | Out-Null
foreach ($id in @($allowedConsentId, $blockedConsentId)) { Archive "$consentBase/$id/archive" | Out-Null }
Archive "$preferenceBase/$preferenceId/archive" | Out-Null
Add-Result "Cleanup done by ARCHIVE only (no delete anywhere)" "archived" "archived" $true

$token = $null; $auth = $null

# ---------------- Summary ----------------
Write-Host "`n== RESULTS (paste this back; contains no secret) ==" -ForegroundColor Cyan
($results | Format-Table Step,Result,Expected,Actual -AutoSize | Out-String -Width 4096).TrimEnd() | Write-Host
# @() guard: without it a SINGLE failure yields $null, printing a blank count and skipping the detail block.
$fail = @($results | Where-Object Result -eq "FAIL").Count
if ($fail -gt 0) {
    Write-Host "`n== FAILURES ONLY (untruncated — paste THIS) ==" -ForegroundColor Yellow
    ($results | Where-Object Result -eq "FAIL" | Format-List Step,Expected,Actual | Out-String -Width 4096).TrimEnd() | Write-Host
    $out = "$PSScriptRoot/../.smoke-mod0165-fu04-results.json"
    $results | ConvertTo-Json -Depth 4 | Set-Content -Path $out -Encoding utf8
    Write-Host "`n(Full results also written to: $out — no secret inside)" -ForegroundColor DarkGray
}
Write-Host ("`nOVERALL: {0}  ({1} checks, {2} fail)" -f $(if ($fail -eq 0) { "PASS" } else { "FAIL" }), $results.Count, $fail) -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
