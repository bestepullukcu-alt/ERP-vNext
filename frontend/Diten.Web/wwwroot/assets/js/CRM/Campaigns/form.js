/**
 * MOD-0165-FU04 Campaigns — Create/Edit form behaviour (Golden Compact aligned).
 *
 * The POST path is unchanged: this is still an MVC model-bound form, every control keeps its generated `name`, and
 * Select2 writes straight through to the underlying <select> so the posted payload is identical to before.
 */
(function (window, document) {
    'use strict';
    const form = document.getElementById('campaignForm');
    if (!form) return;

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));

    // ---------------------------------------------------------------- FU11 campaign code placeholder
    // Rendered only on create; on edit the input carries the real, already-assigned code and no peek URL.
    const codeInput = form.querySelector('input[data-code-peek-url]');

    /**
     * Shows the code the server WOULD assign next, as the field's PLACEHOLDER.
     *
     * Non-committing on both sides: the endpoint reads the sequence instead of incrementing it, and the field is left
     * EMPTY, so the real code is still assigned at save. That is also why a stale peek is harmless — two authors can
     * see the same hint and still save under different codes, because neither of them posts what they saw.
     *
     * The value is never written into the input. Typing a code still wins, and a failed peek simply leaves the field
     * as it was: no hint at all beats a hint that would not be honoured.
     */
    const loadCodePlaceholder = async () => {
        if (!codeInput || codeInput.readOnly) return;
        const url = codeInput.dataset.codePeekUrl;
        const template = window.CampaignL10n?.CampaignCodeAutoPlaceholder;
        if (!url || !template) return;

        try {
            const response = await fetch(url, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            });
            if (!response.ok) return;

            // 200 with no data is a legitimate answer: the server found no free candidate and says so honestly.
            const code = (await response.json().catch(() => ({})))?.data?.campaignCode;
            if (code) codeInput.placeholder = template.replace('{0}', code);
        } catch (error) {
            console.error('[Campaigns] The next campaign code could not be peeked.', error);
        }
    };

    const cycleSelect = document.getElementById('CyclePeriodId');
    const cycleWindow = document.getElementById('cyclePeriodWindow');
    const cycleWarning = document.getElementById('cyclePeriodScopeWarning');

    /** Renders the selected period's window under the picker so a containment rejection is explainable. */
    const renderCycleWindow = () => {
        if (!cycleSelect || !cycleWindow) return;
        const option = cycleSelect.selectedOptions?.[0];
        const start = option?.dataset?.start;
        const end = option?.dataset?.end;
        if (!start || !end) { cycleWindow.textContent = ''; return; }
        const status = option.dataset.status || '';
        const label = window.CampaignL10n?.CyclePeriodWindow || '';
        cycleWindow.innerHTML =
            `<span class="fw-medium">${esc(label)}</span> ${esc(start)} — ${esc(end)}`
            + (status ? ` <span class="badge bg-label-secondary ms-1">${esc(status)}</span>` : '');
    };

    // ---------------------------------------------------------------- FU10 targeting mode
    const targetingSection = document.getElementById('campaignTargetingSection');
    const targetingModeEl = document.getElementById('targetingMode');
    const targetedSegmentsEl = document.getElementById('targetedSegmentIds');
    const targetedSegmentsNote = document.getElementById('targetedSegmentsNote');
    const segmentsUrl = targetingSection?.dataset.segmentsUrl || '/CRM/Campaigns/api/segments';

    /**
     * Shows only the block the chosen mode can actually use, mirroring the segment editor's own static/dynamic
     * switch and what the runtime already enforces:
     *   manual  -> the audience is hand-authored on Details, so a segment picker here would author nothing
     *   segment -> the audience comes from segments, and a manual target write is refused server-side
     * Hiding beats disabling: an author never has to wonder why a section they can see does nothing.
     */
    const applyTargetingModeVisibility = () => {
        const mode = (targetingModeEl?.value || 'manual').trim();
        targetingSection?.querySelectorAll('[data-targeting-block]').forEach(block => {
            block.classList.toggle('d-none', block.dataset.targetingBlock !== mode);
        });
        if (targetedSegmentsEl) targetedSegmentsEl.required = mode === 'segment';
        renderSegmentNote();
    };

    /** Mixing subject types is allowed and deliberate, so the mix is shown rather than left to be discovered. */
    const renderSegmentNote = () => {
        if (!targetedSegmentsEl || !targetedSegmentsNote) return;
        const selected = Array.from(targetedSegmentsEl.selectedOptions || []);
        if (selected.length === 0) { targetedSegmentsNote.textContent = ''; return; }

        const types = Array.from(new Set(selected.map(o => o.dataset.subjectType).filter(Boolean)));
        const superseded = selected.filter(o => o.dataset.superseded === 'true').length;
        const parts = [`${selected.length}`];
        if (types.length) parts.push(types.join(' + '));
        if (superseded) parts.push(`${superseded} × ${window.CampaignL10n?.SegmentSuperseded || ''}`);
        targetedSegmentsNote.textContent = parts.join(' · ');
    };

    /**
     * Fills the segment picker from the ACTIVE list.
     *
     * Segments the campaign is ALREADY linked to are rendered server-side and kept here even when they have since
     * been archived or superseded and are therefore absent from the active list. Dropping them would post a shorter
     * set and silently unlink segments the author never touched.
     */
    const loadSegments = async () => {
        if (!targetedSegmentsEl) return;
        const linked = new Set(Array.from(targetedSegmentsEl.options).map(o => o.value));

        try {
            const response = await fetch(`${segmentsUrl}?segmentStatus=active`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            });
            if (!response.ok) return;
            const items = (await response.json().catch(() => ({})))?.data?.items || [];

            items.forEach(item => {
                if (linked.has(item.segmentId)) return;
                const option = document.createElement('option');
                option.value = item.segmentId;
                option.textContent = `${item.segmentCode} - ${item.segmentName}`;
                option.dataset.subjectType = item.subjectType || '';
                option.dataset.status = item.segmentStatus || '';
                option.dataset.superseded = item.superseded ? 'true' : 'false';
                targetedSegmentsEl.appendChild(option);
            });
        } catch (error) {
            // A picker that cannot load its options leaves the existing links alone.
            console.error('[Campaigns] Segment options could not be loaded.', error);
        } finally {
            renderSegmentNote();
        }
    };

    // ---------------------------------------------------------------- FU09 scope cascade
    const scopeSection = document.getElementById('campaignScopeSection');
    const scopeTypeEl = document.getElementById('scopeType');
    const countryEl = document.getElementById('countryScope');
    const legalEntityEl = document.getElementById('legalEntityId');
    const businessUnitEl = document.getElementById('businessUnitId');
    const buFilterCountryEl = document.getElementById('buFilterCountry');

    const scopeOptionsUrl = scopeSection?.dataset.scopeOptionsUrl || '/CRM/Campaigns/api/scope-options';
    const applicableUrl = scopeSection?.dataset.applicableUrl || '/CRM/Campaigns/api/applicable-cycle-periods';

    let scopeOptions = null;

    const fillOptions = (el, items, placeholder) => {
        if (!el) return;
        const current = el.dataset.selected || el.value || '';
        el.innerHTML = `<option value="">${esc(placeholder || '')}</option>`
            + (items || []).map(i => `<option value="${esc(i.value)}">${esc(i.label)}</option>`).join('');
        if (current) el.value = current;
    };

    /** Says WHY a list is empty. An unpublished set, an unreachable dependency and "no plan matches" are three
     *  different situations, and an author who cannot tell them apart has no way to act. */
    const setNote = (level, key) => {
        const note = scopeSection?.querySelector(`[data-scope-note="${level}"]`);
        if (!note) return;
        const text = key ? (window.CampaignL10n?.[key] || '') : '';
        note.textContent = text;
        note.classList.toggle('d-none', !text);
    };

    /** Only the block belonging to the selected level is shown: the address is discriminated, never combined. */
    const applyScopeType = () => {
        const level = (scopeTypeEl?.value || '').trim();
        scopeSection?.querySelectorAll('[data-scope-block]').forEach(block => {
            block.classList.toggle('d-none', block.dataset.scopeBlock !== level);
        });
    };

    const renderScopeOptions = () => {
        if (!scopeOptions) return;
        fillOptions(scopeTypeEl, (scopeOptions.scopeTypes || []).map(v => ({
            value: v, label: window.CampaignL10n?.['ScopeType_' + v] || v
        })), '');
        if (scopeTypeEl && !scopeTypeEl.value) scopeTypeEl.value = 'tenant';

        fillOptions(countryEl, scopeOptions.countries, window.CampaignL10n?.SelectOption);
        setNote('country', scopeOptions.countrySetPublished ? null : 'ReferenceSetUnpublished');

        fillOptions(legalEntityEl, scopeOptions.legalEntities, window.CampaignL10n?.SelectOption);
        setNote('legal-entity', scopeOptions.legalEntityLookupAvailable ? null : 'DependencyUnavailable');

        fillOptions(buFilterCountryEl, scopeOptions.countries, window.CampaignL10n?.ShowAll);
        fillOptions(businessUnitEl, scopeOptions.businessUnits, window.CampaignL10n?.SelectOption);
        setNote(
            'business-unit',
            !scopeOptions.businessUnitSetPublished
                ? 'ReferenceSetUnpublished'
                : (scopeOptions.businessUnitFromTerritory ? null : 'NoTerritoryPlanMatches'));

        applyScopeType();
    };

    const loadScopeOptions = async () => {
        if (!scopeSection) return;
        const country = (buFilterCountryEl?.value || '').trim();
        const params = new URLSearchParams();
        if (country) params.set('country', country);
        const start = document.getElementById('StartDate')?.value;
        const end = document.getElementById('EndDate')?.value;
        if (start) params.set('startDate', new Date(start).toISOString());
        if (end) params.set('endDate', new Date(end).toISOString());

        try {
            const response = await fetch(`${scopeOptionsUrl}?${params.toString()}`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            });
            if (!response.ok) return;
            scopeOptions = (await response.json().catch(() => ({})))?.data || null;
            renderScopeOptions();
        } catch (error) {
            // A selector that cannot load its options leaves what is already selected alone.
            console.error('[Campaigns] Scope options could not be loaded.', error);
        }
    };

    /** The address the author is CURRENTLY editing — which may not be the one on disk yet. */
    const currentScopeQuery = () => {
        const level = (scopeTypeEl?.value || '').trim();
        const params = new URLSearchParams();
        if (level) params.set('scopeType', level);
        if (level === 'country' && countryEl?.value) params.set('countryScope', countryEl.value);
        if (level === 'legal-entity' && legalEntityEl?.value) params.set('legalEntityId', legalEntityEl.value);
        if (level === 'business-unit' && businessUnitEl?.value) params.set('businessUnitId', businessUnitEl.value);
        return params;
    };

    /**
     * Fills the period picker from the APPLICABLE list.
     *
     * The option already rendered server-side for the CURRENT binding is kept even when the period has since been
     * closed and is therefore absent from the active list. Dropping it would leave the picker empty, post a null
     * binding and silently unbind the campaign — so the current value is preserved and re-selected.
     *
     * Select2 is initialised only AFTER this resolves; initialising first would snapshot an empty list.
     */
    const loadCyclePeriods = async () => {
        if (!cycleSelect) return;
        const currentId = cycleSelect.dataset.currentId || '';
        const preserved = Array.from(cycleSelect.options).filter(o => o.value && o.value === currentId);

        try {
            // FU09 - the APPLICABLE list, decided on the server. Filtering in the browser would be a second copy of
            // the rule, and a direct API call would walk past it.
            const response = await fetch(`${applicableUrl}?${currentScopeQuery().toString()}`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            });
            if (!response.ok) return;
            const body = await response.json().catch(() => ({}));
            const items = body?.data?.items || [];

            items.forEach(item => {
                if (item.cyclePeriodId === currentId) return; // already present as the preserved option
                const option = document.createElement('option');
                option.value = item.cyclePeriodId;
                option.textContent = `${item.cycleCode} — ${item.cycleName}`;
                option.dataset.start = (item.startDate || '').slice(0, 10);
                option.dataset.end = (item.endDate || '').slice(0, 10);
                option.dataset.status = item.cycleStatus || '';
                cycleSelect.appendChild(option);
            });
        } catch (error) {
            // A picker that cannot load its options leaves the current binding intact rather than clearing it.
            console.error('[Campaigns] Cycle period options could not be loaded.', error);
        } finally {
            if (currentId) cycleSelect.value = currentId;
            else if (preserved.length === 0) cycleSelect.value = '';

            // The current binding is kept even when the new scope no longer makes it applicable - dropping it would
            // post null and silently unbind the campaign. It is flagged instead, and the server refuses the save.
            if (currentId) {
                const stillOffered = items.some(i => i.cyclePeriodId === currentId);
                const option = Array.from(cycleSelect.options).find(o => o.value === currentId);
                if (option && !stillOffered) {
                    const badge = window.CampaignL10n?.CyclePeriodNotApplicable || '';
                    if (badge && !option.textContent.includes(badge)) option.textContent += ` (${badge})`;
                }
                if (cycleWarning) {
                    cycleWarning.textContent = stillOffered
                        ? ''
                        : (window.CampaignL10n?.CyclePeriodScopeMismatch || '');
                    cycleWarning.classList.toggle('d-none', stillOffered);
                }
            }

            renderCycleWindow();
        }
    };

    /**
     * Select2 is initialised AFTER every option is in the DOM: the vocabulary selects are Razor-rendered, and the
     * cycle-period picker is filled by loadCyclePeriods() first — initialising earlier would snapshot an empty list.
     * `change.select2` is re-broadcast as a native `change` because Select2 suppresses the native event, and
     * jQuery-unaware listeners (validation, the end-date guard, the period-window renderer) would otherwise never fire.
     */
    const initSelect2 = () => {
        const $ = window.jQuery;
        if (typeof $ !== 'function' || typeof $.fn?.select2 !== 'function') return;
        $('.select2', form).each(function () {
            const $el = $(this);
            if ($el.hasClass('select2-hidden-accessible')) return;
            // placeholder: data-placeholder attribute'u Select2 tarafından otomatik okunur; boş bir çoklu seçim
            // kutusunun ne beklediğini söylemesi için burada açıkça geçiliyor.
            $el.wrap('<div class="position-relative"></div>').select2({
                dropdownParent: $el.parent(),
                width: '100%',
                placeholder: $el.data('placeholder') || ''
            });
            $el.on('change.select2', function () {
                this.dispatchEvent(new Event('change', { bubbles: true }));
            });
        });
    };

    form.addEventListener('submit', event => {
        const start = document.getElementById('StartDate')?.value;
        const end = document.getElementById('EndDate')?.value;
        if (start && end && new Date(end) < new Date(start)) {
            event.preventDefault();
            window.showToast?.(window.CampaignL10n?.EndDateBeforeStartDate || 'End date cannot be earlier than start date.', 'error');
        }
    });

    cycleSelect?.addEventListener('change', renderCycleWindow);
    targetingModeEl?.addEventListener('change', applyTargetingModeVisibility);
    targetedSegmentsEl?.addEventListener('change', renderSegmentNote);

    // FU09 - the address decides which periods are applicable, so every part of it reloads the picker.
    scopeTypeEl?.addEventListener('change', () => { applyScopeType(); void loadCyclePeriods(); });
    countryEl?.addEventListener('change', () => { void loadCyclePeriods(); });
    legalEntityEl?.addEventListener('change', () => { void loadCyclePeriods(); });
    businessUnitEl?.addEventListener('change', () => { void loadCyclePeriods(); });
    // The country filter narrows the business-unit candidates; it is not the campaign's scope.
    buFilterCountryEl?.addEventListener('change', () => { void loadScopeOptions(); });

    // The code hint touches no picker, so it is not awaited with them - a slow peek must not delay Select2.
    void loadCodePlaceholder();

    // Options first, Select2 second (see initSelect2): every picker must be complete before Select2 reads it.
    (async () => {
        applyTargetingModeVisibility();
        await loadScopeOptions();
        await loadCyclePeriods();
        await loadSegments();
    })().finally(initSelect2);
})(window, document);
