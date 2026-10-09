/**
 * MOD-0174 P3 — Durum Değiştir (pack §11.3, K-E). ONE dialog implementation: window.showConfirm (no raw Swal.fire). The
 * body is the localized _StatusDialog template: current status, the targets with their allowed / disallowed reason
 * (Expired = "Sistem verir; elle seçilemez"), the Blocked impact line + the required acknowledgement; the reason (≥ 10)
 * is the dialog's input. The post carries the lot's ETag (If-Match) and a key minted when the dialog opened.
 * The service decides (transition, reason, acknowledgement); its refusal is shown in the reader's language.
 */
'use strict';

window.LotStatus = (function () {
    const ui = window.TraceUi;
    const t = ui.t;

    const impactText = (stock) => {
        if (!stock) return '';
        const units = {};
        (stock.rows || []).forEach((row) => {
            const value = Number(row.onHand);
            if (!Number.isFinite(value) || value <= 0) return;
            units[row.baseUomId || ''] = (units[row.baseUomId || ''] || 0) + value;
        });
        // Units are never added across base units (pack §11.3): one figure per unit.
        const amount = Object.entries(units).map(([uom, n]) => `${ui.qty(n)} ${uom}`.trim()).join(' · ') || '0';
        return t('Status.Dialog.Impact', amount, stock.locations || 0, (stock.warehouses || []).join(', '));
    };

    const open = async (lotId, options) => {
        const reply = await ui.getJson(`/Lots/data/${encodeURIComponent(lotId)}`);
        if (!reply.ok) {
            window.showConfirm?.(t('Status.Dialog.Title'), null, { type: 'error', subtext: ui.esc(ui.errorText(reply)), hideIcon: false });
            return;
        }
        const data = reply.json;
        const template = document.getElementById('lot-status-template');
        if (!template) return;
        const body = template.content.firstElementChild.cloneNode(true);
        body.querySelector('[data-current]').textContent = t('Status.' + data.lot.status);
        const optionsBox = body.querySelector('[data-options]');
        (data.statusOptions || []).forEach((option, index) => {
            const id = `lotStatusOption${index}`;
            const row = document.createElement('div');
            row.className = 'form-check';
            row.innerHTML = `<input class="form-check-input" type="radio" name="lotStatusTarget" id="${id}" value="${ui.esc(option.status)}"${option.allowed ? '' : ' disabled'}>`
                + `<label class="form-check-label" for="${id}">${ui.esc(t('Status.' + option.status))}`
                + (option.reason ? ` <small class="text-muted">— ${ui.esc(option.reason)}</small>` : '') + '</label>';
            optionsBox.append(row);
        });
        body.querySelector('[data-impact]').textContent = impactText(data.stock);

        const key = (window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random()}`).toString();
        let popupRef = null;
        const chosen = () => popupRef?.querySelector('input[name="lotStatusTarget"]:checked')?.value || '';

        window.showConfirm?.(t('Status.Dialog.Title'), async (reason) => {
            const target = chosen();
            const token = ui.antiforgery(document.getElementById('lots-antiforgery'));
            const result = await ui.postJson(`/Lots/status/${encodeURIComponent(lotId)}`, {
                idempotencyKey: key,
                toStatus: target,
                reason: (reason || '').trim(),
                acknowledgeImpact: !!popupRef?.querySelector('#lotStatusAck')?.checked,
                ifMatch: data.etag
            }, token);
            if (result.ok && result.json?.pending) {
                // MOD-0175 KQ-I (pack §11.4): 202 — the release waits for a quality approval; the lot is unchanged. The requester
                // signs and sends the decision on the Karar Gönder page (pendingRequestId).
                window.showToast?.(t('Status.Dialog.Pending.Title'), 'success');
                const submitUrl = result.json.submitUrl;
                window.showConfirm?.(t('Status.Dialog.Pending.Title'), () => { if (submitUrl) window.location.href = submitUrl; }, {
                    type: 'success',
                    subtext: ui.esc(t('Status.Dialog.Pending.Text')),
                    confirmButtonText: t('Status.Dialog.Pending.Link'),
                    cancelButtonText: t('Cancel')
                });
                options?.onDone?.(result.json);
                return;
            }
            if (result.ok && result.json?.success) {
                window.showToast?.(t('Status.Dialog.Done'), 'success');
                options?.onDone?.(result.json);
                return;
            }
            window.showConfirm?.(t('Status.Dialog.Title'), null, { type: 'error', subtext: ui.esc(ui.errorText(result)) });
        }, {
            type: 'warning',
            subtext: body.outerHTML,
            confirmButtonText: t('Status.Dialog.Confirm'),
            cancelButtonText: t('Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Status.Dialog.Reason'),
            inputAttributes: { maxlength: 500 },
            inputValidator: (value) => {
                const target = chosen();
                if (!target) return t('Status.Dialog.PickOne');
                if (target === 'Blocked' && !popupRef?.querySelector('#lotStatusAck')?.checked) return t('Status.Dialog.AcknowledgeRequired');
                if ((value || '').trim().length < 10) return t('Validation.ReasonMin');
                return null;
            },
            didOpen: (popup, swal) => {
                popupRef = popup;
                // Blocked = danger: the impact line and the acknowledgement appear, the confirm button names the act.
                popup.querySelectorAll('input[name="lotStatusTarget"]').forEach((radio) => radio.addEventListener('change', () => {
                    const blocking = chosen() === 'Blocked';
                    popup.querySelector('[data-danger]')?.classList.toggle('d-none', !blocking);
                    const confirm = swal.getConfirmButton();
                    if (confirm) {
                        confirm.textContent = blocking ? t('Status.Dialog.ConfirmBlock') : t('Status.Dialog.Confirm');
                        confirm.classList.toggle('btn-danger', blocking);
                    }
                }));
            }
        });
    };

    return { open };
})();
