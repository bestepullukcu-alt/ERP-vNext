'use strict';

(function (global) {
    function WorkItemList(options) {
        this.root = options.root;
        this.rowTemplate = options.rowTemplate;
        this.l10n = options.l10n || {};
        this.mockData = global.WorkCenterMockData || null;
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

            if (actionButton && !actionButton.disabled) {
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

    WorkItemList.prototype.humanize = function (value) {
        if (!value) {
            return '-';
        }

        const text = String(value);
        return text.charAt(0).toUpperCase() + text.slice(1);
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
        if (normalized === 'approver') return 'inbox-row__badge--role-approver';
        return 'inbox-row__badge--role-default';
    };

    WorkItemList.prototype.resolvePriorityClass = function (priority) {
        const normalized = (priority || '').toLowerCase();
        if (normalized === 'yuksek' || normalized === 'high') return 'bg-label-danger';
        if (normalized === 'orta' || normalized === 'medium') return 'bg-label-warning';
        if (normalized === 'dusuk' || normalized === 'low') return 'bg-label-success';
        return 'bg-label-secondary';
    };

    WorkItemList.prototype.resolveStatusClass = function (status) {
        const map = {
            'Pending Approval': 'bg-label-primary',
            'Pending Acceptance': 'bg-label-info',
            'Open': 'bg-label-secondary',
            'Planned': 'bg-label-warning',
            'In Progress': 'bg-label-primary',
            'Waiting for Information': 'bg-label-warning',
            'Pending Review': 'bg-label-info',
            'Closed': 'bg-label-success',
            'Cancelled': 'bg-label-dark'
        };

        return map[status] || 'bg-label-secondary';
    };

    WorkItemList.prototype.resolveDueStateClass = function (kind) {
        if (kind === 'overdue') return 'bg-label-danger';
        if (kind === 'due_soon') return 'bg-label-warning';
        if (kind === 'on_track') return 'bg-label-success';
        return 'bg-label-secondary';
    };

    WorkItemList.prototype.resolveFlagClass = function (kind) {
        const normalized = (kind || '').toLowerCase();
        if (normalized === 'danger') return 'bg-label-danger';
        if (normalized === 'warning') return 'bg-label-warning';
        if (normalized === 'success') return 'bg-label-success';
        if (normalized === 'primary') return 'bg-label-primary';
        if (normalized === 'info') return 'bg-label-info';
        return 'bg-label-secondary';
    };

    WorkItemList.prototype.getItemActionConfig = function (item) {
        if (this.mockData && typeof this.mockData.getListActionConfig === 'function') {
            return this.mockData.getListActionConfig(item);
        }

        return {
            primary: {
                action: 'accept',
                label: this.l10n.Accept || 'Accept',
                icon: 'bx bx-check icon-base',
                variant: 'btn-label-success'
            },
            secondary: [],
            bulkSelectable: false
        };
    };

    WorkItemList.prototype.applyActionButton = function (button, config, isPrimary) {
        if (!button) {
            return;
        }

        const iconEl = button.querySelector('i');
        const labelEl = button.querySelector('span');
        const variants = [
            'btn-label-success',
            'btn-label-secondary',
            'btn-label-warning',
            'btn-label-danger',
            'btn-label-primary',
            'btn-outline-secondary',
            'btn-outline-danger'
        ];

        if (!config) {
            button.classList.add('d-none');
            button.setAttribute('data-action', '');
            button.setAttribute('title', '');
            button.disabled = true;
            variants.forEach((className) => button.classList.remove(className));
            if (iconEl) {
                iconEl.className = 'bx bx-dots-horizontal-rounded';
            }
            return;
        }

        button.classList.remove('d-none');
        button.setAttribute('data-action', config.action || '');
        button.setAttribute('title', config.label || '');
        button.disabled = Boolean(config.disabled);
        variants.forEach((className) => button.classList.remove(className));
        button.classList.add(config.variant || (isPrimary ? 'btn-label-success' : 'btn-label-secondary'));

        if (iconEl) {
            iconEl.className = config.icon || 'bx bx-check';
            if (isPrimary && !iconEl.className.includes('me-1')) {
                iconEl.className += ' me-1';
            }
        }

        if (labelEl) {
            labelEl.textContent = config.label || '-';
        }
    };

    WorkItemList.prototype.applyDropdownAction = function (element, config, isPrimary) {
        if (!element) {
            return;
        }

        const button = element.querySelector('[data-action]');
        if (!config || !button) {
            element.classList.add('d-none');
            if (button) {
                button.setAttribute('data-action', '');
                button.disabled = true;
                button.classList.remove('fw-semibold');
                button.classList.add('disabled');
            }
            return;
        }

        element.classList.remove('d-none');
        element.setAttribute('data-action-item', config.action || '');
        button.setAttribute('data-action', config.action || '');
        button.textContent = config.label || '-';
        button.disabled = Boolean(config.disabled);
        button.classList.toggle('disabled', Boolean(config.disabled));
        button.classList.toggle('fw-semibold', Boolean(isPrimary && !config.disabled));
    };

    WorkItemList.prototype.renderSignals = function (row, item) {
        const signalsHost = row.querySelector('[data-field-container="signals"]');
        if (!signalsHost) {
            return;
        }

        if (!Array.isArray(item.flags) || item.flags.length === 0) {
            signalsHost.classList.add('d-none');
            signalsHost.innerHTML = '';
            return;
        }

        signalsHost.classList.remove('d-none');
        signalsHost.innerHTML = item.flags.map((flag) => {
            const title = String(flag.title || '').replace(/"/g, '&quot;');
            return `<span class="badge inbox-row__signal ${this.resolveFlagClass(flag.kind)}" title="${title}">${flag.label}</span>`;
        }).join('');
    };

    WorkItemList.prototype.renderRow = function (item) {
        const fragment = this.rowTemplate.content.cloneNode(true);
        const row = fragment.querySelector('[data-work-item-row]');
        if (!row) {
            return document.createDocumentFragment();
        }

        row.setAttribute('data-item-id', item.id);
        row.classList.toggle('inbox-row--selected', item.id === this.state.selectedItemId);

        const actionConfig = this.getItemActionConfig(item);

        const checkbox = row.querySelector('.item-checkbox');
        if (checkbox) {
            checkbox.checked = this.state.selectedItemIds && this.state.selectedItemIds.has(item.id);
            checkbox.disabled = !actionConfig.bulkSelectable;
        }

        const setField = function (name, value) {
            const field = row.querySelector(`[data-field="${name}"]`);
            if (field) {
                field.textContent = value || '-';
            }
        };

        const typeBadge = row.querySelector('[data-field="type"]');
        if (typeBadge) {
            typeBadge.textContent = item.displayType || this.humanize(item.type);
            typeBadge.className = `badge inbox-row__type inbox-row__badge-outline flex-shrink-0 ${this.resolveTypeClass(item.type)}`;
        }

        const statusBadge = row.querySelector('[data-field="status"]');
        if (statusBadge) {
            statusBadge.textContent = item.status || '-';
            statusBadge.className = `badge inbox-row__status flex-shrink-0 ${this.resolveStatusClass(item.status)}`;
        }

        const priorityBadge = row.querySelector('[data-field="priority"]');
        if (priorityBadge) {
            priorityBadge.textContent = item.displayPriority || this.humanize(item.priority);
            priorityBadge.className = `badge inbox-row__priority ${this.resolvePriorityClass(item.priority)}`;
        }

        const dueStateBadge = row.querySelector('[data-field="dueState"]');
        if (dueStateBadge) {
            dueStateBadge.textContent = item.dueStateLabel || '-';
            dueStateBadge.className = `badge inbox-row__due-state ${this.resolveDueStateClass(item.dueStateKind)}`;
        }

        const roleBadge = row.querySelector('[data-field="role"]');
        if (roleBadge) {
            const roleValue = item.viewerRole || item.role;
            if (roleValue && roleValue !== 'Informed') {
                roleBadge.textContent = roleValue;
                roleBadge.className = `badge inbox-row__badge-outline flex-shrink-0 ${this.resolveRoleClass(roleValue)}`;
            } else {
                roleBadge.className = 'badge inbox-row__badge-outline inbox-row__badge--role-default flex-shrink-0 d-none';
            }
        }

        this.applyActionButton(row.querySelector('.inbox-row__action-primary'), actionConfig.primary, true);

        const secondaryButtons = Array.from(row.querySelectorAll('.inbox-row__action-secondary[data-action]'));
        secondaryButtons.forEach((button, index) => {
            this.applyActionButton(button, actionConfig.secondary[index], false);
        });

        const dropdownItems = Array.from(row.querySelectorAll('[data-action-item]'));
        const dropdownConfigs = [actionConfig.primary].concat(actionConfig.secondary);
        dropdownItems.forEach((element, index) => {
            this.applyDropdownAction(element, dropdownConfigs[index], index === 0);
        });

        this.renderSignals(row, item);

        setField('title', item.title);
        setField('context', item.context);
        setField('assignedBy', item.assignedBy || item.creator);
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
