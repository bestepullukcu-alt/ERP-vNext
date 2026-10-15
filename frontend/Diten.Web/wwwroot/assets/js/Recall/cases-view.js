/**
 * MOD-0177 R6 — Vakalar, the PURE part (no DOM; the Node test runs it against THIS file). One merged row (RecallController list door:
 * rowKind Case | Drill, already in the server's order) → the texts of the nine columns. A DRILL row is not a case (RECALL 1.5.0, CT
 * decision A): its class and level are "—" (mockup difference D-25), its type says "Tatbikat", its progress cell is the drill result.
 * No row links anywhere in R6 (the case detail and the drill planner are R7 — no dead link).
 */
'use strict';

var RecallCasesView = (function () {
    const DASH = '—';

    /** column → { text, html? } for one row; fmt = { t, qty, date, duration }. */
    const cells = (row, fmt) => {
        const t = fmt.t;
        const drill = row.rowKind === 'Drill';
        const lot = drill ? row.lot?.lotNumber : row.rootLot?.lotNumber;
        const owner = row.owner?.name || row.owner?.userId || '';
        if (drill) {
            const result = row.passed == null
                ? t('Recall.DrillStatus.' + row.status)
                : `${row.passed ? t('Recall.Drill.Passed') : t('Recall.Drill.Failed')} · ${fmt.duration(row.durationMinutes)}`;
            return {
                number: row.number, kind: 'Drill', type: t('Recall.Type.Drill'), product: row.itemLabel || row.item?.itemId || '', lot: lot || '',
                class: DASH, level: DASH, opened: fmt.date(row.startedAtUtc), owner, progress: result, status: t('Recall.DrillStatus.' + row.status),
                statusCode: row.status, clickable: false
            };
        }

        const progress = (row.progress || []).map((p) => `${fmt.qty(p.returned)} / ${fmt.qty(p.shipped)} ${p.baseUomId}`).join(' · ') || DASH;
        return {
            number: row.number, kind: 'Case', type: t('Recall.Type.Real'), product: row.itemLabel || row.item?.itemId || '', lot: lot || '',
            class: t('Recall.Class.N', row.class), level: t('Recall.Level.' + row.level) || row.level, opened: fmt.date(row.openedAtUtc), owner, progress,
            status: t('Recall.Status.' + row.status), statusCode: row.status, clickable: false
        };
    };

    return { cells, DASH };
})();

if (typeof window !== 'undefined') window.RecallCasesView = RecallCasesView;
