# Tenant Architecture Phase Gates (Faz 1)

## Sprint 1-2 Gate
- [ ] Tenant context contracts (`ITenantContext`, `TenantScope`, `TenantGuard`) available in Auth/Platform/MDM.
- [ ] Gateway and service tenant resolution priority aligned (JWT > Header > Subdomain).
- [ ] Missing tenant on protected path -> `400`.

## Sprint 3-4 Gate
- [ ] Identity core extension entities available (`PlatformUser`, `TenantUserMembership`).
- [ ] Event envelope contract in place.
- [ ] Outbox skeleton includes retry/failure state.
- [ ] Build-time schema files committed under `events/schemas`.

## Sprint 5-6 Gate
- [ ] Authorization cache contract key/version/ttl implemented.
- [ ] Fail-closed policy documented and enforced in integration points.

## Sprint 7-8 Gate
- [ ] Cross-service DB isolation checks prepared.
- [ ] Architecture + tenancy test gates integrated into CI pipeline.
- [ ] Release checklist executed before closure.
