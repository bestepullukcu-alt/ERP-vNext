'use strict';

(function () {
    const l10n = window.L10n || {};
    const removedItemIds = new Set();
    const PAGE_SIZE = 20;

    const elements = {
        inboxTabTrigger: document.getElementById('workcenter-inbox-tab'),
        allWorkTabTrigger: document.getElementById('workcenter-allwork-tab'),
        viewSwitchButtons: Array.from(document.querySelectorAll('[data-view-switch]')),
        inboxLoadMoreBtn: document.getElementById('inboxLoadMoreBtn'),
        rowTemplate: document.getElementById('inboxRowTemplate'),
        inboxRoot: document.querySelector('[data-work-item-list="inbox"]'),
        allWorkRoot: document.querySelector('[data-work-item-list="allwork"]'),
        inboxSearchInput: document.getElementById('inboxSearchInput'),
        inboxFilterTypeSelect: document.getElementById('inboxFilterType'),
        btnInboxFilterApply: document.getElementById('btnInboxFilterApply'),
        btnInboxFilterReset: document.getElementById('btnInboxFilterReset')
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
        }
    ];

    const buildMockInboxItems = (count) => {
        const today = new Date('2026-03-24T09:00:00');
        return Array.from({ length: count }, function (_, index) {
            const template = baseInboxItems[index % baseInboxItems.length];
            const sequence = index + 1;
            const createdDate = new Date(today);
            createdDate.setDate(today.getDate() - (index % 9));

            return {
                id: `inb-${String(sequence).padStart(3, '0')}`,
                type: template.type,
                status: 'Backlog',
                priority: 'Orta',
                title: sequence > baseInboxItems.length ? `${template.title} #${sequence}` : template.title,
                source: template.source,
                context: template.context,
                assignedBy: template.assignedBy,
                createdDate: createdDate.toISOString().slice(0, 10),
                meta: template.meta,
                requiredAction: template.requiredAction,
                isUnread: index % 3 !== 0
            };
        });
    };

    const state = {
        activeTab: 'inbox',
        inboxItems: buildMockInboxItems(40),
        visibleCount: PAGE_SIZE,
        selectedItemId: null,
        filterText: '',
        filterType: ''
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

    const renderInbox = () => {
        inboxList.setItems(getVisibleInboxItems());
        inboxList.setSelectedItemId(state.selectedItemId);
        updateLoadMoreVisibility();
    };

    const applyViewSwitchRules = () => {
        const isInboxActive = state.activeTab === 'inbox';
        elements.viewSwitchButtons.forEach((button) => {
            const view = button.getAttribute('data-view-switch');
            const shouldEnable = isInboxActive && view === 'list';
            button.disabled = !shouldEnable;
            button.classList.toggle('active', shouldEnable);
            button.classList.toggle('btn-outline-primary', shouldEnable);
            button.classList.toggle('btn-outline-secondary', !shouldEnable);
        });
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
            }
        }
    });

    const allWorkList = elements.allWorkRoot
        ? new window.WorkItemList({
            root: elements.allWorkRoot,
            rowTemplate: elements.rowTemplate,
            l10n: l10n,
            onSelect: function () { },
            onAction: function () { }
        })
        : null;

    const bindEvents = () => {
        elements.inboxTabTrigger?.addEventListener('shown.bs.tab', () => {
            state.activeTab = 'inbox';
            applyViewSwitchRules();
            updateLoadMoreVisibility();
        });

        elements.allWorkTabTrigger?.addEventListener('shown.bs.tab', () => {
            state.activeTab = 'all-work';
            applyViewSwitchRules();
            updateLoadMoreVisibility();
        });

        elements.inboxLoadMoreBtn?.addEventListener('click', () => {
            state.visibleCount += PAGE_SIZE;
            renderInbox();
        });

        elements.inboxSearchInput?.addEventListener('input', () => {
            state.filterText = elements.inboxSearchInput.value.trim();
            state.visibleCount = PAGE_SIZE;
            elements.inboxSearchInput.classList.toggle('border-primary', !!state.filterText);
            elements.inboxSearchInput.classList.toggle('bg-label-primary', !!state.filterText);
            renderInbox();
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
            if (elements.inboxFilterTypeSelect) {
                elements.inboxFilterTypeSelect.value = '';
            }
            if (elements.inboxSearchInput) {
                elements.inboxSearchInput.value = '';
                elements.inboxSearchInput.classList.remove('border-primary', 'bg-label-primary');
            }
            renderInbox();
        });
    };

    const init = () => {
        bindEvents();
        applyViewSwitchRules();

        inboxList.setLoading(true);
        elements.inboxLoadMoreBtn?.classList.add('d-none');

        if (allWorkList) {
            allWorkList.setLoading(false);
            allWorkList.setItems([]);
        }

        setTimeout(() => {
            inboxList.setLoading(false);
            renderInbox();
        }, 500);
    };

    init();
})();
