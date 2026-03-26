'use strict';

(function () {
    const l10n = window.L10n || {};
    const PAGE_SIZE = 20;
    const LOAD_MORE_DEFAULT_LABEL = l10n.LoadMore || 'Load More';
    const SCOPE_STORAGE_KEY = 'workcenter.activeScope';
    const INBOX_ALLOWED_STATUSES = new Set(['NEW', 'WAITING_INFO', 'IN_REVIEW']);

    const normalizeTab = (value) => (value === 'all' ? 'all' : 'inbox');
    const normalizeScope = (value) => {
        const normalized = (value || '').toLowerCase();
        return ['all', 'task', 'issue', 'meeting', 'note'].includes(normalized) ? normalized : 'all';
    };

    const readQueryTab = () => {
        try {
            const params = new URLSearchParams(window.location.search || '');
            return normalizeTab((params.get('tab') || '').toLowerCase());
        } catch {
            return 'inbox';
        }
    };

    const readStoredScope = () => {
        try {
            return normalizeScope(window.sessionStorage.getItem(SCOPE_STORAGE_KEY));
        } catch {
            return 'all';
        }
    };

    const writeStoredScope = (scope) => {
        try {
            window.sessionStorage.setItem(SCOPE_STORAGE_KEY, normalizeScope(scope));
        } catch {
            // Ignore storage errors in private mode/sandboxed contexts.
        }
    };

    const elements = {
        workCenterPage: document.getElementById('workCenterPage'),
        inboxTabTrigger: document.getElementById('workcenter-inbox-tab'),
        allWorkTabTrigger: document.getElementById('workcenter-allwork-tab'),
        allWorkViewSwitchHost: document.getElementById('allWorkViewSwitch'),
        inboxLoadMoreBtn: document.getElementById('inboxLoadMoreBtn'),
        rowTemplate: document.getElementById('inboxRowTemplate'),
        inboxRoot: document.querySelector('[data-work-item-list="inbox"]'),
        allWorkRoot: document.querySelector('[data-work-item-list="allwork"]'),
        inboxSearchInput: document.getElementById('inboxSearchInput'),
        inboxSearchWrap: document.querySelector('.wc-search-wrap'),
        inboxSearchSuggestions: document.getElementById('inboxSearchSuggestions'),
        inboxFilterTypeSelect: document.getElementById('inboxFilterType'),
        btnInboxFilterApply: document.getElementById('btnInboxFilterApply'),
        btnInboxFilterReset: document.getElementById('btnInboxFilterReset'),
        inboxMasterCheckbox: document.getElementById('inboxMasterCheckbox'),
        inboxSelectionLabel: document.getElementById('inboxSelectionLabel'),
        inboxDefaultState: document.getElementById('inboxDefaultState'),
        inboxBulkState: document.getElementById('inboxBulkState'),
        btnBulkAccept: document.getElementById('btnBulkAccept'),
        btnBulkSnooze: document.getElementById('btnBulkSnooze'),
        btnBulkReturn: document.getElementById('btnBulkReturn'),
        allWorkScopeButtons: Array.from(document.querySelectorAll('[data-allwork-scope]'))
    };

    if (!elements.rowTemplate || !elements.inboxRoot || !elements.allWorkRoot || typeof window.WorkItemList !== 'function') {
        return;
    }

    const state = {
        activeTab: readQueryTab(),
        activeScope: readStoredScope(),
        workItems: [],
        visibleCount: PAGE_SIZE,
        selectedItemId: null,
        selectedItemIds: new Set(),
        filterText: '',
        filterType: '',
        searchSuggestions: [],
        searchSuggestionIndex: -1
    };

    const initialTabFromServer = normalizeTab((elements.workCenterPage?.dataset.initialTab || '').toLowerCase());
    if (!window.location.search || !new URLSearchParams(window.location.search).has('tab')) {
        state.activeTab = initialTabFromServer;
    }

    const baseItems = [
        { type: 'Task', status: 'NEW', title: 'Menolyt sosyal medya kit - AZ/UA', source: 'Icra / Uygulama', context: 'Project Atlas', assignedBy: 'Lina K.', meta: 'Menolyt Rebrand & Genisleme', requiredAction: 'Icerik onayla' },
        { type: 'Issue', status: 'WAITING_INFO', title: 'Inventory sync failed for WH-04', source: 'Supply Chain', context: 'Standalone', assignedBy: 'Ops Bot', meta: '8 SKU update failed due to stale lock.', requiredAction: 'Kayitlari incele' },
        { type: 'Task', status: 'IN_REVIEW', title: 'Vendor onboarding policy review', source: 'Procurement', context: 'Project Horizon', assignedBy: 'Mert A.', meta: 'SLA clauses require legal pre-check.', requiredAction: 'Review et' },
        { type: 'Meeting', status: 'DONE', title: 'Risk committee weekly triage', source: 'PMO', context: 'Program Phoenix', assignedBy: 'Deniz C.', meta: 'Agenda finalization and attendee confirmation.', requiredAction: 'Katilimi onayla' },
        { type: 'Issue', status: 'CLOSED', title: 'Customer escalation context note', source: 'CRM', context: 'Standalone', assignedBy: 'Bora T.', meta: 'Contains escalation timeline summary.', requiredAction: 'Notu dogrula' },
        { type: 'Task', status: 'NEW', title: 'Complete Q2 warehouse audit prep', source: 'Warehouse', context: 'Project Atlas', assignedBy: 'Aylin E.', meta: 'Attach variance report before submission.', requiredAction: 'Kanit paketi hazirla' },
        { type: 'Issue', status: 'IN_REVIEW', title: 'Payment export timeout on large batch', source: 'Accounting', context: 'Standalone', assignedBy: 'System Monitor', meta: 'Exports above 1200 records exceed timeout.', requiredAction: 'Fix onceliklendir' },
        { type: 'Task', status: 'WAITING_INFO', title: 'Contract amendment quality gate', source: 'Legal', context: 'Project Horizon', assignedBy: 'Nadia P.', meta: 'Clause wording waiting business confirmation.', requiredAction: 'Maddeleri onayla' },
        { type: 'Meeting', status: 'DONE', title: 'Sprint retrospective moderation', source: 'Engineering', context: 'Program Neon', assignedBy: 'Kerem I.', meta: 'Facilitator needed for action items.', requiredAction: 'Toplantiyi yonet' },
        { type: 'Task', status: 'NEW', title: 'Regulatory submission reminder', source: 'Compliance', context: 'Standalone', assignedBy: 'Ela R.', meta: 'Submission package ready for acceptance.', requiredAction: 'Paket uygunlugunu kontrol et' },
        { type: 'Note', status: 'CLOSED', title: 'Q2 All Hands Summary', source: 'Corporate', context: 'Standalone', assignedBy: 'Ceren K.', meta: 'Summary of the all hands meeting.', requiredAction: 'Oku' }
    ];

    const buildMockItems = (count) => {
        const today = new Date('2026-03-24T09:00:00');
        const priorities = ['Yuksek', 'Orta', 'Dusuk'];
        return Array.from({ length: count }, (_, index) => {
            const template = baseItems[index % baseItems.length];
            const sequence = index + 1;
            const createdDate = new Date(today);
            createdDate.setDate(today.getDate() - (index % 9));
            const dueDate = new Date(createdDate);
            dueDate.setDate(createdDate.getDate() + ((index % 5) + 1));

            return {
                id: `wi-${String(sequence).padStart(3, '0')}`,
                type: template.type,
                status: template.status,
                priority: priorities[index % priorities.length],
                role: index % 3 === 0 ? 'Owner' : (index % 2 === 0 ? 'Reviewer' : 'Informed'),
                title: sequence > baseItems.length ? `${template.title} #${sequence}` : template.title,
                source: template.source,
                context: template.context,
                assignedBy: template.assignedBy,
                createdDate: createdDate.toISOString().slice(0, 10),
                dueDate: dueDate.toISOString().slice(0, 10),
                meta: template.meta,
                requiredAction: template.requiredAction,
                isUnread: index % 3 !== 0
            };
        });
    };

    const notify = (message, type) => {
        if (typeof window.showToast === 'function') {
            window.showToast(message, type || 'info');
            return;
        }
        console.log('[WorkCenter]', type || 'info', message);
    };

    const syncTabToUrl = (tab, replace) => {
        const targetTab = normalizeTab(tab);
        const url = new URL(window.location.href);
        url.searchParams.set('tab', targetTab);

        if (replace) {
            window.history.replaceState({ tab: targetTab }, '', url);
            return;
        }

        const currentTab = readQueryTab();
        if (currentTab === targetTab) {
            return;
        }
        window.history.pushState({ tab: targetTab }, '', url);
    };

    const getInboxItems = () => state.workItems.filter((item) => INBOX_ALLOWED_STATUSES.has(item.status));

    const getFilteredInboxItems = () => {
        let items = getInboxItems();
        if (state.filterText) {
            const q = state.filterText.toLowerCase();
            items = items.filter((item) =>
                (item.title || '').toLowerCase().includes(q) ||
                (item.source || '').toLowerCase().includes(q) ||
                (item.context || '').toLowerCase().includes(q) ||
                (item.assignedBy || '').toLowerCase().includes(q)
            );
        }
        if (state.filterType) {
            items = items.filter((item) => (item.type || '').toLowerCase() === state.filterType.toLowerCase());
        }
        return items;
    };

    const getVisibleInboxItems = () => getFilteredInboxItems().slice(0, state.visibleCount);

    const getAllWorkItems = () => {
        const items = state.workItems.slice();
        if (state.activeScope === 'all') {
            return items;
        }
        return items.filter((item) => (item.type || '').toLowerCase() === state.activeScope);
    };

    const closeSearchSuggestions = () => {
        if (!elements.inboxSearchSuggestions) {
            return;
        }
        state.searchSuggestions = [];
        state.searchSuggestionIndex = -1;
        elements.inboxSearchSuggestions.innerHTML = '';
        elements.inboxSearchSuggestions.classList.add('d-none');
        elements.inboxSearchInput?.setAttribute('aria-expanded', 'false');
    };

    const buildSearchSuggestions = (query) => {
        const q = (query || '').trim().toLowerCase();
        if (q.length < 2) {
            return [];
        }

        return getInboxItems()
            .map((item) => ({ id: item.id, title: item.title || '' }))
            .filter((item) => item.title.toLowerCase().includes(q))
            .sort((a, b) => a.title.localeCompare(b.title))
            .slice(0, 6);
    };

    const renderSearchSuggestions = () => {
        if (!elements.inboxSearchSuggestions) {
            return;
        }

        const items = state.searchSuggestions;
        if (!items.length) {
            closeSearchSuggestions();
            return;
        }

        elements.inboxSearchSuggestions.innerHTML = items.map((item, index) => {
            const activeClass = index === state.searchSuggestionIndex ? ' is-active' : '';
            return `<li role="option" aria-selected="${index === state.searchSuggestionIndex}" class="wc-search-suggestion-item${activeClass}">
                        <button type="button" class="wc-search-suggestion-btn" data-suggestion-id="${item.id}">${item.title}</button>
                    </li>`;
        }).join('');

        elements.inboxSearchSuggestions.classList.remove('d-none');
        elements.inboxSearchInput?.setAttribute('aria-expanded', 'true');
    };

    const applySuggestion = (suggestionId) => {
        const selected = state.searchSuggestions.find((item) => item.id === suggestionId);
        if (!selected) {
            return;
        }

        state.filterText = selected.title;
        state.selectedItemId = selected.id;
        state.visibleCount = PAGE_SIZE;

        if (elements.inboxSearchInput) {
            elements.inboxSearchInput.value = selected.title;
            elements.inboxSearchInput.classList.add('border-primary', 'bg-label-primary');
        }

        closeSearchSuggestions();
        renderCurrentList();
    };

    const updateLoadMoreVisibility = () => {
        if (!elements.inboxLoadMoreBtn) {
            return;
        }

        const hasMore = getFilteredInboxItems().length > getVisibleInboxItems().length;
        const shouldShow = state.activeTab === 'inbox' && hasMore;
        elements.inboxLoadMoreBtn.classList.toggle('d-none', !shouldShow);
    };

    const setLoadMoreBusy = (isBusy) => {
        if (!elements.inboxLoadMoreBtn) {
            return;
        }

        elements.inboxLoadMoreBtn.disabled = Boolean(isBusy);
        if (isBusy) {
            elements.inboxLoadMoreBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>${l10n.Loading || 'Loading...'} `;
            return;
        }
        elements.inboxLoadMoreBtn.textContent = LOAD_MORE_DEFAULT_LABEL;
    };

    const updateBulkActionBar = () => {
        if (!elements.inboxMasterCheckbox || !elements.inboxBulkState || !elements.inboxDefaultState || !elements.inboxSelectionLabel) {
            return;
        }

        const count = state.selectedItemIds.size;
        const visibleItems = getVisibleInboxItems();

        if (count > 0) {
            elements.inboxDefaultState.classList.add('d-none');
            elements.inboxDefaultState.classList.remove('d-flex');
            elements.inboxBulkState.classList.remove('d-none');
            elements.inboxBulkState.classList.add('d-flex');
            elements.inboxSelectionLabel.textContent = `${count} Kayıt Seçildi`;
            elements.inboxMasterCheckbox.checked = true;
            const allSelected = visibleItems.length > 0 && visibleItems.every((i) => state.selectedItemIds.has(i.id));
            elements.inboxMasterCheckbox.indeterminate = !allSelected;
            return;
        }

        elements.inboxBulkState.classList.add('d-none');
        elements.inboxBulkState.classList.remove('d-flex');
        elements.inboxDefaultState.classList.remove('d-none');
        elements.inboxDefaultState.classList.add('d-flex');
        elements.inboxSelectionLabel.textContent = 'Tümünü Seç';
        elements.inboxMasterCheckbox.checked = false;
        elements.inboxMasterCheckbox.indeterminate = false;
    };

    const setScopeButtons = () => {
        elements.allWorkScopeButtons.forEach((button) => {
            const scope = normalizeScope(button.getAttribute('data-allwork-scope'));
            button.classList.toggle('btn-primary', scope === state.activeScope);
            button.classList.toggle('btn-outline-primary', scope !== state.activeScope);
        });
    };

    const hideAllWorkDecisionUi = () => {
        if (!elements.allWorkRoot) {
            return;
        }

        const rows = elements.allWorkRoot.querySelectorAll('[data-work-item-row]');
        rows.forEach((row) => {
            row.querySelector('.inbox-row__checkbox')?.classList.add('d-none');
            row.querySelector('.inbox-row__actions')?.classList.add('d-none');
        });
    };

    const loadWorkItems = async () => Promise.resolve(buildMockItems(40));

    const navigateToTaskDetail = (itemId, tab) => {
        const returnUrl = encodeURIComponent('/WorkCenter?tab=' + tab);
        window.location.href = '/WorkCenter/Task/' + encodeURIComponent(itemId) + '?returnUrl=' + returnUrl;
    };

    const inboxList = new window.WorkItemList({
        root: elements.inboxRoot,
        rowTemplate: elements.rowTemplate,
        l10n: l10n,
        onSelect: (itemId) => {
            const selected = getInboxItems().find((item) => item.id === itemId);
            if (!selected) {
                return;
            }

            if (selected.type === 'Task') {
                navigateToTaskDetail(itemId, 'inbox');
                return;
            }

            selected.isUnread = false;
            state.selectedItemId = itemId;
            renderCurrentList();
            notify(l10n.ActionDetailOpened || 'Item detail opened.', 'info');
        },
        onAction: (itemId, action) => {
            const selected = getInboxItems().find((item) => item.id === itemId);
            if (!selected) {
                return;
            }

            if (action === 'accept') {
                selected.status = 'DONE';
                if (state.selectedItemId === itemId) {
                    state.selectedItemId = null;
                }
                state.selectedItemIds.delete(itemId);
                renderCurrentList();
                notify(l10n.ActionAcceptSuccess || 'Item accepted and removed from Inbox.', 'success');
                return;
            }

            if (action === 'return' || action === 'reject') {
                notify(l10n.ActionReturnSuccess || 'Item was returned (mock).', 'warning');
                return;
            }

            if (action === 'decline') {
                notify(l10n.ActionDeclineSuccess || 'Item was declined (mock).', 'warning');
                return;
            }

            if (action === 'propose-time') {
                notify(l10n.ActionProposeTimeSuccess || 'New time proposed (mock).', 'info');
                return;
            }

            if (action === 'reassign') {
                notify(l10n.ActionReassignSuccess || 'Reassign flow started (mock).', 'info');
                return;
            }

            if (action === 'snooze') {
                notify(l10n.ActionSnoozeSuccess || 'Item snoozed (mock).', 'info');
            }
        }
    });

    const allWorkList = new window.WorkItemList({
        root: elements.allWorkRoot,
        rowTemplate: elements.rowTemplate,
        l10n: l10n,
        onSelect: (itemId) => {
            const selected = state.workItems.find((item) => item.id === itemId);
            if (selected && selected.type === 'Task') {
                navigateToTaskDetail(itemId, 'all');
                return;
            }
            state.selectedItemId = itemId;
            renderCurrentList();
        },
        onAction: () => { }
    });

    const applyViewSwitchRules = () => {
        const isAllWorkActive = state.activeTab === 'all';
        elements.allWorkViewSwitchHost?.classList.toggle('d-none', !isAllWorkActive);
    };

    const renderCurrentList = () => {
        if (state.activeTab === 'all') {
            allWorkList.setSelectedItemId(state.selectedItemId);
            allWorkList.setItems(getAllWorkItems());
            hideAllWorkDecisionUi();
            updateLoadMoreVisibility();
            return;
        }

        if (typeof inboxList.setSelectedItemIds === 'function') {
            inboxList.setSelectedItemIds(state.selectedItemIds);
        }
        inboxList.setSelectedItemId(state.selectedItemId);
        inboxList.setItems(getVisibleInboxItems());
        updateLoadMoreVisibility();
        updateBulkActionBar();
    };

    let suppressPushState = false;

    const activateTab = (tab) => {
        const normalizedTab = normalizeTab(tab);
        const trigger = normalizedTab === 'all' ? elements.allWorkTabTrigger : elements.inboxTabTrigger;
        if (!trigger || !window.bootstrap?.Tab) {
            state.activeTab = normalizedTab;
            renderCurrentList();
            return;
        }

        const tabInstance = window.bootstrap.Tab.getOrCreateInstance(trigger);
        tabInstance.show();
    };

    const bindEvents = () => {
        elements.inboxTabTrigger?.addEventListener('shown.bs.tab', () => {
            state.activeTab = 'inbox';
            applyViewSwitchRules();
            renderCurrentList();
            if (!suppressPushState) {
                syncTabToUrl('inbox', false);
            }
        });

        elements.allWorkTabTrigger?.addEventListener('shown.bs.tab', () => {
            state.activeTab = 'all';
            applyViewSwitchRules();
            renderCurrentList();
            window.AllWork?.onTabActivated?.();
            if (!suppressPushState) {
                syncTabToUrl('all', false);
            }
        });

        window.addEventListener('popstate', () => {
            suppressPushState = true;
            activateTab(readQueryTab());
            suppressPushState = false;
        });

        elements.allWorkScopeButtons.forEach((button) => {
            button.addEventListener('click', () => {
                state.activeScope = normalizeScope(button.getAttribute('data-allwork-scope'));
                writeStoredScope(state.activeScope);
                setScopeButtons();
                if (state.activeTab === 'all') {
                    renderCurrentList();
                }
            });
        });

        elements.inboxLoadMoreBtn?.addEventListener('click', async () => {
            setLoadMoreBusy(true);
            try {
                await Promise.resolve();
                state.visibleCount += PAGE_SIZE;
                renderCurrentList();
            } finally {
                setLoadMoreBusy(false);
            }
        });

        elements.inboxSearchInput?.addEventListener('input', () => {
            state.filterText = elements.inboxSearchInput.value.trim();
            state.visibleCount = PAGE_SIZE;
            elements.inboxSearchInput.classList.toggle('border-primary', !!state.filterText);
            elements.inboxSearchInput.classList.toggle('bg-label-primary', !!state.filterText);
            state.searchSuggestions = buildSearchSuggestions(state.filterText);
            state.searchSuggestionIndex = state.searchSuggestions.length ? 0 : -1;
            renderSearchSuggestions();
            renderCurrentList();
        });

        elements.inboxSearchInput?.addEventListener('keydown', (event) => {
            if (!state.searchSuggestions.length) {
                if (event.key === 'Escape') {
                    closeSearchSuggestions();
                }
                return;
            }

            if (event.key === 'ArrowDown') {
                event.preventDefault();
                state.searchSuggestionIndex = (state.searchSuggestionIndex + 1) % state.searchSuggestions.length;
                renderSearchSuggestions();
                return;
            }

            if (event.key === 'ArrowUp') {
                event.preventDefault();
                state.searchSuggestionIndex = (state.searchSuggestionIndex - 1 + state.searchSuggestions.length) % state.searchSuggestions.length;
                renderSearchSuggestions();
                return;
            }

            if (event.key === 'Enter') {
                if (state.searchSuggestionIndex < 0) {
                    return;
                }
                event.preventDefault();
                const selected = state.searchSuggestions[state.searchSuggestionIndex];
                applySuggestion(selected.id);
                return;
            }

            if (event.key === 'Escape') {
                closeSearchSuggestions();
            }
        });

        elements.inboxSearchSuggestions?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-suggestion-id]');
            if (!button) {
                return;
            }
            applySuggestion(button.getAttribute('data-suggestion-id'));
        });

        document.addEventListener('click', (event) => {
            if (!elements.inboxSearchWrap || elements.inboxSearchWrap.contains(event.target)) {
                return;
            }
            closeSearchSuggestions();
        });

        elements.btnInboxFilterApply?.addEventListener('click', () => {
            state.filterType = elements.inboxFilterTypeSelect?.value || '';
            state.visibleCount = PAGE_SIZE;
            renderCurrentList();
        });

        elements.btnInboxFilterReset?.addEventListener('click', () => {
            state.filterType = '';
            state.filterText = '';
            state.visibleCount = PAGE_SIZE;
            closeSearchSuggestions();
            if (elements.inboxFilterTypeSelect) {
                elements.inboxFilterTypeSelect.value = '';
            }
            if (elements.inboxSearchInput) {
                elements.inboxSearchInput.value = '';
                elements.inboxSearchInput.classList.remove('border-primary', 'bg-label-primary');
            }
            renderCurrentList();
        });

        elements.inboxMasterCheckbox?.addEventListener('change', (event) => {
            const isChecked = event.target.checked;
            const visibleItems = getVisibleInboxItems();

            if (isChecked) {
                visibleItems.forEach((item) => state.selectedItemIds.add(item.id));
            } else {
                state.selectedItemIds.clear();
            }
            renderCurrentList();
        });

        elements.inboxRoot?.addEventListener('change', (event) => {
            if (!event.target.classList.contains('item-checkbox')) {
                return;
            }

            const row = event.target.closest('[data-work-item-row]');
            if (!row) {
                return;
            }

            const itemId = row.getAttribute('data-item-id');
            if (!itemId) {
                return;
            }

            if (event.target.checked) {
                state.selectedItemIds.add(itemId);
            } else {
                state.selectedItemIds.delete(itemId);
            }
            updateBulkActionBar();
        });

        elements.btnBulkAccept?.addEventListener('click', () => {
            const selectedIds = Array.from(state.selectedItemIds);
            selectedIds.forEach((id) => {
                const item = state.workItems.find((x) => x.id === id);
                if (item) {
                    item.status = 'DONE';
                }
            });
            const count = selectedIds.length;
            state.selectedItemIds.clear();
            notify(`${count} kayıt başarıyla onaylandı.`, 'success');
            renderCurrentList();
        });

        elements.btnBulkSnooze?.addEventListener('click', () => {
            notify(`${state.selectedItemIds.size} kayıt ertelendi.`, 'warning');
            state.selectedItemIds.clear();
            renderCurrentList();
        });

        elements.btnBulkReturn?.addEventListener('click', () => {
            notify(`${state.selectedItemIds.size} kayıt iade edildi.`, 'danger');
            state.selectedItemIds.clear();
            renderCurrentList();
        });
    };

    const init = async () => {
        bindEvents();
        setScopeButtons();

        if (window.bootstrap?.Tooltip) {
            const tooltipButtons = Array.from(document.querySelectorAll('#workCenterPage [data-bs-toggle="tooltip"]'));
            tooltipButtons.forEach((button) => window.bootstrap.Tooltip.getOrCreateInstance(button));
        }

        inboxList.setLoading(true);
        allWorkList.setLoading(true);
        elements.inboxLoadMoreBtn?.classList.add('d-none');
        setLoadMoreBusy(false);

        try {
            state.workItems = await loadWorkItems();
        } finally {
            inboxList.setLoading(false);
            allWorkList.setLoading(false);

            suppressPushState = true;
            activateTab(state.activeTab);
            suppressPushState = false;

            applyViewSwitchRules();
            renderCurrentList();
            syncTabToUrl(state.activeTab, true);
        }
    };

    init();
})();
