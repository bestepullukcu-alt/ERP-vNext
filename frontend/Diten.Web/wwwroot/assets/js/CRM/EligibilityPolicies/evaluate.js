/**
 * SCMM-11-UI (CAND-CAP-0011) eligibility evaluate/preview panel. Read-only: pick a policy, build a context, POST
 * eligibility:evaluate through the same-origin proxy, and render the disjoint result. The resolver decides — this only
 * sends the context and shows the outcome. A non-Eligible result is NEVER silent (badge + reason + per-condition table).
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;
    const L = window.PolicyL10n || window.L10n || {};
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const api = '/CRM/EligibilityPolicies/api';

    const stateTone = s => ({ eligible: 'success', blocked: 'danger', unresolved: 'warning' }[String(s || '').toLowerCase()] || 'secondary');
    const stateLabel = s => {
        const k = String(s || '').toLowerCase();
        return { eligible: L.Eligible, blocked: L.Blocked, unresolved: L.Unresolved }[k] || s || '—';
    };
    const blockingLabel = b => {
        const k = String(b || '').toLowerCase();
        return { context: L.BlockingLevelContext, policy: L.BlockingLevelPolicy }[k] || b || '';
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (!$ || !$.fn.select2) return;

        if (window.flatpickr) { const at = document.getElementById('evalAt'); if (at) window.flatpickr(at, { dateFormat: 'Y-m-d', allowInput: true }); }

        // Policy picker — searchable select2 over the ready list endpoint (no raw id entry).
        const picker = document.getElementById('evalPolicyPicker');
        if (picker) {
            $(picker).select2({
                width: '100%', placeholder: picker.getAttribute('data-placeholder') || '', allowClear: true, minimumInputLength: 0,
                ajax: {
                    dataType: 'json', delay: 250, data: params => ({ term: params.term || '' }),
                    transport: (params, success, failure) => {
                        params.url = `${picker.getAttribute('data-url')}?search=${encodeURIComponent(params.data.term || '')}&includeArchived=false`;
                        const req = $.ajax(params); req.then(success); req.fail(failure); return req;
                    },
                    processResults: body => {
                        const items = (body && body.data && (body.data.items || body.data)) || [];
                        return { results: (Array.isArray(items) ? items : []).map(p => ({
                            id: p.eligibilityPolicyId,
                            text: [p.policyCode, p.policyName].filter(Boolean).join(' — ') + (p.policyVersion ? ` (v${p.policyVersion})` : '')
                        })).filter(r => r.id) };
                    }
                }
            });
        }

        const initCtxRow = row => {
            $(row).find('.eval-ctx-dim').each(function () {
                if (!$(this).hasClass('select2-hidden-accessible')) $(this).select2({ width: '100%', minimumResultsForSearch: Infinity, placeholder: this.getAttribute('data-placeholder') || '' });
            });
            $(row).find('.eval-ctx-values').each(function () {
                if (!$(this).hasClass('select2-hidden-accessible')) $(this).select2({ width: '100%', tags: true, tokenSeparators: [','], placeholder: this.getAttribute('data-placeholder') || '' });
            });
        };
        document.querySelectorAll('#evalContextRows .eval-ctx-row').forEach(initCtxRow);
        $('#evalPinned').select2({ width: '100%', tags: true, tokenSeparators: [','], placeholder: document.getElementById('evalPinned')?.getAttribute('data-placeholder') || '' });

        const rowsHost = document.getElementById('evalContextRows');
        document.getElementById('evalAddContext')?.addEventListener('click', () => {
            const first = rowsHost.querySelector('.eval-ctx-row');
            const clone = first.cloneNode(true);
            // Strip any select2 artifacts from the clone, reset values.
            clone.querySelectorAll('.select2-container').forEach(n => n.remove());
            clone.querySelectorAll('select').forEach(s => { s.classList.remove('select2-hidden-accessible'); s.removeAttribute('data-select2-id'); s.value = ''; $(s).find('option[selected]').prop('selected', false); if (s.multiple) s.innerHTML = ''; });
            rowsHost.appendChild(clone);
            initCtxRow(clone);
        });
        rowsHost?.addEventListener('click', event => {
            const rm = event.target.closest('.eval-ctx-remove');
            if (!rm) return;
            event.preventDefault();
            if (rowsHost.querySelectorAll('.eval-ctx-row').length > 1) rm.closest('.eval-ctx-row').remove();
        });

        document.getElementById('btnEvaluate')?.addEventListener('click', () => void runEvaluate());
    });

    const showResult = result => {
        document.getElementById('evalResultEmpty')?.classList.add('d-none');
        const host = document.getElementById('evalResult');
        host.classList.remove('d-none');
        const badge = document.getElementById('evalStateBadge');
        badge.className = `badge bg-label-${stateTone(result.state)}`;
        badge.textContent = stateLabel(result.state);
        document.getElementById('evalBlockingLevel').textContent = result.blockingLevel ? blockingLabel(result.blockingLevel) : '';
        document.getElementById('evalReason').textContent = result.reason || '';
        const tbody = document.getElementById('evalConditions');
        tbody.innerHTML = (result.conditions || []).map(c =>
            `<tr><td>${esc(c.dimension)}</td><td>${esc(c.match)}</td>`
            + `<td><span class="badge bg-label-${stateTone(c.state)}">${esc(stateLabel(c.state))}</span></td>`
            + `<td class="text-muted small">${esc(c.reason || '')}</td></tr>`).join('')
            || `<tr><td colspan="4" class="text-muted small">—</td></tr>`;
    };

    async function runEvaluate() {
        const policyId = document.getElementById('evalPolicyPicker')?.value;
        if (!policyId) { window.showToast?.(L.SelectPolicyFirst, 'error'); return; }

        const context = [];
        document.querySelectorAll('#evalContextRows .eval-ctx-row').forEach(row => {
            const dim = row.querySelector('.eval-ctx-dim')?.value;
            const values = $(row.querySelector('.eval-ctx-values')).val() || [];
            if (dim && values.length) context.push({ dimension: dim, values });
        });
        if (!context.length) { window.showToast?.(L.ContextRequired, 'error'); return; }

        const pinned = $('#evalPinned').val() || [];
        const at = document.getElementById('evalAt')?.value || null;
        const payload = { policyId, context, pinnedSelections: pinned.length ? pinned : null, at: at || null };

        try {
            const res = await fetch(`${api}/eligibility:evaluate`, {
                method: 'POST', credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const body = await res.json().catch(() => ({}));
            if (!res.ok || !body.data) throw new Error((body.errors || [L.ErrorState]).join(' · '));
            showResult(body.data);
        } catch (err) {
            window.showToast?.(err.message || L.ErrorState, 'error');
        }
    }
})(window, document);
