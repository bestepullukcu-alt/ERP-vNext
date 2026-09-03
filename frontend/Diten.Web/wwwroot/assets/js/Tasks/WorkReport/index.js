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

    /*
     * ── DIRECTION AGAINST THE PREVIOUS PERIOD ────────────────────────────────────────────────────────────
     *
     * Computed from TWO NUMBERS THE SERVER MEASURED, never from an arrow it pre-computed — a reader can check
     * a difference between two figures on screen and cannot check an arrow.
     *
     * ⚠ AND THE SCREEN NEVER WORKS OUT WHICH DAYS "PREVIOUS" MEANS. That definition lives once, in
     * `WorkReportRepository.PreviousPeriod`; a second copy here would drift by a day the first time somebody
     * reasoned about month lengths, and then two figures on this page would disagree.
     */
    var trend = function (selector, current, previous, lowerIsBetter) {
        var el = $(selector);
        if (!el) { return; }

        var has = typeof current === 'number' && typeof previous === 'number';
        el.hidden = !has;
        if (!has) { return; }

        var delta = Math.round((current - previous) * 100) / 100;
        if (delta === 0) {
            el.textContent = t('TrendSame');
            el.className = 'small mb-0 text-muted';
            return;
        }

        var up = delta > 0;
        // "Better" is not "bigger". More work closed is good; a longer cycle time is not — so each caller says
        // which direction it wants rather than the helper guessing from the number.
        var good = lowerIsBetter ? !up : up;
        el.textContent = tf(up ? 'TrendUp' : 'TrendDown', Math.abs(delta), previous);
        el.className = 'small mb-0 ' + (good ? 'text-success' : 'text-danger');
    };

    var duration = function (value) {
        return (value === null || value === undefined) ? t('NotMeasured') : tf('CycleTimeDays', value);
    };

    var renderTiles = function (totals, previous) {
        var cycle = totals.cycleTime || {};
        // Absent, not zero: the API sends null when nothing closed, because a zero reads as "everything closed
        // instantly" — the most flattering lie a report can tell.
        setText('[data-wr-cycle-value]', duration(cycle.averageDays));
        setText('[data-wr-cycle-median]', tf('CycleTimeMedian', duration(cycle.medianDays)));
        // ⚠ THE DENOMINATOR THE AVERAGE WAS ACTUALLY COMPUTED OVER — see WorkReportDuration.Count.
        setText('[data-wr-cycle-over]', tf('CycleTimeOver', cycle.count || 0));
        trend('[data-wr-cycle-trend]', cycle.averageDays, (previous && (previous.cycleTime || {}).averageDays), true);

        /*
         * The cancellation span, on its own line. Hidden when nothing was cancelled rather than shown as a
         * zero — "we abandoned work after 0 days" is a sentence nobody means.
         */
        var cancel = totals.cancellationTime || {};
        var cancelEl = $('[data-wr-cancel-value]');
        if (cancelEl) {
            cancelEl.hidden = !(cancel.count > 0);
            if (cancel.count > 0) {
                cancelEl.textContent = tf('CancelTime', duration(cancel.averageDays), duration(cancel.medianDays), cancel.count);
            }
        }

        var rework = totals.rework || {};
        setText('[data-wr-rework-tasks]', tf('ReworkTasks', rework.tasksReturned || 0));
        setText('[data-wr-rework-returns]', tf('ReworkReturns', rework.totalReturns || 0));

        setText('[data-wr-rework-trend]', '');
        trend('[data-wr-rework-trend]', (totals.rework || {}).totalReturns,
            (previous && (previous.rework || {}).totalReturns), true);

        setText('[data-wr-unattended-value]', String((totals.flow || {}).unattended || 0));

        /*
         * AGEING — measured at the PERIOD'S END, which is what makes the report evidence: the same period says
         * the same thing when it is reopened in a review months later. Hidden when nothing was open.
         */
        var aging = totals.aging || {};
        var agingTotal = (aging.upTo7Days || 0) + (aging.from8To30Days || 0) + (aging.olderThan30Days || 0);
        var agingEl = $('[data-wr-aging]');
        if (agingEl) {
            agingEl.hidden = agingTotal === 0;
            agingEl.textContent = tf('AgingBuckets',
                aging.upTo7Days || 0, aging.from8To30Days || 0, aging.olderThan30Days || 0);
        }

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

    /*
     * ── WHAT TO CALL A GROUP ─────────────────────────────────────────────────────────────────────────────
     *
     * Three sources, in order, and the order is the honesty:
     *   1. the reserved buckets, named HERE because they are sentences the server has no translation for;
     *   2. the server's own `label` — a type, unit or company name, from the data Platform owns;
     *   3. a name this screen resolved itself, which is ONLY the assignee axis (Platform has no user entity);
     *   4. failing all of that, the KEY — never an invented placeholder, which would put a word on screen that
     *      matches nothing anybody can search for.
     */
    var people = {};

    var groupLabel = function (group, groupBy) {
        if (group.key === '__unassigned__') { return t('GroupUnassigned'); }
        if (group.key === '__other__') { return t('GroupOther'); }
        if (group.label) { return group.label; }

        // The one axis the server cannot name. `priority` is named here too — it is an enum, and a server-side
        // English word would be a second, untranslated vocabulary.
        if (groupBy === 'Assignee' && group.key && people[group.key]) { return people[group.key]; }
        if (groupBy === 'Priority' && group.key) { return t('Priority' + group.key) || group.key; }

        return group.key || t('GroupUnnamed');
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

        /*
         * ⚠ THE CAP, STATED. Fifty groups is a reading limit; the tail is FOLDED into one bucket rather than
         * cut, so the parts still add up. But a reader comparing units has to be told they are looking at the
         * busiest fifty — a silent cut is how somebody concludes a unit has no work when it simply did not
         * place.
         */
        var truncated = report.groupsTruncated || 0;
        show('[data-wr-groups-truncated]', truncated > 0);
        if (truncated > 0) {
            setText('[data-wr-groups-truncated]', tf('GroupsTruncated', groups.length - 1, truncated));
        }

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
            xaxis: { categories: groups.map(function (g) { return groupLabel(g, report.groupBy); }) },
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
            // The trend and ageing lines live inside hidden cards, but they are hidden in their own right too:
            // a stale arrow surviving into an empty period would be a number about nothing.
            ['[data-wr-cycle-trend]', '[data-wr-cancel-value]', '[data-wr-rework-trend]',
             '[data-wr-aging]', '[data-wr-flow-trend]', '[data-wr-late-trend]']
                .forEach(function (sel) { show(sel, false); });
            setText('[data-wr-status]',
                report.scopeApplied === 'tenant' ? t('NoData') : t('NoDataScoped'));
            return;
        }

        setText('[data-wr-status]', '');

        // The previous period's TOTALS, when one was asked for. Null — not a bucket of zeroes — when it was
        // not, so the screen draws no arrow rather than a misleading downward one.
        var previous = report.previous && report.previous.totals;

        renderTiles(totals, previous);
        trend('[data-wr-flow-trend]', (totals.flow || {}).closed, previous && (previous.flow || {}).closed, false);
        trend('[data-wr-late-trend]', (totals.timeliness || {}).late,
            previous && (previous.timeliness || {}).late, true);
        show('[data-wr-charts]', true);
        renderFlow(totals.flow || {});
        renderOutcomes(totals.outcomes);
        renderTimeliness(totals.timeliness || {});
        renderGroups(report);
    };

    /* ── loading ─────────────────────────────────────────────────────────────────────────────────────────── */

    var valueOf = function (selector) {
        var el = $(selector);
        return (el && el.value) ? el.value : '';
    };

    var query = function () {
        return {
            from: valueOf('#wrFrom'),
            to: valueOf('#wrTo'),
            groupBy: valueOf('#wrGroupBy') || 'None',
            /*
             * ⚠ FILTERS, NOT PERMISSIONS. Each one narrows the scope the SERVER resolved; none of them can widen
             * it. Naming somebody else's id here comes back empty rather than as their work, because the server
             * applies the scope before it applies any of this.
             */
            legalEntityId: valueOf('#wrLegalEntity'),
            organizationUnitId: valueOf('#wrUnit'),
            taskTypeCode: valueOf('#wrTaskType'),
            assigneeUserId: valueOf('#wrAssignee'),
            priority: valueOf('#wrPriority')
        };
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
            + '&groupBy=' + encodeURIComponent(q.groupBy)
            // The SERVER decides which days "previous" means. Asking for it is all the screen does.
            + '&comparePrevious=true';

        // Only what was actually chosen travels: an empty parameter is not "match nothing", it is "not asked",
        // and the server's contract says so by making every filter nullable.
        ['legalEntityId', 'organizationUnitId', 'taskTypeCode', 'assigneeUserId', 'priority'].forEach(function (name) {
            if (q[name]) { url += '&' + name + '=' + encodeURIComponent(q[name]); }
        });

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

    /* ── filter option sources ───────────────────────────────────────────────────────────────────────────── */

    /*
     * ⚠ NO NEW ENDPOINT WAS WRITTEN FOR ANY OF THESE. Each is a lookup this product already serves, called
     * same-origin through a proxy that already existed:
     *   companies → /Tasks/api/legal-entities   (already on TasksController, hits /api/legal-entities/lookup)
     *   units     → /OrganizationUnits/api
     *   types     → /Tasks/api/task-types/active
     *   people    → /Platform/Workflow/lookup/users   ← the person picker this product already uses
     *
     * MEASURED 2026-09-04: Platform has NO user entity and no auth client, which is why the person axis is the
     * one the server cannot label and the one resolved here.
     */
    var getJson = function (url) {
        return fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (p) {
                var body = (p && p.data !== undefined) ? p.data : p;
                return Array.isArray(body) ? body : (body && Array.isArray(body.items) ? body.items : []);
            })
            .catch(function () { return []; });
    };

    var pick = function (row, names) {
        for (var i = 0; i < names.length; i++) {
            if (row[names[i]]) { return row[names[i]]; }
        }
        return '';
    };

    /**
     * Fill one picker, keeping its "any" option first.
     *
     * ⚠ THE CURRENT VALUE SURVIVES A REFILL when it is still offered — otherwise narrowing the units by company
     * would silently clear a unit the reader had already chosen, and the next Apply would quietly widen the
     * report they were looking at.
     */
    var fillSelect = function (selector, rows, valueKeys, labelKeys) {
        var el = $(selector);
        if (!el) { return; }

        var previous = el.value;
        var any = el.querySelector('option[value=""]');
        el.innerHTML = '';
        if (any) { el.appendChild(any); }

        rows.forEach(function (row) {
            var value = pick(row, valueKeys);
            if (!value) { return; }
            var option = document.createElement('option');
            option.value = value;
            option.textContent = pick(row, labelKeys) || value;
            el.appendChild(option);
        });

        if (previous && el.querySelector('option[value="' + previous.replace(/"/g, '\\"') + '"]')) {
            el.value = previous;
        }
    };

    var allUnits = [];

    /** Units, narrowed to the chosen company — the one dependency between two filters. */
    var refreshUnits = function () {
        var company = valueOf('#wrLegalEntity');
        var rows = company
            ? allUnits.filter(function (u) { return String(u.legalEntityId || u.LegalEntityId || '') === company; })
            : allUnits;
        fillSelect('#wrUnit', rows, ['id', 'Id'], ['name', 'Name', 'code', 'Code']);
    };

    var loadFilterOptions = function () {
        return Promise.all([
            getJson('/Tasks/api/legal-entities'),
            getJson('/OrganizationUnits/api'),
            getJson('/Tasks/api/task-types/active'),
            getJson('/Platform/Workflow/lookup/users')
        ]).then(function (results) {
            fillSelect('#wrLegalEntity', results[0], ['id', 'Id', 'legalEntityId'], ['displayName', 'name', 'legalName', 'Name']);

            allUnits = results[1] || [];
            refreshUnits();

            // The CODE is the value — the filter matches on the code a person reads and types, not on an id.
            fillSelect('#wrTaskType', results[2], ['code', 'Code'], ['name', 'Name']);

            var users = results[3] || [];
            fillSelect('#wrAssignee', users, ['id', 'Id', 'userId'], ['displayName', 'fullName', 'name', 'email']);

            // The same names label the ASSIGNEE breakdown — the server cannot supply them, so this is the only
            // place they can come from.
            people = {};
            users.forEach(function (u) {
                var id = pick(u, ['id', 'Id', 'userId']);
                var name = pick(u, ['displayName', 'fullName', 'name', 'email']);
                if (id && name) { people[String(id)] = name; }
            });
        });
    };

    document.addEventListener('DOMContentLoaded', function () {
        var form = $('#workReportFilter');
        if (!form) { return; }

        var company = $('#wrLegalEntity');
        if (company) { company.addEventListener('change', refreshUnits); }

        // The report is drawn regardless of whether the lookups answered: a picker that failed to load leaves
        // the reader with fewer choices, not with no report.
        loadFilterOptions().catch(function () { /* reported by the empty pickers themselves */ });

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            load();
        });

        load();
    });

    // Exposed for the test harness — the real render path, not a copy of it.
    window.WorkReportScreen = {
        render: render, load: load, outcomeLabel: outcomeLabel, hasWork: hasWork,
        // Exposed for the harness — the real functions, not copies of them.
        groupLabel: groupLabel, query: query,
        setPeople: function (map) { people = map || {}; }
    };
})();
