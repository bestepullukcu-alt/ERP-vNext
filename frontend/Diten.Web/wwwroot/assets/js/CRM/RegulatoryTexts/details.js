/**
 * WP-KP-5a-UI — the Details page of a Regulatory master-data record (Safety Text / Country Legal Profile; WP-KP-5a).
 * It is also the MOD-0023 Regulatory task's deep link (/CRM/SafetyTexts/{id}, /CRM/LegalProfiles/{id}).
 *
 *  - Every value is written with textContent / esc(): a safety text or a legal footer is PLAIN TEXT, never HTML —
 *    a "<script>" typed into the body shows as those characters (renderPreview is the only writer of the previews).
 *  - Status line (draft → in review → active; superseded / archived), version history (same key), decision record.
 *  - Actions by status + permission + CRM's per-record flags: Edit, Submit, Withdraw, New version, Archive.
 *  - Decision (K1, one channel = the MOD-0023 task, closed by CRM): drawn ONLY while CRM says canDecide (the submitter
 *    never gets it — CRM's SoD). Approve: comment optional; Reject: comment REQUIRED (checked here too; CRM and the Web
 *    proxy refuse it as well). After a decision the page reloads its state (approve → active).
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('regulatoryDetailsRoot');
    if (!root) return;

    const cfg = root.dataset;
    const api = `${cfg.endpoint}/${cfg.resource}`;
    const id = cfg.id;
    const canManage = cfg.canManage === 'true';
    const canSubmit = cfg.canSubmit === 'true';
    const L = () => window.L10n || {};
    const $ = (sel) => root.querySelector(sel);
    const esc = (v) => String(v ?? '').replace(/[&<>'"]/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const fmt = (tpl, ...args) => String(tpl || '').replace(/\{(\d)\}/g, (_, i) => String(args[Number(i)] ?? ''));
    const pick = (o, ...names) => { for (const n of names) { if (o && o[n] != null && o[n] !== '') return o[n]; } return null; };
    const date = (v) => (v ? new Date(v).toLocaleString(document.documentElement.lang || undefined) : '');
    const day = (v) => (v ? new Date(v).toLocaleDateString(document.documentElement.lang || undefined) : '');

    const STATUS_TONES = { draft: 'secondary', 'in-review': 'warning', active: 'success', superseded: 'info', archived: 'dark' };
    const statusLabel = (s) => L()['Status_' + String(s || '').replace(/-/g, '_')] || s || '';
    const KNOWN_ERRORS = [
        'safety_text_open_draft_exists', 'legal_profile_open_draft_exists', 'review_template_missing',
        'rejection_comment_required', 'not_editable', 'self_decision_forbidden', 'country_invalid',
        'language_not_in_country', 'reference_set_unavailable', 'product_not_found', 'dependency_unavailable',
        'decision_invalid'
    ];

    /** The ONLY writer of a text preview: plain text, paragraphs kept by CSS (white-space: pre-wrap). Never HTML. */
    const renderPreview = (el, value) => {
        if (!el) return;
        el.textContent = value || '';
        el.classList.toggle('is-empty', !value);
    };

    /** A rejection needs a comment — the reason the author will act on (the same rule CRM enforces). */
    const rejectCommentMissing = (outcome, comment) => outcome === 'reject' && !String(comment || '').trim();

    let model = null;

    const showAlert = (message) => {
        const el = $('#rtAlert');
        if (!el) return;
        el.textContent = message || '';
        el.classList.toggle('d-none', !message);
    };

    /** CRM / proxy refusal → localised text (Err_{code}); never a raw code. */
    const errorText = (body, status) => {
        const errors = Array.isArray(body?.errors) ? body.errors.map(String) : [];
        const code = [body?.code, body?.reasonCode, ...errors].find((c) => KNOWN_ERRORS.includes(String(c || '').trim()));
        if (code) {
            const text = L()['Err_' + code] || '';
            return code === 'review_template_missing' ? `${text} ${L().ReviewTemplateAdminNote || ''}`.trim() : text;
        }
        if (status === 403) return L().Err_self_decision_forbidden || L().ErrorOccurred || '';
        return errors.filter((e) => !/^[a-z_]+$/.test(e)).join(' · ') || L().ErrorOccurred || '';
    };

    const call = async (method, url, body) => {
        const res = await fetch(url, {
            method,
            credentials: 'same-origin',
            headers: body ? { Accept: 'application/json', 'Content-Type': 'application/json' } : { Accept: 'application/json' },
            body: body ? JSON.stringify(body) : undefined
        });
        const json = res.status === 204 ? {} : await res.json().catch(() => ({}));
        if (!res.ok) throw Object.assign(new Error(errorText(json, res.status)), { status: res.status });
        return json?.data;
    };

    // ---------------- render ----------------

    const renderHeader = () => {
        const code = pick(model, 'safetyTextCode', 'countryLegalProfileCode', 'legalProfileCode', 'profileCode', 'code') || '';
        const version = pick(model, 'version');
        $('#rtTitle').textContent = [code, version ? `v${version}` : ''].filter(Boolean).join(' · ');
        $('#rtCrumb').textContent = code;
        document.title = code || document.title;
    };

    const renderStatusLine = () => {
        const status = String(model.status || '');
        const steps = ['draft', 'in-review', 'active'];
        const at = steps.indexOf(status);
        const html = steps.map((s, i) => {
            const tone = i === at ? `bg-${STATUS_TONES[s]}` : (at > i ? 'bg-label-success' : 'bg-label-secondary');
            return `<span class="badge ${tone}">${esc(statusLabel(s))}</span>`;
        }).join('<i class="bx bx-chevron-right text-muted regulatory-flow-arrow" aria-hidden="true"></i>');
        const terminal = at < 0 && status ? ` <span class="badge bg-${STATUS_TONES[status] || 'secondary'} ms-2">${esc(statusLabel(status))}</span>` : '';
        $('#rtStatusLine').innerHTML = html + terminal;
        const by = pick(model, 'submittedBy');
        $('#rtSubmitted').textContent = by ? fmt(L().SubmittedTpl || '{0} · {1}', by, date(pick(model, 'submittedAt'))) : '';
    };

    const renderValues = () => {
        root.querySelectorAll('[data-value]').forEach((el) => {
            const key = el.dataset.value;
            let value = key === 'product' ? pick(model, 'globalProductCodeDisplay', 'globalProductId') : pick(model, key);
            if (el.dataset.format === 'date') value = day(value);
            el.textContent = value == null || value === '' ? '-' : String(value);
        });
        root.querySelectorAll('[data-preview]').forEach((el) => renderPreview(el, pick(model, el.dataset.preview)));
    };

    const renderDecisions = () => {
        const decisions = Array.isArray(model.decisions) ? model.decisions : [];
        $('#rtDecisions').innerHTML = decisions.length === 0
            ? `<li class="small text-muted">${esc(L().NoDecisions || '')}</li>`
            : decisions.map((d) => {
                const outcome = String(pick(d, 'outcome') || '').toLowerCase();
                const tone = outcome === 'approve' || outcome === 'approved' ? 'success' : 'danger';
                const label = L()['DecisionOutcome_' + (outcome.startsWith('approve') ? 'approve' : 'reject')] || outcome;
                return `<li class="border-start border-2 ps-3 pb-3">
                        <div class="d-flex align-items-center gap-2 flex-wrap"><span class="badge bg-label-${tone}">${esc(label)}</span>
                            <span class="small fw-medium text-heading">${esc(pick(d, 'by', 'decidedBy') || '')}</span></div>
                        <div class="small text-muted">${esc(date(pick(d, 'at', 'decidedAt')))}</div>
                        ${pick(d, 'comment') ? `<div class="small regulatory-preview mt-1" dir="auto">${esc(d.comment)}</div>` : ''}
                    </li>`;
            }).join('');
    };

    const renderVersions = (rows) => {
        const host = $('#rtVersions');
        if (!rows.length) { host.innerHTML = `<li class="small text-muted">${esc(L().NoVersions || '')}</li>`; return; }
        host.innerHTML = rows.map((r) => {
            const self = String(r.id) === String(id);
            const title = self
                ? `<span class="fw-semibold text-heading">v${esc(r.version)}</span> <span class="badge bg-label-primary">${esc(L().ThisVersion || '')}</span>`
                : `<a class="fw-semibold" href="${esc(cfg.detailsUrl + encodeURIComponent(r.id))}">v${esc(r.version)}</a>`;
            return `<li class="d-flex align-items-center gap-2 flex-wrap pb-2">${title}
                    <span class="badge bg-label-${STATUS_TONES[r.status] || 'secondary'}">${esc(statusLabel(r.status))}</span>
                    <span class="small text-muted">${esc(day(r.updatedAt))}</span></li>`;
        }).join('');
    };

    const loadVersions = async () => {
        const params = new URLSearchParams({ start: '0', length: '500', includeArchived: 'true', orderBy: 'version', orderDir: 'desc' });
        params.set('countryCode', model.countryCode || '');
        params.set('languageCode', model.languageCode || '');
        if (cfg.hasProduct === 'true') params.set('productId', model.globalProductId || '');
        try { renderVersions((await call('GET', `${api}?${params}`))?.items || []); }
        catch { renderVersions([]); }
    };

    const actionButton = (key, icon, text, tone) =>
        `<button type="button" class="btn btn-sm ${tone}" data-rt-action="${key}"><i class="bx ${icon} me-1"></i>${esc(text || '')}</button>`;

    const renderActions = () => {
        const s = String(model.status || '');
        const actions = [];
        if (canManage && model.canEdit === true) actions.push(`<a class="btn btn-sm btn-label-primary" href="${esc(cfg.editUrl + encodeURIComponent(id))}"><i class="bx bx-edit me-1"></i>${esc(L().Edit || '')}</a>`);
        if (canSubmit && model.canSubmit === true) actions.push(actionButton('submit', 'bx-send', L().SubmitForApproval, 'btn-primary'));
        if (canSubmit && s === 'in-review') actions.push(actionButton('withdraw', 'bx-undo', L().Withdraw, 'btn-label-warning'));
        if (canManage && (s === 'active' || s === 'superseded')) actions.push(actionButton('new-version', 'bx-git-branch', L().NewVersion, 'btn-label-info'));
        if (canManage && s !== 'archived') actions.push(actionButton('archive', 'bx-archive-in', L().Archive, 'btn-label-danger'));
        $('#rtActions').innerHTML = actions.join('');
    };

    const renderDecisionPanel = () => {
        const panel = $('#rtDecisionPanel');
        // Person-based SoD and candidacy are CRM's: the panel exists only when it says canDecide on an open review.
        const allowed = model.canDecide === true && String(model.status) === 'in-review';
        panel.classList.toggle('d-none', !allowed);
        if (!allowed) return;
        $('#rtDecisionComment').value = '';
        $('#rtDecisionError').textContent = '';
    };

    const render = () => {
        renderHeader();
        renderStatusLine();
        renderValues();
        renderDecisions();
        renderActions();
        renderDecisionPanel();
    };

    const load = async () => {
        showAlert('');
        try {
            model = await call('GET', `${api}/${encodeURIComponent(id)}`);
            if (!model) throw new Error(L().NotFound || '');
            render();
            void loadVersions();
        } catch (error) {
            showAlert(error.message || L().ErrorOccurred || '');
        }
    };

    // ---------------- actions ----------------

    const confirmThen = (text, confirmText, run) => {
        if (window.showConfirm) window.showConfirm(text, run, { type: 'question', confirmButtonText: confirmText });
        else run();
    };

    const ACTIONS = {
        submit: { confirm: () => L().SubmitConfirm, label: () => L().SubmitForApproval, toast: () => L().Submitted },
        withdraw: { confirm: () => L().WithdrawConfirm, label: () => L().Withdraw, toast: () => L().Withdrawn },
        'new-version': { confirm: () => L().NewVersionConfirm, label: () => L().NewVersion, toast: () => L().VersionCreated },
        archive: { confirm: () => L().ArchiveConfirm, label: () => L().Archive, toast: () => L().Archived }
    };

    root.addEventListener('click', (event) => {
        const button = event.target.closest('[data-rt-action]');
        if (!button) return;
        const key = button.dataset.rtAction;
        const action = ACTIONS[key];
        if (!action) return;
        confirmThen(action.confirm(), action.label(), async () => {
            try {
                const data = await call('POST', `${api}/${encodeURIComponent(id)}/${key}`);
                window.showToast?.(action.toast() || '', 'success');
                // A new version is a new draft record: open it.
                const created = key === 'new-version' ? (typeof data === 'string' ? data : pick(data || {}, 'id', 'safetyTextId', 'countryLegalProfileId')) : null;
                if (created) { window.location.href = cfg.detailsUrl + encodeURIComponent(created); return; }
                await load();
            } catch (error) {
                showAlert(error.message);
            }
        });
    });

    $('#rtDecisionPanel')?.addEventListener('click', async (event) => {
        const button = event.target.closest('[data-decision]');
        if (!button) return;
        const outcome = button.dataset.decision;
        const comment = $('#rtDecisionComment').value;
        if (rejectCommentMissing(outcome, comment)) {
            $('#rtDecisionError').textContent = L().DecisionCommentRequired || L().Err_rejection_comment_required || '';
            $('#rtDecisionComment').focus();
            return;
        }
        $('#rtDecisionError').textContent = '';
        try {
            await call('POST', `${api}/${encodeURIComponent(id)}/decision`, { outcome, comment: String(comment || '').trim() || null });
            window.showToast?.((outcome === 'approve' ? L().Approved : L().Rejected) || '', 'success');
            await load();   // approve → active (previous superseded); reject → back to draft with the decision kept
        } catch (error) {
            $('#rtDecisionError').textContent = error.message;
        }
    });

    load();
})(window, document);
