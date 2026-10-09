/* MOD-0290-FU03 Product Legal Entity Scope Assignment — same-origin only.
 *
 * WP-MDM-SCOPE-ACCEPT-01 FIX1 (BL-440 golden reference): the page stands on the list component (DitenDataTable.createList,
 * SERVER mode). The factory owns the table, the inline filter, Save View (same moduleKey/pageKey and the same five
 * columns as before, so saved views still load), the toolbar and the server wire (start/length/search/orderBy/orderDir/
 * lifecycleStatus → the MVC proxy → MDM). This file keeps only the page's own business: the quick view with the product's
 * policy, configure / replace / end with their attempt token and anti-forgery, the legal-entity selector.
 * No selection column (HasSelection = false): a product's scope is only ever changed one product at a time (GxP).
 */
const ProductLegalEntityScopesList = (() => {
    'use strict';

    const endpoint = '/MasterDataManagement/ProductLegalEntityScopes/api';
    const tableEl = document.getElementById('dt-product-legal-entity-scopes');
    const host = document.getElementById('product-legal-entity-scopes');
    const canConfigure = host?.dataset.canConfigure === 'true';
    const canReplace = host?.dataset.canReplace === 'true';
    const canEnd = host?.dataset.canEnd === 'true';
    const L = window.L10n || {};
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'ProductLegalEntityScopes' };
    const saveViewColumnIndexes = [1, 2, 3];
    const baseOrder = [[1, 'asc']];
    let list = null;
    let writeMode = 'create';

    const value = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
    const unwrap = payload => payload?.data ?? payload?.Data ?? payload;
    const escapeHtml = input => String(input ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));
    // The lifecycle in the reader's words (seven languages) — never the stored code.
    const LIFECYCLE_KEYS = Object.freeze({
        Draft: 'LifecycleDraft', 1: 'LifecycleDraft',
        PendingIdentityApproval: 'LifecyclePendingIdentityApproval', 2: 'LifecyclePendingIdentityApproval',
        IdentityApproved: 'LifecycleIdentityApproved', 3: 'LifecycleIdentityApproved',
        Retired: 'LifecycleRetired', 4: 'LifecycleRetired'
    });
    const lifecycleText = status => L[LIFECYCLE_KEYS[status]] || L.Unknown || '-';
    // A failed LIST read speaks the page's own sentences (seven languages), never DtDefaults' English.
    const onListError = function (xhr) {
        const status = Number(xhr?.status) || 0;
        if (status === 401) {
            if (!this?._retried) window.DtDefaults?.handleUnauthorized?.(this);
            return;
        }
        if (xhr?.statusText === 'abort') return;
        window.showToast?.(({ 400: L.ErrorValidation, 403: L.ErrorForbidden, 404: L.ErrorNotFound, 409: L.ErrorConflict,
            503: L.ErrorProviderUnavailable, 504: L.ErrorProviderTimeout })[status] || L.ErrorGateway, 'error');
    };

    const statusText = status => ({ 400: L.ErrorValidation, 403: L.ErrorForbidden, 404: L.ErrorNotFound, 409: L.ErrorConflict,
        503: L.ErrorProviderUnavailable, 504: L.ErrorProviderTimeout })[status] || L.ErrorGateway;
    // WP-MDM-SCOPE-ACCEPT-01 FIX2 (SC4): the scope rollout's refusals in the reader's words (seven languages) — never the
    // stored code. An unknown code falls back to the sentence of its HTTP status.
    const CODE_KEYS = Object.freeze({
        PRODUCT_SCOPE_WRITE_FENCE_ACTIVE: 'ErrorFenceActive',
        PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE: 'ErrorWriterBusy',
        PRODUCT_SCOPE_WRITER_LEASE_CONTENDED: 'ErrorWriterBusy',
        PRODUCT_SCOPE_FAIL_CLOSED_SUSPENDED: 'ErrorSuspended',
        PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID: 'ErrorRolloutUnavailable',
        // FIX4 K1 / (2, 4, 6): the codes this round added.
        PRODUCT_SCOPE_WRITER_LEASE_CAPACITY_EXCEEDED: 'ErrorWriterCapacity',
        PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE: 'ErrorStoreUnavailable',
        PRODUCT_SCOPE_WRITER_LEASE_AMBIGUOUS: 'ErrorWriterBusy',
        PRODUCT_SCOPE_REPLACE_CONTENDED: 'ErrorWriterBusy',
        PRODUCT_SCOPE_RECONCILIATION_REQUIRED: 'ReconciliationPending'
    });
    const failureText = (payload, status) => {
        const code = payload?.errors?.[0] ?? payload?.Errors?.[0];
        return L[CODE_KEYS[code]] || statusText(status);
    };
    const errorMessage = async response => {
        await response.text().catch(() => '');
        return statusText(response.status);
    };
    const formatDate = input => input ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(input)) : (L.Unknown || '-');
    const productLabel = row => `${value(row, 'canonicalCode', 'CanonicalCode')} — ${value(row, 'globalProductName', 'GlobalProductName')}`;
    const productId = row => value(row, 'id', 'Id');
    const policyPeriod = policy => {
        const periods = value(policy, 'periods', 'Periods') || [];
        return periods.find(period => !value(period, 'effectiveToUtc', 'EffectiveToUtc')) || null;
    };
    const resolveManageMode = policy => {
        if (!policy) return canConfigure ? 'create' : null;
        return policyPeriod(policy) && canReplace ? 'replace' : null;
    };
    const canEndPolicy = policy => Boolean(policyPeriod(policy));
    const setText = (id, content) => {
        const element = document.getElementById(id);
        if (element) element.textContent = content === null || content === undefined || content === '' ? (L.Unknown || '-') : String(content);
    };

    const fetchPolicy = async id => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (response.status === 404) return null;
        if (!response.ok) throw new Error(await errorMessage(response));
        return unwrap(await response.json());
    };
    // FIX2 (10): companies the reader may not see arrive as a count, never as ids — "other companies: N".
    const legalEntitiesText = period => {
        const hidden = Number(value(period, 'otherLegalEntityCount', 'OtherLegalEntityCount')) || 0;
        return [
            (value(period, 'legalEntityIds', 'LegalEntityIds') || []).join(', '),
            hidden > 0 ? String(L.OtherLegalEntities || '{0}').replace('{0}', String(hidden)) : ''
        ].filter(Boolean).join(' · ') || '-';
    };
    const showDetail = async row => {
        try {
            const policy = await fetchPolicy(productId(row));
            const period = policyPeriod(policy);
            setText('oc-title', productLabel(row));
            setText('oc-policy-status', policy ? L.Configured : L.LegacyUnclassified);
            setText('oc-mode', period
                ? (Number(value(period, 'mode', 'Mode')) === 1 ? L.GroupWide : L.Scoped)
                : (policy ? L.Unknown : L.LegacyUnclassified));
            setText('oc-legal-entities', legalEntitiesText(period));
            setText('oc-effective-from', formatDate(value(period, 'effectiveFromUtc', 'EffectiveFromUtc')));
            setText('oc-effective-to', formatDate(value(period, 'effectiveToUtc', 'EffectiveToUtc')));
            setText('oc-version', value(policy, 'version', 'Version'));
        } catch (error) { window.showToast?.(error.message || L.ErrorGateway, 'error'); }
    };

    const fillProduct = row => {
        const select = document.getElementById('globalProductId');
        select.innerHTML = '';
        select.add(new Option(productLabel(row), productId(row), true, true));
    };
    const loadOptions = async id => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/create-options`, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (!response.ok) throw new Error(await errorMessage(response));
        const options = unwrap(await response.json());
        const select = document.getElementById('legalEntityIds');
        select.innerHTML = '';
        const legalEntities = value(options, 'legalEntities', 'LegalEntities') || [];
        if (legalEntities.length > 200) throw new Error(L.ErrorGateway);
        legalEntities.forEach(item => select.add(new Option(`${value(item, 'code', 'Code')} — ${value(item, 'name', 'Name')}`, value(item, 'id', 'Id'))));
    };
    const applyLegalEntitySelection = selectedIds => {
        const selected = new Set((selectedIds || []).map(String));
        Array.from(document.getElementById('legalEntityIds')?.options || [])
            .forEach(option => { option.selected = selected.has(option.value); });
        $('#legalEntityIds').trigger('change');
    };
    const syncScopeMode = () => {
        const scoped = document.getElementById('scopeMode')?.value === '2';
        document.getElementById('legalEntityField')?.classList.toggle('d-none', !scoped);
        document.getElementById('legalEntityIds')?.toggleAttribute('required', scoped);
        if (!scoped) $('#legalEntityIds').val([]).trigger('change');
        updateRequiredProgress();
    };
    const updateRequiredProgress = () => {
        const scoped = document.getElementById('scopeMode')?.value === '2';
        const total = scoped ? 3 : 2;
        const completed = Number(Boolean(document.getElementById('globalProductId')?.value))
            + Number(Boolean(document.getElementById('scopeMode')?.value))
            + (scoped ? Number((document.getElementById('legalEntityIds')?.selectedOptions?.length || 0) > 0) : 0);
        setText('requiredProgress', `${completed}/${total}`);
    };
    const openForm = async (row, mode) => {
        writeMode = mode;
        const form = document.getElementById('formProductScope');
        form?.reset();
        fillProduct(row);
        document.getElementById('productScopeFormTitle').textContent = mode === 'replace' ? L.ReplaceTitle : L.ConfigureTitle;
        const policy = mode === 'replace' ? await fetchPolicy(productId(row)) : null;
        document.getElementById('expectedVersion').value = policy ? String(value(policy, 'version', 'Version')) : '';
        const period = policyPeriod(policy);
        document.getElementById('scopeMode').value = period ? String(value(period, 'mode', 'Mode')) : '';
        await loadOptions(productId(row));
        applyLegalEntitySelection(value(period, 'legalEntityIds', 'LegalEntityIds') || []);
        syncScopeMode();
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
    };
    const managePolicy = async row => {
        const policy = await fetchPolicy(productId(row));
        const mode = resolveManageMode(policy);
        if (mode) return openForm(row, mode);
        window.showToast?.(policy ? L.ErrorConflict : L.ErrorForbidden, 'error');
    };
    const submitForm = async () => {
        const form = document.getElementById('formProductScope');
        const product = document.getElementById('globalProductId')?.value;
        const mode = document.getElementById('scopeMode')?.value;
        const ids = Array.from(document.getElementById('legalEntityIds')?.selectedOptions || []).map(option => option.value);
        if (!form || !product || !mode || mode === '2' && ids.length === 0 || ids.length > 200) {
            form?.classList.add('was-validated');
            window.showToast?.(L.ErrorValidation, 'error');
            return;
        }
        const body = new FormData();
        body.set('GlobalProductId', product);
        body.set('Mode', mode);
        if (writeMode === 'replace') body.set('ExpectedVersion', document.getElementById('expectedVersion').value);
        ids.forEach(id => body.append('LegalEntityIds', id));
        body.set('FormAttemptToken', document.getElementById('formAttemptToken').value);
        const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        body.set('__RequestVerificationToken', antiForgeryToken);
        const response = await fetch(writeMode === 'replace' ? `${endpoint}/replace` : endpoint, {
            method: 'POST', credentials: 'same-origin', headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken }, body
        });
        const payload = await response.json().catch(() => null);
        if (response.status === 202) { window.showToast?.(L.ReconciliationPending, 'warning'); return; }
        if (!response.ok) { window.showToast?.(failureText(payload, response.status), 'error'); return; }
        const nextToken = payload?.formAttemptToken;
        if (nextToken) document.getElementById('formAttemptToken').value = nextToken;
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
        list?.reload('RecordSaved');
    };
    const endPolicy = async row => {
        const policy = await fetchPolicy(productId(row));
        if (!policy) return window.showToast?.(L.ErrorNotFound, 'error');
        if (!canEndPolicy(policy)) return window.showToast?.(L.ErrorConflict, 'error');
        window.showConfirm?.(L.EndConfirmation, async () => {
            const form = document.getElementById('formProductScope');
            const body = new FormData();
            body.set('GlobalProductId', productId(row));
            body.set('ExpectedVersion', value(policy, 'version', 'Version'));
            body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
            const antiForgeryToken = form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            body.set('__RequestVerificationToken', antiForgeryToken);
            const response = await fetch(`${endpoint}/end`, { method: 'POST', credentials: 'same-origin', headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken }, body });
            const payload = await response.json().catch(() => null);
            if (response.status === 202) {
                window.showToast?.(L.ReconciliationPending, 'warning');
                return;
            }
            if (!response.ok) return window.showToast?.(failureText(payload, response.status), 'error');
            if (payload?.formAttemptToken) document.getElementById('formAttemptToken').value = payload.formAttemptToken;
            list?.reload('RecordSaved');
        }, { type: 'warning', confirmButtonText: L.End });
    };

    const renderActions = row => window.DitenDataTable.renderActions([
        { key: 'quickView', className: 'js-quick-view', text: L.ViewDetails, icon: 'bx bx-show', attrs: { 'data-id': productId(row) } },
        ...(canConfigure || canReplace ? [{ key: 'manage', className: 'js-scope-manage', text: L.Configure, icon: 'bx bx-link', attrs: { 'data-id': productId(row) } }] : []),
        ...(canEnd ? [{ key: 'end', className: 'js-scope-end', text: L.End, icon: 'bx bx-stop-circle', attrs: { 'data-id': productId(row) } }] : [])
    ]);
    const reportFailure = error => window.showToast?.(error?.message || L.ErrorGateway, 'error');

    // The createList options of the page — one place, so the wire and the saved-view keys are measurable.
    const buildListOptions = () => ({
        tableEl,
        dataMode: 'server',
        ajax: { url: endpoint, type: 'GET', headers: getAuthHeaders(), error: onListError },
        quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: row => { void showDetail(row); }, actionKey: 'quickView' },
        actions: {
            onRowAction: {
                manage: ({ row }) => { void managePolicy(row).catch(reportFailure); },
                end: ({ row }) => { void endPolicy(row).catch(reportFailure); }
            }
        },
        toolbar: {
            addNewText: '',
            exportColumns: saveViewColumnIndexes,
            colvisColumns: saveViewColumnIndexes
        },
        filters: {
            hostId: 'inlineFilterHost',
            collapseId: 'inlineFilterCollapse',
            applyBtn: 'btnFilterApply',
            resetBtn: 'btnFilterReset',
            fields: [{ id: 'filterLifecycleStatus', key: 'lifecycleStatus', kind: 'single' }]
        },
        // Same keys as before the migration: every saved view of this page still loads.
        savedView: {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            saveViewColumnIndexes,
            defaultVisibleColumnIndexes: saveViewColumnIndexes,
            baseOrder
        },
        config: {
            pageLength: 20,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            // `data` names are MDM's orderBy whitelist (canonicalCode, globalProductName, lifecycleStatus).
            columns: [
                { data: 'id', name: 'control' },
                { data: 'canonicalCode', name: 'canonicalCode' },
                { data: 'globalProductName', name: 'globalProductName' },
                { data: 'lifecycleStatus', name: 'lifecycleStatus' },
                { data: null, name: 'action' }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, render: data => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 2, render: escapeHtml },
                { targets: 3, render: data => escapeHtml(lifecycleText(data)) },
                { targets: -1, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3', render: (data, type, row) => renderActions(row) }
            ]
        }
    });

    const initDataTable = async () => {
        if (!tableEl || !window.DitenDataTable?.createList) return;
        list = await window.DitenDataTable.createList(buildListOptions());
    };
    const bind = () => {
        document.getElementById('scopeMode')?.addEventListener('change', syncScopeMode);
        document.getElementById('legalEntityIds')?.addEventListener('change', updateRequiredProgress);
        document.getElementById('btnSaveProductScope')?.addEventListener('click', () => { void submitForm().catch(reportFailure); });
    };
    const initSelectors = () => {
        const $legalEntities = $('#legalEntityIds');
        if ($legalEntities.length && !$legalEntities.hasClass('select2-hidden-accessible')) {
            $legalEntities.select2({ dropdownParent: $(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', width: 'element' });
        }
    };
    return {
        init: async () => { initSelectors(); bind(); await initDataTable(); },
        buildListOptions,
        testHooks: { resolveManageMode, canEndPolicy, applyLegalEntitySelection, lifecycleText, failureText, legalEntitiesText }
    };
})();
document.addEventListener('DOMContentLoaded', () => ProductLegalEntityScopesList.init());
