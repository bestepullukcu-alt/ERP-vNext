/**
 * MOD-0028-FU05 - CollectionInstance detail.
 */
'use strict';

(function () {
    const L = window.L10n || {};
    const text = (v, fallback) => (v === null || v === undefined || v === '' ? (fallback || '-') : String(v));
    const formatDate = (v) => {
        if (!v) return '-';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString(window.CurrentLanguage || undefined);
    };

    const row = (label, value) => `
        <dt class="col-sm-3 text-muted">${label}</dt>
        <dd class="col-sm-9"><code>${text(value)}</code></dd>`;

    const load = async () => {
        const id = window.FU05InstanceId;
        if (!id) return;
        const response = await fetch(`/DocumentManagementInstantiations/instances/${id}`, { credentials: 'same-origin' });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok || payload?.isSuccessful === false) {
            window.showToast?.(L.ErrorOccurred || 'Error', 'error');
            return;
        }

        const data = payload?.data || {};
        const title = document.getElementById('instanceTitle');
        if (title) title.textContent = `${text(data.name)} · ${text(data.fullPath)}`;

        const meta = document.getElementById('instanceMeta');
        if (meta) {
            meta.innerHTML = [
                row(L.CompanyId || 'Company', data.companyId),
                row(L.BaselineId || 'Baseline', data.baselineReleaseId),
                row(L.CanonicalId || 'Canonical', data.canonicalId),
                row(L.Path || 'Path', data.fullPath),
                row(L.Status || 'Status', data.instanceStatus),
                row(L.InstanceToken || 'Instance token', data.instanceToken),
                row(L.CorrelationId || 'Version', data.versionToken)
            ].join('');
        }

        const bindings = document.getElementById('bindingRows');
        if (bindings) {
            const rows = data.scopeBindings || [];
            bindings.innerHTML = rows.length
                ? rows.map((x) => `<tr><td>${text(x.orgBindingScopeType)}</td><td><code>${text(x.orgBindingScopeId)}</code></td><td>${text(x.bindingStatus)}</td><td>${formatDate(x.lastValidatedAt)}</td></tr>`).join('')
                : `<tr><td colspan="4" class="text-muted">${text(L.EmptyInstances, '')}</td></tr>`;
        }
    };

    document.addEventListener('DOMContentLoaded', () => load());
})();
