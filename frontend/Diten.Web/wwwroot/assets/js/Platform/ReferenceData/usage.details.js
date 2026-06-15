/**
 * Business Reference Data - Usage Dependency read-only Details.
 * Fetches the set-scoped registry via the proxy and renders the matching registration.
 */
'use strict';

(function () {
    const root = document.getElementById('rd-usage-details');
    if (!root) return;

    const api = window.ReferenceDataApi;
    const L = window.L10n || {};
    const setCode = root.dataset.setCode || '';
    const usageId = root.dataset.usageId || '';

    const text = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value == null || value === '' ? '-' : String(value); };
    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString(window.CurrentLanguage || undefined);
    };
    const resolutionMap = {
        latest: L.UsageResolutionLatest || 'Latest',
        pinned: L.UsageResolutionPinned || 'Pinned',
        'as-of': L.UsageResolutionAsOf || 'As Of'
    };
    const criticalityMap = {
        critical: { title: L.UsageImpactCritical || 'Critical', class: 'bg-label-danger' },
        high: { title: L.UsageCriticalityHigh || 'High', class: 'bg-label-warning' },
        medium: { title: L.UsageCriticalityMedium || 'Medium', class: 'bg-label-info' },
        low: { title: L.UsageCriticalityLow || 'Low', class: 'bg-label-secondary' }
    };

    const setStatus = (message) => {
        const el = document.getElementById('rd-usage-details-status');
        if (!el) return;
        if (!message) { el.className = 'alert d-none mb-3'; el.textContent = ''; return; }
        el.className = 'alert alert-danger mb-3';
        el.textContent = message;
    };

    const render = (item) => {
        text('ucd-module', item.consumerModule || item.ConsumerModule);
        text('ucd-name', item.consumerName || item.ConsumerName);
        text('ucd-endpoint', item.consumerEndpoint || item.ConsumerEndpoint);
        text('ucd-scope-type', item.scopeType || item.ScopeType);
        text('ucd-scope-key', item.scopeKey || item.ScopeKey);
        const resolution = (item.resolutionMode || item.ResolutionMode || '').toLowerCase();
        text('ucd-resolution', resolutionMap[resolution] || resolution || '-');
        text('ucd-version-pin', item.versionPin || item.VersionPin);
        text('ucd-asof', (item.asOfDate || item.AsOfDate) ? formatDate(item.asOfDate || item.AsOfDate) : '-');
        text('ucd-last-resolved', (item.lastResolvedAt || item.LastResolvedAt) ? formatDate(item.lastResolvedAt || item.LastResolvedAt) : '-');
        text('ucd-notes', item.notes || item.Notes);
        const critEl = document.getElementById('ucd-criticality');
        if (critEl) {
            const entry = criticalityMap[(item.criticality || item.Criticality || '').toLowerCase()] || { title: item.criticality || '-', class: 'bg-label-primary' };
            critEl.innerHTML = `<span class="badge ${entry.class}">${entry.title}</span>`;
        }
        const titleEl = document.getElementById('ucd-title');
        if (titleEl) titleEl.textContent = `${item.consumerName || item.ConsumerName || ''} — ${setCode}`;
    };

    const load = async () => {
        if (!usageId || typeof api?.getUsageRegistrations !== 'function') return;
        try {
            const data = await api.getUsageRegistrations(setCode);
            const items = data?.items || data?.Items || [];
            const item = items.find((x) => String(x.usageRegistrationId || x.UsageRegistrationId) === String(usageId));
            if (item) render(item);
            else setStatus(L.ErrorOccurred || 'Record not found.');
        } catch (error) {
            if (error?.isHandled) return;
            setStatus(error?.message || L.ErrorOccurred || '');
        }
    };

    document.addEventListener('DOMContentLoaded', load);
})();
