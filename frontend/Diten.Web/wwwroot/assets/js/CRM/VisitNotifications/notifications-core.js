/**
 * WP-VW-W5 (W5-WEB) — the sent visit e-mails page (mockup 06-epostalar): its PURE rules — no DOM, no fetch, no clock (the
 * page passes "today"). Loaded before index.js / settings.js; the Web tests run these functions in Node. Texts are KEYS
 * (the page resolves them through window.L10n), never words.
 *
 *   EVENTS / eventKey(code)      the four visit report events. The Platform catalogue has no hyphen in a code
 *                                (crm.visitreport.adverseevent — §37 PLT-NOTIFY); rows written before W5-FIX carry the
 *                                hyphenated form (crm.visit-report.adverse-event). BOTH name the same event here.
 *   statusOf(status, notes)      the status badge: sent · queued · failed · no recipient · PV / MSL route pending
 *   recipientsOf(item)           who the e-mail was for, as ROLES (line manager · upper manager · PV · MSL) + the rep's
 *                                name — the twin of the CRM's VisitNotificationSender.Build: a mailbox without an address
 *                                is dropped (route pending) and, when nobody is left in "to", "cc" moves up. NEVER an
 *                                e-mail address: the CRM keeps none and the page shows none.
 *   template(key, vars)          the preview of the e-mail (06 mockup): kind, subject, band, title, intro, rows,
 *                                sections, note, button — from the CRM's template variables (no patient data)
 *   maskEmails(text)             an address typed into a free text (a note) is masked in the preview
 *   rangeOf(period, today, from, to)   the period filter (the baseline "" = the last 30 days) — the proxy's twin
 *   validEmail(v) / settingsBody(form, version)   the settings card (the CRM's own rule: one address, ≤ 254)
 *   errorKey(response)           a CRM refusal → the user's text key (409 = the version conflict)
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.VisitNotificationsCore = api; }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    /** The four events: the label key, the mockup's icon and tone. */
    const EVENTS = [
        { key: 'submitted', icon: 'bx-file', tone: 'primary' },
        { key: 'adverseevent', icon: 'bx-error', tone: 'danger' },
        { key: 'offlabelrequest', icon: 'bx-help-circle', tone: 'info' },
        { key: 'overdue', icon: 'bx-lock-alt', tone: 'danger' }
    ];
    const EVENT_KEYS = EVENTS.map((e) => e.key);

    /** The event of a code, hyphenated (before W5-FIX) or not (the catalogue's): "crm.visit-report.adverse-event" and
     *  "crm.visitreport.adverseevent" are both "adverseevent"; anything else is null. */
    const eventKey = (code) => {
        const m = /^crm\.visitreport\.([a-z]+)$/.exec(String(code == null ? '' : code).trim().toLowerCase().replace(/-/g, ''));
        return m && EVENT_KEYS.indexOf(m[1]) > -1 ? m[1] : null;
    };
    const eventOf = (code) => EVENTS.find((e) => e.key === eventKey(code)) || null;

    const STATUSES = {
        sent: { key: 'St_sent', tone: 'success', icon: 'bx-check-circle' },
        queued: { key: 'St_queued', tone: 'warning', icon: 'bx-time-five' },
        failed: { key: 'St_failed', tone: 'danger', icon: 'bx-error' },
        'no-recipient': { key: 'St_no_recipient', tone: 'secondary', icon: 'bx-user-x' },
        'route-pending': { key: 'St_route_pending', tone: 'info', icon: 'bx-git-branch' }
    };
    const STATUS_CODES = Object.keys(STATUSES);

    /** The badge of a row: "route pending" names WHICH mailbox is missing (PV / MSL, from the row's notes). */
    const statusOf = (status, notes) => {
        const s = STATUSES[status];
        if (!s) { return { key: 'St_unknown', tone: 'secondary', icon: 'bx-help-circle', code: status || '' }; }
        const n = Array.isArray(notes) ? notes : [];
        if (status === 'route-pending') {
            const key = n.indexOf('msl-route-pending') > -1 && n.indexOf('pv-route-pending') === -1 ? 'St_route_pending_msl' : 'St_route_pending_pv';
            return Object.assign({}, s, { key: key, code: status });
        }
        if (status === 'failed' && n.indexOf('attempts-exhausted') > -1) { return Object.assign({}, s, { key: 'St_failed_final', code: status }); }
        return Object.assign({ code: status }, s);
    };

    /** The recipient spec the CRM writes for each event (VisitReportNotifier) — for a row read without its detail. */
    const specOf = (key, rep) => {
        const lm = (role) => ({ role: role, kind: 'line-manager', userId: rep || null, mailbox: null });
        if (key === 'adverseevent') { return [{ role: 'to', kind: 'mailbox', userId: null, mailbox: 'pv' }, lm('cc')]; }
        if (key === 'offlabelrequest') { return [{ role: 'to', kind: 'mailbox', userId: null, mailbox: 'msl' }, lm('cc')]; }
        return key ? [lm('to')] : [];
    };

    /**
     * Who the e-mail went (or goes) to, as roles — the CRM's Build on the spec: a mailbox whose route is pending is left
     * out (it is listed under `pending`), and when "to" ends up empty the "cc" ones move up. A line manager the Platform
     * found up the chain (via manager-chain) is the "upper manager". Each entry: { key, name } — the name is the REP's
     * (the manager of whom); the page never receives an address and writes none.
     */
    const recipientsOf = (item) => {
        const it = item || {};
        const row = it.row || it;
        const key = eventKey(row.eventCode);
        const notes = Array.isArray(row.notes) ? row.notes : [];
        const spec = Array.isArray(it.recipients) && it.recipients.length ? it.recipients : specOf(key, row.resourceId);
        const resolved = Array.isArray(it.resolved) ? it.resolved : [];
        const rep = row.resourceName || row.resourceId || '';
        // one line manager per e-mail: found up the chain (whichever list the Platform put it in after cc moved up)
        const chain = () => resolved.some((r) => r && r.via === 'manager-chain');
        const to = [];
        const cc = [];
        const pending = [];
        spec.forEach((r) => {
            if (!r) { return; }
            let entry = null;
            if (r.kind === 'line-manager') { entry = { key: chain() ? 'Rcp_upper_manager' : 'Rcp_manager', name: rep }; }
            else if (r.kind === 'user') { entry = { key: 'Rcp_user', name: '' }; }
            else if (r.kind === 'mailbox' && (r.mailbox === 'pv' || r.mailbox === 'msl')) {
                if (notes.indexOf(r.mailbox + '-route-pending') > -1) { pending.push({ key: 'Rcp_' + r.mailbox, name: '' }); return; }
                entry = { key: 'Rcp_' + r.mailbox, name: '' };
            }
            if (entry) { (r.role === 'cc' ? cc : to).push(entry); }
        });
        if (row.status === 'no-recipient') { return { to: [], cc: [], pending: [], none: true }; }   // nobody was resolved: no role is claimed
        if (!to.length && cc.length) { to.push.apply(to, cc.splice(0, cc.length)); }
        return { to: to, cc: cc, pending: pending, none: false };
    };

    /** The list cell: the roles in order, "to" first, without repeats (the name is the drawer's). */
    const roleKeys = (r) => {
        const out = [];
        (r.to || []).concat(r.cc || []).forEach((x) => { if (out.indexOf(x.key) === -1) { out.push(x.key); } });
        return out;
    };

    /** An address typed into a free text is masked: the preview shows no e-mail address at all. */
    const EMAIL = /[^\s@<>"'(),;:]+@[^\s@<>"'(),;:]+\.[^\s@<>"'(),;:]+/g;
    const maskEmails = (text) => String(text == null ? '' : text).replace(EMAIL, '•••@•••');
    const hasEmail = (text) => { EMAIL.lastIndex = 0; const found = EMAIL.test(String(text == null ? '' : text)); EMAIL.lastIndex = 0; return found; };

    /** A link the preview may draw: same-origin relative or http(s); anything else ("javascript:") is no link. */
    const safeUrl = (url) => {
        const u = String(url == null ? '' : url).trim();
        return /^\/(?!\/)/.test(u) || /^https?:\/\//i.test(u) ? u : '';
    };

    const yes = (v) => String(v || '').toLowerCase() === 'true';

    /**
     * The preview of the e-mail (the 06 mockup's shell, the Platform template's content): every value is a template
     * variable (a DATA string the page escapes and isolates), every word a key. An empty value hides its row / section —
     * as the Platform template does. `dates` formats the yyyy-MM-dd / ISO values in the reader's culture.
     *   { kindKey, subject: { key, args }, band: { tone, icon, key, args }, title: { key, args }, intro: { key, args },
     *     rows: [{ key, value }], sections: [{ key, items: [value] }], noteKey, cta: { key, url } }
     */
    const template = (key, vars, dates) => {
        const v = vars || {};
        const d = dates || { day: (x) => x, moment: (x) => x };
        const val = (name) => maskEmails(String(v[name] == null ? '' : v[name]).trim());
        const doctor = val('doctorName');
        const institution = val('institutionName');
        const who = doctor || institution;
        const visitDay = val('visitDate') ? d.day(val('visitDate')) : '';
        const products = val('products');
        const rows = (list) => list.filter((r) => r.value).map((r) => ({ key: r.key, value: r.value }));
        const cta = { key: key === 'overdue' ? 'CtaReviewRecord' : 'CtaOpenReport', url: safeUrl(v.reportUrl) };
        if (key === 'adverseevent') {
            return {
                kindKey: 'Kind_adverseevent', subject: { key: 'Subj_adverseevent', args: [products || '—'] },
                band: { tone: 'danger', icon: 'bx-error', key: 'Band_adverseevent', args: [] },
                title: { key: 'Title_adverseevent', args: [] }, intro: { key: 'Intro_adverseevent', args: [] },
                rows: rows([{ key: 'Row_reporter', value: val('repName') }, { key: 'Row_sourceDoctor', value: [doctor, institution].filter(Boolean).join(' · ') },
                    { key: 'Row_suspectProduct', value: products }, { key: 'Row_visit', value: visitDay }]),
                sections: val('adverseEventNote') ? [{ key: 'Sec_repNote', items: [val('adverseEventNote')] }] : [],
                noteKey: 'Note_adverseevent', cta: cta
            };
        }
        if (key === 'offlabelrequest') {
            return {
                kindKey: 'Kind_offlabelrequest', subject: { key: 'Subj_offlabelrequest', args: [who || '—'] },
                band: { tone: 'info', icon: 'bx-info-circle', key: 'Band_offlabelrequest', args: [] },
                title: { key: 'Title_offlabelrequest', args: [] }, intro: { key: 'Intro_offlabelrequest', args: [] },
                rows: rows([{ key: 'Row_doctor', value: doctor }, { key: 'Row_institution', value: institution }, { key: 'Row_product', value: products },
                    { key: 'Row_rep', value: val('repName') }, { key: 'Row_visit', value: visitDay }]),
                sections: [], noteKey: null, cta: cta
            };
        }
        if (key === 'overdue') {
            return {
                kindKey: 'Kind_overdue', subject: { key: 'Subj_overdue', args: [who || '—', visitDay || '—'] },
                band: { tone: 'danger', icon: 'bx-lock-alt', key: 'Band_overdue', args: [] },
                title: { key: 'Title_overdue', args: [] }, intro: { key: 'Intro_overdue', args: [val('repName') || '—', visitDay || '—'] },
                rows: rows([{ key: 'Row_rep', value: val('repName') }, { key: 'Row_doctor', value: doctor }, { key: 'Row_institution', value: institution },
                    { key: 'Row_visit', value: visitDay }, { key: 'Row_deadline', value: val('deadline') ? d.moment(val('deadline')) : '' }]),
                sections: [], noteKey: null, cta: cta
            };
        }
        // submitted (and the fallback of an unknown event: the summary's shape)
        const safety = [];
        if ('adverseEvent' in v) { safety.push({ key: yes(v.adverseEvent) ? 'Safety_adverse_yes' : 'Safety_adverse_no' }); }
        if ('offLabelRequest' in v) { safety.push({ key: yes(v.offLabelRequest) ? 'Safety_offlabel_yes' : 'Safety_offlabel_no' }); }
        const sections = [];
        if (val('note')) { sections.push({ key: 'Sec_note', items: [val('note')] }); }
        if (safety.length) { sections.push({ key: 'Sec_safety', itemKeys: safety.map((s) => s.key), items: [] }); }
        if (val('followUpDate')) { sections.push({ key: 'Sec_followUp', items: [d.day(val('followUpDate'))] }); }
        return {
            kindKey: 'Kind_submitted', subject: { key: 'Subj_submitted', args: [who || '—', visitDay || '—'] },
            band: { tone: 'success', icon: 'bx-check-circle', key: 'Band_submitted', args: [] },
            title: { key: 'Title_submitted', args: [who || '—'] }, intro: { key: 'Intro_submitted', args: [val('repName') || '—'] },
            rows: rows([{ key: 'Row_rep', value: val('repName') }, { key: 'Row_doctor', value: doctor }, { key: 'Row_institution', value: institution },
                { key: 'Row_visit', value: visitDay }, { key: 'Row_products', value: products }]),
            sections: sections, noteKey: null, cta: cta
        };
    };

    // ── the period filter (the proxy's twin: VisitNotificationsController.ResolveRange) ───────────────────────
    const PERIODS = ['', 'last7', 'last30', 'month', 'custom'];
    const ISO = /^\d{4}-\d{2}-\d{2}$/;
    const addDays = (iso, n) => { const t = new Date(iso + 'T00:00:00Z'); t.setUTCDate(t.getUTCDate() + n); return t.toISOString().slice(0, 10); };

    /** The window of a period in UTC days (both ends included): "" / last30 = the 30 days up to today; last7; month = the
     *  1st of this month to today; custom = from … to (yyyy-MM-dd, from ≤ to). { from, to } or { error }. */
    const rangeOf = (period, today, from, to) => {
        switch (String(period || '').toLowerCase()) {
            case '': case 'last30': return { from: addDays(today, -29), to: today };
            case 'last7': return { from: addDays(today, -6), to: today };
            case 'month': return { from: today.slice(0, 8) + '01', to: today };
            case 'custom':
                if (!ISO.test(String(from || '')) || !ISO.test(String(to || ''))) { return { error: 'ErrRangeMissing' }; }
                if (to < from) { return { error: 'ErrRangeOrder' }; }
                return { from: from, to: to };
            default: return { error: 'ErrRangeMissing' };
        }
    };
    const customRangeError = (period, from, to) => (period === 'custom' ? (rangeOf('custom', '', from, to).error || null) : null);

    // ── the settings card (the CRM's VisitNotificationRules.IsValidEmail) ─────────────────────────────────────
    const MAX_EMAIL = 254;
    const validEmail = (value) => {
        const v = String(value == null ? '' : value).trim();
        if (!v) { return true; }
        if (v.length > MAX_EMAIL || /[\s,;<>]/.test(v)) { return false; }
        const at = v.lastIndexOf('@');
        if (at <= 0 || v.indexOf('@') !== at) { return false; }
        const dot = v.indexOf('.', at);
        return dot > at + 1 && !v.endsWith('.') && dot < v.length - 1;
    };

    /** The PUT body: both addresses (an empty one CLEARS it), both switches, the version read (409 when it moved). */
    const settingsBody = (form, version) => ({
        pvEmail: String((form && form.pvEmail) || '').trim(),
        mslEmail: String((form && form.mslEmail) || '').trim(),
        reportSummary: !!(form && form.reportSummary),
        overdueReminders: !!(form && form.overdueReminders),
        expectedVersion: Number(version) || 0
    });

    const ERROR_CODES = ['visit_notification_email_invalid', 'visit_notification_invalid', 'visit_notification_not_found', 'permission_denied'];

    /** A refusal → its text key: 409 = the settings changed meanwhile; a known code → Err_{code}; else the generic text. */
    const errorKey = (response) => {
        if (response && response.status === 409) { return 'Err_conflict'; }
        const body = (response && response.body) || {};
        const codes = [].concat(body.errors || [], (body.errorCodes || []).map((c) => c && c.code));
        const known = codes.find((c) => ERROR_CODES.indexOf(c) > -1);
        if (known) { return 'Err_' + known; }
        if (response && response.status === 403) { return 'Err_permission_denied'; }
        if (response && response.status === 404) { return 'Err_visit_notification_not_found'; }
        if (response && (response.status === 0 || response.status === 502)) { return 'Err_unavailable'; }
        return 'Err_generic';
    };

    return {
        EVENTS, EVENT_KEYS, STATUSES, STATUS_CODES, PERIODS, MAX_EMAIL, ERROR_CODES, eventKey, eventOf, statusOf, specOf, recipientsOf, roleKeys,
        maskEmails, hasEmail, safeUrl, template, rangeOf, customRangeError, validEmail, settingsBody, errorKey
    };
}));
