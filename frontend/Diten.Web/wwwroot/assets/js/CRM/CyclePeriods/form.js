/**
 * MOD-0165-FU07 Cycle Periods — the cascading scope selector on the Create / Edit page (Golden Compact).
 *
 * One rule drives the whole file: a period lives at exactly ONE address. The chosen level decides which reference
 * control is visible, and the others are HIDDEN AND CLEARED — a hidden-but-populated field would still be posted, and
 * the runtime would refuse the write as an ambiguous scope, which is a baffling way to fail a form the author filled
 * in correctly.
 *
 * The business-unit list is COUNTRY-FIRST: it is derived from the territory plans that cover a country over the
 * period's days, so it stays gated until a country is chosen and reloads whenever that country or the window changes.
 * The country it reads is #buFilterCountry — which posts as BusinessUnitCountryContext, informational only — and
 * deliberately NOT #countryScope, which is the reference of a country-SCOPED period. Reading the scope element here was
 * the defect: it is cleared whenever the level is not `country`, so the business-unit list was never filtered at all.
 * Because the filter now posts, applyScopeType clearing it at every other level is load-bearing: a context left behind
 * after switching to `tenant` would be stored against a period that has no business unit.
 *
 * An empty list is stated out loud rather than left as a silent empty dropdown, and it is never replaced by a
 * hardcoded list: an option the platform does not know would be authored and then refused at save.
 */
(function (window, document) {
    'use strict';

    const section = document.getElementById('scopeSection');
    const scopeTypeEl = document.getElementById('scopeType');
    if (!section || !scopeTypeEl) return;

    const L = window.CyclePeriodsL10n || window.L10n || {};
    const optionsUrl = section.dataset.scopeOptionsUrl || '/CRM/CyclePeriods/api/scope-options';

    // #countryScope is intentionally not read anywhere in this file — see the header note. The business-unit block has
    // its own country control: same source (COUNTRY_CODES), separate element, because it narrows a list and is stored
    // as CONTEXT rather than scoping a period.
    const buFilterCountryEl = document.getElementById('buFilterCountry');
    const businessUnitCountryFirst = document.getElementById('businessUnitCountryFirst');
    const legalEntityEl = document.getElementById('legalEntityId');
    const businessUnitEl = document.getElementById('businessUnitId');
    const businessUnitHint = document.getElementById('businessUnitHint');
    const startEl = document.querySelector('input[name="StartDate"]');
    const endEl = document.querySelector('input[name="EndDate"]');

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));

    /// Shows the one reference control the chosen level needs and clears every other one.
    const applyScopeType = () => {
        const scopeType = (scopeTypeEl.value || '').trim();
        section.querySelectorAll('.scope-ref').forEach(block => {
            const isActive = block.dataset.scopeRef === scopeType;
            block.classList.toggle('d-none', !isActive);
            if (isActive) return;

            // Cleared, not merely hidden: the browser posts hidden inputs too.
            block.querySelectorAll('select, input').forEach(field => { field.value = ''; });
        });
    };

    const fetchOptions = async (country) => {
        const params = new URLSearchParams();
        if (country) params.set('country', country);
        if (startEl?.value) params.set('startDate', startEl.value);
        if (endEl?.value) params.set('endDate', endEl.value);

        const response = await fetch(`${optionsUrl}?${params.toString()}`, {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        });
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error((body.errors || [L.ErrorOccurred]).join(' · '));
        return body.data;
    };

    /// Gates the business-unit list behind a country, because an unfiltered list of units is a list of units from
    /// everywhere — which is not an answer to "which unit runs this period".
    /// <para>An already-chosen unit is NEVER gated away: a disabled select is not posted at all, so gating an edit (or
    /// a form coming back from a failed post) would silently strip the reference the period already has and turn a
    /// rename into an invalid write.</para>
    const applyBusinessUnitGate = () => {
        if (!businessUnitEl) return;

        const hasCountry = !!(buFilterCountryEl?.value || '').trim();
        const hasChoice = !!(businessUnitEl.value || '').trim();
        const gated = !hasCountry && !hasChoice;

        businessUnitEl.disabled = gated;
        businessUnitCountryFirst?.classList.toggle('d-none', !gated);
        businessUnitHint?.classList.toggle('d-none', gated);
    };

    /// Reloads the business-unit candidates for the current country + window. The author's current choice is kept when
    /// it is still offered; when it is not, it is kept anyway — a valid code outside the plan is accepted and stamped
    /// `manual`, so silently dropping it would lose a legitimate answer.
    const refreshBusinessUnits = async () => {
        if (!businessUnitEl) return;

        const country = (buFilterCountryEl?.value || '').trim();
        const previous = businessUnitEl.value;
        if (!country && !previous) {
            // Nothing to ask for yet. The gate already says why, and a country-less call would answer with every
            // unit the tenant has - the unfiltered list this change exists to remove.
            applyBusinessUnitGate();
            return;
        }

        let data;
        try {
            data = await fetchOptions(country);
        } catch (error) {
            // A failed lookup leaves the list exactly as it was: emptying it would look like "there are none".
            applyBusinessUnitGate();
            return;
        }

        const options = Array.isArray(data?.businessUnits) ? data.businessUnits : [];
        const head = `<option value="">${esc(L.SelectPlaceholder || '')}</option>`;
        const rendered = options
            .map(o => `<option value="${esc(o.value)}" title="${esc(o.hint || '')}">${esc(o.label)}</option>`)
            .join('');

        const keepsPrevious = previous && options.some(o => o.value === previous);
        const orphan = previous && !keepsPrevious
            ? `<option value="${esc(previous)}" selected>${esc(previous)}</option>`
            : '';

        businessUnitEl.innerHTML = head + orphan + rendered;
        if (keepsPrevious) businessUnitEl.value = previous;

        if (businessUnitHint) {
            businessUnitHint.textContent = !data?.businessUnitReady
                ? (L.BusinessUnitNoPlan || '')
                : data.businessUnitFromTerritory
                    ? (L.BusinessUnitFromTerritory || '')
                    : (L.BusinessUnitFromVocabulary || '');
        }

        applyBusinessUnitGate();
    };

    // #countryScope is NOT wired here on purpose: it is the reference of a country-scoped period, and changing it must
    // not reshuffle a different level's list.
    scopeTypeEl.addEventListener('change', () => { applyScopeType(); applyBusinessUnitGate(); });
    buFilterCountryEl?.addEventListener('change', () => { void refreshBusinessUnits(); });
    businessUnitEl?.addEventListener('change', applyBusinessUnitGate);
    startEl?.addEventListener('change', () => { void refreshBusinessUnits(); });
    endEl?.addEventListener('change', () => { void refreshBusinessUnits(); });

    applyScopeType();
    applyBusinessUnitGate();
})(window, document);
