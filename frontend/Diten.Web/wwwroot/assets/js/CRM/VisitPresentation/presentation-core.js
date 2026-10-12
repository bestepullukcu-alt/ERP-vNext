/**
 * WP-VW-W3 (W3-WEB-b) — the Visit Presentation's PURE rules (no DOM, no fetch, no clock): the slide navigation and
 * "skip product", the per-product / per-slide timers (paused while the page is hidden), the target-time format (the
 * page culture's decimal separator — "1,5 dk" in Turkish), the capture body of PUT capture / POST finish (§2.2 shape),
 * the signature card rule, the rep's PIN state machine (set → locked → unlock → panel; an exit NEVER finishes the
 * visit) and the finish-summary rows. Browser: window.VisitPresentationCore. Node: module.exports (the Web tests).
 *
 *   deviceOf(width)                       phone < 768 ≤ tablet < 1200 ≤ desktop
 *   deck(presentation)                    the slides with their product position (first slide of a product, n / N)
 *   productOrder(slides)                  the products in slide order
 *   step(i, delta, count)                 Back / Next, clamped
 *   skipProduct(slides, i)                the first slide of the NEXT product, or -1 on the last one
 *   swipeStep(dx, rtl)                    a 50 px swipe → ±1 (mirrored in Arabic)
 *   timers(execution) / tick(t, slide, visible, seconds)   the timers: seeded from the execution, counting only while visible
 *   clock(seconds)                        mm:ss
 *   target(seconds, culture, L)           "45 sn" / "1,5 dk"
 *   slideBar(elapsed, target)             { pct, over }
 *   marks(execution) / toggleFlag / setInterest / setObjection   the per-product marks
 *   captureBody(state)                    the §2.2 body (kind, productTimings, slideTimings, marks, note, personalNotes, coVisitorUserIds)
 *   signatureCard(device, policy, marks, execution)        show the doctor-signature card?
 *   signatureContext(marks)               sample when any product asked for a sample, else approval
 *   pin(state, event)                     the PIN machine (effects: locked | panel; never finish)
 *   summaryRows(slides, state, labels)    the E4c product rows (time, share, interest + flag tags)
 *   briefLines(brief, L, format)          "For this doctor" (decision 7)
 *   errorText(errors, L)                  an API refusal in the user's language
 */
(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.VisitPresentationCore = api; }
})(typeof window !== 'undefined' ? window : null, function () {
    'use strict';

    const MAX_SECONDS = 86400;
    const SWIPE_PX = 50;
    const LONG_PRESS_MS = 650;
    const CAPTURE_EVERY_SECONDS = 30;
    const CAPTURE_DEBOUNCE_MS = 1500;
    const MAX_CO_VISITORS = 3;
    const INTERESTS = ['interested', 'neutral', 'not_interested'];
    const FLAGS = ['question', 'objection', 'sample_request', 'literature_request', 'follow_up'];
    const KINDS = ['in_person', 'remote'];

    const deviceOf = width => (width < 768 ? 'phone' : width < 1200 ? 'tablet' : 'desktop');

    // ── the deck ─────────────────────────────────────────────────────────────────────────────────────────────
    /** The slides in the read's order, each with its product position. The server already chose them (promo: every
     *  frozen step; reminder / pharmacy: the first step) — the page never re-selects. */
    const deck = presentation => {
        const slides = ((presentation && presentation.slides) || []).slice().sort((a, b) => (a.index || 0) - (b.index || 0));
        const seen = {};
        return slides.map((s, i) => {
            const first = !seen[s.productId];
            seen[s.productId] = true;
            return Object.assign({}, s, { position: i, firstOfProduct: first });
        });
    };

    const productOrder = slides => {
        const out = [];
        (slides || []).forEach(s => { if (out.indexOf(s.productId) < 0) { out.push(s.productId); } });
        return out;
    };

    const step = (i, delta, count) => (count <= 0 ? 0 : Math.max(0, Math.min(count - 1, i + delta)));

    /** "Skip product": the first slide of the next product; -1 when the current product is the last one. */
    const skipProduct = (slides, i) => {
        const current = slides[i] && slides[i].productId;
        for (let j = i + 1; j < slides.length; j++) {
            if (slides[j].productId !== current) { return j; }
        }
        return -1;
    };

    /** A horizontal swipe of at least 50 px is one slide; right-to-left reading mirrors it. */
    const swipeStep = (dx, rtl) => {
        if (Math.abs(dx) < SWIPE_PX) { return 0; }
        const forward = dx < 0 ? 1 : -1;
        return rtl ? -forward : forward;
    };

    // ── timers ───────────────────────────────────────────────────────────────────────────────────────────────
    const slideKey = (productId, stepId) => productId + '|' + stepId;

    /** The timers, resumed from the execution's last capture (a reopened page continues). */
    const timers = execution => {
        const t = { products: {}, slides: {} };
        ((execution && execution.productTimings) || []).forEach(p => { t.products[p.productId] = p.seconds || 0; });
        ((execution && execution.slideTimings) || []).forEach(s => { t.slides[slideKey(s.productId, s.stepId)] = s.seconds || 0; });
        return t;
    };

    /** One tick: the current slide and its product gain <paramref name="seconds"/> — only while the page is visible. */
    const tick = (t, slide, visible, seconds) => {
        if (!visible || !slide) { return t; }
        const add = seconds == null ? 1 : seconds;
        t.products[slide.productId] = Math.min(MAX_SECONDS, (t.products[slide.productId] || 0) + add);
        const key = slideKey(slide.productId, slide.stepId);
        t.slides[key] = Math.min(MAX_SECONDS, (t.slides[key] || 0) + add);
        return t;
    };

    const slideSeconds = (t, slide) => (slide ? t.slides[slideKey(slide.productId, slide.stepId)] || 0 : 0);
    const productSeconds = (t, productId) => t.products[productId] || 0;
    const totalSeconds = t => Object.keys(t.products).reduce((a, k) => a + (t.products[k] || 0), 0);

    const pad = n => (n < 10 ? '0' : '') + n;
    const clock = seconds => {
        const s = Math.max(0, Math.floor(seconds || 0));
        return pad(Math.floor(s / 60)) + ':' + pad(s % 60);
    };

    /** The slide's target in the page culture: under a minute in seconds, else minutes with ONE decimal in the culture's
     *  separator ("1,5 dk" in Turkish). Null target → null (the bar is not drawn). */
    const target = (seconds, culture, L) => {
        if (seconds == null || !(seconds > 0)) { return null; }
        const labels = L || {};
        if (seconds < 60) { return fmt(labels.TargetSeconds || '{0} s', String(seconds)); }
        const minutes = Math.round(seconds / 6) / 10;
        let text;
        try {
            text = new Intl.NumberFormat(culture || 'en', { maximumFractionDigits: 1 }).format(minutes);
        } catch (e) {
            text = String(minutes);
        }
        return fmt(labels.TargetMinutes || '{0} min', text);
    };

    const slideBar = (elapsed, targetSeconds) => {
        if (!(targetSeconds > 0)) { return { pct: 0, over: false }; }
        return { pct: Math.min(100, Math.round((elapsed / targetSeconds) * 100)), over: elapsed > targetSeconds };
    };

    // ── marks ────────────────────────────────────────────────────────────────────────────────────────────────
    const emptyMark = () => ({ interest: null, flags: [], objectionCategory: null });

    const marks = execution => {
        const m = {};
        ((execution && execution.marks) || []).forEach(x => {
            m[x.productId] = { interest: x.interest || null, flags: (x.flags || []).slice(), objectionCategory: x.objectionCategory || null };
        });
        return m;
    };

    const markOf = (all, productId) => all[productId] || (all[productId] = emptyMark());

    /** The interest of a product: choosing the current one again clears it. */
    const setInterest = (all, productId, interest) => {
        const m = markOf(all, productId);
        m.interest = m.interest === interest || INTERESTS.indexOf(interest) < 0 ? null : interest;
        return all;
    };

    /** A flag on / off. Turning "objection" OFF also clears its category; turning it ON is done by setObjection. */
    const toggleFlag = (all, productId, flag) => {
        if (FLAGS.indexOf(flag) < 0) { return all; }
        const m = markOf(all, productId);
        const at = m.flags.indexOf(flag);
        if (at >= 0) {
            m.flags.splice(at, 1);
            if (flag === 'objection') { m.objectionCategory = null; }
        } else {
            m.flags.push(flag);
        }
        return all;
    };

    /** "Objected" → a category from objectionCategories: the flag and its category together. */
    const setObjection = (all, productId, category) => {
        const m = markOf(all, productId);
        if (m.flags.indexOf('objection') < 0) { m.flags.push('objection'); }
        m.objectionCategory = category || null;
        return all;
    };

    const markCount = m => (m ? (m.interest ? 1 : 0) + m.flags.length : 0);

    // ── the capture body (§2.2, fixed) ───────────────────────────────────────────────────────────────────────
    /**
     * state = { kind, slides, timers, marks, note, personalNotes: { productId: text }, coVisitorUserIds: [] }.
     * Products and slides are the deck's (the plan's); seconds are capped at 86 400; empty notes are not sent.
     */
    const captureBody = state => {
        const s = state || {};
        const slides = s.slides || [];
        const t = s.timers || { products: {}, slides: {} };
        const products = productOrder(slides);
        const all = s.marks || {};
        return {
            kind: KINDS.indexOf(s.kind) >= 0 ? s.kind : 'in_person',
            productTimings: products.map(p => ({ productId: p, seconds: Math.min(MAX_SECONDS, t.products[p] || 0) })),
            slideTimings: slides.map(x => ({ productId: x.productId, stepId: x.stepId, seconds: Math.min(MAX_SECONDS, t.slides[slideKey(x.productId, x.stepId)] || 0) })),
            marks: products
                .filter(p => markCount(all[p]) > 0)
                .map(p => ({
                    productId: p,
                    interest: all[p].interest || null,
                    flags: all[p].flags.slice(),
                    objectionCategory: all[p].flags.indexOf('objection') >= 0 ? (all[p].objectionCategory || null) : null
                })),
            note: s.note && String(s.note).trim() ? String(s.note).trim() : null,
            personalNotes: products
                .filter(p => s.personalNotes && s.personalNotes[p] && String(s.personalNotes[p]).trim())
                .map(p => ({ productId: p, text: String(s.personalNotes[p]).trim() })),
            coVisitorUserIds: (s.coVisitorUserIds || []).slice(0, MAX_CO_VISITORS)
        };
    };

    // ── the signature ────────────────────────────────────────────────────────────────────────────────────────
    const anySample = all => Object.keys(all || {}).some(p => (all[p].flags || []).indexOf('sample_request') >= 0);

    /** The doctor-signature card: never on a phone; when the tenant asks for the doctor's confirmation, or a sample
     *  was requested; not again once the execution carries a signature. */
    const signatureCard = (device, policy, all, execution) =>
        device !== 'phone'
        && !(execution && execution.signature && execution.signature.present)
        && (!!(policy && policy.doctorConfirmation) || anySample(all));

    const signatureContext = all => (anySample(all) ? 'sample' : 'approval');

    // ── the PIN (decision 6) ─────────────────────────────────────────────────────────────────────────────────
    /**
     * A 4-digit PIN the rep sets when the tablet goes to the doctor; only its DIGEST is kept, in memory, never sent.
     * state = { phase: none | set | confirm | locked | unlock | open, digest, buffer, first, error }.
     * events: begin (set a new PIN) · digit · back · request (the long press: ask to unlock) · cancel · relock
     * ("back to the doctor"). The effect of a step is 'locked' (the doctor screen locks) or 'panel' (the rep panel
     * opens). There is NO finish effect: leaving the doctor screen never ends the visit.
     */
    const digest = code => {
        let h = 2166136261;
        const text = 'vp-pin:' + code;
        for (let i = 0; i < text.length; i++) {
            h ^= text.charCodeAt(i);
            h = Math.imul(h, 16777619) >>> 0;
        }
        return h.toString(16);
    };

    const pinInitial = () => ({ phase: 'none', digest: null, buffer: '', first: null, error: false, effect: null });

    const pin = (state, event) => {
        const s = Object.assign({}, state || pinInitial(), { effect: null, error: false });
        const e = event || {};
        switch (e.type) {
            case 'begin':
                return Object.assign(s, { phase: 'set', buffer: '', first: null });
            case 'request':
                return s.phase === 'locked' ? Object.assign(s, { phase: 'unlock', buffer: '' }) : s;
            case 'cancel':
                if (s.phase === 'unlock') { return Object.assign(s, { phase: 'locked', buffer: '' }); }
                if (s.phase === 'set' || s.phase === 'confirm') { return Object.assign(s, { phase: s.digest ? 'open' : 'none', buffer: '', first: null }); }
                return s;
            case 'relock':
                return s.digest && s.phase === 'open' ? Object.assign(s, { phase: 'locked', buffer: '', effect: 'locked' }) : s;
            case 'back':
                return Object.assign(s, { buffer: s.buffer.slice(0, -1) });
            case 'digit': {
                if (['set', 'confirm', 'unlock'].indexOf(s.phase) < 0 || !/^[0-9]$/.test(String(e.digit))) { return s; }
                const buffer = s.buffer + e.digit;
                if (buffer.length < 4) { return Object.assign(s, { buffer: buffer }); }
                if (s.phase === 'set') { return Object.assign(s, { phase: 'confirm', first: digest(buffer), buffer: '' }); }
                if (s.phase === 'confirm') {
                    return digest(buffer) === s.first
                        ? Object.assign(s, { phase: 'locked', digest: s.first, first: null, buffer: '', effect: 'locked' })
                        : Object.assign(s, { phase: 'set', first: null, buffer: '', error: true });
                }
                return digest(buffer) === s.digest
                    ? Object.assign(s, { phase: 'open', buffer: '', effect: 'panel' })
                    : Object.assign(s, { buffer: '', error: true });
            }
            default:
                return s;
        }
    };

    // ── the finish summary (E4c) ─────────────────────────────────────────────────────────────────────────────
    const summaryRows = (slides, state, labels) => {
        const L = labels || {};
        const t = (state && state.timers) || { products: {}, slides: {} };
        const all = (state && state.marks) || {};
        const total = totalSeconds(t);
        const categories = (state && state.categories) || {};
        return productOrder(slides).map(p => {
            const first = slides.find(x => x.productId === p) || {};
            const m = all[p] || emptyMark();
            const tags = [];
            if (m.interest) { tags.push({ key: 'interest_' + m.interest, text: L['Interest_' + m.interest] || m.interest }); }
            m.flags.forEach(f => {
                const label = L['Flag_' + f] || f;
                tags.push({
                    key: 'flag_' + f,
                    text: f === 'objection' && m.objectionCategory ? fmt(L.ObjectionWith || '{0}: {1}', label, categories[m.objectionCategory] || m.objectionCategory) : label
                });
            });
            const seconds = t.products[p] || 0;
            return {
                productId: p,
                name: first.productName || first.productCode || '',
                role: first.role === 'promo' ? 'promo' : 'reminder',
                seconds: seconds,
                pct: total > 0 ? Math.round((seconds / total) * 100) : 0,
                tags: tags
            };
        });
    };

    // ── "for this doctor" (decision 7) ───────────────────────────────────────────────────────────────────────
    const briefLines = (brief, L, formatDate) => {
        const b = brief || {};
        const labels = L || {};
        if (!b.lastVisitDate) { return []; }
        const lines = [fmt(labels.BriefLastVisit || '{0}', formatDate ? formatDate(b.lastVisitDate) : b.lastVisitDate)];
        if (b.outcomeCode) { lines.push(fmt(labels.BriefOutcome || '{0}', b.outcomeCode)); }
        if (b.followUpNotes) { lines.push(fmt(labels.BriefFollowUp || '{0}', b.followUpNotes)); }
        const samples = (b.samples || []).reduce((a, s) => a + (s.quantity || 0), 0);
        if (samples > 0) { lines.push(fmt(labels.BriefSamples || '{0}', String(samples))); }
        return lines;
    };

    // ── the API refusal in the user's language ───────────────────────────────────────────────────────────────
    const errorText = (errors, L) => {
        const labels = L || {};
        const list = Array.isArray(errors) ? errors : [];
        const code = list.find(e => typeof e === 'string' && /^[a-z0-9_]+$/.test(e) && labels['Err_' + e]);
        return code ? labels['Err_' + code] : (labels.ActionFailed || list[0] || '');
    };

    function fmt(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template).replace(/\{(\d+)\}/g, (m, i) => (args[i] == null ? '' : String(args[i])));
    }

    return {
        MAX_SECONDS, SWIPE_PX, LONG_PRESS_MS, CAPTURE_EVERY_SECONDS, CAPTURE_DEBOUNCE_MS, MAX_CO_VISITORS,
        INTERESTS, FLAGS, KINDS,
        deviceOf, deck, productOrder, step, skipProduct, swipeStep,
        slideKey, timers, tick, slideSeconds, productSeconds, totalSeconds, clock, target, slideBar,
        emptyMark, marks, setInterest, toggleFlag, setObjection, markCount,
        captureBody, signatureCard, signatureContext,
        digest, pinInitial, pin,
        summaryRows, briefLines, errorText, fmt
    };
});
