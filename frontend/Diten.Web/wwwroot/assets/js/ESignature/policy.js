'use strict';

/*
 * MOD-0022 E5W2 — e-İmza politikası (pack §11.3, KS-2 / KS-3 / KS-5 / KS-8, CT-D15; mockup v2 tab 5). Page:
 * Views/ESignature/Settings/Policy.cshtml.
 *
 *   GET  policy[?legalEntityId]            → { tenant, override, effective, legalEntities (companies WITH an override) }
 *   GET  actions                           → the signable-action catalog: a row the stored policy predates is shown with the
 *                                            catalog default — what the server applies for it (ESignatureCatalog.GetRowAsync)
 *   GET  policy/history[?legalEntityId]    → the change rows (who, when, scope, change lines, reason)
 *   PUT  policy/tenant | policy/legal-entities/{id}  { rows, reauthEvery, reauthWindowMinutes, sampleApprovalRequired (tenant
 *        only, CT-D15), reason ≥ 10, expectedVersion } — the first company PUT creates the override (expectedVersion null)
 *   POST policy/legal-entities/{id}/revert { reason }  — red reasoned confirm; the override's content stays in the history
 *
 * Second factor is "Yakında" (KS-11): always sent as notAvailable, the control is drawn disabled. Without esign.policy.manage
 * every control is read-only and no save / copy / revert control exists (absent, not disabled).
 */
(function (global) {
    const E = global.ESign;
    const P = global.ESignPages;
    const t = E.t;
    const esc = E.escapeHtml;
    const $ = (id) => document.getElementById(id);
    const canManage = $('esignPolicyRoot')?.getAttribute('data-can-manage') === 'true' && P.can('esign.policy.manage');
    const MODES = ['none', 'optional', 'savedAccepted', 'drawRequired'];

    let actions = [];
    let meanings = [];
    let companies = [];       // [{ id, name, hasOverride }]
    let view = null;          // PolicyView
    let scope = 'tenant';     // 'tenant' | company id
    let base = null;          // the policy being edited (tenant or override); null = the company uses the tenant default
    let creating = false;     // a company copy not saved yet (expectedVersion null)
    let draft = null;         // { rows, reauthEvery, reauthWindowMinutes, sampleApprovalRequired }
    let original = '';

    const actionOf = (code) => actions.find((a) => a.actionCode === code);
    const actionName = (row) => {
        const a = actionOf(row.actionCode);
        const names = (a && a.names) || {};
        return row.actionName || names[P.lang] || names.tr || names.en || row.actionCode;
    };

    /** The stored rows plus a catalog-default row for every action the stored policy predates (sorted by module, action). */
    function rowsOf(policy) {
        const rows = (policy.rows || []).map((r) => ({ actionCode: r.actionCode, module: r.module, actionName: r.actionName, required: !!r.required, meaningCode: r.meaningCode || null, visualMode: r.visualMode || 'optional' }));
        actions.forEach((a) => {
            if (!rows.some((r) => r.actionCode === a.actionCode)) {
                rows.push({ actionCode: a.actionCode, module: a.module, actionName: null, required: !!a.defaultRequired, meaningCode: a.defaultMeaningCode || null, visualMode: a.defaultVisualMode || 'optional', fromCatalog: true });
            }
        });
        return rows.sort((x, y) => `${x.module}|${x.actionCode}`.localeCompare(`${y.module}|${y.actionCode}`));
    }

    function startDraft(policy) {
        draft = {
            rows: rowsOf(policy),
            reauthEvery: policy.reauthEvery !== false,
            reauthWindowMinutes: policy.reauthWindowMinutes || 15,
            sampleApprovalRequired: (view && view.tenant && view.tenant.sampleApprovalRequired) !== false
        };
        original = JSON.stringify(draft);
    }

    const tenantRow = (code) => rowsOf(view.tenant).find((r) => r.actionCode === code);
    const sameRow = (a, b) => !!a && !!b && a.required === b.required && (a.meaningCode || null) === (b.meaningCode || null) && a.visualMode === b.visualMode;
    const originalRows = () => JSON.parse(original).rows;

    // ── load ─────────────────────────────────────────────────────────────────────────────────────────────────────
    async function load() {
        const q = scope === 'tenant' ? '' : `?legalEntityId=${encodeURIComponent(scope)}`;
        const r = await E.call('GET', `${P.API}/policy${q}`);
        if (!r.ok) { P.failed(r); return; }
        view = E.dataOf(r) || {};
        (view.legalEntities || []).forEach((le) => {
            const c = companies.find((x) => x.id === le.id);
            if (c) c.hasOverride = le.hasOverride; else companies.push({ id: le.id, name: le.name, hasOverride: le.hasOverride });
        });
        creating = false;
        base = scope === 'tenant' ? view.tenant : (view.override || null);
        if (base) startDraft(base); else { draft = null; original = ''; }
        render();
        loadHistory();
    }

    async function loadHistory() {
        const q = scope === 'tenant' ? '' : `?legalEntityId=${encodeURIComponent(scope)}`;
        const r = await E.call('GET', `${P.API}/policy/history${q}`);
        const items = r.ok ? (E.dataOf(r) || []) : [];
        $('esignPolicyHistory').innerHTML = items.length
            ? items.map((h) => `<div class="border-bottom pb-3 mb-3">
                <div class="d-flex flex-wrap gap-2 align-items-center small"><span class="fw-medium">${esc(P.at(h.atUtc))}</span><span class="text-muted">${esc(h.by || '')}</span>
                <span class="badge bg-label-secondary">${esc(t(`ESign.Policy.Scope.${h.scope === 'legalEntity' ? 'Company' : 'Tenant'}`))}${h.legalEntityId ? ` · ${esc(companyName(h.legalEntityId))}` : ''}</span></div>
                <div class="d-flex flex-wrap gap-1 mt-2">${(h.changes || []).map((c) => `<span class="badge bg-label-primary fw-normal">${esc(c)}</span>`).join('')}</div>
                <small class="d-block text-muted mt-1">${esc(t('ESign.Common.ReasonQuoted', h.reason || ''))}</small></div>`).join('')
            : `<p class="text-muted small mb-0">${esc(t('ESign.Policy.HistoryEmpty'))}</p>`;
    }

    const companyName = (id) => ((companies.find((c) => c.id === id) || {}).name || id || '');

    // ── render ───────────────────────────────────────────────────────────────────────────────────────────────────
    function render() {
        document.querySelectorAll('[data-esign-scope]').forEach((b) => {
            const on = (b.getAttribute('data-esign-scope') === 'tenant') === (scope === 'tenant');
            b.classList.toggle('btn-primary', on);
            b.classList.toggle('btn-label-secondary', !on);
        });
        const select = $('esignPolicyCompany');
        select.classList.toggle('d-none', scope === 'tenant');
        $('esignPolicyScopeHint').textContent = scope === 'tenant' ? t('ESign.Policy.ScopeHint.Tenant') : t('ESign.Policy.ScopeHint.Company');

        const usesTenant = scope !== 'tenant' && !base && !creating;
        $('esignPolicyUsesTenant').classList.toggle('d-none', !usesTenant);
        if (usesTenant) $('esignPolicyUsesTenantTitle').textContent = t('ESign.Policy.UsesTenant.Title', companyName(scope));

        const isOverride = scope !== 'tenant' && (base || creating);
        $('esignPolicyOverrideBanner').classList.toggle('d-none', !isOverride);
        if (isOverride) $('esignPolicyOverrideText').textContent = t(creating ? 'ESign.Policy.Override.New' : 'ESign.Policy.Override.Text', companyName(scope));
        $('esignPolicyRevert')?.classList.toggle('d-none', !base || creating || scope === 'tenant');

        $('esignPolicyEditor').classList.toggle('d-none', !draft);
        if (draft) renderEditor();
        refreshDirty();
    }

    function meaningOptions(selected) {
        const options = [`<option value="">${esc(t('ESign.Policy.MeaningSignerChooses'))}</option>`];
        const codes = new Set(meanings.filter((m) => m.isActive).map((m) => m.code));
        if (selected) codes.add(selected);
        [...codes].forEach((code) => options.push(`<option value="${esc(code)}"${code === selected ? ' selected' : ''}>${esc(P.meaningName(meanings, code))}</option>`));
        return options.join('');
    }

    function renderEditor() {
        const ro = !canManage;
        const dis = ro ? ' disabled' : '';
        const before = originalRows();
        $('esignPolicyRules').querySelector('tbody').innerHTML = draft.rows.map((r, i) => {
            const differs = scope !== 'tenant' && !sameRow(r, tenantRow(r.actionCode));
            const dirty = !sameRow(r, before.find((x) => x.actionCode === r.actionCode));
            return `<tr data-row="${i}">
                <td class="text-nowrap"><span class="text-muted">${esc(P.moduleLabel(r.module))} ·</span> <span class="fw-medium text-heading">${esc(actionName(r))}</span>
                    ${differs ? `<span class="badge bg-label-warning ms-1">${esc(t('ESign.Policy.Differs'))}</span>` : ''}
                    ${dirty ? `<span class="d-inline-block rounded-circle bg-primary ms-1" style="width:8px;height:8px" title="${esc(t('ESign.Policy.Unsaved'))}"></span>` : ''}
                    <small class="d-block text-muted font-monospace">${esc(r.actionCode)}</small></td>
                <td><select class="form-select form-select-sm" style="min-width:190px" data-field="meaningCode" aria-label="${esc(t('ESign.Policy.Col.Meaning'))}"${dis}>${meaningOptions(r.meaningCode)}</select></td>
                <td><div class="btn-group btn-group-sm" role="group">
                    <button type="button" class="btn ${r.required ? 'btn-primary' : 'btn-label-secondary'}" data-field="required" data-value="true"${dis}>${esc(t('ESign.Policy.Required'))}</button>
                    <button type="button" class="btn ${!r.required ? 'btn-primary' : 'btn-label-secondary'}" data-field="required" data-value="false"${dis}>${esc(t('ESign.Policy.Off'))}</button>
                </div></td>
                <td><select class="form-select form-select-sm" style="min-width:180px" data-field="visualMode" aria-label="${esc(t('ESign.Policy.Col.Visual'))}"${dis}>${MODES.map((m) => `<option value="${m}"${m === r.visualMode ? ' selected' : ''}>${esc(t(`ESign.Dialog.VisualMode.${m}`))}</option>`).join('')}</select></td>
                <td class="text-nowrap"><div class="form-check form-switch d-inline-block mb-0 opacity-50"><input class="form-check-input" type="checkbox" disabled aria-label="${esc(t('ESign.Policy.Col.SecondFactor'))}"></div><span class="badge bg-label-secondary">${esc(t('ESign.Policy.Soon'))}</span></td>
            </tr>`;
        }).join('') || `<tr><td colspan="5" class="text-center text-muted">${esc(t('ESign.Policy.NoActions'))}</td></tr>`;

        $('esignReauthEvery').checked = draft.reauthEvery;
        $('esignReauthWindow').checked = !draft.reauthEvery;
        $('esignReauthMinutes').value = draft.reauthWindowMinutes;
        $('esignReauthMinutes').disabled = ro || draft.reauthEvery;
        $('esignReauthMinutesHint').textContent = draft.reauthEvery ? t('ESign.Policy.MinutesHintEvery') : t('ESign.Policy.MinutesHint');
        ['esignReauthEvery', 'esignReauthWindow'].forEach((id) => { $(id).disabled = ro; });
        const sample = $('esignSampleApproval');
        sample.checked = draft.sampleApprovalRequired;
        sample.disabled = ro || scope !== 'tenant'; // CT-D15: a tenant setting; an override echoes it read-only
        $('esignSampleApprovalHint').textContent = scope !== 'tenant' ? t('ESign.Policy.SampleApproval.TenantOnly') : t('ESign.Policy.SampleApproval.Sub');
    }

    function changeLines() {
        const before = JSON.parse(original);
        const lines = [];
        draft.rows.forEach((r) => {
            const o = before.rows.find((x) => x.actionCode === r.actionCode) || {};
            const name = actionName(r);
            if (o.required !== r.required) lines.push(t('ESign.Policy.Change.Required', name, t(r.required ? 'ESign.Policy.Required' : 'ESign.Policy.Off')));
            if ((o.meaningCode || null) !== (r.meaningCode || null)) lines.push(t('ESign.Policy.Change.Meaning', name, r.meaningCode ? P.meaningName(meanings, r.meaningCode) : t('ESign.Policy.MeaningSignerChooses')));
            if (o.visualMode !== r.visualMode) lines.push(t('ESign.Policy.Change.Visual', name, t(`ESign.Dialog.VisualMode.${r.visualMode}`)));
        });
        if (before.reauthEvery !== draft.reauthEvery) lines.push(t(draft.reauthEvery ? 'ESign.Policy.Reauth.Every' : 'ESign.Policy.Reauth.Window'));
        if (before.reauthWindowMinutes !== draft.reauthWindowMinutes) lines.push(t('ESign.Policy.Change.Minutes', draft.reauthWindowMinutes));
        if (before.sampleApprovalRequired !== draft.sampleApprovalRequired) lines.push(t('ESign.Policy.SampleApproval'));
        if (creating && !lines.length) lines.push(t('ESign.Policy.Override.New', companyName(scope)));
        return lines;
    }

    function refreshDirty() {
        const bar = $('esignPolicyDirty');
        if (!bar) return;
        const dirty = !!draft && (creating || JSON.stringify(draft) !== original);
        bar.classList.toggle('d-none', !dirty);
        if (dirty) $('esignPolicyDirtyText').textContent = t('ESign.Policy.DirtyText', changeLines().length);
    }

    // ── edit ─────────────────────────────────────────────────────────────────────────────────────────────────────
    function wireEditor() {
        const tbody = $('esignPolicyRules').querySelector('tbody');
        tbody.addEventListener('change', (e) => {
            const el = e.target.closest('[data-field]');
            const tr = e.target.closest('tr[data-row]');
            if (!el || !tr || !canManage) return;
            const row = draft.rows[Number(tr.getAttribute('data-row'))];
            row[el.getAttribute('data-field')] = el.value || null;
            if (!row.visualMode) row.visualMode = 'optional';
            renderEditor();
            refreshDirty();
        });
        tbody.addEventListener('click', (e) => {
            const el = e.target.closest('button[data-field="required"]');
            const tr = e.target.closest('tr[data-row]');
            if (!el || !tr || !canManage) return;
            draft.rows[Number(tr.getAttribute('data-row'))].required = el.getAttribute('data-value') === 'true';
            renderEditor();
            refreshDirty();
        });
        const general = () => {
            if (!draft || !canManage) return;
            draft.reauthEvery = $('esignReauthEvery').checked;
            const minutes = Number($('esignReauthMinutes').value);
            draft.reauthWindowMinutes = Number.isFinite(minutes) ? Math.min(60, Math.max(1, Math.round(minutes))) : draft.reauthWindowMinutes;
            if (scope === 'tenant') draft.sampleApprovalRequired = $('esignSampleApproval').checked;
            renderEditor();
            refreshDirty();
        };
        ['esignReauthEvery', 'esignReauthWindow', 'esignReauthMinutes', 'esignSampleApproval'].forEach((id) => $(id).addEventListener('change', general));
    }

    async function save() {
        const lines = changeLines();
        const reason = await P.reason({ title: t('ESign.Policy.Save.Title'), subtext: t('ESign.Policy.Save.Sub'), lines, confirmText: t('ESign.Policy.Save'), type: 'warning' });
        if (!reason) return;
        const body = {
            rows: draft.rows.map((r) => ({ actionCode: r.actionCode, required: r.required, meaningCode: r.meaningCode || null, visualMode: r.visualMode, secondFactor: 'notAvailable' })),
            reauthEvery: draft.reauthEvery,
            reauthWindowMinutes: draft.reauthWindowMinutes,
            reason,
            expectedVersion: creating ? null : base.version
        };
        if (scope === 'tenant') body.sampleApprovalRequired = draft.sampleApprovalRequired;
        const url = scope === 'tenant' ? `${P.API}/policy/tenant` : `${P.API}/policy/legal-entities/${encodeURIComponent(scope)}`;
        const r = await E.call('PUT', url, body);
        if (!r.ok) {
            P.failed(r);
            if (E.codeOf(r) === 'ESIGN_POLICY_VERSION_CONFLICT') load();
            return;
        }
        P.toast(t('ESign.Policy.Saved'));
        await load();
    }

    async function revert() {
        const reason = await P.reason({
            title: t('ESign.Policy.Revert.Title', companyName(scope)),
            subtext: t('ESign.Policy.Revert.Sub'),
            confirmText: t('ESign.Policy.Revert'),
            type: 'danger'
        });
        if (!reason) return;
        const r = await E.call('POST', `${P.API}/policy/legal-entities/${encodeURIComponent(scope)}/revert`, { reason, idempotencyKey: E.newKey() });
        if (!r.ok) { P.failed(r); return; }
        P.toast(t('ESign.Policy.Reverted', companyName(scope)));
        await load();
    }

    function copyForCompany() {
        creating = true;
        base = null;
        startDraft(view.tenant);
        original = JSON.stringify(draft); // a copy of the tenant default; the save creates the override
        render();
    }

    function fillCompanies() {
        P.fill($('esignPolicyCompany'), companies.map((c) => ({ value: c.id, text: c.hasOverride ? `${c.name} · ${t('ESign.Policy.Differs')}` : c.name })));
    }

    async function init() {
        if (!$('esignPolicyRoot')) return;
        const [a, m, c] = await Promise.all([E.call('GET', `${P.API}/actions`), P.meanings(), P.companies()]);
        actions = a.ok ? (E.dataOf(a) || []) : [];
        meanings = m;
        companies = c.map((x) => ({ id: x.id, name: x.name, hasOverride: false }));
        wireEditor();
        document.querySelectorAll('[data-esign-scope]').forEach((b) => b.addEventListener('click', () => {
            const want = b.getAttribute('data-esign-scope');
            if (want === 'tenant') { scope = 'tenant'; load(); return; }
            fillCompanies();
            if (!companies.length) { P.failed({ ok: false, status: 0, json: null }, 'ESign.Policy.NoCompanies'); return; }
            scope = $('esignPolicyCompany').value || companies[0].id;
            $('esignPolicyCompany').value = scope;
            load();
        }));
        $('esignPolicyCompany').addEventListener('change', (e) => { scope = e.target.value; load(); });
        $('esignPolicyCopy')?.addEventListener('click', copyForCompany);
        $('esignPolicyRevert')?.addEventListener('click', revert);
        $('esignPolicySave')?.addEventListener('click', save);
        $('esignPolicyDiscard')?.addEventListener('click', () => { if (creating) { creating = false; draft = null; original = ''; render(); } else { draft = JSON.parse(original); render(); } });
        await load();
        fillCompanies();
    }

    init();
})(window);
