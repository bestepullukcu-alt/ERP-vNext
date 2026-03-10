# GOLDEN RULE: JavaScript Module Pattern & DataTable v2 Standard

Diten ERP vNext projelerinde her modülün `index.js` dosyası aşağıdaki "Module Pattern" (IIFE) yapısında olmalıdır. Global Scope asla kirletilmez.

## 🏗️ JS Mimari Kuralları

1. **Encapsulation:** Tüm değişkenler ve fonksiyonlar `const {{ModuleName}} = (function () { ... })();` içinde olmalıdır.
2. **`DtDefaults.create()` ZORUNLU:** Ham `DataTable({...})` çağrısı KESİNLİKLE YASAKTIR. Her DataTable sayfası `window.DtDefaults.create({...})` ile başlatılır. Bu wrapper otomatik olarak skeleton, stateSave, responsive class fix ve hover'ı devreye alır.
3. **`DtDefaults.exportButtons()` ZORUNLU:** Butonlar elle `layout` içinde tanımlanmaz. Her zaman `DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons)` kullanılır.
4. **AJAX Gateway:** Tüm istekler `window.ApiBaseUrl` (`/api/...`) üzerinden gider. Servis bazlı URL (`/mdm/api/v1/...`) kullanılmaz.
5. **L10n Bridge:** Metinler JS içinde hardcoded yazılmaz; `window.L10n` objesinden okunur.
6. **Silme:** Tek satır silme `window.showConfirm()`, toplu silme `Swal.fire` ile yapılır. Direkt `window.showConfirm` bypass edilemez.
7. **Toast:** Başarı/hata bildirimleri her zaman `window.showToast('Key', 'success'|'error')` ile verilir.

---

## 📄 JavaScript Master Template

```javascript
/**
 * {{ModuleName}} Management Module
 * Diten ERP vNext - {{AreaName}} Module
 */
'use strict';

const {{ModuleName}}List = (function () {
    // ── Variables ──────────────────────────────────────────────────────────
    let dt;
    const dtTableEl = document.querySelector('.datatables-{{ModuleNameLower}}');
    const apiUrl = window.ApiBaseUrl || 'http://localhost:5000';
    const L = window.L10n || {};

    // ── Auth Helpers ───────────────────────────────────────────────────────
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

    // ── DataTable Initialization ───────────────────────────────────────────
    const initDataTable = () => {
        if (!dtTableEl) return;

        // Extra buttons (Filter + Import buttonları gerekli ise tanımla)
        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter || 'Filter', 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#offcanvasFilter' }
            }
            // importBtn: { ... }   // Gerekli ise buraya ekle
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: apiUrl + '/api/{{ModuleNameLower}}',
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            columns: [
                { data: 'id',       name: 'control'   },   // Responsive control
                { data: 'id',       name: 'checkbox'  },   // Checkbox
                // {{JSColumns}} — modüle özgü kolonlar
                { data: 'isActive', name: 'isActive'  },
                { data: null,       name: 'action'    }
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
                    // Status Badge
                    targets: -2,
                    render: (data) => {
                        const cls = data ? 'bg-label-success' : 'bg-label-secondary';
                        const txt = data ? (L.Active || 'Active') : (L.Passive || 'Passive');
                        return `<span class="badge ${cls}">${txt}</span>`;
                    }
                },
                {
                    // Actions
                    targets: -1,
                    title: L.Actions || 'Actions',
                    searchable: false,
                    orderable: false,
                    className: 'cell-fit',
                    render: (data, type, full) =>
                        `<div class="d-flex align-items-center">
                            <a href="javascript:;" class="btn btn-icon delete-record text-danger me-1"><i class="bx bx-trash icon-md"></i></a>
                            <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></a>
                            <div class="dropdown-menu dropdown-menu-end m-0">
                                <a href="/{{ModuleName}}/Details/${full.id}" class="dropdown-item">${L.ViewDetails || 'View Details'}</a>
                                <a href="javascript:void(0);" class="dropdown-item" data-bs-toggle="offcanvas" data-bs-target="#offcanvasDetailsPreview" onclick="populateOffcanvas(this)" data-json='${JSON.stringify(full).replace(/'/g, "&#39;")}'>${L.QuickView || 'Quick View'}</a>
                                <a href="/{{ModuleName}}/Edit/${full.id}" class="dropdown-item">${L.Edit || 'Edit'}</a>
                            </div>
                        </div>`
                }
            ],
            // DtDefaults.exportButtons: 3 grup (Export, ColVis/Filter, AddNew)
            buttons: window.DtDefaults.exportButtons(
                L.AddNew{{ModuleName}} || 'Add New',
                { onclick: "window.location.href='/{{ModuleName}}/Create'" },
                extraButtons
            ),
            drawCallback: function () {
                const filterCount = 0; // Aktif filtre sayısı (filtre implemente edildikçe güncelleyin)
                window.DtDefaults.updateVisualState(this.api(), filterCount);
            }
        }));
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

        // Tek satır silme — window.showConfirm zorunludur
        dtTableEl.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.delete-record');
            if (!deleteBtn) return;

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

            const msg = (L.BulkDeleteConfirm || 'Delete {0} selected records?').replace('{0}', ids.length);
            Swal.fire({
                title: L.AreYouSure || 'Are you sure?',
                html: `<div class="mb-2">${msg}</div>`,
                iconHtml: '<div class="swal-icon-circle"><i class="bx bx-trash"></i></div>',
                showCancelButton: true,
                confirmButtonText: L.BulkDelete || 'Bulk Delete',
                cancelButtonText: L.Cancel || 'Cancel',
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
                        (L.BulkDeleteSuccess || '{0} records deleted').replace('{0}', data.deletedCount),
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

---

## ⚠️ Yasak Pratikler (Anti-patterns)

| ❌ Yasak | ✅ Doğru |
|----------|----------|
| `$(...).DataTable({...})` | `new DataTable(el, DtDefaults.create({...}))` |
| `layout: { topEnd: { buttons: [...] } }` elle tanımlama | `DtDefaults.exportButtons(text, attr, extras)` |
| `Swal.fire(...)` tek satır sil | `window.showConfirm('Key', callback, entityName)` |
| `toastr.success(...)` / `toastr.error(...)` | `window.showToast('Key', 'success'\|'error')` |
| `url: window.ApiBaseUrl + '/mdm/api/v1/...'` | `url: apiUrl + '/api/{{ModuleNameLower}}'` |
| `$.ajax(...)` CRUD | `fetch(...)` ile native async |