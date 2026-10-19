/**
 * MOD-0177 R7b — Tatbikat Planlayıcı (pack §11.1, §8.10; mockup "isDrills"). Draws what the doors answer; every decision is RecallDrillView's
 * (drill-view.js, measured by Node): the clock (server time only), the steps (server order), the buttons (action code + status), the calls.
 * Reads: /Recall/data/drill-plans, /Recall/data/drill-runs, /Recall/data/drill-runs/{id}. After every write the run and the lists are read
 * again — the server's answer is the truth. A run opened from Vakalar (?run=) is shown here, read-only unless it is running.
 */
'use strict';

(function () {
    if (typeof document === 'undefined' || !window.RecallUi || !window.RecallDrillView) return;
    const ui = window.RecallUi;
    const view = window.RecallDrillView;
    const { t, esc, byId } = ui;
    const root = byId('rc-drills');
    if (!root) return;
    const me = root.dataset.userId;
    const state = { plans: [], runs: [], warning: null, run: null, serverNow: null, answeredAt: 0, setup: null, mode: 'Auto', plan: null, prepared: null };
    const show = (el, on) => el?.classList.toggle('d-none', !on);
    const modal = (id) => window.bootstrap?.Modal?.getOrCreateInstance(byId(id)) || { show() {}, hide() {} };
    const person = (u) => u?.name || u?.userId || '';

    // ── calls ─────────────────────────────────────────────────────────────────────────────────────────────────────
    const antiforgery = () => document.querySelector('#rc-antiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const write = async (path, body, options = {}) => {
        const headers = { Accept: 'application/json', 'Content-Type': 'application/json', RequestVerificationToken: antiforgery() };
        if (options.ifMatch) headers['If-Match'] = options.ifMatch;
        try {
            const res = await fetch(path, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
            return { ok: res.ok, status: res.status, json: await res.json().catch(() => null) };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const deps = {
        write,
        newKey: () => ui.newKey().replace(/^r6-/, 'r7-'),
        sign: (subject) => (window.ESign?.open ? window.ESign.open(subject) : Promise.resolve(null))
    };
    const sentence = (reply) => ui.errorText(reply);
    const saved = (key) => { const box = byId('rc-saved'); box.textContent = t(key); show(box, true); };

    // ── formatting ───────────────────────────────────────────────────────────────────────────────────────────────
    const frequency = (p) => (p.frequency === 'CustomDays' ? t('Recall.Freq.CustomN', p.customDays) : t('Recall.Freq.' + p.frequency) || p.frequency);
    const passBadge = (passed) => `<span class="badge ${passed ? 'bg-label-success' : 'bg-label-danger'}">${esc(t(passed ? 'Recall.Drill.Passed' : 'Recall.Drill.Failed'))}</span>`;
    const resultBadge = (run) => (run.status === 'Completed' ? passBadge(run.passed) : ui.drillStatusBadge(run.status));

    // ── the lists ────────────────────────────────────────────────────────────────────────────────────────────────
    const drawWarning = () => {
        const w = state.warning;
        const band = byId('rc-freq-band');
        if (!w?.warning) { band.classList.add('d-none'); band.classList.remove('d-flex'); return; }
        byId('rc-freq-title').textContent = t('Recall.Freq.Band', w.warningDays);
        byId('rc-freq-detail').textContent = w.lastCompletedAtUtc ? t('Recall.Freq.Detail', ui.date(w.lastCompletedAtUtc), w.daysSinceLastCompleted) : t('Recall.Freq.None');
        band.classList.remove('d-none');
        band.classList.add('d-flex');
    };

    const running = () => state.runs.find((r) => r.status === 'Running') || null;

    const drawPlans = () => {
        const offers = view.offers(null, ui.can, !!running());
        const last = (planId) => state.runs.find((r) => r.planId === planId && r.status === 'Completed');
        byId('rc-plans').querySelector('tbody').innerHTML = state.plans.map((p) => {
            const l = last(p.planId);
            const buttons = [
                offers.start && p.active ? `<button type="button" class="btn btn-sm btn-label-primary py-0" data-start="${esc(p.planId)}"><i class="bx bx-play me-1"></i>${esc(t('Recall.Drills.StartRun'))}</button>` : '',
                offers.planSave && byId('rc-plan-modal') ? `<button type="button" class="btn btn-sm btn-label-secondary py-0" data-edit="${esc(p.planId)}">${esc(t('Recall.Drills.EditPlan'))}</button>` : ''
            ].join(' ');
            return `<tr><td><div class="fw-medium text-heading">${esc(p.name)}</div><small class="font-monospace text-muted">${esc(p.code)}</small>${p.active ? '' : ` <span class="badge bg-label-secondary">${esc(t('Recall.Drills.Inactive'))}</span>`}</td>`
                + `<td class="small">${esc(frequency(p))}</td><td class="small">${esc(t('Recall.DrillScope.' + p.scopeKind) || p.scopeKind)}</td>`
                + `<td class="small">${esc(ui.date(p.nextDueOn))}</td><td class="small">${esc(person(p.owner))}</td>`
                + `<td>${l ? `${resultBadge(l)}<div class="small text-muted">${esc(ui.date(l.startedAtUtc))}</div>` : '—'}</td><td class="text-end text-nowrap">${buttons}</td></tr>`;
        }).join('') || `<tr><td colspan="7" class="text-muted">${esc(t('Recall.Drills.NoPlans'))}</td></tr>`;
        byId('rc-plans').querySelectorAll('[data-start]').forEach((b) => b.addEventListener('click', () => openSetup(state.plans.find((p) => p.planId === b.dataset.start))));
        byId('rc-plans').querySelectorAll('[data-edit]').forEach((b) => b.addEventListener('click', () => openPlan(state.plans.find((p) => p.planId === b.dataset.edit))));
    };

    const drawRuns = () => {
        byId('rc-runs').querySelector('tbody').innerHTML = state.runs.map((r) => `<tr>`
            + `<td><a href="?run=${encodeURIComponent(r.runId)}" class="font-monospace" data-run="${esc(r.runId)}">${esc(r.number)}</a></td>`
            + `<td class="font-monospace small">${esc(r.lot?.lotNumber || '')}</td><td class="small">${esc(ui.date(r.startedAtUtc))}</td>`
            + `<td class="small">${r.durationMinutes == null ? '—' : esc(ui.duration(r.durationMinutes))}</td><td>${esc(String(r.gapCount ?? 0))}</td><td>${resultBadge(r)}</td></tr>`).join('')
            || `<tr><td colspan="6" class="text-muted">${esc(t('Recall.Drills.NoRuns'))}</td></tr>`;
        byId('rc-runs').querySelectorAll('[data-run]').forEach((a) => a.addEventListener('click', (event) => { event.preventDefault(); openRun(a.dataset.run, true); }));
    };

    // ── the panel ────────────────────────────────────────────────────────────────────────────────────────────────
    const tick = () => {
        const c = view.clock(state.run, state.serverNow, performance.now() - state.answeredAt);
        byId('rc-drill-clock').textContent = c ? c.text : '—';
        byId('rc-drill-clock').classList.toggle('text-danger', !!c?.over);
        byId('rc-drill-clock-sub').textContent = !c ? '' : state.run?.status === 'Cancelled' ? t('Recall.DrillStatus.Cancelled') : c.stopped ? t('Recall.Drills.ClockStopped')
            : c.over ? t('Recall.Drills.ClockOver', c.targetHours) : t('Recall.Drills.ClockRemaining', c.targetHours, ui.duration(c.remaining));
    };

    const drawRun = () => {
        const run = state.run;
        const panel = byId('rc-drill-panel');
        show(panel, !!run || !!state.setup);
        if (!run) return;
        show(byId('rc-drill-setup'), false);
        byId('rc-drill-plan').textContent = state.plans.find((p) => p.planId === run.planId)?.name || run.planCode || '';
        byId('rc-drill-lot').textContent = run.lot?.lotNumber || '';
        byId('rc-drill-number').textContent = `${run.number} · ${t(run.selectionMode === 'Auto' ? 'Recall.Drills.ModeAuto' : 'Recall.Drills.ModeManual')}`;
        const offers = view.offers(run, ui.can, true);
        const finishedOrClosed = view.finished(run) || run.status !== 'Running';
        show(byId('rc-drill-run'), run.status === 'Running' && !view.finished(run));
        show(byId('rc-drill-done'), finishedOrClosed);
        byId('rc-drill-steps').innerHTML = view.steps(run).map((s) => `<li class="d-flex align-items-center gap-2">`
            + `<span class="badge rounded-pill ${s.done ? 'bg-success' : s.current ? 'bg-primary' : 'bg-label-secondary'}">${s.done ? '<i class="bx bx-check"></i>' : s.n}</span>`
            + `<span class="small ${s.current ? 'fw-medium' : ''}">${esc(t('Recall.Drills.Step.' + s.step))}${s.done ? ` <span class="text-muted">· ${esc(person(s.by))}</span>` : ''}</span></li>`).join('');
        byId('rc-drill-step-info').textContent = run.nextStep ? t('Recall.Drills.StepInfo.' + run.nextStep) : '';
        const sum = run.scopeSummary;
        byId('rc-drill-summary').textContent = sum ? t('Recall.Drills.Summary', sum.nodeCount, sum.lotCount, sum.stockCount, sum.shipmentCount) : '';
        show(byId('rc-drill-summary'), !!sum);
        byId('rc-drill-gaps').textContent = run.gapCount ? t('Recall.Drills.GapBand', run.gapCount) : '';
        show(byId('rc-drill-gaps'), !!run.gapCount);
        const stepButton = byId('rc-drill-step');
        if (stepButton) {
            show(stepButton, offers.step);
            byId('rc-drill-step-text').textContent = run.nextStep ? t('Recall.Drills.StepButton.' + run.nextStep) : '';
        }
        show(byId('rc-drill-cancel'), offers.cancel);
        const c = view.clock(run, state.serverNow, 0);
        byId('rc-drill-duration').textContent = c ? ui.duration(c.minutes) : '—';
        byId('rc-drill-result-label').textContent = t('Recall.Drills.ResultTarget', run.targetHours);
        byId('rc-drill-result').innerHTML = run.status === 'Cancelled' ? '—' : run.passed == null
            ? (c ? `<span class="badge ${c.over ? 'bg-label-danger' : 'bg-label-success'}">${esc(t(c.over ? 'Recall.Badge.OverTarget' : 'Recall.Badge.OnTarget'))}</span>` : '—')
            : passBadge(run.passed); // the server decides pass / fail when the timer stops (duration vs target)
        byId('rc-drill-gapcount').textContent = String(run.gapCount ?? 0);
        byId('rc-drill-status').innerHTML = ui.drillStatusBadge(run.status);
        const note = run.resultNote || run.cancelReason;
        byId('rc-drill-note').textContent = note ? (run.cancelReason ? t('Recall.Drills.CancelledReason', run.cancelReason) : t('Recall.Drills.ResultNoteShown', run.resultNote)) : '';
        show(byId('rc-drill-note'), !!note);
        show(byId('rc-drill-sign'), offers.sign);
        tick();
    };

    const openRun = async (runId, scroll = false) => {
        ui.hideError(byId('rc-error'));
        const reply = await ui.get(`/Recall/data/drill-runs/${encodeURIComponent(runId)}`);
        if (!reply.ok) { ui.showError(byId('rc-error'), sentence(reply)); return; }
        state.run = reply.json.data;
        state.serverNow = reply.json.serverNowUtc;
        state.answeredAt = performance.now();
        state.setup = null;
        drawRun();
        if (scroll) byId('rc-drill-panel').scrollIntoView?.({ behavior: 'smooth', block: 'start' });
    };

    const load = async () => {
        const [plans, runs] = await Promise.all([ui.get('/Recall/data/drill-plans'), ui.get('/Recall/data/drill-runs?page=1&pageSize=12')]);
        if (!plans.ok) ui.showError(byId('rc-error'), sentence(plans));
        if (!runs.ok) ui.showError(byId('rc-error'), sentence(runs));
        state.plans = plans.ok ? plans.json.data || [] : [];
        state.runs = runs.ok ? runs.json.data?.items || [] : [];
        state.warning = runs.ok ? runs.json.data?.frequencyWarning : null;
        const target = state.runs[0]?.targetHours;
        byId('rc-drill-target').textContent = target ? t('Recall.Drills.Target', target) : '';
        drawWarning();
        drawPlans();
        drawRuns();
        const live = running();
        const asked = root.dataset.run;
        if (state.run) await openRun(state.run.runId);
        else if (live) await openRun(live.runId);
        else if (asked) await openRun(asked, true);
        root.dataset.run = '';
    };

    // ── start ────────────────────────────────────────────────────────────────────────────────────────────────────
    const openSetup = (plan) => {
        if (!plan) return;
        state.setup = plan;
        state.run = null;
        state.mode = 'Auto';
        byId('rc-drill-plan').textContent = plan.name;
        byId('rc-drill-lot').textContent = '—';
        byId('rc-drill-number').textContent = '';
        byId('rc-drill-clock').textContent = '00:00';
        byId('rc-drill-clock-sub').textContent = '';
        ['rc-drill-run', 'rc-drill-done'].forEach((id) => show(byId(id), false));
        show(byId('rc-drill-setup'), true);
        show(byId('rc-drill-panel'), true);
        setMode('Auto');
    };
    const setMode = async (mode) => {
        state.mode = mode;
        root.querySelectorAll('[data-mode]').forEach((b) => b.classList.toggle('active', b.dataset.mode === mode));
        show(byId('rc-drill-lot-box'), mode === 'Manual');
        if (mode === 'Manual' && byId('rc-drill-lot-select').options.length === 0) {
            const reply = await ui.get('/Recall/lookups/drill-lots?q=');
            byId('rc-drill-lot-select').innerHTML = (reply.ok ? reply.json.items || [] : []).map((l) => `<option value="${esc(l.value)}">${esc(l.text)}</option>`).join('');
            if (!reply.ok) ui.showError(byId('rc-error'), sentence(reply));
        }
    };
    root.querySelectorAll('[data-mode]').forEach((b) => b.addEventListener('click', () => setMode(b.dataset.mode)));
    byId('rc-drill-back')?.addEventListener('click', () => { state.setup = null; show(byId('rc-drill-panel'), false); });
    byId('rc-drill-start')?.addEventListener('click', async () => {
        ui.hideError(byId('rc-error'));
        const result = await view.start(deps, state.setup.planId, state.mode, byId('rc-drill-lot-select').value);
        byId('rc-drill-lot-select').classList.toggle('is-invalid', !!result.errors?.lot);
        if (!result.ok) { if (result.reply) ui.showError(byId('rc-error'), sentence(result.reply)); return; }
        state.setup = null;
        state.run = result.run;
        await load();
    });

    // ── steps, cancel ────────────────────────────────────────────────────────────────────────────────────────────
    byId('rc-drill-step')?.addEventListener('click', async () => {
        ui.hideError(byId('rc-error'));
        const result = await view.step(deps, state.run);
        if (!result.ok) ui.showError(byId('rc-error'), result.reply ? sentence(result.reply) : t('Recall.Errors.GENERIC'));
        await load();
    });
    byId('rc-drill-cancel')?.addEventListener('click', () => {
        byId('rc-reason-title').textContent = t('Recall.Drills.CancelTitle');
        byId('rc-reason-text').value = '';
        byId('rc-reason-text').classList.remove('is-invalid');
        ui.hideError(byId('rc-reason-error'));
        modal('rc-reason-modal').show();
    });
    byId('rc-reason-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const result = await view.cancel(deps, state.run, byId('rc-reason-text').value);
        byId('rc-reason-text').classList.toggle('is-invalid', !!result.errors?.reason);
        if (!result.ok) { if (result.reply) ui.showError(byId('rc-reason-error'), sentence(result.reply)); return; }
        modal('rc-reason-modal').hide();
        saved('Recall.Drills.CancelledDone');
        await load();
    });

    // ── the result: two steps (prepare → MOD-0022 → result) ─────────────────────────────────────────────────────
    byId('rc-drill-sign')?.addEventListener('click', () => {
        const run = state.run;
        const c = view.clock(run, state.serverNow, 0);
        state.prepared = null;
        byId('rc-sign-title').textContent = t('Recall.Drills.SignTitle');
        byId('rc-sign-lines').innerHTML = [
            t('Recall.Drills.SignLine.Duration', c ? ui.duration(c.minutes) : '—', run.targetHours, t(c?.over ? 'Recall.Badge.OverTarget' : 'Recall.Badge.OnTarget')),
            t('Recall.Drills.SignLine.Gaps', run.gapCount ?? 0),
            t('Recall.Drills.ReadOnly')
        ].map((l) => `<li>${esc(l)}</li>`).join('');
        document.querySelectorAll('#rc-sign-fields [data-sign-field]').forEach((f) => show(f, f.dataset.signField === 'note'));
        byId('rc-sign-note-label').textContent = t('Recall.Drills.ResultNote');
        byId('rc-sign-note').value = '';
        byId('rc-sign-note').disabled = false;
        byId('rc-sign-note').classList.remove('is-invalid');
        show(byId('rc-sign-ready'), false);
        ui.hideError(byId('rc-sign-error'));
        show(byId('rc-sign-next'), true);
        show(byId('rc-sign-commit'), false);
        byId('rc-sign-step1').className = 'fw-medium';
        byId('rc-sign-step2').className = 'text-muted';
        modal('rc-sign-modal').show();
    });
    byId('rc-sign-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        ui.hideError(byId('rc-sign-error'));
        const first = await view.prepareResult(deps, state.run, byId('rc-sign-note').value);
        byId('rc-sign-note').classList.toggle('is-invalid', !!first.errors?.note);
        if (!first.ok) { if (first.reply) ui.showError(byId('rc-sign-error'), sentence(first.reply)); return; }
        state.prepared = first.prepared;
        byId('rc-sign-note').disabled = true;
        byId('rc-sign-step1').className = 'text-muted';
        byId('rc-sign-step2').className = 'fw-medium';
        byId('rc-sign-ready').textContent = first.signatureRequired ? t('Recall.Sign.ToSign') : t('Recall.Sign.NoSignature');
        show(byId('rc-sign-ready'), true);
        byId('rc-sign-commit-text').textContent = t(first.signatureRequired ? 'Recall.Sign.SignSave' : 'Recall.Sign.Save');
        show(byId('rc-sign-next'), false);
        show(byId('rc-sign-commit'), true);
    });
    byId('rc-sign-commit')?.addEventListener('click', async () => {
        if (view.needsSignature(state.prepared) && !window.ESign?.open) { ui.showError(byId('rc-sign-error'), t('Recall.Errors.ESIGN_UNAVAILABLE')); return; }
        const result = await view.signResult(deps, state.run, state.prepared, byId('rc-sign-note').value);
        if (result.ok) {
            modal('rc-sign-modal').hide();
            saved('Recall.Drills.Signed');
            await load();
            return;
        }
        if (result.cancelled) { ui.showError(byId('rc-sign-error'), t('Recall.Sign.Cancelled')); return; }
        ui.showError(byId('rc-sign-error'), result.reply ? sentence(result.reply) : t('Recall.Errors.GENERIC'));
    });

    // ── the plan form ────────────────────────────────────────────────────────────────────────────────────────────
    // FU-0177-PLAN-OWNER: the current owner (or "me") is the first option; a search adds the active users of the caller's tenant.
    const ownerNow = () => ({ value: state.plan?.owner?.userId || me, text: state.plan ? person(state.plan.owner) : t('Recall.Drills.OwnerMe') });
    const ownerPicker = (options) => {
        const select = byId('rc-plan-owner-select');
        if (!select) return;
        select.innerHTML = options.map((o) => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    const findOwner = async () => {
        const reply = await ui.get(view.ownerSearch(byId('rc-plan-owner-search').value));
        const options = view.ownerOptions(reply.ok ? reply.json?.items : [], ownerNow());
        ownerPicker(options);
        if (options.length > 1) byId('rc-plan-owner-select').value = options[1].value;
        show(byId('rc-plan-owner-none'), reply.ok && options.length < 2);
        if (!reply.ok) ui.showError(byId('rc-plan-error'), sentence(reply));
    };
    byId('rc-plan-owner-find')?.addEventListener('click', findOwner);
    byId('rc-plan-owner-search')?.addEventListener('keydown', (event) => { if (event.key === 'Enter') { event.preventDefault(); findOwner(); } });
    const openPlan = (plan) => {
        state.plan = plan || null;
        byId('rc-plan-title').textContent = t(plan ? 'Recall.Drills.EditPlan' : 'Recall.Drills.AddPlan');
        byId('rc-plan-name').value = plan?.name || '';
        byId('rc-plan-frequency').value = plan?.frequency || 'Quarterly';
        byId('rc-plan-days').value = plan?.customDays || '';
        byId('rc-plan-scope').value = plan?.scopeKind || 'RandomFinishedGoodLot';
        byId('rc-plan-next').value = plan?.nextDueOn || '';
        byId('rc-plan-active').checked = plan ? plan.active : true;
        byId('rc-plan-owner').textContent = t('Recall.Drills.Owner', plan ? person(plan.owner) : t('Recall.Drills.OwnerMe'));
        ownerPicker(view.ownerOptions([], ownerNow()));
        if (byId('rc-plan-owner-search')) byId('rc-plan-owner-search').value = '';
        show(byId('rc-plan-days-box'), byId('rc-plan-frequency').value === 'CustomDays');
        ['rc-plan-name', 'rc-plan-days', 'rc-plan-next'].forEach((id) => byId(id).classList.remove('is-invalid'));
        ui.hideError(byId('rc-plan-error'));
        modal('rc-plan-modal').show();
    };
    byId('rc-plan-add')?.addEventListener('click', () => openPlan(null));
    byId('rc-plan-frequency')?.addEventListener('change', () => show(byId('rc-plan-days-box'), byId('rc-plan-frequency').value === 'CustomDays'));
    byId('rc-plan-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const input = {
            name: byId('rc-plan-name').value, frequency: byId('rc-plan-frequency').value, customDays: byId('rc-plan-days').value,
            scopeKind: byId('rc-plan-scope').value, nextDueOn: byId('rc-plan-next').value, active: byId('rc-plan-active').checked,
            ownerUserId: view.ownerOf(byId('rc-plan-owner-select')?.value, state.plan, me)
        };
        const result = await view.savePlan(deps, state.plan, input);
        byId('rc-plan-name').classList.toggle('is-invalid', !!result.errors?.name);
        byId('rc-plan-days').classList.toggle('is-invalid', !!result.errors?.customDays);
        byId('rc-plan-next').classList.toggle('is-invalid', !!result.errors?.nextDueOn);
        if (!result.ok) { if (result.reply) ui.showError(byId('rc-plan-error'), sentence(result.reply)); else if (result.errors?.owner) ui.showError(byId('rc-plan-error'), t('Recall.Errors.GENERIC')); return; }
        modal('rc-plan-modal').hide();
        saved('Recall.Drills.PlanSaved');
        await load();
    });

    setInterval(() => { if (state.run && state.run.status === 'Running') tick(); }, 1000);
    setInterval(() => { if (state.run && state.run.status === 'Running') openRun(state.run.runId); }, 60000);
    load();
})();
