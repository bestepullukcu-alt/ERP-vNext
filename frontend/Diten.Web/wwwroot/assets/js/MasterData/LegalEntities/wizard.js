'use strict';

// MOD-0220 — Legal Entity 8-step create/edit wizard (Görev 5). Full-page bs-stepper. Plain fetch AJAX
// (consistent with index.js). Lookups are fetched through the Diten.Web proxy (/LegalEntities/api/lookups
// and /api/platform-lookups) so the HttpOnly token is attached server-side. Create POSTs to
// /LegalEntities/api (operationalStatus is server-default Draft); Edit PUTs to /LegalEntities/api/{id}.
(function () {
    const page = document.getElementById('le-wizard-page');
    if (!page) return;

    const endpoint = '/LegalEntities/api';
    const entityId = page.dataset.leId || '';
    const isEdit = page.dataset.leMode === 'edit';
    let L = {};
    let stepper = null;
    let currentIndex = 0;
    let selfList = []; // for parent dropdown

    const byId = (id) => document.getElementById(id);
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const trim = (v) => (typeof v === 'string' ? v.trim() : '');

    const loadL10n = () => {
        const node = byId('legal-entities-wizard-l10n');
        if (!node) return;
        try {
            const raw = JSON.parse(node.textContent || '{}');
            Object.keys(raw).forEach((k) => { L[k] = raw[k]; });
        } catch (e) { console.error('[LE Wizard] L10n parse failed.', e); }
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

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    // ─── Lookups (all via Diten.Web proxy) ───────────────────────────────────
    const fillSelect = (selectId, items, { keepFirst = true } = {}) => {
        const select = byId(selectId);
        if (!select) return;
        const first = keepFirst ? select.querySelector('option') : null;
        select.innerHTML = '';
        if (first) select.appendChild(first);
        (items || []).forEach((it) => {
            const code = it.code ?? it.Code ?? it.value ?? it.Value;
            const name = it.name ?? it.Name ?? code;
            if (code == null) return;
            const opt = document.createElement('option');
            opt.value = code;
            opt.textContent = name;
            select.appendChild(opt);
        });
    };

    const fetchLookup = (type) => fetch(`${endpoint}/lookups/${encodeURIComponent(type)}`, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r)).then(unwrapList).catch(() => []);
    const fetchPlatformLookup = (key) => fetch(`${endpoint}/platform-lookups/${encodeURIComponent(key)}`, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r)).then(unwrapList).catch(() => []);
    const fetchSelfList = () => fetch(endpoint, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r)).then(unwrapList).catch(() => []);

    const loadLookups = async () => {
        const [legalForm, orgRole, controlType, accStandard, taxRegime, countries, currencies, self] = await Promise.all([
            fetchLookup('legal-form'),
            fetchLookup('organization-role'),
            fetchLookup('control-type'),
            fetchLookup('accounting-standard'),
            fetchLookup('tax-regime'),
            fetchPlatformLookup('countries'),
            fetchPlatformLookup('currencies'),
            fetchSelfList()
        ]);

        fillSelect('leLegalFormCode', legalForm);
        fillSelect('leOrganizationRoleCode', orgRole);
        fillSelect('leControlTypeCode', controlType);
        fillSelect('leAccountingStandardCode', accStandard);
        fillSelect('leTaxRegimeCode', taxRegime);
        fillSelect('leCountryCode', countries);
        fillSelect('leBaseCurrencyCode', currencies);

        // Parent self list — exclude self when editing.
        selfList = (self || []).filter((e) => {
            const id = e.legalEntityId || e.LegalEntityId || e.id || e.Id;
            return !(isEdit && String(id) === String(entityId));
        });
        const parentSelect = byId('leParentLegalEntityId');
        if (parentSelect) {
            selfList.forEach((e) => {
                const id = e.legalEntityId || e.LegalEntityId || e.id || e.Id;
                const name = e.legalName || e.LegalName || '';
                const code = e.code || e.Code || '';
                if (!id) return;
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = code ? `${code} — ${name}` : name;
                parentSelect.appendChild(opt);
            });
        }
    };

    // ─── Branch/RepOffice → parent required ──────────────────────────────────
    const roleNeedsParent = () => {
        const role = String(byId('leOrganizationRoleCode')?.value || '').toUpperCase();
        return role === 'BRANCH' || role === 'REPOFFICE';
    };

    const refreshParentRequirement = () => {
        const needs = roleNeedsParent();
        const star = byId('leParentRequiredStar');
        const select = byId('leParentLegalEntityId');
        const hint = byId('leParentHint');
        if (star) star.style.display = needs ? '' : 'none';
        if (select) { if (needs) select.setAttribute('data-required', ''); else select.removeAttribute('data-required'); }
        if (hint) hint.textContent = needs ? (L.ParentRequiredForBranch || '') : '';
        updateRequiredCounter();
    };

    // ─── Required tracking ───────────────────────────────────────────────────
    // Hard-required: code, legalName, legalFormCode, organizationRoleCode, countryCode,
    // baseCurrencyCode, registeredAddressJson, + parentLegalEntityId when role is Branch/RepOffice.
    const requiredFieldIds = () => {
        const ids = ['leCode', 'leLegalName', 'leLegalFormCode', 'leOrganizationRoleCode', 'leCountryCode', 'leBaseCurrencyCode', 'leRegisteredAddressJson'];
        if (roleNeedsParent()) ids.push('leParentLegalEntityId');
        return ids;
    };

    const isFilled = (id) => trim(byId(id)?.value).length > 0;

    const updateRequiredCounter = () => {
        const ids = requiredFieldIds();
        const filled = ids.filter(isFilled).length;
        // Text lives in a dedicated span so the badge icon is preserved.
        const counter = byId('le-required-counter-text') || byId('le-required-counter');
        if (counter) counter.textContent = `${L.RequiredCounter || 'Required'} ${filled}/${ids.length}`;
        const badge = byId('le-required-counter');
        if (badge) {
            const done = filled === ids.length;
            badge.classList.toggle('bg-label-success', done);
            badge.classList.toggle('bg-label-primary', !done);
        }
    };

    // ─── Address JSON validation ─────────────────────────────────────────────
    const parseAddressJson = (raw) => {
        const text = trim(raw);
        if (!text) return { ok: false };
        try {
            const obj = JSON.parse(text);
            if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return { ok: false };
            return { ok: true, obj };
        } catch { return { ok: false }; }
    };

    const registeredAddressValid = () => {
        const r = parseAddressJson(byId('leRegisteredAddressJson')?.value);
        if (!r.ok) return false;
        return !!(trim(r.obj.line1) && trim(r.obj.city) && trim(r.obj.country));
    };

    // ─── Per-step validation ─────────────────────────────────────────────────
    // Map each step (index 0-7) to the required field ids it owns.
    const stepRequired = {
        0: ['leCode', 'leLegalName', 'leLegalFormCode', 'leOrganizationRoleCode'],
        1: ['leCountryCode'],
        2: [], // parent conditionally required — handled below
        3: ['leBaseCurrencyCode'],
        4: ['leRegisteredAddressJson'],
        5: [], 6: [], 7: []
    };

    const validateStep = (index, surfaceErrors) => {
        const ids = (stepRequired[index] || []).slice();
        if (index === 2 && roleNeedsParent()) ids.push('leParentLegalEntityId');
        let ok = true;
        const errors = [];
        ids.forEach((id) => {
            const el = byId(id);
            const filled = isFilled(id);
            if (!filled) {
                ok = false;
                if (el && surfaceErrors) el.classList.add('is-invalid');
            } else if (el) {
                el.classList.remove('is-invalid');
            }
        });
        // Step 5 (index 4): registered address must be valid JSON with required keys.
        if (index === 4 && isFilled('leRegisteredAddressJson') && !registeredAddressValid()) {
            ok = false;
            if (surfaceErrors) {
                byId('leRegisteredAddressJson')?.classList.add('is-invalid');
                errors.push(L.InvalidAddressJson || 'Invalid address JSON.');
            }
        }
        if (index === 2 && roleNeedsParent() && !isFilled('leParentLegalEntityId') && surfaceErrors) {
            errors.push(L.ParentRequiredForBranch || '');
        }
        if (surfaceErrors) {
            if (!ok) showAlert(errors.length ? errors : [L.RequiredField || 'Required fields are missing.'], 'danger');
            else showAlert(null);
        }
        return ok;
    };

    // ─── Collect WriteRequest payload (flat, camelCase) ──────────────────────
    const valueOrNull = (id) => { const v = trim(byId(id)?.value); return v.length ? v : null; };

    const collectPayload = () => {
        const ownershipRaw = trim(byId('leOwnershipPercent')?.value);
        const reviewRaw = trim(byId('leReviewDueUtc')?.value);
        const corr = trim(byId('leCorrespondenceAddressJson')?.value);

        return {
            code: trim(byId('leCode')?.value),
            legalName: trim(byId('leLegalName')?.value),
            displayName: valueOrNull('leDisplayName'),
            legalFormCode: valueOrNull('leLegalFormCode'),
            organizationRoleCode: valueOrNull('leOrganizationRoleCode'),
            registrationNumber: valueOrNull('leRegistrationNumber'),
            taxId: valueOrNull('leTaxId'),
            countryCode: valueOrNull('leCountryCode'),
            statutoryStatus: byId('leStatutoryStatus')?.value || 'Registered',
            parentLegalEntityId: valueOrNull('leParentLegalEntityId'),
            ownershipPercent: ownershipRaw.length ? Number(ownershipRaw) : null,
            controlTypeCode: valueOrNull('leControlTypeCode'),
            fiscalYearVariant: valueOrNull('leFiscalYearVariant'),
            accountingStandardCode: valueOrNull('leAccountingStandardCode'),
            taxRegimeCode: valueOrNull('leTaxRegimeCode'),
            baseCurrencyCode: valueOrNull('leBaseCurrencyCode'),
            registeredAddressJson: trim(byId('leRegisteredAddressJson')?.value) || null,
            correspondenceAddressJson: corr.length ? corr : null,
            officialEmail: valueOrNull('leOfficialEmail'),
            officialPhone: valueOrNull('leOfficialPhone'),
            website: valueOrNull('leWebsite'),
            approvalStatus: byId('leApprovalStatus')?.value || 'Draft',
            reviewDueUtc: reviewRaw.length ? new Date(reviewRaw + 'T00:00:00Z').toISOString() : null,
            sourceSystem: valueOrNull('leSourceSystem'),
            legacyCode: valueOrNull('leLegacyCode'),
            evidenceStatus: byId('leEvidenceStatus')?.value || 'NotStarted'
        };
    };

    // Full validation across all hard-required fields (for Save Draft / Submit).
    const validateAll = (surfaceErrors) => {
        let ok = true;
        const errors = [];
        requiredFieldIds().forEach((id) => {
            if (!isFilled(id)) {
                ok = false;
                if (surfaceErrors) byId(id)?.classList.add('is-invalid');
            }
        });
        if (isFilled('leRegisteredAddressJson') && !registeredAddressValid()) {
            ok = false;
            if (surfaceErrors) { byId('leRegisteredAddressJson')?.classList.add('is-invalid'); errors.push(L.InvalidAddressJson || ''); }
        }
        if (roleNeedsParent() && !isFilled('leParentLegalEntityId')) {
            if (surfaceErrors) errors.push(L.ParentRequiredForBranch || '');
        }
        if (surfaceErrors && !ok) showAlert(errors.length ? errors : [L.RequiredField || 'Required fields are missing.'], 'danger');
        return ok;
    };

    // ─── Save (POST create / PUT edit) ───────────────────────────────────────
    const save = async (triggerBtn) => {
        if (!validateAll(true)) { stepper?.to(0); return; }
        showAlert(null);
        const payload = collectPayload();
        const url = isEdit ? `${endpoint}/${encodeURIComponent(entityId)}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';

        const buttons = ['le-save-draft', 'le-submit', 'le-next'].map(byId).filter(Boolean);
        buttons.forEach((b) => { b.disabled = true; });
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
            try { const json = await res.json(); errors = (json.errors || json.Errors || []); } catch { /* non-JSON */ }
            showAlert(errors.length ? errors : [L.ErrorOccurred || 'An error occurred.'], 'danger');
        } catch (error) {
            console.error('[LE Wizard] Save failed.', error);
            showAlert([L.ErrorOccurred || 'An error occurred.'], 'danger');
        } finally {
            buttons.forEach((b) => { b.disabled = false; });
        }
    };

    // ─── Review summary ──────────────────────────────────────────────────────
    const labelOf = (selectId) => {
        const el = byId(selectId);
        if (!el) return '-';
        if (el.tagName === 'SELECT') return el.options[el.selectedIndex]?.textContent?.trim() || '-';
        return trim(el.value) || '-';
    };

    const buildReview = () => {
        const host = byId('le-review-summary');
        if (!host) return;

        // Golden Reference Compact "view" design — backbone-preview-section cards.
        const field = (icon, label, value, col) => `
            <div class="${col || 'col-12 col-md-6'}">
                <div class="backbone-preview-field">
                    <i class="bx ${icon}"></i>
                    <div>
                        <div class="backbone-preview-label">${escapeHtml(label || '')}</div>
                        <div class="backbone-preview-value mt-1">${escapeHtml(value || '-')}</div>
                    </div>
                </div>
            </div>`;
        const section = (title, fields) => `
            <section class="card backbone-preview-section p-4">
                <h6 class="text-uppercase text-heading fw-semibold mb-4">${escapeHtml(title)}</h6>
                <div class="row g-4">${fields}</div>
            </section>`;

        const identity = section(L.SectionIdentity || 'Identity',
            field('bx-purchase-tag-alt', L.Code, labelOf('leCode')) +
            field('bx-file', L.LegalName, labelOf('leLegalName')) +
            field('bx-rename', L.DisplayName, labelOf('leDisplayName')) +
            field('bx-been-here', L.LegalForm, labelOf('leLegalFormCode')) +
            field('bx-been-here', L.OrgRole, labelOf('leOrganizationRoleCode')));

        const statutory = section(L.SectionStatutory || 'Statutory & Tax',
            field('bx-id-card', L.RegistrationNumber, labelOf('leRegistrationNumber')) +
            field('bx-receipt', L.TaxId, labelOf('leTaxId')) +
            field('bx-map', L.Country, labelOf('leCountryCode')) +
            field('bx-check-shield', L.StatutoryStatus, labelOf('leStatutoryStatus')));

        const finance = section(L.SectionFinance || 'Finance',
            field('bx-calendar', L.FiscalYearVariant, labelOf('leFiscalYearVariant')) +
            field('bx-calculator', L.AccountingStandard, labelOf('leAccountingStandardCode')) +
            field('bx-receipt', L.TaxRegime, labelOf('leTaxRegimeCode')) +
            field('bx-dollar-circle', L.BaseCurrency, labelOf('leBaseCurrencyCode')));

        const addresses = section(L.SectionAddresses || 'Addresses & Contacts',
            field('bx-map-pin', L.RegisteredAddress, labelOf('leRegisteredAddressJson'), 'col-12') +
            field('bx-envelope-open', L.CorrespondenceAddress, labelOf('leCorrespondenceAddressJson'), 'col-12') +
            field('bx-envelope', L.OfficialEmail, labelOf('leOfficialEmail')) +
            field('bx-phone', L.OfficialPhone, labelOf('leOfficialPhone')) +
            field('bx-globe', L.Website, labelOf('leWebsite')));

        const structure = section(L.SectionStructure || 'Structure',
            field('bx-sitemap', L.ParentLegalEntity, labelOf('leParentLegalEntityId'), 'col-12') +
            field('bx-pie-chart-alt', L.OwnershipPercent, labelOf('leOwnershipPercent'), 'col-12') +
            field('bx-slider-alt', L.ControlType, labelOf('leControlTypeCode'), 'col-12'));

        const governance = section(L.SectionGovernance || 'Governance',
            field('bx-check-shield', L.ApprovalStatus, labelOf('leApprovalStatus'), 'col-12') +
            field('bx-calendar-event', L.ReviewDue, labelOf('leReviewDueUtc'), 'col-12') +
            field('bx-data', L.SourceSystem, labelOf('leSourceSystem'), 'col-12') +
            field('bx-code-alt', L.LegacyCode, labelOf('leLegacyCode'), 'col-12'));

        const evidence = section(L.SectionEvidence || 'Evidence',
            field('bx-file-find', L.EvidenceStatus, labelOf('leEvidenceStatus'), 'col-12'));

        host.innerHTML = `
            <div class="row g-4">
                <div class="col-12 col-lg-8 d-flex flex-column gap-4">${identity}${statutory}${finance}${addresses}</div>
                <div class="col-12 col-lg-4 d-flex flex-column gap-4">${structure}${governance}${evidence}</div>
            </div>`;
    };

    // ─── Pre-populate on edit ────────────────────────────────────────────────
    const toDateInput = (iso) => { if (!iso) return ''; const d = new Date(iso); return Number.isNaN(d.getTime()) ? '' : d.toISOString().slice(0, 10); };
    const setVal = (id, v) => { const el = byId(id); if (el && v != null) el.value = v; };

    const populate = (d) => {
        setVal('leCode', d.code);
        setVal('leLegalName', d.legalName);
        setVal('leDisplayName', d.displayName);
        setVal('leLegalFormCode', d.legalFormCode);
        setVal('leOrganizationRoleCode', d.organizationRoleCode);
        setVal('leRegistrationNumber', d.registrationNumber);
        setVal('leTaxId', d.taxId);
        setVal('leCountryCode', d.countryCode);
        if (d.statutoryStatus) byId('leStatutoryStatus').value = titleCase(d.statutoryStatus);
        setVal('leParentLegalEntityId', d.parentLegalEntityId);
        if (d.ownershipPercent != null) setVal('leOwnershipPercent', d.ownershipPercent);
        setVal('leControlTypeCode', d.controlTypeCode);
        setVal('leFiscalYearVariant', d.fiscalYearVariant);
        setVal('leAccountingStandardCode', d.accountingStandardCode);
        setVal('leTaxRegimeCode', d.taxRegimeCode);
        setVal('leBaseCurrencyCode', d.baseCurrencyCode);
        setVal('leRegisteredAddressJson', prettyJson(d.registeredAddressJson));
        setVal('leCorrespondenceAddressJson', prettyJson(d.correspondenceAddressJson));
        setVal('leOfficialEmail', d.officialEmail);
        setVal('leOfficialPhone', d.officialPhone);
        setVal('leWebsite', d.website);
        if (d.approvalStatus) byId('leApprovalStatus').value = titleCase(d.approvalStatus);
        setVal('leReviewDueUtc', toDateInput(d.reviewDueUtc));
        setVal('leSourceSystem', d.sourceSystem);
        setVal('leLegacyCode', d.legacyCode);
        if (d.evidenceStatus) byId('leEvidenceStatus').value = evidenceTitleCase(d.evidenceStatus);
        if (d.completenessScore != null) {
            byId('leCompletenessWrap').style.display = '';
            byId('leCompletenessScore').value = `${d.completenessScore}%`;
        }
        refreshParentRequirement();
        updateRequiredCounter();
    };

    // Map UPPERCASE read enum → PascalCase select option value.
    const titleCase = (s) => { const v = String(s || '').toLowerCase(); return v.charAt(0).toUpperCase() + v.slice(1); };
    const evidenceTitleCase = (s) => {
        const map = { NOTSTARTED: 'NotStarted', COMPLETE: 'Complete', VERIFIED: 'Verified' };
        return map[String(s || '').toUpperCase()] || 'NotStarted';
    };
    const prettyJson = (raw) => {
        if (!raw) return '';
        try { return JSON.stringify(typeof raw === 'string' ? JSON.parse(raw) : raw, null, 2); } catch { return typeof raw === 'string' ? raw : ''; }
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

    // ─── Stepper wiring ──────────────────────────────────────────────────────
    const totalSteps = 8;

    const updateNavButtons = (index) => {
        const isLast = index === totalSteps - 1;
        byId('le-next')?.classList.toggle('d-none', isLast);
        byId('le-submit')?.classList.toggle('d-none', !isLast);
    };

    const initStepper = () => {
        const el = byId('le-wizard-stepper');
        if (!el || !window.Stepper) return;
        stepper = new window.Stepper(el, { linear: false, animation: false });
        el.addEventListener('shown.bs-stepper', (event) => {
            const to = event.detail?.to ?? 0;
            currentIndex = to;
            updateNavButtons(to);
            if (to === totalSteps - 1) buildReview();
        });
        updateNavButtons(0);
    };

    const bindEvents = () => {
        byId('le-next')?.addEventListener('click', () => {
            if (!validateStep(currentIndex, true)) return;
            stepper?.next();
        });
        byId('le-prev')?.addEventListener('click', () => stepper?.previous());
        byId('le-submit')?.addEventListener('click', (e) => save(e.currentTarget));
        byId('le-save-draft')?.addEventListener('click', (e) => save(e.currentTarget));

        byId('leOrganizationRoleCode')?.addEventListener('change', refreshParentRequirement);
        // Live required-counter updates on any required input.
        ['leCode', 'leLegalName', 'leLegalFormCode', 'leOrganizationRoleCode', 'leCountryCode', 'leBaseCurrencyCode', 'leRegisteredAddressJson', 'leParentLegalEntityId']
            .forEach((id) => { const el = byId(id); if (el) ['input', 'change'].forEach((ev) => el.addEventListener(ev, () => { el.classList.remove('is-invalid'); updateRequiredCounter(); })); });
    };

    const init = async () => {
        loadL10n();
        initStepper();
        bindEvents();
        await loadLookups();
        if (isEdit) await loadForEdit();
        refreshParentRequirement();
        updateRequiredCounter();
    };

    init();
})();
