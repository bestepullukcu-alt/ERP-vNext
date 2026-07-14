/**
 * MOD-0028-FU10 — Reconciliation, Evidence & Deviation dashboard (Index).
 * Same-origin MVC proxy profile. Baseline selector + provider/scope + dry-run / apply-findings, the 10 summary
 * counts, qualification-readiness panel and a detected-deviation preview. No token/X-Tenant-Id is built in the
 * browser; the MVC proxy adds them server-side.
 */
'use strict';

const ReconciliationDashboard = (function () {
    const endpoint = '/DocumentManagement/Reconciliation/api';
    const perms = window.ReconciliationPerms || {};
    let L = window.L10n || {};
    const t = (key) => L[key] || key;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || { 'X-Requested-With': 'XMLHttpRequest' };

    const el = (id) => document.getElementById(id);
    const unwrap = (json) => json?.data ?? json?.Data ?? null;
    const g = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];

    let selectedBaselineId = '';

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    // ── Provider-unavailable & error reason handling ──
    const reasonOf = (payload) => payload?.reason_code || payload?.reasonCode || '';
    const errorMessage = (payload) => {
        const reason = reasonOf(payload);
        if (reason === 'PROVIDER_UNAVAILABLE') return t('ProviderUnavailableMessage');
        if (Array.isArray(payload?.errors) && payload.errors.length) return payload.errors[0];
        return t('ErrorOccurred');
    };

    const initSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        $('.select2-recon').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $(document.body),
                width: '100%',
                minimumResultsForSearch: $s.attr('id') === 'reconBaseline' ? 0 : Infinity,
                placeholder: $s.data('placeholder') || ''
            });
        });
    };

    const loadBaselines = async () => {
        try {
            const res = await fetch(`${endpoint}/baselines`, { credentials: 'same-origin', headers: getAuthHeaders() });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) {
                if (window.DitenUnauthorized?.handle(res, payload)) return;
                window.showToast?.(errorMessage(payload), 'error');
                return;
            }
            const list = unwrap(payload) || [];
            const items = Array.isArray(list) ? list : (list.items || list.Items || []);
            const select = el('reconBaseline');
            if (!select) return;
            select.innerHTML = `<option value="">${t('SelectBaseline')}</option>`;
            items.forEach((b) => {
                const id = g(b, 'id', 'Id');
                if (!id) return;
                const version = g(b, 'baselineVersion', 'BaselineVersion') || id;
                const status = g(b, 'status', 'Status') || '';
                const opt = document.createElement('option');
                opt.value = id;
                opt.textContent = status ? `${version} · ${status}` : version;
                select.appendChild(opt);
            });
            if (window.jQuery) $(select).trigger('change.select2');
        } catch (e) {
            console.error('[Reconciliation] Baseline load failed.', e);
            window.showToast?.(t('ErrorOccurred'), 'error');
        }
    };

    const setRunEnabled = (enabled) => {
        const dry = el('btnDryRun');
        const apply = el('btnApplyFindings');
        if (dry) dry.disabled = !enabled;
        if (apply) apply.disabled = !enabled;
        const dev = el('lnkDeviations');
        const evi = el('lnkEvidence');
        if (dev) { dev.classList.toggle('disabled', !enabled); dev.href = enabled ? `/DocumentManagementReconciliation/Deviations/${selectedBaselineId}` : 'javascript:void(0);'; }
        if (evi) { evi.classList.toggle('disabled', !enabled); evi.href = enabled ? `/DocumentManagementReconciliation/Evidence/${selectedBaselineId}` : 'javascript:void(0);'; }
    };

    // ── Renderers ──
    const num = (v) => (typeof v === 'number' ? v : (parseInt(v, 10) || 0));
    const metricCard = (labelKey, value, cls) => `
        <div class="col-6 col-sm-4 col-xl-2">
            <div class="card shadow-none border h-100">
                <div class="card-body p-3 text-center">
                    <div class="fs-4 fw-bold text-${cls || 'heading'}">${num(value)}</div>
                    <small class="text-muted">${t(labelKey)}</small>
                </div>
            </div>
        </div>`;

    const renderSummary = (result) => {
        const s = g(result, 'summary', 'Summary') || {};
        const wrap = el('summaryMetrics');
        if (!wrap) return;
        wrap.innerHTML = [
            metricCard('ExpectedCount', g(s, 'expectedCount', 'ExpectedCount')),
            metricCard('ActualCount', g(s, 'actualCount', 'ActualCount')),
            metricCard('MatchedCount', g(s, 'matchedCount', 'MatchedCount'), 'success'),
            metricCard('MissingCount', g(s, 'missingCount', 'MissingCount'), 'danger'),
            metricCard('ExtraCount', g(s, 'extraCount', 'ExtraCount'), 'warning'),
            metricCard('RenamedCount', g(s, 'renamedCount', 'RenamedCount')),
            metricCard('MovedCount', g(s, 'movedCount', 'MovedCount')),
            metricCard('MetadataMismatchCount', g(s, 'metadataMismatchCount', 'MetadataMismatchCount')),
            metricCard('DeviationCount', g(s, 'deviationCount', 'DeviationCount'), 'warning'),
            metricCard('BlockingDeviationCount', g(s, 'blockingDeviationCount', 'BlockingDeviationCount'), 'danger')
        ].join('');
        const badge = el('summaryModeBadge');
        if (badge) {
            const dryRun = g(result, 'dryRun', 'DryRun');
            badge.textContent = dryRun ? t('DryRunBadge') : t('AppliedBadge');
            badge.className = `badge ${dryRun ? 'bg-label-info' : 'bg-label-success'}`;
            badge.classList.remove('d-none');
        }
        el('summaryCard')?.classList.remove('d-none');
    };

    const severityClass = (sev) => ({ CRITICAL: 'danger', MAJOR: 'warning', WARNING: 'warning', INFO: 'info' }[String(sev || '').toUpperCase()] || 'secondary');
    const escapeHtml = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    const renderDeviationsPreview = (result) => {
        const list = g(result, 'deviations', 'Deviations') || [];
        const wrap = el('deviationsPreview');
        const card = el('deviationsPreviewCard');
        if (!wrap || !card) return;
        card.classList.remove('d-none');
        if (!list.length) {
            wrap.innerHTML = `<div class="alert alert-success mb-0"><i class="icon-base bx bx-check-circle me-1"></i>${t('NoDeviationsDetected')}</div>`;
            return;
        }
        wrap.innerHTML = list.map((d) => {
            const type = g(d, 'deviationType', 'DeviationType');
            const sev = g(d, 'severity', 'Severity');
            const expected = g(d, 'expectedFullPath', 'ExpectedFullPath');
            const actual = g(d, 'actualFullPath', 'ActualFullPath');
            const desc = g(d, 'description', 'Description');
            const rec = g(d, 'recommendation', 'Recommendation');
            return `<div class="border rounded p-3 mb-2">
                <div class="d-flex align-items-center gap-2 mb-1">
                    <span class="badge bg-label-primary">${escapeHtml(t(type) || type)}</span>
                    <span class="badge bg-label-${severityClass(sev)}">${escapeHtml(t('Severity' + (sev || '')) || sev)}</span>
                </div>
                <div class="small text-muted">${escapeHtml(expected || '')}${actual ? ' → ' + escapeHtml(actual) : ''}</div>
                <div class="mt-1">${escapeHtml(desc || '')}</div>
                ${rec ? `<div class="small text-muted mt-1"><span class="fw-medium">${t('Recommendation')}:</span> ${escapeHtml(rec)}</div>` : ''}
            </div>`;
        }).join('');
    };

    const runReconciliation = async (apply) => {
        if (!selectedBaselineId) { window.showToast?.(t('SelectBaselineFirst'), 'warning'); return; }
        const scope = el('reconScope')?.value || 'DefinitionToInstance';
        const provider = el('reconProvider')?.value || 'InHouse';
        const path = apply ? `apply-findings/${selectedBaselineId}` : `dry-run/${selectedBaselineId}`;
        const body = new FormData();
        body.append('__RequestVerificationToken', token());
        body.append('scope', scope);
        body.append('provider', provider);
        try {
            const res = await fetch(`${endpoint}/${path}`, { method: 'POST', body });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) {
                if (window.DitenUnauthorized?.handle(res, payload)) return;
                const type = reasonOf(payload) === 'PROVIDER_UNAVAILABLE' ? 'warning' : 'error';
                window.showToast?.(errorMessage(payload), type);
                return;
            }
            const result = unwrap(payload);
            if (!result) { window.showToast?.(t('ErrorOccurred'), 'error'); return; }
            renderSummary(result);
            renderDeviationsPreview(result);
            await loadReadiness();
            window.showToast?.(apply ? t('FindingsApplied') : t('DryRunComplete'), 'success');
        } catch (e) {
            console.error('[Reconciliation] Run failed.', e);
            window.showToast?.(t('ErrorOccurred'), 'error');
        }
    };

    const readinessMetric = (labelKey, value, cls) => `
        <div class="col-6 col-sm-4 col-xl-2">
            <div class="bg-label-secondary rounded p-3 text-center h-100">
                <div class="fs-5 fw-bold text-${cls || 'heading'}">${num(value)}</div>
                <small class="text-muted">${t(labelKey)}</small>
            </div>
        </div>`;

    const loadReadiness = async () => {
        if (!selectedBaselineId) return;
        try {
            const res = await fetch(`${endpoint}/readiness/${selectedBaselineId}`, { credentials: 'same-origin', headers: getAuthHeaders() });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) {
                if (window.DitenUnauthorized?.handle(res, payload)) return;
                return;
            }
            const d = unwrap(payload);
            if (!d) return;
            const ready = g(d, 'ready', 'Ready') === true;
            const badge = el('readinessBadge');
            if (badge) {
                badge.textContent = ready ? t('Ready') : t('NotReady');
                badge.className = `badge ${ready ? 'bg-label-success' : 'bg-label-danger'}`;
            }
            const metrics = el('readinessMetrics');
            if (metrics) {
                metrics.innerHTML = [
                    readinessMetric('ExpectedInstanceCount', g(d, 'expectedInstanceCount', 'ExpectedInstanceCount')),
                    readinessMetric('EvidenceCount', g(d, 'evidenceCount', 'EvidenceCount')),
                    readinessMetric('MissingEvidenceCount', g(d, 'missingEvidenceCount', 'MissingEvidenceCount'), 'danger'),
                    readinessMetric('PermissionsAppliedCount', g(d, 'permissionsAppliedCount', 'PermissionsAppliedCount'), 'success'),
                    readinessMetric('QaVerifiedCount', g(d, 'qaVerifiedCount', 'QaVerifiedCount'), 'success'),
                    readinessMetric('OpenBlockingDeviationCount', g(d, 'openBlockingDeviationCount', 'OpenBlockingDeviationCount'), 'danger')
                ].join('');
            }
            const reasons = g(d, 'reasons', 'Reasons') || [];
            const reasonsWrap = el('readinessReasonsWrap');
            const reasonsList = el('readinessReasons');
            if (reasonsWrap && reasonsList) {
                if (reasons.length) {
                    reasonsList.innerHTML = reasons.map((r) => `<li class="text-muted small">${escapeHtml(r)}</li>`).join('');
                    reasonsWrap.classList.remove('d-none');
                } else {
                    reasonsWrap.classList.add('d-none');
                }
            }
            el('readinessCard')?.classList.remove('d-none');
        } catch (e) {
            console.error('[Reconciliation] Readiness load failed.', e);
        }
    };

    const onBaselineChange = () => {
        selectedBaselineId = el('reconBaseline')?.value || '';
        setRunEnabled(!!selectedBaselineId);
        el('summaryCard')?.classList.add('d-none');
        el('deviationsPreviewCard')?.classList.add('d-none');
        el('readinessCard')?.classList.add('d-none');
        if (selectedBaselineId) void loadReadiness();
    };

    const bindEvents = () => {
        const baseline = el('reconBaseline');
        if (baseline) {
            if (window.jQuery) $(baseline).on('change', onBaselineChange);
            else baseline.addEventListener('change', onBaselineChange);
        }
        el('btnDryRun')?.addEventListener('click', () => void runReconciliation(false));
        el('btnApplyFindings')?.addEventListener('click', () => {
            if (!selectedBaselineId) { window.showToast?.(t('SelectBaselineFirst'), 'warning'); return; }
            window.showConfirm?.(t('ApplyFindingsConfirm'), () => void runReconciliation(true), {
                type: 'warning',
                confirmButtonText: t('ApplyFindings')
            });
        });
    };

    const init = async () => {
        if (!el('reconBaseline')) return;
        syncL10n();
        initSelect2();
        bindEvents();
        await loadBaselines();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ReconciliationDashboard.init());
