/**
 * WP-VW-W3 (W3-WEB-c) — what the editor and the record page share: the same-origin API (no token is ever built here; the
 * MVC proxy carries the HttpOnly session), escaping, the culture's date format, the category chip (code → tone; label from
 * BRD), the status badge, the error text (coded CRM refusals; 409 = someone else saved; 503 = a dependency is down) and
 * the rep's remote preview. Browser: window.ObjectionPage.
 */
(function (window, document) {
    'use strict';
    const C = window.ObjectionCore;
    const L = window.ObjectionL10n || {};

    const esc = v => String(v == null ? '' : v).replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const lang = () => document.documentElement.lang || undefined;

    const date = v => {
        if (!v) return '';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString(lang(), { day: 'numeric', month: 'short', year: 'numeric' });
    };
    const dateTime = v => {
        if (!v) return '';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? '' : d.toLocaleString(lang(), { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    };

    class ApiError extends Error {
        constructor(status, errors) {
            super((errors && errors[1]) || String(status));
            this.status = status;
            this.errors = errors || [];
        }
    }

    const call = async (method, url, body) => {
        const init = { method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (body !== undefined) {
            init.headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(body);
        }
        const res = await window.fetch(url, init);
        let json = null;
        try { json = await res.json(); } catch (e) { json = null; }
        if (!res.ok) {
            const errors = json && (json.errors || (json.message ? [json.message] : null));
            throw new ApiError(res.status, Array.isArray(errors) ? errors : []);
        }
        return json && Object.prototype.hasOwnProperty.call(json, 'data') ? json.data : json;
    };
    const api = base => ({
        get: path => call('GET', base + path),
        post: (path, body) => call('POST', base + path, body === undefined ? {} : body),
        put: (path, body) => call('PUT', base + path, body)
    });

    const errorText = error => {
        if (!error) return L.ActionFailed || '';
        const list = error.errors || [];
        if (list.some(e => typeof e === 'string' && L['Err_' + e])) return C.errorText(list, L);
        if (error.status === 409) return L.Err_version_conflict || L.ActionFailed || '';
        if (error.status === 503 || error.status === 502) return L.Err_service_unavailable || L.ActionFailed || '';
        return L.ActionFailed || '';
    };

    const showAlert = (el, error) => {
        if (!el) return;
        el.textContent = typeof error === 'string' ? error : errorText(error);
        el.classList.remove('d-none');
    };
    const hideAlert = el => { if (el) { el.textContent = ''; el.classList.add('d-none'); } };
    const toast = (text, type) => { if (window.showToast) window.showToast(text, type || 'success'); };

    const STATUS_TONES = { draft: 'secondary', in_review: 'warning', approved: 'success', retired: 'dark' };
    const statusLabel = s => L['Status_' + s] || s || '';
    const statusBadge = s => `<span class="badge bg-label-${STATUS_TONES[s] || 'secondary'}">${esc(statusLabel(s))}</span>`;

    let categories = null;
    const loadCategories = async base => {
        if (categories) return categories;
        try { categories = (await api(base).get('/lookups/categories')) || []; }
        catch (e) { categories = null; throw e; }
        return categories;
    };
    const categoryLabel = (code, list) => {
        const hit = (list || categories || []).find(c => c.code === code);
        return hit ? hit.label : (code || '');
    };
    const categoryChip = (code, list) => code
        ? `<span class="badge bg-label-${C.categoryTone(code)} or-cat" data-category="${esc(code)}">${esc(categoryLabel(code, list))}</span>`
        : '';

    /** The rep's remote preview (question · answer · the claim references). */
    const renderPreview = (question, answer, refs) => {
        const q = document.getElementById('orPreviewQuestion');
        const a = document.getElementById('orPreviewAnswer');
        const r = document.getElementById('orPreviewRefs');
        if (q) q.textContent = question ? `“${question}”` : (L.PreviewEmptyQuestion || '');
        if (a) {
            a.textContent = answer || (L.PreviewEmptyAnswer || '');
            a.classList.toggle('text-muted', !answer);
        }
        if (r) r.innerHTML = (refs || []).map(x => `<li>${esc(x)}</li>`).join('');
    };

    const claimRef = c => [c.code, c.name].filter(Boolean).join(' · ');

    window.ObjectionPage = Object.freeze({
        L, esc, date, dateTime, api, ApiError, errorText, showAlert, hideAlert, toast,
        statusLabel, statusBadge, loadCategories, categoryLabel, categoryChip, renderPreview, claimRef
    });
})(window, document);
