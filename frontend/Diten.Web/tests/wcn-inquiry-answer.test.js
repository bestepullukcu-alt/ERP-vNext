const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * BL-439 — "Bilgi bekle", the half the person being asked sees, and the half the asker sees when it comes back.
 *
 * THE DEFECT (owner, 2026-09-23): "soru sorduğumda bu, sorduğum kişide görünmüyor." The question was stored and
 * shown on the ASKER's own card; nothing reached the addressee. The provider now emits the question as its own
 * `inquiry` item in the addressee's inbox, with one action — `answer`.
 *
 * The items below are the REAL provider's output (tests/fixtures/task-inquiry-provider-projection.json, written by
 * TaskInquiryProviderContractGoldenTests), never a hand-built lookalike: a card that renders a shape nobody sends
 * proves nothing about the one they do.
 */
const MATRIX = JSON.parse(fs.readFileSync(
  path.resolve(__dirname, "fixtures", "task-inquiry-provider-projection.json"), "utf8"));
const QUESTION = MATRIX.asked_read.summary.text;

/* Echo the key AND its arguments, so a sentence that dropped a fact fails here rather than passing on the key. */
const KEY_AND_ARGS = {
  t: (key) => key,
  tf: (key, ...args) => `${key}(${args.join(",")})`,
  tn: (key, args) => `${key}(${Object.values(args || {}).join(",")})`
};

const tick = () => new Promise((resolve) => { setTimeout(resolve, 0); });

let swalCalls;
let dispatched;
let validation;

const wireWrites = (confirmWith) => {
  dispatched = null;
  global.WorkCenterNextApi.dispatchAction = async (itemId, actionCode, providerCode, body) => {
    dispatched = { itemId, actionCode, providerCode, body };
    return { ok: true, status: 204 };
  };
  global.TasksApi.assignablePeople = async () => ({ ok: true, status: 200, data: [] });
  swalCalls = [];
  validation = null;
  global.Swal = {
    fire: (opts) => {
      swalCalls.push(opts);
      return Promise.resolve(confirmWith ? { isConfirmed: true, value: confirmWith } : { isConfirmed: false });
    },
    showValidationMessage: (message) => { validation = message; }
  };
};

afterEach(() => { delete global.Swal; });

describe("the addressee's inbox holds the question", () => {
  it("draws it as a Soru row: framed title, the question as its body, who asked, and one Cevapla button", async () => {
    await bootSurface({ items: [MATRIX.asked_read], wcn: KEY_AND_ARGS });

    const row = app().querySelector(`[data-wcn-row="${MATRIX.asked_read.id}"]`);
    expect(row, "the question never reached the inbox").toBeTruthy();
    expect(row.querySelector(".wcn-row-title").textContent).toBe("WorkAggregation_Title_Inquiry(Lot 42 serbest bırakma)");
    expect(row.querySelector(".wcn-row-summary").textContent).toBe(QUESTION);
    const chipTexts = [...row.querySelectorAll(".wcn-chip")].map((chip) => chip.textContent.trim());
    expect(chipTexts).toContain("TypeInquiry");
    expect(chipTexts).toContain("Ali Tufanoğlu");

    const buttons = [...row.querySelectorAll("[data-wcn-action]")].map((button) => button.getAttribute("data-wcn-action"));
    expect(buttons).toEqual(["answer"]);
  });

  it("without the read key the button stays on the row, disabled, with its reason", async () => {
    await bootSurface({ items: [MATRIX.asked_none], wcn: KEY_AND_ARGS });

    const button = app().querySelector('[data-wcn-action="answer"]');
    expect(button).toBeTruthy();
    expect(button.disabled).toBe(true);
  });
});

describe("the Cevapla dialog", () => {
  it("quotes the question, asks for an answer (not a reason) and caps it where the server does", async () => {
    await bootSurface({ items: [MATRIX.asked_read], wcn: KEY_AND_ARGS });
    wireWrites(null);

    app().querySelector('[data-wcn-action="answer"]').click();
    await tick();

    expect(swalCalls).toHaveLength(1);
    const html = swalCalls[0].html;
    expect(html).toContain("InquiryQuestionLabel");
    expect(html).toContain(QUESTION);
    expect(html).toContain("InquiryAnswerLabel");
    expect(html).toContain('placeholder="InquiryAnswerPlaceholder"');
    expect(html).toContain('maxlength="4000"');
    expect(html).not.toContain(">ReasonLabel<");
    expect(html).toContain("OutcomeAnswer");
  });

  /* MUTATION TARGET (metin zorunlu). An empty answer never leaves the dialog. */
  it("refuses to confirm an empty answer, with the answer's own sentence", async () => {
    await bootSurface({ items: [MATRIX.asked_read], wcn: KEY_AND_ARGS });
    wireWrites(null);

    app().querySelector('[data-wcn-action="answer"]').click();
    await tick();
    document.body.insertAdjacentHTML("beforeend", swalCalls[0].html);
    document.getElementById("wcnReasonText").value = "   ";

    expect(swalCalls[0].preConfirm()).toBe(false);
    expect(validation).toBe("InquiryAnswerRequired");
    document.getElementById("wcnReasonText").remove();
  });

  it("sends the answer as `answer` — never as `reason` — with the item's version, to the one dispatch address", async () => {
    await bootSurface({ items: [MATRIX.asked_read], wcn: KEY_AND_ARGS });
    wireWrites({ reason: "Cuma günü tedarikçiden." });

    app().querySelector('[data-wcn-action="answer"]').click();
    await tick();
    await tick();

    expect(dispatched).not.toBeNull();
    expect(dispatched.itemId).toBe(MATRIX.asked_read.id);
    expect(dispatched.actionCode).toBe("answer");
    expect(dispatched.providerCode).toBe("tasks");
    expect(dispatched.body).toEqual({ expectedVersion: 4, answer: "Cuma günü tedarikçiden." });
  });
});

describe("parking the task never offers the holder as the person being waited on", () => {
  it("drops the holder from the 'waiting on' picker (the server refuses waiting on yourself)", async () => {
    const holderTask = JSON.parse(JSON.stringify(MATRIX.holder_answered));
    await bootSurface({ items: [holderTask], wcn: KEY_AND_ARGS });
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();
    wireWrites(null);
    const holderId = holderTask.assignee.id;
    global.TasksApi.assignablePeople = async () => ({
      ok: true, status: 200,
      data: [{ userId: holderId, displayName: "Ali Tufanoğlu" }, { userId: "43943943-0000-4000-8000-0000000000c2", displayName: "Ayşe Yılmaz" }]
    });

    const inquire = app().querySelector('[data-wcn-action="inquire"]');
    expect(inquire, "the holder's row offers no inquire action").toBeTruthy();
    inquire.click();
    await tick();
    await tick();

    expect(swalCalls).toHaveLength(1);
    expect(swalCalls[0].html).toContain("Ayşe Yılmaz");
    expect(swalCalls[0].html).not.toContain(`value="${holderId}"`);
  });

  it("the question card claims no role — the task stays the holder's", async () => {
    await bootSurface({ items: [MATRIX.asked_read], wcn: KEY_AND_ARGS });
    const mapped = global.WorkCenterNextApi.mapPayload([MATRIX.asked_read]).items[0];
    expect(mapped.viewerRole || null).toBeNull();
  });
});

describe("the asker sees who answered", () => {
  const bootMine = async () => {
    await bootSurface({ items: [MATRIX.holder_answered], wcn: KEY_AND_ARGS });
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();
  };

  /* MUTATION TARGET ("X cevapladı" görünür). */
  it("draws 'X cevapladı' on the row, with the answer itself as its tooltip", async () => {
    await bootMine();

    const row = app().querySelector(`[data-wcn-row="${MATRIX.holder_answered.id}"]`);
    expect(row, "the holder's task is not on İşlerim").toBeTruthy();
    const chip = [...row.querySelectorAll(".wcn-chip")].find((c) => /InquiryAnsweredBy/.test(c.textContent));
    expect(chip, "no 'answered' chip on the asker's row").toBeTruthy();
    expect(chip.textContent.trim()).toBe("InquiryAnsweredBy(Ayşe Yılmaz)");
    expect(chip.getAttribute("title")).toBe("Cuma günü tedarikçiden.");
  });

  it("says it in one sentence on the detail page too", async () => {
    await bootSurface({
      rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${MATRIX.holder_answered.id}"`,
      items: [MATRIX.holder_answered],
      wcn: KEY_AND_ARGS
    });

    expect(app().innerHTML).toContain("GuidanceInquiryAnswered(Ayşe Yılmaz,Cuma günü tedarikçiden.)");
  });

  it("draws nothing when there is no answer to report", async () => {
    const plain = JSON.parse(JSON.stringify(MATRIX.holder_answered));
    delete plain.inquiryAnswer;
    await bootSurface({ items: [plain], wcn: KEY_AND_ARGS });
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();

    expect(app().innerHTML).not.toContain("InquiryAnsweredBy");
  });
});

describe("the strings exist in all seven languages", () => {
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const web = (...p) => path.resolve(__dirname, "..", ...p);
  const value = (xml, key) => {
    const match = xml.match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
    return match ? match[1] : null;
  };

  it("every key the question and the answer use is non-empty in every WorkCenterNext resx", () => {
    const keys = [
      "TypeInquiry", "WorkAggregation_Title_Inquiry", "WorkAggregation_Action_Answer", "OutcomeAnswer",
      "InquiryQuestionLabel", "InquiryAnswerLabel", "InquiryAnswerPlaceholder", "InquiryAnswerRequired",
      "InquiryAnsweredBy", "GuidanceInquiryAsked", "GuidanceInquiryAskedNoText", "GuidanceInquiryAnswered",
      "AuditEventInquiryAnswered"
    ];
    const missing = [];
    LANGS.forEach((lang) => {
      const xml = fs.readFileSync(web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
      keys.forEach((key) => { if (!String(value(xml, key) || "").trim()) { missing.push(`${lang}/${key}`); } });
    });
    expect(missing).toEqual([]);
  });

  it("the title frame keeps its {title} slot, and the person sentences keep theirs, in every language", () => {
    LANGS.forEach((lang) => {
      const xml = fs.readFileSync(web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
      expect(value(xml, "WorkAggregation_Title_Inquiry"), lang).toContain("{title}");
      expect(value(xml, "InquiryAnsweredBy"), lang).toContain("{0}");
      ["GuidanceInquiryAsked", "GuidanceInquiryAnswered"].forEach((key) => {
        expect(value(xml, key), `${lang}/${key}`).toContain("{0}");
        expect(value(xml, key), `${lang}/${key}`).toContain("{1}");
      });
    });
  });

  it("the three refusals are bridged and translated for the Tasks surface", () => {
    const bridge = fs.readFileSync(web("Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
    ["ErrorInquiryAnswerRequired", "ErrorInquiryAnswerTooLong", "ErrorInquiryNotAddressee"].forEach((key) => {
      expect(bridge).toContain(`${key} = Localizer["${key}"].Value`);
      LANGS.forEach((lang) => {
        const xml = fs.readFileSync(web("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`), "utf8");
        expect(String(value(xml, key) || "").trim(), `${lang}/${key}`).not.toBe("");
      });
    });
    const api = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "api.js"), "utf8");
    expect(api).toContain("TASK_INQUIRY_ANSWER_REQUIRED: 'errorInquiryAnswerRequired'");
    expect(api).toContain("TASK_INQUIRY_ANSWER_TOO_LONG: 'errorInquiryAnswerTooLong'");
    expect(api).toContain("TASK_INQUIRY_NOT_ADDRESSEE: 'errorInquiryNotAddressee'");
  });
});
