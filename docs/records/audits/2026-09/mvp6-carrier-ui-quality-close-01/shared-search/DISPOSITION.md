# Shared tenant-shell Search disposition

Status: **OPEN — exact unapplied candidate; shared integration-owner decision required**

## Finding

- Exact caller: `frontend/Diten.Web/wwwroot/assets/js/main.js:209-214`; line 213 hardcodes
  `Search [CTRL + K]` in the Autocomplete placeholder.
- Render chain: `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml:314-322` renders
  `#autocomplete`; line 536 loads shared `main.js`; `main.js:339-345` invokes `initializeAutocomplete()`.
- Resource owner: shared frontend / tenant-shell integration owner.
- Both source files are outside the approved Carrier 21-path allowlist.
- `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` already contain the `Search` key. No resource addition is
  needed. Arabic currently contains `بحث...`.

## Exact unapplied candidate

- Patch: `SHARED-SEARCH.patch`
- SHA-256: `2abe82415f68870f8d714af093473f497bcb1f210f970e491c32d288b1440074`
- `main.js` preimage → target:
  `f9b6c53e430e7a1149093e7089772c3903fc1b2bea9367e0504029fa1b1890ab` →
  `1ae6b74ae5774865677d32b0d4b427b3b03e11175527f17adc0cc016295447fe`
- `_LayoutTenantShell.cshtml` preimage → target:
  `c4bc7593d37daf10795ea17e72a9401515cf24deff4b7bb3f0d247b847ac04a3` →
  `babcf035b3c7fc14eebd9442586debde2f78200d37d43423f0a5fadef17a7af4`

The candidate exposes a culture-bound tenant-shell value through
`data-global-search-placeholder`; `main.js` consumes it and preserves the existing English fallback for shells
without the attribute. It is intentionally **not applied**.

## Acceptance

1. Tenant shell renders `<localized Search> [CTRL + K]` in `en,tr,fr,es,zh,ar,ru`.
2. Arabic renders `بحث [CTRL + K]` with `lang=ar`, `dir=rtl`.
3. Opening Ctrl+K uses the same localized placeholder.
4. Platform/archive shells retain their current fallback behavior.
5. The seven `SharedResource` files and all 21 Carrier-owned files remain byte-identical.
6. No browser console error is introduced.

## Copyable owner decision

> Shared frontend integration owner'ın, yalnız kaydedilmiş iki preimage eşleşirse
> `2abe82415f68870f8d714af093473f497bcb1f210f970e491c32d288b1440074` SHA-256'lı exact patch'i
> uygulamasını; `main.js` için `1ae6b74ae5774865677d32b0d4b427b3b03e11175527f17adc0cc016295447fe`,
> `_LayoutTenantShell.cshtml` için `babcf035b3c7fc14eebd9442586debde2f78200d37d43423f0a5fadef17a7af4`
> hedeflerini
> üretmesini onaylıyorum. Kapsam yalnız tenant-shell global-search placeholder yerelleştirmesidir. Carrier
> source, global-search davranışı, Platform/archive shell, Auth, Gateway, permission veya başka shared kaynak
> değişikliği yetkisi vermiyorum.
