const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * HANDING WORK ON — MOD-0024, owner report 2026-09-23.
 *
 * THE DEFECT. The edit form drew the assignment section, let the user pick a different person, and reported a
 * saved change. `UpdateTaskItemRequest` carries no assignment field — measured: twenty-one fields, Title and
 * Priority and DelegationAllowed among them, AssigneeUserId not — so the server dropped it every time. The
 * owner found it by trying to hand over a task he had assigned to himself.
 *
 * THE SHAPE OF THE CURE. A handover is an operation, not a field: `POST /{id}/reassign` already takes the new
 * person AND a mandatory reason, refuses a pool task, and admits only the current assignee or the requester.
 * So the edit form shows the assignment read-only and offers that operation — which also keeps the reason,
 * the thing a saveable field would have thrown away.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const KEYS = ["ReassignAction", "ReassignTitle", "ReassignHint", "ReassignAssigneeLabel",
  "ReassignAssigneePlaceholder", "ReassignReasonLabel", "ReassignReasonPlaceholder",
  "ReassignAssigneeRequired", "ReassignReasonRequired", "ReassignDone", "ReassignNoPeople",
  "AssignmentReadOnlyHint"];

describe("the edit form stops offering a change the server drops", () => {
  const form = () => read("Views", "Tasks", "_Form.cshtml");

  test("the editable assignment rows hide on edit — and stay in the DOM, so the payload is unchanged", () => {
    const source = form();
    expect(source).toMatch(/<div class="row g-3@\(isEdit \? " d-none" : ""\)" data-task-section="assignment-edit">/);
    // Removing them would empty the payload builder's draft and break saving the fields that DO save.
    expect(source, "the rows were deleted rather than hidden").toContain('id="taskAssignmentTarget"');
    expect(source).toContain('id="taskAssignee"');
    // ⚠ ONE CARD, NOT TWO: the two-column grid pins eleven sections (tasks-form-grid-structure), and the
    // read-only rows belong to the assignment card rather than beside it.
    expect(source).toMatch(/id="taskAssignmentSummary"/);
    expect(source, "the summary was given a card of its own").not.toMatch(/<section[^>]*id="taskAssignmentSummary"/);
  });

  test("edit shows the assignment read-only, in the product's immutable-field paint", () => {
    const source = form();
    const card = source.slice(source.indexOf('id="taskAssignmentSummary"'), source.indexOf("btnTaskReassign"));
    ["taskAssignmentTargetRead", "taskAssigneeRead"].forEach((id) => {
      expect(card, `${id} is missing`).toContain(id);
    });
    expect(card.match(/readonly/g)?.length, "a box is editable, or the paint is missing").toBe(2);
    expect(card.match(/bg-label-secondary/g)?.length).toBe(2);
    expect(source, "the read-only rows are drawn on create too").toMatch(/@if \(isEdit\)\s*\{\s*<div class="row g-3" id="taskAssignmentSummary">/);
  });

  test("the button starts hidden and the page loads the module that decides", () => {
    expect(form()).toMatch(/id="btnTaskReassign" class="btn btn-label-primary d-none"/);
    const edit = read("Views", "Tasks", "Edit.cshtml");
    const module = edit.indexOf("Tasks/reassign.js");
    const page = edit.indexOf("Tasks/form-page.js");
    expect(module, "the edit page never loads the handover module").toBeGreaterThan(-1);
    expect(page, "form-page.js calls mount, so it must come after").toBeGreaterThan(module);
    expect(read("wwwroot", "assets", "js", "Tasks", "form-page.js")).toContain("TaskReassign?.mount(taskId, existing.data)");
  });
});

describe("the button is offered only where the server would accept it", () => {
  let calls;

  const load = () => {
    ["TaskReassign"].forEach((key) => { delete global[key]; });
    loadScript("wwwroot/assets/js/Tasks/reassign.js");
    return global.window.TaskReassign;
  };

  beforeEach(() => {
    calls = [];
    document.body.innerHTML =
      '<input id="taskAssignmentTargetRead" /><input id="taskAssigneeRead" />' +
      '<select id="taskAssignee"><option value="u-1" selected>Metin Yıldız</option></select>' +
      '<select id="taskPoolPosition"><option value="p-1" selected>QA Müdürü</option></select>' +
      '<button id="btnTaskReassign" class="d-none"></button>';
    // camelCase, because that is what the serialized bridge payload delivers (tasks-localization).
    global.window.L10n = { targetSelf: "Kendim", targetPerson: "Bir kişi", targetPool: "Havuz" };
    global.window.CurrentUser = { id: "me", displayName: "Ali Tufanoğlu" };
    global.window.TasksApi = { assignablePeople: async () => ({ ok: true, data: [] }), transition: async (...a) => { calls.push(a); return { ok: true }; } };
  });

  const offered = () => !document.getElementById("btnTaskReassign").classList.contains("d-none");
  const task = (over) => Object.assign(
    { assignmentTarget: "Person", assigneeUserId: "me", createdByUserId: "someone", lifecycle: "InProgress", version: 3 }, over);

  test("offered to the person holding the task", () => {
    load().mount("t-1", task());
    expect(offered()).toBe(true);
  });

  test("offered to the person who created it — the server admits the requester too", () => {
    load().mount("t-1", task({ assigneeUserId: "other", createdByUserId: "me" }));
    expect(offered()).toBe(true);
  });

  test("NOT offered on a pool task — the server refuses those outright", () => {
    /*
     * ⚠ THE READER IS THE CREATOR HERE, ON PURPOSE. With a stranger in that seat the admission rule would hide
     * the button by itself and this test would pass with the pool rule deleted — measured: it did, until this
     * line. Only the pool rule can hide it now.
     */
    load().mount("t-1", task({ assignmentTarget: "PositionPool", assigneeUserId: null, createdByUserId: "me" }));
    expect(offered(), "a pool task offered a handover the server would refuse").toBe(false);
  });

  test("NOT offered on a closed task, nor to a bystander", () => {
    load().mount("t-1", task({ lifecycle: "Completed" }));
    expect(offered()).toBe(false);
    document.getElementById("btnTaskReassign").classList.add("d-none");
    load().mount("t-1", task({ assigneeUserId: "other", createdByUserId: "other" }));
    expect(offered(), "someone who is neither holder nor requester was offered the action").toBe(false);
  });

  test("the summary reads names, never identifiers", () => {
    load().mount("t-1", task());
    expect(document.getElementById("taskAssignmentTargetRead").value).toBe("Bir kişi");
    expect(document.getElementById("taskAssigneeRead").value).toBe("Metin Yıldız");
    // A self-assigned task names the reader, not their GUID.
    load().mount("t-1", task({ assignmentTarget: "SelfAssigned" }));
    expect(document.getElementById("taskAssigneeRead").value).toBe("Ali Tufanoğlu");
  });
});

describe("what the dialog sends is what the server's record takes", () => {
  test("the payload names exactly ExpectedVersion, AssigneeUserId and Reason", () => {
    const source = read("wwwroot", "assets", "js", "Tasks", "reassign.js");
    const call = source.slice(source.indexOf("TasksApi.transition("), source.indexOf("if (response.ok)"));
    expect(call).toContain("'reassign'");
    expect(call).toMatch(/expectedVersion: Number\(task\.version\)/);
    expect(call).toMatch(/assigneeUserId: result\.value\.to/);
    expect(call).toMatch(/reason: result\.value\.reason/);
  });

  test("neither the person nor the reason can be left out", () => {
    const source = read("wwwroot", "assets", "js", "Tasks", "reassign.js");
    const pre = source.slice(source.indexOf("preConfirm:"), source.indexOf("if (!result.isConfirmed"));
    expect(pre).toContain("reassignAssigneeRequired");
    expect(pre).toContain("reassignReasonRequired");
    // The server is still the authority; a refusal is shown as it came.
    expect(source).toMatch(/response\.errors && response\.errors\[0\]/);
  });

  test("⚠ the raw dialog names SweetAlert in full, so the product-wide guard can see it", () => {
    /*
     * An earlier draft aliased it (`const S = global.Swal`) and `dialog-one-implementation.test.js`, which
     * looks for `Swal.fire(`, did not report the file at all. The exception is licensed in two lists; a rename
     * must not be able to slip past either of them.
     */
    // Comments stripped first: the file EXPLAINS the alias it no longer uses, and a note about a mistake is
    // not the mistake.
    const source = read("wwwroot", "assets", "js", "Tasks", "reassign.js");
    const code = source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");
    expect(code).toMatch(/global\.Swal\.fire\(/);
    expect(code, "SweetAlert is hidden behind a short alias again").not.toMatch(/const\s+S\s*=\s*global\.Swal/);
  });

  test("the current holder is not offered as the new one", () => {
    const source = read("wwwroot", "assets", "js", "Tasks", "reassign.js");
    expect(source).toMatch(/filter\(\(person\) =>[\s\S]{0,120}!==\s*String\(task\.assigneeUserId/);
  });
});

describe("all seven languages carry the handover's words", () => {
  LANGS.forEach((lang) => {
    test(lang, () => {
      const resx = read("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`);
      KEYS.forEach((key) => {
        const m = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`).exec(resx);
        expect(m, `${key} missing in ${lang}`).toBeTruthy();
        expect(m[1].trim().length).toBeGreaterThan(0);
      });
    });
  });

  test("the six non-English files are translated, not copies", () => {
    const value = (resx, key) => new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`).exec(resx)[1];
    const en = read("Resources", "Views", "Tasks", "TasksIndex.en.resx");
    LANGS.filter((l) => l !== "en").forEach((lang) => {
      const resx = read("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`);
      const same = KEYS.filter((key) => value(resx, key) === value(en, key));
      expect(same, `${lang} carries untranslated English for: ${same.join(", ")}`).toEqual([]);
    });
  });

  test("the bridge publishes every key the dialog reads", () => {
    const bridge = read("Views", "Tasks", "_IndexL10n.cshtml");
    KEYS.forEach((key) => expect(bridge, `${key} is not bridged`).toMatch(new RegExp(`${key} = Localizer\\["${key}"\\]\\.Value`)));
  });

  test("the reason box asks for an EXAMPLE, not its own name repeated", () => {
    // The owner's placeholder rule: "örn. …" teaches; "Gerekçe" is the label again.
    const tr = read("Resources", "Views", "Tasks", "TasksIndex.tr.resx");
    const placeholder = /<data name="ReassignReasonPlaceholder"[^>]*>\s*<value>([^<]+)</.exec(tr)[1];
    expect(placeholder.toLowerCase()).toMatch(/örn\./);
  });
});
