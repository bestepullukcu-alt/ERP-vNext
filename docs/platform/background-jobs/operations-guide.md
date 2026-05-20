# Background Job Scheduler Operations Guide

Module: MOD-0026  
Runtime foundation: Hangfire-backed Platform scheduler

## Purpose

The background job foundation provides recurring job registration, deferred job execution, execution logging, dashboard authorization, and observability hooks for Platform-owned jobs.

## Current Live Surface

- `PlatformRecurringJobRegistrar`
- `HangfireRecurringJobRegistrationHostedService`
- `HangfireBackgroundJobScheduler`
- `HangfireBackgroundJobExecutor`
- `JobExecutionLogWriter`
- Dashboard authorization for platform actors
- Background job observability metrics and health checks

## Operational Checks

1. Confirm MongoDB is available before starting Platform API.
2. Confirm Hangfire storage readiness health check passes.
3. Review job execution logs for failures.
4. Verify recurring registration logs after deployment.
5. Keep business job logic in the owning module; MOD-0026 owns the scheduler foundation.

## Known Boundaries

Tenant lifecycle jobs such as trial expiry or auto-suspend are consuming-module work and should be implemented by the owning module using this scheduler.

