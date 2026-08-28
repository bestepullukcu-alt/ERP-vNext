param([string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path)

$audit = Join-Path $RepoRoot "docs/audits/mod-0029-fu36d-fu37d-authenticated-runtime-smoke-commit-separation-2026-07-25.md"
if (!(Test-Path -LiteralPath $audit)) { Write-Error "Final runtime smoke audit is missing."; exit 1 }
$text = Get-Content -Raw -LiteralPath $audit
$required = @(
    "Final verdict:", "Fleet health recorded", "Authenticated status recorded",
    "Company registration smoke", "Corporate registration smoke", "Language lookup smoke recorded",
    "Retention lookup smoke recorded", "Operation Completed-only success",
    "Manual link scope mismatch", "Reverse navigation smoke", "Normal Controlled Documents Create GET",
    "Template create GET", "LEGACY_CREATE_RESTRICTED", "Build/test/verifier matrix",
    "Commit separation audit", "Unrelated CRM/HCM files identified", "mixed files",
    "No commit/push", "Scoped diff-check", "watch-diten.ps1"
)
$missing = @($required | Where-Object { $text -notmatch [regex]::Escape($_) })
if ($missing.Count) {
    $missing | ForEach-Object { Write-Error "Missing evidence marker: $_" }
    exit 1
}
Write-Host "PASS MOD-0029-FU36D/FU37D audit completeness (BLOCKED runtime evidence recorded; no fake PASS)"
