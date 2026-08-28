'use strict';
(() => {
  const endpoint = '/Platform/WorkingCalendarImports/api';
  const base = endpoint;
  const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};
  const unwrap = x => x?.data ?? x?.Data ?? x;
  const get = async url => unwrap(await (await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() })).json());
  const apiError = body =>
    (Array.isArray(body?.errors) && body.errors[0])
    || body?.message
    || body?.detail
    || window.L10n?.ErrorOccurred
    || 'Import failed';
  const showFormError = message => {
    const alert = document.getElementById('formWorkingCalendarImportAlert');
    if (!alert) return;
    alert.textContent = message || '';
    alert.classList.toggle('d-none', !message);
  };
  // Select2 snapshots the <select> it wraps, so every fetch-populate must re-wrap it.
  // Pass a selector to refresh a single control; omit it to refresh the whole offcanvas.
  const initOffcanvasSelect2 = (selector) => {
    if (!window.jQuery || !$.fn.select2) return;
    const $offcanvas = $('#offcanvasCreateEdit');
    $offcanvas.find(selector || '.select2-offcanvas').each(function () {
      const $el = $(this);
      if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
      $el.select2({ dropdownParent: $offcanvas, width: '100%' });
    });
  };
  let table;
  const L = () => window.L10n || {};
  const filterCollapseId = 'inlineFilterCollapse';
  const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const statusBadgeClass = status => ({
    'in-review': 'bg-label-warning',
    applied: 'bg-label-success',
    discarded: 'bg-label-secondary',
    failed: 'bg-label-danger'
  }[status] || 'bg-label-primary');
  const formatDate = value => {
    if (!value) return '';
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? String(value).slice(0, 10) : d.toLocaleDateString(window.CurrentLanguage || undefined);
  };
  const getAppliedFilterCount = () => {
    let count = ($('#filterStatus').val() || []).length ? 1 : 0;
    ['filterCountry', 'filterYear', 'filterTargetCalendarId', 'filterTriggerSource']
      .forEach(id => { if (document.getElementById(id)?.value) count += 1; });
    return count;
  };
  // The inline filter host is authored inside the card; the toolbar Filter button expects it
  // directly under the toolbar row, so relocate it once DataTables has rendered the toolbar.
  const mountInlineFilter = () => {
    const host = document.getElementById('inlineFilterHost');
    const filterBtn = document.querySelector('.dt-filter-btn');
    const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
    if (host && toolbarRow) {
      toolbarRow.insertAdjacentElement('afterend', host);
      host.classList.remove('px-6');
      host.classList.add('px-3');
    }
  };
  const toggleInlineFilter = () => {
    const collapseEl = document.getElementById(filterCollapseId);
    if (!collapseEl) return;
    bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
  };
  const bindInlineFilterA11y = () => {
    const btn = document.querySelector('.dt-filter-btn');
    const collapseEl = document.getElementById(filterCollapseId);
    if (!btn || !collapseEl || btn.dataset.bound) return;
    btn.dataset.bound = '1';
    collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
    collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
  };
  const load = async () => {
    const p = new URLSearchParams();
    const statuses = $('#filterStatus').val() || [];
    if (statuses.length === 1) p.set('status', statuses[0]);
    if (document.getElementById('filterCountry').value) p.set('countryCode', document.getElementById('filterCountry').value);
    if (document.getElementById('filterYear').value) p.set('calendarYear', document.getElementById('filterYear').value);
    if (document.getElementById('filterTargetCalendarId').value) p.set('targetCalendarId', document.getElementById('filterTargetCalendarId').value);
    if (document.getElementById('filterTriggerSource').value) p.set('triggerSource', document.getElementById('filterTriggerSource').value);
    const rows = await get(`${base}?${p}`);
    table?.clear().rows.add(rows || []).draw();
    const targetFilter = document.getElementById('filterTargetCalendarId');
    if (targetFilter && targetFilter.options.length === 0) {
      targetFilter.add(new Option('', ''));
      Array.from(new Map((rows || []).map(x => [x.targetCalendarId, x.targetCalendarCodeSnapshot])).entries())
        .forEach(([value, label]) => targetFilter.add(new Option(label, value)));
    }
    document.getElementById('skeleton-loader')?.classList.add('d-none');
  };
  document.addEventListener('DOMContentLoaded', async () => {
    // Keep offcanvas controls usable even when an upstream lookup is temporarily unavailable.
    initOffcanvasSelect2();
    const contract = await get(`${base}/contract`);
    const providerStatus = await get(`${base}/provider-status`);
    const importEnabled = providerStatus.enabled === true;
    (contract.statuses || []).forEach(x => document.getElementById('filterStatus').add(new Option(x, x)));
    document.getElementById('filterTriggerSource').add(new Option('', ''));
    (contract.triggerSources || []).forEach(x => document.getElementById('filterTriggerSource').add(new Option(x, x)));
    // Scoped to the filter bar on purpose: Select2 renders its own container as
    // <span class="select2 select2-container">, so a bare $('.select2') would also match the
    // containers already built for the offcanvas selects and wrap them a second time.
    $('#inlineFilterHost .select2').select2({ width: '100%' });
    // The shared defaults own the golden look: toolbar layout, responsive column collapse,
    // Sneat class fixes and i18n. Building a bare `new DataTable(...)` skips all of it.
    const dataColumns = [2, 3, 4, 5, 6, 7, 8, 9, 10];
    table = new DataTable('.datatables-workingcalendarimports', window.DtDefaults.create({
      data: [],
      stateSave: false,
      order: [[2, 'desc']],
      columns: [
        { data: 'id', name: 'control' },
        { data: 'id', name: 'checkbox' },
        { data: 'batchCode', name: 'batchCode' },
        { data: 'countryCode', name: 'countryCode' },
        { data: 'calendarYear', name: 'calendarYear' },
        { data: 'targetCalendarCodeSnapshot', name: 'targetCalendarCodeSnapshot' },
        { data: 'importStatus', name: 'importStatus' },
        { data: 'triggerSource', name: 'triggerSource' },
        { data: null, name: 'candidates' },
        { data: 'requestedBy', name: 'requestedBy' },
        { data: 'requestedAt', name: 'requestedAt' },
        { data: 'id', name: 'action' }
      ],
      columnDefs: [
        { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
        { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: data => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${esc(data)}">` },
        { targets: 2, responsivePriority: 1, render: data => `<span class="fw-medium text-heading">${esc(data)}</span>` },
        { targets: 6, render: (data, type) => type === 'display' ? `<span class="badge ${statusBadgeClass(data)}">${esc(data)}</span>` : data },
        { targets: 7, render: (data, type) => type === 'display' ? `<span class="badge bg-label-secondary">${esc(data)}</span>` : data },
        { targets: 8, orderable: false, render: (data, type, full) => `${full.approvedCount}/${full.rejectedCount}/${full.undecidedCount}` },
        { targets: 10, render: (data, type) => type === 'display' ? formatDate(data) : data },
        {
          targets: -1,
          title: L().Actions,
          searchable: false,
          orderable: false,
          className: 'cell-fit text-end all',
          render: id => window.DitenDataTable.renderActions([
            { className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-id': id, title: L().QuickView } },
            { className: 'js-review-item', icon: 'bx bx-check-square', text: L().Review, attrs: { 'data-id': id } }
          ])
        }
      ],
      buttons: window.DtDefaults.exportButtons(
        // Falls back to the server-rendered offcanvas title so the primary action keeps its
        // localized label even if the l10n payload has not picked up the key yet.
        L().StartImport || document.getElementById('offcanvasCreateEditLabel')?.textContent?.trim(),
        { 'data-import-trigger': '1' },
        {
          filterBtn: {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L().Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
          }
        },
        { exportColumns: dataColumns, colvisColumns: dataColumns }
      ),
      initComplete: function () {
        mountInlineFilter();
        bindInlineFilterA11y();
        // Bind the toolbar primary action after DataTables renders it — must NOT ride on
        // addNewAttr onclick, which DataTables would evaluate once at init.
        const addNew = document.querySelector('.add-new');
        if (!addNew) return;
        addNew.disabled = !importEnabled;
        addNew.classList.toggle('disabled', !importEnabled);
        addNew.setAttribute('aria-disabled', String(!importEnabled));
        addNew.addEventListener('click', e => {
          e.preventDefault();
          if (!importEnabled) return;
          showFormError('');
          bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
        });
      },
      drawCallback: function () {
        window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
      }
    }));
    const loadTargets = async () => {
      const cc = document.getElementById('CountryCode').value, year = document.getElementById('CalendarYear').value;
      const el = document.getElementById('TargetCalendarId');
      if (!cc || !year) {
        el.innerHTML = '';
        initOffcanvasSelect2('#TargetCalendarId');
        return;
      }
      const rows = await get(`${base}/calendars?countryCode=${encodeURIComponent(cc)}&calendarYear=${year}&includeArchived=false`);
      el.innerHTML = '';
      // /calendars proxies the working-calendars LIST endpoint, so it answers {totalCount, items} — not a bare array.
      (rows?.items ?? rows?.Items ?? [])
        .filter(x => x.scopeType === 'country' && (x.calendarStatus === 'active' || x.calendarStatus === 'draft'))
        .forEach(x => el.add(new Option(`${x.calendarCode} — ${x.calendarName}`, x.id)));
      initOffcanvasSelect2('#TargetCalendarId');
    };
    const countries = await get(`${base}/countries`);
    (countries || []).forEach(x => document.getElementById('CountryCode').add(new Option(x.name || x.label || x.code, x.code || x.value)));
    initOffcanvasSelect2('#CountryCode');
    document.getElementById('CalendarYear').value = new Date().getUTCFullYear();
    // Bound through jQuery, not addEventListener: Select2 announces a selection with jQuery's
    // .trigger('change'), which never dispatches a real DOM event, so a native listener would
    // simply never run when the user picks a country.
    $('#CountryCode').on('change', loadTargets);
    $('#CalendarYear').on('change', loadTargets);
    await loadTargets();
    // Re-wrap now that both selects are populated; the early call above wrapped them while empty.
    initOffcanvasSelect2();
    document.getElementById('btnFilterApply').addEventListener('click', load);
    // Delegated on document, not on the table: when responsive collapses a row the action
    // buttons are re-rendered inside the details modal, which lives outside <table>.
    document.addEventListener('click', e => {
      const link = e.target.closest('.js-review-item'); if (!link) return;
      e.preventDefault();
      window.location.href = `/Platform/WorkingCalendarImports/Review/${link.dataset.id}`;
    });
    document.addEventListener('click', async e => {
      const button = e.target.closest('.js-quick-view'); if (!button) return;
      const row = await get(`${base}/${button.dataset.id}`);
      document.querySelectorAll('[data-qv]').forEach(el => {
        const key = el.dataset.qv;
        if (key === 'review') el.href = `/Platform/WorkingCalendarImports/Review/${row.id}`;
        else el.textContent = row[key] ?? '—';
      });
      bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
    });
    document.getElementById('formWorkingCalendarImport').addEventListener('submit', async e => {
      e.preventDefault();
      const form = e.currentTarget;
      form.classList.add('was-validated');
      showFormError('');
      if (!form.checkValidity()) return;

      const saveButton = document.getElementById('btnSaveWorkingCalendarImport');
      if (saveButton) saveButton.disabled = true;
      try {
        const response = await fetch(base, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({
          targetCalendarId: document.getElementById('TargetCalendarId').value,
          includeNonPublicTypes: document.getElementById('IncludeNonPublicTypes').checked,
          notes: document.getElementById('Notes').value || null }) });
        const body = response.status === 204 ? null : await response.json().catch(() => null);
        if (response.ok) {
          bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
          form.classList.remove('was-validated');
          showFormError('');
          await load();
        } else {
          const message = apiError(body);
          showFormError(message);
          window.showToast?.(message, 'error');
        }
      } catch (error) {
        const message = apiError(error);
        showFormError(message);
        window.showToast?.(message, 'error');
      } finally {
        if (saveButton) saveButton.disabled = false;
      }
    });
    await load();
  });
})();
