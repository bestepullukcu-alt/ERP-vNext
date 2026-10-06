const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * DILIM 1e — THE REPORT'S ROWS, AS A FILE, ON THE SCREEN.
 *
 * Platform's WorkReportExportTests owns "every column sums to its number" and "the scope reaches the read". This
 * file owns what only the screen can get wrong:
 *
 *   · the download asks for the SAME report that is on screen — same period, same five filters, same scope —
 *     and not whatever the pickers happen to say now;
 *   · the object URL is released (a blob URL left alive pins the whole file in memory for the page's lifetime);
 *   · a 401/403 and a refused, too-large export each get their own sentence, not a raw error;
 *   · the button and its behaviour arrived together (UI-027), in seven languages.
 *
 * ⚠ PRESENCE FIRST, as every file in this module insists on: each "did not happen" assertion is preceded by
 * proof that the right thing did happen in the same test.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const VIEW = fs.readFileSync(web("Views", "Tasks", "WorkReport.cshtml"), "utf8");
const L10N = fs.readFileSync(web("Views", "Tasks", "_WorkReportL10n.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) =>
  fs.readFileSync(web("Resources", "Views", "Tasks", "WorkReport", `WorkReportIndex.${lang}.resx`), "utf8");

const VIEW_KEYS = ["ExportButton", "ExportCsv", "ExportJson"];
const SCRIPT_KEYS = [
  "ExportFilePrefix", "ExportDownloaded", "ExportTooManyRows", "ExportForbidden", "ExportFailed",
  // BL-347 follow-up (WP-PSS-MOD0024-FOLLOWUPS-02) — its own sentence, same gate as its four siblings.
  "ExportAuditNotRecorded"
];

const LABELS = {
  exportFilePrefix: "is-raporu",
  exportDownloaded: "{0} satır indirildi.",
  exportTooManyRows: "Filtreleri daraltın.",
  exportForbidden: "Yetkiniz yok.",
  exportFailed: "Hazırlanamadı.",
  exportAuditNotRecorded: "Denetim kaydına yazılamadı.",
  loadFailed: "The report could not be loaded.",
  periodInvalid: "The end of the period must come after its start."
};

const MARKUP = `
  <form id="workReportFilter">
    <input type="date" id="wrFrom" value="2026-06-01" /><input type="date" id="wrTo" value="2026-07-01" />
    <select id="wrGroupBy"><option value="None" selected>None</option></select>
    <button type="button" data-wr-export-toggle disabled></button>
    <ul><li><button type="button" data-wr-export="csv">CSV</button></li>
        <li><button type="button" data-wr-export="json">JSON</button></li></ul>
  </form>
  <select id="wrLegalEntity"><option value="">Any</option><option value="le-live">Live</option></select>
  <select id="wrUnit"><option value="">Any</option></select>
  <select id="wrTaskType"><option value="">Any</option></select>
  <select id="wrAssignee"><option value="">Any</option></select>
  <select id="wrPriority"><option value="">Any</option><option value="Low">Low</option></select>
  <p data-wr-status></p>
  <div data-wr-skeleton-tiles hidden></div><div data-wr-skeleton-charts hidden></div>
  <div data-wr-tiles hidden></div><div data-wr-charts hidden></div><div data-wr-summary hidden></div>
  <script id="work-report-l10n" type="application/json">${JSON.stringify(LABELS)}</script>
  <script id="work-report-outcomes-l10n" type="application/json">{}</script>
`;

/** The report a reader loaded: TWO filters and a scope preference, so a dropped one cannot hide. */
const LOADED = {
  from: "2026-06-01", to: "2026-07-01", groupBy: "OrganizationUnit",
  legalEntityId: "le-1", organizationUnitId: "", taskTypeCode: "", assigneeUserId: "", priority: "High",
  scopePreference: "own"
};

let fetchCalls;
let nextResponse;
let toasts;
let created;
let revoked;
let clicked;

const response = ({ status = 200, headers = {}, body = null } = {}) => {
  const lower = Object.fromEntries(Object.entries(headers).map(([k, v]) => [k.toLowerCase(), v]));
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: (name) => (name.toLowerCase() in lower ? lower[name.toLowerCase()] : null) },
    blob: () => Promise.resolve(new Blob(["Id,Title\r\n"])),
    json: () => (body === null ? Promise.reject(new Error("no body")) : Promise.resolve(body))
  };
};

const FILE_OK = () => response({
  headers: {
    "content-disposition": "attachment; filename=work-report_2026-06-01_2026-06-30.csv; filename*=UTF-8''work-report_2026-06-01_2026-06-30.csv",
    "X-Work-Report-Export-Row-Count": "12"
  }
});

const boot = () => {
  fetchCalls = [];
  toasts = [];
  created = [];
  revoked = [];
  clicked = [];
  nextResponse = FILE_OK;
  document.body.innerHTML = MARKUP;
  delete global.WorkReportScreen;

  global.fetch = (url) => {
    fetchCalls.push(url);
    return Promise.resolve(nextResponse());
  };
  window.showToast = (message, type) => toasts.push({ message, type });
  URL.createObjectURL = () => { const u = `blob:wr-${created.length + 1}`; created.push(u); return u; };
  URL.revokeObjectURL = (u) => revoked.push(u);
  HTMLAnchorElement.prototype.click = function () {
    clicked.push({ download: this.download, href: this.href, attached: document.body.contains(this) });
  };

  loadScript("wwwroot/assets/js/Tasks/WorkReport/index.js");
  return window.WorkReportScreen;
};

const params = (url) => {
  const out = {};
  new URL(url, "http://x").searchParams.forEach((v, k) => { out[k] = v; });
  return out;
};

describe("Dilim 1e — the file is the rows of the report ON SCREEN", () => {
  test("the export asks for the same period, the same five filters and the same scope as the loaded report", () => {
    const screen = boot();
    screen.setLastQuery(LOADED);

    const exported = params(screen.exportUrl("csv"));
    const listed = params(screen.itemsUrl("Opened", null, null, 0));

    // PRESENCE: the two filters and the preference actually travel.
    expect(exported.legalEntityId).toBe("le-1");
    expect(exported.priority).toBe("High");
    expect(exported.scope).toBe("own");
    expect(exported.format).toBe("csv");

    /*
     * ⚠ SABOTAGE 2's GUARD on the screen. The list a tile opens and the file share one query — period,
     * filters, scope — so drop a filter from the export URL and this identity breaks. Only what is the
     * list's own (bucket, axis, page) or the file's own (format) may differ.
     */
    const shared = (p) => { const { bucket, groupBy, skip, format, ...rest } = p; return rest; };
    expect(shared(exported)).toEqual(shared(listed));

    // The file is the totals' rows: no axis, so a group cannot silently narrow it.
    expect(exported.groupBy).toBeUndefined();
  });

  test("it reads the report that is on screen, not the pickers a reader changed and has not applied", () => {
    const screen = boot();
    screen.setLastQuery(LOADED);
    document.getElementById("wrLegalEntity").value = "le-live";
    document.getElementById("wrPriority").value = "Low";

    const exported = params(screen.exportUrl("json"));
    expect(exported.legalEntityId).toBe("le-1");
    expect(exported.priority).toBe("High");
  });

  test("nothing on screen, nothing exported", async () => {
    const screen = boot();
    await screen.downloadExport("csv");
    expect(fetchCalls).toEqual([]);
  });
});

describe("Dilim 1e — the download itself (the audit log's pattern)", () => {
  test("downloads under the reader's own file name, says how many rows, and RELEASES the object URL", async () => {
    const screen = boot();
    screen.setLastQuery(LOADED);

    await screen.downloadExport("csv");

    expect(fetchCalls).toHaveLength(1);
    expect(fetchCalls[0]).toMatch(/^\/Tasks\/api\/work-report\/export\?/);

    // PRESENCE: one blob URL, one click on an attached link, under the localized name with the server's period.
    expect(created).toEqual(["blob:wr-1"]);
    expect(clicked).toHaveLength(1);
    expect(clicked[0].download).toBe("is-raporu_2026-06-01_2026-06-30.csv");
    expect(clicked[0].attached).toBe(true);

    // ⚠ SABOTAGE 3's GUARD. A blob URL never revoked keeps the whole file alive until the page closes.
    expect(revoked).toEqual(["blob:wr-1"]);
    // And the link does not stay in the page.
    expect(document.querySelectorAll("a[download]")).toHaveLength(0);

    expect(toasts).toEqual([{ message: "12 satır indirildi.", type: "success" }]);
  });

  test("the object URL is released even when the click throws", async () => {
    const screen = boot();
    screen.setLastQuery(LOADED);
    HTMLAnchorElement.prototype.click = () => { throw new Error("blocked"); };

    await screen.downloadExport("csv");

    expect(created).toEqual(["blob:wr-1"]);
    expect(revoked).toEqual(["blob:wr-1"]);
  });

  test.each([401, 403])("a %i is answered with the permission sentence, and no file is made", async (status) => {
    const screen = boot();
    screen.setLastQuery(LOADED);
    nextResponse = () => response({ status, body: { reason_code: "PERM_DENIED" } });

    await screen.downloadExport("csv");

    expect(fetchCalls).toHaveLength(1);
    expect(toasts).toEqual([{ message: LABELS.exportForbidden, type: "error" }]);
    expect(created).toEqual([]);
  });

  test("a refused, too-large export says NARROW IT — not that it failed", async () => {
    const screen = boot();
    screen.setLastQuery(LOADED);
    nextResponse = () => response({ status: 400, body: { reason_code: "WORK_REPORT_EXPORT_TOO_LARGE", errors: ["…"] } });

    await screen.downloadExport("csv");

    expect(toasts).toEqual([{ message: LABELS.exportTooManyRows, type: "error" }]);
    expect(created).toEqual([]);
  });

  test("any other failure is the plain failure sentence", async () => {
    const screen = boot();
    screen.setLastQuery(LOADED);
    nextResponse = () => response({ status: 503 });

    await screen.downloadExport("json");

    expect(toasts).toEqual([{ message: LABELS.exportFailed, type: "error" }]);
    expect(created).toEqual([]);
  });

  /*
   * BL-347 follow-up (WP-PSS-MOD0024-FOLLOWUPS-02). Before this, a 503 DATA_EXPORT_AUDIT_NOT_RECORDED reached
   * the reader as the same generic "could not be prepared" the test above pins for an UNIDENTIFIED 503 — which
   * is wrong here: the export worked, the audit trail did not, and the reader is told to retry rather than
   * suspect their filters or the export itself.
   */
  test("a 503 whose reason is the audit trail failing to record gets its OWN sentence, not the plain failure one", async () => {
    const screen = boot();
    screen.setLastQuery(LOADED);
    nextResponse = () => response({
      status: 503, body: { reason_code: "DATA_EXPORT_AUDIT_NOT_RECORDED", errors: ["…"] }
    });

    await screen.downloadExport("csv");

    expect(toasts).toEqual([{ message: LABELS.exportAuditNotRecorded, type: "error" }]);
    expect(created).toEqual([]);
  });

  // ⚠ SABOTAGE GUARD — a 503 with a DIFFERENT (or absent) reason must still fall through to the plain failure
  // sentence; only the specific code above earns its own message.
  test("a 503 with a different or absent reason still gets the plain failure sentence", async () => {
    const screen = boot();
    screen.setLastQuery(LOADED);
    nextResponse = () => response({ status: 503, body: { reason_code: "SOMETHING_ELSE" } });

    await screen.downloadExport("csv");

    expect(toasts).toEqual([{ message: LABELS.exportFailed, type: "error" }]);
  });

  test("a server name that does not carry the stable prefix is left exactly as sent", () => {
    const screen = boot();
    expect(screen.localFileName("something-else.csv", "csv")).toBe("something-else.csv");
    expect(screen.localFileName("", "json")).toBe("is-raporu.json");
  });
});

describe("Dilim 1e — button and behaviour arrive together (UI-027)", () => {
  test("clicking a format entry downloads that format", async () => {
    const screen = boot();
    screen.setLastQuery(LOADED);

    document.querySelector('[data-wr-export="json"]').click();
    await new Promise((r) => setTimeout(r, 0));

    expect(fetchCalls).toHaveLength(1);
    expect(params(fetchCalls[0]).format).toBe("json");
  });

  test("the toggle is disabled until a report has loaded, and again when a load fails", async () => {
    const screen = boot();
    const toggle = document.querySelector("[data-wr-export-toggle]");
    expect(toggle.disabled).toBe(true);

    nextResponse = () => ({ ok: true, status: 200, json: () => Promise.resolve({ data: null }) });
    await screen.load();
    expect(toggle.disabled).toBe(false);

    nextResponse = () => response({ status: 500 });
    await screen.load();
    expect(toggle.disabled).toBe(true);
  });

  test("the view ships the toggle, disabled, beside the filter badge, with exactly the two formats", () => {
    const toggle = VIEW.indexOf("data-wr-export-toggle");
    const badge = VIEW.indexOf("data-wr-filter-count");
    const apply = VIEW.indexOf('type="submit"');

    expect(toggle).toBeGreaterThan(0);
    // The same toolbar: after the filter toggle and its badge, before Apply.
    expect(badge).toBeGreaterThan(0);
    expect(toggle).toBeGreaterThan(badge);
    expect(toggle).toBeLessThan(apply);
    expect(VIEW).toMatch(/data-bs-toggle="dropdown"[^>]*\bdisabled\b[^>]*data-wr-export-toggle/);

    const formats = [...VIEW.matchAll(/data-wr-export="([a-z]+)"/g)].map((m) => m[1]);
    // Exactly the audit export's two — an XLSX entry here would fail this, by value.
    expect(formats).toEqual(["csv", "json"]);
  });
});

describe("Dilim 1e — seven languages", () => {
  test.each(LANGS)("%s carries every export string, non-empty", (lang) => {
    const file = resx(lang);
    for (const key of [...VIEW_KEYS, ...SCRIPT_KEYS]) {
      const match = new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]+)</value></data>`).exec(file);
      // ⚠ SABOTAGE 4's GUARD — a language missing one key fails here, by name.
      expect(match, `${lang} is missing ${key}`).not.toBeNull();
      expect(match[1].trim()).not.toBe("");
    }
  });

  test("the script's strings travel through the bridge and the view's are rendered by the view", () => {
    for (const key of SCRIPT_KEYS) { expect(L10N).toContain(`${key} = Localizer["${key}"].Value`); }
    for (const key of VIEW_KEYS) { expect(VIEW).toContain(`Localizer["${key}"]`); }
  });

  test("the download counter keeps its placeholder in every language", () => {
    for (const lang of LANGS) {
      expect(/<data name="ExportDownloaded"[^>]*><value>[^<]*\{0\}[^<]*<\/value>/.test(resx(lang)), lang).toBe(true);
    }
  });

  test("the file-name prefix is safe to put in a file name in every language", () => {
    for (const lang of LANGS) {
      const value = /<data name="ExportFilePrefix"[^>]*><value>([^<]+)<\/value>/.exec(resx(lang))[1];
      expect(value, lang).not.toMatch(/[\\/:*?"<>|\s]/);
    }
  });
});
