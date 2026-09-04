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

    /**
     * ⚠ ONLY EVER FED BY `fact()`, WHICH ESCAPES EVERY CELL ITSELF. No caller hands this a server string
     * directly; the one that assembles the markup is the one that escapes it, so no render path can forget.
     */
    var setHtml = function (selector, html) {
        var el = $(selector);
        if (el) { el.innerHTML = html; }
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
    /*
     * ⚠ WHETHER THE READER MAY EVEN ASK FOR TENANT-WIDE IS LEARNED, NEVER GUESSED — Dilim 1f.
     *
     * There is no "may I widen the scope" endpoint, and there should not be one just to answer a display
     * question the report already answers every time it runs: a response that comes back `scopeApplied ===
     * 'tenant'` PROVES the caller can reach it, because the server only ever returns that when the permission
     * check upstream (`WorkReportScopeSource`) already passed. Once learned in a page session it is remembered,
     * so switching the chip to "your scope" and back does not make the OTHER chip disappear.
     */
    var canSeeTenantWide = false;
    var scopePreference = null;

    var renderScope = function (report) {
        var tenant = report.scopeApplied === 'tenant';
        if (tenant) { canSeeTenantWide = true; }

        /*
         * ⚠ TWO CHIPS ONLY WHEN BOTH ARE REAL CHOICES. A reader with no path to tenant-wide gets the SAME quiet
         * info badge this screen has always shown — a chip that cannot change anything when clicked reads as
         * broken, and a screen offering a choice that only ever has one real answer is worse than no choice at
         * all.
         */
        show('[data-wr-scope-chips]', canSeeTenantWide);
        show('[data-wr-scope-badge]', !canSeeTenantWide);

        if (canSeeTenantWide) {
            document.querySelectorAll('[data-wr-scope-chip]').forEach(function (chip) {
                var active = chip.getAttribute('data-wr-scope-chip') === (tenant ? 'tenant' : 'own');
                // `.wcn-seg.active` is the product's own segmented-control state (backbone-custom.css) — the
                // same one Görev Merkezi's status switch uses. Nothing here decides what "active" LOOKS like.
                chip.classList.toggle('active', active);
                chip.setAttribute('aria-pressed', active ? 'true' : 'false');
            });
        } else {
            var badge = $('[data-wr-scope-badge]');
            if (badge) {
                badge.textContent = tenant ? t('ScopeTenant') : t('ScopeScoped');
                badge.className = 'badge ' + (tenant ? 'bg-label-primary' : 'bg-label-info');
            }
        }

        // The description sentence changes with the APPLIED scope, exactly as it did before this slice —
        // never with what was merely requested. See WorkReportScopeSource for why those can differ.
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
            el.className = 'wr-trend wr-trend--flat text-muted';
            return;
        }

        var up = delta > 0;
        // "Better" is not "bigger". More work closed is good; a longer cycle time is not — so each caller says
        // which direction it wants rather than the helper guessing from the number.
        var good = lowerIsBetter ? !up : up;
        el.textContent = tf(up ? 'TrendUp' : 'TrendDown', Math.abs(delta), previous);
        /*
         * ⚠ THE DIRECTION ARROW IS DRAWN BY CSS, NOT WRITTEN INTO THE TEXT — `.wr-trend--up/--down::before` in
         * backbone-custom.css. A character put in `textContent` would join the sentence, and the sentence is
         * what seven translations and this module's own tests read back. A pseudo-element is invisible to both.
         *
         * `text-success` / `text-danger` STAY: the pill tints itself from `currentColor`, so the meaning still
         * lives in the one class a reader of this line can check, and nothing new decides good from bad.
         */
        el.className = 'wr-trend ' + (up ? 'wr-trend--up' : 'wr-trend--down') + ' '
            + (good ? 'text-success' : 'text-danger');
    };

    var duration = function (value) {
        return (value === null || value === undefined) ? t('NotMeasured') : tf('CycleTimeDays', value);
    };

    /**
     * ONE FACT ROW — a label on the left, its value on the right, in every card.
     *
     * ⚠ THE SHAPE IS WHAT MAKES FOUR CARDS READ AS ONE ROW. These used to be sentences of different lengths,
     * so a reader comparing cards was scanning prose; as a two-column list every value lands on the same
     * right-hand edge, and the edge is shared ACROSS the four cards because they all use this. That is the
     * whole point of the layout, and it is why nothing here takes a free-form string.
     *
     * `click` is optional and is the SAME bucket vocabulary every other number on this screen uses — never a
     * new one invented for a row.
     */
    var fact = function (label, value, click) {
        var cell = click
            ? '<span class="wr-clickable" role="button" tabindex="0" data-wr-click="' + esc(click) + '">' + esc(value) + '</span>'
            : esc(value);
        return '<dt>' + esc(label) + '</dt><dd>' + cell + '</dd>';
    };

    /** The answer line: the bare number, with its unit as a separate quiet word beside it. */
    var setAnswer = function (selector, value) { setText(selector, value === null || value === undefined ? '' : String(value)); };

    /**
     * THE SKELETON IS ON, OR THE REPORT IS. Never both, and never neither.
     *
     * ⚠ THIS SCREEN HAD THREE STATES THAT LOOKED THE SAME: loading, an empty period, and a failed call all
     * rendered as a blank page. Every branch that ends a load — a drawn report, an empty one, an invalid
     * period, a failed fetch — turns this off, so the shimmer can only ever mean "still on its way".
     */
    var showSkeleton = function (loading) {
        show('[data-wr-skeleton-tiles]', loading);
        show('[data-wr-skeleton-charts]', loading);

        /*
         * ⚠ TURNING THE SKELETON ON TURNS THE REPORT OFF, AND MISSING THIS WAS A REAL DEFECT.
         *
         * The FIRST load is harmless — the report regions start hidden in the markup, so the skeleton simply
         * fills the space. Every load AFTER that is not: pressing Apply with a report already on screen used
         * to insert the skeleton ABOVE the still-visible numbers, growing the page by its whole height and
         * producing exactly the jump a skeleton exists to prevent. The two are one switch, not two.
         */
        if (loading) {
            show('[data-wr-tiles]', false);
            show('[data-wr-charts]', false);
            show('[data-wr-summary]', false);
        }
    };

    /** One day, in milliseconds — named because the period's length is computed from it below. */
    var MS_PER_DAY = 24 * 60 * 60 * 1000;

    /**
     * THE PERIOD IN ONE COLUMN — the same figures the tiles and the flow bars publish, gathered beside the
     * outcomes chart.
     *
     * ⚠ EVERY FIGURE HERE IS A REPEAT, AND EACH ONE OPENS THE SAME BUCKET ITS ORIGINAL OPENS. That is the
     * whole discipline of putting a summary on a page that already carries these numbers: two places may
     * differ in presentation, never in which rows they mean. The buckets are the static `data-wr-click`
     * attributes in the view — this function never names one.
     *
     * ⚠ AND THE ONLY ARITHMETIC IS THE DAY COUNT. No rate, no share, no division of one published figure by
     * another; see the view's own comment for why "closed ÷ opened" is not a completion rate and is left out.
     */
    var renderSummary = function (report, totals) {
        var flow = totals.flow || {};

        /*
         * ⚠ `to` IS EXCLUSIVE — the first day NOT counted, which is what the picker's value means here and what
         * `load()` sends. A plain difference is therefore already the number of days IN the period; adding one
         * "to include both ends" would report 31 days for a month and quietly disagree with every count beside
         * it.
         */
        var from = report.from ? new Date(report.from) : null;
        var to = report.to ? new Date(report.to) : null;
        var days = (from && to) ? Math.round((to - from) / MS_PER_DAY) : null;
        setText('[data-wr-summary-days]', days === null ? '' : tf('SummaryDays', days));

        setText('[data-wr-summary-opened]', flow.opened || 0);
        setText('[data-wr-summary-closed]', flow.closed || 0);
        setText('[data-wr-summary-completed]', flow.completed || 0);
        setText('[data-wr-summary-cancelled]', flow.cancelled || 0);
        setText('[data-wr-summary-unattended]', flow.unattended || 0);

        // The scope the numbers were computed under, from the RESPONSE — never from the chip a reader clicked.
        var scopeWord = report.scopeApplied === 'tenant' ? t('ScopeTenant') : t('ScopeScoped');
        setText('[data-wr-summary-note]', tf('SummaryScopeNote', dateRange(), scopeWord));

        show('[data-wr-summary]', true);
    };

    var renderTiles = function (totals, previous) {
        var cycle = totals.cycleTime || {};
        var cancel = totals.cancellationTime || {};

        /*
         * ⚠ ABSENT, NOT ZERO. The API sends null when nothing closed, because a zero reads as "everything
         * closed instantly" — the most flattering lie a report can tell. With nothing measured there is no
         * unit either: "Not measured days" is not a sentence anybody means.
         */
        var measured = cycle.averageDays !== null && cycle.averageDays !== undefined;
        setAnswer('[data-wr-cycle-value]', measured ? cycle.averageDays : t('NotMeasured'));
        setText('[data-wr-cycle-unit]', measured ? t('UnitDays') : '');
        // ⚠ THE DENOMINATOR THE AVERAGE WAS ACTUALLY COMPUTED OVER — see WorkReportDuration.Count.
        setText('[data-wr-cycle-over]', tf('CycleTimeOver', cycle.count || 0));

        /*
         * ⚠ CANCELLATIONS READ SEPARATELY, and now as their own rows. Averaged into the line above they turned
         * "how long our work takes" into "how long before we gave up" — the defect Dilim 1b repairs. Their rows
         * are absent rather than zeroed when nothing was cancelled: "we abandoned work after 0 days" is a
         * sentence nobody means either.
         */
        var cycleFacts = fact(t('LabelMedian'), duration(cycle.medianDays));
        if (cancel.count > 0) {
            cycleFacts += fact(t('LabelUntilCancelled'), duration(cancel.averageDays));
            // The count opens the cancelled work itself — the same bucket the flow chart's own bar opens.
            cycleFacts += fact(t('LabelCancelled'), cancel.count, 'Cancelled');
        }
        setHtml('[data-wr-cycle-facts]', cycleFacts);
        trend('[data-wr-cycle-trend]', cycle.averageDays, (previous && (previous.cycleTime || {}).averageDays), true);

        var rework = totals.rework || {};
        setAnswer('[data-wr-rework-tasks]', rework.tasksReturned || 0);
        setHtml('[data-wr-rework-facts]', fact(t('LabelTotalReturns'), rework.totalReturns || 0));
        setText('[data-wr-rework-trend]', '');
        trend('[data-wr-rework-trend]', rework.totalReturns, (previous && (previous.rework || {}).totalReturns), true);

        setAnswer('[data-wr-unattended-value]', (totals.flow || {}).unattended || 0);

        /*
         * AGEING — measured at the PERIOD'S END, which is what makes the report evidence: the same period says
         * the same thing when it is reopened in a review months later. Hidden when nothing was open.
         *
         * ⚠ THESE WERE ONE LOCALIZED SENTENCE UNTIL NOW, and the split was a deliberate trade, not a tidy-up.
         * The sentence could not be compared band to band — three numbers inside prose, at three different
         * horizontal positions. As rows they line up with every other value in the row of cards. The cost is
         * real and was paid: three band names became their own keys in all seven languages, so no translation
         * has to keep a clause order that no longer exists.
         */
        var aging = totals.aging || {};
        var bands = [
            [t('AgingUpTo7Label'), aging.upTo7Days || 0, 'AgingUpTo7Days'],
            [t('AgingFrom8To30Label'), aging.from8To30Days || 0, 'AgingFrom8To30Days'],
            [t('AgingOlderThan30Label'), aging.olderThan30Days || 0, 'AgingOlderThan30Days']
        ];
        var agingTotal = bands.reduce(function (sum, b) { return sum + b[1]; }, 0);
        var agingEl = $('[data-wr-aging]');
        if (agingEl) {
            agingEl.hidden = agingTotal === 0;
            agingEl.innerHTML = bands.map(function (b) { return fact(b[0], b[1], b[2]); }).join('');
        }

        var effort = totals.effort || {};
        /*
         * ⚠ TWO NUMBERS, ONE ABOVE THE OTHER — NEVER DIVIDED. Spent leads because it is what happened;
         * estimated follows because it is what was planned. Computing a percentage here would put back exactly
         * what `There_is_no_efficiency_percentage_anywhere_in_the_contract` keeps out of the contract, one
         * layer further from anyone who would notice.
         */
        setAnswer('[data-wr-effort-value]', effort.spentHours || 0);
        setText('[data-wr-effort-over]', tf('EffortOver', effort.taskCount || 0));
        setHtml('[data-wr-effort-facts]', fact(t('LabelEstimated'), tf('Hours', effort.estimatedHours || 0)));

        show('[data-wr-tiles]', true);
    };

    /**
     * ARE WE KEEPING UP WITH WHAT ARRIVES? — opened against closed, with the closure split beside it.
     *
     * ⚠ A COMPARISON, NOT A TIME SERIES, and that is a limit of the endpoint rather than a design choice: 5a
     * returns ONE period's totals and does no sub-period bucketing, so there is no series to plot. A line drawn
     * from four totals would be a picture of nothing. Bucketing belongs to the query if it is ever wanted.
     */
    /*
     * ── THE CHART VOCABULARY, IN ONE PLACE ───────────────────────────────────────────────────────────────
     *
     * ⚠ COLOURS ARE MEANING HERE, NOT DECORATION, so they are named rather than left to the library's palette:
     * closed and on-time are green, cancelled and late are red, undated is grey — the same reading every other
     * screen in this product gives those words. apex takes colours as JS values and has no way to read a CSS
     * variable, which is why these are literals here and NOT a style attribute anywhere — FG-003 is about
     * styling DOM elements, and no element is styled by this.
     */
    var FLOW_COLORS = ['#666cff', '#28c76f', '#00cfe8', '#ea5455'];
    var TIMELINESS_COLORS = ['#28c76f', '#ea5455', '#a8aaae'];

    /*
     * ⚠ THE LEGEND DOT IS COLOURED BY A CLASS, NOT BY THIS ARRAY — FG-003 forbids this file writing
     * `element.style`, so the swatch colour lives in backbone-custom.css. That leaves the SAME three colours
     * described in two places, and a ring whose legend disagreed with it would be worse than no legend at
     * all: `The_ring_and_its_legend_cannot_disagree_about_a_colour` reads both and fails if they drift.
     */
    var LEGEND_DOT = { OnTime: 'ontime', Late: 'late', WithoutDueDate: 'undated' };

    /**
     * A legend entry that carries its own figure — "Closed 16", not "Closed".
     *
     * ⚠ IT READS A NUMBER THE SERIES ALREADY HOLDS; it does not compute one. No share, no percentage, no
     * division — see `There_is_no_efficiency_percentage_anywhere_in_the_contract`, which this file has kept
     * true through every slice. A donut's series is flat and a bar's is nested one level; both shapes are
     * handled here so the two charts can share the one formatter.
     */
    var legendWithCount = function (name, opts) {
        var series = opts && opts.w && opts.w.globals ? opts.w.globals.series : null;
        if (!series) { return name; }
        var value = Array.isArray(series[0]) ? series[0][opts.seriesIndex] : series[opts.seriesIndex];
        return value === undefined ? name : name + '  ' + value;
    };

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
            /*
             * ⚠ THE FIGURE MOVED, IT DID NOT DISAPPEAR. A label printed inside a bar is unreadable on a short
             * one and invisible on a zero — the two bars a reader most needs to read. The legend below carries
             * every category's count at a fixed size, so a zero reads as clearly as an eighty.
             */
            dataLabels: { enabled: false },
            colors: FLOW_COLORS,
            plotOptions: { bar: { distributed: true, borderRadius: 4, columnWidth: '45%', cursor: 'pointer' } },
            legend: { show: true, position: 'bottom', horizontalAlign: 'left', formatter: legendWithCount }
        });
    };

    /** WHAT DID THE CLOSURES DECIDE? — the outcome histogram (Faz 3's ClosureReasonCode). */
    /**
     * WHAT DID THE CLOSURES DECIDE? — a sorted horizontal bar, capped to the busiest eight.
     *
     * ⚠ A WHEEL DOES NOT SURVIVE A LONG TAIL, AND THIS AXIS HAS ONE — Dilim 1d. A task type's closure
     * dictionary is not bounded to a handful of entries the way flow or timeliness are; a donut with twenty
     * slices is an unreadable ring of slivers and a legend nobody can match back to a colour. The SAME shape
     * `renderGroups` already draws for the breakdown axis — sorted, horizontal, a named "other" bucket for the
     * rest — answers this one too, and answers it the way the server's OWN cap
     * (`WorkReportDto.MaxGroups`/`OtherKey`) already treats a long axis: show the busiest, fold the rest, say
     * so. `rows` arrives from the API already sorted busiest-first (`WorkReportTally.Measure`), so the fold
     * below is a straight `slice`, not a re-sort.
     *
     * ⚠ NO CHART-TYPE PICKER. The shape follows what the AXIS is, not a preference a reader could set to
     * something that misreads the data — offering one invites turning a comparison into a decoration.
     */
    var OUTCOMES_SHOWN = 8;

    var renderOutcomes = function (outcomes) {
        var rows = outcomes || [];
        // An empty chart is a blank card that says nothing. A sentence says the thing.
        show('[data-wr-outcomes-empty]', rows.length === 0);

        if (rows.length === 0) {
            if (charts.outcomes) { charts.outcomes.destroy(); charts.outcomes = null; }
            var host = $('[data-wr-chart-outcomes]');
            if (host) { host.innerHTML = ''; }
            return;
        }

        var shown = rows.slice(0, OUTCOMES_SHOWN);
        var folded = rows.slice(OUTCOMES_SHOWN);

        /*
         * ⚠ THE FOLDED SUM IS ADDITION, NOT A NEW MEASURE. Every number in it is one the API already published
         * for that row; this only totals rows that no longer fit on screen, the same way the aging tile already
         * sums three published buckets to decide whether to show itself at all. No ratio, no derived figure.
         */
        var otherTotal = folded.reduce(function (sum, row) { return sum + (row.count || 0); }, 0);

        var categories = shown.map(function (row) { return outcomeLabel(row.code); });
        var data = shown.map(function (row) { return row.count || 0; });
        if (otherTotal > 0) {
            categories.push(t('OutcomesOther'));
            data.push(otherTotal);
        }

        draw('outcomes', '[data-wr-chart-outcomes]', {
            chart: {
                type: 'bar', height: Math.max(220, categories.length * 44), toolbar: { show: false },
                events: {
                    dataPointSelection: function (_e, _ctx, cfg) {
                        /*
                         * ⚠ THE FOLDED "OTHER" BAR OPENS NOTHING — Dilim 1c's own bucket kinds have no "the
                         * rest of the outcomes" cell to ask for, and inventing a fetch here would be exactly the
                         * chart-local query Dilim 1c's own guard exists to forbid. A click past the shown rows
                         * is a no-op rather than a request for a bucket that does not exist.
                         */
                        if (cfg.dataPointIndex >= shown.length) { return; }
                        // The ARGUMENT is the CODE `rows` carries — never the translated label the axis shows,
                        // because the code is the identity the server's `Outcome` cell matches on.
                        openItems('Outcome', shown[cfg.dataPointIndex].code);
                    }
                }
            },
            series: [{ name: t('OutcomesTitle'), data: data }],
            xaxis: { categories: categories },
            dataLabels: { enabled: true },
            plotOptions: { bar: { horizontal: true, borderRadius: 4, distributed: true, cursor: 'pointer' } },
            legend: { show: false }
        });
    };

    /**
     * THE RING, READ AS THREE ALIGNED LINES — name, count, share.
     *
     * ⚠ THE SHARE IS THE RING'S OWN GEOMETRY WRITTEN DOWN, NOT A NEW MEASURE — a donut already draws these
     * proportions; this only says out loud what a reader would otherwise estimate by eye from an arc. It is a
     * COMPOSITION of one axis (these three bands are the whole of it, and they add to 100), which is a
     * different thing from the estimate-versus-actual ratio pack §8 keeps off this screen: that one divides
     * two INDEPENDENT figures and lands on a person. Nothing here touches the effort card, and
     * `The_effort_card_never_publishes_a_share` now guards that separately and by name.
     *
     * ⚠ ZERO TOTAL DRAWS NO SHARES. `0/0` is not "0%", it is "there was nothing to be a share of" — printing
     * three tidy 0% lines under an empty ring would read as a measured result.
     */
    var renderTimelinessLegend = function (kinds, counts) {
        var host = $('[data-wr-timeliness-legend]');
        if (!host) { return; }

        var total = counts.reduce(function (sum, n) { return sum + n; }, 0);

        host.innerHTML = kinds.map(function (kind, i) {
            /*
             * ⚠ ROUNDED FOR READING, AND THE ROUNDING IS ALLOWED TO NOT SUM TO 100. Forcing the last row to
             * absorb the remainder would print a share that does not match its own count — the row would be
             * arithmetically tidy and individually wrong. Three honest roundings beat one adjusted lie.
             */
            var share = total > 0 ? Math.round((counts[i] / total) * 100) : null;

            return '<li data-wr-click="' + esc(kind) + '" role="button" tabindex="0">'
                + '<span class="wr-legend-dot wr-legend-dot--' + esc(LEGEND_DOT[kind]) + '"></span>'
                + '<span class="wr-legend-label">' + esc(t(kind)) + '</span>'
                + '<span class="wr-legend-count">' + esc(counts[i]) + '</span>'
                + '<span class="wr-legend-share">' + (share === null ? '' : esc(tf('SharePercent', share))) + '</span>'
                + '</li>';
        }).join('');
    };

    /** DID THE WORK LAND ON TIME? — and undated work is its own bar, never folded into "on time". */
    var renderTimeliness = function (timeliness) {
        // One series per band; `seriesIndex` is which band was clicked, not which category — there is only one.
        var kinds = ['OnTime', 'Late', 'WithoutDueDate'];

        /*
         * ⚠ A RING, AND THE CLICK NOW READS `dataPointIndex` — NOT `seriesIndex`. This was a stacked bar, where
         * each band was its own SERIES; a donut makes the three bands three POINTS of one series, and apex
         * reports those in a different field. Reading the old field here would not throw and would not log: it
         * would quietly open "on time" for every slice a reader clicked. `kinds` is unchanged and still the one
         * array both the chart and the handler read, so the mapping stays checkable in one place — and
         * `EVERY slice of the timeliness ring opens its OWN band` measures all three.
         */
        draw('timeliness', '[data-wr-chart-timeliness]', {
            chart: {
                type: 'donut', height: 260, toolbar: { show: false },
                events: { dataPointSelection: function (_e, _ctx, cfg) { openItems(kinds[cfg.dataPointIndex]); } }
            },
            series: [timeliness.onTime || 0, timeliness.late || 0, timeliness.withoutDueDate || 0],
            labels: [t('OnTime'), t('Late'), t('WithoutDueDate')],
            colors: TIMELINESS_COLORS,
            dataLabels: { enabled: false },
            // The library's own legend is off: see the view for why this one is built by hand.
            legend: { show: false }
        });

        renderTimelinessLegend(kinds, [
            timeliness.onTime || 0, timeliness.late || 0, timeliness.withoutDueDate || 0
        ]);
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
            showSkeleton(false);
            show('[data-wr-tiles]', false);
            show('[data-wr-charts]', false);
            show('[data-wr-summary]', false);
            // The trend and ageing lines live inside hidden cards, but they are hidden in their own right too:
            // a stale arrow surviving into an empty period would be a number about nothing.
            ['[data-wr-cycle-trend]', '[data-wr-rework-trend]',
             '[data-wr-aging]', '[data-wr-flow-trend]', '[data-wr-late-trend]']
                .forEach(function (sel) { show(sel, false); });
            /*
             * ⚠ AND THE FACT TABLES ARE EMPTIED, NOT JUST HIDDEN. The rows are BUILT from the last report that
             * had work in it; left in place they are a previous period's cancellations sitting inside a card
             * the next reader may well see again. Hiding the card is not the same as forgetting the numbers.
             */
            ['[data-wr-cycle-facts]', '[data-wr-rework-facts]', '[data-wr-effort-facts]', '[data-wr-aging]']
                .forEach(function (sel) { setHtml(sel, ''); });
            setText('[data-wr-status]',
                report.scopeApplied === 'tenant' ? t('NoData') : t('NoDataScoped'));
            closeItems();
            return;
        }

        setText('[data-wr-status]', '');

        // The previous period's TOTALS, when one was asked for. Null — not a bucket of zeroes — when it was
        // not, so the screen draws no arrow rather than a misleading downward one.
        var previous = report.previous && report.previous.totals;

        showSkeleton(false);
        renderTiles(totals, previous);
        renderSummary(report, totals);
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
            priority: valueOf('#wrPriority'),
            /*
             * ⚠ A DISPLAY PREFERENCE, READ FROM JS STATE — NOT A PICKER. There is no form control for this; the
             * reader sets it by clicking a scope chip (see the click wiring below), and `scopePreference` is
             * the only place that choice lives between one Apply and the next. Null means "no preference sent",
             * which is what makes an old bookmark or an old test — anything from before this slice — behave
             * exactly as it always did.
             */
            scopePreference: scopePreference
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
            showSkeleton(false);
            show('[data-wr-tiles]', false);
            show('[data-wr-charts]', false);
            show('[data-wr-summary]', false);
            setText('[data-wr-status]', t('PeriodInvalid'));
            return Promise.resolve();
        }

        /*
         * ⚠ NO "LOADING" SENTENCE — THE SKELETON IS THE SENTENCE. A word saying "loading" beside a page full
         * of shimmering blocks is the same statement twice, in two languages, one of which has to be
         * translated seven times. The status line is cleared so a PREVIOUS load's message cannot sit under a
         * skeleton and describe a report that is no longer on screen.
         */
        setText('[data-wr-status]', '');
        showSkeleton(true);

        // Dates go as whole days at UTC midnight — `to` is EXCLUSIVE, which is what the picker's value means
        // here: the first day NOT counted.
        var url = ENDPOINT
            + '?from=' + encodeURIComponent(q.from + 'T00:00:00Z')
            + '&to=' + encodeURIComponent(q.to + 'T00:00:00Z')
            + '&groupBy=' + encodeURIComponent(q.groupBy)
            // The SERVER decides which days "previous" means. Asking for it is all the screen does.
            + '&comparePrevious=true';

        /*
         * ⚠ SENT ONLY WHEN THE READER ACTUALLY CHOSE ONE — an absent parameter is what keeps every caller from
         * before Dilim 1f getting exactly the behaviour they always got. And it is a PREFERENCE, not a
         * permission: the server narrows an over-reaching request rather than rejecting it, so sending "tenant"
         * from a scope chip nobody granted access to is never a security event on this side either.
         */
        if (q.scopePreference) { url += '&scope=' + encodeURIComponent(q.scopePreference); }

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
                showSkeleton(false);
                show('[data-wr-tiles]', false);
                show('[data-wr-charts]', false);
                // The third state, and the only one that says WHY nothing is here.
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

        /*
         * ⚠ THE PICKER STARTS DISABLED IN THE MARKUP AND IS RELEASED HERE — a filter offering an empty list is
         * indistinguishable from a filter offering "no companies exist", and a reader who opens it before the
         * lookup lands draws the second conclusion. Enabling it exactly where its options arrive is what makes
         * the two states tell themselves apart, and needs no state flag of its own to stay in step.
         */
        el.disabled = false;

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

        /*
         * ⚠ select2 DOES NOT NOTICE `<option>` ELEMENTS CHANGING UNDER IT — it renders from a snapshot taken
         * when it was initialized, and `innerHTML = ''` above just emptied that snapshot's DOM without telling
         * select2 anything happened. `.trigger('change')` is select2's own documented way to say "re-read your
         * options"; without it, the company/unit/type/assignee pickers would stay showing "Any" forever, no
         * matter how many rows `loadFilterOptions` fetched.
         */
        if (window.jQuery && window.jQuery(el).hasClass('select2-hidden-accessible')) {
            window.jQuery(el).trigger('change');
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

    /*
     * ── DILIM 1d — THE PROJECT'S OWN PICKERS, NOT NATIVE ONES ───────────────────────────────────────────────
     *
     * flatpickr on the two dates, select2 on the breakdown and the five filters — the SAME two libraries
     * the organisation and device-enablement forms already use, loaded once in
     * the tenant shell rather than pulled in specially for this screen.
     */
    var initDatePickers = function () {
        if (!window.flatpickr) { return; }
        ['#wrFrom', '#wrTo'].forEach(function (selector) {
            var el = $(selector);
            if (el) { el.flatpickr({ monthSelectorType: 'static', dateFormat: 'Y-m-d' }); }
        });
    };

    var initSelect2 = function () {
        if (!window.jQuery || !window.jQuery.fn.select2) { return; }
        var $ = window.jQuery;
        var $body = $(document.body);

        $('#wrGroupBy, #wrLegalEntity, #wrUnit, #wrTaskType, #wrAssignee, #wrPriority').each(function () {
            var $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) { $s.select2('destroy'); }
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                // Sizing follows the MARKUP, not this file: a chip marked `form-select-sm` in the view keeps
                // that size once select2 replaces it — the same shape GoldenReferenceCompact's filter bar uses.
                selectionCssClass: $s.hasClass('form-select-sm') ? 'form-select form-select-sm' : 'form-select',
                placeholder: $s.data('placeholder') || '',
                width: 'element',
                allowClear: $s.is('[data-wr-filter]'),
                // The breakdown has six fixed options — a search box for six items is a control looking for a
                // reason to exist. The five filters can carry a whole tenant's units or people, so they search.
                minimumResultsForSearch: $s.attr('data-minimum-results-for-search') === 'Infinity' ? Infinity : 0
            });
        });

        /*
         * ⚠ THE NATIVE-CHANGE BRIDGE — copied in spirit from `Tasks/form.js`'s own comment on the same defect.
         *
         * select2 announces a choice by calling jQuery's `.trigger('change')`, which does NOT run listeners
         * added with `addEventListener`. Every listener already on this page — `refreshUnits` on the company
         * picker, the filter-count badge below — was written against the native event, and would go silently
         * deaf the moment select2 wrapped the element. `event.originalEvent` is jQuery's own mark of a REAL DOM
         * event it is merely relaying; its absence is what tells this bridge the event was jQuery-synthesised
         * and needs re-dispatching as a native one. Checking that, rather than a re-entrancy flag, is what stops
         * the bridge from echoing its own dispatch back into an infinite loop.
         */
        $('#wrGroupBy, #wrLegalEntity, #wrUnit, #wrTaskType, #wrAssignee, #wrPriority').on('change', function (event) {
            if (event && event.originalEvent) { return; }
            this.dispatchEvent(new Event('change', { bubbles: true }));
        });
    };

    /*
     * ⚠ THE ACTIVE-FILTER COUNT IS A CORRECTNESS SIGNAL, NOT DECORATION — see the view's own comment on the
     * badge. Counted from the five `[data-wr-filter]` selects' CURRENT values, whatever the panel's own
     * open/closed state — a reader who closes the panel after choosing three filters must still see "3".
     */
    var updateFilterCount = function () {
        var badge = $('[data-wr-filter-count]');
        if (!badge) { return; }

        var count = Array.prototype.slice.call(document.querySelectorAll('[data-wr-filter]'))
            .filter(function (el) { return !!el.value; })
            .length;

        badge.hidden = count === 0;
        badge.textContent = String(count);
    };

    document.addEventListener('DOMContentLoaded', function () {
        var form = $('#workReportFilter');
        if (!form) { return; }

        initDatePickers();
        initSelect2();

        var company = $('#wrLegalEntity');
        if (company) { company.addEventListener('change', refreshUnits); }

        document.querySelectorAll('[data-wr-filter]').forEach(function (el) {
            el.addEventListener('change', updateFilterCount);
        });
        updateFilterCount();

        /*
         * ⚠ CLICKING A CHIP SETS THE PREFERENCE AND RE-LOADS — it does not toggle its own visual state directly.
         * `renderScope` is the ONLY place a chip's pressed/unpressed styling is decided, from the response's
         * OWN `scopeApplied`, so the chip a reader sees active always matches the scope the numbers on screen
         * were actually computed under — never a click's optimistic guess about what the server will say back.
         */
        document.querySelectorAll('[data-wr-scope-chip]').forEach(function (chip) {
            chip.addEventListener('click', function () {
                scopePreference = chip.getAttribute('data-wr-scope-chip');
                load();
            });
        });

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

        /*
         * ⚠ THE SAME SCOPE PREFERENCE THE LOADED REPORT USED — Dilim 1f's own 1c-shaped identity rule. A tile
         * counted under "your scope" has to open a list counted under that SAME scope; reading the picker's
         * CURRENT chip instead of `lastQuery`'s would let a click widen past what its own tile just reported,
         * the exact defect `lastQuery` was captured to prevent for the five ordinary filters.
         */
        if (lastQuery.scopePreference) { url += '&scope=' + encodeURIComponent(lastQuery.scopePreference); }

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

    /*
     * ── ONE ROW'S VOCABULARY ─────────────────────────────────────────────────────────────────────────────
     *
     * Three lifecycles, three ways of saying the same thing: a word, a badge tint, and the colour of the bar
     * down the row's left edge. They are declared TOGETHER, in one table, because a row whose bar said one
     * thing while its badge said another would be worse than a row with neither — a reader trusts a colour
     * faster than they read a word, and would be trusting the wrong one.
     *
     * ⚠ THE BAR IS NOT THE ONLY CARRIER. Colour alone excludes anyone who cannot separate the two hues, so
     * the badge always spells the state out; the bar is a scanning aid on top of a label, never instead of it.
     */
    var LIFECYCLE = {
        Done: { key: 'Completed', badge: 'bg-label-success', accent: 'wcn-row-accent-success' },
        Cancelled: { key: 'Cancelled', badge: 'bg-label-danger', accent: 'wcn-row-accent-danger' }
    };

    /*
     * Every non-terminal state (Open, Planned, InProgress, Waiting, PendingReview) reads as one word here —
     * which of those it is in detail is the DETAIL PAGE's question, not this list's.
     */
    var OPEN_LIFECYCLE = { key: 'ItemsStatusOpen', badge: 'bg-label-secondary', accent: 'wcn-row-accent-secondary' };

    var lifecycleOf = function (lifecycle) { return LIFECYCLE[lifecycle] || OPEN_LIFECYCLE; };

    var lifecycleWord = function (lifecycle) { return t(lifecycleOf(lifecycle).key); };

    /**
     * THE MONOGRAM BESIDE A PERSON — the first two characters of whatever the person resolved to.
     *
     * ⚠ IT IS A SHAPE, NOT AN IDENTITY, and the full value stays on screen beside it. When `people` cannot
     * resolve a name the row falls back to the raw account (an email today), and abbreviating THAT to two
     * letters and nothing else would leave a reader looking at "AD" with no way to tell who it was.
     */
    var monogram = function (name) {
        return (name || '').trim().slice(0, 2).toUpperCase();
    };

    /**
     * ONE ROW — the whole row IS the link to the task's detail page (`/WorkCenterNext/Details/{id}`, the route
     * every other surface in this product uses), rather than a separate button, so keyboard and screen-reader
     * users get one obvious target instead of a row that half-works with either.
     *
     * ⚠ THE ROW IS `.wcn-row`, THE PRODUCT'S OWN WORK-ITEM ROW — the same card Görev Merkezi lists work items
     * in, with its padding, radius, hover tint, focus ring and skin-aware border/shadow already decided. This
     * panel lists work items too; borrowing the row means a reader recognises the object, and means this
     * screen owns no hover colour it would then have to keep in step with a component it does not own.
     *
     * ⚠ AND THE FACTS ARE STACKED, NOT STRUNG TOGETHER. They used to be one line of dot-separated clauses —
     * status, person, two dates — which wrapped at whatever point the width happened to fall, so no two rows
     * were the same height and nothing lined up down the list. Title and state on the first line, person and
     * dates on the second, is what makes a column of rows scannable.
     */
    var itemRowHtml = function (item) {
        var due = item.dueAt ? new Date(item.dueAt).toLocaleDateString() : '\u2014';
        var closed = item.closedAt ? new Date(item.closedAt).toLocaleDateString() : '\u2014';
        var assignee = item.assigneeUserId
            ? (people[item.assigneeUserId] || item.assigneeUserId)
            : t('ItemsUnassigned');
        var state = lifecycleOf(item.lifecycle);

        return '<a class="wcn-row wr-item-row" href="/WorkCenterNext/Details/' + esc(item.id) + '">'
            + '<span class="wcn-row-accent ' + esc(state.accent) + '"></span>'
            + '<span class="wr-item-main">'
            + '<span class="wr-item-head">'
            + '<span class="wr-item-title">' + esc(item.title || item.id) + '</span>'
            + '<span class="badge ' + esc(state.badge) + '">' + esc(t(state.key)) + '</span>'
            + '</span>'
            + '<span class="wr-item-meta">'
            + '<span class="wr-item-who"><span class="wr-item-avatar">' + esc(monogram(assignee)) + '</span>'
            + esc(assignee) + '</span>'
            + '<span class="wr-item-dates">' + esc(tf('ItemsColDue', due))
            + ' \u00b7 ' + esc(tf('ItemsColClosed', closed)) + '</span>'
            + '</span></span></a>';
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
        // Dilim 1d — layout wiring, exposed so the badge and the pickers can be exercised without a
        // real DOMContentLoaded firing (the test harness's jsdom document is already 'complete').
        updateFilterCount: updateFilterCount,
        initDatePickers: initDatePickers,
        initSelect2: initSelect2,
        // Dilim 1c — the real drill-down path, not a copy of it.
        openItems: openItems,
        loadItemsPage: loadItemsPage,
        itemsUrl: function (bucket, argument, groupKey, skip) { return itemsUrl(bucket, argument, groupKey, skip); },
        cellTitle: cellTitle,
        setLastQuery: function (q) { lastQuery = q; },
        setLastReport: function (r) { lastReport = r; },
        // Dilim 1f — the real scope-preference state, not a copy of it. `render(...)` is what the test harness
        // calls to exercise `renderScope`'s chip/badge toggling, exactly as 1a/1b/1c/1d already do for the rest
        // of the screen; these two hooks let a test set or read the state a real chip click sets and reads.
        showSkeleton: showSkeleton,
        setScopePreference: function (value) { scopePreference = value; },
        getScopePreference: function () { return scopePreference; }
    };
})();
