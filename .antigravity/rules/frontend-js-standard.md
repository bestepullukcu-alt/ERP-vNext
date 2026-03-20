# GOLDEN RULE: JavaScript Module Pattern & DataTable v2 Standard

Diten ERP vNext projelerinde her modülün `index.js` dosyası aşağıdaki "Module Pattern" (IIFE) yapısında olmalıdır. Global Scope asla kirletilmez.

## 🏗️ JS Mimari Kuralları

1. **Encapsulation:** Tüm değişkenler ve fonksiyonlar `const {{ModuleName}} = (function () { ... })();` içinde olmalıdır.
2. **`DtDefaults.create()` ZORUNLU:** Ham `DataTable({...})` çağrısı KESİNLİKLE YASAKTIR. Her DataTable sayfası `window.DtDefaults.create({...})` ile başlatılır. Bu wrapper otomatik olarak skeleton, stateSave, responsive class fix ve hover'ı devreye alır.
3. **`DtDefaults.exportButtons()` ZORUNLU:** Butonlar elle `layout` içinde tanımlanmaz. Her zaman `DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons, options)` kullanılır. `options` ile `exportColumns` / `colvisColumns` override edilebilir.  
   - **Responsive UI guard:** `DtDefaults` içindeki Export (collection) butonu `dt-export-collection-btn` class’ını taşır; `backbone-custom.css` bu class ile Export’u mobil toolbar’da `.btn-icon` yüksekliğiyle hizalar. Bu class kaldırılmaz/değiştirilmez.
4. **Personalization Client ZORUNLU:** Save View / kullanıcı tercihleri için raw `fetch('/api/personalization/...')` veya localStorage helper yazılmaz. Her zaman shared `window.personalizationClient` kullanılır.
   - **401/Auth Refresh Guard:** `window.personalizationClient` içindeki istekler `401 Unauthorized` aldığında merkezi unauthorized akışını kullanmalıdır (`window.DtDefaults.handleUnauthorized()` veya proje eşdeğeri). Expired JWT senaryosu generic `ErrorOccurred` toast'ı ile gizlenmez; kullanıcı refresh/login akışına taşınır.
5. **AJAX Gateway:** Tüm istekler `window.ApiBaseUrl` (`/api/...`) üzerinden gider. Servis bazlı URL (`/mdm/api/v1/...`) kullanılmaz.
6. **L10n Bridge:** Metinler JS içinde hardcoded yazılmaz; `window.L10n` objesinden okunur. `window.L10n` payload'ı `_IndexL10n.cshtml` + `index.l10n.js` deseniyle yüklenir; `Index.cshtml` içine uzun assignment bloğu gömülmez.
7. **Silme:** Tek satır silme `window.showConfirm()`, toplu silme `Swal.fire` ile yapılır. Direkt `window.showConfirm` bypass edilemez.
8. **Toast:** Başarı/hata bildirimleri her zaman `window.showToast('Key', 'success'|'error')` ile verilir.
   - İstisna: auth refresh/login'e devredilmiş `401` akışında kullanıcıya ek olarak generic hata toast'ı basılmaz.
9. **Save View (v2) — Applied State:** Save View görünürlüğü ve kaydedilen state, staged UI seçimlerine göre değil **applied/effective** tablo state’ine göre hesaplanmalıdır:
   - Filter değişimi tek başına (Apply basılmadan) Save View’u göstermemelidir.
   - Uygulama paterni: `appliedFilters` (veya benzeri) state’ini sadece Apply/Reset’te güncelle; `getCurrentView()` filtre değerlerini buradan okusun.
10. **Save View (v2) — Shared Payload:** Saved View payload’ı minimum olarak `filters + search + colVis + columnOrder + sorting` içermelidir. `pageNumber/pageLength` persist edilmez.
11. **ColReorder (v2):** Kolon sürükle-bırak aktif edilen sayfalarda:
   - `colReorder` DataTable config’i `DtDefaults.create({...})` içine verilir.
   - `columnOrder` Save View kapsamına eklenir.
   - `column-reorder.dt` / `columns-reordered.dt` event’leri dirty-state hesabına dahil edilir.
12. **Inline Filter Select2 Styling:** `#inlineFilterHost` içindeki Select2 single-select filtrelerinde `selectionCssClass: 'form-select form-select-sm'` kullanılır. Görsel standardın CSS karşılığı `backbone-custom.css` içindedir; Index.cshtml içine tekrar yazılmaz.
    > ⚠️ **Overflow uyarısı:** `selectionCssClass: 'form-select form-select-sm'` Bootstrap'ın `.form-select` sınıfı üzerinden `.select2-selection` elementine `inline-size: 100% !important` uygular. Select açıldığında sayfa yatay/dikey scroll yapabilir. `backbone-custom.css` içindeki `#inlineFilterHost .dt-filter-bar .filter-chip .select2-selection { inline-size: auto !important; }` override'ı bu bug'ı önler ve kaldırılamaz (MOD-0031).
13. **Shared CSS Placement:** Toolbar, inline filter, badge stacking ve Select2 dropdown stilleri tekrar kullanılabilir ise `backbone-custom.css` içinde tutulur; `@section Styles` yalnızca gerçekten modüle özgü istisnalar için kullanılır.

---

## 📄 JavaScript Master Template

```javascript
/**
 * {{ModuleName}} DataTables Page Script
 * Diten ERP vNext - {{AreaName}}/{{ModuleName}}
 */
'use strict';

const {{ModuleName}}List = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-{{ModuleNameLower}}');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    let L = window.L10n || {};
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) {
            L = current;
            return;
        }

        L = L || {};
    };

    const getTenantId = () => {
        try {
            const user = JSON.parse(localStorage.getItem('user') || '{}');
            return user.tenantId || '00000000-0000-0000-0000-000000000001';
        } catch (e) {
            return '00000000-0000-0000-0000-000000000001';
        }
    };

    const getCookie = (name) => {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    };

    const getAuthHeaders = () => {
        const token = getCookie('access_token');
        return {
            'X-Tenant-Id': getTenantId(),
            'Authorization': token ? `Bearer ${token}` : ''
        };
    };

    const getStatusMap = () => ({
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    });

    const tryParseRowJson = (element) => {
        if (!element) return null;
        const raw = element.getAttribute('data-json');
        if (!raw) return null;

        try {
            return JSON.parse(raw.replace(/&#39;/g, "'"));
        } catch (err) {
            console.error('[{{ModuleName}} QuickView] Could not parse row data', err);
            return null;
        }
    };

    const populateOffcanvas = (data) => {
        if (!data) return;

        document.getElementById('oc-title').innerText = data.name || data.title || '-';
        document.getElementById('oc-subtitle').innerText = data.subtitle || '-';

        const statusEl = document.getElementById('oc-status');
        const status = getStatusMap()[String(data.isActive)] || { title: L.Unknown || String(data.isActive), class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        document.getElementById('oc-btn-edit').href = `/{{ModuleName}}/Edit/${data.id}`;

        // DİNAMİK OFFCANVAS JS ATAMALARI — modüle özgü alanlar buraya
        // {{DynamicOffcanvasJs}}
    };

    /**
     * Mount inline filter panel right under DataTable toolbar row.
     * (_Filter.cshtml is rendered on the page, we relocate it near the filter button.)
     */
    const mountInlineFilter = () => {
        if (!dtTableEl) return;

        const host = document.getElementById(filterHostId);
        if (!host) return;

        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-6'); // project standard (do not use mx-*)
            return;
        }

        // Fallback: place it before the table within the same container
        const dtContainer = dtTableEl.closest('.dt-container') || dtTableEl.closest('.dataTables_wrapper') || dtTableEl.parentElement;
        if (dtContainer) {
            dtContainer.insertAdjacentElement('beforeend', host);
            host.classList.add('px-6');
        }
    };

    /**
     * Some DataTables button render flows don't play nicely with Bootstrap's data-API.
     * Bind explicit toggle behavior for the inline collapse.
     */
    const bindInlineFilterToggle = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el) return;
        if (btn.dataset.inlineFilterBound) return;
        btn.dataset.inlineFilterBound = '1';

        // Keep aria-expanded in sync
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));

        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const instance = bootstrap.Collapse.getOrCreateInstance(el, { toggle: false });
            if (el.classList.contains('show')) instance.hide(); else instance.show();
        });
    };

    const initDataTable = () => {
        if (!dtTableEl) return;

        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: function () { window.showToast?.(L.ComingSoon, 'info'); }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-controls': filterCollapseId,
                    'aria-expanded': 'false'
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/{{ModuleNameLower}}',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            // İhtiyaç varsa aktif et:
            // colReorder: { columns: ':gt(1):not(:last-child)' },
            columns: [
                { data: 'id',       name: 'control'   },   // Responsive control
                { data: 'id',       name: 'checkbox'  },   // Checkbox
                // {{JSColumns}} — modüle özgü kolonlar (name: zorunlu)
                { data: 'isActive', name: 'isActive'  },
                { data: 'action',   name: 'action'    }
            ],
            columnDefs: [
                {
                    // Responsive Control Column
                    targets: 0,
                    className: 'control',
                    searchable: false,
                    orderable: false,
                    responsivePriority: 2,
                    render: () => ''
                },
                {
                    // Checkbox Column
                    targets: 1,
                    orderable: false,
                    searchable: false,
                    responsivePriority: 3,
                    className: 'dt-checkboxes-cell cell-fit',
                    render: (data) =>
                        `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">`
                },
                // {{JSColumnDefs}} — modüle özgü kolonDef'ler buraya
                {
                    // Status Badge (display HTML, filter plain text)
                    targets: -2,
                    render: (data, type) => {
                        const status = getStatusMap()[String(data)] || { title: L.Unknown || String(data), class: 'bg-label-primary' };
                        if (type === 'display') return `<span class="badge ${status.class}" text-capitalized>${status.title}</span>`;
                        return status.title || '';
                    }
                },
                {
                    // Actions
                    targets: -1,
                    title: L.Actions,
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit',
                    render: (data, type, full) =>
                        `<div class="d-flex align-items-center">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="/{{ModuleName}}/Details/${full.id}" class="dropdown-item">${L.ViewDetails}</a>
                                <a href="javascript:void(0);" class="dropdown-item js-quick-view" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" data-json='${JSON.stringify(full).replace(/'/g, "&#39;")}'>${L.QuickView}</a>
                                <a href="/{{ModuleName}}/Edit/${full.id}" class="dropdown-item">${L.Edit}</a>
                            </div>
                        </div>`
                }
            ],
            // DtDefaults.exportButtons: 3 grup (Export, ColVis/Filter, AddNew)
            buttons: window.DtDefaults.exportButtons(
                L.AddNew{{ModuleName}},
                { onclick: "window.location.href='/{{ModuleName}}/Create'" },
                extraButtons,
                {
                    exportColumns: {{ExportColumns}},
                    colvisColumns: {{ColvisColumns}}
                }
            ),
            initComplete: function () {
                mountInlineFilter();
                bindInlineFilterToggle();
                setupFilters(this.api());
            },
            drawCallback: function () {
                const filterCount = 0; // Aktif filtre sayısı (filtre implemente edildikçe güncelleyin)
                window.DtDefaults.updateVisualState(this.api(), filterCount);
            }
        }));

        dt.on('column-visibility.dt', function () {
            const filterCount = 0;
            window.DtDefaults.updateVisualState(dt, filterCount);
        });

        dt.on('column-reorder.dt columns-reordered.dt', function () {
            const filterCount = 0;
            window.DtDefaults.updateVisualState(dt, filterCount);
        });
    };

    const setupFilters = (api) => {
        // {{DynamicFilterSetup}}

        const state = api.state.loaded();
        let initialFilterCount = 0;
        if (state) {
            // {{DynamicFilterRestore}}
        }

        window.DtDefaults.updateVisualState(api, initialFilterCount);

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            // {{DynamicFilterApply}}
            api.draw();

            const filterCount = 0;
            window.DtDefaults.updateVisualState(api, filterCount);

            const el = document.getElementById(filterCollapseId);
            if (el) bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', () => {
            // {{DynamicFilterReset}}
            api.state.clear();
            api.columns().search('').draw();
            window.DtDefaults.updateVisualState(api, 0);
        });
    };

    // ── Checkbox & Bulk Action ─────────────────────────────────────────────

    const getSelectedIds = () => {
        const ids = [];
        dtTableEl.querySelectorAll('.dt-checkboxes:checked').forEach(cb => ids.push(cb.value));
        return ids;
    };

    const updateBulkBar = () => {
        const ids = getSelectedIds();
        const bar = document.getElementById('bulkActionBar');
        const countEl = document.getElementById('bulkSelectedCount');
        if (!bar || !countEl) return;

        if (ids.length > 0) {
            bar.classList.remove('d-none');
            countEl.textContent = ids.length;
        } else {
            bar.classList.add('d-none');
            countEl.textContent = '0';
        }

        // Header checkbox senkronizasyonu
        const headerCb = dtTableEl?.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) {
            const total = dtTableEl.querySelectorAll('tbody .dt-checkboxes').length;
            headerCb.checked = ids.length > 0 && ids.length === total;
            headerCb.indeterminate = ids.length > 0 && ids.length < total;
        }
    };

    const clearSelection = () => {
        dtTableEl?.querySelectorAll('.dt-checkboxes:checked').forEach(cb => {
            cb.checked = false;
            cb.closest('tr')?.classList.remove('selected');
        });
        const headerCb = dtTableEl?.querySelector('thead .dt-checkboxes-select-all');
        if (headerCb) { headerCb.checked = false; headerCb.indeterminate = false; }
        updateBulkBar();
    };

    // ── Event Handlers ─────────────────────────────────────────────────────

    const handleEvents = () => {
        if (!dtTableEl) return;

        // Tek satır silme + Quick View click delegation
        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (deleteBtn) {
                let tr = deleteBtn.closest('tr');
                if (tr.classList.contains('child')) tr = tr.previousElementSibling;
                const row = dt.row(tr);
                const data = row.data();

                window.showConfirm?.('DeleteConfirmation', () => {
                    fetch(`${apiUrl}/api/{{ModuleNameLower}}/${data.id}`, {
                        method: 'DELETE',
                        headers: getAuthHeaders()
                    }).then(res => {
                        if (res.ok) {
                            row.remove().draw();
                            window.showToast?.('RecordDeleted', 'success');
                        } else {
                            window.showToast?.('ErrorOccurred', 'error');
                        }
                    }).catch(() => window.showToast?.('ErrorOccurred', 'error'));
                }, data.name || data.title);
            }

            const quickViewBtn = e.target.closest('.js-quick-view');
            if (quickViewBtn) {
                populateOffcanvas(tryParseRowJson(quickViewBtn));
            }
        });

        // Satır checkbox değişimi
        $(dtTableEl).on('change', '.dt-checkboxes', function () {
            const tr = $(this).closest('tr');
            if (this.checked) tr.addClass('selected'); else tr.removeClass('selected');
            updateBulkBar();
        });

        // Header "Tümünü Seç"
        $(dtTableEl).on('change', '.dt-checkboxes-select-all', function () {
            const isChecked = this.checked;
            dtTableEl.querySelectorAll('tbody .dt-checkboxes').forEach(cb => {
                cb.checked = isChecked;
                const tr = cb.closest('tr');
                if (isChecked) tr?.classList.add('selected'); else tr?.classList.remove('selected');
            });
            updateBulkBar();
        });

        // Seçimi temizle
        document.getElementById('btnClearSelection')?.addEventListener('click', () => clearSelection());

        // Toplu silme (Swal.fire doğrudan kullanımı burada kabul edilir)
        document.getElementById('btnBulkDelete')?.addEventListener('click', () => {
            const ids = getSelectedIds();
            if (!ids.length) return;

            const msg = (L.BulkDeleteConfirm || '').replace('{0}', ids.length);
            Swal.fire({
                title: L.AreYouSure,
                html: `<div class="mb-2">${msg}</div>`,
                iconHtml: '<div class="swal-icon-circle"><i class="bx bx-trash"></i></div>',
                showCancelButton: true,
                confirmButtonText: L.BulkDelete,
                cancelButtonText: L.Cancel,
                width: '400px',
                padding: '2.5rem 1.5rem 2rem',
                customClass: {
                    popup: 'rounded-4 shadow-lg',
                    title: 'fs-4 fw-bold text-heading mt-4 mb-2 d-block w-100 text-center',
                    htmlContainer: 'text-muted mb-3 d-block w-100 text-center',
                    actions: 'd-flex justify-content-center mt-4 w-100',
                    confirmButton: 'btn btn-danger waves-effect waves-light mx-2',
                    cancelButton: 'btn btn-label-secondary waves-effect mx-2',
                    icon: 'border-0 m-0 p-0 d-flex justify-content-center w-100'
                },
                buttonsStyling: false,
                reverseButtons: true
            }).then(result => {
                if (!result.isConfirmed) return;
                fetch(`${apiUrl}/api/{{ModuleNameLower}}/bulk`, {
                    method: 'DELETE',
                    headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                    body: JSON.stringify({ ids })
                }).then(res => {
                    if (res.ok) return res.json();
                    throw new Error('Bulk delete failed');
                }).then(data => {
                    window.showToast?.(
                        (L.BulkDeleteSuccess || '').replace('{0}', data.deletedCount),
                        'success'
                    );
                    clearSelection();
                    dt.ajax.reload();
                }).catch(() => window.showToast?.('ErrorOccurred', 'error'));
            });
        });
    };

    // ── Public API ─────────────────────────────────────────────────────────
    return {
        init: () => { initDataTable(); handleEvents(); }
    };
})();

document.addEventListener('DOMContentLoaded', () => {{ModuleName}}List.init());
```

## L10n Loader Contract

- `Index.cshtml` yükleme sırası zorunludur:
  1. `<partial name="_IndexL10n" />`
  2. `index.l10n.js`
  3. `index.js`
- `index.l10n.js` sadece payload parse + `window.L10n` merge yapar; DataTable init veya event binding içermez.
- `index.js` defensive olarak `syncL10n()` benzeri bir guard çağırabilir.

---

## ⚠️ Yasak Pratikler (Anti-patterns)

| ❌ Yasak | ✅ Doğru |
|----------|----------|
| `$(...).DataTable({...})` | `new DataTable(el, DtDefaults.create({...}))` |
| `layout: { topEnd: { buttons: [...] } }` elle tanımlama | `DtDefaults.exportButtons(text, attr, extras, options)` |
| `Swal.fire(...)` tek satır sil | `window.showConfirm('Key', callback, entityName)` |
| `toastr.success(...)` / `toastr.error(...)` | `window.showToast('Key', 'success'\|'error')` |
| `url: window.ApiBaseUrl + '/mdm/api/v1/...'` | `url: apiUrl + '/api/{{ModuleNameLower}}'` |
| `$.ajax(...)` CRUD | `fetch(...)` ile native async |
