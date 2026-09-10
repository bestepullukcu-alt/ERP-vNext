# Lookups / Reference Data Technical Reference

PSS-011 is the Platform source for system lookup lists used by Platform Admin forms and filters.

## Ownership

Platform lookups include module catalog domains/services, subscription cycles, audit categories, countries, currencies, locales, languages, time zones, tenant tiers, and feature categories.

## Consumption Rules

- Platform admin dropdowns and Select2 filters should call `/api/lookups/*`.
- Local hardcoded fallback arrays should not be used for Platform-owned lookup values.
- ERP business reference data belongs to its owning tenant/domain service and should not be added to Platform lookups without an approved scope decision.

## Frontend Contract

Consumers should treat `code`, `name`, and `value` as the stable lookup option shape where available. Display labels should come from the lookup payload or localization resources, not inline strings.

