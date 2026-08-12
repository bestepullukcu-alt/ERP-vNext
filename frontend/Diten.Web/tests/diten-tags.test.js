const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * DitenTags — the ONE tag input, shared by the three screens that had three of them.
 *
 * Measured before this change: Tagify ran on the LIBRARY'S DEFAULT CSS (the single override in
 * backbone-custom.css is scoped to #tenantSecurityRoot and belongs to another screen), and three screens
 * constructed it independently. Change one and the other two drift — the pattern this project has corrected
 * five times.
 *
 * LAYOUT "C": the box stays EMPTY and the tags flow BELOW it. The reason is alignment, not taste: every control
 * on the form is 36px, and tags rendered inside grow the box to 60px as they accumulate, breaking the row it
 * shares. The height assertion is therefore the layout's real claim.
 *
 * ⚠ jsdom performs NO LAYOUT — offsetHeight is always 0 — so the pixel height cannot be asserted here. What is
 * asserted here is the MECHANISM (tags are not rendered inside the box; the stylesheet pins the height); the
 * pixels are measured in the browser and reported with the round.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const CSS = () => read("wwwroot", "assets", "css", "backbone-custom.css");
const COMPONENT = () => read("wwwroot", "assets", "js", "shared", "diten-tags.js");

/*
 * A Tagify stand-in with the surface the component actually uses. The real library needs layout and a full DOM;
 * what matters here is the CONTRACT between the component and it — the options passed in, and the value/remove
 * API used to render the strip.
 */
const installTagifyDouble = () => {
  const instances = [];
  global.Tagify = function (input, options) {
    const listeners = {};
    const api = {
      input,
      options,
      value: [],
      DOM: { scope: (() => { const s = global.document.createElement("tags"); input.parentNode.insertBefore(s, input); return s; })() },
      on(event, handler) { (listeners[event] ||= []).push(handler); return api; },
      emit(event) { (listeners[event] || []).forEach((h) => h({ detail: {} })); },
      addTags(values) {
        values.forEach((v) => api.value.push({ value: v }));
        api.emit("change");
      },
      /*
       * Mirrors the REAL library, including the part that bit: `removeTag` takes a DOM tag ELEMENT and silently
       * does nothing when handed a value object — which is exactly how a no-op remove button shipped past a
       * green suite. `removeTags` is the one that accepts values.
       */
      removeTag(tagElement) {
        if (!(tagElement instanceof global.window.HTMLElement)) { return; }
        api.value = api.value.filter((t) => t.value !== tagElement.getAttribute("value"));
        api.emit("change");
      },
      removeTags(value) {
        api.value = api.value.filter((t) => t.value !== value);
        api.emit("change");
      }
    };
    instances.push(api);
    return api;
  };
  return instances;
};

const load = () => {
  delete global.DitenTags;
  loadScript("wwwroot/assets/js/shared/diten-tags.js");
  return global.DitenTags;
};

const host = (id = "taskTags") => {
  document.body.innerHTML = `<div id="root"><input id="${id}" name="tags" /></div>`;
  return document.getElementById(id);
};

// ── the layout claim ────────────────────────────────────────────────────────

describe('layout "C" — the box stays 36px however many tags there are', () => {
  test("tags are NOT rendered inside the input box", () => {
    /*
     * The mechanism behind the height: with the chips outside, the box holds one line of input and can never
     * grow. A stylesheet that merely clipped an overflowing box would look right and still push the row.
     */
    const css = CSS();
    const rule = /\.diten-tags\s+\.tagify__tag\s*\{([^}]*)\}/.exec(css);

    expect(rule, "nothing removes the in-box chips").toBeTruthy();
    expect(rule[1], "the in-box chips are still laid out").toMatch(/display:\s*none/);
  });

  test("the stylesheet PINS the box height rather than merely suggesting it", () => {
    /*
     * min-block-size alone would still grow. 38px is MEASURED from the neighbouring .form-control in this theme
     * (20.625px line + 2x7.688px padding + 2x1px border) — the brief said "36px", the rendered controls are 38,
     * and matching the NEIGHBOUR is what the requirement is actually about.
     */
    const css = CSS();
    const box = /\.diten-tags\s*\{([^}]*)\}/.exec(css);

    expect(box, "there is no .diten-tags rule at all").toBeTruthy();
    expect(box[1], "the height is not pinned").toMatch(/block-size:\s*38px/);
  });

  test("the component MARKS the control, or the stylesheet never attaches", () => {
    /*
     * The defect this test exists for, found on screen and not by the suite: every rule above keys off
     * `.diten-tags`, Tagify renders its own <tags class="tagify form-control"> scope, and nothing added the
     * class. The component ran, the value was correct, and the layout was the library default — a green suite
     * over an unstyled control.
     */
    installTagifyDouble();
    const DitenTags = load();
    host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];

    expect(instance.DOM.scope.classList.contains("diten-tags"),
      "the Tagify scope was never marked — no .diten-tags rule can apply").toBe(true);
  });

  test("ten tags produce ten chips OUTSIDE the box, and the box gains none", () => {
    installTagifyDouble();
    const DitenTags = load();
    const input = host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];
    instance.addTags(["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"]);

    const strip = document.querySelector(".diten-tags-strip");
    expect(strip, "no strip was rendered").toBeTruthy();
    expect(strip.querySelectorAll(".diten-tags-chip")).toHaveLength(10);
    // The strip is a SIBLING of the control, never inside it.
    expect(strip.closest("tags"), "the strip was rendered inside the box").toBeNull();
  });

  test("the strip is NOT rendered at all when there are no tags", () => {
    // An empty band is its own kind of noise, and it would also occupy the space the alignment fix reclaimed.
    installTagifyDouble();
    const DitenTags = load();
    host();

    DitenTags.enhance(document, { selector: "#taskTags" });

    expect(document.querySelector(".diten-tags-strip"), "an empty strip was rendered").toBeNull();
  });

  test("removing the last tag removes the strip again", () => {
    installTagifyDouble();
    const DitenTags = load();
    host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];
    instance.addTags(["only"]);
    expect(document.querySelector(".diten-tags-strip")).toBeTruthy();

    // Through the CHIP'S OWN BUTTON, not through the API directly — the button is what was broken, and a test
    // that called the library itself would have kept passing.
    document.querySelector(".diten-tags-remove").click();

    expect(document.querySelector(".diten-tags-strip"), "the strip outlived its last tag").toBeNull();
  });

  test("the chip's own button removes the tag AND the value — the click, not the API", () => {
    /*
     * The defect a green suite hid: the button called `removeTag(dataObject)`, which the real library ignores
     * (it wants a DOM tag element). Nothing was removed, nothing threw, and only a click on the running page
     * showed it. Asserted through the BUTTON, and on the value the payload is built from.
     */
    installTagifyDouble();
    const DitenTags = load();
    const input = host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];
    instance.addTags(["kalite", "acil"]);

    document.querySelector(".diten-tags-remove").click();

    expect(instance.value.map((t) => t.value), "the library's value still holds the removed tag")
      .toEqual(["acil"]);
    expect(document.querySelectorAll(".diten-tags-chip")).toHaveLength(1);
    expect(input.__tagify).toBe(instance);
  });

  test("the counter is rendered beside the chips", () => {
    installTagifyDouble();
    const DitenTags = load();
    host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];
    instance.addTags(["a", "b", "c"]);

    expect(document.querySelector(".diten-tags-count").textContent).toContain("3");
  });
});

// ── the ORIGINAL INPUT must never be seen ───────────────────────────────────

describe("the original <input> is hidden, empty or full", () => {
  /*
   * A LIVE defect this suite did not catch, and the reason it did not is worth stating: every assertion above
   * measures the <tags> element and the strip. Nothing asked whether the element Tagify REPLACES is still on
   * screen. It was — and the moment the first tag was added it opened to 38px and showed the raw comma value
   * ("kalite,acil,regülasyon,…") as a second box under the strip.
   *
   * ROOT CAUSE, and it was this component's doing rather than a theme collision: Tagify hides the original
   * input with an ADJACENT-SIBLING rule (tagify.css:121 `.tagify + input { position:absolute; left:-9999em;
   * transform:scale(0) }`). The strip is inserted between the <tags> element and that input, so the two stopped
   * being adjacent, the vendor rule stopped matching, and `form-control` made the input visible again.
   *
   * The fix therefore does NOT rely on DOM order: the component marks the input itself and the stylesheet hides
   * it by that class. These tests assert the mark, the rule, and the order-independence.
   */
  test("the component MARKS the original input so the hiding cannot depend on sibling order", () => {
    installTagifyDouble();
    const DitenTags = load();
    const input = host();

    DitenTags.enhance(document, { selector: "#taskTags" });

    expect(input.classList.contains("diten-tags-source"),
      "the original input is unmarked — nothing can hide it once the strip breaks adjacency").toBe(true);
  });

  test("it stays marked once tags exist — the state the defect appeared in", () => {
    installTagifyDouble();
    const DitenTags = load();
    const input = host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];
    instance.addTags(["kalite", "acil"]);

    expect(input.classList.contains("diten-tags-source")).toBe(true);
    // …and it is still the element carrying the value, because the payload contract depends on it.
    expect(input.id).toBe("taskTags");
  });

  test("the stylesheet hides it the way the vendor does — not by dropping it from the form", () => {
    /*
     * Off-screen rather than display:none, mirroring tagify.css. The element still has to be a real, submittable
     * form control carrying the comma-separated value; a display:none rule would work today and is a different
     * promise from the one the library makes about its own hidden input.
     */
    const css = CSS();
    const rule = /\.diten-tags-source\s*\{([^}]*)\}/.exec(css);

    expect(rule, "nothing hides the original input").toBeTruthy();
    expect(rule[1], "the input is not moved off-screen").toMatch(/position:\s*absolute/);
    expect(rule[1], "an off-screen offset is missing").toMatch(/-9999em/);
  });

  test("the rule outranks .form-control, which is what made it visible", () => {
    // The theme paints `form-control` as a visible 38px box. The hiding rule has to win that, and !important is
    // what the vendor itself uses for the same fight.
    const css = CSS();
    const rule = /\.diten-tags-source\s*\{([^}]*)\}/.exec(css);
    expect(rule[1], "the hide can be overridden by the theme").toMatch(/!important/);
  });

  test("the strip still sits between the box and the input — order is no longer load-bearing", () => {
    // Pinned deliberately: the layout wants the chips directly under the box. That placement is what broke the
    // vendor's adjacency rule, so this test states that the placement is kept and no longer matters.
    installTagifyDouble();
    const DitenTags = load();
    const input = host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];
    instance.addTags(["kalite"]);

    const strip = document.querySelector(".diten-tags-strip");
    expect(strip.previousElementSibling.tagName.toLowerCase()).toBe("tags");
    expect(strip.nextElementSibling, "the input is no longer after the strip").toBe(input);
  });
});

// ── the contract that must not break ────────────────────────────────────────

describe("the value contract is CARRIED OVER, not rewritten", () => {
  test("the underlying input stays comma-separated", () => {
    /*
     * `originalInputValueFormat` is why the payload builder can keep splitting on commas. Losing it changes what
     * the API receives, silently, on three screens at once.
     */
    installTagifyDouble();
    const DitenTags = load();
    host();

    const instance = DitenTags.enhance(document, { selector: "#taskTags" })[0];
    const format = instance.options.originalInputValueFormat;

    expect(format, "the comma contract is gone").toBeTypeOf("function");
    expect(format([{ value: "a" }, { value: "b" }])).toBe("a,b");
  });

  test("a caller's own options survive — the security screens need theirs", () => {
    // The IP field carries a pattern, delimiters and a max; a component that dropped them would silently accept
    // anything the user typed.
    installTagifyDouble();
    const DitenTags = load();
    host("allowedIps");

    const instance = DitenTags.enhance(document, {
      selector: "#allowedIps",
      tagify: { pattern: /^x$/, delimiters: ",| ", maxTags: 50 }
    })[0];

    expect(instance.options.maxTags).toBe(50);
    expect(instance.options.delimiters).toBe(",| ");
    expect(String(instance.options.pattern)).toBe("/^x$/");
    // …and the shared contract is still applied on top.
    expect(instance.options.originalInputValueFormat).toBeTypeOf("function");
  });

  test("enhancing twice does not build a second Tagify on the same node", () => {
    const instances = installTagifyDouble();
    const DitenTags = load();
    host();

    DitenTags.enhance(document, { selector: "#taskTags" });
    const second = DitenTags.enhance(document, { selector: "#taskTags" });

    expect(instances).toHaveLength(1);
    expect(second, "the second call reported work it did not do").toHaveLength(0);
  });

  test("it degrades quietly when the library is absent", () => {
    delete global.Tagify;
    const DitenTags = load();
    host();

    expect(() => DitenTags.enhance(document, { selector: "#taskTags" })).not.toThrow();
  });
});

// ── one component, three screens ────────────────────────────────────────────

describe("nothing constructs Tagify directly any more", () => {
  const OURS = [
    ["wwwroot", "assets", "js", "Tasks", "form.js"],
    ["wwwroot", "assets", "js", "Platform", "Tenants", "security.js"],
    ["wwwroot", "assets", "js", "Governance", "TenantSecuritySettings", "index.js"]
  ];

  test("our three screens call the shared component", () => {
    OURS.forEach((file) => {
      expect(read(...file), `${file.join("/")} does not use DitenTags`).toMatch(/DitenTags\.enhance/);
    });
  });

  test("our three screens never call `new Tagify` themselves", () => {
    /*
     * The whole point of the round. Sneat's own demo files are excluded by name: they are template leftovers,
     * not screens of ours, and rewriting them would be churn with no reader.
     */
    OURS.forEach((file) => {
      expect(read(...file), `${file.join("/")} still constructs Tagify directly`)
        .not.toMatch(/new\s+(window\.|global\.)?Tagify\s*\(/);
    });
  });

  test("only the shared component knows the library exists", () => {
    expect(COMPONENT()).toMatch(/new\s+(global\.)?Tagify\s*\(/);
  });
});

// ── l10n ────────────────────────────────────────────────────────────────────

describe("the component's strings live in ONE place", () => {
  const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
  const KEYS = ["TagsPlaceholder", "TagsCount", "TagsRemove"];

  test("they are SharedResource strings, not copied into three screen resx files", () => {
    /*
     * DECISION: SharedResource. The sentences describe the CONTROL ("type a tag and press Enter"), not any
     * screen's subject matter, and three copies of one sentence drift the moment one is edited. The delivery
     * path is the one _DataTableL10n already established — rendered by the LAYOUT, so no page can forget it.
     */
    LOCALES.forEach((locale) => {
      const xml = read("Resources", `SharedResource.${locale}.resx`);
      KEYS.forEach((key) => expect(xml, `${locale} has no ${key}`).toContain(`name="${key}"`));
    });

    // And NOT duplicated into the screen resources.
    KEYS.forEach((key) => {
      expect(read("Resources", "Views", "Tasks", "TasksIndex.en.resx"),
        `${key} was copied into the Tasks resource`).not.toContain(`name="${key}"`);
    });
  });

  test("the two counted strings carry the placeholder", () => {
    const tr = read("Resources", "SharedResource.tr.resx");
    ["TagsCount", "TagsRemove"].forEach((key) => {
      const entry = new RegExp(`name="${key}"[\\s\\S]{0,200}?<value>([^<]*)</value>`).exec(tr);
      expect(entry, `${key} missing from tr`).toBeTruthy();
      expect(entry[1], `${key} has no {0} placeholder`).toContain("{0}");
    });
  });

  test("this component's keys are complete in all seven — measured, not assumed", () => {
    /*
     * ⚠ MEASURED, and deliberately NOT a whole-file parity assertion.
     *
     * SharedResource is ALREADY out of parity and was before this change: en/tr carry 263 keys and the other
     * five carry 230 — a 33-key gap this round did not create and does not fix (33 unrelated strings is its own
     * piece of work). Asserting whole-file parity here would fail for somebody else's debt and would tempt the
     * next reader to weaken the check.
     *
     * What this round OWNS is its own three keys, and they are complete in all seven. The gap is pinned below
     * so it cannot GROW from this side without a test noticing.
     */
    const keysOf = (locale) => [...read("Resources", `SharedResource.${locale}.resx`)
      .matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]);

    LOCALES.forEach((locale) => {
      const keys = keysOf(locale);
      KEYS.forEach((key) => {
        expect(keys.filter((k) => k === key), `${locale} does not carry exactly one ${key}`).toHaveLength(1);
      });
      expect(new Set(keys).size, `${locale} contains duplicate keys`).toBe(keys.length);
    });

    // The pre-existing gap, pinned at its measured size. A NEW key added to en/tr alone widens it and fails here.
    const missing = LOCALES.filter((l) => l !== "en")
      .map((l) => keysOf("en").filter((k) => !keysOf(l).includes(k)).length);
    expect(Math.max(...missing), "the pre-existing SharedResource gap grew").toBeLessThanOrEqual(33);
  });

  test("the strings reach the browser through ONE bridge, rendered by the layout", () => {
    // A per-page include is a page that will forget — the reasoning _DataTableL10n already records.
    const partial = read("Views", "Shared", "_TagsL10n.cshtml");
    KEYS.forEach((key) => expect(partial, `${key} is not published`).toContain(key));

    ["_LayoutTenantShell.cshtml", "_LayoutPlatformAdmin.cshtml"].forEach((layout) => {
      expect(read("Views", "Shared", layout), `${layout} does not render the bridge`)
        .toContain("_TagsL10n.cshtml");
    });
  });
});

// ── FG-003 ──────────────────────────────────────────────────────────────────

describe("FG-003", () => {
  test("the component writes no inline styles", () => {
    const source = COMPONENT();
    expect(source, "the component sets element.style").not.toMatch(/\.style\./);
    expect(source, "the component emits a style attribute").not.toMatch(/style="/);
  });

  test("the chip is neutral — the accent colour belongs to the primary action", () => {
    /*
     * A tag is not a link and not a call to action. Painting it primary competes with "Oluştur", which IS the
     * one accented control on the form.
     */
    const css = CSS();
    const chip = /\.diten-tags-chip\s*\{([^}]*)\}/.exec(css);
    expect(chip, "there is no chip rule").toBeTruthy();
    expect(chip[1], "the chip uses the primary accent").not.toMatch(/--bs-primary\b/);
    expect(chip[1], "the chip has no surface token").toMatch(/var\(--bs-/);
  });

  test("the remove control meets the 24px touch target", () => {
    const css = CSS();
    const remove = /\.diten-tags-remove\s*\{([^}]*)\}/.exec(css);
    expect(remove, "there is no remove-button rule").toBeTruthy();
    expect(remove[1]).toMatch(/(min-)?(inline|block)-size:\s*(2[4-9]|[3-9]\d)px/);
  });

  test("the strip is written in logical properties, so RTL needs no second rule", () => {
    const css = CSS();
    const strip = /\.diten-tags-strip\s*\{([^}]*)\}/.exec(css);
    expect(strip, "there is no strip rule").toBeTruthy();
    expect(strip[1], "physical margins break under RTL").not.toMatch(/margin-(left|right)\s*:/);
  });
});
