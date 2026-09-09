# MOD-0012 Secrets & Configuration Vault Audit - 2026-05-12

## Scope

Implemented the approved `secrets-configuration-vault-golden-flow` for AuthService, Platform, DevEnablement, Gateway, and affected server-side frontend JWT/configuration paths.

## Changed File Summary

- Added shared library: `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets`.
- Added static scan tools: `scan_production_secrets.py` and `Scan-ProductionSecrets.ps1`.
- Wired startup secret validation and JWT previous-secret validation in AuthService, Platform, DevEnablement, Gateway, and Diten.Web.
- Removed committed production JWT signing secrets and internal API key placeholders from approved-scope `appsettings.json` files.
- Updated MFA challenge hashing to reject missing `Mfa:HashSecret` instead of falling back to the JWT signing secret.

## Secret Key Mapping

| Key name | Consuming service | Old read location | New provider/options location | Required rule | Validation proof |
|---|---|---|---|---|---|
| `JwtSettings:Secret` | AuthService | `Diten.AuthService.Infrastructure/DependencyInjection.cs` | `AddSecretsProvider` + `JwtSecretRotationResolver` | Required, min 32 | Build + startup validator |
| `JwtSettings:Secret` | Platform | `Diten.Platform.Infrastructure/DependencyInjection.cs` | `AddSecretsProvider` + `JwtSecretRotationResolver` | Required, min 32 | Build + startup validator |
| `JwtSettings:Secret` | DevEnablement | `Diten.DevEnablementService.Api/Program.cs` | `AddSecretsProvider` + `JwtSecretRotationResolver` | Required, min 32 | Build + startup validator |
| `JwtSettings:Secret` | Gateway | `Diten.ApiGateway/Program.cs` | `AddSecretsProvider` + `JwtSecretRotationResolver` | Required, min 32 | Build + startup validator |
| `JwtSettings:Secret` | Frontend server-side token bridge | `Diten.Web/Program.cs` | `AddSecretsProvider` + `JwtSecretRotationResolver` | Required, min 32 | Build + startup validator |
| `JwtSettings:PreviousSecrets` | All JWT validators | Not supported | `JwtSecretRotationResolver.GetValidationKeys()` | Optional collection, no duplicates/current match | Build + startup validator |
| `MongoDbSettings:ConnectionString` | AuthService | Persistence DI direct config bind | `ValidateRequiredSecrets` before Mongo client construction | Required where used | Build + static scan allowlist |
| `MongoDbSettings:ConnectionString` | Platform | Infrastructure DI direct config read | `ValidateRequiredSecrets` before Mongo client construction | Required where used | Build + static scan allowlist |
| `Mongo:ConnectionString` | DevEnablement | Persistence DI direct config read | `ValidateRequiredSecrets` before Mongo client construction | Required where used | Build + static scan allowlist |
| `ConnectionStrings:MongoDb` | Frontend server-side config | `Diten.Web/appsettings.json` | Static scan allowlisted credentialless local endpoint only | Optional if runtime still uses it | Static scan |
| `InternalEventAuth:ApiKey` | AuthService | Options bind only | `ValidateRequiredSecrets` | Required, min 24 | Build + startup validator |
| `AuthService:InternalApiKey` | Platform | Runtime check in invitation service | `ValidateRequiredSecrets` before service startup | Required, min 24 | Build + startup validator |
| `PlatformService:InternalApiKey` | AuthService | Options bind only | `ValidateRequiredSecrets` | Required, min 24 | Build + startup validator |
| `Mfa:HashSecret` | AuthService MFA challenges | `MfaChallengeService` fallback to JWT secret | `MfaOptions.HashSecret`; no fallback | Required when MFA enabled/used | Build + code inspection |
| `Smtp:Password` | AuthService/Platform | Runtime SMTP validation | `ValidateRequiredSecrets` when `Smtp:Enabled=true` | Optional only when SMTP disabled | Build + startup validator |

## Golden Flow Proof

- Shared provider contract exists: `ISecretsProvider.GetSecretAsync` and `GetSecretsAsync`.
- Startup validation is registered before JWT, MongoDB, SMTP/MFA consumers are used.
- JWT validation accepts current and configured previous secrets through `JwtSecretRotationResolver`.
- Production appsettings use empty sensitive values and rely on environment variables for required production secrets.
- Static scan passed and reports only file/key paths on failures.

## Failure Path Proof

- Missing required startup secrets throw `SecretValidationException`.
- Exception messages include key and service context only, not secret values.
- Weak placeholders such as `change-me`, `default-secret`, `password`, `test`, `admin`, and `123456` are rejected.
- MFA challenge hashing no longer uses `JwtSettings:Secret` as a fallback.

## Verification

| Command | Result |
|---|---|
| `dotnet build services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/Diten.BuildingBlocks.Security.Secrets.csproj -c Debug` | PASS |
| `powershell -NoProfile -ExecutionPolicy Bypass -File services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/tools/Scan-ProductionSecrets.ps1 .` | PASS |
| `dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug -p:OutDir=.tmp/build-auth/` | PASS |
| `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug -p:OutDir=.tmp/build-platform/` | PASS |
| `dotnet build services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj -c Debug -p:OutDir=.tmp/build-deven/` | PASS |
| `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug -p:OutDir=.tmp/build-gateway/` | PASS |
| `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug -p:OutDir=.tmp/build-web/` | PASS |
| `dotnet test services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets.Tests/Diten.BuildingBlocks.Security.Secrets.Tests.csproj -c Debug` | PASS, 5 tests |

Standard `bin/Debug` builds were initially blocked by running local service processes locking DLL/exe outputs; validation used isolated `OutDir` folders without stopping user processes.

## Remaining Gaps

- CI wiring for the static scan remains future work per module pack scope.
- Azure/AWS/HashiCorp adapters remain out of scope.

## Readiness Decision

`PASS` - runtime wiring, scan tooling, production config cleanup, shared library unit tests, and required builds pass.
