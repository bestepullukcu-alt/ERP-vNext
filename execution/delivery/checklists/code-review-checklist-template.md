# Code Review Checklist Template

## Purpose
This document provides a standardized template for code reviews. It lists verification checks that must be performed by human reviewers or AI agents before merging code into main branches.

## Scope
Applies to all code reviews for backend API, frontend Razor, and gateway routing pull requests.

## Not the source for
- Work package requirements (use Work Package Checklist).
- Release readiness decisions (use Release Gates).

## Current status
Placeholder.

## Source / migration note
New template referencing core architecture, security, localization, and testing rules.

## Owner / update rule
- Owner: Tech Lead / Architect
- Update Rule: Updated when code review findings or post-incident reviews identify gaps in the review process.

---

# Template: Code Review Checklist

Reviewers must verify that the pull request complies with the following domains:

### 1. Module Pack Acceptance Criteria (AC)
- [ ] All acceptance criteria defined in the specific Module Pack are fully implemented.
- [ ] All boundary conditions and edge cases in the AC are covered.

### 2. Security
- [ ] RBAC authorization checks present (`[HasPermission]` attribute applied where needed).
- [ ] Input data validated against XSS and SQL injection.
- [ ] Tenant boundaries are strictly checked (no cross-tenant data leaks).

### 3. Test Coverage
- [ ] Unit tests cover core business rules and handlers.
- [ ] Test names describe the expected behavior and follow project patterns.

### 4. Localization (L10n)
- [ ] All user-facing strings are extracted to `.resx` files.
- [ ] 7 standard languages (en, fr, es, zh, ar, ru, tr) are accounted for or stubbed.

### 5. Gateway Configuration
- [ ] Downstream and upstream routing in Ocelot gateway are correct.
- [ ] Ports align with the official port schema.

### 6. Audit Trail
- [ ] Entities inherit from audit-enabled base classes.
- [ ] State changes are correctly tracked in the audit logger.

### 7. Smoke Testing
- [ ] The application builds without errors.
- [ ] Local smoke tests verify that main paths work through the API Gateway (port 5000).
