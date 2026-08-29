$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$indexPath = Join-Path $repoRoot 'services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs'
$instancePath = Join-Path $repoRoot 'services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/CollectionInstance.cs'
$fu37Path = Join-Path $repoRoot 'execution/domains/platform-shared-services/module-packs/MOD-0029-FU37-corporate-company-registration-amendment.md'
$source = Get-Content -LiteralPath $indexPath -Raw
$indexName = 'ux_dm_collection_instances_corporate_owner_baseline_node_active'
$marker = $source.IndexOf('Name = CorporateActiveInstanceIndexName', [StringComparison]::Ordinal)
$failures = [System.Collections.Generic.List[string]]::new()

if ($source.IndexOf($indexName, [StringComparison]::Ordinal) -lt 0 -or $marker -lt 0) {
    $failures.Add('Corporate active unique index is missing.')
}
else {
    $start = [Math]::Max(0, $marker - 1400)
    $length = [Math]::Min($source.Length - $start, 2200)
    $block = $source.Substring($start, $length)
    if ($block -notmatch 'Filter\.Eq\(x => x\.InstanceStatus,\s*CollectionInstanceStatus\.Active\)') {
        $failures.Add('Corporate partial index must use positive equality to CollectionInstanceStatus.Active.')
    }
    foreach ($negative in @('Filter.Ne', 'Filter.Not', 'Filter.Lt', '$ne', '$not')) {
        if ($block.IndexOf($negative, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $failures.Add("Unsupported/negative partial-filter expression found: $negative")
        }
    }
}

$instanceSource = Get-Content -LiteralPath $instancePath -Raw
if ($instanceSource -match 'Guid\?\s+CompanyId') {
    $failures.Add('CompanyId nullable quick fix is prohibited.')
}
if ($source -match 'dummy.*CompanyId|CompanyId.*dummy') {
    $failures.Add('Dummy CompanyId marker found.')
}
$fu37 = Get-Content -LiteralPath $fu37Path -Raw
if ($fu37 -notmatch '(?m)^status:\s* ready-for-dev$') {
    $failures.Add('MOD-0029-FU37 must remain at the approved ready-for-dev governance state.')
}
if ($fu37 -notmatch '(?m)^runtime_implementation:\s* implemented-with-runtime-gaps$') {
    $failures.Add('MOD-0029-FU37 must keep runtime gaps explicit until final smoke passes.')
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'PASS: FU06 Corporate unique partial index uses Mongo-compatible positive Active equality.'
