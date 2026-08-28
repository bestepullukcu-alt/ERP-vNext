# MOD-0149 — CRM / Account Foundation — Follow-ups Backlog

Living list of small, non-blocking CRM gaps/enhancements deferred after the MOD-0149 closeout. None are release
blockers. Add new small CRM gaps here; larger ones get their own `*-gap.md` doc and are linked from this list.

## Open

### GAP-CRM-01 — Accounts list: Country / City filters
**Severity:** Low · **Owner:** MOD-0149 frontend · **Blocks release:** No

Add **Country** and **City** filter chips to the Accounts DataTable (`/CRM/Accounts`). Today the inline filter only
offers **Status** and **Account Type** (`_Filter.cshtml` → `filterStatus`, `filterAccountType`).

What it needs:
- **Backend (small):** extend the list projection `AccountListItemDto` with `CountryRef` / `CityRef` so the grid rows
  carry them (currently the DTO exposes Id/AccountName/AccountCode/AccountType/AccountCategory/Status/ParentAccountId —
  no location fields), so client-side filtering has data.
- **Frontend:** two new independent (non-cascading) single-select chips `filterCountry` / `filterCity` in
  `_Filter.cshtml`, options fed from MOD-0048 `country` / `city` published sets via `/CRM/Accounts/lookups` (already
  populated live), plus the filter predicates + Save-View wiring in `wwwroot/assets/js/CRM/Accounts/index.js`
  (mirror the existing status/accountType chip pattern).
- **No hardcoded fallback**, consistent with the rest of the module (options come from published reference sets).

### GAP-CRM-02 — Account External Reference (SourceSystem + multiple refs)
**Severity:** Medium · **Owner:** MOD-0149 · **Blocks release:** No · **Detail:** [external-reference-gap.md](./mod-0149-external-reference-gap.md)

UI captures a single external id with `SourceSystem` hardcoded `"default"`. Deferred: SourceSystem dropdown
(MOD-0048-sourced), multiple external references per account, and a lookup-by-external-id endpoint.

### GAP-CRM-03 — Full Turkey district data (import)
**Severity:** Low · **Owner:** ops/data · **Blocks release:** No

The `district` reference set currently holds only Edirne's 9 districts (starter). The map's geocode auto-fill only
matches district for Edirne; other provinces' districts stay empty until the full list is loaded. Load the full
province→district data via the reference-data **import** tool (`imports/preview` → `commit`), then the map auto-fills
district nationwide.

### GAP-CRM-04 — Contact professional fields cascade by Contact Type
**Severity:** Medium · **Owner:** MOD-0048/EA (metadata) + MOD-0150 frontend · **Blocks release:** No
**Source:** [mod-0150-contact-location-pii-kvkk-hardening.md](./mod-0150-contact-location-pii-kvkk-hardening.md) §15

Contact Create/Edit'te `ProfessionalTitle` / `Specialty` / `Department` artık MOD-0048'den beslenen single-select2
dropdown'lar (`professional-title` / `medical-specialty` / `department-type`, opsiyonel, fallback-option). İstenen
sonraki adım: bu seçeneklerin seçili **Contact Type**'a göre filtrelenmesi (ör. `doctor` için tıbbi uzmanlıklar,
`administrative` için departmanlar).

Neden şimdi yapılmadı: setler **düz** — değerlerin hangi contact-type'a ait olduğunu belirten metadata yok ve
frontend'in tükettiği published-values endpoint'i value başına metadata döndürmüyor (yalnızca code/label/isActive/
sortOrder). `contact-type → options` şeklinde **hardcoded mapping** repo kuralına (no hardcoded list/fallback) aykırı.

Doğru (veri-güdümlü) yol:
- **Governance (MOD-0048):** `medical-specialty` / `department-type` / `professional-title` değerlerine
  `contactTypes` (veya `appliesTo`) metadata attribute'u eklenir — mevcut `account-relationship-type`'ın
  `direction/inverse/selfAllowed` metadata pattern'i gibi (`IReferenceMetadataReader` seam zaten var).
- **Frontend/backend passthrough:** published-values consumer'ı bu metadata'yı taşıyacak şekilde genişletilir;
  `_Form.cshtml`'deki select'lere zaten eklenmiş `data-contact-professional` hook'u üzerinden, seçili Contact Type
  değiştiğinde options metadata ile filtrelenir. Metadata yoksa **tüm options gösterilir** (güvenli varsayılan, no-op).
- **Hardcode yok**, tamamen veri-güdümlü.

## Related (tracked elsewhere, not repeated here)
- Platform/governance and operational follow-ups (MOD-0285 nav migration, catalog `Service` legacy value,
  `CRM` vs `crm.account` naming, import/export endpoints, MOD-0021 audit HTTP wiring, MatchesScope unit tests, dev
  residue) are enumerated in [mod-0149-final-review-hardening-closeout.md](./mod-0149-final-review-hardening-closeout.md).
