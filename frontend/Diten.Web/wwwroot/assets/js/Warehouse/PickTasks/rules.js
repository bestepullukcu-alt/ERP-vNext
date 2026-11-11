/**
 * MOD-0178 DL-6 — the PURE screen rules of Toplama Görevleri (pack §8.19, §11.3; the PutawayTasks/rules.js pattern). No DOM: Node measures this
 * file (frontend/Diten.Web.Tests/JavaScript/Node/warehouse-s2-rules.test.mjs).
 *
 *  - The service decides the state rules (allowedActions / disabledActions + reasonCode — PutawayTaskReads.Actions, Pick branch); the screen never
 *    re-derives them. A control whose KEY the reader lacks is absent; a control the STATE forbids is drawn disabled with its reason.
 *  - A pick task has no edit and no cancel of its own (the outbound's cancel closes it) — neither is on the menu.
 *  - "Paketle" is a LINK (navigation, not an action — §11.3): shown when the task's outbound is Picked and the reader holds warehouse.packages.read
 *    (the Packing page key; NOT the outbound read — Q-10a, AC-34).
 */
'use strict';

const PickRules = (function () {
    /** service action → the key that holds it (pack §11.3, §14.2). */
    const ActionKeys = {
        Confirm: 'warehouse.pick-tasks.confirm',
        Retry: 'warehouse.pick-tasks.confirm',
        MarkReturned: 'warehouse.pick-tasks.confirm',
        CloseShort: 'warehouse.pick-tasks.confirm',
        Claim: 'warehouse.pick-tasks.confirm',
        ResolveIntegrity: 'warehouse.pick-tasks.resolve'
    };

    /** The Packing page key — the "Paketle" link needs it (§11.1, §11.3). */
    const PackKey = 'warehouse.packages.read';

    /** The row menu (§11.3 RowActions) and the QuickView buttons, in order. */
    const RowMenu = ['Confirm', 'Claim', 'CloseShort'];
    const QuickViewMenu = ['Confirm', 'Retry', 'MarkReturned', 'ResolveIntegrity', 'Claim', 'CloseShort'];

    const ActionWord = {
        Confirm: 'Warehouse.Action.Confirm', Retry: 'Warehouse.Action.Retry', MarkReturned: 'Warehouse.Action.Returned', CloseShort: 'Warehouse.Action.CloseShort',
        Claim: 'Warehouse.Action.Claim', ResolveIntegrity: 'Warehouse.Action.ResolveIntegrity'
    };

    const Dangerous = new Set(['ResolveIntegrity']);

    const StatusClass = { Open: 'bg-label-info', InProgress: 'bg-label-primary', Completed: 'bg-label-success', Cancelled: 'bg-label-secondary' };

    const FlagClass = {
        PostingPending: 'bg-label-warning',
        PostingPendingLong: 'bg-warning',
        PostingRefused: 'bg-label-danger',
        PostingIntegrityError: 'bg-danger'
    };

    /** One action's state for this reader: absent without the key; else enabled iff the service allows it, with the service's reason. */
    const actionState = (task, action, can) => {
        const key = ActionKeys[action];
        if (!key || !can(key)) return { action, visible: false, enabled: false, reasonCode: null };
        const allowed = Array.isArray(task?.allowedActions) && task.allowedActions.includes(action);
        const disabled = (task?.disabledActions || []).find((d) => d && d.action === action);
        return { action, visible: true, enabled: allowed, reasonCode: allowed ? null : (disabled?.reasonCode || null), dangerous: Dangerous.has(action) };
    };

    const menu = (task, can, order) => order.map((action) => actionState(task, action, can)).filter((s) => s.visible);

    const statusBadge = (status) => ({ word: `Warehouse.Status.${status}`, cls: StatusClass[status] || 'bg-label-secondary' });

    const flagBadge = (flag) => (flag && flag !== 'None' && FlagClass[flag] ? { word: `Warehouse.Flag.${flag}`, cls: FlagClass[flag] } : null);

    const reasonWord = (reasonCode) => (reasonCode ? `Warehouse.Disabled.${reasonCode}` : null);

    /** The "Paketle" link of a task, or null: its outbound is Picked and the reader may open the Packing page (§11.1 r21). */
    const packLink = (task, can) => {
        const outbound = task?.outbound;
        if (!outbound?.id || outbound.status !== 'Picked' || !can(PackKey)) return null;
        return `/Warehouse/Packing/${encodeURIComponent(outbound.id)}`;
    };

    /** The origin word (§11.4 r24: Warehouse.Origin.* — seven values; a pick task is Outbound). */
    const originWord = (origin) => (origin ? `Warehouse.Origin.${origin}` : null);

    /** −/+ on a quantity: steps of 1, never below 0, never above max when given; at most 6 decimals, no exponent (§12). */
    const step = (value, delta, max) => {
        const current = Number.parseFloat(String(value ?? '').replace(',', '.'));
        let next = (Number.isFinite(current) ? current : 0) + delta;
        if (next < 0) next = 0;
        const limit = Number.parseFloat(String(max ?? '').replace(',', '.'));
        if (Number.isFinite(limit) && next > limit) next = limit;
        return String(Math.round(next * 1e6) / 1e6);
    };

    const isQuantity = (text) => /^\d{1,12}([.,]\d{1,6})?$/.test(String(text ?? '').trim()) && Number.parseFloat(String(text).replace(',', '.')) > 0;

    /** The confirmation body the screen sends: the quantity ONLY — never a target nor a source (§8.19, AC-27 400). */
    const confirmBody = (key, etag, quantity) => ({ idempotencyKey: key, ifMatch: etag, quantity: String(quantity).trim() });

    return { ActionKeys, PackKey, ActionWord, RowMenu, QuickViewMenu, actionState, menu, statusBadge, flagBadge, reasonWord, packLink, originWord, step, isQuantity, confirmBody };
})();

if (typeof window !== 'undefined') window.PickRules = PickRules;
if (typeof module !== 'undefined' && module.exports) module.exports = PickRules;
