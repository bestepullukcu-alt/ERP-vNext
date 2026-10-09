/**
 * WP-VW-W2 (WEB-b) — the Targets rules SHARED by the Visit Planning page (details.js, targets.js, weeks.js) and the
 * Visit Workspace's Plan mode (visit-workspace.js): ONE implementation, no copy. Pure — no DOM, no fetch, no L10n; the
 * Web tests run these functions in Node.
 *
 *   doctorRow(d)                          a 3D doctor read row → the table's row (name, specialty, link, status, blocked)
 *   isDue / isNever                       the quick-filter tests ("due this week" of the read's week, never visited)
 *   splitPlanFirst(all, visible, inPlan, matches)  4M: the PLAN doctors head the list whatever the quick filter says;
 *                                         the others follow under it
 *   quickCounts(all, server, inPlan)      4M: the server's quickCounts (else counted) WITHOUT the plan doctors
 *   roleOf / countRoles / rolesByOrder    the product roles (promo / non-promo); "apply products": the first promo, the
 *                                         others reminders
 *   unionPicks(existing, chosen)          bulk apply ADDS (S-2): kept products keep their role, new ones are appended
 *   pickInput / contactInput              the session update's shapes
 *   selectionUpdate(session, saved, changes)  the EXISTING session update (PUT sessions/{id}): only the touched doctors
 *                                         carry products (null = keep); a doctor can join or leave the plan
 *   pinInput(p)                           a stored day pin → the update's pin (its start time KEPT — W2-BE-b)
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.VisitPlanningTargetsCore = api; }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    // ── doctors ──────────────────────────────────────────────────────────────────────────────────────────────
    const doctorRow = d => {
        const status = (d && d.status) || {};
        return {
            contactId: d.contactId, name: d.displayName || '—', specialty: d.specialty || '', linkId: d.accountContactLinkId || null,
            status, blocked: String(status.consentStatus || '').toLowerCase() === 'blocked', inactive: !!status.inactive
        };
    };
    const isDue = r => !!(r && r.status && r.status.dueThisWeek);
    const isNever = r => !!(r && r.status && r.status.neverVisited);

    /** 4M — the plan doctors (from the account's "all" read) first whatever the quick filter hides; the others under it. */
    const splitPlanFirst = (all, visible, inPlan, matches) => {
        const ok = typeof matches === 'function' ? matches : () => true;
        return {
            plan: (all || []).filter(r => inPlan(r.contactId) && ok(r)),
            others: (visible || []).filter(r => !inPlan(r.contactId))
        };
    };

    /** 4M — the quick-filter counts of the doctors OUTSIDE the plan (the plan ones are always shown). */
    const quickCounts = (all, server, inPlan) => {
        const list = all || [];
        const plan = list.filter(r => inPlan(r.contactId));
        const base = server && server.all != null
            ? { all: Number(server.all) || 0, due: Number(server.due) || 0, never: Number(server.never) || 0 }
            : { all: list.length, due: list.filter(isDue).length, never: list.filter(isNever).length };
        return {
            all: Math.max(0, base.all - plan.length),
            due: Math.max(0, base.due - plan.filter(isDue).length),
            never: Math.max(0, base.never - plan.filter(isNever).length)
        };
    };

    // ── products ─────────────────────────────────────────────────────────────────────────────────────────────
    const ROLE_PROMO = 'promo', ROLE_NON_PROMO = 'non-promo';
    const roleOf = item => (item && item.role === ROLE_NON_PROMO ? ROLE_NON_PROMO : ROLE_PROMO); // null role reads as promo (K-7d)
    const countRoles = items => (items || []).reduce((c, it) => { if (roleOf(it) === ROLE_PROMO) c.promo++; else c.nonPromo++; return c; }, { promo: 0, nonPromo: 0 });
    /** "Apply products" (mockup): the FIRST product is the promo, every other a reminder (non-promo). */
    const rolesByOrder = products => (products || []).map((p, i) => ({
        productId: p.productId, productCode: p.productCode || null, productName: p.productName || null, role: i === 0 ? ROLE_PROMO : ROLE_NON_PROMO
    }));
    // S-2 — bulk apply ADDS: every product the doctor already has stays (with its own role); a picked product the doctor
    // does not have yet is appended. Never a replacement.
    const unionPicks = (existing, chosen) => {
        const merged = (existing || []).map(p => ({ productId: p.productId, productCode: p.productCode, productName: p.productName, role: roleOf(p) }));
        (chosen || []).forEach(p => { if (!merged.some(m => m.productId === p.productId)) merged.push({ productId: p.productId, productCode: p.productCode, productName: p.productName, role: roleOf(p) }); });
        return merged;
    };
    const pickInput = p => ({ productId: p.productId, productCode: p.productCode || null, role: roleOf(p) });
    const contactInput = c => ({ contactId: c.contactId, accountId: c.accountId || null, accountContactLinkId: c.accountContactLinkId || null });

    // ── the existing session update ──────────────────────────────────────────────────────────────────────────
    /**
     * session = { selectedAccountIds, version }; saved = the plan's doctors [{ contactId, accountId, accountContactLinkId }];
     * changes = [{ doctor: { contactId, accountId, accountContactLinkId }, products?: [...], remove?: true }].
     * Only a touched doctor carries `products` (null = keep for the rest); a new doctor joins with its institution; a
     * removed one leaves (its institution stays — removing an institution is its own action).
     */
    const selectionUpdate = (session, saved, changes) => {
        const s = session || {};
        let contacts = (saved || []).map(contactInput);
        const accounts = (s.selectedAccountIds || []).slice();
        let accountsChanged = false;
        (changes || []).forEach(ch => {
            const same = c => c.contactId === ch.doctor.contactId && (c.accountId || null) === (ch.doctor.accountId || null);
            if (ch.remove) { contacts = contacts.filter(c => !same(c)); return; }
            let entry = contacts.find(same) || contacts.find(c => c.contactId === ch.doctor.contactId);
            if (!entry) { entry = contactInput(ch.doctor); contacts.push(entry); }
            if (ch.doctor.accountId && accounts.indexOf(ch.doctor.accountId) === -1) { accounts.push(ch.doctor.accountId); accountsChanged = true; }
            if (ch.products) { entry.products = ch.products.map(pickInput); }
        });
        return { selectedAccountIds: accountsChanged ? accounts : null, selectedPharmacyIds: null, selectedContacts: contacts, expectedVersion: s.version };
    };

    /** A stored day pin → the update's pin; W2-BE-b — its start time is KEPT (a re-save must never drop a time pin). */
    const pinInput = p => ({
        targetType: p.targetType, targetId: p.targetId, contactId: p.contactId || null, date: p.date, scope: p.scope || 'visit',
        startTime: p.startTime || null
    });

    return {
        doctorRow, isDue, isNever, splitPlanFirst, quickCounts,
        ROLE_PROMO, ROLE_NON_PROMO, roleOf, countRoles, rolesByOrder, unionPicks, pickInput, contactInput,
        selectionUpdate, pinInput
    };
}));
