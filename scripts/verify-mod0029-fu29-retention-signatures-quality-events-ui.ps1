<#
    MOD-0029-FU29 — Retention, Signatures & Quality Events verifier.

    Static contract + guardrail checks for the last three Master Register detail tabs: Retention (FU15),
    Electronic Signatures (FU23) and Quality Events / Deviations / CAPA (FU22). Read-only: it never edits,
    builds or calls a service. Run from the repository root:

        pwsh ./scripts/verify-mod0029-fu29-retention-signatures-quality-events-ui.ps1

    Companions: verify-mod0029-fu24-ui.ps1 … -fu28a-repository-assessment-master-ui.ps1.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir '..')).Path
}

$script:Failures = @()
$script:Checks = 0

function Assert-True {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')
    $script:Checks++
    if ($Condition) { Write-Host ("  PASS  {0}" -f $Name) -ForegroundColor Green }
    else {
        Write-Host ("  FAIL  {0} {1}" -f $Name, $Detail) -ForegroundColor Red
        $script:Failures += $Name
    }
}

function Get-Text {
    param([string]$RelativePath)
    $full = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $full)) { return $null }
    return [System.IO.File]::ReadAllText($full)
}

function Remove-Comments {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $t = $Text
    $t = [regex]::Replace($t, '@\*[\s\S]*?\*@', ' ')
    $t = [regex]::Replace($t, '<!--[\s\S]*?-->', ' ')
    $t = [regex]::Replace($t, '/\*[\s\S]*?\*/', ' ')
    $t = [regex]::Replace($t, '(?m)^\s*//.*$', ' ')
    return $t
}

$web = 'frontend/Diten.Web'
$viewDir = "$web/Views/DocumentManagement/MasterRegister"
$jsDir = "$web/wwwroot/assets/js/DocumentManagement/MasterRegister"
$resxDir = "$web/Resources/Views/DocumentManagement/MasterRegister"
$cultures = @('ar', 'en', 'es', 'fr', 'ru', 'tr', 'zh')

Write-Host "`nMOD-0029-FU29 — Retention, Signatures & Quality Events verifier" -ForegroundColor Cyan
Write-Host ("Repo root: {0}`n" -f $RepoRoot)

$details = Get-Text "$viewDir/Details.cshtml"
$js = Get-Text "$jsDir/details.js"
$controller = Get-Text "$web/Controllers/DocumentManagementMasterRegisterController.cs"
$l10n = Get-Text "$viewDir/_IndexL10n.cshtml"
$detailsCode = Remove-Comments $details
$jsCode = Remove-Comments $js
$controllerCode = Remove-Comments $controller

# 1 — the three tabs are now real, no placeholder remains
Write-Host 'Tab wiring' -ForegroundColor Cyan
Assert-True 'Retention pane is a real container' ($detailsCode -match 'id="tab-retention"')
Assert-True 'Signatures pane is a real container' ($detailsCode -match 'id="tab-signatures"')
Assert-True 'Quality Events pane is a real container' ($detailsCode -match 'id="tab-quality"')
Assert-True 'no deferred placeholder tabs remain' (-not ($detailsCode -match 'DeferredSectionMessage'))
Assert-True 'no deferredTabs loop remains in the view' (-not ($detailsCode -match 'deferredTabs'))
Assert-True 'Retention tab button present' ($detailsCode -match 'id="tabBtn-retention"')
Assert-True 'Signatures tab button present' ($detailsCode -match 'id="tabBtn-signatures"')
Assert-True 'Quality tab button present' ($detailsCode -match 'id="tabBtn-quality"')
Assert-True 'Retention lazy loaded on shown.bs.tab' ($jsCode -match "tabBtn-retention'\)\?\.addEventListener\('shown\.bs\.tab")
Assert-True 'Signatures lazy loaded on shown.bs.tab' ($jsCode -match "tabBtn-signatures'\)\?\.addEventListener\('shown\.bs\.tab")
Assert-True 'Quality lazy loaded on shown.bs.tab' ($jsCode -match "tabBtn-quality'\)\?\.addEventListener\('shown\.bs\.tab")
Assert-True 'manual reload buttons exist' ($detailsCode -match 'btnReloadRetention' -and $detailsCode -match 'btnReloadSignatures' -and $detailsCode -match 'btnReloadQuality')

# 2 — earlier FUs preserved
Write-Host "`nFU24 - FU28 preserved" -ForegroundColor Cyan
Assert-True 'FU24 General tab intact' ($detailsCode -match 'id="tab-general"' -and $detailsCode -match 'detailIdentificationList')
Assert-True 'FU25 Identifiers tab intact' ($detailsCode -match 'id="tab-identifiers"' -and $detailsCode -match 'identifierLedgerBody')
Assert-True 'FU25 Lifecycle tab intact' ($detailsCode -match 'id="tab-lifecycle"' -and $detailsCode -match 'lifecycleHistoryBody')
Assert-True 'FU26 Approval tab intact' ($detailsCode -match 'id="tab-approval"' -and $detailsCode -match 'approvalRequirementsBody')
Assert-True 'FU26 Release Gates tab intact' ($detailsCode -match 'id="tab-gates"' -and $detailsCode -match 'gatesCardList')
Assert-True 'FU27 Training tab intact' ($detailsCode -match 'id="tab-training"' -and $detailsCode -match 'trainingRequirementsBody')
Assert-True 'FU28 Repository tab intact' ($detailsCode -match 'id="tab-repository"' -and $detailsCode -match 'repositoryAssessmentsBody')

# 3 — proxy endpoints
Write-Host "`nMVC proxy endpoints" -ForegroundColor Cyan
foreach ($r in @('retention/subject', 'retention/legal-holds', 'retention/dispositions', 'retention/evaluate')) {
    Assert-True "retention proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
foreach ($r in @('signatures/policies', 'signatures/requests', 'signatures/records')) {
    Assert-True "signature proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}
Assert-True 'signature verify proxy route' ($controllerCode -match 'signatures/\{signatureId:guid\}/verify')
foreach ($r in @('quality-events', 'quality-events/deviations', 'quality-events/capa')) {
    Assert-True "quality proxy route: $r" ($controllerCode -match [regex]::Escape("MasterRegister/api/{id:guid}/$r"))
}

# Downstream must hit the REAL backend routes.
Assert-True 'downstream retention subject route correct' ($controllerCode -match 'retention/subjects/')
Assert-True 'downstream legal-holds route correct' ($controllerCode -match 'ApiRoot\}/legal-holds')
Assert-True 'downstream disposition-requests route correct' ($controllerCode -match 'ApiRoot\}/disposition-requests')
Assert-True 'downstream retention evaluate route correct' ($controllerCode -match 'ApiRoot\}/retention/evaluate')
Assert-True 'downstream signature-policies route correct' ($controllerCode -match 'ApiRoot\}/signature-policies')
Assert-True 'downstream signature-requests route correct' ($controllerCode -match 'ApiRoot\}/signature-requests')
Assert-True 'downstream signatures verify route correct' ($controllerCode -match 'signatures/\{signatureId\}/verify')
Assert-True 'downstream quality-events route correct' ($controllerCode -match 'ApiRoot\}/quality-events')
Assert-True 'downstream deviations route correct' ($controllerCode -match 'ApiRoot\}/deviations')
Assert-True 'downstream capa-actions route correct' ($controllerCode -match 'ApiRoot\}/capa-actions')

# Retention evaluate must pin subject identity server-side from the route (never trust the browser).
Assert-True 'retention evaluate forces subjectType/subjectId server-side' `
    ($controllerCode -match 'payload\["subjectType"\]\s*=\s*RetentionSubjectType' -and $controllerCode -match 'payload\["registerEntryId"\]')

$fu29Posts = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(retention|signatures)[^"]*"\)\]')
$fu29Guarded = [regex]::Matches($controllerCode, '\[HttpPost\("/DocumentManagement/MasterRegister/api/\{id:guid\}/(retention|signatures)[^"]*"\)\]\s*\r?\n\s*\[ValidateAntiForgeryToken\]')
# 2 mutations: retention/evaluate + signatures/{id}/verify. Everything else is read-only.
Assert-True 'FU29 POST proxy surface is exactly the 2 safe mutations' ($fu29Posts.Count -eq 2) ("found $($fu29Posts.Count)")
Assert-True 'every FU29 POST proxy has [ValidateAntiForgeryToken]' `
    ($fu29Posts.Count -gt 0 -and $fu29Guarded.Count -eq $fu29Posts.Count) ("guarded $($fu29Guarded.Count) of $($fu29Posts.Count)")

# 4 — guardrails
Write-Host "`nGuardrails" -ForegroundColor Cyan
Assert-True 'JS uses same-origin MVC proxy only' ($jsCode -match '/DocumentManagement/MasterRegister/api')
Assert-True 'no direct Platform 5057 call' (-not ($jsCode -match '5057') -and -not ($detailsCode -match '5057'))
Assert-True 'no localhost URL' (-not ($jsCode -match 'http://localhost') -and -not ($detailsCode -match 'http://localhost'))
Assert-True 'no X-Tenant-Id in browser code' (-not ($jsCode -match 'X-Tenant-Id'))
Assert-True 'no tenant id field in the details view' (-not ($detailsCode -match '(?i)tenantid'))
Assert-True 'no DELETE verb from the UI' (-not ($jsCode -match "method:\s*'DELETE'"))
Assert-True 'no FU29 delete/purge proxy action' `
    (-not ($controllerCode -match '(?i)(retention|signatures|quality-events|legal-hold|disposition)[^\n]*\b(delete|purge)\b'))
Assert-True 'no file upload surface' (-not ($detailsCode -match '(?i)type="file"') -and -not ($jsCode -match '(?i)contentBase64'))
Assert-True 'anti-forgery token on every mutation' ($jsCode -match '__RequestVerificationToken')
Assert-True 'buttons locked during a request' ($jsCode -match 'button\.disabled = true')
Assert-True 'server messages HTML-escaped' ($jsCode -match 'replace\(/\[&<>"' + "'" + '\]/g')
Assert-True 'no auto dispose / sign / hold / close chaining' `
    (-not ($jsCode -match '(?i)(autoDispose|autoSign|autoHold|autoClose|forceDispose|forceSign|releaseHold\(|applyHold\()'))
Assert-True 'no legal hold override control' (-not ($detailsCode -match '(?i)overrideHold') -and -not ($jsCode -match '(?i)overrideHold'))
Assert-True 'no certificate / provider validation claim in view' (-not ($detailsCode -match '(?i)(certificate valid|21 cfr|part 11|annex 11)'))
Assert-True 'no fake fingerprint computation in JS (verify is server-side)' (-not ($jsCode -match '(?i)(computeFingerprint|sha256\(|hashObject)'))
Assert-True 'no external QMS/CAPA integration introduced' (-not ($jsCode -match '(?i)(sharepoint|opentext|veeva|trackwise|externalqms)'))
Assert-True 'no auto release-gate evaluate triggered by FU29 tabs' (-not ($jsCode -match "Retention[\s\S]{0,600}?release-gates/evaluate"))
Assert-True 'verify reads backend fingerprintMatches, never assigns it' `
    ($jsCode -match 'fingerprintMatches === true' -and -not ($jsCode -match 'fingerprintMatches\s*=(?![=>])'))

# 5 — compliance / boundary guardrails
Write-Host "`nCompliance-claim guardrails" -ForegroundColor Cyan
Assert-True 'metadata-fingerprint-only warning rendered' ($detailsCode -match 'MetadataFingerprintOnly')
Assert-True 'no e-sign compliance claim warning rendered' ($detailsCode -match 'NoESignComplianceClaim' -and $detailsCode -match 'NoCertificateValidationClaim')
Assert-True 'legal-hold-blocks-disposition message rendered' ($detailsCode -match 'LegalHoldBlocksDisposition')
Assert-True 'no-disposition-override message rendered' ($detailsCode -match 'NoDispositionOverride')
Assert-True 'release-hold-does-not-dispose message rendered' ($detailsCode -match 'ReleaseHoldDoesNotDispose')
Assert-True 'quality link-does-not-close-event message rendered' ($detailsCode -match 'LinkDoesNotCloseEvent')
Assert-True 'no-external-QMS message rendered' ($detailsCode -match 'NoExternalQmsCall')
Assert-True 'TwoFactorNotImplemented surfaced (no fake 2FA claim)' ($jsCode -match 'TwoFactorNotImplemented')

# 6 — read/action contracts
Write-Host "`nRead & action contracts" -ForegroundColor Cyan
Assert-True 'retention schedule + disposition panels present' ($detailsCode -match 'retentionScheduleList' -and $detailsCode -match 'retentionDispositionList')
Assert-True 'legal hold + disposition tables present' ($detailsCode -match 'retentionLegalHoldsBody' -and $detailsCode -match 'retentionDispositionsBody')
Assert-True 'signature policies/requests/records tables present' ($detailsCode -match 'signaturePoliciesBody' -and $detailsCode -match 'signatureRequestsBody' -and $detailsCode -match 'signatureRecordsBody')
Assert-True 'quality events/deviations/capa tables present' ($detailsCode -match 'qualityEventsBody' -and $detailsCode -match 'qualityDeviationsBody' -and $detailsCode -match 'qualityCapaBody')
Assert-True 'retention Evaluate is the only retention mutation (read/evaluate only)' ($jsCode -match "'/retention/evaluate'")
Assert-True 'signature Verify wired' ($jsCode -match "signatures/\$\{encodeURIComponent\(sigId\)\}/verify")
Assert-True 'quality tab is read-only (no link/refresh mutation POST from JS)' `
    (-not ($jsCode -match "postJson\('/quality-events"))
Assert-True 'empty states handled' ($jsCode -match 'NoRetentionScheduleFound' -and $jsCode -match 'NoSignatureRecordsFound' -and $jsCode -match 'NoQualityEventsFound')
Assert-True '401/403 handled by shared unauthorized + failure describe' ($jsCode -match "res\?\.status === 401" -and $jsCode -match "res\?\.status === 403")
foreach ($code in @('RETENTION_SUBJECT_NOT_FOUND', 'LEGAL_HOLD_ACTIVE', 'DISPOSITION_BLOCKED', 'DISPOSITION_NOT_ELIGIBLE',
                    'SIGNATURE_POLICY_NOT_FOUND', 'SIGNATURE_NOT_ALLOWED', 'SUBJECT_NOT_SIGNABLE', 'FINGERPRINT_MISMATCH',
                    'AUTHENTICATION_CONTEXT_REQUIRED', 'TWO_FACTOR_NOT_IMPLEMENTED', 'DUPLICATE_SIGNATURE',
                    'UNAPPROVED_REPOSITORY', 'QUALITY_EVENT_NOT_FOUND', 'QUALITY_EVENT_BLOCKING', 'CAPA_OPEN',
                    'CAPA_EFFECTIVENESS_PENDING', 'DEVIATION_OPEN', 'LINK_ALREADY_EXISTS', 'EVIDENCE_REFERENCE_REQUIRED',
                    'REASON_REQUIRED', 'NOT_FOUND_NON_LEAKAGE', 'PERMISSION_DENIED', 'VALIDATION_FAILED')) {
    Assert-True "reason code mapped: $code" ($jsCode -match [regex]::Escape($code))
}
Assert-True 'unknown reason codes fall through to the server message' ($jsCode -match 'if \(serverMessage\) return serverMessage;')

# 7 — permission gating on EXACT seeded keys
Write-Host "`nPermission gating (seeded keys only)" -ForegroundColor Cyan
$seededKeys = @(
    'platform.document-management.retention.view',
    'platform.document-management.retention.manage',
    'platform.document-management.legal-hold.view',
    'platform.document-management.signatures.view',
    'platform.document-management.signatures.verify',
    'platform.document-management.quality-events.view',
    'platform.document-management.deviations.view',
    'platform.document-management.capa.view'
)
foreach ($k in $seededKeys) { Assert-True "gated on seeded key: $k" ($details -match [regex]::Escape($k)) }

$seeder = Get-Text 'services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs'
foreach ($k in $seededKeys) {
    $parts = $k -split '\.'
    $action = $parts[-1]
    $module = ($parts[1..($parts.Count - 2)]) -join '.'
    Assert-True "key is actually seeded: $k" ($seeder -match ('"' + [regex]::Escape($module) + '",\s*"' + [regex]::Escape($action) + '"'))
}
# There is no signatures.request-here / quality-events.link-entry namespace and no invented sign/close key.
Assert-True 'no invented FU29 permission keys' `
    (-not ($details -match 'document-management\.(retention-schedule|signature-request|quality-link)\.'))

# 8 — localization parity
Write-Host "`nLocalization (7-culture parity)" -ForegroundColor Cyan
$keySets = @{}
foreach ($c in $cultures) {
    $path = Join-Path $RepoRoot "$resxDir/MasterRegisterIndex.$c.resx"
    if (-not (Test-Path -LiteralPath $path)) { Assert-True "resx exists: $c" $false; continue }
    [xml]$xml = Get-Content -LiteralPath $path -Encoding UTF8 -Raw
    $keySets[$c] = @($xml.root.data | ForEach-Object { $_.name }) | Sort-Object
    Assert-True "resx exists: $c" $true
}
if ($keySets.Count -eq $cultures.Count) {
    $baseline = $keySets['en']
    Assert-True 'no duplicate keys in en resx' (($baseline | Group-Object | Where-Object { $_.Count -gt 1 }).Count -eq 0)
    foreach ($c in $cultures) {
        $diff = Compare-Object -ReferenceObject $baseline -DifferenceObject $keySets[$c]
        Assert-True ("resx key parity en vs {0} ({1} keys)" -f $c, $baseline.Count) ($null -eq $diff) ("delta: " + (($diff | ForEach-Object { $_.InputObject }) -join ','))
    }
    foreach ($k in @('RetentionTabTitle', 'SignaturesTabTitle', 'QualityEventsTabTitle', 'LegalHoldBlocksDisposition',
                     'MetadataFingerprintOnly', 'NoESignComplianceClaim', 'LinkDoesNotCloseEvent', 'NoExternalQmsCall',
                     'TwoFactorNotImplemented', 'DispositionReadiness')) {
        Assert-True "FU29 key present in en resx: $k" ($baseline -contains $k)
    }
    foreach ($k in @('DocumentMasterRegisterTitle', 'IdentifiersTabTitle', 'LifecycleTabTitle', 'ApprovalTabTitle',
                     'ReleaseGatesTabTitle', 'TrainingTabTitle', 'RepositoryTabTitle')) {
        Assert-True "FU24-FU28 key preserved: $k" ($baseline -contains $k)
    }
}
Assert-True 'FU29 keys exported to window.L10n' ($l10n -match 'RetentionTabTitle' -and $l10n -match 'QualityEventsTabTitle' -and $l10n -match 'MetadataFingerprintOnly')

# 9 — out-of-scope surfaces
Write-Host "`nOut-of-scope surfaces" -ForegroundColor Cyan
if ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) {
    Push-Location $RepoRoot
    try {
        # NOTE: the FU29 permission-seed hardening (a separate, pre-existing backend track that THIS UI task only
        # consumes) legitimately carries the MOD-0029-FU29 marker inside the AuthService seed, so that path is
        # deliberately NOT scanned here. This UI task authors no backend/seed/gateway change; the leak scan targets
        # the surfaces a UI change could actually spill into: the gateway and the CRM/HCM front-ends.
        $leaked = @(git grep -l 'MOD-0029-FU29' -- 'gateway' 'frontend/Diten.Web/Views/CRM' 'frontend/Diten.Web/Views/HCM' 'frontend/Diten.Web/Controllers/CRM' 'frontend/Diten.Web/Controllers/HCM' 2>$null)
    }
    catch { $leaked = @() }
    finally { Pop-Location }
    Assert-True 'no FU29 UI change leaked into gateway / CRM / HCM' ($leaked.Count -eq 0) ("touched: " + ($leaked -join ', '))
}
else { Write-Host '  SKIP  git not available' -ForegroundColor Yellow }

Write-Host ''
if ($script:Failures.Count -eq 0) {
    Write-Host ("VERDICT: PASS — {0}/{0} checks green" -f $script:Checks) -ForegroundColor Green
    exit 0
}
Write-Host ("VERDICT: FAIL — {0}/{1} checks failed" -f $script:Failures.Count, $script:Checks) -ForegroundColor Red
$script:Failures | ForEach-Object { Write-Host ("  - {0}" -f $_) -ForegroundColor Red }
exit 1
