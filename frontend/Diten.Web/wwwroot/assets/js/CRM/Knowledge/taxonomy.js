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
        if (kind === 'audience-profiles') return Object.assign({ id:item.audienceProfileId, code:item.profileCode, name:item.profileName, profileType:item.profileType }, common);
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

    const openForm = (kind, row, readOnly) => {
        const spec = SPECS[kind];
        const form = document.getElementById('taxonomyForm');
        form.reset();
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
            const payload = Object.assign({ profileName: src.name, profileType: norm(src.profileType) || null }, common);
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
            profileType: document.getElementById('taxProfileType').value
        });
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

    registerTableFilter();
    (async () => {
        // The contract first (it supplies the status / profile-type vocabulary the filters and the form pick from),
        // then subjects, so the Topics tab can label its SubjectId column with real names.
        await loadContract();
        await load('subjects');
        await Promise.all([load('topics'), load('audience-profiles')]);
    })();
})(window, document);
