---
id: MOD-0012
name: Secrets & Configuration Vault / Secrets Management Foundation
domain: platform-shared-services
status: review
owner: module-pack-author
branch: feature/pss/mod-0012-secrets-configuration-vault
started: 2026-05-12
target: 2026-05-26
form_field_count: 0
golden_reference: secrets-configuration-vault-golden-flow
---

# MOD-0012 - Secrets & Configuration Vault / Secrets Management Foundation

## Module Summary

`NEW-001 Secrets Management` in `docs/platform/master-plan.md` is not a separate domain or standalone module. It is the blocker execution scope for this existing Platform & Shared Services module pack: `MOD-0012 Secrets & Configuration Vault`.

This module provides the platform-wide foundation for reading sensitive runtime configuration through one provider-agnostic seam. The first implementation must remove hardcoded runtime secrets from production `appsettings.json` files and standardize how AuthService, Platform, DevEnablement, Gateway, and any affected frontend configuration paths handle JWT secrets, MongoDB connection strings, internal API keys, SMTP passwords, MFA hash secrets, and future provider credentials.

Planned wave: `W1-*` blocker foundation. This pack is ready for development because the MVP scope, provider precedence, runtime flow, failure path, repo scope, acceptance criteria, validation expectations, and output contract are now decision-complete.

## Golden Flow

1. Developer configures required secrets through environment variables or `dotnet user-secrets`.
2. AuthService, Platform, DevEnablement, Gateway, and affected frontend configuration paths start.
3. `ISecretsProvider` resolves required secrets through the documented provider precedence.
4. Boot-time validation runs before JWT, MongoDB, SMTP, MFA, and internal API clients consume values.
5. Applications start successfully when all required secrets are valid.
6. Logs, audit payloads, exceptions, Swagger, HTTP responses, UI, and browser-facing frontend payloads do not expose plaintext secret values.
7. Static secret scan confirms production `appsettings.json` files contain no real secrets.

## Failure Path To Verify

1. A required production secret is missing or invalid.
2. Application startup fails fast with a controlled, non-secret-revealing error.
3. No fallback value such as `change-me`, `default-secret`, or `Secret ?? "change-me"` is used.
4. No partial service startup occurs for the affected application.
5. No plaintext secret is logged, returned, rendered, written to audit, or exposed through Swagger.
6. A suspicious real secret committed into production `appsettings.json` causes the static secret scan to fail the quality gate.
7. Any attempt to expose a JWT signing secret to browser-facing code, JavaScript, Razor output, API responses, Swagger examples, or frontend configuration payloads is blocked and documented as a security violation.

## Ownership and Boundaries

### Owned Objects

- `ISecretsProvider`
- `SecretsProviderOptions`
- `RequiredSecretDefinition`
- `SecretValidationResult`
- `SecretRotationPolicy`
- `SecretMetadata`
- `SecretRedactionPolicy`

### In Scope

- Provider-agnostic secret read contract.
- Local/development secret source support through `appsettings.Development.json`, `dotnet user-secrets`, and environment variables.
- Production secret resolution through environment variables only in MVP.
- Boot-time required secret validation.
- Repo-level inventory and replacement of direct secret reads.
- Secret key catalog and naming conventions for current services.
- Redaction rules for logs, audit payloads, exceptions, responses, Swagger, UI, and frontend payloads.
- Runnable local static secret scan script or test.
- JWT current + previous secret rotation model.
- Final implementation report with proof of golden flow, failure path, scan results, and frontend secret safety.

### Out Of Scope

- External Azure Key Vault adapter implementation.
- External AWS Secrets Manager adapter implementation.
- External HashiCorp Vault adapter implementation.
- Full admin UI for secret management.
- Plaintext secret display in any UI.
- Provider-specific rotation automation.
- CI/CD pipeline wiring for the scan.
- Gateway route changes.
- Changes to `gateway/Diten.ApiGateway/**/ocelot.json`.
- Committing real production secret values to the repository.
- Changing `.antigravity/**` standards or global agent rules.

## Repo Scope

Allowed implementation scope:

- `services/Diten.Platform/**`
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `gateway/Diten.ApiGateway/**`
- `frontend/Diten.Web/**` only if existing JWT/config usage requires runtime secret-read alignment or frontend secret exposure prevention.
- `execution/domains/platform-shared-services/module-packs/MOD-0012-secrets-configuration-vault.md`
- `docs/audits/**` for completion/audit evidence.

## Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- Other domain service internals not listed in the approved repo scope.

## Dependencies

### Upstream Dependencies

- None. This is a zero-order platform blocker.

### Downstream Consumers

- `NEW-002 Platform Administrators Mgmt`
- `MOD-0026 Background Job Scheduler`
- `MOD-0027 Notification / Email Service`
- `MOD-0034 Webhook Delivery`
- `MOD-0035 Event Bus / Internal Events`
- `MOD-0263 External Messaging Provider`
- `MOD-0266 Blob / File Storage Provider`
- Any module that needs API keys, passwords, signing keys, connection strings, provider credentials, or service-to-service tokens.

## Runtime Constraints

- MVP must not introduce an external vault provider. Azure Key Vault, AWS Secrets Manager, and HashiCorp Vault remain future-state adapters behind the same contracts.
- Production secret resolution must use environment variables only.
- Production `appsettings.json` files must contain no real `Secret`, `ApiKey`, `Password`, `HashSecret`, `Token`, or credentialed `ConnectionString` values.
- Required secrets must fail fast at application startup when missing or invalid.
- Silent fallback values are forbidden for required secrets.
- Optional secrets are allowed only when the owning feature is disabled.
- Secret values must never be written to logs, audit payloads, exception messages, HTTP responses, browser-rendered UI, Razor output, JavaScript, Swagger examples, frontend configuration payloads, or seed data.
- The secret seam must be replaceable without changing downstream module contracts.
- Frontend calls must continue to use Gateway port `5000`; this module does not relax the gateway rule.

## Provider Precedence

| Environment | Priority | Provider | Rule |
|---|---:|---|---|
| Development | 1 | Environment variables | Highest priority. Use .NET `__` mapping for nested keys. |
| Development | 2 | `dotnet user-secrets` | Preferred local developer secret store. |
| Development | 3 | `appsettings.Development.json` | Local-only placeholders only; no production secret values. |
| Production | 1 | Environment variables | Only allowed MVP production provider. |

Provider rules:

- Production `appsettings.json` must contain no real `Secret`, `ApiKey`, `Password`, `HashSecret`, `Token`, or credentialed `ConnectionString` values.
- Required secrets must not use silent fallback values.
- Optional secrets are valid only when their owning feature is disabled and this disabled-feature behavior is tested.

## Provider Contract

The implementation must expose a provider-agnostic contract equivalent to:

```csharp
public interface ISecretsProvider
{
    Task<string> GetSecretAsync(string key, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> GetSecretsAsync(string prefix, CancellationToken ct);
}
```

Registration must include an extension equivalent to:

```csharp
services.AddSecretsProvider(configuration, environment);
```

Startup must include boot-time validation for required secrets before dependent services consume them.

## Shared Library Placement

Common secret contracts and options must live in a shared building-block library so AuthService, Platform, DevEnablement, Gateway, and any server-side frontend code consume one implementation contract instead of duplicating local helper classes.

Preferred placement:

- `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets`

Acceptable fallback if the repository's building-block naming is already configuration-oriented at implementation time:

- `services/Diten.Building.Blocks/Diten.BuildingBlocks.Configuration.Secrets`

The shared library must own:

### Contracts

- `ISecretsProvider`
- `ISecretRequirementValidator`
- `ISecretRedactor`
- `ISecretRotationResolver`

### Options

- `SecretsProviderOptions`
- `RequiredSecretDefinition`
- `JwtSecretRotationOptions`
- `SecretRedactionOptions`

Rule: service projects must reference/consume the shared library and must not create service-local duplicate secret helper classes.

## Startup Wiring Rules

- Secret validation must run during application startup before JWT, MongoDB, SMTP, MFA, and internal API clients are registered or used.
- Startup wiring must avoid async deadlocks in `Program.cs` and DI registration.
- Because the MVP provider is configuration-backed, implementation should prefer synchronous configuration resolution behind startup validation while keeping `ISecretsProvider` provider-agnostic for future external providers.
- Startup failure must throw a controlled exception type such as `SecretValidationException`.
- `SecretValidationException` messages must include the missing/invalid key name and service context, but must never include secret values.
- Services must not partially start when a required startup secret fails validation.

## Environment Variable Naming Standard

Environment variables use .NET double-underscore mapping for nested configuration keys.

| Secret key | Environment variable |
|---|---|
| `JwtSettings:Secret` | `JwtSettings__Secret` |
| `JwtSettings:PreviousSecrets:0` | `JwtSettings__PreviousSecrets__0` |
| `MongoDbSettings:ConnectionString` | `MongoDbSettings__ConnectionString` |
| `Mongo:ConnectionString` | `Mongo__ConnectionString` |
| `ConnectionStrings:MongoDb` | `ConnectionStrings__MongoDb` |
| `AuthService:InternalApiKey` | `AuthService__InternalApiKey` |
| `PlatformService:InternalApiKey` | `PlatformService__InternalApiKey` |
| `InternalEventAuth:ApiKey` | `InternalEventAuth__ApiKey` |
| `Mfa:HashSecret` | `Mfa__HashSecret` |
| `Smtp:Password` | `Smtp__Password` |

## Minimum Secret Validation Rules

- Required secrets must be non-empty after `Trim()`.
- Production placeholders are always forbidden in `appsettings.json`, environment variables, and any production secret source.
- Development local-only placeholders are allowed only in `appsettings.Development.json` when the related feature is disabled or the value is explicitly marked non-production.
- Development environment variables and `dotnet user-secrets` must still reject known weak placeholders for required secrets.
- Known weak placeholders include: `change-me`, `default-secret`, `local-dev-secret`, `password`, `test`, `admin`, `123456`.
- JWT current secret must be at least 32 characters.
- Internal API keys must be at least 24 characters.
- Connection strings must not be blank and must never be logged.
- Previous JWT secrets must not contain the same value as the current secret.
- Duplicate previous JWT secrets are invalid.
- Optional secrets are valid only when the owning feature is disabled.
- `Mfa:HashSecret` is required when MFA challenge hashing is enabled.
- Falling back from `Mfa:HashSecret` to `JwtSettings:Secret` is forbidden.
- If MFA is disabled, `Mfa:HashSecret` may be absent.
- Missing `Mfa:HashSecret` while MFA is enabled must fail startup safely.

## Repo-Level Secret Mapping Task

Implementation must inventory and replace direct secret reads in:

- AuthService `Program.cs`, options binding, JWT setup, internal event auth, SMTP, MFA, and PlatformService internal client setup.
- Platform `Program.cs`, infrastructure DI, MongoDB setup, JWT setup, SMTP, and AuthService internal API client setup.
- DevEnablement `Program.cs`, JWT setup, and Mongo connection setup.
- Gateway JWT validation setup.
- Frontend config usage if it currently reads JWT/config secrets.

The final implementation report must include a table with:

| Field | Required Content |
|---|---|
| Key name | Example: `JwtSettings:Secret`. |
| Consuming service | AuthService, Platform, DevEnablement, Gateway, or Frontend. |
| Old read location | File/class/method where the value was read before. |
| New provider/options location | New `ISecretsProvider` or validated-options path. |
| Required/optional rule | Required, optional, or optional only when feature disabled. |
| Validation test name | Unit/integration/static test proving the behavior. |

## Secret Key Catalog

The first implementation must centralize or validate these keys:

| Key | Current/Future Consumer | Requirement |
|---|---|---|
| `JwtSettings:Secret` | Gateway, AuthService, Platform, DevEnablement | Required; supports current + previous rotation model. Must not be exposed to frontend/browser code. |
| `MongoDbSettings:ConnectionString` | AuthService, Platform | Required where used. |
| `Mongo:ConnectionString` | DevEnablement | Required where used. |
| `ConnectionStrings:MongoDb` | Frontend/current web config usage | Required only if runtime code still depends on it; must not be browser-facing if credentialed. |
| `AuthService:InternalApiKey` | Platform -> AuthService internal calls | Required when internal provisioning calls are enabled. |
| `PlatformService:InternalApiKey` | AuthService -> Platform internal calls | Required when internal platform calls are enabled. |
| `InternalEventAuth:ApiKey` | AuthService internal event endpoints | Required when internal events are enabled. |
| `Mfa:HashSecret` | AuthService MFA challenge hashing | Required when MFA challenge hashing is enabled. Fallback to `JwtSettings:Secret` is forbidden. May be absent only when MFA is disabled. |
| `Smtp:Password` | AuthService/Platform email flows, future MOD-0027 | Required only when SMTP sending is enabled. |
| Future storage provider credentials | MOD-0266 | Future consumer of `ISecretsProvider`; no custom helper. |
| Future webhook signing keys | MOD-0034 | Future consumer of `ISecretsProvider`; supports rotation. |

## Frontend Secret Safety

- JWT signing secrets must never be available to browser-rendered code, JavaScript, Razor output, API responses, Swagger examples, or frontend configuration payloads.
- Frontend may receive only non-secret public configuration such as gateway URL, public API base URL, feature flags, display options, and localization payloads.
- If frontend server-side code needs a secret during startup, it must consume it server-side only through `ISecretsProvider` or validated options and must never serialize it to the browser.
- Any discovered frontend exposure path for `JwtSettings:Secret`, API keys, token signing keys, passwords, or credentialed connection strings is a security violation and must block readiness until removed.

## JWT Rotation Model

- Token signing uses only the current configured JWT secret.
- Token validation accepts the current secret and an explicitly configured previous secret list during a bounded rotation window.
- Previous secrets must be named and configured as a collection, for example `JwtSettings:PreviousSecrets`.
- Previous secrets must not be logged, audited as plaintext, rendered, returned in diagnostics, or sent to frontend payloads.
- Removing a previous secret immediately invalidates tokens signed with that secret after deployment.

## Static Secret Scan

Implementation must provide a runnable local script or test that scans production `appsettings.json` files.

The scan must fail on suspicious non-empty values under keys containing:

- `Secret`
- `ApiKey`
- `Password`
- `HashSecret`
- `Token`
- `ConnectionString`

Allowed values:

- Empty strings in production appsettings.
- Non-secret values explicitly known to be public only when the key path is allowlisted.
- Documented local-only placeholders only outside production appsettings.

Controlled exception policy:

- Static scan may allow known credentialless/public values only when the key path is explicitly allowlisted.
- Allowlist entries must include reason, owner, and expiry/review date.
- Credentialed production connection strings are always treated as secrets and must not exist in production appsettings.
- Scan failures must report file path and key path only, not the suspicious value.
- The scan must never print the suspicious value in failure output.

## Owned Objects

### Contracts / Services

- `ISecretsProvider`
- `ISecretRequirementValidator`
- `ISecretRedactor`
- `ISecretRotationResolver`

### Configuration / Options

- `SecretsProviderOptions`
- `RequiredSecretDefinition`
- `JwtSecretRotationOptions`
- `SecretRedactionOptions`

### Quality Gate / Support

- Static secret scan script or test.
- Audit evidence report under `docs/audits/**`.

## Entity Fields

This MVP is primarily runtime configuration infrastructure and does not require a MongoDB entity.

If future admin metadata is implemented, `SecretMetadata` may be introduced as a platform-level global record, never storing plaintext secret values:

| Field | Type | Rule |
|---|---|---|
| `Id` | `Guid` | System generated. |
| `Key` | `string` | Required; unique secret key name. |
| `DisplayName` | `string` | Required; non-secret label. |
| `Description` | `string?` | Optional; must not include plaintext values. |
| `Provider` | `string` | Required; examples: `Environment`, `UserSecrets`, `AzureKeyVault`. |
| `IsRequired` | `bool` | Required. |
| `LastRotatedAtUtc` | `DateTimeOffset?` | Optional metadata only. |
| `RotationPolicyName` | `string?` | Optional metadata only. |
| `IsDeleted` | `bool` | Required if persisted. |
| `DeletedAt` | `DateTimeOffset?` | Required when soft-deleted if persisted. |

Plaintext secret values must not be persisted in MongoDB by this MVP.

## Acceptance Criteria

### Runtime Criteria

- [ ] AuthService, Platform, DevEnablement, Gateway, and affected frontend server-side configuration paths boot when required secrets are supplied through allowed providers.
- [ ] AuthService, Platform, DevEnablement, Gateway, and affected frontend server-side configuration paths fail fast when required secrets are missing.
- [ ] Boot-time validation runs before JWT, MongoDB, SMTP, MFA, and internal API clients consume secret values.
- [ ] Development provider precedence resolves environment variables, then `dotnet user-secrets`, then `appsettings.Development.json` local-only placeholders.
- [ ] Production provider precedence resolves environment variables only.
- [ ] JWT current + previous secret validation works.
- [ ] MFA enabled + missing `Mfa:HashSecret` fails startup safely.
- [ ] MFA disabled + missing `Mfa:HashSecret` is accepted.

### Integrity Criteria

- [ ] Production `appsettings.json` files contain no real secrets.
- [ ] No silent fallback exists for required secrets.
- [ ] Optional secrets are accepted only when the owning feature is disabled.
- [ ] Required secret using a known weak placeholder in environment variables or `dotnet user-secrets` fails validation.
- [ ] Secret values are redacted everywhere.
- [ ] Downstream modules consume `ISecretsProvider` or validated options, not module-local custom secret helpers.
- [ ] The final report includes the required secret key mapping table.
- [ ] No changes are made to `gateway/Diten.ApiGateway/**/ocelot.json`.

### Security Criteria

- [ ] No plaintext secret appears in logs, audit payloads, exception messages, HTTP responses, Swagger examples, UI, seed data, Razor output, JavaScript, or browser-facing frontend payloads.
- [ ] Frontend JWT signing secret exposure is blocked.
- [ ] Static secret scan fails on committed real-looking secrets.
- [ ] Static secret scan output does not print secret values.
- [ ] Static secret scan allowlist entries require owner, reason, and review date.
- [ ] Any frontend/browser-facing secret exposure path is documented as a security violation and removed.

## Test Expectations

### Static / Quality Gate

- Run the static secret scan locally and include the command/result in the final report.
- Scan production `appsettings.json` files for suspicious non-empty values under keys containing `Secret`, `ApiKey`, `Password`, `HashSecret`, `Token`, or `ConnectionString`.
- Allow empty values and documented local-only placeholders only outside production appsettings.
- Allow credentialless/public values only through explicit key-path allowlist entries that include owner, reason, and review date.
- Treat credentialed production connection strings as secrets even when the key is allowlisted.
- Fail when a real-looking secret is committed to production appsettings.
- Report only file path and key path on failure; never print the suspicious value.

### Unit Tests

- Missing required secret returns/throws a controlled startup validation failure.
- Provider precedence resolves environment variables before lower-priority development sources.
- Production provider precedence ignores development-only sources.
- Required secret using a known weak placeholder in environment variables or user-secrets is rejected.
- MFA enabled with missing `Mfa:HashSecret` fails startup safely.
- MFA disabled with missing `Mfa:HashSecret` is accepted.
- Redaction masks configured sensitive keys and values.
- JWT rotation resolver returns current and previous validation keys without exposing plaintext in logs or diagnostics.
- Optional secrets are allowed only when the owning feature is disabled.

Suggested test names:

- `SecretsProvider_Precedence_EnvironmentWins_InDevelopment`
- `SecretsProvider_Production_IgnoresDevelopmentSources`
- `SecretRequirementValidator_MissingRequiredSecret_FailsStartupSafely`
- `SecretRequirementValidator_PlaceholderSecret_IsRejected`
- `SecretRequirementValidator_RequiredPlaceholderInUserSecrets_IsRejected`
- `SecretRequirementValidator_MfaEnabledMissingHashSecret_FailsStartupSafely`
- `SecretRequirementValidator_MfaDisabledMissingHashSecret_IsAccepted`
- `JwtSecretRotationResolver_CurrentAndPreviousSecrets_AreAcceptedForValidation`
- `JwtSecretRotationResolver_DuplicatePreviousSecret_IsRejected`
- `SecretRedactor_MasksSensitiveKeys_InLogsAndErrors`
- `StaticSecretScan_ProductionAppsettings_WithSuspiciousSecret_Fails`
- `StaticSecretScan_AllowlistedPublicValue_RequiresOwnerReasonAndReviewDate`
- `FrontendConfig_DoesNotExposeJwtSigningSecret`

### Integration / Smoke Tests

- AuthService boots with required secrets provided through environment variables or user-secrets.
- Platform boots with required secrets provided through environment variables or user-secrets.
- DevEnablement boots with required secrets provided through environment variables or user-secrets.
- Gateway boots and validates JWT configuration through the same secret source policy.
- Failure smoke proves a missing production secret blocks startup safely.
- Frontend build and any affected server-side config path pass without exposing JWT signing secrets or other plaintext secrets.

### Build Expectations

```bash
dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet build services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
```

## Final Implementation Report Contract

The final implementation report must include:

- Changed-file summary.
- Secret key mapping table.
- Golden flow proof.
- Failure path proof.
- Build results.
- Unit/integration/static scan results.
- Redaction proof without exposing secret values.
- Frontend secret exposure check.
- Shared library path used.
- Startup wiring summary per service.
- Environment variable mapping used.
- Validation rules implemented.
- Test names and results.
- Remaining gaps / follow-up items.
- Readiness decision: `PASS`, `PARTIAL`, or `BLOCKED`.

## Implementation Notes

- Start with the local/environment abstraction. Do not implement Azure/AWS/HashiCorp providers in the MVP.
- Keep the contract small and stable so future provider adapters can be added without touching downstream modules.
- Prefer ASP.NET Core configuration conventions where possible, including `__` environment variable mapping for nested keys.
- Treat connection strings as secrets when they include credentials or production endpoints.
- Do not expose secret plaintext through admin UI. Future UI may show metadata, validation status, provider name, and rotation status only.
- Existing modules should consume `ISecretsProvider` or validated configuration populated from it, not new module-specific secret helpers.
- Any audit report must prove behavior without including secret values.

### 2026-05-12 Implementation Pass

- Shared secret contracts and MVP implementation were added under `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets`.
- AuthService, Platform, DevEnablement, Gateway, and server-side frontend JWT validation now consume the shared validation/rotation seam.
- Production `appsettings.json` files in the approved scope no longer contain committed JWT signing secrets or internal API key placeholders.
- MFA challenge hashing no longer falls back to `JwtSettings:Secret`; missing `Mfa:HashSecret` fails when MFA challenge hashing is used.
- Static production secret scan tooling was added under the shared library tools folder and verified without printing secret values.
- Shared-library unit tests cover missing required secrets, forbidden placeholders, disabled MFA missing hash secret, duplicate previous JWT secrets, and redaction.
- Gateway `ocelot.json` was not changed.

## Follow-up Items

- Add Azure Key Vault adapter behind `ISecretsProvider`.
- Add AWS Secrets Manager adapter behind `ISecretsProvider`.
- Add HashiCorp Vault adapter behind `ISecretsProvider`.
- Add thin metadata-only admin UI after RBAC/audit dependencies are ready.
- Add provider-specific secret rotation automation.
- Add CI/CD integration for the static secret scan once the pipeline location is finalized.

## Open Questions / Risks

- The exact CI/CD system is not defined in this pack; the first implementation should provide a runnable local/static check and document how it will be wired into CI later.
- Some current services read secrets during `Program.cs`/DI startup; implementation must choose the minimal wiring that supports startup-time secret resolution without async deadlock or hidden fallback.
- Frontend JWT secret usage should be reviewed carefully because browser-facing code must never receive signing secrets.
