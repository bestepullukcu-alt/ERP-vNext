(function () {
    'use strict';
    const root = document.getElementById('demandForecast');
    if (!root) return;
    const t = key => window.L10n[key];
    const el = id => document.getElementById(id);
    const scope = el('forecastScope');
    const company = el('forecastCompany');
    const cycle = el('forecastCycle');
    const sku = el('forecastSku');
    const warehouse = el('forecastWarehouse');
    const versionInput = el('forecastVersion');
    const command = el('forecastCommand');
    const method = el('forecastMethod');
    const reason = el('forecastReason');
    const canCreate = root.dataset.canCreateDraft === 'true';
    let generation = 0;
    let evaluated = null;
    let created = null;
    let draft = null;
    let allWeeks = false;
    let pendingRequest = null;
    let busy = false;
    const read = (value, name) => value?.[name] ?? value?.[name.charAt(0).toUpperCase() + name.slice(1)];
    const unwrap = payload => read(payload, 'data');
    const guid = value => /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(value) && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(value);
    const status = key => { el('forecastStatus').textContent = t(key); };
    const failureKey = code => code === 401 ? 'ForecastSessionExpired' :
        code === 403 ? 'ForecastPermissionLost' : code === 404 ? 'ForecastScopeLost' :
        code === 409 ? 'ForecastConflict' : code === 422 ? 'ForecastSelectionRejected' : 'ForecastUnavailable';

    // This same-origin adapter is the sole transport for the workspace. The MVC
    // proxy owns the HttpOnly JWT and forwards each request through Gateway.
    async function request(path, options = {}) {
        const response = await fetch(path, { credentials: 'same-origin', ...options });
        if (!response.ok) throw { status: response.status };
        const payload = await response.json();
        const data = Array.isArray(payload) ? payload : unwrap(payload);
        if (read(payload, 'isSuccessful') === false || data == null) throw { status: 503 };
        return data;
    }
    function reset() {
        generation++;
        evaluated = null; created = null; draft = null; pendingRequest = null;
        el('forecastEvaluation').hidden = true;
        el('forecastCreated').hidden = true;
        el('forecastDraft').hidden = true;
        el('forecastCandidates').replaceChildren();
        if (method) method.replaceChildren();
    }
    function deny(code) {
        reset();
        // A lost action permission leaves only the explanation and next step.
        root.replaceChildren(node('p', t(failureKey(code)), 'text-muted'));
    }
    function context() {
        return { legalEntityId: company.value.toLowerCase(), planningCycleId: cycle.value.trim().toLowerCase(),
            skuId: sku.value.trim().toLowerCase(), warehouseId: warehouse.value.trim() };
    }
    const signature = value => JSON.stringify(value);
    const node = (tag, text, css) => {
        const result = document.createElement(tag);
        if (text != null) result.textContent = String(text);
        if (css) result.className = css;
        return result;
    };
    const field = (parent, label, value) => {
        const group = node('div', null, 'mb-2');
        group.append(node('dt', t(label)), node('dd', value));
        parent.appendChild(group);
    };
    const metric = value => value == null ? t('ForecastUndefined') :
        new Intl.NumberFormat(document.documentElement.lang || undefined, { maximumFractionDigits: 4 }).format(value);
    function renderCandidates(data) {
        const candidates = read(data, 'candidates');
        if (!Array.isArray(candidates) || candidates.length !== 7 ||
            new Set(candidates.map(item => read(item, 'method'))).size !== 7 ||
            candidates.some(item => !t('ForecastMethod' + read(item, 'method'))))
            throw { status: 503 };
        const host = el('forecastCandidates');
        host.replaceChildren();
        if (method) {
            method.replaceChildren();
            const empty = node('option', t('ForecastChooseMethod'));
            empty.value = ''; method.appendChild(empty); method.value = '';
        }
        el('forecastPolicy').textContent = t('ForecastPolicy') + ': ' + read(data, 'policyVersion');
        const reasons = {
            Eligible: 'ForecastEligible', EligibleBacktestUnavailable: 'ForecastBacktestUnavailable',
            InsufficientReliableWeeks: 'ForecastInsufficientWeeks',
            InsufficientRealDemandEvents: 'ForecastInsufficientEvents',
            MixedOrMissingUnit: 'ForecastMixedUnit', UntrustedOrMixedUnitHistory: 'ForecastUntrustedHistory',
            InvalidForecastOutput: 'ForecastInvalidOutput'
        };
        candidates.forEach(candidate => {
            const name = read(candidate, 'method');
            const eligible = read(candidate, 'eligible') === true;
            const column = node('article', null, 'col-12 col-lg-6');
            const body = node('div', null, 'border rounded p-3 h-100');
            body.append(node('h3', t('ForecastMethod' + name), 'h6'));
            body.append(node('p', t(reasons[read(candidate, 'reason')] || 'ForecastUnavailable'),
                eligible ? 'text-success' : 'text-warning'));
            const fields = node('dl', null, 'mb-0');
            field(fields, 'ForecastReliableWeeks', read(candidate, 'reliableWeeks'));
            field(fields, 'ForecastRealEvents', read(candidate, 'realDemandEvents'));
            ['wapeRatio', 'mae', 'bias', 'mase'].forEach((name, index) =>
                field(fields, ['ForecastWape', 'ForecastMae', 'ForecastBias', 'ForecastMase'][index], metric(read(candidate, name))));
            body.appendChild(fields);
            if (read(candidate, 'forecastUomId'))
                body.append(node('p', t('HistoryUnit') + ': ' + read(candidate, 'forecastUomId')));
            if (read(candidate, 'unitVerificationPending'))
                body.append(node('p', t('HistoryUnitPending'), 'text-warning'));
            const horizons = read(candidate, 'byWeeksAhead');
            if (Array.isArray(horizons) && horizons.length) {
                const details = node('details');
                details.append(node('summary', t('ForecastHorizonMetrics')));
                const table = node('table', null, 'table table-sm');
                const head = node('tr');
                ['ForecastWeeksAhead', 'ForecastSamples', 'ForecastWape', 'ForecastMae', 'ForecastBias', 'ForecastMase']
                    .forEach(key => head.appendChild(node('th', t(key))));
                const thead = node('thead'); thead.appendChild(head); table.appendChild(thead);
                const tbody = node('tbody');
                horizons.forEach(horizon => {
                    const row = node('tr');
                    [read(horizon, 'weeksAhead'), read(horizon, 'count'),
                        ...['wapeRatio', 'mae', 'bias', 'mase'].map(key => metric(read(horizon, key)))]
                        .forEach(value => row.appendChild(node('td', value)));
                    tbody.appendChild(row);
                });
                table.appendChild(tbody);
                const scroll = node('div', null, 'table-responsive'); scroll.appendChild(table);
                details.appendChild(scroll); body.appendChild(details);
            }
            column.appendChild(body); host.appendChild(column);
            if (eligible && method) {
                const option = node('option', t('ForecastMethod' + name));
                option.value = name; method.appendChild(option);
            }
        });
        el('forecastEvaluation').hidden = false;
    }
    scope.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy) return;
        reset();
        const selection = context();
        if (!guid(selection.legalEntityId) || !guid(selection.planningCycleId) ||
            !guid(selection.skuId) || !selection.warehouseId) { status('ForecastInvalidScope'); return; }
        if (!scope.checkValidity()) { scope.classList.add('was-validated'); status('ForecastInvalidScope'); return; }
        const current = generation;
        busy = true; status('ForecastLoading');
        try {
            const data = await request('/SupplyChain/DemandPlanning/api/forecast-runs/evaluation?' + new URLSearchParams(selection));
            if (current !== generation) return;
            if (read(data, 'planningCycleId') !== selection.planningCycleId ||
                read(data, 'skuId') !== selection.skuId || read(data, 'warehouseId') !== selection.warehouseId)
                throw { status: 503 };
            renderCandidates(data);
            evaluated = { selection, data }; status('ForecastSelectExplicitly');
        } catch (error) {
            if (current === generation) {
                if ([401, 403].includes(error.status)) deny(error.status);
                else { reset(); status(failureKey(error.status)); }
            }
        }
        finally { busy = false; }
    });
    if (command && canCreate) command.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy) return;
        const selected = read(evaluated?.data, 'candidates')?.find(item => read(item, 'method') === method.value);
        if (!evaluated || signature(evaluated.selection) !== signature(context()) ||
            read(selected, 'eligible') !== true) { status('ForecastSelectExplicitly'); return; }
        if (!reason.value.trim()) { status('ReasonRequired'); return; }
        if (!command.checkValidity()) { command.classList.add('was-validated'); status('ReasonRequired'); return; }
        const version = Number(versionInput.value);
        if (versionInput.value === '' || !Number.isInteger(version) || version < 0) { status('ForecastInvalidScope'); return; }
        const body = { planningCycleId: evaluated.selection.planningCycleId,
            skuId: evaluated.selection.skuId, warehouseId: evaluated.selection.warehouseId,
            method: method.value, reason: reason.value.trim(), expectedCycleVersion: version };
        const fingerprint = signature({ selection: evaluated.selection, body });
        if (!pendingRequest || pendingRequest.fingerprint !== fingerprint)
            pendingRequest = { fingerprint, key: crypto.randomUUID() };
        const current = generation;
        busy = true; el('forecastCreate').disabled = true; status('ForecastLoading');
        try {
            const result = await request('/SupplyChain/DemandPlanning/api/forecast-runs?' +
                new URLSearchParams({ legalEntityId: company.value }), {
                method: 'POST', headers: { 'Content-Type': 'application/json',
                    'RequestVerificationToken': command.querySelector('[name="__RequestVerificationToken"]').value,
                    'Idempotency-Key': pendingRequest.key }, body: JSON.stringify(body)
            });
            if (current !== generation) return;
            const revisionId = read(read(result, 'draft'), 'revisionId');
            if (!guid(revisionId)) throw { status: 503 };
            created = { revisionId, legalEntityId: company.value };
            el('forecastCreatedInfo').textContent = t('ForecastCreated') + ' ' + revisionId;
            el('forecastCreated').hidden = false; status('ForecastCreated');
            if (window.showToast) window.showToast(t('ForecastCreated'), 'success');
        } catch (error) {
            if (current === generation) {
                el('forecastCreated').hidden = true; el('forecastDraft').hidden = true;
                if ([401, 403].includes(error.status)) deny(error.status);
                else { if (error.status === 404) reset(); status(failureKey(error.status)); }
            }
        } finally { busy = false; if (el('forecastCreate')) el('forecastCreate').disabled = false; }
    });
    function renderDraft() {
        const series = read(draft, 'series');
        const weeks = read(series[0], 'weeks');
        const host = el('forecastDraftWeeks'); host.replaceChildren();
        weeks.slice(0, allWeeks ? 52 : 13).forEach(week => {
            const row = node('tr');
            [read(week, 'weekStart') + ' – ' + read(week, 'weekEnd'),
                metric(read(week, 'quantity')), read(series[0], 'baseUomId')]
                .forEach(value => row.appendChild(node('td', value)));
            host.appendChild(row);
        });
        el('forecastDraftInfo').textContent = [created.revisionId,
            t('HistoryActor') + ': ' + read(draft, 'createdBy'),
            t('HistoryAt') + ': ' + read(draft, 'createdAt'),
            t('Reason') + ': ' + read(draft, 'creationReason')].join(' · ');
        el('forecastDraftUnit').hidden = !weeks.some(week =>
            read(read(week, 'methodProvenance'), 'unitVerificationPending'));
        el('forecastAllWeeks').textContent = t(allWeeks ? 'FirstThirteen' : 'ShowAllWeeks');
        el('forecastDraft').hidden = false;
    }
    el('forecastOpenDraft').addEventListener('click', async () => {
        if (!created || busy) return;
        const current = generation; busy = true; el('forecastDraft').hidden = true;
        try {
            const result = await request('/SupplyChain/DemandPlanning/api/revisions/' + created.revisionId + '?' +
                new URLSearchParams({ legalEntityId: created.legalEntityId }));
            if (current !== generation) return;
            const series = read(result, 'series');
            if (read(result, 'revisionId') !== created.revisionId || !Array.isArray(series) ||
                series.length !== 1 || read(series[0], 'weeks')?.length !== 52) throw { status: 503 };
            draft = result; allWeeks = false; renderDraft();
        } catch (error) {
            if (current === generation) {
                if ([401, 403].includes(error.status)) deny(error.status);
                else status(failureKey(error.status));
            }
        }
        finally { busy = false; }
    });
    el('forecastAllWeeks').addEventListener('click', () => { if (draft) { allWeeks = !allWeeks; renderDraft(); } });
    [company, cycle, sku, warehouse, versionInput].filter(Boolean).forEach(input =>
        input.addEventListener(input === company ? 'change' : 'input', () => { reset(); status('ForecastSelectExplicitly'); }));
    (async function loadCompanies() {
        try {
            const companies = await request('/SupplyChain/api/legal-entities');
            if (!Array.isArray(companies)) throw { status: 503 };
            company.replaceChildren();
            const blank = node('option', t('HistorySelectCompany')); blank.value = ''; company.appendChild(blank);
            companies.forEach(item => {
                const id = read(item, 'legalEntityId');
                if (!guid(id)) return;
                const option = node('option', read(item, 'displayName') ?? read(item, 'legalName'));
                option.value = id; company.appendChild(option);
            });
            if (company.options.length < 2) { status('NoCompanyData'); return; }
            company.disabled = false; scope.hidden = false; status('ForecastSelectExplicitly');
        } catch (error) {
            if ([401, 403].includes(error.status)) deny(error.status);
            else { scope.hidden = true; status(failureKey(error.status)); }
        }
    })();
})();