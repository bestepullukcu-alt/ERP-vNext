/**
 * MOD-0178 DL-6 — the PURE rules of the Packing page (pack §8.18, §8.20; the PutawayTasks/rules.js pattern — an extra file of the S2 set, like
 * DL-3's rules.js: Node measures it, frontend/Diten.Web.Tests/JavaScript/Node/warehouse-s2-rules.test.mjs).
 *
 *  - A package opens only on a Picked outbound ("Yeni koli" disabled with its reason otherwise); the service answers 409 anyway.
 *  - Only an Open package can be edited or closed; each control is absent without its key (packages.create / packages.update).
 *  - What is left to pack per allocation = picked − Σ contents of the Open and Closed packages (a Cancelled package holds nothing); the edited
 *    package's own lines are not counted against itself. The service owns Σ ≤ picked (422 WMS_PACKAGE_EXCEEDS_PICKED).
 */
'use strict';

const PackingRules = (function () {
    const CreateKey = 'warehouse.packages.create';
    const UpdateKey = 'warehouse.packages.update';

    const PackageStatusClass = { Open: 'bg-label-warning', Closed: 'bg-label-success', Cancelled: 'bg-label-secondary' };

    const num = (value) => {
        const n = Number.parseFloat(String(value ?? '').replace(',', '.'));
        return Number.isFinite(n) ? n : 0;
    };

    const round = (n) => Math.round(n * 1e6) / 1e6;

    /** allocSeq → quantity still to pack (≥ 0). */
    const remaining = (allocations, packages, exceptPackageId) => {
        const left = {};
        (allocations || []).forEach((a) => { left[a.allocSeq] = num(a.picked); });
        (packages || []).filter((p) => p && p.status !== 'Cancelled' && p.id !== exceptPackageId).forEach((p) => {
            (p.contents || []).forEach((c) => { if (c.allocSeq in left) left[c.allocSeq] = round(left[c.allocSeq] - num(c.quantity)); });
        });
        Object.keys(left).forEach((k) => { if (left[k] < 0) left[k] = 0; });
        return left;
    };

    /** "Yeni koli": absent without packages.create; enabled only on a Picked outbound, else disabled with its reason word. */
    const newPackage = (outbound, can) => {
        if (!can(CreateKey)) return { visible: false, enabled: false, reason: null };
        if (outbound?.status === 'Picked') return { visible: true, enabled: true, reason: null };
        return { visible: true, enabled: false, reason: outbound?.status === 'Packed' ? 'PackedDone' : 'NotPicked' };
    };

    /** An Open package's buttons for this reader (packages.update): Edit + Close; none for a Closed / Cancelled one. */
    const packageActions = (pkg, can) => (pkg?.status === 'Open' && can(UpdateKey) ? ['Edit', 'Close'] : []);

    /** The contents the form sends: lines with a quantity > 0 only, as wire decimals. */
    const contentsBody = (rows) => (rows || [])
        .filter((r) => num(r.quantity) > 0)
        .map((r) => ({ allocSeq: Number(r.allocSeq), quantity: String(r.quantity).trim().replace(',', '.') }));

    /** Dimensions: empty, or L x W x H in cm (three positive numbers). */
    const isDimensions = (text) => {
        const t = String(text ?? '').trim();
        return t === '' || /^\d{1,6}([.,]\d{1,3})?\s*[xX×]\s*\d{1,6}([.,]\d{1,3})?\s*[xX×]\s*\d{1,6}([.,]\d{1,3})?$/.test(t);
    };

    /** The wire form of the dimensions: "40x30x20". */
    const dimensions = (text) => String(text ?? '').trim().replace(/\s+/g, '').replace(/[X×]/g, 'x').replace(/,/g, '.');

    const statusBadge = (status) => ({ word: `Warehouse.Package.Status.${status}`, cls: PackageStatusClass[status] || 'bg-label-secondary' });

    const step = (value, delta, max) => {
        let next = num(value) + delta;
        if (next < 0) next = 0;
        const limit = Number.parseFloat(String(max ?? '').replace(',', '.'));
        if (Number.isFinite(limit) && next > limit) next = limit;
        return String(round(next));
    };

    return { CreateKey, UpdateKey, remaining, newPackage, packageActions, contentsBody, isDimensions, dimensions, statusBadge, step };
})();

if (typeof window !== 'undefined') window.PackingRules = PackingRules;
if (typeof module !== 'undefined' && module.exports) module.exports = PackingRules;
