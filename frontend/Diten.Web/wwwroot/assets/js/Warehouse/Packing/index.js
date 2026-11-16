/**
 * MOD-0178 DL-6 — Paketleme (PACKINGSTATION) page script: detail + package form, NOT a DataTable (pack §11.1, §11.5).
 *
 * One read: '/Warehouse/Packing/api/{outboundId}' (→ GET /outbound-shipments/{outboundId}/packages — outbound number + status, the allocations'
 * picked / packed, the packages; no ship-to). Writes through the same-origin proxy only: create / edit / close a package. The browser never calls
 * the inventory service, /api/warehouse or the Gateway. The rules (what is left to pack, which button is shown) are PackingRules (rules.js).
 */
'use strict';

const PackingPage = (function () {
    const rules = window.PackingRules;
    const root = document.getElementById('packing-root');
    const outboundId = root?.dataset.outboundId || '';
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const token = () => document.querySelector('#packingActionToken input[name="__RequestVerificationToken"]')?.value || '';
    const offcanvas = (id) => (byId(id) ? window.bootstrap?.Offcanvas.getOrCreateInstance(byId(id)) : null);
    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');
    // E3-FIX-01 (G-1): quantities, weight and dimensions on screen are in the reader's culture (tr "2", "1,5"); an input shows the same number
    // without grouping and takes "1,5" back; the package body keeps the dot-decimal wire strings (rules.contentsBody / weight / dimensions)
    const culture = () => window.CurrentLanguage || document.documentElement.lang || undefined;
    const qty = (value) => rules.quantityText(value, culture());
    const inputQty = (value) => rules.quantityText(value, culture(), false);

    const getJson = async (url) => {
        const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const post = async (url, body) => {
        const headers = Object.assign({}, getAuthHeaders(true), { RequestVerificationToken: token() });
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const failure = (answer) => answer?.json?.error?.message || W('Warehouse.Error.Generic');
    const showAlert = (id, text) => { const box = byId(id); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };

    let state = null; // { outbound, allocations, packages }

    const render = () => {
        const { outbound, allocations, packages } = state;
        byId('packing-outbound').textContent = outbound.number || '-';
        const status = byId('packing-status');
        status.textContent = W(`Warehouse.Outbound.Status.${outbound.status}`);
        status.className = `badge ${outbound.status === 'Packed' ? 'bg-label-success' : outbound.status === 'Picked' ? 'bg-label-primary' : 'bg-label-secondary'}`;

        const create = rules.newPackage(outbound, can);
        const button = byId('btnNewPackage');
        if (button) {
            button.disabled = !create.enabled;
            button.title = create.reason ? W(create.reason) : '';
        }
        const note = byId('packing-state');
        if (note) { note.textContent = create.reason ? W(create.reason) : ''; note.classList.toggle('d-none', !create.reason); }

        const left = rules.remaining(allocations, packages);
        byId('packing-allocations').querySelector('tbody').innerHTML = allocations.map((a) => `<tr data-alloc-seq="${esc(a.allocSeq)}">`
            + `<td>${esc(a.allocSeq)}</td><td>${esc(a.sku)}</td><td>${esc(a.lotNumber)}</td><td class="text-end">${esc(qty(a.picked))}</td>`
            + `<td class="text-end">${esc(qty(a.packed))}</td><td class="text-end">${esc(qty(left[a.allocSeq]))}</td></tr>`).join('');

        const host = byId('packing-packages');
        host.innerHTML = packages.map((p) => {
            const badge = rules.statusBadge(p.status);
            const contents = (p.contents || []).map((c) => `<div class="small">${esc(format(W('PackageContentLine'), c.allocSeq, qty(c.quantity)))}</div>`).join('');
            const facts = [p.weightKg ? `${esc(W('FieldWeight'))}: ${esc(qty(p.weightKg))}` : '', p.dimensionsCm ? `${esc(W('FieldDimensions'))}: ${esc(rules.dimensionsText(p.dimensionsCm, culture()))}` : '']
                .filter(Boolean).map((f) => `<div class="small text-muted">${f}</div>`).join('');
            const actions = rules.packageActions(p, can).map((a) => `<button type="button" class="btn ${a === 'Close' ? 'btn-primary' : 'btn-label-secondary'} wh-touch"`
                + ` data-package-action="${a}" data-package-id="${esc(p.id)}">${esc(W(`Warehouse.Package.Action.${a}`))}</button>`).join('');
            return `<div class="border rounded p-3" data-package-id="${esc(p.id)}" data-package-status="${esc(p.status)}">`
                + `<div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2"><strong class="font-monospace">${esc(p.number)}</strong>`
                + `<span class="badge ${badge.cls}">${esc(W(badge.word))}</span></div>${contents}${facts}`
                + (actions ? `<div class="d-flex flex-wrap gap-2 mt-3">${actions}</div>` : '') + '</div>';
        }).join('');
        byId('packing-packages-empty')?.classList.toggle('d-none', packages.length > 0);
    };

    const load = async () => {
        showAlert('packing-error', '');
        if (!outboundId) { showAlert('packing-error', W('Warehouse.Error.NotFound')); return; }
        const answer = await getJson(`/Warehouse/Packing/api/${encodeURIComponent(outboundId)}`);
        if (!answer.ok) { showAlert('packing-error', failure(answer)); return; }
        state = { outbound: answer.json.outbound, allocations: answer.json.allocations || [], packages: answer.json.packages || [] };
        render();
    };

    // ─── Package panel (§8.20, AC-45) ────────────────────────────────────────
    let formState = null; // { mode, pkg, key }

    const openForm = (mode, pkg) => {
        if (!state || !byId('offcanvasPackage')) return;
        formState = { mode, pkg: pkg || null, key: newKey() };
        showAlert('package-alert', '');
        byId('offcanvasPackageLabel').textContent = W(mode === 'create' ? 'FormTitleCreate' : 'FormTitleEdit');
        byId('package-subtitle').textContent = [state.outbound.number, pkg?.number].filter(Boolean).join(' · ');
        const left = rules.remaining(state.allocations, state.packages, pkg?.id);
        const own = Object.fromEntries((pkg?.contents || []).map((c) => [c.allocSeq, c.quantity]));
        byId('package-contents').innerHTML = state.allocations.map((a) => {
            const max = left[a.allocSeq];
            const id = `packageQty${a.allocSeq}`;
            return `<div data-content-seq="${esc(a.allocSeq)}"><label for="${id}" class="form-label small mb-1">${esc(format(W('PackageContentLine'), a.allocSeq, `${a.sku ?? ''} ${a.lotNumber ?? ''}`.trim()))}</label>`
                + '<div class="input-group wh-stepper">'
                + `<button type="button" class="btn btn-outline-secondary wh-touch" data-wh-step="-1" data-wh-for="${id}" aria-label="${esc(W('QuantityDecrease'))}"><i class="bx bx-minus"></i></button>`
                + `<input type="text" inputmode="decimal" id="${id}" class="form-control text-center wh-touch" data-alloc-seq="${esc(a.allocSeq)}" data-max="${esc(max)}" value="${esc(inputQty(own[a.allocSeq] ?? ''))}" autocomplete="off" />`
                + `<button type="button" class="btn btn-outline-secondary wh-touch" data-wh-step="1" data-wh-for="${id}" aria-label="${esc(W('QuantityIncrease'))}"><i class="bx bx-plus"></i></button>`
                + `</div><small class="text-muted">${esc(format(W('PackageLeftHint'), qty(max)))}</small></div>`;
        }).join('');
        byId('packageContentsError').textContent = '';
        byId('packageDimensionsError').textContent = '';
        byId('packageWeight').value = inputQty(pkg?.weightKg || '');
        byId('packageDimensions').value = rules.dimensionsText(pkg?.dimensionsCm || '', culture());
        offcanvas('offcanvasPackage')?.show();
    };

    const submitForm = async () => {
        if (!formState) return;
        showAlert('package-alert', '');
        const rows = Array.from(document.querySelectorAll('#package-contents input[data-alloc-seq]')).map((i) => ({ allocSeq: i.dataset.allocSeq, quantity: i.value }));
        const contents = rules.contentsBody(rows);
        if (!contents.length) { byId('packageContentsError').textContent = W('ValidationContents'); return; }
        byId('packageContentsError').textContent = '';
        const dims = byId('packageDimensions').value;
        if (!rules.isDimensions(dims)) { byId('packageDimensionsError').textContent = W('ValidationDimensions'); return; }
        byId('packageDimensionsError').textContent = '';
        const body = { idempotencyKey: formState.key, contents, weightKg: rules.weight(byId('packageWeight').value), dimensionsCm: dims.trim() ? rules.dimensions(dims) : null };
        const button = byId('btnPackageSave');
        button.disabled = true;
        try {
            const answer = formState.mode === 'create'
                ? await post(`/Warehouse/Packing/create/${encodeURIComponent(outboundId)}`, body)
                : await post(`/Warehouse/Packing/edit/${encodeURIComponent(formState.pkg.id)}`, Object.assign(body, { ifMatch: formState.pkg.etag }));
            if (!answer.ok) { showAlert('package-alert', failure(answer)); return; }
            offcanvas('offcanvasPackage')?.hide();
            window.showToast?.(answer.json?.message || '', 'success');
            await load();
        } finally {
            button.disabled = false;
        }
    };

    const closePackage = (pkg) => {
        window.showConfirm?.(W('Warehouse.Package.Action.Close.Confirm') || L().AreYouSure, async () => {
            const answer = await post(`/Warehouse/Packing/close/${encodeURIComponent(pkg.id)}`, { idempotencyKey: newKey(), ifMatch: pkg.etag });
            if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
            window.showToast?.(answer.json?.message || '', 'success');
            await load();
        }, { entityName: pkg.number, confirmButtonText: W('Warehouse.Package.Action.Close') });
    };

    const bindEvents = () => {
        byId('btnNewPackage')?.addEventListener('click', () => openForm('create', null));
        byId('btnPackageSave')?.addEventListener('click', submitForm);
        byId('packing-packages')?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-package-action]');
            if (!button || !state) return;
            const pkg = state.packages.find((p) => p.id === button.dataset.packageId);
            if (!pkg) return;
            if (button.dataset.packageAction === 'Edit') openForm('edit', pkg);
            else closePackage(pkg);
        });
        byId('package-contents')?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-wh-step]');
            if (!button) return;
            const input = byId(button.dataset.whFor);
            if (input) input.value = inputQty(rules.step(input.value, Number(button.dataset.whStep), input.dataset.max));
        });
    };

    return {
        init: function () {
            if (!root) return;
            bindEvents();
            load();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => PackingPage.init());
