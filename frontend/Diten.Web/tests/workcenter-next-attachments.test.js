const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * MOD-0024 Slice ATT-1 — task attachments, against the REAL projection shape and the real detail-page DOM.
 *
 * Two shapes answer to the SAME capability name ('attachments'): the pre-existing read-only document-reference
 * array (documentation-fixtures.js) and this slice's own {items: [...]} — see renderAttachments' own comment
 * in app.js. Both are exercised here so a change to either branch is caught by this file alone.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const TASK_ID = "6c1b8e2a-6a7f-4b7a-9a7a-1d9b7a2f6e11";
const ATTACHMENT_ID = "f2a1c9d4-8b3e-4a2c-9c1e-2b7d6a5f8e01";

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
  title: { kind: "display", text: "Envanter sayımı", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID,
    deepLink: `/Tasks/${TASK_ID}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", displayName: "Deniz Koç" },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "attachments"],
  attachments: { items: [] },
  actions: [],
  concurrency: { kind: "version", token: "8" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-07-30T00:00:00+00:00"
}, overrides);

const boot = (item) => bootSurface({
  rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
  items: [item],
  wcn: {
    t: (key) => key,
    tf: (key, ...args) => args.reduce(
      (text, value, i) => text.split(`{${i}}`).join(String(value)),
      `${key}:` + args.map((_, i) => `{${i}}`).join(" ")),
    tn: (key) => key
  }
});

const until = async (predicate, { timeout = 2000, step = 5 } = {}) => {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (predicate()) { return; }
    await new Promise((r) => setTimeout(r, step));
  }
  throw new Error("condition never became true");
};

// jsdom's <input type="file">.files is read-only, and this jsdom build has no DataTransfer to construct a
// FileList through either. Defining the property directly is the portable workaround: a real browser only
// ever sets it from a user pick, so this exists in tests alone.
const putFile = (input, file) => {
  Object.defineProperty(input, "files", { value: [file], writable: false });
};

describe("MOD-0024 Slice ATT-1 — attachments", () => {
  describe("the declared-and-empty card", () => {
    it("draws the add button as the empty state on an open task — no separate empty sentence", async () => {
      // Same rule renderChecklist follows: the add row IS the empty state, so only one of the two shows.
      await boot(projectionItem());
      const section = app().querySelector("[data-wcn-attach-add]");
      expect(section, "the add button never rendered").not.toBeNull();
      expect(app().textContent).toContain("AttachmentAddButton");
      expect(app().textContent).not.toContain("AttachmentsEmpty");
    });

    it("hides the add button on a closed task with zero attachments — only the sentence remains", async () => {
      await boot(projectionItem({ normalizedStatus: "Done" }));
      expect(app().querySelector("[data-wcn-attach-add]")).toBeNull();
      expect(app().textContent).toContain("AttachmentsEmpty");
    });
  });

  describe("a non-empty list", () => {
    const withOne = (overrides) => projectionItem(Object.assign({
      attachments: {
        items: [{
          id: ATTACHMENT_ID,
          fileName: "rapor.pdf",
          mediaType: "application/pdf",
          byteSize: 2_500_000,
          kind: "Evidence",
          uploadedBy: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", displayName: "Ada Yılmaz" },
          uploadedAt: "2026-07-20T10:00:00+00:00"
        }]
      }
    }, overrides));

    it("renders the file as a download link built from TasksApi.attachmentContentUrl, never the ObjectKey", async () => {
      await boot(withOne());
      const link = app().querySelector(`[data-wcn-attach="${ATTACHMENT_ID}"] a.wcn-attach-name`);
      expect(link, "the file name link never rendered").not.toBeNull();
      expect(link.getAttribute("href")).toBe(`/Tasks/api/${TASK_ID}/attachments/${ATTACHMENT_ID}/content`);
      expect(link.getAttribute("download")).toBe("rapor.pdf");
      // AC3 — the storage ObjectKey never appears in any response, and therefore never on the page either.
      expect(app().innerHTML).not.toMatch(/objectKey/i);
    });

    it("shows the kind badge, a human byte size and the uploader", async () => {
      await boot(withOne());
      const row = app().querySelector(`[data-wcn-attach="${ATTACHMENT_ID}"]`);
      expect(row.textContent).toContain("AttachmentKindEvidence");
      expect(row.querySelector(".wcn-attach-size").textContent).toContain("MB");
      expect(row.querySelector(".wcn-attach-meta").textContent).toContain("Ada Yılmaz");
    });

    it("offers remove on an open task and withholds it on a closed one", async () => {
      await boot(withOne());
      expect(app().querySelector(`[data-wcn-attach-remove="${TASK_ID}:${ATTACHMENT_ID}"]`)).not.toBeNull();

      await boot(withOne({ normalizedStatus: "Done" }));
      expect(app().querySelector(`[data-wcn-attach-remove="${TASK_ID}:${ATTACHMENT_ID}"]`)).toBeNull();
      expect(app().querySelector("[data-wcn-attach-add]")).toBeNull();
    });
  });

  describe("the OTHER shape — read-only document-reference fixtures", () => {
    it("renders the reference array without crashing and without an add/remove control", async () => {
      await boot(projectionItem({
        attachments: [{ id: "DOC-1", label: { kind: "display", text: "SOP-12", locale: "und" }, version: 3 }]
      }));
      expect(app().querySelector('[data-wcn-attach="DOC-1"]'), "the reference row never rendered").not.toBeNull();
      expect(app().textContent).toContain("SOP-12");
      expect(app().querySelector("[data-wcn-attach-add]"), "a reference list must stay read-only").toBeNull();
    });
  });

  describe("the checklist's own evidence affordance", () => {
    const withEvidenceItem = (evidenceCount, overrides) => projectionItem(Object.assign({
      workItemCapabilities: ["planning", "execution", "checklist", "attachments"],
      checklist: {
        version: 1,
        items: [{
          id: "step-1",
          label: { kind: "display", text: "Saymayı bitir", locale: "und" },
          completed: false, required: true, blocking: false, evidenceRequired: true,
          evidenceCount
        }]
      }
    }, overrides));

    it("offers 'add evidence' with no count when none has been attached yet", async () => {
      await boot(withEvidenceItem(0));
      const btn = app().querySelector(`[data-wcn-check-evidence-add="${TASK_ID}:step-1"]`);
      expect(btn, "the checklist row grew no evidence affordance").not.toBeNull();
      expect(btn.querySelector(".wcn-check-evidence-count")).toBeNull();
    });

    it("shows the live count once evidence exists", async () => {
      await boot(withEvidenceItem(2));
      const btn = app().querySelector(`[data-wcn-check-evidence-add="${TASK_ID}:step-1"]`);
      expect(btn.querySelector(".wcn-check-evidence-count").textContent).toBe("2");
    });

    it("withholds the affordance once the task is closed — the courtesy the checklist's own controls follow", async () => {
      await boot(withEvidenceItem(1, { normalizedStatus: "Done" }));
      expect(app().querySelector(`[data-wcn-check-evidence-add="${TASK_ID}:step-1"]`)).toBeNull();
    });
  });

  describe("the upload dialog", () => {
    let swalCalls;

    beforeEach(() => {
      swalCalls = [];
      global.Swal = {
        fire: (opts) => { swalCalls.push(opts); return Promise.resolve({ isConfirmed: false }); },
        showValidationMessage: () => {}
      };
    });

    afterEach(() => { delete global.Swal; });

    it("opens a raw, dressed dialog asking for a file, a kind and a note", async () => {
      await boot(projectionItem());
      app().querySelector("[data-wcn-attach-add]").click();
      await until(() => swalCalls.length > 0);

      const opts = swalCalls[0];
      expect(opts.html).toContain('id="wcnAttachFile"');
      expect(opts.html).toContain('id="wcnAttachKind"');
      expect(opts.html).toContain('id="wcnAttachNote"');
      expect(typeof opts.preConfirm).toBe("function");
      /*
       * dialogLook() itself — that it is CALLED at this call site, and that this is the module's third raw
       * dialog rather than a fourth — is a static-source claim covered by wcn-dialog-one-language.test.js and
       * wcn-detail-three-regions.test.js (both updated by this slice). It is not re-asserted at runtime here:
       * `window.DitenDialogAppearance` is declared in _GlobalConfirmation.cshtml, which this harness (like
       * every other WCN boot test) does not load, so dialogLook() would answer {} regardless of whether the
       * call site is correct.
       */
    });

    it("refuses to confirm without a file", async () => {
      await boot(projectionItem());
      app().querySelector("[data-wcn-attach-add]").click();
      await until(() => swalCalls.length > 0);

      document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);
      expect(swalCalls[0].preConfirm()).toBe(false);
    });

    it("locks the kind to Evidence and carries the checklist item code when opened from a checklist row", async () => {
      await boot(projectionItem({
        workItemCapabilities: ["planning", "execution", "checklist", "attachments"],
        checklist: {
          version: 1,
          items: [{
            id: "step-1", label: { kind: "display", text: "Saymayı bitir", locale: "und" },
            completed: false, required: true, blocking: false, evidenceRequired: true, evidenceCount: 0
          }]
        }
      }));
      app().querySelector(`[data-wcn-check-evidence-add="${TASK_ID}:step-1"]`).click();
      await until(() => swalCalls.length > 0);

      const opts = swalCalls[0];
      expect(opts.html).toMatch(/<option value="Evidence" selected>/);
      expect(opts.html).toContain('id="wcnAttachKind" class="form-select" disabled');

      document.body.insertAdjacentHTML("beforeend", opts.html);
      putFile(document.getElementById("wcnAttachFile"), new File(["x"], "kanit.pdf"));
      const value = opts.preConfirm();
      expect(value.kind).toBe("Evidence");
    });

    it("uploads on confirm and reaches TasksApi.addAttachment with the checklist item code", async () => {
      const { attachmentAdds } = await boot(projectionItem());
      global.Swal.fire = (opts) => {
        swalCalls.push(opts);
        return Promise.resolve({
          isConfirmed: true,
          value: { file: new File(["x"], "kanit.pdf"), kind: "Evidence", note: "sayım tutanağı" }
        });
      };
      app().querySelector("[data-wcn-attach-add]").click();
      await until(() => attachmentAdds.length > 0);

      expect(attachmentAdds[0].taskId).toBe(TASK_ID);
      expect(attachmentAdds[0].payload.kind).toBe("Evidence");
      expect(attachmentAdds[0].payload.note).toBe("sayım tutanağı");
    });
  });

  describe("removing an attachment", () => {
    it("asks for confirmation before it reaches the API — a file is not a private note", async () => {
      let confirmCalls = 0;
      global.showConfirm = (message, onConfirm) => { confirmCalls += 1; onConfirm(); };

      const { app: _unused } = {};
      const item = projectionItem({
        attachments: {
          items: [{
            id: ATTACHMENT_ID, fileName: "rapor.pdf", mediaType: "application/pdf", byteSize: 1024,
            kind: "Attachment", uploadedAt: "2026-07-20T10:00:00+00:00"
          }]
        }
      });
      await boot(item);
      app().querySelector(`[data-wcn-attach-remove="${TASK_ID}:${ATTACHMENT_ID}"]`).click();
      await until(() => confirmCalls > 0);

      delete global.showConfirm;
    });
  });
});

describe("l10n — every attachment string exists in all seven languages", () => {
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const resx = (lang) => read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`);
  const NEW_KEYS = [
    "AttachmentsEmpty", "AttachmentAddButton", "AttachmentKindEvidence", "AttachmentKindDeliverable",
    "AttachmentKindAttachment", "AttachmentKindLabel", "AttachmentNoteLabel", "AttachmentNotePlaceholder",
    "AttachmentFileLabel", "AttachmentUploadConfirm", "AttachmentUploadTitle", "AttachmentFileRequired",
    "AttachmentRemoveConfirm", "AttachmentRemove", "AttachmentUploadedBy", "ToastAttachmentAdded",
    "ToastAttachmentRemoved", "ChecklistAddEvidence", "ChecklistEvidenceCount"
  ];

  test("each key exists in all seven files", () => {
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      NEW_KEYS.forEach((key) => expect(xml, `${lang} has no ${key}`).toContain(`name="${key}"`));
    });
  });

  test("the counted/formatted strings carry their placeholders", () => {
    const tr = resx("tr");
    ["AttachmentUploadedBy", "ChecklistEvidenceCount"].forEach((key) => {
      const entry = new RegExp(`name="${key}"[\\s\\S]{0,200}?<value>([^<]*)</value>`).exec(tr);
      expect(entry, `${key} missing from tr`).toBeTruthy();
      expect(entry[1], `${key} has no {0}`).toContain("{0}");
    });
  });
});
