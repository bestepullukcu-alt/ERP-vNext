## JS-015: Client-Side Lookup Loading (Mandatory)
Form sayfalarında (Create/Edit) ve Liste sayfalarındaki (Index) filtrelerde Select/Dropdown listeleri asla sunucu tarafında (`ViewModel` + `asp-items`) doldurulmaz. Tüm lookup verileri AJAX/Fetch ile çekilmeli ve dinamik olarak render edilmelidir.

**Standard Application for Filters (Index):**
1.  **Placement:** `setupFilters` fonksiyonu içerisinde lookup verileri çekilmelidir.
2.  **Execution:** DataTable çizilmeden önce veya `initComplete` içerisinde filtreler beslenmelidir.
3.  **Dependency:** Filtre select kutuları doldurulmadan kullanıcıya "Apply" izni verilmemelidir (veya boş state handle edilmelidir).

## JS-016: L10n Bridge Debugging & Fallback
`window.L10n` nesnesi oluşturulurken eksik anahtarların tespiti geliştirme aşamasında kritiktir.

**Standart Uygulama:**
- `index.l10n.js` veya merkezi L10n loader içerisinde, gelen değer `undefined` veya anahtarın kendisiyle aynı ise (Localizer fallback durumu) konsola `[L10N WARNING]` basılmalıdır.
- **CamelCase Rule:** Backend'den dönen JSON property isimleri ile JS tarafındaki `data.property` eşleşmelerinde case-sensitivity hatalarını önlemek için `payload.name || payload.Name` gibi çift yönlü kontrol standarttır.

Diten ERP vNext projelerinde her modülün `index.js` dosyası aşağıdaki "Module Pattern" (IIFE) yapısında olmalıdır. Global Scope asla kirletilmez.

## 🏗️ JS Mimari Kuralları

1. **Encapsulation:** Tüm değişkenler ve fonksiyonlar `const {{ModuleName}} = (function () { ... })();` içinde olmalıdır.
2. **`DtDefaults.create()` ZORUNLU:** Ham `DataTable({...})` çağrısı KESİNLİKLE YASAKTIR. Her DataTable sayfası `window.DtDefaults.create({...})` ile başlatılır. Bu wrapper otomatik olarak skeleton, stateSave, responsive class fix ve hover'ı devreye alır.
3. **`DtDefaults.exportButtons()` ZORUNLU:** Butonlar elle `layout` içinde tanımlanmaz. Her zaman `DtDefaults.exportButtons(addNewText, addNewAttr, extraButtons, options)` kullanılır. `options` ile `exportColumns` / `colvisColumns` override edilebilir.  
   - **Responsive UI guard:** `DtDefaults` içindeki Export (collection) butonu `dt-export-collection-btn` class’ını taşır; `backbone-custom.css` bu class ile Export’u mobil toolbar’da `.btn-icon` yüksekliğiyle hizalar. Bu class kaldırılmaz/değiştirilmez.
4. **Personalization Client ZORUNLU:** Save View / kullanıcı tercihleri için raw `fetch('/api/personalization/...')` veya localStorage helper yazılmaz. Her zaman shared `window.personalizationClient` kullanılır.
   - **Dual-mode tenant header:** `/api/personalization/*` Platform servisinde yaşar ama hem platform admin hem tenant account ekranlarından kullanılır. `window.personalizationClient` header davranışı aktöre göre olmalıdır:
     - `actorType === 'tenant_user'`: `X-Tenant-Id` gönderilir (`window.CurrentUser.tenantId`).
     - `actorType === 'platform_admin' | 'partner_admin'`: `X-Tenant-Id` gönderilmez.
     - Account/tenant tarafında `X-Tenant-Id` kaldırmak, platform tarafında ise göndermek hatadır. Gateway/Platform middleware bu path'i çift modlu kabul etmelidir.
   - **401/Auth Refresh Guard:** `window.personalizationClient` içindeki istekler `401 Unauthorized` aldığında merkezi unauthorized akışını kullanmalıdır (`window.DtDefaults.handleUnauthorized()` veya proje eşdeğeri). Expired JWT senaryosu generic `ErrorOccurred` toast'ı ile gizlenmez; kullanıcı refresh/login akışına taşınır.
5. **AJAX API Profile (SSOT):** DataTable istekleri module pack/domain bağlamına göre iki profilden biriyle yapılır; ajan profil seçimini açık yazmadan `index.js` üretmez.
   - **`proxy-profile` (Platform/admin MVC default):** Browser JS, same-origin frontend proxy endpoint'ine gider: `/{AreaName}/{ModuleName}/api`. MVC controller bu isteği server-side `GatewayUrl` üzerinden `/api/...` rotasına forward eder ve `access_token` HttpOnly cookie'sini `Authorization: Bearer` header'ına sadece server tarafında çevirir.
   - **`direct-gateway-profile` (browser-safe token stratejisi olan tenant/public shell):** Browser JS merkezi `window.API.{service}` objesini kullanır. Ajanlar asla doğrudan `localhost:5000` gibi hardcoded URL yazmaz.
   - **Yasak:** DataTable JS içinde `document.cookie`, `access_token` veya elle `Authorization: Bearer ...` üretimi KESİNLİKLE YASAKTIR. HttpOnly cookie tarayıcı JS'inden okunamaz; bu deneme `403` ve boş DataTable üretir.
   - **Tenant header:** Platform/admin context'te `X-Tenant-Id` gönderilmez. Tenant console modüllerinde tenant header sadece merkezi shell/helper üzerinden gelir. Personalization Save View için bu merkezi helper `actorType` bazlı çift modlu olmalıdır.
6. **L10n Bridge:** Metinler JS içinde hardcoded yazılmaz; `window.L10n` objesinden okunur. `window.L10n` payload'ı `_IndexL10n.cshtml` + `index.l10n.js` deseniyle yüklenir; `Index.cshtml` içine uzun assignment bloğu gömülmez.
7. **Silme:** Tek satır silme ve toplu silme aynı görsel dilde confirm açmalı ve aynı success lifecycle'ını kullanmalıdır.
   - Tek satır silmede `window.showConfirm()` veya onunla aynı görsel standardı üreten ortak helper kullanılabilir.
   - Başarılı silme sonrası `row.remove().draw()` ile lokal DOM manipülasyonu yapıp hemen toast basmak YASAKTIR.
   - Doğru pattern: başarılı DELETE → `clearSelection()` → `dt.ajax.reload(callback, false)` → callback içinde success toast.
   - `false` ile mevcut paging korunur.
   - **Endpoint sahipliği zorunludur:** Tekil ve bulk delete çağrıları yalnızca modülün kendi endpoint'ine yapılır (`/api/{module}` + `/api/{module}/bulk`). Başka modül endpoint'ine silme isteği göndermek YASAKTIR.
   - **Bulk confirm zorunluluğu:** Bulk delete de tekil delete ile aynı confirm standardını (`window.showConfirm`/ortak wrapper) kullanır; farklı modal/component kullanımı YASAKTIR.
7.1. **Bulk/Row Action Dispatcher (ZORUNLU):** Standart DataTable sayfalarında checkbox selection, bulk action bar ve row action dropdown elle bağlanmaz. Aşağıdaki shared helper'lardan biri kullanılmalıdır:
   - Tercih edilen yol: `window.DitenDataTable.createCrudTable({ tableEl, bulk: bulkOptions, actions: { onRowAction }, config })`.
   - Server-side veya özel ajax callback nedeniyle manuel `new DataTable(... window.DtDefaults.create(...))` gerekiyorsa init sonrası mutlaka `window.DitenDataTable.bindBulkSelection(tableEl, dt, bulkOptions)` ve `window.DitenDataTable.bindActionDispatcher({ tableEl, dt, onRowAction })` çağrılır.
   - Action kolonu `window.DitenDataTable.renderActions(...)` ile üretilir; primary delete + dropdown quickView/edit sırası GoldenReference ile aynı kalır.
   - Responsive control kolonu `targets: 0, className: 'control', responsivePriority: 2`, checkbox kolonu `targets: 1, className: 'dt-checkboxes-cell cell-fit', responsivePriority: 3`, action kolonu `className: 'cell-fit all ...'` standardını taşır.
   - `#btnBulkDelete` gibi modüle özel elle event listener bağlamak, shared bulk bar zaten `[data-bulk-action]` kullandığı için yeni modüllerde YASAKTIR.
8. **Toast:** Başarı/hata bildirimleri her zaman `window.showToast('KeyOrMessage', 'success'|'error'|'warning'|'info')` ile verilir.
   - İstisna: auth refresh/login'e devredilmiş `401` akışında kullanıcıya ek olarak generic hata toast'ı basılmaz.
   - Import gibi henüz uygulanmamış ama hata olmayan aksiyonlar `warning` veya `info` ile gösterilir; hata toast'ı kullanılmaz.
9. **Save View (v2) — Applied State:** Save View görünürlüğü ve kaydedilen state, staged UI seçimlerine göre değil **applied/effective** tablo state’ine göre hesaplanmalıdır:
   - Filter değişimi tek başına (Apply basılmadan) Save View’u göstermemelidir.
   - Uygulama paterni: `appliedFilters` (veya benzeri) state’ini sadece Apply/Reset’te güncelle; `getCurrentView()` filtre değerlerini buradan okusun.
10. **Save View (v2) — Shared Payload:** Saved View payload’ı minimum olarak `filters + search + colVis + columnOrder + sorting` içermelidir. `pageNumber/pageLength` persist edilmez.
10.0.0 **Save View (v2) — Valid Payload Name:** `saveDefaultView` payload'ında `viewName` boş gönderilemez. Backend `ViewName.NotEmpty()` doğrular; bu yüzden `getSavedViewName(defaultViewRecord) || L.SaveView || 'Default'` benzeri non-empty fallback zorunludur.
10.0.1 **Save View (v2) — Reset Baseline:** Inline filter `Reset`, sadece filtre kontrollerini boşaltan bir aksiyon değildir ve saved view'e geri dönme aksiyonu değildir. Reset her zaman fabrika/default tablo state'ini bütün olarak uygular:
   - Reset state: boş `filters/search`, default `colVis`, default `columnOrder`, default `order`.
   - Kullanıcı ColVis ile kolon kapatıp Save View yaptıktan sonra Reset'e basarsa kapatılan kolonlar geri açılmalıdır.
   - Reset sonrası ekran saved view'den farklıysa `dt-save-filter-btn` dirty-state olarak tekrar görünebilir; `setSaveFilterVisible(false)` ile zorla gizlenmez.
   - Reset handler'ı `applySavedTableState(api, getResetBaselineState())` benzeri tek kaynaklı bir restore fonksiyonu kullanmalıdır; filtreleri manuel temizleyip `ajax.reload()` yapmak YASAKTIR, çünkü ColVis/ColReorder stale kalır.
10.1 **Save View CTA Render (ZORUNLU):** Toolbar `extraButtons` içinde `saveFilterBtn` tanımı bulunmak zorundadır (`className` içinde `dt-save-filter-btn` + başlangıçta `d-none`). Dirty-state tespitinde buton görünürlüğü `setSaveFilterVisible()` ile yönetilir.
11. **ColReorder (v2) — Varsayılan Aktif:** Standart kolon yapısına sahip tüm liste sayfalarında (control + checkbox + N veri + action) `colReorder` **varsayılan olarak aktiftir**.
   - `colReorder: { columns: ‘:gt(1):not(:last-child)’ }` DataTable config’e **her zaman** eklenir.
   - Sadece ≤2 veri kolonu olan özel sayfalarda devre dışı bırakılabilir; bırakılırsa neden belirten yorum satırı zorunludur.
   - `columnOrder` Save View kapsamına eklenir (`captureColumnOrder` / `applyColumnOrder`).
   - `column-reorder.dt` / `columns-reordered.dt` event’leri dirty-state hesabına **mutlaka** dahil edilir.
12. **Inline Filter Select2 — Zorunlu Init Paterni:** `#inlineFilterHost` içindeki tüm Select2 filtreler aşağıdaki parametrelerle başlatılır. Eksiği olan implementasyon geçersizdir.

    **Filtre alan tipi kararı (ZORUNLU):**
    - Domain, Service, Category, Type, Owner, Status gibi enum veya sınırlı değer kümesi olan filtreler text input/search olarak üretilmez.
    - Bu alanlar `_Filter.cshtml` içinde `filter-chip` + `select.form-select.form-select-sm.select2` olarak render edilir.
    - Çoklu seçim gereken alanlarda `multiple="multiple"` kullanılır; backend/proxy `a,b,c` gibi çoklu değerleri `IN` filtresi olarak desteklemelidir.
    - Single-select filtrelerde ilk option boş `value=""` + `@SharedLocalizer["ShowAll"]` olmalıdır.

    **Single-select (allowClear destekli):**
    ```javascript
    $select.select2({
        dropdownParent: $(document.body),           // ZORUNLU
        dropdownCssClass: 'dt-inline-filter-dropdown', // ZORUNLU
        minimumResultsForSearch: Infinity,          // ZORUNLU
        selectionCssClass: 'form-select form-select-sm',
        width: 'element',
        allowClear: true
    });
    $select.on('select2:open', clampDropdown);
    ```

    **Multi-select:**
    ```javascript
    $select.select2({
        dropdownParent: $(document.body),           // ZORUNLU
        dropdownCssClass: 'dt-inline-filter-dropdown', // ZORUNLU
        minimumResultsForSearch: Infinity,          // ZORUNLU
        selectionCssClass: 'form-select form-select-sm',
        width: 'element',
        closeOnSelect: false
    });
    // ZORUNLU: Multi-select'te Select2 her zaman .select2-search--inline üretir
    // ve open'da setTimeout(focus,1) ile focus atar → window scroll tetiklenir.
    // minimumResultsForSearch:Infinity bunu durdurmaz. Çözüm CSS katmanındaki 
    // MOD-0031 (fixed position input) kuralıdır.
    $select.on('select2:open', clampDropdown);
    ```

    > 🚫 **`dropdownParent: $select.parent()`** — KESİNLİKLE YASAK.  
    > 🚫 **`width: '100%'`** — YASAK.  
    > 🚫 **Multi-select'te MOD-0031 CSS kuralının uygulanmaması** — YASAK. Yapılmazsa sayfa scroll eder.  
    > ℹ️ Sneat layout'ta scroll container `window`'dur; `content-wrapper`'ın overflow'u yoktur. Focus tabanlı tüm scroll sorunları `backbone-custom.css` içindeki merkezi MOD-0031 kuralı ile çözülür.
13. **Inline Filter Select2 Multi-Select Dynamic Summary (ZORUNLU):** Multi-select filtreleri tek satırda tutmak ve GoldenReference ile aynı label davranışını üretmek için aşağıdaki `syncMultiSelectSummary` mantığı uygulanmalıdır:
    - **Davranış:** Chip içinde görünür label her zaman `data-placeholder` değeridir (örn. `Category`). Seçim varsa label değişmez; sağ tarafta count badge görünür. Seçim yokken count ve clear gizlenir.
    - **Title:** `.select2-selection__rendered` `title` attribute'u seçili item metinlerini virgülle birleştirir; seçim yoksa placeholder kullanılır.
    - **DOM Sync (KRİTİK):** Multi-select container'ına (`.select2-selection--multiple`) bir `.select2-selection__arrow` kabı zorunlu olarak enjekte edilmelidir; aksi halde ok (chevron) hiza/yükseklik farkı oluşur.
    - **Clear Button:** Temizleme butonu (x) native `button` değil, `span` (role="button") olarak enjekte edilmelidir.
    - **Radius Fix (KRİTİK):** "Save View" butonu (`.dt-save-filter-btn`) görünürlüğü her değiştiğinde `window.DtDefaults.refreshButtonGroupRadii()` fonksiyonu çağrılmalıdır. Aksi takdirde buton grubunun köşeleri bozulur.
    - **Seçici:** Multi-select state'ini stile bağlamak için `.select2-container`'a değer varken `dt-inline-filter-multi--has-value` sınıfı eklenmelidir.
14. **Inline Filter Naming:** Semantik filter container class'ları kullanılır.
   - Company type filtresi: `.filter-company-type`
   - Status filtresi: `.filter-status`
   - `user_plan` ve `user_status` yeni sayfalarda kullanılmaz.
   - Geçiş/migration döneminde mevcut sayfalar için JS tarafında fallback desteklenebilir; yeni şablon üretiminde bu legacy isimler referans alınmaz.
14. **Shared CSS Placement:** Toolbar, inline filter, badge stacking ve Select2 dropdown stilleri tekrar kullanılabilir ise `backbone-custom.css` içinde tutulur; `@section Styles` yalnızca gerçekten modüle özgü istisnalar için kullanılır.
15. **CRUD Surface Boundary (ZORUNLU):** Index script'i Quick View offcanvas yönetebilir; ancak Create/Edit formunu offcanvas içinde çalıştıramaz. Create/Edit akışı route tabanlı ayrı sayfalara gider (`/{ModuleName}/Create`, `/{ModuleName}/Edit/{id}`).
16. **Add New Navigation (Compact ZORUNLU):** Compact modüllerde `DtDefaults.exportButtons()` Add New butonu route tabanlı Create sayfasına gitmelidir. `addNewAttr` için `{ href: '/{ModuleName}/Create' }` verilir ve `initComplete` içinde `.add-new` click handler'ı `event.preventDefault(); window.location.href = '/{ModuleName}/Create';` ile bağlanır. Inline `onclick` kullanılmaz.

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
    // Default for Platform/admin MVC modules. For direct-gateway-profile only, replace
    // with: const apiUrl = window.API?.{{ServiceKey}};
    const endpoint = '/{{AreaName}}/{{ModuleName}}/api';
    // ── Save View (personalizationClient) ─────────────────────────────────────
    // Bkz: §Save View — Tam İmplementasyon Şablonu
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: '{{AreaName}}', pageKey: '{{ModuleName}}' };
    // saveViewColumnIndexes: control(0) + checkbox(1) + action(last) HARİÇ tüm kolon indeksleri
    // Örn: 8 kolonlu tablo → [2, 3, 4, 5, 6]; 10 kolonlu → [2, 3, 4, 5, 6, 7, 8]
    const saveViewColumnIndexes = {{SaveViewColumnIndexes}}; // [2, 3, ..., N-2]
    const totalColumnCount = {{TotalColumnCount}};           // thead <th> sayısı
    let saveFilterArmed = false;
    const baseOrder = [[2, 'desc']];
    let appliedFilters = {{AppliedFiltersInit}};             // Örn: { status: '' } veya { companyType: '', status: '' }
    let defaultViewRecord = null;
    let defaultViewState = null;
    const isAuthHandledError = (e) => e?.authHandled === true || e?.code === 'auth-refresh-in-progress';
    // ─────────────────────────────────────────────────────────────────────────
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

    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

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
            host.classList.remove('px-6');
            host.classList.add('px-3'); // project standard (do not use mx-*)
            return;
        }

        // Fallback: place it before the table within the same container
        const dtContainer = dtTableEl.closest('.dt-container') || dtTableEl.closest('.dataTables_wrapper') || dtTableEl.parentElement;
        if (dtContainer) {
            dtContainer.insertAdjacentElement('beforeend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
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

    // initDataTable ASYNC'tir — loadDefaultView() await edilmesi zorunludur
    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView(); // personalizationClient'tan kaydedilmiş view yükle

        const extraButtons = {
            importBtn: {
                text: '<i class="icon-base bx bx-import icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary',
                attr: { title: L.Import, 'data-bs-toggle': 'tooltip' },
                action: function () { window.showToast?.(L.ComingSoon, 'warning'); }
            },
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: L.Filter,
                    'aria-controls': filterCollapseId,
                    'aria-expanded': 'false'
                }
            },
            // Save View butonu: başlangıçta d-none; appliedState ≠ savedView/baseline iken görünür
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        syncPendingTableUiState(tableApi);
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false); // Bu fonksiyon içeriden refreshButtonGroupRadii()'yi çağırmalıdır.
                        window.showToast?.(L.RecordSaved || L.SaveView || 'RecordSaved', 'success');
                    } catch (error) {
                        if (isAuthHandledError(error)) return;
                        console.error('[{{ModuleName}} SaveView] Failed to save default view', error);
                        window.showToast?.(L.ErrorOccurred, 'error');
                    }
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: {
                url: endpoint,
                type: 'GET',
                dataSrc: (json) => json.data || json,
                headers: getAuthHeaders()
            },
            stateSave: false, // data-dt-standard="v2": custom personalizationClient handles persistence
            colReorder: { columns: ':gt(1):not(:last-child)' }, // Varsayılan aktif — control(0), checkbox(1), action(son) sabit; sadece ≤2 veri kolonu olan özel sayfalarda devre dışı bırakılabilir (neden yorumu zorunlu)
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
                    className: 'cell-fit text-end',
                    render: (data, type, full) =>
                        `<div class="d-flex align-items-center justify-content-end">
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
                { href: '/{{ModuleName}}/Create' },
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
                document.querySelector('.add-new')?.addEventListener('click', (event) => {
                    event.preventDefault();
                    window.location.href = '/{{ModuleName}}/Create';
                });
                // Save View dirty-detection'ı init restore bittikten sonra arm et
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount(this.api()));
            }
        }));

        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount(dt));
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount(dt));
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('search.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    };

    const setupFilters = (api) => {
        // Select2 init — zorunlu 4 parametre + clampDropdown (bkz. Kural 12)
        if (window.jQuery && $.fn.select2) {
            const $dropdownParent = $(document.body);

            const clampDropdown = () => {
                requestAnimationFrame(() => {
                    const dropdown = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                    if (!dropdown) return;
                    const rect = dropdown.getBoundingClientRect();
                    const pad = 8;
                    let dx = 0, dy = 0;
                    if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
                    if (rect.left < pad) dx += pad - rect.left;
                    if (rect.bottom > window.innerHeight - pad) dy -= rect.bottom - (window.innerHeight - pad);
                    if (rect.top < pad) dy += pad - rect.top;
                    if (!dx && !dy) return;
                    const cs = window.getComputedStyle(dropdown);
                    const cssLeft = parseFloat(cs.left), cssTop = parseFloat(cs.top);
                    const baseLeft = Number.isFinite(cssLeft) ? cssLeft : rect.left + window.scrollX;
                    const baseTop  = Number.isFinite(cssTop)  ? cssTop  : rect.top  + window.scrollY;
                    if (dx) dropdown.style.left = `${baseLeft + dx}px`;
                    if (dy) dropdown.style.top  = `${baseTop  + dy}px`;
                    dropdown.style.transform = 'none';
                });
            };

            // {{DynamicSelect2Init}} — her filtre select için aşağıdaki bloğu kopyala:
            // $('#filter{{FieldName}}').select2({
            //     dropdownParent: $dropdownParent,
            //     dropdownCssClass: 'dt-inline-filter-dropdown',
            //     minimumResultsForSearch: Infinity,
            //     selectionCssClass: 'form-select form-select-sm',
            //     width: 'element'
            // });
            // $('#filter{{FieldName}}').on('select2:open', clampDropdown);
            // // Multi ise özetleyiciyi bağla (Kural 13 & 14):
            // if ($('#filter{{FieldName}}').prop('multiple')) {
            //     $('#filter{{FieldName}}').next('.select2-container').addClass('dt-inline-filter-multi');
            //     $('#filter{{FieldName}}').on('change.select2-summary', function() { syncMultiSelectSummary($(this)); });
            //     requestAnimationFrame(() => syncMultiSelectSummary($('#filter{{FieldName}}')));
            // }
        }

        // Kaydedilmiş view varsa uygula; yoksa temiz state'de kal
        const defaultView = defaultViewState;
        if (defaultView) {
            applySavedTableState(api, defaultView, { fallbackOrder: baseOrder });
        } else {
            // {{ResetAppliedFiltersToDefault}} — Örn: appliedFilters = { status: '' };
            syncFilterControls(appliedFilters);
        }

        window.DtDefaults.updateVisualState(api, getAppliedFilterCount(api));
        setSaveFilterVisible(false);

        const applyBtn = document.getElementById('btnFilterApply');
        const resetBtn = document.getElementById('btnFilterReset');

        if (applyBtn && !applyBtn.dataset.bound) {
            applyBtn.dataset.bound = '1';
            applyBtn.addEventListener('click', () => {
                // {{ReadFilterValues}} — Örn: const status = $('#filterStatus').val() || '';
                // {{SetAppliedFilters}} — Örn: appliedFilters = { status };
                applyFilterValues(api, appliedFilters);
                api.draw();
                window.DtDefaults.updateVisualState(api, getAppliedFilterCount(api));
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
                const el = document.getElementById(filterCollapseId);
                if (el) bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
            });
        }

        if (resetBtn && !resetBtn.dataset.bound) {
            resetBtn.dataset.bound = '1';
            resetBtn.addEventListener('click', (e) => {
                e.preventDefault(); // form reset'i engelle (savedView restore için)
                const def = defaultViewState;
                const hasSavedDefault = !!def;
                const isDirty = hasSavedDefault ? isDirtyComparedToDefault(api) : false;
                if (hasSavedDefault && isDirty) {
                    applySavedTableState(api, def, { fallbackOrder: baseOrder, resetColumnOrder: !def?.columnOrder });
                } else {
                    applySavedTableState(api, { {{FilterKeys}}: '', search: '' }, {
                        fallbackOrder: baseOrder,
                        clearSearch: true,
                        resetColumns: true,
                        resetColumnOrder: true
                    });
                }
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            });
        }
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

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>\"']/g, (char) => ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
    }[char]));

    const openDeleteDialog = ({ confirmMessage, entityName, confirmButtonText }) => {
        const html = entityName
            ? `<div class="mb-2">${L.ConfirmAction || ''}</div><div class="badge bg-label-primary fs-6 mt-1 py-2 px-3">${escapeHtml(entityName)}</div>`
            : `<div class="mb-2">${confirmMessage || ''}</div>`;

        return Swal.fire({
            title: L.AreYouSure,
            html: html,
            iconHtml: '<div class="swal-icon-circle"><i class="bx bx-trash"></i></div>',
            showCancelButton: true,
            confirmButtonText: confirmButtonText || L.BulkDelete,
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
        }).then(result => result.isConfirmed);
    };

    const reloadTableAndToastSuccess = (message) => {
        clearSelection();
        dt.ajax.reload(() => {
            window.showToast?.(message, 'success');
        }, false);
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
                const data = dt.row(tr).data();

                openDeleteDialog({
                    entityName: data.name || data.title,
                    confirmButtonText: L.DeleteConfirmationYesBtn || L.BulkDelete
                }).then(isConfirmed => {
                    if (!isConfirmed) return;

                    fetch(`${endpoint}/${data.id}`, {
                        method: 'DELETE',
                        headers: getAuthHeaders()
                    }).then(res => {
                        if (res.ok) {
                            reloadTableAndToastSuccess('RecordDeleted');
                        } else {
                            window.showToast?.('ErrorOccurred', 'error');
                        }
                    }).catch(() => window.showToast?.('ErrorOccurred', 'error'));
                });
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
            openDeleteDialog({
                confirmMessage: msg,
                confirmButtonText: L.BulkDelete
            }).then(isConfirmed => {
                if (!isConfirmed) return;
                fetch(`${endpoint}/bulk`, {
                    method: 'DELETE',
                    headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
                    body: JSON.stringify({ ids })
                }).then(res => {
                    if (res.ok) return res.json();
                    throw new Error('Bulk delete failed');
                }).then(data => {
                    reloadTableAndToastSuccess(
                        (L.BulkDeleteSuccess || '').replace('{0}', data.deletedCount)
                    );
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

## 💾 Save View — Tam İmplementasyon Şablonu

> Bu bölüm, her yeni DataTable modülü için **kopyala-yapıştır** yapısındadır.
> `{{...}}` değerlerini modüle göre doldur. Products = referans implementasyon (`frontend/Diten.Web/wwwroot/assets/js/MDM/Products/`).

### Değişken Değerleri (modüle göre doldur)

| Placeholder | Nasıl Hesaplanır | Örnek (SampleModule, 8 kolon) |
|-------------|-----------------|---------------------------|
| `{{SaveViewColumnIndexes}}` | `[2 .. toplam-2]` — control(0), checkbox(1), action(son) HARİÇ | `[2, 3, 4, 5, 6]` |
| `{{TotalColumnCount}}` | `thead <th>` sayısı | `8` |
| `{{AppliedFiltersInit}}` | her filtre key'i için `''` | `{ status: '' }` |
| `{{AreaName}}` / `{{ModuleName}}` | personalizationContext | `'MDM'` / `'SampleModule'` |

### Helper Fonksiyonlar (IIFE içine, initDataTable öncesine ekle)

```javascript
// ─── Save View Helpers ─────────────────────────────────────────────────────

// ─── Save View Helpers ─────────────────────────────────────────────────────

const normalizeSavedString = (value) => typeof value === 'string' ? value.trim() : '';

const getSavedViewFlag = (savedView, camelKey, pascalKey) => {
    if (!savedView || typeof savedView !== 'object') return undefined;
    if (typeof savedView[camelKey] !== 'undefined') return savedView[camelKey];
    if (typeof savedView[pascalKey] !== 'undefined') return savedView[pascalKey];
    return undefined;
};

const getSavedViewDefinition = (savedView) => {
    if (!savedView || typeof savedView !== 'object') return {};
    const raw = savedView.viewDefinition ?? savedView.ViewDefinition ??
                savedView.viewDefinitionJson ?? savedView.ViewDefinitionJson ?? {};
    if (raw && typeof raw === 'object') return raw;
    if (typeof raw === 'string') {
        try { const p = JSON.parse(raw); return (p && typeof p === 'object') ? p : {}; } catch (e) { return {}; }
    }
    return {};
};

const getSavedViewId   = (sv) => normalizeSavedString(getSavedViewFlag(sv, 'id', 'Id') || getSavedViewFlag(sv, '_id', '_id'));
const getSavedViewName = (sv) => normalizeSavedString(getSavedViewFlag(sv, 'viewName', 'ViewName'));
const isSavedViewDefault = (sv) => getSavedViewFlag(sv, 'isDefault', 'IsDefault') === true;

const createDefaultColumnVisibility = () =>
    saveViewColumnIndexes.reduce((acc, i) => { acc[i] = true; return acc; }, {});

const normalizeColumnVisibility = (colVis) => {
    if (!colVis) return null;
    const n = {};
    if (Array.isArray(colVis)) {
        saveViewColumnIndexes.forEach((idx, pos) => {
            if (typeof colVis[idx] === 'boolean') { n[idx] = colVis[idx]; return; }
            if (typeof colVis[pos] === 'boolean') n[idx] = colVis[pos];
        });
    } else if (typeof colVis === 'object') {
        saveViewColumnIndexes.forEach((idx) => { if (typeof colVis[idx] === 'boolean') n[idx] = colVis[idx]; });
    }
    return Object.keys(n).length ? n : null;
};

const areColumnVisibilitiesEqual = (left, right) => {
    const l = normalizeColumnVisibility(left), r = normalizeColumnVisibility(right);
    if (!l && !r) return true;
    if (!l || !r) return false;
    return saveViewColumnIndexes.every((i) => {
        const lv = typeof l[i] === 'boolean' ? l[i] : true;
        const rv = typeof r[i] === 'boolean' ? r[i] : true;
        return lv === rv;
    });
};

const captureColumnVisibility = (api) => {
    const cv = {};
    saveViewColumnIndexes.forEach((i) => { try { cv[i] = !!api.column(i).visible(); } catch (e) {} });
    return Object.keys(cv).length ? cv : null;
};

const applyColumnVisibility = (api, colVis) => {
    const n = normalizeColumnVisibility(colVis);
    if (!n) return;
    saveViewColumnIndexes.forEach((i) => {
        if (typeof n[i] === 'boolean') try { api.column(i).visible(n[i], false); } catch (e) {}
    });
};

const normalizeColumnOrder = (order) => {
    if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
    const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
    if (n.length !== totalColumnCount || new Set(n).size !== totalColumnCount) return null;
    return n;
};

const areColumnOrdersEqual = (left, right) => {
    const l = normalizeColumnOrder(left)  || Array.from({ length: totalColumnCount }, (_, i) => i);
    const r = normalizeColumnOrder(right) || Array.from({ length: totalColumnCount }, (_, i) => i);
    return l.every((v, i) => v === r[i]);
};

const captureColumnOrder = (api) => {
    try { return normalizeColumnOrder(api?.colReorder?.order?.()); } catch (e) { return null; }
};

const applyColumnOrder = (api, order) => {
    const n = normalizeColumnOrder(order);
    if (!n || typeof api?.colReorder?.order !== 'function') return;
    try { api.colReorder.order(n, true); } catch (e) {}
};

const getSearchInputValue = (api) => {
    try { const i = api.table().container()?.querySelector('.dt-search input'); return typeof i?.value === 'string' ? i.value : ''; }
    catch (e) { return ''; }
};

const syncSearchInput = (api, val) => {
    try { const i = api.table().container()?.querySelector('.dt-search input'); if (i) i.value = val || ''; }
    catch (e) {}
};

const mapSavedViewToState = (savedView) => {
    const d = getSavedViewDefinition(savedView);
    return {
        // {{FilterKeyMappings}} — Örn: status: normalizeSavedString(d.status),
        search: normalizeSavedString(d.search),
        colVis: normalizeColumnVisibility(d.colVis),
        columnOrder: normalizeColumnOrder(d.columnOrder),
        order: Array.isArray(d.order) ? d.order : null
    };
};

const getCurrentView = (api) => {
    // {{FilterKeyCapture}} — Örn: const status = appliedFilters?.status || '';
    const search = getSearchInputValue(api);
    return {
        // status,
        search,
        colVis: captureColumnVisibility(api),
        columnOrder: captureColumnOrder(api),
        order: (typeof api.order === 'function') ? api.order() : null
    };
};

const applySavedTableState = (api, state, options) => {
    if (!api || !state) return;
    const fallbackOrder = Array.isArray(options?.fallbackOrder) ? options.fallbackOrder : baseOrder;

    // 1. Filters
    // {{FilterKeySync}} — Örn: appliedFilters = { status: state.status || '' };
    syncFilterControls(appliedFilters);
    applyFilterValues(api, appliedFilters);

    // 2. Search
    if (typeof state.search === 'string') {
        api.search(state.search);
        syncSearchInput(api, state.search);
    } else if (options?.clearSearch) {
        api.search('');
        syncSearchInput(api, '');
    }

    // 3. Layout (Order & Vis)
    applyColumnOrder(api, state.columnOrder || (options?.resetColumnOrder ? Array.from({ length: totalColumnCount }, (_, i) => i) : null));
    applyColumnVisibility(api, state.colVis || (options?.resetColumns ? createDefaultColumnVisibility() : null));

    // 4. Sorting
    if (Array.isArray(state.order)) api.order(state.order); else if (fallbackOrder) api.order(fallbackOrder);

    api.draw(false);
};

const serializeView = (view) => JSON.stringify({
    // filters: { anyKey: view?.anyKey },
    search: view?.search || '',
    colVis: normalizeColumnVisibility(view?.colVis) || createDefaultColumnVisibility(),
    columnOrder: normalizeColumnOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
    order: Array.isArray(view?.order) ? view.order : baseOrder
});

const isDirtyComparedToDefault = (api) => {
    const baseline = defaultViewState || {
        // filters: {}, 
        search: '',
        colVis: createDefaultColumnVisibility(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    };
    return serializeView(getCurrentView(api)) !== serializeView(baseline);
};

const setSaveFilterVisible = (visible) => {
    const btn = document.querySelector('.dt-save-filter-btn');
    if (!btn) return;
    btn.classList.toggle('d-none', !visible);
    window.DtDefaults?.refreshButtonGroupRadii?.(); // ZORUNLU: Radius fix
};

// ─────────────────────────────────────────────────────────────────────────────
```

### Somut Örnek (tek status filtreli modül — SampleModule)

```javascript
// Değişkenler:
const saveViewColumnIndexes = [2, 3, 4, 5, 6]; // name, iso2, iso3, phone, isActive
const totalColumnCount = 8;
let appliedFilters = { status: '' };

// applyFilterValues:
const applyFilterValues = (api, values) => {
    api.column('isActive:name').search(values?.status || '');
};

// syncFilterControls:
const syncFilterControls = (values) => {
    $('#filterStatus').val(normalizeSavedString(values?.status)).trigger('change');
};

// getAppliedFilterCount:
const getAppliedFilterCount = (api) => {
    try { return [api.column('isActive:name').search()].filter(v => v?.trim()).length; }
    catch (e) { return [appliedFilters?.status].filter(Boolean).length; }
};

// mapSavedViewToState — status key ekle:
//   status: normalizeSavedString(d.status),

// getCurrentView — status capture:
//   const status = appliedFilters?.status || '';
//   return { status, search, colVis, ... };

// applySavedTableState — appliedFilters update:
//   appliedFilters = { status: state.status || '' };

// isDirtyComparedToDefault — filter compare:
//   if (!def) return [cur.status].filter(Boolean).length > 0 || !!cur.search || ...
//   return (String(cur.status || '') !== String(ref.status || '')) || ...
```

### İki filtreli modül farkı (Products — productType + category)

```javascript
const saveViewColumnIndexes = [2, 3, 4, 5, 6, 7, 8];
const totalColumnCount = 10;
let appliedFilters = { companyType: '', status: '' };
// ... applyFilterValues her iki kolonu da search eder
// ... isDirtyComparedToDefault her iki key'i karşılaştırır
```

---

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
| `row.remove().draw(); showToast('RecordDeleted', 'success')` | `dt.ajax.reload(() => showToast('RecordDeleted', 'success'), false)` |
| `toastr.success(...)` / `toastr.error(...)` | `window.showToast('KeyOrMessage', 'success'\|'error'\|'warning'\|'info')` |
| `url: window.ApiBaseUrl + '/mdm/api/v1/...'` | Platform/admin: `url: endpoint` where `endpoint = '/{{AreaName}}/{{ModuleName}}/api'`; direct-gateway only: `url: apiUrl + '/api/{{ModuleNameLower}}'` |
| `document.cookie` / `access_token` / `Authorization: Bearer ...` in DataTable JS | HttpOnly token is read only by MVC proxy; JS uses `getAuthHeaders()` for safe UI headers only |
| `$.ajax(...)` CRUD | `fetch(...)` ile native async |

---

## 🥇 ALTIN ÖRNEK: "SampleModule" (JS Standartı)

Ajanlar, fiziksel bir dosya yerine aşağıdaki kodu "Kusursuz Modüler JavaScript" olarak referans almalıdır.

### `wwwroot/assets/js/MDM/SampleModule/index.js`
```javascript
'use strict';

const SampleModuleList = (function () {
    let dt;
    const dtTableEl = document.querySelector('.datatables-sample-module');
    const apiUrl = window.API?.mdm; // direct-gateway-profile only; Platform/admin modules use endpoint = '/Area/Module/api'
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MDM', pageKey: 'SampleModule' };
    
    // 8 kolonlu tablo varsayımı: [2, 3, 4, 5, 6] veri kolonlarıdır
    const saveViewColumnIndexes = [2, 3, 4, 5, 6]; 
    const totalColumnCount = 8;
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { companyType: '', status: '' };
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let L = window.L10n || {};

    const syncL10n = () => { L = window.L10n || {}; };

    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const normalizeString = (v) => typeof v === 'string' ? v.trim() : '';
    
    // --- Save View Mechanics ---
    const captureView = (api) => ({
        companyType: appliedFilters.companyType,
        status: appliedFilters.status,
        search: normalizeString(api.table().container()?.querySelector('.dt-search input')?.value || api.search()),
        colVis: saveViewColumnIndexes.reduce((acc, i) => { acc[i] = !!api.column(i).visible(); return acc; }, {}),
        columnOrder: api?.colReorder?.order?.(),
        order: api.order()
    });

    const serialize = (v) => JSON.stringify(v);

    const isDirty = (api) => {
        const baseline = defaultViewState || { 
            companyType: '', status: '', search: '', 
            colVis: saveViewColumnIndexes.reduce((a, i) => { a[i]=true; return a; }, {}),
            columnOrder: Array.from({length: totalColumnCount}, (_, i) => i),
            order: baseOrder 
        };
        return serialize(captureView(api)) !== serialize(baseline);
    };

    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return;
        const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
        defaultViewRecord = views.find(v => v.isDefault) || views[0] || null;
        if (defaultViewRecord) {
            const def = defaultViewRecord.viewDefinition;
            defaultViewState = typeof def === 'string' ? JSON.parse(def) : def;
        }
    };

    const setSaveFilterVisible = (v) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (btn) btn.classList.toggle('d-none', !v);
    };

    // --- DataTable Init ---
    const initDataTable = async () => {
        if (!dtTableEl) return;
        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            filterBtn: {
                text: '<i class="bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                attr: { title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' }
            },
            saveFilterBtn: {
                text: '<i class="bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">'+L.SaveView+'</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                action: async (e, api) => {
                    const view = captureView(api);
                    defaultViewRecord = defaultViewRecord 
                        ? await personalizationClient.updateView(defaultViewRecord.id, { ...defaultViewRecord, viewDefinition: view })
                        : await personalizationClient.saveView({ ...personalizationContext, viewName: 'Default', viewDefinition: view, isDefault: true });
                    defaultViewState = view;
                    setSaveFilterVisible(false);
                    window.showToast?.(L.RecordSaved, 'success');
                }
            }
        };

        dt = new DataTable(dtTableEl, window.DtDefaults.create({
            ajax: { url: apiUrl + '/api/sample-module', headers: getAuthHeaders() },
            colReorder: { columns: ':gt(1):not(:last-child)' },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'id', name: 'checkbox' },
                { data: 'taxId', name: 'taxId' },
                { data: 'commercialTitle', name: 'commercialTitle' },
                { data: 'city', name: 'city' },
                { data: 'isActive', name: 'isActive' },
                { data: 'id', name: 'action' }
            ],
            buttons: window.DtDefaults.exportButtons(L.AddNewSampleModule, { href: '/SampleModule/Create' }, extraButtons),
            initComplete: function() {
                if (defaultViewState) {
                    const api = this.api();
                    appliedFilters = { companyType: defaultViewState.companyType || '', status: defaultViewState.status || '' };
                    api.search(defaultViewState.search || '');
                    if (defaultViewState.order) api.order(defaultViewState.order);
                    api.draw();
                }
                setTimeout(() => { saveFilterArmed = true; }, 100);
            }
        }));

        dt.on('search.dt order.dt column-visibility.dt column-reorder.dt', () => {
            if (saveFilterArmed) setSaveFilterVisible(isDirty(dt));
        });
    };

    return { init: () => initDataTable() };
})();

document.addEventListener('DOMContentLoaded', () => SampleModuleList.init());
```
