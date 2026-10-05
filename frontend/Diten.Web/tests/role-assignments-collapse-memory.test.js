const fs = require("fs");
const path = require("path");

/*
 * Rİ4 (owner, 2026-10-02) — on the Role Permissions screen every grant redraws the list, and a redrawn group card
 * was born open: the reader closed the groups they were not working in, pressed one chip, and every group opened
 * again. The screen now remembers which modules the reader closed, for as long as the page lives.
 *
 * The production function is sliced out of the page script and run against cards built in this DOM — not a copy
 * of the rule.
 */
const SOURCE = fs.readFileSync(
  path.resolve(__dirname, "..", "wwwroot/assets/js/Governance/RoleAssignments/index.js"), "utf8");

const loadToggle = () => {
  const start = SOURCE.indexOf("const collapsedModules = new Set();");
  const end = SOURCE.indexOf("const renderGroupCard = (moduleKey, perms) =>");
  expect(start, "collapsedModules was not found in the page script").toBeGreaterThan(-1);
  expect(end).toBeGreaterThan(start);
  // eslint-disable-next-line no-new-func
  return new Function(`${SOURCE.slice(start, end)}; return { wireGroupToggle, collapsedModules };`)();
};

// What renderGroupCard hands to wireGroupToggle: a card that already carries its module, born open.
const card = (moduleKey) => {
  const node = document.createElement("div");
  node.className = "ra-group";
  node.dataset.module = moduleKey;
  node.innerHTML = '<div class="ra-group-toggle" role="button" tabindex="0" aria-expanded="true"></div>';
  return node;
};
const isClosed = (node) => node.classList.contains("ra-collapsed");

describe("the Role Permissions screen remembers which groups the reader closed", () => {
  it("a group closed by the reader is drawn closed again after the list is redrawn", () => {
    const { wireGroupToggle } = loadToggle();
    const first = card("crm");
    wireGroupToggle(first);
    expect(isClosed(first), "a fresh page starts open").toBe(false);

    first.querySelector(".ra-group-toggle").click();
    expect(isClosed(first)).toBe(true);

    // renderList throws the cards away and builds new ones — that is what a grant does.
    const redrawn = card("crm");
    wireGroupToggle(redrawn);

    expect(isClosed(redrawn)).toBe(true);
    expect(redrawn.querySelector(".ra-group-toggle").getAttribute("aria-expanded")).toBe("false");
  });

  it("a group the reader left open stays open, and one reopened is forgotten", () => {
    const { wireGroupToggle, collapsedModules } = loadToggle();
    const crm = card("crm");
    const tasks = card("tasks");
    wireGroupToggle(crm);
    wireGroupToggle(tasks);

    crm.querySelector(".ra-group-toggle").click();           // closed
    crm.querySelector(".ra-group-toggle").click();           // opened again
    tasks.querySelector(".ra-group-toggle").dispatchEvent(new KeyboardEvent("keydown", { key: "Enter" }));

    expect([...collapsedModules]).toEqual(["tasks"]);

    const crmAgain = card("crm");
    const tasksAgain = card("tasks");
    wireGroupToggle(crmAgain);
    wireGroupToggle(tasksAgain);
    expect(isClosed(crmAgain)).toBe(false);
    expect(isClosed(tasksAgain)).toBe(true);
  });

  it("the memory is the page's, not the browser's: nothing is written to storage", () => {
    const slice = SOURCE.slice(SOURCE.indexOf("const collapsedModules = new Set();"), SOURCE.indexOf("const renderGroupCard = (moduleKey, perms) =>"));

    expect(slice).not.toMatch(/localStorage|sessionStorage/);
  });
});
