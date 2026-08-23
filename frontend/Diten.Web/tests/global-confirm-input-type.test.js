const fs = require("fs");
const path = require("path");

/*
 * THE SHARED CONFIRM'S INPUT TYPE — a constant became a parameter, and nothing else moved.
 *
 * `_GlobalConfirmation.cshtml` is the ONE confirm dialog in this product; every module's "are you sure" goes
 * through it. It offered an input box and hard-coded that box to be a `textarea`, which is why every dialog that
 * needed a date, a number or a choice stayed a raw `Swal.fire` with none of the wrapper's title, description,
 * width or validation (BL-146).
 *
 * The change is one line: `options.inputType || 'textarea'`. No capability was added — the type this block
 * always wrote is now the type a caller may name, and the default is exactly today's behaviour.
 *
 * <b>HOW THIS IS TESTED.</b> The wrapper's script is EXTRACTED from the Razor view and executed against a Swal
 * stub, so the assertions below are made against the function the browser actually runs — not against a regex on
 * its source. The Razor expressions (`@Json.Serialize(...)`) are the only thing replaced, with the strings they
 * would render. Everything else is the shipped code.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const VIEW = fs.readFileSync(
  path.join(repoRoot, "frontend", "Diten.Web", "Views", "Shared", "_GlobalConfirmation.cshtml"), "utf8");

/** The view's script body, with Razor's localizer calls resolved to plain strings. */
const loadWrapper = (swalStub) => {
  const script = VIEW.slice(VIEW.indexOf("window.showConfirm = function"), VIEW.lastIndexOf("</script>"));
  const js = script.replace(/@Json\.Serialize\(SharedLocalizer\["([^"]+)"\]\.Value\)/g, '"$1"');
  const sandbox = { window: {}, Swal: swalStub, console, confirm: () => false };
  // eslint-disable-next-line no-new-func
  new Function("window", "Swal", "console", "confirm", js)(sandbox.window, swalStub, console, sandbox.confirm);
  return sandbox.window.showConfirm;
};

const captureConfig = () => {
  const seen = {};
  const stub = { fire: (config) => { seen.config = config; return { then: () => {} }; }, showValidationMessage: () => {} };
  return { seen, stub };
};

describe("the callers that were already using the input box", () => {
  /*
   * MEASURED: six call sites across four modules pass `showInput` today —
   *   Platform/Tenants/details.js  ×2      Platform/Tenants/index.js      ×1
   *   Platform/AuditLog/index.js   ×1      DocumentManagement/TemplateMasters/index.js ×1
   *   WorkCenterNext/app.js        ×1  (the module seam, which forwards whatever its caller asked for)
   * Not one of them names a type, so not one of them may change. This file is the whole product's shared
   * component: one module's round must not move another module's dialog.
   */
  it("is still six, and none of them names a type", () => {
    const roots = [path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js")];
    const files = [];
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { if (e.name !== "vendor") { walk(p); } }
      else if (e.name.endsWith(".js")) { files.push(p); }
    });
    roots.forEach(walk);

    const callers = files.filter((f) => /showInput\s*:/.test(fs.readFileSync(f, "utf8")));
    const occurrences = callers.reduce((n, f) =>
      n + (fs.readFileSync(f, "utf8").match(/showInput\s*:/g) || []).length, 0);
    expect(occurrences).toBe(6);

    // The one file that DOES pass a type is the WorkCenterNext seam, and it passes whatever its own caller said —
    // `undefined` for every prose dialog, which is what keeps them on the default.
    const namingAType = callers.filter((f) => /inputType\s*:/.test(fs.readFileSync(f, "utf8")));
    expect(namingAType.map((f) => path.basename(f))).toEqual(["app.js"]);
  });

  it("still gets a textarea when it asks for an input without naming a type", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Suspend", () => {}, { showInput: true, inputPlaceholder: "why?" });
    // MUTATION GUARD: drop the `|| 'textarea'` default and this is `undefined` — every existing caller loses
    // its box, in four modules that were not part of this round.
    expect(seen.config.input).toBe("textarea");
    expect(seen.config.inputAttributes.rows).toBe(3);
  });

  it("keeps the label, the validator and the required check exactly as they were", () => {
    const { seen, stub } = captureConfig();
    let validated = null;
    loadWrapper(stub)("Suspend", () => {}, {
      showInput: true, inputLabel: "Reason", inputRequired: true,
      inputValidator: (v) => { validated = v; return v === "bad" ? "no" : null; }
    });
    expect(seen.config.inputLabel).toBe("Reason");
    expect(typeof seen.config.preConfirm).toBe("function");
    seen.config.preConfirm("ok");
    expect(validated).toBe("ok");
    expect(seen.config.preConfirm("bad")).toBe(false);
  });
});

describe("a caller that does name a type", () => {
  it("gets that type instead", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { showInput: true, inputType: "text" });
    expect(seen.config.input).toBe("text");
  });

  it("keeps everything else the wrapper already gave it", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, {
      showInput: true, inputType: "text", subtext: "what this does", entityName: "Q3",
      confirmButtonText: "Ertele", cancelButtonText: "Vazgeç", width: "400px"
    });
    expect(seen.config.html).toContain("what this does");
    expect(seen.config.html).toContain("Q3");
    expect(seen.config.confirmButtonText).toBe("Ertele");
    expect(seen.config.cancelButtonText).toBe("Vazgeç");
    expect(seen.config.showCancelButton).toBe(true);
  });

  it("reaches the caller's didOpen with the popup", () => {
    const { seen, stub } = captureConfig();
    let opened = null;
    loadWrapper(stub)("Snooze", () => {}, { showInput: true, inputType: "text", didOpen: (p) => { opened = p; } });
    seen.config.didOpen({ tag: "popup" });
    expect(opened).toEqual({ tag: "popup" });
  });
});


describe("the dialog is written in the product's type scale", () => {
  /*
   * MEASURED, on a rendered page, before any change: the description printed at 18px, the input label at 16px
   * and the title at 24px. The product itself writes body copy at 13px and secondary copy at 12px — a census of
   * the detail page's 134 text nodes put 41 at 13px and 46 at 12px, with the page's own title at 18px.
   *
   * Nothing here invents a size. Each value below is a class the theme already ships:
   *   fs-5   → 18px, the theme's own h5 and the size a page titles itself with
   *   small  → 13px, the same size `.form-label` and `.backbone-preview-description` already use
   *   form-label → the product's form label, on every create form in the product
   *
   * ⚠ THIS FILE IS EVERY MODAL IN THE PRODUCT, which is why these are pinned here rather than in a module test.
   */
  it("titles at the product's heading size, not a size of its own", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, {});
    expect(seen.config.customClass.title).toContain("fs-5");
    expect(seen.config.customClass.title, "24px — louder than the page behind it").not.toContain("fs-4");
  });

  it("writes its description at the product's body size", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { subtext: "what this does" });
    // MUTATION GUARD: drop `small` and the description goes back to the browser default of 18px — bigger than
    // the page title it opens over, on every modal in the product.
    expect(seen.config.html).toMatch(/class="[^"]*\bsmall\b[^"]*"/);
    expect(seen.config.html).toContain("what this does");
  });

  it("labels its input the way every form in the product labels a field", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, { showInput: true, inputLabel: "Until" });
    expect(seen.config.customClass.inputLabel).toBe("form-label");
  });

  it("leaves the buttons, the icon and the width alone — not this round's subject", () => {
    const { seen, stub } = captureConfig();
    loadWrapper(stub)("Snooze", () => {}, {});
    expect(seen.config.customClass.confirmButton).toContain("px-5");
    expect(seen.config.customClass.cancelButton).toContain("btn-label-secondary");
    expect(seen.config.customClass.icon).toContain("d-flex");
    expect(seen.config.width).toBe("400px");
  });
});
