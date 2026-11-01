/**
 * MOD-0178 DL-3 — the PURE screen rules of Yerleştirme Görevleri (pack §8.15, §11.3). No DOM: Node measures this file
 * (frontend/Diten.Web.Tests/JavaScript/Node/warehouse-putaway-rules.test.mjs).
 *
 *  - The service decides the state rules (allowedActions / disabledActions + reasonCode, AC-39 / AC-43); the screen never re-derives them.
 *    A control whose KEY the reader lacks is absent; a control the STATE forbids is drawn disabled with its reason.
 *  - Status and integration flag are two badges; the service sends the one flag of the task (priority §4.1), the screen draws exactly it
 *    (AC-46).
 *  - The bin field: only an EXACT code answer selects a bin; anything else is a field error and blocks the submit (AC-44).
 *  - A locked candidate is shown with its lock, reason and blocking document number and cannot be picked (AC-41).
 *  - The bin suggestion is offered, never applied by itself (AC-42).
 */
'use strict';

const PutawayRules = (function () {
    /** service action → the key that holds it (pack §11.3, §14.2). */
    const ActionKeys = {
        Update: 'warehouse.putaway-tasks.update',
        Confirm: 'warehouse.putaway-tasks.confirm',
        Retry: 'warehouse.putaway-tasks.confirm',
        MarkReturned: 'warehouse.putaway-tasks.confirm',
        CloseShort: 'warehouse.putaway-tasks.confirm',
        Claim: 'warehouse.putaway-tasks.confirm',
        Cancel: 'warehouse.putaway-tasks.cancel',
        ResolveIntegrity: 'warehouse.putaway-tasks.resolve'
    };

    /** The row menu (§11.3 RowActions) and the QuickView buttons, in order. */
    const RowMenu = ['Confirm', 'Update', 'Claim', 'CloseShort', 'Cancel'];
    const QuickViewMenu = ['Confirm', 'Retry', 'MarkReturned', 'ResolveIntegrity', 'Update', 'Claim', 'CloseShort', 'Cancel'];

    /** The resx word of a service action's button. */
    const ActionWord = {
        Update: 'Warehouse.Action.Edit', Confirm: 'Warehouse.Action.Confirm', Retry: 'Warehouse.Action.Retry', MarkReturned: 'Warehouse.Action.Returned',
        CloseShort: 'Warehouse.Action.CloseShort', Claim: 'Warehouse.Action.Claim', Cancel: 'Warehouse.Action.Cancel',
        ResolveIntegrity: 'Warehouse.Action.ResolveIntegrity'
    };

    const Dangerous = new Set(['Cancel', 'ResolveIntegrity']);

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
        return {
            action,
            visible: true,
            enabled: allowed,
            reasonCode: allowed ? null : (disabled?.reasonCode || null),
            dangerous: Dangerous.has(action)
        };
    };

    const menu = (task, can, order) => order.map((action) => actionState(task, action, can)).filter((s) => s.visible);

    /** The task status badge — the status comes from the confirmed quantity (§4.1), never from the flag. */
    const statusBadge = (status) => ({ word: `Warehouse.Status.${status}`, cls: StatusClass[status] || 'bg-label-secondary' });

    /** The integration flag badge, or null for None (AC-46: one badge, the service's flag). */
    const flagBadge = (flag) => (flag && flag !== 'None' && FlagClass[flag] ? { word: `Warehouse.Flag.${flag}`, cls: FlagClass[flag] } : null);

    /** A disabled control's reason text key (7 languages). */
    const reasonWord = (reasonCode) => (reasonCode ? `Warehouse.Disabled.${reasonCode}` : null);

    /**
     * The bin field's verdict from the proxy's exact-code answer ({ match, available }): a bin only for an exact hit; otherwise the field
     * error key — never a guess, never a "contains" hit (AC-44).
     */
    const binVerdict = (code, answer) => {
        if (!String(code ?? '').trim()) return { ok: false, error: null, id: null, label: null };
        if (!answer || answer.available === false) return { ok: false, error: 'BinLookupUnavailable', id: null, label: null };
        if (!answer.match?.value) return { ok: false, error: 'BinNotFound', id: null, label: null };
        return { ok: true, error: null, id: answer.match.value, label: answer.match.text || code };
    };

    /** A deviation reason is asked when the actual bin is not the planned one (§8.3). */
    const needsDeviation = (plannedToLocationId, actualToLocationId) =>
        !!plannedToLocationId && !!actualToLocationId && String(plannedToLocationId).toLowerCase() !== String(actualToLocationId).toLowerCase();

    /** −/+ on a quantity: steps of 1, never below 0, never above max when given; at most 6 decimals, no exponent (§12). */
    const step = (value, delta, max) => {
        const current = Number.parseFloat(String(value ?? '').replace(',', '.'));
        let next = (Number.isFinite(current) ? current : 0) + delta;
        if (next < 0) next = 0;
        const limit = Number.parseFloat(String(max ?? '').replace(',', '.'));
        if (Number.isFinite(limit) && next > limit) next = limit;
        return String(Math.round(next * 1e6) / 1e6);
    };

    /** A valid quantity text for the wire: > 0, ≤ 6 decimals, no exponent. */
    const isQuantity = (text) => /^\d{1,12}([.,]\d{1,6})?$/.test(String(text ?? '').trim()) && Number.parseFloat(String(text).replace(',', '.')) > 0;

    /**
     * One "Yerleştirilecek stok" option (AC-41): an eligible row is pickable; a locked row stays in the list, disabled, with the lock, the reason
     * and the blocking document number. `w` maps a word key to its text.
     */
    const candidateOption = (c, w) => {
        const base = [c.locationCode, c.sku, c.lotNumber, `${c.openQuantity ?? ''} ${c.uom ?? ''}`.trim()].filter(Boolean).join(' · ');
        if (c.eligible) return { id: c.key, text: base, disabled: false, locked: false };
        const reason = w(`Warehouse.Candidate.${c.reason}`);
        const document = c.blockingKind ? `${w(`Warehouse.Blocking.${c.blockingKind}`)} ${c.blockingNumber ?? ''}`.trim() : '';
        return { id: c.key, text: [base, reason, document].filter(Boolean).join(' — '), disabled: true, locked: true, reason: c.reason, document };
    };

    /** The suggestion line: shown only when the task has a suggestion that is not already the target; applying it is the reader's act (AC-42). */
    const suggestion = (task) => (task?.suggestedToLocationId && task.suggestedToLocationId !== task.toLocationId
        ? { id: task.suggestedToLocationId, label: task.suggestedToLocation || task.suggestedToLocationId }
        : null);

    /** The target source a save sends: SuggestionApplied only while the target is still the applied suggestion. */
    const targetSource = (appliedSuggestionId, currentTargetId) =>
        appliedSuggestionId && currentTargetId && appliedSuggestionId === currentTargetId ? 'SuggestionApplied' : 'Manual';

    return { ActionKeys, ActionWord, RowMenu, QuickViewMenu, actionState, menu, statusBadge, flagBadge, reasonWord, binVerdict, needsDeviation, step, isQuantity, candidateOption, suggestion, targetSource };
})();

if (typeof window !== 'undefined') window.PutawayRules = PutawayRules;
if (typeof module !== 'undefined' && module.exports) module.exports = PutawayRules;
