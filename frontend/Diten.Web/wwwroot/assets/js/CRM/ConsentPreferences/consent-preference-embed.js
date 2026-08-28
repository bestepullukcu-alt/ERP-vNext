/**
 * Read-only 360 embed — lists a subject's Consent & Preference records on another module's detail page
 * (Account / Contact). Fetches from the same-origin FU03 proxy filtered to the subject; view links open the full
 * record. No create / edit / archive here — this is a projection.
 */
(function (window, document) {
    'use strict';
    const host = document.getElementById('consentPreferenceEmbed');
    if (!host) return;
    const base = '/CRM/ConsentPreferences';
    const subjectType = host.dataset.subjectType || '';
    const subjectId = host.dataset.subjectId || '';
    if (!subjectType || !subjectId) return;

    const L = window.ConsentPreferenceL10n || {};
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[c]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const dtStamp = value => {
        if (!value) return '<span class="text-muted">—</span>';
        const d = new Date(value);
        if (isNaN(d.getTime())) return esc(value);
        const dp = d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: '2-digit' });
        const tp = d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true });
        return `<div class="text-nowrap">${esc(dp)}</div><div class="text-muted small text-nowrap">${esc(tp)}</div>`;
    };
    const iconCell = (text, icon, color) => `<span class="d-flex align-items-center text-heading"><i class="icon-base bx ${icon} ${color} me-2"></i>${esc(text || '—')}</span>`;
    const PREF_TYPE_ICONS = { 'do-not-contact':['bx-block','text-danger'], 'do-not-visit':['bx-block','text-danger'], 'frequency-cap':['bx-tachometer','text-warning'], channel:['bx-broadcast','text-primary'], language:['bx-globe','text-info'] };
    const preferenceTypeCell = v => { const [i, c] = PREF_TYPE_ICONS[String(v || '').toLowerCase()] || ['bx-slider-alt', 'text-secondary']; return iconCell(v, i, c); };
    const PRIORITY_LABELS = { 1: L.PriorityHigh, 50: L.PriorityMedium, 100: L.PriorityLow };
    const PRIORITY_BADGE = { 1: 'danger', 50: 'warning', 100: 'secondary' };
    const priorityCell = v => { const n = Number(v); return badge(PRIORITY_LABELS[n] || v || '—', PRIORITY_BADGE[n] || 'secondary'); };
    const statusClass = s => s === 'granted' ? 'success' : (['denied','withdrawn','restricted','expired'].includes(s) ? 'danger' : 'secondary');
    const viewLink = href => `<a class="btn btn-icon btn-sm btn-text-secondary" href="${href}" title="${esc(L.View)}"><i class="bx bx-show"></i></a>`;

    const query = new URLSearchParams({ subjectType, subjectId, includeArchived: 'false' }).toString();
    const showAlert = msg => { const a = document.getElementById('cpEmbedAlert'); if (a) { a.textContent = msg; a.classList.remove('d-none'); } };

    const fetchItems = async path => {
        const res = await fetch(`${base}/${path}?${query}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
        if (res.status === 403) { const e = new Error('forbidden'); e.status = 403; throw e; }
        const body = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error((body.errors || [L.ErrorState]).join(' · '));
        return body.data?.items || [];
    };

    const mkTable = (el, rows, columns, columnDefs) => {
        if (!el || typeof DataTable === 'undefined') return;
        const config = {
            data: rows, stateSave: false, searching: true, processing: false, paging: rows.length > 10,
            info: rows.length > 0, order: [[4, 'desc']], columns, columnDefs,
            language: { emptyTable: L.EmptyState, zeroRecords: L.EmptyState },
            buttons: window.DtDefaults ? window.DtDefaults.exportButtons('', {}, {}, { exportColumns:[0,1,2,3,4], colvisColumns:[0,1,2,3,4] }) : []
        };
        new DataTable(el, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
    };

    const loadConsents = async () => {
        const rows = await fetchItems('api/consents');
        mkTable(document.getElementById('dt-embed-consents'), rows,
            [ { data:'channel' }, { data:'purpose' }, { data:'legalBasis' }, { data:'consentStatus' }, { data:'effectiveFrom' }, { data:null } ],
            [
                { targets:0, render:v => badge(v) },
                { targets:[1,2], render:v => esc(v || '—') },
                { targets:3, render:v => badge(v, statusClass(v)) },
                { targets:4, render:v => dtStamp(v) },
                { targets:5, orderable:false, searchable:false, className:'text-end', render:(v,t,row) => viewLink(`${base}/Consents/${esc(row.consentId)}`) }
            ]);
    };
    const loadPreferences = async () => {
        const rows = await fetchItems('api/preferences');
        mkTable(document.getElementById('dt-embed-preferences'), rows,
            [ { data:'channel' }, { data:'preferenceType' }, { data:'preferenceValue' }, { data:'priority' }, { data:'effectiveFrom' }, { data:null } ],
            [
                { targets:0, render:v => badge(v) },
                { targets:1, render:v => preferenceTypeCell(v) },
                { targets:2, render:v => esc(v || '—') },
                { targets:3, render:v => priorityCell(v) },
                { targets:4, render:v => dtStamp(v) },
                { targets:5, orderable:false, searchable:false, className:'text-end', render:(v,t,row) => viewLink(`${base}/Preferences/${esc(row.preferenceId)}`) }
            ]);
    };

    (async () => {
        try { await loadConsents(); await loadPreferences(); }
        catch (err) {
            if (err?.status === 403) showAlert(L.ConsentNotAuthorized || L.ErrorState || 'Not authorized.');
            else showAlert(err?.message || L.ErrorState || 'Error.');
        }
    })();
})(window, document);
