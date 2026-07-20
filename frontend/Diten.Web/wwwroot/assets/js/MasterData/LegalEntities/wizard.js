'use strict';

// MOD-0220 finish — Legal Entity create/edit. Left-hand step wizard (bs-stepper, vertical, linear-gated):
// Identity + Statutory/Advanced carry the required fields, Structure/Finance/Addresses are optional detail
// steps. Lookups go through the Diten.Web proxy (legal-form via MDM; countries/currencies via Platform) so the
// HttpOnly token is attached server-side. Create POSTs to /LegalEntities/api (server defaults
// OperationalStatus=Draft, OrganizationRole=LEGALENTITY); Edit PUTs to /LegalEntities/api/{id}. Dates use
// flatpickr in the request culture's format.
(function () {
    const page = document.getElementById('le-wizard-page');
    if (!page) return;

    const endpoint = '/LegalEntities/api';
    const entityId = page.dataset.leId || '';
    const isEdit = page.dataset.leMode === 'edit';
    let L = {};
    let stepper = null;
    const TOTAL_STEPS = 6; // Identity, Statutory, Structure, Finance, Addresses, Overview
    let currentStep = 1;
    let maxReachedStep = 1; // gates forward navigation — a step can't be entered until every step before it validates

    // İŞ1 — required fields grouped by the wizard step they live on (step 3's Parent field is conditional,
    // handled separately by isParentRequired/applyParentRequirement).
    const STEP_REQUIRED = {
        1: ['leCode', 'leLegalName', 'leLegalFormCode'],
        2: ['leCountryCode', 'leBaseCurrencyCode']
    };
    const REQUIRED = [...STEP_REQUIRED[1], ...STEP_REQUIRED[2]];
    const DATE_FIELDS = ['leIncorporationDate', 'leDissolutionDate'];

    const byId = (id) => document.getElementById(id);
    const trim = (v) => (typeof v === 'string' ? v.trim() : '');
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const loadL10n = () => {
        const node = byId('legal-entities-wizard-l10n');
        if (!node) return;
        try { L = JSON.parse(node.textContent || '{}'); } catch (e) { console.error('[LE Wizard] L10n parse failed.', e); }
    };

    const showAlert = (message, level) => {
        const el = byId('le-wizard-alert');
        if (!el) return;
        const list = (Array.isArray(message) ? message : [message]).filter(Boolean);
        if (!list.length) { el.classList.add('d-none'); el.innerHTML = ''; return; }
        el.className = `alert alert-${level === 'error' ? 'danger' : level || 'danger'}`;
        el.innerHTML = list.map((m) => `<div>${escapeHtml(m)}</div>`).join('');
        el.classList.remove('d-none');
    };

    const getAntiForgeryToken = () =>
        document.querySelector('#le-wizard-page input[name="__RequestVerificationToken"]')?.value || '';

    // İŞ1 — error surfacing must handle BOTH shapes: our Response envelope ({errors:[...]}) AND ASP.NET's
    // ModelState 400 ({errors:{Field:["msg"]}}, possibly under {title,errors}). Flatten object value-arrays so the
    // real validation message shows instead of the generic fallback.
    const extractErrors = (json) => {
        const raw = json?.errors ?? json?.Errors;
        if (Array.isArray(raw)) return raw.filter(Boolean);
        if (raw && typeof raw === 'object') return Object.values(raw).flat().filter(Boolean);
        const single = json?.detail || json?.Detail || json?.title || json?.Title || json?.message || json?.Message;
        return single ? [single] : [];
    };

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    // ─── Lookups (all via Diten.Web proxy) ───────────────────────────────────
    const fillSelect = (selectId, items) => {
        const select = byId(selectId);
        if (!select) return;
        const first = select.querySelector('option'); // keep the "Select…" placeholder
        select.innerHTML = '';
        if (first) select.appendChild(first);
        (items || []).forEach((it) => {
            const code = it.code ?? it.Code ?? it.value ?? it.Value;
            // BRD published values expose {code, label}; MDM/platform lookups expose {code, name}. Support both.
            const name = it.name ?? it.Name ?? it.label ?? it.Label ?? code;
            if (code == null) return;
            const opt = document.createElement('option');
            opt.value = code;
            opt.textContent = name; // label = name, code stored (no raw code entry)
            select.appendChild(opt);
        });
    };

    // MOD-0220 — Legal Form / Country / Base Currency now come from governed Business Reference Data (BRD) sets
    // (legal-form / country / base-currency) via the tenant-accessible published-values read. unwrapList yields the
    // set's `items` ({code, label}); fillSelect maps them (label as display, code stored).
    const fetchReferenceData = (setCode) => fetch(`${endpoint}/reference-data/${encodeURIComponent(setCode)}`, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r)).then(unwrapList).catch(() => []);

    // İŞ3 — MDM-local lookups (control-type / accounting-standard / tax-regime) → {data:[{code,name}]}.
    const fetchLookup = (type) => fetch(`${endpoint}/lookups/${encodeURIComponent(type)}`, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r)).then((p) => p?.data ?? p?.Data ?? []).catch(() => []);

    // İŞ3 — Structure parent select: referenceable (ACTIVE) LE list, showing Code — Name, storing the GUID, and
    // excluding the current entity in edit mode (an entity can't be its own parent).
    const loadParentOptions = async () => {
        const select = byId('leParentLegalEntityId');
        if (!select) return;
        const items = await fetch(`${endpoint}/lookup`, { headers: getAuthHeaders() })
            .then((r) => r.ok ? r.json() : Promise.reject(r)).then(unwrapList).catch(() => []);
        const placeholder = select.querySelector('option');
        select.innerHTML = '';
        if (placeholder) select.appendChild(placeholder);
        items.forEach((it) => {
            const id = it.legalEntityId ?? it.LegalEntityId;
            if (!id || (isEdit && String(id) === String(entityId))) return;
            const code = it.code ?? it.Code ?? '';
            const name = it.legalName ?? it.LegalName ?? it.displayName ?? it.DisplayName ?? '';
            const opt = document.createElement('option');
            opt.value = id;
            opt.textContent = code ? `${code} — ${name}` : name; // label only; GUID hidden
            select.appendChild(opt);
        });
    };

    const loadLookups = async () => {
        const [legalForm, countries, currencies, organizationRole, controlType, accountingStandard, taxRegime] = await Promise.all([
            fetchReferenceData('legal-form'),
            fetchReferenceData('country'),
            fetchReferenceData('base-currency'),
            fetchLookup('organization-role'), // İŞA — LE legal-structural role (static MDM lookup, NOT OrgUnit data)
            fetchLookup('control-type'),
            fetchLookup('accounting-standard'),
            fetchLookup('tax-regime')
        ]);
        fillSelect('leLegalFormCode', legalForm);
        fillSelect('leCountryCode', countries);
        fillSelect('leBaseCurrencyCode', currencies);
        fillSelect('leOrganizationRoleCode', organizationRole);
        fillSelect('leControlTypeCode', controlType);
        fillSelect('leAccountingStandardCode', accountingStandard);
        fillSelect('leTaxRegimeCode', taxRegime);
        await loadParentOptions();
    };

    // ─── Culture-aware date pickers ──────────────────────────────────────────
    // Stored value stays ISO (Y-m-d); the visible altInput renders in the request culture's short-date format.
    const initDatePickers = () => {
        if (typeof window.flatpickr !== 'function') return;
        DATE_FIELDS.forEach((id) => {
            const el = byId(id);
            if (el) window.flatpickr(el, { dateFormat: 'Y-m-d', altInput: true, altFormat: L.DateFormat || 'Y-m-d', allowInput: true });
        });
    };
    const dateValue = (id) => trim(byId(id)?.value); // altInput keeps the original input's Y-m-d value
    const setDate = (id, iso) => {
        const el = byId(id);
        if (!el || !iso) return;
        const d = new Date(iso);
        if (Number.isNaN(d.getTime())) return;
        const ymd = d.toISOString().slice(0, 10);
        if (el._flatpickr) el._flatpickr.setDate(ymd, true); else el.value = ymd;
    };

    // ─── Required tracking ───────────────────────────────────────────────────
    const isFilled = (id) => trim(byId(id)?.value).length > 0;

    // İŞA — a Branch or Rep Office MUST declare an owning parent (mirrors backend LegalEntityRules.RequiresParent).
    const PARENT_REQUIRED_ROLES = ['BRANCH', 'REPOFFICE'];
    const isParentRequired = () => PARENT_REQUIRED_ROLES.includes((byId('leOrganizationRoleCode')?.value || '').trim().toUpperCase());

    // Toggle the Parent field's red star + clear a stale invalid state when the role no longer requires a parent.
    // The shared required-fields tracker (required-fields-tracker.js) counts a field as required when its label
    // carries a visible `.text-danger` asterisk, so toggling BOTH `text-danger` and `d-none` on the star makes the
    // "Zorunlu: X / Y" badge include the Parent field only while a Branch / Rep Office role is selected.
    const applyParentRequirement = () => {
        const required = isParentRequired();
        const star = byId('le-parent-required-star');
        if (star) { star.classList.toggle('d-none', !required); star.classList.toggle('text-danger', required); }
        if (!required) byId('leParentLegalEntityId')?.classList.remove('is-invalid');
    };

    // İŞ1 — per-step validation, used both by the wizard's per-step Next gating and by final Submit. Step 3 only
    // validates the conditional Parent field (Structure is otherwise all-optional); steps 4/5 have nothing required.
    const validateStep = (stepNum, surfaceErrors) => {
        let ok = true;
        (STEP_REQUIRED[stepNum] || []).forEach((id) => {
            const el = byId(id);
            if (!isFilled(id)) { ok = false; if (surfaceErrors && el) el.classList.add('is-invalid'); }
            else if (el) { el.classList.remove('is-invalid'); }
        });
        if (stepNum === 3) {
            const parentMissing = isParentRequired() && !isFilled('leParentLegalEntityId');
            const parentEl = byId('leParentLegalEntityId');
            if (parentMissing) { ok = false; if (surfaceErrors && parentEl) parentEl.classList.add('is-invalid'); }
            else if (parentEl) { parentEl.classList.remove('is-invalid'); }
        }
        return ok;
    };

    const stepErrorMessage = () => {
        const parentMissing = isParentRequired() && !isFilled('leParentLegalEntityId');
        return parentMissing
            ? (L.ParentRequiredForBranch || L.RequiredField || 'Required fields are missing.')
            : (L.RequiredField || 'Required fields are missing.');
    };

    const validateAll = (surfaceErrors) => {
        let ok = true;
        [1, 2, 3].forEach((n) => { if (!validateStep(n, surfaceErrors)) ok = false; });
        if (surfaceErrors && !ok) showAlert([stepErrorMessage()], 'danger');
        return ok;
    };

    const firstInvalidStep = () => [1, 2, 3].find((n) => !validateStep(n, false)) || null;

    // İŞ1 — forward navigation is gated: a step's trigger stays disabled until every step before it has been
    // reached (bs-stepper's own click listener is left non-linear so completed steps can be revisited freely).
    const updateStepGating = () => {
        document.querySelectorAll('#le-wizard-stepper .step').forEach((stepEl, idx) => {
            const trigger = stepEl.querySelector('.step-trigger');
            if (trigger) trigger.disabled = idx + 1 > maxReachedStep;
        });
    };

    // ─── Collect WriteRequest payload (flat, camelCase) ──────────────────────
    // Only the surfaced identity + statutory fields are sent; deferred fields are omitted and the server applies
    // its defaults (OrganizationRole=LEGALENTITY, no address). Dates go as ISO instants at UTC midnight.
    const valueOrNull = (id) => { const v = trim(byId(id)?.value); return v.length ? v : null; };
    const dateOrNull = (id) => { const v = dateValue(id); return v.length ? `${v}T00:00:00Z` : null; };
    const numberOrNull = (id) => { const v = trim(byId(id)?.value); if (!v.length) return null; const n = Number(v); return Number.isNaN(n) ? null : n; };

    // İŞ3 — build an address JSON string from a subform's inputs (data-addr="<name>"). Returns null when EVERY field
    // is empty, so an untouched optional address is omitted. Only non-empty keys are included; the backend validator
    // enforces line1/city/country when a registered address IS supplied.
    const ADDR_FIELDS = ['line1', 'city', 'state', 'postalCode', 'country']; // line1 is now a multi-line textarea
    const buildAddressJson = (name) => {
        const obj = {};
        let any = false;
        ADDR_FIELDS.forEach((f) => {
            const el = document.querySelector(`[data-addr="${name}"][data-addr-field="${f}"]`);
            const v = trim(el?.value);
            if (v.length) { obj[f] = v; any = true; }
        });
        return any ? JSON.stringify(obj) : null;
    };

    const collectPayload = () => ({
        code: trim(byId('leCode')?.value),
        legalName: trim(byId('leLegalName')?.value),
        displayName: valueOrNull('leDisplayName'),
        legalFormCode: valueOrNull('leLegalFormCode'),
        registrationNumber: valueOrNull('leRegistrationNumber'),
        taxId: valueOrNull('leTaxId'),
        vatNumber: valueOrNull('leVatNumber'),
        placeOfIncorporation: valueOrNull('lePlaceOfIncorporation'),
        incorporationDate: dateOrNull('leIncorporationDate'),
        dissolutionDate: dateOrNull('leDissolutionDate'),
        countryCode: valueOrNull('leCountryCode'),
        statutoryStatus: byId('leStatutoryStatus')?.value || 'Registered',
        baseCurrencyCode: valueOrNull('leBaseCurrencyCode'),
        // İŞ3 Structure (all optional); İŞA organizationRoleCode (blank → backend defaults LEGALENTITY)
        organizationRoleCode: valueOrNull('leOrganizationRoleCode'),
        parentLegalEntityId: valueOrNull('leParentLegalEntityId'),
        ownershipPercent: numberOrNull('leOwnershipPercent'),
        controlTypeCode: valueOrNull('leControlTypeCode'),
        // İŞ3 Finance (all optional)
        fiscalYearVariant: valueOrNull('leFiscalYearVariant'),
        accountingStandardCode: valueOrNull('leAccountingStandardCode'),
        taxRegimeCode: valueOrNull('leTaxRegimeCode'),
        // İŞ3 Addresses & Contacts (all optional)
        registeredAddressJson: buildAddressJson('registered'),
        correspondenceAddressJson: buildAddressJson('correspondence'),
        officialEmail: valueOrNull('leOfficialEmail'),
        officialPhone: valueOrNull('leOfficialPhone'),
        website: valueOrNull('leWebsite')
    });

    // ─── Save (POST create / PUT edit) ───────────────────────────────────────
    const save = async () => {
        if (!validateAll(true)) {
            const bad = firstInvalidStep();
            if (bad) stepper?.to(bad); // jump the wizard back to the first step with a missing required field
            return;
        }
        showAlert(null);
        const payload = collectPayload();
        const url = isEdit ? `${endpoint}/${encodeURIComponent(entityId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';

        const btn = byId('le-submit');
        if (btn) btn.disabled = true;
        try {
            const res = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() },
                body: JSON.stringify(payload)
            });
            if (res.ok) {
                try { sessionStorage.setItem('le-toast', isEdit ? (L.RecordUpdated || '') : (L.RecordCreated || '')); } catch { /* ignore */ }
                window.location.href = '/LegalEntities';
                return;
            }
            let errors = [];
            try { errors = extractErrors(await res.json()); } catch { /* non-JSON */ }
            showAlert(errors.length ? errors : [L.ErrorOccurred || 'An error occurred.'], 'danger');
        } catch (error) {
            console.error('[LE Wizard] Save failed.', error);
            showAlert([L.ErrorOccurred || 'An error occurred.'], 'danger');
        } finally {
            if (btn) btn.disabled = false;
        }
    };

    // ─── Pre-populate on edit ────────────────────────────────────────────────
    const setVal = (id, v) => { const el = byId(id); if (el && v != null) el.value = v; };
    const titleCase = (s) => { const v = String(s || '').toLowerCase(); return v.charAt(0).toUpperCase() + v.slice(1); };

    // İŞ3 — restore an address subform from its stored JSON (keys matched case-insensitively).
    const fillAddressForm = (name, json) => {
        if (!json) return;
        let obj;
        try { obj = JSON.parse(json); } catch { return; }
        if (!obj || typeof obj !== 'object') return;
        ADDR_FIELDS.forEach((f) => {
            const el = document.querySelector(`[data-addr="${name}"][data-addr-field="${f}"]`);
            const val = obj[f] ?? obj[f.charAt(0).toUpperCase() + f.slice(1)];
            if (el && val != null) el.value = val;
        });
    };

    const populate = (d) => {
        setVal('leCode', d.code);
        setVal('leLegalName', d.legalName);
        setVal('leDisplayName', d.displayName);
        setVal('leLegalFormCode', d.legalFormCode);
        setVal('leRegistrationNumber', d.registrationNumber);
        setVal('leTaxId', d.taxId);
        setVal('leVatNumber', d.vatNumber);
        setVal('lePlaceOfIncorporation', d.placeOfIncorporation);
        setVal('leCountryCode', d.countryCode);
        if (d.statutoryStatus) byId('leStatutoryStatus').value = titleCase(d.statutoryStatus);
        setDate('leIncorporationDate', d.incorporationDate);
        setDate('leDissolutionDate', d.dissolutionDate);
        setVal('leBaseCurrencyCode', d.baseCurrencyCode);
        // İŞ3 — Structure / Finance / Addresses restore
        setVal('leOrganizationRoleCode', d.organizationRoleCode);
        setVal('leParentLegalEntityId', d.parentLegalEntityId);
        setVal('leOwnershipPercent', d.ownershipPercent);
        setVal('leControlTypeCode', d.controlTypeCode);
        setVal('leFiscalYearVariant', d.fiscalYearVariant);
        setVal('leAccountingStandardCode', d.accountingStandardCode);
        setVal('leTaxRegimeCode', d.taxRegimeCode);
        fillAddressForm('registered', d.registeredAddressJson);
        fillAddressForm('correspondence', d.correspondenceAddressJson);
        setVal('leOfficialEmail', d.officialEmail);
        setVal('leOfficialPhone', d.officialPhone);
        setVal('leWebsite', d.website);
        applyParentRequirement(); // İŞA — reflect the restored role's parent requirement
    };

    const loadForEdit = async () => {
        try {
            const res = await fetch(`${endpoint}/${encodeURIComponent(entityId)}`, { headers: getAuthHeaders() });
            if (!res.ok) throw new Error('load failed');
            const payload = await res.json();
            populate(payload.data || payload.Data || {});
        } catch (error) {
            console.error('[LE Wizard] Load for edit failed.', error);
            showAlert([L.ErrorOccurred || ''], 'danger');
        }
    };

    // ─── Read-only Overview (step 6) ─────────────────────────────────────────
    // Show the human-readable value for a field: select → selected option's label; date → the localized altInput
    // text; everything else → the raw trimmed value.
    const displayValue = (id) => {
        const el = byId(id);
        if (!el) return '';
        if (el.tagName === 'SELECT') {
            const opt = el.options[el.selectedIndex];
            return opt && opt.value ? trim(opt.textContent) : '';
        }
        if (DATE_FIELDS.includes(id)) {
            return el._flatpickr && el._flatpickr.altInput ? trim(el._flatpickr.altInput.value) : trim(el.value);
        }
        return trim(el.value);
    };

    // Build a one-line address string from a subform (line / city / state / postal / country), skipping blanks.
    const formatAddress = (name) => {
        const parts = ADDR_FIELDS.map((f) => {
            const el = document.querySelector(`[data-addr="${name}"][data-addr-field="${f}"]`);
            return trim(el?.value).replace(/\s*\n\s*/g, ' '); // flatten the textarea's newlines
        }).filter(Boolean);
        return parts.join(', ');
    };

    const populateOverview = () => {
        const dash = '—';
        document.querySelectorAll('#le-overview [data-ov]').forEach((node) => {
            node.textContent = displayValue(node.dataset.ov) || dash;
        });
        document.querySelectorAll('#le-overview [data-ov-addr]').forEach((node) => {
            node.textContent = formatAddress(node.dataset.ovAddr) || dash;
        });
    };

    // ─── Buttons: Prev/Next sit BELOW the content card; Submit lives in the header next to Cancel and is only
    // enabled on the final Overview step (disabled the rest of the time). ────────────────────────────────────
    const updateActionButtons = () => {
        const prev = byId('le-prev'), next = byId('le-next'), submit = byId('le-submit');
        if (prev) prev.style.display = currentStep > 1 ? '' : 'none';
        if (next) next.style.display = currentStep < TOTAL_STEPS ? '' : 'none';
        if (submit) submit.disabled = currentStep !== TOTAL_STEPS;
    };

    // On the Overview step the content card goes flush (transparent) so each section renders as its own card on
    // the page body — steps 1-5 keep the normal single form card.
    const updateContentChrome = () => {
        byId('le-content-card')?.classList.toggle('le-content-flush', currentStep === TOTAL_STEPS);
    };

    // İŞ1 — Next only advances once the step it's leaving validates; Previous is always allowed. Overview (step 6)
    // is read-only, so entering it just refreshes the summary. bs-stepper step-trigger clicks are disabled per
    // updateStepGating for steps beyond maxReachedStep.
    const goNext = () => {
        if (!validateStep(currentStep, true)) { showAlert([stepErrorMessage()], 'danger'); return; }
        showAlert(null);
        maxReachedStep = Math.max(maxReachedStep, currentStep + 1);
        updateStepGating();
        stepper?.next();
    };

    const initStepper = () => {
        const el = byId('le-wizard-stepper');
        if (!el || typeof window.Stepper !== 'function') return;
        stepper = new window.Stepper(el, { linear: false, animation: false });
        // Safety net: block any programmatic/-click jump past the furthest validated step.
        el.addEventListener('show.bs-stepper', (event) => {
            if (event.detail.indexStep + 1 > maxReachedStep) event.preventDefault();
        });
        // Keep currentStep + the action bar in sync, and refresh the overview whenever step 6 becomes visible.
        el.addEventListener('shown.bs-stepper', (event) => {
            currentStep = event.detail.indexStep + 1;
            updateActionButtons();
            updateContentChrome();
            if (currentStep === TOTAL_STEPS) populateOverview();
        });
        updateStepGating();
        updateActionButtons();
        updateContentChrome();
    };

    const bindEvents = () => {
        byId('le-submit')?.addEventListener('click', () => save());
        byId('le-next')?.addEventListener('click', () => goNext());
        byId('le-prev')?.addEventListener('click', () => stepper?.previous());
        REQUIRED.forEach((id) => {
            const el = byId(id);
            if (el) ['input', 'change'].forEach((ev) => el.addEventListener(ev, () => el.classList.remove('is-invalid')));
        });
        // İŞA — re-evaluate the Parent requirement whenever the org role changes (native listener; select2 also calls it).
        byId('leOrganizationRoleCode')?.addEventListener('change', applyParentRequirement);
    };

    // MOD-0288 Faz-A #4 — make the identity/statutory lookups searchable (select2). Init AFTER options are filled
    // and (on edit) values set, so the current selection renders. select2 keeps the native <select> value in sync,
    // so validation/collectPayload keep reading `.value` unchanged.
    const initSelect2 = () => {
        const jq = window.jQuery;
        if (!jq || !jq.fn || !jq.fn.select2) return;
        // İŞ3/İŞA — the Structure/Finance lookups (incl. the searchable Parent + org-role selects) are select2 too.
        const selects = '#leLegalFormCode, #leCountryCode, #leBaseCurrencyCode, #leOrganizationRoleCode, #leParentLegalEntityId, #leControlTypeCode, #leAccountingStandardCode, #leTaxRegimeCode';
        // Pass each select's own empty first-option text as the select2 placeholder so the unselected state renders
        // in the muted placeholder colour (matching the text inputs) instead of the darker option-text colour.
        jq(selects).each(function () {
            const ph = jq(this).find('option[value=""]').first().text() || (L.SelectOption || '');
            jq(this).select2({ width: '100%', placeholder: ph, minimumResultsForSearch: 0 });
        });
        jq(selects).on('change', function () {
            this.classList.remove('is-invalid');
            applyParentRequirement(); // İŞA — role change may toggle the Parent requirement
        });
    };

    const init = async () => {
        loadL10n();
        initStepper();
        initDatePickers();
        bindEvents();
        await loadLookups();
        if (isEdit) {
            await loadForEdit();
            maxReachedStep = TOTAL_STEPS; // editing an existing record — all steps already populated, don't re-force linear entry
            updateStepGating();
        }
        initSelect2();
        applyParentRequirement(); // reflect the current role's Parent requirement on the shared required tracker
        updateActionButtons();
    };

    init();
})();
