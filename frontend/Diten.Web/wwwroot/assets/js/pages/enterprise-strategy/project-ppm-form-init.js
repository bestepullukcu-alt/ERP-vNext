'use strict';
(function (window, document, $) {
  if (!$) return;

  function mirrorSelect2Invalid($select) {
    var update = function () {
      var invalid = $select.hasClass("is-invalid") || $select.attr("aria-invalid") === "true";
      $select.next(".select2").find(".select2-selection").toggleClass("is-invalid", !!invalid);
    };
    update();
    var el = $select.get(0);
    if (!el) return;
    new MutationObserver(update).observe(el, { attributes: true, attributeFilter: ["class", "aria-invalid"] });
    $select.on("change select2:select select2:unselect", update);
  }

  function initSelect2() {
    var $modal = $("#projectPpmModal");
    var $selects = $modal.find("select.select2");
    if (!$selects.length) return;
    $selects.each(function () {
      var $this = $(this);
      var el = this;
      if (!$this.parent().hasClass("position-relative")) $this.wrap('<div class="position-relative"></div>');
      if ($this.hasClass("select2-hidden-accessible")) {
        try { $this.select2("destroy"); } catch (_) {}
      }
      var isParentInit = $this.attr("id") === "ppm-parent-initiative";
      var cfg = {
        width: "100%",
        placeholder: $this.data("placeholder") || "Select...",
        allowClear: !$this.prop("multiple"),
        closeOnSelect: !$this.prop("multiple"),
        dropdownParent: $this.parent(),
        minimumInputLength: isParentInit ? 1 : 0
      };
      if (isParentInit && window.initiativeStrategyApi && window.initiativeStrategyApi.list) {
        cfg.ajax = {
          delay: 200,
          transport: function (params, success, failure) {
            var term = String((params && params.data && params.data.term) || "").trim();
            window.initiativeStrategyApi.list("search=" + encodeURIComponent(term) + "&page=1&pageSize=25")
              .then(function (res) { success(res); })
              .catch(function (err) { failure(err); });
          },
          processResults: function (data) {
            var items = (data && data.items) || [];
            return { results: items.map(function (x) { return { id: x.initiativeId || "", text: (x.initiativeId || "") + " | " + (x.initiativeName || "") + " | " + ((window.enterpriseWorkbookOptions?.userDisplayName?.(x.owner) || x.owner || "-")) + " | " + (x.status || "-") }; }) };
          }
        };
      }
      $this.select2(cfg);
      if (!el._s2NativeBridge) {
        $this.on("select2:select select2:unselect select2:clear", function () {
          el.dispatchEvent(new Event("change", { bubbles: true }));
        });
        el._s2NativeBridge = true;
      }
      mirrorSelect2Invalid($this);
    });
  }

  function initDatePickers() {
    var inputs = document.querySelectorAll("#projectPpmModal .flatpickr-date");
    if (!inputs.length) return;
    if (!window.flatpickr) {
      inputs.forEach(function (input) {
        input.type = "date";
        if (!input._nativeDateBound) {
          input.addEventListener("click", function () { if (typeof input.showPicker === "function") input.showPicker(); });
          input._nativeDateBound = true;
        }
      });
      return;
    }
    inputs.forEach(function (input) {
      if (input._flatpickr) try { input._flatpickr.destroy(); } catch (_) {}
      window.flatpickr(input, {
        dateFormat: "Y-m-d",
        allowInput: false,
        clickOpens: true,
        disableMobile: true,
        onChange: function () { input.dispatchEvent(new Event("change", { bubbles: true })); }
      });
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    initSelect2();
    initDatePickers();
    var modalEl = document.getElementById("projectPpmModal");
    if (modalEl) {
      modalEl.addEventListener("shown.bs.modal", function () {
        initSelect2();
        initDatePickers();
        var sm = document.getElementById("ppm-project-scope-mode");
        var bt = document.getElementById("ppm-project-budget-type");
        if (sm) sm.dispatchEvent(new Event("change", { bubbles: true }));
        if (bt) bt.dispatchEvent(new Event("change", { bubbles: true }));
      });
    }
  });
})(window, document, window.jQuery);
