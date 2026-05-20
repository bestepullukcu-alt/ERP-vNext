# Secrets & Configuration Vault Technical Reference

Module: MOD-0012  
Scope: shared secrets foundation for AuthService, Platform, DevEnablement, Gateway, and Diten.Web

## Purpose

The secrets foundation removes committed production secrets, validates required runtime secret keys at startup, supports JWT previous-secret validation, and centralizes secret access through the shared secrets provider.

## Protected Secret Areas

- `JwtSettings:Secret`
- `JwtSettings:PreviousSecrets`
- MongoDB connection strings
- Internal API keys
- `Mfa:HashSecret`
- SMTP password when SMTP is enabled

## Runtime Rules

- Production secrets must come from environment/configuration providers, not committed values.
- Weak placeholders are rejected during validation.
- MFA challenge hashing must use `Mfa:HashSecret` and must not fall back to JWT signing secret.
- Secret validation errors must include key/context only, never secret values.

## Verification

Use the production secret scan tooling and service builds described in `docs/audits/pss-mod-0012-secrets-configuration-vault-audit-2026-05-12.md`.
