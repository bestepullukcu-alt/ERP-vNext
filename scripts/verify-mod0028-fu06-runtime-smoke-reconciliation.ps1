$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$auditPath = Join-Path $repoRoot 'docs/audits/mod-0028-fu06-runtime-smoke-reconciliation-2026-07-25.md'
$fixAuditPath = Join-Path $repoRoot 'docs/audits/mod-0028-fu06-mongo-index-compatibility-fix-2026-07-25.md'
$dcpPath = Join-Path $repoRoot 'execution/portfolio/delivery-capability-packs/DCP-004-corporate-collection-controlled-document-registration-scope.md'
$packPath = Join-Path $repoRoot 'execution/domains/platform-shared-services/module-packs/MOD-0028-FU06-corporate-collection-instance-foundation.md'
$fu37Path = Join-Path $repoRoot 'execution/domains/platform-shared-services/module-packs/MOD-0029-FU37-corporate-company-registration-amendment.md'
$failures = [System.Collections.Generic.List[string]]::new()

function Require([string]$Path, [string]$Pattern, [string]$Message) {
    if (-not (Test-Path -LiteralPath $Path) -or -not (Select-String -LiteralPath $Path -Pattern $Pattern -Quiet)) {
        $failures.Add($Message)
    }
}

Require $auditPath 'MongoDB .*27017.*reachable' 'Mongo reachability evidence missing.'
Require $auditPath 'Authenticated Route Smoke' 'Authenticated route section missing.'
Require $auditPath 'Provisioning Happy Path' 'Provisioning evidence section missing.'
Require $auditPath 'Idempotency Evidence' 'Idempotency evidence missing.'
Require $auditPath 'Retry Evidence' 'Retry evidence/gap missing.'
Require $auditPath 'Mongo Index Evidence' 'Mongo index evidence missing.'
Require $auditPath 'Concurrency Evidence' 'Concurrency evidence/gap missing.'
Require $auditPath 'no grant returns false' 'Deny-by-default evidence missing.'
Require $auditPath 'explicit user policy' 'Explicit grant evidence missing.'
Require $auditPath 'Cross-Tenant Evidence' 'Cross-tenant evidence/gap missing.'
Require $auditPath 'Company Compatibility Evidence' 'Company compatibility evidence missing.'
Require $auditPath 'tenant/\{tenantId\}/company/\{companyId\}/folder/\{folderId\}' 'Exact Company partition missing.'
Require $auditPath 'tenant/\{tenantId\}/corporate/\{corporateOwnerId\}/folder/\{folderId\}' 'Exact Corporate partition missing.'
Require $auditPath 'no dummy value' 'No-dummy evidence missing.'
Require $auditPath 'not made nullable' 'No nullable-only evidence missing.'
Require $auditPath 'MOD-0029-FU37 remains `draft`' 'FU37 draft confirmation missing.'
Require $auditPath 'FU36C/FU36D remain paused' 'FU36C/FU36D pause confirmation missing.'
Require $auditPath 'MOD-0029 runtime changed: No' 'MOD-0029 guardrail missing.'
Require $auditPath 'Frontend/AuthService/Gateway/Ocelot changed: No' 'External-path guardrail missing.'
Require $packPath '^runtime_implementation: implemented-runtime-green-with-nonblocking-gaps$' 'FU06 runtime status not reconciled.'
Require $fixAuditPath '^## 8\. Runtime startup evidence$' 'Compatibility-fix runtime evidence missing.'
Require $fixAuditPath 'Health HTTP 200' 'Resolved health evidence missing.'
Require $dcpPath '^status: approved$' 'DCP-004 must remain approved.'
Require $fu37Path '^status: ready-for-dev$' 'FU37 must remain at the approved ready-for-dev governance state.'
Require $fu37Path '^runtime_implementation: implemented-with-runtime-gaps$' 'FU37 runtime-gap status must remain explicit until final smoke passes.'

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PASS: FU06 historical blocker and resolved Mongo compatibility evidence are truthfully reconciled.'
