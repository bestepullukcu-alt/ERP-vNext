'use strict';

/*
 * MOD-0022 E5W2 — what the e-signature pages share (Requests, Verification, AuditExport, Policy, Meanings, SampleApprovals and
 * the İmzam history). Loaded after esign-dialog.js (window.ESign: t, call, dataOf, codeOf, message, formatTime, escapeHtml).
 *
 *   • the list component's words (Views/ESignature/_ESignatureListL10n.cshtml) → window.L10n (createList reads them);
 *   • can(key) — display only (UAS-001 §4): the server's [HasPermission] stays the authority;
 *   • reason(…) — the red SweetAlert2 confirm with a reason of at least 10 characters (premium-modal-standard: window.showConfirm;
 *     emsal Traceability/LotMigration/index.js:240-283);
 *   • the vocabulary families every page renders (module, status, visual mode) and the cached meaning / company look-ups.
 */
(function (global) {
    const E = global.ESign;
    const t = E.t;
    const API = '/ESignature/api';
    const REASON_MIN = 10;
    const REASON_MAX = 1000;

    (function mergeListL10n() {
        const el = global.document.getElementById('esign-list-l10n');
        if (!el) return;
        try {
            global.L10n = Object.assign({}, global.L10n || {}, JSON.parse(el.textContent || '{}'));
        } catch (error) {
            console.error('[ESign] list vocabulary could not be parsed.', error);
        }
    })();

    const can = (key) => global.Permissions?.has?.(key) === true;
    const lang = ((global.document.documentElement.getAttribute('lang') || 'tr').slice(0, 2) || 'tr').toLowerCase();
    const esc = E.escapeHtml;

    /** Module code → its label (ESign.Module.{code without '-'}), else the code itself. */
    function moduleLabel(code) {
        const key = `ESign.Module.${String(code || '').replace(/[^a-z0-9]/gi, '').toLowerCase()}`;
        return (global.ESignL10n || {})[key] ? t(key) : String(code || '');
    }

    const verifyClass = { valid: 'bg-label-success', changed: 'bg-label-warning', invalidated: 'bg-label-danger', tampered: 'bg-label-danger', subjectUnresolvable: 'bg-label-secondary' };
    const verifyIcon = { valid: 'bx-check-shield', changed: 'bx-git-compare', invalidated: 'bx-block', tampered: 'bx-error', subjectUnresolvable: 'bx-help-circle' };
    function verifyBadge(status) {
        if (!status) return '';
        return `<span class="badge ${verifyClass[status] || 'bg-label-secondary'}"><i class="bx ${verifyIcon[status] || 'bx-shield'} me-1"></i>${esc(t(`ESign.Verify.Status.${status}`))}</span>`;
    }

    /** Minute precision in the page language with the offset (pack §9). */
    function at(utc) {
        return E.formatTime(utc, true);
    }

    function failed(r, fallbackKey) {
        const code = E.codeOf(r);
        const text = r.status === 403 && !code ? t('ESign.Dialog.Err.Forbidden') : (code ? E.message(code) : t(fallbackKey || 'ESign.Dialog.Err.Generic'));
        if (global.DitenModal) global.DitenModal.error({ title: t('ESign.Dialog.Err.Title'), message: text, confirmButtonText: t('ESign.Dialog.Close') });
        return text;
    }

    function toast(text, type) {
        if (global.showToast) global.showToast(text, type || 'success');
    }

    /**
     * The reasoned confirm: red (type danger) unless told otherwise, a textarea of 10–1000 characters, the reason or null.
     * `lines` (optional) are shown as a list under the question (the policy save's change lines).
     */
    function reason({ title, subtext, lines, confirmText, type, placeholder }) {
        return new Promise((resolve) => {
            if (typeof global.showConfirm !== 'function') { resolve(null); return; }
            const list = Array.isArray(lines) && lines.length
                ? `<ul class="text-start small mb-0 mt-2">${lines.map((l) => `<li>${esc(l)}</li>`).join('')}</ul>` : '';
            let settled = false;
            global.showConfirm(title, (value) => {
                settled = true;
                resolve(String(value || '').trim());
            }, {
                type: type || 'danger',
                subtext: (subtext ? esc(subtext) : '') + list,
                confirmButtonText: confirmText,
                cancelButtonText: t('ESign.Dialog.Cancel'),
                showInput: true,
                inputType: 'textarea',
                inputLabel: t('ESign.Common.Reason'),
                inputPlaceholder: placeholder || t('ESign.Common.ReasonPlaceholder'),
                inputAttributes: { maxlength: REASON_MAX },
                inputValidator: (value) => (String(value || '').trim().length < REASON_MIN ? t('ESign.Common.ReasonMin', REASON_MIN) : null),
                onCancel: () => { if (!settled) { settled = true; resolve(null); } }
            });
        });
    }

    /** A plain confirm (approve, activate …) → true / false. */
    function confirm({ title, subtext, confirmText, type }) {
        return new Promise((resolve) => {
            if (typeof global.showConfirm !== 'function') { resolve(false); return; }
            let settled = false;
            global.showConfirm(title, () => { settled = true; resolve(true); }, {
                type: type || 'info',
                subtext: subtext ? esc(subtext) : '',
                confirmButtonText: confirmText,
                cancelButtonText: t('ESign.Dialog.Cancel'),
                onCancel: () => { if (!settled) { settled = true; resolve(false); } }
            });
        });
    }

    let meaningsPromise = null;
    /** code → name in the reader's language (all meanings; [] without esign.meanings.read / esign.signatures.sign). */
    function meanings() {
        if (!meaningsPromise) {
            meaningsPromise = E.call('GET', `${API}/meanings`).then((r) => (r.ok ? (E.dataOf(r) || []) : []));
        }
        return meaningsPromise;
    }
    function meaningName(list, code) {
        const m = (list || []).find((x) => x.code === code);
        if (!m) return code || '';
        const names = m.names || {};
        return names[lang] || names.tr || names.en || m.code;
    }

    let companiesPromise = null;
    /** [{ id, name }] — the tenant's companies (MDM lookup through /ESignature/companies); [] when unavailable. */
    function companies() {
        if (!companiesPromise) {
            companiesPromise = E.call('GET', '/ESignature/companies').then((r) => (r.ok ? (E.dataOf(r) || []) : []));
        }
        return companiesPromise;
    }

    /** Fills a <select> with { value, text } options after an optional "all" option. */
    function fill(select, items, allText) {
        if (!select) return;
        select.innerHTML = '';
        if (allText !== undefined) {
            const o = global.document.createElement('option');
            o.value = '';
            o.textContent = allText;
            select.appendChild(o);
        }
        (items || []).forEach((item) => {
            const o = global.document.createElement('option');
            o.value = item.value;
            o.textContent = item.text;
            select.appendChild(o);
        });
    }

    global.ESignPages = Object.freeze({ API, REASON_MIN, can, lang, moduleLabel, verifyBadge, at, failed, toast, reason, confirm, meanings, meaningName, companies, fill });
})(window);
