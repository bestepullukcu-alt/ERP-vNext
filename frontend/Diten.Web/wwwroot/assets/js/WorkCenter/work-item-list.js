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
            selectedItemId: null
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

            if (isDropdownToggle || isDropdownMenu) {
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
        if (normalized === 'task') return 'bg-label-primary';
        if (normalized === 'issue') return 'bg-label-danger';
        if (normalized === 'meeting') return 'bg-label-warning';
        return 'bg-label-dark';
    };

    WorkItemList.prototype.renderRow = function (item) {
        const fragment = this.rowTemplate.content.cloneNode(true);
        const row = fragment.querySelector('[data-work-item-row]');
        if (!row) {
            return document.createDocumentFragment();
        }

        row.setAttribute('data-item-id', item.id);
        row.classList.toggle('inbox-row--selected', item.id === this.state.selectedItemId);

        const type = (item.type || '').toLowerCase();
        const actions = {
            return: type === 'task' || type === 'issue',
            reassign: type === 'task' || type === 'issue',
            decline: type === 'meeting',
            'propose-time': type === 'meeting',
            snooze: true // snooze is available for all
        };

        const dropdownItems = row.querySelectorAll('[data-action-item]');
        dropdownItems.forEach((el) => {
            const actionTarget = el.getAttribute('data-action-item');
            if (actions[actionTarget] === false) {
                el.classList.add('d-none');
            } else {
                el.classList.remove('d-none');
            }
        });

        const setField = function (name, value) {
            const field = row.querySelector(`[data-field="${name}"]`);
            if (field) {
                field.textContent = value || '-';
            }
        };

        const typeBadge = row.querySelector('[data-field="type"]');
        if (typeBadge) {
            typeBadge.textContent = item.type || '-';
            typeBadge.className = `badge inbox-row__type ${this.resolveTypeClass(item.type)}`;
        }

        setField('title', item.title);
        setField('context', item.context);
        setField('assignedBy', item.assignedBy);
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
    };

    global.WorkItemList = WorkItemList;
})(window);
