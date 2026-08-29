(function () {
  'use strict';

  const tableElement = document.getElementById('dt-process-modeling');
  if (!tableElement) return;

  const L = window.ProcessModelingL10n || {};
  const permissionNode = document.getElementById('processModelingPermissionSet');
  const canRead = permissionNode?.dataset.read === 'true';
  const canCreate = permissionNode?.dataset.create === 'true';
  const canUpdate = permissionNode?.dataset.update === 'true';
  const gatewayReady = permissionNode?.dataset.gatewayReady === 'true';
  const defaultOrder = [[2, 'asc']];
  const defaultColumnOrder = [0, 1, 2, 3, 4, 5, 6];
  let appliedFilters = { lifecycle: [] };
  let savedView = null;
  let saveFilterArmed = false;
  let dataTable;

  const normalizeArray = value => Array.isArray(value)
    ? value.map(item => String(item).trim()).filter(Boolean).sort()
    : [];

  const normalizeView = view => ({
    filters: { lifecycle: normalizeArray(view?.filters?.lifecycle) },
    search: String(view?.search || '').trim(),
    colVis: Array.isArray(view?.colVis) ? view.colVis.map(Boolean) : [],
    columnOrder: Array.isArray(view?.columnOrder) ? view.columnOrder.map(Number) : defaultColumnOrder.slice(),
    order: Array.isArray(view?.order) ? view.order.map(entry => [Number(entry[0]), String(entry[1])]) : defaultOrder
  });

  const factoryView = () => normalizeView({
    filters: { lifecycle: [] },
    search: '',
    colVis: defaultColumnOrder.map(() => true),
    columnOrder: defaultColumnOrder,
    order: defaultOrder
  });

  const moduleKey = 'ManagementGovernance';
  const pageKey = 'ProcessModeling';

  async function loadDefaultView() {
    if (!window.personalizationClient?.loadDefaultView) return null;
    try {
      const value = await window.personalizationClient.loadDefaultView(moduleKey, pageKey);
      return value ? normalizeView(value) : null;
    } catch (error) {
      if (!window.personalizationClient?.isAuthHandledError?.(error)) console.warn(error);
      return null;
    }
  }

  async function saveDefaultView(view) {
    if (!window.personalizationClient?.saveView) return;
    const payload = { ...normalizeView(view), viewName: L.SaveView };
    await window.personalizationClient.saveView(moduleKey, pageKey, payload);
    savedView = normalizeView(payload);
    setSaveFilterVisible(false);
  }

  function captureColumnOrder(api) {
    return api.colReorder?.order?.() || defaultColumnOrder.slice();
  }

  function getCurrentView(api) {
    return normalizeView({
      filters: appliedFilters,
      search: api.search(),
      colVis: api.columns().indexes().toArray().map(index => api.column(index).visible()),
      columnOrder: captureColumnOrder(api),
      order: api.order()
    });
  }

  function applySavedTableState(api, view) {
    const normalized = normalizeView(view || factoryView());
    appliedFilters = normalized.filters;
    $('#filterLifecycle').val(appliedFilters.lifecycle).trigger('change.select2');
    api.search(normalized.search);
    normalized.colVis.forEach((visible, index) => api.column(index).visible(visible, false));
    api.colReorder?.order?.(normalized.columnOrder, true);
    api.order(normalized.order);
  }

  function isDirtyComparedToDefault(api) {
    const baseline = savedView || factoryView();
    return JSON.stringify(getCurrentView(api)) !== JSON.stringify(normalizeView(baseline));
  }

  function setSaveFilterVisible(visible) {
    document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
  }

  function updateDirtyState() {
    if (saveFilterArmed && dataTable) setSaveFilterVisible(isDirtyComparedToDefault(dataTable));
  }

  function statusMessage(xhr) {
    const code = xhr?.status;
    if (code === 401) return L.Error401;
    if (code === 403) return L.Error403;
    if (code === 404) return L.Error404;
    if (code === 409) return L.Error409;
    return L.Error503;
  }

  function renderActions(_data, _type, row) {
    if (!canRead || !row?.id) return '';
    return window.DitenDataTable?.renderActions?.([
      {
        text: L.OpenEditor,
        icon: 'bx bx-git-branch',
        attrs: {
          'data-row-action': 'open-editor',
          'data-id': encodeURIComponent(row.id),
          'aria-label': L.OpenEditor,
          title: L.OpenEditor
        }
      }
    ]) || '';
  }

  async function initDataTable() {
    savedView = await loadDefaultView();
    const extraButtons = {
      filterBtn: {
        text: '<i class="bx bx-filter-alt"></i>',
        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
        attr: { 'aria-label': L.Filter, title: L.Filter, 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false' },
        action: () => bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).toggle()
      },
      saveFilterBtn: {
        text: `<i class="bx bx-save"></i><span class="d-none d-lg-inline ms-1">${L.SaveView}</span>`,
        className: 'btn btn-label-secondary dt-save-filter-btn d-none',
        attr: { 'aria-label': L.SaveView, title: L.SaveView },
        action: () => saveDefaultView(getCurrentView(dataTable))
      }
    };

    const options = window.DtDefaults.create({
      serverSide: true,
      processing: true,
      stateSave: false,
      searchDelay: 350,
      order: defaultOrder,
      colReorder: { columns: ':gt(0):not(:last-child)' },
      ajax: {
        url: '/management-governance/process-modeling/api/models',
        data: request => {
          request.lifecycle = appliedFilters.lifecycle;
        },
        dataSrc: json => json?.data?.items || json?.data || [],
        error: xhr => {
          window.showToast?.(statusMessage(xhr), 'warning');
          document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
      },
      columns: [
        { data: null, defaultContent: '', orderable: false, searchable: false, className: 'control' },
        { data: 'modelCode', defaultContent: '' },
        { data: 'name', defaultContent: '' },
        { data: 'currentVersion', defaultContent: '' },
        { data: 'lifecycleState', defaultContent: '' },
        { data: 'updatedAtUtc', defaultContent: '' },
        { data: null, orderable: false, searchable: false, className: 'text-end pe-3', render: renderActions }
      ],
      language: { emptyTable: gatewayReady ? L.Empty : L.GatewayNotReady },
      buttons: window.DtDefaults.exportButtons(
        L.AddNew,
        { 'aria-label': L.AddNew, title: L.AddNew },
        extraButtons,
        { exportColumns: [1, 2, 3, 4, 5], colvisColumns: [1, 2, 3, 4, 5] }
      ),
      initComplete: function () {
        const api = this.api();
        const host = document.getElementById('inlineFilterHost');
        const toolbar = document.querySelector('.dt-filter-btn')?.closest('.dt-layout-row');
        if (host && toolbar) {
          toolbar.insertAdjacentElement('afterend', host);
          host.classList.add('px-3');
        }
        const filterButton = document.querySelector('.dt-filter-btn');
        const filterCollapse = document.getElementById('inlineFilterCollapse');
        filterCollapse?.addEventListener('shown.bs.collapse', () => filterButton?.setAttribute('aria-expanded', 'true'));
        filterCollapse?.addEventListener('hidden.bs.collapse', () => filterButton?.setAttribute('aria-expanded', 'false'));
        applySavedTableState(api, savedView || factoryView());
        api.draw(false);
        document.getElementById('skeleton-loader')?.classList.add('d-none');
        window.DitenDataTable?.bindActionDispatcher?.({
          tableEl: tableElement,
          onRowAction: {
            'open-editor': trigger => {
              const id = trigger.getAttribute('data-id');
              if (id) window.location.assign(`/management-governance/process-modeling/models/${id}`);
            }
          }
        });
        document.querySelector('.add-new')?.addEventListener('click', event => {
          event.preventDefault();
          if (!canCreate || !gatewayReady) {
            window.showToast?.(L.GatewayNotReady, 'warning');
            return;
          }
          bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        });
        setTimeout(() => { saveFilterArmed = true; }, 0);
      },
      drawCallback: function () {
        window.DtDefaults.updateVisualState(this.api(), appliedFilters.lifecycle.length);
        updateDirtyState();
      }
    });

    dataTable = new DataTable(tableElement, options);
    dataTable.on('search.dt order.dt column-visibility.dt column-reorder.dt columns-reordered.dt', updateDirtyState);
  }

  function initFilters() {
    const select = $('#filterLifecycle');
    select.select2({
      dropdownParent: $(document.body),
      dropdownCssClass: 'dt-inline-filter-dropdown',
      selectionCssClass: 'form-select form-select-sm',
      minimumResultsForSearch: Infinity,
      width: 'element'
    });

    document.getElementById('btnFilterApply')?.addEventListener('click', () => {
      appliedFilters = { lifecycle: normalizeArray(select.val()) };
      dataTable?.ajax.reload();
      updateDirtyState();
    });

    document.getElementById('btnFilterReset')?.addEventListener('click', () => {
      const baseline = factoryView();
      savedView = null;
      applySavedTableState(dataTable, baseline);
      dataTable.ajax.reload();
      setSaveFilterVisible(false);
    });
  }

  initFilters();
  initDataTable();
})();
