'use strict';

(function () {
    const l10n = window.L10n || {};
    const removedItemIds = new Set();
    const PAGE_SIZE = 20;
    const LOAD_MORE_DEFAULT_LABEL = l10n.LoadMore || 'Load More';

    const elements = {
        inboxTabTrigger: document.getElementById('workcenter-inbox-tab'),
        allWorkTabTrigger: document.getElementById('workcenter-allwork-tab'),
        allWorkViewSwitchHost: document.getElementById('allWorkViewSwitch'),
        viewSwitchButtons: Array.from(document.querySelectorAll('[data-view-switch]')),
        inboxLoadMoreBtn: document.getElementById('inboxLoadMoreBtn'),
        rowTemplate: document.getElementById('inboxRowTemplate'),
        inboxRoot: document.querySelector('[data-work-item-list="inbox"]'),
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
        btnBulkReturn: document.getElementById('btnBulkReturn')
    };

    if (!elements.rowTemplate || !elements.inboxRoot || typeof window.WorkItemList !== 'function') {
        return;
    }

    const baseInboxItems = [
        {
            type: 'Task',
            title: 'Menolyt sosyal medya kit - AZ/UA',
            source: 'Icra / Uygulama',
            context: 'Project Atlas',
            assignedBy: 'Lina K.',
            meta: 'Menolyt Rebrand & Genisleme',
            requiredAction: 'Icerik onayla'
        },
        {
            type: 'Issue',
            title: 'Inventory sync failed for WH-04',
            source: 'Supply Chain',
            context: 'Standalone',
            assignedBy: 'Ops Bot',
            meta: '8 SKU update failed due to stale lock.',
            requiredAction: 'Kayitlari incele'
        },
        {
            type: 'Task',
            title: 'Vendor onboarding policy review',
            source: 'Procurement',
            context: 'Project Horizon',
            assignedBy: 'Mert A.',
            meta: 'SLA clauses require legal pre-check.',
            requiredAction: 'Review et'
        },
        {
            type: 'Meeting',
            title: 'Risk committee weekly triage',
            source: 'PMO',
            context: 'Program Phoenix',
            assignedBy: 'Deniz C.',
            meta: 'Agenda finalization and attendee confirmation.',
            requiredAction: 'Katilimi onayla'
        },
        {
            type: 'Issue',
            title: 'Customer escalation context note',
            source: 'CRM',
            context: 'Standalone',
            assignedBy: 'Bora T.',
            meta: 'Contains escalation timeline summary.',
            requiredAction: 'Notu dogrula'
        },
        {
            type: 'Task',
            title: 'Complete Q2 warehouse audit prep',
            source: 'Warehouse',
            context: 'Project Atlas',
            assignedBy: 'Aylin E.',
            meta: 'Attach variance report before submission.',
            requiredAction: 'Kanit paketi hazirla'
        },
        {
            type: 'Issue',
            title: 'Payment export timeout on large batch',
            source: 'Accounting',
            context: 'Standalone',
            assignedBy: 'System Monitor',
            meta: 'Exports above 1200 records exceed timeout.',
            requiredAction: 'Fix onceliklendir'
        },
        {
            type: 'Task',
            title: 'Contract amendment quality gate',
            source: 'Legal',
            context: 'Project Horizon',
            assignedBy: 'Nadia P.',
            meta: 'Clause wording waiting business confirmation.',
            requiredAction: 'Maddeleri onayla'
        },
        {
            type: 'Meeting',
            title: 'Sprint retrospective moderation',
            source: 'Engineering',
            context: 'Program Neon',
            assignedBy: 'Kerem I.',
            meta: 'Facilitator needed for action items.',
            requiredAction: 'Toplantiyi yonet'
        },
        {
            type: 'Task',
            title: 'Regulatory submission reminder',
            source: 'Compliance',
            context: 'Standalone',
            assignedBy: 'Ela R.',
            meta: 'Submission package ready for acceptance.',
            requiredAction: 'Paket uygunlugunu kontrol et'
        },
        {
            type: 'Note',
            title: 'Q2 All Hands Summary',
            source: 'Corporate',
            context: 'Standalone',
            assignedBy: 'Ceren K.',
            meta: 'Summary of the all hands meeting.',
            requiredAction: 'Oku'
        }
    ];

    const buildMockInboxItems = (count) => {
        const today = new Date('2026-03-24T09:00:00');
        const priorities = ['Yuksek', 'Orta', 'Dusuk'];
        return Array.from({ length: count }, function (_, index) {
            const template = baseInboxItems[index % baseInboxItems.length];
            const sequence = index + 1;
            const createdDate = new Date(today);
            createdDate.setDate(today.getDate() - (index % 9));
            const dueDate = new Date(createdDate);
            dueDate.setDate(createdDate.getDate() + ((index % 5) + 1));

            return {
                id: `inb-${String(sequence).padStart(3, '0')}`,
                type: template.type,
                status: 'Backlog',
                priority: priorities[index % priorities.length],
                role: index % 3 === 0 ? 'Owner' : (index % 2 === 0 ? 'Reviewer' : 'Informed'),
                title: sequence > baseInboxItems.length ? `${template.title} #${sequence}` : template.title,
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

    const state = {
        activeTab: 'inbox',
        allWorkView: 'data-table',
        inboxItems: [],
        visibleCount: PAGE_SIZE,
        selectedItemId: null,
        selectedItemIds: new Set(),
        filterText: '',
        filterType: '',
        searchSuggestions: [],
        searchSuggestionIndex: -1
    };

    const getInboxItems = () => state.inboxItems.filter((item) => !removedItemIds.has(item.id));

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
            items = items.filter((item) => item.type === state.filterType);
        }
        return items;
    };

    const getVisibleInboxItems = () => getFilteredInboxItems().slice(0, state.visibleCount);

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

        const suggestions = getInboxItems()
            .map((item) => ({
                id: item.id,
                title: item.title || ''
            }))
            .filter((item) => item.title.toLowerCase().includes(q))
            .sort((a, b) => {
                const aStarts = a.title.toLowerCase().startsWith(q) ? 0 : 1;
                const bStarts = b.title.toLowerCase().startsWith(q) ? 0 : 1;
                if (aStarts !== bStarts) {
                    return aStarts - bStarts;
                }
                return a.title.localeCompare(b.title);
            })
            .slice(0, 6);

        return suggestions;
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

        const listHtml = items.map((item, index) => {
            const activeClass = index === state.searchSuggestionIndex ? ' is-active' : '';
            return `<li role="option" aria-selected="${index === state.searchSuggestionIndex}" class="wc-search-suggestion-item${activeClass}">
                        <button type="button" class="wc-search-suggestion-btn" data-suggestion-id="${item.id}">
                            ${item.title}
                        </button>
                    </li>`;
        }).join('');

        elements.inboxSearchSuggestions.innerHTML = listHtml;
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
        renderInbox();
    };

    const notify = (message, type) => {
        if (typeof window.showToast === 'function') {
            window.showToast(message, type || 'info');
            return;
        }
        console.log('[WorkCenter]', type || 'info', message);
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
            elements.inboxLoadMoreBtn.innerHTML = `
                <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                ${l10n.Loading || 'Loading...'}
            `;
            return;
        }

        elements.inboxLoadMoreBtn.textContent = LOAD_MORE_DEFAULT_LABEL;
    };

    const loadInboxItems = async () => {
        // Mock data stays in frontend scope for now; async shape matches future API usage.
        return Promise.resolve(buildMockInboxItems(40));
    };

    const updateBulkActionBar = () => {
        if (!elements.inboxMasterCheckbox || !elements.inboxBulkState || !elements.inboxDefaultState) return;

        const count = state.selectedItemIds.size;
        const visibleItems = getVisibleInboxItems();

        if (count > 0) {
            elements.inboxDefaultState.classList.add('d-none');
            elements.inboxDefaultState.classList.remove('d-flex');
            elements.inboxBulkState.classList.remove('d-none');
            elements.inboxBulkState.classList.add('d-flex');
            elements.inboxSelectionLabel.textContent = `${count} Kayıt Seçildi`;
            elements.inboxMasterCheckbox.checked = true;

            const allSelected = visibleItems.length > 0 && visibleItems.every(i => state.selectedItemIds.has(i.id));
            elements.inboxMasterCheckbox.indeterminate = !allSelected;
        } else {
            elements.inboxBulkState.classList.add('d-none');
            elements.inboxBulkState.classList.remove('d-flex');
            elements.inboxDefaultState.classList.remove('d-none');
            elements.inboxDefaultState.classList.add('d-flex');
            elements.inboxSelectionLabel.textContent = 'Tümünü Seç';
            elements.inboxMasterCheckbox.checked = false;
            elements.inboxMasterCheckbox.indeterminate = false;
        }
    };

    const renderInbox = () => {
        if (typeof inboxList.setSelectedItemIds === 'function') {
            inboxList.setSelectedItemIds(state.selectedItemIds);
        }
        inboxList.setItems(getVisibleInboxItems());
        inboxList.setSelectedItemId(state.selectedItemId);
        updateLoadMoreVisibility();
        updateBulkActionBar();
    };

    const applyViewSwitchRules = () => {
        const isAllWorkActive = state.activeTab === 'all-work';

        elements.allWorkViewSwitchHost?.classList.toggle('d-none', !isAllWorkActive);
    };

    const inboxList = new window.WorkItemList({
        root: elements.inboxRoot,
        rowTemplate: elements.rowTemplate,
        l10n: l10n,
        onSelect: function (itemId) {
            const selected = getInboxItems().find((item) => item.id === itemId);
            if (!selected) {
                return;
            }

            selected.isUnread = false;
            state.selectedItemId = itemId;
            renderInbox();
            notify(l10n.ActionDetailOpened || 'Item detail opened.', 'info');
        },
        onAction: function (itemId, action) {
            const selected = getInboxItems().find((item) => item.id === itemId);
            if (!selected) {
                return;
            }

            if (action === 'accept') {
                removedItemIds.add(itemId);
                if (state.selectedItemId === itemId) {
                    state.selectedItemId = null;
                }

                const remaining = getInboxItems().length;
                if (remaining < state.visibleCount) {
                    state.visibleCount = Math.max(PAGE_SIZE, remaining);
                }

                renderInbox();
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
                return;
            }

            if (action === 'chat') {
                notify('Chat panel opened (mock).', 'info');
                return;
            }

            if (action === 'priority-change') {
                notify('Priority updated (mock).', 'warning');
                return;
            }

            if (action === 'history') {
                notify('History log opened (mock).', 'info');
                return;
            }

            if (action === 'calendar') {
                notify('Calendar opened (mock).', 'info');
                return;
            }

            if (action === 'convert-to-task') {
                notify('Note converted to task (mock).', 'success');
                return;
            }

            if (action === 'archive') {
                notify('Item archived (mock).', 'info');
                return;
            }

            if (action === 'share') {
                notify('Share dialog opened (mock).', 'info');
            }
        }
    });

    const bindEvents = () => {
        elements.inboxTabTrigger?.addEventListener('shown.bs.tab', () => {
            state.activeTab = 'inbox';
            applyViewSwitchRules();
            updateLoadMoreVisibility();
        });

        elements.allWorkTabTrigger?.addEventListener('shown.bs.tab', () => {
            state.activeTab = 'all-work';
            state.allWorkView = 'data-table';
            applyViewSwitchRules();
            updateLoadMoreVisibility();
            window.AllWork?.onTabActivated();
        });

        elements.inboxLoadMoreBtn?.addEventListener('click', async () => {
            setLoadMoreBusy(true);
            try {
                await Promise.resolve();
                state.visibleCount += PAGE_SIZE;
                renderInbox();
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
            renderInbox();
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
            if (!elements.inboxSearchWrap) {
                return;
            }
            if (elements.inboxSearchWrap.contains(event.target)) {
                return;
            }
            closeSearchSuggestions();
        });

        elements.btnInboxFilterApply?.addEventListener('click', () => {
            state.filterType = elements.inboxFilterTypeSelect?.value || '';
            state.visibleCount = PAGE_SIZE;
            renderInbox();
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
            renderInbox();
        });

        // Bulk Selection Events
        elements.inboxMasterCheckbox?.addEventListener('change', (e) => {
            const isChecked = e.target.checked;
            const visibleItems = getVisibleInboxItems();

            if (isChecked) {
                visibleItems.forEach(item => state.selectedItemIds.add(item.id));
            } else {
                state.selectedItemIds.clear();
            }
            renderInbox();
        });

        elements.inboxRoot?.addEventListener('change', (e) => {
            if (e.target.classList.contains('item-checkbox')) {
                const row = e.target.closest('[data-work-item-row]');
                if (!row) return;
                const itemId = row.getAttribute('data-item-id');
                if (e.target.checked) {
                    state.selectedItemIds.add(itemId);
                } else {
                    state.selectedItemIds.delete(itemId);
                }
                updateBulkActionBar();
            }
        });

        elements.btnBulkAccept?.addEventListener('click', () => {
            const count = state.selectedItemIds.size;
            state.selectedItemIds.forEach(id => removedItemIds.add(id));
            state.selectedItemIds.clear();
            notify(`${count} kayıt başarıyla onaylandı.`, 'success');
            renderInbox();
        });

        elements.btnBulkSnooze?.addEventListener('click', () => {
            notify(`${state.selectedItemIds.size} kayıt ertelendi.`, 'warning');
            state.selectedItemIds.clear();
            renderInbox();
        });

        elements.btnBulkReturn?.addEventListener('click', () => {
            notify(`${state.selectedItemIds.size} kayıt iade edildi.`, 'danger');
            state.selectedItemIds.clear();
            renderInbox();
        });
    };

    const init = async () => {
        bindEvents();
        applyViewSwitchRules();

        if (window.bootstrap?.Tooltip) {
            const tooltipButtons = Array.from(document.querySelectorAll('#workCenterPage [data-bs-toggle="tooltip"]'));
            tooltipButtons.forEach((button) => {
                window.bootstrap.Tooltip.getOrCreateInstance(button);
            });
        }

        inboxList.setLoading(true);
        elements.inboxLoadMoreBtn?.classList.add('d-none');
        setLoadMoreBusy(false);

        try {
            state.inboxItems = await loadInboxItems();
        } finally {
            inboxList.setLoading(false);
            renderInbox();
        }
    };

    init();
})();
