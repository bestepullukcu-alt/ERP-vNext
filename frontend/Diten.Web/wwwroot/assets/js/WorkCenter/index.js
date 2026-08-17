'use strict';

(function () {
    const l10n = window.L10n || {};
    const mockData = window.WorkCenterMockData || null;
    const PAGE_SIZE = 20;
    const LOAD_MORE_DEFAULT_LABEL = l10n.LoadMore || 'Load More';
    const SCOPE_STORAGE_KEY = 'workcenter.activeScope';

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

    const currentUserName = mockData?.currentUser?.name || '';

    const initialTabFromServer = normalizeTab((elements.workCenterPage?.dataset.initialTab || '').toLowerCase());
    if (!window.location.search || !new URLSearchParams(window.location.search).has('tab')) {
        state.activeTab = initialTabFromServer;
    }

    const notify = (message, type) => {
        if (typeof window.showToast === 'function') {
            window.showToast(message, type || 'info');
            return;
        }

        console.log('[WorkCenter]', type || 'info', message);
    };

    const resolveViewerRole = (item) => {
        if (!currentUserName) {
            return '';
        }

        if (item.approver === currentUserName) {
            return 'Approver';
        }

        if (item.reviewer === currentUserName) {
            return 'Reviewer';
        }

        if (item.assignee === currentUserName) {
            return 'Owner';
        }

        if (item.creator === currentUserName) {
            return 'Creator';
        }

        return '';
    };

    const buildFlags = (item) => {
        const flags = [];
        const progress = String(item.checklistProgress || '').split('/');
        const completed = parseInt(progress[0], 10);
        const total = parseInt(progress[1], 10);
        const checklistComplete = Number.isFinite(completed) && Number.isFinite(total) && total > 0 && completed >= total;

        if (item.blocked) {
            flags.push({
                label: 'Blocked',
                kind: 'danger',
                title: item.blockedReason || 'Blocked'
            });
        }

        if (item.dependencySummary) {
            flags.push({
                label: 'Dependency',
                kind: 'warning',
                title: item.dependencySummary
            });
        }

        if (item.waitingInfo) {
            flags.push({
                label: `Waiting: ${item.waitingInfo}`,
                kind: 'warning',
                title: `Waiting for ${item.waitingInfo}`
            });
        }

        if (item.reviewRequired) {
            flags.push({
                label: 'Review',
                kind: 'info',
                title: item.reviewer ? `Reviewer: ${item.reviewer}` : 'Review required'
            });
        }

        if (item.approvalRequired) {
            flags.push({
                label: 'Approval',
                kind: 'primary',
                title: item.approver ? `Approver: ${item.approver}` : 'Approval required'
            });
        }

        if (item.hasChecklist) {
            flags.push({
                label: `Checklist ${item.checklistProgress || '0/0'}`,
                kind: checklistComplete ? 'success' : 'secondary',
                title: `Checklist progress ${item.checklistProgress || '0/0'}`
            });
        }

        if (item.hasSubtasks) {
            flags.push({
                label: 'Subtasks',
                kind: 'secondary',
                title: 'Contains subtasks'
            });
        }

        return flags;
    };

    const refreshDerivedFields = (item) => {
        if (!item) {
            return item;
        }

        const dueState = typeof mockData?.computeDueState === 'function'
            ? mockData.computeDueState(item.dueDate)
            : { kind: 'unknown', label: '-' };

        item.assignedBy = item.creator;
        item.displayType = item.displayType || ((item.type || '').charAt(0).toUpperCase() + (item.type || '').slice(1));
        item.displayPriority = item.displayPriority || ((item.priority || '').charAt(0).toUpperCase() + (item.priority || '').slice(1));
        item.viewerRole = resolveViewerRole(item);
        item.dueStateKind = dueState.kind;
        item.dueStateLabel = dueState.label;
        item.flags = buildFlags(item);

        return item;
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

    const getInboxItems = () => state.workItems.slice();

    const getFilteredInboxItems = () => {
        let items = getInboxItems();

        if (state.filterText) {
            const query = state.filterText.toLowerCase();
            items = items.filter((item) =>
                (item.id || '').toLowerCase().includes(query) ||
                (item.title || '').toLowerCase().includes(query) ||
                (item.source || '').toLowerCase().includes(query) ||
                (item.context || '').toLowerCase().includes(query) ||
                (item.assignedBy || '').toLowerCase().includes(query)
            );
        }

        if (state.filterType) {
            items = items.filter((item) => (item.type || '').toLowerCase() === state.filterType.toLowerCase());
        }

        return items;
    };

    const getVisibleInboxItems = () => getFilteredInboxItems().slice(0, state.visibleCount);

    const getBulkSelectableItems = (items) => {
        const sourceItems = Array.isArray(items) ? items : [];
        return sourceItems.filter((item) => {
            const config = mockData?.getListActionConfig ? mockData.getListActionConfig(item) : null;
            return Boolean(config?.bulkSelectable);
        });
    };

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
        const normalized = (query || '').trim().toLowerCase();
        if (normalized.length < 2) {
            return [];
        }

        return getInboxItems()
            .map((item) => ({ id: item.id, title: item.title || '' }))
            .filter((item) => item.id.toLowerCase().includes(normalized) || item.title.toLowerCase().includes(normalized))
            .sort((a, b) => a.title.localeCompare(b.title))
            .slice(0, 6);
    };

    const renderSearchSuggestions = () => {
        if (!elements.inboxSearchSuggestions) {
            return;
        }

        if (!state.searchSuggestions.length) {
            closeSearchSuggestions();
            return;
        }

        elements.inboxSearchSuggestions.innerHTML = state.searchSuggestions.map((item, index) => {
            const activeClass = index === state.searchSuggestionIndex ? ' is-active' : '';
            return `<li role="option" aria-selected="${index === state.searchSuggestionIndex}" class="wc-search-suggestion-item${activeClass}">
                        <button type="button" class="wc-search-suggestion-btn" data-suggestion-id="${item.id}">${item.id} · ${item.title}</button>
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
        elements.inboxLoadMoreBtn.classList.toggle('d-none', !(state.activeTab === 'inbox' && hasMore));
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
        const selectableVisibleItems = getBulkSelectableItems(getVisibleInboxItems());

        elements.inboxMasterCheckbox.disabled = selectableVisibleItems.length === 0;

        if (count > 0) {
            elements.inboxDefaultState.classList.add('d-none');
            elements.inboxDefaultState.classList.remove('d-flex');
            elements.inboxBulkState.classList.remove('d-none');
            elements.inboxBulkState.classList.add('d-flex');
            elements.inboxSelectionLabel.textContent = `${count} Kayit Secildi`;
            elements.inboxMasterCheckbox.checked = true;
            const allSelected = selectableVisibleItems.length > 0 && selectableVisibleItems.every((item) => state.selectedItemIds.has(item.id));
            elements.inboxMasterCheckbox.indeterminate = selectableVisibleItems.length > 0 && !allSelected;
            return;
        }

        elements.inboxBulkState.classList.add('d-none');
        elements.inboxBulkState.classList.remove('d-flex');
        elements.inboxDefaultState.classList.remove('d-none');
        elements.inboxDefaultState.classList.add('d-flex');
        elements.inboxSelectionLabel.textContent = 'Tumunu Sec';
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

    const loadWorkItems = async () => {
        const items = typeof mockData?.buildMockItems === 'function' ? mockData.buildMockItems() : [];
        return Promise.resolve(items.map((item) => refreshDerivedFields(item)));
    };

    const navigateToTaskDetail = (itemId, tab) => {
        const returnUrl = encodeURIComponent('/WorkCenter?tab=' + tab);
        window.location.href = '/WorkCenter/Task/' + encodeURIComponent(itemId) + '?returnUrl=' + returnUrl;
    };

    const navigateToMeetingDetail = (itemId, tab) => {
        const returnUrl = encodeURIComponent('/WorkCenter?tab=' + tab);
        window.location.href = '/WorkCenter/Meeting/' + encodeURIComponent(itemId) + '?returnUrl=' + returnUrl;
    };

    const openWorkItem = (item, tab) => {
        if (!item) {
            return;
        }

        const itemType = (item.type || '').toLowerCase();
        if (itemType === 'task') {
            navigateToTaskDetail(item.id, tab);
            return;
        }
        if (itemType === 'meeting') {
            navigateToMeetingDetail(item.id, tab);
            return;
        }

        item.isUnread = false;
        state.selectedItemId = item.id;
        renderCurrentList();
        notify(l10n.ActionDetailOpened || 'Item detail opened.', 'info');
    };

    const pruneSelection = () => {
        const nextSelection = new Set();
        Array.from(state.selectedItemIds).forEach((id) => {
            const item = state.workItems.find((candidate) => candidate.id === id);
            const config = item && mockData?.getListActionConfig ? mockData.getListActionConfig(item) : null;
            if (item && config?.bulkSelectable) {
                nextSelection.add(id);
            }
        });
        state.selectedItemIds = nextSelection;
    };

    const applyViewSwitchRules = () => {
        const isAllWorkActive = state.activeTab === 'all';
        elements.allWorkViewSwitchHost?.classList.toggle('d-none', !isAllWorkActive);
    };

    const renderCurrentList = () => {
        pruneSelection();

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

    const handleInboxAction = (selected, action) => {
        if (!selected) {
            return;
        }

        switch (action) {
            case 'approve':
                selected.status = 'Pending Acceptance';
                selected.approvalRequired = false;
                selected.approver = '';
                selected.viewerRole = resolveViewerRole(selected);
                notify('Approval completed. Item moved to Pending Acceptance.', 'success');
                break;

            case 'accept':
                selected.status = 'Open';
                selected.approvalRequired = false;
                notify('Item accepted and moved to Open.', 'success');
                state.selectedItemIds.delete(selected.id);
                break;

            case 'reject':
                selected.status = 'Cancelled';
                selected.approvalRequired = false;
                selected.blocked = false;
                notify('Item rejected and moved to Cancelled.', 'warning');
                state.selectedItemIds.delete(selected.id);
                break;

            case 'start-work':
                selected.status = 'In Progress';
                selected.blocked = false;
                selected.blockedReason = '';
                notify('Execution started.', 'success');
                break;

            case 'request-info':
                selected.status = 'Waiting for Information';
                selected.waitingInfo = selected.waitingInfo || 'Business Owner';
                notify('Item moved to Waiting for Information.', 'warning');
                break;

            case 'reject-review':
                selected.status = 'In Progress';
                selected.blocked = false;
                notify('Review rejected. Item returned to In Progress.', 'warning');
                break;

            case 'reassign':
                notify(l10n.ActionReassignSuccess || 'Reassign workflow started (mock).', 'info');
                break;

            case 'inspect-blocker':
                openWorkItem(selected, 'inbox');
                notify(selected.blockedReason || 'Blocking reason opened.', 'warning');
                break;

            case 'continue':
            case 'investigate':
            case 'follow-up':
            case 'review':
            case 'view-summary':
            case 'view-reason':
            case 'history':
            case 'open-detail':
                openWorkItem(selected, 'inbox');
                break;

            default:
                notify('No mock action is configured for this item.', 'info');
                break;
        }

        refreshDerivedFields(selected);
        renderCurrentList();
    };

    const inboxList = new window.WorkItemList({
        root: elements.inboxRoot,
        rowTemplate: elements.rowTemplate,
        l10n: l10n,
        onSelect: (itemId) => {
            const selected = getInboxItems().find((item) => item.id === itemId);
            openWorkItem(selected, 'inbox');
        },
        onAction: (itemId, action) => {
            const selected = getInboxItems().find((item) => item.id === itemId);
            handleInboxAction(selected, action);
        }
    });

    const allWorkList = new window.WorkItemList({
        root: elements.allWorkRoot,
        rowTemplate: elements.rowTemplate,
        l10n: l10n,
        onSelect: (itemId) => {
            const selected = state.workItems.find((item) => item.id === itemId);
            if (selected) {
                openWorkItem(selected, 'all');
            }
        },
        onAction: () => { }
    });

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
            const selectableVisibleItems = getBulkSelectableItems(getVisibleInboxItems());

            if (isChecked) {
                selectableVisibleItems.forEach((item) => state.selectedItemIds.add(item.id));
            } else {
                selectableVisibleItems.forEach((item) => state.selectedItemIds.delete(item.id));
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
                const item = state.workItems.find((candidate) => candidate.id === id);
                if (item) {
                    item.status = 'Open';
                    item.approvalRequired = false;
                    refreshDerivedFields(item);
                }
            });

            const count = selectedIds.length;
            state.selectedItemIds.clear();
            notify(`${count} item moved to Open.`, 'success');
            renderCurrentList();
        });

        elements.btnBulkSnooze?.addEventListener('click', () => {
            notify(`${state.selectedItemIds.size} item snoozed in mock mode.`, 'warning');
            state.selectedItemIds.clear();
            renderCurrentList();
        });

        elements.btnBulkReturn?.addEventListener('click', () => {
            Array.from(state.selectedItemIds).forEach((id) => {
                const item = state.workItems.find((candidate) => candidate.id === id);
                if (item) {
                    item.status = 'Cancelled';
                    refreshDerivedFields(item);
                }
            });
            notify(`${state.selectedItemIds.size} item returned in mock mode.`, 'danger');
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
