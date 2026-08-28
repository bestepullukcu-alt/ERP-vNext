(function (window, document) {
    'use strict';
    const L = window.ConsentPreferenceL10n || {};
    const base = '/CRM/ConsentPreferences';
    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = value => String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const badge = (value, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(value || '—')}</span>`;
    const date = value => value ? new Date(value).toLocaleString() : '—';
    // Effective-window display: "Aug 03, 26" over "05:04 PM".
    const dtStamp = value => {
        if (!value) return '<span class="text-muted">—</span>';
        const d = new Date(value);
        if (isNaN(d.getTime())) return esc(value);
        const dp = d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: '2-digit' });
        const tp = d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true });
        return `<div class="text-nowrap">${esc(dp)}</div><div class="text-muted small text-nowrap">${esc(tp)}</div>`;
    };
    const shortId = value => { const s = String(value || ''); return s.length > 10 ? s.slice(0, 8) + '…' : s; };
    // Icon + label cell (Golden Slim heading style). Used for the Subject Type / Preference Type columns.
    const iconCell = (text, icon, color) => `<span class="text-truncate d-flex align-items-center text-heading"><i class="icon-base bx ${icon} ${color} me-2"></i>${esc(text || '—')}</span>`;
    const SUBJECT_TYPE_ICONS = { contact:['bx-user','text-success'], account:['bx-buildings','text-primary'], hcp:['bx-plus-medical','text-info'], hco:['bx-hospital','text-info'], 'account-contact-link':['bx-link-alt','text-secondary'] };
    const subjectTypeCell = value => { const [i, c] = SUBJECT_TYPE_ICONS[String(value || '').toLowerCase()] || ['bx-user-pin', 'text-secondary']; return iconCell(value, i, c); };
    const PREF_TYPE_ICONS = { 'do-not-contact':['bx-block','text-danger'], 'do-not-visit':['bx-block','text-danger'], 'frequency-cap':['bx-tachometer','text-warning'], channel:['bx-broadcast','text-primary'], language:['bx-globe','text-info'] };
    const preferenceTypeCell = value => { const [i, c] = PREF_TYPE_ICONS[String(value || '').toLowerCase()] || ['bx-slider-alt', 'text-secondary']; return iconCell(value, i, c); };
    // Subject display-name resolution (contact/account) so the SubjectId column shows a name, not a raw GUID.
    const subjectNames = {};
    const RESOLVABLE_SUBJECTS = ['contact', 'account'];
    const resolveSubjects = async rows => {
        const types = [...new Set((rows || []).map(r => String(r.subjectType || '').toLowerCase()).filter(t => RESOLVABLE_SUBJECTS.includes(t) && !subjectNames[t]))];
        await Promise.all(types.map(async type => {
            try {
                const res = await fetch(`${base}/api/subjects/resolve?subjectType=${encodeURIComponent(type)}`, { credentials: 'same-origin', headers: getAuthHeaders() });
                const body = await res.json().catch(() => ({}));
                const map = {};
                (body?.results || []).forEach(x => { map[String(x.id).toLowerCase()] = x.text; });
                subjectNames[type] = map;
            } catch { subjectNames[type] = {}; }
        }));
    };
    const subjectCell = row => {
        const type = String(row.subjectType || '').toLowerCase();
        const name = subjectNames[type] && subjectNames[type][String(row.subjectId || '').toLowerCase()];
        return name
            ? `<span class="fw-medium text-heading">${esc(name)}</span>`
            : `<span class="text-muted" title="${esc(row.subjectId)}">${esc(shortId(row.subjectId))}</span>`;
    };
    // Relocate an inline filter host into the DataTable toolbar (Golden Slim mountInlineFilter pattern).
    const mountInlineFilter = (hostId, api) => {
        const host = document.getElementById(hostId);
        const container = api.table().container();
        const filterBtn = container.querySelector('.dt-filter-btn');
        const row = filterBtn && (filterBtn.closest('.dt-layout-row') || filterBtn.closest('.row') || (filterBtn.closest('.dt-layout-end') || {}).parentElement);
        if (host && row) { row.insertAdjacentElement('afterend', host); host.classList.remove('px-6'); host.classList.add('px-3'); }
    };
    // Keep the Select2 filter dropdown inside the viewport (Golden Slim clampDropdown).
    const clampFilterDropdown = () => {
        requestAnimationFrame(() => {
            const dd = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
            if (!dd) return;
            const rect = dd.getBoundingClientRect(); const pad = 8; let dx = 0;
            if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
            if (rect.left < pad) dx += pad - rect.left;
            if (!dx) return;
            const cs = window.getComputedStyle(dd);
            const baseLeft = parseFloat(cs.left) || rect.left + window.scrollX;
            dd.style.left = `${baseLeft + dx}px`; dd.style.transform = 'none';
        });
    };
    // Select2-enhance the inline filter chips with the Golden Slim dropdown styling.
    const initFilterSelect2 = hostId => {
        const jq = window.jQuery;
        if (!jq || !jq.fn || !jq.fn.select2) return;
        jq(`#${hostId} select.select2`).each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: jq(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: true
            });
            $s.on('select2:open', clampFilterDropdown);
        });
    };
    // Dependent subject picker for the filter (same behavior as the Create form): contact/account are searchable by
    // name via the Gateway proxy; other types (and no match) accept a raw GUID via tags. Driven by the SubjectType chip.
    const wireFilterSubjectPicker = (typeId = 'filterConsentSubjectType', subjectId = 'filterConsentSubjectId') => {
        const jq = window.jQuery;
        const typeSel = document.getElementById(typeId);
        const subjectSel = document.getElementById(subjectId);
        if (!jq || !jq.fn || !jq.fn.select2 || !typeSel || !subjectSel || subjectSel.tagName !== 'SELECT') return;
        const pickerUrl = subjectSel.dataset.pickerUrl;
        const placeholder = subjectSel.dataset.placeholder || '';
        const PICKER_TYPES = ['contact', 'account'];
        const $subject = jq(subjectSel);
        const build = () => {
            const type = (typeSel.value || '').toLowerCase();
            if ($subject.hasClass('select2-hidden-accessible')) $subject.select2('destroy');
            $subject.empty().append(new Option('', ''));
            const opts = {
                dropdownParent: jq(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                width: 'element', allowClear: true, placeholder, tags: true,
                createTag: params => { const t = (params.term || '').trim(); return t ? { id: t, text: t } : null; }
            };
            if (PICKER_TYPES.includes(type)) {
                opts.minimumInputLength = 0;
                opts.ajax = { delay: 250, url: pickerUrl, dataType: 'json', data: params => ({ subjectType: type, search: params.term || '' }), processResults: data => ({ results: (data && data.results) || [] }) };
            }
            $subject.select2(opts);
            $subject.on('select2:open', clampFilterDropdown);
            $subject.prop('disabled', !type).trigger('change.select2');
        };
        jq(typeSel).on('change', build);
        build();
    };
    // Restrictive is DERIVED for display only (never a stored FU02 field): these preference types restrict by nature.
    const RESTRICTIVE_TYPES = ['do-not-contact', 'do-not-visit'];
    // Named priority bands over the backend's integer Priority (must match the Create/Edit form bands). An out-of-band
    // value is shown as its raw number.
    const PRIORITY_LABELS = { 1: L.PriorityHigh, 50: L.PriorityMedium, 100: L.PriorityLow };
    const PRIORITY_BADGE = { 1: 'danger', 50: 'warning', 100: 'secondary' };
    const priorityCell = value => { const n = Number(value); return badge(PRIORITY_LABELS[n] || value || '—', PRIORITY_BADGE[n] || 'secondary'); };
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };
    const setOptions = (id, valuesList, allLabel) => {
        const select = document.getElementById(id);
        if (!select) return;
        select.innerHTML = `<option value="">${esc(allLabel ?? L.Filter ?? 'All')}</option>` + (valuesList || []).map(x => `<option value="${esc(x)}">${esc(x)}</option>`).join('');
    };

    // Golden Slim "Save View": persist per-table column visibility, sort order, page length and search via the shared
    // personalizationClient (/api/personalization/*). The save button (hidden until the view differs from the saved/
    // default baseline) writes a private default view scoped to (moduleKey=CRM, pageKey). colVis/order/pageLength/search
    // only — column reordering is intentionally not persisted (its index remap is error-prone across a client redraw).
    const PERSONALIZATION_MODULE = 'CRM';
    const safeParse = s => { try { return JSON.parse(s); } catch { return null; } };
    // spec (optional): { fields:{key->inputId}, archivedId, reload } — persists the inline filter selections too, so a
    // saved view restores the applied filter (Golden Slim parity).
    const setupSaveView = (api, pageKey, spec) => {
        const pc = window.personalizationClient;
        if (!pc?.getViews) return;
        const jq = window.jQuery;
        const container = api.table().container();
        const saveBtn = container.querySelector('.dt-save-filter-btn');
        const colCount = api.columns().count();
        const idOf = r => r?.id ?? r?.Id ?? null;
        const defOf = r => { const d = r?.viewDefinition ?? r?.ViewDefinition; return typeof d === 'string' ? safeParse(d) : (d || null); };
        const nameOf = r => r?.viewName ?? r?.ViewName ?? null;
        const isDef = r => (r?.isDefault ?? r?.IsDefault) === true;
        const rec = { id: null, name: null };
        let armed = false;
        const getFilters = () => {
            if (!spec) return null;
            const f = {};
            Object.entries(spec.fields).forEach(([k, id]) => { f[k] = document.getElementById(id)?.value || ''; });
            if (spec.archivedId) f.includeArchived = !!document.getElementById(spec.archivedId)?.checked;
            return f;
        };
        const setFilters = f => {
            if (!spec || !f) return;
            Object.entries(spec.fields).forEach(([k, id]) => {
                const el = document.getElementById(id);
                if (!el) return;
                const val = f[k] ?? '';
                if (jq && el.tagName === 'SELECT') {
                    // A saved value that isn't an existing option (e.g. a subject GUID) is injected so Select2 shows it.
                    if (val && !Array.from(el.options).some(o => o.value === val)) el.appendChild(new Option(val, val, true, true));
                    jq(el).val(val).trigger('change.select2');
                } else { el.value = val; }
            });
            if (spec.archivedId) { const c = document.getElementById(spec.archivedId); if (c) c.checked = f.includeArchived !== false; }
        };
        const current = () => ({
            colVis: Array.from({ length: colCount }, (_, i) => api.column(i).visible()),
            order: api.order(),
            pageLength: api.page.len(),
            search: api.search() || '',
            filters: getFilters()
        });
        const ser = v => JSON.stringify(v || {});
        let baselineSer = ser(current());
        const setSaveVisible = v => { if (saveBtn) { saveBtn.classList.toggle('d-none', !v); window.DtDefaults?.refreshButtonGroupRadii?.(); } };
        const refreshDirty = () => { if (armed) setSaveVisible(ser(current()) !== baselineSer); };
        const applyState = s => {
            if (!s) return;
            try {
                if (Array.isArray(s.colVis)) s.colVis.forEach((vis, i) => { try { api.column(i).visible(!!vis, false); } catch { /* stale index */ } });
                if (Array.isArray(s.order) && s.order.length) api.order(s.order);
                if (s.pageLength) api.page.len(s.pageLength);
                api.search(s.search || '');
                api.columns.adjust().draw(false);
                if (spec && s.filters) { setFilters(s.filters); spec.reload?.(); }
            } catch { /* ignore */ }
        };
        const doSave = async () => {
            const view = current();
            const payload = { moduleKey: PERSONALIZATION_MODULE, pageKey, viewName: (rec.name || L.SaveView || 'Default'), viewDefinition: view, isDefault: true, visibility: 'private' };
            const resp = rec.id ? await pc.updateView(rec.id, payload) : await pc.saveView(payload);
            const saved = (resp && typeof resp === 'object') ? (resp.data ?? resp.Data ?? resp) : null;
            if (saved && typeof saved === 'object') { rec.id = idOf(saved) || rec.id; rec.name = nameOf(saved) || rec.name; }
            baselineSer = ser(view);
            setSaveVisible(false);
            window.showToast?.(L.RecordSaved || L.SaveView || 'Saved', 'success');
        };
        // The Save button (a DataTables Buttons node) fires its `action`, which resolves this handler off the container.
        container.__cpSaveView = async () => { try { await doSave(); } catch (e) { if (!e?.authHandled) { console.error('[ConsentPreference SaveView] save failed', e); window.showToast?.(e.message || L.ErrorState, 'error'); } } };
        pc.getViews(PERSONALIZATION_MODULE, pageKey).then(views => {
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            const record = Array.isArray(items) ? (items.find(isDef) || items[0] || null) : null;
            if (record) {
                rec.id = idOf(record); rec.name = nameOf(record);
                const def = defOf(record);
                if (def) { applyState(def); baselineSer = ser(current()); }
            }
        }).catch(err => { if (!err?.authHandled) console.error('[ConsentPreference SaveView] load failed', err); })
          .finally(() => { setTimeout(() => { armed = true; }, 0); });
        api.on('column-visibility.dt length.dt order.dt search.dt', refreshDirty);
        // Mark dirty when an inline filter selection changes (before Apply) — matches Golden Slim behaviour.
        if (spec) {
            Object.values(spec.fields).forEach(id => {
                const el = document.getElementById(id);
                if (!el) return;
                if (jq) jq(el).on('change', refreshDirty); else el.addEventListener('change', refreshDirty);
            });
            if (spec.archivedId) document.getElementById(spec.archivedId)?.addEventListener('change', refreshDirty);
        }
    };

    let contract = null;
    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${base}/api/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            window.ConsentPreferenceContract = contract;
            if (!contract?.isReady) throw new Error(L.ConsentContractUnavailable);
            const v = contract.vocabulary || {};
            setOptions('filterConsentSubjectType', v.subjectTypes);
            setOptions('filterConsentChannel', v.channels);
            setOptions('filterConsentPurpose', v.purposes);
            setOptions('filterConsentStatus', v.consentStatuses);
            setOptions('filterPreferenceSubjectType', v.subjectTypes);
            setOptions('filterPreferenceChannel', v.preferenceChannels);
            setOptions('filterPreferenceType', v.preferenceTypes);
            document.getElementById('btnCreateConsent')?.classList.toggle('d-none', !contract.features?.supportsConsentManagement);
            document.getElementById('btnCreatePreference')?.classList.toggle('d-none', !contract.features?.supportsPreferenceManagement);
            document.dispatchEvent(new CustomEvent('consent-preference:contract-ready', { detail: contract }));
            return true;
        } catch (error) {
            ['consentContractError', 'preferenceContractError'].forEach(id => {
                const host = document.getElementById(id);
                if (host) { host.textContent = error.message || L.ConsentContractUnavailable; host.classList.remove('d-none'); }
            });
            document.getElementById('btnCreateConsent')?.classList.add('d-none');
            document.getElementById('btnCreatePreference')?.classList.add('d-none');
            return false;
        }
    };

    // ---------------- Consent table ----------------
    const consentEl = document.getElementById('dt-consents');
    let consentTable = null;
    const consentQuery = () => {
        const params = new URLSearchParams();
        const fields = { subjectType:'filterConsentSubjectType', subjectId:'filterConsentSubjectId', channel:'filterConsentChannel', purpose:'filterConsentPurpose', consentStatus:'filterConsentStatus' };
        Object.entries(fields).forEach(([key, id]) => { const value = document.getElementById(id)?.value.trim(); if (value) params.set(key, value); });
        params.set('includeArchived', document.getElementById('filterConsentIncludeArchived')?.checked ? 'true' : 'false');
        return params.toString();
    };
    // Applied-filter count for the toolbar filter-button pill (Golden Slim updateVisualState). Non-empty chips count;
    // "Include archived" counts only when unchecked (i.e. the user narrowed away from the default).
    const consentFilterCount = () => {
        const ids = ['filterConsentSubjectType','filterConsentSubjectId','filterConsentChannel','filterConsentPurpose','filterConsentStatus'];
        let n = ids.filter(id => (document.getElementById(id)?.value || '').trim()).length;
        const arch = document.getElementById('filterConsentIncludeArchived');
        if (arch && !arch.checked) n++;
        return n;
    };
    // Actions rendered exactly like the Golden Slim datatable (DitenDataTable.renderActions): primary icon + dropdown.
    const consentActions = row => {
        const id = String(row.consentId);
        const actions = [{ className: 'js-consent-view', icon: 'bx bx-show', text: L.View, attrs: { 'data-href': `${base}/Consents/${id}`, 'title': L.View } }];
        if (!row.isArchived) {
            actions.push({ className: 'js-consent-edit', icon: 'bx bx-edit', text: L.EditConsent, attrs: { 'data-href': `${base}/Consents/${id}/Edit` } });
            actions.push({ className: 'js-archive-consent text-warning', icon: 'bx bx-archive-in', text: L.ArchiveConsent, attrs: { 'data-id': id, 'data-name': (row.channel || '') + ' / ' + (row.purpose || '') } });
        }
        return window.DitenDataTable?.renderActions
            ? window.DitenDataTable.renderActions(actions)
            : `<a class="btn btn-icon js-consent-view" data-href="${base}/Consents/${id}"><i class="bx bx-show"></i></a>`;
    };
    const loadConsents = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        try {
            const data = await envelope(await fetch(`${base}/api/consents?${consentQuery()}`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            const rows = data?.items || [];
            await resolveSubjects(rows);
            if (consentTable) { consentTable.clear(); consentTable.rows.add(rows).draw(); return; }
            const canManage = !!(window.ConsentPreferenceContract?.features?.supportsConsentManagement) && window.ConsentPreferencePerms?.canManageConsent === true;
            const config = {
                data: rows, stateSave: false, searching: true, processing: true,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                order: [[13, 'desc']],
                columns: [
                    { data:null, defaultContent:'' }, { data:'subjectType' }, { data:'subjectId' }, { data:'channel' },
                    { data:'purpose' }, { data:'scopeType' }, { data:'scopeId' }, { data:'legalBasis' },
                    { data:'consentStatus' }, { data:'source' }, { data:'effectiveFrom' }, { data:'effectiveTo' },
                    { data:'isArchived' }, { data:'updatedAt' }, { data:null }
                ],
                columnDefs: [
                    { targets:0, className:'control', orderable:false, render:() => '' },
                    { targets:1, render:value => subjectTypeCell(value) },
                    { targets:2, render:(v,t,row) => subjectCell(row) },
                    { targets:[5,6], render:value => esc(value || '—') },
                    { targets:3, render:value => badge(value) },
                    { targets:8, render:value => badge(value, value === 'granted' ? 'success' : (['denied','withdrawn','restricted','expired'].includes(value) ? 'danger' : 'secondary')) },
                    { targets:[10,11], render:value => dtStamp(value) },
                    { targets:13, render:value => date(value) },
                    { targets:12, render:value => badge(value ? L.Yes : L.No, value ? 'warning' : 'success') },
                    { targets:14, orderable:false, searchable:false, className:'cell-fit all text-end', render:(v,t,row) => consentActions(row) }
                ],
                language: { emptyTable: L.EmptyState, processing: L.Loading },
                buttons: window.DtDefaults ? window.DtDefaults.exportButtons(
                    canManage ? L.CreateConsent : '',
                    canManage ? { 'data-consent-create': '1' } : {},
                    {
                        filterBtn: { text:'<i class="icon-base bx bx-filter-alt icon-sm"></i>', className:'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr:{ title:L.Filter, 'data-bs-toggle':'tooltip' }, action:() => window.bootstrap?.Collapse.getOrCreateInstance(document.getElementById('consentFilterCollapse'))?.toggle() },
                        saveFilterBtn: { text:'<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">'+(L.SaveView||'')+'</span>', className:'btn btn-label-primary d-none dt-save-filter-btn', attr:{ title:L.SaveView, 'data-bs-toggle':'tooltip' }, action: async function (e, api) { const fn = api?.table?.().container?.().__cpSaveView; if (fn) await fn(); } }
                    },
                    { exportColumns:[1,2,3,4,5,6,7,8,9,10,11,12,13], colvisColumns:[1,2,3,4,5,6,7,8,9,10,11,12,13] }) : [],
                initComplete: function () { mountInlineFilter('consentFilterHost', this.api()); initFilterSelect2('consentFilterHost'); wireFilterSubjectPicker(); setupSaveView(this.api(), 'ConsentPreferencesConsents', { fields:{ subjectType:'filterConsentSubjectType', subjectId:'filterConsentSubjectId', channel:'filterConsentChannel', purpose:'filterConsentPurpose', consentStatus:'filterConsentStatus' }, archivedId:'filterConsentIncludeArchived', reload: loadConsents }); },
                drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), consentFilterCount()); }
            };
            consentTable = new DataTable(consentEl, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
            consentTable.on('column-reorder.dt columns-reordered.dt', () => consentEl.dispatchEvent(new CustomEvent('consents:columns-reordered')));
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
            if (!consentTable) document.getElementById('consentContractError')?.classList.remove('d-none');
        } finally { document.getElementById('skeleton-loader')?.classList.add('d-none'); }
    };

    // ---------------- Preference table ----------------
    const preferenceEl = document.getElementById('dt-preferences');
    let preferenceTable = null;
    const preferenceQuery = () => {
        const params = new URLSearchParams();
        const fields = { subjectType:'filterPreferenceSubjectType', subjectId:'filterPreferenceSubjectId', channel:'filterPreferenceChannel', preferenceType:'filterPreferenceType' };
        Object.entries(fields).forEach(([key, id]) => { const value = document.getElementById(id)?.value.trim(); if (value) params.set(key, value); });
        params.set('includeArchived', document.getElementById('filterPreferenceIncludeArchived')?.checked ? 'true' : 'false');
        return params.toString();
    };
    const preferenceFilterCount = () => {
        const ids = ['filterPreferenceSubjectType','filterPreferenceSubjectId','filterPreferenceChannel','filterPreferenceType'];
        let n = ids.filter(id => (document.getElementById(id)?.value || '').trim()).length;
        const arch = document.getElementById('filterPreferenceIncludeArchived');
        if (arch && !arch.checked) n++;
        return n;
    };
    // Actions rendered exactly like the Consent table (DitenDataTable.renderActions): primary icon + dropdown.
    const preferenceActions = row => {
        const id = String(row.preferenceId);
        const actions = [{ className: 'js-preference-view', icon: 'bx bx-show', text: L.View, attrs: { 'data-href': `${base}/Preferences/${id}`, 'title': L.View } }];
        if (!row.isArchived) {
            actions.push({ className: 'js-preference-edit', icon: 'bx bx-edit', text: L.EditPreference, attrs: { 'data-href': `${base}/Preferences/${id}/Edit` } });
            actions.push({ className: 'js-archive-preference text-warning', icon: 'bx bx-archive-in', text: L.ArchivePreference, attrs: { 'data-id': id, 'data-name': (row.channel || '') + ' / ' + (row.preferenceType || '') } });
        }
        return window.DitenDataTable?.renderActions
            ? window.DitenDataTable.renderActions(actions)
            : `<a class="btn btn-icon js-preference-view" data-href="${base}/Preferences/${id}"><i class="bx bx-show"></i></a>`;
    };
    const loadPreferences = async () => {
        document.getElementById('preference-skeleton-loader')?.classList.remove('d-none');
        try {
            const data = await envelope(await fetch(`${base}/api/preferences?${preferenceQuery()}`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            const rows = data?.items || [];
            await resolveSubjects(rows);
            if (preferenceTable) { preferenceTable.clear(); preferenceTable.rows.add(rows).draw(); return; }
            const canManage = !!(window.ConsentPreferenceContract?.features?.supportsPreferenceManagement) && window.ConsentPreferencePerms?.canManagePreference === true;
            const config = {
                data: rows, stateSave: false, searching: true, processing: true,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                order: [[12, 'desc']],
                columns: [
                    { data:null, defaultContent:'' }, { data:'subjectType' }, { data:'subjectId' }, { data:'channel' },
                    { data:'preferenceType' }, { data:'preferenceValue' }, { data:'preferenceType' }, { data:'priority' },
                    { data:'source' }, { data:'effectiveFrom' }, { data:'effectiveTo' }, { data:'isArchived' },
                    { data:'updatedAt' }, { data:null }
                ],
                columnDefs: [
                    { targets:0, className:'control', orderable:false, render:() => '' },
                    { targets:1, render:value => subjectTypeCell(value) },
                    { targets:2, render:(v,t,row) => subjectCell(row) },
                    { targets:3, render:value => badge(value) },
                    { targets:4, render:value => preferenceTypeCell(value) },
                    { targets:6, orderable:false, render:value => RESTRICTIVE_TYPES.includes(value) ? badge(L.Yes, 'danger') : `<span class="text-muted">${esc(L.No)}</span>` },
                    { targets:7, render:value => priorityCell(value) },
                    { targets:[9,10], render:value => dtStamp(value) },
                    { targets:12, render:value => date(value) },
                    { targets:11, render:value => badge(value ? L.Yes : L.No, value ? 'warning' : 'success') },
                    { targets:13, orderable:false, searchable:false, className:'cell-fit all text-end', render:(v,t,row) => preferenceActions(row) }
                ],
                language: { emptyTable: L.EmptyState, processing: L.Loading },
                buttons: window.DtDefaults ? window.DtDefaults.exportButtons(
                    canManage ? L.CreatePreference : '',
                    canManage ? { 'data-preference-create': '1' } : {},
                    {
                        filterBtn: { text:'<i class="icon-base bx bx-filter-alt icon-sm"></i>', className:'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr:{ title:L.Filter, 'data-bs-toggle':'tooltip' }, action:() => window.bootstrap?.Collapse.getOrCreateInstance(document.getElementById('preferenceFilterCollapse'))?.toggle() },
                        saveFilterBtn: { text:'<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">'+(L.SaveView||'')+'</span>', className:'btn btn-label-primary d-none dt-save-filter-btn', attr:{ title:L.SaveView, 'data-bs-toggle':'tooltip' }, action: async function (e, api) { const fn = api?.table?.().container?.().__cpSaveView; if (fn) await fn(); } }
                    },
                    { exportColumns:[1,2,3,4,5,7,8,9,10,11,12], colvisColumns:[1,2,3,4,5,6,7,8,9,10,11,12] }) : [],
                initComplete: function () { mountInlineFilter('preferenceFilterHost', this.api()); initFilterSelect2('preferenceFilterHost'); wireFilterSubjectPicker('filterPreferenceSubjectType', 'filterPreferenceSubjectId'); setupSaveView(this.api(), 'ConsentPreferencesPreferences', { fields:{ subjectType:'filterPreferenceSubjectType', subjectId:'filterPreferenceSubjectId', channel:'filterPreferenceChannel', preferenceType:'filterPreferenceType' }, archivedId:'filterPreferenceIncludeArchived', reload: loadPreferences }); },
                drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), preferenceFilterCount()); }
            };
            preferenceTable = new DataTable(preferenceEl, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
            preferenceTable.on('column-reorder.dt columns-reordered.dt', () => preferenceEl.dispatchEvent(new CustomEvent('preferences:columns-reordered')));
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
            if (!preferenceTable) document.getElementById('preferenceContractError')?.classList.remove('d-none');
        } finally { document.getElementById('preference-skeleton-loader')?.classList.add('d-none'); }
    };

    // ---------------- Row / toolbar actions ----------------
    document.addEventListener('click', event => {
        // Navigation actions (View / Edit) carry their target in data-href.
        const nav = event.target.closest('.js-consent-view, .js-consent-edit, .js-preference-view, .js-preference-edit');
        if (nav && nav.dataset.href) { event.preventDefault(); window.location.href = nav.dataset.href; return; }
        // "Create Consent" lives in the DataTable toolbar (add-new slot) tagged with data-consent-create.
        const create = event.target.closest('[data-consent-create]');
        if (create) { event.preventDefault(); window.location.href = `${base}/Consents/Create`; return; }
        // "Create Preference" lives in the DataTable toolbar (add-new slot) tagged with data-preference-create.
        const createPref = event.target.closest('[data-preference-create]');
        if (createPref) { event.preventDefault(); window.location.href = `${base}/Preferences/Create`; return; }
        const consentArchive = event.target.closest('.js-archive-consent');
        if (consentArchive) {
            window.showConfirm?.(L.ArchiveConsentConfirm, async () => {
                try {
                    await envelope(await fetch(`${base}/api/consents/${consentArchive.dataset.id}/archive`, { method:'POST', credentials:'same-origin', headers: getAuthHeaders() }));
                    window.showToast?.(L.RecordArchived, 'success');
                    await loadConsents();
                } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
            }, { entityName: consentArchive.dataset.name, type:'warning', confirmButtonText:L.ArchiveConsent });
            return;
        }
        const preferenceArchive = event.target.closest('.js-archive-preference');
        if (preferenceArchive) {
            window.showConfirm?.(L.ArchivePreferenceConfirm, async () => {
                try {
                    await envelope(await fetch(`${base}/api/preferences/${preferenceArchive.dataset.id}/archive`, { method:'POST', credentials:'same-origin', headers: getAuthHeaders() }));
                    window.showToast?.(L.RecordArchived, 'success');
                    await loadPreferences();
                } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
            }, { entityName: preferenceArchive.dataset.name, type:'warning', confirmButtonText:L.ArchivePreference });
        }
    });

    document.getElementById('btnConsentFilterApply')?.addEventListener('click', loadConsents);
    document.getElementById('btnConsentFilterReset')?.addEventListener('click', event => {
        event.preventDefault();
        document.getElementById('consentFilterForm')?.reset();
        const jq = window.jQuery;
        if (jq) {
            jq('#consentFilterHost select.select2').val('').trigger('change'); // clearing SubjectType rebuilds the picker disabled
            jq('#filterConsentSubjectId').val(null).trigger('change');
        }
        loadConsents();
    });
    document.getElementById('btnPreferenceFilterApply')?.addEventListener('click', loadPreferences);
    document.getElementById('btnPreferenceFilterReset')?.addEventListener('click', event => {
        event.preventDefault();
        document.getElementById('preferenceFilterForm')?.reset();
        const jq = window.jQuery;
        if (jq) {
            jq('#preferenceFilterHost select.select2').val('').trigger('change'); // clearing SubjectType rebuilds the picker disabled
            jq('#filterPreferenceSubjectId').val(null).trigger('change');
        }
        loadPreferences();
    });

    // A DataTable created inside a hidden tab-pane can't measure column widths, so its responsive extension can't
    // collapse columns and the table overflows horizontally. Recalc when each tab is actually shown.
    document.querySelectorAll('button[data-bs-toggle="tab"]').forEach(btn => {
        btn.addEventListener('shown.bs.tab', event => {
            const target = event.target.getAttribute('data-bs-target');
            try {
                if (target === '#tab-consents' && consentTable) consentTable.columns.adjust().responsive.recalc();
                if (target === '#tab-preferences' && preferenceTable) preferenceTable.columns.adjust().responsive.recalc();
            } catch { /* responsive not ready yet */ }
        });
    });

    (async () => {
        const ready = await loadContract();
        if (consentEl) { if (ready) await loadConsents(); else document.getElementById('skeleton-loader')?.classList.add('d-none'); }
        if (preferenceEl) { if (ready) await loadPreferences(); else document.getElementById('preference-skeleton-loader')?.classList.add('d-none'); }
    })();
})(window, document);
