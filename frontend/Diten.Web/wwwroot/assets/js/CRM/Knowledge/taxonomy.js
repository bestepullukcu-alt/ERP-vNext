/**
 * MOD-0162-FU02 Knowledge Taxonomy (Subjects / Topics / Audience Profiles) — Golden Slim aligned.
 *  - One shared builder drives all three tab tables so they behave identically.
 *  - Native toolbar search + Action (export) collection + ColVis + Filter + Save View buttons.
 *  - Select2 filter chips in a collapse host that is relocated into each table's own toolbar.
 *  - SaveView (filters + search + colVis + order + page length) via personalizationClient, one pageKey per tab.
 *  - Row actions via window.DitenDataTable.renderActions (primary icon + "…" dropdown).
 *  - Create lives in the DataTable toolbar (.add-new slot); there is no delete surface — closing is Archive.
 */
(function (window, document) {
    'use strict';
    if (!document.getElementById('dt-subjects')) return;

    const base = '/CRM/Knowledge/api';
    const L = window.KnowledgeL10n || window.L10n || {};
    const canManage = window.KnowledgeTaxonomyCanManage === true;
    const PERSONALIZATION_MODULE = 'CRM';

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const date = v => v ? new Date(v).toLocaleString() : '—';
    // Effective-window display: "Aug 03, 26" over "05:04 PM" (Golden Slim two-line stamp).
    const dtStamp = v => {
        if (!v) return '<span class="text-muted">—</span>';
        const d = new Date(v);
        if (isNaN(d.getTime())) return esc(v);
        const dp = d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: '2-digit' });
        const tp = d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true });
        return `<div class="text-nowrap">${esc(dp)}</div><div class="text-muted small text-nowrap">${esc(tp)}</div>`;
    };
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const headers = { Accept: 'application/json' };
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    // The FU02 contract vocabulary (taxonomy statuses / audience-profile types). Filters and the form pick from it —
    // never from invented values.
    let contract = null;
    const vocabFor = field => {
        const v = contract?.vocabularies || {};
        if (field === 'status') return (v.taxonomyStatuses || []).map(x => ({ value: x, text: x }));
        if (field === 'profileType') return (v.audienceProfileTypes || []).map(x => ({ value: x, text: x }));
        return null;
    };
    const loadContract = async () => {
        try { contract = await envelope(await fetch(`${base}/contract`, { credentials: 'same-origin', headers })); }
        catch { contract = null; }
    };

    // Map API rows (resource-specific field names) onto a common shape the shared builder can render. Alias and external
    // references are carried through untouched: every write is a full replace, so dropping them here would wipe them.
    const normalize = (kind, item) => {
        const common = {
            description: item.description, status: item.status, sortOrder: item.sortOrder,
            effectiveFrom: item.effectiveFrom, effectiveTo: item.effectiveTo,
            alias: item.alias || [], externalReferences: item.externalReferences || [],
            updatedAt: item.updatedAt || item.createdAt, isArchived: item.isArchived
        };
        if (kind === 'topics') return Object.assign({ id:item.topicId, code:item.topicCode, name:item.topicName, subjectId:item.subjectId, parentTopicId:item.parentTopicId }, common);
        // WP-MOD0162-AUD-UI: dimensions ride through so an edit round-trips them (writes are full replaces).
        if (kind === 'audience-profiles') return Object.assign({ id:item.audienceProfileId, code:item.profileCode, name:item.profileName, profileType:item.profileType, dimensions:item.dimensions || [] }, common);
        return Object.assign({ id:item.subjectId, code:item.subjectCode, name:item.subjectName, parentSubjectId:item.parentSubjectId }, common);
    };

    // ─── Per-tab specification ────────────────────────────────────────────────
    const SPECS = {
        subjects: {
            tableId: 'dt-subjects', hostId: 'subjectsFilterHost', collapseId: 'subjectsFilterCollapse',
            formId: 'subjectsFilterForm', skeletonId: 'skeleton-loader', pageKey: 'KnowledgeTaxonomySubjects',
            createText: L.CreateSubject, editText: L.EditSubject || L.Edit, archiveText: L.ArchiveSubject,
            totalColumns: 12, managedColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], order: [[10, 'desc']],
            filterFields: { status: { id: 'filterSubjectsStatus', multi: true, field: 'status' } },
            archivedId: 'filterSubjectsArchived'
        },
        topics: {
            tableId: 'dt-topics', hostId: 'topicsFilterHost', collapseId: 'topicsFilterCollapse',
            formId: 'topicsFilterForm', skeletonId: 'topics-skeleton-loader', pageKey: 'KnowledgeTaxonomyTopics',
            createText: L.CreateTopic, editText: L.EditTopic || L.Edit, archiveText: L.ArchiveTopic,
            totalColumns: 12, managedColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], order: [[10, 'desc']],
            filterFields: {
                status: { id: 'filterTopicsStatus', multi: true, field: 'status' },
                subjectId: { id: 'filterTopicsSubjectId', multi: true, field: 'subjectId' }
            },
            archivedId: 'filterTopicsArchived'
        },
        'audience-profiles': {
            tableId: 'dt-profiles', hostId: 'profilesFilterHost', collapseId: 'profilesFilterCollapse',
            formId: 'profilesFilterForm', skeletonId: 'profiles-skeleton-loader', pageKey: 'KnowledgeTaxonomyProfiles',
            createText: L.CreateProfile, editText: L.EditProfile || L.Edit, archiveText: L.ArchiveProfile,
            totalColumns: 11, managedColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9], order: [[9, 'desc']],
            filterFields: {
                status: { id: 'filterProfilesStatus', multi: true, field: 'status' },
                profileType: { id: 'filterProfilesProfileType', multi: true, field: 'profileType' }
            },
            archivedId: 'filterProfilesArchived'
        }
    };
    const KINDS = Object.keys(SPECS);

    const state = {};
    KINDS.forEach(kind => { state[kind] = { rows: [], table: null, applied: emptyFilters(kind), armed: false, view: null }; });
    function emptyFilters(kind) {
        const f = { includeArchived: 'true' };
        Object.keys(SPECS[kind].filterFields).forEach(key => { f[key] = SPECS[kind].filterFields[key].multi ? [] : ''; });
        return f;
    }

    // "code — name" labels for every loaded taxonomy row, so ID reference columns (SubjectId, ParentTopicId) and the
    // Subject filter chip read as names instead of raw GUIDs.
    const labels = {};

    // ─── Select2 filter chips (Golden Slim inline-filter styling) ─────────────
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
    // Multi-selects show a placeholder + count badge (not clipped tags) — Golden inline-filter summary.
    const syncMultiSelectSummary = $select => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = window.jQuery('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = window.jQuery('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = window.jQuery('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = window.jQuery('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = norm($select.data('placeholder')) || '';
        const selectedValues = normArr($select.val());
        const selectedTexts = ($select.select2('data') || []).map(i => norm(i.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clear = window.jQuery('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clear.on('mousedown', e => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clear);
        }
    };
    const initSelect2 = hostId => {
        const jq = window.jQuery;
        if (!jq || !jq.fn || !jq.fn.select2) return;
        jq(`#${hostId} select.select2`).each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            const shared = {
                dropdownParent: jq(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element'
            };
            if ($s.prop('multiple')) {
                $s.select2(Object.assign({ containerCssClass: 'dt-inline-filter-multi', closeOnSelect: false }, shared));
                $s.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary($s));
                window.requestAnimationFrame(() => syncMultiSelectSummary($s));
            } else {
                $s.select2(Object.assign({ allowClear: true }, shared));
            }
            $s.on('select2:open', clampFilterDropdown);
        });
    };
    // Options come from the loaded rows only — no invented vocabulary.
    const fillSelect = (id, values) => {
        const el = document.getElementById(id);
        if (!el) return;
        const selected = normArr(window.jQuery ? window.jQuery(el).val() : el.value);
        el.innerHTML = (values || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
        if (selected.length && window.jQuery) window.jQuery(el).val(selected);
    };
    const distinct = (kind, field) => Array.from(new Set(state[kind].rows.map(r => r[field]).filter(Boolean)))
        .map(v => ({ value: v, text: labels[v] || v }));
    const loadFilterOptions = kind => {
        const spec = SPECS[kind];
        // Status / profile type come from the contract vocabulary; subject references come from the loaded rows.
        Object.values(spec.filterFields).forEach(f => fillSelect(f.id, vocabFor(f.field) || distinct(kind, f.field)));
        initSelect2(spec.hostId);
    };

    // ─── Inline filter host relocation (Golden Slim mountInlineFilter) ────────
    const mountInlineFilter = (hostId, api) => {
        const host = document.getElementById(hostId);
        const container = api.table().container();
        const filterBtn = container.querySelector('.dt-filter-btn');
        const row = filterBtn && (filterBtn.closest('.dt-layout-row') || filterBtn.closest('.row') || (filterBtn.closest('.dt-layout-end') || {}).parentElement);
        if (host && row) { row.insertAdjacentElement('afterend', host); host.classList.remove('px-6'); host.classList.add('px-3'); }
    };
    const bindInlineFilterA11y = (collapseId, api) => {
        const btn = api.table().container().querySelector('.dt-filter-btn');
        const el = document.getElementById(collapseId);
        if (!btn || !el || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        btn.setAttribute('aria-controls', collapseId);
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    // ─── Filter read/write + client-side matching ────────────────────────────
    const readControls = kind => {
        const spec = SPECS[kind];
        const f = { includeArchived: document.getElementById(spec.archivedId)?.value || 'true' };
        Object.entries(spec.filterFields).forEach(([key, def]) => {
            const el = document.getElementById(def.id);
            f[key] = def.multi ? normArr(window.jQuery ? window.jQuery(el).val() : []) : (el?.value || '');
        });
        return f;
    };
    const writeControls = (kind, f) => {
        const spec = SPECS[kind];
        const jq = window.jQuery;
        Object.entries(spec.filterFields).forEach(([key, def]) => {
            const el = document.getElementById(def.id);
            if (!el) return;
            const value = def.multi ? normArr(f[key]) : (f[key] || '');
            if (jq) jq(el).val(value).trigger('change'); else el.value = def.multi ? '' : value;
        });
        const arch = document.getElementById(spec.archivedId);
        if (arch) { arch.value = f.includeArchived || 'true'; if (jq) jq(arch).trigger('change'); }
    };
    const matchesMulti = (sel, value) => { const n = normArr(sel); return !n.length || n.includes(norm(value)); };
    const filterCount = kind => {
        const f = state[kind].applied;
        let n = Object.keys(SPECS[kind].filterFields).filter(key => normArr(f[key]).length > 0).length;
        if (f.includeArchived === 'false') n++;
        return n;
    };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search) return;
        const owners = KINDS.map(k => ({ kind: k, el: document.getElementById(SPECS[k].tableId) }));
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            const kind = (owners.find(o => o.el === settings.nTable) || {}).kind;
            if (!kind) return true;
            const r = row || state[kind].table?.row(dataIndex)?.data?.();
            if (!r) return true;
            const f = state[kind].applied;
            if (f.includeArchived === 'false' && r.isArchived) return false;
            return Object.entries(SPECS[kind].filterFields).every(([key, def]) => matchesMulti(f[key], r[def.field]));
        });
    };

    // ─── Save View (personalization) ─────────────────────────────────────────
    const safeParse = s => { try { return JSON.parse(s); } catch { return null; } };
    const setupSaveView = (kind, api) => {
        const pc = window.personalizationClient;
        const spec = SPECS[kind];
        const container = api.table().container();
        const saveBtn = container.querySelector('.dt-save-filter-btn');
        const rec = { id: null, name: null };
        const idOf = r => r?.id ?? r?.Id ?? null;
        const nameOf = r => r?.viewName ?? r?.ViewName ?? null;
        const defOf = r => { const d = r?.viewDefinition ?? r?.ViewDefinition; return typeof d === 'string' ? safeParse(d) : (d || null); };
        const current = () => ({
            colVis: Array.from({ length: spec.totalColumns }, (_, i) => { try { return api.column(i).visible(); } catch { return true; } }),
            order: api.order(), pageLength: api.page.len(), search: api.search() || '',
            filters: readControls(kind)
        });
        const ser = v => JSON.stringify(v || {});
        let baseline = ser(current());
        const setSaveVisible = show => { if (saveBtn) { saveBtn.classList.toggle('d-none', !show); window.DtDefaults?.refreshButtonGroupRadii?.(); } };
        const refreshDirty = () => { if (state[kind].armed) setSaveVisible(ser(current()) !== baseline); };
        const applyState = s => {
            if (!s) return;
            try {
                if (Array.isArray(s.colVis)) s.colVis.forEach((vis, i) => { try { api.column(i).visible(!!vis, false); } catch { /* stale index */ } });
                if (Array.isArray(s.order) && s.order.length) api.order(s.order);
                if (s.pageLength) api.page.len(s.pageLength);
                api.search(s.search || '');
                if (s.filters) { writeControls(kind, s.filters); state[kind].applied = Object.assign(emptyFilters(kind), s.filters); }
                api.columns.adjust().draw(false);
                window.DtDefaults?.updateVisualState?.(api, filterCount(kind));
            } catch { /* ignore */ }
        };
        const doSave = async () => {
            const view = current();
            const payload = { moduleKey: PERSONALIZATION_MODULE, pageKey: spec.pageKey, viewName: (rec.name || L.SaveView || 'Default'), viewDefinition: view, isDefault: true, visibility: 'private' };
            const resp = rec.id ? await pc.updateView(rec.id, payload) : await pc.saveView(payload);
            const saved = (resp && typeof resp === 'object') ? (resp.data ?? resp.Data ?? resp) : null;
            if (saved && typeof saved === 'object') { rec.id = idOf(saved) || rec.id; rec.name = nameOf(saved) || rec.name; }
            baseline = ser(view);
            setSaveVisible(false);
            window.showToast?.(L.RecordSaved || L.SaveView || 'Saved', 'success');
        };
        // The Save button (a DataTables Buttons node) resolves this handler off its own table container.
        container.__taxSaveView = async () => {
            try { await doSave(); }
            catch (e) { if (!e?.authHandled) { console.error('[Knowledge Taxonomy SaveView] save failed', e); window.showToast?.(e.message || L.ErrorState, 'error'); } }
        };
        state[kind].refreshDirty = refreshDirty;
        state[kind].resetBaseline = () => { baseline = ser(current()); };

        api.on('column-visibility.dt length.dt order.dt search.dt', refreshDirty);
        Object.values(spec.filterFields).forEach(def => {
            const el = document.getElementById(def.id);
            if (!el) return;
            if (window.jQuery) window.jQuery(el).on('change', refreshDirty); else el.addEventListener('change', refreshDirty);
        });
        document.getElementById(spec.archivedId)?.addEventListener('change', refreshDirty);

        if (!pc?.getViews) { setTimeout(() => { state[kind].armed = true; }, 0); return; }
        pc.getViews(PERSONALIZATION_MODULE, spec.pageKey).then(views => {
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            const record = Array.isArray(items) ? (items.find(v => (v?.isDefault ?? v?.IsDefault) === true) || items[0] || null) : null;
            if (record) {
                rec.id = idOf(record); rec.name = nameOf(record);
                const def = defOf(record);
                if (def) { applyState(def); baseline = ser(current()); }
            }
        }).catch(err => { if (!err?.authHandled) console.error('[Knowledge Taxonomy SaveView] load failed', err); })
          .finally(() => { setTimeout(() => { state[kind].armed = true; }, 0); });
    };

    // ─── Row actions (Golden Slim: primary icon + "…" dropdown) ──────────────
    // View is always available. The write actions need crm.knowledge.subject.manage. An archived row accepts no update
    // (the backend answers 409), so it offers Restore instead of Edit/Activate/Archive. There is still no delete
    // surface anywhere — closing a row is Archive, reopening it is Restore.
    const actions = (kind, row) => {
        const spec = SPECS[kind];
        const ref = { 'data-kind': kind, 'data-id': esc(row.id) };
        const items = [{ className: 'js-tax-view', icon: 'bx bx-show', text: L.ViewDetails || L.View, attrs: Object.assign({ title: L.View }, ref) }];
        if (canManage && row.isArchived) {
            items.push({ className: 'js-tax-unarchive text-success', icon: 'bx bx-archive-out', text: L.Restore, attrs: Object.assign({ 'data-name': esc(row.name) }, ref) });
        } else if (canManage) {
            items.push({ className: 'js-tax-edit', icon: 'bx bx-edit', text: spec.editText || L.Edit, attrs: Object.assign({}, ref) });
            items.push(norm(row.status) === 'active'
                ? { className: 'js-tax-status', icon: 'bx bx-pause-circle', text: L.Deactivate, attrs: Object.assign({ 'data-status': 'inactive' }, ref) }
                : { className: 'js-tax-status text-success', icon: 'bx bx-check-circle', text: L.Activate, attrs: Object.assign({ 'data-status': 'active' }, ref) });
            items.push({ className: 'js-tax-archive text-warning', icon: 'bx bx-archive-in', text: spec.archiveText, attrs: Object.assign({ 'data-name': esc(row.name) }, ref) });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // ─── Columns ─────────────────────────────────────────────────────────────
    const statusBadge = v => badge(v, v === 'archived' ? 'secondary' : (v === 'active' ? 'success' : 'primary'));
    const archivedBadge = v => badge(v ? L.Yes : L.No, v ? 'warning' : 'success');
    const nameCell = v => `<span class="fw-medium text-heading">${esc(v)}</span>`;
    const refCell = v => v ? `<span class="text-muted" title="${esc(v)}">${esc(labels[v] || v)}</span>` : '<span class="text-muted">—</span>';

    const columnsFor = kind => {
        const ctrl = { data: null, defaultContent: '' };
        const act = { data: null };
        if (kind === 'topics') return {
            columns: [ctrl, { data:'code' }, { data:'name' }, { data:'subjectId' }, { data:'parentTopicId' }, { data:'status' }, { data:'sortOrder' }, { data:'effectiveFrom' }, { data:'effectiveTo' }, { data:'isArchived' }, { data:'updatedAt' }, act],
            columnDefs: [
                { targets:0, className:'control', orderable:false, render:() => '' },
                { targets:2, render:v => nameCell(v) },
                { targets:[3,4], render:v => refCell(v) },
                { targets:5, render:v => statusBadge(v) },
                { targets:[7,8], render:v => dtStamp(v) },
                { targets:9, render:v => archivedBadge(v) },
                { targets:10, render:v => date(v) },
                { targets:11, title:L.Actions, orderable:false, searchable:false, className:'cell-fit all text-end', render:(v,t,row) => actions(kind, row) }
            ]
        };
        if (kind === 'audience-profiles') return {
            columns: [ctrl, { data:'code' }, { data:'name' }, { data:'profileType' }, { data:'status' }, { data:'sortOrder' }, { data:'effectiveFrom' }, { data:'effectiveTo' }, { data:'isArchived' }, { data:'updatedAt' }, act],
            columnDefs: [
                { targets:0, className:'control', orderable:false, render:() => '' },
                { targets:2, render:v => nameCell(v) },
                { targets:3, render:v => v ? badge(v, 'info') : '<span class="text-muted">—</span>' },
                { targets:4, render:v => statusBadge(v) },
                { targets:[6,7], render:v => dtStamp(v) },
                { targets:8, render:v => archivedBadge(v) },
                { targets:9, render:v => date(v) },
                { targets:10, title:L.Actions, orderable:false, searchable:false, className:'cell-fit all text-end', render:(v,t,row) => actions(kind, row) }
            ]
        };
        return {
            columns: [ctrl, { data:'code' }, { data:'name' }, { data:'parentSubjectId' }, { data:'status' }, { data:'sortOrder' }, { data:'description' }, { data:'effectiveFrom' }, { data:'effectiveTo' }, { data:'isArchived' }, { data:'updatedAt' }, act],
            columnDefs: [
                { targets:0, className:'control', orderable:false, render:() => '' },
                { targets:2, render:v => nameCell(v) },
                { targets:3, render:v => refCell(v) },
                { targets:4, render:v => statusBadge(v) },
                { targets:6, render:v => v ? esc(v) : '<span class="text-muted">—</span>' },
                { targets:[7,8], render:v => dtStamp(v) },
                { targets:9, render:v => archivedBadge(v) },
                { targets:10, render:v => date(v) },
                { targets:11, title:L.Actions, orderable:false, searchable:false, className:'cell-fit all text-end', render:(v,t,row) => actions(kind, row) }
            ]
        };
    };

    const buildConfig = kind => {
        const spec = SPECS[kind];
        const cols = columnsFor(kind);
        return Object.assign({
            data: state[kind].rows, stateSave: false, searching: true, processing: true,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            order: spec.order,
            language: { emptyTable: L.EmptyState, processing: L.Loading },
            buttons: window.DtDefaults ? window.DtDefaults.exportButtons(
                canManage ? spec.createText : '',
                canManage ? { 'data-tax-create': kind } : {},
                {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                        attr: { title: L.Filter, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                        action: () => window.bootstrap?.Collapse.getOrCreateInstance(document.getElementById(spec.collapseId), { toggle: false }).toggle()
                    },
                    saveFilterBtn: {
                        text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                        className: 'btn btn-label-primary d-none dt-save-filter-btn',
                        attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                        action: async function (e, api) { const fn = api?.table?.().container?.().__taxSaveView; if (fn) await fn(); }
                    }
                },
                { exportColumns: spec.managedColumns, colvisColumns: spec.managedColumns }) : [],
            initComplete: function () {
                const api = this.api();
                mountInlineFilter(spec.hostId, api);
                bindInlineFilterA11y(spec.collapseId, api);
                loadFilterOptions(kind);
                setupSaveView(kind, api);
                window.DtDefaults?.updateVisualState?.(api, filterCount(kind));
            },
            drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), filterCount(kind)); }
        }, cols);
    };

    // ─── Load ────────────────────────────────────────────────────────────────
    const load = async kind => {
        const spec = SPECS[kind];
        document.getElementById(spec.skeletonId)?.classList.remove('d-none');
        try {
            const data = await envelope(await fetch(`${base}/${kind}?includeArchived=true`, { credentials: 'same-origin', headers }));
            state[kind].rows = (data?.items || []).map(x => normalize(kind, x));
            state[kind].rows.forEach(r => { labels[r.id] = `${r.code} — ${r.name}`; });
            if (state[kind].table) {
                state[kind].table.clear();
                state[kind].table.rows.add(state[kind].rows).draw(false);
                loadFilterOptions(kind);
                return;
            }
            const el = document.getElementById(spec.tableId);
            const config = buildConfig(kind);
            state[kind].table = new DataTable(el, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
        } catch (error) {
            const host = document.getElementById('knowledgeTaxonomyContractError');
            if (host) { host.textContent = error.message || L.ErrorState; host.classList.remove('d-none'); }
        } finally {
            document.getElementById(spec.skeletonId)?.classList.add('d-none');
        }
    };

    // ─── Create / edit / view offcanvas ──────────────────────────────────────
    const canvas = () => window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('taxonomyCanvas'));
    const findRow = (kind, id) => state[kind].rows.find(r => String(r.id) === String(id));

    // Suggested code for a new row: PREFIX-001, PREFIX-002 … It is only a default — the field stays editable.
    const CODE_PREFIX = { subjects: 'SUBJ', topics: 'TOPIC', 'audience-profiles': 'AUDP' };
    const nextCode = kind => {
        const prefix = CODE_PREFIX[kind];
        const pattern = new RegExp(`^${prefix}-(\\d+)$`, 'i');
        const used = new Set(state[kind].rows.map(r => norm(r.code).toUpperCase()));
        const taken = state[kind].rows.map(r => pattern.exec(norm(r.code))).filter(Boolean).map(m => parseInt(m[1], 10));
        let n = (taken.length ? Math.max.apply(null, taken) : 0) + 1;
        let code = `${prefix}-${String(n).padStart(3, '0')}`;
        while (used.has(code)) { n += 1; code = `${prefix}-${String(n).padStart(3, '0')}`; }
        return code;
    };

    // Setting a value programmatically must reach BOTH select2 (so the chip repaints) and the required-fields tracker
    // (which listens with addEventListener, and jQuery's .trigger() would not call it).
    const setValue = (id, value) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.value = value == null ? '' : String(value);
        if (window.jQuery && window.jQuery(el).hasClass('select2-hidden-accessible')) window.jQuery(el).trigger('change.select2');
        el.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const fillFormSelect = (id, options, withEmpty, current) => {
        const el = document.getElementById(id);
        if (!el) return;
        const list = (options || []).slice();
        // A stored value that is no longer offered (an archived reference, a retired vocabulary entry) is kept so the
        // form never silently drops it.
        if (current && !list.some(o => String(o.value) === String(current))) list.unshift({ value: current, text: labels[current] || current });
        el.innerHTML = (withEmpty ? '<option value=""></option>' : '') + list.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    const CLEARABLE = { taxParentSubjectId: true, taxParentTopicId: true, taxProfileType: true };
    const initFormSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        jq('#taxonomyCanvas select.tax-select2').each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: jq('#taxonomyCanvas'),
                placeholder: $s.data('placeholder') || '',
                width: '100%',
                allowClear: CLEARABLE[this.id] === true
            });
        });
    };
    const populateFormOptions = (kind, row) => {
        // "archived" is deliberately not offered: the backend rejects it on update (400) — archiving is its own action.
        const statuses = (vocabFor('status') || []).filter(o => o.value !== 'archived');
        fillFormSelect('taxStatus', statuses.length ? statuses : ['draft', 'active', 'inactive'].map(v => ({ value: v, text: v })), false, row?.status);
        const asOption = r => ({ value: r.id, text: `${r.code} — ${r.name}` });
        // A subject cannot be its own parent. A deeper cycle is caught server-side (400), which surfaces as a toast.
        fillFormSelect('taxParentSubjectId', state.subjects.rows.filter(r => !r.isArchived && r.id !== row?.id).map(asOption), true, row?.parentSubjectId);
        fillFormSelect('taxSubjectId', state.subjects.rows.filter(r => !r.isArchived).map(asOption), true, row?.subjectId);
        // A topic cannot be its own parent.
        fillFormSelect('taxParentTopicId', state.topics.rows.filter(r => !r.isArchived && r.id !== row?.id).map(asOption), true, row?.parentTopicId);
        fillFormSelect('taxProfileType', vocabFor('profileType') || [], true, row?.profileType);
        initFormSelect2();
    };
    const setFormReadOnly = readOnly => {
        document.querySelectorAll('#taxonomyForm input:not([type=hidden]), #taxonomyForm select, #taxonomyForm textarea')
            .forEach(el => { el.disabled = readOnly; });
        if (window.jQuery?.fn?.select2) window.jQuery('#taxonomyCanvas select.tax-select2').trigger('change.select2');
        document.getElementById('taxonomySubmit')?.classList.toggle('d-none', readOnly);
    };

    // ─── WP-MOD0162-AUD-UI: AudienceProfile dimension builder ─────────────────
    // Each row = an axis + its values. A reference axis (account-type / contact-type / medical-specialty) picks values
    // BY NAME from MOD-0048 published-values but STORES the stable ValueCode; a custom axis takes a free AxisCode + free
    // tag values. Dimensions are optional (0+ rows). Writes are full replaces — collectDimensions() is the whole list.
    const REF_AXES = ['account-type', 'contact-type', 'medical-specialty'];
    const AXIS_LABEL = {
        'account-type': () => L.AxisAccountType || 'account-type',
        'contact-type': () => L.AxisContactType || 'contact-type',
        'medical-specialty': () => L.AxisMedicalSpecialty || 'medical-specialty',
        'custom': () => L.AxisCustom || 'custom'
    };
    const isRefAxis = axis => REF_AXES.includes(axis);
    // Soft-cascade doctor detection — by code or (multilingual) label; best-effort, never a hard data binding.
    const DOCTOR_CODES = ['doctor', 'physician', 'md', 'doktor', 'hekim'];
    const looksLikeDoctor = (code, label) => DOCTOR_CODES.includes(norm(code).toLowerCase())
        || /doctor|physician|hekim|doktor|m[eé]decin|arzt|врач|医生|طبيب/i.test(norm(label));

    // published-values cache: setCode → [{code, label, isDeprecated, replacement}]
    const refValues = {};
    const loadRefValues = async setCode => {
        if (refValues[setCode]) return refValues[setCode];
        try {
            const data = await envelope(await fetch(`${base}/reference-data/${encodeURIComponent(setCode)}/values`, { credentials: 'same-origin', headers }));
            refValues[setCode] = (data?.items || []).map(v => {
                const code = norm(v.code || v.valueCode || v.value);
                return {
                    code,
                    label: norm(v.label || v.displayName || v.text) || code,
                    isDeprecated: v.isDeprecated === true || v.isActive === false,
                    replacement: norm(v.replacementValueCode || v.replacementCode),
                    sortOrder: Number(v.sortOrder || 0)
                };
            }).filter(o => o.code).sort((a, b) => a.sortOrder - b.sortOrder || a.label.localeCompare(b.label));
        } catch { refValues[setCode] = []; }
        return refValues[setCode];
    };
    const refValueLabel = (setCode, code) => (refValues[setCode] || []).find(o => o.code === code)?.label || code;

    // WP-MOD0162-AUD-UI-3: compose-then-add. `dimensions` holds ADDED items (display-only); a value's label is
    // captured at Add time so nothing re-looks-up async. Item: { axis, axisCode, values:[code], valueLabels:[label] }.
    let dimensions = [];
    let composeAxis = 'account-type';   // the axis currently in the fixed bottom compose-row
    let dimReadOnly = false;
    // WP-MOD0162-SUBJECT-UI: Subject↔Global Product picker state.
    let subjectGpDisabled = false;      // MDM selector unreachable/forbidden → picker disabled, custom still works
    let suppressGpAuto = false;         // load seeds the picker; its change handler must not prefill the Name then

    // WP-MOD0162-AUD-UI-2: Dimensions apply ONLY to these profile types; Name is derived from the dimension value
    // labels for them, and pre-filled from the ProfileType label (editable) for every other type.
    const DIM_TYPES = ['healthcare-professional', 'pharmacist'];
    let currentFormKind = null;
    let profileNameDirty = false;      // the editable-Name case: a user edit is preserved until ProfileType changes
    let suppressProfileAuto = false;   // openForm sets fields itself; the ProfileType change handler must not fight it
    const titleize = code => norm(code).split(/[-_\s]+/).filter(Boolean)
        .map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
    // The read-only Name for a dimensioned profile: every added value's display label, joined by " / "
    // ("Doctor / Nephrology"). Labels are captured at Add time (stored on the item as valueLabels), so this is
    // synchronous and never blanks while a published-values fetch is in flight.
    const dimensionNameLabel = () => {
        const parts = [];
        dimensions.forEach(d => (d.valueLabels || []).forEach(l => { if (norm(l)) parts.push(l); }));
        return parts.join(' / ');
    };
    const profileTypeValue = () => norm(document.getElementById('taxProfileType')?.value);
    // Dimensions are visible only for a dimensioned profile type on the AudienceProfile form.
    const updateDimVisibility = () => {
        const show = currentFormKind === 'audience-profiles' && DIM_TYPES.includes(profileTypeValue());
        document.getElementById('taxDimensionsSection')?.classList.toggle('d-none', !show);
    };
    // Name: derived + DISABLED for a dimensioned profile; ProfileType-seeded + editable otherwise. Only the
    // AudienceProfile form drives this — Subject/Topic keep a plain editable name. The derived value still submits
    // because the submit handler reads taxName.value in JS (~901), not a native form-serialize that would drop a
    // disabled field.
    const updateProfileName = () => {
        if (currentFormKind !== 'audience-profiles') return;
        const nameEl = document.getElementById('taxName');
        if (!nameEl) return;
        const pt = profileTypeValue();
        const derived = DIM_TYPES.includes(pt);
        // In view mode setFormReadOnly already disabled everything — don't re-enable it here; only manage the
        // disabled appearance in create/edit, mirroring the derived state.
        if (!dimReadOnly) { nameEl.disabled = derived; nameEl.readOnly = derived; }
        if (derived) {
            setValue('taxName', dimensionNameLabel());    // empty until a dimension value is chosen
        } else if (!profileNameDirty) {
            setValue('taxName', pt ? titleize(pt) : '');
        }
    };
    const onProfileTypeChange = () => {
        if (suppressProfileAuto || currentFormKind !== 'audience-profiles') return;
        profileNameDirty = false;                         // a fresh ProfileType re-seeds the editable Name
        const dim = DIM_TYPES.includes(profileTypeValue());
        if (!dim) { dimensions = []; renderDimensions(); }  // clear orphaned dims when leaving a dimensioned type
        updateDimVisibility();
        // (Re)build the compose-row now the section is visible, so its select2 measures a real width.
        if (dim) { if (isRefAxis(composeAxis)) loadRefValues(composeAxis).then(renderCompose); else renderCompose(); }
        updateProfileName();
    };

    // ── Canonical DitenCheckItem shell (exact classes) ────────────────────────
    // DitenCheckItem.row/.addRow are purpose-built for task text-items (fixed grip/level/evidence/remove + a text
    // string) with no slot for Axis+Values select2 — so, per the WP, we reuse the exact CSS classes verbatim rather
    // than the factory. The grip + move affordances are present but WITHDRAWN (visibility:hidden), so a dimension row
    // keeps the same rhythm/height as a Tasks checklist row without offering a reorder that is meaningless for a set.
    const checkitemAffordance = () =>
        `<span class="diten-checkitem-grip diten-checkitem-withdrawn" aria-hidden="true"><i class="bx bx-grid-vertical"></i></span>`
        + `<span class="diten-checkitem-move diten-checkitem-withdrawn" aria-hidden="true">`
        + `<button type="button" class="diten-checkitem-btn" tabindex="-1"><i class="bx bx-chevron-up"></i></button>`
        + `<button type="button" class="diten-checkitem-btn" tabindex="-1"><i class="bx bx-chevron-down"></i></button></span>`;

    // ── Added rows (display-only checklist) ───────────────────────────────────
    const axisDisplay = d => d.axis === 'custom' ? (d.axisCode || (L.AxisCustom || 'custom')) : AXIS_LABEL[d.axis]();
    const renderDimensions = () => {
        const host = document.getElementById('taxDimensions');
        if (!host) return;
        // Display-only rows (no inline edit — change = delete + re-add): axis label + value-label chips + the quiet
        // canonical × (diten-checkitem-remove, transparent → red on hover), never a heavy btn-label-danger block.
        host.innerHTML = dimensions.map((d, i) => {
            const chips = (d.valueLabels || []).map(l => `<span class="badge bg-label-secondary me-1">${esc(l)}</span>`).join('');
            const rm = dimReadOnly ? '' :
                `<button type="button" class="diten-checkitem-btn diten-checkitem-remove js-dim-remove" data-i="${i}" title="${esc(L.RemoveDimension || '')}" aria-label="${esc(L.RemoveDimension || '')}"><i class="bx bx-x"></i></button>`;
            return `<li class="diten-checkitem">
                    ${checkitemAffordance()}
                    <span class="diten-checkitem-text"><span class="fw-medium me-2">${esc(axisDisplay(d))}</span>${chips || '<span class="text-muted">—</span>'}</span>
                    ${rm}
                </li>`;
        }).join('');
        document.getElementById('taxDimensionsEmpty')?.classList.toggle('d-none', dimensions.length > 0);
        updateProfileName();   // the added values drive the derived Name for HCP/pharmacist profiles
    };

    // ── Fixed compose-row: a plain .diten-checkitem shell (NOT a separate card) ──
    const composeValuesControl = () => {
        const dep = ` (${esc(L.Deprecated || 'deprecated')})`;
        if (composeAxis === 'custom') {
            return `<input type="text" class="form-control form-control-sm mb-1" id="composeCustomCode" placeholder="${esc(L.CustomAxisPlaceholder || '')}">
                <select multiple class="form-select form-select-sm" id="composeValues" data-placeholder="${esc(L.CustomValuesPlaceholder || '')}"></select>`;
        }
        const opts = (refValues[composeAxis] || []).map(o =>
            `<option value="${esc(o.code)}">${esc(o.label)}${o.isDeprecated ? dep : ''}</option>`).join('');
        return `<select multiple class="form-select form-select-sm" id="composeValues" data-placeholder="${esc(L.SelectValues || L.SelectOption || '')}">${opts}</select>`;
    };
    const renderCompose = () => {
        const host = document.getElementById('taxDimensionCompose');
        if (!host) return;
        if (dimReadOnly) { host.innerHTML = ''; return; }   // view mode: display rows only, no compose-row
        const axisOpts = REF_AXES.concat('custom')
            .map(a => `<option value="${esc(a)}"${composeAxis === a ? ' selected' : ''}>${esc(AXIS_LABEL[a]())}</option>`).join('');
        // Same .diten-checkitem shell as an added row (matching border/bg/padding); Axis + Values sit in the text slot,
        // a plain Tasks-style Add (btn-label-primary, not a solid purple block) sits where remove would.
        // Compose-row is a vertical stack: row 1 = Axis + Values ALWAYS side by side (flex:1 vs flex:2, both
        // min-width:0 so they stay on one line and shrink rather than wrap, small select2 preserved); row 2 = a
        // left-aligned Add beneath them. No grip/move affordance here — this is an add-row, not a draggable item, so
        // its left padding is just the .diten-checkitem shell's own (grip space removed).
        host.innerHTML = `<div class="diten-checkitem flex-column align-items-stretch gap-2">
                <div class="d-flex gap-2 align-items-start">
                    <span style="flex:1 1 0; min-width:0"><select class="form-select form-select-sm" id="composeAxis" aria-label="${esc(L.Axis || 'Axis')}">${axisOpts}</select></span>
                    <span style="flex:2 1 0; min-width:0">${composeValuesControl()}</span>
                </div>
                <button type="button" class="btn btn-label-primary btn-sm align-self-start" id="btnDimAdd">${esc(L.AddDimension || 'Add')}</button>
            </div>`;
        initComposeSelect2();
    };
    const initComposeSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        // selectionCssClass 'form-select form-select-sm' = the same SMALL rendering the working filter chips use
        // (initSelect2 ~167); without it select2 renders at its default (large) height and the multi container grows
        // past its flex cell and overruns Add.
        jq('#composeAxis').select2({ dropdownParent: jq('#taxonomyCanvas'), selectionCssClass: 'form-select form-select-sm', minimumResultsForSearch: Infinity, width: '100%' })
            .off('change.dc').on('change.dc', async function () {
                composeAxis = jq(this).val();
                if (isRefAxis(composeAxis)) await loadRefValues(composeAxis);
                renderCompose();   // swap the values control for the new axis' vocabulary
            });
        const vopts = { dropdownParent: jq('#taxonomyCanvas'), selectionCssClass: 'form-select form-select-sm', width: '100%', placeholder: document.getElementById('composeValues')?.getAttribute('data-placeholder') || '', closeOnSelect: false };
        if (composeAxis === 'custom') { vopts.tags = true; vopts.tokenSeparators = [',']; }
        jq('#composeValues').select2(vopts);
    };
    // Add the composed axis+values as a display row, CAPTURING the value labels now (from the loaded published-values,
    // so no later async lookup), then reset the compose-row. Stores the stable code; the label is display-only.
    const addComposed = () => {
        if (dimReadOnly) return;
        const jq = window.jQuery;
        const custom = composeAxis === 'custom';
        const axisCode = custom ? norm(document.getElementById('composeCustomCode')?.value) : composeAxis;
        const values = normArr(jq ? jq('#composeValues').val() : []);
        if (!axisCode || values.length === 0) return;   // backend requires a non-empty axis + ≥1 value
        const valueLabels = custom ? values.slice() : values.map(v => refValueLabel(composeAxis, v));
        // Re-adding an axis replaces its earlier row (the backend rejects a duplicate axis).
        const key = axisCode.toLowerCase();
        dimensions = dimensions.filter(d => d.axisCode.toLowerCase() !== key);
        dimensions.unshift({ axis: custom ? 'custom' : composeAxis, axisCode, values, valueLabels });
        // Cascade: adding contact-type=doctor pre-sets the next compose axis to medical-specialty (documented fallback).
        composeAxis = maybeCascadeAxis(dimensions[0]) || 'account-type';
        if (isRefAxis(composeAxis)) { loadRefValues(composeAxis).then(renderCompose); } else { renderCompose(); }
        renderDimensions();
    };
    const maybeCascadeAxis = added => {
        if (added.axis !== 'contact-type') return null;
        const hasDoctor = added.values.some((code, k) => looksLikeDoctor(code, added.valueLabels[k]));
        if (!hasDoctor || dimensions.some(d => d.axis === 'medical-specialty')) return null;
        return 'medical-specialty';
    };

    const loadDimensions = async row => {
        dimensions = (row?.dimensions || []).map(d => {
            const axisCode = norm(d.axisCode);
            return { axis: isRefAxis(axisCode) ? axisCode : 'custom', axisCode, values: normArr(d.values), valueLabels: [] };
        });
        await Promise.all(REF_AXES.filter(a => dimensions.some(d => d.axis === a)).map(loadRefValues));
        // resolve display labels now that published-values are cached (custom = the raw value is its own label)
        dimensions.forEach(d => { d.valueLabels = d.axis === 'custom' ? d.values.slice() : d.values.map(v => refValueLabel(d.axis, v)); });
        renderDimensions();
    };
    // Full-replace list; each item already carries a non-empty axis + ≥1 value and a unique axis (enforced on Add).
    const collectDimensions = () => {
        const seen = new Set(); const out = [];
        dimensions.forEach(d => {
            const axisCode = norm(d.axisCode);
            const values = normArr(d.values);
            if (!axisCode || values.length === 0) return;
            const key = axisCode.toLowerCase();
            if (seen.has(key)) return;
            seen.add(key);
            out.push({ axisCode, values });
        });
        return out;
    };

    // ── WP-MOD0162-SUBJECT-UI: Subject ↔ MDM Global Product picker ────────────
    const gpMode = () => document.querySelector('input[name="taxSubjectSource"]:checked')?.value || 'custom';
    const setGpMode = mode => {
        const custom = mode !== 'global-product';
        const r = document.getElementById(custom ? 'taxSubjectSourceCustom' : 'taxSubjectSourceProduct');
        if (r) r.checked = true;
        document.getElementById('taxGlobalProductWrap')?.classList.toggle('d-none', custom);
    };
    const gpRef = row => (row?.externalReferences || []).find(r => norm(r.sourceSystem).toLowerCase() === 'global-product');
    const setGpDisabled = reason => {
        subjectGpDisabled = true;
        const note = document.getElementById('taxGlobalProductDisabledNote');
        if (note) { note.textContent = L[reason] || L.GlobalProductPickerUnavailable || ''; note.classList.remove('d-none'); }
    };
    // WP-MOD0162-SUBJECT-UI-2: ajax server-side search over the MDM selector (177 rows, MDM cap pageSize=100). The
    // browser types to search; the same-origin proxy attaches the token and clamps pageSize. A { disabled, reason }
    // body → disabled + reason (never a silent empty list); a stored code stays visible via its preselected <option>.
    const initGpSelect2 = () => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        const $s = jq('#taxGlobalProductId');
        if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
        $s.select2({
            dropdownParent: jq('#taxonomyCanvas'),
            selectionCssClass: 'form-select form-select-sm',
            width: '100%',
            placeholder: $s.data('placeholder') || '',
            allowClear: true,
            minimumInputLength: 0,
            ajax: {
                url: `${base}/global-product-options`,
                dataType: 'json',
                delay: 250,
                data: params => ({ search: params.term || '', pageNumber: 1, pageSize: 100 }),
                processResults: body => {
                    if (body && body.disabled) { setGpDisabled(body.reason); return { results: [] }; }
                    subjectGpDisabled = false;
                    document.getElementById('taxGlobalProductDisabledNote')?.classList.add('d-none');
                    return { results: (body && body.options ? body.options : []).map(o => ({ id: o.value, text: o.label })) };
                },
                // Return the jqXHR so select2 can abort an in-flight search when a new keystroke starts (a .then() chain
                // has no .abort()).
                transport: (params, success, failure) => {
                    const request = jq.ajax(params);
                    request.then(success);
                    request.fail(xhr => { setGpDisabled('GlobalProductPickerUnavailable'); failure(xhr); });
                    return request;
                }
            }
        });
        // A user pick prefills the (editable) Name; a programmatic seed (load) is suppressed so it can't clobber a
        // stored/edited Subject name.
        $s.off('change.gp').on('change.gp', function () {
            if (suppressGpAuto) return;
            const sel = $s.select2('data');
            const label = (sel && sel[0]) ? norm(sel[0].text) : '';
            if (norm($s.val()) && label) setValue('taxName', label);
        });
    };
    const loadSubjectGlobalProduct = async row => {
        const ref = gpRef(row);
        const currentId = ref ? norm(ref.externalId) : '';
        let currentLabel = ref ? norm(ref.externalName) : '';
        const el = document.getElementById('taxGlobalProductId');
        if (!el) return;
        subjectGpDisabled = false;
        document.getElementById('taxGlobalProductDisabledNote')?.classList.add('d-none');
        suppressGpAuto = true;
        // Preselect the stored product (resolve id→name when the reference carried none) — an ajax select2 shows a
        // selected value only if its <option> is present.
        if (currentId && !currentLabel) {
            try { const r = await (await fetch(`${base}/global-product-options/${encodeURIComponent(currentId)}`, { credentials: 'same-origin', headers })).json(); currentLabel = norm(r?.label) || currentId; }
            catch { currentLabel = currentId; }
        }
        el.innerHTML = currentId ? `<option value="${esc(currentId)}" selected>${esc(currentLabel || currentId)}</option>` : '';
        // Availability probe (search-less, pageSize=1) so the picker shows disabled + reason immediately, not only
        // after the dropdown is first opened.
        let probe = null;
        try { probe = await (await fetch(`${base}/global-product-options?pageSize=1`, { credentials: 'same-origin', headers })).json(); }
        catch { probe = { disabled: true, reason: 'GlobalProductPickerUnavailable' }; }
        if (probe && probe.disabled) { setGpDisabled(probe.reason); el.disabled = true; }
        else { el.disabled = !!dimReadOnly; }   // view mode keeps it disabled
        initGpSelect2();
        if (window.jQuery) window.jQuery('#taxGlobalProductId').trigger('change.select2');
        setGpMode(currentId ? 'global-product' : 'custom');
        suppressGpAuto = false;
    };

    const openForm = (kind, row, readOnly) => {
        const spec = SPECS[kind];
        const form = document.getElementById('taxonomyForm');
        form.reset();
        currentFormKind = kind;
        suppressProfileAuto = true;   // openForm seeds ProfileType / Name / dimensions itself; don't let the change handler fight it
        populateFormOptions(kind, row);
        document.getElementById('taxKind').value = kind;
        document.getElementById('taxId').value = row?.id || '';
        // Every field goes through setValue so the required-fields tracker in the header recounts on open.
        setValue('taxCode', row ? row.code : nextCode(kind));
        document.getElementById('taxCode').readOnly = !!row;      // the update contract has no code field — code is immutable
        document.getElementById('taxCodeHint')?.classList.toggle('d-none', !!row);
        setValue('taxName', row?.name || '');
        setValue('taxParentSubjectId', row?.parentSubjectId || '');
        setValue('taxSubjectId', row?.subjectId || '');
        setValue('taxParentTopicId', row?.parentTopicId || '');
        setValue('taxProfileType', row?.profileType || '');
        setValue('taxStatus', row?.status || 'active');
        setValue('taxSortOrder', row?.sortOrder ?? 0);
        setValue('taxDescription', row?.description || '');
        document.querySelectorAll('.tax-only-subject').forEach(x => x.classList.toggle('d-none', kind !== 'subjects'));
        document.querySelectorAll('.tax-only-topic').forEach(x => x.classList.toggle('d-none', kind !== 'topics'));
        document.querySelectorAll('.tax-only-profile').forEach(x => x.classList.toggle('d-none', kind !== 'audience-profiles'));
        setFormReadOnly(!!readOnly);
        // WP-MOD0162-AUD-UI(-2): the dimension builder + Name derivation are profile-only. Dimensions show only for a
        // dimensioned ProfileType; Name is derived+read-only for those, ProfileType-seeded+editable otherwise.
        dimReadOnly = !!readOnly;
        // Reset the Name control per open: enabled in create/edit, disabled in view (setFormReadOnly already disabled
        // it). updateProfileName then re-derives the disabled state from the ProfileType for the AudienceProfile form.
        const taxNameEl = document.getElementById('taxName');
        taxNameEl.readOnly = false;
        taxNameEl.disabled = !!readOnly;
        composeAxis = 'account-type';                      // reset the compose-row for each open
        if (kind === 'audience-profiles') {
            void loadDimensions(row);                     // async: renderDimensions() re-derives the Name once it lands
            const pt = row?.profileType || '';
            if (DIM_TYPES.includes(pt)) {
                profileNameDirty = false;
                document.getElementById('taxName').readOnly = true;
                updateProfileName();
            } else {
                profileNameDirty = !!(row && row.name);   // an existing custom name is preserved (not re-seeded)
                if (!profileNameDirty) setValue('taxName', pt ? titleize(pt) : '');
            }
        } else {
            dimensions = []; renderDimensions();
        }
        updateDimVisibility();
        // Render the compose-row AFTER visibility is set so its select2 measures a real width (a d-none section is 0-wide).
        if (currentFormKind === 'audience-profiles') { if (isRefAxis(composeAxis)) loadRefValues(composeAxis).then(renderCompose); else renderCompose(); }
        // WP-MOD0162-SUBJECT-UI: load the Subject↔Global Product picker (id→name + mode) for subjects.
        if (kind === 'subjects') { void loadSubjectGlobalProduct(row); }
        suppressProfileAuto = false;
        // A topic's subject is fixed at creation (the update contract does not carry SubjectId).
        if (!readOnly) document.getElementById('taxSubjectId').disabled = !!row;
        document.getElementById('taxonomyCanvasTitle').textContent = readOnly
            ? (L.ViewDetails || L.View)
            : (row ? (spec.editText || L.Edit) : (spec.createText || ''));
        canvas()?.show();
    };

    // Create and update are both FULL replaces: every payload carries the stored effective window, alias and external
    // references forward, otherwise saving from this form would wipe fields it does not show.
    const writePayload = (kind, src, isUpdate) => {
        const common = {
            status: norm(src.status) || 'active',
            sortOrder: Number(src.sortOrder || 0),
            description: norm(src.description) || null,
            effectiveFrom: src.effectiveFrom || new Date().toISOString(),
            effectiveTo: src.effectiveTo || null,
            alias: src.alias || [],
            externalReferences: src.externalReferences || []
        };
        if (kind === 'topics') {
            const payload = Object.assign({ topicName: src.name, parentTopicId: src.parentTopicId || null }, common);
            return isUpdate ? payload : Object.assign({ topicCode: src.code, subjectId: src.subjectId }, payload);
        }
        if (kind === 'audience-profiles') {
            // WP-MOD0162-AUD-UI: dimensions is a full replace (empty array clears); values are stable ValueCodes.
            const payload = Object.assign({ profileName: src.name, profileType: norm(src.profileType) || null, dimensions: src.dimensions || [] }, common);
            return isUpdate ? payload : Object.assign({ profileCode: src.code }, payload);
        }
        // ParentSubjectId is re-assignable, so it rides both create and update.
        const payload = Object.assign({ subjectName: src.name, parentSubjectId: src.parentSubjectId || null }, common);
        return isUpdate ? payload : Object.assign({ subjectCode: src.code }, payload);
    };
    const save = async (kind, id, src) => {
        const url = id ? `${base}/${kind}/${id}` : `${base}/${kind}`;
        await envelope(await fetch(url, {
            method: id ? 'PUT' : 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
            body: JSON.stringify(writePayload(kind, src, !!id))
        }));
    };

    document.addEventListener('click', async event => {
        // "Create" lives in each table's toolbar (add-new slot) tagged with data-tax-create.
        const create = event.target.closest('[data-tax-create]');
        if (create) { event.preventDefault(); openForm(create.getAttribute('data-tax-create'), null, false); return; }
        const view = event.target.closest('.js-tax-view');
        if (view) { event.preventDefault(); openForm(view.dataset.kind, findRow(view.dataset.kind, view.dataset.id), true); return; }
        const edit = event.target.closest('.js-tax-edit');
        if (edit) { event.preventDefault(); openForm(edit.dataset.kind, findRow(edit.dataset.kind, edit.dataset.id), false); return; }
        // Activate / Deactivate: a status-only update sent as the full row, so nothing else changes.
        const statusBtn = event.target.closest('.js-tax-status');
        if (statusBtn) {
            event.preventDefault();
            const kind = statusBtn.dataset.kind;
            const row = findRow(kind, statusBtn.dataset.id);
            if (!row) return;
            try {
                await save(kind, row.id, Object.assign({}, row, { status: statusBtn.dataset.status }));
                window.showToast?.(L.RecordUpdated, 'success');
                await load(kind);
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
            return;
        }
        // Restore: brings an archived row back as inactive. It can legitimately fail 409 (the code was reused, or the
        // owning subject / parent topic is still archived) — the toast carries the backend's reason.
        const unarchive = event.target.closest('.js-tax-unarchive');
        if (unarchive) {
            event.preventDefault();
            try {
                await envelope(await fetch(`${base}/${unarchive.dataset.kind}/${unarchive.dataset.id}/unarchive`, { method:'POST', credentials:'same-origin', headers }));
                window.showToast?.(L.RestoredAsInactive || L.RecordUpdated, 'success');
                await load(unarchive.dataset.kind);
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
            return;
        }
        const archive = event.target.closest('.js-tax-archive');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.ArchiveTaxonomyConfirm, async () => {
            try {
                await envelope(await fetch(`${base}/${archive.dataset.kind}/${archive.dataset.id}/archive`, { method:'POST', credentials:'same-origin', headers }));
                window.showToast?.(L.RecordArchived, 'success');
                await load(archive.dataset.kind);
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { entityName: archive.dataset.name, type:'warning', confirmButtonText: SPECS[archive.dataset.kind]?.archiveText });
    });

    document.getElementById('taxonomyForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const kind = document.getElementById('taxKind').value;
        const id = document.getElementById('taxId').value;
        const existing = id ? (findRow(kind, id) || {}) : {};
        const src = Object.assign({}, existing, {
            code: document.getElementById('taxCode').value,
            name: document.getElementById('taxName').value,
            status: document.getElementById('taxStatus').value,
            sortOrder: document.getElementById('taxSortOrder').value,
            description: document.getElementById('taxDescription').value,
            subjectId: document.getElementById('taxSubjectId').value || existing.subjectId,
            parentSubjectId: document.getElementById('taxParentSubjectId').value,
            parentTopicId: document.getElementById('taxParentTopicId').value,
            profileType: document.getElementById('taxProfileType').value,
            dimensions: kind === 'audience-profiles' ? collectDimensions() : undefined
        });
        // WP-MOD0162-SUBJECT-UI: full-replace externalReferences, managing only the global-product line and preserving
        // any other reference types. When MDM is disabled the stored refs pass through untouched (never lose the link).
        if (kind === 'subjects') {
            const existingRefs = existing.externalReferences || [];
            if (subjectGpDisabled) {
                src.externalReferences = existingRefs;
            } else {
                const others = existingRefs.filter(r => norm(r.sourceSystem).toLowerCase() !== 'global-product');
                const gpId = gpMode() === 'global-product' ? norm(document.getElementById('taxGlobalProductId')?.value) : '';
                if (gpId) {
                    const el = document.getElementById('taxGlobalProductId');
                    const opt = el?.options[el.selectedIndex];
                    const label = opt ? norm(opt.textContent) : '';
                    others.push({ sourceSystem: 'global-product', externalId: gpId, externalName: label || null, isPrimary: true });
                }
                src.externalReferences = others;
            }
        }
        try {
            await save(kind, id, src);
            window.showToast?.(id ? L.RecordUpdated : L.RecordCreated, 'success');
            canvas()?.hide();
            await load(kind);
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
    });

    // ─── Filter apply / reset ────────────────────────────────────────────────
    document.addEventListener('click', event => {
        const apply = event.target.closest('[data-tax-apply]');
        if (apply) {
            const kind = apply.getAttribute('data-tax-apply');
            const api = state[kind].table;
            if (!api) return;
            state[kind].applied = readControls(kind);
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, filterCount(kind));
            state[kind].refreshDirty?.();
            window.bootstrap?.Collapse.getOrCreateInstance(document.getElementById(SPECS[kind].collapseId), { toggle: false }).hide();
            return;
        }
        const reset = event.target.closest('[data-tax-reset]');
        if (!reset) return;
        event.preventDefault();
        const kind = reset.getAttribute('data-tax-reset');
        const api = state[kind].table;
        state[kind].applied = emptyFilters(kind);
        writeControls(kind, state[kind].applied);
        if (api) { api.search(''); api.draw(); window.DtDefaults?.updateVisualState?.(api, filterCount(kind)); }
        state[kind].refreshDirty?.();
    });

    // A DataTable built inside a hidden tab-pane measures its columns wrong; recalc when the tab is shown.
    const paneKind = { '#tab-subjects':'subjects', '#tab-topics':'topics', '#tab-profiles':'audience-profiles' };
    document.querySelectorAll('button[data-bs-toggle="tab"]').forEach(btn => {
        btn.addEventListener('shown.bs.tab', event => {
            const kind = paneKind[event.target.getAttribute('data-bs-target')];
            try { state[kind]?.table?.columns.adjust().responsive.recalc(); } catch { /* responsive not ready yet */ }
        });
    });

    // WP-MOD0162-AUD-UI-2: ProfileType drives Dimensions visibility + Name; a real user edit of the (editable) Name
    // marks it dirty so the ProfileType prefill stops overwriting it until ProfileType changes again. The <select>
    // element persists across openForm (select2 only re-wraps it), so these bind once. Bind both native + jQuery
    // because select2 raises the change as a jQuery event while setValue() dispatches a native one.
    document.getElementById('taxProfileType')?.addEventListener('change', onProfileTypeChange);
    window.jQuery && window.jQuery('#taxProfileType').on('change', onProfileTypeChange);
    document.getElementById('taxName')?.addEventListener('input', () => {
        if (currentFormKind === 'audience-profiles' && !DIM_TYPES.includes(profileTypeValue())) profileNameDirty = true;
    });

    // WP-MOD0162-SUBJECT-UI: the Subject source toggle shows/hides the Global Product picker; choosing custom clears
    // the picked product so the save carries no global-product reference.
    document.querySelectorAll('input[name="taxSubjectSource"]').forEach(r => r.addEventListener('change', () => {
        const custom = gpMode() !== 'global-product';
        document.getElementById('taxGlobalProductWrap')?.classList.toggle('d-none', custom);
        // Choosing custom clears the picked product (an ajax select2 has no blank <option>, so clear via val(null)).
        if (custom && window.jQuery) window.jQuery('#taxGlobalProductId').val(null).trigger('change');
    }));

    // WP-MOD0162-AUD-UI-3: compose-then-add. Add commits the compose-row as a display row; × removes an added row.
    // Both containers exist at load (offcanvas markup, hidden), so these delegated listeners bind once.
    document.getElementById('taxDimensionCompose')?.addEventListener('click', event => {
        if (event.target.closest('#btnDimAdd')) { event.preventDefault(); addComposed(); }
    });
    document.getElementById('taxDimensions')?.addEventListener('click', event => {
        const rm = event.target.closest('.js-dim-remove');
        if (!rm || dimReadOnly) return;
        dimensions.splice(Number(rm.dataset.i), 1);
        renderDimensions();
    });

    registerTableFilter();
    (async () => {
        // The contract first (it supplies the status / profile-type vocabulary the filters and the form pick from),
        // then subjects, so the Topics tab can label its SubjectId column with real names.
        await loadContract();
        await load('subjects');
        await Promise.all([load('topics'), load('audience-profiles')]);
    })();
})(window, document);
