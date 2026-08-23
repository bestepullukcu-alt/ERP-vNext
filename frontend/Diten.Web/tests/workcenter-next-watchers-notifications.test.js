const { bootSurface, app } = require("./wcn-boot");

/*
 * THE FOUR CREATE-TIME SETTINGS ON THE SUMMARY (2026-08-23).
 *
 * Watchers, the notification preferences and the reminder lead were collected by the create form and reached no
 * surface at all; the delegation policy is enforced on the SERVER and appears here only as a disabled action with
 * a reason, never as a row of its own. These tests pin the three decisions that are easy to undo by accident:
 * the watcher role is quiet text and not a chip, the two notification facts are ONE sentence, and a reminder is
 * never spoken while email is off.
 */
const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

const projectionItem = (overrides) => Object.assign({
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
  title: { kind: "display", text: "Yeni maliyet merkezi açılış talebi", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks",
    providerContractVersion: "1.0",
    objectType: "task",
    objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  // Something has to make the Summary print at all — the card's own "no fields, no card" rule. `summary` is the
  // projection's name for the description (measured in renderSummary, not guessed).
  summary: { kind: "display", text: "Talep, mali işler onayına bağlı.", locale: "und" },
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null
}, overrides);

/*
 * The translator echoes each key back WITH its arguments, because here the argument IS the thing under test: the
 * scope ("all events" / "none" / "2 of them") is a slot INSIDE the sentence pattern, and a translator that
 * printed the outer key alone would let the three answers collapse into one and still pass.
 */
const echoArgs = {
  t: (key) => key,
  tf: (key, ...args) => (args.length ? `${key}(${args.join("|")})` : key),
  tn: (key) => key
};

const bootDetail = (item) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item],
  wcn: echoArgs
});

const watcher = (name, role) => ({
  person: { id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1", displayName: name, isCurrentUser: false },
  role
});

describe("watchers on the summary", () => {
  it("draws no watcher field at all when nobody is watching", async () => {
    await bootDetail(projectionItem());
    // Non-vacuity first: the Summary really rendered — otherwise "no watcher list" would pass on a blank page.
    expect(app().textContent).toContain("Talep, mali işler onayına bağlı.");
    expect(app().textContent).toContain("DetailRequester");
    expect(app().querySelector(".wcn-watchers")).toBeNull();
    expect(app().textContent).not.toContain("DetailWatchers");
  });

  it("lists every watcher by name", async () => {
    await bootDetail(projectionItem({
      watchers: [watcher("Ayşe Yılmaz", "Watcher"), watcher("Mert Demir", "Consultant")]
    }));
    const names = [...app().querySelectorAll(".wcn-watcher-name")].map((n) => n.textContent);
    expect(names).toEqual(["Ayşe Yılmaz", "Mert Demir"]);
  });

  it("says the role QUIETLY and never as a chip", async () => {
    await bootDetail(projectionItem({ watchers: [watcher("Mert Demir", "Consultant")] }));
    const role = app().querySelector(".wcn-watcher-role");
    expect(role).not.toBeNull();
    // Consultant is the ONE role that adds something — the enum has exactly two (Watcher, Consultant), so there
    // is no third key to write and none was invented.
    expect(role.textContent).toBe("WatcherRoleConsultant");
    /*
     * MUTATION GUARD. On this page a chip is a SIGNAL — overdue, priority, blocked. A role changes nothing about
     * what the reader does next, so wearing the chip class would make it compete with the two marks that do.
     */
    expect(role.classList.contains("wcn-tag")).toBe(false);
    expect(app().querySelectorAll(".wcn-watcher .wcn-tag, .wcn-watcher .badge").length).toBe(0);
  });

  it("drops the role of a plain watcher, which would only repeat the heading", async () => {
    await bootDetail(projectionItem({ watchers: [watcher("Ayşe Yılmaz", "Watcher")] }));
    expect(app().querySelector(".wcn-watcher-role")).toBeNull();
  });

  it("states an unresolvable name instead of printing an id", async () => {
    await bootDetail(projectionItem({
      watchers: [{ person: { id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1", displayName: null }, role: "Watcher" }]
    }));
    const name = app().querySelector(".wcn-watcher-name").textContent;
    expect(name).toBe("PersonNameUnavailable");
    expect(name).not.toContain("aaaaaaaa");
  });
});

describe("the notification sentence", () => {
  const sentence = () => {
    const label = [...app().querySelectorAll(".backbone-preview-label")]
      .find((node) => node.textContent === "DetailNotifications");
    return label ? label.parentElement.querySelector(".backbone-preview-value").textContent : null;
  };

  it("is not drawn for a provider that expresses no preference", async () => {
    await bootDetail(projectionItem());
    expect(app().textContent).toContain("DetailRequester");
    expect(sentence()).toBeNull();
  });

  it("says only that email is off — never a reminder beside it", async () => {
    await bootDetail(projectionItem({
      notifications: { emailEnabled: false, events: ["Assigned"] },
      reminderLeadDays: 3
    }));
    /*
     * MUTATION GUARD, and the reason this is ONE row: "E-posta kapalı" beside "3 gün önce hatırlatır" reads as a
     * contradiction the reader has to resolve. Nothing is sent, so the lead is a setting with no effect.
     */
    expect(sentence()).toBe("NotificationsOff");
    // The lead must not survive anywhere in the row — not as a second clause, not as a bare number.
    expect(app().textContent).not.toContain("NotificationsOnWithReminder");
    expect(sentence()).not.toContain("3");
  });

  it("tells a MISSING event list (nobody chose) from an EMPTY one (they chose none)", async () => {
    await bootDetail(projectionItem({ notifications: { emailEnabled: true } }));
    const nobodyChose = sentence();

    await bootDetail(projectionItem({ notifications: { emailEnabled: true, events: [] } }));
    const choseNone = sentence();

    expect(nobodyChose).toBe("NotificationsOn(NotificationsAllEvents)");
    expect(choseNone).toBe("NotificationsOn(NotificationsNoEvents)");
    expect(nobodyChose).not.toBe(choseNone);
  });

  it("counts the events when some were chosen", async () => {
    await bootDetail(projectionItem({
      notifications: { emailEnabled: true, events: ["Assigned", "Commented"] }
    }));
    expect(sentence()).toBe("NotificationsOn(NotificationsSomeEvents(2))");
  });

  it("folds the reminder into the SAME sentence, on one row", async () => {
    await bootDetail(projectionItem({
      notifications: { emailEnabled: true, events: ["Assigned"] },
      reminderLeadDays: 3
    }));
    expect(sentence()).toBe("NotificationsOnWithReminder(NotificationsSomeEvents(1)|3)");
    // One row, one value node — a second one is the two-row shape this decision rejected.
    const labels = [...app().querySelectorAll(".backbone-preview-label")]
      .filter((node) => node.textContent === "DetailNotifications");
    expect(labels.length).toBe(1);
  });

  it("says nothing about a reminder that was never set", async () => {
    await bootDetail(projectionItem({ notifications: { emailEnabled: true, events: ["Assigned"] } }));
    expect(sentence()).toBe("NotificationsOn(NotificationsSomeEvents(1))");
  });
});

describe("the delegation policy never becomes a row", () => {
  it("is not printed as a field of its own", async () => {
    await bootDetail(projectionItem({ delegationAllowed: false }));
    // It is a RULE, enforced on the server and surfaced as a disabled action with a reason. A "Devredilebilir:
    // hayır" row would be a fact the reader can do nothing with, in the card that holds the facts they can.
    expect(app().textContent).not.toContain("DetailDelegation");
    expect(app().textContent).not.toContain("DelegationAllowed");
  });
});
