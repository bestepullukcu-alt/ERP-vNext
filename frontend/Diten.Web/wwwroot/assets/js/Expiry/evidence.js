/**
 * MOD-0176 F5 — Kanıt Paketi (pack §4.7; mockup "ev" + "logs"). The preview is the server's (previewEvidence, four sections); a download
 * creates the export (createEvidenceExport — the server hashes the produced bytes) and then fetches THE stored file; the log shows each
 * export's hash shortened ("sha256:41d07f…e9b3"), the full value in a copy button. No figure is computed here.
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const { t, esc } = ui;

    const scope = () => {
        const preset = ui.byId('ex-ev-preset').value;
        const s = preset === 'custom' ? { from: ui.byId('ex-ev-from').value, to: ui.byId('ex-ev-to').value } : { preset };
        const warehouse = ui.byId('ex-ev-warehouse').value;
        const type = ui.byId('ex-ev-type').value;
        if (warehouse) s.warehouseIds = [warehouse];
        if (type) s.types = [type];
        return s;
    };
    const query = (s) => {
        const p = new URLSearchParams();
        ['preset', 'from', 'to'].forEach((k) => { if (s[k]) p.set(k, s[k]); });
        (s.warehouseIds || []).forEach((w) => p.append('warehouseIds', w));
        (s.types || []).forEach((x) => p.append('types', x));
        return p.toString();
    };
    const shortHash = (sha) => (sha ? `sha256:${sha.slice(0, 6)}…${sha.slice(-4)}` : 'sha256:—');
    const empty = (cols, key) => `<tr><td colspan="${cols}" class="text-muted">${esc(t(key))}</td></tr>`;
    const lotCell = (r) => `${ui.lotLink({ lotId: r.lot?.lotId, lotNumber: r.lot?.lotNumber })}<br><small class="text-muted">${esc(r.warehouseLabel || '—')}</small>`;

    const render = (p) => {
        const k = p.summary || {};
        ui.byId('ex-ev-subtitle').textContent = t('Expiry.Evidence.PeriodLine', ui.date(p.period?.from), ui.date(p.period?.to));
        const pairs = [
            [t('Expiry.Kpi.ComplianceShort'), k.fefoCompliancePct == null ? '—' : `%${ui.qty(k.fefoCompliancePct)}`],
            [t('Expiry.Evidence.Issues'), t('Expiry.Evidence.IssuesValue', ui.qty(k.fefoIssues ?? 0), ui.qty(k.fefoBypasses ?? 0))],
            [t('Expiry.Kpi.CriticalPlain'), t('Expiry.Evidence.LotsAvailableHeld', ui.qty(k.critical?.lots ?? 0), ui.qty(k.critical?.available ?? 0), ui.qty(k.critical?.held ?? 0))],
            [t('Expiry.Kpi.ApproachingPlain'), t('Expiry.Evidence.LotsAvailableHeld', ui.qty(k.approaching?.lots ?? 0), ui.qty(k.approaching?.available ?? 0), ui.qty(k.approaching?.held ?? 0))],
            [t('Expiry.Kpi.Expired'), t('Expiry.Evidence.LotsQuantity', ui.qty(k.expiredOnHand?.lots ?? 0), ui.qty(k.expiredOnHand?.quantity ?? 0))],
            [t('Expiry.Kpi.OpenExceptions'), t('Expiry.Evidence.OpenLate', ui.qty(k.openExceptions ?? 0), ui.qty(k.lateExceptions ?? 0))],
            [t('Expiry.Evidence.AvgResolution'), k.avgResolutionHours == null ? '—' : t('Expiry.Policy.Hours', ui.qty(k.avgResolutionHours))]
        ];
        ui.byId('ex-ev-summary').innerHTML = pairs.map(([a, b]) => `<dt class="col-sm-5 text-muted fw-normal">${esc(a)}</dt><dd class="col-sm-7 mb-1">${esc(b)}</dd>`).join('');

        const exceptions = p.exceptions || [];
        ui.byId('ex-ev-exceptions-count').textContent = ui.qty(exceptions.length);
        ui.byId('ex-ev-exceptions').querySelector('tbody').innerHTML = exceptions.length
            ? exceptions.map((r) => `<tr><td><span class="font-monospace">${esc(r.number)}</span><br>${ui.typeBadge(r.type)}</td><td>${lotCell(r)}</td>`
                + `<td>${ui.caseStatusBadge(r.status)}${r.resolution ? `<div class="small mt-1">${esc(ui.resolutionText(r.resolution))}${r.resolutionReason ? ` · ${esc(r.resolutionReason)}` : ''}</div>` : `<div class="small text-muted mt-1">${esc(t('Expiry.Evidence.Awaiting'))}</div>`}</td>`
                + `<td class="small font-monospace">${esc(r.resolutionSignatureId || '—')}</td></tr>`).join('')
            : empty(4, 'Expiry.Evidence.NoExceptions');

        const changes = p.policyChanges || [];
        ui.byId('ex-ev-policy-count').textContent = ui.qty(changes.length);
        ui.byId('ex-ev-policy').querySelector('tbody').innerHTML = changes.length
            ? changes.map((h) => `<tr><td><small dir="ltr">${esc(ui.dateTime(h.changedAtUtc))}</small><br><small class="text-muted">${esc(ui.person(h.changedBy))}</small></td>`
                + `<td>${esc(h.field)}</td><td>${esc(h.oldValue ?? '—')} → <strong>${esc(h.newValue ?? '—')}</strong></td><td class="small">${esc(h.reason)}</td></tr>`).join('')
            : empty(4, 'Expiry.Evidence.NoPolicyChanges');

        const bypasses = p.fefoBypasses || [];
        ui.byId('ex-ev-bypass-count').textContent = ui.qty(bypasses.length);
        ui.byId('ex-ev-bypasses').querySelector('tbody').innerHTML = bypasses.length
            ? bypasses.map((r) => `<tr><td class="font-monospace">${esc(r.number)}</td><td>${lotCell(r)}</td><td><small dir="ltr">${esc(ui.dateTime(r.openedAtUtc))}</small></td><td>${ui.caseStatusBadge(r.status)}</td><td class="small">${esc(r.resolution ? ui.resolutionText(r.resolution) : t('Expiry.Evidence.Awaiting'))}</td></tr>`).join('')
            : empty(5, 'Expiry.Evidence.NoBypasses');
    };

    const preview = async (event) => {
        event?.preventDefault();
        ui.hideError(ui.byId('ex-error'));
        const reply = await ui.get(`/Expiry/data/evidence/preview?${query(scope())}`);
        if (!reply.ok) { ui.showError(ui.byId('ex-error'), reply); return; }
        render(reply.json.preview || {});
    };

    const log = async () => {
        const list = ui.byId('ex-ev-log');
        const reply = await ui.get('/Expiry/data/evidence/exports?page=1&pageSize=20');
        if (!reply.ok) { list.innerHTML = `<li class="list-group-item text-danger">${esc(ui.errorText(reply))}</li>`; return; }
        const items = reply.json.items || [];
        list.innerHTML = items.length
            ? items.map((e) => `<li class="list-group-item d-flex flex-wrap align-items-center gap-2">`
                + `<small class="text-muted" dir="ltr">${esc(ui.dateTime(e.exportedAtUtc))}</small><small>${esc(ui.person(e.exportedBy))}</small>`
                + `<a class="fw-medium" href="/Expiry/evidence/file/${encodeURIComponent(e.exportId)}">${esc(e.fileName)}</a>`
                + `<span class="badge bg-label-secondary">${esc(e.format)} · ${esc(t('Expiry.Evidence.Rows', ui.qty(e.rowCount)))}</span>`
                + `<span class="ms-auto d-flex align-items-center gap-1"><code class="small" title="${esc(e.sha256)}">${esc(shortHash(e.sha256))}</code>`
                + `<button type="button" class="btn btn-sm btn-icon btn-text-secondary" data-ex-copy="${esc(e.sha256)}" title="${esc(t('Expiry.Evidence.CopyHash'))}" aria-label="${esc(t('Expiry.Evidence.CopyHash'))}"><i class="bx bx-copy"></i></button></span></li>`).join('')
            : `<li class="list-group-item text-muted">${esc(t('Expiry.Evidence.LogEmpty'))}</li>`;
    };

    const download = async (format) => {
        ui.hideError(ui.byId('ex-error'));
        const key = ui.newKey();
        const reply = await ui.call('POST', '/Expiry/api/evidence/exports', { idempotencyKey: key, format, ...scope() }, { idempotencyKey: key });
        if (!reply.ok) { ui.showError(ui.byId('ex-error'), reply); return; }
        window.showToast?.(t('Expiry.Evidence.Exported', reply.json.fileName, shortHash(reply.json.sha256)), 'success');
        window.location.href = `/Expiry/evidence/file/${encodeURIComponent(reply.json.exportId)}`;
        await log();
    };

    document.addEventListener('DOMContentLoaded', async () => {
        await ui.loadWarehouses(ui.byId('ex-ev-warehouse'));
        ui.byId('ex-ev-preset').addEventListener('change', () => {
            const custom = ui.byId('ex-ev-preset').value === 'custom';
            document.querySelectorAll('[data-ex-custom]').forEach((el) => el.classList.toggle('d-none', !custom));
        });
        ui.byId('ex-ev-form').addEventListener('submit', preview);
        document.querySelectorAll('[data-ex-format]').forEach((b) => b.addEventListener('click', () => download(b.getAttribute('data-ex-format'))));
        document.addEventListener('click', async (event) => {
            const copy = event.target.closest('[data-ex-copy]');
            if (!copy) return;
            try { await navigator.clipboard.writeText(copy.getAttribute('data-ex-copy')); window.showToast?.(t('Expiry.Evidence.Copied'), 'success'); } catch (error) { /* clipboard refused */ }
        });
        await Promise.all([preview(), log()]);
    });
})();
