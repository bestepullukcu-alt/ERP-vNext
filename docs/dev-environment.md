# Local development environment

Measured notes for running the stack locally. Written because each of these has already cost someone a debugging
session that ended in "the feature is broken" when the feature was fine and the environment was not.

There was no dev-setup document in this repository before 2026-08-11; this is a starting point, not a complete
one. Add what you hit.

## Mail — Mailpit

Dev SMTP is Mailpit on `localhost:1025`, with its web UI on `http://localhost:8025`. `appsettings.Development.json`
points `Smtp` there.

```bash
brew services start mailpit
```

**Start it so it accepts the dev credentials.** `appsettings.Development.json` sends a dummy username/password,
and Mailpit announces no AUTH by default — MailKit then throws `NotSupportedException` before anything is sent:

```
smtp.provider.send.failure    ExceptionType="NotSupportedException"
notification.dispatch_failed  ReasonCode="PROVIDER_REJECTED" Status=400
```

That is an environment mismatch, not a product defect. Run Mailpit with:

```bash
mailpit --smtp-auth-accept-any --smtp-auth-allow-insecure
```

(If Mailpit runs as a brew service, put those flags in its service definition, or run it in the foreground while
testing notifications.)

Measured consequence when this is wrong: the notification is refused at the transport, and any scheduled job that
treats "attempted" as "delivered" loses the work. BL-065 hardened the due-soon sweep against exactly this — the
claim is now released and retried — but the emails still will not arrive until Mailpit accepts the credentials.

## Eventing — RabbitMQ

Cross-service work (provisioning, the tenant bridge, outbound emails) needs the broker, and
`appsettings.Development.json` already selects it (`Eventing:Transport = RabbitMQ`, `localhost:5672`).

```bash
brew services start rabbitmq
```

Without it, events queue and nothing downstream happens — silently, from the UI's point of view.

## Background jobs are OFF by default

Two flags guard every recurring job, but only one of them is really holding it in Development:
`BackgroundJobs:RegisterStandardJobs` ships **true** in both `appsettings.json` and
`appsettings.Development.json`, so the per-job entry in `BackgroundJobs:EnabledJobs` is the switch that matters.
Production has a larger gate — `BackgroundJobs:Enabled` is **false** in the base file, so the scheduler itself does
not run there.

So "the reminder never arrived" / "recurrence does nothing" is a configuration question before it is a defect:

```jsonc
"EnabledJobs": {
  "Diten.Platform.MOD-0024.TaskDueSoonSweepJob": true,     // BL-065 due-soon reminders
  "Diten.Platform.MOD-0024.TaskRecurrenceSweepJob": true   // recurring task generation
}
```

Trigger a run without waiting for the hour: the Hangfire dashboard at `/hangfire` → Recurring Jobs → **Trigger
now**.

## Notification language

A dispatch is sent in the TENANT's configured language, not the reader's and not the browser's: the chain is
`Tenant.Settings.Language` → `Tenant.DefaultLanguage` → `"en"`. `TenantManagement:DefaultLanguage` is `"en"` in
`appsettings.Development.json`, so a locally provisioned tenant gets English mail even while the UI is Turkish.
That surprised a live verification round; see BL-068 for the product question behind it.
