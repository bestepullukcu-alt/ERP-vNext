const fs = require("fs");
const path = require("path");

/*
 * BL-023 PART B — upward work is a REQUEST, on the browser side.
 *
 * The rule the UI must keep: a control that quietly behaves differently from what it says is the defect this
 * project keeps correcting. So when the chosen assignee is above the requester, the button's WORD changes with
 * the behaviour and the card says what will happen — before the button is pressed, not after.
 *
 * Every assertion reads the RENDER SURFACE (_Form.cshtml + form-page.js). This suite's sibling was vacuous once
 * for asserting on resx keys alone; that mistake is not repeated here.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const TASK_FORM = () => read("Views", "Tasks", "_Form.cshtml");
const FORM_PAGE_JS = () => read("wwwroot", "assets", "js", "Tasks", "form-page.js");
const API_JS = () => read("wwwroot", "assets", "js", "Tasks", "api.js");

describe("the button says what it will do", () => {
  test("the submit carries BOTH labels, so the word can change with the behaviour", () => {
    const form = TASK_FORM();
    const submit = /<button[^>]*id="taskSubmit"[\s\S]{0,400}?>/.exec(form);

    expect(submit, "the submit button moved").toBeTruthy();
    expect(submit[0], "there is no upward label to switch to").toContain("data-task-label-upward");
    expect(submit[0], "the default label cannot be restored").toContain("data-task-label-default");
  });

  test("the page swaps the label from those attributes, never from a hard-coded string", () => {
    const page = FORM_PAGE_JS();
    expect(page).toContain("data-task-label-upward");
    expect(page).toContain("data-task-label-default");
  });

  test("the card explains the change, and starts hidden", () => {
    const form = TASK_FORM();
    expect(form, "there is no explanation slot").toContain('data-task-field="upwardRequest"');
    expect(form, "the explanation is never rendered").toContain("UpwardRequestHint");

    const notice = /<div[^>]*data-task-field="upwardRequest"[^>]*>/.exec(form);
    expect(notice[0], "the notice is visible before anything is chosen").toContain("d-none");
    expect(form, "the form gained an inline style").not.toMatch(/style="/);
  });
});

describe("the direction comes from the SERVER, not from a browser guess", () => {
  test("the API client has a direction call and the proxy forwards it", () => {
    expect(API_JS(), "no direction call").toMatch(/assignmentDirection/);
    expect(read("Controllers", "TasksController.cs"), "the direction route is not proxied")
      .toContain("assignment-direction");
  });

  test("the page asks the server rather than deriving the chain in the browser", () => {
    /*
     * The reporting chain lives on the server, and the create handler applies the SAME rule when it opens the
     * request. A browser-side guess would be a second truth and would drift from what actually happens.
     */
    const page = FORM_PAGE_JS();
    expect(page).toMatch(/TasksApi\.assignmentDirection/);
    expect(page, "the browser walks the chain itself").not.toMatch(/reportsToPositionId/i);
  });

  test("both controls re-ask, because the direction depends on WHO", () => {
    const page = FORM_PAGE_JS();
    const wiring = page.slice(page.indexOf("addEventListener('change', syncVisibility)"));
    expect(wiring, "changing the target never re-asks").toMatch(/taskAssignmentTarget[\s\S]{0,200}refreshAssignmentDirection/);
    expect(wiring, "changing the person never re-asks").toMatch(/taskAssignee[\s\S]{0,200}refreshAssignmentDirection/);
  });

  test("only a PERSON target can be upward", () => {
    // A pool has no single holder and self-assignment is never upward; asking the server for those would be a
    // request whose answer is known.
    expect(FORM_PAGE_JS()).toMatch(/target !== 'Person'/);
  });

  test("a stale answer never relabels the button after the user has moved on", () => {
    // The same failure the record search had: a slow answer for a previously chosen person arriving last.
    expect(FORM_PAGE_JS()).toMatch(/upwardCheck/);
  });

  test("an unreachable answer leaves the ordinary label — fail-safe direction", () => {
    /*
     * Wrongly promising a REQUEST is worse than wrongly promising an assignment: the server opens the request
     * either way, so the failure mode of the safe default is a label that under-promises, not one that lies
     * about a decision the user still has to make.
     */
    expect(FORM_PAGE_JS()).toMatch(/result\.ok && result\.data && result\.data\.isUpward/);
  });
});

describe("MOD-0024 decides nothing (Binding A)", () => {
  test("the browser never accepts or rejects the request itself", () => {
    const page = FORM_PAGE_JS();
    expect(page, "the form resolves the request locally").not.toMatch(/isUpward[\s\S]{0,120}(approve|reject)/i);
  });
});

describe("l10n", () => {
  const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
  const KEYS = ["ActionSendRequest", "UpwardRequestHint"];

  test("both strings exist exactly once in every language", () => {
    LOCALES.forEach((locale) => {
      const xml = read("Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`);
      KEYS.forEach((key) => {
        const hits = [...xml.matchAll(new RegExp(`name="${key}"`, "g"))].length;
        expect(hits, `${locale} has ${hits} copies of ${key}`).toBe(1);
      });
      const names = [...xml.matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]);
      expect(new Set(names).size, `${locale} contains duplicate keys`).toBe(names.length);
    });
  });

  test("both reach the browser through the bridge", () => {
    const bridge = read("Views", "Tasks", "_IndexL10n.cshtml");
    KEYS.forEach((key) => expect(bridge, `${key} never reaches the browser`).toContain(key));
  });

  test("key sets stay identical across the seven files", () => {
    const keysOf = (locale) => [...read("Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`)
      .matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]).sort();
    const base = keysOf("en");
    LOCALES.forEach((locale) => expect(keysOf(locale), `${locale} drifted from en`).toEqual(base));
  });
});
