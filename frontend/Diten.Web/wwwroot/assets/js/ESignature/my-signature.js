'use strict';

/*
 * MOD-0022 E5W1 — İmzam (pack §11.3; mockup v2 "İmzam", v2.app.js:530-568). Page: Views/ESignature/MySignature.cshtml.
 *
 *   GET  /ESignature/api/samples/mine                 → Sample[] (history, newest first; the active one + a pending one)
 *   GET  /ESignature/api/samples/{id}/image           → PNG
 *   POST /ESignature/auth/reauth {intent:"sample"}    → ticket (password ALWAYS — V3); the input is emptied in a finally
 *   POST /ESignature/api/samples                      → Sample (pending when the tenant asks for approval, else active)
 *   POST /ESignature/api/samples/{id}/deactivate | /withdraw
 *
 * Draw: the shared pad (esign-pad.js) + thickness / colour + full-screen landscape. Upload: PNG / JPG ≤ 1 MB, four crop
 * sliders, optional background clearing with a sensitivity threshold, always sent as PNG (kind "uploaded").
 * No secret is stored anywhere (no localStorage / sessionStorage / URL); the ticket lives in one local variable.
 */
(function (global) {
    const E = global.ESign;
    const t = E.t;
    const API = '/ESignature/api';
    const $ = (id) => document.getElementById(id);
    const MAX_BYTES = 1048576;

    const user = global.CurrentUser || {};
    const userName = [user.firstName, user.lastName].filter(Boolean).join(' ') || user.displayName || user.email || '';
    const userLogin = user.email || '';

    let samples = [];
    let table = null;
    let tab = 'draw';
    const upload = { img: null, name: '', size: 0, out: null };

    const minePad = global.ESignPad.attach($('esignMinePad'));
    const fullPad = global.ESignPad.attach($('esignFullPad'));
    minePad.setName(userName);
    fullPad.setName(userName);
    minePad.onChange(() => refreshSave());

    const imageUrl = (s) => `${API}/samples/${encodeURIComponent(s.id)}/image`;
    const fmtL = (f) => t(`ESign.Mine.Format.${f || 'png'}`);
    const kindL = (k) => t(`ESign.Mine.Kind.${k || 'drawn'}`);
    // minute precision in the page language (the mockup's history column); slicing formatTime's text is locale-dependent
    const at = (utc) => {
        const d = utc ? new Date(utc) : null;
        if (!d || Number.isNaN(d.getTime())) return '—';
        return d.toLocaleString(document.documentElement.getAttribute('lang') || undefined, { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    };

    // ── load + render ──────────────────────────────────────────────────────────────────────────────────────────
    async function load() {
        const r = await E.call('GET', `${API}/samples/mine`);
        $('esignMineLoading').classList.add('d-none');
        if (!r.ok) {
            failed(r);
            samples = [];
        } else {
            samples = (E.dataOf(r) || []).slice().sort((a, b) => String(b.createdAtUtc).localeCompare(String(a.createdAtUtc)));
        }
        renderMine();
        renderHistory();
    }

    function failed(r) {
        const code = E.codeOf(r);
        const text = r.status === 403 && !code ? t('ESign.Dialog.Err.Forbidden') : E.message(code);
        if (global.DitenModal) global.DitenModal.error({ title: t('ESign.Dialog.Err.Title'), message: text, confirmButtonText: t('ESign.Dialog.Close') });
    }

    function renderMine() {
        const active = samples.find((s) => s.status === 'active');
        const pending = samples.find((s) => s.status === 'pending');
        $('esignMineHas').classList.toggle('d-none', !active);
        $('esignMineEmpty').classList.toggle('d-none', !!active);
        $('esignMineActive').classList.toggle('d-none', !active);
        if (active) {
            $('esignMineImage').setAttribute('src', imageUrl(active));
            $('esignMineId').textContent = active.sampleNumber || active.id;
            $('esignMineAt').textContent = `${at(active.createdAtUtc)} · ${kindL(active.kind)}`;
            $('esignMineFormat').textContent = fmtL(active.format);
            $('esignMineApprover').textContent = active.decision && active.decision.byName ? active.decision.byName : t('ESign.Mine.NoApprovalNeeded');
            $('esignMineUsed').textContent = t('ESign.Mine.UsedIn', active.usedCount || 0);
            $('esignMineZoom').onclick = () => zoomSample(active);
        }
        $('esignMinePending').classList.toggle('d-none', !pending);
        if (pending) {
            $('esignMinePendingText').textContent = t('ESign.Mine.PendingInfo', pending.sampleNumber || '',
                active ? t('ESign.Mine.PendingKeep') : t('ESign.Mine.PendingNone'));
        }
    }

    function zoomSample(s) {
        E.zoom({
            img: imageUrl(s), signer: (s.owner && s.owner.name) || userName, login: (s.owner && s.owner.login) || userLogin,
            type: t('ESign.Zoom.Sample', kindL(s.kind)), at: E.formatTime(s.createdAtUtc, true), storage: fmtL(s.format),
            hash: s.imageHash ? `sha256:${s.imageHash}` : '—', sub: s.sampleNumber || s.id
        });
    }

    const statusClass = { active: 'bg-label-success', inactive: 'bg-label-secondary', pending: 'bg-label-warning', rejected: 'bg-label-danger', withdrawn: 'bg-label-secondary' };

    function renderHistory() {
        const el = $('dt-esign-samples');
        const rows = samples.map((s) => ({ ...s, _at: at(s.createdAtUtc) }));
        if (table) {
            table.clear().rows.add(rows).draw();
            return;
        }
        const esc = E.escapeHtml;
        table = new global.DataTable(el, global.DtDefaults.create({
            data: rows,
            stateSave: false,
            order: [],
            columns: [
                { data: 'id', orderable: false, searchable: false, render: (d, type, row) => type === 'display'
                    ? `<button type="button" class="border rounded bg-white p-1" data-esign-zoom-sample="${esc(row.id)}" title="${esc(t('ESign.Zoom.Enlarge'))}"><img alt="" src="${esc(imageUrl(row))}" style="max-height:32px;max-width:110px"></button>` : '' },
                { data: 'sampleNumber', render: (d) => `<span class="font-monospace small">${esc(d || '')}</span>` },
                { data: '_at' },
                { data: 'kind', render: (d) => esc(kindL(d)) },
                { data: 'format', render: (d) => esc(fmtL(d)) },
                { data: 'usedCount', render: (d) => (d ? esc(t('ESign.Mine.UsedCount', d)) : '—') },
                { data: 'status', render: (d, type, row) => type === 'display'
                    ? `<span class="badge ${statusClass[d] || 'bg-label-secondary'}">${esc(t(`ESign.Mine.Status.${d}`))}</span>`
                      + (d === 'rejected' && row.decision && row.decision.reason ? `<div class="small text-muted mt-1">${esc(t('ESign.Mine.RejectReason', row.decision.reason))}</div>` : '')
                    : d },
                { data: null, orderable: false, searchable: false, className: 'text-end pe-3', render: (d, type, row) => type === 'display' ? actions(row) : '' }
            ]
        }));
        el.addEventListener('click', (e) => {
            const zoomBtn = e.target.closest('[data-esign-zoom-sample]');
            if (zoomBtn) { const s = samples.find((x) => x.id === zoomBtn.getAttribute('data-esign-zoom-sample')); if (s) zoomSample(s); return; }
            const act = e.target.closest('[data-esign-sample-action]');
            if (act) act.getAttribute('data-esign-sample-action') === 'deactivate' ? deactivate(act.getAttribute('data-id')) : withdraw(act.getAttribute('data-id'));
        });
        const skeleton = $('skeleton-loader');
        if (skeleton) skeleton.remove();
    }

    /** Pasif yap (active) · Sil (unused, not active → withdraw) · "Silinemez" (used) — mockup v2.app.js:544, V22. */
    function actions(s) {
        const esc = E.escapeHtml;
        const parts = [];
        if (s.status === 'active') parts.push(`<button type="button" class="btn btn-sm btn-label-secondary" data-esign-sample-action="deactivate" data-id="${esc(s.id)}">${esc(t('ESign.Mine.Deactivate'))}</button>`);
        if ((s.usedCount || 0) === 0 && (s.status === 'pending' || s.status === 'inactive' || s.status === 'rejected')) {
            parts.push(`<button type="button" class="btn btn-sm btn-label-danger" data-esign-sample-action="withdraw" data-id="${esc(s.id)}">${esc(t('ESign.Mine.Delete'))}</button>`);
        }
        if ((s.usedCount || 0) > 0 && s.status !== 'active') parts.push(`<span class="text-muted small" title="${esc(t('ESign.Mine.LockedTitle'))}"><i class="bx bx-lock-alt me-1"></i>${esc(t('ESign.Mine.Locked'))}</span>`);
        return `<div class="d-inline-flex gap-2 align-items-center">${parts.join('')}</div>`;
    }

    function deactivate(id) {
        global.showConfirm(t('ESign.Mine.Confirm.Deactivate'), async () => {
            const r = await E.call('POST', `${API}/samples/${encodeURIComponent(id)}/deactivate`);
            if (!r.ok) { failed(r); return; }
            if (global.showToast) global.showToast(t('ESign.Mine.Toast.Deactivated'), 'success');
            await load();
        }, { type: 'warning', subtext: t('ESign.Mine.Confirm.DeactivateText'), confirmButtonText: t('ESign.Mine.Deactivate'), cancelButtonText: t('ESign.Dialog.Cancel') });
    }

    function withdraw(id) {
        global.showConfirm(t('ESign.Mine.Confirm.Delete'), async () => {
            const r = await E.call('POST', `${API}/samples/${encodeURIComponent(id)}/withdraw`);
            if (!r.ok) { failed(r); return; }
            if (global.showToast) global.showToast(t('ESign.Mine.Toast.Withdrawn'), 'success');
            await load();
        }, { type: 'danger', subtext: t('ESign.Mine.Confirm.DeleteText'), confirmButtonText: t('ESign.Mine.Delete'), cancelButtonText: t('ESign.Dialog.Cancel') });
    }

    // ── draw: thickness, colour, full screen ────────────────────────────────────────────────────────────────────────
    document.querySelectorAll('[data-esign-pen-width]').forEach((b) => b.addEventListener('click', () => {
        document.querySelectorAll('[data-esign-pen-width]').forEach((x) => x.classList.toggle('active', x === b));
        minePad.setPen({ width: b.getAttribute('data-esign-pen-width') });
        fullPad.setPen({ width: b.getAttribute('data-esign-pen-width') });
    }));
    document.querySelectorAll('[data-esign-pen-color]').forEach((b) => b.addEventListener('click', () => {
        document.querySelectorAll('[data-esign-pen-color]').forEach((x) => x.classList.toggle('active', x === b));
        minePad.setPen({ color: b.getAttribute('data-esign-pen-color') });
        fullPad.setPen({ color: b.getAttribute('data-esign-pen-color') });
    }));

    $('esignOpenFull').addEventListener('click', () => {
        $('esignFull').classList.remove('d-none');
        fullPad.load(minePad.strokes());
        try {
            const d = document.documentElement;
            const req = d.requestFullscreen && d.requestFullscreen();
            if (req && req.then) {
                req.then(() => { if (global.screen.orientation && global.screen.orientation.lock) global.screen.orientation.lock('landscape').catch(() => {}); }).catch(() => {});
            }
        } catch (_) { /* full screen is a convenience */ }
        global.setTimeout(() => fullPad.redraw(), 50);
    });
    function closeFull(apply) {
        if (apply) minePad.load(fullPad.strokes());
        $('esignFull').classList.add('d-none');
        try { if (document.fullscreenElement) document.exitFullscreen(); } catch (_) { /* ignore */ }
        global.setTimeout(() => minePad.redraw(), 50);
    }
    $('esignFullApply').addEventListener('click', () => closeFull(true));
    $('esignFullCancel').addEventListener('click', () => closeFull(false));

    // ── tabs ───────────────────────────────────────────────────────────────────────────────────────────────────
    $('esignTabDraw').addEventListener('shown.bs.tab', () => { tab = 'draw'; $('esignStoreText').textContent = t('ESign.Mine.Store.Drawn'); minePad.redraw(); refreshSave(); });
    $('esignTabUpload').addEventListener('shown.bs.tab', () => { tab = 'upload'; $('esignStoreText').textContent = t('ESign.Mine.Store.Uploaded'); refreshSave(); });

    // ── upload: PNG / JPG ≤ 1 MB, crop, background ─────────────────────────────────────────────────────────────────
    function uploadError(text) {
        $('esignUploadError').textContent = text || '';
        $('esignUploadError').classList.toggle('d-none', !text);
    }
    $('esignUploadFile').addEventListener('change', (e) => {
        const file = e.target.files && e.target.files[0];
        e.target.value = '';
        if (!file) return;
        if (!/^image\/(png|jpeg)$/.test(file.type)) { uploadError(t('ESign.Mine.Upload.ErrType')); return; }
        if (file.size > MAX_BYTES) { uploadError(t('ESign.Mine.Upload.ErrSize')); return; }
        uploadError('');
        const reader = new FileReader();
        reader.onload = () => {
            const img = new Image();
            img.onload = () => {
                upload.img = img;
                upload.name = file.name;
                upload.size = file.size;
                $('esignUploadSource').setAttribute('src', reader.result);
                $('esignUploadName').textContent = file.name;
                $('esignUploadMeta').textContent = `${Math.max(1, Math.round(file.size / 1024))} KB · ${img.naturalWidth}×${img.naturalHeight}`;
                $('esignUploadHas').classList.remove('d-none');
                $('esignUploadPick').classList.add('d-none');
                processUpload();
            };
            img.src = reader.result;
        };
        reader.readAsDataURL(file);
    });
    $('esignUploadRemove').addEventListener('click', () => {
        upload.img = null; upload.out = null;
        $('esignUploadHas').classList.add('d-none');
        $('esignUploadPick').classList.remove('d-none');
        refreshSave();
    });
    document.querySelectorAll('[data-esign-crop]').forEach((r) => r.addEventListener('input', processUpload));
    $('esignUploadBg').addEventListener('change', () => { $('esignUploadThrWrap').classList.toggle('d-none', !$('esignUploadBg').checked); processUpload(); });
    $('esignUploadThr').addEventListener('input', processUpload);

    /** Crops (percent per side), optionally turns light pixels transparent, scales into ≤ 2000×800, PNG (V8). */
    function processUpload() {
        const img = upload.img;
        if (!img) return;
        const pct = (id) => Number($(id).value) / 100;
        const l = pct('esignCropL'), r = pct('esignCropR'), tp = pct('esignCropT'), b = pct('esignCropB');
        const sw = Math.max(1, Math.round(img.naturalWidth * (1 - l - r)));
        const sh = Math.max(1, Math.round(img.naturalHeight * (1 - tp - b)));
        const scale = Math.min(1, 2000 / sw, 800 / sh);
        const cv = document.createElement('canvas');
        cv.width = Math.max(1, Math.round(sw * scale));
        cv.height = Math.max(1, Math.round(sh * scale));
        const ctx = cv.getContext('2d');
        ctx.drawImage(img, img.naturalWidth * l, img.naturalHeight * tp, sw, sh, 0, 0, cv.width, cv.height);
        if ($('esignUploadBg').checked) {
            const thr = Number($('esignUploadThr').value);
            const data = ctx.getImageData(0, 0, cv.width, cv.height);
            for (let i = 0; i < data.data.length; i += 4) {
                const lum = 0.299 * data.data[i] + 0.587 * data.data[i + 1] + 0.114 * data.data[i + 2];
                if (lum >= thr) data.data[i + 3] = 0;
            }
            ctx.putImageData(data, 0, 0);
        }
        upload.out = cv.toDataURL('image/png');
        $('esignUploadResult').setAttribute('src', upload.out);
        const box = $('esignUploadCropBox');
        box.style.left = `${l * 100}%`; box.style.right = `${r * 100}%`; box.style.top = `${tp * 100}%`; box.style.bottom = `${b * 100}%`;
        refreshSave();
    }

    function refreshSave() {
        $('esignSaveSample').disabled = tab === 'draw' ? minePad.isEmpty() : !upload.out;
    }

    // ── save with password (intent sample) ─────────────────────────────────────────────────────────────────────────
    const pwdModalEl = $('esignSamplePwd');
    const pwdModal = global.bootstrap.Modal.getOrCreateInstance(pwdModalEl);
    const clearSamplePassword = () => { $('esignSamplePassword').value = ''; };
    pwdModalEl.addEventListener('hidden.bs.modal', clearSamplePassword);
    $('esignSamplePassword').addEventListener('input', () => { $('esignSamplePassword').classList.remove('is-invalid'); });

    $('esignSaveSample').addEventListener('click', () => {
        if ($('esignSaveSample').disabled) return;
        $('esignSampleUser').value = [userLogin, userName].filter(Boolean).join(' · ');
        clearSamplePassword();
        $('esignSamplePwdFormError').classList.add('d-none');
        pwdModal.show();
        global.setTimeout(() => $('esignSamplePassword').focus(), 250);
    });

    $('esignSamplePwdForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        const input = $('esignSamplePassword');
        const err = (text) => { input.classList.toggle('is-invalid', !!text); $('esignSamplePasswordError').textContent = text || ''; };
        const formErr = (text) => { $('esignSamplePwdFormError').textContent = text || ''; $('esignSamplePwdFormError').classList.toggle('d-none', !text); };
        if (!input.value) { err(t('ESign.Dialog.Err.Password')); return; }
        const button = $('esignSamplePwdSubmit');
        button.disabled = true;
        let auth;
        try {
            auth = await E.call('POST', '/ESignature/auth/reauth', { password: input.value, intent: 'sample', openWindowMinutes: null });
        } finally {
            clearSamplePassword();
            button.disabled = false;
        }
        if (!auth.ok) {
            const code = E.codeOf(auth);
            const params = E.paramsOf(auth);
            if (code === 'ESIGN_REAUTH_FAILED' && params.remainingAttempts != null) err(t('ESign.Dialog.Err.WrongPassword', params.remainingAttempts));
            else formErr(E.message(code));
            return;
        }
        const ticket = (E.dataOf(auth) || {}).ticket;
        const body = tab === 'draw'
            ? { ticket, kind: 'drawn', pngBase64: minePad.png(), strokes: minePad.strokes(), idempotencyKey: E.newKey() }
            : { ticket, kind: 'uploaded', pngBase64: upload.out.split(',')[1], strokes: null, idempotencyKey: E.newKey() };
        const saved = await E.call('POST', `${API}/samples`, body);
        if (!saved.ok) { formErr(E.message(E.codeOf(saved))); return; }
        pwdModal.hide();
        const sample = E.dataOf(saved) || {};
        if (global.showToast) global.showToast(t(sample.status === 'pending' ? 'ESign.Mine.Toast.SavedPending' : 'ESign.Mine.Toast.Saved'), 'success');
        minePad.clear();
        $('esignUploadRemove').click();
        await load();
    });

    load();
})(window);
