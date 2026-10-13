/**
 * MOD-0173-FU02 L-4 — bulk bin generation (K-12, LOC2), opened from the L-1 detail of a Zone or StorageLocation.
 *
 *   window.LocationBulkBins.open({ target: { id, code, name, type }, onDone })
 *
 * PREVIEW → CONFIRM, one dialog:
 *   - the count (aisles × racks × levels × positions) is shown as the reader types; above 1000 the preview is not
 *     offered (the service answers 400 LOCATION_BULK_LIMIT anyway — pack §13 "before preview");
 *   - Preview = dryRun: NOTHING is written; the codes to create and the codes that already exist (skipped, any
 *     status) are listed;
 *   - Create = the SAME request without dryRun, ONE call, ONE transaction, with the dialog's Idempotency-Key (the
 *     dry run does not consume it — LOC1-06): a double click is a replay, never a second batch;
 *   - changing a value after a preview withdraws the Create button until the reader previews again.
 * Same-origin proxy only (/Locations/bins/{id}); the browser never calls the service.
 */
'use strict';

window.LocationBulkBins = (function () {
    const ui = window.LocationUi;
    const L = ui.L;
    const byId = (id) => document.getElementById(id);
    const MAX = 1000;
    const SHOWN = 60; // codes drawn in the preview; the rest is "and N more"

    let state = null;

    const modal = () => {
        const el = byId('modalBulkBins');
        return el && window.bootstrap?.Modal ? window.bootstrap.Modal.getOrCreateInstance(el) : null;
    };

    const read = () => ({
        aisleFrom: (byId('bulkAisleFrom').value || '').trim().toUpperCase(),
        aisleTo: (byId('bulkAisleTo').value || '').trim().toUpperCase(),
        rackFrom: Number(byId('bulkRackFrom').value),
        rackTo: Number(byId('bulkRackTo').value),
        levelFrom: Number(byId('bulkLevelFrom').value),
        levelTo: Number(byId('bulkLevelTo').value),
        positionsPerLevel: Number(byId('bulkPositions').value),
        isStockable: !!byId('bulkStockable').checked
    });

    // Aisles: one letter A–Z, or 1–3 digits; both ends of the same kind (pack §12).
    const aisleCount = (from, to) => {
        const letter = /^[A-Z]$/;
        const digits = /^[0-9]{1,3}$/;
        if (letter.test(from) && letter.test(to)) return to.charCodeAt(0) - from.charCodeAt(0) + 1;
        if (digits.test(from) && digits.test(to)) return Number(to) - Number(from) + 1;
        return 0;
    };
    const span = (from, to) => (Number.isInteger(from) && Number.isInteger(to) && from >= 1 && to <= 99 && to >= from) ? to - from + 1 : 0;

    const count = (v) => {
        const positions = Number.isInteger(v.positionsPerLevel) && v.positionsPerLevel >= 1 && v.positionsPerLevel <= 99 ? v.positionsPerLevel : 0;
        return Math.max(0, aisleCount(v.aisleFrom, v.aisleTo)) * span(v.rackFrom, v.rackTo) * span(v.levelFrom, v.levelTo) * positions;
    };

    const alert = (html) => {
        const box = byId('bulkAlert');
        if (!box) return;
        box.innerHTML = html || '';
        box.classList.toggle('d-none', !html);
    };

    const withdrawPreview = () => {
        byId('bulkPreview')?.classList.add('d-none');
        byId('btnBulkCreate')?.classList.add('d-none');
        if (state) state.preview = null;
    };

    const refreshCount = () => {
        if (!state || state.done) return;
        withdrawPreview();
        alert('');
        const n = count(read());
        const badge = byId('bulkCount');
        if (badge) badge.textContent = n ? ui.format(L().BulkCount, n) : '';
        const tooMany = n > MAX;
        if (tooMany) alert(ui.esc(L().ErrBulkLimit || ''));
        const preview = byId('btnBulkPreview');
        if (preview) preview.disabled = !n || tooMany;
    };

    const chips = (codes, css) => codes.slice(0, SHOWN).map((code) => `<span class="badge ${css} font-monospace">${ui.esc(code)}</span>`).join('')
        + (codes.length > SHOWN ? `<span class="badge bg-label-secondary">${ui.esc(ui.format(L().BulkMore, codes.length - SHOWN))}</span>` : '');

    const post = (dryRun) => ui.call(`/Locations/bins/${encodeURIComponent(state.target.id)}`, {
        body: Object.assign({ idempotencyKey: state.key, dryRun }, read())
    });

    const preview = async () => {
        if (!state || state.busy) return;
        const form = byId('formBulkBins');
        form?.classList.add('was-validated');
        if (form && !form.checkValidity()) return;
        const n = count(read());
        if (!n || n > MAX) return;
        state.busy = true;
        alert('');
        try {
            const reply = await post(true);
            if (!reply.ok) { alert(ui.withReference(reply.message, reply.correlationId)); return; }
            const toCreate = reply.toCreate || [];
            const skipped = reply.skipped || [];
            state.preview = { toCreate: toCreate.length };
            byId('bulkPreviewSummary').textContent = ui.format(L().BulkPreviewSummary, toCreate.length);
            byId('bulkPreviewCodes').innerHTML = chips(toCreate, 'bg-label-primary');
            byId('bulkPreviewSkipped').classList.toggle('d-none', !skipped.length);
            byId('bulkPreviewSkippedSummary').textContent = ui.format(L().BulkSkipped, skipped.length);
            byId('bulkPreviewSkippedCodes').innerHTML = chips(skipped, 'bg-label-warning');
            byId('bulkPreview').classList.remove('d-none');
            const create = byId('btnBulkCreate');
            create.textContent = toCreate.length ? ui.format(L().BulkCreateButton, toCreate.length) : (L().BulkNothingToCreate || '');
            create.disabled = !toCreate.length;
            create.classList.remove('d-none');
        } finally {
            state.busy = false;
        }
    };

    const create = async () => {
        if (!state || state.busy || !state.preview?.toCreate) return;
        state.busy = true;
        const button = byId('btnBulkCreate');
        if (button) button.disabled = true;
        alert('');
        try {
            const reply = await post(false);
            if (!reply.ok) {
                alert(ui.withReference(reply.message, reply.correlationId));
                if (button) button.disabled = false;
                return;
            }
            state.done = true;
            const result = byId('bulkResult');
            let text = reply.message || ui.format(L().BulkResult, reply.count || 0);
            if ((reply.skipped || []).length) text += ' ' + ui.format(L().BulkResultSkipped, reply.skipped.length);
            result.textContent = text;
            result.classList.remove('d-none');
            byId('bulkPreview')?.classList.add('d-none');
            button?.classList.add('d-none');
            byId('btnBulkPreview')?.classList.add('d-none');
            byId('btnBulkClose').textContent = L().Close || '';
            byId('formBulkBins')?.querySelectorAll('input').forEach((input) => { input.disabled = true; });
            if (typeof state.onDone === 'function') state.onDone(reply);
        } finally {
            state.busy = false;
        }
    };

    const open = (options) => {
        const target = options?.target;
        if (!target?.id || !byId('modalBulkBins')) return;
        if (target.type !== 'Zone' && target.type !== 'StorageLocation') return; // K-12 targets only
        state = { target, key: ui.newKey(), onDone: options.onDone || null, preview: null, busy: false, done: false };

        byId('bulkTarget').textContent = `${target.code || ''} · ${target.name || ''} (${ui.typeText(target.type)})`;
        const defaults = { bulkAisleFrom: 'A', bulkAisleTo: 'A', bulkRackFrom: 1, bulkRackTo: 1, bulkLevelFrom: 1, bulkLevelTo: 1, bulkPositions: 1 };
        Object.entries(defaults).forEach(([id, value]) => { const el = byId(id); el.value = value; el.disabled = false; });
        byId('bulkStockable').checked = true;
        byId('bulkStockable').disabled = false;
        byId('formBulkBins')?.classList.remove('was-validated');
        byId('bulkResult').classList.add('d-none');
        byId('btnBulkPreview').classList.remove('d-none');
        byId('btnBulkClose').textContent = L().Cancel || '';
        refreshCount();
        modal()?.show();
    };

    document.addEventListener('DOMContentLoaded', () => {
        byId('formBulkBins')?.addEventListener('input', refreshCount);
        byId('formBulkBins')?.addEventListener('submit', (e) => { e.preventDefault(); preview(); });
        byId('btnBulkPreview')?.addEventListener('click', preview);
        byId('btnBulkCreate')?.addEventListener('click', create);
    });

    return { open };
})();
