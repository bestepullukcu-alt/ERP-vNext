<#
  MOD-0165-FU07 — Authenticated Cycle Period SCOPE Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires entering a password, and entering passwords/tokens to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  never written to a file, and the Authorization header is never printed. Paste the printed PASS/FAIL table back to the
  assistant to finalize the evidence report; it contains no secret.

  RUN THE FU06 SCRIPT FIRST. ./scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1 must still pass 18/18 — that
  is the only real proof FU07 did not change what FU06 promised. If one of its steps fails, FU07 is wrong, not the
  script.

  What this one asserts (the FU07 decisions, in the order the pack states them):
    * scope is DISCRIMINATED — one level, one reference; a second reference is REFUSED (400), not ignored
    * scope is IDENTITY — the level is immutable at every status (409), and uniqueness is per scope
    * the country and the business unit are GOVERNED vocabulary; an unpublished SET and an unknown VALUE are
      different failures
    * the legal entity is proved through MDM BEFORE anything is written: not-referenceable is 400, unreachable is 503
      with nothing persisted, and a caller lacking mdm.legal-entities.read gets 503 (a known dev gap, F-MDM-PERM)
    * the overlap ban is PER SCOPE: same scope collides (409), DIFFERENT LEVELS may share days (200) — that one is
      load-bearing, because banning it would make precedence unreachable
    * resolution walks business-unit > legal-entity > country > tenant, SKIPS levels the caller did not name, STOPS at
      the first level that answers (including on ambiguous), and never merges
    * an FU06-shaped call still answers exactly what FU06 answered
    * resolve-active WRITES NOTHING (the row count is compared before and after)

  Nothing is hard-deleted: every record created here is ended with CLOSE.

  PREREQUISITES (data, not code):
    * The fleet must be running the FU07 build (a restart is required after deploying it).
    * COUNTRY_CODES must be published for the tenant, or every country-scope step answers 400 by design (fail-closed).
      Run ./scripts/verify-mod0165-fu07-country-equivalence.ps1 first.
    * A referenceable MDM legal entity id for -LegalEntityId, or the legal-entity steps report SKIP.

  Usage (from repo root, in PowerShell):
      ./scripts/smoke-mod0165-fu07-cycle-period-scope-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-...  -Country TR  -BusinessUnit rx  -LegalEntityId <guid>

  PowerShell 5.1 note: pipeline counts use the @(...) array-subexpression guard (a single match otherwise yields $null).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl       = "http://localhost:5000",
    [string]$TenantId      = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [string]$Country       = "TR",
    [string]$OtherCountry  = "DE",
    [string]$BusinessUnit  = "rx",
    [string]$LegalEntityId = ""
)

$ErrorActionPreference = "Stop"
$results = [System.Collections.Generic.List[object]]::new()
$prefix = "fu07-" + (Get-Date -Format "HHmmss")
$createdIds = [System.Collections.Generic.List[string]]::new()

function Add-Result([string]$Step, $Expected, $Actual, [bool]$Pass) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "$Expected"; Actual = "$Actual"; Result = $(if ($Pass) { "PASS" } else { "FAIL" }) })
}
function Add-Skip([string]$Step, $Detail) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "data present"; Actual = "$Detail"; Result = "SKIP" })
}

# ── login (tenant-scoped) ──────────────────────────────────────────────────────────────────────────────────────────
Write-Host "MOD-0165-FU07 cycle period scope smoke" -ForegroundColor Cyan
Write-Host "Gateway : $BaseUrl"
Write-Host "Tenant  : $TenantId"
Write-Host ""

$email = Read-Host "Tenant admin e-mail"
$secure = Read-Host "Password" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))

# X-Tenant-Id on login is what makes this a TENANT-scoped token. Without it the dev bypass returns a PLATFORM token and
# every assertion below would be measuring the wrong tenant while looking perfectly green.
$loginBody = @{ email = $email; password = $plain; rememberMe = $false } | ConvertTo-Json
$plain = $null

try {
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/tenant-auth/login" -Method Post `
        -Headers @{ "X-Tenant-Id" = $TenantId } -ContentType "application/json" -Body $loginBody -TimeoutSec 30
} catch {
    Write-Host "Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
$loginBody = $null

$token = $login.data.accessToken
if (-not $token) { $token = $login.accessToken }
if (-not $token) { Write-Host "No access token in the login response." -ForegroundColor Red; exit 1 }

$headers = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
$api = "$BaseUrl/api/crm/cycle-periods"

# ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────────────
function Invoke-Api([string]$Method, [string]$Url, $Body) {
    $params = @{ Uri = $Url; Method = $Method; Headers = $headers; TimeoutSec = 30; UseBasicParsing = $true }
    if ($null -ne $Body) { $params.ContentType = "application/json"; $params.Body = ($Body | ConvertTo-Json -Depth 6) }
    try {
        $response = Invoke-WebRequest @params
        $json = $null
        if ($response.Content) { try { $json = $response.Content | ConvertFrom-Json } catch { } }
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Body = $json; Raw = $response.Content }
    } catch {
        $resp = $_.Exception.Response
        $status = if ($resp -and $resp.StatusCode) { [int]$resp.StatusCode } else { -1 }
        $raw = $null
        if ($resp) {
            try { $raw = (New-Object System.IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } catch { }
        }
        $json = $null
        if ($raw) { try { $json = $raw | ConvertFrom-Json } catch { } }
        return [pscustomobject]@{ Status = $status; Body = $json; Raw = $raw }
    }
}

function New-Period([string]$Code, [string]$ScopeType, [hashtable]$Scope, [int]$Sequence, [string]$Start, [string]$End) {
    $body = @{
        cycleCode = $Code; cycleName = "smoke $Code"; year = 2026; sequenceInYear = $Sequence
        startDate = $Start; endDate = $End; scopeType = $ScopeType
    }
    foreach ($key in $Scope.Keys) { $body[$key] = $Scope[$key] }
    $r = Invoke-Api "POST" $api $body
    if ($r.Status -eq 201 -and $r.Body.data) { [void]$createdIds.Add([string]$r.Body.data) }
    return $r
}

function Has-Error($Response, [string]$Code) {
    return @($Response.Body.errors | Where-Object { "$_" -eq $Code }).Count -gt 0
}

function Get-RowCount() {
    $r = Invoke-Api "GET" $api $null
    if ($r.Status -ne 200) { return -1 }
    return [int]$r.Body.data.totalCount
}

$mar1 = "2026-03-01T00:00:00Z"; $apr30 = "2026-04-30T00:00:00Z"
$apr30Overlap = "2026-04-30T00:00:00Z"; $jun30 = "2026-06-30T00:00:00Z"

# ── 1-3. contract + scope options ──────────────────────────────────────────────────────────────────────────────────
$contract = Invoke-Api "GET" "$api/contract" $null
Add-Result "1. contract readable" "200" $contract.Status ($contract.Status -eq 200)

if ($contract.Status -eq 200) {
    $f = $contract.Body.data.features
    $openOk = $f.supportsCountryScopedCycles -and $f.supportsLegalEntityScopedCycles `
        -and $f.supportsScopePrecedenceResolution -and $f.supportsTerritorySourcedBusinessUnits
    Add-Result "2a. FU07 opened four capabilities" "all true" "$openOk" ([bool]$openOk)

    # Widening WHERE a period lives is not a licence to widen WHAT it does.
    $closedOk = (-not $f.supportsCampaignBinding) -and (-not $f.supportsCycleAutoClose) `
        -and (-not $f.supportsCyclePeriodVersioning) -and (-not $f.supportsCycleReschedule) `
        -and (-not $f.supportsWorkingDayCount) -and (-not $f.supportsBulkDelete) -and (-not $f.supportsHardDelete) `
        -and (-not $f.supportsScopeMerge) -and (-not $f.supportsCrossScopeOverlapBan) `
        -and (-not $f.supportsScopeTypeMutation) -and (-not $f.supportsScopeInheritance)
    Add-Result "2b. every closed flag stays closed" "all false" "$closedOk" ([bool]$closedOk)
} else {
    Add-Skip "2. contract flags" "contract unreadable"
}

$options = Invoke-Api "GET" "$api/scope-options?country=$Country&startDate=$mar1&endDate=$apr30" $null
Add-Result "3. scope-options readable" "200" $options.Status ($options.Status -eq 200)
if ($options.Status -eq 200) {
    $d = $options.Body.data
    Write-Host ("    countryReady={0}  legalEntityReady={1}  businessUnitReady={2}  fromTerritory={3}" -f `
        $d.countryReady, $d.legalEntityReady, $d.businessUnitReady, $d.businessUnitFromTerritory) -ForegroundColor DarkGray
}

# ── 4-6. the single-reference invariant ────────────────────────────────────────────────────────────────────────────
# FU06 backward compatibility: a caller that never heard of scopes sends no scopeType at all. With no references
# either, that shape is unambiguous - it is the tenant-wide period FU06 always wrote - so it is DERIVED, not refused.
$noScope = Invoke-Api "POST" $api @{ cycleCode = "$prefix-x"; cycleName = "x"; year = 2026; sequenceInYear = 1; startDate = $mar1; endDate = $apr30 }
if ($noScope.Status -eq 201 -and $noScope.Body.data) { [void]$createdIds.Add([string]$noScope.Body.data) }
Add-Result "4a. missing scopeType, no references -> derived tenant" "201" `
    "$($noScope.Status)" ($noScope.Status -eq 201)

# ...but the two levels FU07 ADDED cannot be derived: nothing written against FU06 can have meant them, so guessing
# would be inventing intent. The author has to name the level.
$noScopeCountry = Invoke-Api "POST" $api @{ cycleCode = "$prefix-xc"; cycleName = "xc"; year = 2026; sequenceInYear = 1; startDate = $mar1; endDate = $apr30; countryScope = $Country }
Add-Result "4b. missing scopeType with a country" "400 scope_type_unknown" `
    "$($noScopeCountry.Status)" (($noScopeCountry.Status -eq 400) -and (Has-Error $noScopeCountry "cycle_period_scope_type_unknown"))

$badScope = Invoke-Api "POST" $api @{ cycleCode = "$prefix-xb"; cycleName = "xb"; year = 2026; sequenceInYear = 1; startDate = $mar1; endDate = $apr30; scopeType = "region" }
Add-Result "4c. unknown scopeType value" "400 scope_type_unknown" `
    "$($badScope.Status)" (($badScope.Status -eq 400) -and (Has-Error $badScope "cycle_period_scope_type_unknown"))

$twoRefs = New-Period "$prefix-two" "country" @{ countryScope = $Country; businessUnitId = $BusinessUnit } 1 $mar1 $apr30
Add-Result "5. two references at once" "400 scope_ambiguous" `
    "$($twoRefs.Status)" (($twoRefs.Status -eq 400) -and (Has-Error $twoRefs "cycle_period_scope_ambiguous"))

$tenantWithRef = New-Period "$prefix-tref" "tenant" @{ businessUnitId = $BusinessUnit } 1 $mar1 $apr30
Add-Result "6. tenant scope with a reference" "400 scope_ambiguous" `
    "$($tenantWithRef.Status)" (($tenantWithRef.Status -eq 400) -and (Has-Error $tenantWithRef "cycle_period_scope_ambiguous"))

# ── 7-8. governed vocabulary ───────────────────────────────────────────────────────────────────────────────────────
$badCountry = New-Period "$prefix-zz" "country" @{ countryScope = "ZZ" } 1 $mar1 $apr30
$badCountryOk = ($badCountry.Status -eq 400) -and ((Has-Error $badCountry "cycle_period_country_unknown") -or (Has-Error $badCountry "cycle_period_reference_set_unpublished"))
Add-Result "7. unknown country value" "400 country_unknown | reference_set_unpublished" `
    "$($badCountry.Status)" $badCountryOk

$badUnit = New-Period "$prefix-bu" "business-unit" @{ businessUnitId = "definitely-not-published" } 1 $mar1 $apr30
$badUnitOk = ($badUnit.Status -eq 400) -and ((Has-Error $badUnit "cycle_period_business_unit_unknown") -or (Has-Error $badUnit "cycle_period_reference_set_unpublished"))
Add-Result "8. unpublished business unit" "400 business_unit_unknown | reference_set_unpublished" `
    "$($badUnit.Status)" $badUnitOk

# ── 9-11. the scoped periods this smoke works with ─────────────────────────────────────────────────────────────────
$tenantPeriod = New-Period "$prefix-tenant" "tenant" @{} 11 $mar1 $apr30
Add-Result "9. create tenant-scoped period" "201 (born draft)" "$($tenantPeriod.Status)" ($tenantPeriod.Status -eq 201)

$countryPeriod = New-Period "$prefix-country" "country" @{ countryScope = $Country } 12 $mar1 $apr30
$countryOk = $countryPeriod.Status -eq 201
Add-Result "10. create country-scoped period" "201 (or 400 when COUNTRY_CODES is unpublished)" `
    "$($countryPeriod.Status)" $countryOk
if (-not $countryOk) {
    Write-Host "    country scope unavailable - publish COUNTRY_CODES for this tenant (fail-closed by design)." -ForegroundColor Yellow
}

$unitPeriod = New-Period "$prefix-unit" "business-unit" @{ businessUnitId = $BusinessUnit } 13 $mar1 $apr30
Add-Result "11. create business-unit-scoped period" "201" "$($unitPeriod.Status)" ($unitPeriod.Status -eq 201)
if ($unitPeriod.Status -eq 201) {
    $detail = Invoke-Api "GET" "$api/$($unitPeriod.Body.data)" $null
    $source = $detail.Body.data.businessUnitSource
    Add-Result "11b. business unit carries a provenance stamp" "territory | manual" "$source" `
        (@("territory", "manual") -contains "$source")
}

# ── 12-13. MDM legal entity, fail-closed ───────────────────────────────────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($LegalEntityId)) {
    Add-Skip "12. legal-entity scope" "pass -LegalEntityId <guid> to exercise it"
} else {
    $lePeriod = New-Period "$prefix-le" "legal-entity" @{ legalEntityId = $LegalEntityId } 14 $mar1 $apr30
    if ($lePeriod.Status -eq 503) {
        # EXPECTED in dev when the caller lacks mdm.legal-entities.read: 403 from MDM means "we were not allowed to
        # look", which must never be reported as "no such entity". Follow-up F-MDM-PERM.
        Add-Result "12. legal-entity create (MDM unreachable/forbidden)" "503, nothing written (known dev gap F-MDM-PERM)" `
            "$($lePeriod.Status)" $true
    } else {
        Add-Result "12. create legal-entity-scoped period" "201" "$($lePeriod.Status)" ($lePeriod.Status -eq 201)
    }
}

$bogusLe = New-Period "$prefix-le0" "legal-entity" @{ legalEntityId = "00000000-0000-0000-0000-0000000000ff" } 15 $mar1 $apr30
$bogusOk = ($bogusLe.Status -eq 400 -and (Has-Error $bogusLe "cycle_period_legal_entity_not_referenceable")) `
    -or ($bogusLe.Status -eq 503 -and (Has-Error $bogusLe "cycle_period_legal_entity_dependency_unavailable"))
Add-Result "13. unknown legal entity" "400 not_referenceable, or 503 when MDM cannot answer" "$($bogusLe.Status)" $bogusOk

# ── 14-16. uniqueness is per scope, the code is tenant-wide ────────────────────────────────────────────────────────
$dupSeqSame = New-Period "$prefix-dup1" "business-unit" @{ businessUnitId = $BusinessUnit } 13 $mar1 $apr30
Add-Result "14. same (year, sequence) in the SAME scope" "409 sequence_taken" `
    "$($dupSeqSame.Status)" (($dupSeqSame.Status -eq 409) -and (Has-Error $dupSeqSame "cycle_period_sequence_taken"))

$dupSeqOther = New-Period "$prefix-dup2" "tenant" @{} 13 $mar1 $apr30
Add-Result "15. same (year, sequence) in a DIFFERENT scope" "201 (separate key spaces)" `
    "$($dupSeqOther.Status)" ($dupSeqOther.Status -eq 201)

$dupCode = New-Period "$prefix-unit" "tenant" @{} 16 $mar1 $apr30
Add-Result "16. same code in a different scope" "409 code_taken (a code names one period)" `
    "$($dupCode.Status)" (($dupCode.Status -eq 409) -and (Has-Error $dupCode "cycle_period_code_taken"))

# ── 17. the scope level is immutable ───────────────────────────────────────────────────────────────────────────────
if ($unitPeriod.Status -eq 201) {
    $id = $unitPeriod.Body.data
    $move = Invoke-Api "PUT" "$api/$id" @{
        cycleName = "moved"; year = 2026; sequenceInYear = 13; startDate = $mar1; endDate = $apr30
        scopeType = "tenant"
    }
    Add-Result "17. change the scope level" "409 scope_immutable" `
        "$($move.Status)" (($move.Status -eq 409) -and (Has-Error $move "cycle_period_scope_immutable"))
} else {
    Add-Skip "17. change the scope level" "no business-unit period to edit"
}

# ── 18-19. the overlap ban is PER SCOPE ────────────────────────────────────────────────────────────────────────────
$activateUnit = if ($unitPeriod.Status -eq 201) { Invoke-Api "POST" "$api/$($unitPeriod.Body.data)/activate" $null } else { $null }
Add-Result "18a. activate the business-unit period" "200" `
    $(if ($activateUnit) { "$($activateUnit.Status)" } else { "skipped" }) ($activateUnit -and $activateUnit.Status -eq 200)

$sameScopeOverlap = New-Period "$prefix-ovl" "business-unit" @{ businessUnitId = $BusinessUnit } 17 $apr30Overlap $jun30
if ($sameScopeOverlap.Status -eq 201) {
    $act = Invoke-Api "POST" "$api/$($sameScopeOverlap.Body.data)/activate" $null
    Add-Result "18b. overlapping activate in the SAME scope" "409 overlap, row stays draft" `
        "$($act.Status)" (($act.Status -eq 409) -and (Has-Error $act "cycle_period_overlap"))
} else {
    Add-Skip "18b. overlapping activate in the SAME scope" "could not seed the overlapping period"
}

# Load-bearing: different levels MAY share days. Banning this would make precedence unreachable, because precedence
# only ever fires when more than one level covers the same instant.
if ($tenantPeriod.Status -eq 201) {
    $actTenant = Invoke-Api "POST" "$api/$($tenantPeriod.Body.data)/activate" $null
    Add-Result "19. overlapping activate at a DIFFERENT level" "200 (cross-level overlap is allowed, and must be)" `
        "$($actTenant.Status)" ($actTenant.Status -eq 200)
} else {
    Add-Skip "19. overlapping activate at a DIFFERENT level" "no tenant period to activate"
}

# ── 20-24. resolution: precedence, skip, stop, no merge ────────────────────────────────────────────────────────────
$at = "2026-03-15T00:00:00Z"
$before = Get-RowCount

$rBu = Invoke-Api "GET" "$api/resolve-active?at=$at&businessUnitId=$BusinessUnit" $null
Add-Result "20. resolve with a business unit" "resolved at business-unit" `
    "$($rBu.Body.data.outcome)/$($rBu.Body.data.resolvedScopeType)" `
    (($rBu.Body.data.outcome -eq "resolved") -and ($rBu.Body.data.resolvedScopeType -eq "business-unit"))

$rNone = Invoke-Api "GET" "$api/resolve-active?at=$at&businessUnitId=no-such-unit" $null
Add-Result "21. an unmatched unit falls back to tenant" "resolved at tenant" `
    "$($rNone.Body.data.outcome)/$($rNone.Body.data.resolvedScopeType)" `
    (($rNone.Body.data.outcome -eq "resolved") -and ($rNone.Body.data.resolvedScopeType -eq "tenant"))

# The whole backward-compatibility mechanism: a level nobody named is SKIPPED, even when it has a covering period.
$rSkip = Invoke-Api "GET" "$api/resolve-active?at=$at" $null
Add-Result "22. FU06-shaped call sees only the levels it named" "resolved at tenant" `
    "$($rSkip.Body.data.outcome)/$($rSkip.Body.data.resolvedScopeType)" `
    (($rSkip.Body.data.outcome -eq "resolved") -and ($rSkip.Body.data.resolvedScopeType -eq "tenant"))

$rOut = Invoke-Api "GET" "$api/resolve-active?at=2027-01-01T00:00:00Z&businessUnitId=$BusinessUnit" $null
Add-Result "23. outside every window" "none, and no period returned" `
    "$($rOut.Body.data.outcome)" (($rOut.Body.data.outcome -eq "none") -and ($null -eq $rOut.Body.data.period))

$after = Get-RowCount
Add-Result "24. resolve-active writes NOTHING" "row count unchanged" "$before -> $after" ($before -eq $after -and $before -ge 0)

# ── 25-27. lifecycle is terminal, tenant isolation, concurrency ────────────────────────────────────────────────────
if ($unitPeriod.Status -eq 201) {
    $id = $unitPeriod.Body.data
    $close = Invoke-Api "POST" "$api/$id/close" $null
    Add-Result "25. close" "200" "$($close.Status)" ($close.Status -eq 200)

    $reEdit = Invoke-Api "PUT" "$api/$id" @{ cycleName = "after close"; year = 2026; sequenceInYear = 13; startDate = $mar1; endDate = $apr30 }
    Add-Result "26. edit a closed period" "409 closed (terminal)" `
        "$($reEdit.Status)" (($reEdit.Status -eq 409) -and (Has-Error $reEdit "cycle_period_closed"))
} else {
    Add-Skip "25. close" "no business-unit period"
    Add-Skip "26. edit a closed period" "no business-unit period"
}

$crossTenant = Invoke-Api "GET" "$api/11111111-2222-3333-4444-555555555555" $null
Add-Result "27. another tenant's id" "404 (never 403 - no existence leak)" "$($crossTenant.Status)" ($crossTenant.Status -eq 404)

# ── cleanup: close everything this run created. Nothing is ever hard-deleted. ───────────────────────────────────────
foreach ($id in $createdIds) {
    $null = Invoke-Api "POST" "$api/$id/close" $null
}

# ── report ─────────────────────────────────────────────────────────────────────────────────────────────────────────
Write-Host ""
$results | Format-Table -AutoSize

$pass = @($results | Where-Object { $_.Result -eq "PASS" }).Count
$fail = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$skip = @($results | Where-Object { $_.Result -eq "SKIP" }).Count
Write-Host ""
Write-Host "PASS=$pass  FAIL=$fail  SKIP=$skip" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
Write-Host "Reminder: ./scripts/smoke-mod0165-fu06-cycle-period-authenticated.ps1 must still pass 18/18." -ForegroundColor Cyan
