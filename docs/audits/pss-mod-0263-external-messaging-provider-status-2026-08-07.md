# MOD-0263 External Messaging Provider Status - 2026-08-07

## Summary

MOD-0263 SMTP/MailKit Batch 1 implementation evidence is present and focused validation passed locally. The module is not marked done because the manual live SMTP smoke remains pending approved local SMTP catcher, tenant settings, secret-reference setup, dispatch evidence, and cleanup/retention plan.

Current status: `review / pending-live-smoke`.

## Code Evidence

SMTP/MailKit Batch 1 implementation evidence recorded:

- `SmtpMessagingProvider`
- `MailKitSmtpClientFactory`
- `SmtpProviderOptions` and validator
- `SecretReferenceResolver`
- `MessagingProviderErrorMapper`
- DI registration for `SmtpMessagingProvider`
- MailKit package reference
- mocked-transport SMTP tests

`FakeMessagingProvider` remains registered and production-blocked where applicable.

## Validation

- SMTP provider focused tests: PASS, `22/22`.
- MOD-0027 Batch 1 / dispatch adapter regression slice: PASS, `32/32`.
- Platform API build: PASS, `0 warnings`, `0 errors`.
- `git diff --check`: PASS.
- Repository status after validation: clean.

The SMTP tests used mocked transport and did not require a real SMTP server or external network.

## Explicit Exclusions

No real SMTP server, external network dispatch, secrets, appsettings edits, fixture data, seeds, migrations, frontend, Gateway, AuthService, or raw data changes were used.

No SMTP fixtures, dispatches, templates, tenant messaging settings, or secret values were created.

## Remaining Gate

Live SMTP smoke remains pending. Before MOD-0263 can be marked done, an approved smoke must prove:

- local SMTP catcher receives the email;
- the dispatch reaches `Sent`;
- broken credentials produce controlled `Failed`;
- logs and dispatch error metadata do not leak password, body, recipient dump, or raw provider response;
- fixture/config setup and cleanup/retention are explicitly approved.
