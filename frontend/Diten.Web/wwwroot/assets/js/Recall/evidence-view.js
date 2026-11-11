/**
 * MOD-0177 R7b — Kanıt Paketi, the PURE part (no DOM; the Node test runs it against THIS file). evidence.js draws, this file decides:
 *   • the subject — a case, a drill run, or a period (from ≤ to, at most 366 days); exactly one;
 *   • the preview — the seven sections in their order (1_summary … 7_signatures), as the service built them (the file carries the same);
 *   • "PDF indir" / "CSV indir" — createEvidenceExport (the service renders, hashes THE produced bytes and logs: D-12), then the stored file is
 *     downloaded and its SHA-256 computed HERE again: the page says "verified" only when the downloaded bytes hash to the logged value.
 */
'use strict';

var RecallEvidenceView = (function () {
    const SECTIONS = ['1_summary', '2_trace', '3_quarantine', '4_customers', '5_returnsAndEffectiveness', '6_timeline', '7_signatures'];
    const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    const DAY = /^\d{4}-\d{2}-\d{2}$/;
    const MAX_DAYS = 366;

    /** { kind: 'Case' | 'DrillRun' | 'Period', id?, from?, to? } → { ok, params } | { ok: false, error }. */
    const subject = (s) => {
        if (s?.kind === 'Case' && GUID.test(s.id || '')) return { ok: true, params: { caseId: s.id } };
        if (s?.kind === 'DrillRun' && GUID.test(s.id || '')) return { ok: true, params: { runId: s.id } };
        if (s?.kind === 'Period') {
            if (!DAY.test(s.from || '') || !DAY.test(s.to || '')) return { ok: false, error: 'Recall.Evidence.PeriodRequired' };
            const days = (Date.parse(s.to + 'T00:00:00Z') - Date.parse(s.from + 'T00:00:00Z')) / 86400000;
            if (!(days >= 0) || days + 1 > MAX_DAYS) return { ok: false, error: 'Recall.Errors.RECALL_EVIDENCE_RANGE_INVALID' };
            return { ok: true, params: { from: s.from, to: s.to } };
        }
        return { ok: false, error: 'Recall.Evidence.SubjectRequired' };
    };

    const query = (params) => Object.entries(params).map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`).join('&');

    /** The preview's sections in the pack order — a missing one is listed as missing (never invented). */
    const sections = (preview) => SECTIONS.map((key) => ({ key, section: preview?.sections?.[key] || null }));

    /** The subject picker's options from the merged list (cases + drill rows). */
    const options = (rows) => (rows || []).map((r) => (r.rowKind === 'Drill'
        ? { kind: 'DrillRun', id: r.runId, number: r.number, status: r.status, lot: r.lot?.lotNumber || '' }
        : { kind: 'Case', id: r.caseId, number: r.number, status: r.status, lot: r.rootLot?.lotNumber || '' })).filter((o) => o.id);

    /** Hex of a SHA-256 digest (ArrayBuffer | Uint8Array). */
    const hex = (digest) => Array.from(new Uint8Array(digest)).map((b) => b.toString(16).padStart(2, '0')).join('');

    /**
     * deps: { write(path, body, { language }) → { ok, status, json }, newKey(), download(url) → { ok, bytes: Uint8Array }, sha256(bytes) → Promise<hex> }.
     * The export, then the download of THE stored file, then the comparison of its hash with the logged one.
     */
    const exportFile = async (deps, s, format, language) => {
        const picked = subject(s);
        if (!picked.ok) return { ok: false, step: 'validate', error: picked.error };
        if (format !== 'Pdf' && format !== 'Csv') return { ok: false, step: 'validate', error: 'Recall.Errors.GENERIC' };
        const reply = await deps.write('/Recall/cmd/evidence/exports', { idempotencyKey: deps.newKey(), format, ...picked.params }, { language });
        if (!reply.ok) return { ok: false, step: 'export', reply };
        const exported = reply.json;
        const file = await deps.download(`/Recall/export/evidence/${encodeURIComponent(exported.exportId)}`);
        if (!file.ok) return { ok: false, step: 'download', reply: file, exported };
        const computed = await deps.sha256(file.bytes);
        return { ok: true, exported, bytes: file.bytes, computed, verified: computed === String(exported.sha256).toLowerCase() };
    };

    return { SECTIONS, MAX_DAYS, subject, query, sections, options, hex, exportFile };
})();

if (typeof window !== 'undefined') window.RecallEvidenceView = RecallEvidenceView;
