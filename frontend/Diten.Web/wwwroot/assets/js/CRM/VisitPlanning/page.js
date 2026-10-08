/**
 * WP-VP-4B — the Visit Planning DETAIL page skeleton: ONE shared state + event bus that every part of the page reads.
 *
 *   window.VisitPlanningPage
 *     state   : { sessionId, session, preview, weekStart, flags: { canGenerate, canApply, readOnly } }
 *     on(evt, fn) / emit(evt, payload)
 *     setSession(s) → 'session'   setPreview(p) → 'preview'   selectWeek(weekStart, source) → 'week-change'
 *     request(name)  → 'request:<name>' (an action button asks the part that owns it: approve-week, save-targets,
 *                      generate-route)
 *     weeks()        → the period's weeks [{ weekStart, isoWeek, from, to, status, visitCount, … }] — the preview's
 *                      (exact) when there is one, else the plan detail's (3A)
 *     week(ws)       → one of them; weekStatus(ws) → past | approved | draft | empty
 *     actionsFor(status, isLegacy) → which header actions a week offers (pure; the ONE rule, see ACTIONS)
 *
 * Who plugs in where:
 *   - details.js (Targets + Route, unchanged views) publishes the session / preview and follows 'week-change';
 *   - header.js  (summary, week picker, status actions, capacity cards, states) reads the state;
 *   - 4C targets.js and 4D weeks.js + doctor-panel.js subscribe to the same events (no new globals needed).
 * Loaded BEFORE details.js / header.js. Pure state — no DOM work here.
 */
(function (window) {
    'use strict';
    if (window.VisitPlanningPage) return;

    // Header actions per derived week status. A legacy (whole-period committed) plan offers none: it is read-only.
    //   draft    — the week has visits and is not approved: save targets, build the route, approve the week;
    //   empty    — nothing falls in it yet: save targets, build the route (nothing to approve);
    //   approved — frozen: reopen (with a reason) and move on to the next open week;
    //   past     — over: read-only, no action.
    const ACTIONS = Object.freeze({
        draft: Object.freeze(['saveTargets', 'generateRoute', 'approveWeek']),
        empty: Object.freeze(['saveTargets', 'generateRoute']),
        approved: Object.freeze(['reopenWeek', 'nextWeek']),
        past: Object.freeze([]),
        legacy: Object.freeze([])
    });
    const actionsFor = (status, isLegacy) => (isLegacy ? ACTIONS.legacy : (ACTIONS[status] || ACTIONS.past));

    const handlers = {};
    const on = (evt, fn) => { (handlers[evt] = handlers[evt] || []).push(fn); return () => { handlers[evt] = (handlers[evt] || []).filter(h => h !== fn); }; };
    const emit = (evt, payload) => (handlers[evt] || []).slice().forEach(fn => { try { fn(payload); } catch (e) { console.error('[VisitPlanningPage]', evt, e); } });

    const state = { sessionId: null, session: null, preview: null, weekStart: null, flags: { canGenerate: false, canApply: false, readOnly: false } };

    const weeks = () => {
        const fromPreview = state.preview && Array.isArray(state.preview.weeks) ? state.preview.weeks : null;
        if (fromPreview && fromPreview.length) return fromPreview;
        return (state.session && Array.isArray(state.session.weeks)) ? state.session.weeks : [];
    };
    const week = ws => weeks().find(w => w.weekStart === ws) || null;
    const weekStatus = ws => { const w = week(ws); return w ? w.status : null; };
    const isLegacy = () => !!(state.session && String(state.session.status || '').toLowerCase() === 'committed');

    window.VisitPlanningPage = Object.freeze({
        state,
        on,
        emit,
        ACTIONS,
        actionsFor,
        weeks,
        week,
        weekStatus,
        isLegacy,
        setFlags: f => { Object.assign(state.flags, f || {}); },
        setSession: s => { state.session = s; if (s && s.planningSessionId) state.sessionId = s.planningSessionId; emit('session', s); },
        setPreview: p => { state.preview = p; emit('preview', p); },
        selectWeek: (ws, source) => {
            if (!ws) return;
            const changed = ws !== state.weekStart;
            state.weekStart = ws;
            if (changed || source === 'force') emit('week-change', { weekStart: ws, source: source || 'unknown' });
        },
        request: name => emit('request:' + name)
    });
})(window);
