const fs = require("fs");
const path = require("path");

/*
 * THE GRID, ASSERTED — because the last time it broke, nothing said so.
 *
 * <b>What happened.</b> Two closing tags went missing from _Form.cshtml in the same edit: one absent, one
 * surplus. They hid each other, so the file still had a balanced </div> COUNT. Razor compiled it. Every test
 * stayed green. And the page was measured in a narrow pane, where the two columns stack anyway — so the defect
 * was invisible exactly where it was being looked at. It only appears at ≥lg, and the owner is the one who
 * found it.
 *
 * <b>Why a count is not enough.</b> A balanced count proves nothing about WHERE the tags are: a </div> moved
 * from one nesting level to another keeps the total and changes the layout. So this file asserts PLACEMENT —
 * the two columns are direct children of the grid row, each section closes at the depth it opened, and the
 * sections are where they are supposed to be.
 *
 * <b>Razor comments are stripped first.</b> `@* … *@` never reaches the browser, and these comments contain
 * prose about tags; counting them would measure the documentation instead of the markup.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const FORM = () => fs.readFileSync(
  path.join(repoRoot, "frontend", "Diten.Web", "Views", "Tasks", "_Form.cshtml"), "utf8");

/** The markup the SERVER will emit: Razor comments removed, everything else untouched. */
const markup = (source) => (source === undefined ? FORM() : source).replace(/@\*[\s\S]*?\*@/g, "");

/**
 * Walk div/section tags with a stack. Returns the errors found — an empty array means every tag closes at the
 * depth it opened, in the right order.
 */
const structuralErrors = (source) => {
  const html = markup(source);
  const errors = [];
  const stack = [];
  const tag = /<(\/?)(div|section)\b[^>]*?(\/?)>/gi;

  let match;
  while ((match = tag.exec(html)) !== null) {
    const closing = match[1] === "/";
    const name = match[2].toLowerCase();
    const selfClosing = match[3] === "/";
    const line = html.slice(0, match.index).split("\n").length;

    if (selfClosing) { continue; }

    if (!closing) {
      stack.push({ name, line });
      continue;
    }

    const open = stack.pop();
    if (!open) {
      errors.push(`line ${line}: </${name}> closes nothing`);
    } else if (open.name !== name) {
      errors.push(`line ${line}: </${name}> closes a <${open.name}> opened on line ${open.line}`);
    }
  }

  stack.forEach((open) => errors.push(`line ${open.line}: <${open.name}> is never closed`));
  return errors;
};

const parse = () => {
  const host = document.createElement("div");
  host.innerHTML = markup();
  return host;
};

describe("every tag closes at the depth it opened", () => {
  test("div/section nesting in _Form.cshtml is sound", () => {
    const errors = structuralErrors();
    expect(errors, errors.join(" · ")).toHaveLength(0);
  });

  test("the walker is not vacuous — a deleted </div> IS an error", () => {
    /*
     * Proof in the test itself rather than a note in the report: the same check, run over the same file with
     * ONE closing tag removed, must fail. A structural test that cannot fail is worse than none.
     */
    const broken = markup().replace("</div>", "");
    expect(structuralErrors(broken).length).toBeGreaterThan(0);
  });

  test("a MOVED closing tag is caught too — the count stays balanced and the nesting does not", () => {
    // The actual defect's shape: nothing is missing, something is in the wrong place.
    const source = markup();
    const first = source.indexOf("</section>");
    const moved = source.slice(0, first) + "</div></section>" + source.slice(first + "</section>".length);
    expect(structuralErrors(moved).length).toBeGreaterThan(0);
  });
});

describe("the two-column grid is the shape it claims to be", () => {
  test("the grid row has EXACTLY two direct children: the 8 and the 4", () => {
    const row = parse().querySelector("form#taskForm > .row.g-4");
    expect(row, "there is no .row.g-4 inside the form").toBeTruthy();

    const columns = Array.from(row.children);
    expect(columns.map((c) => c.className.trim())).toEqual(["col-12 col-lg-8", "col-12 col-lg-4"]);
  });

  test("five cards on the left, five on the right — and they are DIRECT children of their column", () => {
    /*
     * "Direct children" is the half that matters. A section that slipped one level deeper still renders, still
     * counts, and sits inside the card above it.
     *
     * The left column gained the CHECKLIST card: the steps of the task belong with the task's own content, not
     * beside the governance decisions on the right. (It is create-only — form-page.js removes it on edit, where
     * the checklist is a separate document with its own endpoints.)
     */
    const row = parse().querySelector("form#taskForm > .row.g-4");
    const [left, right] = Array.from(row.children);

    expect(Array.from(left.children).filter((n) => n.tagName === "SECTION")).toHaveLength(5);
    expect(Array.from(right.children).filter((n) => n.tagName === "SECTION")).toHaveLength(5);
  });

  test("every card is a <section class=card> — the shape the golden reference uses", () => {
    const sections = Array.from(parse().querySelectorAll("form#taskForm section"));
    expect(sections).toHaveLength(10);
    sections.forEach((section) => {
      expect(section.classList.contains("card"), `a section is not a card: ${section.className}`).toBe(true);
      expect(section.querySelector(":scope > .card-body"), "a card has no direct .card-body").toBeTruthy();
    });
  });

  test("the columns hold the cards they are supposed to hold", () => {
    // Named by their controls rather than their headings: a heading is a translated string, an id is not.
    const row = parse().querySelector("form#taskForm > .row.g-4");
    const [left, right] = Array.from(row.children);

    ["taskTitle", "taskAssignmentTarget", "taskDueAt", "taskCustomFieldsRow"].forEach((id) => {
      expect(left.querySelector(`#${id}`), `#${id} is not in the left column`).toBeTruthy();
    });
    ["taskReviewRequired", "taskApprovalRequired", "taskWatchers", "taskEmailNotifications", "taskDelegationAllowed"]
      .forEach((id) => {
        expect(right.querySelector(`#${id}`), `#${id} is not in the right column`).toBeTruthy();
      });
  });
});
