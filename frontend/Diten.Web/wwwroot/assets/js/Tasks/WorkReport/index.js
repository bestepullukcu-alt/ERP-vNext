/*
 * ── THE WORK REPORT SCREEN (MOD-0024 Faz 5b) ────────────────────────────────────────────────────────────────
 *
 * Draws what Faz 5a counted. It computes NO measure of its own: every number on screen is a field of the
 * response, and the one place that would be tempting to derive — estimate against actual — is deliberately not
 * derived. Pack §8 excludes an efficiency percentage outright, and a screen that divided two published numbers
 * would reintroduce it behind the API's back.
 *
 * ⚠ EVERY VISUAL ANSWERS ONE QUESTION. Four tiles for the four measures whose answer is a number; three charts
 * for the three that are a comparison; a fourth chart only when a breakdown was asked for. Nothing is drawn to
 * fill space — the reference screens this product is measured against fail exactly there.
 */
(function () {
    'use strict';

    var ENDPOINT = '/Tasks/api/work-report';

    /* ── l10n ────────────────────────────────────────────────────────────────────────────────────────────── */

    var readJson = function (id) {
        var el = document.getElementById(id);
        if (!el) { return {}; }
        try { return JSON.parse(el.textContent || '{}'); } catch (e) { return {}; }
    };

    /*
     * The bridge the sibling screens use: MVC serializes with a camelCase policy, so the keys arrive lowercased
     * on the first letter. Normalising here means the rest of the file names keys the way the resx does.
     */
    var normalize = function (raw) {
        var out = {};
        Object.keys(raw || {}).forEach(function (key) {
            out[key.charAt(0).toUpperCase() + key.slice(1)] = raw[key];
        });
        return out;
    };

    var L = normalize(readJson('work-report-l10n'));

    /*
     * ⚠ THE OUTCOME MAP IS NOT NORMALISED — its keys are CODES, not resource names.
     * `COMPLETED_PARTIALLY` upper-cased on the first letter is still `COMPLETED_PARTIALLY`, but running it
     * through the same helper would say these are labels of the same kind as the rest, and they are not: they
     * are identities the engine stored. Read as it comes.
     */
    var OUTCOMES = readJson('work-report-outcomes-l10n');

    var t = function (key) { return L[key] || key; };

    var tf = function (key) {
        var args = Array.prototype.slice.call(arguments, 1);
        return args.reduce(function (text, value, i) {
            return text.split('{' + i + '}').join(String(value));
        }, t(key));
    };

    /**
     * One outcome's words.
     *
     * ⚠ FALLS BACK TO THE CODE, and that is the right answer twice over: a TENANT's own outcome has only the
     * words its administrator typed — in one language, with nothing to translate — and a SYSTEM outcome missing
     * from the map is a gap somebody needs to see rather than a blank slice in a chart.
     */
    var outcomeLabel = function (code) {
        return (code && OUTCOMES[code]) || code || '';
    };

    /* ── DOM ─────────────────────────────────────────────────────────────────────────────────────────────── */

    var $ = function (selector) { return document.querySelector(selector); };

    var setText = function (selector, text) {
        var el = $(selector);
        if (el) { el.textContent = text; }
    };

    var show = function (selector, visible) {
        var el = $(selector);
        // `hidden` rather than a style: FG-003 forbids writing element.style, and the attribute is what the
        // markup already uses to start these regions closed.
        if (el) { el.hidden = !visible; }
    };

    /* ── charts ──────────────────────────────────────────────────────────────────────────────────────────── */

    var charts = {};

    /**
     * Render or re-render one chart.
     *
     * ⚠ DESTROY BEFORE REDRAW. ApexCharts appends to the element it is given; without this, pressing Apply a
     * second time stacks a second chart under the first and the card grows forever. Measured on every
     * apex-driven screen that forgot it.
     */
    var draw = function (slot, selector, options) {
        var host = $(selector);
        if (!host || typeof window.ApexCharts !== 'function') { return; }

        if (charts[slot]) {
            charts[slot].destroy();
            charts[slot] = null;
        }

        host.innerHTML = '';
        charts[slot] = new window.ApexCharts(host, options);
        charts[slot].render();
    };

    var destroyAll = function () {
        Object.keys(charts).forEach(function (slot) {
            if (charts[slot]) { charts[slot].destroy(); charts[slot] = null; }
        });
    };

    /* ── rendering ───────────────────────────────────────────────────────────────────────────────────────── */

    /**
     * WHAT THE NUMBERS COVER — the sentence that separates "no work" from "no work I may see".
     *
     * Read from the RESPONSE, never from the permission the browser thinks it has: the server decides scope,
     * and a screen that guessed would eventually disagree with it.
     */
    var renderScope = function (report) {
        var tenant = report.scopeApplied === 'tenant';
        var badge = $('[data-wr-scope-badge]');
        if (badge) {
            badge.textContent = tenant ? t('ScopeTenant') : t('ScopeScoped');
            badge.className = 'badge ' + (tenant ? 'bg-label-primary' : 'bg-label-info');
        }
        setText('[data-wr-scope-hint]', tenant ? t('ScopeTenantHint') : t('ScopeScopedHint'));
        show('[data-wr-scope]', true);
    };

    var renderTiles = function (totals) {
        var cycle = totals.cycleTime || {};
        // Absent, not zero: the API sends null when nothing closed, because a zero reads as "everything closed
        // instantly" — the most flattering lie a report can tell.
        setText('[data-wr-cycle-value]',
            cycle.averageDays === null || cycle.averageDays === undefined
                ? t('NotMeasured')
                : tf('CycleTimeDays', cycle.averageDays));
        setText('[data-wr-cycle-over]', tf('CycleTimeOver', cycle.closedCount || 0));

        var rework = totals.rework || {};
        setText('[data-wr-rework-tasks]', tf('ReworkTasks', rework.tasksReturned || 0));
        setText('[data-wr-rework-returns]', tf('ReworkReturns', rework.totalReturns || 0));

        setText('[data-wr-unattended-value]', String((totals.flow || {}).unattended || 0));

        var effort = totals.effort || {};
        /*
         * ⚠ TWO NUMBERS SIDE BY SIDE — NEVER DIVIDED. Estimated and spent are printed as they arrive. Computing
         * a percentage here would put back exactly what `There_is_no_efficiency_percentage_anywhere_in_the_contract`
         * keeps out of the contract, one layer further from anyone who would notice.
         */
        setText('[data-wr-effort-value]',
            tf('EffortHours', effort.estimatedHours || 0, effort.spentHours || 0));
        setText('[data-wr-effort-over]', tf('EffortOver', effort.taskCount || 0));

        show('[data-wr-tiles]', true);
    };

    /**
     * ARE WE KEEPING UP WITH WHAT ARRIVES? — opened against closed, with the closure split beside it.
     *
     * ⚠ A COMPARISON, NOT A TIME SERIES, and that is a limit of the endpoint rather than a design choice: 5a
     * returns ONE period's totals and does no sub-period bucketing, so there is no series to plot. A line drawn
     * from four totals would be a picture of nothing. Bucketing belongs to the query if it is ever wanted.
     */
    var renderFlow = function (flow) {
        draw('flow', '[data-wr-chart-flow]', {
            chart: { type: 'bar', height: 260, toolbar: { show: false } },
            series: [{
                name: t('FlowTitle'),
                data: [flow.opened || 0, flow.closed || 0, flow.completed || 0, flow.cancelled || 0]
            }],
            xaxis: { categories: [t('Opened'), t('Closed'), t('Completed'), t('Cancelled')] },
            dataLabels: { enabled: true },
            plotOptions: { bar: { distributed: true, borderRadius: 4 } },
            legend: { show: false }
        });
    };

    /** WHAT DID THE CLOSURES DECIDE? — the outcome histogram (Faz 3's ClosureReasonCode). */
    var renderOutcomes = function (outcomes) {
        var rows = outcomes || [];
        // An empty donut is a grey ring that says nothing. A sentence says the thing.
        show('[data-wr-outcomes-empty]', rows.length === 0);

        if (rows.length === 0) {
            if (charts.outcomes) { charts.outcomes.destroy(); charts.outcomes = null; }
            var host = $('[data-wr-chart-outcomes]');
            if (host) { host.innerHTML = ''; }
            return;
        }

        draw('outcomes', '[data-wr-chart-outcomes]', {
            chart: { type: 'donut', height: 260 },
            series: rows.map(function (row) { return row.count || 0; }),
            labels: rows.map(function (row) { return outcomeLabel(row.code); }),
            legend: { position: 'bottom' }
        });
    };

    /** DID THE WORK LAND ON TIME? — and undated work is its own bar, never folded into "on time". */
    var renderTimeliness = function (timeliness) {
        draw('timeliness', '[data-wr-chart-timeliness]', {
            chart: { type: 'bar', height: 260, stacked: true, toolbar: { show: false } },
            series: [
                { name: t('OnTime'), data: [timeliness.onTime || 0] },
                { name: t('Late'), data: [timeliness.late || 0] },
                { name: t('WithoutDueDate'), data: [timeliness.withoutDueDate || 0] }
            ],
            xaxis: { categories: [t('TimelinessTitle')] },
            plotOptions: { bar: { horizontal: true, borderRadius: 4 } },
            legend: { position: 'bottom' }
        });
    };

    /** WHERE IS THE WORK HAPPENING? — only when a breakdown was asked for. */
    var renderGroups = function (report) {
        var groups = report.groups || [];
        var wanted = report.groupBy && report.groupBy !== 'None' && groups.length > 0;

        show('[data-wr-groups-card]', wanted);
        if (!wanted) {
            if (charts.groups) { charts.groups.destroy(); charts.groups = null; }
            return;
        }

        setText('[data-wr-groups-title]', tf('GroupsTitle', t('GroupBy' + report.groupBy)));

        draw('groups', '[data-wr-chart-groups]', {
            chart: { type: 'bar', height: Math.max(260, groups.length * 44), stacked: false, toolbar: { show: false } },
            series: [
                { name: t('Opened'), data: groups.map(function (g) { return (g.flow || {}).opened || 0; }) },
                { name: t('Closed'), data: groups.map(function (g) { return (g.flow || {}).closed || 0; }) }
            ],
            /*
             * A row whose key is "" is a task that names no type, or one nobody is holding — the API keeps it on
             * purpose so the groups still add up to the totals. It gets a WORD, because a nameless axis label
             * reads as a rendering bug.
             */
            xaxis: { categories: groups.map(function (g) { return g.key || t('GroupUnnamed'); }) },
            plotOptions: { bar: { horizontal: true, borderRadius: 4 } },
            legend: { position: 'bottom' }
        });
    };

    /**
     * Whether the period held any work at all.
     *
     * ⚠ `unattended` IS EXCLUDED FROM THIS TEST, deliberately. It counts open work as of NOW rather than within
     * the period, so a tenant with a standing backlog would make every empty period look busy — and the reader
     * would go looking for the four tasks the charts cannot show them.
     */
    var hasWork = function (totals) {
        var flow = totals.flow || {};
        return (flow.opened || 0) > 0 || (flow.closed || 0) > 0;
    };

    var render = function (report) {
        renderScope(report);

        var totals = report.totals || {};
        if (!hasWork(totals)) {
            /*
             * ⚠ A SENTENCE, NOT A CHART FULL OF ZEROES. Four bars at zero is a picture that takes a moment to
             * read and says less than one line. And the sentence changes with the scope: "no work I can see" is
             * a different fact from "no work", and the reader is the only one who can tell which matters.
             */
            destroyAll();
            show('[data-wr-tiles]', false);
            show('[data-wr-charts]', false);
            setText('[data-wr-status]',
                report.scopeApplied === 'tenant' ? t('NoData') : t('NoDataScoped'));
            return;
        }

        setText('[data-wr-status]', '');
        renderTiles(totals);
        show('[data-wr-charts]', true);
        renderFlow(totals.flow || {});
        renderOutcomes(totals.outcomes);
        renderTimeliness(totals.timeliness || {});
        renderGroups(report);
    };

    /* ── loading ─────────────────────────────────────────────────────────────────────────────────────────── */

    var query = function () {
        var from = ($('#wrFrom') || {}).value;
        var to = ($('#wrTo') || {}).value;
        var groupBy = ($('#wrGroupBy') || {}).value || 'None';
        return { from: from, to: to, groupBy: groupBy };
    };

    var load = function () {
        var q = query();

        /*
         * The period is required and must move forward. The API refuses both cases too — this is the courtesy,
         * not the enforcement, and it exists so a transposed pair of dates gets a sentence rather than a 400 the
         * reader has to interpret.
         */
        if (!q.from || !q.to || q.to <= q.from) {
            destroyAll();
            show('[data-wr-tiles]', false);
            show('[data-wr-charts]', false);
            setText('[data-wr-status]', t('PeriodInvalid'));
            return Promise.resolve();
        }

        setText('[data-wr-status]', t('Loading'));

        // Dates go as whole days at UTC midnight — `to` is EXCLUSIVE, which is what the picker's value means
        // here: the first day NOT counted.
        var url = ENDPOINT
            + '?from=' + encodeURIComponent(q.from + 'T00:00:00Z')
            + '&to=' + encodeURIComponent(q.to + 'T00:00:00Z')
            + '&groupBy=' + encodeURIComponent(q.groupBy);

        return fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
            .then(function (response) {
                if (!response.ok) { throw new Error('HTTP ' + response.status); }
                return response.json();
            })
            .then(function (payload) {
                // The gateway envelope carries the report under `data`; a bare body is accepted too so the
                // screen does not break if the envelope is ever unwrapped upstream.
                render((payload && payload.data) || payload || {});
            })
            .catch(function () {
                destroyAll();
                show('[data-wr-tiles]', false);
                show('[data-wr-charts]', false);
                setText('[data-wr-status]', t('LoadFailed'));
            });
    };

    document.addEventListener('DOMContentLoaded', function () {
        var form = $('#workReportFilter');
        if (!form) { return; }

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            load();
        });

        load();
    });

    // Exposed for the test harness — the real render path, not a copy of it.
    window.WorkReportScreen = { render: render, load: load, outcomeLabel: outcomeLabel, hasWork: hasWork };
})();
