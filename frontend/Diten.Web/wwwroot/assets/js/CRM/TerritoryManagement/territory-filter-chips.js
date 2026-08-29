/**
 * Golden Reference Compact inline-filter chips, shared by the MOD-0151 grids that were added after the models and
 * hierarchy tables (assignment rules, assignment history).
 *
 * This is the exact behaviour the golden pages ship: a chip renders as "placeholder + count badge + clear ×" rather
 * than a raw select2 tag list, the filter button owns the collapse and keeps aria-expanded in sync (which is what
 * DtDefaults.updateVisualState reads to paint the active state), and the filter badge counts FIELDS that are
 * constrained — not how many values were picked inside them.
 */
(function () {
    'use strict';

    function normalizeArray(value) {
        var list = Array.isArray(value) ? value : (value ? [value] : []);
        return Array.from(new Set(list.map(function (x) { return String(x).trim(); }).filter(Boolean)));
    }

    /// Collapses a multi-select's select2 rendering into the compact chip the golden pages use.
    function syncSummary($select) {
        var $container = $select.next('.select2-container');
        var $rendered = $container.find('.select2-selection__rendered');
        var $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) { return; }

        var $summary = $selection.find('.dt-inline-filter-multi__summary');
        var $actions = $selection.find('.dt-inline-filter-multi__actions');
        var $count = $selection.find('.dt-inline-filter-multi__count');
        if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>').prependTo($selection); }
        if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>').appendTo($selection); }
        if (!$count.length) {
            $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>').appendTo($actions);
        }
        if (!$selection.find('.select2-selection__arrow').length) {
            $selection.append('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>');
        }

        var values = normalizeArray($select.val());
        var placeholder = String($select.data('placeholder') || '');
        var selectedTexts = ($select.select2('data') || []).map(function (item) { return String(item.text || ''); }).filter(Boolean);

        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', values.length > 0);
        $count.toggleClass('d-none', values.length === 0).text(String(values.length));

        $actions.find('.dt-multi-clear-btn').remove();
        if (values.length) {
            $('<span class="dt-multi-clear-btn" role="button" title="Reset">&times;</span>')
                .on('mousedown', function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    $select.val(null).trigger('change');
                })
                .appendTo($actions);
        }
    }

    /// Initialises the given filter selects with the golden select2 options and keeps their summary in sync.
    function initSelect2(selector) {
        if (!window.jQuery || !$.fn.select2) { return; }
        $(selector).each(function () {
            var $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) { $select.select2('destroy'); }
            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $select.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
            $select.off('change.select2-summary').on('change.select2-summary', function () { syncSummary($select); });
            requestAnimationFrame(function () { syncSummary($select); });
        });
    }

    /// Binds the toolbar filter button to the inline collapse. Scoped by a dataset guard so a redraw cannot bind
    /// twice, and it maintains aria-expanded because that is what the toolbar's active state is derived from.
    function bindToggle(boundKey) {
        var button = document.querySelector('.dt-filter-btn');
        var collapse = document.getElementById('inlineFilterCollapse');
        if (!button || !collapse || button.dataset[boundKey] === '1') { return; }
        button.dataset[boundKey] = '1';

        button.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false }).toggle();
        });
        collapse.addEventListener('shown.bs.collapse', function () { button.setAttribute('aria-expanded', 'true'); });
        collapse.addEventListener('hidden.bs.collapse', function () { button.setAttribute('aria-expanded', 'false'); });
    }

    /// The badge counts constrained FIELDS, not selected values — five cities in one chip is still one filter.
    function appliedFieldCount(filters) {
        return Object.keys(filters || {})
            .filter(function (key) { return normalizeArray(filters[key]).length > 0; })
            .length;
    }

    window.TerritoryFilterChips = {
        normalizeArray: normalizeArray,
        syncSummary: syncSummary,
        initSelect2: initSelect2,
        bindToggle: bindToggle,
        appliedFieldCount: appliedFieldCount
    };
})();
