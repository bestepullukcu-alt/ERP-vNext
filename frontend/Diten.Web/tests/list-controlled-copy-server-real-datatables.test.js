const vm = require("vm");
const { read, until, razor, bridgeL10n, bootBrowser, renderPdf } = require("./controlled-copy-harness");

/*
 * BL-452 package 2 — THE CONTROLLED COPY on the SERVER-MODE reference (Golden Compact), through the vendored
 * DataTables + Buttons + pdfmake, the real toolbar, the real factory and the real page. Only the network is fake.
 *
 * The owner's decision (2026-09-24): the exported file is the screen, and a quality record's printout says where it
 * came from. On a server-mode list DataTables holds ONE page (10 rows here); PDF and print must carry EVERY matching
 * row (37) — the same CSV the service writes for the CSV button, asked with the list as the reader sees it — and a
 * header block (screen, tenant, filters, search, sort, rows, creator, date) and an uncontrolled-copy footer.
 */
const TR_WORDS = { Status: "Durum", Active: "Aktif", Passive: "Pasif", ReferenceType: "Referans Türü", Category: "Kategori", Owner: "Sahip", Priority: "Öncelik", ShowAll: "Tümünü Göster", Apply: "Uygula", Reset: "Sıfırla" };
const HEADERS = ["", "", "Kod", "Ad", "Referans Türü", "Kategori", "Sahip", "Sürüm", "Öncelik", "Durum", "İşlemler"];
const LABEL_OF = { code: "Kod", name: "Ad", referenceType: "Referans Türü", category: "Kategori", owner: "Sahip", version: "Sürüm", priority: "Öncelik", isActive: "Durum" };

// 37 rows as the service holds them; row 1 and 2 carry what RFC 4180 exists for.
const ROWS = Array.from({ length: 37 }, (_, i) => ({
  code: `GR-${String(i + 1).padStart(3, "0")}`,
  name: i === 0 ? 'Alpha, "the first"' : i === 1 ? "Alpha\nsecond line" : `Alpha ${i + 1}`,
  referenceType: "Standart", category: "Kalite", owner: "Ayşe", version: "1.0", priority: String(i % 5), isActive: "Pasif"
}));
const csvCell = (v) => (/[",\r\n]/.test(v) ? `"${String(v).replace(/"/g, '""')}"` : String(v));
// What the service writes (package 1): UTF-8 BOM, `,`, RFC 4180 quoting, CRLF — for exactly the requested columns.
const serviceCsv = (keys) => "﻿" + [keys.map((k) => LABEL_OF[k]), ...ROWS.map((r) => keys.map((k) => r[k]))]
  .map((row) => row.map(csvCell).join(",")).join("\r\n") + "\r\n";

describe("controlled copy — server-mode list (Golden Compact): PDF and print carry every row from the service's CSV", () => {
  let b;
  let exportStatus = 200;
  let exportContentType = null; // CT: what the fake export answers as Content-Type (null = header absent)
  const list = () => b.ctx.CompactHandle;
  const button = (format) => list().dt.button(`.dt-controlled-copy[data-export-format='${format}']`);
  const exportFetches = () => b.fetches.filter((f) => f.url.startsWith("http://gw/api/golden-reference-compact/export"));

  beforeAll(async () => {
    document.title = "Altın Compact - Di10";
    document.body.innerHTML =
      '<a class="app-brand-link tenant-brand-link" data-tenant-name="Diten Pharma A.Ş."></a>'
      + razor(read("Views", "DevEnablement", "GoldenReferenceCompact", "_Filter.cshtml"), TR_WORDS)
      + '<div class="card"><div class="card-datatable"><table id="dt-goldenreferencecompact" data-dt-standard="v2" data-dt-data-mode="server" class="datatables-goldenreferencecompact table border-top">'
      + `<thead><tr>${HEADERS.map((h) => `<th>${h}</th>`).join("")}</tr></thead></table></div></div>`;

    b = bootBrowser({
      network: {
        // The list: one page of 10, of 37.
        list: () => ({ success: true, data: { items: ROWS.slice(0, 10).map((r, i) => ({ id: `g${i}`, ...r, isActive: false })), total: 37, filteredTotal: 37 } }),
        fetch: async (url) => {
          if (url.startsWith("http://gw/api/golden-reference-compact/export")) {
            const keys = (new URL(url).searchParams.get("columns") || "").split(",");
            return {
              ok: exportStatus >= 200 && exportStatus < 300, status: exportStatus,
              headers: { get: (name) => (String(name).toLowerCase() === "content-type" ? exportContentType : null) },
              text: async () => serviceCsv(keys),
              blob: async () => new window.Blob([serviceCsv(keys)])
            };
          }
          return { ok: true, status: 200, json: async () => ({ referenceTypes: [], categories: [], owners: [], priorities: [] }) };
        }
      }
    });

    window.API = { deven: "http://gw" };
    window.CurrentLanguage = "tr";
    window.CurrentUser = { tenantId: "t-1", email: "ayse@diten.test" };
    window.L10n = Object.assign(bridgeL10n("tr"), { AddNew: "Yeni", Active: "Aktif", Passive: "Pasif", Actions: "İşlemler" });
    window.Permissions = { has: () => true };
    window.personalizationClient = { getViews: async () => [] };

    const createList = window.DitenDataTable.createList;
    window.DitenDataTable.createList = async (options) => { const h = await createList(options); b.ctx.CompactHandle = h; return h; };
    b.run("wwwroot/assets/js/DevEnablement/GoldenReferenceCompact/index.js");
    vm.runInContext("GoldenReferenceCompactList.init()", b.ctx);
    window.DitenDataTable.createList = createList;
    await until(() => b.ctx.CompactHandle && b.listRequests.length >= 1 && list().dt.rows().count() === 10);

    // The reader's screen: Owner hidden, Status = Pasif applied, "alp" searched, sorted by Ad descending.
    const dt = list().dt;
    dt.column(6).visible(false);
    window.jQuery("#filterStatus").val(["Passive"]);
    document.getElementById("btnFilterApply").click();
    dt.search("alp").order([3, "desc"]).draw();
    await until(() => decodeURIComponent(b.listRequests.at(-1)).includes("search=alp"));
  });

  test("(1) PDF asks exportUrl('csv') — no start/length — and the doc-definition carries all 37 rows, the visible columns in order", async () => {
    expect(list().dt.rows().count(), "DataTables holds one page").toBe(10);
    const before = exportFetches().length;
    button("pdf").trigger();
    await until(() => b.pdfs.length === 1);

    expect(exportFetches().length, "exactly one request, to the export endpoint").toBe(before + 1);
    const call = exportFetches().at(-1);
    const url = decodeURIComponent(call.url);
    expect(call.url, "the list's own export request, CSV").toBe(list().exportUrl("csv"));
    expect(url).toBe("http://gw/api/golden-reference-compact/export?format=csv&columns=code,name,referenceType,category,version,priority,isActive&search=alp&orderBy=name&orderDir=desc&status=Passive");
    expect(url, "the page on screen never limits the copy").not.toMatch(/[?&](start|length|draw)=/);
    expect(call.init.credentials).toBe("include");
    expect(call.init.headers["Accept-Language"]).toBe("tr");

    const doc = b.pdfs[0].docDefinition;
    const table = doc.content.find((c) => c.table && c.table.headerRows === 1).table;
    expect(table.body[0].map((c) => c.text), "the visible columns, in screen order, in the reader's language").toEqual(["Kod", "Ad", "Referans Türü", "Kategori", "Sürüm", "Öncelik", "Durum"]);
    expect(table.body.length - 1, "every matching row, not the 10 on screen").toBe(37);
    expect(table.body[1][1], "a quoted comma and a doubled quote survive").toBe('Alpha, "the first"');
    expect(table.body[2][1], "a line break inside a quoted cell survives").toBe("Alpha\nsecond line");
    expect(b.pdfs[0].downloadedAs).toMatch(/^altın-compact-\d{8}-\d{4}\.pdf$/);
  });

  test("(1) the header block says screen, tenant, filters, search, sort, rows, creator and date; the footer says uncontrolled + page x/y", () => {
    const doc = b.pdfs[0].docDefinition;
    const texts = doc.content.map((c) => c.text).filter(Boolean);
    expect(texts).toEqual(["Diten Pharma A.Ş.", "Altın Compact"]);
    const lines = Object.fromEntries(doc.content.find((c) => c.layout === "noBorders").table.body.map(([l, v]) => [l.text, v.text]));
    expect(lines).toMatchObject({
      "Şirket": "Diten Pharma A.Ş.",
      "Filtreler": "Durum: Pasif",
      "Arama": "alp",
      "Sıralama": "Ad (azalan)",
      "Satır sayısı": "37",
      "Oluşturan": "ayse@diten.test"
    });
    expect(lines["Oluşturma zamanı"], "culture-formatted, with the zone").toMatch(/\d{2}\.\d{2}\.\d{4}.*\d{2}:\d{2}.*\(.+\)$/);
    expect(JSON.stringify(doc), "the old English stamp is gone").not.toMatch(/Generated/);

    const footer = doc.footer(2, 5);
    expect(footer.columns[0].text).toBe(`Bu çıktı kontrolsüz kopyadır — ${lines["Oluşturma zamanı"]}`);
    expect(footer.columns[1].text).toBe("Sayfa 2 / 5");
  });

  test("(1) the vendored pdfmake renders that doc-definition into a real PDF", async () => {
    const pdf = await renderPdf(b.pdfs[0].docDefinition);
    expect(pdf.subarray(0, 5).toString("latin1")).toBe("%PDF-");
    expect(pdf.length).toBeGreaterThan(2000);
    // 37 rows + the block on landscape A4 do not fit one page: the footer callback ran per page.
    expect(pdf.toString("latin1")).toMatch(/\/Type \/Pages[\s\S]*?\/Count [2-9]/);
  }, 20000);

  test("(2) 413 EXPORT_TOO_LARGE → the reader is told, no PDF and no print is produced", async () => {
    exportStatus = 413;
    const pdfs = b.pdfs.length;
    const windows = b.opened.length;
    button("pdf").trigger();
    await until(() => b.toasts.some((t) => t[0] === "ExportTooLarge"));
    button("print").trigger();
    await until(() => b.toasts.filter((t) => t[0] === "ExportTooLarge").length === 2);
    expect(b.toasts.at(-1)).toEqual(["ExportTooLarge", "warning"]);
    expect(b.pdfs.length, "no doc").toBe(pdfs);
    expect(b.opened.length, "the print window was opened inside the click…").toBe(windows + 1);
    expect(b.opened.at(-1).document.querySelector(".print-shell"), "…nothing was written into it…").toBeNull();
    expect(b.opened.at(-1).__closed, "…and it was closed again").toBe(true);
    exportStatus = 200;
  });

  test("(CT) a 200 that is an HTML page (a login redirect the fetch followed) is not a file: no PDF, no print, the reader is told", async () => {
    exportContentType = "text/html; charset=utf-8";
    const pdfs = b.pdfs.length;
    const windows = b.opened.length;
    const errors = b.toasts.filter((t) => t[0] === "ErrorOccurred").length;
    button("pdf").trigger();
    await until(() => b.toasts.filter((t) => t[0] === "ErrorOccurred").length === errors + 1);
    button("print").trigger();
    await until(() => b.toasts.filter((t) => t[0] === "ErrorOccurred").length === errors + 2);
    expect(b.pdfs.length, "no doc from an HTML body").toBe(pdfs);
    expect(b.opened.length).toBe(windows + 1);
    expect(b.opened.at(-1).document.querySelector(".print-shell"), "nothing written into the print window").toBeNull();
    expect(b.opened.at(-1).__closed).toBe(true);
    exportContentType = null;
  });

  test("(4) print: the window's DOM carries the same block, the same 37 rows, the footer and the @page counter — no 'Generated'", async () => {
    const windows = b.opened.length;
    button("print").trigger();
    await until(() => b.opened.length === windows + 1 && b.opened.at(-1).document.querySelector(".print-shell"));
    const doc = b.opened.at(-1).document;
    const pdfLines = b.pdfs[0].docDefinition.content.find((c) => c.layout === "noBorders").table.body.map(([l]) => l.text);

    expect(doc.title).toBe("Altın Compact");
    expect(doc.querySelector(".print-kicker").textContent).toBe("Diten Pharma A.Ş.");
    expect([...doc.querySelectorAll(".print-meta dt")].map((n) => n.textContent), "the same lines as the PDF").toEqual(pdfLines);
    const value = (key) => doc.querySelector(`.print-meta dd[data-line="${key}"]`).textContent;
    expect(value("filters")).toBe("Durum: Pasif");
    expect(value("search")).toBe("alp");
    expect(value("sort")).toBe("Ad (azalan)");
    expect(value("rows")).toBe("37");
    expect(value("createdBy")).toBe("ayse@diten.test");
    expect([...doc.querySelectorAll(".print-table thead th")].map((n) => n.textContent)).toEqual(["Kod", "Ad", "Referans Türü", "Kategori", "Sürüm", "Öncelik", "Durum"]);
    expect(doc.querySelectorAll(".print-table tbody tr").length).toBe(37);
    expect(doc.querySelector(".print-footer").textContent).toMatch(/^Bu çıktı kontrolsüz kopyadır — /);
    const css = doc.querySelector("style").textContent;
    expect(css).toContain('@bottom-right { content: "Sayfa " counter(page) " / " counter(pages);');
    expect(css).toContain('@bottom-left { content: "Bu çıktı kontrolsüz kopyadır — ');
    expect(doc.body.textContent).not.toMatch(/Generated/);
    expect(doc.querySelector("[style]"), "FG-003: no inline style in the print DOM").toBeNull();
  });

  test("(5) CurrentLanguage = 'ar' → the PDF entry opens the print path (the vendored Roboto has no Arabic glyph)", async () => {
    window.CurrentLanguage = "ar";
    try {
      const pdfs = b.pdfs.length;
      const windows = b.opened.length;
      button("pdf").trigger();
      await until(() => b.opened.length === windows + 1 && b.opened.at(-1).document.querySelector(".print-shell"));
      expect(b.pdfs.length, "pdfmake is not asked").toBe(pdfs);
      expect(b.opened.at(-1).__printed, "the browser's print (save as PDF) is opened").toBe(true);
      expect(window.DtDefaults.controlledCopy.last).toMatchObject({ requested: "pdf", output: "print" });
      expect(b.opened.at(-1).document.querySelectorAll(".print-table tbody tr").length, "still every row").toBe(37);
      ["zh", "zh-CN", "ar"].forEach((l) => expect(window.DtDefaults.controlledCopy.pdfNeedsBrowserPrint(l)).toBe(true));
      ["en", "tr", "fr", "es", "ru"].forEach((l) => expect(window.DtDefaults.controlledCopy.pdfNeedsBrowserPrint(l)).toBe(false));
    } finally {
      window.CurrentLanguage = "tr";
    }
  });
});

describe("(6) the CSV parser reads what the service writes (RFC 4180)", () => {
  const parse = (t) => window.DitenDataTable.parseCsv(t);

  test("BOM dropped, quoted comma, doubled quote, CRLF and LF, a line break inside quotes, no phantom last row", () => {
    expect(parse("﻿Kod,Ad\r\nA-1,\"x, y\"\r\nA-2,\"say \"\"hi\"\"\"\nA-3,\"one\r\ntwo\"\r\n")).toEqual([
      ["Kod", "Ad"], ["A-1", "x, y"], ["A-2", 'say "hi"'], ["A-3", "one\r\ntwo"]
    ]);
  });

  test("empty cells stay cells; a file with no trailing line break keeps its last row", () => {
    expect(parse("a,,c\n,,\nlast,row,here")).toEqual([["a", "", "c"], ["", "", ""], ["last", "row", "here"]]);
    expect(parse("")).toEqual([]);
    expect(parse("﻿")).toEqual([]);
  });

  test("the CSV-injection prefix the service adds is data, kept as written", () => {
    expect(parse("'=SUM(A1)\r\n")).toEqual([["'=SUM(A1)"]]);
  });
});
