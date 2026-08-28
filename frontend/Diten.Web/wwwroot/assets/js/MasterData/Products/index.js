(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-products');
    if (!tableEl) return;

    // Same-origin MVC proxy only. The MdmService port (5059) never appears in browser code.
    const endpoint = '/MasterData/Products/api';
    const L = window.ProductL10n || {};
    let table = null;
    let contract = null;

    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = value => String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const badge = (value, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(value || '—')}</span>`;
    const statusLabel = value => L[`Status_${value}`] || value || '—';
    const typeLabel = value => (value && L[`ProductType_${value}`]) || value || '—';
    const date = value => value ? new Date(value).toLocaleDateString() : '—';
    const dateTime = value => value ? new Date(value).toLocaleString() : '—';

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };

    const setOptions = (id, values, placeholder, labeller) => {
        const select = document.getElementById(id);
        if (!select) return;
        select.innerHTML = `<option value="">${esc(placeholder)}</option>`
            + (values || []).map(x => `<option value="${esc(x)}">${esc(labeller ? labeller(x) : x)}</option>`).join('');
    };

    // Brand filter options come from the live brands endpoint, never a hardcoded list. When the operator has no
    // brand read permission the endpoint answers 403 and the filter is simply left empty.
    const loadBrandOptions = async () => {
        const select = document.getElementById('filterBrandId');
        if (!select) return;
        try {
            const data = await envelope(await fetch(`${endpoint}/brands?includeArchived=false`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            const brands = data?.items || [];
            select.innerHTML = `<option value="">${esc(L.BrandId || 'Brand')}</option>`
                + brands.map(x => `<option value="${esc(x.brandId)}">${esc(x.brandCode)} — ${esc(x.brandName)}</option>`).join('');
        } catch {
            select.innerHTML = `<option value="">${esc(L.BrandId || 'Brand')}</option>`;
        }
    };

    // Fail closed: no contract → create action hidden, error banner shown.
    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            if (!contract?.isReady || !contract?.features?.supportsProductManagement) throw new Error(L.BrandProductContractUnavailable);
            setOptions('filterProductStatus', contract.vocabulary?.productStatuses, L.ProductStatus, statusLabel);
            setOptions('filterProductType', contract.vocabulary?.productTypes, L.ProductType, typeLabel);
            setOptions('filterDosageForm', contract.vocabulary?.dosageForms, L.DosageForm);
            return true;
        } catch (error) {
            const host = document.getElementById('productContractError');
            if (host) { host.textContent = error.message || L.BrandProductContractUnavailable; host.classList.remove('d-none'); }
            document.getElementById('btnCreateProduct')?.classList.add('d-none');
            return false;
        }
    };

    const query = () => {
        const params = new URLSearchParams();
        const fields = {
            search: 'filterSearch', productStatus: 'filterProductStatus', brandId: 'filterBrandId',
            productType: 'filterProductType', dosageForm: 'filterDosageForm', therapeuticAreaId: 'filterTherapeuticAreaId'
        };
        Object.entries(fields).forEach(([key, id]) => {
            const value = document.getElementById(id)?.value.trim();
            if (value) params.set(key, value);
        });
        params.set('includeArchived', document.getElementById('filterIncludeArchived')?.checked ? 'true' : 'false');
        return params.toString();
    };

    const actions = row => {
        const id = esc(row.productId);
        // Compact modules open a full Details page rather than a Slim quick-view offcanvas; the js-quick-view
        // hook is kept on the link so the shared DataTable lifecycle can still recognise the row-open action.
        const items = [`<a class="dropdown-item js-quick-view" href="/MasterData/Products/${id}"><i class="bx bx-show me-2"></i>${esc(L.View)}</a>`];
        // Archived products are read-only. There is no delete action at all.
        if (!row.isArchived) {
            items.push(`<a class="dropdown-item" href="/MasterData/Products/${id}/Edit"><i class="bx bx-edit me-2"></i>${esc(L.EditProduct)}</a>`);
            items.push(`<button class="dropdown-item text-warning js-archive-product" data-id="${id}" data-name="${esc(row.productName)}"><i class="bx bx-archive-in me-2"></i>${esc(L.ArchiveProduct)}</button>`);
        }
        return `<div class="dropdown"><button class="btn btn-sm btn-icon" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded"></i></button><div class="dropdown-menu dropdown-menu-end">${items.join('')}</div></div>`;
    };

    const loadRows = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        try {
            const data = await envelope(await fetch(`${endpoint}?${query()}`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            const rows = data?.items || [];
            if (table) { table.clear(); table.rows.add(rows).draw(); return; }

            const config = {
                data: rows, stateSave: false, searching: false, processing: true,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                order: [[15, 'desc']],
                columns: [
                    { data: null, defaultContent: '' }, { data: 'productCode' }, { data: 'productName' },
                    { data: 'productStatus' }, { data: 'brandId' }, { data: 'productType' }, { data: 'dosageForm' },
                    { data: 'strength' }, { data: 'packSize' }, { data: 'unitOfMeasure' }, { data: 'atcCode' },
                    { data: 'therapeuticAreaId' }, { data: 'effectiveFrom' }, { data: 'effectiveTo' },
                    { data: 'isArchived' }, { data: 'updatedAt' }, { data: null }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, render: () => '' },
                    { targets: 2, render: value => `<span class="fw-medium">${esc(value)}</span>` },
                    { targets: 3, render: value => badge(statusLabel(value), value === 'archived' ? 'secondary' : 'primary') },
                    { targets: 5, render: value => esc(typeLabel(value)) },
                    { targets: [4, 6, 7, 8, 9, 10, 11], render: value => esc(value || '—') },
                    { targets: [12, 13], render: value => date(value) },
                    { targets: 14, render: value => badge(value ? L.Yes : L.No, value ? 'warning' : 'success') },
                    { targets: 15, render: value => dateTime(value) },
                    // Dense pharma columns start hidden; the operator can re-enable them via column visibility.
                    { targets: [7, 8, 9, 10, 11, 12, 13], visible: false },
                    { targets: 16, orderable: false, searchable: false, className: 'text-end', render: (value, type, row) => actions(row) }
                ],
                language: { emptyTable: L.EmptyState, processing: L.Loading },
                buttons: window.DtDefaults ? window.DtDefaults.exportButtons('', null, {
                    filterBtn: {
                        text: '<i class="bx bx-filter-alt"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                        attr: { title: L.Filter },
                        action: () => window.bootstrap?.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'))?.toggle()
                    }
                }, {
                    exportColumns: [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15],
                    colvisColumns: [1,2,3,4,5,6,7,8,9,10,11,12,13,14,15]
                }) : []
            };
            table = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
            if (!table) document.getElementById('productContractError')?.classList.remove('d-none');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    // Archive uses POST /archive — never HTTP DELETE.
    document.addEventListener('click', event => {
        // Quick-view rows navigate on their own href; only the archive action needs handling here.
        if (event.target.closest('.js-quick-view')) return;
        const archive = event.target.closest('.js-archive-product');
        if (!archive) return;
        window.showConfirm?.(L.ArchiveProductConfirm, async () => {
            try {
                await envelope(await fetch(`${endpoint}/${archive.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                window.showToast?.(L.RecordArchived, 'success');
                await loadRows();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.ArchiveProduct });
    });

    document.getElementById('btnFilterApply')?.addEventListener('click', loadRows);
    document.getElementById('btnFilterReset')?.addEventListener('click', event => {
        event.preventDefault();
        document.getElementById('productFilterForm')?.reset();
        loadRows();
    });

    (async () => {
        if (await loadContract()) { await loadBrandOptions(); await loadRows(); }
        else document.getElementById('skeleton-loader')?.classList.add('d-none');
    })();
})(window, document);
