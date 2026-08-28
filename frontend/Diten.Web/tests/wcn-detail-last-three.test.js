const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * THE DETAIL PAGE'S LAST THREE, all one class: one thing said too often, one said not at all, one said wrongly.
 *
 *   BL-208 — a disabled row in the narrow bar's menu was silent while the card explained the same action.
 *   BL-207 — the same subtask block was written three times on one page.
 *   BL-219 — "Onay toplantısı planla" claimed to book a calendar it has never been connected to.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
const value = (src, key) => src.split(`name="${key}"`)[1].split("</data>")[0];

const TASK_ID = "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0";
const CHILD_ID = "bd76df2e-8e4b-57d4-b4fa-0e9d65f7c3c1";
const OTHER_ID = "ce87ef3f-9f5c-68e5-c5fb-1f0e76f8d4d2";

const action = (code, over = {}) => Object.assign({
  code, label: { kind: "display", text: code, locale: "und" },
  semanticType: code, enabled: true, source: "provider",
  disabledReasonCode: null, disabledReason: null,
  requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
  supportsBulk: false, riskLevel: "normal"
}, over);

const item = (over = {}) => Object.assign({
  fixtureKind: "workItem",
  id: TASK_ID,
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: "InProgress",
  taskLifecycle: "InProgress",
  executionState: "active",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: "Ay sonu kapanışı", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
    objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}`
  },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "subtasks"],
  actions: [
    action("complete", { enabled: false, disabledReasonCode: "SUBTASK_BLOCKED", disabledReason: { kind: "display", text: "Bir alt görev hâlâ açık", locale: "und" } }),
    action("inquire"),
    // The one CT photographed: dimmed in the menu, explained only in the card.
    action("reassign", { enabled: false, disabledReasonCode: "TASK_REASSIGN_NOT_PERMITTED", disabledReason: { kind: "display", text: "Bu görev devredilemez.", locale: "und" } }),
    action("cancel", { riskLevel: "destructive" })
  ],
  primaryActionCode: "complete",
  overflowActionCodes: [],
  concurrency: { kind: "version", token: "3" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-09-01T00:00:00+00:00",
  subtasks: { mode: "full", items: [{ id: CHILD_ID, title: "Bakiyeleri aktar", status: "not-started" }] },
  blockedState: {
    blocked: true,
    affectedActionCodes: ["complete"],
    blockers: [{
      code: "SUBTASK_BLOCKED",
      label: { kind: "display", text: "Bakiyeleri aktar", locale: "und" },
      taskItemId: CHILD_ID, dependencyType: null, affectedActionCode: "complete"
    }]
  }
}, over);

const boot = (payload) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`, items: [payload || item()]
});

describe("BL-208 — a dimmed row in the narrow bar's menu says why", () => {
  /*
   * MEASURED CAUSE (2026-08-25), and it was neither "not passed" nor "drawn then hidden" in the way the brief
   * guessed: the sticky bar's menu never called `actionMenuLi` at all. It hand-rolled its own
   * `<li><button class="dropdown-item">` carrying the LABEL ONLY, so there was no reason to hide — a second
   * render path that had simply never been taught the concept. `wcn-menu-reason` was drawing correctly the
   * whole time, in every OTHER menu in the app.
   */
  it("routes the bar's menu through the one shared row", async () => {
    // MUTATION GUARD: hand-roll the row again and this goes red.
    expect(APP, "the bar builds its own menu row again")
      .toContain("${rest.map((a) => actionMenuLi(item, a, locked)).join('')}");
  });

  it("prints the reason under the label, and addresses it to that row", async () => {
    await boot();
    const bar = app().querySelector(".wcn-actionbar");
    expect(bar, "the narrow bar is not drawn").not.toBeNull();
    const row = [...bar.querySelectorAll("[data-wcn-action]")]
      .find((b) => b.getAttribute("data-wcn-action") === "reassign");
    expect(row, "the disabled action is missing from the menu").not.toBeNull();

    const reason = row.querySelector(".wcn-menu-reason");
    // MUTATION GUARD: drop the reason from actionMenuLi and this goes red.
    expect(reason, "the dimmed row is silent again").not.toBeNull();
    expect(reason.textContent).toContain("Bu görev devredilemez.");
    expect(row.getAttribute("aria-describedby"), "the sentence is near the row but not addressed to it")
      .toBe(reason.id);
    expect(reason.id, "the sentence has no id to be addressed by").not.toBe("");
  });

  it("gives the menu's sentence an id no other surface claims", async () => {
    await boot();
    const ids = [...app().querySelectorAll("[id]")].map((e) => e.id).filter((id) => id.includes("actreason"));
    /*
     * The detail page can draw ONE action three times: the card's rail, the bar's lead button and this menu.
     * MUTATION GUARD: drop the `-menu` suffix and this goes red on the duplicate, exactly as the `-bar` suffix
     * was added to prevent a round earlier.
     */
    expect(ids.length, "no reason ids at all").toBeGreaterThan(0);
    expect(new Set(ids).size, `duplicate reason ids: ${ids.join(", ")}`).toBe(ids.length);
    expect(APP).toContain("`${actionReasonId(item, action)}-menu`");
  });
});

describe("BL-207 — the subtask block is stated where it can be acted on, not three times", () => {
  it("draws no banner when every blocker is a subtask", async () => {
    await boot();
    /*
     * MUTATION GUARD: let SUBTASK_BLOCKED back into the banner and this goes red.
     *
     * The other two tellings stay and are asserted below: they answer questions ("why is this button dead",
     * "which child") and they stand where the answer can be acted on. The banner answered neither and its
     * "Alt görevlere git" link scrolled to a card already on the screen.
     */
    expect(app().querySelector(".wcn-blocked"), "the third telling is back").toBeNull();
  });

  it("still says it in the two places that stand beside the fix", async () => {
    await boot();
    const btn = app().querySelector('[data-wcn-action="complete"]');
    expect(btn.disabled).toBe(true);
    const said = app().textContent;
    // The action's own sentence, at the button.
    expect(said).toContain("Bir alt görev hâlâ açık");
    // And the child itself, by name, with its checkbox — the place the block is cleared.
    expect(app().querySelector("[data-wcn-subtask]"), "no way to clear it").not.toBeNull();
    expect(said).toContain("Bakiyeleri aktar");
  });

  it("KEEPS the banner for a dependency block, which owns no such card", async () => {
    /*
     * MEASURED: the provider emits exactly two blocker codes — SUBTASK_BLOCKED and DEPENDENCY_BLOCKED
     * (`ResolveBlockers`). Checklist, approval and review are Gates and never reach `blockedState` at all.
     *
     * The Dependencies card shows the relationship and the other task's state, but never says WHICH ACT IS
     * STOPPED right now — that clause (`BlockedAffects*`) exists only in the banner. Dropping it would delete a
     * fact rather than de-duplicate one.
     */
    await boot(item({
      blockedState: {
        blocked: true, affectedActionCodes: ["complete"],
        blockers: [{
          code: "DEPENDENCY_BLOCKED",
          label: { kind: "display", text: "Mizanı kapat", locale: "und" },
          taskItemId: OTHER_ID, dependencyType: "FinishToStart", affectedActionCode: "complete"
        }]
      }
    }));
    const banner = app().querySelector(".wcn-blocked");
    expect(banner, "a block with nowhere else to live was silenced too").not.toBeNull();
    // `tf` echoes the KEY in this harness (the name is its argument), so this asserts the sentence the code
    // reached for — and the clause that exists nowhere else on the page.
    expect(banner.textContent).toContain("BlockerFinishToStart");
    expect(banner.textContent, "the clause naming what is stopped is the banner's whole reason to stay")
      .toContain("BlockedAffectsComplete");
  });

  it("drops only the suppressed code from a mixed set, and keeps the rest", async () => {
    await boot(item({
      blockedState: {
        blocked: true, affectedActionCodes: ["complete"],
        blockers: [
          { code: "SUBTASK_BLOCKED", label: { kind: "display", text: "Bakiyeleri aktar", locale: "und" },
            taskItemId: CHILD_ID, dependencyType: null, affectedActionCode: "complete" },
          { code: "DEPENDENCY_BLOCKED", label: { kind: "display", text: "Mizanı kapat", locale: "und" },
            taskItemId: OTHER_ID, dependencyType: "FinishToStart", affectedActionCode: "complete" }
        ]
      }
    }));
    const banner = app().querySelector(".wcn-blocked");
    expect(banner).not.toBeNull();
    const rows = banner.querySelectorAll(".wcn-blocked-item");
    // One row, not two: the subtask left, the dependency stayed.
    expect(rows.length).toBe(1);
    expect(banner.textContent).toContain("BlockerFinishToStart");
    expect(banner.textContent, "the suppressed code came back in a mixed set")
      .not.toContain("BlockerSubtaskOpen");
  });

  it("has no leftovers from the one-line subtask banner it used to draw", () => {
    // The collapsed "1 alt görev kapanmadan tamamlanamaz" + "Alt görevlere git" branch, its click handler, its
    // CSS and its seven-language strings are all gone — a half-deletion reads as maintained code.
    ["allSubtasks", "BlockedSubtaskOneLine", "BlockedGoToSubtasks", "wcn-goto-subtasks", "wcn-blocked-oneline"]
      .forEach((needle) => { expect(APP, `${needle} survived`).not.toContain(needle); });
    LANGS.forEach((lang) => {
      expect(resx(lang), `${lang} still carries the deleted strings`).not.toContain("BlockedGoToSubtasks");
      expect(resx(lang)).not.toContain("BlockedSubtaskOneLine");
    });
  });
});

describe("BL-219 — the meeting dialog says what it actually does", () => {
  it("no longer claims to write to a calendar", () => {
    /*
     * MEASURED: `applyReviewMeeting` pushes onto `state.meetings` and re-projects `item._fixture`. Nothing
     * reaches the server; the record is gone on reload. The dialog said "İnceleme toplantısını takvime yazar".
     *
     * MUTATION GUARD: restore the old sentence in any language and this goes red.
     *
     * ⚠ THE ACTION IS NOT DELETED, unlike the ticker and `pause`: it has a declared contract
     * (`reviewMeetingPolicy`, WorkAggregationModels.cs:832). Stop lying; do not write the feature.
     */
    const claims = {
      tr: "takvime yazar", en: "Books the review meeting", fr: "Planifie la réunion",
      es: "Reserva la reunión", zh: "写入日历", ar: "يحجز اجتماع", ru: "Записывает встречу"
    };
    LANGS.forEach((lang) => {
      const v = value(resx(lang), "MeetingWhenSubtext");
      expect(v, `${lang} still claims the calendar booking`).not.toContain(claims[lang]);
      expect(v.length, `${lang} says nothing`).toBeGreaterThan(20);
    });
  });

  it("names all three facts, in all seven languages", () => {
    // What it is: not connected · where it lives: this screen · how long: until you close it.
    const marks = {
      tr: ["Takvim", "bu ekranda", "kaybolur"],
      en: ["calendar", "this screen", "lost"],
      fr: ["calendrier", "cet écran", "disparaît"],
      es: ["calendario", "esta pantalla", "se pierde"],
      zh: ["日历", "本页面", "消失"],
      ar: ["التقويم", "هذه الشاشة", "يُفقد"],
      ru: ["календар", "этом экране", "исчезнет"]
    };
    LANGS.forEach((lang) => {
      const v = value(resx(lang), "MeetingWhenSubtext");
      marks[lang].forEach((mark) => {
        expect(v, `${lang} does not say "${mark}"`).toContain(mark);
      });
    });
  });
});
