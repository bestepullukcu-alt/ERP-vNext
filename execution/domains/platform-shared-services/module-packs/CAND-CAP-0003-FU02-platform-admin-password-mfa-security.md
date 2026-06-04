---
id: CAND-CAP-0003-FU02
name: Platform Admin Password & MFA Security
domain: platform-shared-services
status: draft
owner: module-pack-author
branch: feature/pss/pss-010-platform-admin-security
started: 2026-05-13
target: 2026-05-30
---

# CAND-CAP-0003-FU02 — Platform Admin Password & MFA Security

> **Canonicalization (DCP-002):** Governance identity is now **CAND-CAP-0003-FU02**, a child of **CAND-CAP-0003**. Prior repo ID **PSS-010** is a deprecated alias. Temporary candidate; pending EA. Ref: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`.

## Module Summary
Wave 2 delivers advanced personal security settings management for Platform Administrators. This enables authenticated administrators to change their password securely and enable or disable Multi-Factor Authentication (MFA) through a strict multi-stage verification flow (including session re-authentication, initial confirmation challenge, and recovery codes generation) aligning with high-security enterprise SaaS standards.

## Ownership and Boundaries
- **In-scope:** Logged-in Platform Admin security page and forms (`pages-account-settings-security.html` concept), current MFA status indicators, password change verification, and multi-stage MFA setup (password re-authentication, Email OTP confirmation, and recovery codes generation).
- **Out-of-scope:** Reset password flows (unauthenticated forgot-password triggers), MFA configuration for general Tenant users (managed separately), SSO provider linkages, and active SMS OTP gateway routing.
- **Gateway route changes:** Out-of-scope; existing Platform/Auth service catch-all gateway routes are used.

## Repo Scope
- `services/Diten.AuthService/**` (MFA status commands, MFA OTP verification, recovery code generator, and password hashing)
- `frontend/Diten.Web/Controllers/Platform/ProfileController.cs` (Security endpoints and forms)
- `frontend/Diten.Web/Views/Platform/Profile/**` (Limited to Security and Password settings views)
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Profile/**`
- `frontend/Diten.Web/Resources/Views/Platform/Profile/**`

## Protected Paths
- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN layout)
- `gateway/Diten.ApiGateway/**/ocelot.json`
- Other domain service folders (e.g., `services/Diten.DevEnablementService/**`)
- `frontend/Diten.Web/Views/Shared/_LayoutPlatformAdmin.cshtml` (PROHIBITED to make structural or design changes. ONLY adding the nav links to profile under authorization check is allowed in `/Views/Shared/_LayoutPlatformAdmin.cshtml`)

## Dependencies
- `Diten.AuthService` backend user security store and password hashing services.
- `_LayoutPlatformAdmin.cshtml` for rendering the administrator control panel menu and theme assets.
- Premium Alert & Modal Standard (MOD-0013) for all validation and confirmation popups.

## Runtime Constraints
- A Platform Administrator can only view or update **their own** security parameters. The controller and backend handler must resolve the identity solely from the JWT sub (User ID) claim.
- Layout must be strictly **`_LayoutPlatformAdmin.cshtml`**.
- Metin yerelleştirmeleri (L10n) için sadece **`en`** ve **`tr`** dilleri zorunludur.
- **Rate-limiting Implementation:**
  - Rate-limiting is enforced at the Web gateway layer (`Diten.Web`) utilizing the **.NET 8 native `Microsoft.AspNetCore.RateLimiting` middleware**.
  - Configure a dedicated sliding window policy in `Program.cs` that restricts security state mutations, password modification attempts, and MFA toggles to **5 requests per minute** partitioned by the authenticated User ID.
- **MFA Enrolment Security Flow Specifications:**
  - **Password Re-check:** To change password or modify MFA status, the user must re-enter and authenticate their current password first.
  - **Confirmation OTP Gate:** MFA cannot be enabled by simply flipping a switch. AuthService must generate a temporary Email OTP challenge; enabling is locked in the database until this specific challenge is successfully verified.
  - **Recovery Codes:** On successful MFA enablement, AuthService must generate 8 unique recovery codes (8-character alphanumeric). These are displayed exactly once to the user to copy/print.
  - **Audit Logging:** Every password change, MFA toggle, or recovery codes regeneration must record a secure event in MongoDB `audit_logs` containing the actor ID.

## Acceptance Criteria
- [ ] `GET /platform/profile/security` renders the password modification and MFA management sections aligning with `pages-account-settings-security.html` concept.
- [ ] **Secure Password Change:** Requires verifying current password before accepting the update. New password strength is validated against backend rules (length, upper/lower, special characters).
- [ ] **Secure MFA Setup Flow:**
  - [ ] **Re-authentication:** Prompt current password verification.
  - [ ] **OTP Challenge:** Generate and send confirmation Email OTP.
  - [ ] **MFA Confirmation:** Flip MFA status in database only after confirming the Email OTP.
  - [ ] **Recovery Codes:** Generate 8 dynamic recovery codes and render them in a secure copyable modal.
- [ ] MFA toggle and password modification endpoints are rate-limited to **5 attempts per minute** partitioned by User ID utilizing the ASP.NET Core .NET 8 native `RateLimiting` middleware.
- [ ] Every security transaction creates a corresponding audit log record.
- [ ] Confirmation and error modals strictly follow MOD-0013 Premium Alert style rules.

## Test Expectations
- Unit tests cover password criteria verification, password change validation commands, and secure recovery codes generation.
- Integration tests cover the secure MFA flow: password re-check success/failure, OTP challenge creation, OTP validation, and recovery codes generation integrity.
- Integration tests cover the configured .NET 8 Rate Limiter middleware policy on security mutation endpoints.
- Auth service and Web frontend build pass without errors.
