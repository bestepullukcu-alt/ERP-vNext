'use strict';

(function (global) {
    function WorkItemList(options) {
        this.root = options.root;
        this.rowTemplate = options.rowTemplate;
        this.l10n = options.l10n || {};
        this.onAction = options.onAction || function () { };
        this.onSelect = options.onSelect || function () { };
        this.state = {
            items: [],
            loading: true,
            selectedItemId: null,
            selectedItemIds: new Set()
        };

        this.elements = {
            loading: this.root.querySelector('[data-work-item-role="loading"]'),
            empty: this.root.querySelector('[data-work-item-role="empty"]'),
            items: this.root.querySelector('[data-work-item-role="items"]')
        };

        this.bindEvents();
        this.render();
    }

    WorkItemList.prototype.bindEvents = function () {
        const self = this;
        this.root.addEventListener('click', function (event) {
            const isDropdownToggle = event.target.closest('[data-bs-toggle="dropdown"]');
            const isDropdownMenu = event.target.closest('.dropdown-menu');
            const actionButton = event.target.closest('[data-action]');
            const row = event.target.closest('[data-work-item-row]');
            if (!row) {
                return;
            }

            const itemId = row.getAttribute('data-item-id');
            if (!itemId) {
                return;
            }

            if (actionButton) {
                self.onAction(itemId, actionButton.getAttribute('data-action'));
                return;
            }

            if (event.target.closest('.item-checkbox') || isDropdownToggle || isDropdownMenu) {
                return;
            }

            self.state.selectedItemId = itemId;
            self.render();
            self.onSelect(itemId);
        });
    };

    WorkItemList.prototype.setItems = function (items) {
        this.state.items = Array.isArray(items) ? items.slice() : [];
        this.render();
    };

    WorkItemList.prototype.setLoading = function (loading) {
        this.state.loading = Boolean(loading);
        this.render();
    };

    WorkItemList.prototype.setSelectedItemId = function (itemId) {
        this.state.selectedItemId = itemId || null;
        this.render();
    };

    WorkItemList.prototype.setSelectedItemIds = function (idsSet) {
        this.state.selectedItemIds = idsSet || new Set();
    };

    WorkItemList.prototype.formatDate = function (value) {
        if (!value) {
            return '-';
        }
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }
        return date.toLocaleDateString();
    };

    WorkItemList.prototype.resolveTypeClass = function (type) {
        const normalized = (type || '').toLowerCase();
        if (normalized === 'task') return 'inbox-row__badge--type-task';
        if (normalized === 'issue') return 'inbox-row__badge--type-issue';
        if (normalized === 'meeting') return 'inbox-row__badge--type-meeting';
        if (normalized === 'note') return 'inbox-row__badge--type-note';
        return 'inbox-row__badge--type-default';
    };

    WorkItemList.prototype.resolveRoleClass = function (role) {
        const normalized = (role || '').toLowerCase();
        if (normalized === 'owner') return 'inbox-row__badge--role-owner';
        if (normalized === 'reviewer') return 'inbox-row__badge--role-reviewer';
        return 'inbox-row__badge--role-default';
    };

    WorkItemList.prototype.resolvePriorityClass = function (priority) {
        const normalized = (priority || '').toLowerCase();
        if (normalized === 'yuksek' || normalized === 'high') return 'bg-label-danger';
        if (normalized === 'orta' || normalized === 'medium') return 'bg-label-warning';
        if (normalized === 'dusuk' || normalized === 'low') return 'bg-label-success';
        return 'bg-label-secondary';
    };

    WorkItemList.prototype.getTypeActionConfig = function (type) {
        const normalized = (type || '').toLowerCase();
        const typeConfig = {
            task: {
                primary: { action: 'accept', label: this.l10n.Accept || 'Accept', icon: 'bx bx-check icon-base' },
                secondary: [
                    { action: 'snooze', label: this.l10n.Snooze || 'Snooze', icon: 'bx bx-time' },
                    { action: 'return', label: this.l10n.ReturnWithReason || 'Return', icon: 'bx bx-undo' },
                    { action: 'reassign', label: this.l10n.Reassign || 'Reassign', icon: 'bx bx-user-pin' },
                    { action: 'chat', label: 'Chat', icon: 'bx bx-message-rounded-dots' }
                ]
            },
            issue: {
                primary: { action: 'accept', label: 'Investigate', icon: 'bx bx-search icon-base' },
                secondary: [
                    { action: 'return', label: 'Wrong Assignee', icon: 'bx bx-undo' },
                    { action: 'reassign', label: 'Route Department', icon: 'bx bx-user-pin' },
                    { action: 'priority-change', label: 'Update Priority', icon: 'bx bx-error' },
                    { action: 'history', label: 'View History', icon: 'bx bx-history' }
                ]
            },
            meeting: {
                primary: { action: 'accept', label: 'Confirm', icon: 'bx bx-check icon-base' },
                secondary: [
                    { action: 'decline', label: this.l10n.Decline || 'Decline', icon: 'bx bx-x-circle' },
                    { action: 'snooze', label: this.l10n.Snooze || 'Snooze', icon: 'bx bx-time' },
                    { action: 'propose-time', label: this.l10n.ProposeNewTime || 'Propose New Time', icon: 'bx bx-calendar-edit' },
                    { action: 'chat', label: 'Meeting Chat', icon: 'bx bx-message-rounded-dots' },
                    { action: 'calendar', label: 'Open Calendar', icon: 'bx bx-calendar-plus' }
                ]
            },
            note: {
                primary: { action: 'accept', label: 'Read', icon: 'bx bx-check icon-base' },
                secondary: [
                    { action: 'convert-to-task', label: 'Convert to Task', icon: 'bx bx-list-plus' },
                    { action: 'archive', label: 'Archive', icon: 'bx bx-archive' },
                    { action: 'share', label: 'Share', icon: 'bx bx-share-alt' },
                    { action: 'chat', label: 'Discuss Note', icon: 'bx bx-message-rounded-dots' }
                ]
            }
        };

        return typeConfig[normalized] || typeConfig.task;
    };

    WorkItemList.prototype.renderRow = function (item) {
        const fragment = this.rowTemplate.content.cloneNode(true);
        const row = fragment.querySelector('[data-work-item-row]');
        if (!row) {
            return document.createDocumentFragment();
        }

        row.setAttribute('data-item-id', item.id);
        row.classList.toggle('inbox-row--selected', item.id === this.state.selectedItemId);

        const checkbox = row.querySelector('.item-checkbox');
        if (checkbox) {
            checkbox.checked = this.state.selectedItemIds && this.state.selectedItemIds.has(item.id);
        }

        const type = (item.type || '').toLowerCase();
        const config = this.getTypeActionConfig(type);

        const setField = function (name, value) {
            const field = row.querySelector(`[data-field="${name}"]`);
            if (field) {
                field.textContent = value || '-';
            }
        };

        const typeBadge = row.querySelector('[data-field="type"]');
        if (typeBadge) {
            typeBadge.textContent = item.type || '-';
            typeBadge.className = `badge inbox-row__type inbox-row__badge-outline flex-shrink-0 ${this.resolveTypeClass(item.type)}`;
        }

        const priorityBadge = row.querySelector('[data-field="priority"]');
        if (priorityBadge) {
            priorityBadge.textContent = item.priority || '-';
            priorityBadge.className = `badge inbox-row__priority ${this.resolvePriorityClass(item.priority)}`;
        }

        const roleBadge = row.querySelector('[data-field="role"]');
        if (roleBadge) {
            if (item.role && item.role !== 'Informed') {
                roleBadge.textContent = item.role;
                roleBadge.className = `badge inbox-row__badge-outline flex-shrink-0 ${this.resolveRoleClass(item.role)}`;
            } else {
                roleBadge.className = 'badge inbox-row__badge-outline inbox-row__badge--role-default flex-shrink-0 d-none';
            }
        }

        const acceptBtn = row.querySelector('[data-action="accept"]');
        if (acceptBtn) {
            const spanEl = acceptBtn.querySelector('span');
            const iconEl = acceptBtn.querySelector('i');
            const labelText = config.primary.label;
            const iconCls = config.primary.icon;

            if (spanEl) spanEl.textContent = labelText;
            if (iconEl) iconEl.className = `${iconCls} me-1`;

            acceptBtn.setAttribute('title', labelText);
        }

        const secondaryButtons = row.querySelectorAll('.inbox-row__action-secondary[data-action]');
        secondaryButtons.forEach((button) => {
            const action = button.getAttribute('data-action');
            const actionConfig = config.secondary.find((itemCfg) => itemCfg.action === action);
            const iconEl = button.querySelector('i');

            if (!actionConfig) {
                button.classList.add('d-none');
                button.setAttribute('title', '');
                if (iconEl) {
                    iconEl.className = 'bx bx-dots-horizontal-rounded';
                }
                return;
            }

            button.classList.remove('d-none');
            button.setAttribute('title', actionConfig.label);
            if (iconEl) {
                iconEl.className = actionConfig.icon;
            }
        });

        const dropdownItems = row.querySelectorAll('[data-action-item]');
        dropdownItems.forEach((el) => {
            const action = el.getAttribute('data-action-item');
            const actionConfig = action === config.primary.action
                ? config.primary
                : config.secondary.find((itemCfg) => itemCfg.action === action);
            const itemBtn = el.querySelector('[data-action]');

            if (!actionConfig || !itemBtn) {
                el.classList.add('d-none');
                return;
            }

            el.classList.remove('d-none');
            itemBtn.textContent = actionConfig.label;

            if (action === config.primary.action) {
                itemBtn.classList.add('fw-semibold');
            } else {
                itemBtn.classList.remove('fw-semibold');
            }
        });

        if (acceptBtn) {
            const mobileAcceptItem = row.querySelector('[data-action-item="accept"] .dropdown-item');
            if (mobileAcceptItem) {
                mobileAcceptItem.textContent = config.primary.label;
            }
        }

        setField('title', item.title);
        setField('context', item.context);
        setField('assignedBy', item.assignedBy);
        setField('dueDate', this.formatDate(item.dueDate || item.createdDate));
        setField('requiredAction', item.requiredAction || (this.l10n.NoAction || '-'));

        return fragment;
    };

    WorkItemList.prototype.render = function () {
        const isLoading = this.state.loading;
        const hasItems = this.state.items.length > 0;

        this.elements.loading.classList.toggle('d-none', !isLoading);
        this.elements.empty.classList.toggle('d-none', isLoading || hasItems);
        this.elements.items.classList.toggle('d-none', isLoading || !hasItems);

        if (isLoading || !hasItems) {
            this.elements.items.innerHTML = '';
            return;
        }

        this.elements.items.innerHTML = '';
        const fragment = document.createDocumentFragment();
        for (const item of this.state.items) {
            fragment.appendChild(this.renderRow(item));
        }
        this.elements.items.appendChild(fragment);

        if (window.bootstrap?.Tooltip) {
            const tooltips = this.elements.items.querySelectorAll('[data-bs-toggle="tooltip"]');
            for (let i = 0; i < tooltips.length; i++) {
                window.bootstrap.Tooltip.getOrCreateInstance(tooltips[i]);
            }
        }
    };

    global.WorkItemList = WorkItemList;
})(window);
