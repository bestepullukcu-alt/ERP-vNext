(function () {
    'use strict';
    const root = document.getElementById('demandHistory');
    if (!root) return;
    const labels = JSON.parse(document.getElementById('demandHistoryLabels').textContent);
    const company = document.getElementById('demandHistoryCompany');
    const state = document.getElementById('demandHistoryState');
    const rowsHost = document.getElementById('demandHistoryRows');
    const previous = document.getElementById('demandHistoryPrevious');
    const next = document.getElementById('demandHistoryNext');
    const pageSize = 50;
    let page = 0;
    let requestVersion = 0;

    function field(list, label, value) {
        const wrapper = document.createElement('div');
        const name = document.createElement('dt');
        const detail = document.createElement('dd');
        name.textContent = label;
        detail.textContent = value == null ? '' : String(value);
        wrapper.append(name, detail);
        list.appendChild(wrapper);
    }

    function render(items) {
        rowsHost.replaceChildren();
        items.forEach((entry) => {
            const row = entry.row ?? entry.Row;
            const audit = entry.audit ?? entry.Audit;
            if (!row || !audit) return;
            const unitPending = row.unitVerificationPending ?? row.UnitVerificationPending;
            const effectivePending = entry.effectiveUnitVerificationPending ??
                entry.EffectiveUnitVerificationPending ?? unitPending;
            const unitWarning = entry.unitWarning ?? entry.UnitWarning;
            const article = document.createElement('article');
            article.className = 'dp-history-row';
            const heading = document.createElement('h2');
            heading.textContent = row.sourceRecordKey ?? row.SourceRecordKey ?? '';
            const fields = document.createElement('dl');
            fields.className = 'dp-history-fields';
            field(fields, labels.HistorySku, row.skuId ?? row.SkuId);
            field(fields, labels.HistoryWarehouse, row.warehouseId ?? row.WarehouseId);
            field(fields, labels.HistoryDate, row.occurredOn ?? row.OccurredOn);
            field(fields, labels.HistoryQuantity, unitPending ?
                (row.acceptedQuantity ?? row.AcceptedQuantity) : (row.baseQuantity ?? row.BaseQuantity));
            field(fields, labels.HistoryUnit, unitPending ?
                (row.acceptedUomId ?? row.AcceptedUomId) : (row.baseUomId ?? row.BaseUomId));
            field(fields, labels.HistoryBatch, audit.batchId ?? audit.BatchId);
            field(fields, labels.HistoryDecision, audit.action ?? audit.Action);
            field(fields, labels.HistoryActor, audit.actorId ?? audit.ActorId);
            field(fields, labels.HistoryAt, audit.occurredAt ?? audit.OccurredAt);
            article.append(heading, fields);
            if (effectivePending) {
                const warning = document.createElement('p');
                warning.className = 'dp-history-unit-pending mt-2 mb-0';
                warning.textContent = unitWarning === 'DifferentFromCurrentBase' ?
                    labels.HistoryUnitMismatch : labels.HistoryUnitPending;
                article.appendChild(warning);
            }
            rowsHost.appendChild(article);
        });
    }

    async function loadRows() {
        const version = ++requestVersion;
        rowsHost.replaceChildren();
        previous.disabled = true;
        next.disabled = true;
        state.textContent = labels.HistoryLoading;
        try {
            const url = new URL('/SupplyChain/DemandPlanning/api/history/accepted-rows', window.location.origin);
            url.searchParams.set('legalEntityId', company.value);
            url.searchParams.set('skip', String(page * pageSize));
            url.searchParams.set('take', String(pageSize));
            const response = await fetch(url, { credentials: 'same-origin' });
            if (!response.ok) throw new Error(String(response.status));
            const payload = await response.json();
            if (version !== requestVersion) return;
            const items = payload.data ?? payload.Data;
            if (!Array.isArray(items)) throw new Error('Invalid response');
            render(items);
            state.textContent = items.length ? '' : labels.HistoryEmpty;
            previous.disabled = page === 0;
            next.disabled = items.length < pageSize;
        } catch (_) {
            if (version === requestVersion) state.textContent = labels.HistoryUnavailable;
        }
    }

    async function loadCompanies() {
        state.textContent = labels.HistoryLoading;
        try {
            const response = await fetch('/SupplyChain/api/legal-entities', { credentials: 'same-origin' });
            if (!response.ok) throw new Error(String(response.status));
            const payload = await response.json();
            const items = Array.isArray(payload) ? payload : (payload.data ?? payload.Data);
            if (!Array.isArray(items)) throw new Error('Invalid response');
            company.replaceChildren();
            items.forEach((item) => {
                const option = document.createElement('option');
                option.value = item.legalEntityId ?? item.LegalEntityId ?? '';
                option.textContent = item.displayName ?? item.DisplayName ?? item.legalName ?? item.LegalName ?? '';
                if (option.value) company.appendChild(option);
            });
            if (!company.options.length) { state.textContent = labels.HistorySelectCompany; return; }
            company.disabled = false;
            await loadRows();
        } catch (_) { state.textContent = labels.HistoryUnavailable; }
    }

    company.addEventListener('change', () => { page = 0; loadRows(); });
    previous.addEventListener('click', () => { if (page > 0) { page--; loadRows(); } });
    next.addEventListener('click', () => { page++; loadRows(); });
    loadCompanies();
})();
