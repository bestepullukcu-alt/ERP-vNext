(function (window, document) {
    'use strict';
    const page = window.BrandPage;
    if (!page) return;

    const endpoint = '/MasterData/Brands/api';
    const L = window.BrandL10n || {};

    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = value => String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const badge = (value, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(value || '—')}</span>`;
    const statusLabel = value => L[`Status_${value}`] || value || '—';
    const dateTime = value => value ? new Date(value).toLocaleString() : '—';

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };

    // Products tab — READ-ONLY. Rows link to the product detail page, which owns product actions; nothing here
    // mutates a product, and archiving this brand never cascades to them.
    const loadProducts = async () => {
        const tableEl = document.getElementById('dt-brand-products');
        if (!tableEl || !page.canReadProducts) return;

        try {
            const rows = await envelope(await fetch(
                `${endpoint}/${page.brandId}/products?includeArchived=true`,
                { credentials: 'same-origin', headers: getAuthHeaders() })) || [];

            const config = {
                data: rows, stateSave: false, searching: false, processing: true,
                order: [[2, 'asc']],
                columns: [
                    { data: null, defaultContent: '' }, { data: 'productCode' }, { data: 'productName' },
                    { data: 'productStatus' }, { data: 'productType' }, { data: 'isArchived' },
                    { data: 'updatedAt' }, { data: null }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', orderable: false, render: () => '' },
                    { targets: 2, render: value => `<span class="fw-medium">${esc(value)}</span>` },
                    { targets: 3, render: value => badge(statusLabel(value), value === 'archived' ? 'secondary' : 'primary') },
                    { targets: 4, render: value => esc(value || '—') },
                    { targets: 5, render: value => badge(value ? L.Yes : L.No, value ? 'warning' : 'success') },
                    { targets: 6, render: value => dateTime(value) },
                    {
                        targets: 7, orderable: false, searchable: false, className: 'text-end',
                        render: (value, type, row) =>
                            `<a class="btn btn-sm btn-icon" href="/MasterData/Products/${esc(row.productId)}" title="${esc(L.View)}"><i class="bx bx-show"></i></a>`
                    }
                ],
                language: { emptyTable: L.EmptyState, processing: L.Loading }
            };
            new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
        }
    };

    // Archive uses POST /archive — never HTTP DELETE.
    document.getElementById('archiveBrand')?.addEventListener('click', () => {
        window.showConfirm?.(L.ArchiveBrandConfirm, async () => {
            try {
                await envelope(await fetch(`${endpoint}/${page.brandId}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                window.showToast?.(L.RecordArchived, 'success');
                window.location.reload();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { type: 'warning', confirmButtonText: L.ArchiveBrand });
    });

    loadProducts();
})(window, document);
