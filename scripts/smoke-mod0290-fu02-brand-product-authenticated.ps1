<#
  MOD-0290-FU02 — Brand/Product Runtime + UI · Authenticated Gateway Live Smoke (run this YOURSELF).

  Why you run it (not the agent): logging in requires typing a password, and entering credentials to
  authenticate is outside what the assistant may do on your behalf. This script keeps the credential in YOUR
  process memory only — it is never written to a file, and the Authorization header is never printed (masked).
  Paste the printed PASS/FAIL table back to the assistant to finalize the evidence report; it contains no secret.

  Usage (from repo root, in PowerShell 5.1):
      ./scripts/smoke-mod0290-fu02-brand-product-authenticated.ps1
  Optional:
      -BaseUrl http://localhost:5000  -TenantId 97c59330-dbc4-4665-b29c-0c26dbb5cc93

  ALL business calls go through the Gateway (5000). Direct 5059 is used ONLY for /health.
  Cleanup is ARCHIVE-ONLY: nothing is deleted, and no Mongo document is hand-edited.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl   = "http://localhost:5000",
    [string]$TenantId  = "97c59330-dbc4-4665-b29c-0c26dbb5cc93",
    [string]$MdmDirect = "http://localhost:5059"
)

$ErrorActionPreference = "Stop"
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Step, $Expected, $Actual, [bool]$Pass) {
    $results.Add([pscustomobject]@{ Step = $Step; Expected = "$Expected"; Actual = "$Actual"; Result = $(if ($Pass) { "PASS" } else { "FAIL" }) })
}

# PS 5.1-compatible status probe (-SkipHttpErrorCheck is PowerShell 7+ only).
function Status([string]$Url, [string]$Method = "GET", $Headers = $null, $Body = $null) {
    try {
        $p = @{ Uri = $Url; Method = $Method; TimeoutSec = 20; UseBasicParsing = $true }
        if ($Headers) { $p.Headers = $Headers }
        if ($Body)    { $p.Body = $Body; $p.ContentType = "application/json" }
        return [int](Invoke-WebRequest @p).StatusCode
    } catch {
        $resp = $_.Exception.Response
        if ($resp -and $resp.StatusCode) { return [int]$resp.StatusCode }
        return -1
    }
}

$brands   = "$BaseUrl/api/mdm/brands"
$products = "$BaseUrl/api/mdm/products"
$contract = "$BaseUrl/api/mdm/brand-products/contract"
$run      = (Get-Date -Format "yyyyMMddHHmmss")

Write-Host "== MOD-0290-FU02 authenticated smoke ($run) ==" -ForegroundColor Cyan

# ---------------- A. Fleet health ----------------
foreach ($p in @(5000, 5001, 5056, 5057, 5059)) {
    $code = Status "http://localhost:$p/"
    Add-Result "Fleet port $p reachable" "reachable" $code ($code -ne -1)
}
$mdmHealth = Status "$MdmDirect/health"
Add-Result "MDM direct /health (only allowed direct call)" "200" $mdmHealth ($mdmHealth -in 200, 204)

# Unauthenticated preflight — the routes must exist AND fail closed (401/403, never 404/200).
$anonContract = Status $contract
Add-Result "Gateway route exists + fails closed (contract, anon)" "401/403" $anonContract ($anonContract -in 401, 403)
$anonBrands = Status $brands
Add-Result "Gateway route exists + fails closed (brands, anon)" "401/403" $anonBrands ($anonBrands -in 401, 403)
$anonProducts = Status $products
Add-Result "Gateway route exists + fails closed (products, anon)" "401/403" $anonProducts ($anonProducts -in 401, 403)

# ---------------- B. Login (credential stays in your memory) ----------------
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
$loginBody = $null; $cred = $null   # drop the plaintext password ASAP

if (-not $token) {
    Add-Result "Gateway login" "200 + token" "no token" $false
    $results | Format-Table -AutoSize
    Write-Host "Login failed - cannot run authenticated steps." -ForegroundColor Red
    return
}
Add-Result "Gateway login" "200 + token" "200 (token MASKED)" $true

# Tenant claim check (decode the JWT payload locally; nothing secret is printed).
$payload = ($token.Split('.')[1]).Replace('-', '+').Replace('_', '/')
switch ($payload.Length % 4) { 2 { $payload += '==' } 3 { $payload += '=' } }
$claims = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
Add-Result "Tenant claim == target tenant" $TenantId $claims.tenant_id ($claims.tenant_id -eq $TenantId)

$auth = @{ Authorization = "Bearer $token"; "X-Tenant-Id" = $TenantId }
function Get-Json ([string]$Url)       { Invoke-RestMethod -Uri $Url -Method GET  -Headers $auth -TimeoutSec 20 }
function Post-Json([string]$Url, $Obj) { Invoke-RestMethod -Uri $Url -Method POST -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 20 }
function Put-Json ([string]$Url, $Obj) { Invoke-RestMethod -Uri $Url -Method PUT  -Headers $auth -ContentType "application/json" -Body ($Obj | ConvertTo-Json -Depth 8) -TimeoutSec 20 }

# ---------------- C. Contract ----------------
$c = (Get-Json $contract).data
$f = $c.features
$flagsOk = $f.supportsBrandManagement -and $f.supportsProductManagement -and $f.supportsBrandProductReference `
    -and $f.supportsBrandProductHierarchy -and $f.supportsExternalReferences -and $f.supportsArchiveLifecycle `
    -and $f.supportsEffectiveDating -and $f.supportsContractDrivenUi
Add-Result "Contract 200 + all 8 flags true" "true x8" $flagsOk $flagsOk

$forbidden = "supportsCampaignRuntime", "supportsCampaignEngine", "supportsKnowledgeRuntime", "supportsVisitPlanning",
             "supportsRoutePlanning", "supportsFrequencyRuntime", "supportsRecommendationEngine", "supportsWorkflowApproval",
             "supportsDigitalDetailing", "supportsSegmentation", "supportsAtcLocalMaster",
             "supportsTherapeuticAreaFlatReferenceSet", "supportsIndicationMaster", "supportsItemSku",
             "supportsUomMapping", "supportsImportExport", "supportsHardDelete", "supportsMultiBrand"
$flagNames = $f.PSObject.Properties.Name
# PS 5.1: wrap the pipeline in @() so a single match still yields an array (a bare match is a scalar).
$leak = @($forbidden | Where-Object { $flagNames -contains $_ })
Add-Result "Forbidden flags absent from contract" "none" $(if ($leak.Count) { $leak -join ',' } else { "none" }) ($leak.Count -eq 0)
Add-Result "ProductStatus vocabulary has no 'discontinued'" "absent" ($c.vocabulary.productStatuses -join ',') (-not ($c.vocabulary.productStatuses -contains "discontinued"))

# ---------------- D. Create Brand ----------------
# tenantId is injected on purpose: it MUST be ignored (the JWT claim wins).
$brandBody = @{
    brandCode = "SMK-BR-$run"; brandName = "FU02 smoke brand"; brandStatus = "active"
    description = "created through the authenticated Gateway"
    effectiveFrom = "2026-01-01T00:00:00Z"
    externalReferences = @(@{ sourceSystem = "LEGACY-CRM"; externalId = "BR-$run"; isPrimary = $true })
    tenantId = "ffffffff-ffff-ffff-ffff-ffffffffffff"
}
$brandResp = Post-Json $brands $brandBody
$brandId = $brandResp.data
Add-Result "Create Brand (201 + BrandId)" "201 + guid" "$($brandResp.statusCode) / $brandId" ($brandResp.statusCode -eq 201 -and $brandId)

$brandRead = (Get-Json "$brands/$brandId").data
Add-Result "TenantId payload ignored (brand readable in claim tenant)" "readable" "code=$($brandRead.brandCode)" ($brandRead -and -not $brandRead.isArchived)

# ---------------- E. Create Product referencing the Brand ----------------
$productBody = @{
    productCode = "SMK-PR-$run"; productName = "FU02 smoke product"; productStatus = "active"
    brandId = $brandId; productType = "medicine"; dosageForm = "tablet"; strength = "10 mg"; packSize = "28"
    unitOfMeasure = "mg"; atcCode = "C09AA"; effectiveFrom = "2026-01-01T00:00:00Z"
    externalReferences = @(@{ sourceSystem = "LEGACY-CRM"; externalId = "PR-$run"; isPrimary = $true })
}
$productResp = Post-Json $products $productBody
$productId = $productResp.data
Add-Result "Create Product referencing Brand (201)" "201 + guid" "$($productResp.statusCode) / $productId" ($productResp.statusCode -eq 201 -and $productId)

# ---------------- F. Product detail read ----------------
$p = (Get-Json "$products/$productId").data
Add-Result "Product detail read (brand link + ATC pointer)" "brandId + C09AA" "$($p.brandId) / $($p.atcCode)" ($p.brandId -eq $brandId -and $p.atcCode -eq "C09AA")

# ---------------- G. Brand -> products relation ----------------
$rel = (Get-Json "$brands/$brandId/products").data
Add-Result "Brand products relation lists the product" "1 row" "$(@($rel).Count) row(s)" (@($rel | Where-Object { $_.productId -eq $productId }).Count -eq 1)

# ---------------- H. BrandId = null product (FU01 4.1 optional) ----------------
$genericBody = @{
    productCode = "SMK-GEN-$run"; productName = "FU02 smoke generic (no brand)"; productStatus = "active"
    productType = "other"; effectiveFrom = "2026-01-01T00:00:00Z"
}
$genericResp = Post-Json $products $genericBody
$genericId = $genericResp.data
Add-Result "Create Product WITHOUT brand (BrandId optional)" "201" "$($genericResp.statusCode)" ($genericResp.statusCode -eq 201)

# ---------------- I. Response-shape guard ----------------
$forbiddenFields = "campaignTargetId", "visitPlanId", "routePlanId", "routeId", "dueStatus", "overdue",
                   "lastVisitDate", "requiredVisitCount", "periodType", "frequencyPolicyId", "segmentMembership",
                   "knowledgeContentPayload", "contentRenderUrl", "recommendationId", "nextBestAction",
                   "workflowApprovalId", "consentRecordPayload", "preferenceRecordPayload", "patientId",
                   "skuId", "uomMappingId"
$productFields = $p.PSObject.Properties.Name
$brandFields   = $brandRead.PSObject.Properties.Name
$shapeLeak = @($forbiddenFields | Where-Object { $productFields -contains $_ -or $brandFields -contains $_ })
Add-Result "Response shape guard clean" "none" $(if ($shapeLeak.Count) { $shapeLeak -join ',' } else { "none" }) ($shapeLeak.Count -eq 0)

# ---------------- J. Archive Product ----------------
$archiveProduct = Status "$products/$productId/archive" "POST" $auth
Add-Result "Archive Product (POST /archive)" "200/204" $archiveProduct ($archiveProduct -in 200, 204)

$archivedProduct = (Get-Json "$products/$productId").data
Add-Result "Archived product still READABLE" "isArchived=true" "isArchived=$($archivedProduct.isArchived)" ($archivedProduct.isArchived -eq $true)

$updateArchivedProduct = Status "$products/$productId" "PUT" $auth (($productBody | ConvertTo-Json -Depth 8))
Add-Result "Archived product UPDATE refused" "409" $updateArchivedProduct ($updateArchivedProduct -eq 409)

# ---------------- K. Archive Brand ----------------
$archiveBrand = Status "$brands/$brandId/archive" "POST" $auth
Add-Result "Archive Brand (POST /archive)" "200/204" $archiveBrand ($archiveBrand -in 200, 204)

$archivedBrand = (Get-Json "$brands/$brandId").data
Add-Result "Archived brand still READABLE" "isArchived=true" "isArchived=$($archivedBrand.isArchived)" ($archivedBrand.isArchived -eq $true)

$updateArchivedBrand = Status "$brands/$brandId" "PUT" $auth (($brandBody | ConvertTo-Json -Depth 8))
Add-Result "Archived brand UPDATE refused" "409" $updateArchivedBrand ($updateArchivedBrand -eq 409)

# New product link to an archived brand must be refused (and NOT created).
$linkArchivedBody = @{
    productCode = "SMK-LNK-$run"; productName = "FU02 smoke archived-brand link"; productStatus = "active"
    brandId = $brandId; effectiveFrom = "2026-01-01T00:00:00Z"
} | ConvertTo-Json -Depth 8
$linkArchived = Status $products "POST" $auth $linkArchivedBody
Add-Result "New product link to ARCHIVED brand refused" "409" $linkArchived ($linkArchived -eq 409)

# No cascade: the generic product and the archived product both still exist.
$genericAfter = (Get-Json "$products/$genericId").data
Add-Result "Brand archive did NOT cascade (other products intact)" "readable" "isArchived=$($genericAfter.isArchived)" ($genericAfter.isArchived -eq $false)

# ---------------- L. DELETE must not exist ----------------
$deleteBrand = Status "$brands/$brandId" "DELETE" $auth
Add-Result "DELETE brand unsupported" "404/405" $deleteBrand ($deleteBrand -in 404, 405)
$deleteProduct = Status "$products/$productId" "DELETE" $auth
Add-Result "DELETE product unsupported" "404/405" $deleteProduct ($deleteProduct -in 404, 405)

# ---------------- M. Duplicate + validation guards ----------------
$dupBody = @{ brandCode = "SMK-BR-$run"; brandName = "duplicate"; brandStatus = "active"; effectiveFrom = "2026-01-01T00:00:00Z" } | ConvertTo-Json -Depth 8
$dup = Status $brands "POST" $auth $dupBody
Add-Result "Duplicate brand code (archived) refused" "409" $dup ($dup -eq 409)

$badStatusBody = @{ productCode = "SMK-BAD-$run"; productName = "bad status"; productStatus = "discontinued"; effectiveFrom = "2026-01-01T00:00:00Z" } | ConvertTo-Json -Depth 8
$badStatus = Status $products "POST" $auth $badStatusBody
Add-Result "Unauthorized status 'discontinued' refused" "400" $badStatus ($badStatus -eq 400)

$badWindowBody = @{ brandCode = "SMK-WIN-$run"; brandName = "bad window"; brandStatus = "active"; effectiveFrom = "2026-05-01T00:00:00Z"; effectiveTo = "2026-04-01T00:00:00Z" } | ConvertTo-Json -Depth 8
$badWindow = Status $brands "POST" $auth $badWindowBody
Add-Result "Inverted effective window refused" "400" $badWindow ($badWindow -eq 400)

# ---------------- N. Cleanup (ARCHIVE ONLY) ----------------
$cleanupGeneric = Status "$products/$genericId/archive" "POST" $auth
Add-Result "Cleanup: archive generic product (no delete)" "200/204" $cleanupGeneric ($cleanupGeneric -in 200, 204)

# ---------------- Report ----------------
$results | Format-Table -AutoSize
$failed = @($results | Where-Object { $_.Result -eq "FAIL" })
Write-Host ""
Write-Host ("TOTAL {0} | PASS {1} | FAIL {2}" -f $results.Count, ($results.Count - $failed.Count), $failed.Count) `
    -ForegroundColor $(if ($failed.Count) { "Red" } else { "Green" })
Write-Host "Cleanup was archive-only; nothing was deleted and no Mongo document was hand-edited." -ForegroundColor DarkGray
Write-Host "Note: canonical mdm.brands.* / mdm.products.* permissions are NOT seeded by MOD-0290-FU02." -ForegroundColor DarkGray
Write-Host "      403s on the authenticated steps mean the grant is missing - report that, do not work around it." -ForegroundColor DarkGray
