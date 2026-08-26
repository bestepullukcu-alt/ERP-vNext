const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * THE PICKER ROW — two layers, and it does not move.
 *
 * <b>Measured before this change.</b> Every option was ONE string: "Alidf ufanoglu — CT Fabrika Md — E2E Test
 * Unit". Three facts at the same weight, joined by em dashes, in a control that is 38px tall — so a long one
 * wrapped, and a wrapped row is TALLER than its neighbours. The list changed height as the user scrolled it.
 * That is the structural complaint: not "it is ugly" but "the row's height depends on its content".
 *
 * <b>The shape.</b> A person reads as a name with a quiet second line under it; the second line is the one that
 * gets cut. A position reads the same way with its unit and holder count underneath. Once CHOSEN, the row
 * collapses to a single line, because the closed control is 38px and a two-line selection would grow it.
 *
 * <b>⚠ THE ROW IS BUILT, NEVER PRINTED.</b> select2's templateResult renders raw HTML: a display name is
 * user-supplied data, so a name containing markup would execute. Every node here is created with
 * createElement and filled with textContent — escaping is therefore STRUCTURAL rather than a function somebody
 * has to remember to call. The XSS tests below hold that line.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const labels = {
  placeholder: "Kişi seçin…",
  empty: "Henüz hiç kimsenin pozisyonu yok…",
  nameUnavailable: "Ad bilgisi yok",
  holderCount: "{0} kişi"
};

const person = (overrides = {}) => ({
  userId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
  displayName: "Agent Sub",
  positionId: "11111111-1111-1111-1111-111111111111",
  positionCode: "MUH",
  positionName: "Muhasebe Md",
  organizationUnitId: "22222222-2222-2222-2222-222222222222",
  organizationUnitCode: "FIN",
  organizationUnitName: "Finans",
  legalEntityId: "99999999-9999-9999-9999-999999999999",
  ...overrides
});

const position = (overrides = {}) => ({
  positionId: "11111111-1111-1111-1111-111111111111",
  positionCode: "FAB",
  positionName: "CT Fabrika Md",
  organizationUnitId: "22222222-2222-2222-2222-222222222222",
  organizationUnitCode: "E2E",
  organizationUnitName: "E2E Test Unit",
  legalEntityId: "99999999-9999-9999-9999-999999999999",
  activeHolderCount: 1,
  ...overrides
});

const textOf = (node, selector) => {
  const found = node.querySelector(selector);
  return found ? found.textContent : null;
};

beforeEach(() => {
  delete global.TaskForm;
  loadScript("wwwroot/assets/js/Tasks/form.js");
});

// ── 1. the two layers ───────────────────────────────────────────────────────

describe("a person row is two layers, not one string", () => {
  test("the name is the primary layer and carries NOTHING else", () => {
    const node = global.TaskForm.personOptionNode(person(), labels);
    expect(textOf(node, ".diten-opt-primary")).toBe("Agent Sub");
  });

  test("position and unit are the quiet second layer, joined by a middot", () => {
    const node = global.TaskForm.personOptionNode(person(), labels);
    expect(textOf(node, ".diten-opt-secondary")).toBe("Muhasebe Md · Finans");
  });

  test("an unresolved name shows the fallback label, never the user id", () => {
    const node = global.TaskForm.personOptionNode(person({ displayName: null }), labels);
    expect(textOf(node, ".diten-opt-primary")).toBe("Ad bilgisi yok");
    expect(node.textContent).not.toContain("dddddddd");
  });

  test("a CHOSEN person is one line — the closed control is 38px and must not grow", () => {
    const node = global.TaskForm.personSelectionNode(person(), labels);
    expect(node.querySelector(".diten-opt-secondary")).toBeNull();
    expect(textOf(node, ".diten-opt-primary")).toBe("Agent Sub · Muhasebe Md");
  });
});

describe("a position row is the same shape with the holder count", () => {
  test("the position name leads and the unit follows underneath", () => {
    const node = global.TaskForm.positionOptionNode(position(), labels);
    expect(textOf(node, ".diten-opt-primary")).toBe("CT Fabrika Md");
    expect(textOf(node, ".diten-opt-secondary")).toBe("E2E Test Unit · 1 kişi");
  });

  test("the holder count comes from the DTO, and zero is said rather than hidden", () => {
    // An EMPTY pool is a real and important state: pooling work at it means nobody is offered the task.
    const node = global.TaskForm.positionOptionNode(position({ activeHolderCount: 0 }), labels);
    expect(textOf(node, ".diten-opt-secondary")).toBe("E2E Test Unit · 0 kişi");
  });

  test("a chosen position collapses to one line too", () => {
    const node = global.TaskForm.positionSelectionNode(position(), labels);
    expect(node.querySelector(".diten-opt-secondary")).toBeNull();
    expect(textOf(node, ".diten-opt-primary")).toBe("CT Fabrika Md · E2E Test Unit");
  });
});

// ── 2. the initials ─────────────────────────────────────────────────────────

describe("the circle carries initials, and the one-word case is DECIDED", () => {
  test("two or more words: first and LAST, so a middle name does not win", () => {
    expect(global.TaskForm.personInitials("Agent Sub")).toBe("AS");
    expect(global.TaskForm.personInitials("Ali Rıza Tufanoğlu")).toBe("AT");
  });

  test("ONE word: the first TWO letters — a single glyph reads as an error, not a monogram", () => {
    expect(global.TaskForm.personInitials("Diten")).toBe("DI");
  });

  test("a one-LETTER name is left as that letter rather than padded with something invented", () => {
    expect(global.TaskForm.personInitials("X")).toBe("X");
  });

  test("no name at all: a neutral mark, never a letter borrowed from the id", () => {
    expect(global.TaskForm.personInitials("")).toBe("?");
    expect(global.TaskForm.personInitials(null)).toBe("?");
  });

  test("the circle is on the person row and shows those initials", () => {
    const node = global.TaskForm.personOptionNode(person(), labels);
    expect(textOf(node, ".diten-opt-avatar")).toBe("AS");
  });

  test("a POSITION gets a glyph, not initials — a seat is not a person", () => {
    const node = global.TaskForm.positionOptionNode(position(), labels);
    const avatar = node.querySelector(".diten-opt-avatar");
    expect(avatar).toBeTruthy();
    expect(avatar.querySelector("i.bx")).toBeTruthy();
    expect(avatar.textContent.trim()).toBe("");
  });
});

// ── 3. HTML escaping — the security claim ───────────────────────────────────

describe("a display name is DATA, and cannot become markup", () => {
  const hostile = 'Ali <b onclick="alert(1)">Tufan</b> "quoted"';

  test("markup in a name renders as TEXT — no element is created from it", () => {
    const node = global.TaskForm.personOptionNode(person({ displayName: hostile }), labels);
    expect(node.querySelector("b")).toBeNull();
    expect(textOf(node, ".diten-opt-primary")).toBe(hostile);
  });

  test("the same holds for the collapsed selection line", () => {
    const node = global.TaskForm.personSelectionNode(person({ displayName: hostile }), labels);
    expect(node.querySelector("b")).toBeNull();
    expect(node.textContent).toContain('"quoted"');
  });

  test("a hostile POSITION or UNIT name is text too — they are tenant data as well", () => {
    const node = global.TaskForm.positionOptionNode(position({
      positionName: '<img src=x onerror="alert(1)">',
      organizationUnitName: '<script>alert(2)</script>'
    }), labels);
    expect(node.querySelector("img")).toBeNull();
    expect(node.querySelector("script")).toBeNull();
    expect(textOf(node, ".diten-opt-primary")).toBe('<img src=x onerror="alert(1)">');
  });

  test("the initials cannot smuggle markup either", () => {
    const node = global.TaskForm.personOptionNode(person({ displayName: "<b>Bob" }), labels);
    expect(node.querySelector(".diten-opt-avatar").querySelector("b")).toBeNull();
  });

  test("STRUCTURAL, not a helper somebody must remember: the builders never touch innerHTML", () => {
    /*
     * The escaping guarantee above is only as strong as the way the node is built. A future `innerHTML = ...`
     * inside these builders would pass every test above with a benign fixture and fail with a hostile name.
     */
    const source = read("wwwroot", "assets", "js", "Tasks", "form.js");
    const builders = source.slice(source.indexOf("const personInitials"), source.indexOf("const renderPositionOptions"));
    expect(builders.length).toBeGreaterThan(200);
    expect(builders).not.toMatch(/innerHTML|insertAdjacentHTML|outerHTML/);
  });
});

// ── 4. grouping — three factories must not blur ─────────────────────────────

describe("rows are grouped by organization unit", () => {
  const doc = () => global.document;

  test("each unit becomes an <optgroup> and the rows sit inside it", () => {
    const select = doc().createElement("select");
    global.TaskForm.renderPersonOptions(select, [
      person({ userId: "u1", displayName: "A", organizationUnitId: "unit-a", organizationUnitName: "Fabrika A" }),
      person({ userId: "u2", displayName: "B", organizationUnitId: "unit-b", organizationUnitName: "Fabrika B" }),
      person({ userId: "u3", displayName: "C", organizationUnitId: "unit-a", organizationUnitName: "Fabrika A" })
    ], labels);

    const groups = Array.from(select.querySelectorAll("optgroup"));
    expect(groups.map((g) => g.label)).toEqual(["Fabrika A", "Fabrika B"]);
    expect(groups[0].querySelectorAll("option")).toHaveLength(2);
    expect(groups[1].querySelectorAll("option")).toHaveLength(1);
  });

  test("two DIFFERENT units with the SAME name are told apart by their code — never left ambiguous", () => {
    /*
     * The whole point of grouping. If both factories call the unit "Üretim", a heading reading "Üretim" twice
     * is worse than no heading: it says the two lists are the same one.
     */
    const select = doc().createElement("select");
    global.TaskForm.renderPersonOptions(select, [
      person({ userId: "u1", organizationUnitId: "unit-a", organizationUnitCode: "TR-URT", organizationUnitName: "Üretim" }),
      person({ userId: "u2", organizationUnitId: "unit-b", organizationUnitCode: "AZ-URT", organizationUnitName: "Üretim" })
    ], labels);

    expect(Array.from(select.querySelectorAll("optgroup")).map((g) => g.label))
      .toEqual(["Üretim (TR-URT)", "Üretim (AZ-URT)"]);
  });

  test("the same unit appearing twice is ONE group, not two", () => {
    const select = doc().createElement("select");
    global.TaskForm.renderPersonOptions(select, [
      person({ userId: "u1", organizationUnitId: "unit-a" }),
      person({ userId: "u2", organizationUnitId: "unit-a" })
    ], labels);
    expect(select.querySelectorAll("optgroup")).toHaveLength(1);
  });

  test("positions are grouped the same way", () => {
    const select = doc().createElement("select");
    global.TaskForm.renderPositionOptions(select, [
      position({ positionId: "p1", organizationUnitId: "unit-a", organizationUnitName: "Fabrika A" }),
      position({ positionId: "p2", organizationUnitId: "unit-b", organizationUnitName: "Fabrika B" })
    ], labels);
    expect(Array.from(select.querySelectorAll("optgroup")).map((g) => g.label))
      .toEqual(["Fabrika A", "Fabrika B"]);
  });

  test("the option VALUE is still the identity — the payload contract does not move", () => {
    const select = doc().createElement("select");
    global.TaskForm.renderPersonOptions(select, [person({ userId: "the-id" })], labels);
    expect(select.querySelector("optgroup option").value).toBe("the-id");
  });
});

// ── 5. the placeholder is not a row ─────────────────────────────────────────

describe("the placeholder is select2's, not a line in the list", () => {
  test("the dropdown holds EXACTLY as many options as there are people", () => {
    const select = global.document.createElement("select");
    const rows = [person({ userId: "u1" }), person({ userId: "u2" }), person({ userId: "u3" })];
    global.TaskForm.renderPersonOptions(select, rows, labels);

    const selectable = Array.from(select.querySelectorAll("option")).filter((o) => o.value !== "");
    expect(selectable).toHaveLength(3);
    // The one empty option select2 REQUIRES for a placeholder carries no text, so even if it were rendered
    // it could not read as a choice. select2 removes it from the results by id.
    const blanks = Array.from(select.querySelectorAll("option")).filter((o) => o.value === "");
    expect(blanks).toHaveLength(1);
    expect(blanks[0].textContent).toBe("");
  });

  test("the prompt travels as data-placeholder, which is what enhanceSelects reads", () => {
    const select = global.document.createElement("select");
    global.TaskForm.renderPersonOptions(select, [person()], labels);
    expect(select.getAttribute("data-placeholder")).toBe("Kişi seçin…");
  });

  test("a MULTI-select gets no blank option at all — there it would be a selectable empty identity", () => {
    const select = global.document.createElement("select");
    select.multiple = true;
    global.TaskForm.renderPersonOptions(select, [person()], labels, { multiple: true });
    expect(Array.from(select.querySelectorAll("option")).filter((o) => o.value === "")).toHaveLength(0);
  });

  test("the position picker follows the same rule", () => {
    const select = global.document.createElement("select");
    global.TaskForm.renderPositionOptions(select, [position({ positionId: "p1" })], labels);
    const selectable = Array.from(select.querySelectorAll("option")).filter((o) => o.value !== "");
    expect(selectable).toHaveLength(1);
  });
});

// ── 6. all seven pickers, both surfaces ─────────────────────────────────────

describe("the same row on every picker — five on the form, two in the offcanvas", () => {
  test("each rendered picker is MARKED as carrying rows, so enhanceSelects knows to template it", () => {
    const select = global.document.createElement("select");
    global.TaskForm.renderPersonOptions(select, [person()], labels);
    expect(select.getAttribute("data-diten-rows")).toBe("person");

    const pool = global.document.createElement("select");
    global.TaskForm.renderPositionOptions(pool, [position()], labels);
    // 'seat', not 'position': a bare "position" is also a configurable field's options-source key, and
    // form.js is forbidden from naming one (tasks-record-fields.test.js).
    expect(pool.getAttribute("data-diten-rows")).toBe("seat");
  });

  test("the row data rides ON the option, so the template never re-parses a label", () => {
    const select = global.document.createElement("select");
    global.TaskForm.renderPersonOptions(select, [person({ displayName: "Agent Sub" })], labels);
    const option = select.querySelector("optgroup option");
    expect(option.ditenRow).toBeTruthy();
    expect(option.ditenRow.displayName).toBe("Agent Sub");
  });

  test("the form page fills all five person/position pickers through these renderers", () => {
    const source = read("wwwroot", "assets", "js", "Tasks", "form-page.js");
    ["taskAssignee", "taskWatchers", "taskReviewer", "taskApprovalManager"].forEach((id) => {
      const call = new RegExp(`renderPersonOptions\\(el\\('${id}'\\)`);
      expect(source, `${id} is not filled by renderPersonOptions`).toMatch(call);
    });
    expect(source).toMatch(/renderPositionOptions\(el\('taskPoolPosition'\)/);
  });

  test("the quick-create offcanvas uses the SAME two renderers — one draft, one vocabulary", () => {
    const source = read("wwwroot", "assets", "js", "WorkCenterNext", "quick-create.js");
    expect(source).toMatch(/renderPersonOptions\(el\('quickAssignee'\)/);
    expect(source).toMatch(/renderPositionOptions\(el\('quickPoolPosition'\)/);
  });

  test("every caller passes a holder-count template, or the position row would read '{0} kişi'", () => {
    ["wwwroot/assets/js/Tasks/form-page.js", "wwwroot/assets/js/WorkCenterNext/quick-create.js"]
      .forEach((file) => {
        const source = fs.readFileSync(web(...file.split("/")), "utf8");
        expect(source, `${file} never reads pickerHolderCount`).toMatch(/pickerHolderCount/);
      });
  });
});

// ── 7. select2 is told to use them ──────────────────────────────────────────

describe("enhanceSelects binds the templates — and only where rows exist", () => {
  const bindWith = (attribute) => {
    const select = global.document.createElement("select");
    if (attribute) { select.setAttribute("data-diten-rows", attribute); }
    global.document.body.appendChild(select);

    let settings = null;
    const $node = {
      wrap: () => $node,
      parent: () => $node,
      select2: (options) => { settings = options; return $node; },
      on: () => $node
    };
    const jq = () => $node;
    jq.fn = {};
    global.jQuery = jq;

    global.TaskForm.enhanceSelects(global.document.body);
    return settings;
  };

  afterEach(() => {
    global.document.body.innerHTML = "";
    delete global.jQuery;
  });

  test("a marked picker gets templateResult AND templateSelection", () => {
    const select = global.document.createElement("select");
    select.className = "select2";
    select.setAttribute("data-diten-rows", "person");
    global.document.body.appendChild(select);

    let settings = null;
    const $node = {
      wrap: () => $node, parent: () => $node,
      select2: (options) => { settings = options; return $node; },
      on: () => $node
    };
    const jq = () => $node;
    jq.fn = {};
    global.jQuery = jq;

    global.TaskForm.enhanceSelects(global.document.body);
    expect(typeof settings.templateResult).toBe("function");
    expect(typeof settings.templateSelection).toBe("function");
  });

  test("an ORDINARY select (priority, target) is left exactly as it was", () => {
    const select = global.document.createElement("select");
    select.className = "select2";
    global.document.body.appendChild(select);

    let settings = null;
    const $node = {
      wrap: () => $node, parent: () => $node,
      select2: (options) => { settings = options; return $node; },
      on: () => $node
    };
    const jq = () => $node;
    jq.fn = {};
    global.jQuery = jq;

    global.TaskForm.enhanceSelects(global.document.body);
    expect(settings.templateResult).toBeUndefined();
    expect(settings.templateSelection).toBeUndefined();
  });

  test("the GROUP HEADING goes through the template as a plain string — select2 escapes those", () => {
    /*
     * templateResult is called for optgroups too. Returning a node for a heading would bypass select2's own
     * escaping for a label that is also tenant data.
     */
    const template = global.TaskForm.pickerRowTemplate("person", labels);
    const heading = template({ text: "Fabrika <b>A</b>", children: [] });
    expect(typeof heading).toBe("string");
    expect(heading).toBe("Fabrika <b>A</b>");
  });

  test("a result with no row behind it falls back to its plain text rather than an empty line", () => {
    const template = global.TaskForm.pickerRowTemplate("person", labels);
    expect(template({ text: "Aranıyor…" })).toBe("Aranıyor…");
  });

  test("a result WITH a row behind it becomes the two-layer node", () => {
    const option = global.document.createElement("option");
    option.ditenRow = person();
    const template = global.TaskForm.pickerRowTemplate("person", labels);
    const node = template({ text: "ignored", element: option });
    expect(node.nodeType).toBe(1);
    expect(textOf(node, ".diten-opt-secondary")).toBe("Muhasebe Md · Finans");
  });
});

// ── 8. the stylesheet does the not-moving ───────────────────────────────────

describe("the row cannot grow — CSS, not JS (FG-003)", () => {
  const css = () => read("wwwroot", "assets", "css", "backbone-custom.css");
  const rule = (selector) => {
    const match = new RegExp(`^${selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\s*\\{([^}]*)\\}`, "m")
      .exec(css());
    return match ? match[1] : null;
  };

  test("the secondary line is clipped with an ellipsis instead of wrapping", () => {
    const body = rule(".diten-opt-secondary");
    expect(body, ".diten-opt-secondary has no rule").toBeTruthy();
    expect(body).toMatch(/text-overflow:\s*ellipsis/);
    expect(body).toMatch(/overflow:\s*hidden/);
    expect(body).toMatch(/white-space:\s*nowrap/);
  });

  test("the primary line is clipped too — a long NAME must not wrap either", () => {
    const body = rule(".diten-opt-primary");
    expect(body).toMatch(/text-overflow:\s*ellipsis/);
    expect(body).toMatch(/white-space:\s*nowrap/);
  });

  test("clipping only works if the flex child may shrink: min-width 0 is present", () => {
    const body = rule(".diten-opt-body");
    expect(body, ".diten-opt-body has no rule").toBeTruthy();
    expect(body).toMatch(/min-width:\s*0/);
  });

  test("the avatar cannot be squeezed by a long name", () => {
    const body = rule(".diten-opt-avatar");
    expect(body).toMatch(/flex:\s*0\s*0\s*auto|flex-shrink:\s*0/);
  });

  test("the chosen row is height-capped so the 38px control cannot grow", () => {
    const body = rule(".diten-opt--single");
    expect(body, ".diten-opt--single has no rule").toBeTruthy();
    expect(body).toMatch(/height|line-height/);
  });

  test("RTL needs no mirrored rule — the row uses flow-relative properties only", () => {
    /*
     * The avatar leads the row in BOTH directions because it is the first flex child, not because it is
     * "on the left". A `margin-left` here would be the one thing that breaks Arabic.
     */
    const block = css().slice(css().indexOf(".diten-opt {"), css().indexOf(".diten-opt {") + 1800);
    expect(block).not.toMatch(/margin-left|margin-right|padding-left|padding-right|left:|right:/);
  });
});

// ── 9. the new string exists in all seven languages ─────────────────────────

describe("l10n — one new key, seven files, identical sets", () => {
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const resx = (lang) => read("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`);

  test("PickerHolderCount is in every language and carries the {0} counter", () => {
    LANGS.forEach((lang) => {
      const match = /<data name="PickerHolderCount"[^>]*>\s*<value>([^<]*)<\/value>/.exec(resx(lang));
      expect(match, `PickerHolderCount missing in ${lang}`).toBeTruthy();
      expect(match[1], `PickerHolderCount in ${lang} has no {0}`).toContain("{0}");
    });
  });

  test("the bridge publishes it, or the browser would render the key", () => {
    expect(read("Views", "Tasks", "_IndexL10n.cshtml")).toMatch(/PickerHolderCount = Localizer\["PickerHolderCount"\]/);
  });

  test("the key sets stay identical across the seven files", () => {
    const keysOf = (lang) => new Set(Array.from(resx(lang).matchAll(/<data name="([^"]+)"/g)).map((m) => m[1]));
    const reference = keysOf("tr");
    LANGS.filter((l) => l !== "tr").forEach((lang) => {
      const mine = keysOf(lang);
      const missing = [...reference].filter((k) => !mine.has(k));
      const extra = [...mine].filter((k) => !reference.has(k));
      expect(missing, `${lang} is missing: ${missing.join(", ")}`).toHaveLength(0);
      expect(extra, `${lang} has extra: ${extra.join(", ")}`).toHaveLength(0);
    });
  });
});
