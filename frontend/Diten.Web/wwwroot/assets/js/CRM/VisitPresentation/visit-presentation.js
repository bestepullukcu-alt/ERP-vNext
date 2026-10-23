/**
 * WP-VW-W3 (W3-WEB-b) — the Visit Presentation page: the doctor's screen (E4a), the rep's remote (E4b) and the finish
 * summary with the doctor's signature (E4c). The rules live in presentation-core.js (window.VisitPresentationCore); this
 * file reads, draws and wires. Every call goes through the same-origin proxy (data-api-base). The rep's notes (key
 * message, talk track, objections, own note, transition) are drawn in the REP panel only — never in the doctor area.
 */
(function () {
    'use strict';

    const root = document.getElementById('vp-root');
    if (!root) { return; }

    const C = window.VisitPresentationCore;
    const L = window.VisitPresentationL10n || {};
    const base = root.getAttribute('data-api-base') || '';
    const workspaceUrl = root.getAttribute('data-workspace-url') || '/CRM/VisitWorkspace';
    const reportUrl = root.getAttribute('data-report-url') || '/CRM/VisitReport';
    const plannedVisitId = root.getAttribute('data-planned-visit-id') || '';
    const summaryAsked = root.getAttribute('data-phase') === 'summary';
    const perms = {
        record: root.getAttribute('data-can-record') === 'true',
        visit: root.getAttribute('data-can-read-visit') === 'true',
        people: root.getAttribute('data-can-pick-people') === 'true'
    };
    const culture = (document.documentElement && document.documentElement.lang) || 'tr';
    const rtl = String((document.documentElement && document.documentElement.dir) || '').toLowerCase() === 'rtl';

    const el = id => document.getElementById(id);
    const esc = v => String(v == null ? '' : v).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fmt = C.fmt;
    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };

    const state = {
        phase: 'loading', device: C.deviceOf(window.innerWidth || 1280),
        slides: [], cur: 0, timers: { products: {}, slides: {} }, marks: {}, note: '', personalNotes: {}, coVisitorUserIds: [],
        kind: 'in_person', execution: null, policy: {}, brief: null, visit: null, categories: {}, categoryList: [],
        tab: 'notes', objectionPicker: false, openObjection: 0, pin: C.pinInitial(), panelOpen: false,
        visible: true, sinceCapture: 0, captureTimer: null, busy: false, signatureDrawn: false, signatureConfirmed: false,
        people: [], listening: false
    };

    // ── the API ──────────────────────────────────────────────────────────────────────────────────────────────
    async function api(method, path, body, multipart) {
        const options = { method: method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (multipart) {
            options.body = multipart;
        } else if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        try {
            const res = await fetch(base + path, options);
            const text = await res.text();
            let json = null;
            try { json = text ? JSON.parse(text) : null; } catch (e) { json = null; }
            const errors = (json && (json.errors || json.Errors)) || [];
            return { ok: res.ok, status: res.status, data: json ? (json.data !== undefined ? json.data : json.Data) : null, errors: errors };
        } catch (e) {
            return { ok: false, status: 0, data: null, errors: [] };
        }
    }

    const toast = (text, type) => { if (typeof window.showToast === 'function') { window.showToast(text, type || 'success'); } };

    // ── load ─────────────────────────────────────────────────────────────────────────────────────────────────
    async function load() {
        if (!plannedVisitId) { return notice(L.NotStartedText || '', false); }
        const id = encodeURIComponent(plannedVisitId);
        const [presentation, execution, brief, contract, visit] = await Promise.all([
            api('GET', '/presentation/' + id + '?lang=' + encodeURIComponent(culture)),
            api('GET', '/execution/by-plan/' + id),
            api('GET', '/execution/by-plan/' + id + '/brief'),
            api('GET', '/execution/contract'),
            perms.visit ? api('GET', '/visits/' + id) : Promise.resolve({ ok: false, data: null })
        ]);

        if (!execution.ok) { return notice(C.errorText(execution.errors, L) || L.LoadFailed || '', false); }
        state.slides = C.deck(presentation.ok ? presentation.data : null);
        (presentation.ok && presentation.data ? presentation.data.objectionCategories || [] : []).forEach(c => {
            state.categories[c.code] = c.label;
            state.categoryList.push(c);
        });
        state.brief = brief.ok ? brief.data : null;
        state.policy = contract.ok && contract.data ? contract.data.policy || {} : {};
        state.visit = visit.ok ? visit.data : null;
        state.execution = execution.data || null;

        if (!state.execution) {
            return notice(L.NotStartedText || '', true); // not started: a note + the way back, never a redirect
        }

        adopt(state.execution);
        if (state.execution.status === 'started' && summaryAsked) {
            await finish(); // the workspace's "finish the visit" lands here with phase=summary (mockup gap 1)
            return;
        }

        state.phase = state.execution.status === 'finished' ? 'summary' : 'run';
        if (state.phase === 'run' && state.device === 'tablet') { state.panelOpen = true; } // the rep sets the PIN first
        if (perms.people && state.phase === 'run') { loadPeople(); }
        render();
    }

    /** The execution's last capture becomes the page state (a reopened page continues where it stopped). */
    function adopt(execution) {
        state.execution = execution;
        state.timers = C.timers(execution);
        state.marks = C.marks(execution);
        state.note = execution.note || '';
        state.kind = execution.kind || 'in_person';
        state.personalNotes = {};
        (execution.personalNotes || []).forEach(n => { state.personalNotes[n.productId] = n.text; });
        state.coVisitorUserIds = (execution.coVisitors || []).map(v => v.userId);
    }

    async function loadPeople() {
        const res = await api('GET', '/people');
        state.people = res.ok && res.data ? res.data.people || [] : [];
        if (state.tab === 'note') { renderRep(); }
    }

    function notice(text, notStarted) {
        state.phase = notStarted ? 'notStarted' : 'error';
        const box = el('vp-notice');
        if (box) {
            box.innerHTML = (notStarted ? '<strong>' + esc(L.NotStartedTitle || '') + '</strong> ' : '') + esc(text)
                + ' <a href="' + esc(workspaceUrl) + '">' + esc(L.BackToWorkspace || '') + '</a>';
        }
        show('vp-notice', true);
        show('vp-loading', false);
        show('vp-split', false);
        show('vp-summary', false);
        show('vp-topbar', false);
    }

    // ── render ───────────────────────────────────────────────────────────────────────────────────────────────
    const current = () => state.slides[state.cur] || null;
    const visitName = () => (state.visit && (state.visit.targetDisplayName || state.visit.contactDisplayName)) || '';
    const accountName = () => (state.visit && state.visit.accountDisplayName) || '';
    const timeOf = iso => {
        if (!iso) { return ''; }
        try { return new Intl.DateTimeFormat(culture, { hour: '2-digit', minute: '2-digit' }).format(new Date(iso)); } catch (e) { return ''; }
    };
    const dateOf = ymd => {
        try { return new Intl.DateTimeFormat(culture, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(new Date(ymd + 'T00:00:00Z')); } catch (e) { return ymd; }
    };
    const productName = s => (s ? s.productName || s.productCode || '' : '');

    function render() {
        show('vp-loading', false);
        show('vp-notice', false);
        root.classList.remove('vp-desktop', 'vp-tablet', 'vp-phone', 'vp-panel-open');
        root.classList.add('vp-' + state.device);
        if (state.panelOpen) { root.classList.add('vp-panel-open'); }
        if (state.phase === 'summary') {
            show('vp-split', false);
            show('vp-topbar', false);
            show('vp-summary', true);
            renderSummary();
            return;
        }

        show('vp-summary', false);
        show('vp-split', true);
        show('vp-topbar', state.device !== 'phone');
        renderTopbar();
        renderDoc();
        renderRep();
        renderPin();
    }

    function renderTopbar() {
        const name = el('vp-visit-name');
        if (name) { name.textContent = visitName(); }
        const line = el('vp-visit-line');
        if (line) { line.textContent = fmt(L.StartedLine || '{0} · {1}', accountName(), timeOf(state.execution && state.execution.startedAt)); }
        const kinds = el('vp-kinds');
        if (kinds) {
            kinds.innerHTML = C.KINDS.map(k => '<button type="button" class="vp-seg-btn' + (state.kind === k ? ' active' : '')
                + '" data-act="kind" data-kind="' + k + '" aria-pressed="' + (state.kind === k) + '">' + esc(L['Kind_' + k] || k) + '</button>').join('');
        }
        updateClocks();
    }

    /** E4a — ONLY what the doctor may see: product, title, subtitle, approved bullets, image / placeholder, references. */
    function renderDoc() {
        const slide = current();
        const products = C.productOrder(state.slides);
        const tabs = el('vp-ptabs');
        if (tabs) {
            tabs.innerHTML = products.map(p => {
                const first = state.slides.find(s => s.productId === p);
                const on = slide && slide.productId === p;
                return '<button type="button" role="tab" class="vp-ptab' + (on ? ' active' : '') + '" aria-selected="' + !!on
                    + '" data-act="goto" data-index="' + first.position + '">' + esc(productName(first)) + '</button>';
            }).join('');
        }
        const hint = el('vp-doc-hint');
        if (hint) { hint.textContent = state.device === 'desktop' ? (L.DoctorScreenHint || '') : ''; }
        show('vp-exit-hold', state.device === 'tablet' && !state.panelOpen && state.pin.phase === 'locked');
        const hold = el('vp-exit-hold-text');
        if (hold) { hold.textContent = L.ExitHold || ''; }
        show('vp-handle', state.device === 'tablet' && !state.panelOpen && state.pin.phase === 'locked');

        const box = el('vp-slide');
        if (box) {
            if (!slide) {
                box.innerHTML = '<div class="vp-empty">' + esc(L.NoSlides || '') + '</div>';
            } else {
                const kicker = productName(slide) + (slide.role === 'promo' ? '' : ' · ' + (L.RoleReminder || ''));
                const image = slide.asset && slide.asset.kind === 'image' && slide.asset.url
                    ? '<img class="vp-img" alt="" src="' + esc(slide.asset.url) + '">'
                    : '<div class="vp-img vp-img-placeholder">' + esc(L.ImagePlaceholder || '') + '</div>';
                box.innerHTML = '<div class="vp-slide-text">'
                    + '<div class="vp-kicker">' + esc(kicker) + '</div>'
                    + '<div class="vp-title">' + esc(slide.title || '') + '</div>'
                    + (slide.subtitle ? '<div class="vp-subtitle">' + esc(slide.subtitle) + '</div>' : '')
                    + '<ul class="vp-bullets">' + (slide.bullets || []).map(b => '<li><i class="bx bx-check-circle"></i><span>' + esc(b) + '</span></li>').join('') + '</ul>'
                    + '</div>' + image;
            }
        }
        const ref = el('vp-ref');
        if (ref) { ref.textContent = slide ? (slide.references || []).join(' · ') : ''; }
        const dots = el('vp-dots');
        if (dots) {
            dots.innerHTML = state.slides.map((s, i) => '<span class="vp-dot' + (i === state.cur ? ' active' : (slide && s.productId === slide.productId ? ' same' : '')) + '"></span>').join('');
        }
        const no = el('vp-slide-no');
        if (no) { no.textContent = state.slides.length ? (state.cur + 1) + ' / ' + state.slides.length : ''; }
    }

    /** E4b — the rep's remote. */
    function renderRep() {
        const panel = el('vp-rep');
        if (!panel || state.phase !== 'run') { return; }
        const slide = current();
        const product = slide ? productName(slide) : '';
        const m = slide ? state.marks[slide.productId] || C.emptyMark() : C.emptyMark();
        const tablet = state.device === 'tablet';

        const head = '<div class="vp-rep-head">'
            + '<div class="d-flex justify-content-between align-items-center gap-2">'
            + (state.device === 'phone' ? '<a class="vp-icon-btn" href="' + esc(workspaceUrl) + '" aria-label="' + esc(L.BackToWorkspace || '') + '"><i class="bx bx-chevron-left vp-flip"></i></a>' : '')
            + '<div class="flex-grow-1" style="min-width:0"><div class="vp-muted small">' + esc(L.RepTitle || '') + '</div><div class="vp-rep-name">' + esc(visitName()) + '</div></div>'
            + (tablet && state.pin.phase === 'open' ? '<button type="button" class="vp-btn-outline" data-act="relock">' + esc(L.BackToDoctor || '') + '</button>' : '')
            + (tablet && state.pin.phase === 'none' ? '<button type="button" class="vp-btn-primary" data-act="doctor-mode"><i class="bx bx-lock-alt"></i> ' + esc(L.DoctorMode || '') + '</button>' : '')
            + '</div>'
            + '<div class="vp-strip"><span class="vp-pulse"></span><span class="vp-strip-product" id="vp-prod-timer"></span><span class="vp-strip-total" id="vp-total"></span></div>'
            + '</div>';

        const preview = '<div class="vp-rep-top">'
            + '<div class="d-flex gap-2 align-items-center"><div class="vp-mini">' + (slide ? '<span class="vp-mini-kick">' + esc(product) + '</span><span class="vp-mini-title">' + esc(slide.title || '') + '</span>' : '') + '</div>'
            + '<div class="flex-grow-1" style="min-width:0"><div class="vp-muted small">' + esc(fmt(L.DoctorSees || '{0}', state.slides.length ? (state.cur + 1) + ' / ' + state.slides.length : '')) + '</div>'
            + '<div class="vp-mini-line">' + esc(slide ? slide.title || '' : '') + '</div></div></div>'
            + '<div class="vp-nav">'
            + '<button type="button" class="vp-btn-outline" data-act="prev"><i class="bx bx-chevron-left vp-flip"></i> ' + esc(L.PrevSlide || '') + '</button>'
            + '<button type="button" class="vp-btn-primary" data-act="next">' + esc(L.NextSlide || '') + ' <i class="bx bx-chevron-right vp-flip"></i></button>'
            + '<button type="button" class="vp-btn-ghost" data-act="skip"><i class="bx bx-skip-next vp-flip"></i> ' + esc(L.SkipProduct || '') + '</button>'
            + '</div>'
            + '<div class="vp-tabs" role="tablist">'
            + [['notes', 'bx-notepad', L.TabNotes, 0], ['marks', 'bx-flag', L.TabMarks, C.markCount(m)], ['note', 'bx-edit', L.TabNote, state.note ? 1 : 0]]
                .map(t => '<button type="button" role="tab" class="vp-tab' + (state.tab === t[0] ? ' active' : '') + '" aria-selected="' + (state.tab === t[0])
                    + '" data-act="tab" data-tab="' + t[0] + '"><i class="bx ' + t[1] + '"></i>' + esc(t[2] || '') + (t[3] ? '<span class="vp-count">' + t[3] + '</span>' : '') + '</button>').join('')
            + '</div></div>';

        let body = '';
        if (state.tab === 'notes') { body = notesTab(slide, product); }
        else if (state.tab === 'marks') { body = marksTab(slide, product, m); }
        else { body = noteTab(); }

        const foot = '<div class="vp-rep-foot">' + (perms.record
            ? '<button type="button" class="vp-btn-finish" data-act="finish"><i class="bx bx-stop-circle"></i> ' + esc(L.FinishVisit || '') + '</button>'
            : '') + '</div>';

        panel.innerHTML = head + '<div class="vp-rep-body">' + preview + '<div class="vp-tab-body" id="vp-tab-body">' + body + '</div></div>' + foot;
        updateClocks();
        if (state.tab === 'note') { fillPeople(); }
    }

    function notesTab(slide, product) {
        if (!slide) { return '<div class="vp-muted">' + esc(L.NoSlides || '') + '</div>'; }
        const notes = slide.presentation || {};
        const elapsed = C.slideSeconds(state.timers, slide);
        const target = C.target(notes.targetSeconds, culture, L);
        const bar = C.slideBar(elapsed, notes.targetSeconds);
        const brief = slide.firstOfProduct ? C.briefLines(state.brief, L, dateOf) : [];
        let html = '';
        if (notes.keyMessage) {
            html += '<div class="vp-key"><span class="vp-cap">' + esc(L.KeyMessage || '') + '</span><span class="vp-key-text">' + esc(notes.keyMessage) + '</span></div>';
        }
        html += '<div class="vp-slide-timer' + (bar.over ? ' over' : '') + '" id="vp-slide-timer"><div class="d-flex justify-content-between small"><span><i class="bx bx-timer"></i> <span id="vp-slide-el">'
            + esc(fmt(L.ThisSlide || '{0}', C.clock(elapsed))) + '</span></span>'
            + (target ? '<span class="vp-muted">' + esc(fmt(L.TargetLabel || '{0}', target)) + '</span>' : '') + '</div>'
            + (target ? '<div class="vp-bar"><span id="vp-slide-bar" style="width:' + bar.pct + '%"></span></div>' : '') + '</div>';
        if (notes.talkTrack) {
            html += '<div class="vp-block"><span class="vp-cap">' + esc(L.TalkTrack || '') + '</span><div class="vp-talk">' + esc(notes.talkTrack) + '</div></div>';
        }
        if (brief.length) {
            html += '<div class="vp-brief"><span class="vp-cap">' + esc(L.ForThisDoctor || '') + '</span>'
                + brief.map(x => '<span class="vp-brief-line"><i class="bx bx-chevron-right vp-flip"></i>' + esc(x) + '</span>').join('') + '</div>';
        }
        if ((slide.objections || []).length) {
            html += '<div class="vp-block"><span class="vp-cap">' + esc(L.Objections || '') + '</span>'
                + slide.objections.map((o, j) => {
                    const open = state.openObjection === j;
                    return '<div class="vp-acc"><button type="button" class="vp-acc-head" data-act="toggle-objection" data-index="' + j + '" aria-expanded="' + open + '">'
                        + '<i class="bx bx-message-error"></i><span class="flex-grow-1">' + esc(o.question || '') + '</span><i class="bx ' + (open ? 'bx-chevron-up' : 'bx-chevron-down') + '"></i></button>'
                        + (open ? '<div class="vp-acc-body"><div>' + esc(o.answer || '') + '</div>'
                            + ((o.references || []).length ? '<div class="vp-muted small">' + esc(o.references.join(' · ')) + '</div>' : '') + '</div>' : '')
                        + '</div>';
                }).join('') + '</div>';
        }
        html += '<div class="vp-block"><label class="vp-cap vp-own-cap" for="vp-own-note"><i class="bx bx-lock-alt"></i> ' + esc(fmt(L.MyNote || '{0}', product)) + '</label>'
            + '<textarea class="vp-own" id="vp-own-note" rows="2" data-field="personal" data-product="' + esc(slide.productId) + '" placeholder="' + esc(L.MyNotePlaceholder || '') + '"'
            + (perms.record ? '' : ' disabled') + '>' + esc(state.personalNotes[slide.productId] || '') + '</textarea></div>';
        if (notes.transition) {
            html += '<div class="vp-transition"><i class="bx bx-right-arrow-alt vp-flip"></i><span><span class="vp-cap d-block">' + esc(L.TransitionHeading || '') + '</span>' + esc(notes.transition) + '</span></div>';
        }
        return html;
    }

    function marksTab(slide, product, m) {
        if (!slide) { return ''; }
        const icons = { interested: 'bx-like', neutral: 'bx-minus-circle', not_interested: 'bx-dislike' };
        const flagIcons = { question: 'bx-help-circle', objection: 'bx-message-error', sample_request: 'bx-gift', literature_request: 'bx-book-open', follow_up: 'bx-flag' };
        let html = '<div class="vp-cap">' + esc(fmt(L.QuickMarks || '{0}', product)) + '</div>'
            + '<div class="vp-interest">' + C.INTERESTS.map(i => '<button type="button" class="vp-int vp-int-' + i + (m.interest === i ? ' active' : '')
                + '" data-act="interest" data-interest="' + i + '" aria-pressed="' + (m.interest === i) + '"><i class="bx ' + icons[i] + '"></i>' + esc(L['Interest_' + i] || i) + '</button>').join('') + '</div>'
            + '<div class="vp-flags">' + C.FLAGS.map(f => {
                const on = m.flags.indexOf(f) >= 0;
                const label = f === 'objection' && on && m.objectionCategory
                    ? fmt(L.ObjectionWith || '{0}: {1}', L.Flag_objection || f, state.categories[m.objectionCategory] || m.objectionCategory)
                    : (L['Flag_' + f] || f);
                return '<button type="button" class="vp-flag' + (on ? ' active' : '') + '" data-act="flag" data-flag="' + f + '" aria-pressed="' + on + '"><i class="bx ' + flagIcons[f] + '"></i>' + esc(label) + '</button>';
            }).join('') + '</div>';
        if (state.objectionPicker) {
            html += '<div class="vp-cats"><div class="vp-muted small">' + esc(L.ObjectionCategory || '') + '</div><div class="d-flex flex-wrap gap-1">'
                + state.categoryList.map(c => '<button type="button" class="vp-cat' + (m.objectionCategory === c.code ? ' active' : '') + '" data-act="category" data-category="' + esc(c.code) + '">' + esc(c.label) + '</button>').join('')
                + '</div></div>';
        }
        return html;
    }

    function noteTab() {
        const voice = !!(window.SpeechRecognition || window.webkitSpeechRecognition);
        let html = '<div class="vp-block"><div class="d-flex justify-content-between align-items-center"><label class="vp-cap" for="vp-visit-note">' + esc(L.VisitNote || '') + '</label>'
            + (voice ? '<button type="button" class="vp-voice' + (state.listening ? ' on' : '') + '" data-act="voice"><i class="bx bx-microphone"></i>' + esc(state.listening ? L.Listening || '' : L.VoiceNote || '') + '</button>' : '')
            + '</div><textarea class="form-control" id="vp-visit-note" rows="5" data-field="note" placeholder="' + esc(L.VisitNotePlaceholder || '') + '"' + (perms.record ? '' : ' disabled') + '>' + esc(state.note) + '</textarea></div>';
        if (perms.people) {
            const joint = state.coVisitorUserIds.length > 0 || state.jointOpen;
            html += '<div class="vp-block vp-joint"><label class="vp-switch"><span>' + esc(L.JointVisit || '') + '</span><input type="checkbox" id="vp-joint" data-field="joint"' + (joint ? ' checked' : '') + '></label>'
                + (joint ? '<select class="form-select" id="vp-co-visitors" data-field="co-visitors" multiple aria-label="' + esc(L.JointVisit || '') + '"></select><div class="vp-muted small">' + esc(fmt(L.CoVisitorsMax || '{0}', C.MAX_CO_VISITORS)) + '</div>' : '')
                + '</div>';
        }
        if (state.policy && state.policy.durationConsistency) {
            html += '<div class="vp-muted small"><i class="bx bx-timer"></i> ' + esc(fmt(L.DurationConsistency || '{0}', C.clock(state.policy.shortVisitSeconds || 120))) + '</div>';
        }
        return html;
    }

    /** The co-visitor select: the shared person picker's rows (the Meetings attendee lookup). */
    function fillPeople() {
        const select = el('vp-co-visitors');
        if (!select) { return; }
        if (window.DitenPersonPicker && typeof window.DitenPersonPicker.renderPersonOptions === 'function') {
            window.DitenPersonPicker.renderPersonOptions(select, state.people, { placeholder: L.CoVisitorsPlaceholder || '', empty: L.NoPeople || '' }, { multiple: true });
        } else {
            select.innerHTML = state.people.map(p => '<option value="' + esc(p.userId) + '">' + esc(p.displayName || '') + '</option>').join('');
        }
        Array.prototype.forEach.call(select.options || [], o => { o.selected = state.coVisitorUserIds.indexOf(o.value) >= 0; });
    }

    /** The clocks change every second — only their text, never the panel (a textarea keeps its focus). */
    function updateClocks() {
        const slide = current();
        const total = C.clock(C.totalSeconds(state.timers));
        const live = el('vp-live-text');
        // CT (live E4): the chip is the visit's own clock (server start → now); the rep strip's total stays the presented time
        const visit = state.execution && state.execution.startedAt ? C.clock(C.sinceStart(state.execution.startedAt, Date.now())) : total;
        if (live) { live.textContent = fmt(L.LiveChip || '{0}', visit); }
        const prod = el('vp-prod-timer');
        if (prod) { prod.textContent = slide ? productName(slide) + ' ' + C.clock(C.productSeconds(state.timers, slide.productId)) : ''; }
        const tot = el('vp-total');
        if (tot) { tot.textContent = fmt(L.TotalLabel || '{0}', total); }
        if (slide) {
            const elapsed = C.slideSeconds(state.timers, slide);
            const se = el('vp-slide-el');
            if (se) { se.textContent = fmt(L.ThisSlide || '{0}', C.clock(elapsed)); }
            const bar = C.slideBar(elapsed, slide.presentation && slide.presentation.targetSeconds);
            const sb = el('vp-slide-bar');
            if (sb) { sb.style.width = bar.pct + '%'; }
            const box = el('vp-slide-timer');
            if (box) { box.classList.toggle('over', bar.over); }
        }
    }

    function renderPin() {
        const box = el('vp-pin');
        if (!box) { return; }
        const phase = state.pin.phase;
        const open = phase === 'set' || phase === 'confirm' || phase === 'unlock';
        box.classList.toggle('d-none', !open);
        if (!open) { box.innerHTML = ''; return; }
        const title = phase === 'set' ? L.PinSetTitle : phase === 'confirm' ? L.PinConfirmTitle : L.PinUnlockTitle;
        const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', 'back'];
        box.innerHTML = '<div class="vp-pin-card"><div class="vp-pin-title">' + esc(title || '') + '</div>'
            + '<div class="vp-pin-dots">' + [0, 1, 2, 3].map(i => '<span class="' + (i < state.pin.buffer.length ? 'on' : '') + '"></span>').join('') + '</div>'
            + (state.pin.error ? '<div class="vp-pin-error">' + esc(phase === 'unlock' ? L.PinWrong || '' : L.PinMismatch || '') + '</div>' : '')
            + '<div class="vp-pin-keys">' + keys.map(k => k === ''
                ? '<span></span>'
                : '<button type="button" data-act="' + (k === 'back' ? 'pin-back' : 'pin-digit') + '" data-digit="' + k + '"' + (k === 'back' ? ' aria-label="⌫"' : '') + '>' + (k === 'back' ? '⌫' : k) + '</button>').join('') + '</div>'
            + '<button type="button" class="vp-link" data-act="pin-cancel">' + esc(L.CancelButton || '') + '</button></div>';
    }

    function renderConfirm(open) {
        const box = el('vp-confirm');
        if (!box) { return; }
        box.classList.toggle('d-none', !open);
        box.innerHTML = open
            ? '<div class="vp-pin-card vp-confirm-card"><div class="vp-pin-title">' + esc(L.FinishConfirmTitle || '') + '</div><div class="vp-muted">' + esc(L.FinishConfirmText || '') + '</div>'
              + '<div class="d-flex gap-2 w-100"><button type="button" class="vp-btn-outline flex-fill" data-act="confirm-no">' + esc(L.CancelButton || '') + '</button>'
              + '<button type="button" class="vp-btn-finish flex-fill" data-act="confirm-yes">' + esc(L.FinishConfirmYes || '') + '</button></div></div>'
            : '';
    }

    /**
     * FU-VISIT-END-SEPARATOR (W6-FIX-b) — "who · where · from–to": a visit with no institution has no middle part, and
     * the line then has no empty slot between two separators ("AYŞE ABACI · · 17:57" before).
     */
    function summaryLine(e) {
        const account = accountName();
        return account
            ? fmt(L.SummaryLine || '{0} · {1} · {2}–{3}', visitName(), account, timeOf(e.startedAt), timeOf(e.finishedAt))
            : fmt(L.SummaryLineNoAccount || '{0} · {1}–{2}', visitName(), timeOf(e.startedAt), timeOf(e.finishedAt));
    }

    /** E4c — the finish summary (from the SERVER's execution: its timings, marks, note, co-visitors, shortVisit). */
    function renderSummary() {
        const box = el('vp-summary');
        if (!box) { return; }
        const e = state.execution || {};
        const t = C.timers(e);
        const marks = C.marks(e);
        const rows = C.summaryRows(state.slides, { timers: t, marks: marks, categories: state.categories }, L);
        const total = C.totalSeconds(t);
        const showSignature = perms.record && C.signatureCard(state.device, state.policy, marks, e);
        const sample = C.signatureContext(marks) === 'sample';
        const signed = !!(e.signature && e.signature.present);

        let html = '<div class="vp-sum-head"><div class="vp-sum-icon"><i class="bx bx-check-circle"></i></div><div>'
            + '<div class="vp-sum-title">' + esc(L.SummaryTitle || '') + '</div>'
            + '<div class="vp-muted">' + esc(summaryLine(e)) + '</div></div></div>';

        html += '<div class="vp-card"><div class="d-flex justify-content-between align-items-baseline"><span class="vp-card-cap">' + esc(L.DurationHeading || '') + '</span><span class="vp-sum-total">' + esc(C.clock(e.durationSeconds != null ? e.durationSeconds : total)) + '</span></div>'
            + rows.map(r => '<div class="vp-row"><div class="d-flex justify-content-between"><span><strong>' + esc(r.name) + '</strong> <span class="vp-muted">· ' + esc(r.role === 'promo' ? L.RolePromo || '' : L.RoleReminder || '') + '</span></span><span>' + esc(fmt(L.MinutesClock || '{0}', C.clock(r.seconds))) + '</span></div>'
                + '<div class="vp-bar vp-bar-lg"><span style="width:' + r.pct + '%"></span></div>'
                + '<div class="d-flex flex-wrap gap-1">' + r.tags.map(tg => '<span class="vp-tag vp-tag-' + esc(tg.key) + '">' + esc(tg.text) + '</span>').join('') + '</div></div>').join('')
            + (e.shortVisit ? '<div class="vp-warn"><i class="bx bx-error"></i>' + esc(fmt(L.ShortVisitWarning || '{0}', C.target((state.policy && state.policy.shortVisitSeconds) || 120, culture, L) || '')) + '</div>' : '')
            + '</div>';

        const coNames = (e.coVisitors || []).map(v => v.displayName).filter(Boolean);
        if (e.note || coNames.length) {
            html += '<div class="vp-card"><span class="vp-card-cap">' + esc(L.NotesHeading || '') + '</span>'
                + (e.note ? '<div class="vp-pre">' + esc(e.note) + '</div>' : '')
                + (coNames.length ? '<div class="small">' + esc(fmt(L.CoVisitorsLine || '{0}', coNames.join(', '))) + '</div>' : '') + '</div>';
        }

        if (signed) {
            html += '<div class="vp-card vp-signed"><i class="bx bx-check-shield"></i> ' + esc(L.Signed || '') + '</div>';
        } else if (showSignature) {
            html += '<div class="vp-card" id="vp-sig-card"><div class="d-flex justify-content-between align-items-center"><span class="vp-card-cap">' + esc(sample ? L.SignatureTitleSample || '' : L.SignatureTitleApproval || '') + '</span>'
                + '<button type="button" class="vp-link" data-act="clear-signature">' + esc(L.ClearSignature || '') + '</button></div>'
                + '<div class="small">' + esc(sample ? L.SignatureTextSample || '' : L.SignatureTextApproval || '') + '</div>'
                + '<canvas class="vp-canvas" id="vp-sig" width="700" height="160"></canvas>'
                + '<label class="vp-check"><input type="checkbox" id="vp-sig-confirm" data-field="sig-confirm"' + (state.signatureConfirmed ? ' checked' : '') + '> ' + esc(L.ConfirmVisit || '') + '</label>'
                + '<button type="button" class="vp-btn-primary" id="vp-sig-save" data-act="save-signature"' + (state.signatureDrawn && state.signatureConfirmed ? '' : ' disabled') + '>' + esc(L.SignatureSave || '') + '</button></div>';
        }

        html += '<div class="vp-sum-actions">'
            + '<button type="button" class="vp-btn-primary vp-big" data-act="report"><i class="bx bx-edit"></i> ' + esc(L.EnterReport || '') + '</button>'
            + '<button type="button" class="vp-btn-outline vp-big" data-act="later"><span>' + esc(L.Later || '') + '</span><span class="vp-muted small d-block">' + esc(L.LaterHint || '') + '</span></button>'
            + '</div>';

        box.innerHTML = '<div class="vp-sum-inner">' + html + '</div>';
        bindCanvas();
    }

    // ── the signature canvas (plain canvas, no library) ──────────────────────────────────────────────────────
    function bindCanvas() {
        const canvas = el('vp-sig');
        if (!canvas || typeof canvas.getContext !== 'function') { return; }
        const ctx = canvas.getContext('2d');
        // W3E-THEME (K-THEME) — the ink is the layout's primary colour (Template Customizer → :root --bs-primary)
        const primaryInk = () => (window.getComputedStyle ? window.getComputedStyle(document.documentElement).getPropertyValue('--bs-primary') : '').trim() || '#1d2129';
        let down = false;
        const pos = e => {
            const r = canvas.getBoundingClientRect();
            return [(e.clientX - r.left) * canvas.width / r.width, (e.clientY - r.top) * canvas.height / r.height];
        };
        canvas.addEventListener('pointerdown', e => {
            down = true;
            if (canvas.setPointerCapture) { canvas.setPointerCapture(e.pointerId); }
            ctx.strokeStyle = primaryInk(); // W3E-THEME — the layout's primary colour, read when the stroke starts
            ctx.lineWidth = 2.5;
            ctx.lineCap = 'round';
            ctx.beginPath();
            const p = pos(e);
            ctx.moveTo(p[0], p[1]);
        });
        canvas.addEventListener('pointermove', e => {
            if (!down) { return; }
            const p = pos(e);
            ctx.lineTo(p[0], p[1]);
            ctx.stroke();
        });
        canvas.addEventListener('pointerup', () => {
            down = false;
            state.signatureDrawn = true;
            syncSignatureButton();
        });
    }

    function syncSignatureButton() {
        const btn = el('vp-sig-save');
        if (btn) { btn.disabled = !(state.signatureDrawn && state.signatureConfirmed) || state.busy; }
    }

    // ── actions ──────────────────────────────────────────────────────────────────────────────────────────────
    function go(index) {
        const next = C.step(index, 0, state.slides.length);
        if (next === state.cur) { return; }
        state.cur = next;
        state.openObjection = 0;
        state.objectionPicker = false;
        renderDoc();
        renderRep();
        scheduleCapture(); // a slide change sends the timers (debounced)
    }

    function scheduleCapture() {
        if (!perms.record || state.phase !== 'run') { return; }
        if (state.captureTimer) { clearTimeout(state.captureTimer); }
        state.captureTimer = setTimeout(() => { state.captureTimer = null; capture(); }, C.CAPTURE_DEBOUNCE_MS);
    }

    const body = () => C.captureBody({
        kind: state.kind, slides: state.slides, timers: state.timers, marks: state.marks, note: state.note,
        personalNotes: state.personalNotes, coVisitorUserIds: state.coVisitorUserIds
    });

    async function capture() {
        if (!perms.record || state.phase !== 'run' || !state.execution) { return; }
        state.sinceCapture = 0;
        const res = await api('PUT', '/execution/' + encodeURIComponent(state.execution.id) + '/capture', body());
        if (!res.ok) {
            if ((res.errors || []).indexOf('visit_execution_finished') >= 0) { return reloadSummary(); }
            toast(C.errorText(res.errors, L) || L.CaptureFailed || '', 'error');
        }
    }

    async function reloadSummary() {
        const res = await api('GET', '/execution/by-plan/' + encodeURIComponent(plannedVisitId));
        if (res.ok && res.data) { adopt(res.data); }
        state.phase = 'summary';
        render();
    }

    /** The finish: the last capture + the SERVER's finishedAt. Only the panel's own button (or phase=summary) calls it;
     *  the PIN exit never does. */
    async function finish() {
        if (!perms.record || !state.execution || state.busy) { return; }
        state.busy = true;
        if (state.captureTimer) { clearTimeout(state.captureTimer); state.captureTimer = null; }
        const res = await api('POST', '/execution/' + encodeURIComponent(state.execution.id) + '/finish', body());
        state.busy = false;
        if (!res.ok) {
            if ((res.errors || []).indexOf('visit_execution_finished') >= 0) { return reloadSummary(); }
            toast(C.errorText(res.errors, L), 'error');
            state.phase = 'run';
            render();
            return;
        }
        adopt(res.data);
        state.phase = 'summary';
        render();
    }

    async function saveSignature() {
        const canvas = el('vp-sig');
        if (!canvas || state.busy || !state.signatureDrawn || !state.signatureConfirmed) { return; }
        state.busy = true;
        syncSignatureButton();
        const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
        const form = new FormData();
        form.append('file', blob, 'signature.png');
        form.append('confirmed', 'true');
        form.append('context', C.signatureContext(C.marks(state.execution)));
        const res = await api('POST', '/execution/' + encodeURIComponent(state.execution.id) + '/signature', undefined, form);
        state.busy = false;
        // CT (live E4): a duplicate delivery answered "already signed" although the signature IS stored — read the
        // execution again and show the signed state instead of an error.
        if (!res.ok && (res.errors || []).indexOf('visit_signature_exists') > -1) {
            const again = await api('GET', '/execution/by-plan/' + encodeURIComponent(plannedVisitId));
            if (again.ok && again.data && again.data.signature && again.data.signature.present) {
                state.execution = again.data;
                toast(L.SignatureSaved || '');
                renderSummary();
                return;
            }
        }
        if (!res.ok) {
            toast(C.errorText(res.errors, L), 'error');
            syncSignatureButton();
            return;
        }
        state.execution = res.data;
        toast(L.SignatureSaved || '');
        renderSummary();
    }

    function setPin(event) {
        state.pin = C.pin(state.pin, event);
        if (state.pin.effect === 'locked') { state.panelOpen = false; }
        if (state.pin.effect === 'panel') { state.panelOpen = true; }
        render();
    }

    function voiceNote() {
        const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!Recognition || state.listening) { return; }
        const rec = new Recognition();
        rec.lang = culture;
        rec.onresult = e => {
            const text = Array.prototype.map.call(e.results || [], r => (r[0] && r[0].transcript) || '').join(' ').trim();
            if (text) { state.note = (state.note ? state.note + '\n' : '') + text; scheduleCapture(); }
        };
        rec.onend = () => { state.listening = false; renderRep(); };
        state.listening = true;
        renderRep();
        rec.start();
    }

    // ── wiring ───────────────────────────────────────────────────────────────────────────────────────────────
    root.addEventListener('click', e => {
        const t = e.target && e.target.closest ? e.target.closest('[data-act]') : null;
        if (!t) { return; }
        const act = t.getAttribute('data-act');
        const slide = current();
        switch (act) {
            case 'prev': go(C.step(state.cur, -1, state.slides.length)); break;
            case 'next': go(C.step(state.cur, 1, state.slides.length)); break;
            case 'skip': {
                const j = C.skipProduct(state.slides, state.cur);
                if (j < 0) { toast(L.LastProduct || '', 'info'); } else { go(j); }
                break;
            }
            case 'goto': go(Number(t.getAttribute('data-index')) || 0); break;
            case 'tab': state.tab = t.getAttribute('data-tab'); renderRep(); break;
            case 'kind': state.kind = t.getAttribute('data-kind'); renderTopbar(); scheduleCapture(); break;
            case 'interest': if (slide) { C.setInterest(state.marks, slide.productId, t.getAttribute('data-interest')); renderRep(); scheduleCapture(); } break;
            case 'flag': {
                if (!slide) { break; }
                const flag = t.getAttribute('data-flag');
                const m = state.marks[slide.productId] || C.emptyMark();
                if (flag === 'objection' && m.flags.indexOf('objection') < 0) {
                    state.objectionPicker = true; // "objected" asks the category first
                } else {
                    C.toggleFlag(state.marks, slide.productId, flag);
                    if (flag === 'objection') { state.objectionPicker = false; }
                    scheduleCapture();
                }
                renderRep();
                break;
            }
            case 'category':
                if (slide) { C.setObjection(state.marks, slide.productId, t.getAttribute('data-category')); state.objectionPicker = false; renderRep(); scheduleCapture(); }
                break;
            case 'toggle-objection': {
                const j = Number(t.getAttribute('data-index'));
                state.openObjection = state.openObjection === j ? -1 : j;
                renderRep();
                break;
            }
            case 'finish': renderConfirm(true); break;
            case 'confirm-no': renderConfirm(false); break;
            case 'confirm-yes': renderConfirm(false); finish(); break;
            case 'doctor-mode': setPin({ type: 'begin' }); break;
            case 'relock': setPin({ type: 'relock' }); break;
            case 'pin-digit': setPin({ type: 'digit', digit: t.getAttribute('data-digit') }); break;
            case 'pin-back': setPin({ type: 'back' }); break;
            case 'pin-cancel': setPin({ type: 'cancel' }); break;
            case 'voice': voiceNote(); break;
            case 'clear-signature': {
                const canvas = el('vp-sig');
                if (canvas && canvas.getContext) { canvas.getContext('2d').clearRect(0, 0, canvas.width, canvas.height); }
                state.signatureDrawn = false;
                syncSignatureButton();
                break;
            }
            case 'save-signature': saveSignature(); break;
            case 'report': window.location.href = reportUrl + (reportUrl.indexOf('?') > -1 ? '&' : '?') + 'plannedVisitId=' + encodeURIComponent(plannedVisitId); break; // WP-VW-W4: the wizard v2
            case 'later': window.location.href = workspaceUrl; break;
        }
    });

    root.addEventListener('input', e => {
        const t = e.target;
        const field = t && t.getAttribute ? t.getAttribute('data-field') : null;
        if (field === 'note') { state.note = t.value; scheduleCapture(); }
        if (field === 'personal') { state.personalNotes[t.getAttribute('data-product')] = t.value; scheduleCapture(); }
    });

    root.addEventListener('change', e => {
        const t = e.target;
        const field = t && t.getAttribute ? t.getAttribute('data-field') : null;
        if (field === 'joint') {
            state.jointOpen = !!t.checked;
            if (!t.checked) { state.coVisitorUserIds = []; scheduleCapture(); }
            renderRep();
        }
        if (field === 'co-visitors') {
            const picked = Array.prototype.filter.call(t.options || [], o => o.selected).map(o => o.value);
            if (picked.length > C.MAX_CO_VISITORS) {
                toast(fmt(L.CoVisitorsMax || '{0}', C.MAX_CO_VISITORS), 'warning');
                Array.prototype.forEach.call(t.options || [], o => { o.selected = state.coVisitorUserIds.indexOf(o.value) >= 0; });
                return;
            }
            state.coVisitorUserIds = picked;
            scheduleCapture();
        }
        if (field === 'sig-confirm') { state.signatureConfirmed = !!t.checked; syncSignatureButton(); }
    });

    // the tablet's long press (the "exit" button and the edge handle) → the PIN → the rep panel. Never a finish.
    let holdTimer = null;
    root.addEventListener('pointerdown', e => {
        const t = e.target && e.target.closest ? e.target.closest('[data-hold]') : null;
        if (t) {
            if (holdTimer) { clearTimeout(holdTimer); }
            holdTimer = setTimeout(() => { holdTimer = null; setPin({ type: 'request' }); }, C.LONG_PRESS_MS);
            return;
        }
        const slide = e.target && e.target.closest ? e.target.closest('#vp-slide') : null;
        if (slide) { state.swipeX = e.clientX; }
    });
    const releaseHold = () => { if (holdTimer) { clearTimeout(holdTimer); holdTimer = null; } };
    root.addEventListener('pointerup', e => {
        releaseHold();
        if (state.swipeX != null) {
            const s = C.swipeStep(e.clientX - state.swipeX, rtl);
            state.swipeX = null;
            if (s) { go(C.step(state.cur, s, state.slides.length)); }
        }
    });
    root.addEventListener('pointerleave', releaseHold);

    // the timers: one second at a time, only while the page is visible; a capture every 30 s
    document.addEventListener('visibilitychange', () => {
        state.visible = document.visibilityState !== 'hidden';
        if (!state.visible) { capture(); } // leaving the page sends what was counted
    });
    window.setInterval(() => {
        if (state.phase !== 'run') { return; }
        C.tick(state.timers, current(), state.visible, 1);
        if (state.visible) { state.sinceCapture++; }
        updateClocks();
        if (state.sinceCapture >= C.CAPTURE_EVERY_SECONDS) { capture(); }
    }, 1000);

    window.addEventListener('resize', () => {
        const device = C.deviceOf(window.innerWidth || 1280);
        if (device !== state.device) { state.device = device; if (state.phase === 'run' || state.phase === 'summary') { render(); } }
    });

    load();
})();
