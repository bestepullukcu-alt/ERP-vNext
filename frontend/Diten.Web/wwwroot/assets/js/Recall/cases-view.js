/**
 * MOD-0177 R6 — Vakalar, the PURE part (no DOM; the Node test runs it against THIS file). One merged row (RecallController list door:
 * rowKind Case | Drill, already in the server's order) → the texts of the nine columns. A DRILL row is not a case (RECALL 1.5.0, CT
 * decision A): its class and level are "—" (mockup difference D-25), its type says "Tatbikat", its progress cell is the drill result.
 * R7a: a REAL case row opens its Vaka Detayı (/Recall/Cases/Details/{caseId}). R7b: a drill row opens its run in the Tatbikat Planlayıcı
 * (/Recall/Drills?run={runId}) — only when fmt.canDrills (RECALL_DRILLS_VIEW); otherwise it stays without a link (no dead link).
 * R7c (CT decision on O-R7b-1): a FINISHED run (Completed / Cancelled) opens its Kanıt Paketi (/Recall/Evidence?drillRun={runId}, needs
 * fmt.canEvidence = RECALL_EVIDENCE_VIEW); a Running run opens the Tatbikat Planlayıcı.
 */
'use strict';

var RecallCasesView = (function () {
    const DASH = '—';

    /** A drill row's link: Running → the planner; Completed / Cancelled → that run's evidence pack; none without the page's key. */
    const drillLink = (row, fmt) => {
        const id = encodeURIComponent(row.runId || '');
        if (row.status === 'Running') return { clickable: !!fmt.canDrills && !!row.runId, href: `/Recall/Drills?run=${id}` };
        return { clickable: !!fmt.canEvidence && !!row.runId, href: `/Recall/Evidence?drillRun=${id}` };
    };

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
                statusCode: row.status, ...drillLink(row, fmt)
            };
        }

        const progress = (row.progress || []).map((p) => `${fmt.qty(p.returned)} / ${fmt.qty(p.shipped)} ${p.baseUomId}`).join(' · ') || DASH;
        return {
            number: row.number, kind: 'Case', type: t('Recall.Type.Real'), product: row.itemLabel || row.item?.itemId || '', lot: lot || '',
            class: t('Recall.Class.N', row.class), level: t('Recall.Level.' + row.level) || row.level, opened: fmt.date(row.openedAtUtc), owner, progress,
            status: t('Recall.Status.' + row.status), statusCode: row.status, clickable: true, href: `/Recall/Cases/Details/${encodeURIComponent(row.caseId)}`
        };
    };

    return { cells, drillLink, DASH };
})();

if (typeof window !== 'undefined') window.RecallCasesView = RecallCasesView;
