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
    // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 13) — in the interface language, not the browser's.
    const dateTime = value => value
        ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
        : '—';

    // FIX1 (item 7) — the proxy answers every refusal with a sentence in the reader's language; a refusal whose code has
    // no sentence yet comes with `unmapped` (the status sentence is shown) and the console says so.
    const failureSentence = (body, status) => {
        if (body?.unmapped === true) console.warn('Brands: a refusal without a sentence mapping', status);
        const errors = Array.isArray(body?.errors) ? body.errors.filter(Boolean) : [];
        return errors[0] || L[`RefusedStatus_${status}`] || L.RefusedStatus_Other || L.ErrorState;
    };
    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error(failureSentence(body, response.status)), { status: response.status, body });
        return body.data;
    };

    // FIX1 (item 11) — the archive proxy validates the page's anti-forgery token.
    const antiForgeryToken = () =>
        document.querySelector('#brandDetailToken input[name="__RequestVerificationToken"]')?.value || '';

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

    /*
     * Archive uses POST /archive — never HTTP DELETE. FIX1 (item 3): archiving is IRREVERSIBLE (no un-archive exists; the
     * owner decides later whether one should), so the confirmation says so and how many of the brand's products are
     * still in use. WP-MDM-BRAND-ACCEPT-01 FIX2 (M7) — that count is read FRESH when the dialog opens (a product linked or
     * archived after this page was drawn is counted); when it cannot be read the dialog does not open.
     */
    const archiveSentence = liveProducts => {
        const consequence = liveProducts > 0
            ? String(L.ArchiveBrandLiveProducts || '').replace('{0}', String(liveProducts))
            : L.ArchiveBrandNoLiveProducts;
        return [L.ArchiveBrandIrreversible, consequence].filter(Boolean).join(' ');
    };
    const readLiveProducts = async () => {
        const detail = await envelope(await fetch(
            `${endpoint}/${encodeURIComponent(page.brandId)}/detail`,
            { credentials: 'same-origin', headers: getAuthHeaders() }));
        const count = detail?.liveProductCount;
        return Number.isInteger(count) ? count : 0;
    };
    document.getElementById('archiveBrand')?.addEventListener('click', async () => {
        let liveProducts;
        try {
            liveProducts = await readLiveProducts();
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
            return;
        }
        window.showConfirm?.(L.ArchiveBrand, async () => {
            try {
                await envelope(await fetch(`${endpoint}/${page.brandId}/archive`, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: Object.assign(getAuthHeaders(), { RequestVerificationToken: antiForgeryToken() })
                }));
                window.showToast?.(L.RecordArchived, 'success');
                window.location.reload();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { type: 'warning', subtext: archiveSentence(liveProducts), confirmButtonText: L.ArchiveBrand });
    });

    loadProducts();
})(window, document);
