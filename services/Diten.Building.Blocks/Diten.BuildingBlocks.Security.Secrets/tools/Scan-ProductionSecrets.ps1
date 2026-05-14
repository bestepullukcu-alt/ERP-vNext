param(
    [string]$Root = "."
)

$sensitiveTokens = @("Secret", "ApiKey", "Password", "HashSecret", "Token", "ConnectionString")
$allowlist = @{
    "services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.json::MongoDbSettings:ConnectionString" = $true
    "services/Diten.Platform/src/Diten.Platform.API/appsettings.json::MongoDbSettings:ConnectionString" = $true
    "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/appsettings.json::Mongo:ConnectionString" = $true
    "frontend/Diten.Web/appsettings.json::ConnectionStrings:MongoDb" = $true
}

function Flatten-JsonValue {
    param(
        [Parameter(Mandatory = $true)]$Node,
        [string]$Prefix = ""
    )

    if ($null -eq $Node) {
        [pscustomobject]@{ Path = $Prefix; Value = $null }
        return
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Node.PSObject.Properties) {
            $nextPrefix = if ($Prefix) { "$Prefix`:$($property.Name)" } else { $property.Name }
            Flatten-JsonValue -Node $property.Value -Prefix $nextPrefix
        }
        return
    }

    if ($Node -is [System.Array]) {
        for ($i = 0; $i -lt $Node.Count; $i++) {
            Flatten-JsonValue -Node $Node[$i] -Prefix "$Prefix`:$i"
        }
        return
    }

    [pscustomobject]@{ Path = $Prefix; Value = $Node }
}

function Test-SensitiveKey {
    param([string]$Path)
    foreach ($token in $sensitiveTokens) {
        if ($Path.IndexOf($token, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }
    return $false
}

function Test-CredentialedConnectionString {
    param([string]$Value)
    $lower = $Value.ToLowerInvariant()
    return $lower.Contains("://") -and ($Value.Contains("@") -or $lower.Contains("password=") -or $lower.Contains("pwd=") -or $lower.Contains("user=") -or $lower.Contains("username="))
}

$rootPath = (Resolve-Path $Root).Path.TrimEnd("\", "/")
$failures = New-Object System.Collections.Generic.List[string]
$includedPrefixes = @(
    "services/Diten.AuthService/",
    "services/Diten.Platform/",
    "services/Diten.DevEnablementService/",
    "gateway/Diten.ApiGateway/",
    "frontend/Diten.Web/"
)
$files = Get-ChildItem -Path $rootPath -Filter appsettings.json -Recurse |
    Where-Object { $_.FullName -notmatch "\\(bin|obj|_Reference)\\" } |
    Where-Object {
        $relativeCandidate = $_.FullName.Substring($rootPath.Length).TrimStart("\", "/").Replace("\", "/")
        foreach ($prefix in $includedPrefixes) {
            if ($relativeCandidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
        return $false
    }

foreach ($file in $files) {
    $relative = $file.FullName.Substring($rootPath.Length).TrimStart("\", "/").Replace("\", "/")
    $json = Get-Content -Path $file.FullName -Raw | ConvertFrom-Json
    foreach ($entry in Flatten-JsonValue -Node $json) {
        if (-not (Test-SensitiveKey -Path $entry.Path)) {
            continue
        }

        if ($entry.Value -isnot [string]) {
            continue
        }

        $value = [string]$entry.Value
        if ([string]::IsNullOrWhiteSpace($value)) {
            continue
        }

        $allowKey = "$relative::$($entry.Path)"
        if ($allowlist.ContainsKey($allowKey) -and -not (Test-CredentialedConnectionString -Value $value)) {
            continue
        }

        $failures.Add("$relative :: $($entry.Path)")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Static secret scan failed. Suspicious values were found at these paths:"
    foreach ($failure in $failures) {
        Write-Host "- $failure"
    }
    exit 1
}

Write-Host "Static secret scan passed. Files scanned: $($files.Count)"
