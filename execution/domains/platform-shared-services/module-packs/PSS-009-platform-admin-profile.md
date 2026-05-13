---
id: PSS-009
name: Platform Admin Profile & Personal Feed
domain: platform-shared-services
status: approved
owner: module-pack-author
branch: feature/pss/pss-009-platform-admin-profile
started: 2026-05-13
target: 2026-05-30
---

# PSS-009 — Platform Admin Profile & Personal Feed

## Module Summary
Wave 2 delivers personal profile management and personal activity stream auditing for Platform Administrators. This enables authenticated administrators to view their profile, edit contact details, update language/timezone preferences, and upload profile pictures (avatars) using a secure local-to-object abstraction layer.

## Ownership and Boundaries
- **In-scope:** Logged-in Platform Admin profile view (`pages-profile-user.html` design), details update form (`pages-account-settings-account.html` design), avatar upload/removal with localized file lifecycle, and personal activity audit logs timeline query.
- **Out-of-scope:** Editing other administrator profiles (managed globally under Administrators module), Password change or MFA configuration (managed separately under PSS-010), Tenant-side user profiles, and SSO provider configurations.
- **Gateway route changes:** Out-of-scope; existing Platform/Auth service catch-all gateway routes are used.

## Repo Scope
- `services/Diten.AuthService/**` (Application commands, validators, audit logging, and user identity profile fields)
- `frontend/Diten.Web/Controllers/Platform/ProfileController.cs`
- `frontend/Diten.Web/Views/Platform/Profile/**` (Limited to Profile and Account Details views)
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
- `Diten.AuthService` backend user store and profile persistence.
- `_LayoutPlatformAdmin.cshtml` for rendering the administrator control panel menu and theme assets.
- Premium Alert & Modal Standard (MOD-0013) for all validation and confirmation popups.

## Runtime Constraints
- A Platform Administrator can only view or update **their own** profile. The controller and backend handler must resolve the identity solely from the JWT sub (User ID) claim.
- Layout must be strictly **`_LayoutPlatformAdmin.cshtml`**.
- Metin yerelleştirmeleri (L10n) için sadece **`en`** ve **`tr`** dilleri zorunludur.
- **Avatar Storage & Lifecycle Specifications:**
  - **MVP vs. Production Strategy:** The avatar upload engine must depend on an interface abstraction (`IAvatarStorageProvider`). The MVP implementation will use local physical storage (saving files under `frontend/Diten.Web/wwwroot/uploads/avatars/`), but the architecture must remain decoupled so that production-ready migration to S3/Azure Blob Object Storage requires zero changes to application controllers or core services.
  - **Database Reference:** MongoDB `users` collection stores `AvatarUrl` string as public reference (e.g., `/uploads/avatars/{userId}_{timestamp}.png`).
  - **Naming Convention:** `{userId}_{timestamp}.{extension}` (Strictly sanitized to prevent path traversal).
  - **Cleanup Policy:** Any new avatar upload or avatar deletion request (`POST /platform/profile/remove-avatar`) must physically delete the old avatar file from disk before persisting changes.
- **Audit Logging:** Every profile detail update and avatar change must generate an Audit Log entry in MongoDB `audit_logs` collection with `ActorUserId == currentUserId`.
- **Rate-limiting Implementation:**
  - Rate-limiting is enforced at the Web gateway layer (`Diten.Web`) utilizing the **.NET 8 native `Microsoft.AspNetCore.RateLimiting` middleware**.
  - Configure a dedicated sliding window policy in `Program.cs` that restricts profile updates and avatar uploads to **5 requests per minute** partitioned by the authenticated User ID.

## Acceptance Criteria
- [ ] `GET /platform/profile` displays User Profile page with a beautiful header card including cover background, circular avatar, name, and "Platform Administrator" badge.
- [ ] Profile Details tab lists static read-only contact details (email, phone), status, and active platform roles.
- [ ] **Audit Trail Integration:** Profile Activity Feed queries `audit_logs` collection filtering by `ActorUserId == currentUserId` and displays the last 10 personal actions ordered by `TimestampDesc` (e.g., `Login`, `Logout`, `ProfileUpdate`, `AvatarChange`).
- [ ] `GET /platform/profile/settings` renders the Edit Profile form aligning with `pages-account-settings-account.html`.
- [ ] Avatar block supports file upload, validates files (max 2MB, MIME types `image/png`, `image/jpeg`), generates client-side preview, and cleans up physical disk storage during update or reset.
- [ ] Profile Details Form allows updating: First Name, Last Name, Phone Number, and Language Preference (EN/TR).
- [ ] Profile update and avatar upload endpoints are soldered behind the configured .NET 8 User-ID-partitioned rate-limiting policy (5 req/min).
- [ ] Adding Profile navigation to `_LayoutPlatformAdmin.cshtml` is limited ONLY to the dropdown menu profile and settings links.
- [ ] Confirmation and error modals strictly follow MOD-0013 Premium Alert style rules.

## Test Expectations
- Unit tests cover profile detail updates, physical avatar disk cleanup, and security identity isolation (tampering with User ID parameters must throw authorization exceptions).
- Integration tests cover the .NET 8 Rate Limiter middleware policy on profile endpoints, ensuring the 6th request within 1 minute returns a `429 Too Many Requests` status code.
- Frontend Javascript code passes static verification.
- Auth service and Web frontend build pass without errors.
