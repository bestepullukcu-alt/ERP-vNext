'use strict';

/**
 * WorkCenter — All Work DataTable Module
 * Handles type switching, sub-scope filtering, and DataTable lifecycle.
 * Exposes window.AllWork = { onTabActivated, onTypeChange }
 */
window.AllWork = (function () {
    var dt = null;
    var initialized = false;
    var activeType = 'all';
    var activeScope = 'all';

    var FILTER_HOST_ID = 'allWorkFilterHost';
    var FILTER_COLLAPSE_ID = 'allWorkFilterCollapse';
    var FILTER_STATUS_ID = 'allWorkFilterStatus';
    var TABLE_ID = 'dt-all-work';
    var SCOPE_NAV_ID = 'allWorkScopeNav';
    var TYPE_FILTER_ID = 'allWorkTypeFilter';

    // ── Column index map (must match <thead> order in AllWorkTab.cshtml) ──────
    // 0: control | 1: checkbox | 2: type | 3: title | 4: status | 5: priority
    // 6: requiredAction | 7: context | 8: source | 9: owner | 10: dueDate
    // 11: severity | 12: blocker | 13: dateTime | 14: responseStatus
    // 15: organizer | 16: location | 17: reviewStatus | 18: noteDate
    // 19: author | 20: convertAction | 21: lastUpdate | 22: actions

    var COLUMN_MAP = {
        all:     { show: [2,3,4,6,7,8,9,10],        order: [[3,'asc']] },
        task:    { show: [3,4,5,6,7,9,10,17],        order: [[3,'asc']] },
        issue:   { show: [3,4,6,7,9,10,11,12],       order: [[3,'asc']] },
        meeting: { show: [3,6,7,13,14,15,16],         order: [[13,'asc']] },
        note:    { show: [3,7,9,18,19,20,21],          order: [[18,'desc']] }
    };

    // Content column range for colvis / export (exclude control, checkbox, actions)
    var CONTENT_COLS = [2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21];

    // ── Sub-scope configuration ───────────────────────────────────────────────
    var SCOPE_MAP = {
        all: [
            { key: 'all',       label: 'Tümü' },
            { key: 'assigned',  label: 'Bana Atanan' },
            { key: 'created',   label: 'Oluşturduklarım' },
            { key: 'following', label: 'Takip Ettiklerim' },
            { key: 'action',    label: 'Benden Aksiyon Bekleyen' },
            { key: 'done',      label: 'Tamamlanan', muted: true }
        ],
        task: [
            { key: 'all',       label: 'Tüm Görevler' },
            { key: 'assigned',  label: 'Bana Atanan' },
            { key: 'review',    label: 'İncelemeler' },
            { key: 'following', label: 'Takip Ettiklerim' },
            { key: 'done',      label: 'Tamamlanan', muted: true }
        ],
        issue: [
            { key: 'all',       label: 'Tüm Sorunlar' },
            { key: 'assigned',  label: 'Bana Atanan' },
            { key: 'following', label: 'Takip Ettiklerim' },
            { key: 'blocked',   label: 'Bloke Edenler' },
            { key: 'resolved',  label: 'Çözülenler', muted: true }
        ],
        meeting: [
            { key: 'all',       label: 'Tüm Toplantılar' },
            { key: 'attending', label: 'Katılacaklarım' },
            { key: 'pending',   label: 'Yanıt Bekleyenler' },
            { key: 'following', label: 'Takip Ettiklerim' },
            { key: 'past',      label: 'Geçmiş Toplantılar', muted: true }
        ],
        note: [
            { key: 'all',       label: 'Tüm Notlar' },
            { key: 'mine',      label: 'Benim Notlarım' },
            { key: 'shared',    label: 'Paylaşılanlar' },
            { key: 'convert_pending', label: 'Dönüştürülecekler' },
            { key: 'converted', label: 'Dönüştürülenler', muted: true }
        ]
    };

    // ── Mock data ─────────────────────────────────────────────────────────────
    function buildMockData() {
        var today = new Date();
        function addDays(d, n) {
            var r = new Date(d); r.setDate(r.getDate() + n); return r.toISOString().slice(0,10);
        }

        var items = [
            // Tasks
            { type: 'task', title: 'Menolyt sosyal medya kit hazırla',   status: 'InProgress', priority: 'High',   requiredAction: 'Taslak gönder',       context: 'Project Atlas',   source: 'Marketing', owner: 'Lina K.',   dueDate: addDays(today,3),  reviewStatus: 'Pending', isAssignedToMe: true,  isCreatedByMe: false, isFollowing: false, isActionRequired: true,  isDone: false, isReview: false },
            { type: 'task', title: 'API endpoint dokümantasyonu',         status: 'Backlog',    priority: 'Medium', requiredAction: 'İncele ve onayla',    context: 'Core Platform',   source: 'Engineering', owner: 'Sam D.',   dueDate: addDays(today,7),  reviewStatus: 'Required', isAssignedToMe: false, isCreatedByMe: true,  isFollowing: false, isActionRequired: true,  isDone: false, isReview: true  },
            { type: 'task', title: 'Q2 hedef revizyonu',                  status: 'InReview',   priority: 'High',   requiredAction: 'Revize et',           context: 'Strategy 2026',   source: 'Management', owner: 'Ali T.',    dueDate: addDays(today,2),  reviewStatus: null,       isAssignedToMe: true,  isCreatedByMe: false, isFollowing: true,  isActionRequired: true,  isDone: false, isReview: false },
            { type: 'task', title: 'Onboarding flow güncelle',            status: 'Done',       priority: 'Low',    requiredAction: null,                  context: 'HR Portal',       source: 'HR',         owner: 'Ali T.',    dueDate: addDays(today,-5), reviewStatus: null,       isAssignedToMe: true,  isCreatedByMe: false, isFollowing: false, isActionRequired: false, isDone: true,  isReview: false },
            { type: 'task', title: 'Sprint review slide hazırla',         status: 'Backlog',    priority: 'Medium', requiredAction: 'Taslak başlat',       context: 'Agile Process',   source: 'Engineering', owner: 'Maya R.',  dueDate: addDays(today,5),  reviewStatus: null,       isAssignedToMe: false, isCreatedByMe: false, isFollowing: true,  isActionRequired: false, isDone: false, isReview: false },

            // Issues
            { type: 'issue', title: 'Inventory sync WH-04 hatası',        status: 'Open',    priority: null, requiredAction: 'Kayıtları incele',      context: 'Supply Chain',    source: 'Ops',        owner: 'Ops Bot',   dueDate: addDays(today,1),  severity: 'Critical', blocker: 'WH-04 sync durdu',   isAssignedToMe: false, isCreatedByMe: false, isFollowing: true,  isActionRequired: true,  isDone: false, isBlocked: true  },
            { type: 'issue', title: 'Login sayfası mobile layout bozuk',   status: 'InProgress', priority: null, requiredAction: 'Reproduksiyon yap',  context: 'Core Platform',   source: 'QA',         owner: 'Ali T.',    dueDate: addDays(today,4),  severity: 'High',     blocker: null,                 isAssignedToMe: true,  isCreatedByMe: false, isFollowing: false, isActionRequired: true,  isDone: false, isBlocked: false },
            { type: 'issue', title: 'PDF export boş sayfa çıkıyor',       status: 'Open',    priority: null, requiredAction: null,                    context: 'Reporting',       source: 'QA',         owner: 'Sam D.',    dueDate: addDays(today,6),  severity: 'Medium',   blocker: null,                 isAssignedToMe: false, isCreatedByMe: false, isFollowing: true,  isActionRequired: false, isDone: false, isBlocked: false },
            { type: 'issue', title: 'Token refresh loop bug',             status: 'Resolved', priority: null, requiredAction: null,                   context: 'Auth Service',    source: 'Security',   owner: 'Ali T.',    dueDate: addDays(today,-3), severity: 'Critical', blocker: null,                 isAssignedToMe: true,  isCreatedByMe: false, isFollowing: false, isActionRequired: false, isDone: true,  isBlocked: false },

            // Meetings
            { type: 'meeting', title: 'Q2 Planning Kickoff',              status: 'Scheduled', priority: null, requiredAction: 'Katıl',               context: 'Strategy 2026',   source: 'Management', owner: 'CEO',       dueDate: null, dateTime: addDays(today,2) + ' 10:00', responseStatus: 'Accepted',  organizer: 'CEO Office',      location: 'Toplantı Odası A',  isAssignedToMe: false, isCreatedByMe: false, isFollowing: false, isActionRequired: false, isDone: false, isAttending: true,  isResponsePending: false, isPast: false },
            { type: 'meeting', title: 'Design Review - Mobile App',       status: 'Scheduled', priority: null, requiredAction: 'Yanıt ver',           context: 'Project Atlas',   source: 'Design',     owner: 'Lina K.',   dueDate: null, dateTime: addDays(today,3) + ' 14:00', responseStatus: 'Pending',   organizer: 'Lina K.',         location: 'Google Meet',       isAssignedToMe: true,  isCreatedByMe: false, isFollowing: false, isActionRequired: true,  isDone: false, isAttending: false, isResponsePending: true,  isPast: false },
            { type: 'meeting', title: 'Team Retrospective',               status: 'Completed', priority: null, requiredAction: null,                  context: 'Agile Process',   source: 'Engineering', owner: 'Ali T.',   dueDate: null, dateTime: addDays(today,-7) + ' 15:00', responseStatus: 'Attended', organizer: 'Ali T.',          location: 'Toplantı Odası B', isAssignedToMe: true,  isCreatedByMe: true,  isFollowing: false, isActionRequired: false, isDone: false, isAttending: true,  isResponsePending: false, isPast: true  },
            { type: 'meeting', title: 'Vendor Demo - ERP Integration',    status: 'Scheduled', priority: null, requiredAction: null,                  context: 'Integration Hub', source: 'Procurement', owner: 'Maya R.',  dueDate: null, dateTime: addDays(today,5) + ' 11:00', responseStatus: 'Tentative', organizer: 'Vendor Corp.',    location: 'Zoom',              isAssignedToMe: false, isCreatedByMe: false, isFollowing: true,  isActionRequired: false, isDone: false, isAttending: true,  isResponsePending: false, isPast: false },

            // Notes
            { type: 'note', title: 'Atlas proje notları - Kickoff',       status: null, priority: null, requiredAction: null,                          context: 'Project Atlas',   source: null, owner: 'Ali T.', dueDate: null, noteDate: addDays(today,-2), author: 'Ali T.',   convertAction: null,           lastUpdate: addDays(today,-1), isAssignedToMe: true,  isCreatedByMe: true,  isFollowing: false, isActionRequired: false, isDone: false, isMyNote: true,  isShared: false,        isConvertPending: false, isConverted: false },
            { type: 'note', title: 'Müşteri geri bildirim özeti',         status: null, priority: null, requiredAction: null,                          context: 'CRM',             source: null, owner: 'Sam D.', dueDate: null, noteDate: addDays(today,-4), author: 'Sam D.',   convertAction: 'Task\'a çevir', lastUpdate: addDays(today,-4), isAssignedToMe: false, isCreatedByMe: false, isFollowing: false, isActionRequired: false, isDone: false, isMyNote: false, isShared: true,         isConvertPending: true,  isConverted: false },
            { type: 'note', title: 'Sprint 12 aksiyonlar',                status: null, priority: null, requiredAction: null,                          context: 'Agile Process',   source: null, owner: 'Ali T.', dueDate: null, noteDate: addDays(today,-10), author: 'Ali T.', convertAction: null,           lastUpdate: addDays(today,-8), isAssignedToMe: true,  isCreatedByMe: true,  isFollowing: false, isActionRequired: false, isDone: false, isMyNote: true,  isShared: false,        isConvertPending: false, isConverted: true  },
            { type: 'note', title: 'Sistem mimari karar notları',         status: null, priority: null, requiredAction: null,                          context: 'Core Platform',   source: null, owner: 'Maya R.', dueDate: null, noteDate: addDays(today,-1), author: 'Maya R.', convertAction: null,           lastUpdate: addDays(today,-1), isAssignedToMe: false, isCreatedByMe: false, isFollowing: true,  isActionRequired: false, isDone: false, isMyNote: false, isShared: true,         isConvertPending: false, isConverted: false }
        ];

        return items.map(function (item, i) {
            item.id = 'aw-' + String(i + 1).padStart(3, '0');
            return item;
        });
    }

    // ── Scope matching ────────────────────────────────────────────────────────
    function matchesScope(row) {
        var type = activeType;
        var scope = activeScope;

        if (type !== 'all' && row.type !== type) return false;

        if (type === 'all') {
            if (scope === 'all')       return true;
            if (scope === 'assigned')  return !!row.isAssignedToMe;
            if (scope === 'created')   return !!row.isCreatedByMe;
            if (scope === 'following') return !!row.isFollowing;
            if (scope === 'action')    return !!row.isActionRequired && !row.isDone;
            if (scope === 'done')      return !!row.isDone;
        }

        if (type === 'task') {
            if (scope === 'all')       return !row.isDone;
            if (scope === 'assigned')  return !!row.isAssignedToMe && !row.isDone;
            if (scope === 'review')    return !!row.isReview && !row.isDone;
            if (scope === 'following') return !!row.isFollowing && !row.isDone;
            if (scope === 'done')      return !!row.isDone;
        }

        if (type === 'issue') {
            if (scope === 'all')       return !row.isDone;
            if (scope === 'assigned')  return !!row.isAssignedToMe && !row.isDone;
            if (scope === 'following') return !!row.isFollowing && !row.isDone;
            if (scope === 'blocked')   return !!row.isBlocked && !row.isDone;
            if (scope === 'resolved')  return !!row.isDone;
        }

        if (type === 'meeting') {
            if (scope === 'all')       return !row.isPast;
            if (scope === 'attending') return !!row.isAttending && !row.isPast;
            if (scope === 'pending')   return !!row.isResponsePending && !row.isPast;
            if (scope === 'following') return !!row.isFollowing && !row.isPast;
            if (scope === 'past')      return !!row.isPast;
        }

        if (type === 'note') {
            if (scope === 'all')             return true;
            if (scope === 'mine')            return !!row.isMyNote;
            if (scope === 'shared')          return !!row.isShared;
            if (scope === 'convert_pending') return !!row.isConvertPending;
            if (scope === 'converted')       return !!row.isConverted;
        }

        return true;
    }

    // ── Scope nav rendering ───────────────────────────────────────────────────
    function renderScopeNav() {
        var nav = document.getElementById(SCOPE_NAV_ID);
        if (!nav) return;

        var scopes = SCOPE_MAP[activeType] || SCOPE_MAP['all'];
        var html = '<ul class="nav aw-scope-pills d-flex" role="list">';
        scopes.forEach(function (sc) {
            var isActive = sc.key === activeScope;
            var cls = 'nav-link' + (isActive ? ' active' : '') + (sc.muted ? ' aw-scope-muted' : '');
            html += '<li class="nav-item" role="listitem">';
            html += '<button type="button" class="' + cls + '" data-aw-scope="' + sc.key + '">' + sc.label + '</button>';
            html += '</li>';
        });
        html += '</ul>';
        nav.innerHTML = html;

        nav.querySelectorAll('[data-aw-scope]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                activeScope = btn.getAttribute('data-aw-scope');
                nav.querySelectorAll('[data-aw-scope]').forEach(function (b) {
                    var sc = (SCOPE_MAP[activeType] || []).find(function (s) { return s.key === b.getAttribute('data-aw-scope'); });
                    b.classList.toggle('active', b === btn);
                    if (sc && sc.muted) {
                        b.className = 'nav-link aw-scope-muted' + (b === btn ? ' active' : '');
                    } else {
                        b.className = 'nav-link' + (b === btn ? ' active' : '');
                    }
                });
                if (dt) dt.draw(false);
            });
        });
    }

    // ── Column visibility ─────────────────────────────────────────────────────
    function applyColumnVisibility() {
        if (!dt) return;
        var cfg = COLUMN_MAP[activeType] || COLUMN_MAP['all'];
        // Hide all content columns first, then show the ones for this type
        CONTENT_COLS.forEach(function (idx) {
            dt.column(idx).visible(cfg.show.indexOf(idx) !== -1, false);
        });
        dt.columns.adjust();
        dt.order(cfg.order);
    }

    // ── Type filter button UI sync ────────────────────────────────────────────
    function syncTypeButtons() {
        var container = document.getElementById(TYPE_FILTER_ID);
        if (!container) return;
        container.querySelectorAll('[data-allwork-type]').forEach(function (btn) {
            var isActive = btn.getAttribute('data-allwork-type') === activeType;
            btn.classList.toggle('active', isActive);
            btn.classList.toggle('btn-outline-primary', isActive);
            btn.classList.toggle('btn-outline-secondary', !isActive);
            btn.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        });
    }

    // ── Inline filter wiring ──────────────────────────────────────────────────
    function mountInlineFilter() {
        var host = document.getElementById(FILTER_HOST_ID);
        if (!host) return;

        var filterBtn = document.querySelector('.dt-filter-btn');
        var toolbarRow =
            filterBtn && (filterBtn.closest('.dt-layout-row') ||
            filterBtn.closest('.row') ||
            (filterBtn.closest('.dt-layout-end') && filterBtn.closest('.dt-layout-end').parentElement));

        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6');
        } else {
            var tableEl = document.getElementById(TABLE_ID);
            var dtContainer = tableEl && (tableEl.closest('.dt-container') || tableEl.closest('.dataTables_wrapper') || tableEl.parentElement);
            if (dtContainer) { dtContainer.insertAdjacentElement('beforeend', host); host.classList.add('px-6'); }
        }
    }

    function bindInlineFilterToggle() {
        var btn = document.querySelector('#' + TABLE_ID + '_wrapper .dt-filter-btn') || document.querySelector('.dt-filter-btn');
        var el = document.getElementById(FILTER_COLLAPSE_ID);
        if (!btn || !el || btn.dataset.allworkFilterBound) return;
        btn.dataset.allworkFilterBound = '1';

        el.addEventListener('shown.bs.collapse', function () { btn.setAttribute('aria-expanded', 'true'); });
        el.addEventListener('hidden.bs.collapse', function () { btn.setAttribute('aria-expanded', 'false'); });
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            var inst = bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            if (el.classList.contains('show')) inst.hide(); else inst.show();
        });
    }

    function bindFilterFormEvents() {
        var applyBtn = document.getElementById('btnAllWorkFilterApply');
        var resetBtn = document.getElementById('btnAllWorkFilterReset');
        if (applyBtn) applyBtn.addEventListener('click', function () { if (dt) dt.draw(false); });
        if (resetBtn) resetBtn.addEventListener('click', function () {
            var sel = document.getElementById(FILTER_STATUS_ID);
            if (sel) sel.value = '';
            if (dt) dt.draw(false);
        });
    }

    // ── DataTable init ────────────────────────────────────────────────────────
    function initDataTable() {
        var tableEl = document.getElementById(TABLE_ID);
        if (!tableEl || !window.DtDefaults) return;

        // Register custom search filter (scope + status filter)
        $.fn.dataTable.ext.search.push(function (settings, data, dataIndex) {
            if (!dt || settings.nTable.id !== TABLE_ID) return true;
            var row = dt.row(dataIndex).data();
            if (!row) return true;

            // Status filter
            var statusSel = document.getElementById(FILTER_STATUS_ID);
            if (statusSel && statusSel.value && row.status !== statusSel.value) return false;

            return matchesScope(row);
        });

        var extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: 'Import', 'data-bs-toggle': 'tooltip' },
                action: function () { window.showToast && window.showToast('ComingSoon', 'warning'); }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: 'Filter',
                    'aria-controls': FILTER_COLLAPSE_ID,
                    'aria-expanded': 'false'
                }
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary d-none dt-save-filter-btn',
                attr: { title: 'Save View', 'data-bs-toggle': 'tooltip' },
                action: function () { window.showToast && window.showToast('ComingSoon', 'warning'); }
            }
        };

        dt = new DataTable(tableEl, window.DtDefaults.create({
            data: buildMockData(),
            stateSave: false,
            columns: [
                { data: null,             name: 'control',        defaultContent: '' },
                { data: 'id',             name: 'checkbox' },
                { data: 'type',           name: 'type' },
                { data: 'title',          name: 'title' },
                { data: 'status',         name: 'status',         defaultContent: '' },
                { data: 'priority',       name: 'priority',       defaultContent: '' },
                { data: 'requiredAction', name: 'requiredAction', defaultContent: '' },
                { data: 'context',        name: 'context',        defaultContent: '' },
                { data: 'source',         name: 'source',         defaultContent: '' },
                { data: 'owner',          name: 'owner',          defaultContent: '' },
                { data: 'dueDate',        name: 'dueDate',        defaultContent: '' },
                { data: 'severity',       name: 'severity',       defaultContent: '' },
                { data: 'blocker',        name: 'blocker',        defaultContent: '' },
                { data: 'dateTime',       name: 'dateTime',       defaultContent: '' },
                { data: 'responseStatus', name: 'responseStatus', defaultContent: '' },
                { data: 'organizer',      name: 'organizer',      defaultContent: '' },
                { data: 'location',       name: 'location',       defaultContent: '' },
                { data: 'reviewStatus',   name: 'reviewStatus',   defaultContent: '' },
                { data: 'noteDate',       name: 'noteDate',       defaultContent: '' },
                { data: 'author',         name: 'author',         defaultContent: '' },
                { data: 'convertAction',  name: 'convertAction',  defaultContent: '' },
                { data: 'lastUpdate',     name: 'lastUpdate',     defaultContent: '' },
                { data: null,             name: 'actions',        defaultContent: '' }
            ],
            columnDefs: [
                {
                    targets: 0,
                    className: 'control',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 2,
                    render: function () { return ''; }
                },
                {
                    targets: 1,
                    orderable: false,
                    searchable: false,
                    responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: function (data) {
                        return '<input type="checkbox" class="dt-checkboxes form-check-input" value="' + data + '">';
                    }
                },
                {
                    targets: 2,
                    render: function (data) {
                        var badgeMap = {
                            task:    'bg-label-info',
                            issue:   'bg-label-danger',
                            meeting: 'bg-label-warning',
                            note:    'bg-label-secondary'
                        };
                        var cls = badgeMap[data] || 'bg-label-secondary';
                        return '<span class="badge ' + cls + '">' + (data || '') + '</span>';
                    }
                },
                {
                    targets: 4,
                    render: function (data) {
                        if (!data) return '';
                        var statusMap = {
                            'Backlog':    'bg-label-secondary',
                            'InProgress': 'bg-label-primary',
                            'InReview':   'bg-label-warning',
                            'Done':       'bg-label-success',
                            'Cancelled':  'bg-label-danger',
                            'Open':       'bg-label-danger',
                            'Resolved':   'bg-label-success',
                            'Scheduled':  'bg-label-info',
                            'Completed':  'bg-label-success'
                        };
                        return '<span class="badge ' + (statusMap[data] || 'bg-label-secondary') + '">' + data + '</span>';
                    }
                },
                {
                    targets: 5,
                    render: function (data) {
                        if (!data) return '';
                        var map = { 'High': 'bg-label-danger', 'Medium': 'bg-label-warning', 'Low': 'bg-label-success' };
                        return '<span class="badge ' + (map[data] || 'bg-label-secondary') + '">' + data + '</span>';
                    }
                },
                {
                    targets: 6,
                    render: function (data) {
                        return data ? '<span class="text-primary fw-medium">' + data + '</span>' : '';
                    }
                },
                {
                    targets: 11,
                    render: function (data) {
                        if (!data) return '';
                        var map = { 'Critical': 'bg-danger', 'High': 'bg-label-danger', 'Medium': 'bg-label-warning', 'Low': 'bg-label-success' };
                        return '<span class="badge ' + (map[data] || 'bg-label-secondary') + '">' + data + '</span>';
                    }
                },
                // Hidden columns initially (for types other than 'all')
                { targets: [5, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21], visible: false },
                {
                    targets: -1,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit',
                    render: function (data, type, full) {
                        return '<div class="d-flex align-items-center">' +
                            '<a href="javascript:;" class="btn btn-icon btn-sm btn-label-secondary dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>' +
                            '<div class="dropdown-menu dropdown-menu-end m-0">' +
                            '<a href="javascript:void(0);" class="dropdown-item">Detay</a>' +
                            '</div>' +
                            '</div>';
                    }
                }
            ],
            order: [[3, 'asc']],
            buttons: window.DtDefaults.exportButtons(
                '+ Yeni Ekle',
                { 'data-aw-add-new': '' },
                extraButtons,
                { exportColumns: CONTENT_COLS, colvisColumns: CONTENT_COLS, showAllColumns: CONTENT_COLS }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                bindFilterFormEvents();
                // Re-apply column visibility for initial type ('all')
                applyColumnVisibility();
            }
        }));
    }

    // ── Type button binding ───────────────────────────────────────────────────
    function bindTypeButtons() {
        var container = document.getElementById(TYPE_FILTER_ID);
        if (!container) return;
        container.querySelectorAll('[data-allwork-type]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                onTypeChange(btn.getAttribute('data-allwork-type'));
            });
        });
    }

    // ── Public API ────────────────────────────────────────────────────────────
    function onTabActivated() {
        if (!initialized) {
            initDataTable();
            renderScopeNav();
            bindTypeButtons();
            initialized = true;
        }
        if (dt) dt.columns.adjust().draw(false);
    }

    function onTypeChange(type) {
        if (!COLUMN_MAP[type]) return;
        activeType = type;
        activeScope = (SCOPE_MAP[type] || [])[0] ? SCOPE_MAP[type][0].key : 'all';
        syncTypeButtons();
        renderScopeNav();
        if (dt) {
            applyColumnVisibility();
            dt.draw(false);
        }
    }

    return {
        onTabActivated: onTabActivated,
        onTypeChange: onTypeChange
    };
})();
