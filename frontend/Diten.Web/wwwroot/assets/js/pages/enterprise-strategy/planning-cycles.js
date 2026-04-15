"use strict";

window.planningCyclesApp = (function (window, document) {
  const api = window.strategyPlanningApi;
  const workbook = window.enterpriseWorkbookOptions || {};
  const ui = window.enterpriseStrategyUi || {};
  const planningCyclesListUrl = "/management-governance/enterprise-strategy-business-performance/planning/cycles";
  const planningCycleCreateUrl = "/management-governance/enterprise-strategy-business-performance/planning/cycles/create";

  let lookups = {};
  let rows = [];
  let editId = "";
  let editSnapshot = null;
  let currentRandomSuffix = "";
  let ownerLookup = [];
  let tableEl, dt, offcanvasEl, offcanvas, formEl;
  let isCreatePage;

  const editableFieldIds = [
    "planning-cycle-name",
    "planning-cycle-code",
    "planning-cycle-type",
    "planning-cycle-owner-company",
    "planning-cycle-owner-position",
    "planning-cycle-effective-from",
    "planning-cycle-effective-to",
    "planning-cycle-description"
  ];

  function text(v) { return String(v || "").trim(); }
  function fmtDate(v) {
    if (!v) return "-";
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? "-" : d.toLocaleDateString();
  }
  function notify(message, kind) { ui.notify?.(message, kind || "success"); }
  function showError(err, fallback) { notify(ui.getErrorMessage?.(err, fallback) || fallback, "danger"); }
  function byId(id) { return document.getElementById(id); }

  function statusBadge(status) {
    const normalized = text(status).toLowerCase();
    const statusObj = {
      active: { title: 'Active', class: 'bg-label-success' },
      draft: { title: 'Draft', class: 'bg-label-warning' },
      archived: { title: 'Archived', class: 'bg-label-secondary' }
    };
    const s = statusObj[normalized] || { title: status || "-", class: 'bg-label-info' };
    return `<span class="badge ${s.class}">${s.title}</span>`;
  }

  function ownerLabel(ownerId) {
    const id = text(ownerId).toLowerCase();
    if (!id) return "-";
    const fromLookups = ownerLookup.find((x) => text(x.ownerId || x.value).toLowerCase() === id);
    if (fromLookups) return text(fromLookups.displayName || fromLookups.label || fromLookups.ownerId || ownerId);
    return workbook.userDisplayName?.(ownerId) || ownerId;
  }

  function normalizeOwnerOption(row) {
    const ownerId = text(row?.ownerId || row?.value || row?.id);
    if (!ownerId) return null;
    const baseLabel = text(row?.fullName || row?.displayName || row?.label || ownerId);
    return { ownerId, value: ownerId, displayName: baseLabel, label: baseLabel };
  }

  function ownerReferencesFromSources() {
    const workbookRefs = Array.isArray(workbook.ownerReferences)
      ? workbook.ownerReferences
      : (typeof workbook.ownerReferences === "function" ? workbook.ownerReferences() : []);
    const source = workbookRefs.length ? workbookRefs : (lookups.ownerReferences || []);
    const seen = new Set();
    return source
      .map(normalizeOwnerOption)
      .filter(Boolean)
      .filter((x) => {
        const key = text(x.ownerId).toLowerCase();
        if (!key || seen.has(key)) return false;
        seen.add(key);
        return true;
      });
  }

  async function refreshOwnerPositions() {
    const companyId = text(byId("planning-cycle-owner-company")?.value);
    const positionEl = byId("planning-cycle-owner-position");
    if (!positionEl) return;
    const current = text(positionEl.value);
    if (!companyId) {
      workbook.fillSelect?.(positionEl, [], { placeholder: "Select company first", keepCurrent: false });
      positionEl.disabled = true;
      return;
    }
    await workbook.ensurePositionsLoaded?.();
    const globalPositions = lookups.positions || [];
    const companyOptions = workbook.positionOptionsForCompany?.(companyId) || [];
    
    // Use global positions from the new API if available, fallback to workbook
    const options = globalPositions.length ? globalPositions : companyOptions;
    
    workbook.fillSelect?.(positionEl, options, { placeholder: options.length ? "Select owner position" : "No positions available", keepCurrent: false });
    positionEl.disabled = options.length === 0;
    if (current && options.some((o) => String(o.value || o.id) === current)) positionEl.value = current;
    syncCurrentOwnerPerson();
  }

  function syncCurrentOwnerPerson() {
    const positionId = text(byId("planning-cycle-owner-position")?.value);
    const personSelect = byId("planning-cycle-current-owner-person");
    if (!personSelect) return;
    if (!positionId) {
      personSelect.value = "";
      return;
    }
    const match = workbook.positionIncumbent?.(positionId);
    if (match && match.incumbentPersonId) {
      const incumbentId = text(match.incumbentPersonId);
      if (Array.from(personSelect.options).some(o => o.value === incumbentId)) {
        personSelect.value = incumbentId;
        $(personSelect).trigger('change');
      }
    } else {
      personSelect.value = "";
      $(personSelect).trigger('change');
    }
  }

  function slugify(text) {
    if (!text) return "";
    const trMap = { 'ç': 'C', 'ğ': 'G', 'ı': 'I', 'i': 'I', 'ö': 'O', 'ş': 'S', 'ü': 'U', 'Ç': 'C', 'Ğ': 'G', 'İ': 'I', 'Ö': 'O', 'Ş': 'S', 'Ü': 'U' };
    let str = text.toString();
    for (let key in trMap) {
      str = str.replace(new RegExp(key, 'g'), trMap[key]);
    }
    return str.toUpperCase()
      .replace(/\s+/g, '-')
      .replace(/[^\w-]+/g, '')
      .replace(/--+/g, '-')
      .replace(/^-+/, '')
      .replace(/-+$/, '')
      .trim();
  }

  function generateRandomSuffix() {
    return Math.floor(100000 + Math.random() * 900000).toString();
  }

  function updateDerivedCode() {
    const name = byId("planning-cycle-name").value;
    const typeSelect = byId("planning-cycle-type");
    const typeLabel = typeSelect.options[typeSelect.selectedIndex]?.text || "";
    
    const abbreviations = {
      "Annual": "ANN",
      "Strategic": "STR",
      "Operational": "OPR",
      "Quarterly": "QRT",
      "Monthly": "MON",
      "Budget": "BGT",
      "Special": "SPC"
    };

    const namePart = slugify(name);
    let typePart = "";
    if (typeLabel && typeLabel !== "Select") {
      typePart = abbreviations[typeLabel] || slugify(typeLabel).substring(0, 3);
    }

    if (!currentRandomSuffix) currentRandomSuffix = generateRandomSuffix();

    let parts = ["PC"];
    if (typePart) parts.push(typePart);
    if (namePart) parts.push(namePart);
    parts.push(currentRandomSuffix);

    const codeEl = byId("planning-cycle-code");
    if (codeEl) codeEl.value = parts.join("-");
  }

  function setSummaryCards(items) {
    const all = items || [];
    if (byId("planning-cycle-total")) byId("planning-cycle-total").textContent = String(all.length);
    if (byId("planning-cycle-active")) byId("planning-cycle-active").textContent = String(all.filter(x => text(x.status).toLowerCase() === "active").length);
    if (byId("planning-cycle-draft")) byId("planning-cycle-draft").textContent = String(all.filter(x => text(x.status).toLowerCase() === "draft").length);
    if (byId("planning-cycle-archived")) byId("planning-cycle-archived").textContent = String(all.filter(x => text(x.status).toLowerCase() === "archived").length);
  }

  function getInitials(name) {
    return (name.match(/\b\w/g) || []).map(char => char.toUpperCase()).join("").slice(0, 2);
  }

  function fixDataTableLayout() {
    setTimeout(() => {
      const elementsToModify = [
          { selector: '.dt-buttons .btn', classToRemove: 'btn-secondary' },
          { selector: '.dt-search .form-control', classToRemove: 'form-control-sm' },
          { selector: '.dt-length .form-select', classToRemove: 'form-select-sm', classToAdd: 'ms-0' },
          { selector: '.dt-length', classToAdd: 'mb-md-6 mb-0' },
          { selector: '.dt-search', classToAdd: 'mb-md-6 mb-2' },
          {
              selector: '.dt-layout-end',
              classToRemove: 'justify-content-between',
              classToAdd: 'd-flex gap-md-2 justify-content-md-end justify-content-center gap-2 flex-wrap mt-0'
          },
          { selector: '.dt-layout-start', classToAdd: 'mt-0' },
          { selector: '.dt-buttons', classToAdd: 'd-flex gap-2 mb-md-0 mb-6' },
          { selector: '.dt-layout-table', classToRemove: 'row mt-2' },
          { selector: '.dt-layout-full', classToRemove: 'col-md col-12', classToAdd: 'table-responsive' }
      ];

      elementsToModify.forEach(({ selector, classToRemove, classToAdd }) => {
          document.querySelectorAll(selector).forEach(element => {
              if (classToRemove) {
                  classToRemove.split(' ').forEach(className => element.classList.remove(className));
              }
              if (classToAdd) {
                  classToAdd.split(' ').forEach(className => element.classList.add(className));
              }
          });
      });

      // Mount filter panel right under DataTable toolbar row
      const mountFilterPanel = () => {
          const host = document.getElementById('filterCollapse');
          const filterBtn = document.querySelector('.dt-filter-btn');
          if (!host || !filterBtn) return;

          const toolbarRow = 
              filterBtn.closest('.dt-layout-row') || 
              filterBtn.closest('.row') || 
              filterBtn.closest('.dt-layout-end')?.parentElement;

          if (toolbarRow && host.previousElementSibling !== toolbarRow) {
              toolbarRow.insertAdjacentElement('afterend', host);
              host.classList.add('px-3');
          }
      };

      mountFilterPanel();

      // Group Eye and Filter buttons (Ensuring zero-gap)
      const dtButtons = document.querySelector('.dt-buttons');
      if (dtButtons) {
          const eyeBtn = dtButtons.querySelector('.dt-eye-btn');
          const filterBtn = dtButtons.querySelector('.dt-filter-btn');
          if (eyeBtn && filterBtn && !eyeBtn.parentElement.classList.contains('btn-group')) {
              const group = document.createElement('div');
              group.className = 'btn-group';
              eyeBtn.parentNode.insertBefore(group, eyeBtn);
              group.appendChild(eyeBtn);
              group.appendChild(filterBtn);
              
              // Ensure no margins are pushing them apart inside the group
              [eyeBtn, filterBtn].forEach(btn => {
                  btn.classList.remove('ms-2', 'mx-1', 'mx-2', 'mx-3', 'mx-4', 'ms-3');
                  btn.style.margin = '0';
              });
          }
      }
    }, 100);
  }

  function initDataTable() {
    if (dt) return;
    dt = new DataTable(tableEl, {
      responsive: {
        details: {
          display: DataTable.Responsive.display.modal({
            header: function (row) {
              var data = row.data();
              return '<h5 class="modal-title">Planning Cycle Details - ' + (data['name'] || '') + '</h5>';
            }
          }),
          type: 'column',
          renderer: function (api, rowIdx, columns) {
            var data = $.map(columns, function (col, i) {
              // Exclude Checkbox (1) and Actions (8) from modal
              return col.hidden && col.columnIndex !== 1 && col.columnIndex !== 8
                ? '<tr data-dt-row="' + col.rowIndex + '" data-dt-column="' + col.columnIndex + '">' +
                    '<td>' + col.title + ':' + '</td> ' +
                    '<td>' + col.data + '</td>' +
                  '</tr>'
                : '';
            }).join('');

            return data ? $('<table class="table"/>').append(data) : false;
          }
        }
      },
      processing: true,
      serverSide: false,
      ajax: async (data, callback) => {
        try {
          if (typeof Notiflix !== 'undefined') {
            Notiflix.Block.standard('#planning-cycles-card', {
              backgroundColor: 'rgba(255, 255, 255, 0.45)',
              svgSize: '45px',
              svgColor: '#5a5fe0',
              messageFontSize: '14px',
              cssAnimation: true,
              cssAnimationDuration: 300
            });
          }

          // Ensure dependencies are ready
          await Promise.all([
             workbook.ensureLookupsLoaded?.(),
             workbook.ensureUsersLoaded?.(),
             workbook.ensureCompaniesLoaded?.()
          ]);

          const [lookupResult, listResult, positionResult, userResult] = await Promise.allSettled([
            window.strategyEnterpriseMetaApi?.lookups?.().catch(() => ({})),
            api.listCycles().catch(() => []),
            api.getAllPositions().catch(() => []),
            window.strategyEnterpriseMetaApi?.getUsersByTenantId().catch(() => ({ data: [] }))
          ]);

          lookups = lookupResult.status === "fulfilled" ? (lookupResult.value || {}) : {};
          rows = listResult.status === "fulfilled" && Array.isArray(listResult.value) ? listResult.value : [];
          
          // Map external positions (PositionId -> value, PositionName -> text) and sort alphabetically
          lookups.positions = positionResult.status === "fulfilled" && Array.isArray(positionResult.value)
            ? positionResult.value.map(p => ({ value: p.PositionId, label: p.PositionName, text: p.PositionName }))
                .sort((a, b) => (a.text || "").localeCompare(b.text || "", 'tr', { sensitivity: 'base' }))
            : [];

          // Map users (id -> value, fullName -> text) and sort alphabetically
          let rawUsers = [];
          if (userResult.status === "fulfilled") {
            const val = userResult.value;
            rawUsers = Array.isArray(val) ? val : (val?.data || []);
          }
          
          lookups.users = rawUsers
            .map(u => ({ value: u.id, label: u.fullName, text: u.fullName }))
            .sort((a, b) => (a.text || "").localeCompare(b.text || "", 'tr', { sensitivity: 'base' }));

          // Sync dependencies
          ownerLookup = ownerReferencesFromSources();
          wireLookups();
          setSummaryCards(rows);

          callback({ data: rows });
        } catch (err) {
          console.error("Failed to load planning cycles:", err);
          callback({ data: [] });
        } finally {
          if (typeof Notiflix !== 'undefined') {
            Notiflix.Block.remove('#planning-cycles-card', 400);
          }
        }
      },
      columns: [
        { data: null, defaultContent: "" },
        { data: null, defaultContent: "", visible: false },
        { data: "name" },
        { data: "ownerId" },
        { data: "planningCycleType" },
        { data: "effectiveFrom" },
        { data: "status" },
        { data: "ownerCompanyId", visible: false },
        { data: null, className: "text-end" }
      ],
      columnDefs: [
        {
          className: 'control',
          orderable: false,
          targets: 0
        },
        {
          targets: 1,
          orderable: false,
          responsivePriority: 1000,
          checkboxes: { selectAllRender: '<input type="checkbox" class="form-check-input">' },
          render: () => '<input type="checkbox" class="dt-checkboxes form-check-input">'
        },
        {
          targets: 2,
          responsivePriority: 1,
          render: (data, type, full) => {
            return `
              <div class="d-flex flex-column">
                <span class="text-heading fw-medium text-truncate">${full.name || "-"}</span>
                <small class="text-muted text-uppercase" style="font-size: 0.65rem;">${full.code || ""}</small>
              </div>`;
          }
        },
        {
          targets: 3,
          render: (data, type, full) => {
            const name = ownerLabel(full.ownerId);
            const initials = getInitials(name);
            const states = ['success', 'danger', 'warning', 'info', 'dark', 'primary', 'secondary'];
            const state = states[Math.floor(Math.random() * states.length)];
            return `
              <div class="d-flex justify-content-start align-items-center">
                <div class="avatar-wrapper">
                  <div class="avatar avatar-sm me-4">
                    <span class="avatar-initial rounded-circle bg-label-${state}">${initials}</span>
                  </div>
                </div>
                <div class="d-flex flex-column">
                  <span class="text-heading text-truncate fw-medium">${name}</span>
                </div>
              </div>`;
          }
        },
        {
          targets: 4,
          render: (data) => `<span class="text-heading text-capitalize"><i class="icon-base bx bx-calendar-event text-primary me-2"></i>${data || "-"}</span>`
        },
        {
          targets: 5,
          render: (data, type, full) => {
            if (!full.effectiveFrom || !full.effectiveTo) return "-";
            
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            const toDate = new Date(full.effectiveTo);
            const diffDays = Math.ceil((toDate - today) / (1000 * 60 * 60 * 24));

            let toColorClass = "text-success"; // Default Success
            if (diffDays < 0) toColorClass = "text-danger";
            else if (diffDays <= 15) toColorClass = "text-warning";

            const fmt = (dateStr) => {
                if (!dateStr) return "-";
                const d = new Date(dateStr);
                const day = String(d.getDate()).padStart(2, '0');
                const month = d.toLocaleString('en-US', { month: 'short' });
                const year = String(d.getFullYear()).slice(-2);
                return `${day} ${month} ${year}`;
            };

            return `<div class="d-flex flex-column align-items-center small fw-medium">
                <span class="text-muted mb-1">${fmt(full.effectiveFrom)}</span>
                <span class="${toColorClass}">${fmt(full.effectiveTo)}</span>
            </div>`;
          }
        },
        {
          targets: 6,
          render: (data) => statusBadge(data)
        },
        {
          targets: -1,
          responsivePriority: 1,
          className: 'all text-end',
          render: (data, type, full) => {
            const status = text(full.status).toLowerCase();
            const canEdit = status === "draft";
            return `
              <div class="d-flex align-items-center">
                <a href="/management-governance/enterprise-strategy-business-performance/planning/cycles/${full.id}" class="btn btn-icon">
                  <i class="icon-base bx bx-show icon-md"></i>
                </a>
                <a href="javascript:;" class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown">
                  <i class="icon-base bx bx-dots-vertical-rounded icon-md"></i>
                </a>
                <div class="dropdown-menu dropdown-menu-end m-0">
                  ${canEdit ? '<a href="javascript:;" class="dropdown-item btn-edit" data-id="'+full.id+'">Edit</a>' : ''}
                  ${status === 'draft' ? '<a href="javascript:;" class="dropdown-item btn-activate" data-id="'+full.id+'">Activate</a>' : ''}
                  <a href="javascript:;" class="dropdown-item btn-archive" data-id="'+full.id+'">Archive / Delete</a>
                </div>
              </div>`;
          }
        }
      ],
      order: [[2, 'asc']],
      layout: {
        topStart: {
            rowClass: 'row m-3 justify-content-between',
            features: [
                {
                    pageLength: {
                        menu: [10, 25, 50, 100],
                        text: '_MENU_'
                    },
                }
            ]
        },
        topEnd: {
            rowClass: 'row mx-3 justify-content-between',
            features: [
                {
                    search: {
                        placeholder: 'Search Cycle',
                        text: '_INPUT_'
                    }
                },
                {
                    buttons: [
                        {
                            extend: 'collection',
                            className: 'btn btn-label-secondary dropdown-toggle',
                            text: '<i class="icon-base bx bx-export icon-sm me-2"></i>Export',
                            buttons: ['print', 'csv', 'excel', 'pdf', 'copy']
                        },
                        {
                            text: '<i class="icon-base bx bx-show icon-sm"></i>',
                            className: 'btn btn-icon btn-label-secondary dt-eye-btn',
                            action: function () { console.log("Görünüm değiştirildi"); }
                        },
                        {
                            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                            className: 'btn btn-icon btn-label-secondary dt-filter-btn',
                            action: function () { 
                                const filterEl = document.getElementById('filterCollapse');
                                if (filterEl) {
                                    const bsCollapse = bootstrap.Collapse.getOrCreateInstance(filterEl);
                                    bsCollapse.toggle();
                                    // Toggle active class on button
                                    this.node().classList.toggle('active');
                                }
                            }
                        },
                        {
                            text: '<i class="icon-base bx bx-plus icon-sm me-sm-2"></i>Add New Cycle',
                            className: 'btn btn-primary',
                            action: () => resetForm().then(() => offcanvas.show())
                        }
                    ]
                }
            ]
        },
        bottomStart: {
          rowClass: 'row mx-3 justify-content-between',
          features: ['info']
        },
        bottomEnd: {
          paging: { firstLast: false }
        }
        },
        language: {
            paginate: {
                next: '<i class="icon-base bx bx-chevron-right scaleX-n1-rtl icon-18px"></i>',
                previous: '<i class="icon-base bx bx-chevron-left scaleX-n1-rtl icon-18px"></i>'
            }
        },

      initComplete: function () {
        const api = this.api();
        const createFilter = (colIdx, container, placeholder, sourceOptions) => {
          const wrapper = document.querySelector(container);
          if (!wrapper) return;
          wrapper.innerHTML = "";
          const select = document.createElement('select');
          select.className = 'form-select form-select-sm text-capitalize';
          select.innerHTML = `<option value="">${placeholder}</option>`;
          wrapper.appendChild(select);
          
          select.addEventListener('change', () => {
            const val = select.value ? `^${select.value}$` : '';
            api.column(colIdx).search(val, true, false);
          });
          
          const options = sourceOptions || Array.from(new Set(api.column(colIdx).data().toArray())).sort();
          options.forEach(d => {
            const val = typeof d === "object" ? (d.value || d.id || d.ownerId) : d;
            const label = typeof d === "object" ? (d.label || d.displayName || d.name) : d;
            if (!val) return;
            const opt = document.createElement('option');
            opt.value = val;
            opt.textContent = label;
            select.appendChild(opt);
          });
        };

        createFilter(4, '.planning_cycle_type', 'Select Type', lookups.planningCycleTypes);
        createFilter(7, '.planning_cycle_company', 'Select Company', workbook.companyOptions?.());
        createFilter(3, '.planning_cycle_owner', 'Select Owner', ownerLookup);
        createFilter(6, '.planning_cycle_status', 'Select Status', lookups.planningLifecycleStatuses);

        // Manual Apply button
        document.querySelector('.btn-apply-filter')?.addEventListener('click', () => {
             api.draw();
             const filterEl = document.getElementById('filterCollapse');
             if (filterEl) {
                 bootstrap.Collapse.getInstance(filterEl)?.hide();
                 const filterBtn = document.querySelector('.dt-filter-btn');
                 if (filterBtn) filterBtn.classList.remove('active');
             }
             updateFilterBadge();
        });

        // Manual Reset button
        document.querySelector('.btn-reset-filter')?.addEventListener('click', () => {
             const selects = document.querySelectorAll('#filterCollapse select');
             selects.forEach(select => {
                 select.value = "";
             });
             api.columns().search('').draw();
             updateFilterBadge();
        });

        fixDataTableLayout();
      },
      drawCallback: function () {
        fixDataTableLayout();
      }
    });
  }

  function updateFilterBadge() {
    const filterGroups = [
      { id: '.planning_cycle_type', label: 'Type' },
      { id: '.planning_cycle_company', label: 'Company' },
      { id: '.planning_cycle_owner', label: 'Owner' },
      { id: '.planning_cycle_status', label: 'Status' }
    ];

    let count = 0;
    let tooltipRows = [];

    filterGroups.forEach(group => {
      const select = document.querySelector(`${group.id} select`);
      if (select && select.value) {
        count++;
        const selectedText = select.options[select.selectedIndex].text;
        tooltipRows.push(`${group.label}: ${selectedText}`);
      }
    });

    const btn = document.querySelector('.dt-filter-btn');
    if (!btn) return;

    let badge = btn.querySelector('.badge');
    if (count > 0) {
      if (!badge) {
        badge = document.createElement('span');
        badge.className = 'badge rounded-pill bg-primary badge-notifications';
        badge.style.position = 'absolute';
        badge.style.top = '-5px';
        badge.style.right = '-5px';
        badge.style.padding = '0.2rem 0.4rem';
        badge.style.fontSize = '0.65rem';
        badge.style.border = '2px solid white';
        btn.appendChild(badge);
      }
      btn.style.position = 'relative';
      badge.textContent = count;
      
      // Tooltip update
      const tooltipText = tooltipRows.join('<br>');
      badge.setAttribute('data-bs-toggle', 'tooltip');
      badge.setAttribute('data-bs-placement', 'top');
      badge.setAttribute('data-bs-html', 'true');
      badge.setAttribute('title', tooltipText);
      
      // Re-init tooltip if bootstrap is available
      if (window.bootstrap?.Tooltip) {
         const existing = window.bootstrap.Tooltip.getInstance(badge);
         if (existing) existing.dispose();
         new window.bootstrap.Tooltip(badge);
      }
    } else if (badge) {
      if (window.bootstrap?.Tooltip) {
         window.bootstrap.Tooltip.getInstance(badge)?.dispose();
      }
      badge.remove();
    }
  }

  function wireLookups() {
    const types = lookups.planningCycleTypes || [];
    const companies = (workbook.companyOptions?.() || [])
      .sort((a, b) => (a.text || a.label || "").localeCompare(b.text || b.label || "", 'tr', { sensitivity: 'base' }));
    const positions = lookups.positions || [];
    const users = lookups.users || [];

    workbook.fillSelect?.(byId("planning-cycle-type"), types, { placeholder: "Select Type", keepCurrent: true });
    workbook.fillSelect?.(byId("planning-cycle-owner-company"), companies, { placeholder: "Select Company", keepCurrent: true });
    workbook.fillSelect?.(byId("planning-cycle-owner-position"), positions, { placeholder: "Select Position", keepCurrent: true });
    const $personSelect = $("#planning-cycle-current-owner-person");
    if ($personSelect.length) {
        // Clear and add placeholder option
        $personSelect.empty().append('<option></option>');
        
        // Manual Option building
        users.forEach(u => {
            const option = new Option(u.text || u.label || 'Unknown', u.value || u.id, false, false);
            $personSelect.append(option);
        });

        // Initialize/Refresh Select2
        if ($.fn.select2) {
            if ($personSelect.hasClass("select2-hidden-accessible")) {
                $personSelect.select2('destroy');
            }
            $personSelect.select2({
                dropdownParent: $("#planningCycleEditorOffcanvas"),
                placeholder: "Select Person",
                allowClear: true,
                width: '100%'
            });
        }
    }

    // Fill Filters
    const companyFilterSelect = document.querySelector('.planning_cycle_company select');
    if (companyFilterSelect && companies.length) {
        workbook.fillSelect?.(companyFilterSelect, companies, { placeholder: "Select Company", keepCurrent: true });
    }

    const ownerFilterSelect = document.querySelector('.planning_cycle_owner select');
    if (ownerFilterSelect && positions.length) {
        workbook.fillSelect?.(ownerFilterSelect, positions, { placeholder: "Select Position", keepCurrent: true });
    }
  }

  function setStatus(status) {
    const hiddenInput = byId("planning-cycle-status");
    if (!hiddenInput) return;
    
    status = status || "Draft";
    hiddenInput.value = status;

    const container = byId("planning-cycle-status-container");
    if (!container) return;

    const pills = container.querySelectorAll(".status-pill");
    const isEdit = !!editId;

    pills.forEach(pill => {
        const pStatus = pill.dataset.status;
        pill.className = "badge rounded-pill cursor-pointer status-pill px-3 py-2"; // Reset
        
        // Visual Styling based on status
        if (pStatus === "Draft") pill.classList.add("bg-label-primary");
        else if (pStatus === "Active") pill.classList.add("bg-label-success");
        else if (pStatus === "Archived") pill.classList.add("bg-label-secondary");

        // Active State
        if (pStatus === status) {
            pill.classList.add("border", "border-primary", "border-2", "fw-bold");
            pill.style.opacity = "1";
        } else {
            pill.style.opacity = "0.4";
        }

        // Restrictions for Create Mode
        if (!isEdit && pStatus !== "Draft") {
            pill.style.opacity = "0.2";
            pill.style.cursor = "not-allowed";
        } else {
            pill.style.cursor = "pointer";
        }
    });
  }

  async function load() {
    try {
      // Forcing a full re-initialization is safer than ajax.reload() in many async scenarios 
      // where the loading indicator might hang.
      if (dt) {
        if (typeof dt.destroy === "function") {
          dt.destroy();
        }
        dt = null;
      }
      initDataTable();
    } catch (err) {
      console.error("Critical load failure, forcing re-init:", err);
      initDataTable();
    }
  }

  async function fillForm(row) {
    editSnapshot = row || null;
    byId("planning-cycle-name").value = row?.name || "";
    byId("planning-cycle-code").value = row?.code || "";

    // Extract suffix if possible
    if (row?.code) {
      const parts = row.code.split("-");
      if (parts.length > 0) currentRandomSuffix = parts[parts.length - 1];
    }

    byId("planning-cycle-type").value = row?.planningCycleType || "";
    setStatus(row?.status || "Draft");
    byId("planning-cycle-owner-company").value = row?.ownerCompanyId || "";
    await refreshOwnerPositions();
    byId("planning-cycle-owner-position").value = row?.ownerPositionId || "";
    syncCurrentOwnerPerson();

    // Ensure person is set from row if sync didn't catch it or for override
    if (row?.currentOwnerPersonId) {
       $("#planning-cycle-current-owner-person").val(String(row.currentOwnerPersonId)).trigger('change');
    }

    byId("planning-cycle-description").value = row?.description || "";
    byId("planning-cycle-effective-from").value = (row?.effectiveFrom || "").slice(0, 10);
    byId("planning-cycle-effective-to").value = (row?.effectiveTo || "").slice(0, 10);
  }

  async function resetForm() {
    formEl.reset();
    editId = "";
    editSnapshot = null;
    currentRandomSuffix = "";
    byId("planningCycleEditorOffcanvasLabel").textContent = "Create Planning Cycle";
    await refreshOwnerPositions();
    setStatus("Draft");
    $("#planning-cycle-current-owner-person").val(null).trigger('change');
    
    // Clear validation states
    formEl.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));
    byId("planning-cycle-form-error").classList.add("d-none");
  }

  function validateForm() {
    let isValid = true;
    const requiredIds = [
        "planning-cycle-name",
        "planning-cycle-type",
        "planning-cycle-owner-company",
        "planning-cycle-owner-position",
        "planning-cycle-effective-from",
        "planning-cycle-effective-to"
    ];

    const errorEl = byId("planning-cycle-form-error");
    errorEl.classList.add("d-none");
    let errors = [];

    requiredIds.forEach(id => {
        const el = byId(id);
        if (!el) return;
        el.classList.remove("is-invalid");
        if (!el.value?.trim()) {
            el.classList.add("is-invalid");
            isValid = false;
        }
    });

    // Date Range Validation (Independent of required check)
    const fromEl = byId("planning-cycle-effective-from");
    const toEl = byId("planning-cycle-effective-to");
    if (fromEl?.value && toEl?.value) {
        if (new Date(fromEl.value) > new Date(toEl.value)) {
            fromEl.classList.add("is-invalid");
            toEl.classList.add("is-invalid");
            errors.push("Effective To must be on or after Effective From.");
            isValid = false;
        }
    }

    if (!isValid) {
        if (errors.length === 0) {
            errors.push("Please fulfill all mandatory fields marked with an asterisk (*).");
        }
        errorEl.innerHTML = errors.join("<br>");
        errorEl.classList.remove("d-none");
        offcanvasEl.querySelector(".offcanvas-body").scrollTop = 0;
    }

    return isValid;
  }

  async function save() {
    if (!validateForm()) return;

    try {
      const payload = {
        name: text(byId("planning-cycle-name").value),
        code: text(byId("planning-cycle-code").value),
        planningCycleType: text(byId("planning-cycle-type").value),
        status: text(byId("planning-cycle-status").value),
        ownerCompanyId: text(byId("planning-cycle-owner-company").value),
        ownerPositionId: text(byId("planning-cycle-owner-position").value),
        currentOwnerPersonId: text(byId("planning-cycle-current-owner-person").value),
        description: text(byId("planning-cycle-description").value),
        effectiveFrom: text(byId("planning-cycle-effective-from").value),
        effectiveTo: text(byId("planning-cycle-effective-to").value)
      };

      if (editId) {
        await api.updateCycle(editId, payload);
      } else {
        await api.createCycle(payload);
      }

      // Record was saved, handle UI updates safely
      try {
        offcanvas?.hide();
        await load();
      } catch (uiErr) {
        console.warn("View state update failed, but record was saved:", uiErr);
      }

      notify(editId ? "Planning cycle updated." : "Planning cycle created.");
    } catch (err) {
      notify(ui.getErrorMessage?.(err, "Could not save cycle.") || "Could not save cycle.", "error");
    }
  }

  function registerEvents() {
    byId("planning-cycle-save")?.addEventListener("click", save);
    byId("planning-cycle-owner-company")?.addEventListener("change", refreshOwnerPositions);
    byId("planning-cycle-owner-position")?.addEventListener("change", syncCurrentOwnerPerson);

    byId("planning-cycle-name")?.addEventListener("input", updateDerivedCode);
    byId("planning-cycle-type")?.addEventListener("change", updateDerivedCode);
    
    byId("planning-cycle-status-container")?.addEventListener("click", (e) => {
        const pill = e.target.closest(".status-pill");
        if (!pill) return;
        
        const status = pill.dataset.status;
        const isEdit = !!editId;
        
        // Restriction: Create mode can only stay in Draft
        if (!isEdit && status !== "Draft") {
            if (typeof notify === "function") {
                notify("New planning cycles must be created as 'Draft'. Lifecycle status can be updated after the record is saved.", "warning");
            }
            return;
        }
        
        setStatus(status);
    });
    
    // Dynamically clear validation states on input
    formEl?.addEventListener("input", (e) => {
        if (e.target.classList.contains("is-invalid")) {
            e.target.classList.remove("is-invalid");
        }
    });
    
    tableEl?.addEventListener("click", async (e) => {
      const btn = e.target.closest(".btn-edit, .btn-activate, .btn-archive");
      if (!btn) return;
      const id = btn.dataset.id;
      if (btn.classList.contains("btn-edit")) {
        const row = await api.getCycle(id);
        await fillForm(row);
        editId = id;
        byId("planningCycleEditorOffcanvasLabel").textContent = "Edit Planning Cycle";
        offcanvas.show();
      } else if (btn.classList.contains("btn-activate")) {
        await api.activateCycle(id);
        notify("Activated.");
        await load();
      } else if (btn.classList.contains("btn-archive")) {
        await api.archiveCycle(id);
        notify("Archived.");
        await load();
      }
    });

    // Summary Card Filtering
    document.querySelectorAll('.planning-filter-card').forEach(card => {
      card.addEventListener('click', function() {
        const status = this.dataset.filterStatus;
        const statusSelect = document.querySelector('.planning_cycle_status select');

        // Visual Active State
        document.querySelectorAll('.planning-filter-card').forEach(c => {
          c.classList.remove('border', 'border-primary', 'border-2', 'shadow-sm');
        });
        
        if (status !== 'all') {
          this.classList.add('border', 'border-primary', 'border-2', 'shadow-sm');
          dt.column(6).search(status, true, false).draw();
          if (statusSelect) statusSelect.value = status;
        } else {
          dt.column(6).search('').draw();
          if (statusSelect) statusSelect.value = "";
        }
        updateFilterBadge();
      });
    });
  }

  return {
    initPage: async function() {
          tableEl = document.querySelector(".planning-cycles-table");
      offcanvasEl = document.getElementById("planningCycleEditorOffcanvas");
      offcanvas = offcanvasEl ? new bootstrap.Offcanvas(offcanvasEl) : null;
      formEl = document.getElementById("planningCycleForm");

      registerEvents();
      await load();
    }
  };
})(window, document);

document.addEventListener("DOMContentLoaded", () => {
  window.planningCyclesApp.initPage().catch(console.error);
});
