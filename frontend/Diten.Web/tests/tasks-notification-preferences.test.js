const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-065 — the email card's two preferences, on the form side.
 *
 * The rule this suite exists to hold: the card offers ONLY events that can actually be sent. Before this round
 * `duesoon` was a code in the manifest with no sender anywhere in the repository, so listing it would have been a
 * checkbox promising an email nothing produces — the defect this project has now corrected five times. It is
 * listed here because the sweep that sends it landed in the same change.
 *
 * The other rule: the master switch owns the card. Nobody should be tuning which events reach a channel that is
 * switched off.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const TASK_FORM = () => read("Views", "Tasks", "_Form.cshtml");

/** The event codes the SERVER declares — parsed, never restated. */
const serverEventCodes = () => {
  const source = read("..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
    "Features", "Tasks", "TaskModels.cs");
  const block = source.slice(source.indexOf("class TaskNotificationEvents"));
  return [...block.slice(0, block.indexOf("}")).matchAll(/"([^"]+)"/g)].map((m) => m[1]);
};

const loadForm = () => {
  delete global.TaskForm;
  loadScript("wwwroot/assets/js/Tasks/form.js");
  return global.TaskForm;
};

describe("the email card offers the events that exist, and only those", () => {
  test("every offered event is one the server declares", () => {
    const form = TASK_FORM();
    const offered = [...form.matchAll(/name="notifyOnEvents"[^>]*value="([^"]+)"/g)].map((m) => m[1]);

    expect(offered.length, "the card offers no events at all").toBeGreaterThan(0);
    const declared = serverEventCodes();
    const unknown = offered.filter((code) => !declared.includes(code));
    expect(unknown, `offered but not declared by the server:\n${unknown.join("\n")}`).toHaveLength(0);
  });

  test("the due-soon reminder is offered, because something now sends it", () => {
    const form = TASK_FORM();
    expect(form).toMatch(/value="platform\.tasks\.duesoon"/);

    // The sender is what earns it a place on the card. Without this file the checkbox would be a promise.
    const handler = read("..", "..", "services", "Diten.Platform", "src", "Diten.Platform.Application",
      "Features", "Tasks", "Handlers", "CommandHandlers", "SendDueSoonRemindersHandler.cs");
    expect(handler).toContain("TaskNotificationEvents.DueSoon");
  });

  test("the preferences hide with the master switch, and the lead time with its own event", () => {
    const form = TASK_FORM();
    // Class-based toggling only (FG-003) — the same data-task-field mechanism the rest of the form uses.
    expect(form).toMatch(/data-task-field="notificationPrefs"/);
    expect(form).toMatch(/data-task-field="reminderLead"/);

    const source = read("wwwroot", "assets", "js", "Tasks", "form-page.js");
    expect(source, "nothing toggles the preferences with the switch").toContain("taskEmailNotifications");
    expect(source, "the lead time is not tied to the duesoon choice").toContain("reminderLead");
  });

  test("each event is a boxed choice with its own icon, like the review types", () => {
    /*
     * The five events were a plain stack of checkboxes — a wall of text with nothing to scan by. They are the
     * same KIND of thing the review type is (pick from a short list, each with a meaning), so they get the same
     * box: `.choice-box`, already in the stylesheet from that round. One visual language, not two.
     *
     * The icon is what makes the list scannable: a deadline, a completion and an approval request read as
     * different events at a glance instead of as five identical rows.
     */
    // Split on the box opening rather than trying to match a closing tag: the review-type boxes wrap their text
    // in a body div and the event boxes do not, so any single closing pattern is wrong for one of them.
    const form = TASK_FORM();
    const boxes = form.split('<div class="form-check choice-box').slice(1)
      .filter((box) => box.slice(0, box.indexOf("</div>")).includes('name="notifyOnEvents"'));

    expect(boxes.length, "the events are not boxed choices").toBe(5);
    boxes.forEach((box) => {
      const head = box.slice(0, box.indexOf("</div>"));
      const code = /value="([^"]+)"/.exec(head)[1];
      expect(head, `${code} has no icon`).toMatch(/<i class="bx /);
    });
  });

  test("every icon the form asks for actually EXISTS in the icon set", () => {
    /*
     * `bx-hand-right` was picked from memory of the boxicons catalogue. This project ships ICONIFY, whose set is
     * not identical, and the missing glyph rendered as a blank 17×17 gap — no error, no warning, just a hole in
     * one row of five. Every icon name in the form is therefore checked against the stylesheet that has to draw
     * it, so the next confident guess fails here instead of on screen.
     */
    const icons = read("wwwroot", "assets", "vendor", "fonts", "iconify-icons.css");
    const used = [...new Set([...TASK_FORM().matchAll(/class="bx (bx-[a-z0-9-]+)/g)].map((m) => m[1]))];

    expect(used.length, "the form uses no icons at all").toBeGreaterThan(4);
    const missing = used.filter((name) => !icons.includes(`.${name}`));
    expect(missing, `icon names with nothing to draw them:\n${missing.join("\n")}`).toHaveLength(0);
  });

  test("a hoverable box borrows the Task Center list row's hover, not a new one", () => {
    // The list rows in the Task Center already answer "what does hovering a selectable row look like here".
    const css = read("wwwroot", "assets", "css", "backbone-custom.css");
    // Anchored at line start: a skin override (`:root[data-skin=bordered] .wcn-row:hover`) appears first and
    // carries no background, so a plain indexOf finds the wrong rule and reads nothing.
    const rowStart = css.search(/^\.wcn-row:hover \{/m);
    expect(rowStart, "the Task Center row hover moved").toBeGreaterThan(-1);
    const rowRule = css.slice(rowStart, css.indexOf("}", rowStart));
    const tint = /background-color:\s*([^;]+);/.exec(rowRule)[1].trim();

    const boxHover = css.slice(css.indexOf(".choice-box:hover"));
    expect(boxHover.slice(0, boxHover.indexOf("}")), "the box invented its own hover tint").toContain(tint);
  });

  test("the event list is compact — five boxes must not out-shout the card", () => {
    // The review type has two options and room to breathe; five events at the same padding would take over the
    // column. A modifier, so the compact size is a documented variant rather than an override somewhere.
    const css = read("wwwroot", "assets", "css", "backbone-custom.css");
    expect(css, "there is no compact variant of the boxed choice").toMatch(/\.choice-box--compact\s*\{/);
    expect(TASK_FORM()).toMatch(/choice-box choice-box--compact/);
  });

  test("a reminder lead time is a NUMBER of days, never a date", () => {
    // BL-030: a stored instant serialises as an array and breaks the query. The control offers day counts.
    const form = TASK_FORM();
    const values = [...form.matchAll(/name="reminderLeadDays"[\s\S]*?<\/select>/g)][0][0];
    const options = [...values.matchAll(/value="(\d*)"/g)].map((m) => m[1]);

    expect(options.length).toBeGreaterThan(1);
    options.filter((v) => v.length > 0).forEach((v) => expect(Number.isInteger(Number(v))).toBe(true));
  });
});

describe("the preferences travel to the API and come back", () => {
  test("the payload carries the chosen events and the lead time", () => {
    const TaskForm = loadForm();

    const payload = TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned",
      emailNotificationsEnabled: true,
      notifyOnEvents: ["platform.tasks.assigned", "platform.tasks.duesoon"],
      reminderLeadDays: "3"
    });

    expect(payload.notifyOnEvents).toEqual(["platform.tasks.assigned", "platform.tasks.duesoon"]);
    expect(payload.reminderLeadDays, "the lead time must be a number on the wire, not a string").toBe(3);
  });

  test("a switched-off channel sends no preferences to tune", () => {
    /*
     * With the master switch off the card is hidden, so whatever the boxes happen to hold is not a choice the
     * user made. Sending it would store a preference nobody expressed.
     */
    const TaskForm = loadForm();

    const payload = TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned",
      emailNotificationsEnabled: false,
      notifyOnEvents: ["platform.tasks.assigned"],
      reminderLeadDays: 3
    });

    expect(payload.notifyOnEvents).toBeNull();
    expect(payload.reminderLeadDays).toBeNull();
  });

  test("no lead time chosen travels as null, not as zero", () => {
    // Zero is a real answer — "on the day" — so an empty control must not collapse into it.
    const TaskForm = loadForm();

    const payload = TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned",
      emailNotificationsEnabled: true, notifyOnEvents: [], reminderLeadDays: ""
    });

    expect(payload.reminderLeadDays).toBeNull();
    expect(TaskForm.buildCreatePayload({
      title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned",
      emailNotificationsEnabled: true, notifyOnEvents: [], reminderLeadDays: "0"
    }).reminderLeadDays).toBe(0);
  });

  test("the edit form ticks the stored events and selects the stored lead time", () => {
    const TaskForm = loadForm();
    document.body.innerHTML = `
      <input type="checkbox" name="notifyOnEvents" value="platform.tasks.assigned" />
      <input type="checkbox" name="notifyOnEvents" value="platform.tasks.duesoon" />
      <select id="taskReminderLeadDays" name="reminderLeadDays">
        <option value=""></option><option value="3">3</option>
      </select>`;

    TaskForm.applyNotificationPreferences(document, ["platform.tasks.duesoon"], 3);

    const ticked = [...document.querySelectorAll('[name="notifyOnEvents"]')]
      .filter((box) => box.checked)
      .map((box) => box.value);
    expect(ticked).toEqual(["platform.tasks.duesoon"]);
    expect(document.getElementById("taskReminderLeadDays").value).toBe("3");
  });

  test("a task whose owner never chose shows every event ticked", () => {
    /*
     * Null means "never chosen", and the server treats that as "every event". The form has to SHOW that, or the
     * user opens a task that emails about everything and sees nothing ticked.
     */
    const TaskForm = loadForm();
    document.body.innerHTML = `
      <input type="checkbox" name="notifyOnEvents" value="platform.tasks.assigned" />
      <input type="checkbox" name="notifyOnEvents" value="platform.tasks.duesoon" />
      <select id="taskReminderLeadDays" name="reminderLeadDays"><option value=""></option></select>`;

    TaskForm.applyNotificationPreferences(document, null, null);

    const ticked = [...document.querySelectorAll('[name="notifyOnEvents"]')].filter((b) => b.checked);
    expect(ticked).toHaveLength(2);
  });
});

/*
 * The clearing path, from the form's side.
 *
 * The server tells "not editing the preferences" from "clearing them" by whether notifyOnEvents travels: inside
 * such an edit the lead time is applied verbatim, null included. So an EDIT must always carry the event list,
 * or choosing "no reminder" silently changes nothing — which is exactly what shipped and what the owner hit.
 */
describe("an edit can switch the reminder off", () => {
  const loadForm = () => {
    delete global.TaskForm;
    loadScript("wwwroot/assets/js/Tasks/form.js");
    return global.TaskForm;
  };

  const draft = (over) => ({
    title: "t", dueAt: "2026-09-01", assignmentTarget: "SelfAssigned",
    emailNotificationsEnabled: true, notifyOnEvents: ["platform.tasks.duesoon"], ...over
  });

  test("choosing no reminder sends the event list AND a null lead time", () => {
    const TaskForm = loadForm();

    const payload = TaskForm.buildUpdatePayload(draft({ reminderLeadDays: "" }), 4);

    expect(payload.expectedVersion).toBe(4);
    // Both together: the list is what marks the edit, the null is what clears the reminder.
    expect(payload.notifyOnEvents).toEqual(["platform.tasks.duesoon"]);
    expect(payload.reminderLeadDays).toBeNull();
  });

  test("an edit never sends a null event list while the channel is on", () => {
    // A null list would read as "not editing the block", and the cleared lead time would be ignored.
    const TaskForm = loadForm();

    const payload = TaskForm.buildUpdatePayload(draft({ notifyOnEvents: [], reminderLeadDays: "" }), 2);

    expect(payload.notifyOnEvents, "an empty choice must still travel as a list").toEqual([]);
  });

  test("with the channel off the preferences stay out of the payload", () => {
    const TaskForm = loadForm();

    const payload = TaskForm.buildUpdatePayload(
      draft({ emailNotificationsEnabled: false, reminderLeadDays: "3" }), 1);

    expect(payload.notifyOnEvents).toBeNull();
    expect(payload.reminderLeadDays).toBeNull();
  });

  test("switching the channel off KEEPS the stored preferences rather than clearing them", () => {
    /*
     * Deliberate, and pinned here so the next round does not read it as the bug it resembles.
     *
     * With the master switch off the card is hidden, so a null event list travels — which the server reads as
     * "not editing the preferences", leaving what was stored alone. Turning email back on therefore restores the
     * choices the owner made rather than a blank card. The alternative, clearing them on the way out, would make
     * a temporary silence destroy settings the user never revisited; "the master switch wins" is about DELIVERY,
     * not about erasing what to deliver.
     */
    const TaskForm = loadForm();

    const off = TaskForm.buildUpdatePayload(
      draft({ emailNotificationsEnabled: false, notifyOnEvents: [], reminderLeadDays: "" }), 1);

    // Nothing about the preferences is asserted by this payload — that is the point.
    expect(off.notifyOnEvents).toBeNull();
    expect(off.reminderLeadDays).toBeNull();
    expect(off.emailNotificationsEnabled, "the switch itself still travels").toBe(false);
  });
});
