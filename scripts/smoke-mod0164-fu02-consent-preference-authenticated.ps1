<#
  MOD-0164-FU02 — Authenticated Consent & Preference Runtime Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering
  passwords/tokens to authenticate is outside what the assistant may do on your behalf. This
  script keeps the credential in YOUR process memory only — it is never written to a file and the
  Authorization header is never printed (masked). Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0164-fu02-consent-preference-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  All business calls go through the Gateway (5000). Direct 5061 is used ONLY for /health.
  Nothing is hard-deleted: every record this script creates is closed with the ARCHIVE endpoint.
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

$consentBase    = "$BaseUrl/api/crm/consents"
$preferenceBase = "$BaseUrl/api/crm/preferences"
$run            = (Get-Date -Format "yyyyMMddHHmmss")

# Synthetic subject ids: MOD-0164 stores no cross-aggregate FK by design (the caller supplies the id),
# so a smoke subject needs no Contact/AccountContactLink master row and nothing upstream is mutated.
$subject       = [guid]::NewGuid().Guid   # account-contact-link subject under test
$emptySubject  = [guid]::NewGuid().Guid   # a subject with NO consent at all (the unknown case)
$brandScope    = [guid]::NewGuid().Guid   # brand scope id (scope-specificity case)
$evidenceDoc   = [guid]::NewGuid().Guid   # MOD-0029 document id (reference only — no file is touched)
$nowIso        = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$pastIso       = (Get-Date).ToUniversalTime().AddDays(-30).ToString("yyyy-MM-ddTHH:mm:ssZ")
$atIso         = (Get-Date).ToUniversalTime().AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ")

Write-Host "== MOD-0164-FU02 authenticated consent/preference smoke ($run) ==" -ForegroundColor Cyan

# ---------------- 1. Fleet health / preflight ----------------
foreach ($p in @(5000,5001,5056,5057,5061)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Preflight port $p up" "reachable" $code ($code -ne -1)
}
$crmHealth = Status "$CrmDirect/health"
Add-Result "CRM direct /health (only allowed direct call)" "200/ok" $crmHealth ($crmHealth -in 200,204)

# Unauthenticated guards BEFORE login (no token in the process yet).
$noTokenContract = Status "$consentBase/contract"
Add-Result "No token -> 401 (consents/contract)" 401 $noTokenContract ($noTokenContract -eq 401)
$noTokenPreferences = Status $preferenceBase
Add-Result "No token -> 401 (preferences)" 401 $noTokenPreferences ($noTokenPreferences -eq 401)
$garbageToken = Status "$consentBase/contract" "GET" @{ Authorization = "Bearer x.y.z" }
Add-Result "Garbage token -> 401" 401 $garbageToken ($garbageToken -eq 401)

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
$tenantClaim = $claims.tenant_id
Add-Result "Tenant claim == target" $TenantId $tenantClaim ($tenantClaim -eq $TenantId)

$auth = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
function Get-Json([string]$Url) { Invoke-RestMethod -Uri $Url -Method GET -Headers $auth -TimeoutSec 20 }
function Post-Json([string]$Url, $Obj) { Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 6) -TimeoutSec 20 }
function Post-Code([string]$Url, $Obj) {
    try { return [int](Post-Json $Url $Obj).statusCode } catch { return [int]$_.Exception.Response.StatusCode }
}
function Eval-Consent([string]$SubjectId, [string]$Channel = "visit", [string]$Purpose = "medical-visit", [string]$Extra = "") {
    $u = "$consentBase/evaluate?subjectType=account-contact-link&subjectId=$SubjectId&channel=$Channel&purpose=$Purpose&effectiveAt=$atIso&includeDiagnostics=true$Extra"
    return (Get-Json $u).data
}

# ---------------- 3. Contract 200 + flags ----------------
$contract = Get-Json "$consentBase/contract"
$f = $contract.data.features
$flagsOk = $f.supportsConsentManagement -and $f.supportsPreferenceManagement -and $f.supportsConsentEvaluation `
    -and $f.supportsConsentPurposeChannelScope -and $f.supportsConsentEvidenceReference -and $f.supportsConsentFilterProvider
Add-Result "Contract flags all true" "true x6" $flagsOk $flagsOk
$forbidden = "supportsCampaignEngine","supportsVisitPlanning","supportsRoutePlanning","supportsDigitalDetailing","supportsRecommendationEngine","supportsWorkflowApproval"
$flagNames = $f.PSObject.Properties.Name
$leak = $forbidden | Where-Object { $flagNames -contains $_ }
Add-Result "Forbidden flags absent" "none" $(if ($leak) { $leak -join ',' } else { "none" }) (-not $leak)
Add-Result "Contract moduleId + evaluator version" "MOD-0164 + version" "$($contract.data.moduleId) / $($contract.data.evaluationVocabulary.evaluatorVersion)" `
    ($contract.data.moduleId -eq "MOD-0164" -and $contract.data.evaluationVocabulary.evaluatorVersion)

# ---------------- 4. Create granted consent ----------------
$createConsent = @{
    subjectType = "account-contact-link"; subjectId = $subject
    channel = "visit"; purpose = "medical-visit"; legalBasis = "explicit-consent"; consentStatus = "granted"
    effectiveFrom = $pastIso; source = "field-capture"
    evidenceRef = @{ refType = "document"; refId = $evidenceDoc; sourceModule = "MOD-0029"; refCode = "SMOKE-$run" }
    externalReferences = @(@{ sourceSystem = "OldCRM"; externalId = "CONSENT-$run"; externalCode = "OPT-IN"; externalName = "Legacy opt-in"; isPrimary = $true })
    notes = "FU02 smoke — created through the authenticated Gateway"
    tenantId = "ffffffff-ffff-ffff-ffff-ffffffffffff"  # deliberately injected — must be IGNORED (claim wins)
}
$respConsent = Post-Json $consentBase $createConsent
$consentId = $respConsent.data
Add-Result "Create granted consent (201 + ConsentId)" "201 + guid" "$($respConsent.statusCode) / $consentId" ($respConsent.statusCode -eq 201 -and $consentId)

$readConsent = (Get-Json "$consentBase/$consentId").data
Add-Result "TenantId payload ignored (readable in claim tenant)" "readable/granted" "status=$($readConsent.consentStatus)" ($readConsent -and $readConsent.consentStatus -eq "granted")
Add-Result "EvidenceRef stored as MOD-0029 reference (no file copy)" "document/MOD-0029" "$($readConsent.evidenceRef.refType)/$($readConsent.evidenceRef.sourceModule)" `
    ($readConsent.evidenceRef.refType -eq "document" -and $readConsent.evidenceRef.sourceModule -eq "MOD-0029" -and $readConsent.evidenceRef.refId -eq $evidenceDoc)
Add-Result "ExternalReferences stored" "OldCRM/CONSENT-$run" "$($readConsent.externalReferences[0].sourceSystem)/$($readConsent.externalReferences[0].externalId)" `
    ($readConsent.externalReferences.Count -eq 1 -and $readConsent.externalReferences[0].sourceSystem -eq "OldCRM")

# ---------------- 5. Evaluate -> allowed / consent_granted ----------------
$eval1 = Eval-Consent $subject
$ok1 = ($eval1.eligibilityStatus -eq "allowed") -and ($eval1.decision -eq "consent_granted") -and ($eval1.matchedConsentId -eq $consentId)
Add-Result "Evaluate granted -> allowed + consent_granted" "allowed/consent_granted" "$($eval1.eligibilityStatus)/$($eval1.decision)" $ok1
Add-Result "Reason code consent_granted present" "consent_granted" ($eval1.reasonCodes -join ',') ($eval1.reasonCodes -contains "consent_granted")
Add-Result "CandidateConsents diagnostics visible" ">=1 with reason" "$($eval1.candidateConsents.Count) candidate(s)" `
    ($eval1.candidateConsents.Count -ge 1 -and -not [string]::IsNullOrWhiteSpace($eval1.candidateConsents[0].reason))
Add-Result "SelectionReason present" "non-empty" "$($eval1.selectionReason)" (-not [string]::IsNullOrWhiteSpace($eval1.selectionReason))

# Channel/purpose non-transferability: the same subject on another channel/purpose is NOT allowed.
$evalOtherChannel = Eval-Consent $subject "email" "marketing"
Add-Result "Visit consent does not leak to email/marketing" "unknown" "$($evalOtherChannel.eligibilityStatus)" ($evalOtherChannel.eligibilityStatus -eq "unknown")

# No consent at all -> unknown (never a default allowed)
$evalEmpty = Eval-Consent $emptySubject
Add-Result "No consent -> unknown + no_matching_consent" "unknown" "$($evalEmpty.eligibilityStatus)/$($evalEmpty.decision)" `
    ($evalEmpty.eligibilityStatus -eq "unknown" -and $evalEmpty.reasonCodes -contains "no_matching_consent")

# ---------------- 6. Create restrictive preference ----------------
$createPreference = @{
    subjectType = "account-contact-link"; subjectId = $subject
    channel = "visit"; preferenceType = "do-not-visit"; preferenceValue = "true"; priority = 100
    effectiveFrom = $pastIso; source = "subject-declared"
    notes = "FU02 smoke — restrictive preference"
    tenantId = "ffffffff-ffff-ffff-ffff-ffffffffffff"  # must be IGNORED
}
$respPreference = Post-Json $preferenceBase $createPreference
$preferenceId = $respPreference.data
Add-Result "Create do-not-visit preference (201 + PreferenceId)" "201 + guid" "$($respPreference.statusCode) / $preferenceId" ($respPreference.statusCode -eq 201 -and $preferenceId)

# ---------------- 7. Evaluate again -> blocked by preference ----------------
$eval2 = Eval-Consent $subject
$ok2 = ($eval2.eligibilityStatus -eq "blocked") -and ($eval2.decision -eq "preference_restricted")
Add-Result "Restrictive preference blocks granted consent" "blocked/preference_restricted" "$($eval2.eligibilityStatus)/$($eval2.decision)" $ok2
Add-Result "Reason code preference_do_not_visit present" "preference_do_not_visit" ($eval2.reasonCodes -join ',') `
    (($eval2.reasonCodes -contains "preference_do_not_visit") -or ($eval2.reasonCodes -contains "preference_restricted"))
Add-Result "MatchedPreferenceIds carries the preference" "$preferenceId" ($eval2.matchedPreferenceIds -join ',') ($eval2.matchedPreferenceIds -contains $preferenceId)
# NOTE: the @() wrapper is required. In Windows PowerShell 5.1 `($array | Where-Object {...}).Count` yields $null
# when the filter matches exactly ONE object, so the assertion would fail on correct data.
$restrictiveCount = @($eval2.candidatePreferences | Where-Object { $_.restrictive }).Count
Add-Result "CandidatePreferences diagnostics visible (restrictive flag)" "restrictive>=1" `
    "$(@($eval2.candidatePreferences).Count) candidate(s), $restrictiveCount restrictive" `
    (@($eval2.candidatePreferences).Count -ge 1 -and $restrictiveCount -ge 1)
Add-Result "Blocked result still shows the matched consent (explained, not hidden)" "$consentId" "$($eval2.matchedConsentId)" ($eval2.matchedConsentId -eq $consentId)

# ---------------- 8. Archive preference ----------------
$archPreference = Invoke-RestMethod -Uri "$preferenceBase/$preferenceId/archive" -Method POST -Headers $auth -TimeoutSec 20
Add-Result "Archive preference (success)" "200/204" "$($archPreference.statusCode)" ($archPreference.statusCode -in 200,204)
$readPreference = (Get-Json "$preferenceBase/$preferenceId").data
Add-Result "Archived preference still readable + ArchivedAt set" "archived+stamp" "isArchived=$($readPreference.isArchived)/$($readPreference.archivedAt)" `
    ($readPreference.isArchived -eq $true -and $readPreference.archivedAt)

# ---------------- 9. Evaluate again -> allowed ----------------
$eval3 = Eval-Consent $subject
Add-Result "Archived preference no longer restricts -> allowed" "allowed/consent_granted" "$($eval3.eligibilityStatus)/$($eval3.decision)" `
    ($eval3.eligibilityStatus -eq "allowed" -and $eval3.decision -eq "consent_granted")

# ---------------- Extra: scope specificity + restrictive status precedence ----------------
$scopedDenied = @{
    subjectType = "account-contact-link"; subjectId = $subject
    channel = "visit"; purpose = "medical-visit"; legalBasis = "legitimate-interest"; consentStatus = "denied"
    effectiveFrom = $pastIso; source = "manual"; scopeType = "brand"; scopeId = $brandScope
}
$respScoped = Post-Json $consentBase $scopedDenied
$scopedId = $respScoped.data
Add-Result "Create scope-specific denied consent (201)" "201" "$($respScoped.statusCode)" ($respScoped.statusCode -eq 201)

$evalInScope = Eval-Consent $subject "visit" "medical-visit" "&scopeType=brand&scopeId=$brandScope"
Add-Result "Scope-specific consent beats general (blocked in scope)" "blocked + scoped id" "$($evalInScope.eligibilityStatus)/$($evalInScope.matchedConsentId)" `
    ($evalInScope.eligibilityStatus -eq "blocked" -and $evalInScope.matchedConsentId -eq $scopedId)
Add-Result "Specificity reason visible" "consent_selected_by_specificity" ($evalInScope.reasonCodes -join ',') `
    ($evalInScope.reasonCodes -contains "consent_selected_by_specificity")

$evalGeneralAgain = Eval-Consent $subject
Add-Result "General question does not consume the scoped record" "allowed + scope mismatch reason" "$($evalGeneralAgain.eligibilityStatus)" `
    ($evalGeneralAgain.eligibilityStatus -eq "allowed" -and $evalGeneralAgain.reasonCodes -contains "consent_scope_mismatch")

# withdrawal: same question, new status — blocks, and the record is not deleted
$withdrawBody = @{ legalBasis = "explicit-consent"; consentStatus = "withdrawn"; effectiveFrom = $pastIso; source = "subject-declared"; withdrawalReason = "FU02 smoke withdrawal" }
$withdrawResp = Invoke-RestMethod -Uri "$consentBase/$consentId" -Method PUT -Headers $auth -ContentType "application/json" -Body ($withdrawBody | ConvertTo-Json) -TimeoutSec 20
Add-Result "Withdraw consent via PUT (200, history kept)" "200" "$($withdrawResp.statusCode)" ($withdrawResp.statusCode -in 200,204)
$evalWithdrawn = Eval-Consent $subject
Add-Result "Withdrawn consent blocks" "blocked/consent_blocked" "$($evalWithdrawn.eligibilityStatus)/$($evalWithdrawn.decision)" `
    ($evalWithdrawn.eligibilityStatus -eq "blocked" -and $evalWithdrawn.reasonCodes -contains "consent_withdrawn")
$readWithdrawn = (Get-Json "$consentBase/$consentId").data
Add-Result "Withdrawal reason preserved on the record" "FU02 smoke withdrawal" "$($readWithdrawn.withdrawalReason)" ($readWithdrawn.withdrawalReason -eq "FU02 smoke withdrawal")

# ---------------- 10-11. Archive consent -> unknown ----------------
foreach ($id in @($consentId, $scopedId)) {
    try { Invoke-RestMethod -Uri "$consentBase/$id/archive" -Method POST -Headers $auth -TimeoutSec 20 | Out-Null } catch {}
}
$eval4 = Eval-Consent $subject
Add-Result "Archived consent -> unknown (no default allowed)" "unknown/consent_unknown" "$($eval4.eligibilityStatus)/$($eval4.decision)" `
    ($eval4.eligibilityStatus -eq "unknown" -and $eval4.decision -eq "consent_unknown")
Add-Result "Unknown is NOT allowed" "not allowed" "$($eval4.eligibilityStatus)" ($eval4.eligibilityStatus -ne "allowed")
$readArchivedConsent = (Get-Json "$consentBase/$consentId").data
Add-Result "Archived consent still readable (history preserved)" "archived+stamp" "isArchived=$($readArchivedConsent.isArchived)" `
    ($readArchivedConsent.isArchived -eq $true -and $readArchivedConsent.archivedAt)

# ---------------- 12. Negative guards ----------------
$delConsent = Status "$consentBase/$consentId" "DELETE" $auth
Add-Result "DELETE consent unsupported -> 404/405" "404/405" $delConsent ($delConsent -in 404,405)
$delPreference = Status "$preferenceBase/$preferenceId" "DELETE" $auth
Add-Result "DELETE preference unsupported -> 404/405" "404/405" $delPreference ($delPreference -in 404,405)

$updArchived = @{ legalBasis = "contract"; consentStatus = "granted"; effectiveFrom = $pastIso; source = "manual" }
$updCode = try { (Invoke-RestMethod -Uri "$consentBase/$consentId" -Method PUT -Headers $auth -ContentType "application/json" -Body ($updArchived | ConvertTo-Json) -TimeoutSec 15).statusCode } catch { [int]$_.Exception.Response.StatusCode }
Add-Result "Update archived consent -> 409" 409 $updCode ($updCode -eq 409)

$updArchivedPreference = @{ preferenceValue = "false"; priority = 100; effectiveFrom = $pastIso; source = "manual" }
$updPrefCode = try { (Invoke-RestMethod -Uri "$preferenceBase/$preferenceId" -Method PUT -Headers $auth -ContentType "application/json" -Body ($updArchivedPreference | ConvertTo-Json) -TimeoutSec 15).statusCode } catch { [int]$_.Exception.Response.StatusCode }
Add-Result "Update archived preference -> 409" 409 $updPrefCode ($updPrefCode -eq 409)

$base400 = @{ subjectType = "account-contact-link"; subjectId = [guid]::NewGuid().Guid; channel = "visit"; purpose = "medical-visit"; legalBasis = "explicit-consent"; consentStatus = "granted"; effectiveFrom = $pastIso; source = "manual" }
function Neg([string]$Label, [hashtable]$Override) {
    $b = $base400.Clone()
    foreach ($k in $Override.Keys) { $b[$k] = $Override[$k] }
    $code = Post-Code $consentBase $b
    Add-Result "$Label -> 400" 400 $code ($code -eq 400)
}

Neg "Invalid consentStatus"       @{ consentStatus = "maybe" }
Neg "Invalid channel"             @{ channel = "carrier-pigeon" }
Neg "Invalid purpose"             @{ purpose = "gossip" }
Neg "Invalid legalBasis"          @{ legalBasis = "vibes" }
Neg "Missing subjectType"         @{ subjectType = "" }
Neg "Empty subjectId"             @{ subjectId = "00000000-0000-0000-0000-000000000000" }
Neg "EffectiveTo < EffectiveFrom" @{ effectiveFrom = "2027-01-01T00:00:00Z"; effectiveTo = "2026-01-01T00:00:00Z" }
Neg "Withdrawn without reason"    @{ consentStatus = "withdrawn" }
Neg "Malformed EvidenceRef"       @{ evidenceRef = @{ refType = "screenshot"; refId = $evidenceDoc; sourceModule = "MOD-0029" } }
Neg "ScopeId without ScopeType"   @{ scopeId = $brandScope }

$dupExternal = $base400.Clone(); $dupExternal["externalReferences"] = @(@{ sourceSystem = "OldCRM"; externalId = "DUP-$run" }, @{ sourceSystem = "oldcrm"; externalId = "DUP-$run" })
$dupCode = Post-Code $consentBase $dupExternal
Add-Result "Duplicate external mapping -> 409 (no silent merge)" 409 $dupCode ($dupCode -eq 409)

$badPreference = @{ subjectType = "account-contact-link"; subjectId = [guid]::NewGuid().Guid; channel = "visit"; preferenceType = "do-not-visit"; preferenceValue = "sometimes"; priority = 100; effectiveFrom = $pastIso; source = "manual" }
$badPrefCode = Post-Code $preferenceBase $badPreference
Add-Result "Ambiguous restrictive preferenceValue -> 400" 400 $badPrefCode ($badPrefCode -eq 400)

$badEvaluate = Status "$consentBase/evaluate?subjectType=account-contact-link&subjectId=$subject&channel=telepathy&purpose=medical-visit" "GET" $auth
Add-Result "Evaluate with invalid channel -> 400 (malformed question, not 'unknown')" 400 $badEvaluate ($badEvaluate -eq 400)

# ---------------- 13. Response shape guard ----------------
$rawEvaluate = Invoke-WebRequest -Uri "$consentBase/evaluate?subjectType=account-contact-link&subjectId=$subject&channel=visit&purpose=medical-visit&effectiveAt=$atIso&includeDiagnostics=true" -Method GET -Headers $auth -TimeoutSec 15
$banned = "visitPlanId","routeId","dueStatus","overdue","lastVisitDate","campaignTargetId","requiredVisitCount","frequencyPolicyId","periodType","segmentMembership","recommendation","workflowInstanceId","approvalStatus"
$leaked = $banned | Where-Object { $rawEvaluate.Content -match "`"$_`"" }
Add-Result "Evaluate response shape clean (no campaign/visit/route/due/frequency field)" "none" $(if ($leaked) { $leaked -join ',' } else { "none" }) (-not $leaked)

$rawConsent = Invoke-WebRequest -Uri "$consentBase/$consentId" -Method GET -Headers $auth -TimeoutSec 15
$leakedConsent = $banned | Where-Object { $rawConsent.Content -match "`"$_`"" }
Add-Result "Consent record shape clean" "none" $(if ($leakedConsent) { $leakedConsent -join ',' } else { "none" }) (-not $leakedConsent)

# ---------------- 14. Data mutation guard ----------------
$consentsBefore    = (Get-Json "$consentBase" ).data.total
$preferencesBefore = (Get-Json "$preferenceBase").data.total
Eval-Consent $subject                          | Out-Null
Eval-Consent $subject "email" "marketing"      | Out-Null
Eval-Consent $emptySubject                     | Out-Null
Eval-Consent $subject "visit" "medical-visit" "&scopeType=brand&scopeId=$brandScope" | Out-Null
$consentsAfter    = (Get-Json "$consentBase" ).data.total
$preferencesAfter = (Get-Json "$preferenceBase").data.total
Add-Result "Evaluate is write-free (consent count unchanged)"    "$consentsBefore"    "$consentsAfter"    ($consentsBefore -eq $consentsAfter)
Add-Result "Evaluate is write-free (preference count unchanged)" "$preferencesBefore" "$preferencesAfter" ($preferencesBefore -eq $preferencesAfter)

# Unknown-subject evaluation created nothing (no implicit 'unknown' record is ever persisted).
$emptyList = Get-Json "$consentBase`?subjectId=$emptySubject"
Add-Result "Evaluating an unknown subject persists nothing" 0 "$($emptyList.data.total)" ($emptyList.data.total -eq 0)

$token = $null; $auth = $null

# ---------------- Summary ----------------
Write-Host "`n== RESULTS (paste this back; contains no secret) ==" -ForegroundColor Cyan
($results | Format-Table Step,Result,Expected,Actual -AutoSize | Out-String -Width 4096).TrimEnd() | Write-Host
# @() again: without it a SINGLE failure yields $null here, printing a blank count and skipping the detail block.
$fail = @($results | Where-Object Result -eq "FAIL").Count
if ($fail -gt 0) {
    Write-Host "`n== FAILURES ONLY (untruncated — paste THIS) ==" -ForegroundColor Yellow
    ($results | Where-Object Result -eq "FAIL" | Format-List Step,Expected,Actual | Out-String -Width 4096).TrimEnd() | Write-Host
    $out = "$PSScriptRoot/../.smoke-mod0164-fu02-results.json"
    $results | ConvertTo-Json -Depth 4 | Set-Content -Path $out -Encoding utf8
    Write-Host "`n(Full results also written to: $out — no secret inside)" -ForegroundColor DarkGray
}
Write-Host ("`nOVERALL: {0}  ({1} checks, {2} fail)" -f $(if ($fail -eq 0) { "PASS" } else { "FAIL" }), $results.Count, $fail) -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
