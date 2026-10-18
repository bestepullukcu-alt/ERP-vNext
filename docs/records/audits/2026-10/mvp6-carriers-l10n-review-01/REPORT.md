---
module: MOD-0184
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# MOD-0184 Carriers UI — l10n correctness review: 4 Arabic orthography fixes, 5 items for a human translator

    reviewed  : 2026-10-18 via the l10n-agent (after Claims + Returns) · author: CT (gmg-10)
    scope     : frontend/Diten.Web/Resources/Views/SupplyChain/Carriers/CarriersIndex.{en,tr,fr,es,zh,ar,ru}.resx
    verdict   : 270 values (45 keys × 6 non-English) reviewed. **4 value fixes applied, all `ar`** (tanvin /
                šadda orthography consistency). 5 domain-term items deferred; 8 cross-module items fed to the
                consistency pass. Verified by CT: only `ar` changed, parity 45×7, en untouched, valid XML,
                value-only.

## Verification (CT)

`git diff --stat`: only `CarriersIndex.ar.resx` changed (3 lines — the file packs several `<data>` per line,
so 4 value edits land on 3 lines); en and the other five unchanged. Parity 45 keys in every language; ar
parses as valid XML; no key name or `en` value changed.

A false alarm was caught first: a line-count (`grep -c '<data name='`) made the non-English files look like
they held only 5–8 keys. They hold **45** — the files are inline-packed, so the right measure is the match
count, which shows full parity and 0 missing keys. No completeness gap.

## F-R4a-4 (the lane's own Q403/409 strings) — clean

`IdempotencyKeyReused`, `InvalidCarrierTransition`, `CarrierCodeConflict`, `PersistenceUnavailable`,
`ListUnavailable`, `RetrySameRequest`, `CreateReplaySuccess`, `StatusReplaySuccess` are all correct in the
6 languages and consistent with their family (and with the Claims/Returns equivalents where shared). No fix.

## The 4 fixes (ar, orthography only)

| key | fix |
|---|---|
| `ExternalReferencePlaceholder` | tanvin `اً` → `ًا` ("مرجعًا خارجيًا اختياريًا"), matching the rest of the file (and Claims/Returns) |
| `ReasonCodePlaceholder` | same tanvin form ("سببًا … فارغًا") |
| `PersistenceUnavailable` | "حالياً" → "حاليًا" |
| `CarrierNotFound` | "تعذر" → "تعذّر" (šadda, matching `ListUnavailable` in the same file) |

Carrier term is sound and consistent with Claims/Returns (tr Taşıyıcı, fr Transporteur, es Transportista,
zh 承运商, ar شركة النقل, ru Перевозчик); status (Active/Suspended/Retired) and modes
(Air/Parcel/Rail/Road/Sea) correct with gender agreement.

## Deferred to a human translator (5)

`StatusRetired` ar ("متقاعد" = a person's retirement, wrong connotation for a carrier record — also appears
in Platform/MDM, a cross-platform call); `ModeRoad` fr/es/ru and `ModeParcel` ru (noun vs the adjective
form the other modes use — also in Loads, decide together); `SupportedModes(Placeholder)` tr ("Modlar"
calque). CT does not invent domain-term translations.

## 8 cross-module items → the consistency pass (XMC-001, not fixed)

Fed to `mvp6-supplychain-l10n-consistency-01` (see its addendum). The notable ones:
- **Key-name-level drift** (the value sweep missed this): Carriers uses `Actions` / `EmptyList` /
  `ListUnavailable` / `InternalError` / `AccessDenied` / `CarrierNotFound` where Claims uses `ActionsHeader`
  / `EmptyState` / `ListErrorState` / `ErrInternalError` / `ErrForbidden` / `ErrClaimNotFound` — same
  concepts, different **key names** and some different `en`/wire text.
- `SupportReference` fr ("Référence d’assistance" vs Claims/Returns "Référence support") and ru ("Номер
  обращения" vs "Код для поддержки").
- `StatusActive` tr ("Etkin" vs SharedResource "Aktif") and zh ("已停用" for Retired **collides** with
  SharedResource Passive "停用").
- `ChangeStatusTitle` ru (noun "Изменение статуса…" vs Claims/Returns verb "Изменить статус…").

## Files

- `REPORT.md` — this review. Code: `CarriersIndex.ar.resx` (4 ar value edits).
