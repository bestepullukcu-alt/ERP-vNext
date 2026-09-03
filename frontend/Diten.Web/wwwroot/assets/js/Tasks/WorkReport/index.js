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
    // Dilim 1c — the work behind one of the numbers above. Same tier, same proxy pattern, one path segment on.
    var ITEMS_ENDPOINT = '/Tasks/api/work-report/items';

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

    /**
     * THE SAME TEMPLATE `tf()` READS, with one or more of its `{n}` tokens wrapped as a clickable number —
     * for a sentence where only PART of it opens a list, like the ageing line's three bands.
     *
     * ⚠ WHY NOT THREE SEPARATE ELEMENTS. Dilim 1b's own tests read `.textContent` of `[data-wr-aging]` and
     * assert it equals the FULL localized sentence, in whichever of the seven languages puts the numbers in
     * whichever order. Splitting the paragraph into three siblings would answer a different question in every
     * language a translator chose to reorder the clauses in. `.textContent` concatenates every descendant text
     * node regardless of markup, so wrapping the SAME template's tokens in spans changes nothing 1b already
     * checks and adds exactly the three click targets 1c needs.
     *
     * `parts` is `{0: {value, click}, 1: {...}, ...}` — a token missing from `parts` is substituted as plain
     * text, so a caller that wants some tokens clickable and others not (there are none of those yet, but nothing
     * here assumes otherwise) can express that.
     */
    var tfHtml = function (key, parts) {
        var template = t(key);
        var out = '';
        var i = 0;
        while (i < template.length) {
            var token = /^\{(\d+)\}/.exec(template.slice(i));
            if (token) {
                var index = token[1];
                var part = parts[index];
                if (part) {
                    out += '<span class="wr-clickable" role="button" tabindex="0" data-wr-click="'
                        + esc(part.click) + '">' + esc(part.value) + '</span>';
                } else {
                    out += esc(part && part.value !== undefined ? part.value : '');
                }
                i += token[0].length;
            } else {
                out += esc(template[i]);
                i += 1;
            }
        }
        return out;
    };

    /* ── DOM ─────────────────────────────────────────────────────────────────────────────────────────────── */

    var $ = function (selector) { return document.querySelector(selector); };

    /**
     * ⚠ EVERY STRING FROM THE SERVER THAT REACHES `innerHTML` GOES THROUGH THIS FIRST.
     *
     * A task title is text somebody typed, not a translated label — Dilim 1c is the first thing on this screen
     * to render one, and `.textContent`/`setText` (which never parses markup) stop being enough the moment a
     * row is assembled as an HTML string. Escaping happens exactly here, once, so no render path can forget it.
     */
    var esc = function (value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

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
            // Three clickable numbers INSIDE one sentence — see tfHtml for why this is not three elements.
            agingEl.innerHTML = tfHtml('AgingBuckets', {
                0: { value: aging.upTo7Days || 0, click: 'AgingUpTo7Days' },
                1: { value: aging.from8To30Days || 0, click: 'AgingFrom8To30Days' },
                2: { value: aging.olderThan30Days || 0, click: 'AgingOlderThan30Days' }
            });
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
        // The bar's own order IS the click map — one array, read by both the chart and the handler below, so
        // the two cannot name the bars differently.
        var kinds = ['Opened', 'Closed', 'Completed', 'Cancelled'];

        draw('flow', '[data-wr-chart-flow]', {
            chart: {
                type: 'bar', height: 260, toolbar: { show: false },
                // ⚠ EVERY CHART CLICK IN THIS FILE GOES THROUGH `openItems` — see its own comment for why a
                // second, chart-local query would be the exact defect Dilim 1a's CONTROL TOWER sabotage found.
                events: { dataPointSelection: function (_e, _ctx, cfg) { openItems(kinds[cfg.dataPointIndex]); } }
            },
            series: [{
                name: t('FlowTitle'),
                data: [flow.opened || 0, flow.closed || 0, flow.completed || 0, flow.cancelled || 0]
            }],
            xaxis: { categories: [t('Opened'), t('Closed'), t('Completed'), t('Cancelled')] },
            dataLabels: { enabled: true },
            plotOptions: { bar: { distributed: true, borderRadius: 4, cursor: 'pointer' } },
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
            chart: {
                type: 'donut', height: 260,
                // A slice's ARGUMENT is the CODE `rows` carries — never the translated label the axis shows,
                // because the code is the identity the server's `Outcome` cell matches on.
                events: { dataPointSelection: function (_e, _ctx, cfg) { openItems('Outcome', rows[cfg.dataPointIndex].code); } }
            },
            series: rows.map(function (row) { return row.count || 0; }),
            labels: rows.map(function (row) { return outcomeLabel(row.code); }),
            legend: { position: 'bottom' }
        });
    };

    /** DID THE WORK LAND ON TIME? — and undated work is its own bar, never folded into "on time". */
    var renderTimeliness = function (timeliness) {
        // One series per band; `seriesIndex` is which band was clicked, not which category — there is only one.
        var kinds = ['OnTime', 'Late', 'WithoutDueDate'];

        draw('timeliness', '[data-wr-chart-timeliness]', {
            chart: {
                type: 'bar', height: 260, stacked: true, toolbar: { show: false },
                events: { dataPointSelection: function (_e, _ctx, cfg) { openItems(kinds[cfg.seriesIndex]); } }
            },
            series: [
                { name: t('OnTime'), data: [timeliness.onTime || 0] },
                { name: t('Late'), data: [timeliness.late || 0] },
                { name: t('WithoutDueDate'), data: [timeliness.withoutDueDate || 0] }
            ],
            xaxis: { categories: [t('TimelinessTitle')] },
            plotOptions: { bar: { horizontal: true, borderRadius: 4, cursor: 'pointer' } },
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

        // One series per bar; which one was clicked decides whether the drill-down opens what OPENED in
        // that group or what CLOSED in it — both are real, published numbers for the row.
        var seriesKinds = ['Opened', 'Closed'];

        draw('groups', '[data-wr-chart-groups]', {
            chart: {
                type: 'bar', height: Math.max(260, groups.length * 44), stacked: false, toolbar: { show: false },
                events: {
                    dataPointSelection: function (_e, _ctx, cfg) {
                        // ⚠ THE GROUP'S OWN KEY, never its (possibly reserved, possibly translated) LABEL — the
                        // items endpoint matches group membership on the raw key exactly as the chart's own data
                        // was built from it.
                        openItems(seriesKinds[cfg.seriesIndex], null, groups[cfg.dataPointIndex].key);
                    }
                }
            },
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
            plotOptions: { bar: { horizontal: true, borderRadius: 4, cursor: 'pointer' } },
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
        // The report a click's list is opened against — see `openItems`. Kept even for an empty period so a
        // reader who already has the panel open sees it close rather than keep showing a stale list.
        lastReport = report;

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
            closeItems();
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
                /*
                 * ⚠ THE EXACT PARAMETERS THAT PRODUCED WHAT IS ON SCREEN — captured HERE, not read live from
                 * the pickers on click. If a reader changes a filter but has not pressed Apply, the numbers on
                 * screen are still the OLD query's; a click that read the picker's current value would open a
                 * list for a report that is not the one being looked at.
                 */
                lastQuery = q;

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

    /* ── Dilim 1c — FROM A NUMBER TO THE WORK ITSELF ────────────────────────────────────────────────────── */

    /*
     * ⚠ WHY EVERY DRILL-DOWN GOES THROUGH ONE FUNCTION (`openItems`), CALLED FROM ONE PLACE PER NUMBER.
     *
     * Dilim 1a's own history is the reason: `WorkReportFilterTests` once rebuilt the production filter order
     * INSIDE the test, a CONTROL TOWER sabotage that dropped the scope from the real composition stayed green,
     * and the fix was to give production exactly one named composition for a test to point at. A second,
     * chart-local query here — "just fetch the late ones for this bar" — would be that same shape one layer up:
     * a list nobody was watching disagreeing with a number everybody was. So no click handler in this file talks
     * to `fetch` directly; every one of them names a bucket (and, for a chart, an argument or a group key it
     * read off its OWN data) and calls this.
     */
    var lastQuery = null;
    var lastReport = null;
    var itemsState = null;   // { bucket, argument, groupKey, skip, total, rows }

    var itemsUrl = function (bucket, argument, groupKey, skip) {
        var url = ITEMS_ENDPOINT
            + '?from=' + encodeURIComponent(lastQuery.from + 'T00:00:00Z')
            + '&to=' + encodeURIComponent(lastQuery.to + 'T00:00:00Z')
            + '&bucket=' + encodeURIComponent(bucket)
            + '&groupBy=' + encodeURIComponent(lastQuery.groupBy)
            + '&skip=' + encodeURIComponent(skip || 0);

        if (argument) { url += '&argument=' + encodeURIComponent(argument); }
        if (groupKey !== undefined && groupKey !== null) { url += '&groupKey=' + encodeURIComponent(groupKey); }

        // ⚠ THE SAME FIVE FILTERS AS THE REPORT — nothing added, nothing dropped. A list under a different
        // filter than the one that produced the numbers would answer about a different set than the one clicked.
        ['legalEntityId', 'organizationUnitId', 'taskTypeCode', 'assigneeUserId', 'priority'].forEach(function (name) {
            if (lastQuery[name]) { url += '&' + name + '=' + encodeURIComponent(lastQuery[name]); }
        });

        return url;
    };

    /** The cell's own title — reusing the report's OWN labels wherever one already exists. */
    var KIND_TITLES = {
        Opened: 'Opened', Closed: 'Closed', Completed: 'Completed', Cancelled: 'Cancelled',
        Unattended: 'UnattendedTitle', OnTime: 'OnTime', Late: 'Late', WithoutDueDate: 'WithoutDueDate',
        AgingUpTo7Days: 'ItemsAging0to7', AgingFrom8To30Days: 'ItemsAging8to30',
        AgingOlderThan30Days: 'ItemsAging30plus', Returned: 'ReworkTitle'
    };

    var cellTitle = function (bucket, argument) {
        if (bucket === 'Outcome') { return outcomeLabel(argument); }
        return t(KIND_TITLES[bucket] || bucket);
    };

    /** "3 Jun 2026 – 3 Jul 2026", using the reader's own browser locale — not a translated sentence. */
    var dateRange = function () {
        if (!lastReport) { return ''; }
        var fmt = function (iso) { return iso ? new Date(iso).toLocaleDateString() : ''; };
        return tf('ItemsSubtitleRange', fmt(lastReport.from), fmt(lastReport.to));
    };

    var subtitle = function (groupKey) {
        var range = dateRange();
        if (!groupKey) { return range; }

        var groups = (lastReport && lastReport.groups) || [];
        var found = null;
        for (var i = 0; i < groups.length; i++) {
            if (groups[i].key === groupKey) { found = groups[i]; break; }
        }
        // Same fallback ladder `groupLabel` uses for the axis itself, so the panel names a group exactly as
        // the bar the reader clicked was labelled.
        var label = found ? groupLabel(found, lastReport.groupBy) : groupKey;
        return tf('ItemsSubtitleWithGroup', range, label);
    };

    var lifecycleWord = function (lifecycle) {
        if (lifecycle === 'Done') { return t('Completed'); }
        if (lifecycle === 'Cancelled') { return t('Cancelled'); }
        // Every non-terminal state (Open, Planned, InProgress, Waiting, PendingReview) reads as one word here —
        // which of those it is in detail is the DETAIL PAGE's question, not this list's.
        return t('ItemsStatusOpen');
    };

    /**
     * ONE ROW — the whole row IS the link to the task's detail page (`/WorkCenterNext/Details/{id}`, the route
     * every other surface in this product uses), rather than a separate button, so keyboard and screen-reader
     * users get one obvious target instead of a row that half-works with either.
     */
    var itemRowHtml = function (item) {
        var due = item.dueAt ? new Date(item.dueAt).toLocaleDateString() : '—';
        var closed = item.closedAt ? new Date(item.closedAt).toLocaleDateString() : '—';
        var assignee = item.assigneeUserId
            ? esc(people[item.assigneeUserId] || item.assigneeUserId)
            : esc(t('ItemsUnassigned'));

        return '<a class="d-flex flex-column gap-1 py-2 border-bottom text-body text-decoration-none wr-item-row"'
            + ' href="/WorkCenterNext/Details/' + esc(item.id) + '">'
            + '<span class="text-truncate fw-medium">' + esc(item.title || item.id) + '</span>'
            + '<span class="small text-muted">'
            + esc(lifecycleWord(item.lifecycle)) + ' \u00b7 ' + assignee
            + ' \u00b7 ' + esc(tf('ItemsColDue', due)) + ' \u00b7 ' + esc(tf('ItemsColClosed', closed))
            + '</span></a>';
    };

    /** The panel's Bootstrap instance, created lazily — the same `getOrCreateInstance` pattern every other
     * offcanvas in this product uses (PPM's `_DetailsQuickView`, MDM's `_CreateEditOffcanvas`). */
    var itemsOffcanvas = function () {
        var el = $('#wrItemsOffcanvas');
        return (el && window.bootstrap && window.bootstrap.Offcanvas)
            ? window.bootstrap.Offcanvas.getOrCreateInstance(el)
            : null;
    };

    var closeItems = function () {
        var instance = itemsOffcanvas();
        if (instance) { instance.hide(); }
    };

    var renderItemsList = function () {
        var host = $('[data-wr-items-list]');
        if (!host) { return; }

        /*
         * ⚠ ALWAYS THE WHOLE OF `itemsState.rows`, NEVER AN INCREMENT APPENDED ONTO THE EXISTING innerHTML.
         *
         * `loadItemsPage` already concatenates a "show more" page onto `itemsState.rows` before this runs, so
         * `itemsState.rows` is the complete set either way. Re-appending the (already-cumulative) rendered HTML
         * onto what was already on screen doubled every row from the first page the moment a second page was
         * requested — live-verified: page one drew 50 rows, "show more" drew 56, and the DOM ended up holding
         * 106 because the first 50 were rendered a second time on top of themselves. One assignment, from the
         * one state array, is what makes the DOM match `itemsState.rows` instead of drifting further from it on
         * every page.
         */
        host.innerHTML = itemsState.rows.map(itemRowHtml).join('');

        show('[data-wr-items-empty]', itemsState.total === 0);
        setText('[data-wr-items-count]',
            itemsState.total === 0 ? '' : tf('ItemsCount', itemsState.rows.length, itemsState.total));

        var more = $('[data-wr-items-more]');
        if (more) {
            more.hidden = !itemsState.hasMore;
            if (itemsState.hasMore) {
                setText('[data-wr-items-more]', tf('ItemsShowMore', itemsState.total - itemsState.rows.length));
            }
        }
    };

    /**
     * Fetches one page of a cell and renders it — `append` true for "show more", false for a fresh open.
     *
     * ⚠ NOTHING HERE COMPUTES A COUNT. `total` and `hasMore` are the RESPONSE's own fields
     * (`WorkReportItemsDto.Total`/`HasMore`) — never `rows.length`, which is the exact substitution
     * `WorkReportTally.Page`'s own doc-comment warns against: the moment the cap bites, a list that reported
     * its own length would silently rewrite "83 opened" as "50 opened" while the tile beside it still said 83.
     */
    /** `append` is true for "show more" (concatenate onto the existing state), false for a fresh open. */
    var loadItemsPage = function (append) {
        var url = itemsUrl(itemsState.bucket, itemsState.argument, itemsState.groupKey, itemsState.skip);

        show('[data-wr-items-loading]', true);
        show('[data-wr-items-error]', false);

        /*
         * ⚠ NOT `getJson` — that helper is built for the FILTER LOOKUPS (companies, units, types, people) and
         * unwraps its response down to a bare ARRAY, discarding everything else. `WorkReportItemsDto` is an
         * OBJECT whose `total` and `hasMore` matter as much as its `items`, so this fetches for itself, the
         * same way `load()` does for the report.
         */
        return fetch(url, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
            .then(function (response) {
                if (!response.ok) { throw new Error('HTTP ' + response.status); }
                return response.json();
            })
            .then(function (payload) {
                var data = (payload && payload.data) || payload || {};
                var rows = data.items || [];

                itemsState.rows = append ? itemsState.rows.concat(rows) : rows;
                itemsState.total = data.total || 0;
                itemsState.hasMore = !!data.hasMore;
                itemsState.skip = itemsState.rows.length;

                renderItemsList();
            })
            .catch(function () {
                show('[data-wr-items-error]', true);
            })
            .then(function () {
                show('[data-wr-items-loading]', false);
            });
    };

    /**
     * THE ONE DOOR EVERY CLICKABLE NUMBER GOES THROUGH — see the comment above `lastQuery` for why there is
     * only one.
     *
     * ⚠ CANNOT OPEN WHAT THE REPORT DID NOT ALREADY COUNT. Every argument here is either a bucket kind this
     * file's own static markup or its own chart data supplied, or a group key read off the report's OWN
     * `groups[].key` — there is no path from a click to a request this screen invented. The server repeats the
     * same discipline one layer down: `WorkReportItemsCriteria` carries the report's own scope, and the SAME
     * five filters travel through `itemsUrl` unchanged.
     */
    var openItems = function (bucket, argument, groupKey) {
        if (!lastQuery) { return; }

        itemsState = { bucket: bucket, argument: argument || null, groupKey: groupKey || null, skip: 0, total: 0, rows: [], hasMore: false };

        setText('[data-wr-items-title]', cellTitle(bucket, argument));
        setText('[data-wr-items-subtitle]', subtitle(groupKey));
        setText('[data-wr-items-count]', '');
        show('[data-wr-items-empty]', false);
        show('[data-wr-items-error]', false);
        var host = $('[data-wr-items-list]');
        if (host) { host.innerHTML = ''; }
        var more = $('[data-wr-items-more]');
        if (more) { more.hidden = true; }

        var instance = itemsOffcanvas();
        if (instance) { instance.show(); }

        // Returned so a caller (and the test harness) can wait for the first page to land.
        return loadItemsPage(false);
    };

    /*
     * ── WIRING — one delegated listener for every clickable number, static or built by `tfHtml`. ────────────
     *
     * Delegation rather than a listener per element: the aging spans do not exist until the first render, and a
     * per-element `addEventListener` would either miss them or need re-attaching on every redraw. `closest`
     * finds the nearest `[data-wr-click]` ancestor so a click landing on whitespace inside the element still
     * resolves to it.
     *
     * ⚠ GUARDED AGAINST DOUBLE REGISTRATION, AND DISPATCHED THROUGH `window.WorkReportScreen` RATHER THAN THE
     * CLOSURE DIRECTLY. `document` itself is never rebuilt the way `document.body`'s contents are on every
     * render, so a page that loaded this script twice — a duplicate `<script>` tag, a bundler mistake — would
     * otherwise open the panel twice and fire the fetch twice per click. The guard makes a second load a no-op;
     * looking the handler up on `window.WorkReportScreen` (reassigned at the bottom of THIS run, every run)
     * means the one surviving listener always calls the CURRENT script's functions rather than the first one
     * that ever ran, which is what makes a re-load safe rather than merely silent.
     */
    if (!document.__wrItemsWired) {
        document.__wrItemsWired = true;

        document.addEventListener('click', function (event) {
            var target = event.target.closest && event.target.closest('[data-wr-click]');
            if (target && window.WorkReportScreen) {
                window.WorkReportScreen.openItems(target.getAttribute('data-wr-click'));
            }
        });

        // The same targets are `role="button" tabindex="0"` — real buttons and links fire on Enter/Space by
        // themselves, but that markup gets neither for free, so this file gives it both explicitly.
        document.addEventListener('keydown', function (event) {
            if (event.key !== 'Enter' && event.key !== ' ') { return; }
            var target = event.target.closest && event.target.closest('[data-wr-click]');
            if (target && window.WorkReportScreen) {
                event.preventDefault();
                window.WorkReportScreen.openItems(target.getAttribute('data-wr-click'));
            }
        });

        document.addEventListener('click', function (event) {
            if (event.target.closest && event.target.closest('[data-wr-items-more]') && window.WorkReportScreen) {
                window.WorkReportScreen.loadItemsPage(true);
            }
        });
    }

    // Exposed for the test harness — the real render path, not a copy of it.
    window.WorkReportScreen = {
        render: render, load: load, outcomeLabel: outcomeLabel, hasWork: hasWork,
        // Exposed for the harness — the real functions, not copies of them.
        groupLabel: groupLabel, query: query,
        setPeople: function (map) { people = map || {}; },
        // Dilim 1c — the real drill-down path, not a copy of it.
        openItems: openItems,
        loadItemsPage: loadItemsPage,
        itemsUrl: function (bucket, argument, groupKey, skip) { return itemsUrl(bucket, argument, groupKey, skip); },
        cellTitle: cellTitle,
        setLastQuery: function (q) { lastQuery = q; },
        setLastReport: function (r) { lastReport = r; }
    };
})();
