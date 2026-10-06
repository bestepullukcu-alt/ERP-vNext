'use strict';

// MOD-0193 BOM Details — release (Draft → Effective), delete a draft, and the requirement check (explode) on an
// effective version. Every call goes through the same-origin adapter (/Manufacturing/Boms/api/…) with the antiforgery
// header; the adapter forwards the caller's token and answers the screen's words in `message`. Confirmations use the
// premium shared helpers (window.showConfirm / showToast, MOD-0013).
(function () {
    const root = document.querySelector('.bom-details');
    if (!root) return;

    const id = root.getAttribute('data-bom-version-id');
    const legalEntityId = root.getAttribute('data-legal-entity-id');
    const scoped = (path) => `${path}${path.includes('?') ? '&' : '?'}legalEntityId=${encodeURIComponent(legalEntityId)}`;
    const itemId = root.getAttribute('data-item-id');
    const rowVersion = Number(root.getAttribute('data-row-version'));
    const version = root.getAttribute('data-version');
    const L = Object.assign({}, window.L10n || {});
    try {
        const payload = document.getElementById('bom-details-l10n');
        if (payload) Object.assign(L, JSON.parse(payload.textContent || '{}'));
    } catch (e) { console.error('[BOM Details] L10n payload parse failed.', e); }

    const toast = (message, type) => window.showToast?.(message, type);
    const token = () => root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const post = (path, body) => fetch(scoped(path), {
        method: 'POST',
        credentials: 'same-origin',
        headers: Object.assign({ 'RequestVerificationToken': token() }, body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    });
    const failure = async (res) => (await res.json().catch(() => null))?.message || L.ErrorOccurred;

    // Times are stored in UTC; show them on the reader's clock (the server renders UTC as the fallback text).
    root.querySelectorAll('time.js-local-time').forEach((el) => {
        const d = new Date(el.getAttribute('datetime'));
        if (!isNaN(d.getTime())) el.textContent = d.toLocaleString();
    });

    // ── Release ──
    const releaseForm = document.getElementById('bomReleaseForm');
    releaseForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        if (!releaseForm.checkValidity()) {
            releaseForm.classList.add('was-validated');
            return;
        }
        const changeControlRef = document.getElementById('bomChangeControlRef').value.trim();
        const text = (L.ReleaseConfirm || '').replace('{0}', version);
        window.showConfirm?.(text, async () => {
            try {
                const res = await post(`/Manufacturing/Boms/api/${encodeURIComponent(id)}/release`, { changeControlRef, rowVersion });
                if (!res.ok) { toast(await failure(res), 'error'); return; }
                toast(L.ReleaseSuccess, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'danger', confirmButtonText: L.ActionRelease });
    });

    // ── Delete (draft only) ──
    document.getElementById('btnDeleteBom')?.addEventListener('click', () => {
        const text = (L.DeleteConfirm || '').replace('{0}', version);
        window.showConfirm?.(text, async () => {
            try {
                const res = await post(`/Manufacturing/Boms/api/${encodeURIComponent(id)}/delete?rowVersion=${encodeURIComponent(rowVersion)}`);
                if (!res.ok) { toast(await failure(res), 'error'); return; }
                toast(L.RecordDeleted, 'success');
                setTimeout(() => { window.location.href = '/Manufacturing/Boms'; }, 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'danger', confirmButtonText: L.ActionDelete });
    });

    // ── Requirement check (explode the version effective today) ──
    const explodeForm = document.getElementById('bomExplodeForm');
    explodeForm?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const input = document.getElementById('bomExplodeQuantity');
        if (!explodeForm.checkValidity() || Number(input.value) <= 0) {
            input.classList.add('is-invalid');
            toast(L.ErrInvalid, 'error');
            return;
        }
        input.classList.remove('is-invalid');
        try {
            const res = await post(`/Manufacturing/Boms/api/${encodeURIComponent(itemId)}/explode`, { quantity: input.value.trim() });
            if (!res.ok) { toast(await failure(res), 'error'); return; }
            const data = await res.json();
            const result = document.getElementById('bomExplodeResult');
            const body = result.querySelector('tbody');
            body.replaceChildren(...(data.requirements || []).map((r) => {
                const tr = document.createElement('tr');
                [r.componentItemId, r.requiredQuantity, r.uomId].forEach((value, i) => {
                    const td = document.createElement('td');
                    td.textContent = value ?? '';
                    if (i === 0) td.className = 'text-break';
                    if (i === 1) td.className = 'text-end';
                    tr.appendChild(td);
                });
                return tr;
            }));
            result.hidden = false;
        } catch (error) {
            console.error(error);
            toast(L.ErrorOccurred, 'error');
        }
    });
})();
