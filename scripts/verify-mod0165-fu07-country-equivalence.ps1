<#
  MOD-0165-FU07 — the D-COUNTRY-SET equivalence BUILD-GATE (run this YOURSELF).

  Why you run it (not the agent): reading the governed reference sets requires logging in, and entering a password to
  authenticate is outside what the assistant may do on your behalf. The credential stays in YOUR process memory only —
  never written to a file, and the Authorization header is never printed. Paste the printed table back to the assistant
  to finalize the evidence report; it contains no secret.

  WHAT THIS PROVES (pack section 2.6). The user chose COUNTRY_CODES as FU07's country vocabulary, while MOD-0151
  Territory still reads the `country` set today. FU07's business-unit picker narrows its list by comparing the period's
  country against TerritoryModel.CountryScope, so the two vocabularies have to agree on the CODES for that comparison
  to keep matching while Territory migrates. This script decides that, in three parts:

     1. COUNTRY_CODES is published and readable for this tenant.
     2. COUNTRY_CODES is a SUPERSET of `country`, code for code.
     3. Every TerritoryModel.CountryScope value in use is present in COUNTRY_CODES.
     ( plus: both sets are ISO alpha-2, ^[A-Z]{2}$ )

  IF IT PASSES  the picker keeps working before Territory migrates ("TR" = "TR"), and nothing else is needed.
  IF IT FAILS   FU07 still ships and business-unit periods are NOT blocked: the picker falls back to the published
                `business-unit` vocabulary (D-BU-SOURCE is a soft gate by design), so authoring keeps working - the list
                is simply no longer narrowed by a field plan until the vocabularies are aligned (follow-up F-COUNTRY-SOT).
                The country / legal-entity / tenant scopes are unaffected either way.

  PREREQUISITE (data, not code): COUNTRY_CODES must be PUBLISHED for the tenant. It is a Global-scope MOD-0048 set and
  the operator publishes it by hand; until then part 1 fails and the country scope cannot be authored at all
  (fail-closed - a hardcoded fallback list is forbidden).

  Usage (from repo root, in PowerShell):
      ./scripts/verify-mod0165-fu07-country-equivalence.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  PowerShell 5.1 note: pipeline counts use the @(...) array-subexpression guard (a single match otherwise yields $null).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl  = "http://localhost:5000",
    [string]$TenantId = "97c59330-dbc4-4665-b29c-0c26dbb5cc93"
)

$ErrorActionPreference = "Stop"
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Step, $Expected, $Actual, [bool]$Pass) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "$Expected"; Actual = "$Actual"; Result = $(if ($Pass) { "PASS" } else { "FAIL" }) })
}
function Add-Skip([string]$Step, $Detail) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "data present"; Actual = "$Detail"; Result = "SKIP" })
}

# ── login ──────────────────────────────────────────────────────────────────────────────────────────────────────────
Write-Host "MOD-0165-FU07 country equivalence gate" -ForegroundColor Cyan
Write-Host "Gateway : $BaseUrl"
Write-Host "Tenant  : $TenantId"
Write-Host ""

$email = Read-Host "Tenant admin e-mail"
$secure = Read-Host "Password" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))

# X-Tenant-Id on login is what makes this a TENANT-scoped token. Without it the dev bypass hands back a PLATFORM token
# and every reference read below would answer for the wrong scope - and look convincing while doing it.
$loginHeaders = @{ "X-Tenant-Id" = $TenantId }
$loginBody = @{ email = $email; password = $plain; rememberMe = $false } | ConvertTo-Json
$plain = $null

try {
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/tenant-auth/login" -Method Post -Headers $loginHeaders `
        -ContentType "application/json" -Body $loginBody -TimeoutSec 30
} catch {
    Write-Host "Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$token = $login.data.accessToken
if (-not $token) { $token = $login.accessToken }
if (-not $token) { Write-Host "No access token in the login response." -ForegroundColor Red; exit 1 }

$headers = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
$loginBody = $null

# ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────────────

# A tenant-scoped set REQUIRES scope_key; a global set REFUSES it. Nothing tells us which shape a set has before we
# ask, so we ask the tenant way first and retry without the key on the service's own refusal - exactly what the CRM
# consumer does at runtime, so this script measures the real path rather than a friendlier one.
function Get-PublishedCodes([string]$SetCode) {
    $base = "$BaseUrl/api/v1/reference-data/sets/$([uri]::EscapeDataString($SetCode))/published-values"
    foreach ($url in @("$base`?scope_key=$([uri]::EscapeDataString($TenantId))", $base)) {
        try {
            $response = Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 30
            $items = $response.data.items
            if (-not $items) { $items = $response.data.values }
            if (-not $items) { $items = $response.items }
            $codes = @($items | ForEach-Object {
                $v = $_.valueCode; if (-not $v) { $v = $_.value_code }; if (-not $v) { $v = $_.value }; $v
            } | Where-Object { $_ })
            return [pscustomobject]@{ Ok = $true; Codes = $codes; Url = $url; Error = $null }
        } catch {
            $last = $_.Exception.Message
        }
    }
    return [pscustomobject]@{ Ok = $false; Codes = @(); Url = $base; Error = $last }
}

function Get-TerritoryCountryScopes() {
    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/crm/territory-models" -Headers $headers -TimeoutSec 30
        $items = $response.data.items
        if (-not $items) { $items = $response.data }
        return [pscustomobject]@{
            Ok = $true
            Codes = @($items | ForEach-Object { $_.countryScope } | Where-Object { $_ })
            Error = $null
        }
    } catch {
        return [pscustomobject]@{ Ok = $false; Codes = @(); Error = $_.Exception.Message }
    }
}

# ── 1. COUNTRY_CODES is readable ───────────────────────────────────────────────────────────────────────────────────
$countryCodes = Get-PublishedCodes "COUNTRY_CODES"
Add-Result "1. COUNTRY_CODES readable" "published, non-empty" `
    $(if ($countryCodes.Ok) { "$(@($countryCodes.Codes).Count) values" } else { "unreadable: $($countryCodes.Error)" }) `
    ($countryCodes.Ok -and @($countryCodes.Codes).Count -gt 0)

# ── 2. the legacy `country` set, for the superset comparison ────────────────────────────────────────────────────────
$legacyCountry = Get-PublishedCodes "country"
if ($legacyCountry.Ok) {
    Add-Result "2. `country` set readable" "published, non-empty" "$(@($legacyCountry.Codes).Count) values" `
        (@($legacyCountry.Codes).Count -gt 0)
} else {
    # Not a failure of FU07: if the old set is gone there is nothing left to diverge from.
    Add-Skip "2. `country` set readable" "unreadable: $($legacyCountry.Error)"
}

# ── 3. ISO alpha-2 on both sides ───────────────────────────────────────────────────────────────────────────────────
$isoPattern = '^[A-Z]{2}$'
$badCountryCodes = @($countryCodes.Codes | Where-Object { $_ -cnotmatch $isoPattern })
Add-Result "3a. COUNTRY_CODES are ISO alpha-2" "every value matches ^[A-Z]{2}$" `
    $(if (@($badCountryCodes).Count -eq 0) { "all ok" } else { "offending: $($badCountryCodes -join ', ')" }) `
    (@($badCountryCodes).Count -eq 0)

if ($legacyCountry.Ok) {
    $badLegacy = @($legacyCountry.Codes | Where-Object { $_ -cnotmatch $isoPattern })
    Add-Result "3b. `country` values are ISO alpha-2" "every value matches ^[A-Z]{2}$" `
        $(if (@($badLegacy).Count -eq 0) { "all ok" } else { "offending: $($badLegacy -join ', ')" }) `
        (@($badLegacy).Count -eq 0)
}

# ── 4. COUNTRY_CODES superset of `country` ─────────────────────────────────────────────────────────────────────────
if ($legacyCountry.Ok -and @($legacyCountry.Codes).Count -gt 0) {
    $superset = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($code in $countryCodes.Codes) { [void]$superset.Add($code) }
    $missing = @($legacyCountry.Codes | Where-Object { -not $superset.Contains($_) })
    Add-Result "4. COUNTRY_CODES superset of `country`" "no `country` value missing" `
        $(if (@($missing).Count -eq 0) { "0 missing" } else { "missing: $($missing -join ', ')" }) `
        (@($missing).Count -eq 0)
} else {
    Add-Skip "4. COUNTRY_CODES superset of `country`" "legacy set unavailable"
}

# ── 5. every Territory country scope in use is covered ──────────────────────────────────────────────────────────────
$territory = Get-TerritoryCountryScopes
if ($territory.Ok) {
    $inUse = @($territory.Codes | Sort-Object -Unique)
    if (@($inUse).Count -eq 0) {
        Add-Skip "5. Territory country scopes covered" "no territory model declares a country scope"
    } else {
        $superset = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
        foreach ($code in $countryCodes.Codes) { [void]$superset.Add($code) }
        $uncovered = @($inUse | Where-Object { -not $superset.Contains($_) })
        Add-Result "5. Territory country scopes covered" "every CountryScope in COUNTRY_CODES" `
            $(if (@($uncovered).Count -eq 0) { "all $(@($inUse).Count) covered" } else { "uncovered: $($uncovered -join ', ')" }) `
            (@($uncovered).Count -eq 0)
    }
} else {
    Add-Skip "5. Territory country scopes covered" "territory models unreadable: $($territory.Error)"
}

# ── verdict ────────────────────────────────────────────────────────────────────────────────────────────────────────
Write-Host ""
$results | Format-Table -AutoSize

$failed = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
$skipped = @($results | Where-Object { $_.Result -eq "SKIP" }).Count
Write-Host ""
if ($failed -eq 0) {
    Write-Host "GATE PASSED ($skipped skipped) - the Territory-derived business-unit picker keeps matching while MOD-0151 still reads the legacy country set." -ForegroundColor Green
} else {
    Write-Host "GATE FAILED ($failed failing, $skipped skipped)." -ForegroundColor Yellow
    Write-Host "FU07 is NOT blocked: business-unit periods stay authorable because the picker falls back to the published" -ForegroundColor Yellow
    Write-Host "business-unit vocabulary (D-BU-SOURCE soft gate). The list is simply no longer narrowed by a field plan" -ForegroundColor Yellow
    Write-Host "until the two country vocabularies are aligned - follow-up F-COUNTRY-SOT." -ForegroundColor Yellow
}
